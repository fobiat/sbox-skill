<!--
  s&box Skill : SECURITY.md

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Licence : MIT, see LICENSE at the repository root.
-->

# Security

## Reporting

Report anything security-relevant privately, not as a public issue.

Use GitHub's [private vulnerability reporting](https://github.com/fobiat/sbox-skill/security/advisories/new)
on this repository, or email <kyle@fobiat.dev>. Expect an acknowledgement within a
few days. This is a single-maintainer project, so there is no formal SLA beyond
that, and I would rather tell you that than write a number I cannot keep.

If you are not sure whether something counts, report it privately anyway. A wrong
guess costs a message; a public issue about a real problem cannot be taken back.

## What this project actually is, and where the risk sits

Two things ship here, and only one of them executes.

**The skill** is markdown. It never runs. Its failure mode is not code execution,
it is *being wrong*: a reference file that documents an API incorrectly causes a
model to write incorrect code, which has already happened once and is tracked as a
correctness bug rather than a vulnerability. Report those as
[Wrong or missing API](https://github.com/fobiat/sbox-skill/issues/new?template=wrong-api.yml).

**The toolset** is a C# file that compiles into your project's `Editor/` assembly,
and that is the part worth thinking about.

### `Editor/` code is unsandboxed. That is the whole point, and it cuts both ways

s&box compiles gameplay code under a BCL whitelist. It does not do this for
`Editor/` assemblies, which run with `Whitelist = false` and `Unsafe = true`. That
is deliberate on Facepunch's part, and it is exactly what lets this toolset reach
engine internals by reflection to answer questions the public API cannot.

It also means anything in `Editor/` can do what any other .NET program on your
machine can do: read and write files, open sockets, start processes. Nothing about
installing this as a package changes that. **Treat every s&box library that ships
`Editor/` code as code you are choosing to run**, from this project or any other.

The file is ~530 lines and deliberately readable. If you would rather check than
trust, that is the correct instinct, and it is short enough to actually do.

### The editor's MCP server is loopback-only

The MCP server this toolset registers into is the engine's, not one this project
starts. It binds `127.0.0.1` and `localhost` on port 7269 by default, rejects
non-loopback `Origin` headers, caps request bodies at 8 MiB, and speaks plain HTTP
with no server-initiated streams. It is reachable from your machine and not from
your network.

Anything connected to it can drive your editor: create and delete scene objects,
write and compile assets, start play mode, run console commands. That is the
feature. It follows that **you should treat the port as a local trust boundary**,
and be as careful about what you connect to it as you would be about what you run
in a terminal.

You can turn it off or move it in the editor's preferences
(`McpServerEnabled`, `McpServerPort`).

### The tools that change things

Seven of the toolset's tools are marked read-only and only ask questions. The rest
reload project config, drop cached settings, recreate compilers and start builds.
None of them touch anything outside the project the editor currently has open,
and none of them reach your shell's working directory. The worst outcome from a
misfired call is a rebuild you did not want.

## In scope

- Anything in this repository that could execute code you did not intend, or reach
  outside the open project.
- A tool that writes somewhere it should not, or that acts on a project other than
  the one the editor has open.
- Secrets, absolute paths or machine-identifying data committed to this repository.
  The release gate scans for these, so a gap in that scan is itself worth reporting.
- Anything in the CI configuration that would let a fork run code on a maintainer's
  machine. The self-hosted job is deliberately gated so that pull requests from
  forks cannot reach it; if you find a way around that gate, please report it.

## Out of scope

- Vulnerabilities in s&box itself, in Source 2, or in Facepunch's backend. Report
  those to [Facepunch](https://github.com/Facepunch/sbox-issues).
- The fact that `Editor/` assemblies are unsandboxed. That is engine behaviour and
  is documented above so you can make an informed choice.
- The fact that an agent connected to the editor's MCP server can modify your
  project. That is what it is for.
- Anything requiring an attacker to already have code execution on your machine.

## Supported versions

The most recent tagged release, against the engine version named in its header
comments. This project tracks a fast-moving engine, so an old tag is not patched;
it is superseded.
