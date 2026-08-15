# UI & Razor Panels

Razor panels, SCSS styling, Panel lifecycle, data binding, events, built-in controls, and navigation. Everything here was read out of the engine source at version **26.08.05** (`sbox-public`).

---

## Architecture

s&box UI is a **Panel** tree laid out with CSS flexbox. You can build panels in pure C# or in Razor (HTML/CSS plus C#); Razor is a convenience layer over the same tree, and it renders identically to hand-written C# panels either way.

**Hierarchy:** `ScreenPanel` or `WorldPanel` component (on a GameObject) → `PanelComponent` (root) → child `Panel`s

- `PanelComponent` extends `Component`. It's a scene component, not a Panel.
- `Panel` is the actual UI base class. Every UI element inherits from it.
- `PanelComponent.Panel` gives you the root `Panel`, so you have something to parent children to.

**The distinction that trips people up:** you cannot nest a `PanelComponent` inside another panel with `<MyPanelComponent />`. A PanelComponent has to live on a GameObject carrying a ScreenPanel or WorldPanel. Only `Panel` subclasses can be nested inside Razor markup.

---

## Razor Panels

### File Structure

```
MyHud.razor          # PanelComponent (root): add to GameObject with ScreenPanel
MyHud.razor.scss     # Auto-loaded stylesheet (naming convention)
HealthBar.razor      # Child Panel: used inside MyHud
HealthBar.razor.scss
```

### PanelComponent (Root)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent

<root>
    <div class="hud">
        <HealthBar Health=@(PlayerHealth) />
        <label class="ammo">@Ammo</label>
    </div>
</root>

@code
{
    [Property] public float PlayerHealth { get; set; } = 100f;
    int Ammo => GetComponent<Weapon>()?.Ammo ?? 0;

    protected override int BuildHash() => System.HashCode.Combine( PlayerHealth, Ammo );
}
```

### Child Panel

```razor
@using Sandbox;
@using Sandbox.UI;

<root>
    <div class="bar" style="width: @(Health)%"></div>
    <label>@Health HP</label>
</root>

@code
{
    public float Health { get; set; } = 100f;

    protected override int BuildHash() => System.HashCode.Combine( Health );
}
```

### Key Rules

A few things that don't show up until you hit them:

- `<root>` wraps all your HTML. Leave it out and s&box parents elements to the panel root automatically.
- `@code { }` holds the C#: properties, methods, overrides.
- The `@` prefix injects a C# expression into HTML: `@MyVar`, `@(expression)`, `@foreach`, `@if`.
- A `return;` inside the Razor body stops rendering beyond that point.

### BuildHash

Razor panels don't re-render every frame. Whatever `BuildHash()` returns gets compared against last frame's value, and the template body only re-executes when it changes. That's why every piece of data the template reads has to go into the hash: leave one out, and the panel keeps showing a stale value for that field even while everything else updates around it.

```csharp
protected override int BuildHash() => System.HashCode.Combine( Health, Armor, IsAlive );
```

Hover and click also force a rebuild, since those flip pseudo-class state the CSS may depend on. Call `StateHasChanged()` to force one yourself.

### Passing Properties

```razor
<HealthBar Health=@(30) Name=@("Player 1") />
```

### Panel References

```razor
<HealthBar @ref="MyHealthBar" />
@code {
    HealthBar MyHealthBar { get; set; }
}
```

### Two-Way Binding

`:bind` keeps a property and a control's value in sync in both directions, so you don't have to wire a change handler by hand:

```razor
<SliderControl Min=@(0) Max=@(100) Step=@(1) Value:bind=@Volume />
<TextEntry Value:bind=@PlayerName />
@code {
    public float Volume { get; set; } = 50f;
    public string PlayerName { get; set; } = "";
}
```

### RenderFragment (Reusable Components)

A `RenderFragment` property is a slot: the caller supplies markup, the component decides where it lands.

```razor
<!-- InfoCard.razor -->
<root>
    <div class="header">@Header</div>
    <div class="body">@Body</div>
</root>
@code {
    public RenderFragment Header { get; set; }
    public RenderFragment Body { get; set; }
}
```

Usage:
```razor
<InfoCard>
    <Header>Player Stats</Header>
    <Body>
        <label>HP: @Health</label>
    </Body>
</InfoCard>
```

Every panel gets a built-in `ChildContent` fragment for free, so a component that only needs one slot doesn't need to declare one:
```razor
<InfoCard>
    <ChildContent>
        <label>Content goes here</label>
    </ChildContent>
</InfoCard>
```

### RenderFragment\<T\> (Templated Components)

The typed version passes a value into the slot's markup, which is what makes list rows and item templates work:

```razor
<!-- PlayerList.razor -->
<root>
    @foreach ( var player in Players )
    {
        @PlayerRow( player )
    }
</root>
@code {
    public List<Player> Players { get; set; }
    public RenderFragment<Player> PlayerRow { get; set; }
}
```

Usage with `Context`:
```razor
<PlayerList Players=@AllPlayers>
    <PlayerRow Context="item">
        @if (item is Player player)
        {
            <label>@player.Name, @player.Health HP</label>
        }
    </PlayerRow>
</PlayerList>
```

### Generic Panels

```razor
@typeparam T

<root>
    <label>Value: @Value</label>
</root>
@code {
    public T Value { get; set; }
}
```

Usage in Razor: `<MyPanel T="string" Value=@("hello") />`

---

## Panel vs PanelComponent Differences

| | Panel | PanelComponent |
|---|---|---|
| Base class | `Sandbox.UI.Panel` | `Sandbox.Component` |
| Lifecycle | `Tick()`, `OnAfterTreeRender(bool firstTime)` | `OnStart()`, `OnUpdate()`, etc. |
| Style access | `Style.Left = ...` | `Panel.Style.Left = ...` |
| Nesting | Can be used in Razor `<MyPanel />` | Must be on a GameObject, cannot be nested |
| Scene access | Via `Scene` (if has one) | Full component access |

---

## Styling

### SCSS Stylesheets

A stylesheet loads automatically by naming convention: `MyPanel.razor` picks up `MyPanel.razor.scss` with no wiring required.

Or load one explicitly:
```csharp
[StyleSheet( "path/to/style.scss" )]
public class MyPanel : Panel { }
```

Import other stylesheets the normal SCSS way:
```scss
@import "shared/buttons.scss";
```

### Inline Styles

```razor
<label style="color: red; font-size: 24px;">DANGER</label>
<div style="width: @(Progress * 100f)%"></div>
```

### Style Blocks

```razor
<style>
    .health { color: red; font-size: 20px; }
    .armor { color: blue; }
</style>
<root>
    <label class="health">@Health</label>
</root>
```

### Runtime Style Modification

```csharp
// In C# code (Panel.Tick or Component.OnUpdate)
myPanel.Style.Width = Length.Percent( progress * 100f );
myPanel.Style.BackgroundColor = Color.Red;
myPanel.Style.Opacity = 0.5f;
myPanel.Style.Dirty();  // flags the style as changed, forcing a re-layout
```

### CSS Classes (C# API)

```csharp
panel.AddClass( "active" );
panel.RemoveClass( "active" );
panel.SetClass( "active", isActive );     // conditional
panel.ToggleClass( "active" );
panel.HasClass( "active" );               // → bool
panel.BindClass( "alive", () => HP > 0 ); // auto-toggle from func
panel.FlashClass( "hit", 0.5f );          // add, then remove after duration
```

---

## Layout System

**Everything is flexbox.** `display: flex` is the default, and the only display mode besides `none`. There's no grid, no inline, no block, so any CSS knowledge that assumes those modes doesn't transfer.

### Key Layout Properties

| Property | Values | Default |
|----------|--------|---------|
| `flex-direction` | `row`, `row-reverse`, `column`, `column-reverse` | `row` |
| `justify-content` | `flex-start`, `center`, `flex-end`, `space-between`, `space-around`, `space-evenly` | `flex-start` |
| `align-items` | `flex-start`, `center`, `flex-end`, `stretch`, `baseline` | `stretch` |
| `align-self` | same as align-items + `auto` | `auto` |
| `flex-wrap` | `nowrap`, `wrap`, `wrap-reverse` | `nowrap` |
| `flex-grow` | float | `0` |
| `flex-shrink` | float | `1` |
| `flex-basis` | Length | `auto` |
| `gap` | Length | none |
| `position` | `static`, `relative`, `absolute` | `static` |
| `overflow` | `visible`, `hidden`, `scroll` | `visible` |

### Length Units

`Length` is the C# type behind every dimension in the style system, whether you set it from SCSS or from code.

| Unit | SCSS | C# |
|------|------|----|
| Pixels | `10px` | `Length.Pixels( 10 )` |
| Percent | `50%` | `Length.Percent( 50 )` |
| Viewport width | `10vw` | `Length.ViewWidth( 10 )` |
| Viewport height | `10vh` | `Length.ViewHeight( 10 )` |
| Root em | `2rem` | `Length.Rem( 2 )` |
| Em | `1.5em` | `Length.Em( 1.5f )` |
| Auto | `auto` | `Length.Auto` |
| Fraction | n/a | `Length.Fraction( 1 )` |
| Calc | `calc(100% - 20px)` | `Length.Calc( "100% - 20px" )` |

---

## Transitions & Animations

### CSS Transitions

Standard CSS transitions work as expected:

```scss
.button {
    transition: all 0.2s ease;
    background-color: #333;
    transform: scale(1);

    &:hover {
        background-color: #555;
        transform: scale(1.05);
    }
}
```

### Intro/Outro (s&box specific)

Web CSS has no equivalent to these two: `:intro` is active for one frame when the element is first created, so whatever properties you set under it become the state the element transitions FROM. `:outro` gets added the moment `Panel.Delete()` is called, and the panel actually waits for its transitions to finish playing before it's removed for real, which is what makes fade-out and slide-out effects on deletion possible at all.

```scss
.notification {
    transition: all 0.3s ease-out;
    opacity: 1;
    transform: translateY(0);

    &:intro {
        opacity: 0;
        transform: translateY(-20px);
    }

    &:outro {
        opacity: 0;
        transform: translateY(20px);
    }
}
```

### CSS Animations

```scss
@keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
}

.spinner {
    animation-name: spin;
    animation-duration: 1s;
    animation-iteration-count: infinite;
    animation-timing-function: linear;
}
```

### Sound on State Change

`sound-in` and `sound-out` fire off a sound the instant a pseudo-class is applied or removed, no C# event handler needed:

```scss
.button {
    sound-in: "ui.button.over";   // play when :hover applied
    sound-out: "ui.button.out";   // play when :hover removed

    &:active {
        sound-in: "ui.button.press";
    }
}
```

---

## Panel API

`Sandbox.UI.Panel` is the base for all UI elements.

### Tree / Hierarchy

```csharp
panel.Parent                    // Panel
panel.Children                  // IEnumerable<Panel>
panel.ChildrenCount             // int
panel.Descendants               // all nested children (recursive)
panel.Ancestors                 // parent chain
panel.SiblingIndex              // int
panel.AddChild<T>()             // create and add typed child
panel.DeleteChildren()          // remove all children
panel.SortChildren( comparison )
panel.SetChildIndex( child, i )
panel.Delete()                  // remove (respects :outro transitions)
panel.IsDeleting                // bool, true while in outro phase
```

### Identity & Classes

```csharp
panel.ElementName               // tag name (e.g., "label", "div")
panel.Id                        // string id
panel.Class                     // List<string> of CSS classes
panel.Classes                   // space-separated string
```

### Visibility & Focus

```csharp
panel.IsVisible                 // visible (including parent visibility)
panel.IsVisibleSelf             // own visibility
panel.AcceptsFocus              // can receive keyboard focus
panel.HasFocus / HasHovered / HasActive  // pseudo-class states
panel.SetMouseCapture( true )   // capture all mouse input
```

### Scrolling

```csharp
panel.ScrollOffset              // Vector2, current scroll position
panel.ScrollSize                // Vector2, total scrollable area
panel.HasScrollX / HasScrollY   // bool
panel.TryScroll( delta )        // attempt scroll
panel.TryScrollToBottom()
panel.PreferScrollToBottom      // auto-scroll to bottom (chat logs)
```

### Events

Panels can listen for events three ways: the attribute, a direct listener, or a Razor attribute.

```csharp
// Event listener via attribute
[PanelEvent( "onclick" )]
void OnClicked( PanelEvent e ) { }

// Or via method
panel.AddEventListener( "onclick", ( e ) => { /* handler */ } );
panel.CreateEvent( "mycustomevent" );

// Razor onclick
<div onclick=@MyMethod>Click me</div>
```

### Coordinate Conversion

```csharp
panel.ScreenPositionToPanelPosition( screenPos )   // → Vector2
panel.PanelPositionToScreenPosition( panelPos )     // → Vector2
```

### Builder Pattern

Useful for building panels from pure C# without a `.razor` file:

```csharp
var row = panel.Add.Panel( "row" );
row.Add.Label( "Hello", "title" );
row.Add.Image( "ui/icon.png", "icon" );
row.Add.Icon( "favorite" );
```

### Misc

```csharp
panel.PlaySound( "ui.click" );
panel.Tooltip = "Hover text";
panel.UserData = myObject;        // arbitrary data attachment
```

---

## Built-in Controls

### Label

Text display. Supports rich text and selection.

```razor
<label>Simple text</label>
<label class="title">@PlayerName</label>
```

| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | Display text |
| `IsRich` | `bool` | Parse rich text markup |
| `Selectable` | `bool` | Allow text selection |
| `Multiline` | `bool` | Multi-line display |

### Button

Clickable button with text and optional icon.

```razor
<Button Text="Click Me" Icon="play_arrow" onclick=@OnClick />
```

| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | Button label |
| `Icon` | `string` | Material icon name |
| `Disabled` | `bool` | Disable interaction |
| `Active` | `bool` | Active/pressed state |
| `Href` | `string` | Navigation URL (with NavigationHost) |

### TextEntry

Text input field.

```razor
<TextEntry Value:bind=@Name Placeholder="Enter name..." Icon="person" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Text` / `Value` | `string` | Current text |
| `Placeholder` | `string` | Placeholder hint |
| `Icon` | `string` | Prefix icon |
| `Multiline` | `bool` | Multi-line input |
| `Numeric` | `bool` | Numbers only |
| `MinValue` / `MaxValue` | `float` | Numeric range |
| `MinLength` / `MaxLength` | `int` | Character limits |
| `HasClearButton` | `bool` | Show clear button |
| `OnTextEdited` | `Action<string>` | Per-keystroke callback |

### Checkbox

```razor
<Checkbox Value:bind=@IsEnabled LabelText="Enable feature" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Checked` / `Value` | `bool` | Checked state |
| `LabelText` | `string` | Label text |
| `ValueChanged` | `Action<bool>` | Change callback |

### SliderControl

Numeric slider.

```razor
<SliderControl Min=@(0) Max=@(100) Step=@(1) Value:bind=@Volume />
```

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `float` | Current value |
| `Min` / `Max` | `float` | Range |
| `Step` | `float` | Increment step |
| `ShowRange` | `bool` | Show min/max labels |
| `ShowTextEntry` | `bool` | Show editable number |
| `OnValueChanged` | `Action<float>` | Change callback |

### DropDown

Selection from options list.

```razor
<DropDown Value:bind=@SelectedWeapon>
    @foreach ( var weapon in Weapons )
    {
        <option value=@weapon.Id>@weapon.Name</option>
    }
</DropDown>
```

Or build options in code:
```csharp
dropdown.Options = new List<Option>
{
    new Option( "Easy", "easy" ),
    new Option( "Normal", "normal" ),
    new Option( "Hard", "hard" )
};
```

`Option` has: `string Title`, `string Icon`, `string Subtitle`, `string Tooltip`, `object Value`

### SwitchControl

Toggle switch.

```razor
<SwitchControl Value:bind=@IsEnabled />
```

### Image

```razor
<img src="ui/crosshair.png" />
<Image Texture=@myTexture />
```

### Other Controls

| Control | Description |
|---------|-------------|
| `IconPanel` | Material Design icon display |
| `ButtonGroup` | Group of selectable buttons (tab-like) |
| `SplitContainer` | Two panes with draggable divider |
| `VirtualList` | Virtualized scrollable list (for large datasets) |
| `VirtualGrid` | Virtualized scrollable grid |
| `ScenePanel` | Renders a 3D SceneWorld in a panel |
| `WebPanel` | Embedded web browser |
| `MenuPanel` | Context/right-click menu |
| `Form` | Form with field rows |

---

## VirtualGrid

Rendering thousands of item panels the normal way means thousands of live Panel objects, most of them off-screen. `VirtualGrid` only creates panels for items actually in view, and recycles them as you scroll.

```razor
<VirtualGrid Items=@AllItems ItemSize=@(new Vector2(120, 120))>
    <Item Context="item">
        @if (item is MyItem entry)
        {
            <label>@entry.Name</label>
        }
    </Item>
</VirtualGrid>
```

- `Items` accepts any `IEnumerable<T>`.
- `ItemSize` is a `Vector2`: cells scale to fill the available width, preserving aspect ratio.
- The grid needs an explicit size in CSS (`width: 100%; height: 100%`), since it can't infer one from virtualized content.
- Use the `gap` CSS property for spacing between cells.

---

## Navigation

`NavigationHost` (in `Sandbox.UI.Navigation`) behaves like a single-page website: one panel visible at a time, with back/forward history tracked for you.

```razor
@using Sandbox.UI.Navigation
@inherits NavigationHost

<root>
    <NavLinkPanel Href="/home" class="nav-item">Home</NavLinkPanel>
    <NavLinkPanel Href="/settings" class="nav-item">Settings</NavLinkPanel>
    <div class="content">
        <!-- Current page renders here -->
    </div>
</root>
```

Register pages up front, then navigate:
```csharp
protected override void OnStart()
{
    AddDestination( "/home", typeof(HomePage) );
    AddDestination( "/settings", typeof(SettingsPage) );
    Navigate( "/home" );
}
```

| Method | Description |
|--------|-------------|
| `Navigate( string url )` | Go to URL |
| `GoBack()` | Navigate back |
| `GoForward()` | Navigate forward |
| `CurrentUrl` | Current page URL |
| `CurrentPanel` | Current page Panel |

Pages can implement `INavigatorPage` for `OnNavigationOpen()` / `OnNavigationClose()` callbacks.

`NavLinkPanel` auto-applies the `.active` class when its `Href` matches the current URL, so a nav bar's highlight state needs no extra code.

---

## Event Types

| Event | Properties | Triggered By |
|-------|-----------|--------------|
| `PanelEvent` | `Target`, `This`, `Name`, `Value`, `StopPropagation()` | Base for all events |
| `MousePanelEvent` | `MouseButton` | Mouse interactions |
| `ButtonEvent` | `Button`, `Pressed`, `HasShift/Ctrl/Alt` | Keyboard/mouse press |
| `DragEvent` | (inherits PanelEvent) | Drag operations |
| `PasteEvent` | `ClipboardValue` | Paste action |
| `EscapeEvent` | n/a | Escape key |

### Common Razor Events

```razor
<div onclick=@OnClick>Click</div>
<div onmouseover=@OnHover>Hover</div>
<div onrightclick=@OnContext>Right click</div>
<div onmousedown=@OnPress>Press</div>
```

---

## Localization

Any string prefixed with `#` is treated as a localization token and resolved automatically, in markup or in a control property:

```razor
<label>#menu.play</label>
<Button Text="#menu.settings" />
```

Token file: `Localization/en/mygame.json`
```json
{
    "menu.play": "Play Game",
    "menu.settings": "Settings"
}
```

31 languages are supported. Language codes: `en`, `fr`, `de`, `es`, `ja`, `ko`, `zh-cn`, `zh-tw`, `ru`, `pt-br`, etc.

---

## Common Style Properties (Quick Reference)

### Differences from Web CSS

If you're coming from browser CSS, these are the places s&box's style engine diverges:

| Property | s&box Behavior |
|----------|---------------|
| `display` | Only `flex` (default) or `none`. Everything is flexbox. |
| `position` | Only `static`, `relative`, `absolute`. No `fixed`. |
| `font-family` | Specify by font name, not filename. Single font only. |
| `pointer-events` | Default is `none`. Set to `all` for interactivity. |
| `:intro` / `:outro` | s&box-specific. Transitions for create/delete. |
| `sound-in` / `sound-out` | Play sounds on style apply/remove. |
| `background-image-tint` | Custom. Multiplies background image by color. |
| `content` | Sets `Label.Text` directly. |

Note `pointer-events` in particular: a panel that looks clickable but silently eats no input is almost always missing `pointer-events: all` somewhere in its ancestor chain, since the default is `none`.

### Common Patterns

```scss
// Full-screen overlay
.overlay {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
    pointer-events: all;
}

// Centered content
.centered {
    justify-content: center;
    align-items: center;
}

// Scrollable list
.list {
    flex-direction: column;
    overflow: scroll;
    flex-grow: 1;
}

// Fade in on create
.panel {
    transition: opacity 0.3s ease;
    opacity: 1;
    &:intro { opacity: 0; }
    &:outro { opacity: 0; }
}
```

---

## Complete Working Example

A HUD with health bar and kill feed, showing a PanelComponent, its stylesheet, and `:intro`/`:outro` together:

**MyHud.razor:**
```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent

<root>
    <div class="health-bar">
        <div class="fill" style="width: @(Health)%"></div>
        <label>@Health HP</label>
    </div>
    @foreach ( var msg in KillFeed )
    {
        <label class="kill-msg">@msg</label>
    }
</root>

@code
{
    [Property] public float Health { get; set; } = 100f;
    List<string> KillFeed { get; set; } = new();

    protected override int BuildHash() => System.HashCode.Combine( Health, KillFeed.Count );
}
```

**MyHud.razor.scss:**
```scss
MyHud {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
    flex-direction: column;
    justify-content: flex-end;
    padding: 20px;
    pointer-events: none;

    .health-bar {
        width: 300px;
        height: 30px;
        background-color: rgba(0, 0, 0, 0.5);
        border-radius: 4px;
        overflow: hidden;

        .fill {
            height: 100%;
            background-color: #e74c3c;
            transition: width 0.3s ease;
        }

        label {
            position: absolute;
            left: 10px; top: 0; bottom: 0;
            align-items: center;
            color: white;
            font-size: 16px;
            text-shadow: 1px 1px 2px black;
        }
    }

    .kill-msg {
        color: white;
        font-size: 14px;
        transition: all 0.3s ease;
        &:intro { opacity: 0; transform: translateX(-20px); }
        &:outro { opacity: 0; }
    }
}
```
