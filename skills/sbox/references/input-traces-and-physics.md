<!--
  s&box Skill : input-traces-and-physics.md

  Input actions, Scene.Trace, the physics world, math types, time and gizmos.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Input & Physics

Covers the `Input` static class, `SceneTrace`, the physics world, collision and trigger
handling, the math types, and time utilities, all read directly out of the s&box engine
source at version 26.08.05. Any file path or line number cited below points at that
source, not at guesswork.

---

## Input

The `Input` class is the single entry point for all player input, and actions are just
strings you configure under Project Settings > Input.

### Checking Action State

```csharp
protected override void OnUpdate()
{
    if ( Input.Pressed( "attack1" ) )   // just pressed this frame
        Fire();
    if ( Input.Down( "forward" ) )      // held down
        Move( Vector3.Forward * Speed * Time.Delta );
    if ( Input.Released( "use" ) )      // just released this frame
        StopUsing();
}
```

| Call | What it does |
|--------|-------------|
| `Input.Down( string action )` | True for every frame the action is held |
| `Input.Pressed( string action )` | True only on the frame it goes from up to down |
| `Input.Released( string action )` | True only on the frame it goes from down to up |
| `Input.Clear( string action )` | Forces the action's state to clear |
| `Input.ReleaseActions()` | Clears every action at once |

Names get matched against `ProjectSettings/Input.config` **without regard to case**
(`Input.Actions.cs:49`, `OrdinalIgnoreCase`), so calling `Input.Down( "use" )` still finds
the action declared as `"Use"`. Get the name wrong entirely and the engine logs
`Couldn't find Input Action called "..."` exactly **once per name, for the rest of the
session** (`Input.Actions.cs:71-86`), then returns `false` from that call forever after.
That single line scrolls past before most people notice it, and nothing warns you again,
which makes a typo one of the quieter bugs to chase down. Pass `complainOnMissing =
false` to `Input.Down` if you want silence from the start instead. Worth remembering too:
a headless `Application` gets `false` back from all three query methods, since a
dedicated server never receives input at all, so gameplay logic the server actually
needs to run must not sit behind an input check.

### Project Settings Configs

Engine behaviour with no C# entry point of its own lives in three JSON files under
`ProjectSettings/`. Every one of them is a plain `ConfigData` document: the real settings
wrapped in a `__guid` / `__schema` / `__type` / `__version` envelope.

| File | Backing type | What it controls |
|---|---|---|
| `Input.config` | `InputSettings` | The `Actions` list, meaning every action name and its default bind |
| `Platform.config` | `PlatformSettings` | The built-in text chat and subtitle overlays |
| `Collision.config` | `CollisionRules` | The tag-vs-tag collision matrix |

Code reaches them through `ProjectSettings.Input`, `ProjectSettings.Platform` and
`ProjectSettings.Collision`, sitting alongside `.Networking`, `.Physics` and `.Systems`
on the same static class. None of them load until first touched, each pulling from its
own `*.config` file on demand
(`Systems/Project/ProjectSettings/ProjectSettings.cs:5-45`).

**`Input.config`** stores a list of `InputAction` records, each shaped as `{ Name,
GroupName, Title, KeyboardCode, GamepadCode }` (`Systems/Input/InputAction.cs:10`, fields
at `:39,45,51,57,63`), and `InputSettings` is little more than a wrapper around that
list. Out of the box there are 31 actions spread across four groups, `Movement`,
`Actions`, `Inventory` and `Other`. Check this table before you invent an action name of
your own, because these are the defaults it will collide with otherwise (stock values
from `Systems/Input/Input.Common.cs:14-58`):

| Action | Key | Note |
|---|---|---|
| `Use` | `e` | Drives `PlayerController.UseButton`, the button behind `IPressable` |
| `Score` | `tab` | Takes the key you would probably have reached for on a scoreboard |
| `Chat` | `enter` | Stays bound even when your gamemode ships a replacement chat |
| `Voice` | `v` | |
| `Menu` | `Q` | Ships as `Q` by default (`Input.Common.cs:57`). This project's own `ProjectSettings/Input.config` rebinds it to `i`, so don't mistake one file for the other |
| `Drop` | `g` | |
| `Flashlight` | `f` | |
| `View` | `C` | |
| `Slot0`-`Slot9` | `0`-`9` | Rounded out by `SlotPrev` / `SlotNext` on `mouse4` / `mouse5` |

> **These files have no file watcher.** Edit a `.config` (or `.sbproj`) from outside the
> editor and the running instance never notices; reload or restart it to pick the change
> up. See the *Two hard-won workflow facts* section of `field-notes.md`.

### Platform Chat: a global side-channel you can't see

`Sandbox.Platform.Chat` runs at the **platform level**, which means it exists whether or
not your gamemode ever touches it (`Systems/Chat/Chat.cs`). The flow runs client to
host, host validates and throttles at 0.66 seconds per user, then host broadcasts out to
a filtered set of recipients.

```
static bool Enabled          => ProjectSettings.Platform.ChatEnabled          // :17
static bool ShowUI           => ProjectSettings.Platform.ChatShowUI           // :22
static int  MaxMessageLength => ProjectSettings.Platform.ChatMaxMessageLength // :27
static void Say( string message )      // client -> host -> everyone      // :38
static void AddText( string message )  // local-only notification         // :96
```

Hook the `IChatEvent` scene event to watch messages arrive, or block them outright:

```csharp
public sealed class ChatFilter : Component, IChatEvent
{
    void IChatEvent.OnChatMessage( ChatMessageEvent e )
    {
        e.Message = Censor( e.Message );
        e.Suppress = IsMuted( e.Sender );                 // drop it entirely
        e.RecipientFilter = c => InRange( c, e.Sender );  // per-connection visibility
    }
}
```

> **Setting `ChatShowUI: false` hides the overlay; it does not turn chat off.** `Say()`
> only checks `Enabled`, never `ShowUI` (`Chat.cs:40`), so with the UI hidden the pipe
> keeps running invisibly: anything that reaches `Say` still broadcasts to every client,
> bypassing whatever channels, permissions and logging your own gamemode has. The engine
> also pushes "X has joined / left the game" notices through this same channel
> automatically (`Chat.cs:143-169`). **The one switch that actually closes the pipe is
> `"ChatEnabled": false` in `ProjectSettings/Platform.config`**, set that if your
> gamemode runs its own chat system.
>
> A second trap sits underneath the first: a *published* build shipped without a
> `Platform.config` gets `ChatEnabled == false` regardless of whatever value was actually
> saved (`PlatformSettings.cs:18-25`). Chat can work perfectly in the editor and be
> silently dead in the shipped build, and you generally only learn that from a player.

### Analog Movement and Look

```csharp
Vector3 moveDir = Input.AnalogMove;    // WASD or left stick (x=forward, y=right, z=up)
Angles lookDir = Input.AnalogLook;     // mouse delta or right stick (scaled by sensitivity)
```

| Property | Type | What it holds |
|----------|------|-------------|
| `AnalogMove` | `Vector3` | Move input from the keyboard or left stick |
| `AnalogLook` | `Angles` | Look input from the mouse or right stick, already sensitivity-scaled |
| `MouseDelta` | `Vector2` | Raw, unscaled mouse movement for the frame |
| `MouseWheel` | `Vector2` | Current scroll wheel state |
| `EscapePressed` | `bool` | True when Escape was hit; set it back to false to intercept the pause menu |
| `UsingController` | `bool` | True if the last input came from a controller |
| `ControllerCount` | `int` | How many controllers are currently connected |
| `Suppressed` | `bool` | Set true to suppress all input |

### Controllers and Haptics

```csharp
float trigger = Input.GetAnalog( InputAnalog.LeftTrigger );  // -1 to 1

// Haptics
Input.TriggerHaptics( leftMotor: 0.5f, rightMotor: 0.7f, duration: 500 );
Input.TriggerHaptics( HapticEffect.HardImpact, lengthScale: 1f, frequencyScale: 1f, amplitudeScale: 0.5f );
Input.StopAllHaptics();

// Local multiplayer: scope input to a specific controller
using ( Input.PlayerScope( playerIndex ) )
{
    if ( Input.Pressed( "jump" ) ) { /* controller-specific */ }
}
```

The `InputAnalog` enum covers: `LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`,
`LeftTrigger`, `RightTrigger`

### Button Glyphs for UI

```csharp
// Get controller button texture for UI display
Texture glyph = Input.GetGlyph( "jump" );
Texture outlined = Input.GetGlyph( "jump", outline: true );
string keyName = Input.GetButtonOrigin( "jump" );  // e.g. "SPACE" or "A Button"
```

### Bypassing Actions: Raw Keyboard

Skip the action layer entirely and query a physical key directly:

```csharp
if ( Input.Keyboard.Pressed( "w" ) ) { }
if ( Input.Keyboard.Down( "space" ) ) { }
```

---

## Mouse and Screen Metrics

```csharp
Vector2 mousePos = Mouse.Position;       // relative to game window top-left
Vector2 mouseDelta = Mouse.Delta;        // position change since last frame
Mouse.Visibility = MouseVisibility.Auto; // Auto, Visible, Hidden

float w = Screen.Width;
float h = Screen.Height;
Vector2 size = Screen.Size;
float aspect = Screen.Aspect;
```

---

## SceneTrace (Raycasting)

`Scene.Trace` exposes a builder-pattern API for physics traces: chain shape and filter
calls onto it, then finish with `Run()` or `RunAll()`.

### Trace Examples

```csharp
// Ray trace
var tr = Scene.Trace.Ray( startPos, endPos ).Run();
if ( tr.Hit )
{
    Log.Info( $"Hit {tr.GameObject} at {tr.EndPosition}" );
    Log.Info( $"Normal: {tr.Normal}, Distance: {tr.Distance}" );
}

// Ray from mouse position
var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );
var tr = Scene.Trace.Ray( ray, 5000f ).Run();

// Sphere trace
var tr = Scene.Trace.Sphere( 16f, startPos, endPos )
    .WithoutTags( "player" )
    .Run();

// Box trace
var tr = Scene.Trace.Ray( start, end )
    .Size( new BBox( -5, 5 ) )
    .UseHitboxes( true )
    .Run();
```

### Trace Builder Reference

**Choosing a shape:**

| Method | What it does |
|--------|-------------|
| `Ray( Vector3 from, Vector3 to )` | A straight line trace |
| `Ray( Ray ray, float distance )` | A line trace built from an existing `Ray` |
| `Sphere( float radius, Vector3 from, Vector3 to )` | Sweeps a sphere |
| `Box( BBox bbox, Vector3 from, Vector3 to )` | Sweeps a box |
| `Capsule( Capsule capsule, Vector3 from, Vector3 to )` | Sweeps a capsule |
| `Size( BBox hull )` / `Size( Vector3 size )` | Turns the trace into an AABB sweep |
| `Radius( float radius )` | Turns the trace into a sphere sweep |
| `Body( PhysicsBody body, Vector3 to )` | Sweeps an existing physics body |

**Narrowing what can be hit:**

| Method | What it does |
|--------|-------------|
| `WithTag( string tag )` | Requires this tag; stack calls to AND them together |
| `WithAllTags( params string[] tags )` | Requires every tag listed |
| `WithAnyTags( params string[] tags )` | Requires at least one of the listed tags |
| `WithoutTags( params string[] tags )` | Excludes anything carrying one of these tags |
| `WithCollisionRules( string tag )` | Filters using the project's collision rule matrix for this tag |
| `IgnoreGameObject( GameObject obj )` | Excludes one specific object |
| `IgnoreGameObjectHierarchy( GameObject obj )` | Excludes that object and every child below it |
| `HitTriggers()` | Lets trigger colliders register as hits too |
| `HitTriggersOnly()` | Restricts hits to trigger colliders only |
| `IgnoreStatic()` | Excludes static objects |
| `IgnoreDynamic()` | Excludes dynamic objects |

**Execution flags:**

| Method | What it does |
|--------|-------------|
| `UseHitboxes( bool )` | Makes hitbox components eligible to be hit |
| `UsePhysicsWorld( bool )` | Makes physics objects eligible to be hit (on by default) |

**Running it:**

| Method | Returns | What it does |
|--------|---------|-------------|
| `Run()` | `SceneTraceResult` | Returns the first thing hit |
| `RunAll()` | `IEnumerable<SceneTraceResult>` | Returns every hit along the path |

### Reading a SceneTraceResult

| Field | Type | What it holds |
|-------|------|-------------|
| `Hit` | `bool` | True if anything was hit |
| `StartPosition` | `Vector3` | Where the trace began |
| `EndPosition` | `Vector3` | The hit point, or the far end of the trace if nothing was hit |
| `Normal` | `Vector3` | Surface normal at the point of impact |
| `Distance` | `float` | Distance travelled from start to end |
| `Fraction` | `float` | Position along the trace expressed as 0 to 1 |
| `GameObject` | `GameObject` | The object that got hit |
| `Component` | `Component` | The component that got hit |
| `Collider` | `Collider` | The collider that got hit |
| `Body` | `PhysicsBody` | The physics body that got hit |
| `Surface` | `Surface` | Surface material at the hit point |
| `Bone` | `int` | Index of the bone that was hit |
| `Hitbox` | `Hitbox` | The hitbox that was hit, populated when `UseHitboxes` is on |
| `Tags` | `string[]` | Tags carried by the shape that was hit |
| `Direction` | `Vector3` | Direction the trace travelled in |
| `HitPosition` | `Vector3` | The precise hit position, only set if `UseHitPosition()` was called on the builder |
| `Shape` | `PhysicsShape` | The physics shape that was hit |
| `Triangle` | `int` | Triangle index, for mesh shapes |
| `StartedSolid` | `bool` | True if the trace started already inside geometry |

---

## Physics World

Reachable through `Scene.PhysicsWorld`.

```csharp
// Gravity
Vector3 gravity = Scene.PhysicsWorld.Gravity;  // default: (0, 0, -800)
Scene.PhysicsWorld.Gravity = new Vector3( 0, 0, -400 );  // low gravity

// Configuration
Scene.PhysicsWorld.SubSteps = 2;       // substeps per tick
Scene.PhysicsWorld.TimeScale = 0.5f;   // slow-mo physics
```

| Property | Type | What it does |
|----------|------|-------------|
| `Gravity` | `Vector3` | Gravity applied across the whole world |
| `AirDensity` | `float` | Density used for air drag |
| `SubSteps` | `int` | Substeps run per physics tick |
| `TimeScale` | `float` | Scales how fast physics simulates |
| `SimulationMode` | `PhysicsSimulationMode` | Discrete or Continuous |
| `SleepingEnabled` | `bool` | Whether bodies are allowed to sleep |

### Hooking the Physics Step

Implement `IScenePhysicsEvents` on a component to get a callback on either side of the
physics step:

```csharp
public sealed class MyPhysicsHook : Component, IScenePhysicsEvents
{
    void IScenePhysicsEvents.PrePhysicsStep() { }   // after FixedUpdate, before physics
    void IScenePhysicsEvents.PostPhysicsStep() { }  // after physics step
}
```

---

## Collision System

### Configuring Collision Rules

Set this up under Project Settings > Collision. The tag matrix defined there decides
what can collide with what, and it's the exact same matrix a trace consults when you
call `WithCollisionRules`:

```csharp
// Use collision rules in a trace
Scene.Trace.Ray( start, end ).WithCollisionRules( "bullet" ).Run();
```

### Listening for Collisions and Triggers

`scene-and-components.md` covers `ICollisionListener` and `ITriggerListener` in full; here's the
short version:

```csharp
// Collision: requires Rigidbody + Collider
public sealed class Bullet : Component, Component.ICollisionListener
{
    public void OnCollisionStart( Collision collision )
    {
        var hit = collision.Other.GameObject;
        var damageable = hit.GetComponent<Component.IDamageable>();
        damageable?.OnDamage( new DamageInfo { Damage = 25f, Attacker = GameObject } );
        GameObject.Destroy();
    }
}

// Trigger: Collider with IsTrigger = true
public sealed class PickupZone : Component, Component.ITriggerListener
{
    public void OnTriggerEnter( Collider other )
    {
        if ( other.GameObject.Tags.Has( "player" ) )
            GivePickup( other.GameObject );
    }
}
```

---

## Math Types

### Which Way Is Forward

The axis convention here is **Z-up, X-forward, Y-left**:
- `Vector3.Forward` equals `(1, 0, 0)`, +X
- `Vector3.Right` equals `(0, -1, 0)`, -Y
- `Vector3.Up` equals `(0, 0, 1)`, +Z

Anyone arriving from Unity gets bitten by this exactly once: Unity is Y-up and
Z-forward, and carrying that assumption over means your "forward" vector is actually
pointing up, so the first `Rotation.LookAt` call you make sends the camera rolling onto
its side.

### Vector3

```csharp
// Constants
Vector3.Zero, Vector3.One, Vector3.Forward, Vector3.Backward,
Vector3.Up, Vector3.Down, Vector3.Left, Vector3.Right, Vector3.Random

// Construction
var v = new Vector3( x, y, z );
v.WithX( 10 ).WithZ( 0 )         // component replacement

// Operations
v.Normal                           // normalized (unit length)
v.Length / v.LengthSquared         // magnitude
v.IsNearZeroLength                 // nearly zero check
Vector3.Dot( a, b )               // dot product
Vector3.Cross( a, b )             // cross product
Vector3.Lerp( a, b, frac )        // linear interpolation
Vector3.Slerp( a, b, frac )        // spherical interpolation
Vector3.DistanceBetween( a, b )   // distance
Vector3.Direction( from, to )     // normalized direction
Vector3.Reflect( dir, normal )    // reflection off surface
v.Clamp( min, max )               // component clamp
v.ClampLength( maxLen )            // clamp magnitude
v.SubtractDirection( normal )      // cancel velocity along normal
v.ProjectOnNormal( normal )        // project onto normal
v.SnapToGrid( gridSize )          // snap to grid

// Physics helpers
v.WithFriction( amount, stopSpeed ) // apply friction
v.WithAcceleration( target, accel ) // accelerate toward target
v.AddClamped( toAdd, maxLength )    // add with length cap
Vector3.SmoothDamp( current, target, ref vel, smoothTime, dt )

// Angle between two directions
float angle = Vector3.GetAngle( dir1, dir2 );
```

### Rotation

```csharp
// Constants
Rotation.Identity, Rotation.Random

// Construction
Rotation.FromAxis( Vector3.Up, 90f )          // axis + degrees
Rotation.LookAt( direction )                  // face direction (Up = Z)
Rotation.LookAt( direction, upVector )         // with custom up
Rotation.From( pitch, yaw, roll )              // from euler angles
Rotation.From( angles )                        // from Angles struct
Rotation.FromYaw( 45f )                        // single axis
Rotation.FromPitch( 10f )
Rotation.FromToRotation( fromDir, toDir )      // rotation between directions

// Properties
rot.Forward, rot.Backward, rot.Up, rot.Down, rot.Right, rot.Left
rot.Inverse                                     // inverse rotation
rot.Angles()                                    // → Angles (pitch, yaw, roll)
rot.Pitch(), rot.Yaw(), rot.Roll()             // individual angles

// Operations
Rotation.Lerp( a, b, frac )                   // linear interpolation
Rotation.Slerp( a, b, frac )                  // spherical (smooth) interpolation
rot.Distance( other )                           // angular distance in degrees
rot.Clamp( target, maxDegrees )                // clamp rotation
rot * Vector3.Forward                          // rotate a vector
rot * otherRotation                            // combine rotations
Rotation.Difference( from, to )                // rotation from A to B
Rotation.SmoothDamp( current, target, ref vel, smoothTime, dt )
```

### Angles

Represents Euler angles in three parts: `pitch` for up and down, `yaw` for left and
right, `roll` for tilt.

```csharp
var angles = new Angles( pitch, yaw, roll );
angles.Normal                       // normalized to -180..180
angles.ToRotation()                 // → Rotation
angles.Forward                      // forward direction vector
Angles.Lerp( from, to, frac )
angles.WithYaw( 90f )              // replace single component
```

### Transform

Bundles position, rotation and scale together, and is what world/local conversions
operate on.

```csharp
var tx = new Transform( position, rotation, scale );
tx.Forward, tx.Up, tx.Right         // direction vectors
tx.PointToWorld( localPoint )       // local → world
tx.PointToLocal( worldPoint )       // world → local
tx.NormalToWorld( localNormal )     // transform a direction
Transform.Lerp( a, b, frac )       // interpolate all components
```

### BBox (Bounding Box)

```csharp
var box = new BBox( mins, maxs );
var box = new BBox( center, size );     // centered box
box.Center, box.Size, box.Extents
box.Contains( point ), box.Overlaps( other )
box.ClosestPoint( point )
box.Grow( amount )                       // expand by amount
BBox.FromHeightAndRadius( h, r )
BBox.FromPositionAndSize( pos, size )
```

### Ray

```csharp
var ray = new Ray( origin, direction );
ray.Position                         // origin
ray.Forward                          // direction
ray.Project( distance )              // point at distance along ray
```

---

## Time

```csharp
Time.Now          // float, seconds since game startup
Time.Delta        // float, frame delta time
Time.NowDouble    // double precision time
```

### TimeSince

Counts upward starting at zero: assign it `0` to reset the clock, then compare it to a
number to check how much time has passed.

```csharp
TimeSince lastShot = 0;

protected override void OnUpdate()
{
    if ( Input.Pressed( "attack1" ) && lastShot > 0.5f )
    {
        Fire();
        lastShot = 0;  // reset
    }
}
```

It converts implicitly to `float`, giving you the elapsed seconds directly.

### TimeUntil

Counts down toward zero instead: assign it a number of seconds to arm it, and it
converts implicitly to `bool`, true once the countdown expires.

```csharp
TimeUntil nextSpawn = 5f;  // 5 seconds from now

protected override void OnUpdate()
{
    if ( nextSpawn )  // true when countdown hits 0
    {
        SpawnEnemy();
        nextSpawn = 10f;  // reset to 10 seconds
    }
}
```

It also exposes `Relative` for time still remaining, `Passed` for time elapsed since it
was set, and `Fraction` for progress from 0 to 1.

---

## The Game Static Class

```csharp
Game.ActiveScene              // current Scene
Game.IsEditor                 // running in editor
Game.IsPlaying                // actively playing a scene
Game.InGame                   // in a game (not main menu)
Game.IsRunningInVR            // VR mode
Game.Random                   // shared Random instance (auto-seeded per tick)
Game.SteamId                  // local player's Steam ID
```

---

## Surface Materials

Represents the physical material assigned to a collider or physics shape, and it's
where friction, impact sounds and impact effects all come from.

```csharp
// From a trace result
Surface surface = traceResult.Surface;
float friction = surface.Friction;
surface.PlayCollisionSound( hitPosition, speed );

// Find by name
var metal = Surface.FindByName( "metal" );
```

| Property | Type | What it holds |
|----------|------|-------------|
| `Friction` | `float` | How much friction the surface has |
| `Elasticity` | `float` | How bouncy the surface is |
| `Density` | `float` | Mass per cubic metre, kg/m^3 |
| `ImpactEffects` | n/a | Particle effects fired on impact |
| `Sounds` | n/a | Sounds played on impact |
| `Tags` | `string` | Tags carried by the surface |

---

## Gizmo (Debug Drawing)

`Gizmo.Draw`, called from inside `DrawGizmos()`, draws debug visuals that only appear in
the editor:

```csharp
protected override void DrawGizmos()
{
    Gizmo.Draw.Color = Color.Red;
    Gizmo.Draw.LineSphere( WorldPosition, 50f );
    Gizmo.Draw.Arrow( WorldPosition, WorldPosition + Vector3.Forward * 100f );
    Gizmo.Draw.SolidBox( new BBox( -10, 10 ) );
    Gizmo.Draw.WorldText( "Hello", new Transform( WorldPosition + Vector3.Up * 60f ) );
}
```

The draw calls you'll reach for most:
- `Line( a, b )`, `Arrow( from, to )`
- `LineSphere( center, radius )`, `SolidSphere( center, radius )`
- `LineBBox( bbox )`, `SolidBox( bbox )`
- `LineCapsule( capsule )`, `SolidCapsule( start, end, radius )`
- `LineCircle( center, radius )`, `SolidCylinder( start, end, radius )`
- `Model( model, transform )`
- `WorldText( text, transform )`, `ScreenText( text, position )`

And three properties that affect all of them: `Color`, `IgnoreDepth`, `LineThickness`

---

## Two Worked Patterns

### First-Person Camera and Movement

```csharp
public sealed class FPSController : Component
{
    [Property] public CharacterController Controller { get; set; }
    [Property] public float Speed { get; set; } = 200f;
    [Property] public float JumpForce { get; set; } = 300f;

    Angles eyeAngles;

    protected override void OnUpdate()
    {
        // Mouse look
        eyeAngles += Input.AnalogLook;
        eyeAngles.pitch = eyeAngles.pitch.Clamp( -89f, 89f );
        WorldRotation = Rotation.From( eyeAngles );
    }

    protected override void OnFixedUpdate()
    {
        // Movement
        var wishDir = Input.AnalogMove * WorldRotation;

        if ( Controller.IsOnGround )
        {
            Controller.Accelerate( wishDir * Speed );
            Controller.ApplyFriction( 5f );

            if ( Input.Pressed( "jump" ) )
                Controller.Punch( Vector3.Up * JumpForce );
        }
        else
        {
            Controller.Accelerate( wishDir * Speed * 0.2f );
            Controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
        }

        Controller.Move();
    }
}
```

### A Minimal Hitscan Weapon

```csharp
void Fire()
{
    var ray = Scene.Camera.ScreenPixelToRay( Screen.Size / 2f );
    var tr = Scene.Trace.Ray( ray, 5000f )
        .UseHitboxes( true )
        .WithoutTags( "player_local" )
        .IgnoreGameObjectHierarchy( GameObject )
        .Run();

    if ( !tr.Hit ) return;

    var damageable = tr.GameObject.GetComponent<Component.IDamageable>();
    damageable?.OnDamage( new DamageInfo
    {
        Damage = 25f,
        Attacker = GameObject,
        Position = tr.EndPosition
    } );
}
```
