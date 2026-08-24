<!--
  s&box Skill : 17_CONSOLE.md

  Console commands and variables: [ConCmd], [ConVar], ConVarFlags, and the injected caller.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# The Console Surface

`[ConCmd]` and `[ConVar]` are the cheapest way to drive and probe a running session. A command
is one attribute on a static method, and from that moment the editor console, the in-game
console overlay, `ConsoleSystem.Run`, a `+name value` launch switch and the MCP
`console_command` tool can all reach it. No UI, no keybind, no scene wiring.

Read out of engine source at 26.08.05: `engine/Sandbox.System/ConVar/ConVarAttributes.cs`,
`engine/Sandbox.System/ConVar/ConCmdAttributes.cs`,
`engine/Sandbox.Engine/Systems/Console/Command.cs`,
`engine/Sandbox.Engine/Systems/Console/ConVarSystem.cs`,
`engine/Sandbox.Engine/Systems/Console/ConsoleSystem.Run.cs`,
`engine/Sandbox.Engine/Systems/Networking/System/NetworkSystem.cs`.

---

## Declaring a Command

```csharp
[ConCmd( "mygame_give" )]
public static void Give( string item, int amount = 1 )
{
    // ...
}
```

The method **must be static**. `ConVarSystem.AddAssembly` scans every type for members carrying
a `ConVarAttribute` and skips any method where `!method.IsStatic`
(`ConVarSystem.cs:65-71`); an instance method is silently never registered. Access modifier does
not matter: the scan uses `BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public`,
so a `private static` command registers fine. `public static` is the convention.

`ConCmdAttribute` derives from `ConVarAttribute` and adds nothing but the constructor, so
everything on `ConVarAttribute` is available on both:

| Member | Type | Notes |
| :-- | :-- | :-- |
| `Name` | `string` | Positional first argument. Falls back to the member name when blank |
| `Help` | `string` | Defaults to the literal `"No description"` when unset (`Command.cs:128`). Only an explicitly empty string falls through to the member's `[Description]` |
| `Flags` | `ConVarFlags` | Positional second argument, or `Flags =` |
| `Min` / `Max` | `float` | ConVars only. Clamps on write, see below |
| `Saved` | `bool` | Shorthand for `ConVarFlags.Saved` |

The name is validated at registration and **throws** if it contains anything but ASCII letters,
digits, `_`, `.` or `-` (`Command.cs:183-186`). A name already taken is not overwritten: the
second registration logs `Command {name} already exists - not overwriting` and loses
(`ConVarSystem.cs:101-105`). Prefix your commands with the game's name.

---

## The Injected `Connection caller`

This is the single most useful thing on the console surface and it is undocumented anywhere
else. Declare a leading `Connection` parameter and the engine fills it in for you:

```csharp
[ConCmd( "mygame_kick", ConVarFlags.Admin )]
public static void Kick( Connection caller, string steamId, string reason = "no reason given" )
{
    // caller is the connection that ran the command. steamId comes from console arg 0.
}
```

The binding is **by type, in first position only**, and the console's own arguments start after
it (`Command.cs:222-227`):

```csharp
if ( paramCount > 0 && parameters[0].ParameterType == typeof( Connection ) )
{
    parameterStartIndex = 1;
    paramCount--;
    callargs[0] = caller;
}
```

So `mygame_kick 76561198... griefing` binds `steamId = "76561198..."` and
`reason = "griefing"`. Nothing the user types can ever land in `caller`, and the arity check
that follows counts the remaining parameters, not all of them.

`caller` is never null: `Command.cs:200` resolves it as `Caller ?? Connection.Local`. It is the
remote connection only when the command arrived over the network (see below); a locally typed
command reports `Connection.Local`.

---

## `ConVarFlags`

```csharp
[Flags]
public enum ConVarFlags
{
    None = 0, Saved = 1, Replicated = 2, Cheat = 4, UserInfo = 8, Hidden = 16,
    ChangeNotice = 32, Protected = 64, Server = 128, Admin = 256, GameSetting = 512,
}
```

| Flag | What it actually does |
| :-- | :-- |
| `Saved` | Persisted through a `CookieContainer` under the key `convar.{name}`, restored next session |
| `Replicated` | Host's value is pushed to clients through the string table. Its default is never read from the property, to avoid baking the host's current value in |
| `Cheat` | Refused unless `Game.CheatsEnabled` (`ConVarSystem.cs:333-337`) |
| `UserInfo` | Client's value is sent to the host and readable as connection user data |
| `Hidden` | Kept out of find and autocomplete |
| `Protected` | Cannot be touched from game code at all: `ConsoleSystem.Run` refuses it (`ConsoleSystem.Run.cs:62-63`) |
| `Server` | Forwarded to the host in a multiplayer game |
| `Admin` | Forwarded to the host **and** refused there unless the caller is the host |
| `GameSetting` | Lobby/host metadata harvested at publish time, not a per-client preference |

### `Admin` is an engine-enforced host gate, not a hint

Two separate pieces of engine code implement it, and both matter.

**On the client**, `ConVarSystem.RunSingle` intercepts before the command ever runs locally
(`ConVarSystem.cs:347-352`):

```csharp
if ( Networking.IsActive && !Networking.IsHost && (command.IsServer || command.IsAdmin) )
{
    var msg = new ServerCommand { Command = command.Name, Args = args };
    Connection.Host?.SendMessage( msg, NetFlags.Reliable );
    return;
}
```

**On the host**, `NetworkSystem.OnServerCommand` re-validates the message, sets
`Command.Caller = source` and invokes (`NetworkSystem.cs:118-141`). The command body then hits
the gate in `ManagedCommand.Run` (`Command.cs:202-206`):

```csharp
if ( IsAdmin && !caller.IsHost )
{
    caller.SendLog( LogLevel.Warn, "You are not allowed to run this command." );
    return;
}
```

Note what that means: `Admin` today is **host only**, not "any player the game considers an
admin". A non-host client's invocation is round-tripped to the host and then refused, with the
refusal logged back to the caller. If your game has its own admin roster, check it yourself
inside the body against `caller`, and use `ConVarFlags.Server` to get the forwarding without the
host-only gate.

`ConVarFlags.Server` forwarding is otherwise identical, and the host-side handler drops the
message unless the command is a con*command* (`!command.IsConCommand`), carries `Server` or
`Admin`, and passes the cheat check.

---

## Arguments

The argument string is split with `SplitQuotesStrings()`, so `"two words"` survives as one
argument, and each piece is converted with `ToType( param.ParameterType )`.

**Default parameter values are honoured.** When the console runs out of arguments,
`Command.cs:256-260` fills in `param.DefaultValue` for any parameter that has one. Only when a
parameter without a default goes unfilled does the run abort, with:

```
Not enough arguments for command "name"! Expected {n}, got {m}.
```

That is a `Log.Warning`, not an exception. A trailing `params T[]` is supported and swallows
every remaining argument (`Command.cs:234-247`).

An exception thrown inside the body does not escape either. It is caught and logged as
`Log.Error( e.InnerException, $"Exception when calling command \"{Name}\"" )`
(`Command.cs:271-275`), so a command that dies leaves an error line and nothing else.

---

## ConVars

A ConVar goes on a **static property with both a getter and a setter**; a property missing
either is skipped at registration (`ConVarSystem.cs:53-57`).

```csharp
[ConVar( "mygame_gravity", Help = "Downward acceleration", Min = 0f, Max = 2000f )]
public static float Gravity { get; set; } = 800f;

[ConVar( "mygame_hud_scale", ConVarFlags.Saved, Help = "HUD scale multiplier" )]
public static float HudScale { get; set; } = 1f;
```

The property's initializer *is* the default. `ManagedCommand` reads the live value at
registration and records it (`Command.cs:145-166`), except for `Replicated` convars, where
reading would go through the string table and risk baking the host's current value in as the
default.

`Min` / `Max` clamp on write, and **only for `float` and `int`** (`Command.cs:316-327`). Any
other type ignores them entirely. If `Min > Max` the pair is swapped at registration
(`ConVarSystem.cs:118-119`).

Typing a ConVar's name with no arguments prints its current value, default and help instead of
setting anything (`ConVarSystem.cs:327-331`).

### `Saved` versus `GameSetting`

They sound similar and do unrelated jobs.

`ConVarFlags.Saved` is a genuine per-client preference. `TryLoad` / `Save` read and write
`convar.{name}` in a `CookieContainer` (`Command.cs:350-364`), and the write happens on every
value change.

`ConVarFlags.GameSetting` is **publish-time metadata about the lobby**.
`ProjectPublisher.GetGameSettings` collects every non-`ConCmd` member carrying the flag and
stores the list on the package as `GameSettings` (`ProjectPublisher.cs:88-140`,
`PublishPage.3.UploadWizardPage.cs:129-130`). The menu reads it back through
`Package.Info.GameSettings` / `HasGameSettings` to build the Create Game dialog, and the chosen
values arrive as `LaunchArguments.GameSettings`, applied once at instance startup
(`GameInstance.cs:273-277`).

**Never use `GameSetting` for a per-client preference.** It is chosen by whoever creates the
lobby, applied to everyone in it, and does not persist for the individual player. Use `Saved`
for a preference and `GameSetting` for a rule of the match.

---

## Running Commands From Code

```csharp
ConsoleSystem.Run( "mygame_give rifle 2" );
ConsoleSystem.Run( "mygame_give", "rifle", 2 );   // params object[], quoted for you
```

`CanRunCommand` refuses anything not found in the managed library and anything marked
`Protected` (`ConsoleSystem.Run.cs:44-67`), and `RunInternal` **throws** on refusal rather than
returning a status. `ConVarSystem.Run( string )` splits on `;` and newline (respecting quotes)
so a multi-command string works, and both paths assert they are on the main thread.

`ConsoleSystem.SetValue` / `GetValue` exist for reading and writing a ConVar by name without
going through a command string.

---

## Using the Console to Probe a Live Session

A console command is the shortest path from "I need to know what the running game thinks" to
an answer, and it beats adding UI, a keybind or a debug component every time.

```csharp
[ConCmd( "mygame_dump_players" )]
public static void DumpPlayers()
{
    foreach ( var p in Game.ActiveScene.GetAllComponents<PlayerController>() )
        Log.Info( $"{p.GameObject.Name} owner={p.Network.Owner} proxy={p.IsProxy}" );
}
```

Then drive it with the editor MCP `console_command` tool and read the output back with
`read_console`, without touching the running scene. Pair it with a `[ConCmd]` that *mutates*
state to reproduce a networking bug on demand, and add `ConVarFlags.Admin` if the reproduction
has to happen host-side.

`14_VERIFICATION.md` covers the MCP tools themselves.

---

## Trap: a Stack Trace Under a Log Line Is Not an Exception

Every log event that carries no exception gets a stack trace attached anyway.
`Logger.WriteToTargets` is the single funnel for `Trace`, `Info`, `Warning` and `Error`, and it
does this (`Sandbox.System/Logging/Logger.cs:158-166`):

```csharp
if ( ex != null ) { logEvent.Exception = ex; }
else
{
    var stackTrace = new StackTrace( 0, true );
    logEvent.SetStackTrace( stackTrace, 0 );
}
```

That trace then becomes `LogEvent.Stack` and shows up under the line in the editor console and
in `read_console` output. **A stack trace beneath a `Log.Info` means only that something logged
from there.** Do not go hunting for a throw that never happened, and do not treat a stack under
a console warning as evidence of a crash. The one reliable tell is the log level plus whether
the message text is an exception message.

---

## Gotchas

- **An instance method with `[ConCmd]` registers nothing at all.** No warning, no error. The
  scan skips non-static members and moves on.

- **A duplicate name loses silently to the first registration**, with only a `Log.Warning`.
  Two addons that both define `respawn` will fight, and the loser just never runs.

- **`Protected` blocks game code, not the console.** A protected command can still be typed by
  the user or run by the tools; it is `ConsoleSystem.Run` from game code that refuses it.

- **`ConVarFlags.Admin` means host, not admin.** Enforce your own roster inside the body.

- **A failed run is a log line, not a return value.** Wrong arity, cheat lockout, admin refusal
  and a thrown body all end as log output. If a command "did nothing", read the console.

- **Convar `Min`/`Max` silently do nothing on a non-`float`, non-`int` property.** Validate in
  the setter if the type is anything else.
