<div align="center">

# s&box Skill

**Stop your coding agent writing Unity code into a Source 2 project.**

[![Engine](https://img.shields.io/badge/s%26box-26.08.05-f59c1a?style=flat-square)](https://sbox.game)
[![Licence](https://img.shields.io/badge/licence-MIT-3fb950?style=flat-square)](LICENSE)
[![Reference files](https://img.shields.io/badge/reference_files-16-4c8eda?style=flat-square)](skills/sbox/references)
[![Lines](https://img.shields.io/badge/lines-13k-8957e5?style=flat-square)](skills/sbox)
[![MCP tools](https://img.shields.io/badge/MCP_tools-11-e3b341?style=flat-square)](editor-mcp)

*Every API in here is traceable to engine source at a named version.*

</div>

---

## The problem

s&box is Facepunch's Source 2 engine with a C# scripting layer. It borrows `GameObject` and
`Component` from Unity, then diverges nearly everywhere else: different lifecycle, different
networking model, Z-up instead of Y-up, a restricted .NET surface, Razor for UI.

That surface resemblance is the whole problem. Ask any coding agent for an s&box component:

```csharp
public class Mover : MonoBehaviour        // does not exist
{
    void Update()                          // never runs
    {
        transform.position += Vector3.forward * Time.deltaTime;   // none of this is real
        Debug.Log("moving");               // not a thing
    }
}
```

Every line reads correctly. Not one of them exists. Nothing warns you, and you find out later
in the editor with no useful error pointing back at the cause.

Here is the same component, correct:

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

This project exists to make the second one the default.

---

## Contents

| | |
|---|---|
| [Quick start](#quick-start) | Install and confirm it loaded |
| [What is in it](#what-is-in-it) | The 16 reference files |
| [How to get the most from it](#how-to-get-the-most-from-it) | Prompts that change the output |
| [How it works](#how-it-works) | Why it is a router, not a manual |
| [The `sbox_dev` MCP toolset](#the-sbox_dev-mcp-toolset) | 11 editor tools |
| [Field notes](#field-notes) | The part you cannot get from docs |
| [Repository layout](#repository-layout) | Where everything lives |
| [Engine version and drift](#engine-version-and-drift) | What happens when the API moves |
| [Contributing](#contributing) · [Credits](#credits) · [Licence](#licence) | |

---

## Quick start

<details open>
<summary><b>Install for one project</b> (recommended)</summary>

```bash
git clone https://github.com/fobiat/sbox-skill.git /tmp/sbox-skill
mkdir -p your-game/.claude/skills
cp -r /tmp/sbox-skill/skills/sbox your-game/.claude/skills/
```

</details>

<details>
<summary><b>Install globally</b></summary>

```bash
git clone https://github.com/fobiat/sbox-skill.git /tmp/sbox-skill
mkdir -p ~/.claude/skills
cp -r /tmp/sbox-skill/skills/sbox ~/.claude/skills/
```

</details>

<details>
<summary><b>Install as a submodule</b>, so <code>git pull</code> brings updates</summary>

```bash
git submodule add https://github.com/fobiat/sbox-skill.git .claude/skills/sbox-skill
ln -s sbox-skill/skills/sbox .claude/skills/sbox
```

</details>

You end up with:

```
your-game/
├── Code/
├── your-game.sbproj
└── .claude/skills/sbox/
    ├── SKILL.md
    └── references/
        ├── 01_SCENE.md
        └── ...
```

### Confirm it loaded

Ask for something that should trip a Unity reflex:

> Write me an s&box component that moves a cube forward at 200 units per second.

| Result | Meaning |
|---|---|
| `: Component`, `protected override void OnUpdate()`, `Vector3.Forward` | Loaded |
| `MonoBehaviour`, `void Update()`, `transform.Translate`, `Vector3.forward` | Not loaded |

If it did not load, check the directory is named exactly `sbox` and that `SKILL.md` sits at
its root with its frontmatter intact.

---

## What is in it

A router plus sixteen reference files, just over 13,000 lines.

| # | File | Covers |
|---|---|---|
| 01 | [`01_SCENE.md`](skills/sbox/references/01_SCENE.md) | Scene, GameObject, Component, lifecycle, prefabs, scene events, `IPressable` |
| 02 | [`02_COMPONENTS.md`](skills/sbox/references/02_COMPONENTS.md) | Rendering, physics, `CharacterController`, `PlayerController`, `Prop`, inventory, camera, lighting, navigation, effects |
| 03 | [`03_UI.md`](skills/sbox/references/03_UI.md) | Razor panels, `PanelComponent`, `BuildHash`, SCSS, flexbox, controls, world panels |
| 04 | [`04_NETWORKING.md`](skills/sbox/references/04_NETWORKING.md) | Lobbies, ownership, `[Sync]`, `NetList`, RPCs, network events, dedicated servers |
| 05 | [`05_INPUT_PHYSICS.md`](skills/sbox/references/05_INPUT_PHYSICS.md) | Input actions, `Scene.Trace`, physics world, collision listeners, math types, time, gizmos |
| 06 | [`06_EDITOR.md`](skills/sbox/references/06_EDITOR.md) | `EditorTool`, `[CustomEditor]`, docks, the Qt-backed Widget system |
| 07 | [`07_SERVICES.md`](skills/sbox/references/07_SERVICES.md) | Stats, leaderboards, achievements, `FileSystem.Data`, cookies, `Package`, mounting |
| 08 | [`08_AVATARS.md`](skills/sbox/references/08_AVATARS.md) | Citizen model, `Clothing`, `ClothingContainer`, `Dresser`, body and material groups |
| 09 | [`09_RENDERING.md`](skills/sbox/references/09_RENDERING.md) | `.shader` anatomy, HLSL entry points, `Material`, `RenderAttributes`, `CommandList`, layers |
| 10 | [`10_AUDIO.md`](skills/sbox/references/10_AUDIO.md) | Mixer graph, `SoundHandle`, audio processors, `Phrase`, language files |
| 11 | [`11_ACTIONGRAPH.md`](skills/sbox/references/11_ACTIONGRAPH.md) | Exposing C# as graph nodes, graph-backed callbacks |
| 12 | [`12_VR_VOICE.md`](skills/sbox/references/12_VR_VOICE.md) | VR rig, controllers, haptics, voice capture, transmission, playback |
| 13 | [`13_EXAMPLES.md`](skills/sbox/references/13_EXAMPLES.md) | Eleven complete components, FPS controller to press-E vendor |
| 14 | [`14_VERIFICATION.md`](skills/sbox/references/14_VERIFICATION.md) | Editor MCP server, and the ledger of behaviour confirmed live |
| 15 | [`15_API_CORE.md`](skills/sbox/references/15_API_CORE.md) | Full signatures for the types a game touches most |
| 16 | [`16_API_INDEX.md`](skills/sbox/references/16_API_INDEX.md) | Namespace-organised index of the wider API surface |

---

## How to get the most from it

It triggers on its own. The description matches s&box, `.sbproj`, `using Sandbox;`,
`PanelComponent`, `[Sync]`, `Scene.Trace` and the rest, so you never invoke it by name.

What changes the output is how you ask.

<details open>
<summary><b>Name the subsystem</b></summary>

The skill routes by task, so naming the area opens the right reference first.

> Add a **networked** health component. Only the host may change health, and clients get a
> change callback for the HUD.

> Build a **Razor** inventory panel with a scrolling grid, rebuilding only when the item list
> changes.

> Write an **editor tool** that snaps the selected GameObject to the surface under the cursor.

</details>

<details>
<summary><b>Ask for the trap, not just the code</b></summary>

The most valuable content here is the set of calls that look correct and silently do nothing.

> I am about to spawn this prefab from the host and expect clients to see its `[Sync]` values.
> What will bite me?

You should hear that `NetworkSpawn()` with no arguments assigns ownership to whoever called
it, and that `NetworkMode.Snapshot` is the default for scene objects and does not
live-replicate `[Sync]` even though RPCs keep working perfectly.

</details>

<details>
<summary><b>Make it verify before it writes</b></summary>

> Before you write this, confirm every API you plan to use exists in the reference files, and
> list anything you could not find.

The skill's own rule is that a member missing from all three lookup files does not exist. This
turns that rule into a step you can read.

</details>

<details>
<summary><b>Porting from Unity</b></summary>

> Port this Unity script to s&box. Flag every construct with no equivalent instead of
> inventing one.

`SKILL.md` carries a translation table covering roughly fifty constructs, including the ones
with no equivalent at all, such as coroutines.

</details>

---

## How it works

`SKILL.md` is a **router**, not a manual. It answers almost nothing itself. It works out which
reference file holds the answer and sends the reader there.

```
                       ┌──────────────┐
      "write a HUD" ──▶│   SKILL.md   │  routes by task
                       └──────┬───────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
    03_UI.md          04_NETWORKING.md        15_API_CORE.md
  panels, SCSS        ownership, [Sync]       full signatures
   BuildHash            RPCs, lobbies        for common types
```

A single flat document gets skimmed, and a skimmed API reference is exactly how an invented
API gets through. Forcing a second read of a topical file puts real signatures in front of the
reader before anything is written.

Three rules hold the design together:

1. **The router never answers.** Let it start holding answers and it stops being read as an
   index, and the reference files go unopened.
2. **A missing API is not an API.** If a member appears in none of the lookup files, the
   correct move is to stop, not to guess.
3. **Existence is not behaviour.** A signature proves a call compiles. It proves nothing about
   whether the call does anything.

That third rule is what the field notes exist for.

---

## The `sbox_dev` MCP toolset

[`editor-mcp/SboxDevTools.cs`](editor-mcp/SboxDevTools.cs) drops into your project's `Editor/`
folder and adds eleven tools to the editor's MCP server.

Three engine behaviours stop the editor noticing that you changed something on disk, and none
of them raises an error. The `.sbproj` is read once at boot and never watched.
`ProjectSettings/*.config` is cached on first read and never invalidated. Compiler file
watchers go stale once the compilers are recreated in-process.

| Tool | | Does |
|---|---|---|
| `project_find_type` | 🔍 | Search the **running engine** for a type. No match is proof it does not exist |
| `project_type_members` | 🔍 | Real signatures for a type, read live rather than from a document |
| `project_input_actions` | 🔍 | Every input action and its bindings |
| `project_info` | 📖 | What is open, and the compiler settings **live in memory** |
| `project_compilers` | 📖 | Per compiler `IsBuilding`, `NeedsBuild`, `BuildSuccess` |
| `project_source_changes` | 📖 | What each compiler has actually noticed since its last build |
| `project_compile_errors` | 📖 | Diagnostics as rows with file and line |
| `project_reload_config` | ⚙️ | Re-read an externally edited `.sbproj` |
| `project_reload_settings` | ⚙️ | Drop the cached `ProjectSettings` |
| `project_rebuild` | ⚙️ | Recreate compilers and start a build |
| `project_build` | ⚙️ | Rebuild and wait, then report success plus errors |

🔍 queries the live engine · 📖 reads editor state · ⚙️ changes something

The query tools invert the skill's core rule. Instead of "if you cannot find it in three
markdown files, it does not exist", you ask the engine itself, and the answer cannot be out of
date. Full detail in [`editor-mcp/README.md`](editor-mcp/README.md).

---

## Field notes

Most of this project records what the API **is**, read out of engine source. One file records
what it was observed to **do**, in a live editor session, with dates.

Those are different claims, and the gap between them is where the expensive bugs live.

> Take `[Sync]`. A write you are not permitted to make gets discarded before it reaches the
> backing field. No exception, no warning, and the read-back on the very next line already
> shows the authoritative value, so the code looks like it worked. The schema says the property
> exists and is settable. The schema is not wrong. It is just not the whole story.

Where a source-read fact and a live-verified one disagree, **the field note wins**, and the
disagreement is itself worth writing down.

---

## Repository layout

Two independent deliverables, plus the tooling that keeps them honest.

```
sbox-skill/
├── skills/sbox/            the skill, this is what you install
│   ├── SKILL.md            the router
│   └── references/         01_SCENE.md through 16_API_INDEX.md
├── editor-mcp/             the sbox_dev toolset, a drop-in Editor/ file
├── scripts/                repo tooling, not shipped to your game
└── .github/workflows/      CI, mirrors the local gate
```

Take either half on its own. The skill works without the toolset, and the toolset is useful
without the skill.

Everything under `skills/sbox/` is required at runtime. The router resolves to a reference file
for every task, so a missing reference is a dead end rather than a smaller download, and the
gate fails the build if one goes missing.

---

## Engine version and drift

Written against engine **26.08.05**. Every file names the version and the upstream paths it was
read from, so the next pass can diff against a newer engine instead of starting from nothing.

Stale guidance is worse than none, and two cases are already recorded:

| Claim | Reality in 26.08.05 |
|---|---|
| `SceneTrace.WithoutTags` takes `string[]`, not params | It takes `params string[]` (`Scene.Trace.cs:637`) |
| `CameraComponent.AddHookAfterOpaque` is live API | `[Obsolete]` with a `=> null` body. Compiles, returns null, renders nothing. Use `AddCommandList` |

Found something that drifted? The [wrong-API template](.github/ISSUE_TEMPLATE/wrong-api.yml)
asks for the one thing that matters: where you confirmed the truth.

---

## Contributing

One rule outranks the rest: **never write an API you have not verified.**

An omission makes the reader go and look the API up. A wrong signature makes them write code
that fails later for no visible reason. The first is a gap, the second is a trap.

```bash
python3 scripts/check_skill.py
```

The gate checks every routing pointer resolves, no reference file is orphaned, frontmatter is
intact, and no em dash crept in. Green means the structure holds. It does not mean an API is
correct, and only reading the source can tell you that.

Full guidance in [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Credits

Built and maintained by **Kyle (fobiat)**.

[![Website](https://img.shields.io/badge/fobiat.dev-1f6feb?style=flat-square)](https://fobiat.dev/)
[![GitHub](https://img.shields.io/badge/github.com%2Ffobiat-24292f?style=flat-square)](https://github.com/fobiat)
[![Email](https://img.shields.io/badge/kyle%40fobiat.dev-6e7681?style=flat-square)](mailto:kyle@fobiat.dev)

Both halves are his work: the skill in `skills/sbox/`, and the `sbox_dev` toolset in
`editor-mcp/`.

The field notes deserve their own credit. They are not compiled from documentation. Every row
came out of a real editor session where something did not behave the way the API said it
would, and somebody sat there and worked out why. That is the part you cannot generate.

Thanks to Facepunch Studios for publishing the engine's managed layer under a licence that
makes a reference like this possible to write and to share.

---

## Licence

MIT, see [LICENSE](LICENSE).

The API surface described here derives from the s&box engine's managed C# layer, published by
Facepunch Studios at [Facepunch/sbox-public](https://github.com/Facepunch/sbox-public) under
the MIT Licence. That copyright notice travels in `LICENSE`, as MIT requires.

s&box is a trademark of Facepunch Studios Ltd. This project is not affiliated with, endorsed
by, or sponsored by Facepunch Studios.
