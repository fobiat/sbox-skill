<!--
  s&box Skill : 14_VERIFICATION.md

  The editor MCP server, and the ledger of behaviour confirmed in live sessions.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Driving the Editor, and What's Actually Been Verified

Two kinds of claim live in this file. Some were read straight out of the engine source at version **26.08.05** (`sbox-public`; every path below is relative to that repo's root). Others came from watching a live editor Play session and got written into the ledger at the bottom. A source read tells you what the API *is*; a ledger row tells you what it *actually did* when someone ran it. When the two disagree, trust the ledger.

Check this file before touching the editor over MCP, before hand-writing a `.item`-style asset, and before you take it on faith that a `[Sync]` value is actually replicating.

***

## The Editor MCP Server

An MCP server ships inside the s&box editor itself. It acts on **whatever project the editor currently has open**, never on your shell's working directory, so nothing you send through it touches a project the editor hasn't loaded.

### Where to Start

What you see up front is a small slice. The full tool registry lives across editor and addon code, and it shifts as that code hotloads.

| Tool | What it's For |
| :-- | :-- |
| `editor_status` | Call this first: which project is open, whether Play mode is running |
| `list_toolsets` | The registry, grouped into named toolsets |
| `describe_toolset` | Full input schema for one toolset, ready to call |
| `search_tools` | Search the whole registry by keyword |
| `call_tool` / `call_tools` | Call a single tool, or several in one batch |
| `read_console` | The editor's log output, including errors and exceptions |

A failed tool call comes back as a normal result carrying **`isError`**, so read the result instead of expecting an exception. That covers everything *inside* a tool. Two paths above the tool layer do produce real JSON-RPC errors, and a client that assumes there are none will mis-handle them: an unrecognised method returns `MethodNotFound`, and `tools/call` without a `name` returns `InvalidParams` (`McpServer.cs`, `Handle` and `ToolsCall`). At the HTTP layer, a malformed body is a `400` carrying `ParseError`, and a JSON-RPC batch array is a `400` carrying `InvalidRequest`.

### Connecting

The server is embedded in the editor process and starts with it, on by default.

| Fact | Value |
| :-- | :-- |
| Default port | **7269** (`EditorPreferences.McpServerPort`, an editor cookie) |
| URL | `http://127.0.0.1:7269/mcp` (`McpServer.Url`) |
| Binding | Loopback only: `127.0.0.1` and `localhost`, nothing else |
| Origin check | A request carrying a non-loopback `Origin` header gets `403`, guarding against DNS rebinding |
| Method | `POST` only. `GET` and `DELETE` return `405` with `Allow: POST` |
| Path | Exactly `/mcp`. Anything else is a clean `404` |
| Body cap | **8 MiB**, enforced on the read itself rather than trusting `Content-Length` |
| Batching | JSON-RPC arrays are rejected outright: `"Batching is not supported"` |
| Protocol versions | `2025-11-25`, `2025-06-18`, `2025-03-26`, `2024-11-05`; an unknown request gets the newest |

**Capabilities are tools and nothing else.** `initialize` answers with
`{"tools": {"listChanged": false}}` and no other capability key: **no resources, no prompts, no
progress notifications, and no SSE**. There is no server-initiated stream and no session, which
is why `GET` is refused. Do not wait on a progress event that will never arrive; a long tool call
simply holds its HTTP response open until it returns.

### What Ships Built In (engine 26.08.05)

| Toolset | Tools it Contains |
| :-- | :-- |
| `editor` | `editor_status`, `read_console`, `console_command`, `compile_status`, `list_toolsets`, `describe_toolset`, `search_tools`, `call_tool`, `call_tools` |
| `asset` | `asset_search`, `asset_info`, `asset_read`, `asset_write`, `asset_compile`, `asset_dependencies`, `asset_files`, `asset_find_by_file`, `asset_types`, `asset_thumbnail`, `create_asset` |
| `scene` | `list_scenes`, `scene_tree`, `get_game_object`, `find_game_objects`, `create_game_object`, `delete_game_object`, `set_game_object`, `add_component`, `remove_component`, `set_component`, `spawn_model`, `spawn_models`, `scene_trace`, `get_selection`, `set_selection`, `save_scene`, `undo`, `redo`, `get_editor_camera`, `set_editor_camera`, `editor_camera_screenshot`, `camera_screenshot` |
| `component` | `get_component_type` |
| `package` | `find_packages`, `get_package`, `install_package` |
| `play` | `play_start`, `play_stop`, `play_pause` |
| `log` | `log_info`, `log_warning`, `log_error` |

A project registers its own toolset by putting `[McpToolset]` on a static class in its `Editor/` folder and `[McpTool]` on the static methods inside it. The method's XML summary becomes the description an agent reads when deciding whether to call the tool, so write that sentence for a reader rather than as documentation. `[McpTool.ReadOnly]` marks a tool that never changes state. Tools run on the main thread, may return a `Task` to go async, and their return value is serialized to JSON.

> **A client's `tools/list` contains 7 of those 52 tools, and a third-party toolset can never
> appear in it.** `ToolRegistry.ListJson()` emits only methods carrying `[McpListed]`
> (`Mcp/ToolRegistry.cs:51-64`), and `McpListedAttribute` is **`internal` to the engine**
> (`Mcp/McpListedAttribute.cs:11`) on purpose, so a list a client fetched once never goes stale
> as code hotloads. The seven are `editor_status`, `read_console`, `list_toolsets`,
> `describe_toolset`, `search_tools`, `call_tool` and `call_tools`. **Everything else, including
> every tool in the table above beyond those seven and every tool your own project registers, is
> reachable only through `search_tools` / `describe_toolset` and then `call_tool`.**
>
> One consequence: **`readOnlyHint` never reaches a client's tool list for an addon tool.** The
> annotation is written in `ToolJson` (`ToolRegistry.cs:115-123`), which `tools/list` only calls
> for `[McpListed]` tools, so a client's own read-only detection cannot see it. Assume every
> addon tool is treated as a destructive write by the client, which is the protocol's default
> for an unknown tool.

This skill ships one such toolset, `sbox_mcp_server`, as a drop-in file at
`editor-mcp/SboxMcpServer.cs` in its own repository. Eighteen tools, in three groups.

Working around the traps recorded below: `project_reload_config` re-reads an externally
edited `.sbproj`, `project_reload_settings` drops the cached `ProjectSettings` so
`Input.config` and friends come back off disk, `project_rebuild` recreates the compilers,
and `project_build` does that and waits for the result.

Seeing what the editor thinks is true: `project_info`, `project_compilers`,
`project_source_changes` (what each compiler has actually noticed since its last build),
`project_compile_errors`, `project_assembly_freshness` (whether the process is still serving
an older assembly than the one on disk, which a rebuild does not cure) and
`project_package_references` (the `.sbproj` list against what is actually installed, since
`install_package` mounts for the session and writes nothing).

**Checking an API against the running engine rather than against this skill:**
`project_find_type` and `project_type_members` query the live `TypeLibrary` and return real
signatures, with `[Obsolete]` members marked. Prefer them to any file here when the two could
disagree, because the engine cannot be out of date and a reference file eventually will be.
`project_find_member` searches every loaded type at once, for when you know roughly what a
method is called but not what it hangs off. `project_enum_values` answers for an enum, which
`project_type_members` cannot, since an enum has no methods or properties and reports zero of
both. `project_input_actions` and `project_console_commands` do the same job for the two string
surfaces that fail silently when misspelled.

**Checking a content path before it fails at runtime:** `project_content_path` resolves one
against the mounted filesystem and `project_content_search` lists what is actually there.
`Model.Load` hands back the engine's error model rather than null for a path nothing provides,
so an authored typo builds clean, passes every headless test and ships an orange world.

### How the Registry Behaves

- Pagination uses `limit` / `offset`, and **only `limit` clamps**. Clamping is
  `ApplyRange`, which does nothing unless the parameter carries a `[Range]` attribute
  (`Mcp/ToolRegistry.cs:285-298`). Every `limit` in the built-in registry has one; **no `offset`
  anywhere does** (`AssetSystem.cs:25`, `Packages.cs:20`), so an offset past the end is your
  problem, not the server's. The server's own `instructions` blurb says out-of-range values
  clamp; that is true for `limit` and overstated for `offset`.
- Vectors and angles travel as **comma-separated strings**, never arrays: a position is
  `"x,y,z"`, a view angle is `"pitch,yaw,roll"`.
- The coordinate system is Source's: 1 unit equals 1 inch, +x is forward, +y is left, +z is up, angles in degrees.
- A GameObject or Component is addressed by **guid**; an asset by the relative path
  `asset_search` hands back.
- Any tool that edits the scene pushes its own undo step.

### Three Traps That Cost a Session Each

All three are ledger rows, and each cost a session to find, so save yourself the rediscovery.

**1. External edits to `.sbproj` / `.config` files go unnoticed.** (ledger FN-3,
live-verified 2026-08-07.) The `.sbproj` gets read once, at editor boot, and from then on
the only writer the engine recognizes is its own in-editor Project Settings page: nothing
watches the file for outside changes. Change `Metadata.Compiler` on disk and the
compilers just keep building against that stale config forever, silently, with no rebuild
and no warning that anything's wrong. The way out is a `project_reload_config`-style tool
(internally: `Project.LoadMinimal()`, which is `internal`, then `Project.UpdateCompiler()`,
which is **`private`** at `Project.Compiling.cs:44`, so reaching it needs reflection or a
public path that calls it) or restarting the editor outright.

**2. After recreating the compilers, source edits stopped triggering builds.**
(ledger FN-4, live-verified 2026-08-07. **Observed behaviour, unconfirmed mechanism.**)
Before the reload, saving a `.cs` file kicked off a compile immediately. After one, edits and
new files under `Code/` and `Editor/` produced **no build for 15+ seconds**, with `NeedsBuild`
reading false the entire time and `compile_status` looking healthy throughout.

The observation is real. The explanation this file used to give, that recreating the compilers
kills the source file watchers, is **not supported by the source** and has been withdrawn:

- `Project.Static.cs:69` clears `proj.lastCompilerHash = default` before `UpdateCompiler()`, so
  the hash short-circuit at `Project.Compiling.cs:52` cannot skip the rebuild.
- `CompileGroup.CreateCompiler` calls `compiler.MarkForRecompile()` on the new compiler
  (`CompileGroup.cs:117`), so `NeedsBuild` should be **true**, not false.
- Both compiler setup paths end with a watcher: `Project.Compiling.cs:175`
  (`Compiler.WatchForChanges()`) and `:388` (`EditorCompiler.WatchForChanges()`).

Whatever actually happened, the cheaper remedy is to re-arm rather than to dispose and rebuild
the whole `CompileGroup`. **`Compiler.MarkForRecompile()` (`Compiler.cs:322`) and
`Compiler.WatchForChanges()` (`Compiler.Watch.cs:10`) are both `public`**, so a tool can call
them directly on an existing compiler without touching the group at all.

> **Rule of thumb:** after *any* batch of source edits made outside the editor, force the
> rebuild explicitly (`project_rebuild`, which runs a full `RebuildCompilers()`), then poll
> `compile_status` until `IsBuilding` flips false and check `Success` on each compiler. Silence
> is not proof of a successful build.

**3. Recompiling does not cure a stale assembly. Only restarting the editor does.** This one is
worse than the other two, because every instrument reads green while it is happening.

The editor process serves whatever it loaded at boot. **No hotload fires for an external change
to the source tree**, and a `git merge`, a branch switch, a `git stash pop` or a bulk edit from
another tool is exactly that. Neither a rebuild nor a config reload cures it. Stopping play mode
first changes nothing. `compile_status` reports green throughout, because it is reporting on the
last build that succeeded, which it did. The symptom is code behaving like the version you
replaced: a method you deleted still running, a fix you merged still absent, a type you renamed
still resolving under its old name.

> **Never judge freshness by `compile_status`. Judge it by a runtime marker.** Put a version
> string or a build stamp somewhere the running game logs, and read it back through
> `read_console` or a `[ConCmd]` (see `17_CONSOLE.md`). If the marker is stale, close the editor
> and reopen it; nothing short of that will help, and every minute spent rebuilding is wasted.

***

## Verified Networking Behaviour

### Client writes to `[Sync(SyncFlags.FromHost)]` vanish without error

Confirmed twice under ledger **FN-1** (2026-08-07): once on a host-owned object in
Slice 0, once on a **client-owned** object in Slice 2 where the writing client's own
`IsProxy` read `false`. Both runs behaved the same.

What the source shows:

- `Scene/Networking/NetworkObject.DataTable.cs:75` picks the control predicate for a
  synced property as `c => isHostSync ? c.IsHost : HasControl( c )`. `FromHost` skips the
  ownership check entirely, control just means *host*, regardless of who owns the object.
- `Scene/Components/Component.Network.cs:64-68` has the generated setter test
  `dataTable.HasControl( slot )` first, and `return`s the moment that fails, **before**
  `p.Setter?.Invoke( p.Value )` ever runs. The backing field never sees the write.

The client in the live test assigned `9999` to the property. No exception was thrown, and
reading the value back on the very same frame already showed the host's number: this
isn't a write that reverts a moment later, it's a write that was never applied.

```csharp
// A client calling this sees no error and no effect. Debug by reading the value back.
[Sync( SyncFlags.FromHost )] public int Money { get; set; }
```

Nothing signals the failure. A client that genuinely needs to change host state has to
route the change through an `[Rpc.Host]` and validate it on that side instead.

### The same lockout applies one level down, inside `NetList<T>`

One gate covers every mutating method on `NetList<T>`:

```csharp
// Scene/Networking/Containers/NetList.cs:470
private bool CanWriteChanges() => !Parent?.IsProxy ?? true;
```

`Add`, `AddRange`, `Insert`, `Clear`, `RemoveAt`, and `this[i] = v` all bail out early the
moment that gate is false (`NetList.cs:144-249`), and `Remove` just returns `false`
instead. **No exception, no log line** marks any of it. `Parent` resolves to the
`NetworkTable.Entry`, and its `IsProxy` is `!HasControl( Connection.Local )`, which means
a non-host mutation under `FromHost` is a silent no-op, same story as the scalar
property above.

Under ledger **FN-2** (2026-08-07): a `[Sync(SyncFlags.FromHost)] NetList<Entry>`, where
`Entry` combines a `Guid` with a `GameResource`-derived reference, replicated three
seeded rows to a second client. The Guids matched, resource references resolved
correctly, and a reference deliberately left null stayed null, holding steady across 5
samples over 15 s. `04_NETWORKING.md` → *Networked Collections* covers the authoring rules
this produces.

### `[Sync]` never goes live under `NetworkMode.Snapshot`

Ledger **FN-5**, live-verified 2026-08-07, first negatively, then positively.

`Scene/GameObject/GameObject.Network.cs:62` defaults `GameObject.NetworkMode` to
`NetworkMode.Snapshot`, which means **every object dropped into a scene starts out this
way** without anyone asking for it. Snapshot only means "included in the scene snapshot a
joining client receives", it is not synonymous with "networked": no `NetworkObject` gets
created, and `[Sync]` properties only land in a network table through
`NetworkObject.RegisterPropertiesRecursive` (`NetworkObject.DataTable.cs:29-55`). Skip
that step and there is no live sync, full stop.

RPCs are unaffected the whole time this is happening: instance RPC dispatch finds its
target through `Scene.Directory.FindByGuid` / `FindComponentByGuid`
(`Rpc.InstanceRpc.cs:30-52`), a path that never touches the network object at all.

In the live test, the client's `HostCounter` read `0` across **62 consecutive pings**
while the host had already reached 117, and RPCs routed fine on both sides throughout.
The moment the host called `NetworkSpawn()`, replication caught up and stayed lockstep.

> This is the quietest failure mode in the engine: RPCs work, logs look clean, and the
> state is simply frozen at its starting value. Check `IsNetworkRoot` on the object or an
> ancestor **first** whenever a `[Sync]` value looks stuck.

### Calling `NetworkSpawn()` bare hands ownership to whoever called it

```csharp
// Scene/GameObject/GameObject.Network.cs:125
public bool NetworkSpawn() => NetworkSpawn( Connection.Local );
```

On the host that resolves to `Connection.Host`, which is usually what you wanted. But the
parameterless form encodes *whoever happens to call it*, not "the host" specifically, so
the same line spawns a client-owned object the moment it runs from client code, and a
host-owned one otherwise. For a world object nobody should control, and for a player
object that must belong to a specific connection, always be explicit:

```csharp
prop.NetworkSpawn( Connection.Host );        // world object, host-authoritative
player.NetworkSpawn( connection );           // this connection's pawn
```

Under ledger **FN-6** (2026-08-07): a host-created object that got `NetworkSpawn(connection)`d
ended up owned by that connection's client. Each client saw `IsProxy == false` on its own
player and `IsProxy == true` on the other one's, exactly as expected. The full-featured
overload, `NetworkSpawn( NetworkSpawnOptions )` at `GameObject.Network.cs:131-181`, takes
`Owner`, `StartEnabled`, `OwnerTransfer`, `OrphanedMode`, `AlwaysTransmit`, and `Flags`.

### `Rpc.FilterInclude` filters the sender too, not just recipients

Verified under ledger **FN-7** (2026-08-07). `Rpc.FilterInclude` and `Rpc.FilterExclude`
give you `static IDisposable` scopes wrapped around a connection, a list, or a predicate
(`Scene/Networking/Rpc.cs:187-283`, six overloads total), and they layer on top of a
plain `[Rpc.Broadcast]`. An excluded connection **never receives the message** at all,
since the send loop skips non-recipients outright
(`Systems/Networking/System/NetworkSystem.Send.cs:12-32`); before the local copy of the
body even runs, the RPC layer checks `Filter.IsRecipient( Connection.Local )` and drops
it there too when excluded (`Rpc.InstanceRpc.cs:255` and `:289`, `Rpc.StaticRpc.cs:83`).
In other words: **the caller is bound by its own filter, no exception for it**.

Tested at proximity-chat range: at 72 units, both connections heard each other in both
directions. At 1003 units, the speaker only heard itself, and the host's reply never
reached the client at all. Back down at 0 units, delivery picked back up normally.

```csharp
using ( Rpc.FilterInclude( c => InRange( c, origin, 256f ) ) )
{
    ReceiveSpeech( text );   // [Rpc.Broadcast]: reaches only the included set
}
```

Don't add a second range check inside the RPC body "to be safe". The filter already
applied to the local run, and a redundant check will drop the speaker's own copy along with
everyone else's.

***

## Hand-Authoring a GameResource Asset

The full attribute/serialisation reference is in `15_API_CORE.md` →
*GameResource & `[AssetType]`*. What follows is the mechanical recipe, in order, for
creating a custom resource asset from outside the editor.

**1. Define the type.** `[GameResource(...)]` was deprecated across the whole engine
(`Resources/GameResourceAttribute.cs:82`); turn on `TreatWarningsAsErrors` and it stops
the build cold, which is exactly how this got noticed.

```csharp
[AssetType( Name = "Item Definition", Extension = "item", Category = "MyGame" )]
public sealed class ItemDefinition : GameResource
{
    [Property] public string DisplayName { get; set; } = "Item";
    [Property] public int Value { get; set; }
    [Property] public Model WorldModel { get; set; }   // serialises as a path string
}
```

**2. Write the `.item` file.** It's plain JSON: **keys are PascalCase and match the
property names**, and it needs the envelope the compiler is expecting. Taken straight
from a shipped engine asset (`game/addons/base/Assets/ammo/9mm.ammo`):

```json
{
  "Title": "Pistol Ammo",
  "Icon": null,
  "MaxReserve": 300,
  "__references": [],
  "__version": 0
}
```

`__references` lists whatever other assets this one depends on: leave it `[]` and the
editor populates it for you. `__version` is just the resource's version number. Any
property typed as `Model`, `Material`, or `SoundEvent` gets written out as its **path
string** instead, for example `"WorldModel": "models/citizen_props/crate01.vmdl"`.

**3. Compile it.** The source JSON does nothing on its own; at runtime the engine only
ever loads the compiled `_c` variant (`ResourceLibrary.cs:460-466` tacks `_c` on before
reading). Three ways to get there:

- hand `create_asset` a `type: "item"` and the starting JSON, and it writes the source
  file and compiles it in one go; or
- write the file yourself and follow up with `asset_compile`; or
- call `asset_write` against an existing asset to overwrite its JSON and recompile in the
  same step.

`create_asset` refuses to run if the file is already there (reach for `asset_write`
instead), checks the JSON before it touches disk, and surfaces `IsCompileFailed` on
failure; `read_console` has the reason why when that happens.

**4. Read it back at runtime** through `ResourceLibrary.Get<ItemDefinition>( "path/x.item" )`,
`TryGet`, or by enumerating everything with `ResourceLibrary.GetAll<ItemDefinition>()`.
Store a reference as a `ResourcePath` string rather than an ID:
`Resource.ResourceId` carries `[Obsolete]` (`Resources/Resource.cs:16-17`).

> Hand-editing a compiled `_c` file gets you nowhere, and neither does editing the source
> and hoping it takes effect on its own. There's no reliable watcher on this path either.

***

## Two More Traps Worth Knowing

**`IPressable.Press` executes on whichever client is doing the pressing.**
`PlayerController.UpdateLookAt()` only runs from `OnUpdate` when `if ( !IsProxy )` holds
(`PlayerController.DefaultControls.cs:41-49`), which keeps the entire hover/press
pipeline local to the machine that owns that player. Anything inside `Press` that needs
to be authoritative has to go through an `[Rpc.Host]` instead. See
`01_SCENE.md` → *IPressable* and `13_EXAMPLES.md` → *Example 11*.

**Platform chat runs as its own side-channel, disconnected from whatever UI you build.**
Whenever `ProjectSettings.Platform.ChatEnabled` is true, `Sandbox.Platform.Chat.Say()`
posts to the host regardless of `ChatShowUI` (`Systems/Chat/Chat.cs:38-58`). Turning off
the overlay does nothing to the underlying pipe. A gamemode shipping its own chat has to
explicitly set `ChatEnabled: false` in `ProjectSettings/Platform.config`. See
`05_INPUT_PHYSICS.md` → *Project Settings Configs*.
