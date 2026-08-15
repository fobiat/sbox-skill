# s&box Skill

**An agent skill that teaches Claude to write s&box C# that actually compiles.**

[![Engine](https://img.shields.io/badge/s%26box-26.08.05-f59c1a)](https://sbox.game)
[![Licence](https://img.shields.io/badge/licence-MIT-3fb950)](LICENSE)
[![Reference files](https://img.shields.io/badge/reference%20files-16-4c8eda)](skills/sbox/references)

s&box is Facepunch's Source 2 engine with a C# scripting layer. It borrows `GameObject`
and `Component` from Unity and then diverges almost everywhere else: a different lifecycle,
a different networking model, Z-up instead of Y-up, a restricted .NET surface, and Razor
for UI.

That surface similarity is the whole problem. Ask any model for an s&box component and it
hands you `MonoBehaviour`, `void Update()`, `transform.position`, `Physics.Raycast` and
`Debug.Log`. Every one of those reads correctly. Not one of them exists. You find out in
the editor, later, with no useful error.

This skill exists to stop that.

Built and maintained by Kyle (fobiat).
[fobiat.dev](https://fobiat.dev/) &middot; [github.com/fobiat](https://github.com/fobiat) &middot; kyle@fobiat.dev

---

## Contents

- [What you get](#what-you-get)
- [Install](#install)
- [Check it loaded](#check-it-loaded)
- [How to use it](#how-to-use-it)
- [How it works](#how-it-works)
- [The reference library](#the-reference-library)
- [Bonus: the sbox_dev editor MCP toolset](#bonus-the-sbox_dev-editor-mcp-toolset)
- [Field notes: the part you cannot get from docs](#field-notes-the-part-you-cannot-get-from-docs)
- [Engine version and drift](#engine-version-and-drift)
- [Contributing](#contributing)
- [Credits](#credits)
- [Licence](#licence)

---

## What you get

Sixteen reference files and a router, just over 13,000 lines, every factual claim traceable
to engine source at a named version.

It covers the whole surface a game touches, not only the gameplay loop:

|  | Area |
|---|---|
| **Core** | Scene and component model, lifecycle, prefabs, scene events |
| **Content** | The built-in component library: rendering, physics, movement, camera, lighting, navigation |
| **Interface** | Razor panels, SCSS, flexbox, built-in controls, world-space panels |
| **Multiplayer** | Lobbies, ownership, `[Sync]`, RPCs, network events, dedicated servers |
| **Simulation** | Input actions, traces, physics world, math types, time, gizmos |
| **Editor** | `EditorTool`, custom inspectors, docks, and the Qt-backed Widget system |
| **Backend** | Stats, leaderboards, achievements, save data, packages, mounting |
| **Avatars** | Citizen model, `Clothing`, `ClothingContainer`, dressing a renderer |
| **Rendering** | `.shader` authoring, materials, render attributes, command lists, layers |
| **Audio** | The mixer graph, sound handles, processors, and localization |
| **Extras** | Node graphs, VR, voice chat |
| **Lookup** | Full signatures for common types, plus an index of the wider API |
| **Practice** | Eleven complete worked examples, and a ledger of live-verified behaviour |

---

## Install

The skill is the `skills/sbox/` directory. Put it wherever your agent reads skills from.

### Per project

Scopes the skill to one game, which is usually what you want.

```bash
git clone https://github.com/fobiat/sbox-skill.git /tmp/sbox-skill
mkdir -p your-game/.claude/skills
cp -r /tmp/sbox-skill/skills/sbox your-game/.claude/skills/
```

### Everywhere

Available in every project you open.

```bash
git clone https://github.com/fobiat/sbox-skill.git /tmp/sbox-skill
mkdir -p ~/.claude/skills
cp -r /tmp/sbox-skill/skills/sbox ~/.claude/skills/
```

### As a submodule

Best if you want `git pull` to bring engine updates with it.

```bash
git submodule add https://github.com/fobiat/sbox-skill.git .claude/skills/sbox-skill
ln -s sbox-skill/skills/sbox .claude/skills/sbox
```

Any of these ends up looking like this:

```
your-game/
├── Code/
├── your-game.sbproj
└── .claude/
    └── skills/
        └── sbox/
            ├── SKILL.md
            └── references/
                ├── scene-and-components.md
                └── ...
```

---

## Check it loaded

Ask for something that should trip a Unity reflex:

> Write me an s&box component that moves a cube forward at 200 units per second.

**Loaded.** You get `public sealed class ... : Component`, a
`protected override void OnUpdate()`, and movement along `Vector3.Forward`, which is
`(1,0,0)` because s&box is Z-up.

**Not loaded.** You get `MonoBehaviour`, `void Update()`, `transform.Translate`, or
`Vector3.forward`. Check the directory is named exactly `sbox` and that `SKILL.md` sits at
its root with its frontmatter intact.

---

## How to use it

Once installed it triggers on its own. The description matches mentions of s&box, `.sbproj`
files, `using Sandbox;`, `PanelComponent`, `[Sync]`, `Scene.Trace` and the rest, so you do
not need to invoke it by name.

What changes the result is how you ask.

### Name the subsystem

The skill routes by task, so saying which part of the engine you are in opens the right
reference first.

> Add a **networked** health component. Only the host may change health, and clients get a
> change callback for the HUD.

> Build a **Razor** inventory panel with a scrolling grid, and make it rebuild only when
> the item list changes.

> Write an **editor tool** that snaps the selected GameObject to the surface under the cursor.

### Ask for the trap, not just the code

The most valuable content here is the set of calls that look correct and silently do
nothing. Asking directly surfaces them.

> I am about to spawn this prefab from the host and expect clients to see its `[Sync]`
> values. What will bite me?

You should hear that `NetworkSpawn()` with no arguments assigns ownership to whoever called
it, and that `NetworkMode.Snapshot` is the default for scene objects and does not
live-replicate `[Sync]` even though RPCs keep working perfectly.

### Ask it to verify before it writes

> Before you write this, confirm every API you plan to use exists in the reference files,
> and list anything you could not find.

The skill's own rule is that a member missing from all three lookup files does not exist.
This turns that rule into an explicit step you can read.

### Porting from Unity

> Port this Unity script to s&box. Flag every construct with no equivalent instead of
> inventing one.

`SKILL.md` carries a Unity to s&box translation table covering roughly fifty constructs,
including the ones with no direct equivalent, such as coroutines.

---

## How it works

`SKILL.md` is a **router**, not a manual. It answers almost nothing itself. It works out
which reference file holds the answer and sends the model there.

```
                    ┌──────────────┐
   "write a HUD" ──▶│   SKILL.md   │  routes by task
                    └──────┬───────┘
                           │
     ┌─────────────────────┼──────────────────────┐
     ▼                     ▼                      ▼
razor-interfaces      multiplayer.md          api-core.md
   panels, SCSS       ownership, [Sync]     full signatures
   BuildHash          RPCs, lobbies         for common types
```

The shape is deliberate. A single flat document gets skimmed, and a skimmed API reference is
exactly how a hallucination gets through. Forcing a second read of a topical file puts real
signatures in front of the model before it writes any.

Three rules hold the design together:

1. **The router never answers.** Let it start holding answers and it stops being read as an
   index, and the reference files go unopened.
2. **A missing API is not an API.** If a member appears in none of the lookup files, the
   correct move is to stop, not to guess.
3. **Existence is not behaviour.** A signature proves a call compiles. It proves nothing
   about whether the call does anything.

That third rule is what the field notes exist for.

---

## The reference library

| File | What it covers |
|---|---|
| [`scene-and-components.md`](skills/sbox/references/scene-and-components.md) | Scene, GameObject, Component, lifecycle, prefabs, scene events, `IPressable` |
| [`component-library.md`](skills/sbox/references/component-library.md) | Rendering, physics, `CharacterController`, `PlayerController`, `Prop`, inventory, camera, lighting, audio, navigation, effects |
| [`razor-interfaces.md`](skills/sbox/references/razor-interfaces.md) | `.razor` panels, `PanelComponent`, `BuildHash`, SCSS, flexbox, transitions, built-in controls, world panels |
| [`multiplayer.md`](skills/sbox/references/multiplayer.md) | Lobbies, connections, ownership, `[Sync]`, `NetList`, RPCs, network events, dedicated servers |
| [`input-traces-and-physics.md`](skills/sbox/references/input-traces-and-physics.md) | Input actions, `Scene.Trace`, physics world, collision listeners, math types, time, gizmos |
| [`editor-extensions.md`](skills/sbox/references/editor-extensions.md) | `EditorTool`, `[CustomEditor]`, docks, the Widget and Layout system, editor asset access |
| [`backend-and-saved-data.md`](skills/sbox/references/backend-and-saved-data.md) | Stats, leaderboards, achievements, `FileSystem.Data`, cookies, `Package`, mounting |
| [`avatars-and-outfits.md`](skills/sbox/references/avatars-and-outfits.md) | Citizen model paths, `Clothing`, `ClothingContainer`, `Dresser`, body and material groups |
| [`shading-and-render-path.md`](skills/sbox/references/shading-and-render-path.md) | `.shader` anatomy, HLSL entry points, `Material`, `RenderAttributes`, `CommandList`, render layers |
| [`sound-and-language.md`](skills/sbox/references/sound-and-language.md) | Mixer graph, `SoundHandle`, audio processors, `Phrase`, language files, `#` tokens |
| [`node-graphs.md`](skills/sbox/references/node-graphs.md) | Exposing C# as graph nodes, graph-backed callbacks a designer can wire |
| [`vr-and-voice-chat.md`](skills/sbox/references/vr-and-voice-chat.md) | VR rig, controllers, haptics, voice capture, transmission, playback |
| [`worked-examples.md`](skills/sbox/references/worked-examples.md) | Eleven complete components, from an FPS controller to a press-E vendor |
| [`field-notes.md`](skills/sbox/references/field-notes.md) | The editor MCP server, and behaviour confirmed in live sessions |
| [`api-core.md`](skills/sbox/references/api-core.md) | Full signatures for the types a game touches most |
| [`api-index.md`](skills/sbox/references/api-index.md) | Namespace-organised index of the wider API surface |

---

## Bonus: the `sbox_dev` editor MCP toolset

[`editor-mcp/SboxDevTools.cs`](editor-mcp/SboxDevTools.cs) is a drop-in file for your
project's `Editor/` folder. It adds nine MCP tools to the editor's embedded server, covering
the one thing the stock toolsets do not: getting the editor to notice that you changed
something on disk.

Three separate engine behaviours stop it noticing, and none of them raises an error. The
`.sbproj` is read once at boot and never watched. `ProjectSettings/*.config` is cached on
first read and never invalidated. Compiler file watchers go stale after the compilers are
recreated in-process. Every one of them leaves you having edited a file, seen nothing happen,
and concluded your edit was wrong.

| Tool | Reads or writes | Does |
|---|---|---|
| `project_info` | read | What is open, and the compiler settings **live in memory** rather than on disk |
| `project_compilers` | read | Per compiler `IsBuilding`, `NeedsBuild`, `BuildSuccess` |
| `project_source_changes` | read | What each compiler has actually noticed since its last build |
| `project_compile_errors` | read | Diagnostics as rows with file and line, no console scraping |
| `project_input_actions` | read | Every input action the project defines, with its bindings |
| `project_reload_config` | write | Re-read an externally edited `.sbproj` into the live config |
| `project_reload_settings` | write | Drop the cached `ProjectSettings` so the configs come back off disk |
| `project_rebuild` | write | Recreate compilers and start a build, returns immediately |
| `project_build` | write | Rebuild and wait, then report success plus errors |

`project_source_changes` is the one that saves the most time, because it separates "the
compiler never saw my file" from "the compiler saw it and rejected it". Those are very
different problems that look identical from the console.

`project_input_actions` earns its place for a subtler reason. Input actions are strings
resolved at runtime, so `Input.Down( "jump" )` against an action that does not exist compiles
cleanly and silently never fires. An agent writing input code cannot know the real vocabulary
unless something hands it over.

Full details, including every reflected engine member and where it lives upstream, in
[`editor-mcp/README.md`](editor-mcp/README.md).

---

## Field notes: the part you cannot get from docs

Most of this skill records what the API **is**, read out of engine source. One file records
what it was observed to **do**, in a live editor session, with dates.

Those are different claims, and the gap between them is where the expensive bugs live.

Take `[Sync]`. A write you are not permitted to make gets discarded before it reaches the
backing field. No exception, no warning, and the read-back on the very next line already
shows the authoritative value, so the code looks like it worked. The schema says the
property exists and is settable. The schema is not wrong. It is just not the whole story.

Where a source-read fact and a live-verified one disagree, **the field note wins**, and the
disagreement is itself worth writing down.

---

## Engine version and drift

Written against engine **26.08.05**. Every reference file names the version and the upstream
paths it was read from, so the next regeneration can diff against a newer engine instead of
starting from nothing.

The API moves, and stale guidance is worse than none. One case already caught: the trace tag
filters were once documented as taking `string[]` rather than `params`. In 26.08.05 they are
`params string[]`, verified at `engine/Sandbox.Engine/Scene/Scene/Scene.Trace.cs:637`, so
the old advice now produces awkward code and a confused reader.

Found something that has drifted? The
[wrong-API issue template](.github/ISSUE_TEMPLATE/wrong-api.yml) asks for the one thing that
matters: where you confirmed the truth.

---

## Contributing

One rule outranks the rest: **never write an API you have not verified.**

A confident wrong signature is worse than no signature. An omission makes the model look the
API up. A wrong signature makes it write code that fails later for no visible reason.

```bash
python3 tools/check_skill.py
```

The gate checks that every routing pointer resolves, that no reference file is unrouted,
that frontmatter is intact, and that no em dash appears anywhere. Green does not mean an API
is correct. Only reading the source does that.

Full guidance in [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Credits

Written and maintained by **Kyle (fobiat)**.

- Website: [fobiat.dev](https://fobiat.dev/)
- GitHub: [github.com/fobiat](https://github.com/fobiat)
- Email: kyle@fobiat.dev

Both halves of this repository are his work: the skill in `skills/sbox/`, and the `sbox_dev`
editor MCP toolset in `editor-mcp/`.

The field notes deserve a specific credit of their own. They are not compiled from
documentation. Every row in them came out of a real editor session where something did not
behave the way the API said it would, and somebody sat there and worked out why. That is the
part of this repository you cannot generate.

Thanks to Facepunch Studios for publishing the engine's managed layer under a licence that
makes a reference like this possible to write and to share.

---

## Licence

MIT, see [LICENSE](LICENSE).

The API surface described here derives from the s&box engine's managed C# layer, published
by Facepunch Studios at [Facepunch/sbox-public](https://github.com/Facepunch/sbox-public)
under the MIT Licence. That copyright notice travels in `LICENSE`, as MIT requires.

s&box is a trademark of Facepunch Studios Ltd. This project is not affiliated with, endorsed
by, or sponsored by Facepunch Studios.
