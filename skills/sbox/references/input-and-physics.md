# Input & Physics

Input system, SceneTrace (raycasting), physics, collision, math types, and time utilities.

---

## Input

All input is accessed through the static `Input` class. Actions are strings configured in Project Settings.

### Querying Actions

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

| Method | Description |
|--------|-------------|
| `Input.Down( string action )` | Currently held down |
| `Input.Pressed( string action )` | Pressed this frame (was up, now down) |
| `Input.Released( string action )` | Released this frame (was down, now up) |
| `Input.Clear( string action )` | Clear action state |
| `Input.ReleaseActions()` | Release all actions |

Action names are **case-insensitively matched against `ProjectSettings/Input.config`**
(`Input.Actions.cs:49`, `OrdinalIgnoreCase`) — `Input.Down( "use" )` resolves the action
declared as `"Use"`. An undeclared name logs `Couldn't find Input Action called "..."`
**once per name per session** and returns `false` forever after
(`Input.Actions.cs:71-86`) — so a typo produces one line you will scroll past and then
silence. `Input.Down` also takes `complainOnMissing = false` to suppress even that. All
three queries return `false` when `Application.IsHeadless` — a dedicated server never sees
input.

### Project Settings Configs

Three JSON files under `ProjectSettings/` drive engine behaviour that has no C# entry
point. They are plain `ConfigData` documents — an object with the settings plus a
`__guid` / `__schema` / `__type` / `__version` envelope.

| File | Backing type | Controls |
|---|---|---|
| `Input.config` | `InputSettings` | The `Actions` list — every input action name and its default bind |
| `Platform.config` | `PlatformSettings` | Built-in text chat and subtitle overlays |
| `Collision.config` | `CollisionRules` | Tag-vs-tag collision matrix |

Reachable in code as `ProjectSettings.Input`, `ProjectSettings.Platform`,
`ProjectSettings.Collision` — alongside `.Networking`, `.Physics` and `.Systems`, each
lazily loaded from its own `*.config` (`Systems/Project/ProjectSettings/ProjectSettings.cs:5-45`).

**`Input.config`** — an `InputAction` is `{ Name, GroupName, Title, KeyboardCode,
GamepadCode }` (`Systems/Input/InputAction.cs:10`, fields at `:39,45,51,57,63`;
`InputSettings` merely holds the `Actions` list). The stock set ships 31 actions in groups
`Movement` / `Actions` / `Inventory` / `Other`. Reserved bindings you will collide with if
you invent your own (stock defaults per `Systems/Input/Input.Common.cs:14-58`):

| Action | Key | Note |
|---|---|---|
| `Use` | `e` | `PlayerController.UseButton` — the `IPressable` press button |
| `Score` | `tab` | The obvious scoreboard key is already taken |
| `Chat` | `enter` | Bound even when you ship your own chat |
| `Voice` | `v` | |
| `Menu` | `Q` | Stock default (`Input.Common.cs:57`). This project rebinds it to `i` in its own `ProjectSettings/Input.config` — do not read the project file as the stock set |
| `Drop` | `g` | |
| `Flashlight` | `f` | |
| `View` | `C` | |
| `Slot0`-`Slot9` | `0`-`9` | Plus `SlotPrev` / `SlotNext` on `mouse4` / `mouse5` |

> **Nothing watches these files.** An externally edited `.config` (or `.sbproj`) does not
> reach the running editor — reload or restart. See
> `editor-and-verified-behaviour.md` → *Two hard-won workflow facts*.

### Platform Chat — the invisible global side-channel

`Sandbox.Platform.Chat` is a **platform-level** pipe that exists whether or not your
gamemode knows about it (`Systems/Chat/Chat.cs`). Client sends to host, host validates and
rate-limits (0.66 s per user), host broadcasts to a filtered recipient set.

```
static bool Enabled          => ProjectSettings.Platform.ChatEnabled          // :17
static bool ShowUI           => ProjectSettings.Platform.ChatShowUI           // :22
static int  MaxMessageLength => ProjectSettings.Platform.ChatMaxMessageLength // :27
static void Say( string message )      // client -> host -> everyone      // :38
static void AddText( string message )  // local-only notification         // :96
```

Observe or veto messages with the `IChatEvent` scene event:

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

> **`ChatShowUI: false` does not disable chat — it only hides the overlay.** `Say()`
> checks `Enabled`, never `ShowUI` (`Chat.cs:40`), so with the UI off the pipe is still
> live and invisible: any code path that reaches `Say` still broadcasts to every client,
> outside your gamemode's channels, permissions and logging. The engine also auto-
> broadcasts "X has joined / left the game" notifications through it (`Chat.cs:143-169`).
> **A gamemode with its own chat must set `"ChatEnabled": false` in
> `ProjectSettings/Platform.config`** — that is the only switch that closes the pipe.
>
> Second-order trap: `ChatEnabled` returns `false` for a *published* game that shipped
> without a `Platform.config`, regardless of the stored value
> (`PlatformSettings.cs:18-25`). Chat that works in the editor can be dead in the build.

### Analog Input

```csharp
Vector3 moveDir = Input.AnalogMove;    // WASD or left stick (x=forward, y=right, z=up)
Angles lookDir = Input.AnalogLook;     // mouse delta or right stick (scaled by sensitivity)
```

| Property | Type | Description |
|----------|------|-------------|
| `AnalogMove` | `Vector3` | Movement input (keyboard or left stick) |
| `AnalogLook` | `Angles` | Look input (mouse or right stick), sensitivity-scaled |
| `MouseDelta` | `Vector2` | Raw mouse movement delta |
| `MouseWheel` | `Vector2` | Mouse wheel state |
| `EscapePressed` | `bool` | Escape key pressed (set to false to override pause menu) |
| `UsingController` | `bool` | Last input was from a controller |
| `ControllerCount` | `int` | Connected controllers |
| `Suppressed` | `bool` | Suppress all input |

### Controller-Specific

```csharp
float trigger = Input.GetAnalog( InputAnalog.LeftTrigger );  // -1 to 1

// Haptics
Input.TriggerHaptics( leftMotor: 0.5f, rightMotor: 0.7f, duration: 500 );
Input.TriggerHaptics( HapticEffect.HardImpact, lengthScale: 1f, frequencyScale: 1f, amplitudeScale: 0.5f );
Input.StopAllHaptics();

// Local multiplayer — scope input to a specific controller
using ( Input.PlayerScope( playerIndex ) )
{
    if ( Input.Pressed( "jump" ) ) { /* controller-specific */ }
}
```

`InputAnalog` enum: `LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`, `LeftTrigger`, `RightTrigger`

### Input Glyphs

```csharp
// Get controller button texture for UI display
Texture glyph = Input.GetGlyph( "jump" );
Texture outlined = Input.GetGlyph( "jump", outline: true );
string keyName = Input.GetButtonOrigin( "jump" );  // e.g. "SPACE" or "A Button"
```

### Raw Keyboard Input

Bypass action mapping for direct key access:

```csharp
if ( Input.Keyboard.Pressed( "w" ) ) { }
if ( Input.Keyboard.Down( "space" ) ) { }
```

---

## Mouse & Screen

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

Builder-pattern API for physics traces. Access via `Scene.Trace`.

### Basic Traces

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

### Builder Methods

**Shape:**

| Method | Description |
|--------|-------------|
| `Ray( Vector3 from, Vector3 to )` | Line trace |
| `Ray( Ray ray, float distance )` | Line from ray |
| `Sphere( float radius, Vector3 from, Vector3 to )` | Sphere sweep |
| `Box( BBox bbox, Vector3 from, Vector3 to )` | Box sweep |
| `Capsule( Capsule capsule, Vector3 from, Vector3 to )` | Capsule sweep |
| `Size( BBox hull )` / `Size( Vector3 size )` | Make trace an AABB |
| `Radius( float radius )` | Make trace a sphere |
| `Body( PhysicsBody body, Vector3 to )` | Sweep a physics body |

**Filtering:**

| Method | Description |
|--------|-------------|
| `WithTag( string tag )` | Only hit objects with this tag (AND with other WithTag calls) |
| `WithAllTags( string[] tags )` | Must have all tags |
| `WithAnyTags( string[] tags )` | Must have any of these tags |
| `WithoutTags( string[] tags )` | Exclude objects with any of these tags |
| `WithCollisionRules( string tag )` | Use project collision rules for this tag |
| `IgnoreGameObject( GameObject obj )` | Skip this object |
| `IgnoreGameObjectHierarchy( GameObject obj )` | Skip object and all children |
| `HitTriggers()` | Include trigger colliders |
| `HitTriggersOnly()` | Only hit triggers |
| `IgnoreStatic()` | Skip static objects |
| `IgnoreDynamic()` | Skip dynamic objects |

**Options:**

| Method | Description |
|--------|-------------|
| `UseHitboxes( bool )` | Hit hitbox components |
| `UsePhysicsWorld( bool )` | Hit physics objects (default: true) |

**Execute:**

| Method | Returns | Description |
|--------|---------|-------------|
| `Run()` | `SceneTraceResult` | First hit |
| `RunAll()` | `IEnumerable<SceneTraceResult>` | All hits |

### SceneTraceResult

| Field | Type | Description |
|-------|------|-------------|
| `Hit` | `bool` | Whether something was hit |
| `StartPosition` | `Vector3` | Trace start |
| `EndPosition` | `Vector3` | Hit point (or trace end if no hit) |
| `Normal` | `Vector3` | Hit surface normal |
| `Distance` | `float` | Distance from start to end |
| `Fraction` | `float` | 0..1 fraction along trace |
| `GameObject` | `GameObject` | Hit object |
| `Component` | `Component` | Hit component |
| `Collider` | `Collider` | Hit collider |
| `Body` | `PhysicsBody` | Hit physics body |
| `Surface` | `Surface` | Surface material of hit |
| `Bone` | `int` | Hit bone index |
| `Hitbox` | `Hitbox` | Hit hitbox (if UseHitboxes) |
| `Tags` | `string[]` | Tags on hit shape |
| `Direction` | `Vector3` | Trace direction |
| `HitPosition` | `Vector3` | Precise hit position (requires `UseHitPosition()` on builder) |
| `Shape` | `PhysicsShape` | Hit physics shape |
| `Triangle` | `int` | Triangle index (mesh shapes) |
| `StartedSolid` | `bool` | Trace started inside geometry |

---

## Physics World

Access via `Scene.PhysicsWorld`.

```csharp
// Gravity
Vector3 gravity = Scene.PhysicsWorld.Gravity;  // default: (0, 0, -800)
Scene.PhysicsWorld.Gravity = new Vector3( 0, 0, -400 );  // low gravity

// Configuration
Scene.PhysicsWorld.SubSteps = 2;       // substeps per tick
Scene.PhysicsWorld.TimeScale = 0.5f;   // slow-mo physics
```

| Property | Type | Description |
|----------|------|-------------|
| `Gravity` | `Vector3` | World gravity |
| `AirDensity` | `float` | Air drag density |
| `SubSteps` | `int` | Physics substeps per tick |
| `TimeScale` | `float` | Physics time scale |
| `SimulationMode` | `PhysicsSimulationMode` | Discrete or Continuous |
| `SleepingEnabled` | `bool` | Allow body sleeping |

### Physics Events

Implement `IScenePhysicsEvents` to hook into the physics step:

```csharp
public sealed class MyPhysicsHook : Component, IScenePhysicsEvents
{
    void IScenePhysicsEvents.PrePhysicsStep() { }   // after FixedUpdate, before physics
    void IScenePhysicsEvents.PostPhysicsStep() { }  // after physics step
}
```

---

## Collision System

### Collision Rules

Configure in Project Settings > Collision. Tag-based matrix determines what collides with what.

```csharp
// Use collision rules in a trace
Scene.Trace.Ray( start, end ).WithCollisionRules( "bullet" ).Run();
```

### Collision & Trigger Events

See `core-concepts.md` for `ICollisionListener` and `ITriggerListener`. Quick reminder:

```csharp
// Collision — requires Rigidbody + Collider
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

// Trigger — Collider with IsTrigger = true
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

### Coordinate System

s&box uses **Z-up, X-forward, Y-left**:
- `Vector3.Forward` = `(1, 0, 0)` (+X)
- `Vector3.Right` = `(0, -1, 0)` (-Y)
- `Vector3.Up` = `(0, 0, 1)` (+Z)

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
Vector3.Slerp( a, b, frac )       // spherical interpolation
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

Euler angles — `pitch` (up/down), `yaw` (left/right), `roll` (tilt).

```csharp
var angles = new Angles( pitch, yaw, roll );
angles.Normal                       // normalized to -180..180
angles.ToRotation()                 // → Rotation
angles.Forward                      // forward direction vector
Angles.Lerp( from, to, frac )
angles.WithYaw( 90f )              // replace single component
```

### Transform

Position + Rotation + Scale. Used for world/local transforms.

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
Time.Now          // float — seconds since game startup
Time.Delta        // float — frame delta time
Time.NowDouble    // double precision time
```

### TimeSince

Counts up from zero. Assign `0` to reset, compare to check elapsed time.

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

Implicitly converts to `float` (seconds elapsed).

### TimeUntil

Counts down to zero. Assign seconds to set. Converts to `bool` (true when expired).

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

Properties: `Relative` (time remaining), `Passed` (time since start), `Fraction` (0→1 progress).

---

## Game Static

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

## Surface

Physical material on colliders and physics shapes. Determines friction, sounds, and effects.

```csharp
// From a trace result
Surface surface = traceResult.Surface;
float friction = surface.Friction;
surface.PlayCollisionSound( hitPosition, speed );

// Find by name
var metal = Surface.FindByName( "metal" );
```

| Property | Type | Description |
|----------|------|-------------|
| `Friction` | `float` | Surface friction |
| `Elasticity` | `float` | Bounciness |
| `Density` | `float` | kg/m^3 |
| `ImpactEffects` | — | Particle effects on impact |
| `Sounds` | — | Sound effects |
| `Tags` | `string` | Surface tags |

---

## Gizmo (Debug Drawing)

Editor-only debug drawing via `Gizmo.Draw` in `DrawGizmos()`:

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

Key draw methods:
- `Line( a, b )`, `Arrow( from, to )`
- `LineSphere( center, radius )`, `SolidSphere( center, radius )`
- `LineBBox( bbox )`, `SolidBox( bbox )`
- `LineCapsule( capsule )`, `SolidCapsule( start, end, radius )`
- `LineCircle( center, radius )`, `SolidCylinder( start, end, radius )`
- `Model( model, transform )`
- `WorldText( text, transform )`, `ScreenText( text, position )`

Properties: `Color`, `IgnoreDepth`, `LineThickness`

---

## Common Patterns

### FPS Camera + Movement

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

### Hitscan Weapon

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
