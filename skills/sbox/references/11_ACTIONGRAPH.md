<!--
  s&box Skill : 11_ACTIONGRAPH.md

  ActionGraph: exposing C# as nodes, and graph-backed callbacks a designer can wire.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# ActionGraph

ActionGraph is s&box's visual scripting system: node graphs that compile down to real delegates and get invoked from ordinary C#. This file covers the C# authoring surface, exposing methods and properties as nodes, exposing graph-backed callback properties on components, calling into a graph, and how graphs serialize and survive hotload. It does not cover the node editor UI itself. Read out of the engine source at version **26.08.05** (`sbox-public`), plus the shipped `Facepunch.ActionGraphs.dll` XML docs for the parts of the graph engine that live in a closed-source assembly. Paths used:

- `engine/Sandbox.System/Attributes/ActionGraphs.cs` (attribute definitions)
- `engine/Sandbox.Engine/Systems/ActionGraphs/*.cs` (built-in node libraries, `ActionGraphResource`, `MapSourceLocation`)
- `engine/Sandbox.Engine/Scene/Components/Component.cs`, `.../Game/Prop.cs`, `.../Collider/Collider.cs` (delegate-property callbacks on real components)
- `engine/Sandbox.Reflection/TypeLibrary/NodeLibrary.cs`, `TypeLibrary.cs` (member eligibility rules)
- `engine/Sandbox.Reflection/TypeLibrary/HotloadUpgraders.cs` (hotload behavior)
- `engine/Sandbox.System/Extend/ActionExtensions.cs` (`InvokeWithWarning`)
- `engine/Tests/Sandbox.Test.Engine/Scene/GameObjects/Serialize.cs` (`ComponentWithActionGraph`, clone/retarget behavior)
- `game/bin/managed/Facepunch.ActionGraphs.xml` (doc comments for the closed-source `Facepunch.ActionGraphs` assembly: `ActionGraph`, `IActionGraphDelegate`, `SerializationOptions`)

`game/addons` (the shipped base game, menu, and tools code) does not itself define any `[ActionGraphNode]` methods. All node-library authoring shown here is engine code; a typical game project's ActionGraph surface is exposing delegate properties on its own components and letting the built-in reflection rules expose its public API automatically, not writing custom node definitions.

---

## What It Is, and Where a Game Author Touches It

An `ActionGraph` (`Facepunch.ActionGraphs.ActionGraph`) represents an async method as a directed graph: an entry node accepts a signal, routes it through nodes that perform actions, and can return values through output nodes. Graphs compile to a real `System.Delegate` (`ActionGraph.Compile<T>()`, or transparently via `IActionGraphDelegate`), so once compiled a graph-backed callback is indistinguishable from a hand-written C# delegate to anything that calls it.

There are two separate jobs here, and they don't overlap much:

1. **Writing C# that the graph editor can use.** You expose methods as nodes (`[ActionGraphNode]`), expose properties/fields (mostly automatic), and expose delegate-typed `[Property]` members on your components so a designer can wire behavior in the editor without touching code. This is regular C# in your component/library code, no editor involved.
2. **Building the graph itself.** That happens in the ActionGraph node editor (`game/editor/ActionGraph/Code/*`), a Qt-based visual editor. This file does not teach that UI. If you need to build or edit a graph, do it in the editor; the programmatic `ActionGraph.AddNode` / `SetLink` API exists (see the `Facepunch.ActionGraphs.xml` summaries for `AddNode`, `SetLink`, `RemoveLink`) but is what the editor itself uses internally, not a documented end-user authoring path for game code.

Most of the time, "using ActionGraph" from a game codebase means job 1: declare a `[Property] public Action OnSomething { get; set; }` on a component and let a designer plug a graph into it in the inspector.

---

## Exposing a C# Method as a Node

```csharp
[ActionGraphNode( "time.delta" ), Pure, Category( "Time" ), Icon( "Δ" ), Tags( "common" )]
public static float Delta()
{
    return Time.Delta;
}
```

`[ActionGraphNode( "identifier" )]` (`Sandbox.ActionGraphNodeAttribute`, implements `Facepunch.ActionGraphs.INodeAttribute`) can be applied to a static or instance method, a property, or a constructor. `Identifier` is a unique string used to look the node definition up; the engine's own convention is lowercase, dot-separated, `category.name` (`time.delta`, `time.delay`, `log.info`, `log.warning`, `scene.get`, `scene.find`, `random.int`, `op.isnull`, `sys.tostring`, `const.string`, `resource.ref`). There's no enforced format beyond uniqueness, but matching that convention keeps your nodes sorted sensibly next to the built-ins.

Layer the standard display attributes on top; they're the same ones used elsewhere in the editor (`Sandbox.TitleAttribute`, `CategoryAttribute`, `IconAttribute`, `DescriptionAttribute`, `Tags`), not ActionGraph-specific types:

| Attribute | Effect |
|---|---|
| `[Title( "..." )]` | Node header text. Supports `{paramName\|Fallback}` interpolation, e.g. `Title( "Get {T\|Component}" )` substitutes the bound generic type's display name. |
| `[Category( "Group/Sub" )]` | Where the node lives in the node picker tree. |
| `[Icon( "material_icon_name" )]` | Node icon (or a literal glyph like `"Δ"`). |
| `[Description( "..." )]` | Tooltip text. |
| `[Tags( "common" )]` | Search tags; `"common"` surfaces a node in the quick-pick list. |
| `[ActionGraphOperator]` | Render with no header or socket labels and a big centered icon, for math-operator-style nodes (`op.as`, `op.convert`). |

### `ActionGraphNodeAttribute` properties

| Property | Default | Notes |
|---|---|---|
| `Identifier` | (required, ctor arg) | Unique node id. |
| `DefaultInputSignal` | `true` | Whether the node has an execution input pin by default. |
| `DefaultOutputSignal` | `true` | Whether the node has an execution output pin by default. |
| `InheritAsync` | `false` | No further behavior is documented in the shipped XML docs beyond the property name; not independently verified here. Leave at the default unless you have a concrete reason and can test the result. |

### Parameter eligibility

A method's parameters must satisfy `AreParametersActionGraphSafe` (`NodeLibrary.cs`) to be node-eligible at all:

- No `ref struct` parameters (`Span<T>` and friends are rejected outright).
- No pointer parameters.
- `ref`/`out`/`in` parameters are fine, but a parameter can't be both `in` and `out` at once (i.e. plain unannotated `ref` in the IL sense is rejected; `ref readonly` vs `out` is the actual check).
- `Delegate`-typed parameters (a method taking a callback) become output signal pins on the node, and the delegate's own return type must be `void` or `Task`; the delegate's own parameters are recursively checked the same way.

This is a hard gate: a method that fails it is never wired into the node library, `[ActionGraphNode]` or not.

### Pure vs impure

```csharp
// Pure: no side effects, just inputs -> output. Renders as an inline expression node
// with no exec pins, re-evaluated on demand whenever its output is read.
[ActionGraphNode( "random.float" ), Pure, Title( "Random Float" ), Category( "Math/Random" )]
public static float Float( float min = 0f, float max = 1f )
{
    return min + Random.Shared.NextSingle() * (max - min);
}

// Impure (the default: no attribute needed): has side effects. Renders as an action
// node with an exec-in and exec-out pin, runs exactly once when the signal reaches it.
[ActionGraphNode( "log.info" ), Category( "Debug" ), Title( "Log Info" )]
public static void Info( string format = null, params object[] args )
{
    Log.Info( Format( format, args ) );
}
```

`[Pure]` (`IPureAttribute`) declares a method has no side effects, it only computes a result from its inputs. `[Impure]` (`IImpureAttribute`) is the explicit opposite, for a method that has side effects even though it might look side-effect-free (e.g. a `readonly`-looking accessor that logs). Methods with no attribute at all default to impure, which is why every void-returning action in the built-in library (`log.info`, `scene.instantiate`, `sound.play`) skips the attribute entirely and only the calculation-style methods (`time.delta`, `random.int`, `op.isnull`) carry `[Pure]`.

The distinction matters to the evaluator, not just the picker: a pure node has no execution pins and gets pulled lazily, once per read, wherever its output is wired in; the graph can reorder or repeat evaluating it. An impure node is a step in the control-flow chain: it runs exactly once, at the point the signal reaches it, in the order the graph specifies. Marking something pure when it actually mutates state (or has meaningfully different results on repeated calls within one evaluation) will surface as the graph calling it a different number of times than you'd expect, or reordering it relative to other side effects.

---

## Exposing Properties and Fields (Mostly Automatic)

Unlike methods, properties and fields don't need `[ActionGraphNode]` to become "Get X" / "Set X" nodes. The reflection rules in `NodeLibrary.TypeLoader.CanRead`/`CanWrite` (`engine/Sandbox.Reflection/TypeLibrary/NodeLibrary.cs`) are, in order:

- `[ActionGraphIgnore]` on the getter/setter (or the field) always excludes it.
- `[ActionGraphInclude]` on the getter/setter (or the field) always force-includes it, overriding visibility.
- Otherwise: a property getter is readable if it's `public`; a setter is writable if it's `public`, not `init`-only, and the property doesn't carry `[ReadOnly]`. A field follows the same public/no-`[ReadOnly]` rule, and is never writable if `readonly`.
- Enum-typed properties/fields are never writable this way (enum members are constants, not settable state).

So a plain `public float Speed { get; set; }` on your component is already exposed as "Get Speed" / "Set Speed" nodes with no attributes at all. You only need `[ActionGraphIgnore]` to hide something public you don't want graph authors touching, or `[ActionGraphInclude]` to expose something that wouldn't otherwise qualify (a non-public member, for instance).

`[ActionGraphInclude]` also takes `AutoExpand` (`bool`): if true, double-clicking an output of the declaring type in the graph editor auto-expands this member as a follow-on node. Field/property/method/constructor target.

Type-level gating works the same way through `IsActionGraphIgnored`: a member with neither `[ActionGraphIgnore]` nor `[ActionGraphInclude]` inherits its declaring type's ignored state, recursively. In practice this only matters for engine-internal types the library deliberately hides wholesale; your own `Component` subclasses are exposed by default.

---

## Graph-Backed Callback Properties on Components

This is the pattern a game author reaches for most: a `[Property]` of a delegate type (`Action`, `Action<T>`, `Func<T>`, or a custom `delegate`) that a designer wires a graph into from the inspector, exactly like wiring up a `UnityEvent` in Unity, minus the extra wrapper type. `Component` itself does this for its own lifecycle:

```csharp
// engine/Sandbox.Engine/Scene/Components/Component.cs
[Group( "Component" )]
[Property]
public Action OnComponentEnabled { get; set; }

[Group( "Component" )]
[Property]
public Action OnComponentStart { get; set; }
```

and fires them the same way you'd fire any nullable delegate:

```csharp
internal virtual void OnEnabledInternal()
{
    using ( GameTransform.DisableInterpolation() )
    {
        OnEnabled();
        OnComponentEnabled?.Invoke();
    }
}
```

Real components in the engine follow the same shape, with typed arguments:

```csharp
// engine/Sandbox.Engine/Scene/Components/Game/Prop.cs
[Property] public Action OnPropBreak { get; set; }
[Property] public Action<List<Gib>> OnGibsCreated { get; set; }
[Property] public Action<DamageInfo> OnPropTakeDamage { get; set; }
```

```csharp
// engine/Sandbox.Engine/Scene/Components/Collider/Collider.cs
public Action<Collider> OnTriggerEnter { get; set; }
public Action<Collider> OnTriggerExit { get; set; }
```

Your own component code follows the identical shape:

```csharp
public sealed class Turret : Component
{
    [Property] public Action OnTargetAcquired { get; set; }
    [Property] public Action<GameObject> OnFired { get; set; }

    void Fire( GameObject target )
    {
        // ...
        OnFired?.Invoke( target );
    }
}
```

The delegate type on the property is the graph's signature: `Action<GameObject>` means the graph gets one `GameObject` input parameter and no return value. `Func<T>` properties work the same way for graphs that need to return a value (see `ComponentWithActionGraph.Func` below). There's no special "ActionGraph delegate" type you write against; you declare a normal C# delegate-typed property, and the engine wires an `ActionGraph` up to satisfy it when one is attached in the editor. If nothing is attached, the property is simply `null`, so always null-conditional-invoke it.

`[SingleAction]` (`Sandbox.SingleActionAttribute`) forces a delegate-typed field/property to accept only a single attached graph rather than a multicast chain. No engine code uses it as of this version; treat it as available but unverified in practice.

---

## Attribute Reference

| Attribute | Target | Purpose |
|---|---|---|
| `[ActionGraphNode( "id" )]` | method, property, constructor | Exposes the member as a node with the given unique identifier. |
| `[Pure]` / `[Impure]` | method | Declares side-effect-free (expression node, no exec pins) vs has-side-effects (action node, exec pins). No attribute defaults to impure. |
| `[ActionGraphOperator]` | method, property, constructor | Render as a bare operator glyph instead of a titled node with socket labels. |
| `[ActionGraphTarget]` (Sandbox) / `[Target]` (`Facepunch.ActionGraphs`) | parameter | Marks the parameter that receives the graph's implicit "target" (`this`) input, instead of being a normal input pin. Built-in node methods import `Facepunch.ActionGraphs` and use the bare `[Target]` form; `Sandbox.ActionGraphTargetAttribute` is a same-behavior alias for code that doesn't want that `using`. |
| `[ActionGraphProperty]` (Sandbox) / `[Facepunch.ActionGraphs.Property]` | parameter | Marks a parameter that should only be configurable in the inspector as a node property, not exposed as a wireable input pin. Used for things like a constant node's `value`/`name`, where wiring another node into it would be meaningless. |
| `[ActionGraphInclude]` | field, property, method, constructor | Force-include a member that wouldn't otherwise qualify (non-public, etc). `AutoExpand` (bool) controls whether double-clicking an output auto-expands this member as a follow-on node. |
| `[ActionGraphIgnore]` | class, struct, field, property, method, constructor | Force-exclude a member (or, on a type, gate members that don't explicitly opt back in with `[ActionGraphInclude]`). |
| `[ActionGraphExposeWhenCached]` | class, struct | Instances of this type are never reused from an `ActionGraphCache` during deserialization, they're always serialized inline instead. The engine applies this to `ComponentReference`/`GameObjectReference` so IDs get fixed up correctly when duplicating objects or instantiating prefabs. |
| `[SingleAction]` | field, property | Restrict a delegate-typed property to a single attached graph. Unused in engine code as of this version. |

---

## Calling Into a Graph From C#

Because a graph compiles to a real delegate, calling into one is just calling the property:

```csharp
[Property] public Func<string> Func { get; set; }

// ...
var result = Func?.Invoke() ?? "default";
```

Always null-check. A `[Property]` delegate with no graph wired up in the inspector is `null`, not a no-op stub. `Component`'s own lifecycle callbacks (`OnComponentStart`, `OnComponentDisabled`, etc.) and `ActionsInvoker` (`engine/Sandbox.Engine/Systems/ActionGraphs/Components.cs`) route through `InvokeWithWarning()` (`engine/Sandbox.System/Extend/ActionExtensions.cs`), an extension method that swallows any exception thrown inside the delegate and logs it as a warning instead of letting it propagate:

```csharp
public static void InvokeWithWarning( this Action action )
{
    if ( action is null ) return;
    try { action.Invoke(); }
    catch ( System.Exception e ) { Log.Warning( e, $"{e.Message}" ); }
}
```

Not every built-in callback goes through this. `Prop.OnPropBreak`, `Collider.OnTriggerEnter`, and friends call `?.Invoke()` directly with no wrapper, so an exception thrown inside a graph attached to one of those will propagate straight out of the firing method. Check which pattern the specific property you're calling uses before assuming a broken graph can't crash your call site; when writing your own delegate-typed properties, prefer `InvokeWithWarning()` for anything designer-authorable so a bad graph degrades to a log line instead of taking down the caller.

To go from the delegate back to the underlying graph object, `Facepunch.ActionGraphs.DelegateExtensions.GetActionGraphInstance()` (and the plural `GetActionGraphInstances()`, for multicast) returns the wrapping `IActionGraphDelegate` if the delegate is (or contains) a compiled graph:

```csharp
var instance = someComponent.Func.GetActionGraphInstance();
if ( instance is not null )
{
    ActionGraph graph = instance.Graph;
    // inspect / mutate the graph itself
}
```

`IActionGraphDelegate` exposes `Graph` (the wrapped `ActionGraph`), `Delegate` (the compiled, always-up-to-date delegate), `DelegateType`, and `Defaults` (default values for any graph inputs the delegate signature doesn't supply). This is how the cloning machinery retargets a graph's implicit `this` when you duplicate a `GameObject`, covered below.

---

## Serialization

Graphs serialize to JSON as part of whatever contains them: a component's `[Property]` delegate field, or a standalone `.action` asset (`ActionGraphResource`). `ActionGraphResource` defers actual deserialization until the graph is first accessed, in case types aren't loaded yet:

```csharp
// engine/Sandbox.Engine/Systems/ActionGraphs/ActionGraphResource.cs
public ActionGraph Graph
{
    get
    {
        if ( _graph is not null ) return _graph;
        using var optionsScope = PushSerializationScope();
        return _graph = _serializedGraph?.Deserialize<ActionGraph>( Json.options );
    }
    set { _graph = value; _serializedGraph = null; }
}
```

`Facepunch.ActionGraphs.SerializationOptions` (a record) controls how (de)serialization behaves within an ambient scope pushed via `ActionGraph.PushSerializationOptions( options )` (returns an `IDisposable`, used with `using`):

| Field | Purpose |
|---|---|
| `ImpliedTarget` | Input added automatically to any graph deserialized in this scope, representing the "this" the graph is embedded in. Omitted when serializing, since it'll be re-added on load. |
| `Cache` | An `IActionGraphCache` (e.g. `ActionGraphCache`) to reuse graph instances across (de)serialize calls, matched by `ActionGraph.Guid`. |
| `SourceLocation` | An `ISourceLocation` describing where the graph came from, for stack traces and "which asset do I save this back into" in the editor. |
| `GuidMap` | Remaps graph GUIDs encountered while deserializing (used when duplicating). |
| `WriteCacheReferences` | If true and `Cache` is set, serialize a minimal reference stub instead of the full graph JSON. |
| `ForceUpdateCached` | If true, always replace the cached instance on deserialize; otherwise only replace it if `ChangeId` differs. |
| `DeserializeMode` | `Enabled`, `DisabledReturnNull`, or `DisabledThrow`, gates whether graphs can be deserialized at all in this scope (used to lock down deserializing untrusted payloads). |

The engine defines two concrete `ISourceLocation`s (`engine/Sandbox.Engine/Systems/ActionGraphs/SourceLocations.cs`): `MapSourceLocation` (graphs embedded in a Hammer map, cached and looked up by normalized `.vpk` path) and `GameResourceSourceLocation` (graphs embedded in a `GameResource`, scenes, prefabs, or custom resources). `GameResource` itself builds its `SerializationOptions` lazily and pushes the scope around its own `Serialize()`/deserialize paths (`GameResource.CreateSerializationOptions`, `PushSerializationScope`).

### Cloning retargets embedded references

When you clone a `GameObject`, any graph embedded in one of its component properties gets its implicit target (and any `scene.ref` node pointing at a GameObject/Component) rewritten to point at the clone instead of the source, while reusing the same compiled `ActionGraph` instance:

```csharp
// engine/Tests/Sandbox.Test.Engine/Scene/GameObjects/Serialize.cs
public class ComponentWithActionGraph : Component
{
    [Sandbox.Property]
    public Func<string> Func { get; set; }
}

// ...
var clone = source.Clone( Transform.Zero, name: "Clone" );
var cloneComp = clone.GetComponent<ComponentWithActionGraph>();

var cloneAction = cloneComp.Func.GetActionGraphInstance();
Assert.AreSame( graph, cloneAction!.Graph );        // same ActionGraph instance
Assert.AreEqual( "Source", sourceComp.Func() );     // but different implied target
Assert.AreEqual( "Clone", cloneComp.Func() );
```

You don't write any of the retargeting code yourself; it falls out of `GameObject.Clone` pushing a `SerializationOptions` with a fresh `ImpliedTarget`/`GuidMap` around the clone operation (`engine/Sandbox.Engine/Scene/GameObject/GameObject.Clone.cs`).

### Hotload

Three `Hotload.InstanceUpgrader`s handle graphs across a hotload (`engine/Sandbox.Reflection/TypeLibrary/HotloadUpgraders.cs`):

- `ActionGraphUpgrader`: matches `ActionGraph` instances directly. It doesn't replace the instance (`OnTryCreateNewInstance` returns the same object), it patches it in place: rebuilds parameters, remaps variable and node-property types/values through the new assembly's types, and calls `graph.ClearChanges()` so the next validation pass doesn't register a spurious change.
- `ActionGraphDelegateUpgrader`: matches anything implementing `IActionGraphDelegate`. It rebuilds the outer delegate wrapper against the (already-upgraded) graph, the upgraded defaults dictionary, and the upgraded delegate type.
- `ActionGraphImplementedDelegateUpgrader`: matches plain `Delegate` instances that turn out to be backed by exactly one `ActionGraph` (via `GetActionGraphInstance()`). This is what lets a `[Property] public Action Foo { get; set; }` field survive hotload as a normal-looking delegate reference even though nothing about its declared type says "ActionGraph".

Net effect: a graph attached to a component property keeps working across a code hotload, with its node property values and variable defaults carried forward, as long as the types they reference still exist in the new assembly.

---

## Gotchas

- **A delegate-typed `[Property]` with nothing wired is `null`, not a no-op.** Always `?.Invoke()` it. Some built-in callbacks (`Component.OnComponentStart` and friends, `ActionsInvoker`) additionally swallow exceptions via `InvokeWithWarning()`; others (`Prop.OnPropBreak`, `Collider.OnTriggerEnter`) call `?.Invoke()` raw and will propagate an exception out of the firing method. Don't assume one behavior without checking the specific property.
- **`[Pure]` is a promise, not an inference.** The evaluator is free to call a pure node's method any number of times, in any order relative to other pure nodes, wherever its output is wired. Marking a method with real side effects (or non-idempotent behavior) as `[Pure]` will produce evaluation-order bugs that don't reproduce the same way twice.
- **A method with no `[Pure]`/`[Impure]` attribute is impure by default.** Every built-in node with side effects (`log.info`, `scene.instantiate`, `sound.play`) simply omits the attribute rather than writing `[Impure]` explicitly; only add `[Impure]` when you specifically need to override an otherwise-inferred pure classification (there isn't one from attributes alone, but the interface exists for that purpose).
- **`Span<T>`/`ref struct` and pointer parameters silently disqualify a method from node exposure**, `[ActionGraphNode]` or not. If a method you expect to see in the picker isn't there, check its parameter list against `AreParametersActionGraphSafe` before suspecting the attribute.
- **Public means exposed.** Any `public` property or field on your `Component` is a graph node by default, with no attribute required. If you don't want designers setting something from a graph, mark it `[ActionGraphIgnore]` explicitly, don't rely on it being "obviously internal."
- **`init`-only properties are never graph-writable**, even if public. The setter check explicitly rejects `IsExternalInit`.
- **Enum properties/fields are never graph-writable** by the automatic rule, regardless of visibility.
- **Custom `NodeDefinition` subclasses (dynamic node shapes like `op.convert`, `scene.get`) are internal, advanced machinery**, not a documented end-user API. If you need dynamic per-binding input/output shapes beyond what `[ActionGraphNode]` on a plain method gives you, that's editor/engine-internal territory this file doesn't cover further.
- **`ActionGraph.PushSerializationOptions` / `PushTarget` are ambient, scope-based, and easy to get wrong.** They're `IDisposable`-returning static calls you wrap in `using`; forgetting the `using` (or nesting scopes incorrectly) leaves the wrong implied target or cache active for unrelated (de)serialization elsewhere in the same call stack. Follow the pattern in `GameObject.Clone.cs` / `GameResource.CreateSerializationOptions` rather than improvising.
