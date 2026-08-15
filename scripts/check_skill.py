#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : structure checker
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Validates the skill tree before it ships. Four checks:
#    1. SKILL.md carries valid YAML frontmatter with a name and a description.
#    2. Every references/<file>.md named in SKILL.md actually exists.
#    3. Every file in references/ is routed to from SKILL.md.
#    4. No em dash appears anywhere in the skill, per house style.
#
#  Checks 2 and 3 are the ones that matter. SKILL.md is a router: it answers
#  nothing itself and sends the model to a reference file. A pointer to a file
#  that does not exist, or a file no pointer reaches, degrades the skill without
#  producing any error a reader would notice.
# =============================================================================

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKILL_DIR = ROOT / "skills" / "sbox"
SKILL_MD = SKILL_DIR / "SKILL.md"
POINTER = re.compile(r"references/([A-Za-z0-9._-]+\.md)")

failures = []
pointers_checked = 0


def fail(message):
    failures.append(message)


def check_frontmatter(text):
    if not text.startswith("---\n"):
        fail("SKILL.md: missing YAML frontmatter")
        return

    end = text.find("\n---\n", 4)
    if end == -1:
        fail("SKILL.md: unterminated frontmatter")
        return

    frontmatter = text[4:end]
    for key in ("name:", "description:"):
        if key not in frontmatter:
            fail(f"SKILL.md: frontmatter missing {key}")


def check_pointers(text):
    global pointers_checked
    for name in sorted(set(POINTER.findall(text))):
        pointers_checked += 1
        if not (SKILL_DIR / "references" / name).is_file():
            fail(f"SKILL.md points at references/{name}, which does not exist")


def check_orphans(text):
    routed = set(POINTER.findall(text))
    for path in sorted((SKILL_DIR / "references").glob("*.md")):
        if path.name not in routed:
            fail(f"references/{path.name} exists but SKILL.md never routes to it")


def check_house_style():
    targets = sorted(SKILL_DIR.rglob("*.md")) + sorted((ROOT / "editor-mcp").rglob("*"))
    for path in targets:
        if not path.is_file() or path.suffix not in (".md", ".cs"):
            continue
        lines = path.read_text(encoding="utf-8").splitlines()
        for number, line in enumerate(lines, 1):
            if "—" in line:
                rel = path.relative_to(ROOT)
                fail(f"{rel}:{number}: em dash, use a comma, colon, brackets or a new sentence")


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
        for message in failures:
            print(f"  {message}")
        return 1

    print(f"OK: {pointers_checked} routing pointers resolve, frontmatter valid, no em dashes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
