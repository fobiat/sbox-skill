# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.3.0] - 2026-08-15

Renames the editor toolset. **This is a breaking change**: any agent or saved workflow that
calls it by its old toolset id needs to be updated.

### Changed
- The MCP toolset registered by `editor-mcp/SboxMcpServer.cs` (formerly `SboxDevTools.cs`) is
  now `sbox_mcp_server`, was `sbox_dev`. The class is renamed to match. Tool names themselves
  (`project_find_type`, `project_build`, and so on) are unchanged.
- The library package under `library/` follows the same rename: `sbox_dev.sbproj` is now
  `sbox_mcp_server.sbproj`, with `Title` and `Ident` both `sbox_mcp_server`. A package reference
  is now `fobiat.sbox_mcp_server`.
- `editor-mcp/README.md` is restructured: a synopsis up top lists every tool and what it does in
  one table, ahead of install instructions, instead of the tool descriptions being split across
  three separate sections you had to read in order to get the full picture.

## [0.2.0] - 2026-08-15

Distribution only. **The skill and the toolset are unchanged**, byte for byte. This release is
about how you get them.

### Added
- `.claude-plugin/`, making the repo its own plugin marketplace. Install with
  `/plugin marketplace add fobiat/sbox-skill` then `/plugin install sbox@sbox-skill`, and
  updates arrive with `/plugin update` rather than a fresh clone. The skill still carries its
  own frontmatter and still works in any agent that can read a file, so this is one more route
  in and not a dependency.
- `library/`, packaging `sbox_dev` as an s&box library project so it can be published to
  asset.party and pulled in as a package reference instead of a copied file. Three engine
  behaviours make this work, all read from source at 26.08.05 and cited in
  [`library/README.md`](library/README.md): the editor assembly compiles unsandboxed, a
  consuming game or addon references every library that has an `Editor/` folder, and MCP tools
  are discovered across all loaded editor assemblies. Whether asset.party's publish flow accepts
  such a library is untested, and the file-drop install stays supported either way.
- `scripts/sync_library.py`, because a library cannot reference a source file outside its own
  project root, so `library/Editor/SboxDevTools.cs` has to be a copy.

### Changed
- `scripts/verify_release.py` gained a `DISTRIBUTION` section: the two manifests must agree on
  name and version, the plugin source must resolve to the skill, the version must match the
  changelog, and the library copy must be byte-identical to `editor-mcp/SboxDevTools.cs`. A
  drifted copy is a package that is silently wrong rather than obviously broken.

## [0.1.2] - 2026-08-15

Internal refactor of the toolset. **No behaviour change**: all 11 tool names, their read-only
hints, their parameters and their return shapes are identical, verified by
`scripts/verify_release.py` and the compile check.

### Changed
- The reflection plumbing is now one cohesive `Engine` class rather than four loose helpers.
  Calls read as intent (`Engine.CallShared`, `Engine.CallOwned`, `Engine.Hidden`,
  `Engine.Peek`, `Engine.Invoke`) instead of `BindingFlags` noise at every site, and the
  throw-the-missing-name behaviour lives in one place.
- Tools regrouped into the three that describe them, ask the engine, ask the editor, change
  something, with the plumbing separated into project, type and diagnostic sections.
- Method names shortened now that the class name carries the context, so `SboxDevTools.Build()`
  rather than `ProjectBuild()`. Tool names are public API and are untouched.
- `Sandbox.Input` and `Sandbox.InputAction` stay explicitly qualified. `Input` alone resolves to
  a different type inside the `Editor` namespace, which is a compile error rather than a
  preference.

## [0.1.1] - 2026-08-15

Patch release. `0.1.0` shipped `SboxDevTools.cs` in a state that would not compile in a project
whose `Editor/` assembly enables nullable reference types and treats warnings as errors.

### Fixed
- `CS0246` on `Project`, `TypeDescription` and `MethodDescription`. s&box generates an `Editor/`
  csproj with two *static* usings, which import static members and not the `Sandbox` namespace,
  so nothing brought those types into scope. Fixed with an explicit `using Sandbox;`.
- Twelve nullable errors under `TreatWarningsAsErrors`, all in reflection helpers that
  legitimately return null. Annotated `object?`, `string?`, `PropertyInfo?` and `Type?`.

### Added
- `editor-mcp/compilecheck/`, a headless compile check against real engine assemblies. It
  mirrors a real `Editor/` assembly on purpose, nullable on and warnings as errors, because
  relaxing either is what let the defects above ship.

The skill itself is unchanged.

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
