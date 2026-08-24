<!--
  s&box Skill : 16_API_INDEX.md

  Namespace-organised index of the wider API surface.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# API Schema: Extended Reference

A namespace-organized index of public s\&box types, extracted from the engine's managed API surface at version 26.08.05. It covers everything not already listed in one of these:
- `15_API_CORE.md`: full signatures for the ~50 most-used classes
- `02_COMPONENTS.md`: all 144 built-in Component-derived types
- `03_UI.md`, `04_NETWORKING.md`, `05_INPUT_PHYSICS.md`: these three include their API inline rather than deferring here

Use this file for discovery: to answer "does this exist?" and "what does it do?". Each entry follows the format `MethodName( args ) → ReturnType` or `PropertyName : Type`.

A line in this schema proves the member exists and its exact signature. It does not prove how the member behaves at runtime, what it throws, or what order it must be called in; treat behavioral claims elsewhere in the skill as separate from what this file guarantees. The extraction excludes operators, so operator overloads on a type will not appear here even when the type itself is listed.

## Namespace Summary

| Namespace | Types | Notes |
|---|---|---|
| `Sandbox` | 289 | The core runtime: animation, assets, services, scene utilities |
| `(global)` | 174 | Math types (Vector2, Matrix), interfaces (IDamageable, ICollisionListener), and animation events |
| `Sandbox.UI` | 79 | Styling enums (Align, FlexDirection, and the like) plus CSS value types (Length, Shadow) |
| `Sandbox.UI.Construct` | 4 |  |
| `Sandbox.Network` | 12 | Bandwidth stats and the network message types |
| `Sandbox.Physics` | 10 | Physics helpers and collision primitives |
| `Sandbox.Audio` | 16 | DSP processors and audio graph nodes |
| `Sandbox.VR` | 13 | VR input, hand tracking, and controller data |
| `Sandbox.Movement` | 1 | A movement helper |
| `Sandbox.Navigation` | 5 | NavMesh path, link, and area types |
| `Sandbox.Rendering` | 15 | Render targets, materials, and the post-processing API |
| `Sandbox.Resources` | 14 | Resource loading and asset management |
| `Sandbox.Services` | 14 | Leaderboards, achievements, stats, and the server browser |
| `Sandbox.Services.Players` | 2 |  |
| `Sandbox.Diagnostics` | 8 | Performance profiling and allocation tracking |
| `Sandbox.Localization` | 4 |  |
| `Sandbox.Utility` | 12 | Noise, SVG, and other general-purpose utilities |
| `Sandbox.Utility.Svg` | 17 |  |
| `Sandbox.ActionGraphs` | 10 | The visual scripting graph types |
| `Sandbox.Clutter` | 10 | The clutter/scatter system |
| `Sandbox.Volumes` | 2 | Volume primitives |
| `Sandbox.Bind` | 4 |  |
| `Sandbox.Compression` | 1 |  |
| `Sandbox.DataModel` | 2 |  |
| `Sandbox.Html` | 1 |  |
| `Sandbox.Helpers` | 1 |  |
| `Sandbox.Menu` | 1 |  |
| `Sandbox.Modals` | 6 |  |
| `Sandbox.Mounting` | 6 |  |
| `Sandbox.Razor` | 1 |  |
| `Sandbox.Speech` | 3 |  |
| `Sandbox.Tasks` | 1 |  |

---

## Sandbox

### Achievement (class)
- `IsVisible : bool`, `ProgressionFraction : float`, `Name : string`

### AchievementCollection (class)
Holds achievements for a package
- `All : Collections.Generic.IReadOnlyCollection<Achievement>`, `RecountProgression(  ) → Task`, `Get( string name ) → Achievement`

### AnimationBuilder (class)
Provides ability to generate animations for a `Model` at runtime
- `Name : string`, `FrameRate : float`, `Looping : bool`, `WithName( string name ) → AnimationBuilder`, `WithFrameRate( float frameRate ) → AnimationBuilder`

### AnimationGraph (class)
- `IsError : bool`, `Name : string`, `ParamCount : int`, `static Load( string filename ) → AnimationGraph`, `GetParameterType( int index ) → Type`

### AnimationSequence (class)
- `Duration : float`, `IsFinished : bool`, `Name : string`

### AnimGraphDirectPlayback (class)
For communicating with a Direct Playback Anim Node, which allows code to tell it to play a given sequence
- `StartTime : float`, `TimeNormalized : float`, `Duration : float`, `Cancel(  ) → void`, `Play( string name ) → void`

### AnimParam<T> (struct)
Anim param values contain any value for a limited set of types

### AnyOfType<T> (struct)
A wrapper that holds an instance of any concrete type assignable to `T`
- `Value : T`, `HasValue : bool`

### Application (class)
- `AppId : UInt64`, `IsUnitTest : bool`, `IsHeadless : bool`

### AssetTypeAttribute (class)
Marks a `GameResource` subclass as storable on disk as an asset. **This is what you want,
not the obsolete `[GameResource(...)]`.** Configure it with property initializers rather
than constructor arguments: `[AssetType( Name = "Item", Extension = "item", Category = "Game" )]`
- `Name : string`, `Extension : string`, `Category : string` (default `"Other"`), `Flags : AssetTypeFlags`, `IconColor : string`, `static FindTypeByExtension( string extension ) → TypeDescription`
- Full usage, on-disk JSON format and loading: `15_API_CORE.md` → *GameResource & `[AssetType]`*

### AssetTypeFlags (enum)
Flags for `AssetTypeAttribute`
Values: None, NoEmbedding, IncludeThumbnails

### AudioSurface (enum)
Defines acoustic properties of a surface, which defines how sound will bounce
Values: Generic, Brick, Concrete, Ceramic, Gravel, Carpet, Glass, Plaster, Wood, Metal … (+15 more)

### BallJointBuilder (class)
Provides ability to generate a ball joint for a `Model` at runtime
- `EnableSwingLimit : bool`, `EnableTwistLimit : bool`, `SwingLimit : float`, `WithSwingLimit( float v ) → BallJointBuilder`, `WithTwistLimit( float min, float max ) → BallJointBuilder`

### Bitmap (class)
- `static CreateFromBytes( byte[] data ) → Bitmap`, `static IsIes( byte[] data ) → bool`, `static CreateFromPsdBytes( byte[] data ) → Bitmap`, `static IsPsd( byte[] data ) → bool`, `Size : Vector2Int`

### BlendMode (enum)
Blend modes used by the UI system
Values: Normal, Multiply, Lighten, PremultipliedAlpha

### BlobData (class)
Base class for properties that should be serialized to binary format instead of JSON
- `Serialize( BlobData.Writer writer ) → void`, `Deserialize( BlobData.Reader reader ) → void`, `Upgrade( BlobData.Reader reader, int fromVersion ) → void`, `Version : int`

### BoneCollection (class)
A collection of bones
- `Root : BoneCollection.Bone`, `AllBones : IReadOnlyList<BoneCollection.Bone>`, `HasBone( string name ) → bool`, `GetBone( string name ) → BoneCollection.Bone`

### ByteStream (struct)
Write and read bytes to a stream
- `static Create( int size ) → ByteStream`, `ToArray(  ) → byte[]`, `Read(  ) → T`, `EnsureCanWrite( int size ) → void`, `Writable : bool`

### CachingHandler (class)

### CaseInsensitiveConcurrentDictionary<T> (class)

### CaseInsensitiveDictionary<T> (class)

### CharacterControllerHelper (struct)
- `TryMove( float timestep ) → float`, `TraceMove( Vector3 delta ) → SceneTraceResult`, `TraceFromTo( Vector3 start, Vector3 end ) → SceneTraceResult`, `TryMoveWithStep( float timeDelta, float stepsize ) → float`

### ClearFlags (enum)
Flags for clearing a RT before rendering a scene using a SceneCamera
Values: None, Color, Depth, Stencil, All

### CloneConfig (struct)
The low level input of a GameObject

### Clothing (class)
Describes an item of clothing and implicitly which other items it can be worn with
- `HumanSkinModel : string`, `HumanSkinMaterial : string`, `HumanEyesMaterial : string`, `HasPermissions(  ) → bool`, `CanBeWornWith( Clothing target ) → bool`

### ClothingContainer (class)
Holds a collection of clothing items
- `static CreateFromLocalUser(  ) → ClothingContainer`, `static CreateFromJson( string json ) → ClothingContainer`, `static CreateFromConnection( Connection connection, bool removeUnowned ) → ClothingContainer`, `Normalize(  ) → void`, `DisplayName : string`

### Cloud (class)
For accessing assets from the cloud - from code
- `static Asset( string ident ) → string`, `static Model( string ident ) → Model`, `static Material( string ident ) → Material`, `static SoundEvent( string ident ) → SoundEvent`

### CodeArchive (class)
- `CompilerName : string`, `Configuration : Compiler.Configuration`, `Version : long`, `Serialize(  ) → byte[]`

### CodeGeneratorFlags (enum)
Used to specify what type of code generation to perform
Values: WrapPropertyGet, WrapPropertySet, WrapMethod, Static, Instance

### ColliderFlags (enum)
Values: IgnoreTraces, IgnoreMass

### CollisionSoundSystem (class)
This system exists to collect pending collision sounds and filter them into a unique set, to avoid unnesssary sounds pl…
- `RegisterCollision( Collision collision ) → void`, `AddShapeCollision( PhysicsShape shape, Surface surface, Vector3 position, float speed, bool networked ) → void`, `AddShapeCollision( PhysicsShape shape, Surface surface, PhysicsContact contact, bool networked ) → void`

### CompactTerrainMaterial (struct)
Compact terrain material encoding with base/overlay texture blending
- `BaseTextureId : byte`, `OverlayTextureId : byte`, `BlendFactor : byte`

### CompileGroup (class)
- `SuppressBuildNotifications : bool`, `Name : string`, `NeedsBuild : bool`, `Dispose(  ) → void`, `BuildAsync(  ) → Task<bool>`

### Compiler (class)
Given a folder of
- `Group : CompileGroup`, `Output : CompilerOutput`, `IsBuilding : bool`, `static StripDisabledTextTrivia( Microsoft.CodeAnalysis.SyntaxTree tree ) → Microsoft.CodeAnalysis.SyntaxTree`, `MarkForRecompile(  ) → void`

### CompilerExtensions (class)
- `static AddBaseReference( Compiler compiler ) → void`, `static AddToolBaseReference( Compiler compiler ) → void`, `static AddReference( Compiler compiler, Compiler reference ) → void`, `static AddReference( Compiler compiler, Package reference ) → void`

### CompilerOutput (class)
- `Successful : bool`, `Compiler : Compiler`, `Version : Version`

### ComputeBuffer<T> (class)

### ComputeBufferType (enum)
Values: Structured, ByteAddress, Append, IndirectDrawArguments

### ComputeShader (class)
A compute shader is a program that runs on the GPU, often with data provided to/from the CPU by means of a `GpuBuffer`1…
- `DispatchIndirect( GpuBuffer indirectBuffer, uint indirectElementOffset ) → void`, `Dispatch( int threadsX, int threadsY, int threadsZ ) → void`, `DispatchIndirectWithAttributes( RenderAttributes attributes, GpuBuffer indirectBuffer, uint indirectElementOffset ) → void`, `DispatchWithAttributes( RenderAttributes attributes, int threadsX, int threadsY, int threadsZ ) → void`, `Attributes : RenderAttributes`

### ConfigData (class)
Project configuration data is derived from this class
- `Guid : Guid`, `Version : int`, `Serialize(  ) → Text.Json.Nodes.JsonObject`, `Deserialize( string json ) → void`

### ConsoleSystem (class)
A library to interact with the Console System
- `static Run( string command ) → void`, `static SetValue( string name, object value ) → void`, `static GetValue( string name, string defaultValue ) → string`, `static Run( string command, object[] arguments ) → void`

### ControlModeSettings (class)
- `Keyboard : bool`, `VR : bool`, `Gamepad : bool`

### ConVarFlags (enum)
Values: None, Saved, Replicated, Cheat, UserInfo, Hidden, ChangeNotice, Protected, Server, Admin … (+1 more)

### CookieContainer (class)
- `Remove( string key ) → void`, `SetString( string key, string value ) → void`, `GetString( string key, string fallback ) → string`, `TryGetString( string key, string val ) → bool`

### CubemapFogController (class)
- `LodBias : float`, `StartDistance : float`, `EndDistance : float`

### CurrencyValue (struct)
Describes money, in a certain currency
- `Format(  ) → string`

### CursorSettings (class)
- `Version : int`, `Cursors : Dictionary<string,CursorSettings.Cursor>`

### CurveRange (struct)
Two curves
- `A : Curve`, `B : Curve`, `Evaluate( float x, float y ) → float`, `EvaluateDelta( float x, float y ) → float`

### DebugOverlaySystem (class)
- `Trace( SceneTraceResult trace, float duration, bool overlay ) → void`, `ScreenText( Vector2 pixelPosition, TextRendering.Scope textBlock, TextFlag flags, float duration ) → void`, `Box( BBox box, Color color, float duration, Transform transform, bool overlay ) → void`, `Line( Line line, Color color, float duration, Transform transform, bool overlay ) → void`

### DecalDefinition (class)
A decal which can be applied to objects and surfaces
- `ColorTexture : Texture`, `NormalTexture : Texture`, `RoughMetalOcclusionTexture : Texture`

### DecalGameSystem (class)
- `MaxDecals : int`, `ClearDecals(  ) → void`

### DisplayInfo (struct)
Collects all the relevant info (such as description, name, icon, etc) from attributes and other sources about a type or…
- `static ForEnumValues(  ) → ValueTuple<T,DisplayInfo>[]`, `static ForEnumValues( Type t ) → DisplayInfo[]`, `static ForType( Type t, bool inherit ) → DisplayInfo`, `static For( object t, bool inherit ) → DisplayInfo`

### Doo (class)
A visual scripting task composed of executable blocks
- `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`, `GetLabel(  ) → string`, `IsEmpty(  ) → bool`, `Body : List<Doo.Block>`

### DooEngine (class)
System that manages the execution of Doo scripts within a scene
- `SetGlobalVariable( string name, object value ) → void`

### EditorSystemPublic (class)
- `Scene : Scene`, `Camera : CameraComponent`, `ProgressSection( bool modal ) → Editor.IProgressSection`, `ForEachAsync( IEnumerable<T> list, string title, Func<T,Threading.CancellationToken,Task> worker, Threading.CancellationToken cancel, bool modal ) → Task`

### EditorTint (enum)
Values: White, Pink, Green, Yellow, Blue, Red

### EnumDescription (class)
- `GetEntry( object value ) → EnumDescription.Entry`, `GetEntry( long value ) → EnumDescription.Entry`, `GetEntries( long value ) → EnumDescription.Entry[]`, `Unique : EnumDescription.Entry[]`

### FieldDescription (class)
Describes a field
- `FieldType : Type`, `IsField : bool`, `IsInitOnly : bool`, `GetValue( object obj ) → object`, `SetValue( object obj, object value ) → void`

### FileWatch (class)
Watch folders, dispatch events on changed files
- `Enabled : bool`, `Changes : List<string>`, `static Tick(  ) → void`, `Dispose(  ) → void`

### FixedJointBuilder (class)
Provides ability to generate a fixed joint for a `Model` at runtime
- `LinearFrequency : float`, `LinearDamping : float`, `AngularFrequency : float`, `WithLinearFrequency( float v ) → FixedJointBuilder`, `WithLinearDamping( float v ) → FixedJointBuilder`

### FloatSpan (struct)
Provides vectorized operations over a span of floats
- `Max(  ) → float`, `Min(  ) → float`, `Average(  ) → float`, `Sum(  ) → float`

### Friend (struct)
- `IsMe : bool`, `Id : UInt64`, `Name : string`, `OpenInOverlay(  ) → void`, `OpenAddFriendOverlay(  ) → void`

### Frustum (struct)
Represents a frustum
- `GetBBox(  ) → BBox`, `GetCorner( int i ) → Vector3?`, `IsInside( Vector3 point ) → bool`, `static FromCorners( Ray tl, Ray tr, Ray br, Ray bl, float near, float far ) → Frustum`

### GameObjectSystem (class)
Allows creation of a system that always exists in every scene, is hooked into the scene's lifecycle, and is disposed wh…
- `Scene : Scene`, `Id : Guid`, `Dispose(  ) → void`

### GameObjectSystem<T> (class)
A syntax sugar wrapper around GameObjectSystem, which allows you to access your system using SystemName
- `Current : T`, `static Get( Scene scene ) → T`

### GamepadCode (enum)
Game controller codes, driven from SDL
Values: None, A, B, X, Y, SwitchLeftMenu, Guide, SwitchRightMenu, LeftJoystickButton, RightJoystickButton … (+15 more)

### GameResource (class)
The base for assets defined in C# and created through tools: derive from it and tag the
class with `[AssetType]`. See `15_API_CORE.md` → *GameResource & `[AssetType]`*
- `HasUnsavedChanges : bool`, `ResourceVersion : int`, `IsValid : bool`, `StateHasChanged(  ) → void`, `GetReferencedPackages(  ) → IEnumerable<string>`
- From `Resource`: `ResourcePath : string` (persist this), `ResourceName : string`, `ResourceId : int` (**obsolete**)

### GameResourceAttribute (class)
**`[Obsolete( "Use AssetType instead" )]`**: using it fails the build outright under
`TreatWarningsAsErrors`. It subclasses `AssetTypeAttribute`, and its `Icon` /
`IconBgColor` / `IconFgColor` / `CanEmbed` / `Description` members went away with nothing
to replace them but `AssetTypeAttribute.IconColor` and `AssetTypeFlags.NoEmbedding`

### GameTask (class)
A generic `TaskSource`
- `static Yield(  ) → Task`, `static MainThread(  ) → Tasks.SyncTask`, `static WorkerThread(  ) → Tasks.SyncTask`, `static Delay( int ms ) → Task`, `CompletedTask : Task`

### Gizmo (class)
- `Control : Gizmo.GizmoControls`, `Draw : Gizmo.GizmoDraw`, `CursorPosition : Vector2`, `static Scope( string path ) → IDisposable`, `static Snap( Rotation rotationDelta ) → Rotation`

### Global (class)
Utility info for tools usage
- `InGame : bool`, `MapName : string`, `GameIdent : string`

### GlyphStyle (struct)
- `WithNeutralColorABXY(  ) → GlyphStyle`, `WithSolidABXY(  ) → GlyphStyle`

### GpuBuffer (class)
A GPU data buffer intended for use with a `ComputeShader`
- `Dispose(  ) → void`, `Clear( uint value ) → void`, `SetCounterValue( uint counterValue ) → void`, `CopyStructureCount( GpuBuffer destBuffer, int destBufferOffset ) → void`, `ElementCount : int`

### GpuBuffer<T> (class)
A typed GpuBuffer
- `GetData( Span<T> data ) → void`, `GetDataAsync( Action<ReadOnlySpan<T>> callback ) → void`, `SetData( Span<T> data, int elementOffset ) → void`, `GetData( Span<T> data, int start, int count ) → void`

### Gradient (struct)
Describes a gradient between multiple colors
- `Blending : Gradient.BlendMode`, `Colors : Collections.Immutable.ImmutableList<Gradient.ColorFrame>`, `Alphas : Collections.Immutable.ImmutableList<Gradient.AlphaFrame>`, `static FromColors( Color[] colors ) → Gradient`, `FixOrder(  ) → void`

### Graphics (class)
Used to render to the screen using your Graphics Card, or whatever you kids are using in your crazy future computers
- `static FlushGPU(  ) → void`, `static UavBarrier( Texture texture ) → void`, `static UavBarrier( GpuBuffer buffer ) → void`, `static SetupLighting( SceneObject obj, RenderAttributes targetAttributes ) → void`, `IsActive : bool`

### HapticEffect (class)
Contains a haptic effect, which consists of patterns for the controller and triggers
- `SoftImpact : HapticEffect`, `HardImpact : HapticEffect`, `Rumble : HapticEffect`

### HapticPattern (class)
Contains a haptic pattern, which consists of frequency and amplitude values that can change over time
- `SoftImpact : HapticPattern`, `HardImpact : HapticPattern`, `Rumble : HapticPattern`, `GetValue( float t, float frequency, float amplitude ) → void`

### HapticTarget (enum)
Places you can trigger haptics on
Values: Controller, LeftTrigger, RightTrigger

### HingeJointBuilder (class)
Provides ability to generate a hinge joint for a `Model` at runtime
- `EnableTwistLimit : bool`, `TwistLimit : Vector2`, `EnableMotor : bool`, `WithTargetVelocity( Vector3 v ) → HingeJointBuilder`, `WithMaxTorque( float v ) → HingeJointBuilder`

### Hitbox (class)
- `GameObject : GameObject`, `Bone : BoneCollection.Bone`, `Tags : ITagSet`, `Dispose(  ) → void`

### HitboxSet (class)
A set of hitboxes on a model
- `All : IReadOnlyList<HitboxSet.Box>`

### IByteParsable (interface)
- `static ReadObject( ByteStream stream, IByteParsable.ByteParseOptions o ) → object`, `static WriteObject( ByteStream stream, object value, IByteParsable.ByteParseOptions o ) → void`

### IByteParsable<T> (interface)
- `static Read( ByteStream stream, IByteParsable.ByteParseOptions o ) → T`, `static Write( ByteStream stream, T value, IByteParsable.ByteParseOptions o ) → void`

### ICompileReferenceProvider (interface)
Allows you to look up references for a compiler
- `Lookup( string reference ) → Microsoft.CodeAnalysis.PortableExecutableReference`

### IComponentLister (interface)
Interface for types that reference a `ComponentList`, to provide convenience method for accessing that list
- `Create( bool startEnabled ) → T`, `Get( FindMode search ) → T`, `GetAll( FindMode search ) → IEnumerable<T>`, `GetOrCreate( FindMode flags ) → T`, `Components : ComponentList`

### IDynamicFloatContext (interface)
- `LifetimeDelta : float`, `RandomSeed : int`

### IGameInstance (interface)
Todo: make internal - the only thing using ir right now is the binds system
- `IsLoading : bool`, `Current : IGameInstance`, `Scene : Scene`, `ResetBinds(  ) → void`, `SaveBinds(  ) → void`

### IGameObjectNetworkEvents (interface)
Allows listening to network events on a specific GameObject
- `StartControl(  ) → void`, `StopControl(  ) → void`, `NetworkOwnerChanged( Connection newOwner, Connection previousOwner ) → void`

### IHotloadManaged (interface)
During hotloads, instances of types implementing this interface will be notified when they get replaced
- `Persisted(  ) → void`, `Failed(  ) → void`, `Destroyed( Dictionary<string,object> state ) → void`, `Created( IReadOnlyDictionary<string,object> state ) → void`

### IJsonConvert (interface)
Allows writing JsonConverter in a more compact way, without having to pre-register them
- `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`

### IJsonPopulator (interface)
Objects that need to be deserialized into can implement this interface which allows them to be populated from a JSON ob…
- `Serialize(  ) → Text.Json.Nodes.JsonNode`, `Deserialize( Text.Json.Nodes.JsonNode node ) → void`

### ImageFormat (enum)
Format used when creating textures
Values: None, Default, RGBA8888, ABGR8888, RGB888, BGR888, RGB565, I8, IA88, A8 … (+62 more)

### IMemberAttribute (interface)
When applied to an attribute, which is them applied to a member
- `MemberDescription : MemberDescription`

### InputAction (class)
An input action defined by a game project
- `Name : string`, `GroupName : string`, `Title : string`

### InputGlyphSize (enum)
Values: Small, Medium, Large

### InputMotionData (struct)
Represents the current state of a device's motion sensor(s)

### InputSettings (class)
A class that holds all configured input settings for a game
- `Actions : List<InputAction>`, `InitDefault(  ) → void`

### ISceneCollisionEvents (interface)
Listen to all collision events that happen during a physics step
- `OnCollisionStart( Collision collision ) → void`, `OnCollisionUpdate( Collision collision ) → void`, `OnCollisionStop( CollisionStop collision ) → void`, `OnCollisionHit( Collision collision ) → void`

### ISceneEvent<T> (interface)
A wrapper for scene event interfaces
- `static Post( Action<T> action ) → void`, `static PostToGameObject( GameObject go, Action<T> action, FindMode find ) → void`

### ISceneLoadingEvents (interface)
Allows listening to events related to scene loading
- `AfterLoad( Scene scene ) → void`, `BeforeLoad( Scene scene, SceneLoadOptions options ) → void`, `OnLoad( Scene scene, SceneLoadOptions options ) → Task`, `OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context ) → Task`

### ISceneMetadata (interface)
Allows components to add metadata to the scene/prefab file, which is accessible before loading it
- `GetMetadata(  ) → Dictionary<string,string>`

### IScenePhysicsEvents (interface)
Allows events before and after the the physics step
- `PrePhysicsStep(  ) → void`, `PostPhysicsStep(  ) → void`, `OnOutOfBounds( Rigidbody body ) → void`, `OnFellAsleep( Rigidbody body ) → void`

### ISceneStartup (interface)
Allows listening to events related to scene startup
- `OnHostInitialize(  ) → void`, `OnClientInitialize(  ) → void`, `OnHostPreInitialize( SceneFile scene ) → void`

### ISpriteRenderGroup (interface)
Base interface for components that can be grouped for sprite rendering
- `Opaque : bool`, `Additive : bool`, `Shadows : bool`

### ITypeAttribute (interface)
When applied to an attribute, which is then applied to a type
- `TargetType : Type`, `TypeRegister(  ) → void`, `TypeUnregister(  ) → void`

### IValid (interface)
Interface for objects that can become invalid over time, such as references to deleted game objects or disposed resourc…
- `IsValid : bool`

### JointMotion (enum)
Values: Free, Locked

### Json (class)
A convenience JSON helper that handles `Resource` types for you
- `static Deserialize( string source ) → T`, `static Deserialize( Text.Json.Utf8JsonReader reader ) → T`, `static Serialize( object source ) → string`, `static ParseToJsonObject( string json ) → Text.Json.Nodes.JsonObject`

### KeyboardModifiers (enum)
Values: None, Alt, Ctrl, Shift

### KeyStore (class)
Allows storing files by hashed keys, rather than by actual filename
- `static CreateGlobalCache(  ) → KeyStore`, `Get( string key ) → byte[]`, `Exists( string key ) → bool`, `Remove( string key ) → void`

### Language (class)
Allows access to translated phrases, allowing the translation of gamemodes etc
- `SelectedCode : string`, `Current : Localization.LanguageInformation`, `static GetPhrase( string textToken, Dictionary<string,object> data ) → string`

### LanguageContainer (class)
A container for the current language, allowing access to translated phrases and language information
- `SelectedCode : string`, `Current : Localization.LanguageInformation`, `GetPhrase( string textToken, Dictionary<string,object> data ) → string`

### LaunchArguments (class)
These are arguments that were set when launching the current game
- `Map : string`, `MaxPlayers : int`, `Privacy : Network.LobbyPrivacy`

### LoadingContext (class)
- `Title : string`, `IsCompleted : bool`

### LoadingScreen (class)
Holds metadata and raw data relating to a Saved Game
- `Title : string`, `Subtitle : string`, `Media : string`

### LogEvent (struct)
- `Level : LogLevel`, `Logger : string`, `Message : string`

### LogLevel (enum)
Values: Trace, Info, Warn, Error

### MainThread (class)
Utility functions that revolve around the main thread
- `static Wait(  ) → Tasks.SyncTask`, `static Queue( Action method ) → void`

### ManifestSchema (class)
An addon's manifest, describing what files are available
- `Schema : int`, `Files : ManifestSchema.File[]`

### Map (class)
- `PhysicsGroup : PhysicsGroup`, `SceneMap : SceneMap`, `static CreateAsync( string mapName, MapLoader loader, Threading.CancellationToken cancelToken ) → Task<Map>`, `Delete(  ) → void`

### MapLoader (class)
- `World : SceneWorld`, `PhysicsWorld : PhysicsWorld`, `WorldOrigin : Vector3`

### MaterialGroupBuilder (class)
- `AddMaterial( Material material ) → MaterialGroupBuilder`, `WithName( string name ) → MaterialGroupBuilder`, `AddMaterials( Span<Material> materials ) → MaterialGroupBuilder`, `Name : string`

### MemberDescription (class)
Wraps MemberInfo but with caching and sandboxing
- `TypeDescription : TypeDescription`, `DeclaringType : TypeDescription`, `Name : string`, `GetDisplayInfo(  ) → DisplayInfo`, `HasAttribute(  ) → bool`

### Mesh (class)
A mesh is a basic version of a `Model`, containing a set of vertices and indices which make up faces that make up a sha…
- `CreateIndexBuffer(  ) → void`, `SetIndexBufferSize( int elementCount ) → void`, `LockIndexBuffer( Mesh.IndexBufferLockHandler handler ) → void`, `CreateVertexBuffer( VertexAttribute[] layout ) → void`, `PrimitiveType : MeshPrimitiveType`

### MeshPrimitiveType (enum)
Possible primitive types of a `Mesh`
Values: Points, Lines, LineStrip, Triangles, TriangleStrip

### Metadata (class)
A simple class for storing and retrieving metadata values
- `SetValue( string key, object value ) → void`, `TryGetValue( string key, T outValue ) → bool`, `GetValueOrDefault( string key, T defaultValue ) → T`

### MethodDescription (class)
Describes a method
- `IsMethod : bool`, `ReturnType : Type`, `Parameters : Reflection.ParameterInfo[]& modreq(Runtime.InteropServices.InAttribute)`, `CreateDelegate(  ) → T`, `CreateDelegate( object target ) → T`

### ModelArchetype (enum)
Default model archetypes
Values: static_prop_model, animated_model, physics_prop_model, jointed_physics_model, breakable_prop_model, generic_actor_model

### ModelAttachments (class)
- `Model : Model`, `Count : int`, `All : IReadOnlyList<ModelAttachments.Attachment>`, `Get( string name ) → ModelAttachments.Attachment`, `GetTransform( string name ) → Transform?`

### ModelBuilder (class)
Provides ability to generate `Model`s at runtime
- `Create(  ) → Model`, `WithMass( float mass ) → ModelBuilder`, `WithSurface( string name ) → ModelBuilder`, `AddMesh( Mesh mesh ) → ModelBuilder`

### ModelMorphs (class)
Allows fast lookups of morph variables
- `Model : Model`, `Count : int`, `Names : string[]`, `GetName( int i ) → string`, `GetIndex( string name ) → int`

### ModelParts (class)
- `Count : int`, `DefaultMask : UInt64`, `All : IReadOnlyList<Model.BodyPart>`, `Get( string name ) → Model.BodyPart`

### MorphCollection (class)
Used to access and manipulate morphs
- `ResetAll(  ) → void`, `ResetAll( float fadeTime ) → void`, `Reset( int i ) → void`, `Reset( string name ) → void`, `Count : int`

### MouseButtons (enum)
State of mouse buttons being pressed or not
Values: None, Left, Right, Middle, Back, Forward

### MouseVisibility (enum)
The visibility state of the mouse cursor
Values: Visible, Auto, Hidden

### MultisampleAmount (enum)
Values: Multisample2x, Multisample4x, Multisample6x, Multisample8x, Multisample16x, MultisampleScreen, MultisampleNone

### MultiSerializedObject (class)
An object (or data) that can be accessed as an object
- `IsMultipleTargets : bool`, `Targets : IEnumerable<object>`, `TypeIcon : string`, `Rebuild(  ) → void`, `Add( SerializedObject obj ) → void`

### MusicPlayer (class)
Enables music playback
- `SampleRate : int`, `Channels : int`, `Duration : float`, `static PlayUrl( string url ) → MusicPlayer`, `static Play( BaseFileSystem filesystem, string path ) → MusicPlayer`

### NetDictionary<TKey,TValue> (class)
A networkable dictionary for use with the `SyncAttribute` and `HostSyncAttribute`
- `Clear(  ) → void`, `Dispose(  ) → void`, `Add( Collections.Generic.KeyValuePair<TKey,TValue> item ) → void`, `ContainsKey( TKey key ) → bool`, `Count : int`

### NetDictionaryChangeEvent<TKey,TValue> (struct)
Describes a change to a `NetDictionary`2` which is passed to `OnChanged` whenever its contents change
- `Type : Collections.Specialized.NotifyCollectionChangedAction`, `Key : TKey`, `NewValue : TValue`

### NetList<T> (class)
A list built to network over `SyncAttribute` and `HostSyncAttribute`: it ships deltas
across the wire, not the whole collection each time
- `Clear(  ) → void`, `RemoveAt( int index ) → void`, `Dispose(  ) → void`, `Contains( T item ) → bool`, `Count : int`
- `OnChanged : Action<NetListChangeEvent<T>>` looks like an event but is a **public field**; subscribe with `+=`. `[Change]` won't fire on a `NetList` property either, since it wraps the setter and only reacts to reassignment
- A non-controller's mutations are silent no-ops. Set it once with `= new()` and don't reassign it after spawn. See `04_NETWORKING.md` → *Networked Collections*

### NetListChangeEvent<T> (struct)
Describes a change to a `NetListChangeEvent`1` which is passed to `OnChanged` whenever its contents change
- `Type : Collections.Specialized.NotifyCollectionChangedAction`, `Index : int`, `MovedIndex : int`

### NetPermission (enum)
Specifies who can invoke an action over the network
Values: Anyone, HostOnly, OwnerOnly

### NetworkFlags (enum)
Describes the behavior of network objects
Values: None, NoInterpolation, NoPositionSync, NoRotationSync, NoScaleSync, NoTransformSync

### NetworkingSettings (class)
A class that holds all configured networking settings for a game
- `DestroyLobbyWhenHostLeaves : bool`, `AutoSwitchToBestHost : bool`, `ClientsCanSpawnObjects : bool`

### NetworkSpawnOptions (struct)
Configurable options when spawning a networked object
- `OrphanedMode : NetworkOrphaned?`, `OwnerTransfer : OwnerTransfer?`, `Flags : NetworkFlags?`

### Package (class)
Represents an asset on Asset Party
- `IsRemote : bool`, `Org : Package.Organization`, `FullIdent : string`, `static GetCachedTitle( string ident ) → string`, `static FetchAsync( string identString, bool partial ) → Task<Package>`

### Particle (class)
- `Get( string key ) → T`, `RemoveListener( Particle.BaseListener i ) → void`, `Set( string key, T tvalue ) → void`, `AddListener( Particle.BaseListener i, Component sourceComponent ) → void`, `LifeTimeRemaining : float`

### ParticleControlPoint (struct)
- `Value : ParticleControlPoint.ControlPointValueInput`, `StringCP : string`, `VectorValue : Vector3`, `OutputValue(  ) → object`

### ParticleFloat (struct)
Represents a floating-point value that can change over time with support for various evaluation modes
- `Type : ParticleFloat.ValueType`, `Evaluation : ParticleFloat.EvaluationType`, `CurveA : Curve`, `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`

### ParticleGradient (struct)
- `Type : ParticleGradient.ValueType`, `Evaluation : ParticleGradient.EvaluationType`, `GradientA : Gradient`, `Evaluate( float delta, float randomFixed ) → Color`, `Evaluate( Particle p, int seed, int line ) → Color`

### ParticleSnapshot (class)
A particle snapshot that can be created procedurally
- `IsValid : bool`, `Update( Span<ParticleSnapshot.Vertex> vertices ) → void`

### ParticleSystem (class)
A particle effect system that allows for complex visual effects, such as explosions, muzzle flashes, impact effects, etc
- `IsError : bool`, `Name : string`, `Bounds : BBox`, `static Load( string filename ) → ParticleSystem`, `static LoadAsync( string filename ) → Task<ParticleSystem>`

### ParticleVector3 (struct)
- `IsNearlyZero(  ) → bool`, `Evaluate( Particle p, int seed, int line ) → Vector3`, `Evaluate( float delta, float a, float b, float c ) → Vector3`

### PartyRoom (class)
A Party
- `Id : SteamId`, `VoiceCommunicationAllowed : bool`, `PackageIdent : string`, `SetOwner( SteamId friend ) → bool`, `static Find(  ) → Task<PartyRoom.Entry[]>`

### PhysicsBodyBuilder (class)
Provides ability to generate a physics body for a `Model` at runtime
- `Mass : float`, `Surface : Surface`, `BindPose : Transform`, `SetMass( float mass ) → PhysicsBodyBuilder`, `SetSurface( Surface surface ) → PhysicsBodyBuilder`

### PhysicsBodyType (enum)
Values: Static, Keyframed, Dynamic

### PhysicsGroup (class)
Represents a set of PhysicsBody objects
- `World : PhysicsWorld`, `Pos : Vector3`, `MassCenter : Vector3`, `RebuildMass(  ) → void`, `Remove(  ) → void`

### PhysicsGroupDescription (class)
- `Surfaces : IEnumerable<Surface>`, `BoneCount : int`, `Parts : IReadOnlyList<PhysicsGroupDescription.BodyPart>`

### PhysicsIntersection (struct)
- `Self : PhysicsContact.Target`, `Other : PhysicsContact.Target`, `Contact : PhysicsContact`

### PhysicsIntersectionEnd (struct)
- `Self : PhysicsContact.Target`, `Other : PhysicsContact.Target`

### PhysicsJointBuilder (class)
Provides ability to generate a physics joint for a `Model` at runtime
- `Body1 : int`, `Body2 : int`, `Frame1 : Transform`

### PhysicsJointBuilderExtensions (class)
- `static WithBody1( T b, int v ) → T`, `static WithBody2( T b, int v ) → T`, `static WithFrame1( T b, Transform v ) → T`, `static WithFrame2( T b, Transform v ) → T`

### PhysicsLock (struct)
- `X : bool`, `Y : bool`, `Z : bool`

### PhysicsMotionType (enum)
Represents Physics body's motion type
Values: Invalid, Dynamic, Static, Keyframed

### PhysicsSimulationMode (enum)
Physics simulation mode
Values: Discrete, Continuous

### PhysicsTraceBuilder (struct)
- `HitTriggers(  ) → PhysicsTraceBuilder`, `HitTriggersOnly(  ) → PhysicsTraceBuilder`, `IgnoreStatic(  ) → PhysicsTraceBuilder`, `IgnoreDynamic(  ) → PhysicsTraceBuilder`

### PhysicsTraceResult (struct)
- `Bone : int`, `Distance : float`

### Plane (struct)
Represents a plane
- `static GetIntersection( Plane vp1, Plane vp2, Plane vp3 ) → Vector3?`, `GetDistance( Vector3 point ) → float`, `IsInFront( Vector3 point ) → bool`, `SnapToPlane( Vector3 point ) → Vector3`, `Origin : Vector3`

### PolygonMesh (class)
An editable mesh made up of polygons, triangulated into a model
- `CalculateBounds(  ) → BBox`, `GetVertexPositions(  ) → IEnumerable<Vector3>`, `GetEdges(  ) → IEnumerable<Line>`, `Rebuild(  ) → Model`, `IsDirty : bool`

### PostProcessSystem (class)
Manages post-processing effects for cameras and volumes within a scene, handling their application during rendering and…

### PrefabFile (class)
A GameObject which is saved to a file
- `RootObject : Text.Json.Nodes.JsonObject`, `ShowInMenu : bool`, `MenuPath : string`, `static Load( string path ) → PrefabFile`, `GetScene(  ) → PrefabScene`

### PrefabScene (class)
- `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`, `ToPrefabFile(  ) → PrefabFile`, `Load( GameResource resource ) → bool`, `Serialize( GameObject.SerializeOptions options ) → Text.Json.Nodes.JsonObject`, `Variables : PrefabScene.VariableCollection`

### PrefabVariable (class)
A prefab variable definition
- `Id : string`, `Title : string`, `Description : string`, `AddTarget( Guid id, string propertyName ) → void`

### Preferences (class)
Holds information about the current user's preferences
- `FieldOfView : float`, `MusicVolume : float`, `VoipVolume : float`

### Project (class)
Represents an on-disk project
- `HasCompiler : bool`, `ConfigFilePath : string`, `RootDirectory : IO.DirectoryInfo`, `GetRootPath(  ) → string`, `GetProjectPath(  ) → string`

### ProjectSettings (class)
- `Collision : Physics.CollisionRules`, `Input : InputSettings`, `Networking : NetworkingSettings`, `static Get( string filename ) → T`

### PropertyAccessor (enum)
Values: Get, Set

### PropertyDescription (class)
Describes a property
- `CanWrite : bool`, `CanRead : bool`, `IsGetMethodPublic : bool`, `GetValue( object obj ) → object`, `SetValue( object obj, object value ) → void`

### RayTracingAccelerationStructure (class)
Represents a ray tracing acceleration structure that contains geometry for efficient ray intersection testing
- `static Create( object geometryData ) → RayTracingAccelerationStructure`, `IsValid(  ) → bool`, `Dispose(  ) → void`, `Update( object geometryData ) → void`

### RealTime (class)
Access to time
- `Now : float`, `NowDouble : double`, `GlobalNow : double`

### RealTimeSince (struct)
A convenience struct to easily measure time since an event last happened, based on `GlobalNow`
- `Absolute : double`, `Relative : float`

### RealTimeUntil (struct)
A convenience struct to easily manage a time countdown, based on `GlobalNow`
- `Absolute : double`, `Relative : double`, `Passed : double`

### Rect (struct)
Represents a rectangle
- `Width : float`, `Height : float`, `Left : float`, `static FromPoints( Vector2 a, Vector2 b ) → Rect`, `Floor(  ) → Rect`

### RectInt (struct)
Represents a rectangle but with whole numbers
- `Width : int`, `Height : int`, `Left : int`, `static FromPoints( Vector2Int a, Vector2Int b ) → RectInt`, `IsInside( Vector2Int pos ) → bool`

### RenderAttributes (class)
RenderAttributes are a set of values that are passed to the renderer
- `SetData( StringToken k, T value ) → void`, `SetData( StringToken k, T[] value ) → void`, `GetBool( StringToken name, bool defaultValue ) → bool`, `GetVector( StringToken name, Vector3 defaultValue ) → Vector3`

### RenderOptions (class)
- `Game : bool`, `Overlay : bool`, `Bloom : bool`, `Apply( SceneObject obj ) → void`

### RenderTarget (class)
Essentially wraps a couple of textures that we're going to render to
- `Width : int`, `Height : int`, `ColorTarget : Texture`, `static From( Texture color, Texture depth ) → RenderTarget`, `Dispose(  ) → void`

### RenderTextureAsset (class)
Asset that owns a GPU render target texture which can be shared across runtime systems
- `Size : Vector2Int`, `Format : ImageFormat`, `ClearColor : Color`

### Resource (class)
A resource loaded in the engine, such as a `Model` or `Material`
- `ResourceId : int`, `ResourcePath : string`, `ResourceName : string`, `StateHasChanged(  ) → void`, `ConfigurePublishing( ResourcePublishContext context ) → void`

### ResourceExtension<T,TSelf> (class)
An extension of ResourceExtension[t], this gives special helper methods for retrieving resources targetting specific as…
- `static FindDefault(  ) → TSelf`, `static FindForResource( Resource r ) → TSelf`, `static FindForResourceOrDefault( Resource r ) → TSelf`, `static FindAllForResource( Resource r ) → IEnumerable<TSelf>`

### ResourceExtension<T> (class)
A GameResource type that adds extended properties to another resource type
- `ExtensionDefault : bool`, `ExtensionTargets : List<T>`

### ResourceLibrary (class)
The registry holding every available `Resource`
- `static Get( string filepath ) → T`, `static TryGet( string filepath, out T resource ) → bool`, `static GetAll(  ) → IEnumerable<T>`, `static GetAll( string folder, bool recursive = true ) → IEnumerable<T>`, `static LoadAsync( string path ) → Task<T>`, `static GetThumbnail( string path, int width = 256, int height = 256 ) → Task<Bitmap>`
- `static Get( int identifier ) → T` is **obsolete**: identifier-based lookup is on its way out
- `ResourceLibrary.IEventListener`: `OnRegister`, `OnUnregister`, `OnSave`, `OnExternalChanges`, `OnExternalChangesPostLoad`

### ResourcePublishContext (class)
Created by the editor when publishing a resource, passed into Resource
- `PublishingEnabled : bool`, `ReasonForDisabling : string`, `IncludeCode : bool`, `SetPublishingDisabled( string reason ) → void`

### ResourceSystem (class)
- `GetAll(  ) → IEnumerable<T>`, `Get( int identifier ) → T`, `Get( string filepath ) → T`, `TryGet( string filepath, T resource ) → bool`

### RigidbodyFlags (enum)
Values: DisableCollisionSounds

### Rpc (class)
- `static PreCall(  ) → void`, `static FilterInclude( Connection connection ) → IDisposable`, `static FilterExclude( Connection connection ) → IDisposable`, `static OnCallRpc( WrappedMethod m, T[] argument ) → void`, `Caller : Connection`

### SceneAnimationSystem (class)

### SceneCamera (class)
Represents a camera and holds render hooks
- `Bloom : SceneCamera.BloomAccessor`, `Name : string`, `ExcludeTags : ITagSet`, `GetFrustum( Rect pixelRect ) → Frustum`, `GetRay( Vector3 cursorPosition ) → Ray`

### SceneCameraDebugMode (enum)
Values: Normal, FullBright, NormalMap, Albedo, Roughness, Diffuse, Reflect, Transmission, ShowUV, ShaderIDColor … (+4 more)

### SceneCubemap (class)
- `Priority : int`, `Projection : SceneCubemap.ProjectionMode`, `TintColor : Color`, `RenderDirty(  ) → void`

### SceneCullingBox (class)
A box which can be used to explicitly control scene visibility
- `IsValid : bool`, `World : SceneWorld`, `Transform : Transform`, `Delete(  ) → void`

### SceneCustomObject (class)
A scene object that allows custom rendering within a scene world
- `RenderSceneObject(  ) → void`

### SceneDirectionalLight (class)
A directional scene light that is used to mimic sun light in a `SceneWorld`
- `SkyColor : Color`, `ShadowCascadeCount : int`, `ShadowCascadeSplitRatio : float`, `SetCascadeDistanceScale( float distance ) → void`

### SceneDynamicObject (class)
- `Clear(  ) → void`, `AddVertex( Vertex v ) → void`, `AddVertex( Span<Vertex> v ) → void`, `Init( Graphics.PrimitiveType type ) → void`, `Material : Material`

### SceneExtensions (class)
- `static CopyToClipboard( Component component ) → void`, `static PasteValues( Component target ) → void`, `static ShouldShowInHierarchy( GameObject target ) → bool`, `static PasteComponent( GameObject target ) → void`

### SceneFile (class)
A scene file contains a collection of GameObject with Components and their properties
- `Id : Guid`, `GameObjects : Text.Json.Nodes.JsonObject[]`, `SceneProperties : Text.Json.Nodes.JsonObject`, `GetMetadata( string title, string defaultValue ) → string`

### SceneFogVolume (class)
Represents a volume of fog in a scene, contributing to volumetric fog effects set on `VolumetricFog`
- `Transform : Transform`, `BoundingBox : BBox`, `FogStrength : float`, `Delete(  ) → void`

### SceneLayerType (enum)
Values: Unknown, Translucent, Shadow, EffectsTranslucent, EffectsOpaque, DepthPrepass, Opaque

### SceneLight (class)
Base class for light scene objects for use with a `SceneWorld`
- `LightColor : Color`, `Radius : float`, `ConstantAttenuation : float`

### SceneLineObject (class)
A scene object which is used to draw lines
- `TessellationLevel : int`, `LineTexture : Texture`, `StartCap : SceneLineObject.CapStyle`, `StartLine(  ) → void`, `EndLine(  ) → void`

### SceneMap (class)
Map geometry that can be rendered within a `SceneWorld`
- `World : SceneWorld`, `IsValid : bool`, `Bounds : BBox`, `static CreateAsync( SceneWorld sceneWorld, string map, Threading.CancellationToken cancelToken ) → Task<SceneMap>`, `Delete(  ) → void`

### SceneMapLoader (class)

### SceneModel (class)
A model scene object that supports animations and can be rendered within a `SceneWorld`
- `ClearBoneOverrides(  ) → void`, `HasBoneOverrides(  ) → bool`, `ResetAnimParameters(  ) → void`, `UpdateToBindPose(  ) → void`, `UseAnimGraph : bool`

### SceneNetworkSystem (class)
This is created and referenced by the network system, as a way to route
- `SetSnapshotAsync( Network.SnapshotMsg msg ) → Task`, `GetMountedVPKs( Connection source, Network.MountedVPKsResponse msg ) → void`, `MountVPKs( Connection source, Network.MountedVPKsResponse msg ) → Task`, `GetSnapshot( Connection source, Network.SnapshotMsg msg ) → void`

### SceneObject (class)
A model scene object that can be rendered within a `SceneWorld`
- `World : SceneWorld`, `Transform : Transform`, `Rotation : Rotation`, `Delete(  ) → void`, `ClearMaterialOverride(  ) → void`

### SceneParticles (class)
A SceneObject used to render particles
- `RenderParticles : bool`, `EmissionStopped : bool`, `PhysicsWorld : PhysicsWorld`, `IsControlPointSet( int index ) → bool`, `GetControlPointPosition( int index ) → Vector3`

### ScenePointLight (class)
A point light scene object for use in a `SceneWorld`

### SceneRenderLayer (enum)
SceneObjects can be rendered on layers other than the main game layer
Values: Default, ViewModel, OverlayWithDepth, OverlayWithoutDepth

### SceneSkyBox (class)
Renders a skybox within a `SceneWorld`
- `SkyMaterial : Material`, `SkyTint : Color`, `FogParams : SceneSkyBox.FogParamInfo`, `SetSkyLighting( Vector3 ConstantSkyLight ) → void`

### SceneSpotLight (class)
A simple spot light scene object for use in a `SceneWorld`
- `ConeInner : float`, `ConeOuter : float`, `FallOff : float`

### SceneSpriteSystem (class)
- `Dispose(  ) → void`

### SceneUtility (class)
- `static Instantiate( GameObject template ) → GameObject`, `static GetPrefabScene( PrefabFile prefabFile ) → PrefabScene`, `static RunInBatchGroup( Action action ) → void`, `static Instantiate( GameObject template, Transform transform ) → GameObject`

### SceneWorld (class)
A scene world that contains `SceneObject`s
- `Trace : Engine.Utility.RayTrace.MeshTraceRequest`, `SceneObjects : Collections.Generic.IReadOnlyCollection<SceneObject>`, `Delete(  ) → void`, `DeletePendingObjects(  ) → void`

### SelectionSystem (class)
An ordered collection of unique objects with add/remove callbacks
- `Count : int`, `OnItemAdded : Action<object>`, `OnItemRemoved : Action<object>`, `Clear(  ) → void`, `Any(  ) → bool`

### SerializedCollection (class)
- `KeyType : Type`, `ValueType : Type`, `TargetObject : object`, `NewKeyProperty(  ) → SerializedProperty`, `Remove( SerializedProperty property ) → bool`

### SerializedObject (class)
An object (or data) that can be accessed as an object
- `IsValid : bool`, `IsMultipleTargets : bool`, `Targets : IEnumerable<object>`, `NoteChanged( SerializedProperty childProperty ) → void`, `GetProperty( string v ) → SerializedProperty`

### SerializedProperty (class)
- `SourceFile : string`, `SourceLine : int`, `HasChanges : bool`, `GetDefault(  ) → object`, `HasAttribute(  ) → bool`

### Shader (class)
A shader is a specialized and complex computer program that use world geometry, materials and textures to render graphi…
- `Schema : Shader.ShaderSchema`, `IsValid : bool`, `static Load( string filename ) → Shader`

### SimpleVertex (struct)

### SliderJointBuilder (class)
Provides ability to generate a slider joint for a `Model` at runtime
- `EnableLimit : bool`, `Limit : Vector2`, `WithLimit( float min, float max ) → SliderJointBuilder`

### SoundEvent (class)
A sound event
- `UI : bool`, `Volume : RangedFloat`, `Pitch : RangedFloat`

### SoundFile (class)
A sound resource
- `OnSoundReloaded : Action`, `IsLoaded : bool`, `Format : SoundFormat`, `static Load( string filename ) → SoundFile`, `GetSamplesAsync(  ) → Task<Int16[]>`

### SoundFormat (enum)
Values: PCM16, PCM8, MP3, ADPCM

### Soundscape (class)
A soundscape is used for environmental ambiance of a map by playing a set of random sounds at given intervals
- `MasterVolume : RangedFloat`, `LoopedSounds : List<Soundscape.LoopedSound>`, `StingSounds : List<Soundscape.StingSound>`

### SoundStream (class)
- `SampleRate : int`, `Channels : int`, `QueuedSampleCount : int`, `Close(  ) → void`, `Play( float volume, float pitch ) → SoundHandle`

### Sphere (struct)
Represents a sphere
- `Unit : Sphere`, `Volume : float`, `RandomPointInside : Vector3`, `GetVolume(  ) → float`, `Contains( Vector3 value ) → bool`

### Spline (class)
Collection of curves in 3D space
- `Clear(  ) → void`, `ConvertToPolyline(  ) → List<Vector3>`, `SampleAtDistance( float distance ) → Spline.Sample`, `SampleAtClosestPosition( Vector3 position ) → Spline.Sample`, `IsLoop : bool`

### Sprite (class)
Represents a sprite resource that can be static or animated
- `static FromTexture( Texture texture ) → Sprite`, `GetAnimationIndex( string name ) → int`, `GetAnimation( int index ) → Sprite.Animation`, `GetAnimation( string name ) → Sprite.Animation`, `Animations : List<Sprite.Animation>`

### Standalone (class)
- `BuildDate : DateTime`, `IsDevelopmentBuild : bool`, `VersionDate : DateTime`

### StandaloneManifest (class)
- `Name : string`, `Ident : string`, `ExecutableName : string`

### SteamId (struct)
Represents a Steam ID (64-bit unique identifier for Steam accounts)
- `Value : long`, `ValueUnsigned : UInt64`, `AccountType : SteamId.AccountTypes`

### StereoTargetEye (enum)
Values: None, LeftEye, RightEye, Both

### Storage (class)
- `static CreateEntry( string type ) → Storage.Entry`, `static GetAll( string type ) → Storage.Entry[]`

### StreamChannel (struct)
- `UserId : string`, `Username : string`, `DisplayName : string`

### StreamChatMessage (struct)
- `Channel : string`, `DisplayName : string`, `Message : string`

### StreamClip (struct)
- `EditUrl : string`, `Id : string`

### Streamer (class)
- `Username : string`, `UserId : string`, `Service : StreamService`, `static ClearChat(  ) → void`, `static GetUser( string username ) → Task<StreamUser>`

### StreamPoll (struct)
- `Id : string`, `BroadcasterId : string`, `BroadcasterName : string`, `End( bool archive ) → Task<StreamPoll>`

### StreamPrediction (struct)
- `Id : string`, `BroadcasterId : string`, `BroadcasterLogin : string`, `Lock(  ) → Task<StreamPrediction>`, `Cancel(  ) → Task<StreamPrediction>`

### StreamService (enum)
Streamer integration services
Values: None, Twitch

### StreamUser (struct)
- `Following : Task<List<StreamUserFollow>>`, `Followers : Task<List<StreamUserFollow>>`, `Id : string`, `Unban(  ) → void`, `CreateClip( bool hasDelay ) → Task<StreamClip>`

### StreamUserFollow (struct)
- `UserId : string`, `Username : string`, `DisplayName : string`

### StringToken (struct)
Strings are commonly converted to tokens in engine, to save space and speed up things like comparisons
- `static Literal( string value, uint token ) → StringToken`

### SystemsConfig (class)
Configuration for GameObjectSystem properties at a project level
- `GetPropertyValue( TypeDescription systemType, PropertyDescription property ) → object`, `TryGetPropertyValue( TypeDescription systemType, PropertyDescription property, object value ) → bool`, `SetPropertyValue( TypeDescription systemType, PropertyDescription property, object value ) → void`

### TaskSource (struct)
Provides a way for us to cancel tasks after common async shit is executed
- `static CreateLinkedTokenSource(  ) → Threading.CancellationTokenSource`, `MainThread(  ) → Tasks.SyncTask`, `WorkerThread(  ) → Tasks.SyncTask`, `Frame(  ) → Task`, `IsValid : bool`

### TerrainFlags (enum)
Values: None, NoTile

### TerrainMaterial (class)
Description of a Terrain Material
- `AlbedoImage : string`, `RoughnessImage : string`, `NormalImage : string`

### TerrainStorage (class)
Stores heightmaps, control maps and materials
- `TerrainSize : float`, `TerrainHeight : float`, `HeightMap : UInt16[]`, `SetResolution( int resolution ) → void`

### TextFlag (enum)
Flags dictating position of text (and other elements)
Values: None, Left, Right, CenterHorizontally, Justify, Absolute, Top, Bottom, CenterVertically, LeftTop … (+12 more)

### TextRendering (class)
- `static GetOrCreateTexture( TextRendering.Scope scope, Vector2 clip, TextFlag flag ) → Texture`

### Texture (class)
A texture is an image used in rendering
- `static CreateCustom(  ) → TextureBuilder`, `static CreateRenderTarget(  ) → TextureBuilder`, `static Find( string filepath ) → Texture`, `static Load( string path_or_url, bool warnOnMissing ) → Texture`, `IsError : bool`

### Texture2DBuilder (struct)
- `Finish(  ) → Texture`, `WithName( string name ) → Texture2DBuilder`, `WithMultisample( MultisampleAmount amount ) → Texture2DBuilder`, `WithAnonymous( bool isAnonymous ) → Texture2DBuilder`

### Texture3DBuilder (struct)
- `Finish(  ) → Texture`, `WithName( string name ) → Texture3DBuilder`, `WithData( byte[] data ) → Texture3DBuilder`, `WithMultisample( MultisampleAmount amount ) → Texture3DBuilder`

### TextureArrayBuilder (struct)
- `Finish(  ) → Texture`, `WithName( string name ) → TextureArrayBuilder`, `WithData( byte[] data ) → TextureArrayBuilder`, `WithMultisample( MultisampleAmount amount ) → TextureArrayBuilder`

### TextureBuilder (struct)
- `WithStaticUsage(  ) → TextureBuilder`, `WithSemiStaticUsage(  ) → TextureBuilder`, `WithDynamicUsage(  ) → TextureBuilder`, `WithGPUOnlyUsage(  ) → TextureBuilder`

### TextureCubeBuilder (struct)
- `Finish(  ) → Texture`, `WithName( string name ) → TextureCubeBuilder`, `WithData( byte[] data ) → TextureCubeBuilder`, `WithMultisample( MultisampleAmount amount ) → TextureCubeBuilder`

### TextureFlags (enum)
Flags providing hints about a texture
Values: None, PremultipliedAlpha

### ThreadSafe (class)
Provides utilities for working with threads, particularly for identifying and asserting code is running on the main thr…
- `CurrentThreadId : int`, `CurrentThreadName : string`, `IsMainThread : bool`, `static AssertIsNotMainThread(  ) → void`, `static AssertIsMainThread( string memberName ) → void`

### TrailTextureConfig (struct)
Defines how a trail is going to be textured
- `Clamp : bool`, `Default : TrailTextureConfig`, `Texture : Texture`, `DoesMaterialUseLineShader( Material value ) → bool`

### TransformProxy (class)
- `GetLocalTransform(  ) → Transform`, `GetWorldTransform(  ) → Transform`, `SetLocalTransform( Transform& modreq(Runtime.InteropServices.InAttribute) value ) → void`, `SetWorldTransform( Transform value ) → void`

### Triangle (struct)
- `Perimeter : float`, `Area : float`, `IsRight : bool`, `ClosestPoint( Vector3 P ) → Vector3`

### TypeDescription (class)
Describes a type
- `TargetType : Type`, `BaseType : TypeDescription`, `IsValid : bool`, `GetAttribute( bool inherited ) → T`, `GetAttributes( bool inherited ) → IEnumerable<T>`

### Variant (struct)
A Variant is a type that can hold any value, and also keeps track of the type of the value it holds
- `Type : Type`, `Value : object`, `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`, `Get(  ) → T`

### Vertex (struct)

### VertexAttribute (struct)

### VertexAttributeFormat (enum)
Values: Float32, Float16, SInt32, UInt32, SInt16, UInt16, SInt8, UInt8

### VertexAttributeType (enum)
Values: Position, Normal, Tangent, TexCoord, Color, BlendIndices, BlendWeights

### VertexBuffer (class)
- `Clear(  ) → void`, `Init( bool useIndexBuffer ) → void`, `Add( Vertex v ) → void`, `AddIndex( int i ) → void`, `Indexed : bool`

### VertexLayout (class)
Allows for the definition of custom vertex layouts

### VideoPlayer (class)
Enables video playback and access to the video texture and audio
- `OnLoaded : Action`, `OnAudioReady : Action`, `OnFinished : Action`, `Resume(  ) → void`, `Stop(  ) → void`

### VideoWriter (class)
Allows the creation of video content by encoding a sequence of frames
- `Width : int`, `Height : int`, `Dispose(  ) → void`, `FinishAsync(  ) → Task`, `AddFrame( ReadOnlySpan<byte> data, TimeSpan? timestamp ) → bool`

### VolumetricFogParameters (class)
- `Enabled : bool`, `Anisotropy : float`, `Scattering : float`

### WebSocket (class)
A WebSocket client for connecting to external services
- `Dispose(  ) → void`, `AddSubProtocol( string protocol ) → void`, `Send( string message ) → Threading.Tasks.ValueTask`, `Send( byte[] data ) → Threading.Tasks.ValueTask`, `IsConnected : bool`

### WebSurface (class)
Enables rendering and interacting with a webpage
- `OnTexture : WebSurface.TextureChangedDelegate`, `Url : string`, `Size : Vector2`, `TellMouseMove( Vector2 position ) → void`, `TellMouseWheel( int delta ) → void`

### WorkshopItemMetaData (struct)
Some metadata we'll pack into a workshop submission when publishing
- `Title : string`, `PackageIdent : string`, `WorkshopId : UInt64`

### WrappedMethod (struct)
Provides data about a wrapped method in a `CodeGeneratorAttribute` callback
- `Resume : Action`, `Object : object`, `IsStatic : bool`, `GetAttribute(  ) → U`

### WrappedMethod<T> (struct)
Provides data about a wrapped method in a `CodeGeneratorAttribute` callback
- `Object : object`, `IsStatic : bool`, `TypeName : string`, `GetAttribute(  ) → U`

### WrappedPropertyGet<T> (struct)
Provides data about a wrapped property getter in a `CodeGeneratorAttribute` callback
- `Value : T`, `Object : object`, `IsStatic : bool`, `GetAttribute(  ) → U`

### WrappedPropertySet<T> (struct)
Provides data about a wrapped property setter in a `CodeGeneratorAttribute` callback
- `Value : T`, `Object : object`, `IsStatic : bool`, `GetAttribute(  ) → U`

## (Global Namespace)

### AccountTypes (enum)
The different types of Steam accounts
Values: Invalid, Individual, Multiseat, GameServer, AnonGameServer, Pending, ContentServer, Clan, Lobby, ConsoleUser … (+1 more)

### ActionBind (class)
- `IsCommon : bool`, `Name : string`, `Default : string`

### AdditionalFile (class)
Represents a file to send to the compiler along with all the code
- `Text : string`, `LocalPath : string`

### Animation (class)
Contains one or multiple frames that can be played in sequence
- `Name : string`, `FrameRate : float`, `Origin : Vector2`

### AnimationState (class)
Contains the state of a sprite instance's animation playback
- `JustFinished : bool`, `ResetState(  ) → void`, `TryAdvanceFrame( Sprite.Animation animation, float deltaTime ) → bool`

### AnimTagEvent (struct)
- `Name : string`, `Status : SceneModel.AnimTagStatus`

### AnimTagStatus (enum)
Enumeration that describes how the AnimGraph tag state changed
Values: Fired, Start, End

### Attachment (class)
- `WorldTransform : Transform`, `Model : Model`, `Index : int`, `IsNamed( string name ) → bool`

### AttributeAccess (class)
- `GrabDepthTexture( string token ) → Rendering.RenderTargetHandle`, `GetRenderTarget( string name ) → RenderTarget`, `SetValue( StringToken token, Rendering.RenderValue value ) → void`, `Set( StringToken token, Rendering.RenderTargetHandle.ColorIndexRef buffer ) → void`

### AutoCompleteResult (struct)
- `Command : string`, `Description : string`, `Location : string`

### BaseListener (class)
Allows creating a class that will exist for as long as a particle
- `OnEnabled( Particle p ) → void`, `OnDisabled( Particle p ) → void`, `OnUpdate( Particle p, float dt ) → void`, `Source : Component`

### BeamInstance (class)
Represents an individual beam instance within the effect
- `Delta : float`, `Destroy(  ) → void`

### BindEntry (struct)
- `FullString : string`

### BloomAccessor (class)
- `Enabled : bool`, `Mode : SceneCamera.BloomAccessor.BloomMode`, `Strength : float`

### BloomMode (enum)
Values: Additive, Screen, Blur

### BodyGroups (enum)
Values: Head, Chest, Legs, Hands, Feet

### BodyPart (class)
- `Index : int`, `Name : string`, `Mask : UInt64`

### Bone (class)
A bone in a `BoneCollection`
- `Index : int`, `Name : string`, `Parent : BoneCollection.Bone`, `IsNamed( string name ) → bool`

### Bone (struct)
A bone definition for use with `ModelBuilder`
- `Name : string`, `ParentName : string`, `Position : Vector3`

### BoneVelocity (struct)
- `Linear : Vector3`, `Angular : Vector3`

### Box (class)
A single hitbox on the model
- `Shape : object`, `RandomPointInside : Vector3`, `RandomPointOnEdge : Vector3`

### BroadcastEvent (class)
A message that is broadcast when a frame is displayed
- `Type : Sprite.BroadcastEventType`, `Message : string`, `Sound : SoundEvent`

### BroadcastEventType (enum)
Values: CustomMessage, PlaySound, SpawnPrefab

### ByteParseOptions (struct)

### Callback (class)
Callback delegate for receiving progress updates
- `Invoke( Utility.DataProgress progress ) → void`, `EndInvoke( IAsyncResult result ) → void`, `BeginInvoke( Utility.DataProgress progress, AsyncCallback callback, object object ) → IAsyncResult`

### CapStyle (enum)
Values: None, Triangle, Arrow, Rounded

### Child (class)
- `Compile(  ) → bool`, `SetInputData( string data ) → void`

### Choice (struct)
- `Id : string`, `Title : string`, `Votes : int`

### Choice (class)
- `Name : string`, `Mask : UInt64`

### ClothingCategory (enum)
Values: None, Hat, HatCap, Hair, Skin, Footwear, Bottoms, Tops, Gloves, Facial … (+81 more)

### ClothingEntry (class)
- `Clothing : Clothing`, `ItemDefinitionId : int`, `Tint : float?`

### ClutterStorage (class)
Manages storage and serialization of painted clutter instances
- `GetAllInstances(  ) → IReadOnlyDictionary<string,List<Clutter.ClutterGridSystem.ClutterStorage.Instance>>`, `ClearAll(  ) → void`, `GetInstances( string modelPath ) → IReadOnlyList<Clutter.ClutterGridSystem.ClutterStorage.Instance>`, `ClearModel( string modelPath ) → bool`, `TotalCount : int`

### Color32 (struct)
A 32bit color, commonly used by things like vertex buffers
- `White : Color32`, `Black : Color32`, `Transparent : Color32`, `static FromRgb( uint rgb ) → Color32`, `static FromRgba( uint rgba ) → Color32`

### ColorHsv (struct)
A color in Hue-Saturation-Value/Brightness format
- `Hue : float`, `Saturation : float`, `Value : float`, `ToColor(  ) → Color`, `WithHue( float hue ) → ColorHsv`

### Colors (class)
Using pure primary colors is horrible
- `Red : Color`, `Forward : Color`, `Pitch : Color`

### CommonData (class)
- `Health : float`, `Flammable : bool`, `Explosive : bool`

### Cone (struct)
A tapered shape between two points with a radius at each end
- `RandomPointInside : Vector3`, `RandomPointOnEdge : Vector3`, `Bounds : BBox`, `GetEdgeDistance( Vector3 p ) → float`, `Contains( Vector3 p ) → bool`

### Configuration (struct)
- `Whitelist : bool`, `Unsafe : bool`, `ReleaseMode : Compiler.ReleaseMode`, `GetPreprocessorSymbols(  ) → HashSet<string>`, `GetParseOptions(  ) → Microsoft.CodeAnalysis.CSharp.CSharpParseOptions`

### ControlPointValueInput (enum)
Values: GameObject, Vector3, Float, Color

### CullMode (enum)
Cull mode, either inside or outside
Values: Inside, Outside

### Cursor (struct)
- `Image : string`, `Hotspot : Vector2`

### DataReceivedHandler (class)
Event handler which processes binary messages from the WebSocket service
- `Invoke( Span<byte> data ) → void`, `EndInvoke( IAsyncResult result ) → void`, `BeginInvoke( Span<byte> data, AsyncCallback callback, object object ) → IAsyncResult`

### DataStream (class)
- `Write( string strValue ) → void`, `Write( byte[] bytes ) → void`

### DecalEntry (class)
- `Material : Material`, `Depth : RangedFloat`, `Rotation : RangedFloat`

### DeserializeOptions (struct)
- `TransformOverride : Transform?`

### DisconnectedHandler (class)
Event handler which fires when the WebSocket disconnects from the server
- `EndInvoke( IAsyncResult result ) → void`, `Invoke( int status, string reason ) → void`, `BeginInvoke( int status, string reason, AsyncCallback callback, object object ) → IAsyncResult`

### DontExecuteOnServer (interface)
A component with this interface will not run on dedicated servers

### DownsampleMethod (enum)
Which method to use when downsampling a texture
Values: Box, GaussianBlur, GaussianBorder, Max, Min, MinMax, Default, None

### Entry (struct)
- `ObjectValue : object`, `IntegerValue : long`, `Name : string`

### Enumerator (struct)
Zero-allocation enumerator for `CircularBuffer`1`
- `CurrentRef : T`, `Current : T`, `MoveNext(  ) → bool`, `Reset(  ) → void`, `Dispose(  ) → void`

### ExecuteInEditor (interface)
A component with this interface will run in the editor

### FaceMode (enum)
Values: Camera, Normal, Cylinder

### Feature (class)
A feature is usually displayed as a tab, to break things up in the inspector
- `Name : string`, `Description : string`, `Icon : string`

### File (struct)
- `Url : string`, `Crc : string`, `Path : string`

### Filter (struct)
- `IsRecipient( Connection connection ) → bool`

### FilterType (enum)
Values: Include, Exclude

### Flag (enum)
Command buffer flags allow us to skip command buffers if the camera doesn't want a particular thing
Values: None, PostProcess, Hud

### FlagsAccessor (struct)
- `IsSky : bool`, `IsTranslucent : bool`, `IsAlphaTest : bool`, `GetInt( string name, int defaultValue ) → int`, `GetFloat( string name, float defaultValue ) → float`

### FogLightingMode (enum)
Values: None, Baked, Dynamic, DynamicNoShadows

### FootstepEvent (struct)
- `FootId : int`, `Transform : Transform`, `Volume : float`

### FractalParameters (class)
Parameters for constructing a fractal noise field, which layers multiple octaves of a noise function with increasing fr…
- `Octaves : int`, `Gain : float`, `Lacunarity : float`

### Frame (struct)
Keyframes times and values should range between 0 and 1
- `Time : float`, `Value : float`, `In : float`, `WithTime( float time ) → Curve.Frame`, `WithValue( float value ) → Curve.Frame`

### Frame (class)
Describes a single animation frame
- `Texture : Texture`, `BroadcastMessages : List<Sprite.BroadcastEvent>`

### Frame (struct)
- `VoiceCount : int`, `MaxLevelLeft : float`, `MaxLevelRight : float`

### Function (class)
An easing function that transforms the linear input into non linear output
- `Invoke( float delta ) → float`, `EndInvoke( IAsyncResult result ) → float`, `BeginInvoke( float delta, AsyncCallback callback, object object ) → IAsyncResult`

### GameObjectSystemData (struct)
- `SnapshotData : byte[]`, `TableData : byte[]`, `Type : int`

### GameObjectUndoFlags (enum)
Values: Properties, Components, Children, All

### GenericEvent (struct)
- `Type : string`, `Int : int`, `Float : float`

### GizmoControls (class)
Extendable helper to create common gizmos
- `Rotate( string name, Rotation value, Rotation newValue ) → bool`, `Scale( string name, float value, float outValue ) → bool`, `Sphere( string name, float radius, float outRadius, Color color ) → bool`, `DragSquare( string name, Vector2 size, Rotation rotation, Vector3 movement, Action drawHandle ) → bool`

### GizmoDraw (class)
Contains functions to add objects to the Gizmo Scene
- `Model( string modelName ) → SceneModel`, `Model( Model modelName ) → SceneModel`, `Line( Line line ) → void`, `LineBBox( BBox box ) → void`, `Color : Color`

### GizmoHitbox (class)
Contains functions to add objects to the immediate mode Scene
- `LineScope(  ) → IDisposable`, `TrySetHovered( float distance ) → void`, `TrySetHovered( Vector3 position ) → void`, `Sphere( Sphere sphere ) → void`, `CanInteract : bool`

### GradientColorOffset (struct)

### GradientGenerator (struct)

### GridAxis (enum)
Values: XY, YZ, ZX

### Group (class)
A group is a collection of properties that are related to each other, and can be displayed together in the inspector, u…
- `Name : string`, `Properties : List<SerializedProperty>`

### HandleMode (enum)
Describes how the line should behave when entering/leaving a frame
Values: Mirrored, Split, Flat, Linear, Stepped

### ICollisionListener (interface)
A `Component` with this interface can react to collisions
- `OnCollisionStart( Collision collision ) → void`, `OnCollisionUpdate( Collision collision ) → void`, `OnCollisionStop( CollisionStop collision ) → void`

### IColorProvider (interface)
When applied to a `Component`, the component will be able to provide the color to use for certain UI editor elements
- `ComponentColor : Color`

### IconModes (enum)
Values: Generic, CitizenSkin, HumanSkin, Foot, Hand, Eyes, Head, Mouth, Chest, Wrist … (+1 more)

### IconSetup (struct)
- `Path : string`, `Mode : Clothing.IconSetup.IconModes`, `PositionOffset : Vector3`

### IDamageable (interface)
A component that can be damaged by something
- `OnDamage( DamageInfo& modreq(Runtime.InteropServices.InAttribute) damage ) → void`

### IEventListener (interface)
- `OnRegister( GameResource resource ) → void`, `OnUnregister( GameResource resource ) → void`, `OnSave( GameResource resource ) → void`, `OnExternalChanges( GameResource resource ) → void`

### IEventListener (interface)
Implement this interface to receive navmesh editor events
- `OnAreaDefinitionChanged(  ) → void`

### IEvents (interface)
Events from the PlayerController
- `OnJumped(  ) → void`, `FailPressing(  ) → void`, `PreInput(  ) → void`, `OnEyeAngles( Angles angles ) → void`

### IHasBounds (interface)
A component that has bounds
- `LocalBounds : BBox`

### ImpactEffectData (struct)
- `Regular : List<string>`, `Bullet : List<string>`, `BulletDecal : List<string>`

### IndexBufferLockHandler (class)
- `Invoke( Span<int> data ) → void`, `EndInvoke( IAsyncResult result ) → void`, `BeginInvoke( Span<int> data, AsyncCallback callback, object object ) → IAsyncResult`

### INetworkListener (interface)
A `Component` with this interface can react to network events
- `OnConnected( Connection channel ) → void`, `OnDisconnected( Connection channel ) → void`, `OnActive( Connection channel ) → void`, `OnBecameHost( Connection previousHost ) → void`

### INetworkSnapshot (interface)
When implemented on a `Component` or `GameObjectSystem` it can read and write data to and from a network snapshot
- `ReadSnapshot( ByteStream reader ) → void`, `WriteSnapshot( ByteStream writer ) → void`

### INetworkSpawn (interface)
A `Component` with this interface can listen for when a GameObject in its ancestors has been network spawned
- `OnNetworkSpawn( Connection owner ) → void`

### INetworkVisible (interface)
A `Component` with this interface can determine whether a networked object should be visible for a specific `Connection`
- `IsVisibleToConnection( Connection connection, BBox& modreq(Runtime.InteropServices.InAttribute) worldBounds ) → bool`

### Inputs (struct)
The input state, allows interaction with Gizmos
- `IsHovered : bool`, `CursorPosition : Vector2`, `CursorRay : Ray`

### InstalledVoice (struct)
- `Name : string`, `Gender : string`, `Age : string`

### Instance (class)
Holds the backend state for a Gizmo scope
- `Debug : bool`, `DebugHitboxes : bool`, `World : SceneWorld`, `Clear(  ) → void`, `Dispose(  ) → void`

### Instance (struct)
- `Position : Vector3`, `Rotation : Rotation`, `Scale : float`

### IPressable (interface)
The "walk up and press E" interface, for anything a player can press. It's nested on
`Component`, so declare it as `Component.IPressable`
- `Press( Component.IPressable.Event e ) → bool` (**the only required member**), `CanPress( e ) → bool`, `Pressing( e ) → bool`, `Release( e ) → void`, `Hover( e ) → void`, `Look( e ) → void`, `Blur( e ) → void`, `GetTooltip( e ) → Component.IPressable.Tooltip?`
- `Event` : `record struct ( Component Source, Ray? Ray = default )`, where `Source` is the pressing `PlayerController`
- `Tooltip` : `record struct ( string Title, string Icon, string Description, bool Enabled = true, IPressable Pressable = default )`
- **`Press` fires on the pressing client, never the host.** See `01_SCENE.md` → *IPressable*

### ISceneEditorSession (interface)
- `Scene : Scene`, `HasUnsavedChanges : bool`, `Selection : SelectionSystem`, `AddSelectionUndo(  ) → void`, `GetSelection(  ) → IEnumerable<object>`

### ISceneStage (interface)
Called on update start
- `Start(  ) → void`, `End(  ) → void`

### ISceneUndoScope (interface)
- `WithGameObjectCreations(  ) → ISceneUndoScope`, `WithComponentCreations(  ) → ISceneUndoScope`, `Push(  ) → IDisposable`, `WithGameObjectDestructions( IEnumerable<GameObject> gameObjects ) → ISceneUndoScope`

### ITarget (interface)
The target of a MaterialAccessor
- `GetMaterialCount(  ) → int`, `ClearOverrides(  ) → void`, `Get( int index ) → Material`, `SetOverride( int index, Material material ) → void`, `IsValid : bool`

### Item (class)
Describes a type of item that can be in the inventory
- `ItemId : UInt64`, `DefinitionId : int`, `Definition : Services.Inventory.ItemDefinition`

### ItemDefinition (class)
Describes a type of item that can be in the inventory
- `Price : CurrencyValue`, `BasePrice : CurrencyValue`, `Id : int`

### ITemporaryEffect (interface)
Allows components to indicate their state in a generic way
- `IsActive : bool`, `static DisableLoopingEffects( GameObject go ) → void`, `DisableLooping(  ) → void`

### ITintable (interface)
A `Component` that lets you change its color
- `Color : Color`

### ITraceProvider (interface)
When implementing an ITraceProvider, the most important thing to keep in mind is that the call to DoTrace should be thr…
- `DoTrace( SceneTrace& modreq(Runtime.InteropServices.InAttribute) trace ) → SceneTraceResult?`, `DoTrace( SceneTrace& modreq(Runtime.InteropServices.InAttribute) trace, List<SceneTraceResult> results ) → void`

### ITriggerListener (interface)
A `Component` with this interface can react to interactions with triggers
- `OnTriggerEnter( Collider other ) → void`, `OnTriggerExit( Collider other ) → void`, `OnTriggerEnter( GameObject other ) → void`, `OnTriggerExit( GameObject other ) → void`

### IVolume (interface)
- `GetVolume(  ) → Volumes.SceneVolume`, `Test( Vector3 worldPosition ) → bool`, `Test( BBox worldBBox ) → bool`, `Test( Sphere worldSphere ) → bool`

### JsonConvert (class)
- `Read( Text.Json.Utf8JsonReader reader, Type typeToConvert, Text.Json.JsonSerializerOptions options ) → TagSet`, `Write( Text.Json.Utf8JsonWriter writer, TagSet val, Text.Json.JsonSerializerOptions options ) → void`

### Keyboard (class)
Keyboard related glyph methods
- `static Down( string keyName ) → bool`, `static Pressed( string keyName ) → bool`, `static Released( string keyName ) → bool`, `static GetGlyph( string key, InputGlyphSize size, bool outline ) → Texture`

### LightShape (enum)
Values: Sphere, Capsule, Rectangle

### Line (struct)
Represents a line in 3D space
- `Start : Vector3`, `End : Vector3`, `Delta : Vector3`, `ClosestPoint( Vector3 pos ) → Vector3`, `Distance( Vector3 pos ) → float`

### LipSyncAccessor (class)
- `FrameNumber : int`, `FrameDelay : int`, `LaughterScore : float`

### ListenerState (class)
One of these is created for every listener that uses an audio processor

### LoopMode (enum)
The different loop modes for sprite animation
Values: None, Loop, PingPong

### Map (class)
Stats for the current map
- `All : IEnumerable<Achievement>`, `static Unlock( string name ) → void`, `static Get( string name ) → Achievement`

### Matrix (struct)
Represents a 4x4 matrix
- `Identity : Matrix`, `Inverted : Matrix`, `M11 : float`, `static Lerp( Matrix ma, Matrix mb, float frac ) → Matrix`, `static Slerp( Matrix ma, Matrix mb, float frac ) → Matrix`

### MessageReceivedHandler (class)
Event handler which processes text messages from the WebSocket service
- `Invoke( string message ) → void`, `EndInvoke( IAsyncResult result ) → void`, `BeginInvoke( string message, AsyncCallback callback, object object ) → IAsyncResult`

### MorphAccessor (class)
- `ContainsOverride( string name ) → bool`, `Get( string name ) → float`, `Clear( string name ) → void`, `Set( string name, float weight ) → void`, `Names : string[]`

### NetworkAccessor (class)
- `Active : bool`, `RootGameObject : GameObject`, `IsOwner : bool`, `EnableInterpolation(  ) → bool`, `DisableInterpolation(  ) → bool`

### NoiseType (enum)
Values: Random, Perlin, Simplex

### ObjectEntry (struct)
Holds key values for the map object
- `TypeName : string`, `TargetName : string`, `ParentName : string`, `GetValue( string key, T defaultValue ) → T`, `GetResource( string key, T defaultValue ) → T`

### OldSoundData (struct)
- `FootLeft : string`, `FootRight : string`, `FootLaunch : string`

### OnSpeechResult (class)
Called when we have a result from speech recognition
- `Invoke( Speech.SpeechRecognitionResult result ) → void`, `EndInvoke( IAsyncResult result ) → void`, `BeginInvoke( Speech.SpeechRecognitionResult result, AsyncCallback callback, object object ) → IAsyncResult`

### Option (struct)
- `Name : string`, `Icon : string`

### Options (struct)
- `ForDisk : bool`, `Compiler : Resources.ResourceCompiler`, `Default : Resources.ResourceGenerator.Options`

### Organization (class)
Represents an organization on Asset Party
- `Ident : string`, `Title : string`, `SocialTwitter : string`

### Outcome (struct)
- `Id : string`, `Title : string`, `Users : int`

### Overlay (class)
Provides static methods for displaying various modal overlays in the game UI
- `static ShowBinds(  ) → void`, `static ShowPlayerList(  ) → void`, `static ShowPauseMenu(  ) → void`, `static Close(  ) → void`, `IsOpen : bool`

### PackageUsageStats (struct)
Statistics for user interactions with this package
- `Total : Package.PackageUsageStats.Group`, `Month : Package.PackageUsageStats.Group`, `Week : Package.PackageUsageStats.Group`

### Pair (struct)
A pair of case- and order-insensitive tags, used as a key to look up a `Result`
- `Left : string`, `Right : string`, `Contains( string tag ) → bool`

### ParameterAccessor (class)
- `Clear(  ) → void`, `Reset( string name ) → void`, `Clear( string name ) → void`, `Contains( string name ) → bool`, `Graph : AnimationGraph`

### Parameters (class)
Parameters for constructing a noise field
- `Seed : int`, `Frequency : float`

### PrefabVariableTarget (struct)
Targets a property in a component or gameobject
- `Id : Guid`, `Property : string`

### Pressed (class)
Access to the currently pressed path information
- `Ray : Ray`, `This : bool`, `Any : bool`, `static ClearPath(  ) → void`

### PrimitiveType (enum)
Values: Points, Lines, LinesWithAdjacency, LineStrip, LineStripWithAdjacency, Triangles, TrianglesWithAdjacency, TriangleStrip, TriangleStripWithAdjacency

### ProjectionMode (enum)
Values: Sphere, Box

### PropertyPath (class)
Describes the path to a `SerializedProperty` from either a `GameObject` or `Component`
- `FullName : string`, `Properties : IReadOnlyList<SerializedProperty>`, `Targets : IEnumerable<object>`

### RangedFloat (struct)
A float between two values, which can be randomized or fixed
- `Min : float`, `Max : float`, `FixedValue : float`, `static Parse( string str ) → RangedFloat`, `GetValue(  ) → float`

### RangeType (enum)
Range type of `RangedFloat`
Values: Fixed, Between

### Reader (struct)
Context for reading binary blob data

### ReleaseMode (enum)
Values: Debug, Release

### Result (enum)
Result of a collision between two objects
Values: Unset, Collide, Trigger, Ignore

### Result (struct)
- `Distance : float`, `StartPosition : Vector3`, `EndPosition : Vector3`

### ReviewScore (enum)
Values: None, Negative, Positive, Promise

### SceneObjectFlagAccessor (class)
- `CastShadows : bool`, `ExcludeGameLayer : bool`, `NeedsEnvironmentMap : bool`

### SceneSettings (class)
- `EditMode : string`, `Selection : bool`, `ViewMode : string`, `ClearEnabledGizmos(  ) → void`, `IsGizmoEnabled( Type type ) → bool`

### ScrapeEffectData (struct)
- `RoughnessFactor : float`, `RoughThreshold : float`, `SmoothParticles : List<string>`

### SequenceAccessor (class)
- `PlaybackRate : float`, `Duration : float`, `IsFinished : bool`

### SerializeOptions (class)
- `SceneForNetwork : bool`, `Cloning : bool`, `SingleNetworkObject : bool`

### Slots (enum)
Values: Skin, HeadTop, HeadBottom, Face, Chest, LeftArm, RightArm, LeftWrist, RightWrist, LeftHand … (+19 more)

### SmoothDamped (struct)
Everything you need to smooth damp a Vector3
- `Current : Vector3`, `Target : Vector3`, `SmoothTime : float`, `Update( float timeDelta ) → void`

### SoundEvent (struct)
- `Name : string`, `Position : Vector3`, `AttachmentName : string`

### SoundSelectionMode (enum)
Values: Forward, Backward, Random, RandomExclusive

### SpringDamped (struct)
Everything you need to create a springy Vector3
- `Update( float timeDelta ) → void`

### StyleProperty (struct)
- `Name : string`, `Value : string`, `OriginalValue : string`

### SurfacePrefabCollection (struct)
Holds a dictionary of common prefabs associated with a surface
- `BulletImpact : GameObject`, `BluntImpact : GameObject`

### SurfaceSoundCollection (struct)
Holds a dictionary of common sounds associated with a surface
- `FootLeft : SoundEvent`, `FootRight : SoundEvent`, `FootLaunch : SoundEvent`

### Target (struct)

### TerrainMaterialSettings (class)
- `HeightBlendEnabled : bool`, `HeightBlendSharpness : float`

### TextSceneObject (class)
- `Text : string`, `FontName : string`, `FontSize : float`, `RenderSceneObject(  ) → void`

### ThumbnailOptions (struct)
- `Width : int`, `Height : int`

### TileSizeOption (enum)
Tile size options for streaming mode
Values: Size256, Size512, Size1024, Size2048, Size4096

### UI (class)
Static materials for UI rendering purposes
- `Basic : Material`, `Box : Material`

### VariableCollection (class)
A collection of variabnles that have been configured for this scene
- `static DeconstructKey( string property ) → ValueTuple<Guid,Guid,string>`, `IsVariable( SerializedProperty property ) → bool`, `Create( string name ) → PrefabVariable`, `Remove( PrefabVariable variable ) → void`

### Vector2 (struct)
A 2-dimensional vector
- `static FromRadians( float radians ) → Vector2`, `static FromDegrees( float degrees ) → Vector2`, `static Abs( Vector2 value ) → Vector2`, `static Parse( string str ) → Vector2`, `x : float`

### Vector2Int (struct)
- `static Parse( string str ) → Vector2Int`, `static Min( Vector2Int a, Vector2Int b ) → Vector2Int`, `static Max( Vector2Int a, Vector2Int b ) → Vector2Int`, `Abs(  ) → Vector2Int`, `Normal : Vector2`

### Vector3Int (struct)
- `static Parse( string str ) → Vector3Int`, `static Cross( Vector3Int a, Vector3Int b ) → Vector3Int`, `static Dot( Vector3Int a, Vector3Int b ) → float`, `static Dot( Vector3Int a, Vector3 b ) → float`, `Normal : Vector3`

### Vector4 (struct)
A 4-dimensional vector/point
- `static Parse( string str ) → Vector4`, `static Max( Vector4 a, Vector4 b ) → Vector4`, `static Dot( Vector4 a, Vector4 b ) → float`, `static DistanceBetweenSquared( Vector4 a, Vector4 b ) → float`, `x : float`

### Vertex (struct)
A vertex to update a particle snapshot with

### VertexDetail (struct)
- `Position : Vector3`, `Normal : Vector3`, `Color : Vector4`

### VideoDisplayMode (struct)
- `Width : int`, `Height : int`, `RefreshRate : float`

### VolumeTypes (enum)
Values: Sphere, Box, Capsule, Infinite

### VSCodeExtensions (class)
- `Recommendations : string[]`

### Writer (struct)
Context for writing binary blob data

## Sandbox.UI

### Align (enum)
Possible values for align-items CSS property
Values: Auto, FlexStart, Center, FlexEnd, Stretch, Baseline, SpaceBetween, SpaceAround, SpaceEvenly

### BackgroundRepeat (enum)
Possible values for background-repeat CSS property
Values: Repeat, RepeatX, RepeatY, NoRepeat, Clamp

### BasePopup (class)
A panel that gets deleted automatically when clicked away from
- `StayOpen : bool`, `static CloseAll( UI.Panel exceptThisOne ) → void`, `OnDeleted(  ) → void`

### BaseStyles (class)
Auto generated container class for majority of CSS properties available
- `Overflow : UI.OverflowMode?`, `Content : string`, `Width : UI.Length?`, `Dirty(  ) → void`, `Clone(  ) → object`

### BaseVirtualPanel (class)
Base class for virtualized, scrollable panels that only create item panels when visible
- `NeedsRebuild : bool`, `OnLastCell : Action`, `ItemCount : int`, `Clear(  ) → void`, `Tick(  ) → void`

### BorderImageFill (enum)
State of fill setting of border-image-slice (border-image) CSS property
Values: Unfilled, Filled

### BorderImageRepeat (enum)
Possible values for border-image-repeat (border-image) CSS property
Values: Stretch, Round

### Box (class)
Represents position and size of a `Panel` on the screen
- `Left : float`, `Right : float`, `Top : float`

### ButtonEvent (class)
Keyboard (and mouse) key press `PanelEvent`
- `Button : string`, `Pressed : bool`, `KeyboardModifiers : KeyboardModifiers`

### Clipboard (class)
- `static SetText( string text ) → void`

### CopyEvent (class)

### CutEvent (class)

### DisplayMode (enum)
Possible values for display CSS property
Values: Flex, None, Contents

### DragEvent (class)

### Emoji (class)
Helper class for working with Unicode emoji
- `static FindEmoji( string lookup ) → string`

### EscapeEvent (class)

### FlexDirection (enum)
Possible values for flex-direction CSS property
Values: Column, ColumnReverse, Row, RowReverse

### FontSmooth (enum)
Possible values for font-smooth CSS property
Values: Auto, Never, Always

### FontStyle (enum)
Possible values for font-style CSS property
Values: None, Italic, Oblique

### FontVariantNumeric (enum)
Possible values for font-variant-numeric CSS property
Values: Normal, TabularNums

### Image (class)
A generic box that displays a given texture within itself
- `Texture : Texture`, `HasContent : bool`, `SetTexture( string name ) → void`, `SetProperty( string name, string value ) → void`

### ImageRendering (enum)
Possible values for image-rendering CSS property
Values: Anisotropic, Bilinear, Trilinear, Point

### InputFocus (class)
Handles input focus for `Panel`s
- `Current : UI.Panel`, `Next : UI.Panel`, `static Clear(  ) → bool`, `static Set( UI.Panel panel ) → bool`, `static Clear( UI.Panel panel ) → bool`

### IStyleBlock (interface)
A CSS rule - ie "
- `FileName : string`, `AbsolutePath : string`, `FileLine : int`, `GetRawValues(  ) → List<UI.IStyleBlock.StyleProperty>`, `SetRawValue( string key, string value, string originalValue ) → bool`

### IStyleTarget (interface)
Everything the style system needs to work out a style
- `ElementName : string`, `Id : string`, `PseudoClass : UI.PseudoClass`, `HasClasses( string[] classes ) → bool`

### Justify (enum)
Possible values for justify-content CSS property
Values: FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround, SpaceEvenly

### KeyFrames (class)
Represents a CSS @keyframes rule
- `Name : string`

### Label (class)
A generic text label
- `GetSelectedText(  ) → string`, `LanguageChanged(  ) → void`, `ScrollToCaret(  ) → void`, `GetWordBoundaryIndices(  ) → List<int>`, `Selectable : bool`

### LayoutCascade (struct)

### Length (struct)
A variable unit based length
- `static Pixels( float pixels ) → UI.Length?`, `static Percent( float percent ) → UI.Length?`, `static ViewHeight( float percentage ) → UI.Length?`, `static ViewWidth( float percentage ) → UI.Length?`, `Auto : UI.Length`

### LengthUnit (enum)
Possible units for various CSS properties that require length, used by `Length` struct
Values: Auto, Pixels, Percentage, ViewHeight, ViewWidth, ViewMin, ViewMax, Start, Cover, Contain … (+6 more)

### LengthUnitExtension (class)
- `static IsDynamic( UI.LengthUnit unit ) → bool`

### Margin (struct)
Represents a Rect where each side is the thickness of an edge/padding/margin/border, rather than positions
- `Width : float`, `Height : float`, `Left : float`, `EdgeAdd( UI.Margin edges ) → UI.Margin`, `EdgeSubtract( UI.Margin edges ) → UI.Margin`

### MaskMode (enum)
Possible values for mask-mode CSS property
Values: MatchSource, Alpha, Luminance

### MaskScope (enum)
Possible values for mask-scope CSS property
Values: Default, Filter

### MixinDefinition (class)
Represents a parsed @mixin definition that can be included elsewhere
- `Name : string`, `HasVariadicParameter : bool`, `Content : string`, `Expand( Dictionary<string,string> arguments, string contentBlock ) → string`

### MixinParameter (struct)
A single parameter in a mixin definition

### MousePanelEvent (class)
Mouse related `PanelEvent`
- `MouseButton : MouseButtons`

### ObjectFit (enum)
Values: Fill, Contain, Cover, None

### OverflowMode (enum)
Possible values for the "overflow" CSS rule, dictating what to do with content that is outside of a panels bounds
Values: Visible, Hidden, Scroll, Clip, ClipWhole

### Panel (class)
A simple User Interface panel
- `Add : UI.Construct.PanelCreator`, `HasChildren : bool`, `Parent : UI.Panel`, `FindRootPanel(  ) → UI.RootPanel`, `FindPopupPanel(  ) → UI.Panel`

### PanelEvent (class)
Base `Panel` event
- `This : UI.Panel`, `Name : string`, `Value : object`, `StopPropagation(  ) → void`, `Is( string name ) → bool`

### PanelInputType (enum)
Values: UI, Game

### PanelRenderTreeBuilder (class)
This is a tree renderer for panels
- `CloseElement(  ) → void`, `AddStyleDefinitions( int sequence, string styles ) → void`, `AddContent( int sequence, T content ) → void`, `AddMarkupContent( int sequence, string markupContent ) → void`

### PanelStyle (class)
- `Dirty(  ) → void`, `SetBackgroundImage( Texture texture ) → void`, `SetBackgroundImage( string image ) → void`, `SetBackgroundImageAsync( string image ) → Task`, `HasBeforeElement : bool`

### PanelTransform (struct)
- `IsEmpty(  ) → bool`, `AddTranslateX( UI.Length? length ) → bool`, `AddTranslateY( UI.Length? length ) → bool`, `AddTranslateZ( UI.Length? length ) → bool`, `Entries : int`

### PasteEvent (class)
- `ClipboardValue : string`

### PointerEvents (enum)
Possible values for pointer-events CSS property
Values: All, None

### PositionMode (enum)
Possible values for position CSS property
Values: Static, Relative, Absolute

### PseudoClass (enum)
List of CSS pseudo-classes used by the styling system for hover, active, etc
Values: None, Unknown, Hover, Active, Focus, Intro, Outro, Empty, FirstChild, LastChild … (+3 more)

### RenderState (struct)
Describes panel's position and size for rendering operations
- `X : float`, `Y : float`, `Width : float`

### RootPanel (class)
A root panel
- `PanelBounds : Rect`, `Scale : float`, `RenderedManually : bool`, `RenderManual( float opacity ) → void`, `OnDeleted(  ) → void`

### ScenePanel (class)
Allows to render a scene world onto a panel
- `World : SceneWorld`, `Camera : SceneCamera`, `RenderOnce : bool`, `RenderNextFrame(  ) → void`, `Tick(  ) → void`

### SelectionEvent (class)

### Shadow (struct)
Shadow style settings
- `Scale( float f ) → UI.Shadow`, `LerpTo( UI.Shadow shadow, float delta ) → UI.Shadow`

### ShadowList (class)
A list of shadows
- `AddFrom( UI.ShadowList other ) → void`, `SetFromLerp( UI.ShadowList a, UI.ShadowList b, float frac ) → void`

### StyleBlock (class)
A CSS rule - ie "
- `Selectors : UI.StyleSelector[]`, `FileName : string`, `AbsolutePath : string`, `GetRawValues(  ) → List<UI.IStyleBlock.StyleProperty>`, `TestBroadphase( UI.IStyleTarget target ) → bool`

### Styles (class)
Represents all supported CSS properties and their currently assigned values
- `ResetAnimation(  ) → void`, `BuildTransformMatrix( Vector2 size ) → Matrix`, `StartAnimation( string name, float duration, int iterations, float delay, string timing, string direction, string fillmode ) → void`, `Dirty(  ) → void`, `HasTransitions : bool`

### StyleSelector (class)
A CSS selector like "Panel
- `Id : string`, `Classes : string[]`, `Score : int`, `Test( UI.IStyleTarget target, UI.PseudoClass forceFlag ) → bool`, `TestBroadphase( UI.IStyleTarget target ) → bool`

### StyleSheet (class)
- `Release(  ) → void`, `SetMixin( UI.MixinDefinition mixin ) → void`, `GetMixin( string name ) → UI.MixinDefinition`, `TryGetMixin( string name, UI.MixinDefinition mixin ) → bool`, `FileName : string`

### StyleSheetCollection (struct)
A collection of `StyleSheet` objects applied directly to a panel
- `CollectVariables(  ) → IEnumerable<ValueTuple<string,string>>`, `Add( UI.StyleSheet sheet ) → void`, `Remove( UI.StyleSheet sheet ) → void`, `Remove( string wildcardGlob ) → void`

### SvgPanel (class)
A generic panel that draws an SVG scaled to size
- `Src : string`, `Color : string`, `HasContent : bool`, `FinalLayout( Vector2 offset ) → void`

### TextAlign (enum)
Possible values for text-align CSS property
Values: Auto, Left, Center, Right

### TextDecoration (enum)
Possible values for text-decoration CSS property
Values: None, Underline, LineThrough, Overline

### TextDecorationStyle (enum)
Possible values for text-decoration-style CSS property
Values: Solid, Double, Dotted, Dashed, Wavy

### TextOverflow (enum)
Possible values for text-overflow CSS property
Values: None, Ellipsis, Clip

### TextSkipInk (enum)
Possible values for text-decoration-skip-ink CSS property
Values: All, None

### TextTransform (enum)
Possible values for text-transform CSS property
Values: None, Capitalize, Uppercase, Lowercase

### TransitionDesc (struct)
Describes transition of a single CSS property, a

### TransitionList (class)
A list of CSS properties that should transition when changed
- `Clear(  ) → void`

### Transitions (class)
Handles the storage, progression and application of CSS transitions for a single `Panel`
- `HasAny : bool`

### VirtualGrid (class)
A virtualized, scrollable grid panel that only creates item panels when visible
- `ItemSize : Vector2`

### VirtualList (class)
A virtualized, scrollable list panel that only creates item panels when visible
- `ItemHeight : float`

### WebPanel (class)
A panel that displays an interactive web page
- `OnDeleted(  ) → void`, `OnMouseWheel( Vector2 value ) → void`, `OnKeyTyped( Char k ) → void`, `OnButtonEvent( UI.ButtonEvent e ) → void`, `Surface : WebSurface`

### WhiteSpace (enum)
Possible values for white-space CSS property
Values: Normal, NoWrap, PreLine, Pre

### WordBreak (enum)
Possible values for word-break CSS property
Values: Normal, BreakAll

### WorldInput (class)
- `Enabled : bool`, `Ray : Ray`, `MouseLeftPressed : bool`

### WorldPanel (class)
An interactive 2D panel rendered in the 3D world
- `Transform : Transform`, `Tags : ITagSet`, `Position : Vector3`, `OnDeleted(  ) → void`, `Delete( bool immediate ) → void`

### Wrap (enum)
Possible values for flex-wrap CSS property
Values: NoWrap, Wrap, WrapReverse

## Sandbox.UI.Construct

### ImageConstructor (class)
- `static Image( UI.Construct.PanelCreator self, string image, string classname ) → UI.Image`

### LabelConstructor (class)
- `static Label( UI.Construct.PanelCreator self, string text, string classname ) → UI.Label`

### PanelCreator (struct)
Used for `Add` for quick panel creation with certain settings
- `Panel(  ) → UI.Panel`, `Panel( string classname ) → UI.Panel`

### SceneConstructor (class)
- `static ScenePanel( UI.Construct.PanelCreator self, SceneWorld world, Vector3 position, Rotation rotation, float fieldOfView, string classname ) → UI.ScenePanel`

## Sandbox.Network

### ConnectionStats (struct)
- `Ping : int`, `OutPacketsPerSecond : float`, `OutBytesPerSecond : float`

### GameNetworkSystem (class)
An instance of this is created by the NetworkSystem when a server is joined, or created
- `OnInitialize(  ) → void`, `Push(  ) → IDisposable`, `OnConnected( Connection client ) → void`, `OnJoined( Connection client ) → void`, `IsHost : bool`

### HostStats (struct)
- `OutBytesPerSecond : float`, `InBytesPerSecond : float`, `Fps : UInt16`

### InitialSnapshotResponse (struct)
- `Snapshot : Network.SnapshotMsg`, `HandshakeId : Guid`

### LobbyConfig (struct)
- `DestroyWhenHostLeaves : bool`, `AutoSwitchToBestHost : bool`, `Hidden : bool`

### LobbyInformation (struct)
- `IsFull : bool`, `IsHidden : bool`, `Get( string key, string defaultValue ) → string`

### LobbyPrivacy (enum)
Values: Public, Private, FriendsOnly

### MountedVPKsResponse (struct)
- `HandshakeId : Guid`, `MountedVPKs : List<string>`

### NetworkFile (struct)
- `Name : string`, `Content : byte[]`

### NetworkSocket (class)

### ReconnectMsg (struct)
Sent to the server to tell clients to reconnect
- `Game : string`, `Map : string`

### SnapshotMsg (struct)
- `Time : double`, `SceneData : string`, `BlobData : byte[]`

## Sandbox.Physics

### BallSocketJoint (class)
A ballsocket constraint
- `Friction : float`, `SwingLimit : Vector2`, `SwingLimitEnabled : bool`

### CollisionRules (class)
This is a JSON serializable description of the physics's collision rules
- `SerializedPairs : Text.Json.Nodes.JsonNode`, `Defaults : Dictionary<string,Physics.CollisionRules.Result>`, `Pairs : Dictionary<Physics.CollisionRules.Pair,Physics.CollisionRules.Result>`, `Clean(  ) → void`, `GetCollisionRule( string left, string right ) → Physics.CollisionRules.Result`

### ControlJoint (class)
The control joint is designed to control the movement of a body while remaining responsive to collisions
- `LinearVelocity : Vector3`, `AngularVelocity : Vector3`, `MaxVelocityForce : float`

### FixedJoint (class)
A generic "rope" type constraint
- `SpringLinear : Physics.PhysicsSpring`, `SpringAngular : Physics.PhysicsSpring`

### HingeJoint (class)
A hinge-like constraint
- `MaxAngle : float`, `MinAngle : float`, `Friction : float`

### PhysicsSettings (class)
- `UseFixedUpdate : bool`, `SubSteps : int`, `FixedUpdateFrequency : float`

### PhysicsSpring (struct)
Spring related settings for joints such as `FixedJoint`
- `Frequency : float`, `Damping : float`, `Maximum : float`

### PulleyJoint (class)
A pulley constraint

### SliderJoint (class)
A slider constraint, basically allows movement only on the arbitrary axis between the 2 constrained objects on creation
- `MaxLength : float`, `MinLength : float`, `Friction : float`

### SpringJoint (class)
A rope-like constraint that is has springy/bouncy
- `SpringLinear : Physics.PhysicsSpring`, `MaxLength : float`, `MinLength : float`

## Sandbox.Audio

### AudioChannel (struct)
Represents an audio channel, between 0 and 7
- `Left : Audio.AudioChannel`, `Right : Audio.AudioChannel`, `Get(  ) → int`

### AudioMeter (class)
Allows the capture and monitor of an audio source
- `Current : Audio.AudioMeter.Frame`

### AudioProcessor (class)
Takes a bunch of samples and processes them
- `Enabled : bool`, `Mix : float`, `Serialize(  ) → Text.Json.Nodes.JsonObject`, `Deserialize( Text.Json.Nodes.JsonObject node ) → void`

### AudioProcessor<T> (class)
Audio processor that allows per listener state

### DelayProcessor (class)
- `Delay : float`, `Volume : float`

### DspPresetHandle (struct)
A handle to a DspPreset
- `static GetDropdownSelection(  ) → object[]`, `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`, `Name : string`

### DspProcessor (class)
- `Effect : Audio.DspPresetHandle`

### HighPassProcessor (class)
Just a test - don't count on this sticking around
- `Cutoff : float`

### LowPassProcessor (class)
Just a test - don't count on this sticking around
- `Cutoff : float`

### MixBuffer (class)
Contains 512 samples of audio data, this is used when mixing a single channel
- `Silence(  ) → void`, `CopyFrom( Audio.MixBuffer other ) → void`, `Scale( float volume ) → void`, `MixFrom( Audio.MixBuffer other, float scale ) → void`, `LevelMax : float`

### Mixer (class)
Takes a bunch of sound, changes its volumes, mixes it together, outputs it
- `Meter : Audio.AudioMeter`, `Id : Guid`, `Name : string`, `GetOcclusionTags(  ) → Collections.Generic.IReadOnlySet<uint>`, `ClearProcessors(  ) → void`

### MixerHandle (struct)
A handle to a Mixer
- `static GetDropdownSelection(  ) → object[]`, `static JsonRead( Text.Json.Utf8JsonReader reader, Type typeToConvert ) → object`, `static JsonWrite( object value, Text.Json.Utf8JsonWriter writer ) → void`, `GetOrDefault(  ) → Audio.Mixer`, `Name : string`

### MixerSettings (class)
- `Version : int`, `Mixers : Text.Json.Nodes.JsonObject`

### MultiChannelBuffer (class)
Holds up to 8 mix buffers, which usually represent output speakers
- `Dispose(  ) → void`, `Silence(  ) → void`, `Get( Audio.AudioChannel i ) → Audio.MixBuffer`, `Get( int i ) → Audio.MixBuffer`, `ChannelCount : int`

### PerChannel<T> (struct)
Stores a variable per channel
- `Value : T[]`, `Get( Audio.AudioChannel i ) → T`, `Set( Audio.AudioChannel i, T value ) → void`

### PitchProcessor (class)
- `Pitch : float`

## Sandbox.VR

### AnalogInput (struct)
Represents a VR analog input action (e
- `Value : float`, `Delta : float`, `Active : bool`

### AnalogInput2D (struct)
Represents a two-dimensional VR analog input action (e
- `Value : Vector2`, `Delta : Vector2`, `Active : bool`

### DigitalInput (struct)
Represents a VR digital input action (e
- `IsPressed : bool`, `WasPressed : bool`, `Delta : bool`

### FingerValue (enum)
Accessors for `VRController
Values: ThumbCurl, IndexCurl, MiddleCurl, RingCurl, PinkyCurl, ThumbIndexSplay, IndexMiddleSplay, MiddleRingSplay, RingPinkySplay

### MotionRange (enum)
Values: Hand, Controller

### TrackedDeviceRole (enum)
Values: Unknown, LeftHand, RightHand, Head, Gamepad, Treadmill, Stylus, LeftFoot, RightFoot, LeftShoulder … (+13 more)

### TrackedDeviceType (enum)
Values: Invalid, Hmd, Controller, Tracker, BaseStation, Redirect

### TrackedObject (class)
Represents a physically tracked VR object with a transform
- `Active : bool`, `Velocity : Vector3`, `AngularVelocity : Angles`

### VRController (class)
Represents a VR controller, along with its transform, velocity, and inputs
- `Transform : Transform`, `AimTransform : Transform`, `IsHandTracked : bool`, `GetModel(  ) → Model`, `StopAllVibrations(  ) → void`

### VRHandJoint (enum)
Values: Palm, Wrist, ThumbMetacarpal, ThumbProximal, ThumbDistal, ThumbTip, IndexMetacarpal, IndexProximal, IndexIntermediate, IndexDistal … (+16 more)

### VRHandJointData (struct)

### VRInput (class)
- `Current : VR.VRInput`, `Scale : float`, `Anchor : Transform`

### VROverlay (class)
VR overlays draw over the top of the 3D scene, they will not be affected by lighting, post processing effects or anythi…
- `Visible : bool`, `Transform : Transform`, `SortOrder : uint`, `Dispose(  ) → void`, `SetTransformAbsolute( Transform transform ) → void`

## Sandbox.Movement

### ISitTarget (interface)
A component that can be sat in by a player
- `CalculateEyeTransform( PlayerController controller ) → Transform`, `AskToLeave( PlayerController controller ) → void`, `UpdatePlayerAnimator( PlayerController controller, SkinnedModelRenderer renderer ) → void`

## Sandbox.Navigation

### CalculatePathRequest (struct)
Defines the input for a pathfinding request on the navmesh

### NavMesh (class)
Navigation Mesh - allowing AI to navigate a world
- `IsEnabled : bool`, `IsGenerating : bool`, `IsDirty : bool`, `SetDirty(  ) → void`, `UnloadTile( Vector3 worldPosition ) → void`

### NavMeshPath (struct)
Contains the result of a pathfinding operation
- `Status : Navigation.NavMeshPathStatus`, `IsValid : bool`, `Points : IReadOnlyList<Navigation.NavMeshPathPoint>`

### NavMeshPathPoint (struct)
Represents a point in a navmesh path, including its position in 3D space

### NavMeshPathStatus (enum)
Values: StartNotFound, TargetNotFound, PathNotFound, Partial, Complete

## Sandbox.Rendering

### CommandList (class)
- `ClearRenderTarget(  ) → void`, `GrabDepthTexture( string token ) → Rendering.RenderTargetHandle`, `InsertList( Rendering.CommandList otherBuffer ) → void`, `ReleaseRenderTarget( Rendering.RenderTargetHandle handle ) → void`, `GlobalAttributes : Rendering.CommandList.AttributeAccess`

### FilterMode (enum)
Represents filtering modes for texture sampling in the rendering pipeline
Values: Point, Bilinear, Trilinear, Anisotropic

### GradientFogSetup (struct)
Setup for defining gradient fog in a view
- `Enabled : bool`, `StartDistance : float`, `EndDistance : float`, `LerpTo( Rendering.GradientFogSetup desired, float delta, bool clamp ) → Rendering.GradientFogSetup`

### HudPainter (struct)
2D Drawing functions for a `CommandList`
- `SetBlendMode( BlendMode mode ) → void`, `SetMatrix( Matrix matrix ) → void`, `DrawTexture( Texture texture, Rect rect ) → void`, `DrawCircle( Vector2 position, Vector2 size, Color color ) → void`

### ReflectionSetup (struct)
Allows special setup for reflections, such as offsetting the reflection plane
- `FallbackColor : Color?`

### RefractionSetup (struct)
Allows special setup for refraction, such as offsetting the clip plane
- `FallbackColor : Color?`

### RendererSetup (struct)
When manually rendering a Renderer this will let you override specific elements of that render

### RenderTargetHandle (struct)
A render target handle used with CommandLists
- `ColorTexture : Rendering.RenderTargetHandle.ColorTextureRef`, `DepthTexture : Rendering.RenderTargetHandle.DepthTextureRef`, `ColorIndex : Rendering.RenderTargetHandle.ColorIndexRef`

### RenderValue (enum)
Values: ColorTarget, DepthTarget, MsaaCombo

### ResourceState (enum)
Used to describe a GPU resources state for barrier transitions
Values: Common, Present, VertexOrIndexBuffer, RenderTarget, UnorderedAccess, DepthWrite, DepthRead, NonPixelShaderResource, PixelShaderResource, StreamOut … (+10 more)

### SamplerState (struct)
Represents a sampler state used to control how textures are sampled in shaders
- `Filter : Rendering.FilterMode`, `AddressModeU : Rendering.TextureAddressMode`, `AddressModeV : Rendering.TextureAddressMode`

### Stage (enum)
Values: AfterDepthPrepass, AfterOpaque, AfterSkybox, AfterTransparent, AfterViewmodel, BeforePostProcess, Tonemapping, AfterPostProcess, AfterUI

### TextureAddressMode (enum)
Specifies how texture coordinates outside the [0
Values: Wrap, Mirror, Clamp, Border, MirrorOnce

### TextureStreaming (class)
Gives global access to the texture streaming system
- `static ExecuteWithDisabled( Action action ) → void`

### ViewSetup (struct)
When manually rendering a camera this will let you override specific elements of that render

## Sandbox.Resources

### ColorTextureGenerator (class)
Generate a texture which is just a single color
- `Color : Color`

### EmbeddedResource (struct)
A JSON definition of an embedded resource
- `ResourceCompiler : string`, `ResourceGenerator : string`, `TypeName : string`

### ImageFileGenerator (class)
Load images from disk and convert them to textures
- `FilePath : string`, `MaxSize : int`, `ConvertHeightToNormals : bool`, `CreateEmbeddedResource(  ) → Resources.EmbeddedResource?`

### LinearGradient (class)
- `Size : Vector2Int`, `IsHdr : bool`, `Angle : float`

### RadialGradient (class)
- `Size : Vector2Int`, `IsHdr : bool`, `Scale : float`

### RandomTextureGenerator (class)
- `Type : Resources.RandomTextureGenerator.NoiseType`, `Seed : int`, `Size : Vector2Int`, `static IntToRandomFloat( long seed ) → float`

### RenderTextureAssetGenerator (class)
Provides a texture generator entry that returns the texture owned by a RenderTexture asset
- `Asset : RenderTextureAsset`

### ResourceCompileContext (class)
- `AbsolutePath : string`, `RelativePath : string`, `ResourceVersion : int`, `ReadSource(  ) → byte[]`, `ReadSourceAsString(  ) → string`

### ResourceCompiler (class)
Takes the "source" of a resource and creates a compiled version
- `Context : Resources.ResourceCompileContext`

### ResourceGenerator (class)
Creates a resource from a json definition
- `static Create( string generatorName ) → Resources.ResourceGenerator<T>`, `static Create( Resources.EmbeddedResource serialized ) → Resources.ResourceGenerator<T>`, `static CreateResource( Resources.EmbeddedResource obj, Resources.ResourceGenerator.Options options, Type type ) → Resource`, `GetHash(  ) → UInt64`, `CacheToDisk : bool`

### ResourceGenerator<T> (class)
A resource generator targetting a specific type
- `FindCached(  ) → T`, `FindOrCreate( Resources.ResourceGenerator.Options options ) → T`, `Create( Resources.ResourceGenerator.Options options ) → T`, `FindOrCreateAsync( Resources.ResourceGenerator.Options options, Threading.CancellationToken token ) → Threading.Tasks.ValueTask<T>`, `UseMemoryCache : bool`

### SvgSourceGenerator (class)
- `Size : Vector2Int`, `Source : string`, `Colorize : bool`

### TextTextureGenerator (class)
- `Size : Vector2Int`, `Margin : UI.Margin`, `TextFlags : TextFlag`

### TextureGenerator (class)
- `Create( Resources.ResourceGenerator.Options options ) → Texture`, `CreateAsync( Resources.ResourceGenerator.Options options, Threading.CancellationToken token ) → Threading.Tasks.ValueTask<Texture>`, `CreateEmbeddedResource(  ) → Resources.EmbeddedResource?`

## Sandbox.Services

### AchievementOverview (class)
Activity Feed
- `Package : Package`, `Achievements : Achievement[]`, `LastSeen : DateTimeOffset`

### Achievements (class)
Allows access to stats for the current game
- `All : IEnumerable<Achievement>`, `static Unlock( string name ) → void`

### Auth (class)
- `static GetToken( string serviceName, Threading.CancellationToken token ) → Task<string>`

### BenchmarkSystem (class)
Allows access to stats for the current game
- `Finish(  ) → void`, `Sample(  ) → void`, `Start( string name ) → void`, `SendAsync( Threading.CancellationToken token ) → Task<Guid>`

### Feed (class)
Activity Feed
- `Timestamp : DateTimeOffset`, `Text : string`, `Url : string`

### Inventory (class)
Allows access to the Steam Inventory system
- `Items : Collections.Generic.IReadOnlyCollection<Services.Inventory.Item>`, `Definitions : Collections.Generic.IReadOnlyCollection<Services.Inventory.ItemDefinition>`, `static HasItem( int inventoryDefinitionId ) → bool`, `static FindDefinition( int definitionId ) → Services.Inventory.ItemDefinition`

### Leaderboards (class)
- `static Get( string name ) → Services.Leaderboards.Board`, `static GetFromStat( string statName ) → Services.Leaderboards.Board2`, `static GetFromStat( string packageIdent, string statName ) → Services.Leaderboards.Board2`

### Messaging (class)

### News (class)
News Posts
- `Id : Guid`, `Created : DateTimeOffset`, `Title : string`, `static GetPlatformNews( int take, int skip ) → Task<Services.News[]>`, `static GetNews( int take, int skip ) → Task<Services.News[]>`

### Notification (class)
Player notification
- `Created : DateTimeOffset`, `Updated : DateTimeOffset`, `Count : int`

### Review (class)
Package Reviews
- `Player : Services.Players.Profile`, `Content : string`, `Score : Services.Review.ReviewScore`, `static Get( string packageIdent, SteamId steamid ) → Task<Services.Review>`, `static Fetch( string packageIdent, int take, int skip ) → Task<Services.Review[]>`

### Screenshots (class)
Implements Steamscreenshots

### ServerList (class)
- `Query(  ) → void`, `Dispose(  ) → void`, `AddFilter( string key, string value ) → void`, `IsQuerying : bool`

### Stats (class)
Allows access to stats for the current game
- `static Flush(  ) → void`, `static FlushAsync( Threading.CancellationToken token ) → Task`, `static FlushAndWaitAsync( Threading.CancellationToken token ) → Task`, `static GetGlobalStats( string packageIdent ) → Services.Stats.GlobalStats`, `Global : Services.Stats.GlobalStats`

## Sandbox.Services.Players

### Overview (class)
An overview of a player
- `AvatarJson : string`, `Player : Services.Players.Profile`, `GamesPlayed : long`, `static Get( SteamId steamid ) → Task<Services.Players.Overview>`

### Profile (class)
Player profile
- `Id : SteamId`, `Name : string`, `Url : string`, `static Get( SteamId steamid ) → Task<Services.Players.Profile>`

## Sandbox.Diagnostics

### Allocations (class)
Tools for diagnosing heap allocations

### Assert (class)
- `static NotNull( T obj ) → void`, `static IsNull( T obj ) → void`, `static IsValid( IValid obj ) → void`, `static NotNull( T obj, string message ) → void`

### FastTimer (struct)
A lightweight, high-resolution timer for performance measurement
- `StartTick : long`, `ElapsedTicks : long`, `ElapsedMicroSeconds : double`, `static StartNew(  ) → Diagnostics.FastTimer`, `Start(  ) → void`

### FrameStats (struct)
Stats returned from the engine each frame describing what was rendered, and how much of it
- `ObjectsRendered : double`, `ObjectsPreCull : double`, `ObjectsTested : double`

### GpuProfilerStats (class)
GPU profiler stats collected from the scene system timestamp manager
- `Enabled : bool`, `TotalGpuTimeMs : float`, `Entries : IReadOnlyList<Diagnostics.GpuTimingEntry>`, `static GetSmoothedDuration( string name ) → float`, `static GetMaxDuration( string name ) → float`

### GpuTimingEntry (struct)
GPU timing data for a single render pass/group
- `Name : string`, `DurationMs : float`

### Performance (class)
- `static Scope( string title ) → Diagnostics.Performance.ScopeSection`

### PerformanceStats (class)
- `FrameTime : double`, `GpuFrametime : float`, `GpuFrameNumber : uint`

## Sandbox.Localization

### LanguageInformation (class)
- `Title : string`, `Abbreviation : string`, `Parent : string`

### Languages (class)
A list of supported languages and metadata surrounding them
- `List : IEnumerable<Localization.LanguageInformation>`, `static Find( string key ) → Localization.LanguageInformation`

### Phrase (class)
A translated string
- `Render(  ) → string`, `Render( Dictionary<string,object> data ) → string`

### PhraseCollection (class)
Holds a bunch of localized phrases
- `Set( string key, string value ) → void`, `GetPhrase( string phrase, Dictionary<string,object> data ) → string`

## Sandbox.Utility

### CircularBuffer<T> (class)
Circular buffer, push pop and index access is always O(1)
- `Capacity : int`, `IsFull : bool`, `IsEmpty : bool`, `Front(  ) → T`, `Back(  ) → T`

### Crc32 (class)
Generates 32-bit Cyclic Redundancy Check (CRC32) checksums
- `static FromString( string str ) → uint`, `static FromStreamAsync( IO.Stream stream ) → Task<uint>`, `static FromBytes( IEnumerable<byte> byteStream ) → uint`

### Crc64 (class)
Generate 64-bit Cyclic Redundancy Check (CRC64) checksums
- `static FromString( string str ) → UInt64`, `static FromStreamAsync( IO.Stream stream ) → Task<UInt64>`, `static FromStream( IO.Stream stream ) → UInt64`, `static FromBytes( byte[] stream ) → UInt64`

### DataProgress (struct)
Provides progress information for operations that process blocks of data, such as file uploads, downloads, or large dat…
- `ProgressBytes : long`, `TotalBytes : long`, `DeltaBytes : long`

### DisposeAction (struct)
A simple IDisposable that invokes an action when disposed
- `static Create( Action action ) → IDisposable`, `Dispose(  ) → void`

### Easing (class)
Easing functions used for transitions
- `static Linear( float f ) → float`, `static QuadraticIn( float f ) → float`, `static QuadraticOut( float f ) → float`, `static QuadraticInOut( float f ) → float`

### EditorTools (class)
Functions to interact with the tools system
- `InspectorObject : object`

### FloatBitmap (class)
- `Width : int`, `Height : int`, `Depth : int`, `Dispose(  ) → void`, `EncodeTo( ImageFormat format ) → byte[]`

### INoiseField (interface)
A noise function that can be sampled at a 1-, 2-, or 3D position
- `Sample( float x ) → float`, `Sample( Vector2 vec ) → float`, `Sample( Vector3 vec ) → float`, `Sample( float x, float y ) → float`

### Noise (class)
Provides access to coherent noise utilities
- `static ValueField( Utility.Noise.Parameters parameters ) → Utility.INoiseField`, `static PerlinField( Utility.Noise.Parameters parameters ) → Utility.INoiseField`, `static SimplexField( Utility.Noise.Parameters parameters ) → Utility.INoiseField`, `static Perlin( float x, float y ) → float`

### Parallel (class)
Wrappers of the parallel class
- `static ForEach( IEnumerable<T> source, Action<T> body ) → bool`, `static ForEach( IEnumerable<T> source, Threading.CancellationToken token, Action<T> body ) → bool`, `static For( int fromInclusive, int toExclusive, Action<int> body ) → bool`, `static ForAsync( int fromInclusive, int toExclusive, Threading.CancellationToken token, Func<int,Threading.CancellationToken,Threading.Tasks.ValueTask> body ) → Task`

### Steam (class)
- `static CategorizeSteamId( SteamId steamid ) → SteamId.AccountTypes`, `static IsFriend( SteamId steamid ) → bool`, `static IsOnline( SteamId steamid ) → bool`, `static FilterText( string input, SteamId? from ) → string`, `SteamId : SteamId`

## Sandbox.Utility.Svg

### AddCirclePathCommand (class)
See
- `X : float`, `Y : float`, `Radius : float`

### AddOvalPathCommand (class)
See
- `Rect : Rect`

### AddPolyPathCommand (class)
See ,
- `Close : bool`, `Points : IReadOnlyList<Vector2>`

### AddRectPathCommand (class)
See
- `Rect : Rect`

### AddRoundRectPathCommand (class)
See
- `Rect : Rect`, `Rx : float`, `Ry : float`

### ArcToPathCommand (class)
See
- `Rx : float`, `Ry : float`, `XAxisRotate : float`

### ClosePathCommand (class)
See

### CubicToPathCommand (class)
See
- `X0 : float`, `Y0 : float`, `X1 : float`

### LineToPathCommand (class)
See
- `X : float`, `Y : float`

### MoveToPathCommand (class)
See
- `X : float`, `Y : float`

### PathArcSize (enum)
Controls arc size in `ArcToPathCommand`
Values: Small, Large

### PathCommand (class)
Base class for SVG path commands

### PathDirection (enum)
Controls arc direction in `ArcToPathCommand`
Values: Clockwise, CounterClockwise

### PathFillType (enum)
How to determine which sections of the path are filled
Values: Winding, EvenOdd

### QuadToPathCommand (class)
See
- `X0 : float`, `Y0 : float`, `X1 : float`

### SvgDocument (class)
Helper class for reading Scalable Vector Graphics files
- `Paths : IReadOnlyList<Utility.Svg.SvgPath>`, `static FromString( string contents ) → Utility.Svg.SvgDocument`

### SvgPath (class)
A shape in a `SvgDocument`, described as a vector path
- `FillType : Utility.Svg.PathFillType`, `IsEmpty : bool`, `Bounds : Rect`

## Sandbox.ActionGraphs

### ActionGraphEditorExtensions (class)
Helper methods for action graph editor tools
- `static GetSceneReferences( Facepunch.ActionGraphs.IActionGraphDelegate actionGraphDelegate ) → IEnumerable<ActionGraphs.SceneReferenceNode>`, `static GetNodeProperties( GameObject go ) → IReadOnlyDictionary<string,object>`, `static GetNodeProperties( string prefabPath ) → IReadOnlyDictionary<string,object>`, `static GetNodeProperties( Component component ) → IReadOnlyDictionary<string,object>`

### ActionGraphExtensions (class)
- `static GetReferencedComponentTypes( Facepunch.ActionGraphs.ActionGraph graph ) → Collections.Generic.IReadOnlyCollection<Type>`, `static GetEmbeddedTarget( Facepunch.ActionGraphs.ActionGraph actionGraph ) → object`, `static GetEmbeddedTarget( Facepunch.ActionGraphs.IActionGraphDelegate actionGraph ) → object`, `static GetTargetType( Facepunch.ActionGraphs.ActionGraph actionGraph ) → Type`

### ActionGraphResource (class)
Some game logic implemented using visual scripting
- `DisplayInfo : DisplayInfo`, `SerializedGraph : Text.Json.Nodes.JsonNode`, `Graph : Facepunch.ActionGraphs.ActionGraph`

### GameResourceSourceLocation (class)
Source location for action graphs that belong to a `GameResource`
- `Resource : GameResource`

### IActionComponent (interface)
A component that only provides actions to implement with an Action Graph

### IActionGraphEvents (interface)
- `SceneReferenceTriggered( ActionGraphs.SceneReferenceTriggeredEvent ev ) → void`

### ISerializationOptionProvider (interface)
A `ISourceLocation` that provides `SerializationOptions`
- `SerializationOptions : Facepunch.ActionGraphs.SerializationOptions`

### MapSourceLocation (class)
Source location for action graphs that belong to a Hammer map
- `MapPathName : string`, `SerializationOptions : Facepunch.ActionGraphs.SerializationOptions`, `static Get( string mapPathName ) → ActionGraphs.MapSourceLocation`

### SceneReferenceNode (struct)
An `Node` from an `ActionGraph` that references a `GameObject` or `Component`
- `Node : Facepunch.ActionGraphs.Node`, `TargetObject : GameObject`, `TargetComponent : Component`

### SceneReferenceTriggeredEvent (struct)
- `Source : GameObject`, `Target : IValid`, `Node : Facepunch.ActionGraphs.Node`

## Sandbox.Clutter

### ClutterDefinition (class)
A weighted collection of Prefabs and Models for random selection during clutter placement
- `TileSizeEnum : Clutter.ClutterDefinition.TileSizeOption`, `TileSize : float`, `TileRadius : int`

### ClutterEntry (class)
Represents a single weighted entry in a `ClutterDefinition`
- `Prefab : GameObject`, `Model : Model`, `Weight : float`

### ClutterGridSystem (class)
Game object system that manages clutter generation
- `ClearAllPainted(  ) → void`, `Flush(  ) → void`, `ClearComponent( Clutter.ClutterComponent component ) → void`, `InvalidateTilesInBounds( BBox bounds ) → void`, `Storage : Clutter.ClutterGridSystem.ClutterStorage`

### ClutterInstance (struct)
Represents a single clutter instance to be spawned
- `Transform : Transform`, `Entry : Clutter.ClutterEntry`, `IsModel : bool`

### Scatterer (class)
Base class to override if you want to create custom scatterer logic
- `static GenerateSeed( int baseSeed, int x, int y ) → int`, `Scatter( BBox bounds, Clutter.ClutterDefinition clutter, int seed, Scene scene ) → List<Clutter.ClutterInstance>`

### SimpleScatterer (class)
- `Scale : RangedFloat`, `Density : float`, `PlaceOnGround : bool`

### SlopeMapping (class)
Maps an clutter entry to a slope angle range
- `MinAngle : float`, `MaxAngle : float`, `EntryIndex : int`

### SlopeScatterer (class)
Scatterer that filters and selects assets based on the slope angle of the surface
- `Scale : RangedFloat`, `Density : float`, `HeightOffset : float`

### TerrainMaterialMapping (class)
Maps a terrain material to a list of clutter entries that can spawn on it
- `Material : TerrainMaterial`, `EntryIndices : List<int>`

### TerrainMaterialScatterer (class)
Scatterer that selects assets based on the terrain material at the hit position
- `Scale : RangedFloat`, `Density : float`, `HeightOffset : float`

## Sandbox.Volumes

### SceneVolume (struct)
A generic way to represent volumes in a scene
- `GetVolume(  ) → float`, `GetBounds(  ) → BBox`, `DrawGizmos( bool withControls ) → void`, `Test( Vector3 position ) → bool`

### VolumeSystem (class)
A base GameObjectSystem for handling of IVolume components
- `FindSingle( Vector3 position ) → T`, `FindAll( Vector3 position ) → IEnumerable<T>`

## Sandbox.Bind

### BindSystem (class)
Data bind system, bind properties to each other
- `Name : string`, `ThrottleUpdates : bool`, `CatchExceptions : bool`, `Tick(  ) → void`, `Flush(  ) → void`

### Builder (struct)
A helper to create binds between two properties (or whatever you want) Example usage: set "BoolValue" from value of "St…
- `ReadOnly( bool makeReadOnly ) → Bind.Builder`, `Set( Bind.Proxy binding ) → Bind.Builder`, `From( Bind.Proxy source ) → Bind.Link`, `FromObject( object obj ) → Bind.Link`

### Link (class)
Joins two proxies together, so one can be updated from the other (or both from each other)
- `IsValid : bool`, `OneWay : bool`, `Left : Bind.Proxy`

### Proxy (class)
Gets and Sets a value from somewhere
- `Name : string`, `Value : object`, `CanRead : bool`

## Sandbox.Compression

### LZ4 (class)
Encode and decode LZ4 compressed data
- `static DecompressFrame( ReadOnlySpan<byte> data ) → byte[]`, `static CompressBlock( ReadOnlySpan<byte> data, IO.Compression.CompressionLevel compressionLevel ) → byte[]`, `static DecompressBlock( ReadOnlySpan<byte> src, Span<byte> dest ) → int`, `static CompressFrame( ReadOnlySpan<byte> data, IO.Compression.CompressionLevel compressionLevel ) → byte[]`

## Sandbox.DataModel

### GameSetting (struct)
A `ConVarAttribute` that has been marked with `GameSetting` This is stored as project metadata so we can set up a game …
- `Name : string`, `Title : string`, `Group : string`

### ProjectConfig (class)
Configuration of a `Project`
- `Directory : IO.DirectoryInfo`, `AssetsDirectory : IO.DirectoryInfo`, `Title : string`, `ToJson(  ) → string`, `TryGetMeta( string keyname, T outvalue ) → bool`

## Sandbox.Html

### INode (interface)
- `IsElement : bool`, `IsText : bool`, `IsComment : bool`, `static Parse( string html ) → Html.INode`, `GetAttribute( string name, string def ) → string`

## Sandbox.Helpers

### UndoSystem (class)
A system that aims to wrap the main reusable functionality of an undo system
- `Undo(  ) → bool`, `Redo(  ) → bool`, `Initialize(  ) → void`, `Snapshot( string changeTitle ) → void`, `Back : Collections.Generic.Stack<Helpers.UndoSystem.Entry>`

## Sandbox.Menu

### LoadingProgress (struct)
- `Fraction : double`, `Mbps : double`, `Percent : double`, `CalculateETA(  ) → TimeSpan`

## Sandbox.Modals

### CreateGameOptions (struct)
Passed to IModalSystem
- `Package : Package`, `OnComplete : Action<Modals.CreateGameResults>`

### CreateGameResults (struct)
- `MapIdent : string`, `MaxPlayers : int`, `ServerName : string`

### FriendsListModalOptions (struct)
- `ShowOfflineMembers : bool`, `ShowOnlineMembers : bool`

### IModalSystem (interface)
- `PauseMenu(  ) → void`, `HasModalsOpen(  ) → bool`, `PlayerList(  ) → void`, `CloseAll( bool immediate ) → void`, `IsModalOpen : bool`

### ServerListConfig (struct)
- `GamePackageFilter : string`, `MapPackageFilter : string`

### WorkshopPublishOptions (struct)
Passed to IModalSystem
- `Title : string`, `Description : string`, `Thumbnail : Bitmap`, `AddCategory( string name ) → void`

## Sandbox.Mounting

### Directory (class)
- `static GetAll(  ) → Mounting.MountInfo[]`, `static Get( string name ) → Mounting.BaseGameMount`, `static Mount( string name ) → Task<Mounting.BaseGameMount>`

### MountInfo (struct)
Information about a single mount
- `Ident : string`, `Title : string`, `Available : bool`

### MountUtility (class)
- `static FindLoader( string loaderPath ) → Mounting.ResourceLoader`, `static GetPreviewTexture( string loaderPath ) → Texture`, `static GetPreviewTexture( Mounting.ResourceLoader loader ) → Texture`

### PrefabBuilder (class)
A scoped builder for creating prefabs within a Mount
- `static Destroy( PrefabFile prefab ) → void`, `Scope(  ) → Mounting.PrefabBuildScope`, `Create(  ) → PrefabFile`, `WithName( string name ) → Mounting.PrefabBuilder`

### PrefabBuildScope (struct)
Disposable scope that manages a temporary scene for `PrefabBuilder`
- `Dispose(  ) → void`

### TextureLoader (class)
- `static FromDds( ReadOnlySpan<byte> bytes ) → Texture`

## Sandbox.Razor

### RenderTreeBuilderOld (class)

## Sandbox.Speech

### Recognition (class)
- `IsListening : bool`, `IsSupported : bool`, `static Stop(  ) → void`, `static Start( Speech.Recognition.OnSpeechResult callback, IEnumerable<string> choices ) → void`

### SpeechRecognitionResult (struct)
A result from speech recognition
- `Confidence : float`, `Text : string`, `Success : bool`

### Synthesizer (class)
A speech synthesis stream
- `WithBreak(  ) → Speech.Synthesizer`, `Play(  ) → SoundHandle`, `TrySetVoice( string voiceName ) → Speech.Synthesizer`, `WithText( string input ) → Speech.Synthesizer`, `CurrentVoice : string`

## Sandbox.Tasks

### SyncTask (struct)
- `GetResult(  ) → void`, `GetAwaiter(  ) → Tasks.SyncTask`, `OnCompleted( Action continuation ) → void`, `IsCompleted : bool`

---

*Pulled from the s\&box engine's managed API surface at version 26.08.05: 738 types spread across 32 namespaces, operator overloads aside.*