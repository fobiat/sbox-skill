<!--
  s&box Skill : 01_SCENE.md

  Scene, GameObject and Component: the object model, lifecycle, prefabs and scene events.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Scene, GameObject, Component

This is the object model that every other page in this skill assumes you already know: the Scene system, GameObjects, Components, their lifecycle, editor Properties, Prefabs, events, and the systems that tie it together. It comes straight from the **26.08.05** engine source (`sbox-public`); the code snippets are usage patterns layered on top of those APIs, not internals copied out of the engine.

## The Shape of the Object Model

s&box arranges everything into a **Scene > GameObject > Component** hierarchy. A `Scene` holds any number of `GameObject`s, each one carrying a transform plus whatever `Component`s you've attached, and your actual gameplay logic lives inside those `Component` subclasses.

One detail worth flagging early: `Scene` is a subclass of `GameObject`, not a separate wrapper type sitting above one. It occupies the root position in the hierarchy directly. That's why hierarchy checks such as `IsRoot` and `Root`, covered further down, behave identically whether you're inspecting a leaf object or the scene itself.

***

## Working with GameObjects

A `GameObject` is best thought of as a container: transform, tags, child objects, and whatever components are attached to it all live there.

### Creating One and Getting Rid of It

```csharp
// Create empty GameObject
var go = new GameObject();
go.Name = "MyObject";

// Create as child
var child = new GameObject( true, "Child" );
child.SetParent( go );

// Destroy
go.Destroy();

// Check if still valid (not destroyed)
if ( go.IsValid() ) { /* safe to use */ }
```

### The Transform

By default a GameObject's transform is stored relative to its parent. The world-space accessors handle the math needed to resolve that into an absolute value.

```csharp
// World space
go.WorldPosition = new Vector3( 100, 0, 50 );
go.WorldRotation = Rotation.FromAxis( Vector3.Up, 90f );
go.WorldScale = Vector3.One * 2f;

// Local space (relative to parent)
go.LocalPosition = new Vector3( 10, 0, 0 );
go.LocalRotation = Rotation.Identity;
go.LocalScale = Vector3.One;

// Full transform struct
go.WorldTransform = new Transform( position, rotation, scale );
```

`GameTransform` adds an interpolation layer on top of the raw transform values:
- `GameObject.Transform.Position` / `.Rotation` / `.Scale`: world-space values
- `GameObject.Transform.LocalPosition` / `.LocalRotation` / `.LocalScale`: values relative to the parent
- `Transform.LerpTo( Transform target, float frac )`: eases smoothly toward a target transform
- `Transform.ClearInterpolation()`: skips straight to the final position, no interpolation

### Tagging

A tag is just a string underneath, but tags **flow downward**: a child picks up every tag its parent carries, on top of whatever tags it adds itself. That inheritance only runs one way, so there's no way to strip an inherited tag from a single child; the tag lives on the parent, and removing it there is the only way to clear it out of the whole subtree beneath.

```csharp
go.Tags.Add( "enemy" );
go.Tags.Remove( "enemy" );
go.Tags.Set( "enemy", isEnemy );  // conditional
bool has = go.Tags.Has( "enemy" );
```

### Navigating the Hierarchy

```csharp
go.Children                              // List<GameObject>
go.Parent                                // GameObject or null
go.SetParent( other, keepWorldPosition: true );
go.IsRoot                                // true if parented to scene
go.Root                                  // root ancestor
go.IsDescendant( other )                 // hierarchy check
go.IsAncestor( other )
```

### The Properties You'll Reach For Most

| Property | Type | What it tells you |
|---|---|---|
| `Scene` | `Scene` | The scene that owns this object |
| `Enabled` | `bool` | Whether the object itself is switched on |
| `Active` | `bool` | True only if `Enabled` holds here and on every ancestor above it |
| `IsValid` | `bool` | Flips to false the moment the object is destroyed |
| `IsProxy` | `bool` | True for a networked object owned by some other client |
| `Id` | `Guid` | A unique identifier for the object |
| `Flags` | `GameObjectFlags` | Bit flags: `Hidden`, `NotSaved`, `DontDestroyOnLoad`, and others |
| `Network` | `NetworkAccessor` | Where the networking API is reached from |

***

## Working with Components

Your gameplay logic actually lives in Component subclasses, and any one instance belongs to exactly one `GameObject`, never more.

### Writing Your Own Component

```csharp
public sealed class MyComponent : Component
{
    [Property] public float Speed { get; set; } = 200f;
    [Property] public GameObject Target { get; set; }

    protected override void OnUpdate()
    {
        if ( Target is null || !Target.IsValid() ) return;

        var direction = (Target.WorldPosition - WorldPosition).Normal;
        WorldPosition += direction * Speed * Time.Delta;
    }
}
```

A few things to notice in that example:
- `sealed class`: keep components sealed by default; only drop it when something genuinely needs to inherit.
- `[Property]`: makes the field editable from the inspector and tells the serializer to persist it.
- `WorldPosition`, `WorldRotation`, and similar are available directly on the component, not only on `GameObject`; they simply forward to `GameObject.WorldPosition` and its siblings, so you don't have to type `GameObject.` every time.
- The same applies to `Scene`, `GameObject`, `Transform`, `Components`, and `Tags`, all reachable directly from any component.

### Adding, Finding, and Iterating Components

```csharp
// Add (optional: startEnabled parameter, defaults true)
var renderer = go.AddComponent<ModelRenderer>();
var renderer = go.GetOrAddComponent<ModelRenderer>();  // idempotent

// Query single (optional: includeDisabled, defaults false)
var c = go.GetComponent<ModelRenderer>();
var c = go.GetComponentInChildren<ModelRenderer>();   // also: includeDisabled, includeSelf
var c = go.GetComponentInParent<ModelRenderer>();     // also: includeDisabled, includeSelf

// Query multiple (same optional params)
var all = go.GetComponents<ModelRenderer>();
var all = go.GetComponentsInChildren<ModelRenderer>();
var all = go.GetComponentsInParent<ModelRenderer>();

// Advanced: FindMode flags
var c = go.Components.Get<ModelRenderer>( FindMode.Disabled | FindMode.InAncestors );
var all = go.Components.GetAll<ModelRenderer>( FindMode.Enabled | FindMode.InSelf | FindMode.InChildren );
var everything = go.Components.GetAll();  // all components on this GameObject

// Scene-wide fast lookup
var game = Scene.Get<GameManager>();
foreach ( var model in Scene.GetAll<ModelRenderer>() ) { }
```

### Removing Components

```csharp
component.Destroy();           // remove component from its GameObject
component.DestroyGameObject(); // destroy the entire GameObject
// also: GameObject.Destroy()
```

### Referencing Components From the Inspector

```csharp
// Drag-and-drop reference in editor
[Property] ModelRenderer BodyRenderer { get; set; }

// Auto-create if missing
[RequireComponent] ModelRenderer BodyRenderer { get; set; }
```

***

## Component Lifecycle

Every lifecycle hook is a `protected virtual` method declared on `Component`, and you implement one by overriding it in your subclass. All of them return `void` except `OnLoad`, which returns `async Task`.

### The Order Things Run In

```
Scene Load
  └─ OnLoad (async): loading screen stays open until all complete
  └─ OnValidate: after deserialization / property changes
  └─ OnAwake: once, when created, if parent GameObject enabled

Per Frame (for enabled components):
  ┌─ OnStart: once, before first update
  ├─ OnFixedUpdate: every fixed timestep (use for physics/movement)
  ├─ OnUpdate: every frame
  └─ OnPreRender: every frame, after bone calculations (NOT on dedicated server)

State Changes:
  ├─ OnEnabled: when component becomes enabled
  ├─ OnDisabled: when component becomes disabled
  └─ OnDestroy: once, when destroyed
```

**Being "enabled" takes three things holding at once: the component's own `Enabled` flag, its `GameObject`'s enabled state, and the enabled state of every ancestor GameObject above it.**

### What Each Hook Is For

| Method | Timing | Notes |
| --- | --- | --- |
| `OnLoad()` | Right after deserialization | Returns `async Task`; the loading screen stays up until it resolves. A natural place for procedural generation. |
| `OnValidate()` | On a property change, or during deserialization | Useful for clamping or validating property values. Not really a lifecycle hook in the strict sense. |
| `OnAwake()` | Once, right after load, provided the parent is enabled | Your setup point, always runs ahead of `OnStart`. |
| `OnStart()` | Once, ahead of the first update | Runs the first time the component is enabled, and always before the first `OnFixedUpdate`. |
| `OnEnabled()` | Every time the component turns enabled | Good place to wire subscriptions or start effects. |
| `OnUpdate()` | Once per frame | Where ordinary per-frame logic goes. |
| `OnFixedUpdate()` | Once per fixed timestep | For physics, movement, and traces; the right home for `CharacterController` movement. |
| `OnPreRender()` | Every frame, once bones are calculated (skipped on dedicated server) | Visual-only adjustments. **Never runs on a dedicated server.** |
| `OnDisabled()` | Every time the component turns disabled | Undo whatever `OnEnabled` set up. |
| `OnDestroy()` | Once, at destruction | The final opportunity for cleanup. |

### A Few More Virtual Hooks

| Method | What For |
| --- | --- |
| `OnParentChanged(GameObject old, GameObject new)` | Fires on reparenting |
| `OnTagsChanged()` | Fires whenever a tag is added or removed |
| `OnRefresh()` | Fires once a network snapshot refresh completes |
| `DrawGizmos()` | Editor-only, for drawing debug gizmos |

### Don't Rely on Cross-Object Ordering

**You cannot count on the relative order the same callback fires in across different GameObjects.** Nothing guarantees it stays stable run to run. If one object's callback genuinely needs to run before another's, put that dependency into a `GameObjectSystem` with its explicit stage and order controls, rather than hoping component ordering cooperates.

***

## Opt-In Component Interfaces

On top of the base lifecycle, implementing one of these interfaces alongside `Component` opts you into extra engine callbacks.

### ExecuteInEditor

A component's lifecycle runs only in Play mode by default. Adding `Component.ExecuteInEditor` makes `OnAwake`, `OnEnabled`, `OnDisabled`, `OnUpdate`, and `OnFixedUpdate` fire in edit mode too, handy for editor tooling and gizmo-driven authoring helpers.

```csharp
public sealed class MyEditorTool : Component, Component.ExecuteInEditor
{
    protected override void OnUpdate()
    {
        if ( Game.IsEditor ) { /* editor-only logic */ }
    }
}
```

### ICollisionListener

For reacting to physics collisions; nothing fires unless a collider exists somewhere on the same GameObject or one of its children.

```csharp
public sealed class HitDetector : Component, Component.ICollisionListener
{
    public void OnCollisionStart( Collision collision ) { }   // first contact
    public void OnCollisionUpdate( Collision collision ) { }  // sustained contact (per physics step)
    public void OnCollisionStop( CollisionStop collision ) { } // separation
}
```

### ITriggerListener

Think of it as `ICollisionListener`'s non-physical sibling, firing when something overlaps a trigger volume.

```csharp
public sealed class TriggerZone : Component, Component.ITriggerListener
{
    public void OnTriggerEnter( Collider other ) { }
    public void OnTriggerExit( Collider other ) { }
}
```

### IDamageable

The engine's standard interface for damage. Query it generically through `Components.Get<IDamageable>()` rather than hard-coding a specific component type, so any damageable thing plugs into the same code path.

```csharp
public sealed class Health : Component, Component.IDamageable
{
    [Property] public float HP { get; set; } = 100f;

    public void OnDamage( in DamageInfo damage )
    {
        HP -= damage.Damage;
        if ( HP <= 0 ) GameObject.Destroy();
    }
}
```

Among its fields, `DamageInfo` carries `float Damage`, `GameObject Attacker`, and `Vector3 Position`.

### IPressable

This is the interface behind "walk up and press E": doors, buttons, levers, world
pickups, ATMs, vending machines, and anything else the stock `PlayerController` should let a
player interact with on purpose. `ITriggerListener` fires from *walking into* something;
`IPressable` fires from *choosing to activate* it.

```csharp
public sealed class Lever : Component, Component.IPressable
{
    public bool Press( Component.IPressable.Event e )      // the only required member
    {
        Toggle( e.Source.GameObject );   // e.Source is the pressing PlayerController
        return true;                     // true => Release() will be called later
    }

    public bool CanPress( Component.IPressable.Event e ) => !IsJammed;

    public Component.IPressable.Tooltip? GetTooltip( Component.IPressable.Event e )
        => new( "Lever", "touch_app", IsOn ? "Turn off" : "Turn on" );
}
```

Every member other than `Press` already ships with a default implementation, defined at
`Scene/Components/Markers/IPressable.cs:13-67`:

| Member | Fires |
| --- | --- |
| `bool Press( Event e )` | **Required.** Fires the moment a press starts. Return `true` on success |
| `bool CanPress( Event e )` | Defaults to `true`. Gates both whether the press is allowed and whether the tooltip appears |
| `bool Pressing( Event e )` | Runs every frame the button stays held. Return `false` to cancel it |
| `void Release( Event e )` | Fires once the press ends. The interface's own documentation claims this only happens "if `Press` returned `true`", but the shipped `PlayerController` actually disregards `Press`'s return value entirely and tracks/releases the pressed object regardless (`PlayerController.Pressing.cs:157,160`; `StopPressing` `:120-133`). Don't write logic that assumes that pairing holds |
| `void Hover( Event e )` | Fires the instant the player's look lands on it |
| `void Look( Event e )` | Fires each frame the player's look stays on it |
| `void Blur( Event e )` | Fires once the player looks elsewhere |
| `Tooltip? GetTooltip( Event e )` | Defaults to `null`. Return a value to show a prompt |

`Event` is declared as `record struct Event( Component Source, Ray? Ray = default )`, where
`Source` is the pressing `PlayerController`, so `e.Source.GameObject` is how you get at the
player. `Tooltip` is declared as `record struct Tooltip( string Title, string Icon, string
Description, bool Enabled = true, IPressable Pressable = default )`; the controller fills in
`Pressable` on its own and ANDs `Enabled` together with `CanPress` before the tooltip reaches
`PlayerController.Tooltips`.

> **`Press` executes on the CLIENT doing the pressing, never on the host.** The whole
> hover/press pipeline runs out of `PlayerController.OnUpdate` inside `if ( !IsProxy )`
> (`PlayerController.DefaultControls.cs:41-49`), making it purely local input handling.
> Anything `Press` needs to do authoritatively has to route through an `[Rpc.Host]` call, the
> same pattern the engine's own `Door` follows: `IPressable.Press` calls
> `Toggle( e.Source.GameObject )`, and `Toggle` itself carries `[Rpc.Host]`
> (`Scene/Components/Map/Door.cs:283-290`, `:361-362`).

Here's how the stock controller wires all of this together (`PlayerController.Input.cs`, `PlayerController.Pressing.cs`):

- `EnablePressing` defaults `true` (`Input.cs:38`); `UseButton` defaults `"use"`
  (`Input.cs:43`); `ReachLength` defaults `130` units (`Input.cs:48`).
- `TryGetLookedAt()` (`Pressing.cs:205-266`) does target selection: a trace fired from the
  eye out to `ReachLength`, retried at radius `0`, then `2`, then `4`, so small props are
  easy to hit and gaps don't block the reach entirely. It also calls `HitTriggers()` and
  skips the player's own hierarchy.
- The lookup itself is `GetComponentsInParent<IPressable>( includeSelf: true )`, so the
  interface is allowed to live on a **parent** of the hit collider, not necessarily on the
  collider's own GameObject.
- Reading `Hovered`, `Pressed`, and `Tooltips` off the controller gives you everything needed
  to draw your own prompt UI.
- `PlayerController.IEvents` is how other components on the player hook in: it exposes
  `GetUsableComponent( GameObject )`, `StartPressing`, `StopPressing`, and `FailPressing`.
- **`UseLookControls = false` silently disables pressing as a side effect.**
  `UpdateLookAt()` only runs inside that same branch, so switching off look controls drags
  pressing down with it.

See `13_EXAMPLES.md` → *Example 11* for a complete worked version.

***

## Properties (Editor Attributes)

`[Property]` is what surfaces a field or property in the editor inspector and flags it for serialization. Everything listed below stacks on top of it, shaping how that field looks and behaves once exposed.

### Attributes You'll Use Constantly

| Attribute | What It Does |
| --- | --- |
| `[Property]` | Exposes to the inspector and serializes |
| `[Hide]` | Serializes but stays out of the inspector |
| `[RequireComponent]` | Creates the component automatically if it's missing |
| `[Group( "Name" )]` | Groups fields visually in the inspector |
| `[ToggleGroup( "BoolPropName" )]` | Groups fields behind an enable/disable checkbox |
| `[Title( "Display Name" )]` | Overrides the displayed name |
| `[Range( min, max )]` | A numeric slider bounded by min/max (clamped by default) |
| `[Step( n )]` | Sets the increment step for a numeric field |
| `[ReadOnly]` | Shows the value but blocks editing |
| `[ShowIf( "Prop", value )]` | Shows the field conditionally |
| `[HideIf( "Prop", value )]` | Hides the field conditionally |
| `[Feature( "Tab" )]` | Puts the field in its own inspector tab |
| `[FeatureEnabled( "Tab" )]` | A bool that toggles a feature tab on or off |
| `[Order( n )]` | Controls where the property sits in order |
| `[Header( "text" )]` | Adds a section header above the property |
| `[Space]` | Adds a visual spacer |
| `[InlineEditor]` | Expands a struct or class inline |
| `[Advanced]` | Stays hidden until the user asks to see advanced options |
| `[Flags]` | Turns an enum into a multi-select |

### For String Fields

| Attribute | Purpose |
| --- | --- |
| `[TextArea]` | A multi-line text box |
| `[Placeholder( "hint" )]` | Placeholder text shown when empty |
| `[InputAction]` | A dropdown listing configured input actions |
| `[ImageAssetPath]` | An image file picker |
| `[MapAssetPath]` | A map file picker |
| `[FontName]` | A font dropdown |
| `[FilePath]` | A general-purpose file picker |

### Validation

```csharp
[Property, Validate( nameof(IsSpeedValid), "Speed must be positive", LogLevel.Warn )]
public float Speed { get; set; } = 100f;

bool IsSpeedValid() => Speed > 0;
```

***

## Prefabs

A Prefab is nothing more than a reusable `GameObject` template, saved to disk as a `.prefab` file. Editing the prefab asset updates every instance of it automatically, across every scene it appears in.

### Spawning a Prefab From Code

```csharp
public sealed class Spawner : Component
{
    [Property] public GameObject Prefab { get; set; }  // drag PrefabFile here

    protected override void OnUpdate()
    {
        if ( Input.Pressed( "attack1" ) )
        {
            // Clone at position
            GameObject instance = Prefab.Clone( WorldPosition );

            // Clone with position + rotation
            GameObject instance2 = Prefab.Clone( WorldPosition, WorldRotation );

            // Break link to prefab source (becomes regular GameObjects)
            instance.BreakFromPrefab();
        }
    }
}
```

`GameObject.Clone()` ships with 11 overloads, though a `Vector3 position` and an optional `Rotation rotation` cover most cases.

### Overriding a Single Instance

A prefab instance dropped into a scene can carry its own property overrides, extra components, or extra child GameObjects, all layered on top without touching the source prefab. Those overrides are stored per-instance and stick around even after the source prefab changes.

### Loading a Prefab Without a Reference

```csharp
// Load a prefab by file path
var prefab = GameObject.GetPrefab( "prefabs/bullet.prefab" );
var instance = prefab.Clone( WorldPosition );
```

***

## Scene Events

This is how you broadcast custom events out to every active Component and GameObjectSystem in a scene and have them listen back. These stay strictly **local**: none of it crosses the network.

### Declaring an Event Interface

```csharp
public interface IPlayerEvent : ISceneEvent<IPlayerEvent>
{
    void OnSpawned( Player player ) { }
    void OnDied( Player player ) { }
}
```

Deriving the interface from `ISceneEvent<T>` gets you static `Post()` and `PostToGameObject()` helpers for free. Because every method has a default, empty body, a listener only has to implement the specific events it actually cares about.

### Sending an Event

```csharp
// To all listeners in scene
IPlayerEvent.Post( x => x.OnSpawned( player ) );

// To a specific GameObject only
IPlayerEvent.PostToGameObject( target.GameObject, x => x.OnDied( player ) );

// Raw Scene.RunEvent also works on any type
Scene.RunEvent<SkinnedModelRenderer>( x => x.Tint = Color.Red );
```

### Implementing a Listener

Implementing the interface on any Component or GameObjectSystem is all it takes to listen:

```csharp
public sealed class ScoreTracker : Component, IPlayerEvent
{
    void IPlayerEvent.OnDied( Player player )
    {
        Log.Info( $"{player.Name} died" );
    }
}
```

### Interfaces the Engine Ships With

| Interface | Events | What For |
| --- | --- | --- |
| `ISceneStartup` | `OnHostPreInitialize`, `OnHostInitialize`, `OnClientInitialize` | Scene and game initialization |
| `ISceneLoadingEvents` | `AfterLoad` | Setup that runs right after a scene loads |
| `IScenePhysicsEvents` | Physics callbacks | Handling physics events |
| `IGameObjectNetworkEvents` | Network lifecycle | Reacting to network state changes |

`ISceneStartup` is the one to know well for game initialization: `OnHostInitialize` fires once the scene finishes loading on the host, your cue to spawn cameras and stand up lobbies, and `OnClientInitialize` fires on the host and on every client alike, for spawning anything client-side.

***

## GameObjectSystem

Exactly one `GameObjectSystem` exists per scene. It hooks into specific frame stages and processes components in bulk rather than one at a time, and the engine instantiates it automatically for every scene, you never construct one yourself.

### Defining a System

```csharp
public class GravitySystem : GameObjectSystem<GravitySystem>
{
    public GravitySystem( Scene scene ) : base( scene )
    {
        // Listen to a specific stage with explicit order
        Listen( Stage.StartUpdate, 0, ApplyGravity, "ApplyGravity" );
    }

    void ApplyGravity()
    {
        foreach ( var body in Scene.GetAllComponents<GravityBody>() )
        {
            body.Velocity += Vector3.Down * 800f * Time.Delta;
        }
    }
}
```

### Available Stages

| Stage | Timing |
| --- | --- |
| `StartUpdate` | At the start of the frame update |
| `UpdateBones` | After animation, before rendering |
| `PhysicsStep` | During the `FixedUpdate` physics tick |
| `Interpolation` | During the transform interpolation pass |
| `FinishUpdate` | At the end of the frame update |
| `StartFixedUpdate` | At the start of the fixed update |
| `FinishFixedUpdate` | At the end of the fixed update |
| `SceneLoaded` | Once scene load completes |

### Reaching a System Instance

```csharp
// Via generic static property (requires GameObjectSystem<T>)
GravitySystem.Current.DoSomething();

// Via scene lookup
var system = Scene.GetSystem<GravitySystem>();
```

Systems can implement `ISceneStartup` and the other event interfaces too, which makes them a natural fit for game-manager code.

### Configuring a System

Any `[Property]` declared on a system appears as a configurable setting under Project Settings > Systems, saved on a per-project basis.

***

## Working With Async Code

Async work in s&box runs on the **main thread**, so a coroutine-based mental model carries over directly if that's where you're coming from. It's the standard way to write gameplay logic that unfolds over time or through a sequence of steps.

```csharp
protected override void OnStart()
{
    _ = SpawnWaves();  // fire-and-forget from sync code
}

async Task SpawnWaves()
{
    for ( int wave = 0; wave < 10; wave++ )
    {
        SpawnEnemies( wave );
        await Task.DelaySeconds( 30f );  // wait 30 seconds between waves
    }
}
```

Worth knowing:
- `await Task.DelaySeconds( float )`: pauses for a given number of game-time seconds
- `await Task.DelayRealtimeSeconds( float )`: pauses for real-time seconds instead, reachable via `Component.Task`
- `await Task.Frame()`: pauses for exactly one frame
- `await Task.WhenAll( task1, task2 )`: runs multiple tasks at once

**Cancellation:** every component has a `Task` property that cancels itself the moment its GameObject stops being valid. Decide ahead of time what your method should do if the object gets destroyed mid-`await`: cancellation stops the rest of the method from running, but whatever it already changed before that point stays changed.

***

## Coming From Unity: A Translation Table

Most Unity muscle memory carries straight over if that's your background, just under different names. Here's the mapping:

| Unity Habit (avoid in s&box) | s&box Equivalent (use this) |
|-------------------------------|------------------------|
| `class Foo : MonoBehaviour` | `class Foo : Component` |
| `void Start()` | `protected override void OnStart()` |
| `void Update()` | `protected override void OnUpdate()` |
| `void FixedUpdate()` | `protected override void OnFixedUpdate()` |
| `void OnEnable()` | `protected override void OnEnabled()` |
| `void OnDisable()` | `protected override void OnDisabled()` |
| `void OnDestroy()` | `protected override void OnDestroy()` |
| `void Awake()` | `protected override void OnAwake()` |
| `GetComponent<T>()` | `GetComponent<T>()` (same, but also `Components.Get<T>()` for FindMode) |
| `FindObjectOfType<T>()` | `Scene.Get<T>()` or `Scene.GetAll<T>()` |
| `Instantiate( prefab )` | `prefab.Clone( position )` |
| `transform.position` | `WorldPosition` or `GameObject.WorldPosition` |
| `transform.localPosition` | `LocalPosition` or `GameObject.LocalPosition` |
| `[SerializeField]` | `[Property]` |
| `[HideInInspector]` | `[Hide]` |
| `[Header("X")]` | `[Header("X")]` (same) |
| `[Range(0,1)]` | `[Range(0,1)]` (same) |
| `StartCoroutine()` | `_ = MyAsyncMethod()` (native async/await) |
| `yield return new WaitForSeconds(n)` | `await Task.DelaySeconds(n)` |
| `Debug.Log()` | `Log.Info()` |
| `Destroy( gameObject )` | `GameObject.Destroy()` or `DestroyGameObject()` |
| `gameObject.SetActive( false )` | `GameObject.Enabled = false` |
| `Application.isPlaying` | `Game.IsEditor` (inverted sense) |
| `SceneManager.LoadScene()` | `Scene.Load()` or `Scene.LoadFromFile()` |
| `DontDestroyOnLoad( go )` | `go.Flags = GameObjectFlags.DontDestroyOnLoad` |
| `Physics.Raycast()` | `Scene.Trace.Ray()` (see 05_INPUT_PHYSICS.md) |
| `OnCollisionEnter()` | Implement `Component.ICollisionListener` interface |
| `OnTriggerEnter()` | Implement `Component.ITriggerListener` interface |

***

## What .NET APIs Are Off-Limits

Your code runs against a whitelist of permitted .NET APIs. That's a platform security boundary, not a style suggestion, so there's no quiet workaround for anything you intend to publish. The ones worth knowing:

| Blocked API | Use Instead |
| --- | --- |
| `System.IO.*` (file I/O) | Use the s&box Filesystem API instead |
| `Console.Log` | `Log.Info()` |
| Raw sockets, HTTP clients | Use the s&box `Http` class instead |

Editor-only code and libraries fall outside the whitelist entirely. A standalone build can turn the whitelist off too, but doing so gives up the ability to publish that build to the platform.
