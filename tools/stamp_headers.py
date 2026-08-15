#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : header stamper
#
#  Author   : Kyle (fobiat) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Writes the authorship and provenance block at the top of every skill markdown
#  file, and rewrites it in place on re-run so the stamp never accumulates.
#
#  In SKILL.md the block goes after the YAML frontmatter, never before it. The
#  skill loader reads frontmatter only when it opens the file, so anything above
#  it stops the skill from registering at all.
# =============================================================================

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKILL_DIR = ROOT / "skills" / "sbox"
ENGINE = "26.08.05"
AUTHOR = "Kyle (fobiat) <kyle@fobiat.dev>"
LINKS = "https://fobiat.dev/   https://github.com/fobiat"

BLOCK = re.compile(r"\A<!--\n  s&box Skill.*?-->\n\n?", re.DOTALL)

PURPOSE = {
    "SKILL.md": "Router. Identifies which reference file answers the task and sends the reader there.",
    "scene-and-components.md": "Scene, GameObject and Component: the object model, lifecycle, prefabs and scene events.",
    "component-library.md": "The built-in component library: rendering, physics, movement, camera, lighting, audio, navigation.",
    "razor-interfaces.md": "Razor UI: panels, SCSS, the flexbox layout model, built-in controls and world-space panels.",
    "multiplayer.md": "The networking model: lobbies, ownership, [Sync] state, RPCs and network events.",
    "input-traces-and-physics.md": "Input actions, Scene.Trace, the physics world, math types, time and gizmos.",
    "editor-extensions.md": "Authoring editor extensions: EditorTool, custom inspectors, docks and the Widget UI system.",
    "backend-and-saved-data.md": "Backend services and saved state: stats, leaderboards, save data, packages and mounting.",
    "avatars-and-outfits.md": "The Citizen avatar: Clothing, ClothingContainer and dressing a SkinnedModelRenderer.",
    "shading-and-render-path.md": "Shader authoring and the render path: .shader files, materials, render attributes and layers.",
    "sound-and-language.md": "The audio mixer graph and sound handles, plus phrases and language files.",
    "node-graphs.md": "ActionGraph: exposing C# as nodes, and graph-backed callbacks a designer can wire.",
    "vr-and-voice-chat.md": "VR rig, controllers and haptics, plus voice chat capture, transmission and playback.",
    "api-core.md": "Full signatures for the types a game touches most.",
    "api-index.md": "Namespace-organised index of the wider API surface.",
    "worked-examples.md": "Complete worked examples, from an FPS controller to a press-E vendor.",
    "field-notes.md": "The editor MCP server, and the ledger of behaviour confirmed in live sessions.",
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


def split_frontmatter(text):
    if not text.startswith("---\n"):
        return "", text
    end = text.find("\n---\n", 4)
    if end == -1:
        return "", text
    return text[: end + 5], text[end + 5 :].lstrip("\n")


def stamp(path):
    text = path.read_text(encoding="utf-8")
    frontmatter, body = split_frontmatter(text)
    body = BLOCK.sub("", body)

    new = frontmatter + ("\n" if frontmatter else "") + header(path.name) + body
    if new == text:
        return False

    path.write_text(new, encoding="utf-8")
    return True


def main():
    targets = [SKILL_DIR / "SKILL.md"] + sorted((SKILL_DIR / "references").glob("*.md"))
    missing = [p.name for p in targets if p.name not in PURPOSE]
    if missing:
        print(f"FAIL: no purpose line for {', '.join(missing)}")
        return 1

    changed = sum(stamp(path) for path in targets)
    print(f"OK: stamped {len(targets)} files, {changed} changed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
