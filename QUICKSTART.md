<div align="center">

# Quickstart

**Both halves, set up and working together, in about ten minutes.**

</div>

<br>

The skill and the toolset solve two halves of the same problem. The skill tells your agent what
the API is. The toolset lets it check against the engine actually running. Used together they
close the loop: write, verify, build, read the real error.

You can run either alone. This walks through both.

<br>

## 1. Get the files

```bash
git clone https://github.com/fobiat/sbox-skill.git
```

Nothing to build. The skill is plain markdown, the toolset is one C# file.

<br>

## 2. Install the skill

Pick whichever matches your agent.

**If your tool auto-loads skills from a directory**, `SKILL.md` already has the frontmatter, so
it triggers by itself:

```powershell
New-Item -ItemType Directory -Force your-game\.claude\skills | Out-Null
Copy-Item -Recurse sbox-skill\skills\sbox your-game\.claude\skills\
```

<details><summary>bash</summary>

```bash
mkdir -p your-game/.claude/skills
cp -r sbox-skill/skills/sbox your-game/.claude/skills/
```

</details>

**Otherwise**, copy it somewhere your agent can read and add three lines to your project
instructions file:

```powershell
New-Item -ItemType Directory -Force your-game\docs | Out-Null
Copy-Item -Recurse sbox-skill\skills\sbox your-game\docs\sbox-skill
```

```markdown
## s&box

Before writing or changing any s&box C#, read `docs/sbox-skill/SKILL.md` and open the reference
file it routes you to. Do not write an API that is not in those files.
```

<br>

## 3. Install the toolset

```powershell
New-Item -ItemType Directory -Force your-game\Editor | Out-Null
Copy-Item sbox-skill\editor-mcp\SboxMcpServer.cs your-game\Editor\
```

<details><summary>bash</summary>

```bash
mkdir -p your-game/Editor
cp sbox-skill/editor-mcp/SboxMcpServer.cs your-game/Editor/
```

</details>

`Editor/` is a separately compiled assembly, not an `#if EDITOR` block in your game code. That
is what lets it reach engine internals.

Restart the editor.

<br>

## 4. Point your agent at the editor

The toolset registers into the MCP server that ships **inside the s&box editor**. Nothing here
starts a server; the editor already has one, and your agent has to be told where it is.

It listens on `http://127.0.0.1:7269/mcp`, loopback only. For Claude Code:

```bash
claude mcp add --transport http sbox http://127.0.0.1:7269/mcp
```

For anything that reads a JSON config, the same thing:

```json
{ "mcpServers": { "sbox": { "type": "http", "url": "http://127.0.0.1:7269/mcp" } } }
```

The editor must be running for any of this to answer, and it acts on **whatever project the
editor currently has open**, never on your shell's working directory.

The port is `McpServerPort` in the editor's preferences, and `McpServerEnabled` turns the
server off entirely. If you have changed either, use your value rather than 7269.

> **The toolset will not appear in your client's tool list, and that is expected.** Listing is
> gated on an attribute that is internal to the engine, so only the editor's own seven top-level
> tools are listed. Everything else, including all of `sbox_mcp_server`, is reached through
> `search_tools`, `describe_toolset` and `call_tool`. Run `search_tools` with `project_` and you
> should see the toolset's tools come back.

<br>

## 5. Confirm both took

**The skill.** Ask for something that should trip a Unity reflex:

> Write me an s&box component that moves a cube forward at 200 units per second.

| You get | Meaning |
|---|---|
| `: Component`, `protected override void OnUpdate()`, `Vector3.Forward` | Working |
| `MonoBehaviour`, `void Update()`, `Vector3.forward` | Not loaded, check the path |

**The toolset.** Ask it to look something up:

> Use `project_find_type` to check whether `SceneTrace` exists, then list its members.

You should get a real signature list back. If the tool is missing, check the editor restarted
and that `Editor/` compiled.

<br>

## 6. Use them together

This is the part worth reading. Each half covers the other's blind spot.

| Half | Good at | Blind spot |
|---|---|---|
| The skill | Idioms, traps, worked examples, what to do instead | Frozen at engine 26.08.05 |
| The toolset | Exact current signatures, live compiler state | Knows nothing about idiom or intent |

### The loop

Put this in your instructions file and the two start reinforcing each other:

```markdown
When writing s&box C#:

1. Read the reference file SKILL.md routes to before writing anything.
2. For any API you are not certain of, call `project_find_type` and
   `project_type_members` rather than guessing. No match means it does not exist.
3. After editing, call `project_build`. If it fails, call `project_compile_errors`.
4. If the build reports nothing changed, call `project_source_changes`. An empty
   result means the file watchers are stale, not that your edit was wrong.
```

### What that fixes, concretely

**Guessing.** Step 2 turns the skill's core rule into an action. "Do not write an API you cannot
find" is only enforceable when there is somewhere to look, and the engine is a better source
than any document.

**Silent staleness.** Step 4 is the one people do not expect. Three separate engine behaviours
mean your edit may never reach the compiler at all: the `.sbproj` is never watched,
`ProjectSettings/*.config` is cached forever, and compiler file watchers go stale after the
compilers are recreated. None of them raise an error. Without step 4 the agent assumes its code
was wrong and starts rewriting working code.

**Drift.** When the engine moves ahead of the reference files, step 2 catches it first, because
the live answer wins over the document by construction.

<br>

## 7. Worked example

A realistic exchange once both are installed.

> **You:** Add a networked health component. Only the host may change health, and clients get a
> change callback for the HUD.

A well-behaved agent should now:

1. Open `04_NETWORKING.md`, because the task said networked.
2. Write `[Sync( SyncFlags.FromHost )] public float Health { get; set; }` with
   `[Change( nameof( OnHealthChanged ) )]`.
3. Call `project_find_type` on `SyncFlags` if it is unsure the flag exists.
4. Call `project_build`, then `project_compile_errors` if it fails.

And it should warn you about two things without being asked, because both are in the reference:

- A client writing to that property gets its write **discarded before the backing field**, with
  no exception, and the read-back on the next line already shows the host's value.
- If the object is a scene object, `NetworkMode.Snapshot` is the default and does **not**
  live-replicate, even though RPCs keep working perfectly.

If it does not mention either, the skill is not loaded.

<br>

## Common problems

<details>
<summary><b>The agent still writes <code>MonoBehaviour</code></b></summary>

<br>

The skill is not being read. Check the path in your instructions file actually resolves, and
that `SKILL.md` still starts with its `---` frontmatter block. Anything above that block stops a
skills-directory tool registering it at all.

</details>

<details>
<summary><b><code>sbox_mcp_server</code> does not appear in <code>list_toolsets</code></b></summary>

<br>

The `Editor/` assembly did not compile, or the editor did not restart. Check the editor console
for a compile error in `SboxMcpServer.cs`. If it names a missing engine member, the engine has
moved past 26.08.05 and that member was renamed. The thrown name tells you which one, which is
the whole reason it throws instead of failing quietly.

</details>

<details>
<summary><b>I edited a file and nothing happened</b></summary>

<br>

That is the exact case step 4 exists for. `project_source_changes` tells you whether the
compiler saw it. Empty result means stale watchers, so run `project_build`. If the compiler did
see it, you have a normal compile error, so run `project_compile_errors`.

</details>

<details>
<summary><b>The reference disagrees with the engine</b></summary>

<br>

The engine wins, always. Check with `project_type_members`, then please
[open an issue](https://github.com/fobiat/sbox-skill/issues/new?template=wrong-api.yml) saying
where you confirmed it. Stale guidance is worse than none, and drift is only findable by the
people hitting it.

</details>

<br>

## Next

- [`SKILL.md`](skills/sbox/SKILL.md) for the Unity translation table and the full trap list.
- [`14_VERIFICATION.md`](skills/sbox/references/14_VERIFICATION.md) for behaviour confirmed in
  live editor sessions, with dates.
- [`editor-mcp/README.md`](editor-mcp/README.md) for every tool and every engine member it
  reaches.
