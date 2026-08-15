<div align="center">

# s&box Skill

### Nothing in s&box tells you when you are wrong.

[![Engine](https://img.shields.io/badge/s%26box-26.08.05-f59c1a?style=for-the-badge)](https://sbox.game)
[![Licence](https://img.shields.io/badge/MIT-3fb950?style=for-the-badge)](LICENSE)
[![Reference files](https://img.shields.io/badge/16_references-4c8eda?style=for-the-badge)](skills/sbox/references)
[![MCP tools](https://img.shields.io/badge/11_MCP_tools-e3b341?style=for-the-badge)](editor-mcp)
[![Devlog](https://img.shields.io/badge/devlog-1F9E7A?style=for-the-badge)](https://fobiat.dev/blog/p/sbox-skill)

</div>

<br>

> **In short.** Two things for building [s&box](https://sbox.game) games, either usable on its own.
>
> **The skill** is 16 reference files that teach a coding agent the real s&box API, so it stops
> writing Unity code into your Source 2 project. Plain markdown, no runtime, no dependencies, so
> it works with any agent that can read a file. Every API in it is traceable to engine source at
> a named version.
>
> **The toolset** is one C# file for your `Editor/` folder that adds 11 tools to the editor's
> MCP server. It answers the questions the editor otherwise leaves silent: does this type
> actually exist, has the compiler noticed my edit, why did nothing happen when I changed that
> config.

<br>

You write a component. It compiles. You press play. Nothing moves.

There is no error. There is no warning. The console is clean. Somewhere between the code you
wrote and the thing you expected, a call that looked completely correct did nothing at all, and
the engine did not think that was worth mentioning.

This project is thirteen thousand lines about that gap.

<br>

## The five that will cost you an afternoon

Every one of these compiles. Every one of these looks right in review.

| You wrote | What actually happens |
|---|---|
| `void Update()` | Never called. The lifecycle method is `protected override void OnUpdate()`, and yours is just a method nobody invokes |
| `NetworkSpawn()` | Ownership goes to whoever called it, not the host. A world object now belongs to one random client |
| `[Sync]` on a scene object | `NetworkMode.Snapshot` is the default and does not live-replicate. RPCs keep working perfectly, which is what makes it so hard to spot |
| A client writing `[Sync(SyncFlags.FromHost)]` | Discarded before it reaches the backing field. No exception, and the read-back on the next line already shows the authoritative value |
| `Model.Load( "typo/path.vmdl" )` | Returns `null`, not the error placeholder. Only an empty path gives you the checkerboard you were expecting |

None of these are exotic. They are all first-week code.

<br>

## Why an agent gets this wrong

s&box borrows `GameObject` and `Component` from Unity, then diverges nearly everywhere else.
Different lifecycle, different networking model, Z-up instead of Y-up, restricted .NET, Razor
for UI.

The resemblance is close enough to be dangerous. Ask any coding agent for a mover:

```csharp
public class Mover : MonoBehaviour        // does not exist
{
    void Update()                          // never runs
    {
        transform.position += Vector3.forward * Time.deltaTime;   // none of this is real
        Debug.Log( "moving" );             // not a thing
    }
}
```

Four lines, four inventions, zero warnings until much later. Here it is correct:

```csharp
public sealed class Mover : Component
{
    [Property] public float Speed { get; set; } = 200f;

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;
        WorldPosition += Vector3.Forward * Speed * Time.Delta;   // Forward is (1,0,0), Z-up
    }
}
```

Install this and the second one is what you get.

<br>

## Get it running

> Setting up **both halves together**? Follow the
> **[Quickstart](QUICKSTART.md)** instead, which covers the loop they form and the problems each
> one fixes for the other.

Clone it somewhere your agent can read:

```bash
git clone https://github.com/fobiat/sbox-skill.git
```

There is no runtime and nothing to build. `skills/sbox/SKILL.md` is the entry point, and it
routes to `references/`. Any agent that can open a file can use it. Pick whichever of these
matches your setup.

<details open>
<summary><b>Any agent, via its instructions file</b></summary>

<br>

Most agents read a project instructions file, whatever it is called in your tool. Point it at
the router and let it do the rest:

```markdown
## s&box

Before writing or changing any s&box C#, read `docs/sbox-skill/SKILL.md` and open the
reference file it routes you to. Do not write an API that is not in those files.
```

Copy `skills/sbox/` to wherever that path points, and you are done.

</details>

<details>
<summary><b>Agents with a skills directory</b></summary>

<br>

If your tool auto-loads skills from a directory, `SKILL.md` already carries the frontmatter for
it, so it triggers on its own with no instructions file needed:

```bash
mkdir -p your-game/.claude/skills
cp -r sbox-skill/skills/sbox your-game/.claude/skills/
```

Swap the path for your tool's own skills directory. Use the home directory equivalent instead
to make it available in every project.

</details>

<details>
<summary><b>As a submodule</b>, so <code>git pull</code> brings updates with it</summary>

<br>

```bash
git submodule add https://github.com/fobiat/sbox-skill.git vendor/sbox-skill
ln -s ../vendor/sbox-skill/skills/sbox docs/sbox-skill
```

</details>

<details>
<summary><b>No agent at all</b></summary>

<br>

It reads fine as documentation. Start at
[`SKILL.md`](skills/sbox/SKILL.md) for the Unity translation table and the list of silent
failures, or go straight to whichever
[reference file](skills/sbox/references) matches what you are building.

</details>

<br>

**Check it took.** Ask for something that should trip a Unity reflex:

> Write me an s&box component that moves a cube forward at 200 units per second.

Working, if you see `: Component`, `protected override void OnUpdate()` and `Vector3.Forward`.
Not working, if you see `MonoBehaviour`, `void Update()` or `Vector3.forward`. In that case
check your agent can actually reach the files, and that `SKILL.md` still has its frontmatter.

<br>

## What is actually in it

`SKILL.md` is a router. It answers almost nothing itself, it works out which file holds the
answer and sends the reader there.

```
                       ┌──────────────┐
      "write a HUD" ──▶│   SKILL.md   │
                       └──────┬───────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
    03_UI.md          04_NETWORKING.md        15_API_CORE.md
```

That shape is the point. A single flat document gets skimmed, and a skimmed API reference is
exactly how an invented API gets through. Making the reader open a second file puts real
signatures in front of them before they write any.

| | | |
|---|---|---|
| [`01_SCENE`](skills/sbox/references/01_SCENE.md) | Object model | Scene, GameObject, Component, lifecycle, prefabs, scene events, `IPressable` |
| [`02_COMPONENTS`](skills/sbox/references/02_COMPONENTS.md) | The library | Rendering, physics, controllers, `Prop`, inventory, camera, lighting, navigation, effects |
| [`03_UI`](skills/sbox/references/03_UI.md) | Interface | Razor panels, `BuildHash`, SCSS, flexbox, controls, world panels |
| [`04_NETWORKING`](skills/sbox/references/04_NETWORKING.md) | Multiplayer | Lobbies, ownership, `[Sync]`, `NetList`, RPCs, dedicated servers |
| [`05_INPUT_PHYSICS`](skills/sbox/references/05_INPUT_PHYSICS.md) | Simulation | Input actions, `Scene.Trace`, physics world, math types, time, gizmos |
| [`06_EDITOR`](skills/sbox/references/06_EDITOR.md) | Tooling | `EditorTool`, `[CustomEditor]`, docks, the Widget system, which is not Razor |
| [`07_SERVICES`](skills/sbox/references/07_SERVICES.md) | Backend | Stats, leaderboards, achievements, save data, packages, mounting |
| [`08_AVATARS`](skills/sbox/references/08_AVATARS.md) | Players | Citizen model, `Clothing`, `ClothingContainer`, body and material groups |
| [`09_RENDERING`](skills/sbox/references/09_RENDERING.md) | Pixels | `.shader` anatomy, HLSL entry points, materials, render attributes, layers |
| [`10_AUDIO`](skills/sbox/references/10_AUDIO.md) | Sound | Mixer graph, `SoundHandle`, processors, phrases, language files |
| [`11_ACTIONGRAPH`](skills/sbox/references/11_ACTIONGRAPH.md) | Visual scripting | Exposing C# as nodes, graph-backed callbacks |
| [`12_VR_VOICE`](skills/sbox/references/12_VR_VOICE.md) | Extras | VR rig, controllers, haptics, voice capture and playback |
| [`13_EXAMPLES`](skills/sbox/references/13_EXAMPLES.md) | Worked code | Eleven complete components, FPS controller to press-E vendor |
| [`14_VERIFICATION`](skills/sbox/references/14_VERIFICATION.md) | Field notes | Editor MCP server, and behaviour confirmed live with dates |
| [`15_API_CORE`](skills/sbox/references/15_API_CORE.md) | Lookup | Full signatures for the types a game touches most |
| [`16_API_INDEX`](skills/sbox/references/16_API_INDEX.md) | Lookup | Namespace index of the wider API surface |

<br>

## The file that matters most

Fifteen of those record what the API **is**, read out of engine source at a named version.

`14_VERIFICATION` records what it was observed to **do**, in a live editor session, with dates.

They are not the same claim, and the distance between them is where every entry in that table
at the top comes from. A schema entry proves a call compiles. It says nothing about whether the
call does anything.

Where a source-read fact and a live-verified one disagree, the field note wins. Those rows are
the part of this project that cannot be generated. Somebody had to sit there while something
misbehaved and work out why.

<br>

## Talking to it

On a tool that auto-loads skills it triggers by itself, matching `.sbproj`, `using Sandbox;`,
`PanelComponent`, `[Sync]`, `Scene.Trace` and the rest. Everywhere else your instructions file
does the same job. Either way you should not have to name it. What changes the output is how
you ask.

**Name the subsystem** and the right reference opens first.

> Add a **networked** health component. Only the host may change health, and clients get a
> change callback for the HUD.

**Ask for the trap**, not just the code. This is where the value is.

> I am about to spawn this prefab from the host and expect clients to see its `[Sync]` values.
> What will bite me?

**Make it verify before it writes.**

> Before you write this, confirm every API you plan to use exists in the reference files, and
> list anything you could not find.

**Porting from Unity?** There is a translation table covering about fifty constructs, including
the ones with no equivalent at all, like coroutines.

> Port this Unity script to s&box. Flag every construct with no equivalent instead of inventing
> one.

<br>

## The other half: `sbox_dev`

[One file](editor-mcp/SboxDevTools.cs) into your `Editor/` folder, eleven tools onto the
editor's MCP server. It solves a different silent failure: the editor not noticing you changed
anything.

Three reasons it might not, none of which raise an error. The `.sbproj` is read once at boot
and never watched. `ProjectSettings/*.config` is cached forever on first read. Compiler file
watchers go stale once the compilers are recreated in-process.

| | Tools |
|---|---|
| **Ask the live engine** | `project_find_type`, `project_type_members`, `project_input_actions` |
| **Ask the editor** | `project_info`, `project_compilers`, `project_source_changes`, `project_compile_errors` |
| **Change something** | `project_reload_config`, `project_reload_settings`, `project_rebuild`, `project_build` |

The first group inverts this project's own rule. Instead of "if it is in none of the reference
files it does not exist", you ask the running engine, and that answer cannot go out of date.

Details in [`editor-mcp/README.md`](editor-mcp/README.md).

> **Status:** every engine member the toolset reaches is verified present in 26.08.05 by file
> and line, but it has not yet been compiled against a live editor. Report anything that does
> not load and it gets fixed quickly.

<br>

## Layout

```
sbox-skill/
├── skills/sbox/        the skill, this is what you install
├── editor-mcp/         the sbox_dev toolset, a drop-in Editor/ file
├── scripts/            repo tooling, never shipped to your game
└── .github/workflows/  CI, mirrors the local gate
```

Either half stands alone. Everything under `skills/sbox/` is required at runtime, because the
router resolves to a reference for every task, and the gate fails the build if one goes missing.

<br>

## When the engine moves

Written against **26.08.05**. Every file names its version and the upstream paths it was read
from, so the next pass diffs against a newer engine instead of starting over.

Stale guidance is worse than none, and two cases are already on record:

| Older material says | 26.08.05 says |
|---|---|
| `SceneTrace.WithoutTags` takes `string[]`, not params | `params string[]`, at `Scene.Trace.cs:637` |
| `CameraComponent.AddHookAfterOpaque` is live | `[Obsolete]` with a `=> null` body. Compiles, returns null, renders nothing. Use `AddCommandList` |

Found one that drifted? The [wrong-API template](.github/ISSUE_TEMPLATE/wrong-api.yml) asks for
the only thing that matters: where you confirmed the truth.

<br>

## Contributing

One rule outranks everything else: **never write an API you have not verified.**

An omission makes the reader go and look it up. A wrong signature makes them write code that
fails later for no visible reason. The first is a gap. The second is a trap, and this whole
project exists because traps are expensive.

```bash
python3 scripts/check_skill.py
```

Green means the structure holds: routing pointers resolve, no reference is orphaned,
frontmatter intact, no em dashes. It does not mean an API is correct. Only reading the source
tells you that. More in [CONTRIBUTING.md](CONTRIBUTING.md).

<br>

---

<div align="center">

Built and maintained by **Kyle (fobiat)**

[![Website](https://img.shields.io/badge/fobiat.dev-1f6feb?style=for-the-badge)](https://fobiat.dev/)
[![GitHub](https://img.shields.io/badge/fobiat-24292f?style=for-the-badge&logo=github)](https://github.com/fobiat)
[![Email](https://img.shields.io/badge/kyle@fobiat.dev-6e7681?style=for-the-badge)](mailto:kyle@fobiat.dev)

<sub>MIT licensed. The API surface described here derives from the s&box engine's managed C#
layer, published by Facepunch Studios at
<a href="https://github.com/Facepunch/sbox-public">Facepunch/sbox-public</a> under the MIT
Licence, whose notice travels in <a href="LICENSE">LICENSE</a> as required.<br>
s&box is a trademark of Facepunch Studios Ltd. This project is not affiliated with, endorsed
by, or sponsored by Facepunch Studios.</sub>

</div>
