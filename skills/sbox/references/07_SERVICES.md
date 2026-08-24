<!--
  s&box Skill : 07_SERVICES.md

  Backend services and saved state: stats, leaderboards, save data, packages and mounting.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Services & Persistence

Backend services (stats, leaderboards, achievements), save data, package querying/mounting,
and how mounted content reaches game code. Read out of engine source at version 26.08.05:
`engine/Sandbox.Engine/Game/Services/Stats/`, `engine/Sandbox.Engine/Game/Services/Leaderboards/`,
`engine/Sandbox.Engine/Game/Services/Achievements/`, `engine/Sandbox.Services/Api/`,
`engine/Sandbox.Engine/Systems/Filesystem/`, `engine/Sandbox.Filesystem/BaseFileSystem.cs`,
`engine/Sandbox.Engine/Systems/Cookies/Cookie.cs`, `engine/Sandbox.Engine/Services/Packages/`,
`engine/Sandbox.Engine/Systems/Filesystem/Storage/`, `engine/Sandbox.Engine/Game/PartyRoom/`,
`engine/Sandbox.Engine/Core/Internal/IModalSystem.cs`,
`engine/Sandbox.Engine/Utility/Json/`, `engine/Sandbox.Access/Rules/BaseAccess.cs`.

---

## Overview

`Sandbox.Services` (`Stats`, `Leaderboards`, `Achievements`) is game code's window onto Facepunch's
backend. All three are static classes that resolve "which package is this for" from the calling
assembly, then talk to `Sandbox.Backend` (a set of Refit REST clients configured against
`https://public.facepunch.com/sbox`).

**Client vs server:**
- `Stats.Increment`/`SetValue` queue locally and get flushed on a timer. The flush itself is a
  no-op on a dedicated server: `PostStatsAsync` returns immediately if
  `Application.IsDedicatedServer` (`Api.StatsManager.cs:145`). Values queued server-side are
  silently discarded, never sent.
- `Achievements.Unlock` returns immediately on a dedicated server too (`Achievements.cs:10`):
  achievements belong to an authenticated local player, and a dedicated server has none.
- Reading stats/leaderboards (`Refresh()`) isn't gated the same way; it just requires
  `Backend.Stats`/`Backend.Leaderboards` to be initialized, which they are on any instance that
  has bootstrapped the backend.
- In short: submit stats and unlock achievements from client code only. Read-only queries are
  safe anywhere they're reachable.

**Does this need a published package?** No, for persistence. `FileSystem.Data` works for a
purely local, never-published game: `GameInstanceDll.cs:697-716` scopes it either by
`org.package` (parsed ident) or by a `.local` bucket keyed on whatever ident the game was
launched with, so unpublished dev games still get a working, disk-backed data folder.

Stats and leaderboards are different. `Stats.GlobalStats.Refresh()` and `PlayerStats.Refresh()`
both bail out early if the resolved package ident starts with `"local."`
(`Stats.Global.cs:117`, `Stats.Player.cs:126`), so an ident that never parsed as `org.package`
never pulls fresh data from the backend. `Increment`/`SetValue` still queue and predict locally
regardless, but nothing lands on a leaderboard until the game has a real `org.package` ident,
which in practice means it's been published (a local ident that happens to parse as `org.package`
because you ran it with `-rungame org.package` also works: the check is purely string-based).

All of `Stats`, `Leaderboards`, and `Achievements` resolve their target package from the calling
assembly: if the assembly name starts with `package.`, the ident after the prefix is used;
otherwise `Application.GameIdent` (`Stats.cs:126-133`). You don't pass a package ident explicitly
in the common case.

---

## Stats

`Sandbox.Services.Stats`. A stat is just a named number your game submits; leaderboards and
achievement progress are both built on top of stats, not separate systems.

```csharp
Stats.Increment( "kills", 1 );
Stats.Increment( "kills", 1, new Dictionary<string, object> { ["weapon"] = "smg" } );
Stats.SetValue( "highscore", 5000 );
```

| Member | Signature | Notes |
|---|---|---|
| `Stats.Increment` | `( string name, double amount )` | Adds to a running total. Compounded client-side into one record per name per session before sending. |
| `Stats.Increment` | `( string name, double amount, Dictionary<string, object> data )` | Same, with arbitrary JSON-able context. |
| `Stats.SetValue` | `( string name, double amount, string context = null, object data = null )` | Overwrites rather than adds. |
| `Stats.Flush` / `FlushAsync` | `()` | Force pending stats to the backend. Fire-and-forget; doesn't guarantee availability for query. |
| `Stats.FlushAndWaitAsync` | `( CancellationToken )` | Waits until ingested and queryable. |

Stat names are restricted to letters, digits, `.`, `-`, `_` (`Api.StatsManager.cs:157-170`);
anything else is silently dropped before it's even queued.

### Reading stats back

```csharp
var kills = Stats.LocalPlayer.Get( "kills" );
Log.Info( $"{kills.Value} ({kills.Sum} total, {kills.Max} best)" );

var global = Stats.Global.Get( "kills" );
Log.Info( $"{global.Players} players, avg {global.Avg}" );
```

`Stats.LocalPlayer` and `Stats.Global` are cached, refreshing objects (`PlayerStats` /
`GlobalStats`), not one-shot fetches. `Get( name )` returns a default (zeroed) struct if the stat
doesn't exist rather than throwing. Both types re-fetch at most once every 10 seconds
(`Stats.Player.cs:123`, `Stats.Global.cs:114`). Calling `Refresh()` in a tight loop is safe but
wasteful, it'll just early-return.

`PlayerStat` fields: `Name`, `Title`, `Description`, `Unit`, `Value`, `ValueString`, `Max`, `Min`,
`Avg`, `Sum`, `Last`/`LastValue`, `First`/`FirstValue`. `GlobalStat` adds `Players`, `Velocity`
(change per hour) and drops the per-player first/last fields. `Title`/`Description`/`Unit` are
whatever was configured for the stat on the backend dashboard, not something you set from code.

```csharp
Stats.GetPlayerStats( "myorg.mygame", steamId );   // another player's stats for a package
Stats.GetGlobalStats( "myorg.mygame" );             // another package's global stats
```

### Map-scoped stats

```csharp
Stats.Map.SetValue( "traps_triggered", 1 );
Stats.Map.GetLocal( "traps_triggered" );
Stats.Map.Global;
```

`Stats.Map` mirrors the top-level API but scopes everything to `Application.MapPackage`'s ident
instead of the game's: useful for community maps tracking their own numbers separately from the
gamemode.

### Local prediction

`Increment`/`SetValue` immediately call `PlayerStats.Predict()` on the local cache
(`Stats.cs:61-62`, `:76-77`, `:110-111`), folding the new value into `Max`/`Min`/`Sum`/`Last`
before the network round-trip completes. UI reading `Stats.LocalPlayer` updates instantly; it
doesn't wait on the backend.

---

## Leaderboards

`Sandbox.Services.Leaderboards`. There's no separate "create a leaderboard" call: a leaderboard
is a query over an existing stat, aggregated server-side. Boards are configured (title, unit,
visibility) on the backend dashboard against a stat name.

```csharp
var board = Leaderboards.GetFromStat( "kills" );
board.SetAggregationSum();
board.SetSortDescending();
board.MaxEntries = 50;
await board.Refresh();

foreach ( var entry in board.Entries )
    Log.Info( $"#{entry.Rank} {entry.DisplayName}: {entry.Value}" );
```

`Board2` (returned by `GetFromStat`) is the current API. Configuration is via setter methods, not
settable properties, because the underlying query is a mutable struct field:

| Method | Effect |
|---|---|
| `SetAggregationSum/Avg/Min/Max/Last()` | How multiple submissions per player collapse into one board value |
| `SetSortAscending/Descending()` | Rank order |
| `SetFriendsOnly( bool )` | Restrict to Steam friends |
| `SetCountryCode( string )` / `SetCountryAuto()` | Restrict to a country |
| `FilterByYear/Month/Week/Day/None()` + `SetDatePeriod( DateTime )` | Time-windowed leaderboard |
| `CenterOnSteamId( long )` / `CenterOnMe()` | Center the returned page on a player instead of rank 1 |
| `IncludeSteamIds( params long[] )` | Force specific players into the results regardless of rank |
| `MaxEntries` / `Offset` | Paging |

```csharp
public readonly struct Entry
{
    public readonly long Rank;
    public readonly double Value;
    public readonly long SteamId;
    public readonly string CountryCode;
    public readonly string DisplayName;
    public readonly DateTimeOffset Timestamp;
    public readonly string DataUrl;   // set if the submission carried extra JSON data
}
```

`Refresh()` is throttled by a static (class-level, not per-instance) semaphore
(`LeaderboardsEx.cs:125`), and every `Board2` in the process queues behind the same lock, so don't
expect concurrent leaderboard fetches to run in parallel. `ApiException` from a 429 or 404 is
caught and swallowed inside `Refresh()`; check `Entries.Length`/`TotalEntries` after awaiting
rather than wrapping the call in your own try/catch for those codes.

`Leaderboards.Get( name )` (the older `Board` type, `Leaderboards.cs`) still works and exposes a
simpler `Group` string (`"global"`/`"country"`/`"friends"`) instead of the setter methods above.
It calls a v1 legacy endpoint. Prefer `GetFromStat` for new code.

---

## Achievements

`Sandbox.Services.Achievements`. Achievement definitions (name, title, icon, unlock condition)
live on the backend dashboard, not in code. Two unlock modes exist: `Manual` (game calls
`Unlock`) and `Stat` (unlocks automatically once a source stat crosses a threshold, checked by an
internal tick, so you never call anything for stat-driven achievements beyond submitting the stat).

```csharp
Achievements.Unlock( "first_blood" );   // no-op if the achievement is stat-driven, or already unlocked

foreach ( var a in Achievements.All )
    Log.Info( $"{a.Title}: {(a.IsUnlocked ? "unlocked" : $"{a.ProgressionFraction:P0}")}" );
```

`Achievement` fields: `Name`, `Title`, `Description`, `Icon`, `IsUnlocked`, `UnlockTimestamp`,
`Score`, `IsVisible` (respects the backend's visibility mode, including "hidden until unlocked"),
`HasProgression`, `ProgressionFraction` (0-1, unclamped), `CurrentValue`, `GlobalUnlocked`,
`GlobalFraction`.

There's no `Achievements.Get( name )` on the top-level class (only `Achievements.Map.Get` exists).
Use `Achievements.All.FirstOrDefault( a => a.Name == name )`.

`Achievements.Map` mirrors this for the current map package, same shape as `Stats.Map`.

---

## Persistence and save data

**`System.IO.File` and `System.IO.Directory` are not available to game code at all.** The
sandbox compiler enforces a member-level allowlist (`Sandbox.Access/Rules/BaseAccess.cs`), and
neither type appears in it: only `System.IO.Path` (partially), `Stream`, `MemoryStream`,
`StreamReader`/`TextReader`/`TextWriter`/`StringWriter`, `BinaryReader`/`BinaryWriter`, and the
compression streams are whitelisted. Referencing `System.IO.File.*` is a compile error (`SB500`),
not a runtime exception. `FileSystem` and `BaseFileSystem` (in the fully-whitelisted
`Sandbox.Filesystem`/`Sandbox.Engine` assemblies) are the sanctioned replacement.

### FileSystem.Data: the one you want

```csharp
FileSystem.Data.WriteJson( "save.json", saveData );
var loaded = FileSystem.Data.ReadJsonOrDefault<SaveData>( "save.json", new SaveData() );

if ( FileSystem.Data.FileExists( "save.json" ) )
    FileSystem.Data.DeleteFile( "save.json" );
```

`FileSystem.Data` is a `BaseFileSystem` scoped per-package: `org.package` for a published game, or
a `.local` bucket keyed by whatever ident the game launched with for local dev
(`GameInstanceDll.cs:697-716`). It's real disk storage that survives between sessions, created
automatically, no setup required. This is the correct place for save games and player progress.

`FileSystem.OrganizationData` is the same idea one level up: shared across every package
published by the same org, for data you want visible to all your games rather than one.

### BaseFileSystem members

All of these exist on `FileSystem.Data`, `FileSystem.OrganizationData`, and `FileSystem.Mounted`
(read-only there, see below):

| Member | Signature | Notes |
|---|---|---|
| `ReadAllText` / `ReadAllTextAsync` | `( string path )` | Returns `null` if the file doesn't exist rather than throwing |
| `WriteAllText` | `( string path, string contents )` | Overwrites; creates parent directories automatically |
| `ReadAllBytes` / `ReadAllBytesAsync` | `( string path )` | Throws if missing |
| `WriteAllBytes` | `( string path, byte[] contents )` | |
| `ReadJson<T>` | `( string filename, T defaultValue = default )` | Throws on invalid JSON |
| `ReadJsonOrDefault<T>` | `( string filename, T returnOnError = default )` | Swallows any exception, including missing file |
| `WriteJson<T>` | `( string filename, T data )` | |
| `FileExists` / `DirectoryExists` | `( string path )` | |
| `DeleteFile` / `DeleteDirectory` | `( string path, bool recursive = false )` for the directory overload | |
| `CreateDirectory` | `( string folder )` | No-op if it already exists |
| `FindFile` / `FindDirectory` | `( string folder, string pattern = "*", bool recursive = false )` | Returns paths relative to `folder` |
| `OpenRead` / `OpenWrite` | `( string path, FileMode mode = ... )` | Raw `Stream` access for incremental/streaming reads |
| `FileSize` | `( string filepath )` | Bytes |
| `GetCrc` / `GetCrcAsync` | `( string filepath )` | CRC64, `0` if missing |
| `DirectorySize` | `( string path, bool recursive = false )` | Sum of contained file sizes |

`IsReadOnly` tells you upfront whether writes will throw. Check it before writing to
`FileSystem.Mounted`, which is backed by a `ReadOnlyFileSystem`.

### Cookie: small persisted key/value data

```csharp
Game.Cookies.SetString( "last_class", "engineer" );
var cls = Game.Cookies.GetString( "last_class", "default" );

Game.Cookies.Set( "settings", mySettingsObject );   // JSON-encoded
var settings = Game.Cookies.Get<Settings>( "settings", new Settings() );
```

`Game.Cookies` is a `CookieContainer` backed by `FileSystem.Data` (`GameInstanceDll.cs:718`), so
it's per-package the same way `FileSystem.Data` is. It's meant for small settings-shaped values,
not save games: entries expire and are pruned 30 days after last read/write
(`Cookie.cs:57`, `MarkUsed`), with a 24-hour grace period after expiry before actual deletion. It
autosaves once a minute on a timer, plus at shutdown; there's no public method to force a save.
Mutating a cookie doesn't hit disk until the next tick or shutdown.

| Member | Signature |
|---|---|
| `SetString` / `GetString` | `( string key, string value )` / `( string key, string fallback = "" )` |
| `Set<T>` / `Get<T>` | `( string key, T value )` / `( string key, T fallback )`, JSON-encoded |
| `TryGetString` / `TryGet<T>` | `( string key, out ... )` |
| `Remove` | `( string key )` |

### FileSystem.Cache: throwaway keyed storage

```csharp
FileSystem.Cache.Set( key, bytes );
if ( FileSystem.Cache.TryGet( key, out var bytes ) ) { }
```

`FileSystem.Cache` is a `KeyStore` in a shared, engine-global cache folder, keyed by MD5 of
whatever string you pass. It is explicitly documented as "may be deleted at any time": don't put
anything here you can't afford to lose or regenerate.

---

## Sandbox.Json

`WriteJson` / `ReadJson` above go through `Sandbox.Json`, and so should anything you serialize
by hand. `System.Text.Json.JsonSerializer` is whitelisted (`BaseAccess.cs:257`) so calling it
compiles, but it runs with **default options** and will not round-trip an engine type: a
`Vector3`, a `Color`, an `Angles`, a `Model` reference, an `ActionGraph`, or anything
implementing `IJsonConvert`. `Sandbox.Json` is the same serializer configured with
`GlobalContext.Current.JsonSerializerOptions` (`Utility/Json/Json.cs:17`), which has all of that
registered.

```csharp
var text  = Json.Serialize( myObject );        // null in, null out
var back  = Json.Deserialize<SaveDoc>( text ); // throws on malformed
```

| Member | Signature | Notes |
| :-- | :-- | :-- |
| `Serialize` | `( object source )` → `string` | Returns `null` when `source` is null |
| `Serialize<T>` | `( Utf8JsonWriter writer, T target )` | Streaming overload; also `( writer, object, Type )` |
| `Deserialize<T>` | `( string source )` | Throws on malformed input |
| `Deserialize` | `( string source, Type t )` | And `( ref Utf8JsonReader, ... )` for both |
| `TryDeserialize<T>` | `( string source, out T obj )` → `bool` | Catches everything, returns false |
| `TryDeserialize` | `( string source, Type t, out object obj )` → `bool` | |
| `ParseToJsonObject` | `( string json )` → `JsonObject` | Also takes a `ref Utf8JsonReader` |
| `ToNode` | `( object obj )` / `( object obj, Type type )` → `JsonNode` | |
| `FromNode<T>` | `( JsonNode node )` | And `FromNode( JsonNode, Type )` |

Use `TryDeserialize` on the load path. `Deserialize<T>` throwing inside `OnStart` takes the
whole component down.

### Making a custom `[Property]` type serialize

Two interfaces, and they solve different problems.

**`IJsonConvert`** replaces the whole representation, using static abstract members so no
converter needs registering:

```csharp
public struct Coord : IJsonConvert
{
    public int X, Y;

    public static object JsonRead( ref Utf8JsonReader reader, Type typeToConvert )
        => Parse( reader.GetString() );

    public static void JsonWrite( object value, Utf8JsonWriter writer )
        => writer.WriteStringValue( $"{((Coord)value).X},{((Coord)value).Y}" );
}
```

`JsonConvertFactory` picks up any type assignable to `IJsonConvert`
(`Utility/Json/IJsonConvert.cs:15-27`), so implementing the interface is the entire wiring.

**`IJsonPopulator`** keeps normal property serialization but lets an *existing instance* be
filled from a node rather than replaced, which is what you want for a class that owns
subscriptions or engine handles:

```csharp
public interface IJsonPopulator
{
    JsonNode Serialize();
    void Deserialize( JsonNode node );
}
```

---

## Versioned Save Documents

`FileSystem.Data` gives you bytes. Everything below is the shape that keeps those bytes
readable after the game ships and the schema moves. None of it is engine machinery; it is the
pattern that survives contact with a rolling update, written down because every real project
reinvents it badly first.

```csharp
public sealed class ProfileDoc
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public string DisplayName { get; set; } = "";
    public int Level { get; set; } = 1;
    public List<string> Unlocks { get; set; } = new();   // added in v2
    public LoadoutDoc Loadout { get; set; }              // added in v3
}
```

**Migrate with a monotone ladder, one `if` per version step.**

```csharp
static void Migrate( ProfileDoc doc )
{
    if ( doc.Version < 2 )
    {
        // Unlocks' own initializer is the migration. Nothing to write here except the stamp.
        doc.Version = 2;
    }

    if ( doc.Version < 3 )
    {
        doc.Loadout = LoadoutDoc.Default();
        doc.Version = 3;
    }
}
```

Each block runs, stamps, and falls through to the next, so a v1 document walks the whole chain
in one pass. **A field whose default is correct needs no migration code at all**, because the
property initializer already produced it before deserialization ran. Only a field whose correct
value depends on the old data earns a line.

**Clamp a newer document down rather than trusting or rejecting it.** A player who ran a newer
build, or who synced a save from another machine, will hand you a `Version` above yours. Loading
it as-is means reading fields your code does not understand and writing back a document that
silently drops them; refusing it outright loses the save.

```csharp
if ( doc.Version > ProfileDoc.CurrentVersion )
    doc.Version = ProfileDoc.CurrentVersion;
```

**Re-null-check every collection and nested document unconditionally after deserialize.** A
property initializer runs *before* the deserializer, and an explicit `null` in the JSON beats
it: `{"Unlocks": null}` leaves you with a null list even though the property said `= new()`.
That JSON exists in the wild the moment anything ever wrote it, including an earlier version of
your own save code.

```csharp
public static ProfileDoc Decode( string json )
{
    if ( string.IsNullOrWhiteSpace( json ) ) return null;
    if ( !Json.TryDeserialize<ProfileDoc>( json, out var doc ) || doc is null ) return null;
    if ( doc.Version <= 0 ) return null;                       // not one of ours

    if ( doc.Version > ProfileDoc.CurrentVersion )
        doc.Version = ProfileDoc.CurrentVersion;

    Migrate( doc );

    doc.Unlocks ??= new();                                     // beats an explicit JSON null
    doc.Loadout ??= LoadoutDoc.Default();
    doc.Loadout.Slots ??= new();                               // nested, same rule

    return doc;
}
```

**`Decode` returns null for blank, malformed and unrecognised input, never an empty document.**
The difference matters at the call site: a null means "there is no save here or it is not
readable", which the caller can log, back up and start fresh from. An empty-but-valid document
means "this player has nothing", which is a legitimate state you never want a parse failure to
impersonate. A corrupt save that silently becomes a fresh profile is a bug report you will never
be able to reproduce.

**Chain nested documents.** Give each nested type its own `CurrentVersion`, `Version` and
migration ladder, and call the child's migration from the parent's. A nested document that
shares the parent's version number cannot be reused anywhere else and forces a parent version
bump every time a leaf field changes.

This whole pattern pairs with the networking one in `04_NETWORKING.md` →
*A `[Sync] string` snapshot versus a `NetList`*: the same versioned `Encode`/`Decode` envelope
serves both the save file and the replicated copy, so the two never drift.

---

## Package

`Sandbox.Package` represents an asset on the backend (a game, map, model, addon, etc). Most game
code only needs to query and mount packages, not publish them.

### Finding and fetching

```csharp
var pkg = await Package.FetchAsync( "myorg.mygame", partial: false );

var results = await Package.FindAsync( "type:map ugc" );
foreach ( var p in results.Packages ) Log.Info( p.Title );

var versions = await Package.FetchVersions( "myorg.mygame" );
```

| Member | Signature | Notes |
|---|---|---|
| `Package.FetchAsync` | `( string identString, bool partial, bool useCache = true )` | The main lookup. Returns `null` on failure, never throws for a 404. |
| `Package.FindAsync` | `( string query, int take = 200, int skip = 0, CancellationToken token = default )` | Search. Query is a space-separated string of terms/facets (`type:map`, `local:true`, free text). |
| `Package.ListAsync` | `( string id, CancellationToken token = default )` | Curated groupings, for discovery UI. |
| `Package.TryParseIdent` | `( string ident, out (string org, string package, int? version, bool local) )` | Accepts `org/package`, `org.package`, `org.package#version`, `org.package#local`, or a full `sbox.game`/`asset.party` URL. |
| `Package.FormatIdent` | `( string org, string package, int? version = null, bool local = false )` | Inverse of `TryParseIdent`. |

`FindAsync` with `local:true` in the query redirects to locally-installed addons instead of
hitting the backend (`Package.Static.cs:320-329`), useful for an in-game "browse my local
projects" list without a network round trip.

### Mounting

```csharp
var pkg = await Package.MountAsync( "myorg.mygame-extra-content", partial: false );
// or, if you already have a Package:
var fs = await pkg.MountAsync( withCode: false );
```

`MountAsync` downloads (if remote) and mounts the package's content filesystem, merging it into
`FileSystem.Mounted`. `withCode: true` additionally loads the package's compiled assembly if it
has one. Only do this for packages you intend to run code from, not passive content packs.
`pkg.IsMounted()` checks current state without triggering a download.

### Package metadata

`Package` exposes `Title`, `Summary`, `Description`, `Thumb`/`ThumbWide`/`ThumbTall`, `Tags`,
`Org` (with `Ident`, `Title`, socials), `TypeName`, `Public`, `Archived`, `FileSize`, `Usage`
(play stats: `Total`/`Month`/`Week`/`Day` each with `Users`/`Seconds`/`Sessions`, plus
`UsersNow`), `Reviews` (`Score`, `PositiveRatings`, `NegativeRatings`), `Favourited`, `VotesUp`,
`VotesDown`, `Screenshots`, `Url`.

`package.Info` is a thin typed view over well-known config values (`MaxPlayers`, `MinPlayers`,
`DefaultMap`, `NeedsMap`, `LaunchMode`, `IsQuickPlay`, `GameSettings`, etc) that would otherwise
be `GetValue<T>( "SomeKey", default )` string-keyed lookups. `package.Metadata` (only populated
for compiled model/material/clothing packages) exposes compiler-extracted stats like triangle
counts, bone counts, and referenced textures. Pattern-match on `ModelMetaData` /
`MaterialMetaData` / `ClothingMetaData`.

`Package.SortByReferences` topologically sorts a package list so dependencies come before
dependents, useful before loading a set of packages that reference each other.

---

## Mounting

"Mounting" a package means adding its content filesystem into the aggregate exposed as
`FileSystem.Mounted`. Every package your game depends on (including itself) ends up merged into
this one `BaseFileSystem`, which is how `CloneModel( "models/citizen/citizen.vmdl" )` finds an
asset regardless of which package actually shipped it: asset paths are resolved against the
combined mount, not any one package's own filesystem.

```csharp
var tex = FileSystem.Mounted.FileExists( "textures/my_texture.png" );
```

`FileSystem.Mounted` is read-only (backed by an `AggregateFileSystem`); use `FileSystem.Data` for
anything you write. Packages get added to it as they're loaded: your own package's content, then
each `PackageReferences` dependency, recursively. You don't normally call anything to populate
it yourself.

There's a second, unrelated "mounting" system: `Sandbox.Mounting` (`engine/Mounting/`) is the
editor/tools-facing subsystem (`Sandbox.Mounting.Directory`, `BaseGameMount`) that mounts
*other games' installs* (Half-Life, Source, GoldSrc, etc via Steam) so the asset browser can pull
their content into the editor. That's a content-creation tool for the asset pipeline, not
something running game code calls at runtime. Don't confuse it with `FileSystem.Mounted`.

---

## The Rest of the Backend Surface

Stats, leaderboards and achievements are the parts of the backend most games touch, but they are
not all of it. **Watch the namespaces**: only some of these live in `Sandbox.Services`.

| Type | Namespace | For |
| :-- | :-- | :-- |
| `Storage` | `Sandbox` | Cloud-saved and workshop-published content bundles |
| `Inventory` | `Sandbox.Services` | Steam Inventory items and their definitions |
| `Screenshots` | `Sandbox.Services` | Steam screenshot library (no public members, see below) |
| `ServerList` | `Sandbox.Services` | Querying the Steam master server list |
| `PartyRoom` | `Sandbox` | A Steam lobby of friends that travels between games |
| `Game.Overlay` | `Sandbox` | Opening the platform's own modals |

### Storage

`Storage` bundles a set of files into an entry with an id, a type, a timestamp, metadata and a
thumbnail, then either keeps it locally or publishes it to the Steam Workshop. This is what
"dupes", saved builds, custom maps and player-made content ride on.

```csharp
var entry = Storage.CreateEntry( "dupe" );      // "dupe", "save", whatever you call it
entry.Files.WriteJson( "contents.json", build );
entry.SetMeta( "PieceCount", build.Pieces.Count );
entry.SetThumbnail( bitmap );

var mine = Storage.GetAll( "dupe" );            // Entry[]
```

`Storage.Entry` (`Systems/Filesystem/Storage/Storage.Entry.cs`):

| Member | Signature |
| :-- | :-- |
| `Id` / `Type` / `Created` | `string` / `string` / `DateTimeOffset`, all get-only |
| `Files` | `BaseFileSystem`, the entry's own writable folder |
| `SetMeta<T>` / `GetMeta<T>` | `( string key, T value )` / `( string key, T defaultValue = default )` |
| `Thumbnail` | `Texture` |
| `SetThumbnail` | `( Bitmap bitmap )` |
| `Delete` | `()` |
| `Publish` | `( string title = "Unnammed", string[] tags = null, Dictionary<string,string> keyvalues = null )` |
| `Publish` | `( WorkshopPublishOptions options )` |

Reading other people's entries goes through `Storage.Query`
(`Storage.Query.cs:37-116`), which is a plain object you fill in and `Run`:

```csharp
var result = await new Storage.Query
{
    TagsRequired = { "vehicle" },
    SortOrder    = Storage.SortOrder.RankedByTrend,
    RankTrendDays = 7,
}.Run();

foreach ( var item in result.Items )
    Log.Info( $"{item.Title} by {item.Owner?.Name} ({item.VotesUp}+)" );

var installed = await result.Items[0].Install();   // → Storage.Entry
```

`Query` carries `FileIds`, `TagsRequired`, `TagsExcluded`, `KeyValues`, `SearchText`,
`MaxCacheAge`, `SortOrder`, `Author` and `RankTrendDays`. `QueryResult` carries `ResultCount`,
`TotalCount`, `NextCursor`, `Items`, plus `HasMoreResults()` and `GetNextResults()` for paging.
`QueryItem` is the metadata-only record (title, description, votes, tags, keyvalues, owner
`Profile`, sizes, timestamps) and only `Install()` actually downloads.

`Storage.Visibility` is `Public` / `FriendsOnly` / `Private` / `Unlisted`, matching Steam's
`ERemoteStoragePublishedFileVisibility`. `Storage.SortOrder` has 20 values, mirroring Steam's
UGC query orders (`RankedByVote`, `RankedByPublicationDate`, `RankedByTrend`,
`RankedByLastUpdatedDate` and friends).

### Inventory

Steam Inventory, for games that sell items. Most of the class is `internal`: `Refresh` and
`CheckOut` are engine-driven, so game code reads rather than writes.

| Member | Signature |
| :-- | :-- |
| `Inventory.Items` | `IReadOnlyCollection<Item>`, what this user owns |
| `Inventory.HasItem` | `( int inventoryDefinitionId )` → `bool` |
| `Inventory.Definitions` | `IReadOnlyCollection<ItemDefinition>` |
| `Inventory.FindDefinition` | `( int definitionId )` → `ItemDefinition` |
| `Item` | `ItemId` (`ulong`), `DefinitionId` (`int`), `Definition` |
| `ItemDefinition` | `Id`, `Name`, `Description`, `IconUrl`, `IconUrlLarge`, `PackageIdent`, `Category`, `Rarity`, `Asset`, `StoreHidden`, `SellStart`/`SellEnd`, `Price`/`BasePrice` (`CurrencyValue`) |

### Screenshots

`Sandbox.Services.Screenshots` is public but its only member,
`AddScreenshotToLibrary( ReadOnlySpan<byte>, int, int )`, is `internal`. There is nothing here
for game code to call in 26.08.05; it exists so the engine's own screenshot key can write into
the Steam library.

### ServerList

```csharp
using var list = new ServerList();
list.AddFilter( "map", "de_dust" );
list.Query();
// list is itself a List<ServerList.Entry>, filled as responses arrive
```

`ServerList` derives from `List<ServerList.Entry>` and is `IDisposable`; dispose it or the
native query leaks. `IsQuerying` goes false when the sweep completes. `Entry` carries
`IPAddressAndPort`, `SteamId`, `Map`, `Game`, `GameVersion`, `Name`, `Tags`, `Players`,
`MaxPlayers`, `Ping` and `Tick`. The constructor already filters on the network protocol
version, so you only ever see servers this build can actually join.

For the in-game browser, prefer `Game.Overlay.ShowServerList( new ServerListConfig( game, map ) )`
and let the platform draw it.

### PartyRoom

A `PartyRoom` is a Steam lobby that persists across games: friends group up in the menu, then
travel together into whatever gets launched. `PartyRoom.Current` is null when the local player
is not in one.

| Member | Signature |
| :-- | :-- |
| `PartyRoom.Current` | `static PartyRoom`, get-only |
| `PartyRoom.Create` | `( int maxMembers )` / `( int maxMembers, string name, bool ispublic )` → `Task<PartyRoom>` |
| `PartyRoom.Find` | `()` → `Task<Entry[]>`, public parties |
| `Id` / `Name` / `MaxMembers` / `MemberCount` | `SteamId` / `string` / `int` / `int` |
| `Members` | `IEnumerable<Friend>` |
| `Owner` | `Friend`, get-only |
| `SetOwner` | `( SteamId friend )` → `bool` |
| `Kick` | `( SteamId friend )` |
| `SendChatMessage` | `( string text )` |
| `Leave` | `()` |
| `PackageIdent` | `string`, what the party is playing |
| `JoinState` | `OwnerJoinState`, where the owner is in the join sequence |
| `VoiceRecording` | `bool`, party voice mic on/off |
| `VoiceCommunicationAllowed` | `bool`, false once a game instance exists |

Events are assignable delegates, not C# events, so **assigning replaces whatever was there**:

```csharp
public Action<Friend, string> OnChatMessage { get; set; }
public Action<Friend>         OnJoin { get; set; }
public Action<Friend>         OnLeave { get; set; }
public Action<Friend, byte[]> OnVoiceData { get; set; }
```

Use `+=` rather than `=` if anything else might already be listening, and clear your handler
when your panel or component goes away. `PartyRoom.IEventListener` exists for the same events as
an interface if you would rather not hold a delegate at all.

### Platform Modals: `Game.Overlay`

The platform's own UI is reachable from game code through `Game.Overlay`
(`Game/Game/Game.Overlay.cs`). The interface behind it, `Sandbox.Modals.IModalSystem`, has an
`internal static Current`, so `Game.Overlay` is the entry point, not `IModalSystem`.

```csharp
Game.Overlay.ShowPackageModal( "facepunch.sandbox" );
Game.Overlay.ShowPlayer( steamId );
Game.Overlay.ShowFriendsList( new FriendsListModalOptions { ShowOfflineMembers = false } );
Game.Overlay.ShowServerList( new ServerListConfig( game: "myorg.mygame" ) );
if ( Game.Overlay.IsOpen ) { /* pause input */ }
```

Others: `ShowGameModal`, `ShowMapModal`, `ShowNewsModal`, `ShowOrganizationModal`,
`ShowReviewModal`, `ShowReportModal`, `ShowPackageSelector`, `ShowMapSelector`,
`ShowSettingsModal( page )`, `ShowBinds`, `ShowPlayerList`, `ShowPauseMenu`, `CreateGame`,
`WorkshopPublish`, `Close`, `CloseAll`, plus `IsPauseMenuOpen`.

The option structs live in `Sandbox.Modals`:

- **`WorkshopPublishOptions`**: `Title`, `Description`, `Thumbnail` (`Bitmap`, 512x512, no
  transparency), `StorageEntry` (`Storage.Entry`, the files being published), `KeyValues`,
  `Tags`, `Metadata` (a string readable from a query *before* download), `Visibility`
  (default `Public`), `CanSelectVisibility`, `PublishedFileId` (set it to update an existing
  item instead of creating one), `OnComplete( ulong publishedFileId )`, and
  `AddCategory<TEnum>( string name )` which prompts the user to pick an enum value and stores
  it as `KeyValues[name]`.
- **`FriendsListModalOptions`**: `ShowOfflineMembers`, `ShowOnlineMembers`, and `Anchor`
  (`Anchoring { Panel, Position, Offset }`) to hang the modal off a button instead of centring it.
- **`CreateGameOptions`**: `Package`, `OnComplete( CreateGameResults )`. `CreateGameResults`
  hands back `GameSettings`, `Map`, `MaxPlayers`, `ServerName`, `Privacy`. Those `GameSettings`
  are the `ConVarFlags.GameSetting` convars your game published; see `17_CONSOLE.md`.
- **`ServerListConfig`**: `GamePackageFilter`, `MapPackageFilter`, or the
  `ServerListConfig( game, map )` constructor.

---

## Gotchas

- **Stats submitted from a dedicated server go nowhere.** They queue locally, then
  `PostStatsAsync` silently drops the flush because `Application.IsDedicatedServer` is true. If
  you need server-authoritative stat tracking, RPC the value to the owning client and have the
  client submit it, or accept that dedicated-server stat submission isn't supported.

- **A local/unpublished game's stats and leaderboards never refresh from the backend.** Ident
  strings starting with `"local."` short-circuit `PlayerStats.Refresh()` and
  `GlobalStats.Refresh()` before any network call. `Increment`/`SetValue` still work locally
  (prediction updates `Stats.LocalPlayer` immediately) so UI built against local stats looks
  correct in dev, but nothing is actually being persisted or aggregated server-side until the
  game is published.

- **`FileSystem.Data` is per-package, not per-org and not global.** Two different games by the
  same author get two separate data folders. If you want data shared across your own games, use
  `FileSystem.OrganizationData` deliberately, it isn't the default.

- **`Game.Cookies` has no public `Save()`.** It autosaves once a minute and at shutdown. If you
  need a value durable *right now* (e.g. right before triggering a crash-prone operation), use
  `FileSystem.Data` directly instead of `Game.Cookies`.

- **Cookies expire.** Any cookie key not read or written for 30 days is deleted (with a 24-hour
  grace period after it first goes stale). Don't use `Game.Cookies` for data you need to survive
  indefinitely with no access pattern, use `FileSystem.Data`.

- **`FileSystem.Cache` can vanish at any time**, by design. It's for regenerable/derived data
  only (thumbnails, decoded assets, anything you can rebuild), never for save data.

- **A property initializer does not survive an explicit JSON `null`.** `= new()` on a
  collection runs before deserialization, and `{"Unlocks": null}` overwrites it. Re-null-check
  every collection and nested document after `Deserialize`, unconditionally, every time.

- **`JsonSerializer` compiles but drops engine types.** It is whitelisted, so nothing stops
  you, and a `Vector3` or a `Model` reference will not round-trip. Use `Sandbox.Json`.

- **`Storage` is `Sandbox.Storage`, not `Sandbox.Services.Storage`.** Same for `PartyRoom`.
  Only `Stats`, `Leaderboards`, `Achievements`, `Inventory`, `Screenshots` and `ServerList`
  actually live under `Sandbox.Services`.

- **`ServerList` is `IDisposable` and holds a native query.** `using var list = new ServerList();`
  or you leak it.

- **`PartyRoom`'s events are settable properties, not C# events.** `OnJoin = handler` throws
  away whoever was listening. Use `+=`, and unsubscribe when the listener dies.

- **Leaderboard refreshes serialize across the whole process.** `Board2.Refresh()` shares one
  static semaphore across every leaderboard instance. Firing off several boards in parallel
  (e.g. a UI showing five different leaderboard tabs) queues them one after another, it doesn't
  parallelize.

- **There's no in-code leaderboard creation.** A `Board2` just queries a stat with an
  aggregation; the board itself (title, unit, visibility) is configured on the backend dashboard
  against that stat name. If a board you expect doesn't show entries, check the stat name and the
  dashboard configuration before suspecting the query code.

- **`Package.FetchAsync`/`FindAsync` never throw on 404/network failure.** They log a warning
  and return `null` (fetch) or an empty result (find). Always null-check rather than wrapping
  these in try/catch for the common failure case.

- **`Achievements.Unlock` is inert for stat-driven achievements.** Calling it manually does
  nothing unless the achievement's unlock mode is `Manual` on the backend; stat-driven
  achievements only unlock via the automatic tick that checks stat thresholds.
