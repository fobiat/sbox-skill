# `sbox_dev` editor MCP toolset

Six MCP tools that close the gap between editing s&box source on disk and getting it
compiled.

The s&box editor embeds an MCP server, and its stock toolsets (`editor`, `asset`, `scene`,
`component`, `package`, `play`, `log`) cover reading and driving the editor well. What they
do not cover is the failure that costs a whole session:

- The engine reads the `.sbproj` at editor boot, or writes it from the in-editor Project
  Settings page. **Nothing watches it for external edits.** Change `Metadata.Compiler` on
  disk and the compilers keep building with the stale config indefinitely, with no warning.
- After compilers are recreated in-process, their source file watchers have been observed to
  **stop firing**, so edits to `.cs` files never compile.

Both are recorded as field notes in the skill (`FN-3` and `FN-4`). In each case an agent
edits files, sees no error, and concludes the change did nothing wrong. This toolset gives
it a way out.

## Install

Copy the file into your project's editor assembly:

```bash
mkdir -p your-game/Editor
cp editor-mcp/SboxDevTools.cs your-game/Editor/
```

`Editor/` is a **separately compiled assembly**, not an `#if EDITOR` block. Code there is
unsandboxed, which is what lets this reach internal engine API by reflection.

Restart the editor, or run `project_rebuild` if you already have the toolset loaded. Then:

```
list_toolsets          → sbox_dev appears
describe_toolset       → full input schemas for the six tools
```

## The tools

| Tool | Reads or writes | What it does |
|---|---|---|
| `project_info` | read | Which project is open, where it is on disk, and the compiler settings **currently live in memory**, which is the point: those are what Roslyn is using, not what the `.sbproj` on disk says. |
| `project_compilers` | read | Every compiler with `IsBuilding`, `NeedsBuild` and `BuildSuccess`. A compiler at `NeedsBuild: true` with `IsBuilding: false` has work queued that nothing started. |
| `project_compile_errors` | read | Diagnostics as structured rows with file and line, instead of scraping `read_console`. Takes `includeWarnings` and `limit`. |
| `project_reload_config` | write | Re-reads the `.sbproj` into the live config and recreates compilers, so an externally edited `Metadata.Compiler` block actually reaches Roslyn. Returns the settings now live. |
| `project_rebuild` | write | Recreates every compiler and starts a build from source on disk. Returns immediately. |
| `project_build` | write | Rebuild and **wait**, then report success plus errors. One call instead of rebuild-then-poll. |

Read-only tools carry `McpToolHints.ReadOnly`, so a client can run them without prompting
the user.

## Which one to reach for

**"I edited a `.cs` file and nothing changed."** `project_build`. It resets the compilers,
which also resets stale file watchers, and tells you whether the result compiled.

**"I edited the `.sbproj` and nothing changed."** `project_reload_config`, then check the
returned settings are what you wrote. Nothing else picks that file up.

**"The build failed and I want the errors."** `project_compile_errors`. Pass
`includeWarnings: true` when the project builds with `TreatWarningsAsErrors`, because then a
warning is what failed the build.

## How it works, and how it breaks

Every engine member used here is `internal`, so it is reached by reflection: editor
assemblies are unsandboxed but still live outside `Sandbox.Engine`. Reflected members are
resolved through `RequiredMethod` and `RequiredProperty`, which throw the missing name.

That is deliberate. The failure mode of a file like this is **silent staleness** after an
engine update: a renamed internal method turns a tool into a no-op that still returns
success. A thrown name beats that, because it tells you exactly which member to go and look
up.

Verified against engine **26.08.05**:

| Reflected member | Where it lives upstream |
|---|---|
| `Project.RebuildCompilers()` | `engine/Sandbox.Engine/Systems/Project/Project/Project.Static.cs:50` |
| `Project.LoadMinimal()` | `engine/Sandbox.Engine/Systems/Project/Project/Project.cs:126` |
| `Project.UpdateCompiler()` | `engine/Sandbox.Engine/Systems/Project/Project/Project.Compiling.cs:44` |
| `Project.CompileAsync()` | `engine/Sandbox.Engine/Systems/Project/Project/Project.Compiling.cs:391` |
| `Project.GetCompileDiagnostics()` | `engine/Sandbox.Engine/Systems/Project/Project/Project.Compiling.cs:403` |
| `Project.Compiler` / `EditorCompiler` | `engine/Sandbox.Engine/Systems/Project/Project/Project.Compiling.cs:20,27` |
| `ProjectConfig.GetCompileSettings()` | `engine/Sandbox.Engine/Systems/Project/ProjectConfig.cs:272` |

Roslyn diagnostics are flattened by reflection rather than by referencing
`Microsoft.CodeAnalysis`, which editor addon code cannot assume is available to it.

## Writing your own tools

The pattern is two attributes on a static class, and it is worth knowing because the XML
summary is not documentation, it is the tool description the agent reads when deciding
whether to call it.

```csharp
[McpToolset( "my_tools", "What this group is for, shown by list_toolsets." )]
public static class MyTools
{
    /// <summary>
    /// This sentence is what the agent sees in search_tools. Write it for a reader
    /// deciding whether this is the tool they want.
    /// </summary>
    [McpTool.ReadOnly( "my_tool" )]
    public static object MyTool( [Description( "Shown in the input schema." )] int count = 10 )
    {
        return new { Count = count };
    }
}
```

Tools run on the main thread and may return a `Task` to go async. Return values are
serialized to JSON. Return a `Bitmap` to send an image, or an `McpResult` to compose text
and images yourself. Omit the `ReadOnly` hint and the client assumes the tool writes, and
possibly destroys, state.

Tool and toolset names are public API. Agents and their saved workflows break when they
change.
