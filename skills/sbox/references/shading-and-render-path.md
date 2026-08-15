<!--
  s&box Skill : shading-and-render-path.md

  Shader authoring and the render path: .shader files, materials, render attributes and layers.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Shaders and Rendering

Covers `.shader` source authoring, the HLSL entry points s&box shaders actually use, setting
shader/material parameters from C#, immediate-mode drawing with `Graphics`/`CommandList`, custom
render objects, and render layering. Read out of engine source at version 26.08.05. Paths below
are repo-relative to the s&box source tree (`game/`, `engine/`).

Key files read to ground this document: `game/templates/{unlit,material,compute}.shader`,
`game/templates/default.vmat`, `game/addons/base/Assets/shaders/{glass,ui_basic}.shader` and
`common/*.hlsl`, `game/core/shaders/{system,sbox_vertex,sbox_pixel}.fxc`,
`engine/Sandbox.Engine/Resources/{Material,Shader}/*.cs`,
`engine/Sandbox.Engine/Systems/Render/{RenderAttributes,Graphics*,ComputeShader}.cs`,
`engine/Sandbox.Engine/Systems/Render/CommandList/*.cs`,
`engine/Sandbox.Engine/Systems/SceneSystem/{SceneCustomObject,SceneObject.Layer}.cs`,
`engine/Sandbox.Engine/Scene/Components/Camera/CameraComponent*.cs`,
`engine/Sandbox.Engine/Scene/Components/Render/Renderer.cs`.

---

## 1. The asset picture

Three file types, three roles:

| Extension | What it is | Example |
|---|---|---|
| `.shader` | Source. A block-structured DSL wrapping HLSL, hand-written or exported by the ShaderGraph editor | `game/addons/base/Assets/shaders/glass.shader` |
| `.shader_c` | Compiled shader (all static combos baked). What actually ships | referenced directly in `game/core/materials/error.vmat`: `shader "shaders/error.shader_c"` |
| `.vmat` | Material. References one shader by path and holds parameter overrides (textures, floats, vectors) | `game/templates/default.vmat` |

Verified from `game/core/materials/*.vmat`: the `shader` key in a `.vmat` is a project-relative
path such as `"shaders/complex.shader"` or `"shaders/error.shader_c"`, i.e. a `shaders/` folder
sitting next to `materials/` in the asset tree. `game/templates/default.vmat` (marked
`// THIS FILE IS AUTO-GENERATED`) uses a bare `shader "complex.vfx"` as a template placeholder;
real materials in this tree reference `.shader` or `.shader_c` paths, not `.vfx`. Don't treat
`.vfx` as a file extension you author against, it doesn't appear as a real asset path anywhere in
this source tree.

A `.vmat` is organized into layers (`Layer0 { ... }`), each holding `key "value"` pairs matching
the shader's exposed parameter names (`g_flMetalness`, `TextureColor`, `g_vColorTint`, etc, see
`game/templates/default.vmat`). Which keys are valid is entirely determined by what the shader
declares with `UiGroup`/annotation blocks (section 3).

Engine-shipped shader templates worth starting from:
- `game/templates/unlit.shader`: minimal forward+depth unlit shader
- `game/templates/material.shader`: minimal PBR shader using the standard shading model
- `game/templates/compute.shader`: minimal compute shader

The full shipped shader library lives at `game/addons/base/Assets/shaders/*.shader` (glass,
foliage, terrain, postprocess, UI, several `_cs.shader` compute shaders). Shared HLSL used by
those lives under `game/addons/base/Assets/shaders/common/`. Lower-level, non-project includes
(`system.fxc`, `sbox_vertex.fxc`, `sbox_pixel.fxc`, `vr_*.fxc`) live in `game/core/shaders/`.

**ShaderGraph** (`game/editor/ShaderGraph/`, node types in `game/addons/tools/Code/ShaderGraph/`)
is the visual node editor. It compiles a node graph down to the same `.shader` text format shown
below, edited in the Shader Editor tool. No separate public runtime C# API was found for it beyond
what's covered here for `Material`/`Shader`; it's an authoring tool, not a runtime surface.

---

## 2. `.shader` file anatomy

Grounded directly in `game/templates/unlit.shader`, the full minimal shader:

```c
FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput { #include "common/vertexinput.hlsl" };
struct PixelInput  { #include "common/pixelinput.hlsl" };

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		return float4( 1, 1, 1, 1 );
	}
}
```

Block meaning, verified against multiple shipped shaders:

| Block | Purpose |
|---|---|
| `HEADER` | Optional. `Description`, `Version`, `DevShader = true;` (seen in `ui_basic.shader`) |
| `FEATURES` | Compile-time options exposed in the material editor's shader combo box. Declared with `Feature( F_NAME, range, "UI Group" )` |
| `MODES` | Which render passes this shader participates in: `Forward()`, `Depth()` / `Depth( S_MODE_DEPTH )`, `Default()` (compute/utility shaders), `VrForward()`, `ToolsShadingComplexity( "path.shader" )` |
| `COMMON` | Code shared between `VS` and `PS`, always starts with `#include "common/shared.hlsl"` |
| `struct VertexInput` / `struct PixelInput` | Interstage structs. Bodies are almost always just `#include "common/vertexinput.hlsl"` / `pixelinput.hlsl"`, extended with extra members when needed (`glass.shader` adds `bool bIsFrontface : SV_IsFrontFace;` inside its own `#if` guard) |
| `VS` | Vertex shader code, entry point `MainVs` |
| `PS` | Pixel shader code, entry point `MainPs`, returns `SV_Target0` |
| `CS` | Compute shader code, entry point `MainCs`, see below |

`game/templates/material.shader` is the same skeleton with a real pixel shader body:

```c
PS
{
    #include "common/pixel.hlsl"

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		/* m.Metalness = 1.0f; // Forces the object to be metalic */
		return ShadingModelStandard::Shade( m );
	}
}
```

That's the minimum viable PBR shader: build a `Material` struct from the interpolated pixel
input, optionally override fields, hand it to `ShadingModelStandard::Shade`.

Compute shaders use a `CS` block instead of `VS`/`PS`, verified from `game/templates/compute.shader`:

```c
MODES
{
	Default();
}

CS
{
	#include "system.fxc"

	RWTexture2D<float4> Result < Attribute( "Result" ); >;

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		Result[ id.xy ] = float4( id.x & id.y, ( id.x & 15 ) / 15.0, ( id.y & 15 ) / 15.0, 0.0 );
	}	
}
```

### Features, static combos, dynamic combos

Three distinct mechanisms, all seen together in `glass.shader`:

```c
FEATURES
{
    #include "common/features.hlsl"
    Feature( F_GLASS_QUALITY, 0..2( 0 ="Default Glass ( Refractive, Tinted )", 1 = "Simple Glass ( Faster To Render )", 2 = "Layered Glass ( Multi-Layer Compositing )" ), "Glass");
    Feature( F_OVERLAY_LAYER, 0..1, "Glass");
}
```

```c
PS
{
    StaticCombo( S_GLASS_QUALITY, F_GLASS_QUALITY, Sys( ALL ) );
    StaticCombo( S_OVERLAY_LAYER, F_OVERLAY_LAYER, Sys( ALL ) );
    StaticCombo( S_MODE_DEPTH, 0..1, Sys(ALL) );

    DynamicCombo( D_SKYBOX, 0..1, Sys(PC) );
```

- `F_*` (feature): a material-level choice, set per-material, baked into the exported `.vmat`
  and shown in the material editor.
- `S_*` (static combo): a shader permutation, resolved at compile time. Usually tied straight to
  an `F_*` feature (`StaticCombo( S_GLASS_QUALITY, F_GLASS_QUALITY, Sys(ALL) )`), but can also be
  declared with its own range (`StaticCombo( S_MODE_DEPTH, 0..1, Sys(ALL) )`).
- `D_*` (dynamic combo): resolved per-draw-call at runtime without recompiling, set from C# with
  `RenderAttributes.SetCombo` (section 4). `common/vertex.hlsl` declares
  `DynamicCombo( D_BAKED_LIGHTING_FROM_LIGHTMAP, 0..1, Sys( ALL ) )` this way.

`Feature`, `StaticCombo`, `DynamicCombo`, `RenderState`, `Attribute` and friends are not ordinary
C preprocessor macros, they're special syntax the s&box shader compiler parses directly. The
`#define StaticCombo( comboName, range, sys ) ;` style no-ops in `game/core/shaders/system.fxc`
only exist, per that file's own comment, to "allow fxc to compile out combo and rule
declarations" when the block is preprocessed outside the real shader compiler. Don't read the
`system.fxc` defines as the actual semantics.

---

## 3. HLSL entry points, structs, and includes

### Vertex/pixel interstage data

`common/vertexinput.hlsl` (included inside `struct VertexInput { ... }`), verified fields:

```c
float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
float2 vTexCoord2 : TEXCOORD1 < Semantic( LowPrecisionUv1 ); >;	
float4 vNormalOs : NORMAL < Semantic( OptionallyCompressedTangentFrame ); >;	

#if ( VS_INPUT_HAS_TANGENT_BASIS )
float4 vTangentUOs_flTangentVSign : TANGENT < Semantic( TangentU_SignV ); >;
#endif
```

Plus, gated behind combos: `vBlendWeight` (compute skinning), `nInstanceTransformID`,
`nBoneIndex`, `vLightmapUV` (baked lighting).

`common/pixelinput.hlsl` (included inside `struct PixelInput { ... }`), verified fields:

```c
#if ( PROGRAM == VFX_PROGRAM_PS )
	float3 vPositionWithOffsetWs : TEXCOORD0;
#else
	float3 vPositionWs : TEXCOORD0;
#endif

float3 vNormalWs 		: TEXCOORD1;
float4 vTextureCoords 	: TEXCOORD2;
float4 vVertexColor 	: TEXCOORD4;

#if ( PS_INPUT_HAS_TANGENT_BASIS )
	float3 vTangentUWs : TEXCOORD6; 
	float3 vTangentVWs : TEXCOORD7; 
#endif

centroid float2 vLightmapUV : TEXCOORD3;
```

Plus `vPositionPs : SV_Position` on the VS side, `vPositionSs : SV_Position` on the PS side.

### Entry point helpers

`common/vertex.hlsl` includes `sbox_vertex.fxc`, which defines the two functions every `MainVs`
calls (`game/core/shaders/sbox_vertex.fxc`):

```c
PixelInput ProcessVertex( VertexInput i )
{
    return VS_CommonProcessing( i );
}

PixelInput FinalizeVertex( PixelInput i )
{
    return VS_CommonProcessing_Post( i );
}
```

`VS_CommonProcessing`/`_Post` themselves live in `vr_common_vs_code.fxc` and do the standard
object-to-world/clip transform, skinning, instancing. You don't need to look inside them for
ordinary shader work, call `ProcessVertex` then your own code then `FinalizeVertex`.

`common/pixel.hlsl` includes `sbox_pixel.fxc` (sets up default `RenderState` for depth/blend, see
section on render states below), plus `common/material.hlsl` and `common/shadingmodel.hlsl`.

### The `Material` struct

`common/material.hlsl` (`COMMON_PIXEL_MATERIAL_H`), the shared surface-description struct every
standard shader fills in and hands to the shading model:

```c
class Material
{
    float3 Albedo;
    float  Metalness;
    float  Roughness;
    float3 Emission;
    float3 Normal;                  // World normal
    float  TintMask;
    float  AmbientOcclusion;
    float3 Transmission;
    float  Opacity;

    float3 WorldPosition;
    float3 WorldPositionWithOffset; // World position relative to the camera
    float4 ScreenPosition;          // SV_Position

    float3 TangentNormal;
    float3 WorldTangentU;
    float3 WorldTangentV;
    float2 LightmapUV;

    float2 TextureCoords;

    static Material Init( float3 RelativeWorldPosition = 0.0f, float4 ScreenPosition = 0.0f );
    static Material Init( PixelInput i );   // only when PixelInput is defined
    static Material lerp( Material a, Material b, float amount );
    static Material From( PixelInput i );   // legacy, needs Material.CommonInputs.hlsl included
};
```

`Material::Init( PixelInput i )` seeds position/normal/tangent/UV from the interpolated vertex
data and leaves albedo/roughness/etc at defaults (white albedo, roughness 1, non-metal). You fill
in the rest yourself by sampling textures, or use `Material::From( i )`, which additionally
requires `#include "common/utils/Material.CommonInputs.hlsl"` and samples the standard
`g_tColor`/`g_tNormal`/`g_tRma` textures for you (see below).

`common/shadingmodel.hlsl` defines `ShadingModelStandard::Shade( Material m ) : float4`, which
converts `Material` to the internal `CombinerInput`/lighting-term format, runs direct + indirect
lighting, composites atmospherics, and handles debug tools-vis / wireframe / depth-normals output
modes. This is the function every PBR shader's `MainPs` ends with.

### Standard texture inputs

`common/utils/Material.CommonInputs.hlsl` is what `material.shader`-style shaders pull in for the
conventional Color/Normal/Roughness/Metalness/AO texture set:

```c
CreateInputTexture2D(TextureColor, Srgb, 8, "", "_color", "Material,10/10", Default3(1.0, 1.0, 1.0));
CreateInputTexture2D(TextureNormal, Linear, 8, "NormalizeNormals", "_normal", "Material,10/20", Default3(0.5, 0.5, 1.0));
CreateInputTexture2D(TextureRoughness, Linear, 8, "", "_rough", "Material,10/30", Default(0.5));
CreateInputTexture2D(TextureMetalness, Linear, 8, "", "_metal", "Material,10/40", Default(1.0));
CreateInputTexture2D(TextureAmbientOcclusion, Linear, 8, "", "_ao", "Material,10/50", Default(1.0));

Texture2D g_tColor < Channel(RGB, AlphaWeighted(TextureColor, TextureTranslucency), Srgb); Channel(A, Box(TextureTranslucency), Linear); OutputFormat(BC7); SrgbRead(true); > ;
Texture2D g_tNormal < Channel(RGB, Box(TextureNormal), Linear); Channel(A, Box(TextureTintMask), Linear); OutputFormat(BC7); SrgbRead(false); > ;
Texture2D g_tRma < Channel(R, Box(TextureRoughness), Linear); Channel(G, Box(TextureMetalness), Linear); Channel(B, Box(TextureAmbientOcclusion), Linear); Channel(A, Box(TextureBlendMask), Linear); OutputFormat(BC7); SrgbRead(false); > ;
```

`CreateInputTexture2D` declares an *artist-facing* input (what shows up per-channel in the texture
compiler/material editor); `g_tColor`/`g_tNormal`/`g_tRma` are the actual packed runtime textures
the shader samples, built from those inputs at compile time via `Channel(...)`. This is why a
`.vmat` sets `TextureColor "materials/default/default_color.tga"` (an *input* name) rather than
`g_tColor` directly, verified in `game/templates/default.vmat`.

### Annotation macros (`< ... >` blocks)

All verified in `game/core/shaders/system.fxc`. These appear inside `< >` after a variable
declaration and control material-editor UI, defaults, ranges, and render/sampler state:

| Macro | Use |
|---|---|
| `Attribute( "Name" )` | Binds this shader variable to a C# `RenderAttributes`/`Material` key of that name (see section 4) |
| `Default( x )`, `Default2/3/4( ... )` | Default value shown in the material editor |
| `Range( min, max )`, `Range2/3/4` | Slider bounds |
| `UiGroup( "Group,order" )` | Material editor grouping/ordering, e.g. `"Material,10/10"` |
| `UiType( Color )` | Editor widget hint |
| `SrgbRead( bool )` | Whether the sampled texture is treated as sRGB |
| `Channel( dstChannels, mipAlgorithm, outputColorSpace )`, `OutputFormat( fmt )` | How the texture compiler packs an input into a runtime texture |
| `Source( arg )`, `SourceArg( arg )` | Bind to an engine-provided constant, e.g. `float4 g_vViewport < Source( Viewport ); >;` (`ui_basic.shader`) |
| `RenderState( Name, Value )` | Fixed-function state: `RenderState( BlendEnable, true )`, `RenderState( DepthWriteEnable, false )`, `RenderState( CullMode, NONE )` |
| `Filter(...)`, `AddressU/V/W(...)`, `MaxAniso(...)` | `SamplerState` declaration parameters |
| `BoolAttribute`/`FloatAttribute`/`Float3Attribute`/`TextureAttribute( name, val )` | Declares a material-system attribute other engine systems query by name (e.g. `BoolAttribute(translucent, true)`, `BoolAttribute(alphatest, true)`) |

`ui_basic.shader`'s `PS` block shows several of these together: `Texture2D g_tColor <
Attribute( "Texture" ); SrgbRead( true ); >;` alongside `float4 g_vViewport < Source( Viewport ); >;`
and fixed-function state via `RenderState( CullMode, NONE ); RenderState( DepthWriteEnable, false );`.
`Attribute( "Texture" )` on `g_tColor` there is exactly what `Graphics.DrawText` targets from C#
with `Attributes.Set( "Texture", texture )` (`engine/Sandbox.Engine/Systems/Render/Graphics.Draw.cs`).

---

## 4. Setting shader parameters from C#

### Material

`Sandbox.Material` (`engine/Sandbox.Engine/Resources/Material/Material.cs`) wraps a native
material resource. Every material exposes `Attributes` (a `RenderAttributes`, see below) plus
direct parameter setters:

```csharp
public static Material Load( string filename );                 // cached load from disk
public static Task<Material> LoadAsync( string filename );
public static Material Create( string materialName, string shader, bool anonymous = true );
public static Material FromShader( Shader shader );              // cached per-shader empty material
public static Material FromShader( string path );
public Material CreateCopy( string name = null );

public string Name { get; }
public string ShaderName { get; }                                 // native.GetString("shader", "invalid")
public Shader Shader { get; set; }
public RenderAttributes Attributes { get; }

public Texture GetTexture( string name );
public Vector4 GetVector4( string name );
public Color GetColor( string name );

public bool Set( string param, Vector4 value );
public bool Set( string param, Texture texture );
public bool Set( string param, Color value );
public bool Set( string param, Vector3 value );
public bool Set( string param, Vector2 value );
public bool Set( string param, float value );
public bool Set( string param, int value );
public bool Set( string param, bool value );

public void SetFeature( string name, int value );  // F_ feature combo, triggers ReloadStaticCombos
public int GetFeature( string name );
```

`Material.Create` asserts it isn't called mid-render (`if ( Graphics.IsActive ) throw ...`), it's
a main-thread-only, outside-the-render-loop operation. `Material.Set( string, Texture )` has a
compatibility shim: on old `Application.GamePackage.ApiVersion < 22` projects, a param name not
already starting with `g_t` gets `g_t` prefixed automatically. New code should pass the exact
shader-declared name.

```csharp
var mat = Material.Load( "materials/mymat.vmat" );
mat.Set( "g_flOpacityScale", 0.5f );
mat.Set( "TextureColor", myTexture );
mat.SetFeature( "F_GLASS_QUALITY", 2 );
```

### RenderAttributes

`Sandbox.RenderAttributes` (`engine/Sandbox.Engine/Systems/Render/RenderAttributes.cs`) is the
general key/value bag passed into any draw call, bound to shader variables through the
`Attribute( "Name" )` annotation shown above. Its own doc comment gives the canonical example:

```hlsl
float4 CornerRadius < Attribute( "BorderRadius" ); >;
Texture2D g_tColor 	< Attribute( "Texture" ); SrgbRead( false ); >;
```

```csharp
public RenderAttributes();                    // standalone, e.g. Renderer.Attributes fallback

public void Set( StringToken k, int/uint/float/double/bool/string value );
public void Set( StringToken k, Vector2/Vector3/Vector4/Vector2Int/Vector3Int/Angles/Matrix value );
public void Set( StringToken k, Texture value, int mip = -1 );
public void Set( StringToken k, SamplerState value );
public void Set( StringToken k, GpuBuffer value );
public void SetData<T>( StringToken k, T value ) where T : unmanaged;       // constant buffer
public void SetData<T>( StringToken k, Span<T>/T[]/List<T> value ) where T : unmanaged;

public void SetCombo( StringToken k, int value );      // D_ dynamic combo
public void SetCombo( StringToken k, bool value );
public void SetComboEnum<T>( StringToken k, T value ) where T : unmanaged, Enum;

public bool/float/int/uint/Vector3/Vector4/Angles/Matrix/Texture Get...( StringToken name, T defaultValue = default );

public void Clear();
```

Overloads also accept plain `string` keys (converted to `StringToken` internally) for legacy call
sites. `SetCombo`/`SetComboEnum` are how you flip a `D_*` dynamic combo from C#, e.g.
`Graphics.DrawRoundedRectangle` sets `Attributes.SetCombo( "D_BACKGROUND_IMAGE", 0 )`.

Every `Renderer`-derived component (`ModelRenderer`, `SkinnedModelRenderer`, etc, all inheriting
`Sandbox.Renderer` in `engine/Sandbox.Engine/Scene/Components/Render/Renderer.cs`) exposes an
`Attributes` property that forwards to the underlying `SceneObject`'s attributes once it exists:

```csharp
var renderer = go.Components.Get<ModelRenderer>();
renderer.Attributes.Set( "g_flOpacityScale", 0.25f );
```

Per that source file's doc comment: renderer attributes are not saved to disk and are not cloned
when copying the renderer.

`ComputeShader` (below) has its own `Attributes` property used by `Dispatch()`.

---

## 5. `Graphics` and `CommandList` immediate drawing

### `Graphics` (static, `engine/Sandbox.Engine/Systems/Render/Graphics.cs`)

Only usable while `Graphics.IsActive` is true, i.e. inside an active render callback (a
`SceneCustomObject.RenderSceneObject`, a `CommandList` entry executing on the render thread, a
camera hook). Calling most `Graphics` methods outside that context throws
(`Graphics.AssertRenderBlock`).

```csharp
public static bool IsActive { get; }
public static SceneLayerType LayerType { get; }
public static RenderAttributes Attributes { get; }        // current render context's attributes
public static Transform CameraTransform { get; }
public static Vector3 CameraPosition { get; }
public static Rotation CameraRotation { get; }
public static Rect Viewport { get; set; }
public static RenderTarget RenderTarget { get; set; }     // binds render target + resizes viewport
public static Frustum Frustum { get; }
public static float FieldOfView { get; }

public static void Clear( Color color, bool clearColor = true, bool clearDepth = true, bool clearStencil = true );
public static RenderTarget GrabFrameTexture( string targetName = "FrameTexture", RenderAttributes attrs = null, DownsampleMethod downsampleMethod = DownsampleMethod.None, int maxMips = 0 );
public static RenderTarget GrabDepthTexture( string targetName = "DepthTexture", RenderAttributes attrs = null );
public static void CopyTexture( Texture src, Texture dst );  // format/size must match
public static void FlushGPU();

public static void Render( SceneObject obj, Transform? transform = null, Color? color = null, Material material = null );
public static void Blit( Material material, RenderAttributes attributes = null );          // fullscreen quad
public static void DrawQuad( in Rect rect, in Material material, in Color color, RenderAttributes attributes = null );
public static void Draw( Span<Vertex> vertices, int vertCount, Material material, RenderAttributes attrs = null, PrimitiveType primitiveType = PrimitiveType.Triangles );
public static void DrawModel( Model model, Transform transform, RenderAttributes attributes = null );
public static void DrawModelInstanced( Model model, Span<Transform> transforms, RenderAttributes attributes = null );
public static Rect DrawText( in Rect position, string text, Color color, ... );
```

`DrawModelInstanced`/`DrawModelInstancedIndirect` use standard-shader GPU instancing; the shader
side reads `GetTransformMatrix( int instance )` (referenced directly in these methods' doc
comments in `Graphics.Draw.Mesh.cs`), with a documented cap of 1,048,576 transform slots/frame.

### `Sandbox.Rendering.CommandList`

`CommandList` (`engine/Sandbox.Engine/Systems/Render/CommandList/CommandList.cs`) is a recorded,
replayable list of render commands: you call its methods any time (off the render thread, before
the frame even starts rendering), each call appends an `Entry` to an internal list, and
`ExecuteOnRenderThread()` runs them all later, in order, on the render thread. This is why its
API mirrors `Graphics` almost 1:1, every `CommandList` method is a deferred wrapper around the
matching `Graphics` call:

```csharp
public CommandList( string debugName = null );
public bool Enabled { get; set; }
public HudPainter Paint { get; }                     // 2D drawing, see component-library.md HudPainter

public void Reset();                                  // clear all recorded entries
public void Blit( Material material, RenderAttributes attributes = null );
public void DrawQuad( Rect rect, Material material, Color color );
public void DrawModel( Model model, Transform transform, RenderAttributes attributes = null );
public void DrawModelInstanced( Model model, Span<Transform> transforms, RenderAttributes attributes = null );
public void Draw<T>( GpuBuffer<T> vertexBuffer, Material material, ... ) where T : unmanaged;
public void DrawIndexed<T>( GpuBuffer<T> vertexBuffer, GpuBuffer indexBuffer, Material material, ... ) where T : unmanaged;
public void DrawRenderer( Renderer renderer, RendererSetup rendererSetup = default );
public void DrawView( CameraComponent camera, RenderTargetHandle target, ViewSetup viewSetup = default );
public void DrawReflection( CameraComponent camera, Plane plane, in RenderTargetHandle target, ReflectionSetup setup = default );
public void DrawRefraction( CameraComponent camera, Plane plane, in RenderTargetHandle target, RefractionSetup setup = default );

public RenderTargetHandle GetRenderTarget( string name, ImageFormat format, int numMips = 1, int sizeFactor = 1 );  // + overloads for width/height, msaa
public void SetRenderTarget( RenderTargetHandle handle );
public void ReleaseRenderTarget( RenderTargetHandle handle );

public void DispatchCompute( ComputeShader compute, int threadsX, int threadsY, int threadsZ );
public void DispatchCompute( ComputeShader compute, RenderTargetHandle.SizeHandle dimension );
public void DispatchComputeIndirect( ComputeShader compute, GpuBuffer indirectBuffer, uint offset = 0 );

public void InsertList( CommandList otherBuffer );    // nest one list inside another
public void ExecuteOnRenderThread();                  // called by the engine, not usually by you
```

`RenderTargetHandle` (`ref struct`, name-keyed, not the render target itself) has `.ColorTexture`,
`.DepthTexture`, `.ColorIndex`, `.Size` accessors for feeding one command's output into the next
command's `Attribute`-bound texture input.

### Hooking a `CommandList` into a camera

`CameraComponent.AddCommandList` (`engine/Sandbox.Engine/Scene/Components/Camera/CameraComponent.Commands.cs`)
is the real, current mechanism for injecting custom render passes:

```csharp
public void AddCommandList( CommandList buffer, Stage stage, int order = 0 );
public void RemoveCommandList( CommandList buffer, Stage stage );
public void ClearCommandLists( Stage stage );
```

`Sandbox.Rendering.Stage` (`engine/Sandbox.Engine/Systems/Render/Stage.cs`), verified values:

```csharp
public enum Stage
{
	AfterDepthPrepass = 1000,
	AfterOpaque = 2000,
	AfterSkybox = 3000,
	AfterTransparent = 4000,
	AfterViewmodel = 5000,
	BeforePostProcess = 6000,
	Tonemapping = 6500,
	AfterPostProcess = 7000,
	AfterUI = 8000,
}
```

Lists registered for the same `Stage` run in ascending `order`. Typical pattern: build a
`CommandList` once (e.g. in `OnStart`), record whatever draw/dispatch calls you need, and register
it on the camera:

```csharp
var cmds = new CommandList( "MyEffect" );
cmds.Blit( myMaterial );
Scene.Camera.AddCommandList( cmds, Stage.AfterTransparent );
```

See section 8 for the older `AddHookAfterOpaque`-style API, it still compiles but does nothing.

---

## 6. Custom render objects and render hooks

### `SceneCustomObject`

`Sandbox.SceneCustomObject` (`engine/Sandbox.Engine/Systems/SceneSystem/SceneCustomObject.cs`)
is a `SceneObject` whose rendering is entirely your code, for drawing things the component system
has no renderer for:

```csharp
public class SceneCustomObject : SceneObject
{
    public SceneCustomObject( SceneWorld sceneWorld );
    public Action<SceneObject> RenderOverride;              // called by default RenderSceneObject
    public virtual void RenderSceneObject();                // override, or set RenderOverride
}
```

Bounds default to infinite (`native.SetBoundsInfinite()`) so a forgotten/unset bounds doesn't
just make the object invisible. Inside `RenderSceneObject`/`RenderOverride`, `Graphics.IsActive`
is true and the whole `Graphics` API above is available:

```csharp
var obj = new SceneCustomObject( Scene.SceneWorld );
obj.RenderOverride = so =>
{
    Graphics.Draw( vertices, vertices.Length, myMaterial );
};
```

### `Renderer` base class (what `ModelRenderer` etc actually are)

`Sandbox.Renderer` (abstract, `engine/Sandbox.Engine/Scene/Components/Render/Renderer.cs`) is the
base every built-in renderer component derives from. Besides `Attributes` (section 4), it exposes
per-instance command list hooks that run immediately before/after that specific renderer draws:

```csharp
public CommandList ExecuteBefore { get; set; }
public CommandList ExecuteAfter { get; set; }
protected void BackupRenderAttributes( RenderAttributes attributes );
protected void RestoreRenderAttributes( RenderAttributes attributes );
```

These run via `SceneObjectCallbacks.OnBeforeObjectRender`/`OnAfterObjectRender`, i.e. once per
render of that specific scene object, not once per frame globally, useful for a per-object effect
(outline, custom decal pass) that shouldn't touch every other renderer in the scene.

### `ComputeShader`

`Sandbox.ComputeShader` (`engine/Sandbox.Engine/Systems/Render/ComputeShader.cs`) wraps a
compute `.shader`:

```csharp
public class ComputeShader
{
    public RenderAttributes Attributes { get; }
    public ComputeShader( string path );
    public void Dispatch( int threadsX = 32, int threadsY = 32, int threadsZ = 32 );
    public void DispatchIndirect( GpuBuffer indirectBuffer, uint indirectElementOffset = 0 );
    public void DispatchWithAttributes( RenderAttributes attributes, int threadsX, int threadsY, int threadsZ );
}
```

Per its own doc comment: called outside a graphics context, `Dispatch` runs immediately; called
inside one (mid-render), it runs async. Thread counts are automatically divided by the
`[numthreads(x,y,z)]` group size declared in the shader.

```csharp
var cs = new ComputeShader( "shaders/compute.shader" );
cs.Attributes.Set( "Result", myTexture );
cs.Dispatch( myTexture.Width, myTexture.Height, 1 );
```

---

## 7. `SceneRenderLayer`, render tags, and view layering

`Sandbox.SceneRenderLayer` (`engine/Sandbox.Engine/Systems/SceneSystem/SceneObject.Layer.cs`),
verified in full:

```csharp
public enum SceneRenderLayer
{
	Default,
	ViewModel = 10,          // drawn on top of everything else, with altered depth
	OverlayWithDepth = 20,   // after post processing, still using the scene's depth
	OverlayWithoutDepth = 30 // after post processing, no depth (draws over everything)
}
```

The setter lives on `SceneObject` itself, not on any component:

```csharp
public partial class SceneObject
{
    public SceneRenderLayer RenderLayer { get; set; }
}
```

Internally each non-default value maps to a string layer-match token (`"viewmodel"`,
`"OverlayWithDepth"`, `"OverlayWithoutDepth"`) via `native.SetLayerMatchID`.

**Verified gap**: grepping every component under `engine/Sandbox.Engine/Scene/Components/` for
`RenderLayer` turns up exactly one hit, `TextRenderer.cs`, which hardcodes
`RenderLayer = SceneRenderLayer.Default` in its own constructor. No `[Property]` anywhere exposes
`RenderLayer` for prefab/inspector editing, and no shipped `Renderer`-derived component (
`ModelRenderer`, `SkinnedModelRenderer`, `DecalRenderer`, etc) lets you set it. The only other
call sites are the engine's own `DebugOverlaySystem` draw helpers, which set it directly on
scene objects they create themselves in code. Practically: if you want something rendered in the
`ViewModel` layer, you need your own `SceneCustomObject` (or another path that hands you a raw
`SceneObject`) and set `.RenderLayer` on it in code. There is no stock-component,
prefab-authorable way to do it.

Don't confuse `SceneRenderLayer` with `Sandbox.Rendering.Stage` (section 5). `Stage` controls
*when* a `CommandList` runs relative to the render pipeline's passes (`AfterOpaque`,
`AfterViewmodel`, etc); `SceneRenderLayer` controls which pass bucket a `SceneObject` itself is
drawn into. They're unrelated enums that happen to both mention "viewmodel".

### `CameraComponent.RenderTags` / `RenderExcludeTags`

Ordinary tag-based include/exclude filtering on `CameraComponent` (documented in
`component-library.md`): a camera only renders objects whose tags satisfy `RenderTags`/don't
match `RenderExcludeTags`. This is the mechanism actually reachable from components and prefabs
for view-specific rendering (e.g. a first-person-only camera that only renders objects tagged
`viewmodel`), as distinct from the `SceneRenderLayer` enum above.

---

## 8. Gotchas

- **`CameraComponent.AddHookAfterOpaque` / `AddHookAfterTransparent` / `AddHookBeforeOverlay` /
  `AddHookAfterUI` are `[Obsolete]` no-ops that return `null`** in this engine version
  (`engine/Sandbox.Engine/Scene/Components/Camera/CameraComponent.cs`, obsoleted 09/06/2025 and
  02/10/2025). They still compile and won't error, they just do nothing. Use
  `CameraComponent.AddCommandList( CommandList, Stage, order )` instead (section 5). Any older
  sample code (including this skill's own `component-library.md`, which still lists them) is
  describing dead API.
- `StaticCombo`/`DynamicCombo`/`Feature`/`RenderState`/`Attribute` are shader-compiler syntax, not
  C preprocessor macros, don't reason about them via the no-op `#define`s in `system.fxc`.
- `Material.Create` throws if called while `Graphics.IsActive` is true, materials must be created
  outside the render loop.
- `Material.Set( string, Texture )` silently rewrites `param` to `g_t{param}` on old
  (`ApiVersion < 22`) projects if it doesn't already start with `g_t`. New code should just use
  the shader's exact declared name.
- `SceneRenderLayer.ViewModel` has no reachable `[Property]` on any shipped renderer component
  (section 7), don't assume you can set it from a prefab.
- `Graphics.*` calls outside an active render context throw via `Graphics.AssertRenderBlock()`.
  `CommandList` methods don't throw when called outside that context because they only *record*
  the call; the actual `Graphics` call (and its assert) happens later when the list executes on
  the render thread.
- `CommandList.DrawModelInstanced` copies the `transforms` span to a heap array before deferring
  (`transforms.ToArray()`), so it's safe to pass a stack-local span, but that copy has a cost if
  called every frame with large instance counts, prefer the GPU-buffer indirect variants for that.
- `Renderer.Attributes` are explicitly documented as not saved to disk and not cloned when copying
  the renderer, they're a runtime-only override layer.
- `ComputeShader.Dispatch` behaves differently depending on call context: synchronous outside a
  render block, async inside one. Don't assume dispatch results are visible immediately either way
  without the appropriate resource barrier/read-back.
- `RenderAttributes.SetCombo` only affects `D_*` dynamic combos. Setting an `F_*` feature at
  runtime requires `Material.SetFeature`, which triggers `native.ReloadStaticCombos()`, a real
  shader-permutation reload, not a per-draw-call operation.
