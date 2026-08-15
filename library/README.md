# s&box MCP Server as an s&box code library

This directory packages the [s&box MCP Server toolset](../editor-mcp) as a **library** project,
published to asset.party and pulled in as a package reference instead of copied file by file.

**Live at [sbox.game/fobiat/sbox_mcp_server](https://sbox.game/fobiat/sbox_mcp_server/)** since
2026-08-15, listed as *s&box MCP Server & AI Skill*.

Install becomes: open Project Settings, add `fobiat.sbox_mcp_server` to package references,
restart.

## Why this works

An s&box library can carry an `Editor/` folder, and a consuming project compiles against it.
Three facts from engine source at **26.08.05**, all in `sbox-public`:

| What | Where |
|---|---|
| The editor assembly is compiled unsandboxed, `Whitelist = false` and `Unsafe = true` | `Sandbox.Engine/Systems/Project/Project/Project.Compiling.cs:329` |
| A consuming project references each library that has an editor path | `Project.Compiling.cs:371`, via `AddEditorReference` |
| MCP tools are discovered across every loaded editor assembly, not just the project's own | `Sandbox.Tools/Mcp/ToolRegistry.cs:33`, `EditorTypeLibrary.GetMethodsWithAttribute<McpToolAttribute>()` |

The engine's own `library.minimal` template ships an `Editor/MyEditorMenu.cs`, so this is the
intended shape of a library rather than a trick.

## The two limits worth knowing

**Only games and addons pick it up.** The reference loop at `Project.Compiling.cs:370` is gated
on `Config.Type == "game" || Config.Type == "addon"`. A library that depends on this library
will not get the editor code. That is fine for the intended use and would not be if you were
building tooling on top of it.

**The policy question is now answered.** Everything above is read from engine source, which
could say what the engine does but not what asset.party allows. A library whose only content is
an unsandboxed editor assembly **is** accepted: the package published on 2026-08-15, 54.8 KB,
and the page reads Released.

## Layout

```
library/
  sbox_mcp_server.sbproj   Type: library, Org: fobiat, Ident: sbox_mcp_server
  Editor/
    SboxMcpServer.cs         byte-identical copy of editor-mcp/SboxMcpServer.cs
    SboxMcpServer.Editor.cs  byte-identical copy of editor-mcp/SboxMcpServer.Editor.cs
  ProjectSettings/         Collision, Input and Platform defaults the editor writes
```

Opening the project in the editor also writes `.sbox/`, `.vscode/`, `*.slnx`,
`Editor/*.editor.csproj` and `Editor/Properties/launchSettings.json`. Every one of those
hardcodes an absolute Steam install path or a home directory, so all five are gitignored and
`scripts/verify_release.py` fails if one ever gets tracked.

The editor also rewrites the `.sbproj` on first open, dropping `Tags`, `HasAssets`,
`AssetsPath`, `MenuResources`, `HasCode` and `CodePath`, and adding `IncludeSourceFiles`,
`Mounts` and `IsStandaloneOnly`. That is a schema migration, not damage: the result has the
same shape as Facepunch's own shipped `editor/DooEditor/DooEditor.sbproj`. Tags now live on the
asset.party page rather than in the project file.

`editor-mcp/SboxMcpServer*.cs` stays canonical. A library cannot reference a source file outside
its own project root, so these are copies rather than links, kept honest by two things: run
`python3 scripts/sync_library.py` after editing a canonical file, and
`scripts/verify_release.py` fails the build if any pair ever differs.

## Republishing it

The same four steps run again for every update.

1. Add this directory as a project in the s&box editor. It should appear under Libraries.
2. Check the toolset still registers: `list_toolsets` should show `sbox_mcp_server` with eighteen
   tools.
3. Publish from the editor, which uploads to asset.party under `fobiat.sbox_mcp_server`. The
   listing takes its display name from `Title`, so it reads **s&box MCP Server & AI Skill**; the
   description, tags and thumbnail are filled in on the site, not in the project file.
4. In a **separate game or addon project**, add `fobiat.sbox_mcp_server` to package references,
   restart the editor, and run `list_toolsets` again.

Step 4 is the one that decides an update. Loading from a package is a different code path from
loading a local `Editor/` folder, and only step 4 exercises it. If the tools do not appear
there, the file-drop install in [`editor-mcp/README.md`](../editor-mcp/README.md) is still the
supported path and nothing is lost.

## The listing art

Everything the page needs is generated, not hand-drawn, so an engine bump is a re-run:

```bash
python scripts/render_listing.py && python scripts/render_video.py
```

| Slot on the site | File |
|---|---|
| Thumbnail, 16:9 | `assets/brand/thumb-wide.png`, 910x512 |
| Tall, 9:16 | `assets/brand/thumb-tall.png`, 512x910 |
| Screenshots | `assets/brand/listing/01-problem.png` through `05-install.png`, 1920x1080 |
| Loading screen | `assets/brand/listing/loading-screen.png` |
| Video | `assets/brand/listing/video-1-mcp-server.mp4`, `video-2-agent-skill.mp4` |

On the page the two halves are called the **MCP Server** and the **AI Agent Skill**. The word
*toolset* is this repository's, and it earns its place here because the distinction from the
editor's own server is real. On a store listing it only invites the question, so the art does
not use it.

The listing's own copy, for reference when regenerating it:

| Field | Value |
|---|---|
| Title | s&box MCP Server & AI Skill |
| Summary | 18 MCP tools for the s&box editor, and 17 reference files that stop an AI agent writing Unity code into your Source 2 project. |
| Tags | agent, ai, api, automation, claude, code, development, editor, library, llm, mcp, mcpserver, reference, tool, tools, workflow |

A library consuming this library will not get the editor code either way, per the reference
loop gate described above.
