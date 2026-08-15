<!--
  s&box Skill : scene-and-components.md

  Scene, GameObject and Component: the object model, lifecycle, prefabs and scene events.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Core Concepts

The object model everything else in this skill depends on: the Scene system, GameObjects, Components, their lifecycle, editor Properties, Prefabs, events, and systems. Pulled directly from the engine source at version **26.08.05** (`sbox-public`); the snippets below are usage patterns built on top of those APIs, not internals lifted from the engine itself.

## How the Pieces Fit Together

Everything in s&box is arranged as **Scene > GameObject > Component**. A `Scene` holds a set of `GameObject`s; each of those carries a transform and any number of `Component`s, and it's inside those `Component` subclasses that you write your actual gameplay logic.

Notably, `Scene` itself is a subclass of `GameObject` rather than a wrapper around one: it sits at the top of the hierarchy as the root, not as a separate container type. This is why hierarchy checks like `IsRoot` and `Root` (covered further down) work the same regardless of whether you're inspecting a leaf object or the scene object itself.

---

## GameObjects

Think of a `GameObject` as a container: it holds a transform, a set of tags, any child objects, and the components attached to it.

### Making and Removing One

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

### Transforms

A GameObject's transform is always expressed relative to its parent; the world-space accessors do the math to resolve the absolute value for you.

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

`GameTransform` layers interpolation on top of the raw transform:
- `GameObject.Transform.Position` / `.Rotation` / `.Scale`: the values in world space
- `GameObject.Transform.LocalPosition` / `.LocalRotation` / `.LocalScale`: the values relative to the parent
- `Transform.LerpTo( Transform target, float frac )`: blend smoothly toward a target transform
- `Transform.ClearInterpolation()`: jump immediately to the final, non-interpolated position

### Tags on GameObjects

A tag is nothing more than a string, but tags **inherit downward**: a child automatically carries every tag set on its parent, alongside whatever tags it sets itself. Because inheritance only flows one direction, you can't strip an inherited tag from just one child; the tag belongs to the parent, and removing it there is the only way to clear it from the whole subtree beneath it.

```csharp
go.Tags.Add( "enemy" );
go.Tags.Remove( "enemy" );
go.Tags.Set( "enemy", isEnemy );  // conditional
bool has = go.Tags.Has( "enemy" );
```

### Parent/Child Relationships

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
|----------|------|--------------------|
| `Scene` | `Scene` | Which scene owns this object |
| `Enabled` | `bool` | Whether this object itself is switched on |
| `Active` | `bool` | True only when `Enabled` is set here and on every ancestor |
| `IsValid` | `bool` | False once the object has been destroyed |
| `IsProxy` | `bool` | True when this is a networked object owned by a different client |
| `Id` | `Guid` | The object's unique identifier |
| `Flags` | `GameObjectFlags` | Bit flags such as `Hidden`, `NotSaved`, `DontDestroyOnLoad` |
| `Network` | `NetworkAccessor` | Entry point for the networking API |

---

## Components

Component subclasses are where your gameplay logic actually lives, and each instance of one belongs to a single `GameObject`, never more than one.

### Writing a Custom Component

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

Notes on what's happening in that snippet:
- `sealed class`: leave your components sealed unless you have an actual reason for something to inherit from them.
- `[Property]`: this is what makes the field editable in the inspector and tells the serializer to persist it.
- `WorldPosition`, `WorldRotation`, and the rest are exposed straight on the component itself, not just on `GameObject`. They forward to `GameObject.WorldPosition` and so on, saving you from typing `GameObject.` constantly.
- The same goes for `Scene`, `GameObject`, `Transform`, `Components`, and `Tags`: all reachable directly from inside any component.

### Adding & Querying Components

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

### Removing / Destroying

```csharp
component.Destroy();           // remove component from its GameObject
component.DestroyGameObject(); // destroy the entire GameObject
// also: GameObject.Destroy()
```

### Component References in Inspector

```csharp
// Drag-and-drop reference in editor
[Property] ModelRenderer BodyRenderer { get; set; }

// Auto-create if missing
[RequireComponent] ModelRenderer BodyRenderer { get; set; }
```

---

## Lifecycle Methods

Each lifecycle hook is a `protected virtual` method on `Component`; you implement one by overriding it in your subclass. They're all `void`-returning except `OnLoad`, which is `async Task`.

### Execution Order

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

**"Enabled" is a three-part condition: the component's own `Enabled` flag has to be set, its `GameObject` has to be enabled, and so does every ancestor GameObject above it.**

### Method Details

| Method | When Called | Notes |
|--------|-----------|-------|
| `OnLoad()` | After deserialization | `async Task`; the loading screen won't dismiss until it finishes. Good spot for procedural generation. |
| `OnValidate()` | Property change / deserialization | For enforcing limits on property values. Doesn't count as a true lifecycle hook. |
| `OnAwake()` | Once, after load, if parent enabled | Your initialization point, guaranteed to run before `OnStart`. |
| `OnStart()` | Once, before first update | Fires the first time the component is enabled, always ahead of the first `OnFixedUpdate`. |
| `OnEnabled()` | Each time component becomes enabled | Wire up subscriptions, kick off effects. |
| `OnUpdate()` | Every frame | Your everyday per-frame logic goes here. |
| `OnFixedUpdate()` | Every fixed timestep | Physics, movement, traces. The right place for `CharacterController` movement. |
| `OnPreRender()` | Every frame, after bone calc | Adjustments to visuals. **Skipped entirely on a dedicated server.** |
| `OnDisabled()` | Each time component becomes disabled | Tear down whatever `OnEnabled` set up. |
| `OnDestroy()` | Once, when destroyed | Last chance for cleanup. |

### Additional Virtual Methods

| Method | Purpose |
|--------|---------|
| `OnParentChanged(GameObject old, GameObject new)` | Fires when the object gets reparented |
| `OnTagsChanged()` | Fires when tags are added or removed |
| `OnRefresh()` | Fires after a network snapshot refresh |
| `DrawGizmos()` | Editor-only hook for debug drawing |

### Execution Order Warning

**The relative order in which the same callback fires across different GameObjects is not something you can count on.** It isn't guaranteed to hold stable from run to run. When one object's callback genuinely has to run before another's, that dependency belongs in a `GameObjectSystem` using its explicit stage/order controls, not in the hope that component ordering happens to cooperate.

---

## Component Interfaces

Beyond the base lifecycle, you can opt into extra engine callbacks by implementing one of these interfaces alongside `Component`.

### ExecuteInEditor

By default, a component's lifecycle only runs while the game is in Play mode. Add `Component.ExecuteInEditor` and `OnAwake`, `OnEnabled`, `OnDisabled`, `OnUpdate`, and `OnFixedUpdate` will also fire in edit mode, useful for editor tooling and gizmo-based authoring helpers.

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

For reacting to physics collisions. Nothing will fire unless there's a collider somewhere on the same GameObject or one of its children.

```csharp
public sealed class HitDetector : Component, Component.ICollisionListener
{
    public void OnCollisionStart( Collision collision ) { }   // first contact
    public void OnCollisionUpdate( Collision collision ) { }  // sustained contact (per physics step)
    public void OnCollisionStop( CollisionStop collision ) { } // separation
}
```

### ITriggerListener

The non-physical sibling of `ICollisionListener`, for reacting when something overlaps a trigger volume.

```csharp
public sealed class TriggerZone : Component, Component.ITriggerListener
{
    public void OnTriggerEnter( Collider other ) { }
    public void OnTriggerExit( Collider other ) { }
}
```

### IDamageable

s&box's standard damage interface. Prefer querying it generically through `Components.Get<IDamageable>()` over hard-coding a particular component type, so anything capable of taking damage plugs into the same code path.

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

`DamageInfo` carries, among other fields: `float Damage`, `GameObject Attacker`, `Vector3 Position`.

### IPressable

The interface behind "walk up and press E": doors, buttons, levers, world
pickups, ATMs, vending machines, and anything else the stock `PlayerController` should let a
player interact with directly. Where `ITriggerListener` fires from *walking into* something,
`IPressable` fires from *deliberately activating* it.

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

Every member but `Press` ships with a default implementation
(`Scene/Components/Markers/IPressable.cs:13-67`):

| Member | When |
|---|---|
| `bool Press( Event e )` | **Required.** Fires when the press begins. Return `true` if it succeeded |
| `bool CanPress( Event e )` | Defaults `true`. Controls both whether the press is allowed and whether the tooltip shows |
| `bool Pressing( Event e )` | Called every frame the button is held. Returning `false` cancels it |
| `void Release( Event e )` | Fires when the press ends. The interface's own doc claims this only happens "if `Press` returned `true`", but the shipped `PlayerController` actually ignores `Press`'s return value and tracks/releases the pressed object no matter what (`PlayerController.Pressing.cs:157,160`; `StopPressing` `:120-133`): don't build logic around that pairing holding |
| `void Hover( Event e )` | Fires the moment the player starts looking at it |
| `void Look( Event e )` | Fires every frame the player keeps looking at it |
| `void Blur( Event e )` | Fires when the player looks away |
| `Tooltip? GetTooltip( Event e )` | Defaults `null`. Return a value to surface a prompt |

`Event` is defined as `record struct Event( Component Source, Ray? Ray = default )`: `Source` is
the `PlayerController` doing the pressing, so `e.Source.GameObject` gets you the player.
`Tooltip` is `record struct Tooltip( string Title, string Icon, string Description, bool
Enabled = true, IPressable Pressable = default )`; the controller fills in `Pressable` itself
and ANDs `Enabled` against `CanPress` before it ends up in `PlayerController.Tooltips`.

> **`Press` runs on the pressing CLIENT, never the host.** The entire hover/press
> pipeline is driven from `PlayerController.OnUpdate` inside `if ( !IsProxy )`
> (`PlayerController.DefaultControls.cs:41-49`), so it's purely local input handling. Whatever
> you need `Press` to do authoritatively has to go through an `[Rpc.Host]` call, the same way
> the engine's own `Door` handles it: `IPressable.Press` calls `Toggle( e.Source.GameObject )`,
> and `Toggle` itself is `[Rpc.Host]` (`Scene/Components/Map/Door.cs:283-290`, `:361-362`).

How the stock controller wires this up (`PlayerController.Input.cs`, `PlayerController.Pressing.cs`):

- `EnablePressing` (default `true`, `Input.cs:38`), `UseButton` (default `"use"`,
  `Input.cs:43`), `ReachLength` (default `130` units, `Input.cs:48`).
- Target selection happens in `TryGetLookedAt()` (`Pressing.cs:205-266`): a trace fired from
  the eye out to `ReachLength`, retried at radius `0`, then `2`, then `4` so small props
  aren't a pain to hit and gaps don't block the reach entirely. It calls `HitTriggers()` and
  skips over the player's own hierarchy.
- Lookup uses `GetComponentsInParent<IPressable>( includeSelf: true )`,
  which means the interface can live on a **parent** of the collider that got hit, not
  necessarily the collider's own GameObject.
- `Hovered`, `Pressed` and `Tooltips`, read off the controller, give you what you need to draw
  your own prompt UI.
- A player's other components can hook in through `PlayerController.IEvents`:
  `GetUsableComponent( GameObject )`, `StartPressing`, `StopPressing`, `FailPressing`.
- **Setting `UseLookControls = false` quietly kills pressing too.** `UpdateLookAt()` only
  runs inside that same branch, so turning off look controls takes pressing down with it.

A full worked example lives in `worked-examples.md` → *Example 11*.

---

## Properties (Editor Attributes)

`[Property]` is the attribute responsible for surfacing a field or property in the editor inspector and marking it for serialization. Everything below stacks on top of it to shape how that field looks and behaves once it's there.

### Common Attributes

| Attribute | Effect |
|-----------|--------|
| `[Property]` | Expose to inspector, serialize |
| `[Hide]` | Serialize but hide from inspector |
| `[RequireComponent]` | Auto-create component if missing |
| `[Group( "Name" )]` | Visual grouping in inspector |
| `[ToggleGroup( "BoolPropName" )]` | Group with enable/disable checkbox |
| `[Title( "Display Name" )]` | Override display name |
| `[Range( min, max )]` | Numeric slider with limits (clamped by default) |
| `[Step( n )]` | Numeric increment step |
| `[ReadOnly]` | Display but disallow editing |
| `[ShowIf( "Prop", value )]` | Conditional visibility |
| `[HideIf( "Prop", value )]` | Conditional hiding |
| `[Feature( "Tab" )]` | Separate tab in inspector |
| `[FeatureEnabled( "Tab" )]` | Bool to toggle feature tab |
| `[Order( n )]` | Control property ordering |
| `[Header( "text" )]` | Section header above property |
| `[Space]` | Visual spacer |
| `[InlineEditor]` | Expand struct/class inline |
| `[Advanced]` | Hidden unless user requests |
| `[Flags]` | Multi-select enum |

### String-Specific

| Attribute | Effect |
|-----------|--------|
| `[TextArea]` | Multi-line text input |
| `[Placeholder( "hint" )]` | Placeholder text |
| `[InputAction]` | Dropdown of configured input actions |
| `[ImageAssetPath]` | Image file picker |
| `[MapAssetPath]` | Map file picker |
| `[FontName]` | Font dropdown |
| `[FilePath]` | General file picker |

### Validation

```csharp
[Property, Validate( nameof(IsSpeedValid), "Speed must be positive", LogLevel.Warn )]
public float Speed { get; set; } = 100f;

bool IsSpeedValid() => Speed > 0;
```

---

## Prefabs

A Prefab is just a `GameObject` template you can reuse, saved to disk as a `.prefab` file. Edit the prefab asset, and every instance of it, across every scene it's placed in, updates automatically to match.

### Spawning in Code

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

`GameObject.Clone()` comes in 11 overloads; most of the time you'll just need a `Vector3 position` and an optional `Rotation rotation`.

### Instance Overrides

Any prefab instance placed in a scene can have its own property overrides, extra components, or extra child GameObjects layered on top, none of which touch the source prefab. These overrides live per-instance and survive whenever the source prefab is updated.

### Static Prefab Loading

```csharp
// Load a prefab by file path
var prefab = GameObject.GetPrefab( "prefabs/bullet.prefab" );
var instance = prefab.Clone( WorldPosition );
```

---

## Scene Events

A way to broadcast custom events to every active Component and GameObjectSystem in a scene, and have them listen back. These events stay **local**: nothing about them crosses the network.

### Defining an Event

```csharp
public interface IPlayerEvent : ISceneEvent<IPlayerEvent>
{
    void OnSpawned( Player player ) { }
    void OnDied( Player player ) { }
}
```

Because the interface derives from `ISceneEvent<T>`, you get static `Post()` and `PostToGameObject()` helpers automatically. Giving every method a default (empty) body means a listener only needs to implement the specific events it actually cares about.

### Broadcasting

```csharp
// To all listeners in scene
IPlayerEvent.Post( x => x.OnSpawned( player ) );

// To a specific GameObject only
IPlayerEvent.PostToGameObject( target.GameObject, x => x.OnDied( player ) );

// Raw Scene.RunEvent also works on any type
Scene.RunEvent<SkinnedModelRenderer>( x => x.Tint = Color.Red );
```

### Listening

Any Component or GameObjectSystem can listen just by implementing the interface:

```csharp
public sealed class ScoreTracker : Component, IPlayerEvent
{
    void IPlayerEvent.OnDied( Player player )
    {
        Log.Info( $"{player.Name} died" );
    }
}
```

### Built-in Event Interfaces

| Interface | Events | Use For |
|-----------|--------|---------|
| `ISceneStartup` | `OnHostPreInitialize`, `OnHostInitialize`, `OnClientInitialize` | Scene/game initialization |
| `ISceneLoadingEvents` | `AfterLoad` | Post-scene-load setup |
| `IScenePhysicsEvents` | Physics callbacks | Physics event handling |
| `IGameObjectNetworkEvents` | Network lifecycle | Network state changes |

`ISceneStartup` is the one worth knowing well for game init: `OnHostInitialize` runs after the scene finishes loading on the host, which is your cue to spawn cameras and stand up lobbies, while `OnClientInitialize` runs on the host and every client alike, for spawning anything client-side.

---

## GameObjectSystem

One `GameObjectSystem` exists per scene. It hooks into particular frame stages and works on components in bulk instead of one instance at a time. The engine spins one up automatically for every scene; you never construct it by hand.

### Creating a System

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

### Stages

| Stage | When |
|-------|------|
| `StartUpdate` | Beginning of frame update |
| `UpdateBones` | After animations, before rendering |
| `PhysicsStep` | During `FixedUpdate` physics tick |
| `Interpolation` | Transform interpolation pass |
| `FinishUpdate` | End of frame update |
| `StartFixedUpdate` | Beginning of fixed update |
| `FinishFixedUpdate` | End of fixed update |
| `SceneLoaded` | After scene load completes |

### Access

```csharp
// Via generic static property (requires GameObjectSystem<T>)
GravitySystem.Current.DoSomething();

// Via scene lookup
var system = Scene.GetSystem<GravitySystem>();
```

Because systems can implement `ISceneStartup` and the other event interfaces, they end up being a natural fit for game managers.

### Configuration

Any `[Property]` on a system shows up as a configurable setting under Project Settings > Systems, and is saved per-project.

---

## Async in Components

Async work in s&box executes on the **main thread**, so if you're coming from a coroutine-based mental model, that maps over directly. It's the idiomatic way to write gameplay logic that unfolds over time or in sequence.

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

The APIs worth knowing:
- `await Task.DelaySeconds( float )`: pause for a number of game-time seconds
- `await Task.DelayRealtimeSeconds( float )`: pause for real-time seconds instead (reachable via `Component.Task`)
- `await Task.Frame()`: pause for exactly one frame
- `await Task.WhenAll( task1, task2 )`: run several tasks concurrently

**Cancellation:** every component exposes a `Task` property that cancels automatically once its GameObject stops being valid. It's worth thinking through, ahead of time, what your method should do if the object is destroyed mid-`await`: cancellation halts the rest of the method, but anything it already changed before that point stays changed.

---

## Unity Anti-Pattern Table

If you're coming from Unity, most of your muscle memory carries over, just renamed. Here's the translation table:

| Unity Pattern (WRONG in s&box) | s&box Pattern (CORRECT) |
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
| `Physics.Raycast()` | `Scene.Trace.Ray()` (see input-traces-and-physics.md) |
| `OnCollisionEnter()` | Implement `Component.ICollisionListener` interface |
| `OnTriggerEnter()` | Implement `Component.ITriggerListener` interface |

---

## .NET Restrictions

Your code runs against a whitelist of allowed .NET APIs, and that's a platform security boundary rather than a style guideline, meaning there's no quiet way around it for anything you plan to publish. The main ones to know:

| Blocked | Alternative |
|---------|-------------|
| `System.IO.*` (file I/O) | Use s&box Filesystem API |
| `Console.Log` | `Log.Info()` |
| Raw sockets, HTTP clients | Use s&box `Http` class |

Editor-only code and libraries sit outside the whitelist entirely. A standalone build can also disable the whitelist, but doing so forfeits the ability to publish that build to the platform.
