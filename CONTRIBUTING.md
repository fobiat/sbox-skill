# Contributing

The whole value of this repository is that a model can trust what it reads. Everything below
follows from that.

## Never write an API you have not verified

Every type, method, property and attribute here has to be traceable to engine source at
[Facepunch/sbox-public](https://github.com/Facepunch/sbox-public), or to a behaviour someone
actually watched happen in a live editor session.

This is not pedantry, it is the difference between the two failure modes. If the skill leaves
something out, the model notices it is missing and goes and looks it up. If the skill states
it wrongly, the model writes it with complete confidence, and the failure shows up much later
in the editor with nothing pointing back at the cause.

So: when you cannot verify a thing, either leave it out or mark it unverified in the text.
Both are fine. A short honest file beats a long confident wrong one. What is never fine is
filling a gap with something that sounds right.

## Two kinds of claim, kept apart

**Source-read** tells you what the API *is*. It comes from reading engine source at a named
version. Cite the repo-relative path.

**Live-verified** tells you what the API *does*. It comes from running the thing and watching
what actually happened. Cite the date.

They are not the same claim and the gap between them is where the expensive bugs live. When
the two disagree, the ledger in [`references/14_VERIFICATION.md`](skills/sbox/references/14_VERIFICATION.md)
wins, and the disagreement is itself worth writing down, because it is exactly the kind of
thing nobody can look up.

If you contribute a field note, give it the next `FN-` number and keep its date. A note
without a date cannot be checked against a later engine, which makes it a rumour.

## House style

The reference files are dense on purpose. A model reading them is spending budget, and every
sentence that does not carry a fact is a sentence that pushed a real one further away.

- **No em dashes.** Comma, colon, brackets, or start a new sentence. CI fails the build on a
  single one, so this is enforced rather than requested.
- **Comment the non-obvious why, never the what.** Most code blocks need no comments at all.
  If a comment restates the line above it, delete it.
- **Prefer a signature, a path or a number to an adjective.** "Returns null on a bad path" is
  worth more than "handles errors gracefully".
- **Skip the throat-clearing.** No "it is worth noting", no restating the heading in the first
  sentence, no closing paragraph that summarises what you just said.
- **Signature tables beat prose** for API surface. Save prose for traps and reasons, which are
  the things a table cannot hold.

## Adding a reference file

1. Write it in `skills/sbox/references/`.
2. Open it with one paragraph naming what it covers, the engine version, and the upstream
   paths you read.
3. Add a row to the routing table in `SKILL.md`. An unrouted file is an invisible file, and
   CI fails for exactly that reason.
4. Add its one-line purpose to the `PURPOSE` map in `scripts/stamp_headers.py`, then run that
   script to stamp the header.
5. Run the gate.

## The gate

```bash
python3 scripts/check_skill.py
```

It checks that every routing pointer resolves, that no reference file is orphaned, that the
frontmatter is intact, and that no em dash crept in.

Green means the structure holds. It does not mean an API is correct, and nothing automated
can tell you that. Only reading the source does.

## Engine version

Everything here is written against a named engine version. When you verify something against
a newer one, update the version where you touched it rather than leaving a stale number
sitting above fresh text. A reference that does not say what it is true for cannot be checked
later, and an unchecked reference eventually becomes a wrong one.

## Reporting something wrong

The [wrong-API template](.github/ISSUE_TEMPLATE/wrong-api.yml) asks for one thing above all
others: where you confirmed the truth. A path into `sbox-public`, a compiler error, or an
observed result in a live editor session all count equally. "I think this is wrong" is a fine
place to start a conversation and not enough on its own to change a line.

## Credits

Written and maintained by **Kyle (fobiat)**, [fobiat.dev](https://fobiat.dev/),
[github.com/fobiat](https://github.com/fobiat), kyle@fobiat.dev.

MIT licensed. See [LICENSE](LICENSE), which also carries Facepunch's own MIT notice, since
the API surface described here derives from their published managed source.
