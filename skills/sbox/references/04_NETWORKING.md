<!--
  s&box Skill : 04_NETWORKING.md

  The networking model: lobbies, ownership, [Sync] state, RPCs and network events.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Multiplayer

This page maps RPCs, [Sync] state, ownership and authority, host versus client behavior, replication, lobbies, and dedicated-server setup. Every claim below was pulled from the **26.08.05** engine source (`sbox-public`); a few of the trickier ones were also verified live, and those are marked inline against ledger rows.

***

## Overview

The networking model in s&box stays deliberately simple: whoever owns a networked object is authoritative for it by default, driving its position, rotation, and its [Sync] properties. An object with no owner falls back to the host, which simulates it in the owner's place.

Five words recur throughout this document, worth nailing down before anything else:

- **Host**: whichever machine is actually running the game world, be that a singleplayer session, a lobby host, or a dedicated server.
- **Client**: any connected player besides the host.
- **Owner**: whichever connection is responsible for simulating a given networked object.
- **Proxy**: a networked object that belongs to someone else (`IsProxy == true`).
- **NetworkMode**: the setting controlling whether and how a GameObject is networked at all.

***

## Lobby & Connection

### Standing Up a Lobby

```csharp
Networking.CreateLobby( new LobbyConfig
{
    MaxPlayers = 8,
    Privacy = LobbyPrivacy.Public,
    Name = "My Game"
} );
```

`LobbyConfig` exposes: `MaxPlayers`, `Privacy` (`Public`/`Private`/`FriendsOnly`), `Name`, `Hidden`, `DestroyWhenHostLeaves`, and `AutoSwitchToBestHost`.

### Finding and Joining a Lobby

```csharp
// List all lobbies for this game
var lobbies = await Networking.QueryLobbies();

// Join by lobby ID
Networking.Connect( lobby.LobbyId );

// Join best available lobby
await Networking.JoinBestLobby( gameIdent );

// Disconnect
Networking.Disconnect();
```

`LobbyInformation` carries: `LobbyId`, `OwnerId`, `Members`, `MaxMembers`, `Name`, `Map`, `Game`, `IsFull`, `IsHidden`.

### The Connection Type

```csharp
Connection.Local       // your own connection
Connection.All         // all connected players (IReadOnlyList<Connection>)
Connection.Host        // the host connection
Connection.Find( id )  // find by Guid
```

| Property | Type | What it tells you |
|----------|------|--------------------|
| `Id` | `Guid` | A stable identifier for the connection |
| `DisplayName` | `string` | The player's shown name |
| `SteamId` | `SteamId` | Their Steam ID |
| `IsHost` | `bool` | Whether this connection is the host |
| `IsActive` | `bool` | Set once the connection is fully established |
| `IsConnecting` | `bool` | Set while the connection is still being established |
| `Ping` | `float` | Round-trip latency, in milliseconds |
| `CanSpawnObjects` | `bool` | Whether this connection may spawn networked objects (true by default) |
| `CanRefreshObjects` | `bool` | Whether this connection may refresh objects it owns |

`Connection` also exposes a few methods worth remembering:
- `Kick( string reason )`: host-only.
- `HasPermission( string permission )`: returns a `bool`.
- `Down/Pressed/Released( string action )`: reads a remote player's input state, host-only.

### Global Networking State

| Property | What It Tells You |
|---|---|
| `Networking.IsHost` | True when you're the host, or when not connected at all |
| `Networking.IsClient` | True when connected but not the host |
| `Networking.IsActive` | True while a connection is live |
| `Networking.IsConnecting` | True while a connection attempt is in progress |
| `Networking.ServerName` | The server's name |
| `Networking.MaxPlayers` | The configured player cap |

***

## Networked Objects

### NetworkMode

Configurable either in the inspector or from code, this setting decides whether a GameObject is networked at all, and how.

| Mode | Behavior |
|------|----------|
| `NetworkMode.Never` | Not networked, period |
| `NetworkMode.Object` | A full networked object, with an owner, [Sync] properties, and RPCs |
| `NetworkMode.Snapshot` **(default)** | Included in the initial scene snapshot handed to a joining client |

> **A `Snapshot`-mode object never live-replicates its `[Sync]` state.** Every object dropped
> into a scene gets this mode by default (`GameObject.Network.cs:62`), which makes it the
> engine's most easily missed trap. A `[Sync]` property only becomes part of a network table
> once a `NetworkObject` exists for it, and a `NetworkObject` doesn't exist until the object
> has gone through `NetworkSpawn`. Yet **RPCs carry on regardless**: instance RPC dispatch
> looks up its target through `Scene.Directory.FindByGuid` and never touches the network
> object at all. The result is deceptive: RPCs fire, logs show nothing wrong, and the
> `[Sync]` value just sits frozen at whatever it started as. Live testing caught this
> directly, a client stuck reading `0` for 62 consecutive pings while the host had already
> reached 117. Treat a stale `[Sync]` value as a cue to check `IsNetworkRoot` on the object,
> or on an ancestor, before looking anywhere else.
>
> A `Snapshot` object nested under a networked root is unaffected, since the root's table
> reaches down into it (`NetworkObject.DataTable.cs:47-54`). The failure is specific to a
> top-level Snapshot object with no networked ancestor above it.

### Spawning an Object Onto the Network

```csharp
// Create and network-spawn a prefab
var go = PlayerPrefab.Clone( spawnPoint.WorldPosition );
go.NetworkSpawn( connection );        // specific owner, prefer this, always
go.NetworkSpawn( Connection.Host );   // world object nobody should control
go.NetworkSpawn();                    // Connection.Local, see the warning below
go.NetworkSpawn( new NetworkSpawnOptions
{
    Owner = connection,
    OrphanedMode = NetworkOrphaned.Host,
    OwnerTransfer = OwnerTransfer.Takeover,
    AlwaysTransmit = true
} );
```

> **Call `NetworkSpawn()` with no arguments and ownership defaults to `Connection.Local`.**
> The implementation is literally a forward: `NetworkSpawn() => NetworkSpawn( Connection.Local )`
> (`GameObject.Network.cs:125`). Ownership therefore tracks whoever happens to execute that
> line, host-owned if it runs on the host, client-owned if the same code path runs on a
> client. That's a silent trap for anything meant to be host-authoritative: the object
> spawns fine, replicates fine, and then quietly refuses the host's own writes because
> ownership landed on a client instead. Name the owner explicitly every time:
> `NetworkSpawn( Connection.Host )` or `NetworkSpawn( connection )`.

The full-featured overload, `NetworkSpawn( NetworkSpawnOptions )`
(`GameObject.Network.cs:131-181`), declines to run in four situations: on a `PrefabScene`, in
the editor scene, on an object that's already spawned, or when
`Connection.Local.CanSpawnObjects` is false, and that last case also logs a warning. None of
the four throw; all four just return `false`, so check the return value rather than assuming
the call succeeded.

### The Data Table Is Built Once, and `Network.Refresh` Rebuilds It

Structural changes made after `NetworkSpawn()`, adding a component, reparenting, and so on,
don't propagate automatically. This is not a caching quirk; it is the design.

`NetworkObject.CreateDataTable()` disposes the old table, makes a new one, and calls
`RegisterPropertiesRecursive( GameObject )` (`NetworkObject.DataTable.cs:21-27`). That walk
enumerates `go.Components.GetAll()` **at that instant** (`:37-43`) and registers one table entry
per `[Sync]` property it finds. It runs at spawn and nowhere else on its own.

> **A component added after the object was spawned has no table entries and never replicates.**
> Its `[Sync]` properties compile, assign, and read back locally. Nothing warns you.

The cure is `Network.Refresh`, which re-runs registration and then sends a refresh snapshot.
Three overloads, all in `GameObject.Network.cs:733-789`:

```csharp
go.Network.Refresh();                  // whole object: re-register, re-serialize, resend
go.Network.Refresh( descendantGo );    // one GameObject in the hierarchy
go.Network.Refresh( component );       // one Component
```

All three carry the same two limits, and both fail quietly-ish:

- `if ( !Active || (IsProxy && !Networking.IsHost) ) return;`, a proxy that is not the host
  cannot refresh at all, silently.
- `if ( !connection.CanRefreshObjects )` logs `"{go} is trying to refresh - but we don't have
  permission!"` and returns. `Connection.CanRefreshObjects` is `IsHost || Info?.CanRefreshObjects`
  (`Connection.cs:137-140`), seeded from `ProjectSettings.Networking.ClientsCanRefreshObjects`,
  which defaults true (`NetworkingSettings.cs:33`). Turn that setting off and client refreshes
  stop working.

**The same call is needed for a plain `Component.Enabled` write.** The delta snapshot carries
exactly five fixed slots: position, rotation, scale, interpolation and `GameObject.Enabled`. A
component's `Enabled` is not among them, and the whole block is authored only where `IsProxy`
is false (`NetworkObject.cs:634-655`):

```csharp
if ( !IsProxy )
{
    ...
    LocalSnapshotState.AddCached( _snapshotCache, SnapshotEnabledSlot, GameObject.Enabled );
}
```

So on a **client-owned** object, the host toggling a renderer off sees it happen locally and
nobody else does: the host is a proxy for that object, so it authors no snapshot block, and the
component's `Enabled` was never a snapshot field in the first place. Follow the write with
`go.Network.Refresh( theComponent )`.

> **Counter-warning: do not reach for the whole-object `Refresh()` to fix a tag.** A refresh
> from the host re-serializes the host's copy of the object over the owner's. `OnRefreshMessage`
> only protects the receiver when the sender is *not* the host: it strips `NetworkFlags` and
> calls `PreserveFromHostSyncMembers` inside `if ( !source.IsHost )`
> (`NetworkObject.cs:769-786`). From the host, nothing is preserved, so a host `Refresh()` on a
> client-owned `PlayerController` stomps the owner's own state back to the host's stale copy.
> Derive the tag from replicated state instead: a `[Sync]` value plus an `OnUpdate` that applies
> `Tags.Set( "x", value )` locally on every machine.

### Tearing an Object Down

```csharp
go.Destroy();  // works for networked objects too
```

***

## [Sync] Properties

A synced property's value travels automatically from its owner out to every client. Nobody but the owner can change it, unless the property carries `SyncFlags.FromHost`.

```csharp
public sealed class PlayerStats : Component
{
    [Sync] public int Kills { get; set; }
    [Sync] public string PlayerName { get; set; }
    [Sync] public Vector3 AimDirection { get; set; }
    [Sync( SyncFlags.FromHost )] public int TeamId { get; set; }     // only host can change
    [Sync( SyncFlags.Interpolate )] public float Health { get; set; } // smoothly interpolated
}
```

### What Can Be Synced

The set covers unmanaged value types (`int`, `bool`, `float`, `Vector3`, `Rotation`, `Transform`, `Color`, `Angles`, and similar), plus `string`, `GameObject`, `Component`, `GameResource`, and any struct built entirely from supported types.

### SyncFlags

| Flag | Effect |
|------|--------|
| `SyncFlags.FromHost` | Value is controlled by the host rather than the object's owner |
| `SyncFlags.Query` | Polls for changes every tick; needed when the backing field is written outside its setter |
| `SyncFlags.Interpolate` | Interpolates smoothly between ticks; supported for `float`, `double`, `Angles`, `Rotation`, `Transform`, `Vector3` |

> **An unauthorized write vanishes silently before it ever reaches the backing field.**
> The generated setter checks `dataTable.HasControl( slot )` first and simply `return`s if
> that fails, never touching the real setter (`Component.Network.cs:64-68`). There's no
> exception and no warning, and because the very next read already returns the authoritative
> value, the failure doesn't present as a revert, it looks exactly like the write never
> happened at all. Under `FromHost` the control check is literally `c => c.IsHost`
> (`NetworkObject.DataTable.cs:75`), which overrides ownership entirely: even a client that
> owns the object can't write to a `FromHost` property. Confirmed live on two separate
> occasions (ledger FN-1). A client that needs host state changed should send an
> `[Rpc.Host]` call and let the host validate and apply it.

**One level above the per-property gate, the receiver drops the entire message.** Before any
table entry is consulted, `SceneNetworkSystem.OnNetworkTableChanges` checks the sender against
the object's owner (`SceneNetworkSystem.cs:1216-1219`):

```csharp
// Can we receive network table changes from this source?
if ( !source.IsHost && source.Id != obj._net.Owner )
    return;
```

Two things follow, and both are easy to get backwards:

- **The whole `ObjectNetworkTableMsg` is discarded, not the offending field.** A client that
  smuggles one bad property into an otherwise legitimate update loses the update, not just the
  property. If a batch of `[Sync]` writes "sometimes doesn't arrive", suspect this and not the
  individual property.
- **The authority here is ownership, not `SyncFlags`.** A plain `[Sync]` on an object nobody
  owns is already safe against client writes, because an unowned object's `HasControl` is
  `c.IsHost` (`NetworkObject.cs:893-901`) and the message-level check rejects any non-host
  sender outright. `SyncFlags.FromHost` is what you need on an object a **client** owns and the
  host must still control; it buys you nothing on a host-owned or unowned one.

### Reacting to Changes

```csharp
[Sync, Change( "OnHealthChanged" )]
public float Health { get; set; } = 100f;

void OnHealthChanged( float oldValue, float newValue )
{
    Log.Info( $"Health changed from {oldValue} to {newValue}" );
}
```

### Syncing Lists and Dictionaries

```csharp
[Sync] public NetList<int> Inventory { get; set; } = new();
[Sync] public NetDictionary<string, int> AmmoCount { get; set; } = new();
```

`NetList<T>` and `NetDictionary<K,V>` behave like the ordinary collections they mirror, `Add`, `Remove`, indexers, and the rest, but only the delta crosses the wire, never a full copy of the collection. Neither one supports `[Property]`.

Four rules apply to both, and every one of them fails quietly if broken.

**1. You catch changes through a field, not the `[Change]` attribute.**

```csharp
[Sync( SyncFlags.FromHost )] public NetList<ItemEntry> Items { get; set; } = new();

protected override void OnStart()
{
    Items.OnChanged += e => Log.Info( $"{e.Type} at {e.Index}: {e.OldValue} -> {e.NewValue}" );
}
```

The collection exposes `OnChanged` as a plain public `Action<NetListChangeEvent<T>>` field
(`Containers/NetList.cs:56`; `NetDictionary` mirrors it). **`[Change]` does nothing here.**
It's implemented as a `WrapPropertySet` code generator (`ChangeAttribute.cs`), which only
triggers when the whole list gets reassigned, not when a single element is added, removed, or
replaced. `NetListChangeEvent<T>` itself carries `Type` (a `NotifyCollectionChangedAction`),
`Index`, `MovedIndex`, `NewValue`, and `OldValue` (`NetList.cs:11-18`).

**2. Set it up once, in the property initializer, and leave it alone after spawn.** A
one-shot call, `INetworkProperty.Init( slot, parent )`, guarded by
`if ( !entry.Initialized )` (`NetworkTable.cs:170-174`), is what wires the collection to its
network table entry. Assign a new `new()` after spawn and the replacement never picks up a
`Parent`, so it also loses the proxy protection described in rule 3 and starts accepting
writes it ought to reject. Clearing a list means calling `Clear()`, not replacing it outright.

**3. Anyone who isn't the controller has their mutations dropped without a sound.** Every
mutating method checks `CanWriteChanges()` first, which resolves to
`!Parent?.IsProxy ?? true` (`NetList.cs:470`). On failure, `Add`, `AddRange`, `Insert`,
`Clear`, `RemoveAt`, and `this[i] = v` all just `return` early (`NetList.cs:144-249`), and
`Remove` returns `false`. Neither an exception nor a log line follows. With
`SyncFlags.FromHost`, "controller" narrows to the host specifically, since the entry's
control predicate is `c => isHostSync ? c.IsHost : HasControl( c )`
(`NetworkObject.DataTable.cs:75`), meaning every mutation attempted by a client is a silent
no-op there too. Send client intent through an `[Rpc.Host]` rather than mutating the
collection directly.

**4. A struct element is free to hold references, `GameResource`, `GameObject`, and
`Component` included.** Elements pass through `Game.TypeLibrary.ToBytes`/`FromBytes`
(`NetList.cs:459-468`). One made entirely of primitives (a `Guid` still counts) gets written
as raw POD; one holding a reference doesn't (`SandboxedUnsafe.IsAcceptablePod` walks its
fields recursively, `:12-23` and `:55-67`), so it drops to a reflection-based packer that
serializes each field individually (`TypeLibrary/Serializer.cs:63-88`), and any reference
field implementing `BytePack.ISerializer` writes a compact handle in its place, a 64-bit
path hash for a `Resource` (`Resource.cs:171-193`), or a guid for a `GameObject` or
`Component`.

```csharp
public struct ItemEntry              // legal as a NetList<T> element
{
    public Guid Id { get; set; }
    public ItemDefinition Definition { get; set; }   // GameResource reference
    public int Count { get; set; }
}
```

Live verification (ledger FN-2, 2026-08-07) confirmed it: three seeded entries built to
this exact shape replicated cleanly to a second client, Guids matched, resource references
resolved correctly, and a reference deliberately left null stayed null on the other end.
Class-type reference fields need a public parameterless constructor and public settable
properties to work at all; a cycle among them throws.

**5. Mutating a copy of a struct element changes nothing.** `list[i]` returns a copy, so
`list[i].Count = 3` does not compile and `var e = list[i]; e.Count = 3;` compiles and does
nothing. Assign back through the indexer, which is a real `Replace` on the underlying
`ObservableCollection<T>` and does replicate:

```csharp
var e = Items[i];
e.Count = 3;
Items[i] = e;      // this is the edit
```

**6. `NetDictionary<string, V>` is case-sensitive.** It wraps an `ObservableDictionary<K,V>`
(`NetDictionary.cs:56`) whose backing store is `new Dictionary<TKey, TValue>()` with the default
comparer (`ObservableDictionary.cs:30`). `"Rifle"` and `"rifle"` are two keys. Normalize before
you insert or look up, and do it in one helper so both sides agree.

### A `[Sync] string` Snapshot Versus a `NetList`

`NetList` and `NetDictionary` are the right tool for a **hot collection mutated element-wise**:
a projectile pool, a per-tick score table, anything where a single `Add` per frame is the
traffic and sending the whole collection each time would hurt.

They are the wrong tool, and the more common case in a real gamemode, for a collection that is
**read whole and rebuilt when it changes**: a roster, a shop catalog, a scoreboard, a queue, a
party list. For those, sync one versioned string:

```csharp
[Sync( SyncFlags.FromHost )] public string RosterJson { get; set; } = "";

public sealed class RosterDoc
{
    public const int CurrentVersion = 2;
    public int Version { get; set; } = CurrentVersion;
    public List<RosterRow> Rows { get; set; } = new();
}

// Host, on any change:
RosterJson = Json.Serialize( doc );

// Everyone, on read:
var doc = RosterDoc.Decode( RosterJson );   // null for blank / malformed / unknown
```

Why this wins for the read-whole case:

- **It versions.** The envelope carries its own `Version`, so a row gained or a field renamed is
  a migration step, not a wire break. `07_SERVICES.md` → *Versioned Save Documents* is the same
  `Encode`/`Decode` pair, so the save file and the replicated copy stay one format.
- **It survives a rolling update.** A client on the previous build decodes a newer document by
  clamping it down and reading the fields it knows. A `NetList<T>` whose `T` gained a field
  fails at the element serializer, and the failure is per-element and hard to see.
- **It has no per-element replication semantics to get wrong.** All six rules above stop
  applying: nothing to initialize once, no `Parent`, no `OnChanged` wiring, no proxy gate per
  method, no struct-copy trap, no case-sensitivity surprise. It is one property with one owner.
- **`[Change]` works on it**, because the whole property really is reassigned. That is the
  natural "rebuild my UI" hook, and it is exactly the hook `[Change]` does *not* give you on a
  `NetList`.

The cost is that every change resends the whole document. That is the trade you are making
deliberately: it is fine for a 40-player roster that changes on join and leave, and wrong for a
list that changes every tick.

Rule of thumb: **if the reader always wants the whole thing, sync the whole thing.** Reserve
`NetList` for collections whose mutations outnumber their full reads.

***

## RPC Messages

Calling an RPC method runs it on remote machines too, not just locally. They can be declared on Components or on static classes.

### [Rpc.Broadcast]

Runs on every client and the host alike:

```csharp
[Rpc.Broadcast]
public void PlayHitEffect( Vector3 position, Vector3 normal )
{
    // Runs on everyone
    var effect = HitEffectPrefab.Clone( position );
    effect.WorldRotation = Rotation.LookAt( normal );
}
```

### [Rpc.Host]

Executes on the **host**, and nowhere else:

```csharp
[Rpc.Host]
public void RequestSpawn()
{
    // Only runs on host, safe for authoritative logic
    var player = PlayerPrefab.Clone( GetSpawnPoint() );
    player.NetworkSpawn( Rpc.Caller );
}
```

### [Rpc.Owner]

Executes on whichever connection **owns** the object, falling back to the host if there's no owner:

```csharp
[Rpc.Owner]
public void NotifyHit( float damage )
{
    // Only runs on the object's owner
    Health -= damage;
}
```

### RPCs on Static Methods

```csharp
[Rpc.Broadcast]
public static void AnnounceMessage( string message )
{
    Log.Info( message );
}
```

### NetFlags

Flags passed alongside the attribute tune how the call is delivered:

```csharp
[Rpc.Broadcast( NetFlags.Unreliable )]
public void UpdatePosition( Vector3 pos ) { }

[Rpc.Host( NetFlags.OwnerOnly )]  // only owner can call this
public void DealDamage( float amount ) { }
```

| Flag | Description |
|------|-------------|
| `NetFlags.Reliable` | **Default.** Delivery is guaranteed; use it for anything that matters. |
| `NetFlags.Unreliable` | Cheap and fast, but may arrive late, out of order, or not at all. Good for effects and position updates. |
| `NetFlags.SendImmediate` | Skips batching and goes out immediately. Meant for voice streaming. |
| `NetFlags.DiscardOnDelay` | Dropped rather than delayed if it can't send promptly. Unreliable only. |
| `NetFlags.HostOnly` | Restricts the call to the host. |
| `NetFlags.OwnerOnly` | Restricts the call to the object's owner. |

### Narrowing Who Receives It

Trim which connections actually receive a Broadcast RPC:

```csharp
// Exclude specific connections
using ( Rpc.FilterExclude( c => c.DisplayName == "Harry" ) )
{
    PlayEffect();
}

// Include only specific connections
using ( Rpc.FilterInclude( targetConnection ) )
{
    SendPrivateMessage( message );
}
```

### Identifying the Caller

From inside an RPC body, you can inspect who made the call:

```csharp
[Rpc.Broadcast]
public void SendChatMessage( string message )
{
    if ( Rpc.Calling )  // true if called from remote connection
    {
        Log.Info( $"{Rpc.Caller.DisplayName}: {message}" );
    }
}
```

`Rpc.Caller` gives you the calling `Connection`; `Rpc.CallerId` gives just their `Guid`. `Rpc.Calling` reads `true` whenever the method was triggered remotely.

### What Arguments an RPC Can Take

The same set as [Sync] properties: unmanaged types, `string`, `GameObject`, `Component`, `GameResource`.

***

## Ownership

### Determining the Controller

| Situation | Controller |
|-----------|-----------|
| A scene object with no owner named explicitly | Host |
| `NetworkSpawn()` by a client | That calling client becomes the owner |
| `NetworkSpawn( connection )` | Whichever connection was named |
| Owner disconnects | Determined by the `NetworkOrphaned` mode in effect |

### Checking IsProxy Before You Simulate

This is the one ownership check that matters most: bail out of simulation for anything you don't own.

```csharp
protected override void OnUpdate()
{
    if ( IsProxy ) return;  // someone else controls this

    // Your movement/input code here
    HandleInput();
}
```

### Transferring Ownership

```csharp
go.Network.TakeOwnership();                // become the owner
go.Network.DropOwnership();                // release to host
go.Network.AssignOwnership( connection );  // give to specific client (host only by default)
```

### OwnerTransfer Mode

Governs who's allowed to change ownership at all:

```csharp
go.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
```

| Mode | Who Can Transfer |
|------|------------------|
| `OwnerTransfer.Fixed` **(default)** | Restricted to the host |
| `OwnerTransfer.Takeover` | Open to anyone |
| `OwnerTransfer.Request` | Requires asking the host first |

### Handling a Disconnected Owner

Determines the fallback the moment an owner drops off:

```csharp
go.Network.SetOrphanedMode( NetworkOrphaned.Host );
```

| Mode | What Happens |
|---|---|
| `NetworkOrphaned.Destroy` **(default)** | The object is destroyed |
| `NetworkOrphaned.Host` | Ownership passes to the host |
| `NetworkOrphaned.Random` | Ownership passes to a random client |
| `NetworkOrphaned.ClearOwner` | Ownership clears and the host simulates it instead |

### Network Accessor (go.Network)

| Property | Type | Meaning |
|---|---|---|
| `Active` | `bool` | Whether this object is networked at all |
| `IsOwner` | `bool` | Whether we're the owner |
| `Owner` | `Connection` | The owning connection, or null if there isn't one |
| `OwnerId` | `Guid` | The owner's connection identifier |
| `IsProxy` | `bool` | Whether someone else controls it |
| `IsCreator` | `bool` | Whether we're the one who created this object |
| `OwnerTransfer` | `OwnerTransfer` | Who is permitted to transfer ownership |
| `NetworkOrphaned` | `NetworkOrphaned` | The configured disconnect behavior |
| `AlwaysTransmit` | `bool` | Whether updates always send, regardless of visibility (true by default) |
| `Interpolation` | `bool` | Whether transform interpolation is smoothed |
| `Flags` | `NetworkFlags` | Any additional flags set |

Worth knowing on the same accessor:
- `TakeOwnership()`: returns `bool`.
- `AssignOwnership( Connection )`: returns `bool`.
- `DropOwnership()`: returns `bool`.
- `Refresh()`: pushes structural changes out to clients.
- `Refresh( Component )`: refreshes just the one component.
- `ClearInterpolation()`: snaps straight to position, for teleporting.
- `SetOwnerTransfer( OwnerTransfer )`
- `SetOrphanedMode( NetworkOrphaned )`

### NetworkFlags

| Flag | What It Does |
|---|---|
| `NetworkFlags.NoInterpolation` | Turns off transform interpolation |
| `NetworkFlags.NoPositionSync` | Leaves position out of sync |
| `NetworkFlags.NoRotationSync` | Leaves rotation out of sync |
| `NetworkFlags.NoScaleSync` | Leaves scale out of sync |
| `NetworkFlags.NoTransformSync` | Excludes the transform entirely |

***

## Smoothing Networked Movement

By default, a networked transform interpolates smoothly between updates. Teleporting requires clearing that interpolation explicitly, or the object will visibly slide into its new position on every other client:

```csharp
WorldPosition = newPosition;
Network.ClearInterpolation();  // snap immediately for all clients
```

To turn interpolation off altogether:

```csharp
go.Network.Interpolation = false;
```

***

## Network Events

### INetworkListener (Host Only)

This is how you respond to players joining and leaving. Implement it on any Component sitting in the scene.

```csharp
public sealed class GameManager : Component, Component.INetworkListener
{
    [Property] public GameObject PlayerPrefab { get; set; }

    public void OnActive( Connection connection )
    {
        // Player fully loaded, spawn their character
        var player = PlayerPrefab.Clone( GetSpawnPoint() );
        player.NetworkSpawn( connection );
    }

    public void OnDisconnected( Connection connection )
    {
        Log.Info( $"{connection.DisplayName} left" );
    }

    public bool AcceptConnection( Connection connection, ref string reason )
    {
        if ( IsBanned( connection ) )
        {
            reason = "You are banned";
            return false;
        }
        return true;
    }
}
```

| Method | When Called |
|--------|-----------|
| `AcceptConnection( Connection, ref string reason )` | Runs on the host to accept or deny a connection; return false to reject it |
| `OnConnected( Connection )` | Fires once the client connects, while it's still loading |
| `OnActive( Connection )` | Fires once the client has fully loaded and is entering the game |
| `OnDisconnected( Connection )` | Fires when the client leaves |
| `OnBecameHost( Connection previousHost )` | Fires when you've just become host because the previous one left |

### INetworkSpawn

Fires whenever an ancestor `GameObject` gets network-spawned:

```csharp
public sealed class WeaponSetup : Component, Component.INetworkSpawn
{
    public void OnNetworkSpawn( Connection owner )
    {
        // Initialize weapon for the new owner
    }
}
```

### IGameObjectNetworkEvents

Delivers ownership-change events scoped to one specific GameObject:

```csharp
public sealed class OwnerTracker : Component, IGameObjectNetworkEvents
{
    void IGameObjectNetworkEvents.NetworkOwnerChanged( Connection newOwner, Connection previousOwner ) { }
    void IGameObjectNetworkEvents.StartControl() { }   // we just became controller (no longer proxy)
    void IGameObjectNetworkEvents.StopControl() { }    // we just became proxy
}
```

### INetworkSnapshot (Custom Snapshot Data)

Use this to hand custom data, voxel data, world state, whatever, to a client as it joins:

```csharp
public sealed class VoxelWorld : Component, Component.INetworkSnapshot
{
    byte[] VoxelData;

    void INetworkSnapshot.WriteSnapshot( ref ByteStream writer )
    {
        writer.Write( VoxelData.Length );
        writer.WriteArray( VoxelData );
    }

    void INetworkSnapshot.ReadSnapshot( ref ByteStream reader )
    {
        var length = reader.Read<int>();
        VoxelData = reader.ReadArray<byte>( length ).ToArray();
    }
}
```

***

## Culling What Gets Sent

Every networked object transmits to every connection by default. Bigger games should turn off `AlwaysTransmit` and implement `INetworkVisible` in its place:

```csharp
public sealed class DistanceCulling : Component, Component.INetworkVisible
{
    public bool IsVisibleToConnection( Connection connection, in BBox worldBounds )
    {
        return connection.DistanceSquared( WorldPosition ) < 5000f * 5000f;
    }
}
```

Culling an object stops its sync vars and transform from updating, though the object itself remains present on the client, just disabled. RPCs keep arriving regardless.

A Hammer map with VIS compiled falls back to PVS automatically.

***

## The NetworkHelper Shortcut

A ready-made component that covers basic multiplayer setup with no custom code:

```csharp
// Add to a GameObject in your scene
// Set PlayerPrefab in inspector
// Set StartServer = true
// Optionally add SpawnPoint components to the scene
```

| Property | What It Does |
|---|---|
| `StartServer` | Creates a lobby automatically when the scene loads |
| `PlayerPrefab` | The prefab spawned for each connecting player |
| `SpawnPoints` | A list of spawn locations, chosen from at random |

Under the hood it spawns and assigns player prefabs through `INetworkListener.OnActive`, which makes it a decent reference if you end up writing your own version.

***

## Scene Startup (ISceneStartup)

This is where host-side game initialization belongs, and it's best implemented on a `GameObjectSystem`:

```csharp
public sealed class GameManager : GameObjectSystem<GameManager>, ISceneStartup
{
    public GameManager( Scene scene ) : base( scene ) { }

    void ISceneStartup.OnHostPreInitialize( SceneFile scene )
    {
        // Before scene loads (host only), scene is empty
    }

    void ISceneStartup.OnHostInitialize()
    {
        // After scene loads (host only), spawn cameras, start lobby
        Networking.CreateLobby();
    }

    void ISceneStartup.OnClientInitialize()
    {
        // After scene loads (host + client, NOT dedicated server)
        // Spawn client-side only objects (mark as not networked!)
    }
}
```

***

## Dedicated Servers

### Code That Only Exists on the Server

Host-only code belongs behind `#if SERVER` blocks, or in a file named with the `.Server.cs` suffix; either way it's stripped out of published client builds entirely:

```csharp
#if SERVER
public void AdminCommand()
{
    // This code only exists on the server
}
#endif
```

### Configuring User Permissions

Set these up through `users/config.json`:

```json
{
    "users": {
        "76561198000000000": {
            "permissions": ["admin", "moderator"],
            "claims": { "role": "admin" }
        }
    }
}
```

Test for one with `connection.HasPermission( "admin" )`.

***

## Patterns Worth Copying

### A Networked Player Controller

```csharp
public sealed class MyPlayer : Component, Component.INetworkSpawn
{
    [Sync] public string DisplayName { get; set; }
    [Sync] public int Score { get; set; }
    [Property] public float Speed { get; set; } = 200f;

    public void OnNetworkSpawn( Connection owner )
    {
        DisplayName = owner.DisplayName;
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;  // don't control other players

        var wishDir = Input.AnalogMove.Normal;
        WorldPosition += wishDir * Speed * Time.Delta;
    }

    [Rpc.Broadcast]
    public void PlayJumpEffect()
    {
        // Everyone sees the effect
        Sound.Play( "player.jump", WorldPosition );
    }

    [Rpc.Host]
    public void RequestDamage( GameObject target, float amount )
    {
        // Only host processes damage, authoritative
        var health = target.GetComponent<HealthComponent>();
        health?.TakeDamage( amount );
    }
}
```

### Cheat Sheet

| Task | Code |
|------|------|
| Am I the host? | `Networking.IsHost` |
| Am I a proxy? (on Component/GameObject) | `IsProxy` |
| All connected players | `Connection.All` |
| My own connection | `Connection.Local` |
| The host's connection | `Connection.Host` |
| Spawn a networked object owned by the host | `go.NetworkSpawn( Connection.Host )` |
| Spawn a networked object for a specific player | `go.NetworkSpawn( connection )` |
| Take ownership of an object | `go.Network.TakeOwnership()` |
| Give up ownership | `go.Network.DropOwnership()` |
| Is this object networked? | `go.Network.Active` |
| Teleport without a visible slide | `WorldPos = x; Network.ClearInterpolation()` |
| Push structural changes to clients | `go.Network.Refresh()` |
| Create a lobby | `Networking.CreateLobby( config )` |
| Disconnect from the game | `Networking.Disconnect()` |
