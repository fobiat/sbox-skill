<!--
  s&box Skill : 07_SERVICES.md

  Backend services and saved state: stats, leaderboards, save data, packages and mounting.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
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
`engine/Sandbox.Access/Rules/BaseAccess.cs`.

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
