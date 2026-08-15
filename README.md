# s&box Skill

An agent skill that teaches Claude to write correct [s&box](https://sbox.game) C#.

s&box is Facepunch's Source 2 game engine with a C# scripting layer. It looks
enough like Unity that a language model will confidently write `MonoBehaviour`,
`Start()`, `transform.position` and `Physics.Raycast` into a file where none of
those exist. Every one of them compiles in the model's head and fails in the
editor. This skill exists to stop that.

Built and maintained by Kyle (fobiat), <https://fobiat.dev>.

## What it is

Ten reference files and a router. `SKILL.md` deliberately contains almost no
answers: it identifies which reference file holds the answer and sends the model
there. That shape is the point. A single flat document gets skimmed, and a
skimmed API reference is how hallucinations get through.

| File | Covers |
|---|---|
| `references/core-concepts.md` | Scene, GameObject, Component, lifecycle, prefabs, scene events |
| `references/components-builtin.md` | Rendering, physics, character controller, camera, lighting, audio, navigation |
| `references/ui-razor.md` | Razor panels, SCSS, flexbox, built-in controls, world panels |
| `references/networking.md` | Lobbies, ownership, `[Sync]`, RPCs, network events, dedicated servers |
| `references/input-and-physics.md` | Input actions, `Scene.Trace`, physics world, math types, time, gizmos |
| `references/editor-tooling.md` | Editor extensions: `EditorTool`, custom inspectors, docks, the Widget UI system |
| `references/services-and-persistence.md` | Stats, leaderboards, save data, packages, mounting |
| `references/avatar-and-clothing.md` | Citizen avatar, `Clothing`, `ClothingContainer`, dressing a renderer |
| `references/shaders-and-rendering.md` | `.shader` authoring, materials, render attributes, render layers |
| `references/audio-and-localization.md` | Mixer graph, sound handles, `Phrase`, language files |
| `references/api-schema-core.md` | Full signatures for the most-used types |
| `references/api-schema-extended.md` | Namespace-organised index of the wider API surface |
| `references/patterns-and-examples.md` | Complete worked examples: FPS controller, networked player, HUD, weapon, AI |
| `references/editor-and-verified-behaviour.md` | Editor MCP server, and behaviour confirmed in live sessions |

## Why the verification ledger matters

Most of this skill records what the API **is**, read out of engine source. One
file records what it was observed to **do**, in a live editor session, with dates.

Those are not the same thing, and the gap between them is where the expensive
bugs live. A `[Sync]` write you are not allowed to make is dropped before it
reaches the backing field: no exception, no warning, and the immediate read-back
already shows the authoritative value. The schema says the property exists and is
settable. The schema is not wrong. It is just not the whole story.

Where a source-read fact and a live-verified fact disagree, the ledger wins.

## Install

Copy `skills/sbox/` into your project:

```
your-project/.claude/skills/sbox/
```

Or into `~/.claude/skills/sbox/` to make it available everywhere.

## Engine version

Written against engine version **26.08.05**. The API surface moves. Every
reference file names the version and the upstream paths it was read from, so a
future regeneration can diff against a newer engine rather than starting over.

## Licence

MIT, see [LICENSE](LICENSE). The described API surface derives from Facepunch's
MIT-licensed [sbox-public](https://github.com/Facepunch/sbox-public); that
copyright notice travels in `LICENSE` as MIT requires.

Not affiliated with Facepunch Studios.
