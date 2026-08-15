#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : header stamper
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Writes the authorship and provenance block at the top of every skill markdown
#  file, replacing any previous block in place so the stamp never accumulates.
#
#  In SKILL.md the block goes after the YAML frontmatter, never before it. The
#  skill loader reads frontmatter only when it opens the file, so anything above
#  it stops the skill from registering at all.
#
#  Line endings are read and written verbatim. read_text/write_text would have
#  rewritten every file to os.linesep, which is a whole-file diff on any machine
#  whose checkout convention differs from the last person to run this. See
#  .gitattributes.
# =============================================================================

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKILL_DIR = ROOT / "skills" / "sbox"
ENGINE = "26.08.05"
AUTHOR = "fobiat (Kyle Tarff) <kyle@fobiat.dev>"
LINKS = "https://fobiat.dev/   https://github.com/fobiat"

# Tolerant of indentation and of a reflowed opening line, because a header that
# fails to match is a header that gets prepended a second time.
STAMP = re.compile(r"<!--\s*s&box Skill\s*:.*?-->", re.DOTALL)
LEADING_STAMP = re.compile(r"\A\s*<!--\s*s&box Skill\s*:.*?-->[ \t]*\n*", re.DOTALL)

PURPOSE = {
    "SKILL.md": "Router. Identifies which reference file answers the task and sends the reader there.",
    "01_SCENE.md": "Scene, GameObject and Component: the object model, lifecycle, prefabs and scene events.",
    "02_COMPONENTS.md": "The built-in component library: rendering, physics, movement, camera, lighting, audio, navigation.",
    "03_UI.md": "Razor UI: panels, SCSS, the flexbox layout model, built-in controls and world-space panels.",
    "04_NETWORKING.md": "The networking model: lobbies, ownership, [Sync] state, RPCs and network events.",
    "05_INPUT_PHYSICS.md": "Input actions, Scene.Trace, the physics world, math types, time and gizmos.",
    "06_EDITOR.md": "Authoring editor extensions: EditorTool, custom inspectors, docks and the Widget UI system.",
    "07_SERVICES.md": "Backend services and saved state: stats, leaderboards, save data, packages and mounting.",
    "08_AVATARS.md": "The Citizen avatar: Clothing, ClothingContainer and dressing a SkinnedModelRenderer.",
    "09_RENDERING.md": "Shader authoring and the render path: .shader files, materials, render attributes and layers.",
    "10_AUDIO.md": "The audio mixer graph and sound handles, plus phrases and language files.",
    "11_ACTIONGRAPH.md": "ActionGraph: exposing C# as nodes, and graph-backed callbacks a designer can wire.",
    "12_VR_VOICE.md": "VR rig, controllers and haptics, plus voice chat capture, transmission and playback.",
    "15_API_CORE.md": "Full signatures for the types a game touches most.",
    "16_API_INDEX.md": "Namespace-organised index of the wider API surface.",
    "13_EXAMPLES.md": "Complete worked examples, from an FPS controller to a press-E vendor.",
    "14_VERIFICATION.md": "The editor MCP server, and the ledger of behaviour confirmed in live sessions.",
}


def header(name):
    purpose = PURPOSE.get(name, "Reference material for writing s&box C#.")
    return (
        "<!--\n"
        f"  s&box Skill : {name}\n"
        "\n"
        f"  {purpose}\n"
        "\n"
        f"  Author  : {AUTHOR}\n"
        f"  Links   : {LINKS}\n"
        f"  Engine  : s&box {ENGINE}\n"
        "  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,\n"
        "            which is MIT licensed. See LICENSE at the repository root.\n"
        "-->\n\n"
    )


def read_preserving_eol(path):
    raw = path.read_text(encoding="utf-8", newline="")
    eol = "\r\n" if "\r\n" in raw else "\n"
    return raw.replace("\r\n", "\n"), eol


def split_frontmatter(text):
    if not text.startswith("---\n"):
        return "", text
    end = text.find("\n---\n", 4)
    if end == -1:
        return "", text
    return text[: end + 5], text[end + 5 :].lstrip("\n")


def stamp(path):
    text, eol = read_preserving_eol(path)
    frontmatter, body = split_frontmatter(text)

    while LEADING_STAMP.match(body):
        body = LEADING_STAMP.sub("", body, count=1)

    # Anything left is a stamp somewhere in the middle of the file, which this
    # script did not put there and must not silently delete.
    duplicate = STAMP.search(body)
    if duplicate:
        line = body.count("\n", 0, duplicate.start()) + 1
        return False, line

    new = frontmatter + ("\n" if frontmatter else "") + header(path.name) + body
    if new == text:
        return False, None

    path.write_text(new.replace("\n", eol), encoding="utf-8", newline="")
    return True, None


def main():
    targets = [SKILL_DIR / "SKILL.md"] + sorted((SKILL_DIR / "references").glob("*.md"))
    missing = [p.name for p in targets if p.name not in PURPOSE]
    if missing:
        print(f"FAIL: no purpose line for {', '.join(missing)}")
        return 1

    changed = 0
    duplicates = []
    for path in targets:
        was_changed, duplicate_line = stamp(path)
        changed += was_changed
        if duplicate_line:
            duplicates.append(f"{path.relative_to(ROOT)}: second header at body line {duplicate_line}")

    if duplicates:
        print(f"FAIL: {len(duplicates)} file(s) carry a header below the first one\n")
        for message in duplicates:
            print(f"  {message}")
        return 1

    print(f"OK: stamped {len(targets)} files, {changed} changed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
