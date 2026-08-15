# Changelog

All notable changes to this project are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.4.0] - 2026-08-15

A full audit against the decompiled engine and `sbox-public`, then the fixes it found. **Update
if you use either half.** The skill taught one fact backwards and the toolset did not compile in
a default project, and both are corrected here.

### Fixed
- **`SboxMcpServer.cs` now carries `#nullable enable`.** Without it the file emitted 21
  `CS8632` errors in any project whose `.sbproj` does not set `Nullables`, which is the default.
  Every user with `TreatWarningsAsErrors` on hit this. The drop-in is now self-sufficient
  regardless of the host project's settings.
- **`Model.Load` guidance was inverted, in four places.** The skill said an unresolvable path
  returns `null` and shipped the recipe `if ( model is null )`. A non-blank path that resolves
  to nothing comes back as the engine's **error model**: non-null, `IsError` set. So the
  documented check was a branch that can never fire, and `authored ?? Model.Load( fallback )`
  cannot fall back. Both outcomes are possible, so the guidance is now
  `model is null || model.IsError`. Corrected in `SKILL.md`, `15_API_CORE.md`,
  `13_EXAMPLES.md` and `README.md`.
- **`project_reload_settings` could not do what it claimed.** It cleared the `ProjectSettings`
  cache and told you to confirm with `project_input_actions`, which would report stale actions
  forever: `Input.GetActions()` reads a static assigned only by `Input.ReadConfig`. It now calls
  that, and reports the action count before and after.
- **`project_reload_config` could silently no-op.** `UpdateCompiler()` early-returns on an
  unchanged `CompilerHash`, while the tool still reported the freshly-loaded settings, so it
  looked like it worked. It now clears the cached hash and reports whether compilers were
  actually recreated.
- `15_API_CORE.md` presented seven `[Obsolete]` `GameTransform` members as current API. Under
  `TreatWarningsAsErrors` each is a build failure.
- `06_EDITOR.md` claimed Hammer and MapEditor have no documented extension surface. There are 56
  public documented types across `Editor.MapDoc`, `Editor.MapEditor` and its `EntityDefinitions`.
- `02_COMPONENTS.md` named `Terrain.HeightMapSize`, which does not exist.
- `SKILL.md` taught `await Task.Frame()` unconditionally. It resolves only inside a `Component`,
  where the `Task` property shadows the type with `TaskSource`.

### Added
- **Seven MCP tools, taking the toolset from 11 to 18.** `project_content_path` and
  `project_content_search` resolve content paths against the mounted filesystem before a typo
  silently loads the error model. `project_assembly_freshness` reports when the process is
  serving an older assembly than the one on disk, which a rebuild does not cure and
  `compile_status` reports as green. `project_package_references` reconciles the `.sbproj`
  against what is installed, since `install_package` writes nothing. `project_console_commands`,
  `project_find_member` and `project_enum_values` close gaps in what could be asked of the
  engine.
- `references/17_CONSOLE.md`, the `[ConCmd]` and `[ConVar]` surface, which had one incidental
  mention across the whole skill.
- **How to actually connect an agent to the editor.** Port 7269,
  `claude mcp add --transport http sbox http://127.0.0.1:7269/mcp`. This was documented nowhere,
  which made half of `QUICKSTART.md` unperformable.
- `SECURITY.md`, plus `toolset-bug.yml` and `feature-request.yml` issue templates.
- Windows install commands throughout. Every command in the repo was POSIX, for a Windows-only
  engine, and `QUICKSTART.md` step 2 failed outright on a missing `mkdir`.
- New coverage: `MoveMode`, `ICameraModifier`, the runtime `PhysicsJoint` factory API,
  `Sandbox.Json`, versioned save documents, and the rest of the `Sandbox.Services` surface.
- Eight trap bullets, all live-verified, including that the host cannot slow a client-owned
  `PlayerController` and that `NetFlags` on an RPC attribute replaces the default rather than
  adding to it.

### Changed
- **The compile check now builds twice**, `Nullable=enable` and `Nullable=disable`, both with
  warnings as errors. It previously tested only the strict configuration while claiming to
  mirror a real project. A generated editor project takes its nullable setting from the
  `.sbproj`, which defaults to off, so the loose configuration is the one most readers get and
  the only one where `CS8632` can fire. That is how the bug above shipped.
- **CI compiles the C#.** It previously ran two Python scripts, and the only stand-in for a
  compile was counting braces and parentheses. A self-hosted Windows job now runs the real
  build, gated so a fork pull request can never reach the runner.
- **The release gate no longer green-lights an already-tagged version.** Its changelog regex
  could not match `[Unreleased]`, so it skipped the real section and compared a released version
  to itself. It now also cross-checks every tool name against every document that lists them,
  caps the skill frontmatter length, and survives paths containing spaces.
- The skill marks its claims *source*, *editor* or *unverified*. The `Model.Load` error shipped
  because a source read was written with the confidence of a live-verified one.

### Removed
- `library/ProjectSettings/` stays, but `Tags` does not come back: `ProjectConfig` has no such
  property, and listing branding is set on the asset.party page rather than in the manifest.

## [0.3.1] - 2026-08-15

Preparing the library for its first asset.party publish. **Nothing in the skill or the toolset
changed**, so no reinstall is needed.

### Changed
- `library/sbox_mcp_server.sbproj` is the schema the current editor writes: `Tags`, `HasAssets`,
  `AssetsPath`, `MenuResources`, `HasCode` and `CodePath` are gone, `IncludeSourceFiles`,
  `Mounts` and `IsStandaloneOnly` are new. Same shape as Facepunch's own shipped
  `editor/DooEditor/DooEditor.sbproj`. Tags now belong on the asset.party page.
- `Title` is now `s&box MCP Server` rather than the ident, since it is the display name on
  asset.party. `Ident` is untouched, so a package reference is still `fobiat.sbox_mcp_server`.
- `skills/sbox/references/06_EDITOR.md` no longer says `Code/` is set by a `CodePath` field in
  the `.sbproj`. `CodePath` is a get-only property and that field does not exist. Read from
  `bin/managed/Sandbox.Engine.dll` metadata, build 2026-08-07.

### Added
- `library/ProjectSettings/`, the Collision, Input and Platform defaults the editor writes.
- Gitignore rules for the five per-machine files the editor generates on open (`.sbox/`,
  `.vscode/`, `*.slnx`, `*.editor.csproj`, `Properties/launchSettings.json`). Each hardcodes an
  absolute Steam install path or a home directory.
- `scripts/verify_release.py` now scans every tracked file for local-path leakage rather than
  the skill alone, fails if an editor-generated file is tracked, and checks the sbproj `Org` and
  that `Title` is not just the ident. The old skill-only scan would not have caught
  `launchSettings.json`, which carried a full home directory path.

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
