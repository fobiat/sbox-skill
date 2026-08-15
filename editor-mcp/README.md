<div align="center">

# s&box MCP Server

**Eighteen MCP tools for the s&box editor, for when you edit a file and nothing happens.**

[![Released](https://img.shields.io/badge/sbox.game-released-f59c1a?style=flat-square)](https://sbox.game/fobiat/sbox_mcp_server/)
[![Engine](https://img.shields.io/badge/s%26box-26.08.05-3e3e3e?style=flat-square)](https://sbox.game)
[![Licence](https://img.shields.io/badge/licence-MIT-3fb950?style=flat-square)](../LICENSE)
[![Tools](https://img.shields.io/badge/tools-18-00a8e8?style=flat-square)](SboxMcpServer.cs)
[![Two files](https://img.shields.io/badge/install-2_files-4c8eda?style=flat-square)](SboxMcpServer.cs)

*A package reference, or two files in `Editor/`. Restart, done.*

</div>

---

## Synopsis

The s&box editor already ships an MCP server with its own toolsets (`editor`, `asset`, `scene`,
`component`, `package`, `play`, `log`). This file adds one more, registered as `sbox_mcp_server`,
with eighteen tools in three groups: ask the running engine what is actually true, ask the editor
what it currently believes, and make it notice a change on disk.

| Tool | Group | What it does |
|---|---|---|
| `project_find_type` | Ask the engine | Looks up a type by name in the loaded engine and reports what it is, so you know whether an API exists before you write it |
| `project_type_members` | Ask the engine | Lists a type's real methods and properties, read straight from the running engine |
| `project_input_actions` | Ask the engine | Lists every input action the project defines, with keyboard and gamepad bindings |
| `project_find_member` | Ask the engine | Searches every loaded type for a member by name fragment, for when you know the method but not what it hangs off |
| `project_enum_values` | Ask the engine | Lists an enum's named values, which `project_type_members` cannot because an enum has no methods or properties |
| `project_console_commands` | Ask the engine | Lists the project's console commands with their real argument list, so a wrong form does not just print usage and change nothing |
| `project_content_path` | Ask the engine | Resolves a content path against the mounted filesystem, before a typo silently loads the error model |
| `project_content_search` | Ask the engine | Lists what the mounted set actually ships under a directory, for when a manifest and the mount disagree |
| `project_info` | Ask the editor | Reports which project is open and the compiler settings currently live in memory |
| `project_compilers` | Ask the editor | Lists each compiler's build state: `IsBuilding`, `NeedsBuild`, `BuildSuccess` |
| `project_source_changes` | Ask the editor | Reports what each compiler has actually noticed since its last build |
| `project_compile_errors` | Ask the editor | Returns compile diagnostics as rows with file and line, instead of console text to scrape |
| `project_assembly_freshness` | Ask the editor | Compares the assembly each compiler emitted against the one the process is actually serving, because recompiling does not cure a stale one |
| `project_package_references` | Ask the editor | Reconciles the `.sbproj` package references against what is installed, since `install_package` writes nothing |
| `project_reload_config` | Change something | Re-reads the `.sbproj` from disk into the live config and recreates the compilers |
| `project_reload_settings` | Change something | Drops the cached `ProjectSettings`, so `Input.config` and friends are re-read from disk |
| `project_rebuild` | Change something | Recreates every compiler and starts a build. Returns immediately |
| `project_build` | Change something | Rebuilds and waits, then reports success plus any errors |

The fourteen "ask" tools carry `McpToolHints.ReadOnly`, so a client can run them without
stopping to ask permission. The four "change something" tools do not, since they touch live
state.

Note that the read-only hint never reaches a client's tool list for a third-party toolset:
listing is gated on an attribute internal to the engine, so these are reached through
`search_tools` and `call_tool` instead. Search `project_` and they come back.

<br>

## Quickstart

**As a package.** Open Project Settings, add `fobiat.sbox_mcp_server` to package references,
restart. Nothing to clone, and the editor's own package flow brings updates. The
[library project](../library) that builds it is in this repo.

**As files**, if you would rather read an unsandboxed `Editor/` assembly before it compiles
into your project. Two files, one `partial class SboxMcpServer`: split at 1000 lines because
that is the largest a single file can be under most projects' own file-size gates, not because
the engine requires it.

```bash
mkdir -p your-game/Editor
cp editor-mcp/SboxMcpServer.cs editor-mcp/SboxMcpServer.Editor.cs your-game/Editor/
```

> Pairing this with the skill? The **[Quickstart](../QUICKSTART.md)** covers the loop the two
> form together, which is where most of the value is.

`Editor/` is a **separately compiled assembly**, not an `#if EDITOR` block in your game code.
Code living there is unsandboxed, which is what lets this reach internal engine API at all.

Restart the editor, then:

```
list_toolsets       → sbox_mcp_server, eighteen tools
describe_toolset    → full input schemas
```

---

## The problem

None of the editor's stock toolsets cover the part that burns an afternoon: getting the editor
to **notice** you changed something on disk.

There are three reasons it might not, and not one of them raises an error.

| What you changed | Why nothing happened |
|---|---|
| `.sbproj` | Read once at editor boot, or written from the Project Settings page. Nothing watches it. Your `Metadata.Compiler` block never reaches Roslyn. |
| `ProjectSettings/*.config` | Cached on first read, never invalidated. Your edited `Input.config` keeps serving the old actions until restart. |
| A `.cs` file | Compiler file watchers go stale once the compilers are recreated in-process. The edit never reaches the compiler at all. |

Each produces the same experience: you make a change, get no error, and conclude the change
was wrong. It usually was not. It never arrived.

These tools let you check instead of guess.

---

## The tools, in more detail

### 🔍 Ask the running engine

The strongest three. They query the engine actually loaded in the editor, so the answer cannot
be stale and cannot be a plausible invention.

`project_input_actions` earns its place for a subtle reason. Input actions are strings resolved
at runtime, so `Input.Down( "jump" )` against an action that does not exist compiles perfectly
and then silently never fires. No compile error, no warning. Without something handing over the
real vocabulary, you are spelling from memory.

### 📖 Ask the editor what it thinks is true

`project_info`, `project_compilers`, `project_source_changes` and `project_compile_errors` read
the editor's own in-memory state rather than the engine's type system. This is where the
"nothing watches the file" problem gets diagnosed.

### ⚙️ Make something happen

`project_reload_config`, `project_reload_settings`, `project_rebuild` and `project_build` are
the only four that write state, which is why they are the only four without the read-only hint.

---

## Why each tool exists

The source file is deliberately thin on commentary, at under 5% comment lines. The reasoning
lives here instead, once, where it can be maintained.

Six engine behaviours can swallow an edit or an authored value without raising anything. Each
one leaves you having changed a file, seen no error, and concluded the change was wrong when it
simply never arrived.

| What you changed | Why nothing happened | The tool |
|---|---|---|
| `.sbproj` | Read once at editor boot. Nothing watches it, so a `Metadata.Compiler` edit never reaches Roslyn | `project_reload_config` |
| `ProjectSettings/*.config` | Cached on first read and never invalidated, so an edited `Input.config` keeps serving the old actions | `project_reload_settings` |
| A `.cs` file | Compiler file watchers stop firing once the compilers are recreated in-process | `project_rebuild`, `project_build` |
| A content path in an authored asset | `Model.Load` returns the engine's **error model**, non-null with `IsError` set, so a null check never fires and the world ships orange | `project_content_path` |
| Nothing, you pulled a branch | The process serves whatever assembly it loaded at boot. A rebuild does not cure it and `compile_status` reads green throughout | `project_assembly_freshness` |
| Installed a package over MCP | `install_package` mounts for the session and writes nothing to the `.sbproj`, so it silently is not there next boot | `project_package_references` |

Two more close gaps rather than traps. `project_console_commands` and `project_input_actions`
cover the two string surfaces that fail silently when misspelled: a wrong console argument form
prints usage and changes nothing, which from outside is indistinguishable from having run.
`project_enum_values` exists because `project_type_members` reports zero methods and zero
properties for an enum, which reads as "the type is empty" rather than "wrong tool".

### Three implementation details worth knowing

**`project_reload_config` clears a cached hash first.** `Project.UpdateCompiler()` early-returns
when `CompilerHash` still matches `lastCompilerHash`, and that hash covers only the compile
settings, org, ident, type, the standalone flag and package references. Without zeroing it, an
edit to anything else is a silent no-op, and the tool would still report the freshly loaded
settings and look like it had worked.

**`project_reload_settings` does more than clear the cache.** Clearing alone changes nothing for
input actions: `Input.GetActions()` reads a static field that only `Input.ReadConfig` ever
assigns, and nothing calls it on a settings reload. The tool calls it, and reports the action
count before and after so you can see the file actually parsed.

**`project_assembly_freshness` compares versions, and version equality is not proof.**
Observed 2026-08-15: an `Editor/` file was replaced, the editor rebuilt the assembly from
`0.0.145.0` to `0.0.146.0`, built and loaded agreed, the tool reported `Stale: false`, and the
MCP registry kept serving the previous method body. The tool answers "is the loaded version
behind the built one", which is the question it can answer from the outside. It cannot see
that a registry still holds a `MethodDescription` pointing at replaced code. A clean
`Stale: false` narrows the problem, it does not close it, and the cure remains closing and
reopening the editor.

**`project_compilers` reads `Output?.Successful`, not `BuildSuccess`.** The latter is
`Output?.Successful ?? false`, which cannot tell a build that failed from one that has never
run. The nullable form can, and the tool reports the difference.

### On discoverability

These tools never appear in a client's `tools/list`. Listing is gated on `[McpListed]`, which is
`internal` to the engine, so only the editor's own seven top-level tools are listed. That is
deliberate on Facepunch's part: a list a client fetched once never goes stale as addon code
hotloads.

The practical consequence is that **each tool's XML summary is its entire discovery surface**,
because `search_tools` matches against the name and that description. That is why the summaries
in the source are the one category of comment kept, and why they are written as search targets
rather than as documentation.

## Which one to reach for

<details open>
<summary><b>"I edited a <code>.cs</code> file and nothing happened"</b></summary>

Run `project_source_changes` first.

- Compiler noticed **nothing** → the watchers are stale, run `project_build`.
- Compiler noticed **the file** → you have a compile error, run `project_compile_errors`.

Two calls, and you know which of two very different problems you have. This is the split that
saves the most time, because from the console they look identical.

</details>

<details>
<summary><b>"I edited the <code>.sbproj</code> and nothing happened"</b></summary>

`project_reload_config`, then read the settings it returns and confirm they are what you wrote.
Do not assume, the whole point is that this failure is silent.

</details>

<details>
<summary><b>"I edited <code>Input.config</code> and my new action does not work"</b></summary>

`project_reload_settings`, then `project_input_actions` to confirm the file parsed and the name
is spelled the way your code spells it.

</details>

<details>
<summary><b>"Does this compile?"</b></summary>

`project_build`. It recreates the compilers on the way through, so it rules out the stale
watcher case in the same call.

</details>

<details>
<summary><b>"Does this API even exist?"</b></summary>

`project_find_type`, then `project_type_members`. A no-match answer is proof, not a search that
needs rewording.

</details>

---

## Verifying it before you trust it

```bash
SBOX_MANAGED=/path/to/sbox/bin/managed/ dotnet build compilecheck/compilecheck.csproj
```

That compiles `SboxMcpServer.cs` against real engine assemblies using the same settings s&box
puts in a generated `Editor/` project: nullable enabled, warnings as errors, and the two static
global-namespace usings rather than a blanket `using Sandbox;`. Those settings are deliberate.
Relaxing any of them would let the file pass here and fail in somebody's editor, which is the
exact failure this is meant to catch.

Green proves the C# is sound and every engine member it names resolves. It does not prove the
toolset registers or that its tools run, which only opening the editor shows.

---

## How it breaks, and why that is deliberate

Most of what these tools touch is `internal`, so it is reached by reflection. Every reflected
member resolves through `RequiredMethod` or `RequiredProperty`, which **throw the name they
could not find**.

That is a design decision. The natural failure mode of a file like this is silent staleness: an
engine update renames an internal method, reflection quietly returns nothing, and the tool goes
on reporting success while doing nothing at all. That is the exact class of bug these tools
exist to catch, so it would be a poor joke to ship it inside them. A thrown name is uglier and
far more useful, because it tells you which member to go and read.

<details>
<summary><b>Every reflected member, verified against 26.08.05</b></summary>

| Member | Upstream path |
|---|---|
| `Project.RebuildCompilers()` | `Systems/Project/Project/Project.Static.cs:50` |
| `Project.LoadMinimal()` | `Systems/Project/Project/Project.cs:126` |
| `Project.UpdateCompiler()` | `Systems/Project/Project/Project.Compiling.cs:44` |
| `Project.CompileAsync()` | `Systems/Project/Project/Project.Compiling.cs:391` |
| `Project.GetCompileDiagnostics()` | `Systems/Project/Project/Project.Compiling.cs:403` |
| `Project.Compiler` / `EditorCompiler` | `Systems/Project/Project/Project.Compiling.cs:20,27` |
| `Compiler.ChangeSummary` | `Sandbox.Compiling/Compiler/Compiler.SyntaxTree.cs:258` |
| `ProjectConfig.GetCompileSettings()` | `Systems/Project/ProjectConfig.cs:272` |
| `ProjectSettings.ClearCache()` | `Systems/Project/ProjectSettings/ProjectSettings.cs:50` |

Paths are relative to `engine/` in
[Facepunch/sbox-public](https://github.com/Facepunch/sbox-public).

Three things need no reflection because they are public API:
`Sandbox.Input.GetActions()`, the `ProjectSettings` accessors, and
`Sandbox.Internal.GlobalToolsNamespace.EditorTypeLibrary`.

Roslyn diagnostics are flattened by reflection rather than by referencing
`Microsoft.CodeAnalysis`, which editor addon code cannot assume is available to it.

</details>

---

## Writing your own tools

Two attributes on a static class. One thing worth knowing before you start: the XML summary is
**not documentation**. It is the description an agent reads when deciding whether your tool is
the one it wants. Write it for that reader.

```csharp
[McpToolset( "my_tools", "What this group is for. list_toolsets shows this." )]
public static class MyTools
{
    /// <summary>
    /// This sentence is what search_tools surfaces. Say what the tool answers,
    /// not how it is implemented.
    /// </summary>
    [McpTool.ReadOnly( "my_tool" )]
    public static object MyTool( [Description( "Shown in the input schema." )] int count = 10 )
    {
        return new { Count = count };
    }
}
```

| Rule | Detail |
|---|---|
| Threading | Tools run on the main thread. Return a `Task` to go async |
| Return values | Serialized to JSON. Return a `Bitmap` for an image, or an `McpResult` to compose text and images |
| Permissions | Leave `ReadOnly` off and the client assumes the tool writes, and possibly destroys, state |
| Naming | Tool and toolset names are public API. Agents and saved workflows break when you rename one |

---

## Credits

Built and maintained by **fobiat (Kyle Tarff)**.

[![Website](https://img.shields.io/badge/fobiat.dev-1f6feb?style=flat-square)](https://fobiat.dev/)
[![GitHub](https://img.shields.io/badge/github.com%2Ffobiat-24292f?style=flat-square)](https://github.com/fobiat)
[![Email](https://img.shields.io/badge/kyle%40fobiat.dev-6e7681?style=flat-square)](mailto:kyle@fobiat.dev)

MIT licensed, see [LICENSE](../LICENSE). Built against the s&box engine by Facepunch Studios,
whose managed layer is MIT licensed at
[Facepunch/sbox-public](https://github.com/Facepunch/sbox-public). Not affiliated with
Facepunch.
