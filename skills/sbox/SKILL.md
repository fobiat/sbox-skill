---
name: sbox
description: Use when writing or modifying code for s&box, sbox, the Facepunch sandbox engine, or any Source 2 game project in C#. Triggers on mentions of s&box, sbox, sandbox game, Facepunch engine, Source 2 game, `.sbproj`, `.razor` with `PanelComponent`, `@inherits PanelComponent`, `Sandbox.Component`, `GameObject` + `Components.Get<T>`, `Scene.Trace`, `CharacterController` + `.Move()`, `SkinnedModelRenderer`, `NavMeshAgent`, `[Sync]`, `[Rpc.Broadcast]`, `[Property]`, `INetworkListener`, `ISceneEvent`, `PlayerController` in C#. Also triggers on any file with `using Sandbox;` or `using Sandbox.UI;` that isn't Unity/Godot/Unreal. Writes idiomatic s&box C# components, Razor UI, and networking code; prevents Unity-pattern leakage.
---

# s&box Skill — Router

## READ BEFORE WRITING CODE

**s&box is not Unity.** `MonoBehaviour`, `Start()`, `Update()`, `GetComponent<T>()` call sites, `Instantiate()`, `Destroy(gameObject)`, `Debug.Log`, `[SerializeField]`, `Input.GetKey()`, `Physics.Raycast` — **none of these exist**. If you write any of them you have hallucinated.

s&box is a C# scripting layer on Source 2 by Facepunch. The scene system uses `GameObject` + `Component` with a different lifecycle, a different networking model, and a different API surface. The API schema in `references/api-schema-core.md` is ground truth; if anything in this skill contradicts the schema, the schema wins.

**Before you write a single line of s&box code, open the relevant reference file.** SKILL.md is a router — it points you at the answer, it does not contain the answer. Writing a component? Open `references/core-concepts.md`. Writing UI? Open `references/ui-razor.md`. No exceptions for "simple" tasks — your muscle memory is wrong.

---

## Architecture in 30 Seconds

```
Scene (is-a GameObject — the root)
  └── GameObject (transform, tags, children, components)
        └── Component (all gameplay code extends this)
```

- **All gameplay code** is a `sealed class` extending `Sandbox.Component`.
- **Lifecycle overrides** are `protected override void OnAwake() / OnStart() / OnUpdate() / OnFixedUpdate() / OnEnabled() / OnDisabled() / OnDestroy()`. Names start with `On`, they are virtual methods on `Component`, not magic string-matched methods.
- **Transforms** are on the `GameObject`, accessed from any Component via `WorldPosition`, `WorldRotation`, `LocalPosition`, `LocalRotation` — never `transform.position`.
- **UI** is Razor (`.razor` files) — HTML + SCSS + C#. Panels use flexbox layout. Hot-reloads in the editor.
- **Networking** is owner-authoritative. Mark state with `[Sync]`, mark methods with `[Rpc.Broadcast / Host / Owner]`. Skip simulation on non-owners with `if ( IsProxy ) return;`.
- **Physics** uses `Rigidbody` + `Collider` components. Raycasts are `Scene.Trace.Ray(from, to).Run()` — a builder-pattern API. Collisions come through `Component.ICollisionListener` and `Component.ITriggerListener` interfaces.
- **Coordinate system** is **Z-up**: `Vector3.Forward = (1,0,0)`, `Vector3.Right = (0,-1,0)`, `Vector3.Up = (0,0,1)`.
- **Restricted .NET:** `System.IO.File`, raw sockets, `Console`, `Thread`, `Process` — all blocked. Use `FileSystem.Data`, `Http`, `Log`, `async/await`.

---

## Routing Table — "I need to…"

Match the task, open the file. Do not guess; open the file.

| Task | Read |
|---|---|
| Understand the scene/GameObject/Component model | `references/core-concepts.md` |
| Write a `Component` (lifecycle, `[Property]`, Tags, async) | `references/core-concepts.md` |
| Spawn / clone / destroy a prefab | `references/core-concepts.md` → *Prefabs* |
| Fire a scene event (`ISceneEvent<T>`) | `references/core-concepts.md` → *Scene Events* |
| Write a `GameObjectSystem` | `references/core-concepts.md` → *GameObjectSystem* |
| Use `ModelRenderer` / `SkinnedModelRenderer` / bones / animgraph | `references/components-builtin.md` → *Rendering* |
| Use `Rigidbody`, any `Collider`, joints | `references/components-builtin.md` → *Physics* |
| Use `CharacterController` for movement | `references/components-builtin.md` → *CharacterController* |
| Use the full `PlayerController` (built-in FPS/TPS) | `references/components-builtin.md` → *Gameplay* |
| Make something usable — "walk up and press E" (`IPressable`) | `references/core-concepts.md` → *IPressable*, then `references/patterns-and-examples.md` → *Example 11* |
| Use `Prop`, or decide whether to compose renderer+collider+rigidbody yourself | `references/components-builtin.md` → *Prop* |
| Use the built-in inventory (`BaseInventoryComponent` / `BaseInventoryItem`) | `references/components-builtin.md` → *Inventory* |
| Set up a camera, HUD painter, post-processing | `references/components-builtin.md` → *Camera*, *Post-Processing* |
| Use lights, fog, envmap probes, skybox | `references/components-builtin.md` → *Lighting*, *Environment* |
| Add audio (`SoundPointComponent`, `Sound.Play`) | `references/components-builtin.md` → *Audio* |
| Use `NavMeshAgent`, `NavMeshLink`, query NavMesh | `references/components-builtin.md` → *Navigation* |
| Create particles, decals, trails, beams | `references/components-builtin.md` → *Effects*, *Rendering* |
| Write a Razor UI panel (`.razor`, `PanelComponent`, `BuildHash`) | `references/ui-razor.md` |
| Style with SCSS — flexbox, transitions, `:intro` / `:outro`, `:bind` | `references/ui-razor.md` → *Styling*, *Layout System*, *Transitions* |
| Use built-in controls (`Button`, `TextEntry`, `DropDown`, `VirtualList`) | `references/ui-razor.md` → *Built-in Controls* |
| Build a world-space panel or a NavigationHost app | `references/ui-razor.md` → *WorldPanel*, *Navigation* |
| Set up a lobby, connect/disconnect, query `Connection` | `references/networking.md` → *Lobby & Connection* |
| Network an object (`NetworkMode`, `NetworkSpawn`, ownership) | `references/networking.md` → *Networked Objects*, *Ownership* |
| Use `[Sync]` / `[Sync(SyncFlags.X)]` / `[Change]` / `NetList` / `NetDictionary` | `references/networking.md` → *[Sync] Properties* |
| Write RPCs (`[Rpc.Broadcast/Host/Owner]`, `NetFlags`, caller info, filtering) | `references/networking.md` → *RPC Messages* |
| React to connections (`INetworkListener`, `INetworkSpawn`, snapshot data) | `references/networking.md` → *Network Events* |
| Use `ISceneStartup` for host vs client initialization | `references/networking.md` → *Scene Startup* |
| Dedicated server / `#if SERVER` / user permissions | `references/networking.md` → *Dedicated Servers* |
| Poll keyboard/mouse/controller input, haptics, glyphs | `references/input-and-physics.md` → *Input* |
| Raycast / sphere / box / capsule trace with tag filters | `references/input-and-physics.md` → *SceneTrace* |
| Access `PhysicsWorld`, gravity, physics events | `references/input-and-physics.md` → *Physics World* |
| Implement collision/trigger listeners | `references/input-and-physics.md` → *Collision System* |
| Use `Vector3` / `Rotation` / `Angles` / `Transform` / `BBox` / `Ray` / `Capsule` | `references/input-and-physics.md` → *Math Types* |
| Use `Time.Now`, `Time.Delta`, `TimeSince`, `TimeUntil` | `references/input-and-physics.md` → *Time* |
| Draw debug gizmos (`DrawGizmos`, `Gizmo.Draw`) | `references/input-and-physics.md` → *Gizmo* |
| Declare/author a custom data asset (`GameResource` + `[AssetType]`, `.item` JSON) | `references/api-schema-core.md` → *GameResource & `[AssetType]`* |
| Load a `Model` / `Material` / resource by path safely | `references/api-schema-core.md` → *Model*, *GameResource & `[AssetType]`* |
| Edit `Input.config` / `Platform.config`, or kill the built-in platform chat | `references/input-and-physics.md` → *Project Settings Configs*, *Platform Chat* |
| Drive the editor over MCP (assets, scene, compile, play mode) | `references/editor-and-verified-behaviour.md` → *The Editor MCP Server* |
| Find out what has actually been proven in a live editor session | `references/editor-and-verified-behaviour.md` → *Verified Networking Behaviour* |
| Need the full signature of `GameObject`, `Component`, `Scene`, `Input`, etc. | `references/api-schema-core.md` |
| Look up whether a given type exists & what it does | `references/api-schema-extended.md` |
| See a complete worked example of a pattern before writing your own | `references/patterns-and-examples.md` |

---

## Unity → s&box Translation Table

Any time you write one of the left column, you are hallucinating. Use the right column.

| Unity / Wrong | s&box / Correct |
|---|---|
| `class Foo : MonoBehaviour` | `public sealed class Foo : Component` |
| `void Awake()` | `protected override void OnAwake()` |
| `void Start()` | `protected override void OnStart()` |
| `void Update()` | `protected override void OnUpdate()` |
| `void FixedUpdate()` | `protected override void OnFixedUpdate()` |
| `void OnEnable()` / `OnDisable()` | `protected override void OnEnabled()` / `OnDisabled()` |
| `void OnDestroy()` | `protected override void OnDestroy()` |
| `[SerializeField] float speed` | `[Property] public float Speed { get; set; }` |
| `[HideInInspector]` | `[Hide]` |
| `transform.position` | `WorldPosition` (or `GameObject.WorldPosition`) |
| `transform.localPosition` | `LocalPosition` |
| `transform.rotation` | `WorldRotation` |
| `transform.forward` | `WorldRotation.Forward` |
| `gameObject.SetActive(false)` | `GameObject.Enabled = false` |
| `Destroy(gameObject)` / `Destroy(this)` | `GameObject.Destroy()` / `Component.Destroy()` / `DestroyGameObject()` |
| `Instantiate(prefab, pos, rot)` | `prefab.Clone( pos, rot )` |
| `Instantiate(prefab); NetworkServer.Spawn(...)` | `prefab.Clone(pos).NetworkSpawn( owner )` |
| `GetComponent<T>()` in `Start/Update` | `GetComponent<T>()` is fine; also `Components.Get<T>( FindMode )` for ancestor/descendant searches |
| `FindObjectOfType<T>()` / `FindObjectsOfType<T>()` | `Scene.Get<T>()` / `Scene.GetAll<T>()` / `Scene.GetAllComponents<T>()` |
| `GameObject.Find("Name")` | `Scene.Directory.FindByName("Name")` |
| `OnCollisionEnter(Collision c)` | Implement `Component.ICollisionListener.OnCollisionStart(Collision c)` |
| `OnTriggerEnter(Collider c)` | Implement `Component.ITriggerListener.OnTriggerEnter(Collider c)` |
| `Physics.Raycast(...)` | `Scene.Trace.Ray( from, to ).Run()` (builder; returns `SceneTraceResult`) |
| `Physics.OverlapSphere(pos, r)` | `Scene.Trace.Sphere( r, pos, pos ).RunAll()` |
| `Rigidbody.AddForce(f, ForceMode.Impulse)` | `Rigidbody.ApplyImpulse( f )` |
| `Rigidbody.AddForce(f)` | `Rigidbody.ApplyForce( f )` |
| `Rigidbody.velocity` | `Rigidbody.Velocity` (capital V) |
| `Input.GetKey(KeyCode.W)` | `Input.Down( "forward" )` — actions are strings configured in Project Settings |
| `Input.GetKeyDown(...)` | `Input.Pressed( "action" )` |
| `Input.GetAxis("Horizontal")` / `Vertical` | `Input.AnalogMove` (`Vector3`) |
| `Input.mousePosition` | `Mouse.Position` (`Vector2`) |
| `Camera.main` | `Scene.Camera` |
| `Camera.main.ScreenPointToRay(Input.mousePosition)` | `Scene.Camera.ScreenPixelToRay( Mouse.Position )` |
| `StartCoroutine(Foo())` with `IEnumerator` | `async Task Foo()` with `await Task.DelaySeconds(...)`; call as `_ = Foo();` |
| `yield return new WaitForSeconds(1f)` | `await Task.DelaySeconds( 1f )` |
| `yield return null` | `await Task.Frame()` |
| `Debug.Log(x)` / `.LogWarning` / `.LogError` | `Log.Info(x)` / `Log.Warning(x)` / `Log.Error(x)` |
| `Time.time` | `Time.Now` |
| `Time.deltaTime` | `Time.Delta` |
| `Time.fixedDeltaTime` | `Scene.FixedDelta` |
| `Mathf.Lerp / Clamp / Approach` | `MathX.Lerp / Clamp / Approach` |
| `Random.Range(a, b)` | `Game.Random.Next(a, b)` / `Game.Random.NextSingle()` |
| `Vector3.forward = (0,0,1)` | `Vector3.Forward = (1,0,0)` — **s&box is Z-up**; re-check every literal direction |
| `SceneManager.LoadScene("name")` | `Scene.LoadFromFile("path/to/scene.scene")` or `Scene.Load( sceneResource )` |
| `DontDestroyOnLoad(go)` | `go.Flags = GameObjectFlags.DontDestroyOnLoad` |
| `class Foo : ScriptableObject` + `[CreateAssetMenu]` | `[AssetType( Name = ..., Extension = ..., Category = ... )] class Foo : GameResource` |
| `Resources.Load<T>("path")` / `Resources.FindObjectsOfTypeAll<T>()` | `ResourceLibrary.Get<T>( "path.ext" )` / `ResourceLibrary.GetAll<T>()` |
| `Application.isPlaying` | `Game.IsPlaying` (or `!Game.IsEditor`) |
| `System.IO.File.ReadAllText(...)` | `FileSystem.Data.ReadAllText(...)` / `FileSystem.Mounted.ReadAllText(...)` |
| `UnityEngine.Networking.UnityWebRequest` | `Http.RequestStringAsync(...)` / `Http.RequestJsonAsync<T>(...)` |
| `Update()` reads `Input.*` AND moves rigidbody | Read input in `OnUpdate`, move in `OnFixedUpdate` |

If a Unity pattern isn't in the table, assume it doesn't exist in s&box and look it up in `references/api-schema-core.md` before writing it.

---

## The Ten Rules You Must Not Break

1. **Every gameplay class extends `Component`.** Not `MonoBehaviour`, not `object`, not `ScriptableObject` — just `Component`. Mark it `sealed` unless inheritance is required.
2. **Lifecycle methods are `protected override void On*()`.** If you wrote `void Update()` instead of `protected override void OnUpdate()`, your code does nothing.
3. **Serialize fields with `[Property]`.** Not `[SerializeField]`, not `public` alone. `[Property]` both shows in the inspector and saves to prefab/scene.
4. **Networked state uses `[Sync]`.** Only the object owner may assign (or only the host, with `SyncFlags.FromHost`). Everyone else sees replicated values, and their writes are **discarded silently** — no exception, no warning, the backing field is never touched. Combine with `[Change(nameof(Method))]` for scalar change callbacks; for `NetList` / `NetDictionary` subscribe to the collection's own `OnChanged` field instead. And the object must be `NetworkSpawn`ed for any of this to replicate at all.
5. **Guard networked logic with `if ( IsProxy ) return;`.** Every component that reads input or drives movement starts with this line — otherwise every client tries to move every player.
6. **Ray/box/sphere traces go through `Scene.Trace`.** It's a builder: `Scene.Trace.Ray(from, to).UseHitboxes(true).WithoutTags(new[]{"player"}).Run()` returns a `SceneTraceResult`. Never `Physics.Raycast`.
7. **UI is Razor + flexbox.** `display: flex` is the default and effectively the only layout. `display: block` does not exist. Properties `:intro` / `:outro` animate creation and deletion. Every root panel overrides `BuildHash()` to control re-render.
8. **Coroutines don't exist — use `async Task`.** `await Task.DelaySeconds( n )`, `await Task.Frame()`. Fire-and-forget with `_ = MyTask();`. The `Component.Task` property scopes cancellation to the GameObject lifetime.
9. **Never touch blocked .NET APIs.** `System.IO.File`, `Console`, `Thread`, raw sockets, `System.Net.Http.HttpClient` — use `FileSystem.Data`, `Log`, `async/await`, `Http` instead. Code that references these won't compile in the sandbox.
10. **Look up every API before you use it.** The schema is ground truth. If you can't find a method in `api-schema-core.md` or `api-schema-extended.md`, either you're guessing (stop) or it's nested on a specific type (search the topical reference).

---

## Project Structure (s&box Game)

An s&box game project (`.sbproj`) typically looks like:

```
MyGame/
├── MyGame.sbproj                 # project manifest
├── code/                         # C# source (gameplay code)
│   ├── GameManager.cs
│   ├── Player.cs
│   ├── weapons/
│   │   └── Rifle.cs
│   └── ui/
│       ├── Hud.razor             # Razor panel
│       ├── Hud.razor.scss        # auto-loaded stylesheet
│       └── InventoryPanel.razor
├── ProjectSettings/              # Input.config, Platform.config, Collision.config …
├── prefabs/                      # .prefab files
├── scenes/                       # .scene files
├── models/                       # .vmdl, .vmdl_c
├── materials/                    # .vmat, .vmat_c
├── sounds/                       # .sound, .wav, .mp3
├── ui/                           # UI images, textures
└── localization/
    └── en/
        └── mygame.json
```

Notable:
- `.razor` and `.razor.scss` files pair by name — the stylesheet auto-loads when the panel is built.
- `.cs` files are hot-reloaded in the editor; saving a file rebuilds and re-injects within seconds. **Do not rely on this when editing from outside the editor** — see `references/editor-and-verified-behaviour.md`.
- Asset paths in code are forward-slash strings rooted at the project: `Model.Load( "models/dev/box.vmdl" )`.
- There is no `Assets/` folder; paths are flat under the project root.
- `ProjectSettings/*.config` and the `.sbproj` are read at boot and **not watched** — an external edit needs an explicit reload or an editor restart.

---

## A Reference Component (Shape Only — Not the Content)

This is the *shape* of a component. The *content* depends on what you're building — read the referenced file, then write it.

```csharp
using Sandbox;

public sealed class MyComponent : Component
{
    [Property] public float Speed { get; set; } = 200f;
    [Property] public GameObject Target { get; set; }

    [Sync] public int Score { get; set; }

    TimeSince _lastAction;

    protected override void OnStart()
    {
        // runs once before first update
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;        // networking guard
        if ( !Target.IsValid() ) return;

        // per-frame logic (input, visuals, camera)
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;
        // physics / movement — deterministic timestep
    }

    [Rpc.Broadcast]
    public void PlayEffect( Vector3 position )
    {
        // runs on all clients — cosmetic / non-authoritative
    }
}
```

For complete runnable examples — a full FPS controller, a networked player, a Razor HUD, a hitscan weapon, a NavMeshAgent AI, a physics grenade, a prefab spawner, a trigger pickup, a press-E `IPressable` vendor — see `references/patterns-and-examples.md`.

---

## Gotchas Captured From Real Builds

These are things that will bite you. They're documented deeper in the reference files; this is the quick-lookup list.

- `ICollisionListener` parameter names are `collision`, not `other` (the raw docs are wrong about this).
- `Color`, `Capsule`, `Vector3`, `Rotation`, `Angles`, `Transform`, `BBox`, `Ray` are **global** types, not `Sandbox.*`.
- `LobbyConfig` and `LobbyPrivacy` live in `Sandbox.Network` — you need `using Sandbox.Network;`.
- `Scene` is-a `GameObject` — it *is* the root GameObject. `Scene.GetAllObjects(true)` walks the tree.
- Most `Component.ISomething` interfaces are **nested** on `Component`: `Component.IDamageable`, `Component.ICollisionListener`, `Component.ITriggerListener`, `Component.INetworkListener`, `Component.INetworkSpawn`. But `IGameObjectNetworkEvents` is top-level `Sandbox.IGameObjectNetworkEvents`.
- `ComponentList.GetOrCreate<T>( FindMode )` requires the `FindMode` arg. For the common "on this GameObject" case, use `GetOrAddComponent<T>()` on `GameObject` or `Component` instead.
- `SceneTrace.WithoutTags` / `WithAnyTags` / `WithAllTags` take `string[]` — not `params`. Pass `new[] { "tag" }`, or use `WithTag(string)` for the single-tag case.
- `Game.Random` is `System.Random` — `NextSingle()`, `Next(n)` work directly. The `FromList` extension exists but requires a `defVal`; `list[Game.Random.Next(list.Count)]` is simpler.
- `FileSystem` is a static facade. Actual methods are on `BaseFileSystem`, accessed via `FileSystem.Data` (game user data) or `FileSystem.Mounted` (mounted content).
- There is no standalone `Log` class — it's `Sandbox.Diagnostics.Logger`, but the global `Log` instance works fine everywhere.
- `PlayerController.TraceBody` has **4** parameters (the 4th is `heightScale`), not 3.
- Operators (`Rotation * Rotation`, `Vector3 * Rotation`) aren't listed in the API schema because they're systematically excluded — they do exist, use them normally.
- `NavigationHost` is in `Sandbox.UI.Navigation`, needs `@using Sandbox.UI.Navigation` in your Razor file.
- **`[GameResource(...)]` is obsolete engine-wide.** Custom data assets use `[AssetType( Name = ..., Extension = ..., Category = ... )]` — property initializers, not constructor args. Under `TreatWarningsAsErrors` the old attribute is a build failure.
- **`Model.Load( "typo/path.vmdl" )` returns `null`**, not `Model.Error`. Only a null/empty path gives you the error placeholder. Null-check every `Model.Load`.
- **`GameObject.NetworkSpawn()` with no arguments owns the object to `Connection.Local`** — whoever called it. For a host-authoritative world object that is a quiet wrong-owner trap. Always pass an explicit owner.
- **`NetworkMode.Snapshot` is the default for scene objects and does not live-replicate `[Sync]`** — while RPCs keep working perfectly. Stale-state bugs here look like nothing is wrong.
- **`[Sync]` writes you aren't allowed to make are dropped before the backing field** — no exception, no warning, and the immediate read-back already shows the authoritative value. Same for every `NetList<T>` mutation.
- **`NetList<T>.OnChanged` is a public `Action<NetListChangeEvent<T>>` field, not `[Change]`.** `[Change]` wraps the property *setter*, so on a collection it only fires if you reassign the whole thing.
- **`IPressable.Press` executes on the pressing CLIENT.** The whole USE pipeline runs inside `if ( !IsProxy )` on the player. Authoritative effects need an `[Rpc.Host]`.
- **`Prop` is breakable, flammable and gibbable, not just "a model with physics."** For a non-destructible object compose `ModelRenderer` + `ModelCollider` + `Rigidbody` yourself. Its `Break()` method is an editor button that decomposes the component — it does not break the prop.
- **`Sandbox.Platform.Chat` is a global pipe that exists whether your gamemode uses it or not.** `ChatShowUI: false` only hides the overlay; `Say()` still broadcasts. Disable it at the source with `"ChatEnabled": false` in `ProjectSettings/Platform.config`.
- **Externally edited `.sbproj` / `.config` files are not watched**, and after in-process compiler recreation the source file watchers stop firing. Rebuild explicitly and check compile status; never infer success from the absence of errors.
- `PlayerController.IEvents.PostCameraSetup` is obsolete — implement `ICameraModifier` instead.

---

## Verification Loop (When In Doubt)

If you're about to write an API call and you're not sure it exists:

1. **Check the topical file** for the area first (`networking.md`, `ui-razor.md`, etc.). Topical files include inline signatures for the APIs they cover.
2. **If not there, check `api-schema-core.md`.** It has full signatures for the ~50 most-used types.
3. **If not there, check `api-schema-extended.md`.** It's a namespace-organized index of 738 more types — find the type, then look up full signatures elsewhere if needed.
4. **If a method or property is in NONE of the above, it does not exist.** Do not write it. Revisit your design — there is almost certainly an s&box-idiomatic way to do what you want.

A schema entry proves an API *exists*; it does not prove it *behaves* the way you assume.
The traps in this skill are almost all cases where a correct-looking call silently does
nothing. `references/editor-and-verified-behaviour.md` records which behaviours have been
observed in a live editor session (with dates and evidence) versus merely read out of the
source — check it before you conclude "the API says this should work."

The schema under `raw/api-schema.json` is not shipped with the skill and is not for you to read directly; the reference files are the curated, searchable view of it.

---

*This file is a router. Reference files teach. Do not answer s&box questions from SKILL.md alone — open the relevant reference and read it.*
