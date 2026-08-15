<div align="center">

<img src="assets/brand/icon-dark.svg" alt="" width="104" height="104">

# s&box Skill and MCP Server

### Nothing in s&box tells you when you are wrong.

[![Reference files](https://img.shields.io/badge/17_references-2BB88E?style=for-the-badge)](skills/sbox/references)
[![MCP tools](https://img.shields.io/badge/18_MCP_tools-00A8E8?style=for-the-badge)](editor-mcp)
[![Engine](https://img.shields.io/badge/s%26box-26.08.05-3E3E3E?style=for-the-badge)](https://sbox.game)
[![Licence](https://img.shields.io/badge/MIT-3E3E3E?style=for-the-badge)](LICENSE)
[![Devlog](https://img.shields.io/badge/devlog-3E3E3E?style=for-the-badge)](https://fobiat.dev/blog/p/sbox-skill)

</div>

<br>

> **In short.** Two things for building [s&box](https://sbox.game) games, either usable on its own.
>
> **The skill** is 17 reference files that teach a coding agent the real s&box API, so it stops
> writing Unity code into your Source 2 project. Plain markdown, no runtime, no dependencies, so
> it works with any agent that can read a file. Every API in it is traceable to engine source at
> a named version.
>
> **The toolset** is one C# file for your `Editor/` folder that adds 18 tools to the editor's
> MCP server. It answers the questions the editor otherwise leaves silent: does this type
> actually exist, has the compiler noticed my edit, why did nothing happen when I changed that
> config.

<br>

> **On the two words.** The package is called **s&box MCP Server**. Strictly it is a
> *toolset*: s&box ships the MCP server inside the editor, on `127.0.0.1:7269`, and this
> registers into it rather than starting anything. Both words appear below and they mean
> different things: the **server** is the editor's, the **toolset** is what this adds to it.

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
| `Model.Load( "typo/path.vmdl" )` | Comes back as the engine's error model, non-null with `IsError` set, so the null check everyone writes is a branch that can never fire and the world ships orange with a clean console |

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

## The s&box MCP Server

[One file](editor-mcp/SboxMcpServer.cs) into your `Editor/` folder, eighteen tools onto the
editor's MCP server. It solves a different silent failure: the editor not noticing you changed
anything.

Six reasons it might not, none of which raise an error.

| What you changed | Why nothing happened |
|---|---|
| `.sbproj` | Read once at editor boot, never watched |
| `ProjectSettings/*.config` | Cached on first read, never invalidated |
| A `.cs` file | Compiler watchers stop firing once compilers are recreated |
| A content path | `Model.Load` returns the error model, not null, so your null check never fires |
| Nothing, you pulled a branch | The process serves the assembly it loaded at boot |
| Installed a package over MCP | `install_package` mounts for the session and writes nothing |

Each one leaves you having edited a file, seen no error, and concluding the edit was wrong
when it simply never arrived.

| | Tools |
|---|---|
| **Ask the live engine** | `project_find_type`, `project_type_members`, `project_find_member`, `project_enum_values`, `project_input_actions`, `project_console_commands`, `project_content_path`, `project_content_search` |
| **Ask the editor** | `project_info`, `project_compilers`, `project_source_changes`, `project_compile_errors`, `project_assembly_freshness`, `project_package_references` |
| **Change something** | `project_reload_config`, `project_reload_settings`, `project_rebuild`, `project_build` |

The first group inverts this project's own rule. Instead of "if it is in none of the reference
files it does not exist", you ask the running engine, and that answer cannot go out of date.

### Pointing an agent at it

The server is the editor's, not this project's. It listens on `http://127.0.0.1:7269/mcp`,
loopback only, while the editor is running.

```bash
claude mcp add --transport http sbox http://127.0.0.1:7269/mcp
```

The port is `McpServerPort` in the editor's preferences. It acts on whatever project the editor
currently has open, never on your shell's working directory.

One thing that reads as a bug and is not: **the toolset never appears in your client's tool
list.** Listing is gated on an attribute internal to the engine, so only the editor's own seven
top-level tools are listed and everything else is reached through `search_tools`,
`describe_toolset` and `call_tool`. Search `project_` and they come back.

Details in [`editor-mcp/README.md`](editor-mcp/README.md). There is also a
[library project](library) that packages the same file for asset.party, so it can be a package
reference rather than a copied file.

> **Status:** compiles clean against real engine assemblies in **both** nullable configurations,
> `enable` and `disable`, each with warnings as errors, verified by
> [`editor-mcp/compilecheck`](editor-mcp/compilecheck). Both, because a generated `Editor/`
> project takes that setting from the `.sbproj` field `Nullables`, which defaults to off, and
> checking only the strict one is how a shipped file compiled here and failed on a drop-in.
>
> All 14 read-only tools have been called against a live editor and returned structured data. The
> 4 that mutate editor state have not, since they start builds in an open session. Report anything
> that misbehaves and it gets fixed.

<br>

## Get it running

> Setting up **both halves together**? Follow the
> **[Quickstart](QUICKSTART.md)** instead, which covers the loop they form and the problems each
> one fixes for the other. The **[in-depth guide](GUIDE.md)** is the long version: every tool,
> the six ways an edit disappears, and how to write a toolset of your own.

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
<summary><b>Claude Code</b>, as a plugin</summary>

<br>

The repo is its own plugin marketplace, so there is nothing to clone and nothing to copy:

```
/plugin marketplace add fobiat/sbox-skill
/plugin install sbox@sbox-skill
```

The skill triggers on its own frontmatter, and `/plugin update` brings new engine versions with
it. `editor-mcp/SboxMcpServer.cs` comes along in the installed plugin, so the
[toolset](#the-other-half-the-sbox-mcp-server) is a file copy away rather than another download.

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
## Layout

```
sbox-skill/
├── skills/sbox/        the skill, this is what you install
├── editor-mcp/         the s&box MCP Server toolset, a drop-in Editor/ file
├── library/            the same toolset as an s&box library, for asset.party
├── .claude-plugin/     marketplace manifest, makes the repo installable
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

Built and maintained by **fobiat (Kyle Tarff)**

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
