<!--
  s&box Skill : api-core.md

  Full signatures for the types a game touches most.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# API Schema: Core Classes

Full public signatures for the ~50 most-used classes in the s&box engine, extracted from the managed API surface at version 26.08.05. This is a lookup reference, not a tutorial: check it when you need to confirm a method exists, its exact name, its parameter list, or its return type. For usage patterns and worked examples, see the topical reference files instead.

The extraction excludes operators. Signature lines are mechanical and exact; the surrounding prose is not part of the API and can be wrong, but the signatures themselves should not be.

All types are in the `Sandbox` namespace unless noted. Types with no namespace prefix are global.

---

## GameObject

```
// Construction
new GameObject()
new GameObject( string name )
new GameObject( bool enabled, string name )
new GameObject( bool enabled )
new GameObject( GameObject parent, bool enabled = true, string name = null )

// Properties
Scene Scene
GameTransform Transform
string Name
bool Enabled, bool Active, bool IsValid, bool IsDestroyed
Guid Id
GameObject Parent
List<GameObject> Children
bool IsRoot
GameObject Root
GameObjectFlags Flags
GameTags Tags
ComponentList Components
// Transform shortcuts
Vector3 WorldPosition, Rotation WorldRotation, Vector3 WorldScale
Vector3 LocalPosition, Rotation LocalRotation, Vector3 LocalScale
Transform WorldTransform, Transform LocalTransform
// Networking
bool IsProxy, bool IsNetworkRoot
NetworkMode NetworkMode
NetworkAccessor Network
// Prefabs
bool IsPrefabInstance, bool IsPrefabInstanceRoot, string PrefabInstanceSource

// Methods
void Destroy()
void DestroyImmediate()
void Clear()
T AddComponent<T>( bool startEnabled = true )
T GetOrAddComponent<T>( bool startEnabled = true )
T GetComponent<T>( bool includeDisabled = false )
IEnumerable<T> GetComponents<T>( bool includeDisabled = false )
T GetComponentInChildren<T>( bool includeDisabled = false, bool includeSelf = true )
IEnumerable<T> GetComponentsInChildren<T>( bool includeDisabled = false, bool includeSelf = true )
T GetComponentInParent<T>( bool includeDisabled = false, bool includeSelf = true )
IEnumerable<T> GetComponentsInParent<T>( bool includeDisabled = false, bool includeSelf = true )
void SetParent( GameObject value, bool keepWorldPosition = true )
bool IsDescendant( GameObject obj )
bool IsAncestor( GameObject obj )
void AddSibling( GameObject obj, bool before, bool keepWorldPosition = true )
BBox GetBounds()
BBox GetLocalBounds()
IEnumerable<GameObject> GetAllObjects( bool enabledOnly )
GameObject GetNextSibling( bool enabledOnly )
void RunEvent<T>( Action<T> action, FindMode find )
// Cloning (11 overloads, most common)
GameObject Clone( Vector3 position )
GameObject Clone( Vector3 position, Rotation rotation )
GameObject Clone( Transform transform )
GameObject Clone()
// Networking
bool NetworkSpawn()
bool NetworkSpawn( Connection owner )
bool NetworkSpawn( NetworkSpawnOptions options )
// Prefabs
static GameObject GetPrefab( string prefabFilePath )
void BreakFromPrefab()
void UpdateFromPrefab()
// Audio
SoundHandle PlaySound( SoundEvent snd, Vector3 positionOffset )
void StopAllSounds( float fadeOutTime )
```

---

## Component (abstract)

```
// Properties
Scene Scene
GameTransform Transform
GameObject GameObject
ComponentList Components
bool Enabled, bool Active, bool IsValid
Guid Id
ITagSet Tags
ComponentFlags Flags
// Transform shortcuts
Vector3 WorldPosition, Rotation WorldRotation, Vector3 WorldScale
Vector3 LocalPosition, Rotation LocalRotation, Vector3 LocalScale
Transform WorldTransform, Transform LocalTransform
// Networking
NetworkAccessor Network
bool IsProxy
// Async
protected TaskSource Task

// Lifecycle (protected virtual)
Task OnLoad()
void OnAwake()
void OnStart()
void OnEnabled()
void OnUpdate()
void OnFixedUpdate()
void OnPreRender()          // NOT called on dedicated server
void OnDisabled()
void OnDestroy()
void OnValidate()
void OnRefresh()
void OnTagsChanged()
void OnParentChanged( GameObject oldParent, GameObject newParent )
void DrawGizmos()           // editor only
void OnParentDestroy()      // public virtual

// Methods
void Destroy()
void DestroyGameObject()
void Invoke( float secondsDelay, Action action, CancellationToken ct )
// Component queries (same signatures as GameObject)
T AddComponent<T>( bool startEnabled = true )
T GetOrAddComponent<T>( bool startEnabled = true )
T GetComponent<T>( bool includeDisabled = false )
IEnumerable<T> GetComponents<T>( bool includeDisabled = false )
// ... (all GetComponentIn* variants same as GameObject)
```

---

## Scene (extends GameObject)

```
// Properties
CameraComponent Camera
PhysicsWorld PhysicsWorld
SceneTrace Trace
NavMesh NavMesh
float TimeScale
bool IsFixedUpdate
float FixedDelta, float FixedUpdateFrequency
int MaxFixedUpdates, int PhysicsSubSteps
bool IsLoading, bool IsEditor
static IEnumerable<Scene> All
float NetworkFrequency, float NetworkRate

// Methods
IEnumerable<T> GetAll<T>()
T Get<T>()
T GetSystem<T>()
IEnumerable<T> GetAllComponents<T>()
GameObject CreateObject( bool enabled = true )
void Destroy()
bool Load( GameResource sceneResource )
bool Load( SceneLoadOptions options )
bool LoadFromFile( string filename )
void RunEvent<T>( Action<T> action, FindMode find )
IEnumerable<GameObject> FindAllWithTags( IEnumerable<string> tags )
IEnumerable<GameObject> FindAllWithTag( string tag )
IDisposable AddHook( Stage stage, int order, Action fn, string name, string desc )
IDisposable Push()                    // push as active scene
IDisposable BatchGroup()
void ProcessDeletes()
IEnumerable<GameObject> FindInPhysics( Sphere s )
IEnumerable<GameObject> FindInPhysics( BBox b )
IEnumerable<GameObject> FindInPhysics( Frustum f )
```

---

## GameTransform

```
// Properties
Vector3 Position, Rotation Rotation, Vector3 Scale           // world
Vector3 LocalPosition, Rotation LocalRotation, Vector3 LocalScale
Transform Local, Transform World, Transform InterpolatedLocal
GameObject GameObject
TransformProxy Proxy

// Methods
void LerpTo( Transform target, float frac )
void ClearInterpolation()
void ClearLerp()
IDisposable DisableProxy()
```

---

## Vector3 (global)

```
// Constructors
Vector3( float x, float y, float z )
Vector3( float x, float y )
Vector3( float all )

// Static fields
Vector3 Zero, One, Forward, Backward, Up, Down, Left, Right
// Forward = (1,0,0)  Right = (0,-1,0)  Up = (0,0,1)  (Z-up coordinate system)

// Static property
Vector3 Random

// Instance properties
float x, y, z
Vector3 Normal                   // normalized
float Length, LengthSquared
bool IsNearZeroLength

// Instance methods
Vector3 WithX( float ), WithY( float ), WithZ( float )
Vector3 ClampLength( float max )
Vector3 ClampLength( float min, float max )
Vector3 Clamp( Vector3 min, Vector3 max )
Vector3 Clamp( float min, float max )
float Dot( Vector3 b )
Vector3 Cross( Vector3 b )
float Distance( Vector3 target )
float DistanceSquared( Vector3 target )
Vector3 LerpTo( Vector3 target, float frac, bool clamp = true )
Vector3 SlerpTo( Vector3 target, float frac )
Vector3 SubtractDirection( Vector3 direction, float strength = 1 )
Vector3 ProjectOnNormal( Vector3 normal )
Vector3 SnapToGrid( float gridSize )
Vector3 WithFriction( float frictionAmount, float stopSpeed = 140 )
Vector3 WithAcceleration( Vector3 target, float acceleration )
Vector3 AddClamped( Vector3 toAdd, float maxLength )
Vector3 Approach( float length, float amount )
Vector3 Abs()
Vector3 RotateAround( Vector3 center, Rotation rot )
bool AlmostEqual( Vector3 v, float delta = 0.0001f )

// Static methods
float Dot( Vector3 a, Vector3 b )
Vector3 Cross( Vector3 a, Vector3 b )
Vector3 Lerp( Vector3 a, Vector3 b, float frac, bool clamp = true )
Vector3 Slerp( Vector3 a, Vector3 b, float frac )
float DistanceBetween( Vector3 a, Vector3 b )
float DistanceBetweenSquared( Vector3 a, Vector3 b )
Vector3 Direction( Vector3 from, Vector3 to )
Vector3 Reflect( Vector3 direction, Vector3 normal )
float GetAngle( Vector3 v1, Vector3 v2 )
Vector3 Min( Vector3 a, Vector3 b )
Vector3 Max( Vector3 a, Vector3 b )
Vector3 SmoothDamp( Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float deltaTime )
Vector3 SpringDamp( Vector3 current, Vector3 target, ref Vector3 velocity, float dt, float frequency = 2, float damping = 0.5f )
Vector3 CubicBezier( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t )
Vector3 Parse( string str )
bool TryParse( string str, out Vector3 result )
```

---

## Rotation (global)

```
// Constructors
Rotation()
Rotation( float x, float y, float z, float w )

// Static fields
Rotation Identity

// Static property
Rotation Random

// Instance properties
float x, y, z, w
Vector3 Forward, Backward, Up, Down, Right, Left
Rotation Inverse, Normal, Conjugate

// Instance methods
Angles Angles()
float Pitch(), Yaw(), Roll()
float Distance( Rotation to )              // angular distance in degrees
float Angle()                               // angle from identity
Rotation LerpTo( Rotation target, float frac, bool clamp = true )
Rotation SlerpTo( Rotation target, float frac, bool clamp = true )
Rotation Clamp( Rotation to, float degrees )
Rotation RotateAroundAxis( Vector3 axis, float degrees )
Vector3 ClosestAxis( Vector3 normal )
bool AlmostEqual( Rotation r, float delta = 1e-7f )

// Static methods
Rotation FromAxis( Vector3 axis, float degrees )
Rotation LookAt( Vector3 forward )
Rotation LookAt( Vector3 forward, Vector3 up )
Rotation From( float pitch, float yaw, float roll )
Rotation From( Angles angles )
Rotation FromYaw( float yaw )
Rotation FromPitch( float pitch )
Rotation FromRoll( float roll )
Rotation FromToRotation( Vector3 fromDir, Vector3 toDir )
Rotation Difference( Rotation from, Rotation to )
Rotation Lerp( Rotation a, Rotation b, float frac, bool clamp = true )
Rotation Slerp( Rotation a, Rotation b, float frac, bool clamp = true )
Rotation SmoothDamp( Rotation current, Rotation target, ref Vector3 velocity, float smoothTime, float dt )
```

---

## Angles (global)

```
Angles( float pitch, float yaw, float roll )
float pitch, yaw, roll
static Angles Zero
Angles Normal                    // normalized -180..180
Vector3 Forward
Rotation ToRotation()
Angles WithPitch( float ), WithYaw( float ), WithRoll( float )
static Angles Lerp( Angles from, Angles to, float frac )
```

---

## Transform (global)

```
Transform( Vector3 position )
Transform( Vector3 position, Rotation rotation, float scale )
Transform( Vector3 position, Rotation rotation, Vector3 scale )
Vector3 Position
Rotation Rotation
Vector3 Scale
static Transform Zero
Vector3 Forward, Backward, Up, Down, Right, Left
Ray ForwardRay
Vector3 PointToWorld( Vector3 local )
Vector3 PointToLocal( Vector3 world )
Vector3 NormalToWorld( Vector3 local )
Vector3 NormalToLocal( Vector3 world )
Rotation RotationToWorld( Rotation local )
Rotation RotationToLocal( Rotation world )
Transform ToLocal( Transform child )
Transform ToWorld( Transform child )
static Transform Lerp( Transform a, Transform b, float frac, bool clamp = true )
static Transform Concat( Transform parent, Transform local )
```

---

## Color (global)

```
Color( float r, float g, float b, float a = 1 )
float r, g, b, a
// Static colors
Color White, Black, Gray, Red, Green, Blue, Yellow, Orange, Cyan, Magenta, Transparent
Color Random
// Instance
string Hex                       // "#RRGGBB" or "#RRGGBBAA"
Color WithAlpha( float alpha )
Color WithRed( float ), WithGreen( float ), WithBlue( float )
Color Darken( float fraction )
Color Lighten( float fraction )
Color Desaturate( float fraction )
Color Saturate( float fraction )
Color Invert()
Color LerpTo( Color target, float frac, bool clamp = true )
// Static
Color Lerp( Color a, Color b, float frac, bool clamped = true )
Color FromBytes( int r, int g, int b, int a = 255 )
Color FromRgb( uint rgb )
Color FromRgba( uint rgba )
Color? Parse( string value )
bool TryParse( string value, out Color color )
```

---

## BBox (global)

```
BBox( Vector3 mins, Vector3 maxs )
BBox( Vector3 center, float size )
Vector3 Mins, Maxs
Vector3 Center, Size, Extents
float Volume
Vector3 RandomPointInside, RandomPointOnEdge
Vector3[] Corners
bool Contains( Vector3 point, float epsilon = 0 )
bool Contains( BBox other )
bool Overlaps( BBox other )
Vector3 ClosestPoint( Vector3 point )
BBox Grow( float amount )
BBox Translate( Vector3 offset )
BBox Rotate( Rotation rot )
BBox Transform( Transform tx )
void AddPoint( Vector3 point )
void AddBBox( BBox box )
static BBox FromHeightAndRadius( float h, float r )
static BBox FromPositionAndSize( Vector3 pos, float size )
static BBox FromPositionAndSize( Vector3 pos, Vector3 size )
```

---

## Ray (global)

```
Ray( Vector3 origin, Vector3 direction )
Vector3 Position          // origin
Vector3 Forward           // direction
Vector3 Project( float distance )    // point at distance
Ray ToLocal( Transform tx )
Ray ToWorld( Transform tx )
```

---

## Capsule (global)

```
Capsule( Vector3 a, Vector3 b, float radius )
Vector3 CenterA, CenterB
float Radius
BBox Bounds, float Volume
bool Contains( Vector3 point )
static Capsule FromHeightAndRadius( float height, float radius )
```

---

## Input (static)

```
static bool Down( string action, bool complainOnMissing = true )
static bool Pressed( string action )
static bool Released( string action )
static void Clear( string action )
static void ReleaseActions()
static void SetAction( string action, bool down )
static Vector3 AnalogMove
static Angles AnalogLook
static Vector2 MouseDelta, MouseWheel
static bool EscapePressed
static bool UsingController
static int ControllerCount
static bool Suppressed
static float GetAnalog( InputAnalog analog )
static IDisposable PlayerScope( int index )
static Texture GetGlyph( string name, InputGlyphSize size = 0, bool outline = false )
static string GetButtonOrigin( string name, bool ignoreController = false )
static void TriggerHaptics( float leftMotor, float rightMotor, float leftTrigger = 0, float rightTrigger = 0, int duration = 500 )
static void TriggerHaptics( HapticEffect pattern, float lengthScale = 1, float frequencyScale = 1, float amplitudeScale = 1 )
static void StopAllHaptics()
```

---

## Time (static)

```
static float Now
static float Delta
static double NowDouble
```

## TimeSince (struct)

```
// Assign 0 to reset, compare to float for elapsed seconds
float Relative          // seconds elapsed
float Absolute          // timestamp when reset
// Implicit: (float)timeSince → Relative
```

## TimeUntil (struct)

```
// Assign seconds to set countdown, compare as bool (true when expired)
float Relative          // seconds remaining
float Passed            // seconds since start
float Fraction          // 0→1 progress
// Implicit: (bool)timeUntil → true when expired
```

---

## Mouse (static)

```
static Vector2 Position
static Vector2 Delta
static Vector2 Velocity
static MouseVisibility Visibility    // Auto, Visible, Hidden
static string CursorType
static bool Active
```

## Screen (static)

```
static Vector2 Size
static float Width, Height, Aspect
static float DesktopScale
```

---

## Model

```
static Model Load( string filename )          // main thread only; see notes below
static Task<Model> LoadAsync( string filename )
static Model Cube, Sphere, Plane, Error
BBox Bounds, PhysicsBounds, RenderBounds
bool IsValid, string Name
int BoneCount, AnimationCount, MorphCount, MeshCount
BoneCollection Bones
HitboxSet HitboxSet
ImmutableArray<Material> Materials
ModelAttachments Attachments
string GetAnimationName( int index )
Transform? GetAttachment( string name )
string GetBoneName( int boneIndex )
Transform GetBoneTransform( int boneIndex )
```

**`Model.Load` can return null: always null-check it.** Two different failure results
(`Resources/Model/Model.Load.cs:12-28`):

- Null / empty / whitespace path → returns `Model.Error`, the visible pink-checker
  placeholder (`models/dev/error.vmdl`, `Model.Static.cs:58`).
- A path that doesn't resolve → falls through to native lookup and returns **null**
  (`Model.Static.cs:12-15` returns null for an invalid native handle).

So "no model set" gives you an error model, but "typo in the path" gives you a null
reference two frames later.

```csharp
var model = Model.Load( path );
if ( model is null )
{
    Log.Warning( $"Missing model: {path}" );
    model = Model.Error;      // fail visibly, not with a NullReferenceException
}
```

`Model.Load` asserts it is on the main thread. Use `Model.LoadAsync` off the hot path.

---

## GameResource & `[AssetType]`

A `GameResource` is a data asset defined in C# and authored as JSON on disk: the s&box
answer to a Unity `ScriptableObject`. Item definitions, ammo types, vehicle stats,
loot tables.

### Declaring the type

**`[GameResource( title, extension, description )]` is obsolete engine-wide**
(`Resources/GameResourceAttribute.cs:82`, `[Obsolete( "Use AssetType instead" )]`). Under
`TreatWarningsAsErrors` it is a hard build failure. Use `[AssetType]` with **property
initializers**, not constructor arguments:

```csharp
[AssetType( Name = "Item Definition", Extension = "item", Category = "MyGame" )]
public sealed class ItemDefinition : GameResource
{
    [Property] public string DisplayName { get; set; } = "Item";
    [Property] public int Value { get; set; }
    [Property] public Model WorldModel { get; set; }
}
```

`AssetTypeAttribute` (`Resources/GameResourceAttribute.cs:9-61`):

| Member | Type | Notes |
|---|---|---|
| `Name` | `string` | Title in the editor. Defaults to the class name if unset |
| `Extension` | `string` | File extension, no period. Letters only, keep it under 8 chars |
| `Category` | `string` | Grouping in the asset browser. Default `"Other"` |
| `Flags` | `AssetTypeFlags` | `NoEmbedding`, `IncludeThumbnails` |
| `IconColor` | `string` | Thumbnail accent, default `"#67ac5c"` |
| `static FindTypeByExtension( string )` | `TypeDescription` | Reverse lookup |

The base class is unchanged: you still derive from `GameResource`, and
`PostLoad()` / `StateHasChanged()` still work. The engine's own
`BaseAmmoResource` is the live reference implementation:
`[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Game" )]`
(`Scene/Components/Game/Weapon/BaseAmmoResource.cs:8-10`).

### The on-disk JSON format

Hand-authorable. **PascalCase keys matching the property names**, plus the envelope.
Real engine asset (`game/addons/base/Assets/ammo/9mm.ammo`):

```json
{
  "Title": "Pistol Ammo",
  "Icon": null,
  "MaxReserve": 300,
  "__references": [],
  "__version": 0
}
```

- `__references`: assets this one depends on. Leave `[]`; the editor maintains it.
- `__version`: resource version, for your own migration logic.
- A `Model` / `Material` / `SoundEvent` / other resource-typed `[Property]` serialises as
  its **path string**: `"WorldModel": "models/citizen_props/crate01.vmdl"`.

The source file is inert until compiled. `CompileGameResource`
(`Sandbox.Tools/Assets/AssetSystem.Compile.cs:87-123`) reads the JSON, scans it for
references, and writes the compiled blob; the runtime only ever loads the `_c`
(`ResourceLibrary.LoadGameResource`, `:460`, appends `_c` at `:465` before reading, then
`TryLoadFromData` → `Register` → `PostLoadInternal` at `:491`, `:511`, `:514`).

### Loading and enumerating

```
static T       ResourceLibrary.Get<T>( string filepath )                       // :558
static bool    ResourceLibrary.TryGet<T>( string filepath, out T resource )    // :567
static IEnumerable<T> ResourceLibrary.GetAll<T>()                              // :573
static IEnumerable<T> ResourceLibrary.GetAll<T>( string filepath, bool recursive = true )  // :581
static Task<T> ResourceLibrary.LoadAsync<T>( string path )                     // :586
static T       ResourceLibrary.Get<T>( int identifier )     // [Obsolete] :551
```

(Line numbers in `Resources/ResourceLibrary.cs`.) `GetAll<T>()` is how you build a
registry at startup; every compiled resource of that type is already registered.

`ResourceLibrary.IEventListener` gives you `OnRegister` / `OnUnregister` / `OnSave` /
`OnExternalChanges` / `OnExternalChangesPostLoad` for editor-time hot-reload.

### Identity: persist the path, not the id

On `Resource` (`Resources/Resource.cs`):

| Member | Notes |
|---|---|
| `string ResourcePath` | The canonical identifier. **Persist this.** (`:26`) |
| `string ResourceName` | Filename without extension (`:32`) |
| `int ResourceId` | **`[Obsolete]`**, a 32-bit hash of the path (`:16-17`) |

Over the network a resource reference costs 8 bytes: `Resource` implements
`BytePack.ISerializer` and writes only its 64-bit path hash
(`Resource.cs:171-193`), resolving it on the far side from the already-loaded index, or
loading from disk if the client hasn't touched it yet. That is why a `GameResource`
reference is legal inside a `[Sync]` struct or a `NetList<T>` element.

> Authoring a resource asset from outside the editor has its own gotchas (nothing watches
> your source file). See `field-notes.md` → *Authoring a GameResource
> Asset by Hand*.

---

## Material

```
static Material Load( string filename )
static Task<Material> LoadAsync( string filename )
static Material Create( string name, string shader, bool anonymous = true )
static Material FromShader( string path )
Material CreateCopy( string name = null )
bool IsValid, string Name, string ShaderName
bool Set( string param, float/int/bool/Color/Vector2/Vector3/Vector4/Texture value )
Texture GetTexture( string name )
Color GetColor( string name )
```

---

## Sound (static)

```
static SoundHandle Play( string eventName, float fadeInTime = 0 )
static SoundHandle Play( SoundEvent soundEvent, float fadeInTime = 0 )
static SoundHandle Play( SoundEvent soundEvent, Vector3 position, float fadeInTime = 0 )
static SoundHandle Play( string eventName, Vector3 position, float fadeInTime = 0 )
static void StopAll( float fade )
static void Preload( string eventName )
static Transform Listener
static float MasterVolume
```

## SoundHandle

```
Vector3 Position
float Volume, Pitch, Decibels
bool IsPlaying, Paused, Finished, IsStopped
float Distance                   // hearing distance
float ElapsedTime
bool Occlusion, Reflections
bool FollowParent
void Stop( float fadeTime = 0 )
void SetParent( GameObject obj )
void ClearParent()
```

---

## Log (Diagnostics.Logger)

```
// Global instance: Log
void Info( FormattableString message )
void Info( object message )
void Warning( FormattableString message )
void Warning( object message )
void Warning( Exception ex, FormattableString message )
void Error( FormattableString message )
void Error( object message )
void Error( Exception ex )
void Trace( FormattableString message )
```

---

## Http (static)

```
static Task<string> RequestStringAsync( string uri, string method = "GET", HttpContent content = null, Dictionary<string,string> headers = null, CancellationToken ct = null )
static Task<byte[]> RequestBytesAsync( string uri, string method = "GET", ... )
static Task<T> RequestJsonAsync<T>( string uri, string method = "GET", ... )
static Task<HttpResponseMessage> RequestAsync( string uri, string method = "GET", ... )
static Task<Stream> RequestStreamAsync( string uri, string method = "GET", ... )
static HttpContent CreateJsonContent<T>( T target )
static bool IsAllowed( Uri uri )
```

---

## SceneTrace

```
// Shape (all return SceneTrace for chaining)
SceneTrace Ray( Vector3 from, Vector3 to )
SceneTrace Ray( Ray ray, float distance )
SceneTrace Sphere( float radius, Vector3 from, Vector3 to )
SceneTrace Box( BBox bbox, Vector3 from, Vector3 to )
SceneTrace Capsule( Capsule capsule, Vector3 from, Vector3 to )
SceneTrace Size( BBox hull )
SceneTrace Size( Vector3 size )
SceneTrace Radius( float radius )
SceneTrace Body( PhysicsBody body, Vector3 to )
// Filters
SceneTrace WithTag( string tag )
SceneTrace WithAllTags( string[] tags )
SceneTrace WithAnyTags( string[] tags )
SceneTrace WithoutTags( string[] tags )
SceneTrace WithCollisionRules( string tag, bool asTrigger = false )
SceneTrace IgnoreGameObject( GameObject obj )
SceneTrace IgnoreGameObjectHierarchy( GameObject obj )
SceneTrace HitTriggers()
SceneTrace HitTriggersOnly()
SceneTrace IgnoreStatic()
SceneTrace IgnoreDynamic()
// Options
SceneTrace UseHitboxes( bool hit = true )
SceneTrace UseHitPosition( bool enabled = true )
SceneTrace UsePhysicsWorld( bool hit = true )
// Execute
SceneTraceResult Run()
IEnumerable<SceneTraceResult> RunAll()
```

## SceneTraceResult (struct)

```
bool Hit, bool StartedSolid
Vector3 StartPosition, EndPosition, HitPosition, Normal, Direction
float Fraction, float Distance
GameObject GameObject
Component Component
Collider Collider
PhysicsBody Body
Surface Surface
Hitbox Hitbox
int Bone, int Triangle
string[] Tags
```

---

## Collision & DamageInfo

```
// Collision (struct, passed to ICollisionListener)
CollisionSource Self, Other
PhysicsContact Contact

// CollisionSource (struct)
PhysicsBody Body, PhysicsShape Shape, Surface Surface
Collider Collider, GameObject GameObject
bool IsTrigger

// PhysicsContact (struct)
Vector3 Point, Speed, Normal
float NormalSpeed, Impulse

// CollisionStop (struct, passed to OnCollisionStop)
CollisionSource Self, Other

// DamageInfo (class)
float Damage
GameObject Attacker, Weapon
Hitbox Hitbox
Vector3 Origin, Position
PhysicsShape Shape
TagSet Tags
bool IsExplosion
// Constructors
DamageInfo()
DamageInfo( float damage, GameObject attacker, GameObject weapon )
DamageInfo( float damage, GameObject attacker, GameObject weapon, Hitbox hitbox )
```

---

## Tags (ITagSet / GameTags / TagSet)

```
// ITagSet (abstract base, used by Component.Tags)
bool Has( string tag )
void Add( string tag )
void Remove( string tag )
void Set( string tag, bool state )
void Toggle( string tag )
void RemoveAll()
bool HasAny( params string[] tags )
bool HasAll( params string[] tags )
IEnumerable<string> TryGetAll()

// GameTags (on GameObject.Tags, includes ancestor inheritance)
bool Has( string tag, bool includeAncestors )

// TagSet (standalone, e.g. DamageInfo.Tags)
TagSet()
TagSet( IEnumerable<string> tags )
bool IsEmpty
```

---

## MathX (static)

```
static float Lerp( float from, float to, float frac, bool clamp = true )
static float LerpInverse( float value, float from, float to, bool clamp = true )
static float Clamp( float v, float min, float max )
static float Remap( float value, float oldLow, float oldHigh, float newLow = 0, float newHigh = 1 )
static float Approach( float current, float target, float delta )
static float DeltaDegrees( float from, float to )        // -180..180
static float NormalizeDegrees( float degree )             // 0..360
static float LerpDegrees( float from, float to, float frac, bool clamp = true )
static float SnapToGrid( float f, float gridSize )
static float DegreeToRadian( float deg )
static float RadianToDegree( float rad )
static bool AlmostEqual( float a, float b, float within = 0.0001f )
static float SmoothDamp( float current, float target, ref float velocity, float smoothTime, float dt )
static float SpringDamp( float current, float target, ref float velocity, float dt, float frequency = 2, float damping = 0.5f )
static float ExponentialDecay( float current, float target, float halflife, float dt )
```

---

## Curve

```
Curve()
Curve( params Curve.Frame[] frames )
static Curve Linear, Ease, EaseIn, EaseOut
float Evaluate( float time )
float EvaluateDelta( float time )     // normalized 0-1 input/output
Curve Reverse()
int AddPoint( float x, float y )
Vector2 TimeRange, ValueRange
int Length
```

---

## Game (static)

```
static Scene ActiveScene
static bool IsEditor, IsPlaying, IsPaused
static bool InGame                    // not in main menu
static bool IsRunningInVR, IsRunningOnHandheld
static string Ident                   // game identifier
static SteamId SteamId
static Random Random                  // auto-seeded per tick
static bool CheatsEnabled
static TypeLibrary TypeLibrary
static CookieContainer Cookies        // persistent cross-session data
static void Close()
static void Disconnect()
static bool ChangeScene( SceneLoadOptions options )
```

---

## Connection

```
static Connection Local, Host
static IReadOnlyList<Connection> All
static Connection Find( Guid id )
Guid Id
string DisplayName, Name, Address
SteamId SteamId
bool IsHost, IsActive, IsConnecting
float Ping, Latency
bool CanSpawnObjects, CanRefreshObjects, CanDestroyObjects
ConnectionStats Stats
void Kick( string reason )
bool HasPermission( string permission )
bool Down( string action ), Pressed( string action ), Released( string action )
```

---

## Networking (static)

```
static bool IsHost, IsClient, IsActive, IsConnecting
static string ServerName, MapName
static int MaxPlayers
static void CreateLobby( LobbyConfig config )
static Task<bool> JoinBestLobby( string ident )
static Task<List<LobbyInformation>> QueryLobbies( CancellationToken ct = null )
static void Connect( string target )
static void Connect( ulong steamId )
static void Disconnect()
static void SetData( string key, string value )
static string GetData( string key, string defaultValue = "" )
```

---

## Surface

```
float Friction, Elasticity, Density
string Description, Tags
static Surface FindByName( string name )
SoundHandle PlayCollisionSound( Vector3 position, float speed = 320 )
bool HasTag( string tag )
```

---

## FileSystem (static) / BaseFileSystem

```
// Access
static BaseFileSystem FileSystem.Data       // user game data
static BaseFileSystem FileSystem.Mounted    // all mounted content

// BaseFileSystem
bool FileExists( string path )
bool DirectoryExists( string path )
void CreateDirectory( string folder )
string ReadAllText( string path )
void WriteAllText( string path, string contents )
Span<byte> ReadAllBytes( string path )
void WriteAllBytes( string path, byte[] contents )
T ReadJson<T>( string filename, T defaultValue = null )
void WriteJson<T>( string filename, T data )
IEnumerable<string> FindFile( string folder, string pattern = "*", bool recursive = false )
IEnumerable<string> FindDirectory( string folder, string pattern = "*", bool recursive = false )
void DeleteFile( string path )
Stream OpenRead( string path )
Stream OpenWrite( string path, FileMode mode = FileMode.Create )
long FileSize( string path )
```

---

## SceneLoadOptions

```
SceneLoadOptions()
bool ShowLoadingScreen
bool IsAdditive
bool DeleteEverything
Transform Offset
bool SetScene( SceneFile sceneFile )
bool SetScene( string sceneFileName )
```

---

## ComponentList (GameObject.Components)

```
int Count
T Get<T>( bool includeDisabled = false )
T Get<T>( FindMode search )
IEnumerable<T> GetAll<T>( FindMode find )
IEnumerable<Component> GetAll()
Component Get( Guid id )
bool TryGet<T>( out T component, FindMode search )
T GetOrCreate<T>( FindMode flags )
T Create<T>( bool startEnabled = true )
T GetInAncestorsOrSelf<T>( bool includeDisabled = false )
T GetInDescendantsOrSelf<T>( bool includeDisabled = false )
T GetInChildrenOrSelf<T>( bool includeDisabled = false )
T GetInParentOrSelf<T>( bool includeDisabled = false )
```

---

## GameObjectDirectory (Scene.Directory)

```
int Count, GameObjectCount, ComponentCount
GameObject FindByGuid( Guid guid )
Component FindComponentByGuid( Guid guid )
IEnumerable<GameObject> FindByName( string name, bool caseinsensitive = true )
```

---

## Key Enums

```
// FindMode (flags, combine with |)
Enabled, Disabled
InSelf, InParent, InAncestors, InChildren, InDescendants
// Common combos: EnabledInSelfAndDescendants, EverythingInSelfAndChildren

// GameObjectFlags
None, Hidden, NotSaved, DontDestroyOnLoad, Bone, EditorOnly, NotNetworked

// ComponentFlags
None, Hidden, NotSaved, NotEditable, NotNetworked, NotCloned

// NetworkMode
Never, Object, Snapshot

// OwnerTransfer
Takeover, Fixed, Request

// NetworkOrphaned
Destroy, Host, Random, ClearOwner

// SyncFlags
FromHost, Query, Interpolate

// NetFlags
Unreliable, Reliable, SendImmediate, DiscardOnDelay, HostOnly, OwnerOnly

// InputAnalog
LeftStickX, LeftStickY, RightStickX, RightStickY, LeftTrigger, RightTrigger
```
