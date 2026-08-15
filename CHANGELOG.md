# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-08-15

Initial release. Written against s&box engine **26.08.05**.

### The skill

A router (`SKILL.md`) and sixteen reference files under `skills/sbox/references/`, covering:

- **`01_SCENE`** Scene, GameObject and Component: object model, lifecycle, prefabs, scene
  events, `IPressable`.
- **`02_COMPONENTS`** The built-in component library: rendering, physics,
  `CharacterController`, `PlayerController`, `Prop`, inventory, camera, lighting, audio,
  navigation, effects.
- **`03_UI`** Razor panels, `PanelComponent`, `BuildHash`, SCSS, the flexbox layout model,
  built-in controls, world-space panels.
- **`04_NETWORKING`** Lobbies, connections, ownership, `[Sync]`, `NetList`, RPCs, network
  events, dedicated servers.
- **`05_INPUT_PHYSICS`** Input actions, `Scene.Trace`, the physics world, collision
  listeners, math types, time, gizmos.
- **`06_EDITOR`** Editor extensions: `EditorTool`, `[CustomEditor]`, docks, and the
  Qt-backed Widget system, which is not Razor.
- **`07_SERVICES`** Stats, leaderboards, achievements, `FileSystem.Data`, cookies,
  `Package`, mounting.
- **`08_AVATARS`** Citizen model paths, `Clothing`, `ClothingContainer`, `Dresser`, body and
  material groups.
- **`09_RENDERING`** `.shader` anatomy, HLSL entry points, `Material`, `RenderAttributes`,
  `CommandList`, render layers.
- **`10_AUDIO`** The mixer graph, `SoundHandle`, audio processors, `Phrase`, language files.
- **`11_ACTIONGRAPH`** Exposing C# as graph nodes, and graph-backed callbacks.
- **`12_VR_VOICE`** VR rig, controllers, haptics, voice capture, transmission, playback.
- **`13_EXAMPLES`** Eleven complete components, from an FPS controller to a press-E vendor.
- **`14_VERIFICATION`** The editor MCP server, and the `FN-1` to `FN-7` ledger of behaviour
  confirmed in live editor sessions.
- **`15_API_CORE`** Full signatures for the types a game touches most.
- **`16_API_INDEX`** Namespace-organised index of the wider API surface.

### The `sbox_dev` MCP toolset

`editor-mcp/SboxDevTools.cs`, a drop-in file for a project's `Editor/` folder. Eleven tools.

Querying the running engine, which cannot be out of date: `project_find_type`,
`project_type_members`, `project_input_actions`.

Reading editor state: `project_info`, `project_compilers`, `project_source_changes`,
`project_compile_errors`.

Acting on the project: `project_reload_config`, `project_reload_settings`,
`project_rebuild`, `project_build`.

Read-only tools carry `McpToolHints.ReadOnly` so a client can run them without prompting.

### Tooling

- `scripts/check_skill.py` gates routing pointers, unrouted reference files, frontmatter and
  em dashes.
- `scripts/stamp_headers.py` maintains the authorship and provenance header on every skill file.
- CI mirrors the local gate on push and pull request.

### Engine corrections

Two behaviours where documentation and older material disagree with 26.08.05, both confirmed
in source:

- `SceneTrace.WithoutTags` / `WithAnyTags` / `WithAllTags` take `params string[]`
  (`engine/Sandbox.Engine/Scene/Scene/Scene.Trace.cs:637`). Material claiming otherwise
  describes an earlier engine.
- `CameraComponent.AddHookAfterOpaque` and its three siblings are `[Obsolete( "Use
  CommandList" )]` with `=> null` bodies, so a call compiles, returns a null `IDisposable`
  and renders nothing. Use `AddCommandList( CommandList, Stage, order )`.
