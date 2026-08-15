#!/usr/bin/env python3
"""Structural checks for the skill. A dead routing pointer degrades the skill
silently, which is the exact failure the router design exists to prevent."""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKILL_DIR = ROOT / "skills" / "sbox"
SKILL_MD = SKILL_DIR / "SKILL.md"

failures = []
checked = 0


def fail(msg):
    failures.append(msg)


def check_frontmatter(text):
    if not text.startswith("---\n"):
        fail("SKILL.md: missing YAML frontmatter")
        return
    end = text.find("\n---\n", 4)
    if end == -1:
        fail("SKILL.md: unterminated frontmatter")
        return
    fm = text[4:end]
    for key in ("name:", "description:"):
        if key not in fm:
            fail(f"SKILL.md: frontmatter missing {key}")


def check_pointers(text):
    global checked
    for name in sorted(set(re.findall(r"references/([A-Za-z0-9._-]+\.md)", text))):
        checked += 1
        if not (SKILL_DIR / "references" / name).is_file():
            fail(f"SKILL.md points at references/{name}, which does not exist")


def check_orphans(text):
    named = set(re.findall(r"references/([A-Za-z0-9._-]+\.md)", text))
    for path in sorted((SKILL_DIR / "references").glob("*.md")):
        if path.name not in named:
            fail(f"references/{path.name} exists but SKILL.md never routes to it")


def check_house_style():
    """No em dashes. The rule is absolute, so it is enforced rather than asked for."""
    for path in sorted(SKILL_DIR.rglob("*.md")):
        for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if "—" in line:
                rel = path.relative_to(ROOT)
                fail(f"{rel}:{n}: em dash (use a comma, colon, brackets or a new sentence)")


def main():
    if not SKILL_MD.is_file():
        print(f"FAIL: {SKILL_MD} not found")
        return 1
    text = SKILL_MD.read_text(encoding="utf-8")
    check_frontmatter(text)
    check_pointers(text)
    check_orphans(text)
    check_house_style()
    if failures:
        print(f"FAIL: {len(failures)} problem(s)\n")
        for f in failures:
            print(f"  {f}")
        return 1
    print(f"OK: {checked} routing pointers resolve, frontmatter valid, no em dashes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
