# Contributing

The value of this skill is that a model can trust what it reads. That makes one
rule matter more than all the others.

## Never write an API you have not verified

Every type, method, property and attribute in these files must be traceable to
the engine source at [Facepunch/sbox-public](https://github.com/Facepunch/sbox-public),
or to a behaviour observed in a live editor session.

A confident wrong signature is worse than no signature at all. If the skill omits
something, the model looks it up. If the skill states it incorrectly, the model
writes it and the failure surfaces later, in the editor, with no obvious cause.

When you cannot verify something, either leave it out or mark it explicitly as
unverified. Both are fine. Guessing is not.

## Source-read versus live-verified

These are different kinds of claim and the skill keeps them apart.

**Source-read** tells you what the API is. It comes from reading engine source at
a named version. Cite the repo-relative path.

**Live-verified** tells you what the API does. It comes from running the thing and
watching what happened. Cite the date.

Where the two disagree, the ledger in `references/field-notes.md`
wins, and the disagreement itself is worth recording. Most of the traps in this
skill are cases where a correct-looking call silently does nothing.

## House style

The reference files are dense on purpose. A model reading them has a budget.

- **No em dashes.** Use a comma, a colon, brackets, or start a new sentence. CI
  enforces this, so a stray one fails the build.
- Comment sparingly in code blocks. Comment the non-obvious why, never the what.
  Most blocks need no comments.
- Prefer a concrete signature, path or number over an adjective.
- Skip the throat-clearing. No "it is worth noting", no restating the heading in
  the first sentence, no closing paragraph that summarises what was just said.
- Signature tables beat prose for API surface. Prose is for traps and reasons.

## Adding a reference file

1. Write it in `skills/sbox/references/`.
2. Open it with one paragraph naming what it covers, the engine version, and the
   upstream paths you read.
3. Add a row to the routing table in `SKILL.md`. An unrouted file is invisible,
   and CI will fail for exactly that reason.
4. Run `python3 tools/check_skill.py`.

## Engine version

The skill names the version it was written against. When you verify against a
newer engine, update the version where you touched it rather than silently
leaving a stale number in place. A reference that does not say what it is true
for cannot be checked later.

## Before you open a PR

```
python3 tools/check_skill.py
```

Green means the routing table resolves, frontmatter is intact, and no em dashes
slipped in. It does not mean an API is correct. Only reading the source does that.
