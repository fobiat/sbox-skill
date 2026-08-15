<div align="center">

# `sbox_dev`

**Eleven MCP tools for the s&box editor, for when you edit a file and nothing happens.**

[![Engine](https://img.shields.io/badge/s%26box-26.08.05-f59c1a?style=flat-square)](https://sbox.game)
[![Licence](https://img.shields.io/badge/licence-MIT-3fb950?style=flat-square)](../LICENSE)
[![Tools](https://img.shields.io/badge/tools-11-e3b341?style=flat-square)](SboxDevTools.cs)
[![Single file](https://img.shields.io/badge/install-1_file-4c8eda?style=flat-square)](SboxDevTools.cs)

*One file. Drop it in `Editor/`, restart, done.*

</div>

---

## The problem

The s&box editor already ships an MCP server, and its stock toolsets (`editor`, `asset`,
`scene`, `component`, `package`, `play`, `log`) drive it well enough. What none of them cover
is the part that burns an afternoon: getting the editor to **notice** you changed something on
disk.

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

## Install

```bash
mkdir -p your-game/Editor
cp editor-mcp/SboxDevTools.cs your-game/Editor/
```

> Pairing this with the skill? The **[Quickstart](../QUICKSTART.md)** covers the loop the two
> form together, which is where most of the value is.

`Editor/` is a **separately compiled assembly**, not an `#if EDITOR` block in your game code.
Code living there is unsandboxed, which is what lets this reach internal engine API at all.

Restart the editor, then:

```
list_toolsets       → sbox_dev, eleven tools
describe_toolset    → full input schemas
```

---

## The tools

### 🔍 Ask the running engine

The strongest three. They query the engine actually loaded in the editor, so the answer cannot
be stale and cannot be a plausible invention.

| Tool | Answers |
|---|---|
| `project_find_type` | Does this type exist, what kind is it, what does it derive from |
| `project_type_members` | Real signatures for its methods and properties |
| `project_input_actions` | Every input action this project defines, with bindings |

`project_input_actions` earns its place for a subtle reason. Input actions are strings resolved
at runtime, so `Input.Down( "jump" )` against an action that does not exist compiles perfectly
and then silently never fires. No compile error, no warning. Without something handing over the
real vocabulary, you are spelling from memory.

### 📖 Ask the editor what it thinks is true

| Tool | Answers |
|---|---|
| `project_info` | What is open, and the compiler settings **live in memory** rather than on disk |
| `project_compilers` | Per compiler: `IsBuilding`, `NeedsBuild`, `BuildSuccess` |
| `project_source_changes` | What each compiler has actually noticed since its last build |
| `project_compile_errors` | Diagnostics as rows with file and line, not console text to scrape |

All seven of the above carry `McpToolHints.ReadOnly`, so a client can run them without stopping
to ask permission.

### ⚙️ Make something happen

| Tool | Does |
|---|---|
| `project_reload_config` | Re-reads the `.sbproj` into the live config and recreates compilers |
| `project_reload_settings` | Drops the cached `ProjectSettings` so the configs come back off disk |
| `project_rebuild` | Recreates every compiler and starts a build. Returns immediately |
| `project_build` | Rebuild and **wait**, then report success plus errors |

---

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

That compiles `SboxDevTools.cs` against real engine assemblies using the same settings s&box
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

Built and maintained by **Kyle (fobiat)**.

[![Website](https://img.shields.io/badge/fobiat.dev-1f6feb?style=flat-square)](https://fobiat.dev/)
[![GitHub](https://img.shields.io/badge/github.com%2Ffobiat-24292f?style=flat-square)](https://github.com/fobiat)
[![Email](https://img.shields.io/badge/kyle%40fobiat.dev-6e7681?style=flat-square)](mailto:kyle@fobiat.dev)

MIT licensed, see [LICENSE](../LICENSE). Built against the s&box engine by Facepunch Studios,
whose managed layer is MIT licensed at
[Facepunch/sbox-public](https://github.com/Facepunch/sbox-public). Not affiliated with
Facepunch.
