# Changelog

All notable changes to this skill are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Editor extension authoring reference: `EditorTool`, custom inspectors, docks,
  and the Qt-backed Widget UI system, which is not Razor.
- Services and persistence reference: stats, leaderboards, save data, packages,
  mounting.
- Avatar and clothing reference: `Clothing`, `ClothingContainer`, dressing a
  `SkinnedModelRenderer`.
- Shaders and rendering reference: `.shader` authoring, materials, render
  attributes, render layers.
- Audio and localization reference: the mixer graph, sound handles, `Phrase`
  and language files.

- Node graph and VR/voice-chat references.
- `sbox_dev` editor MCP toolset (`editor-mcp/SboxDevTools.cs`), nine tools:
  `project_info`, `project_compilers`, `project_source_changes`, `project_compile_errors`,
  `project_input_actions`, `project_reload_config`, `project_reload_settings`,
  `project_rebuild`, `project_build`. Read-only tools carry `McpToolHints.ReadOnly`.
- `tools/check_skill.py` gates routing pointers, unrouted files, frontmatter and em dashes.
- `tools/stamp_headers.py` writes the authorship and provenance header into every skill file.

### Changed
- Extracted from a private game project into a standalone skill.
- Reference files renamed off the inherited scheme, and the router repointed.
- Verified-behaviour ledger renumbered to `FN-1` through `FN-7`.

### Fixed
- `SceneTrace.WithoutTags` / `WithAnyTags` / `WithAllTags` are `params string[]` in 26.08.05.
  The skill taught that they were not, which was true of an earlier engine.
- `CameraComponent.AddHookAfterOpaque` and its three siblings are `[Obsolete]` with `=> null`
  bodies. Replaced with `AddCommandList( CommandList, Stage, order )`.
