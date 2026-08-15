<!--
  s&box Skill : 02_COMPONENTS.md

  The built-in component library: rendering, physics, movement, camera, lighting, audio, navigation.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Component Library

Every component you'll actually reach for day to day lives here: rendering, physics, character movement, the player controller, props, inventory, camera, lighting, audio, navigation and effects, each entry pulled straight from engine source at version 26.08.05. Every type below lives in the `Sandbox` namespace unless a line says otherwise.

***

## Rendering

### ModelRenderer

A bare-bones renderer: it draws a static mesh at the GameObject's position and nothing more. No skeleton, no bones, no animation; for those, skip ahead to SkinnedModelRenderer.

```csharp
var renderer = go.AddComponent<ModelRenderer>();
renderer.Model = Model.Load( "models/dev/box.vmdl" );
renderer.Tint = Color.Red;
renderer.MaterialOverride = Material.Load( "materials/custom.vmat" );
```

| Property | Type | What it holds |
|:---|:---|:---|
| `Model` | `Model` | The model asset being drawn |
| `Tint` | `Color` | Multiplies the render color |
| `MaterialOverride` | `Material` | Replaces every material on the model |
| `Materials` | `MaterialAccessor` | Accessor for individual materials |
| `BodyGroups` | `ulong` | Bitmask of the active body groups |
| `MaterialGroup` | `string` | Name of the currently active material group |
| `RenderType` | `ShadowRenderType` | Controls how shadows are cast |
| `LodOverride` | `int?` | Pins rendering to a single LOD level |
| `CreateAttachments` | `bool` | Spawns a child GameObject for each model attachment |
| `Bounds` | `BBox` | Axis-aligned bounds in world space, read-only |
| `SceneObject` | `SceneObject` | The scene object this renderer wraps |

Useful methods:
- `SetBodyGroup( string name, int value )` / `SetBodyGroup( string name, string choice )`
- `GetBodyGroup( string name )` → `int`
- `SetMaterial( Material material, int triangle = -1 )`: assigns a material to a single triangle
- `GetMaterial( int triangle = -1 )` → `Material`
- `ClearMaterialOverrides()`
- `SetMaterialOverride( Material material, string target )`: swaps a material chosen by attribute name
- `GetAttachmentObject( string name )` → `GameObject`: the child object living at that attachment point
- `GetBoneObject( BoneCollection.Bone bone )` → `GameObject`

Also inherits from the `Renderer` base:
- `RenderOptions RenderOptions`: flags controlling render behavior
- `Attributes` → `RenderAttributes`: shader attribute block
- `ExecuteBefore` / `ExecuteAfter` → `CommandList`: hooks for injecting custom render commands

### SkinnedModelRenderer (sealed)

Builds on everything `ModelRenderer` does and adds a skeleton on top: bone objects, morph targets, IK, and the animgraph that drives all three.

```csharp
var body = go.AddComponent<SkinnedModelRenderer>();
body.Model = Model.Load( "models/citizen/citizen.vmdl" );

// Animation parameters (animgraph)
body.Set( "move_direction", moveDir );
body.Set( "move_speed", speed );
body.Set( "b_grounded", isGrounded );
body.Set( "b_jump", true );

// IK
body.SetIk( "foot_left", footTransform );
body.ClearIk( "foot_left" );

// Look direction
body.SetLookDirection( "aim_eyes", lookDirection );
body.SetLookDirection( "aim_head", lookDirection, 0.5f );
```

| Property | Type | What it holds |
|:---|:---|:---|
| `CreateBoneObjects` | `bool` | Spawns a GameObject per bone |
| `BoneMergeTarget` | `SkinnedModelRenderer` | Merges this model's bones onto another (clothing, attachments) |
| `UseAnimGraph` | `bool` | Turns the animgraph on, usually disabled for ragdolls |
| `AnimationGraph` | `AnimationGraph` | Swaps in a different animgraph asset |
| `PlaybackRate` | `float` | Multiplier applied to animation speed |
| `RootMotion` | `Transform` | The root motion delta for the current frame |
| `Parameters` | `ParameterAccessor` | Reads and writes animgraph parameters |
| `Morphs` | `MorphAccessor` | Accessor for morph targets |
| `Sequence` | `SequenceAccessor` | Plays a sequence directly |
| `OnFootstepEvent` | `Action<FootstepEvent>` | Fires on a footstep anim event |
| `OnGenericEvent` | `Action<GenericEvent>` | Fires on any other anim event |
| `OnSoundEvent` | `Action<SoundEvent>` | Fires when the animation triggers a sound |

Useful methods:
- `Set( string name, float/int/bool/Vector3/Rotation value )`: writes an animgraph parameter
- `GetFloat/GetInt/GetBool/GetVector/GetRotation( string name )`: reads one back
- `ClearParameters()`: resets every parameter to default
- `SetIk( string name, Transform tx )`: sets an IK target, enabling ik.{name}.enabled plus its position and rotation
- `ClearIk( string name )`: turns that IK target back off
- `SetLookDirection( string name, Vector3 direction, float weight = 1f )`
- `TryGetBoneTransform( string boneName, out Transform tx )` → `bool`: world-space transform of a bone
- `TryGetBoneTransformLocal( string boneName, out Transform tx )` → `bool`
- `GetBoneObject( string boneName )` → `GameObject`
- `GetBoneObject( int index )` → `GameObject`
- `GetAttachment( string name, bool worldSpace = true )` → `Transform?`
- `SetBoneTransform( BoneCollection.Bone bone, Transform transform )`
- `PostAnimationUpdate()`: forces an immediate animation update instead of waiting for the next frame

### Additional Renderer Components

| Component | What It Does |
|-----------|-----------|
| `DecalRenderer` | Projects a material onto nearby surfaces. Inherits the Renderer base properties. |
| `Decal` (sealed) | PBR decal with full material control. Notable properties: `Material`, `Size`, `Color`, lifetime, animated effects. |
| `LineRenderer` (sealed) | Draws a line through a series of points, with 22 properties covering width, color and curves. |
| `SpriteRenderer` (sealed) | Draws a flat 2D sprite. Properties: `Texture`, `Color`, `Size`, `FlipHorizontal/Vertical`. |
| `TextRenderer` (sealed) | Draws text in 3D space. Properties: `Text`, `Color`, `FontSize`, `FontFamily`. |
| `TrailRenderer` (sealed) | Leaves a trail behind a moving object. Properties: `Color`, `Width`, `Lifetime`, `Face`. |
| `BeamEffect` (sealed) | A laser or beam-style visual. Properties: `Targets`, `Width`, `Color`, `Speed`. |

***

## Physics

### Rigidbody (sealed)

Turns a GameObject into a simulated physics body. It needs a `Collider` somewhere on itself or a child object: without one to collide against, a Rigidbody is just numbers nobody reads.

```csharp
var rb = go.AddComponent<Rigidbody>();
rb.Gravity = true;
rb.Mass = 10f;

// Forces: continuous, reapply every frame
rb.ApplyForce( Vector3.Up * 500f );
rb.ApplyForceAt( hitPosition, explosionForce );
rb.ApplyTorque( Vector3.Up * 100f );

// Impulses: instantaneous, apply once
rb.ApplyImpulse( Vector3.Forward * 1000f );
rb.ApplyImpulseAt( hitPosition, bulletForce );

// Smooth kinematic movement
rb.SmoothMove( targetTransform, 0.1f, Time.Delta );
```

| Property | Type | What it holds |
|:---|:---|:---|
| `Gravity` | `bool` | Whether gravity affects this body |
| `GravityScale` | `float` | Multiplier applied to gravity |
| `LinearDamping` | `float` | Damps linear velocity over time |
| `AngularDamping` | `float` | Damps angular velocity over time |
| `Mass` | `float` | Computed mass, read-only unless overridden |
| `MassOverride` | `float` | Replaces the computed mass |
| `Velocity` | `Vector3` | Current linear velocity |
| `AngularVelocity` | `Vector3` | Current angular velocity |
| `Locking` | `PhysicsLock` | Locks specific position or rotation axes |
| `MotionEnabled` | `bool` | Whether the body is allowed to move |
| `Sleeping` | `bool` | Current physics sleep state |
| `StartAsleep` | `bool` | Begins asleep rather than active |
| `RigidbodyFlags` | `RigidbodyFlags` | Extra behavioral flags |
| `CollisionEventsEnabled` | `bool` | Whether collision events fire |
| `Touching` | `IEnumerable<Collider>` | Colliders currently in contact |
| `Joints` | `IReadOnlySet<Joint>` | Joints attached to this body |
| `PhysicsBody` | `PhysicsBody` | The underlying physics body |

Useful methods:
- `ApplyForce( Vector3 )` / `ApplyForceAt( Vector3 position, Vector3 force )`
- `ApplyTorque( Vector3 )`
- `ApplyImpulse( Vector3 )` / `ApplyImpulseAt( Vector3 position, Vector3 force )`
- `ClearForces()`
- `SmoothMove( Transform target, float timeToArrive, float timeDelta )`: interpolates kinematically toward a target
- `SmoothMove( Vector3 position, float timeToArrive, float timeDelta )`
- `SmoothRotate( Rotation rotation, float timeToArrive, float timeDelta )`
- `GetVelocityAtPoint( Vector3 position )` → `Vector3`
- `FindClosestPoint( Vector3 position )` → `Vector3`
- `ResetInertiaTensor()`

### Collision Shapes

Every collider shape derives from the abstract `Collider` base and shares this set of properties:

| Property | Type | What it holds |
|:---|:---|:---|
| `IsTrigger` | `bool` | Marks it a trigger volume with no physical response |
| `Static` | `bool` | Marks the collider as non-moving |
| `Friction` | `float?` | Overrides surface friction |
| `Elasticity` | `float?` | Overrides bounciness |
| `Surface` | `Surface` | Surface material used for footsteps and impacts |
| `SurfaceVelocity` | `Vector3` | Velocity applied like a conveyor belt |
| `Touching` | `IEnumerable<Collider>` | Colliders currently overlapping this one |
| `Rigidbody` | `Rigidbody` | The attached rigidbody, if there is one |
| `Joints` | `IReadOnlySet<Joint>` | Joints attached here |

Each shape then contributes its own fields:

| Component | Shape-Specific Fields |
|:--|:--|
| `BoxCollider` (sealed) | `Vector3 Scale`, `Vector3 Center` |
| `SphereCollider` (sealed) | `Vector3 Center`, `float Radius` |
| `CapsuleCollider` | `Vector3 Start`, `Vector3 End`, `float Radius` |
| `HullCollider` (sealed) | `PrimitiveType Type`, `Vector3 Center`, `Vector3 BoxSize`, `float Height`, `float Radius` |
| `ModelCollider` | `Model Model`: collision geometry pulled from the model's own physics data |
| `PlaneCollider` (sealed) | `Vector2 Scale`, `Vector3 Center`, `Vector3 Normal` |
| `MeshComponent` (sealed) | An editable polygon mesh with collision built in |

### CharacterController

Movement with collision but no Rigidbody underneath. Each step it traces against the world and slides along whatever it hits, rather than running a full rigid-body simulation, which is exactly what makes it predictable enough to build player movement on.

```csharp
[Property] public CharacterController Controller { get; set; }

protected override void OnFixedUpdate()
{
    if ( Controller.IsOnGround )
    {
        if ( Input.Pressed( "jump" ) )
            Controller.Punch( Vector3.Up * 300f );

        Controller.Accelerate( wishDir * 200f );
        Controller.ApplyFriction( 5f );
    }
    else
    {
        Controller.Accelerate( wishDir * 50f );
        Controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
    }

    Controller.Move();
}
```

| Property | Type | What it holds |
|:---|:---|:---|
| `Radius` | `float` | Radius of the movement capsule |
| `Height` | `float` | Height of the movement capsule |
| `StepHeight` | `float` | Tallest step it can climb automatically |
| `GroundAngle` | `float` | Steepest slope treated as walkable |
| `Acceleration` | `float` | Scales how quickly velocity ramps up internally |
| `Bounciness` | `float` | 0 stops dead on a wall, 1 bounces fully |
| `Velocity` | `Vector3` | This tick's velocity vector |
| `IsOnGround` | `bool` | Whether it's currently grounded |
| `GroundObject` | `GameObject` | What it's currently standing on |
| `GroundCollider` | `Collider` | The ground's collider |
| `IgnoreLayers` | `TagSet` | Tags excluded from traces |
| `UseCollisionRules` | `bool` | Whether project collision rules apply |
| `BoundingBox` | `BBox` | The controller's collision bounds right now, read-only |

Useful methods:
- `Move()`: moves along the current `Velocity`, tracing and sliding as it goes
- `MoveTo( Vector3 targetPosition, bool useStep )`: traces and slides directly toward a position
- `Accelerate( Vector3 wishVelocity )`: adds acceleration, automatically scaled by time
- `ApplyFriction( float amount, float stopSpeed = 140f )`
- `Punch( Vector3 amount )`: detaches from the ground and adds velocity, useful for jumps
- `TraceDirection( Vector3 direction )` → `SceneTraceResult`

### Joint Types

All joint types derive from the abstract `Joint` class and link two physics bodies together.

| Joint Type | What It Constrains |
|:--|:--|
| `FixedJoint` (sealed) | Welds two objects together rigidly |
| `HingeJoint` (sealed) | Rotation around a single axis, for doors and wheels. Properties: `MinAngle`, `MaxAngle`, `Friction`. |
| `BallJoint` (sealed) | Free rotation, like a shoulder. Properties: `SwingLimit`, `TwistLimit`. |
| `SliderJoint` (sealed) | Translation along a single axis, for drawers. |
| `SpringJoint` (sealed) | A spring connection with configurable spring and damping values. |
| `WheelJoint` (sealed) | Simulates a vehicle wheel. |

***

## Camera

### CameraComponent (sealed)

Every scene needs at least one of these to draw anything at all. It renders the scene outward from its own GameObject's transform.

```csharp
var cam = go.AddComponent<CameraComponent>();
cam.FieldOfView = 90f;
cam.ZNear = 1f;
cam.ZFar = 10000f;

// Screen-to-world conversion
Ray ray = cam.ScreenPixelToRay( Mouse.Position );

// World-to-screen conversion
Vector2 screenPos = cam.PointToScreenPixels( worldPosition );
```

| Property | Type | What it holds |
|:---|:---|:---|
| `FieldOfView` | `float` | Field of view, in degrees |
| `FovAxis` | `Axis` | Axis the FOV value applies to |
| `ZNear` | `float` | Near clip plane distance |
| `ZFar` | `float` | Far clip plane distance |
| `Priority` | `int` | Higher priority cameras render on top |
| `IsMainCamera` | `bool` | Whether this is the main camera (read-only) |
| `Orthographic` | `bool` | Switches to orthographic projection |
| `OrthographicHeight` | `float` | View height under orthographic projection |
| `ClearFlags` | `ClearFlags` | What gets cleared before rendering |
| `BackgroundColor` | `Color` | Color used to clear the background |
| `RenderTags` | `TagSet` | Restricts rendering to tagged objects |
| `RenderExcludeTags` | `TagSet` | Excludes tagged objects from rendering |
| `Viewport` | `Vector4` | Normalized viewport rectangle |
| `RenderTarget` | `Texture` | Renders into a texture instead of the screen |
| `Hud` | `HudPainter` | Draws before post-processing runs |
| `Overlay` | `HudPainter` | Draws on top of everything else |
| `EnablePostProcessing` | `bool` | Toggles post-process effects |

Useful methods:
- `ScreenPixelToRay( Vector2 pixelPosition )` → `Ray`: converts a mouse position into a world ray
- `ScreenNormalToRay( Vector3 normalPosition )` → `Ray`: converts normalized screen coordinates into a ray
- `PointToScreenPixels( Vector3 worldPosition )` → `Vector2`: converts a world point to screen pixels
- `PointToScreenNormal( Vector3 worldPosition )` → `Vector2`: converts a world point to normalized screen space
- `ScreenToWorld( Vector2 screen )` → `Vector3`: finds the point on the near plane for a screen coordinate
- `GetFrustum()` → `Frustum`
- `RenderToTexture( Texture target, ViewSetup config = null )` → `bool`
- `AddCommandList( CommandList list, Stage stage, int order )`: injects render commands at a given pipeline stage. See `references/09_RENDERING.md`.

**The old render hooks are flat-out dead in 26.08.05.** `AddHookAfterOpaque( string debugName, int order, Action<SceneCamera> effect )`, `AddHookAfterTransparent`, `AddHookBeforeOverlay` and `AddHookAfterUI` all carry `[Obsolete]`, and their bodies just return `null`. The call still compiles, still hands back a null `IDisposable`, and renders precisely nothing. Reach for `AddCommandList` with a `Stage` instead.

### HudPainter

Immediate-mode drawing directly onto the camera's HUD. For something small like a health bar or a debug readout, it costs a lot less than standing up a whole UI panel.

```csharp
protected override void OnUpdate()
{
    var hud = Scene.Camera.Hud;
    hud.DrawRect( new Rect( 10, 10, 200, 30 ), Color.Black.WithAlpha( 0.5f ) );
    hud.DrawText( new TextRendering.Scope( "Hello!", Color.White, 24 ), new Vector2( 20, 15 ) );
    hud.DrawLine( new Vector2( 0, 0 ), new Vector2( 100, 100 ), 2, Color.Red );
}
```

`Scene.Camera.Hud` renders before post-processing kicks in. `Scene.Camera.Overlay` sits above everything, post-processing included.

***

## Lighting

Every light type inherits from the abstract `Light` class:

| Property | Type | What it holds |
|:---|:---|:---|
| `LightColor` | `Color` | Light color, with intensity baked in |
| `Shadows` | `bool` | Whether the light casts shadows |
| `ShadowBias` | `float` | Bias applied to shadow depth |
| `ShadowHardness` | `float` | Controls shadow edge softness |
| `FogMode` | `FogInfluence` | How this light interacts with fog |
| `FogStrength` | `float` | Strength of that fog influence |

### Available Light Types

| Component | Extra Properties |
|:--|:--|
| `PointLight` | `float Radius`, `float Attenuation` |
| `SpotLight` | `float Radius`, `float ConeOuter`, `float ConeInner`, `float Attenuation`, `Texture Cookie` |
| `DirectionalLight` | `Color SkyColor`, `int ShadowCascadeCount`, `float ShadowCascadeSplitRatio` |
| `AmbientLight` | `Color Color` (applied scene-wide) |

***

## Audio

### BaseSoundComponent (abstract)

The shared base for any component that plays a positioned sound out in the world.

| Property | Type | What it holds |
|:---|:---|:---|
| `SoundEvent` | `SoundEvent` | The sound asset to play |
| `PlayOnStart` | `bool` | Plays automatically once enabled |
| `Volume` | `float` | Scales overall loudness |
| `Pitch` | `float` | Scales playback pitch |
| `Force2d` | `bool` | Ignores spatial positioning entirely |
| `Repeat` | `bool` | Loops the sound |
| `MinRepeatTime` / `MaxRepeatTime` | `float` | Range for a randomized repeat interval |
| `Distance` | `float` | Maximum distance the sound can be heard from |
| `DistanceAttenuation` | `bool` | Enables falloff over distance |
| `Falloff` | `Curve` | Curve shaping that falloff |
| `Occlusion` | `bool` | Whether geometry can occlude the sound |
| `TargetMixer` | `MixerHandle` | Which audio mixer receives it |

Playback control: `StartSound()`, `StopSound()`

### Sound Component Variants

| Component | What It Plays |
|:--|:--|
| `SoundPointComponent` (sealed) | Plays a sound from a single point; the one you'll use most. |
| `SoundBoxComponent` (sealed) | Plays a sound throughout a box volume. Extra property: `Vector3 Scale`. |
| `AudioListener` (sealed) | Moves the listening point away from the camera. Property: `bool IsActive`. |

### Triggering Sounds From Code

```csharp
// On a GameObject (positional)
SoundHandle handle = GameObject.PlaySound( mySoundEvent );
GameObject.StopAllSounds( fadeOutTime: 0.5f );

// Global
Sound.Play( mySoundEvent );
Sound.Play( mySoundEvent, worldPosition );
```

***

## UI Panels

s&box builds its UI on Panels, an HTML/CSS-flavored layout system. These components form the bridge connecting a panel tree to the scene.

### ScreenPanel (sealed)

The root for screen-space (2D) UI. Attach it to any GameObject that has a `PanelComponent` child.

| Property | Type | What it holds |
|:---|:---|:---|
| `Scale` | `float` | Overall UI scale |
| `AutoScreenScale` | `bool` | Auto-scales toward a 1080p target (default: true) |
| `ScaleStrategy` | `AutoScale` | Which scaling mode is used |
| `Opacity` | `float` | How transparent the panel renders |
| `ZIndex` | `int` | Draw order |
| `TargetCamera` | `CameraComponent` | Which camera renders this panel |

### WorldPanel (sealed)

Draws panels out in 3D world space. Add it to a GameObject, then hang `PanelComponent` children off it.

| Property | Type | What it holds |
|:---|:---|:---|
| `PanelSize` | `Vector2` | Panel dimensions, measured in world units |
| `RenderScale` | `float` | Multiplier applied to render resolution |
| `LookAtCamera` | `bool` | Enables billboarding toward the camera |
| `HorizontalAlign` / `VerticalAlign` | alignment enum | Alignment relative to the panel's position |
| `InteractionRange` | `float` | Maximum distance for interaction |

### PanelComponent (abstract)

The base class for your own UI panels. Override it to build UI directly in C#, or write it in Razor instead.

```csharp
// C# approach
public sealed class HealthDisplay : PanelComponent
{
    protected override void OnTreeFirstBuilt()
    {
        var label = new Label();
        label.Parent = Panel;
    }
}

// Razor approach (in .razor file)
@inherits PanelComponent
<root>
    <div class="health">@Health</div>
</root>
@code {
    [Property] public float Health { get; set; }
}
```

Panel-class helpers: `AddClass`, `RemoveClass`, `HasClass`, `SetClass`, `BindClass`, `StateHasChanged`

### WorldInput (sealed)

Routes mouse and keyboard input to `WorldPanel` components. Attach it to a camera or a VR controller.

***

## Navigation

### NavMeshAgent (sealed)

Handles AI pathfinding across the scene's NavMesh. As soon as it has a target it takes ownership of its own GameObject's position and rotation, so if anything else is driving that transform too, turn off `UpdatePosition` and `UpdateRotation` first.

```csharp
[RequireComponent] NavMeshAgent Agent { get; set; }

protected override void OnUpdate()
{
    Agent.MoveTo( targetPosition );

    // Use velocity for animation
    var speed = Agent.Velocity.Length;
    body.Set( "move_speed", speed );
}
```

| Property | Type | What it holds |
|:---|:---|:---|
| `MaxSpeed` | `float` | Top movement speed |
| `Acceleration` | `float` | Rate of acceleration |
| `Height` | `float` | How tall the agent is |
| `Radius` | `float` | How wide the agent is |
| `UpdatePosition` | `bool` | Syncs the GameObject's position (disable for custom traversal) |
| `UpdateRotation` | `bool` | Syncs the GameObject's rotation |
| `Velocity` | `Vector3` | The agent's velocity right now |
| `WishVelocity` | `Vector3` | Velocity the agent wants |
| `IsNavigating` | `bool` | Whether it's currently moving toward a target |
| `TargetPosition` | `Vector3?` | The active target, if any |
| `Separation` | `float` | Distance kept from other agents |
| `AutoTraverseLinks` | `bool` | Traverses NavMeshLinks automatically (default: true) |
| `IsTraversingLink` | `bool` | Whether it's currently on a link |
| `LinkEnter` / `LinkExit` | `Action` | Fired on link traversal |
| `AllowedAreas` / `ForbiddenAreas` | `HashSet<NavMeshAreaDefinition>` | Filters which areas are usable |

Useful methods:
- `MoveTo( Vector3 targetPosition )`: begins pathfinding toward a target
- `Stop()`: halts navigation
- `SetAgentPosition( Vector3 position )`: manually updates position, for custom traversal
- `CompleteLinkTraversal()`: signals that link traversal has finished
- `GetLookAhead( float distance )` → `Vector3`: a point further along the current path
- `GetPath()` → `NavMeshPath` / `SetPath( NavMeshPath )`

### NavMeshLink

Bridges NavMesh polygons across gaps: ladders, jumps, teleports. Override it for custom traversal logic:

```csharp
public sealed class JumpLink : NavMeshLink
{
    protected virtual void OnLinkEntered( NavMeshAgent agent ) { }
    protected virtual void OnLinkExited( NavMeshAgent agent ) { }
}
```

Fires: `Action<NavMeshAgent> LinkEntered`, `Action<NavMeshAgent> LinkExited`

### Querying the Scene NavMesh

Static queries against the current scene's NavMesh, callable from anywhere in code:

```csharp
Scene.NavMesh.GetRandomPoint()                         // random navmesh point
Scene.NavMesh.GetRandomPoint( position, radius )       // random within radius
Scene.NavMesh.GetClosestPoint( position )              // snap to navmesh
Scene.NavMesh.GetClosestEdge( position )               // nearest edge
Scene.NavMesh.SetDirty()                               // rebuild in background
Scene.NavMesh.CalculatePath( new CalculatePathRequest {
    Start = from, Target = to, Agent = agent
})                                                     // calculate path
```

***

## Gameplay

### PlayerController (sealed)

A complete first- or third-person player controller with input, camera, physics and animation already wired together. Under the hood it's physics-driven, built on a Rigidbody rather than a CharacterController.

Every feature listed below can be switched off individually via right-click on its tab in the inspector.

| Feature | Relevant Properties |
|:--|:--|
| **Body** | `BodyRadius`, `BodyHeight`, `BodyMass`, `BodyCollisionTags` |
| **Input** | `UseInputControls`, `WalkSpeed`, `RunSpeed`, `DuckedSpeed`, `JumpSpeed` |
| **Camera** | `UseCameraControls`, `ThirdPerson`, `HideBodyInFirstPerson`, `CameraOffset`, `EyeDistanceFromTop` |
| **Look** | `UseLookControls`, `PitchClamp`, `LookSensitivity`, `RotateWithGround` |
| **Animator** | `UseAnimatorControls`, `Renderer` (SkinnedModelRenderer) |
| **Pressing** | `EnablePressing` (default on), `UseButton` (default `"use"`), `ReachLength` (default `130`) |

| Property | Type | What it holds |
|:---|:---|:---|
| `Velocity` | `Vector3` | Velocity for the current tick |
| `WishVelocity` | `Vector3` | Desired movement direction (set this to drive custom input) |
| `EyeAngles` | `Angles` | Direction the player is looking (set this to drive custom input) |
| `EyePosition` | `Vector3` | World-space eye position |
| `IsOnGround` | `bool` | Whether the player is grounded |
| `IsDucking` | `bool` | Whether the player is crouched |
| `IsClimbing` | `bool` | Whether the player is on a ladder |
| `IsSwimming` | `bool` | Whether the player is in water |
| `GroundObject` | `GameObject` | What the player is standing on |
| `Hovered` | `Component` | Whatever pressable thing the player is currently looking at |
| `Pressed` | `Component` | Whatever the player is currently holding USE on |
| `Tooltips` | `List<IPressable.Tooltip>` | Rebuilt every frame; draw your prompt UI from this list |

Useful methods:
- `Jump( Vector3 velocity )`: a physics-driven jump
- `CreateRagdoll( string name = "Ragdoll" )` → `GameObject`
- `TraceBody( Vector3 from, Vector3 to, float scale = 1f, float heightScale = 1f )` → `SceneTraceResult`
- `StartPressing( Component )` / `StopPressing()`: drives pressing manually

**Custom input:** turn off `UseInputControls`, then set `WishVelocity` and `EyeAngles` yourself inside `OnFixedUpdate`.

**Pressing, "press E to use":** implement `Component.IPressable` on whatever component should be pressable. The controller traces `ReachLength` outward from the eye with an expanding radius, then hunts for `GetComponentsInParent<IPressable>( includeSelf: true )` on whatever the trace connects with. All of it runs client-side, on the pressing player only (`OnUpdate`, wrapped in `if ( !IsProxy )`), so anything that needs to be authoritative, granting an item, opening a door for everyone else, needs its own `[Rpc.Host]` call to actually take effect. One trap worth flagging: setting `UseLookControls = false` silently disables pressing along with it. See `01_SCENE.md` → *IPressable* and `13_EXAMPLES.md` → *Example 11*.

**Events:** implement `PlayerController.IEvents` on a sibling component:
```csharp
public sealed class MyListener : Component, PlayerController.IEvents
{
    void PlayerController.IEvents.OnJumped() { }
    void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity ) { }
    void PlayerController.IEvents.OnEyeAngles( ref Angles angles ) { }
    void PlayerController.IEvents.PreInput() { }

    // Pressing hooks
    Component PlayerController.IEvents.GetUsableComponent( GameObject go ) => null;
    void PlayerController.IEvents.StartPressing( Component target ) { }
    void PlayerController.IEvents.StopPressing( Component target ) { }
    void PlayerController.IEvents.FailPressing() { }
}
```

`PostCameraSetup( CameraComponent )` carries **`[Obsolete]`** as of engine 26.08.05. Use `ICameraModifier` in its place: it runs inside the camera's ordered modifier chain, positioned after the player's own view, which itself sits at order 0.

### Prop

`Prop` (not sealed: `Component, ExecuteInEditor, IDamageable`) actually does **two** unrelated jobs at once, and most gamemodes only want one of them. It pays to know which half you're signing up for before dropping it on a GameObject.

**Job one: it self-assembles a renderer, collider and physics setup from a `Model`.** Assign `Model` and it constructs the rest for you (`Scene/Components/Game/Prop.cs:214-333`):

| It creates | Trigger condition |
|:--|:--|
| `SkinnedModelRenderer` | `Model.BoneCount > 0` |
| `ModelRenderer` | when that's not true |
| `ModelCollider` (`Static = true`) | when `IsStatic` is on |
| `ModelCollider` + `Rigidbody` | one physics part, inheriting mass, damping and gravity scale from the model |
| `ModelPhysics` | multiple physics parts (ragdoll-style) |

Internally these are tracked as "procedural components," and `Prop` tears the whole set down and rebuilds it from scratch whenever `Model` or `IsStatic` changes.

**Job two: it's breakable, flammable, explosive and gibbable**, regardless of whether that's what you wanted. `[Property, Sync] float Health` seeds itself from `Model.Data.Health`, and from there you inherit `IDamageable.OnDamage`, `Ignite()`, `CreateExplosion()`, `Kill( DamageInfo )`, and `CreateGibs()` / `NetworkCreateGibs()` driven by the model's break-piece list, along with `OnPropBreak` / `OnGibsCreated` / `OnPropTakeDamage` action properties for hooking into any of it.

| Member | Type | What to know |
|:--|:--|:--|
| `Model` | `Model` | Reassigning it rebuilds the procedural components |
| `Health` | `float` | `[Property, Sync]`. Seeded automatically from `Model.Data.Health` |
| `IsStatic` | `bool` | Gives it a static collider and skips the rigidbody |
| `StartAsleep` | `bool` | Physics stays asleep until something wakes it |
| `BodyGroups`, `MaterialGroup`, `Tint` | | Passed through to the renderer |
| `IsFlammable`, `IsExplosive`, `IsOnFire` | `bool` | Sourced from `Model.Data` |
| `LastAttacker` | `GameObject` | |
| `OnDamage( in DamageInfo )` | | The `IDamageable` entry point; not `TakeDamage` |
| `Kill( DamageInfo = null )` | | Breaks it immediately |
| `CreateGibs( bool wasImpact = false )` | `List<Gib>` | |
| `Break()` | | **Not what the name implies.** An editor `[Button]` that removes the `Prop` component itself while leaving the procedural pieces in place (`Prop.cs:641-672`) |

> **Building a non-destructible pickup, prop or piece of furniture? Skip `Prop`.**
> Adding it hands you a synced `Health`, an `IDamageable` implementation that any
> damage source in the game will happily find and use, and gib spawning, none of
> which you asked for. Compose it yourself instead: `ModelRenderer` + `ModelCollider`
> (or a primitive `Collider`) + `Rigidbody` if it needs to move. That path is also
> the only way to get a renderer/collider pairing that `Prop` would never have
> chosen on its own.

### Inventory (BaseInventoryComponent / BaseInventoryItem)

A full slot-based inventory shipped with the engine (`Scene/Components/Game/Inventory/`, ten files). Items live as child GameObjects, and the inventory just toggles them on and off as slots switch. Authority sits with the host throughout: a client requests a change, the host applies it, and the outcome replicates back down to everyone else.

Reach for it when your items are held objects, weapons, tools, consumables, with a deploy-and-holster lifecycle. Build your own instead when items are really just data rows (a trading inventory, a bank, a shop) with no GameObject per item, when one item needs to exist in multiple places simultaneously, or when neither built-in behaviour mode matches the slot semantics you need.

**`BaseInventoryComponent`**

| Member | What it does |
|:--|:--|
| `Behaviour` | `Hotbar` (one item per slot, Rust/Minecraft style) or `Buckets` (category buckets ordered by `SlotOrder`, HL2 style). Defaults to `Hotbar` |
| `MaxSlots` | Defaults to `6` |
| `Items` | `IEnumerable<BaseInventoryItem>`, ordered by slot and then `SlotOrder` |
| `ActiveItem` | `[Sync(FromHost), Change]`, private setter; call `Switch` instead |
| `ActiveItemChanged` | `event Action<old, new>`, every peer gets notified |
| `Add( item, slot = -1 )` / `Pickup( prefab \| path, slot = -1 )` | Host-side operation only |
| `Remove` / `Drop` | Goes through the host, wrapped as `[Rpc.Host]` calls `HostRemove` / `HostDrop` |
| `Transfer( item, toInventory, slot )` | **Host-only, and not routed**: `if ( !Networking.IsHost ) return false;` with no RPC wrapper (`BaseInventoryComponent.Actions.cs:189-190`), so a client-side call silently does nothing. Its own doc calls this out: "Games route their own requests here" |
| `Switch( item, allowHolster = false )` / `SwitchToBest()` / `ForceHolster()` | |
| `MoveSlot( from, to )` | Swaps items if the destination is occupied |
| `GetSlot` / `GetSlotItems` / `FindEmptySlot` / `GetItem<T>` / `HasItem<T>` | Read-only lookups |
| `virtual GetBestItem()` | Falls back to the highest `Value` item, skipping anything flagged `ShouldAvoid`. Runs on the host |
| `OnAdding( item, slot )` / `OnRemoving( item )` / `OnDropping( item )` / `OnMovingSlot( from, to )` | Overridable gates; return `false` to block the action. Plus `OnItemAdded( item )` |

**Pickup** (`PickupMode`): `None` leaves it to you to call `PickupWorldItem` directly, `Touch` has the host sweep a `PickupRadius` sphere (default `48`) every 0.25 s, and `Use` makes the item `IPressable` so a player walks up and presses E. All three paths funnel into `virtual PickupWorldItem`, which is itself host-routed through an `[Rpc.Host( NetFlags.OwnerOnly )]`. Override `CanPickupWorldItem` if you want range or line-of-sight checks: the base implementation deliberately skips a range check, since "close enough" is a decision for game policy, not engine policy. `AutoSwitchOnPickup` (on by default) and `AutoSwitchOnEmpty` decide whether a fresh pickup deploys automatically.

**Ammo** lives as a reserve pool on the *inventory* itself rather than on the item: a `[Sync(FromHost)] NetDictionary<string,int>` keyed by ammo type, exposed through `GetAmmo` / `HasAmmo` / `GiveAmmo` / `SetAmmo` / `TakeAmmo` against `BaseAmmoResource` (itself a `GameResource`). The reserve pool stays put across a `Transfer`. `BaseAmmoPickup : BaseInventoryItem` is a world pickup that tops up the pool rather than occupying a slot.

**Loadout**: `UsesLoadout` / `GiveOnStart` / `StartingItems` (prefabs) / `StartingAmmo`, all granted through `GiveLoadout()` on the host. It grants unconditionally on every call, so it's your job to make sure it only runs once per life.

**`BaseInventoryItem`** is `Component, Component.IPressable`. It carries editor metadata (`DisplayName`, `DisplayIcon`, `Value`, `PreferredSlot`, `SlotOrder`), a `[Sync(FromHost)] int Slot`, and an `Inventory` reference derived from the hierarchy, so it resolves correctly on every peer. Override the protected hooks you need: `OnCanPickup`, `OnCanSwitchTo`, `OnHolstering` (return `false` to refuse, though forced holsters ignore it), `OnEquipped` / `OnHolstered` (fire on every peer), `OnControl` (**owning client only, every single frame**, this is where input reading belongs), `OnAdding` / `OnAdded` / `OnRemoved` (host), `OnDrop`.

`BaseInventoryComponent.Pump()` is what drives `OnControl`; set `ManualPumping` if you want to control that timing yourself.

### SpawnPoint (sealed)

A marker for player spawn locations, used by `NetworkHelper` to decide where players land.

### NetworkHelper (sealed)

Stands up a networked lobby and hands out player prefabs to incoming connections.

| Property | Type | What it holds |
|:---|:---|:---|
| `PlayerPrefab` | `GameObject` | Prefab spawned for each connecting player |
| `StartServer` | `bool` | Starts hosting automatically |
| `SpawnPoints` | `bool` | Uses SpawnPoint components to decide placement |

***

## Effects

### ParticleEffect (sealed)

The core particle system: 66 configurable properties covering emission, simulation and rendering, though none of it produces anything visible until emitter and renderer child components are attached.

- **Emitter types**: `ParticleSphereEmitter`, `ParticleBoxEmitter`, `ParticleConeEmitter`, `ParticleRingEmitter`, `ParticleModelEmitter`
- **Renderer types**: `ParticleSpriteRenderer`, `ParticleModelRenderer`, `ParticleTrailRenderer`, `ParticleLightRenderer`, `ParticleTextRenderer`
- **Controllers**: `ParticleAttractor` and any custom `ParticleController` subclass

### LegacyParticleSystem

Plays back Source Engine `.vpcf` particle files.

### TemporaryEffect (sealed)

Destroys its own `GameObject` automatically once every child particle and sound has finished playing. Implement the `ITemporaryEffect` interface on your own components if you need them counted in that check too.

***

## Environment

| Component | What It Adds |
|:--|:--|
| `SkyBox2D` | A flat 2D skybox background |
| `GradientFog` | Fog that fades by distance. Properties: `Start/EndDistance`, `Color`, `Height`. |
| `CubemapFog` | A cubemap-driven fog effect |
| `VolumetricFogVolume` | Volumetric fog rendered in 3D |
| `EnvmapProbe` (sealed) | A cubemap reflection probe. Properties: `Resolution`, `Parallax`, bounds. |
| `IndirectLightVolume` (sealed) | A grid of dynamic GI probes |
| `Terrain` (sealed) | Terrain generated from a heightmap. Properties: `TerrainSize`, `HeightMapSize`, `ClipMapLodLevels`. |
| `MapInstance` | Loads a map (`.vpk` or `.scene`) into the current scene |

***

## Post-Processing

Attach these to a `GameObject` carrying a `CameraComponent`, or use `PostProcessVolume` instead if you want the effect confined to a region rather than applied across the whole camera.

| Component | Main Properties |
|:--|:--|
| `AmbientOcclusion` (sealed) | `Intensity`, `Radius`, `Quality` |
| `Bloom` | `Threshold`, `Strength`, `Radius` |
| `DepthOfField` (sealed) | `FocalDistance`, `FrontBlur`, `BackBlur`, `BlurSize` |
| `MotionBlur` (sealed) | `Scale` |
| `ColorAdjustments` (sealed) | `Brightness`, `Contrast`, `Saturation`, `HueRotate` |
| `ColorGrading` (sealed) | `Temperature`, `Tint`, `Shadows/Midtones/Highlights` |
| `Tonemapping` | `Mode`, `MinExposure`, `MaxExposure`, `ExposureSpeed` |
| `ChromaticAberration` (sealed) | `Offset`, `Scale` |
| `FilmGrain` (sealed) | `Intensity`, `Response` |
| `Vignette` (sealed) | `Intensity`, `Roundness`, `Smoothness`, `Color` |
| `Blur` (sealed) | `Amount` |
| `Pixelate` (sealed) | `Scale` |
| `Sharpen` (sealed) | `Strength`, `Size` |
| `ScreenSpaceReflections` | `MaxRayLength` |
| `HighlightOutline` | An object outline effect (requires `Highlight` on the camera) |

`PostProcessVolume` fades its effects in and out based on where the camera sits, rather than applying them uniformly everywhere.

***

## ModelPhysics (sealed)

Drives physics for ragdolls and other physics-controlled models by building a physics body for every bone in the skeleton.

```csharp
var physics = go.AddComponent<ModelPhysics>();
physics.Renderer = go.GetComponent<SkinnedModelRenderer>();
physics.Model = physics.Renderer.Model;
physics.MotionEnabled = true;  // start simulating
```

| Property | Type | What it holds |
|:---|:---|:---|
| `Renderer` | `SkinnedModelRenderer` | The target model |
| `Model` | `Model` | The physics model in use |
| `MotionEnabled` | `bool` | Turns physics simulation on or off |
| `Mass` | `float` | Total combined mass |
| `Locking` | `PhysicsLock` | Locks specific axes |
| `PhysicsGroup` | `PhysicsGroup` | The underlying physics group |

Method: `CopyBonesFrom( SkinnedModelRenderer source, bool teleport )` copies bone positions across from another renderer. This is the call you reach for at the moment a live skeleton hands control off to its ragdoll.

***

## The Citizen Animation Helper (sealed)

`Sandbox.Citizen.CitizenAnimationHelper` is a high-level animation controller purpose-built for the Citizen model.

```csharp
[RequireComponent] CitizenAnimationHelper AnimHelper { get; set; }

protected override void OnUpdate()
{
    AnimHelper.WithVelocity( Velocity );
    AnimHelper.WithWishVelocity( WishVelocity );
    AnimHelper.IsGrounded = controller.IsOnGround;
    AnimHelper.WithLook( eyeDirection );
    AnimHelper.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
}
```

Drives animgraph parameters covering movement, look direction, hold types, ducking, and more besides.

***

## Voice Chat

The `Voice` component captures microphone input and transmits it to other players over multiplayer.

| Property | Type | What it holds |
|:---|:---|:---|
| `Mode` | `VoiceMode` | Push-to-talk, always-on, and similar modes |
| `PushToTalkInput` | `string` | Input action name bound to push-to-talk |
| `IsRecording` | `bool` | Whether it's currently recording (read-only) |
| `Volume` | `float` | How loud playback comes through |
