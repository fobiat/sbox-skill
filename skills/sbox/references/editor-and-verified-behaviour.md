# Editor Tooling & Live-Verified Behaviour

Everything here was either read out of the engine source at version **26.08.05**
(`sbox-public`, paths below are relative to that repo root) or observed in a live editor
Play session and recorded in the ledger at the foot of this file. Source-read
facts tell you what the API *is*; ledger rows tell you what it *does*. Where the two
disagree, the ledger wins.

Read this file before you drive the editor, before you author a `.item`-style asset by
hand, and before you assume a `[Sync]` value is replicating.

---

## The Editor MCP Server

The s&box editor embeds an MCP server. It operates on **whatever project is currently
open in the editor** — not on your working directory. Nothing you do through it applies
to a project the editor hasn't loaded.

### Entry points

Only a handful of tools are listed up front. The real registry lives in editor and addon
code and changes as code hotloads.

| Tool | Use |
|---|---|
| `editor_status` | Start here. What project is open, is play mode running |
| `list_toolsets` | The registry grouped into named toolsets |
| `describe_toolset` | Full input schemas for one toolset, ready to invoke |
| `search_tools` | Keyword search across the registry |
| `call_tool` / `call_tools` | Invoke one / batch several |
| `read_console` | What the editor logged. Errors and exceptions land here |

Tool failures come back as **results with `isError`**, not protocol errors. Read them.

### Stock toolsets (engine 26.08.05)

| Toolset | Tools |
|---|---|
| `editor` | `editor_status`, `read_console`, `console_command`, `compile_status`, `list_toolsets`, `describe_toolset`, `search_tools`, `call_tool`, `call_tools` |
| `asset` | `asset_search`, `asset_info`, `asset_read`, `asset_write`, `asset_compile`, `asset_dependencies`, `asset_files`, `asset_find_by_file`, `asset_types`, `asset_thumbnail`, `create_asset` |
| `scene` | `list_scenes`, `scene_tree`, `get_game_object`, `find_game_objects`, `create_game_object`, `delete_game_object`, `set_game_object`, `add_component`, `remove_component`, `set_component`, `spawn_model`, `spawn_models`, `scene_trace`, `get_selection`, `set_selection`, `save_scene`, `undo`, `redo`, `get_editor_camera`, `set_editor_camera`, `editor_camera_screenshot`, `camera_screenshot` |
| `component` | `get_component_type` |
| `package` | `find_packages`, `get_package`, `install_package` |
| `play` | `play_start`, `play_stop`, `play_pause` |
| `log` | `log_info`, `log_warning`, `log_error` |

A project can register its own toolset from `Editor/` code with `[McpToolset]` +
`[McpTool]` from a file in its `Editor/` folder. A project-registered toolset looks like
`mygame_dev` exposing `project_rebuild` and `project_reload_config`.

### Registry conventions

- Paging is `limit` / `offset`; out-of-range values clamp rather than error.
- Vectors and angles are **comma strings**, not arrays: position `"x,y,z"`, view angles
  `"pitch,yaw,roll"`.
- Source coordinate system: 1 unit = 1 inch, +x forward, +y left, +z up. Degrees.
- GameObjects and Components are identified by **guid**; assets by the relative path
  `asset_search` returns.
- Every scene-editing tool pushes an undo step.

### Two hard-won workflow facts

Both are ledger rows, both cost a session to find.

**1. Externally edited `.sbproj` / `.config` files are not watched.** (ledger
A-07, live-verified 2026-08-07.) The engine reads the `.sbproj` at editor boot, or writes
it from the in-editor Project Settings page. Nothing watches it for external edits. Edit
`Metadata.Compiler` on disk and the compilers keep building with the stale config
indefinitely — no warning, no rebuild. Recover with a `project_reload_config`-style tool
(`Project.LoadMinimal()` + `Project.UpdateCompiler()`, both engine-internal) or an editor
restart.

**2. After in-process compiler recreation, the source file watchers die.**
(ledger A-08, live-verified 2026-08-07.) Pre-reload, writing a `.cs` file
triggers a compile. Post-reload, new files and content edits to `Code/` and `Editor/`
produced **no build for 15+ seconds** — `NeedsBuild` stayed false. Your edit is simply not
compiled, and `compile_status` truthfully reports the last successful build, so it looks
like everything is fine.

> **Workflow rule:** call `project_rebuild` (a full `RebuildCompilers()`) after *every
> batch* of external source edits, then poll `compile_status` until `IsBuilding` is false
> and check each compiler's `Success`. Never infer "it compiled" from the absence of
> errors.

---

## Verified Networking Behaviour

### `[Sync(SyncFlags.FromHost)]` silently drops client writes

Ledger **A-02**, live-verified 2026-08-07 (twice: Slice 0 on a host-owned object, Slice 2
on a **client-owned** object where the writing client had `IsProxy == false`).

Source chain:

- `Scene/Networking/NetworkObject.DataTable.cs:75` — the control predicate for a synced
  property is `c => isHostSync ? c.IsHost : HasControl( c )`. `FromHost` bypasses the
  ownership branch entirely: control means *host*, owner or not.
- `Scene/Components/Component.Network.cs:64-68` — the generated setter checks
  `dataTable.HasControl( slot )` and, when it fails, `return`s **before**
  `p.Setter?.Invoke( p.Value )`. The backing field is never written.

Observed: the client assigned `9999`; no exception, and the *immediate read-back* already
showed the host's value. Not "reverted a frame later" — never applied at all.

```csharp
// A client calling this sees no error and no effect. Debug by reading the value back.
[Sync( SyncFlags.FromHost )] public int Money { get; set; }
```

There is no "write failed" signal. If a client needs to change host state, route it
through an `[Rpc.Host]` and validate there.

### `NetList<T>` obeys the same rule, one layer down

Every mutating method on `NetList<T>` is guarded:

```csharp
// Scene/Networking/Containers/NetList.cs:470
private bool CanWriteChanges() => !Parent?.IsProxy ?? true;
```

`Add`/`AddRange`/`Insert`/`Clear`/`RemoveAt`/`this[i] = v` all `return` early when it is
false (`NetList.cs:144-249`); `Remove` returns `false`. **No exception, no log line.**
`Parent` is the `NetworkTable.Entry`, whose `IsProxy` is `!HasControl( Connection.Local )`
— so under `FromHost` a non-host mutation is a no-op, exactly like the scalar case.

Ledger **A-04**, live-verified 2026-08-07: a `[Sync(SyncFlags.FromHost)] NetList<Entry>`
where `Entry` is a struct of `Guid` + a `GameResource`-derived reference replicated three
seeded rows to a second client with matching Guids, correctly resolved resource
references, and a deliberately-null reference faithfully null, stable over 5 samples in
15 s. See `networking.md` → *Networked Collections* for the authoring rules that fall out
of this.

### `NetworkMode.Snapshot` does not live-replicate `[Sync]`

Ledger **A-09**, live-verified 2026-08-07 — negatively, then positively.

`GameObject.NetworkMode` defaults to `NetworkMode.Snapshot`
(`Scene/GameObject/GameObject.Network.cs:62`), so **every object you place in a scene
starts this way**. Snapshot means "included in the scene snapshot a joining client
receives" — it does not create a `NetworkObject`, and `[Sync]` properties are only
registered into a network table by `NetworkObject.RegisterPropertiesRecursive`
(`NetworkObject.DataTable.cs:29-55`). No `NetworkObject`, no live sync.

RPCs keep working the whole time: instance RPC dispatch resolves the target through
`Scene.Directory.FindByGuid` / `FindComponentByGuid` (`Rpc.InstanceRpc.cs:30-52`) and
never consults the network object.

Observed: the client read `HostCounter = 0` for **62 straight pings** while the host was
at 117; RPCs routed correctly in both configurations. After the host called
`NetworkSpawn()`, replication was live and lockstep.

> This is the quietest failure in the engine: your RPCs work, your logs look healthy, and
> your state is frozen at its initial value. If a `[Sync]` value looks stale, check
> `IsNetworkRoot` on the object or an ancestor **first**.

### `NetworkSpawn()` with no arguments gives ownership to the caller

```csharp
// Scene/GameObject/GameObject.Network.cs:125
public bool NetworkSpawn() => NetworkSpawn( Connection.Local );
```

On the host that is `Connection.Host`, which is usually what you wanted — but the
parameterless form encodes *whoever happens to call it*, so the same line spawns a
client-owned object the moment it runs from client code, and a host-owned one otherwise.
For a world object nobody should control, and for a player object that must belong to a
specific connection, always be explicit:

```csharp
prop.NetworkSpawn( Connection.Host );        // world object, host-authoritative
player.NetworkSpawn( connection );           // this connection's pawn
```

Ledger **A-10**, live-verified 2026-08-07: a host-created object `NetworkSpawn(connection)`d
is owned by that client — each client had `IsProxy == false` on its own player and
`IsProxy == true` on the other's. `NetworkSpawn( NetworkSpawnOptions )` at
`GameObject.Network.cs:131-181` is the full form (`Owner`, `StartEnabled`, `OwnerTransfer`,
`OrphanedMode`, `AlwaysTransmit`, `Flags`).

### `Rpc.FilterInclude` narrows sender-side, and binds the caller too

Ledger **A-12**, live-verified 2026-08-07. `Rpc.FilterInclude` / `Rpc.FilterExclude` are
`static IDisposable` scopes over a connection, list or predicate
(`Scene/Networking/Rpc.cs:187-283`, six overloads) that compose with a plain
`[Rpc.Broadcast]`. The send loop skips non-recipients, so an excluded connection is
**never sent the message** (`Systems/Networking/System/NetworkSystem.Send.cs:12-32`), and
before running the body locally the RPC layer checks
`Filter.IsRecipient( Connection.Local )` and skips it when excluded
(`Rpc.InstanceRpc.cs:255` and `:289`, `Rpc.StaticRpc.cs:83`) — **the caller obeys its own
filter**.

Observed at proximity-chat range: at distance 72, both connections received both
directions; at distance 1003 the speaker's message reached only itself, and the host's
reverse message never appeared client-side; back at distance 0, delivery resumed.

```csharp
using ( Rpc.FilterInclude( c => InRange( c, origin, 256f ) ) )
{
    ReceiveSpeech( text );   // [Rpc.Broadcast] — reaches only the included set
}
```

Do not add a second range check inside the RPC body "to be safe" — the filter already
applied to the local run, and a redundant check will drop the speaker's own copy.

---

## Authoring a GameResource Asset by Hand

The full attribute/serialisation reference is in `api-schema-core.md` →
*GameResource & `[AssetType]`*. This is the mechanical recipe, in order, for creating a
custom resource asset from outside the editor.

**1. Define the type.** `[GameResource(...)]` is obsolete engine-wide
(`Resources/GameResourceAttribute.cs:82`) — with `TreatWarningsAsErrors` it is a hard
build failure, which is how it was found.

```csharp
[AssetType( Name = "Item Definition", Extension = "item", Category = "MyGame" )]
public sealed class ItemDefinition : GameResource
{
    [Property] public string DisplayName { get; set; } = "Item";
    [Property] public int Value { get; set; }
    [Property] public Model WorldModel { get; set; }   // serialises as a path string
}
```

**2. Write the `.item` file.** Plain JSON, **PascalCase keys matching the property
names**, plus the envelope the compiler expects. Real engine example
(`game/addons/base/Assets/ammo/9mm.ammo`):

```json
{
  "Title": "Pistol Ammo",
  "Icon": null,
  "MaxReserve": 300,
  "__references": [],
  "__version": 0
}
```

`__references` is the list of other assets this one depends on (leave `[]` and let the
editor fill it), `__version` the resource version. A `Model`/`Material`/`SoundEvent`
-typed property is written as its **path string**, e.g. `"WorldModel":
"models/citizen_props/crate01.vmdl"`.

**3. Compile it.** Source JSON is inert; the runtime only ever loads the compiled `_c`
(`ResourceLibrary.cs:460-466` appends `_c` before reading). Either:

- `create_asset` with `type: "item"` and the starting JSON — creates the source file and
  compiles it; or
- write the file yourself and call `asset_compile`; or
- `asset_write` to overwrite an existing asset's JSON and recompile in one step.

`create_asset` fails if the file already exists (use `asset_write`), validates the JSON
before touching disk, and reports `IsCompileFailed` — when it does, `read_console` says
why.

**4. Read it back at runtime** with `ResourceLibrary.Get<ItemDefinition>( "path/x.item" )`
/ `TryGet`, or enumerate with `ResourceLibrary.GetAll<ItemDefinition>()`. Persist a
reference as `ResourcePath` (a string) — `Resource.ResourceId` is `[Obsolete]`
(`Resources/Resource.cs:16-17`).

> Do not hand-edit a compiled `_c` file, and do not expect an edited source file to take
> effect without a compile. There is no watcher you can rely on here either.

---

## Two Traps Worth Repeating

**`IPressable.Press` runs on the pressing client.** `PlayerController.UpdateLookAt()` is
called from `OnUpdate` only inside `if ( !IsProxy )`
(`PlayerController.DefaultControls.cs:41-49`), so the whole hover/press pipeline is local
to the machine that owns the player. Anything authoritative inside `Press` must go through
an `[Rpc.Host]`. See `core-concepts.md` → *IPressable* and
`patterns-and-examples.md` → *Example 11*.

**Platform chat is a global side-channel independent of your UI.**
`Sandbox.Platform.Chat.Say()` posts to the host whenever
`ProjectSettings.Platform.ChatEnabled` is true, regardless of `ChatShowUI`
(`Systems/Chat/Chat.cs:38-58`). Hiding the overlay does not disable the pipe. A gamemode
with its own chat must set `ChatEnabled: false` in `ProjectSettings/Platform.config`. See
`input-and-physics.md` → *Project Settings Configs*.
