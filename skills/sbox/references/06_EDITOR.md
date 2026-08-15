<!--
  s&box Skill : 06_EDITOR.md

  Authoring editor extensions: EditorTool, custom inspectors, docks and the Widget UI system.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Editor Tooling

How to author editor extensions: viewport tools, custom inspectors, dockable windows,
Qt-backed UI, gizmo drawing, and asset access from editor code.

Read out of engine source at version 26.08.05 (`sbox-public`). Primary paths:
`game/addons/tools/Code/` (the Editor addon that ships the stock editor),
`engine/Sandbox.Tools/` (the `Editor` namespace: `Widget`, `Layout`, attributes,
`Asset`, `SelectionSystem`), `engine/Sandbox.Engine/Editor/Gizmos/` (`Gizmo`, in
namespace `Sandbox`), `engine/Sandbox.System/SerializedObject/CustomEditorAttribute.cs`.

This is a different surface from `references/03_UI.md`. Razor/`Panel` builds
in-game UI rendered by the game's own UI system. Everything in this file is
**Qt-backed desktop UI** that only exists inside the s&box editor process. A `Panel`
and a `Widget` share no base class, no layout model, and no styling system. Do not
mix them up.

---

## 1. Project structure: `Editor/` is a separate assembly

A project's code splits by folder, not by preprocessor symbol. There is **no
`#if EDITOR`** in s&box; that's a Unity habit and it doesn't apply here.

- `Code/` (or whatever `CodePath` in the `.sbproj` points at) compiles to the main
  game assembly. It's referenced by both the standalone game and the editor.
- `Editor/` (checked with `Project.HasEditorPath()`, source
  `engine/Sandbox.Engine/Systems/Project/Project/Project.cs`) compiles to a **second,
  separate assembly** that only loads inside the editor process. It's built by
  `EditorCompiler`, a distinct `CompileGroup` instance (see
  `Project.Compiling.cs:UpdateEditorCompiler`).
- The `Editor/` compiler adds a reference to the already-built main assembly
  (`EditorCompiler.AddReference( Compiler )`) plus `Sandbox.Tools` (where the `Editor`
  namespace types live: `Widget`, `Layout`, `Asset`, `EditorTool`, etc.) and other
  editor-only assemblies (`Facepunch.ActionGraphs`, `SkiaSharp`,
  `Microsoft.CodeAnalysis`...). The main `Code/` assembly cannot see any of this.
- Every project template ships a `global using Editor;` in `Editor/Assembly.cs`
  (see `game/templates/game.minimal/Editor/Assembly.cs`). Put that in your own
  `Editor/Assembly.cs` too, alongside `global using Sandbox;`.

**Practical effect:** anything that references `Widget`, `Layout`, `EditorTool`,
`SceneEditorSession`, `EditorUtility`, or any other `Editor`-namespace type must live
under an `Editor/` folder. Code under `Code/` cannot reference the editor assembly at
all (it doesn't exist yet when `Code/` compiles) and ships to players, so putting
editor-only logic there is both impossible for `Editor`-typed code and a shipping-size
mistake for anything else.

One exception: **`Gizmo` (static class) and `Component.DrawGizmos()` live in
`namespace Sandbox`**, inside the main engine assembly, not `Editor`. That's why a
plain gameplay `Component` can override `DrawGizmos()` and call `Gizmo.Draw.Line(...)`
from `Code/` with no `Editor/` folder involved (see §6). Everything else in this file
(`Widget`, `Layout`, `EditorTool`, docks, `[CustomEditor]`) is `Editor/`-only.

```csharp
// Editor/Assembly.cs
global using Sandbox;
global using Editor;
global using System.Collections.Generic;
global using System.Linq;
```

Addons follow the same rule: the stock editor itself is the `tools` addon at
`game/addons/tools/`, with a `Code/` folder full of `Editor`-namespace types. It's not
special-cased beyond being loaded as a foundational addon; any addon or game project
can add its own `Editor/` folder the same way.

---

## 2. `[EditorTool]` and `EditorTool` / `EditorTool<T>`

Viewport tools (the toolbar down the left of the scene view: Move, Rotate, Terrain,
NavMesh...) are `EditorTool` subclasses. There are two registration paths.

### Top-level tools: `[EditorTool]`

```csharp
[EditorTool( "tools.object-tool" )]   // shortcut identifier string, optional
[Title( "Object Select" )]
[Icon( "layers" )]
[Alias( "object" )]
[Group( "Scene" )]                    // toolbar group
[Order( -9999 )]                      // sort order within group
public class ObjectEditorTool : EditorTool
{
    public override IEnumerable<EditorTool> GetSubtools()
    {
        yield return new PositionEditorTool();
        yield return new RotationEditorTool();
        yield return new ScaleEditorTool();
    }
}
```

`EditorToolAttribute( string shortcut = "" )` has a `Hidden` property (skip toolbar
listing, e.g. `EyeDropperTool`). `EditorToolManager` finds candidates with
`EditorTypeLibrary.GetTypesWithAttribute<EditorToolAttribute>()` matched by type name
against `EditorToolManager.CurrentModeName`, and switches tool with the static
`EditorToolManager.SetTool( string name )` / `SetSubTool( string name )`.

Sub-tools returned from `GetSubtools()` (e.g. `PositionEditorTool`,
`RotationEditorTool`) don't carry `[EditorTool]` themselves, just `[Title]`/`[Icon]`/
`[Group]`/`[Order]` for their button in the sub-toolbar.

### Per-component tools: `EditorTool<T>`

These activate automatically when a `GameObject` with a matching `Component` is
selected, no attribute needed:

```csharp
public class BoxColliderTool : EditorTool<BoxCollider>
{
    IDisposable _undoScope;

    public override void OnUpdate()
    {
        var collider = GetSelectedComponent<BoxCollider>();
        if ( collider == null ) return;

        var box = BBox.FromPositionAndSize( collider.Center, collider.Scale );

        using ( Gizmo.Scope( "Box Collider Editor", collider.WorldTransform ) )
        {
            if ( Gizmo.Control.BoundingBox( "Bounds", box, out var newBox ) )
            {
                _undoScope ??= SceneEditorSession.Active.UndoScope( "Resize Box Collider" )
                    .WithComponentChanges( collider ).Push();

                collider.Center = newBox.Center;
                collider.Scale = newBox.Size;
            }

            if ( Gizmo.WasLeftMouseReleased ) { _undoScope?.Dispose(); _undoScope = null; }
        }
    }
}
```

`EditorTool<T>` (source: `Scene/Tools/ComponentEditorTool.cs`) is just
`public class EditorTool<T> : EditorTool where T : Component { }`, an empty marker
subclass. `EditorToolManager.CreateToolFor(Type)` resolves it by constructing
`typeof(EditorTool<>).MakeGenericType(componentType)` and asking
`EditorTypeLibrary.GetTypes(targetType)`, walking up `component.GetType().BaseType`
until it finds a match or hits `Component`. Multiple component tools can be active at
once (one per selected component type), unlike top-level tools where only one is
current.

### Lifecycle

| Member | Purpose |
|---|---|
| `OnEnabled()` / `OnDisabled()` | Called when the tool becomes / stops being current. |
| `OnUpdate()` | Called every editor frame while active, wrapped in a try/catch by `EditorTool.Frame`. |
| `OnSelectionChanged()` | Called when the scene selection changes (also gates `RebuildSidebarOnSelectionChange`). |
| `ShouldKeepActive()` | Return `true` to keep a component tool alive after its component leaves the selection. |
| `Dispose()` | Disposes overlay widgets and sub-tools; override to clean up your own state. |

### What's on the base class

- `Selection` → `SelectionSystem` (the current `SceneEditorSession`'s selection)
- `Scene` → the active `Scene`
- `Camera` → `CameraComponent` for the current viewport, set each frame
- `Trace` → `SceneTrace` seeded from `Gizmo.CurrentRay` / `Gizmo.RayDepth`
- `MeshTrace` → `Trace` with `.UseRenderMeshes(true, EditorPreferences.BackfaceSelection)`,
  `.WithoutTags("hidden")`, `.UsePhysicsWorld(false)`. Use this for viewport picking
  against render meshes rather than colliders.
- `GetSelectedComponent<T>()` → first `T` on a selected `GameObject`
- `CurrentTool` / `Tools` → active sub-tool and the list from `GetSubtools()`
- `AllowGameObjectSelection`, `AllowContextMenu`: toggle default viewport behavior
- `HasBoxSelectionMode()` / `HasLassoSelectionMode()` (override to opt in), with
  `OnBoxSelect(Frustum, Rect, bool isFinal)` / `OnLassoSelect(List<Vector2>, bool isFinal)`
  callbacks and helper `IsPointInLasso`
- `AddOverlay( Widget widget, TextFlag align = TextFlag.RightTop, Vector2 offset = default )`:
  parents a `Widget` into the viewport's `SceneOverlay` widget and aligns it (used
  for the camera preview window, terrain brush panel, etc.)
- `CreateToolSidebar()`, `CreateToolbarWidget()`, `CreateToolFooter()`,
  `CreateShortcutsWidget()`: override any to contribute UI; return `null` (default)
  to contribute nothing
- `GetSerializedSelection()` → `SerializedObject` (or `MultiSerializedObject` for
  multi-select) over the current selection, useful for driving a `ControlSheet`
- `BuildSceneContextMenu( Menu menu, Ray ray, SceneTraceResult? trace )`: add items
  to the viewport right-click menu

### Sidebar example

`CreateToolSidebar()` returning a `ToolSidebarWidget` (source:
`Scene/Tools/Sidebar/ToolSidebarWidget.cs`):

```csharp
public override Widget CreateToolSidebar()
{
    var sidebar = new ToolSidebarWidget( null );
    sidebar.AddTitle( "Terrain", "landscape" );

    var group = sidebar.AddGroup( "Brush Type" );
    // group is a Layout - Add widgets to it directly

    sidebar.AddShortcuts( ("Raise/Lower", "1"), ("Smooth", "2") );
    return sidebar;
}
```

---

## 3. Custom inspectors: `[CustomEditor]` and `ControlWidget`

The property grid (Inspector dock, `ControlSheet` rows) is built by asking every type
tagged `[CustomEditor]` to score itself against a `SerializedProperty`, then
constructing the highest scorer.

`CustomEditorAttribute` lives in `namespace Sandbox`
(`engine/Sandbox.System/SerializedObject/CustomEditorAttribute.cs`), not `Editor`,
but the widgets it targets are `Editor`-namespace `ControlWidget` subclasses:

```csharp
public class CustomEditorAttribute : Attribute
{
    public Type TargetType { get; }
    public Type[] WithAllAttributes { get; set; }
    public bool ForMethod { get; set; }
    public string NamedEditor { get; set; }   // matches [Editor("Name")] on the property
    public bool ForInterface { get; set; }
}
```

Scoring (`GetEditorScore`, same file): exact `TargetType` match scores highest, a base
type match scores lower, an unrelated type returns `-100` and is excluded. Multiple
`[CustomEditor]` attributes can stack on one class (`AllowMultiple = true`).

```csharp
[CustomEditor( typeof( Color ) )]
[CustomEditor( typeof( Color32 ) )]
[CustomEditor( typeof( ColorHsv ) )]
public class ColorControlWidget : ControlWidget
{
    public override bool SupportsMultiEdit => true;

    public ColorControlWidget( SerializedProperty property ) : base( property )
    {
        Layout = Layout.Row();
        Layout.Add( new ColorSwatchWidget( property ) { FixedWidth = Theme.RowHeight } );
        Layout.Add( new ColorStringWidget( property ) );
    }

    protected override void OnPaint() { /* nothing, children paint themselves */ }
}
```

`ControlWidget` (source: `engine/Sandbox.Tools/ControlWidget/ControlWidget.cs`) is
`public abstract class ControlWidget : Widget`. Key members:

| Member | Purpose |
|---|---|
| `SerializedProperty SerializedProperty` | The property this widget edits. |
| `static ControlWidget Create( SerializedProperty property )` | Resolution entry point: scores every `[CustomEditor]` type, constructs the winner. |
| `SupportsMultiEdit` | Override `true` if the widget can edit N properties with different values at once. |
| `IsWideMode` | Prefer full inspector width with label above, instead of label-left. |
| `OnValueChanged()` | Called (via a polled `Think()`/`ValueHash` each frame) when the underlying value changes externally. |
| `Prime()` | Called right after construction to force an initial `OnValueChanged`. |
| `ToClipboardString()` / `FromClipboardString(string)` | Copy/paste support. |

`ControlWidget.Create` falls back to `TryCreateGenericObjectControlWidget`, which
uses the built-in `GenericControlWidget` type (looked up by name via
`EditorTypeLibrary.GetType<ControlWidget>("GenericControlWidget")`) to auto-generate
a sub-property sheet for a plain class or struct with no dedicated editor.

`ControlObjectWidget` (source: `Widgets/ControlWidgets/ControlObjectWidget.cs`) is
the base for widgets that edit a property by exposing it as a nested
`SerializedObject` (so its own subproperties get their own `ControlWidget`s). It
optionally constructs a default instance when the property is `null`
(`ShouldCreateInstanceWhenNull`, skipped for value types, abstract types, `object`,
delegates, or a property marked `[AllowNull]`).

`[CustomEmbeddedEditorAttribute( Type targetType = null )]` (same file as
`CustomEditorAttribute`) marks a widget as an *inline* editor embedded directly in
the inspector body rather than a single-row control, see
`Widgets/ControlWidgets/EmbeddedSpriteControlWidget.cs` for the one shipped example.

`ControlSheet` (source: `Editor/ControlSheet/ControlSheet.cs`) is a `GridLayout` that
lays out label + `ControlWidget` rows automatically:

```csharp
var sheet = new ControlSheet();
sheet.AddProperty( myObject, x => x.SomeFloatProperty );
```

---

## 4. Dockable windows and editor apps

### `[Dock]`: register a dockable panel

```csharp
[Dock( "Editor", "Inspector", "manage_search", DockArea.Right )]
public class Inspector : Widget
{
    public Inspector( Widget parent ) : base( parent ) { Layout = Layout.Column(); }
}
```

`DockAttribute( string target, string name, string icon = null, DockArea area = DockArea.Bottom )`
(source: `engine/Sandbox.Tools/Qt/DockManager/DockAttribute.cs`) registers the type
against a named target window (`"Editor"` for the main editor, `"Hammer"` for the map
editor). The dock is instantiated lazily by
`EditorTypeLibrary.Create<Widget>( targetType, [window] )`. The constructor **must**
take a single `Widget parent` argument, matching `DockWindow`. `DockArea` is an enum
(`Left`, `Right`, `Top`, `Bottom`, ...).

### `[EditorApp]`: register a standalone tool window

```csharp
[EditorApp( "Widget Gallery", "grid_view", "A test window, for testing" )]
public class WidgetGalleryWindow : Window { }
```

`EditorAppAttribute( string title, string icon, string description )` (source:
`engine/Sandbox.Tools/Editor/EditorAppAttribute.cs`) lists the type in the editor's
Tools/Apps menu; selecting it calls `EditorTypeLibrary.Create<Widget>(TargetType)` then
`.Show()`. The window class itself just needs to derive from `Window` (or
`DockWindow` for one with its own docking layout). The attribute is what makes it
discoverable, not a base class.

### `[EditorForAssetType]`: register an asset editor

```csharp
[EditorForAssetType( "sprite" )]
[EditorApp( "Sprite Editor", "emoji_emotions", "Edit 2D Sprites" )]
public class SpriteEditorWindow : Window, IAssetEditor { }
```

`EditorForAssetTypeAttribute( string extension )` (source:
`engine/Sandbox.Tools/Assets/AssetEditorAttribute.cs`) is what makes double-clicking
an asset of that extension open this window. It's independent of `[EditorApp]` (you
can have one without the other) but they're usually paired so the editor also shows up
standalone in the Tools menu. `"__fallback"` is a special extension value used by
`GameResourceEditor` to catch any `GameResource`-derived asset with no dedicated
editor.

### `DockWindow`

`DockWindow : Window` (source: `Qt/DockManager/DockWindow.cs`) owns a `DockManager`
and persists its layout to a per-window `ProjectCookie` keyed by `StateCookie`.
Override `BuildDefaultLayout()` to define the first-run arrangement:

```csharp
protected override void BuildDefaultLayout()
{
    var props = DockManager.OpenDock( "Properties", DockArea.Left );
    var view  = DockManager.OpenDock( "Rect View", DockArea.Right );
    DockManager.SetSplitterProportions( view, 0.30f, 0.70f );
}
```

`ResetLayout()` clears every dock and calls `BuildDefaultLayout()` again;
`RestoreLayout()` tries the saved cookie first and falls back to
`BuildDefaultLayout()`. `CreateDynamicViewMenu(Menu menu)` builds a checkable
show/hide list of every registered dock for you, for a Window > View menu.

---

## 5. The Widget/Layout UI system (Qt, not Razor)

**This has nothing to do with `Panel`/Razor.** `Widget` wraps a native `Native.QWidget`
(`_widget` field, source: `engine/Sandbox.Tools/Qt/Widget.cs`), a thin C#
binding over Qt. There is no virtual DOM, no CSS cascade, no `.razor` compiler, no
`BuildHash`. Layout is done imperatively in C# by adding widgets to `Layout` objects,
not by declaring markup.

### Widget basics

```csharp
public class MyDock : Widget
{
    public MyDock( Widget parent ) : base( parent )
    {
        Layout = Layout.Column();
        Layout.Margin = 8;
        Layout.Spacing = 4;

        Layout.Add( new Label( "Hello" ) );

        var row = Layout.AddRow();
        row.Add( new Button( "OK" ) { Clicked = () => Log.Info( "clicked" ) } );
    }
}
```

`Widget( Widget parent, bool isDarkWindow = false )`: parent can be `null` while
you're building it up, but a top-level window generally wants `null`. Key members:
`Parent`, `Children`, `Enabled`, `ReadOnly` (propagates to children), `Visible`,
`Size`/`FixedWidth`/`FixedHeight`/`MinimumWidth`/`MinimumHeight`, `HorizontalSizeMode`
/ `VerticalSizeMode` (`SizeMode`: `Default`, `CanGrow`, `CanShrink`, `Expand`,
`Flexible`, ...), `GetAncestor<T>()`, `GetDescendants<T>()`, `Update()` (queue
repaint), `AdjustSize()`, `Focus()`, `Destroy()`.

### `Layout`

Abstract base in `Qt/Layout/BaseLayout.cs`. Factory methods:

```csharp
Layout.Row( bool reversed = false )      // BoxLayout, left-to-right
Layout.Column( bool reversed = false )   // BoxLayout, top-to-bottom
Layout.Grid()                            // GridLayout
Layout.Flow()                            // VerticalLayout (wrapping flow)
```

Instance helpers: `Add<T>( T widget )`, `Add( Layout layout )`, `AddRow(...)` /
`AddColumn(...)` (adds and returns a nested layout), `AddStretchCell(int stretch = 0)`,
`AddSpacingCell(float size)`, `AddSeparator(...)`, `Clear(bool deleteWidgets)`,
`Margin`, `Spacing`. `GridLayout.AddCell<T>( int x, int y, T widget, int xSpan = 1,
int ySpan = 1, TextFlag alignment = 0 )` places a widget at a grid cell;
`ControlSheet` is built on this.

### Common controls (all in `Editor` namespace, `engine/Sandbox.Tools/Qt/`)

| Type | Notable members |
|---|---|
| `Label` | `Text`, constructible with initial text |
| `Button` | `Clicked` action, `Text`, `Icon` |
| `IconButton` | icon-only button, seen throughout the stock editor toolbars |
| `CheckBox` | `Checked`, `Text` |
| `LineEdit` | `Text`, events `TextChanged`, `TextEdited`, `ReturnPressed`, `EditingFinished` |
| `TextEdit` | multi-line text |
| `ComboBox` | `AddItem( string text, string icon = null, Action onSelected = null, ... )`, events `TextChanged`, `ItemChanged` |
| `Menu` | `AddOption( string name, string icon = null, Action action = null, string shortcut = null )` returns `Option`; `AddOption(string[] path, ...)` for nested submenus |
| `ToolBar` | `ToolBar( Widget parent, string name = null )`, `AddOption( string text, string icon = null, Action action = null )` |
| `ScrollArea` | scrollable container |
| `Splitter` / `LinkableSplitter` | resizable panes |
| `TreeView` (in `game/addons/tools/Code/Widgets/TreeView/`) | `BaseItemWidget`-derived; subclass `TreeNode`, override `OnPaint(VirtualWidget item)` and `BuildChildren()` |
| `ListView` (`engine/Sandbox.Tools/Widgets/ListView.cs`) | similar virtualized-item pattern |
| `Dialog` | wraps a `Window` for you: `new Dialog(parent)`, add content to it as the `Widget`, `.Show()` |

### Paint / `OnPaint`

Custom drawing overrides `protected override void OnPaint()` and uses the static
`Paint` class (`SetPen`, `SetBrush`, `ClearPen`, `DrawRect`, `DrawText`, `DrawIcon`,
`MeasureText`, ...). `ControlWidget.OnPaint` shows the standard three-phase pattern:

```csharp
protected override void OnPaint()
{
    Paint.Antialiasing = true;
    PaintUnder();    // background
    PaintControl();  // your content, override this one in subclasses
    PaintOver();     // hover/focus overlay
}
```

`TreeNode.OnPaint( VirtualWidget item )` shows the same idea for virtualized list
items:

```csharp
public override void OnPaint( VirtualWidget item )
{
    PaintSelection( item );
    Paint.SetPen( Color.White );
    Paint.DrawIcon( item.Rect, "description", 18, TextFlag.LeftCenter );
    Paint.DrawText( item.Rect.Shrink( 24, 0, 0, 0 ), Info.Name, TextFlag.LeftCenter );
}
```

### `[Shortcut]`

```csharp
[Shortcut( "tools.terrain.raise-lower", "1", typeof( SceneViewWidget ) )]
public void ActivateRaiseLowerTool() => EditorToolManager.SetSubTool( nameof( RaiseLowerTool ) );
```

`ShortcutAttribute( string identifier, string keyBind, Type targetOverride = null,
ShortcutType type = ShortcutType.Widget )` (source: `Editor/ShortcutAttribute.cs`).
`ShortcutType` is `Widget`, `Window`, or `Application`, controlling the scope the
binding is active in.

---

## 6. Editor gizmos and scene overlay drawing

`Gizmo` (source: `engine/Sandbox.Engine/Editor/Gizmos/`) is a `static partial class`
in **`namespace Sandbox`**, so it's usable from a plain `Component` in `Code/` with no
`Editor/` folder:

```csharp
protected override void DrawGizmos()
{
    Gizmo.Draw.Color = Color.Yellow;
    Gizmo.Draw.LineBBox( LocalBounds );
}
```

`Component.DrawGizmos()` (`protected virtual void`, source:
`Scene/Components/Component.Gizmos.cs`) is called by the editor scene view when the
component (or its `GameObject`) is selected or always-drawn; wrap draws in
`Gizmo.Scope(...)` to get local-space coordinates and isolate color/transform state.

### Scoping and transforms

```csharp
using ( Gizmo.Scope( "MyHandle", component.WorldTransform ) )
{
    Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
    Gizmo.Draw.LineSphere( 0, 8 );
}
```

`Gizmo.Scope( string path, Transform tx )` pushes a named path and sets
`Gizmo.Transform` for the block, restoring both on dispose. `Gizmo.ObjectScope<T>( T
obj, Transform tx )` additionally sets `Gizmo.Object`, which is what makes
`Gizmo.Select()` add the object to the `SelectionSystem` on click rather than a raw
path string.

### Draw (`Gizmo.Draw`, `GizmoDraw`)

Lines: `Line(a,b)`, `LineBBox(BBox)`, `LineSphere(center,radius)`,
`LineCircle(center, radius, ...)`, `Arrow(from,to,...)`. Solids:
`SolidBox(BBox)`, `SolidSphere(center,radius,...)`, `SolidCone(base,extent,radius,...)`.
Models: `Model(string modelName, Transform)`. Text: `Text(...)` (world-space),
`WorldText(...)`, `ScreenText(...)`. Screen-space: `ScreenRect(Rect, Color, ...)`.
State: `Color`, `LineThickness`, `IgnoreDepth`, `CullBackfaces`.

### Interactive handles (`Gizmo.Control`)

These both draw a handle **and** return whether it was dragged this frame, the
standard s&box editor-tool pattern:

```csharp
if ( Gizmo.Control.Position( "handle", position, out var newPos ) )
    position = newPos;
```

| Method | Signature |
|---|---|
| `Position` | `bool Position( string name, Vector3 position, out Vector3 newPos, Rotation? axisRotation = null, float squareSize = 3.0f )` |
| `Rotate` | `bool Rotate( string name, Rotation value, out Rotation newValue )` |
| `Scale` | `bool Scale( string name, Vector3 value, out Vector3 outValue, ... )` |
| `BoundingBox` | `bool BoundingBox( string name, BBox value, out BBox outValue )` (overloads add `out bool outPressed`, `out Vector3 outResizeAxis`) |
| `Sphere` | `bool Sphere( string name, float radius, out float outRadius, Color color )` |
| `Capsule` | `bool Capsule( string name, Capsule capsule, out Capsule outCapsule, Color color )` |
| `Arrow` | `bool Arrow( string name, Vector3 axis, out float distance, ... )` |
| `DragBox` / `DragSquare` | free-drag handles returning a `Vector3 movement` |

### Input and state queries

`Gizmo.CurrentRay`, `Gizmo.PreviousRay`, `Gizmo.CursorPosition`,
`Gizmo.IsLeftMouseDown` / `WasLeftMousePressed` / `WasLeftMouseReleased` (and Right
variants), `Gizmo.IsCtrlPressed` / `IsShiftPressed` / `IsAltPressed`,
`Gizmo.IsHovered` / `IsSelected` (relative to the current scope's `Path`/`Object`),
`Gizmo.Snap( Vector3 input, Vector3 movement )` for grid snapping.

### Scene overlay widgets

`EditorTool.SceneOverlay` is a `Widget` (`SceneOverlayWidget.Active`) parented over
the viewport. `EditorTool.AddOverlay( Widget widget, TextFlag align, Vector2 offset )`
docks a floating `WidgetWindow` inside it. This is how the camera preview and
terrain brush panels appear pinned to a viewport corner while the corresponding tool
is active. `Dispose()` on the tool destroys anything added this way automatically.

Undo support while dragging a gizmo handle goes through
`SceneEditorSession.Active.UndoScope( "Description" ).WithComponentChanges( component ).Push()`,
opened on the frame the mouse is pressed and disposed on `Gizmo.WasLeftMouseReleased`
(see the `BoxColliderTool` example in §2).

---

## 7. Asset access from editor code

`Asset` and `AssetSystem` live in `namespace Editor` (`engine/Sandbox.Tools/Assets/`)
, editor-only, unlike `Resource`/`ResourceLibrary` which are usable from game code.

```csharp
var asset = AssetSystem.FindByPath( "materials/default.vmat" );
if ( asset is not null )
{
    var material = asset.LoadResource<Material>();
    EditorUtility.OpenFileFolder( asset.AbsolutePath );
}

foreach ( var a in AssetSystem.All.Where( x => x.Path.EndsWith( ".prefab" ) ) )
    Log.Info( a.Name );
```

Key `Asset` members (source: `Assets/Asset.cs`): `Name`, `Path` (relative, compiled
extension, e.g. `.vsnd` for a `.wav` source), `RelativePath` / `AbsolutePath`
(on-disk, source extension), `Tags` (`AssetTags`), `GetSourceFile(bool absolute =
false)`, `T LoadResource<T>() where T : Resource`, `IsTransient`, `IsCloud`.

`AssetSystem` statics: `FindByPath(string path)`, `RegisterFile(string
absoluteFilePath)`, `All` (seen used as `AssetSystem.All` in the stock `TreeView`
widget gallery example), `CreateResource(string extension, string path)` (used by
`SceneEditorSession.Save` to create a new `.scene`/`.prefab` asset on disk).

`EditorUtility` (source: `engine/Sandbox.Tools/Utility/Utility.cs`, `static partial
class`) has the surrounding file/dialog operations: `OpenFileDialog` /
`SaveFileDialog` (via `Utility.FileDialog.cs`), `OpenFolder`, `OpenFile`,
`OpenFileFolder`, `MoveAssetToDirectory`, `RenameAsset`, `CopyAssetToDirectory`,
`DisplayDialog(...)` (blocking or callback-based confirm dialogs),
`GetFileThumbnail(string filePath, int width, int height)`,
`GetSerializedObject(object obj)`, `OpenControlSheet(SerializedObject, Widget parent,
bool createWindow = true)`.

`EditorPreferences` (`static class`, `engine/Sandbox.Tools/EditorPreferences.cs`)
exposes persisted editor settings as plain static properties backed by
`EditorCookie`: `BackfaceSelection`, camera settings (`CameraFieldOfView`,
`CameraSpeed`, ...), `WorldSpaceGizmos`, `GizmoScale`, `CreateObjectsAtOrigin`, and
more. Read these rather than hardcoding tool defaults.

`SceneEditorSession` (source: `Scene/Session/SceneEditorSession*.cs`) is the editor's
wrapper around an open `Scene`: `SceneEditorSession.Active` is the current one,
`.Scene`, `.Selection` (`SelectionSystem`), `.UndoSystem` / `UndoScope(...)`,
`.Save(bool saveAs)`, `.Reload()`, static `.Resolve(GameObject)` /
`.Resolve(Component)` / `.Resolve(SceneFile)`. `SelectionSystem` (source:
`engine/Sandbox.System/Utility/SelectionSystem.cs`, `namespace Sandbox`) is a plain
ordered set: `Add`, `Remove`, `Set`, `Clear`, `Contains`, `OnItemAdded` /
`OnItemRemoved` callbacks.

`EditorEvent` (source: `engine/Sandbox.Tools/Events/EditorEvent.cs`) is the
editor-side event bus, parallel to the game's `[Event]` system: register a method
with `[EditorEvent.Frame]` (runs every editor frame) or `[EditorEvent.Hotload]` (runs
after a hotload), or a raw string via `[Event("scene.saved")]`. Register/unregister
instances explicitly with `EditorEvent.Register(this)` / `Unregister(this)` if the
object isn't already part of the auto-registered widget tree. Notable built-in event
names seen in the stock editor: `"scene.saved"`, `"scene.play"`, `"scene.stop"`,
`"asset.contextmenu"`, `"project.settings.saved"`, `"editor.preferences"`.

---

## 8. Hammer / MapEditor: no documented extension surface

`engine/Sandbox.Tools/MapEditor/` (68 files: `Hammer.cs`, `HammerMainWindow.cs`,
`NativeHammer.cs`, `MapDoc/`, ...) is the internal implementation of the level editor.
It has no `[HammerTool]`-style attribute or documented extension point analogous to
`[EditorTool]` or `[Dock]` for third-party addons. Don't invent one. If a task needs
Hammer-specific extensibility, that's unverified territory; say so rather than
guessing an API.

---

## Gotchas

- **No `#if EDITOR`.** The split is the `Editor/` folder compiling to a separate
  assembly. Code under `Code/` physically cannot reference `Editor`-namespace types,
  it's not a matter of stripping a symbol.
- **`Gizmo` is in `namespace Sandbox`, not `Editor`.** A gameplay `Component`'s
  `DrawGizmos()` override works with zero `Editor/` folder. Everything else
  (`Widget`, `EditorTool`, `SceneEditorSession`) is `Editor`-only.
- **`EditorTool<T>` needs no attribute.** Top-level tools use `[EditorTool]`;
  per-component tools are discovered purely by matching `EditorTool<YourComponentType>`
  against the selected `GameObject`'s components, walking up the base-type chain.
  Adding `[EditorTool]` to an `EditorTool<T>` subclass does nothing useful, it isn't
  what registers it.
- **`Widget` and `Panel` do not share a base class.** `Widget.OnPaint()` uses the
  `Paint` static class; `Panel` uses SCSS and the UI render system. Neither API works
  on the other's type. If a task says "editor UI", it means `Widget`/`Layout`; if it
  says in-game HUD or menu, it means `Panel`/Razor (see `references/03_UI.md`). If
  in doubt, ask which one is meant: the failure mode, writing Razor markup against a
  `Widget` or vice versa, doesn't compile at all.
- **`ControlWidget.Create` picks the highest `[CustomEditor]` score, not the first
  match.** Multiple attributes can target the same or overlapping types; an exact
  `TargetType` match always outranks a base-type match, so a more specific editor you
  add for a derived type wins automatically without touching the base one.
- **`[Dock]` and `[EditorApp]` classes are constructed by `EditorTypeLibrary.Create`,
  not `new`.** A `[Dock]` widget's constructor signature must accept exactly a single
  `Widget parent` argument (`DockAttribute.Register` calls `Create<Widget>(TargetType,
  [window])`); get the signature wrong and it silently fails to instantiate rather than
  throwing at compile time.
- **`MeshTrace` on `EditorTool` deliberately disables the physics world**
  (`UsePhysicsWorld(false)`) and enables render-mesh hits. It's for viewport
  picking against what's drawn, not gameplay collision. Don't reach for it inside
  actual gameplay code; use `Scene.Trace` there (see `references/05_INPUT_PHYSICS.md`).
- **Undo scopes are manual and paired.** `SceneEditorSession.UndoScope(...).Push()`
  opens a scope; nothing closes it automatically except your own `Dispose()` call on
  mouse-release. Every stock component tool (`BoxColliderTool`, `CapsuleColliderTool`)
  follows the same pressed/released pairing shown in §2. Copy that shape, don't
  invent a different one.
