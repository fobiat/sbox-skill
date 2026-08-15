<!--
  s&box Skill : GUIDE.md

  The long version. README.md sells it, QUICKSTART.md gets it running in ten
  minutes, this explains how it works and why each piece is shaped the way it is.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# The in-depth guide

[README](README.md) is the pitch. [QUICKSTART](QUICKSTART.md) gets both halves running in about
ten minutes. This is the one that explains why any of it is shaped the way it is, and it assumes
you have read neither.

**Contents**

1. [The problem both halves solve](#1-the-problem-both-halves-solve)
2. [Installing](#2-installing)
3. [Connecting an agent to the editor](#3-connecting-an-agent-to-the-editor)
4. [The eighteen tools](#4-the-eighteen-tools)
5. [The loop](#5-the-loop)
6. [Six ways an edit disappears](#6-six-ways-an-edit-disappears)
7. [How the skill is organised](#7-how-the-skill-is-organised)
8. [How to trust it](#8-how-to-trust-it)
9. [Writing your own toolset](#9-writing-your-own-toolset)
10. [When something is wrong](#10-when-something-is-wrong)

---

## 1. The problem both halves solve

s&box borrows `GameObject` and `Component` from Unity and diverges nearly everywhere after that.
Different lifecycle, different networking model, Z-up instead of Y-up, Razor for UI, a restricted
subset of .NET. The resemblance is close enough that a model trained on a decade of Unity answers
pattern-matches the first two names and invents everything downstream, confidently and without a
single warning.

That produces two distinct failures, and they need two distinct fixes.

**The agent writes an API that does not exist.** It compiles, or nearly, and then nothing happens.
The skill fixes this: seventeen reference files describing what the API actually is, every entry
traceable to a path in Facepunch's MIT-licensed engine source at a named version.

**The editor does not notice what you changed.** You edit a file, no error appears, and the thing
you changed has no effect, because the change never reached the compiler at all. The toolset fixes
this: eighteen MCP tools that ask the running engine directly.

Either half works alone. Together they close a loop, which section 5 covers.

> **On the two words.** The package is called **s&box MCP Server**. Strictly it is a
> *toolset*: s&box ships the MCP server inside the editor, on `127.0.0.1:7269`, and this
> registers into it rather than starting anything. Both words appear below and they mean
> different things: the **server** is the editor's, the **toolset** is what this adds to it.


---

## 2. Installing

### The skill

It is plain markdown. No runtime, no dependencies, nothing to build. Any agent that can open a
file can use it.

**As a Claude Code plugin.** The repository is its own marketplace:

```
/plugin marketplace add fobiat/sbox-skill
/plugin install sbox@sbox-skill
```

**Into a skills directory**, where the frontmatter makes it self-triggering:

```powershell
New-Item -ItemType Directory -Force $HOME\.claude\skills | Out-Null
Copy-Item -Recurse sbox-skill\skills\sbox $HOME\.claude\skills\
```

Putting it under your home directory rather than a project makes it available everywhere.

**Anywhere else**, with three lines in your agent's instructions file:

```markdown
## s&box

Before writing or changing any s&box C#, read `docs/sbox-skill/SKILL.md` and open the reference
file it routes you to. Do not write an API that is not in those files.
```

### The toolset

One file into your project's `Editor/` folder:

```powershell
New-Item -ItemType Directory -Force your-game\Editor | Out-Null
Copy-Item sbox-skill\editor-mcp\SboxMcpServer.cs your-game\Editor\
```

Or as a package reference, `fobiat.sbox_mcp_server`, added in Project Settings.

`Editor/` matters. It is a separately compiled assembly that s&box builds **without** the BCL
whitelist it applies to gameplay code, which is what lets the toolset reach engine internals at
all. It is also why you should read any `Editor/` file you install, from this project or any
other. That is not a warning specific to this one, it is how the folder works.

Restart the editor after copying. The assembly is compiled at boot.

---

## 3. Connecting an agent to the editor

This is the step that used to be missing, and without it half the product is unreachable.

**The MCP server is the editor's, not this project's.** Nothing here starts a server. s&box ships
one inside the editor process, this toolset registers into it, and your client has to be told
where it is.

| | |
|---|---|
| URL | `http://127.0.0.1:7269/mcp` |
| Port | `McpServerPort` in editor preferences, default 7269 |
| On/off | `McpServerEnabled`, default on |
| Binding | Loopback only. A non-loopback `Origin` gets 403 |
| Method | `POST` only, path exactly `/mcp` |
| Body cap | 8 MiB |
| Batching | JSON-RPC arrays are refused outright |

```bash
claude mcp add --transport http sbox http://127.0.0.1:7269/mcp
```

Or in any client that reads a JSON config:

```json
{ "mcpServers": { "sbox": { "type": "http", "url": "http://127.0.0.1:7269/mcp" } } }
```

The editor must be running, and it acts on **whatever project the editor currently has open**,
never on your shell's working directory.

### The thing that looks like a bug and is not

**These tools never appear in your client's tool list.** Only the editor's own seven top-level
tools do. Listing is gated on `[McpListed]`, which is `internal` to the engine, so a third-party
toolset cannot apply it.

That is deliberate on Facepunch's part: a list a client fetched once never goes stale as addon
code hotloads. Everything else is reached through `search_tools`, `describe_toolset` and
`call_tool`.

To confirm the install worked, search rather than list:

> Use `search_tools` with the query `project_` and tell me how many come back.

Eighteen is correct.

One consequence worth knowing: the read-only hint never reaches your client's tool list either, so
a client will treat every tool here as a destructive write and may prompt before each. Fourteen
of the eighteen genuinely only ask questions; the four that change something are the reload,
rebuild and build pair.

---

## 4. The eighteen tools

### Ask the running engine

The answer comes from the engine loaded in your editor, so it cannot be stale and it cannot be a
plausible invention. A no-match is a real answer.

| Tool | What it answers |
|---|---|
| `project_find_type` | Does this type exist, and what shape is it |
| `project_type_members` | The real signatures, with `[Obsolete]` members marked |
| `project_find_member` | Which type is this method on, when you know the name but not the owner |
| `project_enum_values` | An enum's named values, which `project_type_members` cannot report |
| `project_input_actions` | The project's input action names and bindings |
| `project_console_commands` | Console commands with their real argument lists |
| `project_content_path` | Does this content path resolve against the mounted filesystem |
| `project_content_search` | What the mounted set actually ships under a directory |

`project_content_path` is the one to reach for most often and the least obvious. `Model.Load` on
a path that resolves to nothing returns the engine's **error model**, non-null with `IsError` set,
so a typo in an authored asset builds clean, passes every headless test, and ships an orange
world with a clean console.

### Ask the editor what it believes

| Tool | What it answers |
|---|---|
| `project_info` | Which project is open, and the compiler settings live in memory |
| `project_compilers` | Per-compiler build state, distinguishing never-built from failed |
| `project_source_changes` | What each compiler has noticed since its last build |
| `project_compile_errors` | Diagnostics as rows with file and line |
| `project_assembly_freshness` | Whether the process is serving an older assembly than the one on disk |
| `project_package_references` | The `.sbproj` list against what is actually installed |

### Make it notice

| Tool | What it does |
|---|---|
| `project_reload_config` | Re-reads the `.sbproj` and recreates the compilers |
| `project_reload_settings` | Drops cached `ProjectSettings` and re-reads input actions |
| `project_rebuild` | Recreates every compiler and starts a build, returns immediately |
| `project_build` | Does that and waits, then reports success plus errors |

---

## 5. The loop

Each half covers the other's blind spot.

| | Good at | Blind spot |
|---|---|---|
| The skill | Idiom, traps, worked examples, what to do instead | Frozen at a named engine version |
| The toolset | Exact current signatures, live compiler state | Knows nothing about idiom or intent |

Put this in your instructions file:

```markdown
When writing s&box C#:

1. Read the reference file SKILL.md routes to before writing anything.
2. For any API you are not certain of, call `project_find_type` and `project_type_members`
   rather than guessing. No match means it does not exist.
3. For any content path in an authored asset, call `project_content_path`. A bad path does
   not throw, it loads the error model.
4. After editing, call `project_build`. If it fails, call `project_compile_errors`.
5. If the build reports nothing changed, call `project_source_changes`, then
   `project_assembly_freshness`. Silence is not proof of success.
```

Step 2 is what turns the skill's core rule into an action. "Do not write an API you cannot find"
is only enforceable when there is somewhere to look, and the engine is a better source than any
document, including this one.

---

## 6. Six ways an edit disappears

None of these raise an error. Each leaves you having changed a file, seen nothing wrong, and
concluding the change was wrong when it simply never arrived.

| What you changed | Why nothing happened | Ask |
|---|---|---|
| `.sbproj` | Read once at editor boot, never watched | `project_reload_config` |
| `ProjectSettings/*.config` | Cached on first read, never invalidated | `project_reload_settings` |
| A `.cs` file | Compiler watchers stop firing once compilers are recreated | `project_rebuild` |
| A content path | `Model.Load` returns the error model, not null | `project_content_path` |
| Nothing, you pulled a branch | The process serves the assembly it loaded at boot | `project_assembly_freshness` |
| Installed a package over MCP | `install_package` mounts for the session and writes nothing | `project_package_references` |

The fifth is the worst of them. Recompiling does not cure a stale assembly, stopping play does
not either, and `compile_status` reads green the whole time. Only closing and reopening the editor
does.

---

## 7. How the skill is organised

`SKILL.md` is a router. It answers almost nothing itself; it works out which file holds the answer
and sends the reader there.

That shape is deliberate. A single flat document gets skimmed, and a skimmed API reference is
exactly how an invented API gets through. Making the reader open a second file puts real content
in front of them at the moment they are about to write.

| Range | What is in it |
|---|---|
| `01` to `05` | Scene and components, the component library, Razor UI, networking, input and physics |
| `06` to `12` | Editor tooling, services, avatars, rendering, audio, ActionGraph, VR and voice |
| `13` | Eleven complete worked components |
| `14` | The MCP server, and the ledger of live-verified behaviour |
| `15` to `16` | Full signatures for the most-used types, and a namespace index |
| `17` | Console commands and convars |

---

## 8. How to trust it

Claims carry one of three states, and the distinction is not decorative.

- **source** means read out of the engine's managed source. It tells you what an API *is*.
- **editor** means watched happening in a live session, with a date. It tells you what it *does*.
- **unverified** means neither, and says so.

That vocabulary exists because of a specific failure. The skill documented `Model.Load` as
returning `null` for an unresolvable path, with a source citation attached, and it was wrong. The
call returns the error model. A project following those notes shipped five commits deleting the
dead branch the skill recommended. Reading `return null` in the source was true; the conclusion
drawn from it was not.

So: where a reference file and the running engine disagree, **the engine wins**, and
`project_find_type` is right there. Where a source-read claim and a ledger row disagree, the
ledger wins.

---

## 9. Writing your own toolset

A project can register its own tools from its `Editor/` folder, and should, for anything that
encodes conventions only that project has.

```csharp
[McpToolset( "yourgame_dev", "What this toolset is for." )]
public static class YourTools
{
    /// <summary>One dense sentence: what it does and the failure it prevents.</summary>
    [McpTool.ReadOnly( "yourgame_something" )]
    public static SomethingResult Something(
        [Description( "What this parameter is." )] string target,
        [Sandbox.Range( 1, 500 )] int limit = 50 )
    {
        ...
    }
}
```

Rules that are not obvious and each cost something to discover:

- **Every tool must be `public static`.** A non-static one is silently ignored by discovery. No
  warning, no error.
- **Return a named class, not `object`.** The registry rejects anything in a `System` namespace
  when deciding whether to emit an output schema, so `object` gets you no schema and no
  `structuredContent`, just a text blob.
- **The XML summary is your entire discovery surface.** See section 3. Write it with the words
  someone would search for.
- **Never export a tool name another toolset already uses.** The registry deduplicates by name in
  a sorted dictionary and skips the loser with a warning, so which one survives depends on
  enumeration order.
- **`[Sandbox.Range]` clamps at runtime**, not just in the schema, and only on parameters that
  carry it.
- Tools run on the main thread. Return `Task<T>` for anything slow or you will hang the editor.

The boundary worth holding: a tool any s&box project would want belongs upstream. A tool that
encodes *your* conventions belongs in *your* toolset, under your own prefix.

---

## 10. When something is wrong

**The agent still writes `MonoBehaviour`.** The skill is not being read. Check the path in your
instructions file resolves, and that `SKILL.md` still opens with its `---` frontmatter block.
Anything above that block stops a skills directory registering it at all.

**`search_tools` finds no `project_` tools.** The `Editor/` assembly did not compile, or the
editor did not restart. Check the editor console. If the toolset compiled but a tool throws, the
error names the engine member it wanted, which is deliberate: the natural failure here is silent
staleness after an engine update, and a thrown name tells you exactly what to go and read.

**A reference file is wrong at your engine version.** That is the failure that matters most, and
[an issue](https://github.com/fobiat/sbox-skill/issues/new?template=wrong-api.yml) with the
version and what you observed is genuinely the most useful thing you can send. A wrong signature
is worse than a missing one, which section 8 is the long story of.
