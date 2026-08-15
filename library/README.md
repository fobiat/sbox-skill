# `sbox_dev` as an s&box code library

This directory packages the [`sbox_dev` toolset](../editor-mcp) as a **library** project, so it
can be published to asset.party and pulled in as a package reference instead of copied file by
file.

Install becomes: open Project Settings, add `fobiat.sbox_dev` to package references, restart.

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

**Publishing is untested.** Everything above is read from engine source. Whether asset.party's
publish flow accepts a library whose only content is an unsandboxed editor assembly is a policy
question that source cannot answer. Publish it once and find out before relying on it.

## Layout

```
library/
  sbox_dev.sbproj      Type: library, Org: fobiat, Ident: sbox_dev
  Editor/
    SboxDevTools.cs    byte-identical copy of editor-mcp/SboxDevTools.cs
```

`editor-mcp/SboxDevTools.cs` stays canonical. A library cannot reference a source file outside
its own project root, so this is a copy rather than a link, kept honest by two things: run
`python3 scripts/sync_library.py` after editing the canonical file, and
`scripts/verify_release.py` fails the build if the two ever differ.

## Publishing it

1. Add this directory as a project in the s&box editor. It should appear under Libraries.
2. Check the toolset still registers: `list_toolsets` should show `sbox_dev` with eleven tools.
3. Publish from the editor, which uploads to asset.party under `fobiat.sbox_dev`.
4. In a consumer, add `fobiat.sbox_dev` to package references and restart the editor.

Step 2 matters. If the tools do not appear when the library is loaded from a package rather
than from a local `Editor/` folder, the file-drop install in
[`editor-mcp/README.md`](../editor-mcp/README.md) is still the supported path and nothing is
lost.
