# Built-in Components

API reference for the most commonly used built-in components. All are in the `Sandbox` namespace unless noted.

---

## Rendering

### ModelRenderer

Renders a static model at the GameObject's position.

```csharp
var renderer = go.AddComponent<ModelRenderer>();
renderer.Model = Model.Load( "models/dev/box.vmdl" );
renderer.Tint = Color.Red;
renderer.MaterialOverride = Material.Load( "materials/custom.vmat" );
```

| Property | Type | Description |
|----------|------|-------------|
| `Model` | `Model` | Model to render |
| `Tint` | `Color` | Color tint |
| `MaterialOverride` | `Material` | Override all materials |
| `Materials` | `MaterialAccessor` | Per-material access |
| `BodyGroups` | `ulong` | Active body group mask |
| `MaterialGroup` | `string` | Active material group name |
| `RenderType` | `ShadowRenderType` | Shadow casting mode |
| `LodOverride` | `int?` | Force a specific LOD level |
| `CreateAttachments` | `bool` | Create child GameObjects for model attachments |
| `Bounds` | `BBox` | World-space bounding box (read-only) |
| `SceneObject` | `SceneObject` | Underlying scene object |

Key methods:
- `SetBodyGroup( string name, int value )` / `SetBodyGroup( string name, string choice )`
- `GetBodyGroup( string name )` → `int`
- `SetMaterial( Material material, int triangle = -1 )` — per-triangle material override
- `GetMaterial( int triangle = -1 )` → `Material`
- `ClearMaterialOverrides()`
- `SetMaterialOverride( Material material, string target )` — override by attribute name
- `GetAttachmentObject( string name )` → `GameObject` — child object at named attachment point
- `GetBoneObject( BoneCollection.Bone bone )` → `GameObject`

Inherits from `Renderer` base:
- `RenderOptions RenderOptions` — render flags
- `Attributes` → `RenderAttributes` — shader attributes
- `ExecuteBefore` / `ExecuteAfter` → `CommandList` — custom render commands

### SkinnedModelRenderer (sealed)

Extends `ModelRenderer` with skeletal animation, bones, morphs, and IK.

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

| Property | Type | Description |
|----------|------|-------------|
| `CreateBoneObjects` | `bool` | Create GameObjects for each bone |
| `BoneMergeTarget` | `SkinnedModelRenderer` | Merge bones onto another model (clothing/attachments) |
| `UseAnimGraph` | `bool` | Enable animgraph (usually off for ragdolls) |
| `AnimationGraph` | `AnimationGraph` | Override the animgraph |
| `PlaybackRate` | `float` | Animation speed multiplier |
| `RootMotion` | `Transform` | Current root motion delta |
| `Parameters` | `ParameterAccessor` | Animgraph parameter access |
| `Morphs` | `MorphAccessor` | Morph target access |
| `Sequence` | `SequenceAccessor` | Direct sequence playback |
| `OnFootstepEvent` | `Action<FootstepEvent>` | Footstep callback |
| `OnGenericEvent` | `Action<GenericEvent>` | Generic anim event callback |
| `OnSoundEvent` | `Action<SoundEvent>` | Sound event callback |

Key methods:
- `Set( string name, float/int/bool/Vector3/Rotation value )` — set animgraph parameter
- `GetFloat/GetInt/GetBool/GetVector/GetRotation( string name )` — read animgraph parameter
- `ClearParameters()` — reset all parameters
- `SetIk( string name, Transform tx )` — set IK target (enables ik.{name}.enabled, position, rotation)
- `ClearIk( string name )` — disable IK target
- `SetLookDirection( string name, Vector3 direction, float weight = 1f )`
- `TryGetBoneTransform( string boneName, out Transform tx )` → `bool` — world-space bone transform
- `TryGetBoneTransformLocal( string boneName, out Transform tx )` → `bool`
- `GetBoneObject( string boneName )` → `GameObject`
- `GetBoneObject( int index )` → `GameObject`
- `GetAttachment( string name, bool worldSpace = true )` → `Transform?`
- `SetBoneTransform( BoneCollection.Bone bone, Transform transform )`
- `PostAnimationUpdate()` — force immediate animation update

### Other Renderers

| Component | Description |
|-----------|-------------|
| `DecalRenderer` | Projects a material onto surfaces. Properties: inherits Renderer base. |
| `Decal` (sealed) | PBR decal projection with full material controls. Properties: `Material`, `Size`, `Color`, lifetime, animated effects. |
| `LineRenderer` (sealed) | Renders a line through a list of points. 22 properties for width, color, curves. |
| `SpriteRenderer` (sealed) | Renders a 2D sprite. Properties: `Texture`, `Color`, `Size`, `FlipHorizontal/Vertical`. |
| `TextRenderer` (sealed) | Renders 3D text. Properties: `Text`, `Color`, `FontSize`, `FontFamily`. |
| `TrailRenderer` (sealed) | Trail behind moving objects. Properties: `Color`, `Width`, `Lifetime`, `Face`. |
| `BeamEffect` (sealed) | Laser/beam visual. Properties: `Targets`, `Width`, `Color`, `Speed`. |

---

## Physics

### Rigidbody (sealed)

Adds physics simulation. Requires at least one `Collider` on the same or child GameObject.

```csharp
var rb = go.AddComponent<Rigidbody>();
rb.Gravity = true;
rb.Mass = 10f;

// Forces (continuous — apply every frame)
rb.ApplyForce( Vector3.Up * 500f );
rb.ApplyForceAt( hitPosition, explosionForce );
rb.ApplyTorque( Vector3.Up * 100f );

// Impulses (instantaneous — apply once)
rb.ApplyImpulse( Vector3.Forward * 1000f );
rb.ApplyImpulseAt( hitPosition, bulletForce );

// Smooth kinematic movement
rb.SmoothMove( targetTransform, 0.1f, Time.Delta );
```

| Property | Type | Description |
|----------|------|-------------|
| `Gravity` | `bool` | Affected by gravity |
| `GravityScale` | `float` | Gravity multiplier |
| `LinearDamping` | `float` | Velocity damping |
| `AngularDamping` | `float` | Angular velocity damping |
| `Mass` | `float` | Computed mass (read-only if no override) |
| `MassOverride` | `float` | Override computed mass |
| `Velocity` | `Vector3` | Linear velocity |
| `AngularVelocity` | `Vector3` | Angular velocity |
| `Locking` | `PhysicsLock` | Lock position/rotation axes |
| `MotionEnabled` | `bool` | Can move |
| `Sleeping` | `bool` | Physics sleeping state |
| `StartAsleep` | `bool` | Start in sleep state |
| `RigidbodyFlags` | `RigidbodyFlags` | Additional flags |
| `CollisionEventsEnabled` | `bool` | Fire collision events |
| `Touching` | `IEnumerable<Collider>` | Currently touching colliders |
| `Joints` | `IReadOnlySet<Joint>` | Attached joints |
| `PhysicsBody` | `PhysicsBody` | Underlying physics body |

Key methods:
- `ApplyForce( Vector3 )` / `ApplyForceAt( Vector3 position, Vector3 force )`
- `ApplyTorque( Vector3 )`
- `ApplyImpulse( Vector3 )` / `ApplyImpulseAt( Vector3 position, Vector3 force )`
- `ClearForces()`
- `SmoothMove( Transform target, float timeToArrive, float timeDelta )` — kinematic interpolation
- `SmoothMove( Vector3 position, float timeToArrive, float timeDelta )`
- `SmoothRotate( Rotation rotation, float timeToArrive, float timeDelta )`
- `GetVelocityAtPoint( Vector3 position )` → `Vector3`
- `FindClosestPoint( Vector3 position )` → `Vector3`
- `ResetInertiaTensor()`

### Colliders

Base class `Collider` (abstract). All colliders share:

| Property | Type | Description |
|----------|------|-------------|
| `IsTrigger` | `bool` | Trigger volume (no physics response) |
| `Static` | `bool` | Non-moving collider |
| `Friction` | `float?` | Surface friction override |
| `Elasticity` | `float?` | Bounciness override |
| `Surface` | `Surface` | Surface material (footsteps, impacts) |
| `SurfaceVelocity` | `Vector3` | Conveyor belt velocity |
| `Touching` | `IEnumerable<Collider>` | Currently overlapping colliders |
| `Rigidbody` | `Rigidbody` | Attached rigidbody (if any) |
| `Joints` | `IReadOnlySet<Joint>` | Attached joints |

Collider types:

| Component | Own Properties |
|-----------|---------------|
| `BoxCollider` (sealed) | `Vector3 Scale`, `Vector3 Center` |
| `SphereCollider` (sealed) | `Vector3 Center`, `float Radius` |
| `CapsuleCollider` | `Vector3 Start`, `Vector3 End`, `float Radius` |
| `HullCollider` (sealed) | `PrimitiveType Type`, `Vector3 Center`, `Vector3 BoxSize`, `float Height`, `float Radius` |
| `ModelCollider` | `Model Model` — collision from model physics |
| `PlaneCollider` (sealed) | `Vector2 Scale`, `Vector3 Center`, `Vector3 Normal` |
| `MeshComponent` (sealed) | Editable polygon mesh with built-in collision |

### CharacterController

Collision-constrained movement without a Rigidbody. Traces against the world to slide along surfaces.

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

| Property | Type | Description |
|----------|------|-------------|
| `Radius` | `float` | Capsule radius |
| `Height` | `float` | Capsule height |
| `StepHeight` | `float` | Max step-up height |
| `GroundAngle` | `float` | Max walkable slope angle |
| `Acceleration` | `float` | Internal acceleration factor |
| `Bounciness` | `float` | 0 = stop dead on walls, 1 = full bounce |
| `Velocity` | `Vector3` | Current velocity |
| `IsOnGround` | `bool` | Standing on ground |
| `GroundObject` | `GameObject` | What we're standing on |
| `GroundCollider` | `Collider` | Ground collider |
| `IgnoreLayers` | `TagSet` | Tags to ignore during traces |
| `UseCollisionRules` | `bool` | Use project collision rules |
| `BoundingBox` | `BBox` | Current collision bounds (read-only) |

Key methods:
- `Move()` — move using current `Velocity`, trace-and-slide
- `MoveTo( Vector3 targetPosition, bool useStep )` — direct trace-and-slide to position
- `Accelerate( Vector3 wishVelocity )` — add acceleration (auto time-scaled)
- `ApplyFriction( float amount, float stopSpeed = 140f )`
- `Punch( Vector3 amount )` — disconnect from ground and add velocity (for jumping)
- `TraceDirection( Vector3 direction )` → `SceneTraceResult`

### Joints

Base class `Joint` (abstract). All joints connect two physics bodies.

| Joint Type | Description |
|------------|-------------|
| `FixedJoint` (sealed) | Welds two objects rigidly |
| `HingeJoint` (sealed) | Single-axis rotation (doors, wheels). Properties: `MinAngle`, `MaxAngle`, `Friction`. |
| `BallJoint` (sealed) | Free rotation (shoulder joints). Properties: `SwingLimit`, `TwistLimit`. |
| `SliderJoint` (sealed) | Single-axis translation (drawers). |
| `SpringJoint` (sealed) | Spring connection with configurable spring/damping. |
| `WheelJoint` (sealed) | Vehicle wheel simulation. |

---

## Camera

### CameraComponent (sealed)

Every scene needs at least one. Renders the scene from the GameObject's transform.

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

| Property | Type | Description |
|----------|------|-------------|
| `FieldOfView` | `float` | FOV in degrees |
| `FovAxis` | `Axis` | Which axis FOV applies to |
| `ZNear` | `float` | Near clip plane |
| `ZFar` | `float` | Far clip plane |
| `Priority` | `int` | Camera priority (highest renders) |
| `IsMainCamera` | `bool` | Is this the main camera (read-only) |
| `Orthographic` | `bool` | Orthographic projection |
| `OrthographicHeight` | `float` | Ortho view height |
| `ClearFlags` | `ClearFlags` | How to clear before rendering |
| `BackgroundColor` | `Color` | Background clear color |
| `RenderTags` | `TagSet` | Only render objects with these tags |
| `RenderExcludeTags` | `TagSet` | Exclude objects with these tags |
| `Viewport` | `Vector4` | Viewport rect (normalized) |
| `RenderTarget` | `Texture` | Render to texture instead of screen |
| `Hud` | `HudPainter` | Draw before post-processing |
| `Overlay` | `HudPainter` | Draw on top of everything |
| `EnablePostProcessing` | `bool` | Enable post-process effects |

Key methods:
- `ScreenPixelToRay( Vector2 pixelPosition )` → `Ray` — mouse position to world ray
- `ScreenNormalToRay( Vector3 normalPosition )` → `Ray` — normalized screen coords to ray
- `PointToScreenPixels( Vector3 worldPosition )` → `Vector2` — world to screen pixels
- `PointToScreenNormal( Vector3 worldPosition )` → `Vector2` — world to normalized screen
- `ScreenToWorld( Vector2 screen )` → `Vector3` — screen point on near plane
- `GetFrustum()` → `Frustum`
- `RenderToTexture( Texture target, ViewSetup config = null )` → `bool`
- `AddHookAfterOpaque( string debugName, int order, Action<SceneCamera> effect )` → `IDisposable`
- `AddHookAfterTransparent(...)` / `AddHookBeforeOverlay(...)` / `AddHookAfterUI(...)`

### HudPainter

Immediate-mode drawing on camera HUD. More efficient than panels for simple UI.

```csharp
protected override void OnUpdate()
{
    var hud = Scene.Camera.Hud;
    hud.DrawRect( new Rect( 10, 10, 200, 30 ), Color.Black.WithAlpha( 0.5f ) );
    hud.DrawText( new TextRendering.Scope( "Hello!", Color.White, 24 ), new Vector2( 20, 15 ) );
    hud.DrawLine( new Vector2( 0, 0 ), new Vector2( 100, 100 ), 2, Color.Red );
}
```

`Scene.Camera.Hud` draws before post-processing. `Scene.Camera.Overlay` draws on top of everything.

---

## Lighting

All lights inherit from `Light` (abstract):

| Property | Type | Description |
|----------|------|-------------|
| `LightColor` | `Color` | Light color and intensity |
| `Shadows` | `bool` | Cast shadows |
| `ShadowBias` | `float` | Shadow bias |
| `ShadowHardness` | `float` | Shadow edge hardness |
| `FogMode` | `FogInfluence` | How light affects fog |
| `FogStrength` | `float` | Fog influence strength |

### Light Types

| Component | Additional Properties |
|-----------|----------------------|
| `PointLight` | `float Radius`, `float Attenuation` |
| `SpotLight` | `float Radius`, `float ConeOuter`, `float ConeInner`, `float Attenuation`, `Texture Cookie` |
| `DirectionalLight` | `Color SkyColor`, `int ShadowCascadeCount`, `float ShadowCascadeSplitRatio` |
| `AmbientLight` | `Color Color` — applied globally |

---

## Audio

### BaseSoundComponent (abstract)

Base for spatial sound sources.

| Property | Type | Description |
|----------|------|-------------|
| `SoundEvent` | `SoundEvent` | Sound to play (asset reference) |
| `PlayOnStart` | `bool` | Auto-play when enabled |
| `Volume` | `float` | Volume multiplier |
| `Pitch` | `float` | Pitch multiplier |
| `Force2d` | `bool` | Ignore spatial positioning |
| `Repeat` | `bool` | Loop playback |
| `MinRepeatTime` / `MaxRepeatTime` | `float` | Random repeat interval |
| `Distance` | `float` | Max audible distance |
| `DistanceAttenuation` | `bool` | Enable distance falloff |
| `Falloff` | `Curve` | Distance falloff curve |
| `Occlusion` | `bool` | Sound occlusion |
| `TargetMixer` | `MixerHandle` | Audio mixer target |

Methods: `StartSound()`, `StopSound()`

### Concrete Sound Components

| Component | Description |
|-----------|-------------|
| `SoundPointComponent` (sealed) | Plays sound at a point in the world. Most common. |
| `SoundBoxComponent` (sealed) | Plays sound within a box volume. Extra: `Vector3 Scale`. |
| `AudioListener` (sealed) | Client hears from this point instead of camera. Property: `bool IsActive`. |

### Playing Sounds From Code

```csharp
// On a GameObject (positional)
SoundHandle handle = GameObject.PlaySound( mySoundEvent );
GameObject.StopAllSounds( fadeOutTime: 0.5f );

// Global
Sound.Play( mySoundEvent );
Sound.Play( mySoundEvent, worldPosition );
```

---

## UI Components

UI in s&box uses Panels (HTML/CSS layout). Components connect panels to the scene.

### ScreenPanel (sealed)

Root for screen-space (2D) UI. Add to any GameObject with a `PanelComponent` child.

| Property | Type | Description |
|----------|------|-------------|
| `Scale` | `float` | UI scale |
| `AutoScreenScale` | `bool` | Auto-scale to 1080p target (default: true) |
| `ScaleStrategy` | `AutoScale` | Scaling mode |
| `Opacity` | `float` | Panel opacity |
| `ZIndex` | `int` | Render order |
| `TargetCamera` | `CameraComponent` | Which camera to draw on |

### WorldPanel (sealed)

Renders panels in 3D world space. Add to a GameObject, then add `PanelComponent` children.

| Property | Type | Description |
|----------|------|-------------|
| `PanelSize` | `Vector2` | Panel dimensions in world units |
| `RenderScale` | `float` | Resolution multiplier |
| `LookAtCamera` | `bool` | Billboard mode |
| `HorizontalAlign` / `VerticalAlign` | alignment enum | Alignment relative to position |
| `InteractionRange` | `float` | Max interaction distance |

### PanelComponent (abstract)

Base class for your UI panels. Override to build UI in C# or use Razor.

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

Methods: `AddClass`, `RemoveClass`, `HasClass`, `SetClass`, `BindClass`, `StateHasChanged`

### WorldInput (sealed)

Routes mouse/keyboard input to `WorldPanel` components. Attach to camera or VR controller.

---

## Navigation

### NavMeshAgent (sealed)

AI pathfinding on the NavMesh. Takes over position/rotation of its GameObject.

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

| Property | Type | Description |
|----------|------|-------------|
| `MaxSpeed` | `float` | Maximum movement speed |
| `Acceleration` | `float` | Acceleration rate |
| `Height` | `float` | Agent height |
| `Radius` | `float` | Agent radius |
| `UpdatePosition` | `bool` | Sync GameObject position (disable for custom traversal) |
| `UpdateRotation` | `bool` | Sync GameObject rotation |
| `Velocity` | `Vector3` | Current velocity |
| `WishVelocity` | `Vector3` | Desired velocity |
| `IsNavigating` | `bool` | Currently moving to target |
| `TargetPosition` | `Vector3?` | Current target |
| `Separation` | `float` | Crowd separation distance |
| `AutoTraverseLinks` | `bool` | Auto-traverse NavMeshLinks (default: true) |
| `IsTraversingLink` | `bool` | Currently on a link |
| `LinkEnter` / `LinkExit` | `Action` | Link traversal events |
| `AllowedAreas` / `ForbiddenAreas` | `HashSet<NavMeshAreaDefinition>` | Area filtering |

Key methods:
- `MoveTo( Vector3 targetPosition )` — start pathfinding to target
- `Stop()` — stop navigating
- `SetAgentPosition( Vector3 position )` — manual position update (for custom traversal)
- `CompleteLinkTraversal()` — signal link traversal complete
- `GetLookAhead( float distance )` → `Vector3` — point ahead on current path
- `GetPath()` → `NavMeshPath` / `SetPath( NavMeshPath )`

### NavMeshLink

Connects NavMesh polygons across gaps (ladders, jumps, teleports). Override for custom traversal:

```csharp
public sealed class JumpLink : NavMeshLink
{
    protected virtual void OnLinkEntered( NavMeshAgent agent ) { }
    protected virtual void OnLinkExited( NavMeshAgent agent ) { }
}
```

Events: `Action<NavMeshAgent> LinkEntered`, `Action<NavMeshAgent> LinkExited`

### Scene NavMesh API

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

---

## Gameplay

### PlayerController (sealed)

Full first/third person player controller with input, camera, physics, and animation built in. Physics-based (uses Rigidbody internally).

Features are optional — disable via right-click on feature tabs in inspector.

| Feature | Key Properties |
|---------|---------------|
| **Body** | `BodyRadius`, `BodyHeight`, `BodyMass`, `BodyCollisionTags` |
| **Input** | `UseInputControls`, `WalkSpeed`, `RunSpeed`, `DuckedSpeed`, `JumpSpeed` |
| **Camera** | `UseCameraControls`, `ThirdPerson`, `HideBodyInFirstPerson`, `CameraOffset`, `EyeDistanceFromTop` |
| **Look** | `UseLookControls`, `PitchClamp`, `LookSensitivity`, `RotateWithGround` |
| **Animator** | `UseAnimatorControls`, `Renderer` (SkinnedModelRenderer) |
| **Pressing** | `EnablePressing` (default on), `UseButton` (default `"use"`), `ReachLength` (default `130`) |

| Property | Type | Description |
|----------|------|-------------|
| `Velocity` | `Vector3` | Current velocity |
| `WishVelocity` | `Vector3` | Desired movement (set this for custom input) |
| `EyeAngles` | `Angles` | Where player is looking (set this for custom input) |
| `EyePosition` | `Vector3` | Eye position in world |
| `IsOnGround` | `bool` | Grounded |
| `IsDucking` | `bool` | Crouching |
| `IsClimbing` | `bool` | On ladder |
| `IsSwimming` | `bool` | In water |
| `GroundObject` | `GameObject` | What we're standing on |
| `Hovered` | `Component` | What we're looking at, if it's pressable |
| `Pressed` | `Component` | What we're holding USE on |
| `Tooltips` | `List<IPressable.Tooltip>` | Rebuilt every frame — draw your prompt UI from this |

Key methods:
- `Jump( Vector3 velocity )` — physics-based jump
- `CreateRagdoll( string name = "Ragdoll" )` → `GameObject`
- `TraceBody( Vector3 from, Vector3 to, float scale = 1f, float heightScale = 1f )` → `SceneTraceResult`
- `StartPressing( Component )` / `StopPressing()` — drive pressing yourself

**Custom input:** Disable `UseInputControls`, then set `WishVelocity` and `EyeAngles` in `OnFixedUpdate`.

**Pressing / "press E to use":** implement `Component.IPressable` on the target component.
The controller finds it by tracing `ReachLength` from the eye with an expanding radius and
searching `GetComponentsInParent<IPressable>( includeSelf: true )`. **It all runs on the
pressing client** (`OnUpdate`, inside `if ( !IsProxy )`), so authoritative effects need an
`[Rpc.Host]`. Note `UseLookControls = false` silently disables pressing too. See
`core-concepts.md` → *IPressable* and `patterns-and-examples.md` → *Example 11*.

**Events:** Implement `PlayerController.IEvents` on a sibling component:
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

`PostCameraSetup( CameraComponent )` is **`[Obsolete]`** as of engine 26.08.05 — implement
`ICameraModifier` instead, which runs in the camera's ordered modifier chain after the
player's own view (order 0).

### Prop

`Prop` (not sealed — `Component, ExecuteInEditor, IDamageable`) does **two** things, and
most gamemodes only want one of them. Know which half you're buying.

**Half one — it self-assembles renderer + collider + physics from a `Model`.** Set
`Model` and it creates the components for you (`Scene/Components/Game/Prop.cs:214-333`):

| It creates | When |
|---|---|
| `SkinnedModelRenderer` | `Model.BoneCount > 0` |
| `ModelRenderer` | otherwise |
| `ModelCollider` (`Static = true`) | `IsStatic` is set |
| `ModelCollider` + `Rigidbody` | one physics part — inherits mass, damping, gravity scale from the model |
| `ModelPhysics` | multiple physics parts (ragdoll-style) |

These are tracked as "procedural components" and are torn down and rebuilt whenever
`Model` or `IsStatic` changes.

**Half two — it is breakable, flammable, explosive and gibbable.** `[Property, Sync] float
Health` seeded from `Model.Data.Health`, `IDamageable.OnDamage`, `Ignite()`,
`CreateExplosion()`, `Kill( DamageInfo )`, `CreateGibs()` / `NetworkCreateGibs()` driven by
the model's break-piece list, plus `OnPropBreak` / `OnGibsCreated` / `OnPropTakeDamage`
action properties.

| Member | Type | Notes |
|---|---|---|
| `Model` | `Model` | Setting it rebuilds the procedural components |
| `Health` | `float` | `[Property, Sync]`. Auto-seeded from `Model.Data.Health` |
| `IsStatic` | `bool` | Static collider, no rigidbody |
| `StartAsleep` | `bool` | Physics asleep until woken |
| `BodyGroups`, `MaterialGroup`, `Tint` | | Forwarded to the renderer |
| `IsFlammable`, `IsExplosive`, `IsOnFire` | `bool` | From `Model.Data` |
| `LastAttacker` | `GameObject` | |
| `OnDamage( in DamageInfo )` | | The `IDamageable` entry point — not `TakeDamage` |
| `Kill( DamageInfo = null )` | | Break it now |
| `CreateGibs( bool wasImpact = false )` | `List<Gib>` | |
| `Break()` | | **Not what it sounds like.** An editor `[Button]` that deletes the `Prop` component and leaves the procedural components behind (`Prop.cs:641-672`) |

> **If you want a non-destructible pickup, prop or piece of furniture, do not use `Prop`.**
> You inherit a synced `Health`, an `IDamageable` implementation that any damage source
> will find, and gib spawning. Compose it yourself instead — `ModelRenderer` +
> `ModelCollider` (or a primitive `Collider`) + `Rigidbody` if it should move — which is
> also the only way to get a renderer/collider combination `Prop` wouldn't have chosen.

### Inventory (BaseInventoryComponent / BaseInventoryItem)

A complete slot-based inventory shipped in the engine
(`Scene/Components/Game/Inventory/`, ten files). Items are child GameObjects; the
inventory enables/disables them as you switch. **Host authoritative** — clients request,
the host applies, the result replicates back.

Reach for it when your items are *held things* — weapons, tools, consumables — with a
deploy/holster lifecycle. Build your own when items are pure data rows (a trading
inventory, a bank, a shop) with no GameObject per item, when a single item must exist in
several places, or when you need slot semantics neither behaviour mode covers.

**`BaseInventoryComponent`**

| Member | Notes |
|---|---|
| `Behaviour` | `Hotbar` (one item per slot, Rust/Minecraft) or `Buckets` (category buckets sorted by `SlotOrder`, HL2). Default `Hotbar` |
| `MaxSlots` | Default `6` |
| `Items` | `IEnumerable<BaseInventoryItem>`, ordered by slot then `SlotOrder` |
| `ActiveItem` | `[Sync(FromHost), Change]`, private setter. Use `Switch` |
| `ActiveItemChanged` | `event Action<old, new>`, fires on every peer |
| `Add( item, slot = -1 )` / `Pickup( prefab \| path, slot = -1 )` | Host only |
| `Remove` / `Drop` | Routed through the host (`[Rpc.Host]`-wrapped `HostRemove` / `HostDrop`) |
| `Transfer( item, toInventory, slot )` | **Host-only, not routed** — `if ( !Networking.IsHost ) return false;` with no RPC wrapper (`BaseInventoryComponent.Actions.cs:189-190`); a client call silently no-ops. "Games route their own requests here" per its own doc |
| `Switch( item, allowHolster = false )` / `SwitchToBest()` / `ForceHolster()` | |
| `MoveSlot( from, to )` | Swaps if occupied |
| `GetSlot` / `GetSlotItems` / `FindEmptySlot` / `GetItem<T>` / `HasItem<T>` | Queries |
| `virtual GetBestItem()` | Fallback priority — highest `Value`, avoiding `ShouldAvoid` items. Runs on the host |
| `OnAdding( item, slot )` / `OnRemoving( item )` / `OnDropping( item )` / `OnMovingSlot( from, to )` | Overridable gates; return `false` to refuse. Plus `OnItemAdded( item )` |

**Pickup** (`PickupMode`): `None` (you call `PickupWorldItem` yourself), `Touch` (host
sweeps a `PickupRadius` sphere, default `48`, every 0.25 s), or `Use` (the item is
`IPressable` — walk up and press E). All three funnel into `virtual PickupWorldItem`,
which is host-routed via an `[Rpc.Host( NetFlags.OwnerOnly )]`. Override
`CanPickupWorldItem` to add range or line-of-sight validation — **the base deliberately has
no range check**, on the grounds that plausibility is game policy. `AutoSwitchOnPickup`
(default on) and `AutoSwitchOnEmpty` control deploy-on-take.

**Ammo** is a reserve pool on the *inventory*, not the item: a
`[Sync(FromHost)] NetDictionary<string,int>` keyed by ammo type, with `GetAmmo` /
`HasAmmo` / `GiveAmmo` / `SetAmmo` / `TakeAmmo` against `BaseAmmoResource` (itself a
`GameResource`). Reserve stays behind on `Transfer`. `BaseAmmoPickup : BaseInventoryItem`
is a world pickup that donates to the pool instead of taking a slot.

**Loadout**: `UsesLoadout` / `GiveOnStart` / `StartingItems` (prefabs) / `StartingAmmo`,
granted by `GiveLoadout()` on the host — unconditionally, so it is on you to call it once
per life.

**`BaseInventoryItem`** is `Component, Component.IPressable`. Editor metadata
(`DisplayName`, `DisplayIcon`, `Value`, `PreferredSlot`, `SlotOrder`), a
`[Sync(FromHost)] int Slot`, and `Inventory` derived from the hierarchy so it is correct
on every peer. Override the protected hooks: `OnCanPickup`, `OnCanSwitchTo`,
`OnHolstering` (return `false` to refuse — not consulted on forced holsters),
`OnEquipped` / `OnHolstered` (every peer), `OnControl` (**owning client only, every
frame** — read input here), `OnAdding` / `OnAdded` / `OnRemoved` (host), `OnDrop`.

`BaseInventoryComponent.Pump()` drives `OnControl`; set `ManualPumping` to take over the
timing yourself.

### SpawnPoint (sealed)

Marker for player spawn locations. Used by `NetworkHelper` to place players.

### NetworkHelper (sealed)

Sets up a networked lobby and assigns player prefabs to connections.

| Property | Type | Description |
|----------|------|-------------|
| `PlayerPrefab` | `GameObject` | Prefab to spawn for each player |
| `StartServer` | `bool` | Auto-start hosting |
| `SpawnPoints` | `bool` | Use SpawnPoint components for placement |

---

## Effects

### ParticleEffect (sealed)

Core particle system. 66 configurable properties covering emission, simulation, and rendering.

Add emitter and renderer child components to configure the full effect:
- **Emitters**: `ParticleSphereEmitter`, `ParticleBoxEmitter`, `ParticleConeEmitter`, `ParticleRingEmitter`, `ParticleModelEmitter`
- **Renderers**: `ParticleSpriteRenderer`, `ParticleModelRenderer`, `ParticleTrailRenderer`, `ParticleLightRenderer`, `ParticleTextRenderer`
- **Controllers**: `ParticleAttractor` and custom `ParticleController` subclasses

### LegacyParticleSystem

Plays Source Engine `.vpcf` particle files.

### TemporaryEffect (sealed)

Auto-destroys its `GameObject` after all child particles and sounds finish. Uses `ITemporaryEffect` interface — implement on custom components for compatibility.

---

## Environment

| Component | Description |
|-----------|-------------|
| `SkyBox2D` | 2D skybox background |
| `GradientFog` | Distance-based gradient fog. Properties: `Start/EndDistance`, `Color`, `Height`. |
| `CubemapFog` | Cubemap-based fog effect |
| `VolumetricFogVolume` | 3D volumetric fog volume |
| `EnvmapProbe` (sealed) | Cubemap reflection probe. Properties: `Resolution`, `Parallax`, bounds. |
| `IndirectLightVolume` (sealed) | Dynamic GI probe grid |
| `Terrain` (sealed) | Heightmap-based terrain. Properties: `TerrainSize`, `HeightMapSize`, `ClipMapLodLevels`. |
| `MapInstance` | Loads a map (`.vpk` or `.scene`) into the scene |

---

## Post-Processing

Add these to a `GameObject` with a `CameraComponent` (or use `PostProcessVolume` for region-based).

| Component | Key Properties |
|-----------|---------------|
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
| `HighlightOutline` | Object outline effect (needs `Highlight` on camera) |

`PostProcessVolume` — region-based, blends effects based on camera position.

---

## ModelPhysics (sealed)

Physics for ragdolls and physics-driven models. Creates physics bodies for each bone.

```csharp
var physics = go.AddComponent<ModelPhysics>();
physics.Renderer = go.GetComponent<SkinnedModelRenderer>();
physics.Model = physics.Renderer.Model;
physics.MotionEnabled = true;  // start simulating
```

| Property | Type | Description |
|----------|------|-------------|
| `Renderer` | `SkinnedModelRenderer` | Target model |
| `Model` | `Model` | Physics model |
| `MotionEnabled` | `bool` | Enable/disable physics simulation |
| `Mass` | `float` | Total mass |
| `Locking` | `PhysicsLock` | Lock axes |
| `PhysicsGroup` | `PhysicsGroup` | Underlying physics group |

Method: `CopyBonesFrom( SkinnedModelRenderer source, bool teleport )` — copy bone positions (for ragdoll transition)

---

## Citizen Animation Helper (sealed)

`Sandbox.Citizen.CitizenAnimationHelper` — high-level animation control for the Citizen model.

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

Sets animgraph parameters for movement, look direction, hold types, ducking, and more.

---

## Voice

`Voice` component — records and transmits voice to other players in multiplayer.

| Property | Type | Description |
|----------|------|-------------|
| `Mode` | `VoiceMode` | Push-to-talk, always-on, etc. |
| `PushToTalkInput` | `string` | Input action name for PTT |
| `IsRecording` | `bool` | Currently recording (read-only) |
| `Volume` | `float` | Playback volume |
