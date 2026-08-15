---
name: sbox
description: Use when writing or modifying code for s&box, sbox, the Facepunch sandbox engine, or any Source 2 game project in C#. Triggers on mentions of s&box, sbox, sandbox game, Facepunch engine, Source 2 game, `.sbproj`, `.razor` with `PanelComponent`, `@inherits PanelComponent`, `Sandbox.Component`, `GameObject` + `Components.Get<T>`, `Scene.Trace`, `CharacterController` + `.Move()`, `SkinnedModelRenderer`, `NavMeshAgent`, `[Sync]`, `[Rpc.Broadcast]`, `[Property]`, `[AssetType]`, `INetworkListener`, `ISceneEvent`, `PlayerController`, `ClothingContainer`, `EditorTool`, `Mixer`, `.shader` files in a Source 2 project. Also triggers on any file with `using Sandbox;` or `using Sandbox.UI;` that is not Unity, Godot or Unreal. Covers gameplay components, Razor UI, networking, editor extensions, avatars, shaders, audio, services and persistence. Prevents Unity-pattern leakage.
---

<!--
  s&box Skill : SKILL.md

  Router. Identifies which reference file answers the task and sends the reader there.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# s&box: Router

## Read this before writing code

**s&box is not Unity.** `MonoBehaviour`, `Start()`, `Update()`, `GetComponent<T>()` call sites, `Instantiate()`, `Destroy(gameObject)`, `Debug.Log`, `[SerializeField]`, `Input.GetKey()`, `Physics.Raycast`: none of these exist. If you have written one, you have invented it.

s&box is a C# scripting layer on Source 2, built by Facepunch. It borrows the `GameObject` plus `Component` shape from Unity and almost nothing else. The lifecycle is different, the networking model is different, the coordinate system is different, and most of the API surface is named differently.

That surface similarity is the whole problem. Unity muscle memory produces code that looks right, reads right, and does not compile.

**Open the relevant reference file before you write a single line.** This file is a router. It tells you where the answer is; it does not contain the answer. Writing a component? Open `references/01_SCENE.md`. Writing UI? Open `references/03_UI.md`. There is no exception for a task that feels simple, because the tasks that feel simple are the ones muscle memory ruins.

The API schema is ground truth. Where this file and the schema disagree, the schema wins.

---

## The architecture in thirty seconds

```
Scene (is-a GameObject, the root)
  └── GameObject (transform, tags, children, components)
        └── Component (all gameplay code extends this)
```

- **All gameplay code** is a `sealed class` extending `Sandbox.Component`.
- **Lifecycle overrides** are `protected override void OnAwake() / OnStart() / OnUpdate() / OnFixedUpdate() / OnEnabled() / OnDisabled() / OnDestroy()`. They are virtual methods on `Component`, not magic methods matched by name, and they all start with `On`.
- **Transforms live on the GameObject**, reachable from any Component as `WorldPosition`, `WorldRotation`, `LocalPosition`, `LocalRotation`. There is no `transform`.
- **Game UI is Razor**: `.razor` files holding HTML, SCSS and C#, laid out with flexbox, hot-reloaded in the editor. **Editor UI is not Razor.** It is a separate Qt-backed `Widget` system, and confusing the two is a common and expensive mistake. See `references/06_EDITOR.md`.
- **Networking is owner-authoritative.** Mark state `[Sync]`, mark methods `[Rpc.Broadcast / Host / Owner]`, and skip simulation on non-owners with `if ( IsProxy ) return;`.
- **Physics** is `Rigidbody` plus `Collider` components. Traces are a builder: `Scene.Trace.Ray( from, to ).Run()`. Collisions arrive through `Component.ICollisionListener` and `Component.ITriggerListener`.
- **The coordinate system is Z-up.** `Vector3.Forward = (1,0,0)`, `Vector3.Right = (0,-1,0)`, `Vector3.Up = (0,0,1)`. Re-check every literal direction you write.
- **.NET is restricted.** `System.IO.File`, raw sockets, `Console`, `Thread` and `Process` are blocked at compile time. Use `FileSystem.Data`, `Http`, `Log` and `async/await`.

---

## Routing table

Match the task, open the file. Do not guess, open the file.

| Task | Read |
|---|---|
| Understand the Scene, GameObject and Component model | `references/01_SCENE.md` |
| Write a `Component` (lifecycle, `[Property]`, tags, async) | `references/01_SCENE.md` |
| Spawn, clone or destroy a prefab | `references/01_SCENE.md`, *Prefabs* |
| Fire a scene event (`ISceneEvent<T>`) | `references/01_SCENE.md`, *Scene Events* |
| Write a `GameObjectSystem` | `references/01_SCENE.md`, *GameObjectSystem* |
| Make something usable, walk up and press E (`IPressable`) | `references/01_SCENE.md`, *IPressable*, then `references/13_EXAMPLES.md` |
| Declare a custom data asset (`GameResource` + `[AssetType]`) | `references/15_API_CORE.md`, *GameResource* |
| Use `ModelRenderer`, `SkinnedModelRenderer`, bones, animgraph | `references/02_COMPONENTS.md`, *Rendering* |
| Use `Rigidbody`, colliders, joints | `references/02_COMPONENTS.md`, *Physics* |
| Move something with `CharacterController` | `references/02_COMPONENTS.md`, *CharacterController* |
| Use the built-in `PlayerController` | `references/02_COMPONENTS.md`, *Gameplay* |
| Use `Prop`, or compose renderer + collider + rigidbody yourself | `references/02_COMPONENTS.md`, *Prop* |
| Use the built-in inventory components | `references/02_COMPONENTS.md`, *Inventory* |
| Set up a camera, HUD painter or post-processing | `references/02_COMPONENTS.md`, *Camera* |
| Use lights, fog, envmap probes, skybox | `references/02_COMPONENTS.md`, *Lighting* |
| Use `NavMeshAgent`, `NavMeshLink`, query the NavMesh | `references/02_COMPONENTS.md`, *Navigation* |
| Create particles, decals, trails, beams | `references/02_COMPONENTS.md`, *Effects* |
| Write a Razor panel (`.razor`, `PanelComponent`, `BuildHash`) | `references/03_UI.md` |
| Style with SCSS, flexbox, `:intro` / `:outro`, `:bind` | `references/03_UI.md`, *Styling* |
| Use built-in controls (`Button`, `TextEntry`, `DropDown`, `VirtualList`) | `references/03_UI.md`, *Built-in Controls* |
| Build a world-space panel or a NavigationHost app | `references/03_UI.md`, *WorldPanel* |
| Set up a lobby, connect, disconnect, query `Connection` | `references/04_NETWORKING.md`, *Lobby & Connection* |
| Network an object (`NetworkMode`, `NetworkSpawn`, ownership) | `references/04_NETWORKING.md`, *Networked Objects* |
| Use `[Sync]`, `[Change]`, `NetList`, `NetDictionary` | `references/04_NETWORKING.md`, *Sync Properties* |
| Write RPCs (`[Rpc.Broadcast/Host/Owner]`, `NetFlags`, filtering) | `references/04_NETWORKING.md`, *RPC Messages* |
| React to connections (`INetworkListener`, `INetworkSpawn`) | `references/04_NETWORKING.md`, *Network Events* |
| Split host and client startup (`ISceneStartup`) | `references/04_NETWORKING.md`, *Scene Startup* |
| Run a dedicated server, `#if SERVER`, permissions | `references/04_NETWORKING.md`, *Dedicated Servers* |
| Poll keyboard, mouse, controller, haptics, glyphs | `references/05_INPUT_PHYSICS.md`, *Input* |
| Trace a ray, sphere, box or capsule with tag filters | `references/05_INPUT_PHYSICS.md`, *SceneTrace* |
| Reach `PhysicsWorld`, gravity, physics events | `references/05_INPUT_PHYSICS.md`, *Physics World* |
| Implement collision and trigger listeners | `references/05_INPUT_PHYSICS.md`, *Collision System* |
| Use `Vector3`, `Rotation`, `Angles`, `Transform`, `BBox`, `Ray` | `references/05_INPUT_PHYSICS.md`, *Math Types* |
| Use `Time.Now`, `Time.Delta`, `TimeSince`, `TimeUntil` | `references/05_INPUT_PHYSICS.md`, *Time* |
| Draw debug gizmos (`DrawGizmos`, `Gizmo.Draw`) | `references/05_INPUT_PHYSICS.md`, *Gizmo* |
| Write an editor tool, custom inspector or dock | `references/06_EDITOR.md` |
| Build editor UI with `Widget` and `Layout` (not Razor) | `references/06_EDITOR.md`, *Widget system* |
| Record stats, read or submit leaderboards, unlock achievements | `references/07_SERVICES.md` |
| Save and load player or game data | `references/07_SERVICES.md`, *Persistence* |
| Query, mount or read a `Package` | `references/07_SERVICES.md`, *Package* |
| Dress a player, use `Clothing` or `ClothingContainer` | `references/08_AVATARS.md` |
| Find the Citizen model, body groups or material groups | `references/08_AVATARS.md` |
| Write a `.shader`, or set material and render attributes | `references/09_RENDERING.md` |
| Work with render layers, custom render objects, `CommandList` | `references/09_RENDERING.md` |
| Route sound through the mixer graph, control a `SoundHandle` | `references/10_AUDIO.md` |
| Localize text, use `Phrase` or `#` tokens | `references/10_AUDIO.md`, *Localization* |
| Expose a C# method as a graph node, or a graph-backed callback | `references/11_ACTIONGRAPH.md` |
| Serialize, invoke or hotload an ActionGraph from code | `references/11_ACTIONGRAPH.md`, *Serialization* |
| Detect VR, read the rig, controllers or haptics | `references/12_VR_VOICE.md` |
| Capture, transmit or play voice chat, show a speaking indicator | `references/12_VR_VOICE.md`, *Voice* |
| Edit `Input.config` or `Platform.config`, kill the platform chat | `references/05_INPUT_PHYSICS.md`, *Project Settings Configs* |
| Drive the editor over MCP (assets, scene, compile, play mode) | `references/14_VERIFICATION.md`, *Editor MCP Server* |
| Find out what has actually been proven in a live session | `references/14_VERIFICATION.md`, *Verified Behaviour* |
| Get the full signature of `GameObject`, `Component`, `Scene`, `Input` | `references/15_API_CORE.md` |
| Check whether a type exists at all | `references/16_API_INDEX.md` |
| See a complete worked example before writing your own | `references/13_EXAMPLES.md` |

---

## Unity to s&box

Any time you write the left column, you are inventing an API. Use the right.

| Unity, wrong | s&box, correct |
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
| `transform.position` | `WorldPosition` |
| `transform.localPosition` | `LocalPosition` |
| `transform.rotation` | `WorldRotation` |
| `transform.forward` | `WorldRotation.Forward` |
| `gameObject.SetActive(false)` | `GameObject.Enabled = false` |
| `Destroy(gameObject)` | `GameObject.Destroy()` / `Component.Destroy()` / `DestroyGameObject()` |
| `Instantiate(prefab, pos, rot)` | `prefab.Clone( pos, rot )` |
| `Instantiate(prefab); NetworkServer.Spawn(...)` | `prefab.Clone(pos).NetworkSpawn( owner )` |
| `GetComponent<T>()` in `Start`/`Update` | `GetComponent<T>()` works; use `Components.Get<T>( FindMode )` for ancestor or descendant searches |
| `FindObjectOfType<T>()` | `Scene.Get<T>()` / `Scene.GetAll<T>()` / `Scene.GetAllComponents<T>()` |
| `GameObject.Find("Name")` | `Scene.Directory.FindByName("Name")` |
| `OnCollisionEnter(Collision c)` | `Component.ICollisionListener.OnCollisionStart(Collision c)` |
| `OnTriggerEnter(Collider c)` | `Component.ITriggerListener.OnTriggerEnter(Collider c)` |
| `Physics.Raycast(...)` | `Scene.Trace.Ray( from, to ).Run()`, returns `SceneTraceResult` |
| `Physics.OverlapSphere(pos, r)` | `Scene.Trace.Sphere( r, pos, pos ).RunAll()` |
| `Rigidbody.AddForce(f, ForceMode.Impulse)` | `Rigidbody.ApplyImpulse( f )` |
| `Rigidbody.AddForce(f)` | `Rigidbody.ApplyForce( f )` |
| `Rigidbody.velocity` | `Rigidbody.Velocity` |
| `Input.GetKey(KeyCode.W)` | `Input.Down( "forward" )`, actions are strings set in Project Settings |
| `Input.GetKeyDown(...)` | `Input.Pressed( "action" )` |
| `Input.GetAxis("Horizontal")` | `Input.AnalogMove`, a `Vector3` |
| `Input.mousePosition` | `Mouse.Position`, a `Vector2` |
| `Camera.main` | `Scene.Camera` |
| `Camera.main.ScreenPointToRay(...)` | `Scene.Camera.ScreenPixelToRay( Mouse.Position )` |
| `StartCoroutine(Foo())` | `async Task Foo()`, called as `_ = Foo();` |
| `yield return new WaitForSeconds(1f)` | `await Task.DelaySeconds( 1f )` |
| `yield return null` | `await Task.Frame()` |
| `Debug.Log(x)` | `Log.Info(x)` / `Log.Warning(x)` / `Log.Error(x)` |
| `Time.time` | `Time.Now` |
| `Time.deltaTime` | `Time.Delta` |
| `Time.fixedDeltaTime` | `Scene.FixedDelta` |
| `Mathf.Lerp / Clamp / Approach` | `MathX.Lerp / Clamp / Approach` |
| `Random.Range(a, b)` | `Game.Random.Next(a, b)` / `Game.Random.NextSingle()` |
| `Vector3.forward = (0,0,1)` | `Vector3.Forward = (1,0,0)`, s&box is Z-up |
| `SceneManager.LoadScene("name")` | `Scene.LoadFromFile("path/to/scene.scene")` |
| `DontDestroyOnLoad(go)` | `go.Flags = GameObjectFlags.DontDestroyOnLoad` |
| `class Foo : ScriptableObject` + `[CreateAssetMenu]` | `[AssetType( Name = ..., Extension = ..., Category = ... )] class Foo : GameResource` |
| `Resources.Load<T>("path")` | `ResourceLibrary.Get<T>( "path.ext" )` |
| `Application.isPlaying` | `Game.IsPlaying` |
| `System.IO.File.ReadAllText(...)` | `FileSystem.Data.ReadAllText(...)` |
| `UnityWebRequest` | `Http.RequestStringAsync(...)` / `Http.RequestJsonAsync<T>(...)` |
| `PlayerPrefs` | `Game.Cookies`, see `references/07_SERVICES.md` |
| `[MenuItem(...)]` editor script | `[EditorTool]` / `[CustomEditor]`, see `references/06_EDITOR.md` |
| `Update()` reads input and moves a rigidbody | Read input in `OnUpdate`, move in `OnFixedUpdate` |

If a Unity pattern is not in this table, assume it does not exist and look it up in `references/15_API_CORE.md` before writing it.

---

## The ten rules

1. **Every gameplay class extends `Component`.** Not `MonoBehaviour`, not `object`, not `ScriptableObject`. Mark it `sealed` unless something genuinely inherits from it.
2. **Lifecycle methods are `protected override void On*()`.** Write `void Update()` instead of `protected override void OnUpdate()` and your code silently never runs.
3. **Serialize with `[Property]`.** Not `[SerializeField]`, not bare `public`. `[Property]` both shows the field in the inspector and saves it into the prefab or scene.
4. **Networked state uses `[Sync]`.** Only the owner may assign, or only the host with `SyncFlags.FromHost`. Everyone else sees replicated values and their writes are discarded silently, with no exception and no warning. Pair with `[Change(nameof(Method))]` for scalars; for `NetList` and `NetDictionary` subscribe to the collection's own `OnChanged` field instead. None of it replicates unless the object was `NetworkSpawn`ed.
5. **Guard networked logic with `if ( IsProxy ) return;`.** Any component that reads input or drives movement opens with this line. Without it every client tries to move every player.
6. **Traces go through `Scene.Trace`.** It is a builder: `Scene.Trace.Ray(from, to).UseHitboxes(true).WithoutTags("player").Run()`. Never `Physics.Raycast`.
7. **Game UI is Razor and flexbox.** `display: flex` is the default and effectively the only layout; `display: block` does not exist. `:intro` and `:outro` animate creation and deletion. Root panels override `BuildHash()` to control re-render. Editor UI is a different system entirely.
8. **There are no coroutines.** Use `async Task` with `await Task.DelaySeconds( n )` and `await Task.Frame()`. Fire and forget with `_ = MyTask();`. The `Component.Task` property scopes cancellation to the GameObject's lifetime.
9. **Never touch blocked .NET APIs.** `System.IO.File`, `Console`, `Thread`, raw sockets and `HttpClient` are rejected by the sandbox compiler, not at runtime. Use `FileSystem.Data`, `Log`, `async/await` and `Http`.
10. **Look up every API before you use it.** If you cannot find a method in `15_API_CORE.md` or `16_API_INDEX.md`, either you are guessing, or it is nested on a specific type. Stop and check.

---

## Project layout

```
MyGame/
├── MyGame.sbproj                 # project manifest
├── Code/                         # C# gameplay source
│   ├── GameManager.cs
│   ├── Player.cs
│   └── UI/
│       ├── Hud.razor
│       ├── Hud.razor.scss        # auto-loaded, pairs by name
│       └── InventoryPanel.razor
├── Editor/                       # editor-only code, tools and inspectors
├── ProjectSettings/              # Input.config, Platform.config, Collision.config
├── prefabs/
├── scenes/
├── models/                       # .vmdl
├── materials/                    # .vmat
├── shaders/                      # .shader
├── sounds/
└── Localization/
    └── en/
        └── mygame.json
```

- `.razor` and `.razor.scss` pair by filename, and the stylesheet loads itself when the panel is built.
- Asset paths in code are forward-slash strings rooted at the project: `Model.Load( "models/dev/box.vmdl" )`.
- There is no `Assets/` folder. Paths are flat under the project root.
- `.cs` files hot-reload in the editor, but **do not rely on that when editing from outside the editor**. See `references/14_VERIFICATION.md`.
- `ProjectSettings/*.config` and the `.sbproj` are read at boot and **not watched**. An external edit needs an explicit reload or an editor restart.

---

## The shape of a component

This is the shape, not the content. What goes inside depends on what you are building, so read the reference file first.

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
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;
        if ( !Target.IsValid() ) return;
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;
    }

    [Rpc.Broadcast]
    public void PlayEffect( Vector3 position )
    {
    }
}
```

Complete runnable examples, including an FPS controller, a networked player, a Razor HUD, a hitscan weapon, a NavMeshAgent AI, a physics grenade, a prefab spawner, a trigger pickup and a press-E vendor, are in `references/13_EXAMPLES.md`.

---

## Traps worth knowing up front

Each of these is documented properly in a reference file. They are here because they cost real time and a model will not suspect any of them.

- `ICollisionListener` names its parameter `collision`, not `other`. The published docs are wrong about this.
- `Color`, `Capsule`, `Vector3`, `Rotation`, `Angles`, `Transform`, `BBox` and `Ray` are global types, not `Sandbox.*`.
- `LobbyConfig` and `LobbyPrivacy` need `using Sandbox.Network;`.
- `Scene` is-a `GameObject`. It is the root. `Scene.GetAllObjects(true)` walks the tree.
- Most `Component.ISomething` interfaces are nested on `Component`, including `IDamageable`, `ICollisionListener`, `ITriggerListener`, `INetworkListener` and `INetworkSpawn`. But `IGameObjectNetworkEvents` is top-level `Sandbox.IGameObjectNetworkEvents`.
- `SceneTrace.WithoutTags` / `WithAnyTags` / `WithAllTags` are `params string[]` in 26.08.05, so `WithoutTags( "player", "trigger" )` is correct. They also take an `ITagSet`. Older material claims they are not params; that was true of an earlier engine and is now wrong.
- `Game.Random` is a `System.Random`. `list[Game.Random.Next(list.Count)]` is simpler than the `FromList` extension, which needs a default value.
- `FileSystem` is a static facade. The methods live on `BaseFileSystem`, reached through `FileSystem.Data` or `FileSystem.Mounted`.
- `PlayerController.TraceBody` takes four parameters. The fourth is `heightScale`.
- Operators such as `Rotation * Rotation` are missing from the schema because the extraction excludes them systematically. They exist, use them.
- **`[GameResource(...)]` is obsolete engine-wide.** Custom data assets use `[AssetType( Name = ..., Extension = ..., Category = ... )]` with property initializers, not constructor arguments. Under `TreatWarningsAsErrors` the old attribute is a build failure.
- **`Model.Load( "typo/path.vmdl" )` returns `null`**, not `Model.Error`. Only a null or empty path gives the error placeholder. Null-check every load.
- **`GameObject.NetworkSpawn()` with no arguments assigns ownership to `Connection.Local`**, whoever called it. For a host-authoritative world object that is a quiet wrong-owner bug. Always pass an explicit owner.
- **`NetworkMode.Snapshot` is the default for scene objects and does not live-replicate `[Sync]`**, while RPCs keep working perfectly. Stale-state bugs here look like nothing is wrong.
- **`NetList<T>.OnChanged` is a public `Action<NetListChangeEvent<T>>` field, not `[Change]`.** `[Change]` wraps the property setter, so on a collection it only fires when you reassign the whole thing.
- **`IPressable.Press` runs on the pressing client.** The USE pipeline sits inside `if ( !IsProxy )` on the player, so authoritative effects need an `[Rpc.Host]`.
- **`Prop` is breakable, flammable and gibbable.** For a non-destructible object, compose `ModelRenderer` + `ModelCollider` + `Rigidbody` yourself. Its `Break()` method is an editor button that decomposes the component; it does not break the prop.
- **`Sandbox.Platform.Chat` is a global pipe that exists whether your gamemode uses it or not.** `ChatShowUI: false` only hides the overlay and `Say()` still broadcasts. Disable it with `"ChatEnabled": false` in `ProjectSettings/Platform.config`.
- **`Mixer.Spacializing` is an obsolete alias for `Mixer.Spatializing`.** Both spellings exist, the misspelled one forwards to the correct one, and deserialization accepts either key.
- `PlayerController.IEvents.PostCameraSetup` is obsolete. Implement `ICameraModifier` instead.

---

## When you are not sure an API exists

1. **Check the topical file** for that area first. Topical files carry inline signatures for what they cover.
2. **Then `15_API_CORE.md`**, which has full signatures for the most-used types.
3. **Then `16_API_INDEX.md`**, a namespace-organised index of the wider surface. Find the type, then get its full signature elsewhere.
4. **If it is in none of them, it does not exist.** Do not write it. There is almost always an idiomatic way to do what you wanted.

A schema entry proves an API exists. It does not prove it behaves the way you assume, and nearly every trap above is a case where a correct-looking call silently does nothing. `references/14_VERIFICATION.md` records which behaviours were observed in a live editor session, with dates, as opposed to merely read out of source. Check it before concluding that the API says something should work.

---

*This file routes. The reference files teach. Do not answer an s&box question from this file alone.*

*s&box skill by fobiat (Kyle Tarff), https://fobiat.dev/ and https://github.com/fobiat. MIT licensed. Written against engine 26.08.05 from Facepunch's MIT-licensed managed source.*
