# `sbox_dev`

Nine MCP tools for the s&box editor, built for the situation where you edit a file, nothing
happens, and nothing tells you why.

## The problem this exists for

The editor already ships a decent MCP server. Its stock toolsets (`editor`, `asset`, `scene`,
`component`, `package`, `play`, `log`) read and drive the editor well enough. What none of
them cover is the part that actually burns an afternoon: getting the editor to *notice* that
you changed something on disk.

There are three separate reasons it might not, and not one of them raises an error.

**The `.sbproj` is never watched.** The engine reads it once at editor boot, or writes it
back out when you use the in-editor Project Settings page. Change `Metadata.Compiler` in a
text editor and the compilers carry on with the old config indefinitely.

**`ProjectSettings/*.config` is cached on first read and never invalidated.** Edit
`Input.config` and the old actions keep resolving until you restart. This is a different
trap from the first one, and knowing about the `.sbproj` will not save you from it.

**Compiler file watchers go stale.** After the compilers get recreated in-process, their
source watchers have been observed to stop firing, so `.cs` edits never reach Roslyn at all.

Each one produces the same experience: you make a change, you get no error, and the obvious
conclusion is that your change was wrong. It usually was not. It just never arrived.

These tools let an agent check instead of guess.

## Install

```bash
mkdir -p your-game/Editor
cp editor-mcp/SboxDevTools.cs your-game/Editor/
```

`Editor/` is a **separately compiled assembly**, not an `#if EDITOR` block in your game code.
Code that lives there is unsandboxed, which is what lets this reach internal engine API at all.

Restart the editor. Then:

```
list_toolsets       → sbox_dev, nine tools
describe_toolset    → full input schemas
```

## The tools

### Reading

| Tool | What it tells you |
|---|---|
| `project_info` | What is open, where it is, and the compiler settings **live in memory**. That last part is the point: those are the settings Roslyn is using, which is not necessarily what the `.sbproj` on disk currently says. |
| `project_compilers` | Per compiler: `IsBuilding`, `NeedsBuild`, `BuildSuccess`. A compiler at `NeedsBuild: true` with `IsBuilding: false` has work queued that nothing started, which is what a stalled build looks like from outside. |
| `project_source_changes` | What each compiler has actually noticed since its last build. This separates "the compiler never saw my file" from "the compiler saw it and rejected it", which are very different problems that look identical from the console. |
| `project_compile_errors` | Diagnostics as rows with file and line, rather than text you have to scrape out of `read_console`. Takes `includeWarnings` and `limit`. |
| `project_input_actions` | Every input action the project defines, with keyboard and gamepad bindings. |

All five carry `McpToolHints.ReadOnly`, so a client can run them without stopping to ask you.

`project_input_actions` deserves a note. Input actions are strings resolved at runtime, so
`Input.Down( "jump" )` against an action that does not exist compiles perfectly and then
silently never fires. There is no compile error and no warning to notice. An agent writing
input code has no way to know the real vocabulary unless something tells it, and this is that
something.

### Writing

| Tool | What it does |
|---|---|
| `project_reload_config` | Re-reads the `.sbproj` into the live config and recreates the compilers. Returns the settings now live, so you can confirm the change landed instead of assuming. |
| `project_reload_settings` | Drops the cached `ProjectSettings` so `Input.config`, `Platform.config`, `Collision.config` and the rest come back off disk. |
| `project_rebuild` | Recreates every compiler and starts a build. Returns immediately. Recreating the compilers is what resets stale watchers. |
| `project_build` | Rebuild and **wait**, then report success plus errors. One call instead of rebuild-then-poll. |

## Which one to reach for

**"I edited a `.cs` file and nothing happened."** Run `project_source_changes` first. If the
compiler noticed nothing, the watchers are stale, so run `project_build`. If it noticed the
file, your problem is a compile error, so run `project_compile_errors`. Two calls, and you
know which of the two very different problems you have.

**"I edited the `.sbproj` and nothing happened."** `project_reload_config`, then read the
settings it returns back and confirm they are what you wrote.

**"I edited `Input.config` and my new action does not work."** `project_reload_settings`,
then `project_input_actions` to confirm the file parsed and the name is spelled the way your
code spells it.

**"Does this compile?"** `project_build`. It resets the compilers on the way through, so it
also rules out the stale-watcher case in the same call.

## How it breaks, and why that is deliberate

Most of what these tools touch is `internal`, so it is reached by reflection. Every reflected
member resolves through `RequiredMethod` or `RequiredProperty`, which throw the name they
could not find.

That is a design decision, not laziness. The natural failure mode of a file like this is
**silent staleness**: an engine update renames an internal method, the reflection quietly
returns nothing, and the tool keeps reporting success while doing nothing at all. That is the
exact class of bug these tools exist to catch, so it would be a poor joke to ship it inside
them. A thrown name is worse to look at and far better to have, because it tells you which
member to go and read.

Verified against engine **26.08.05**:

| Member | Upstream |
|---|---|
| `Project.RebuildCompilers()` | `Systems/Project/Project/Project.Static.cs:50` |
| `Project.LoadMinimal()` | `Systems/Project/Project/Project.cs:126` |
| `Project.UpdateCompiler()` | `Systems/Project/Project/Project.Compiling.cs:44` |
| `Project.CompileAsync()` | `Systems/Project/Project/Project.Compiling.cs:391` |
| `Project.GetCompileDiagnostics()` | `Systems/Project/Project/Project.Compiling.cs:403` |
| `Project.Compiler` / `EditorCompiler` | `Systems/Project/Project/Project.Compiling.cs:20,27` |
| `Compiler.ChangeSummary` | `Sandbox.Compiling/Compiler/Compiler.SyntaxTree.cs:258` |
| `ProjectConfig.GetCompileSettings()` | `Systems/Project/ProjectSettings/../ProjectConfig.cs:272` |
| `ProjectSettings.ClearCache()` | `Systems/Project/ProjectSettings/ProjectSettings.cs:50` |

Paths are relative to `engine/` in [Facepunch/sbox-public](https://github.com/Facepunch/sbox-public).

Two things are public API and need no reflection: `Sandbox.Input.GetActions()` and the
`ProjectSettings` accessors. Roslyn diagnostics are flattened by reflection rather than by
referencing `Microsoft.CodeAnalysis`, which editor addon code cannot assume is available.

## Writing your own

Two attributes on a static class, and one thing worth knowing before you start: the XML
summary is **not documentation**. It is the description an agent reads when it is deciding
whether your tool is the one it wants. Write it for that reader.

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

Tools run on the main thread and may return a `Task` to go async. Return values are
serialized to JSON. Return a `Bitmap` to send an image, or an `McpResult` to compose text and
images yourself. Leave the `ReadOnly` hint off and the client will assume your tool writes,
and possibly destroys, state, which means it may stop and ask the user every time.

Tool and toolset names are public API. Agents and their saved workflows break when you rename
one, so pick the name you can live with.

## Credits

Written and maintained by **Kyle (fobiat)**.

- [fobiat.dev](https://fobiat.dev/)
- [github.com/fobiat](https://github.com/fobiat)
- kyle@fobiat.dev

MIT licensed, see [LICENSE](../LICENSE). Built against the s&box engine by Facepunch Studios,
whose managed layer is MIT licensed at
[Facepunch/sbox-public](https://github.com/Facepunch/sbox-public). Not affiliated with
Facepunch.
