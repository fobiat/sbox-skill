#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : listing art for sbox.game
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Regenerates every image on the asset.party listing: the wide and tall
#  thumbnails, five screenshots and the loading screen. Run it after an engine
#  bump so the version and tool names on the art match the repository.
#
#      python scripts/render_listing.py
#
#  The two halves are always the MCP Server and the AI Agent Skill. Never
#  "toolset", which is the repository's internal word for the same code.
# =============================================================================

import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
from brand import *

W, H, SS = 1920, 1080, 2
OUT = "assets/brand/listing"

KW = {"public", "private", "protected", "internal", "sealed", "class", "struct", "void",
      "override", "return", "if", "else", "new", "var", "float", "int", "bool", "string",
      "get", "set", "using", "static", "readonly", "const", "null", "true", "false", "this"}


def toks(line, comment_col=LEG):
    """Cheap C# colouriser: comments, strings, attributes, keywords."""
    out = []
    m = re.search("//", line)
    code, tail = (line[:m.start()], line[m.start():]) if m else (line, "")
    for part in re.split(r"(\[[A-Za-z][^\]]*\]|\"[^\"]*\"|\b\w+\b)", code):
        if not part:
            continue
        if part[0] == "[" or part[0] == '"':
            out.append((part, GREEN))
        elif part in KW or re.fullmatch(r"\d+\w*", part):
            out.append((part, BLUE))
        else:
            out.append((part, WHITE))
    if tail:
        out.append((tail, comment_col))
    return out


def head(d, kicker, kcol, title, sub):
    if kicker:
        text(d, (72 * SS, 104 * SS), kicker, font(BOLD, 25 * SS), kcol, 1.5 * SS)
    text(d, (72 * SS, 158 * SS), title, font(BOLD, 56 * SS), WHITE, -1.5 * SS)
    if sub:
        text(d, (72 * SS, 208 * SS), sub, font(MED, 26 * SS), GREY)


def panel(d, x, y, w, h, fill=PANEL):
    d.rounded_rectangle([x * SS, y * SS, (x + w) * SS, (y + h) * SS],
                        radius=14 * SS, fill=fill, outline=EDGE, width=max(1, SS))


def rule(d, x, y, w):
    d.rectangle([x * SS, y * SS, (x + w) * SS, y * SS + SS], fill=EDGE)


def shot(name, draw_fn):
    im, d = canvas(W, H, SS, barh=8)
    draw_fn(d)
    footer(d, W, H, SS)
    save(im, W, H, os.path.join(OUT, name))


# ---------------------------------------------------------------- 01 problem
TRAPS = [
    ("void Update()",
     "Never called. The lifecycle method is OnUpdate(), and yours is just a method nobody invokes."),
    ("NetworkSpawn()",
     "Ownership goes to whoever called it, not the host. A world object now belongs to one random client."),
    ("[Sync] on a scene object",
     "NetworkMode.Snapshot is the default and does not live-replicate. RPCs keep working perfectly, which is what makes it so hard to spot."),
    ("[Sync(SyncFlags.FromHost)]",
     "Written by a client, it is discarded before it reaches the backing field. No exception, and the read-back already shows the authoritative value."),
    ("Model.Load( \"typo.vmdl\" )",
     "Comes back as the engine's error model, non-null with IsError set, so the null check everyone writes is a branch that can never fire."),
]


def s01(d):
    head(d, "THE PROBLEM", RED, "Five things that compile, look right, and do nothing",
         "No error. No warning. A clean console, and an afternoon gone.")
    y = 300
    fc, fp = font(MED, 26 * SS), font(REG, 23 * SS)
    for code, why in TRAPS:
        panel(d, 72, y, 1776, 116)
        text(d, (108 * SS, (y + 46) * SS), code, fc, MCP)
        for i, ln in enumerate(wrap(why, fp, 1080 * SS)):
            text(d, (700 * SS, (y + 44 + i * 32) * SS), ln, fp, GREY)
        y += 132


# ---------------------------------------------------------------- 02 the skill
REFS = [("01_SCENE", "Scene, GameObject, lifecycle, prefabs"),
        ("02_COMPONENTS", "Rendering, physics, controllers, camera"),
        ("03_UI", "Razor panels, BuildHash, SCSS, flexbox"),
        ("04_NETWORKING", "Lobbies, ownership, [Sync], NetList, RPCs"),
        ("05_INPUT_PHYSICS", "Input actions, Scene.Trace, math, time"),
        ("06_EDITOR", "EditorTool, docks, the Widget system"),
        ("07_SERVICES", "Stats, leaderboards, save data, packages"),
        ("08_AVATARS", "Citizen model, Clothing, body groups"),
        ("09_RENDERING", "Shader anatomy, HLSL entry points, layers"),
        ("10_AUDIO", "Mixer graph, SoundHandle, processors"),
        ("11_ACTIONGRAPH", "Exposing C# as nodes, graph callbacks"),
        ("12_VR_VOICE", "VR rig, controllers, haptics, voice"),
        ("13_EXAMPLES", "Eleven complete components, FPS to vendor"),
        ("14_VERIFICATION", "Confirmed live in the editor, with dates"),
        ("15_API_CORE", "Full signatures for the types you use"),
        ("16_API_INDEX", "Namespace index of the wider API surface"),
        ("17_CONSOLE", "[ConCmd], [ConVar], the injected caller")]


def s02(d):
    head(d, "THE AI AGENT SKILL", AI, "17 reference files your agent reads before it writes",
         "Plain markdown. No runtime, no dependencies, works with any agent that can open a file.")
    fn, fd = font(BOLD, 24 * SS), font(REG, 20 * SS)
    for i, (nm, desc) in enumerate(REFS):
        col, row = i // 6, i % 6
        x, y = 72 + col * 600, 292 + row * 78
        panel(d, x, y, 560, 66)
        text(d, ((x + 22) * SS, (y + 29) * SS), nm, fn, AI)
        text(d, ((x + 22) * SS, (y + 55) * SS), desc, fd, GREY)
    panel(d, 72, 760, 1776, 158)
    fq, fr = font(MED, 24 * SS), font(REG, 23 * SS)
    text(d, (108 * SS, 800 * SS),
         "SKILL.md is a router. It answers almost nothing itself, it works out which file holds the answer and sends the agent there.",
         fr, GREY)
    cols = (400, 700, 1010, 1370)
    for base, label, col, words in (
            (856, "Without it", RED,
             ("MonoBehaviour", "void Update()", "Vector3.forward", "Debug.Log")),
            (898, "With it", AI,
             ("Component", "OnUpdate()", "Vector3.Forward", "Log.Info"))):
        text(d, (108 * SS, base * SS), label, fq, GREY)
        for cx, wd in zip(cols, words):
            text(d, (cx * SS, base * SS), wd, fq, col)


# ---------------------------------------------------------------- 03 skill in use
WRONG = [
    'public class Mover : MonoBehaviour   // does not exist',
    '{',
    '    void Update()                    // never called',
    '    {',
    '        transform.position +=        // no such property',
    '          Vector3.forward            // it is .Forward',
    '          * Time.deltaTime;          // it is Time.Delta',
    '    }',
    '}',
]

RIGHT = [
    'public sealed class Mover : Component',
    '{',
    '    [Property] public float Speed { get; set; } = 200f;',
    '',
    '    protected override void OnUpdate()',
    '    {',
    '        if ( IsProxy ) return;',
    '        WorldPosition += Vector3.Forward * Speed * Time.Delta;',
    '    }',
    '}',
]

PROMPTS = [
    ("Name the subsystem",
     "Add a networked health component. Only the host may change health."),
    ("Ask for the trap",
     "I am spawning this prefab from the host. What will bite me?"),
    ("Make it verify first",
     "Confirm every API you plan to use exists. List what you could not find."),
]


def s03(d):
    head(d, "THE AI AGENT SKILL", AI, "Same prompt, before and after",
         "s&box borrows GameObject and Component from Unity, then diverges nearly everywhere else.")
    fc, fh = font(REG, 21 * SS), font(BOLD, 25 * SS)
    for idx, (title, dot, lines, ccol) in enumerate(
            [("What an agent writes unprompted", RED, WRONG, RED),
             ("What the skill makes it write", AI, RIGHT, LEG)]):
        x = 72 + idx * 900
        panel(d, x, 288, 840, 452)
        cap(d, ((x + 34) * SS, 330 * SS), 7 * SS, dot)
        text(d, ((x + 56) * SS, 338 * SS), title, fh, WHITE)
        rule(d, x + 26, 362, 788)
        for i, ln in enumerate(lines):
            spans(d, ((x + 30) * SS, (406 + i * 33) * SS), toks(ln, ccol), fc)
    panel(d, 72, 764, 1776, 154)
    fl, fp = font(BOLD, 23 * SS), font(REG, 23 * SS)
    text(d, (108 * SS, 800 * SS), "What changes the output is how you ask", fl, WHITE)
    for i, (label, prompt) in enumerate(PROMPTS):
        y = 840 + i * 34
        text(d, (108 * SS, y * SS), label, fp, AI)
        text(d, (490 * SS, y * SS), prompt, fp, GREY)


# ---------------------------------------------------------------- 04 mcp server
GROUPS = [
    ("Ask the live engine", MCP,
     ["project_find_type", "project_type_members", "project_find_member",
      "project_enum_values", "project_input_actions", "project_console_commands",
      "project_content_path", "project_content_search"]),
    ("Ask the editor", MCP,
     ["project_info", "project_compilers", "project_source_changes",
      "project_compile_errors", "project_assembly_freshness", "project_package_references"]),
    ("Change something", GREY,
     ["project_reload_config", "project_reload_settings", "project_rebuild", "project_build"]),
]


def s04(d):
    head(d, "THE MCP SERVER", MCP, "18 tools, so an agent can ask instead of guess",
         "A reference file can go out of date. The running engine cannot.")
    fh, ft = font(BOLD, 27 * SS), font(MED, 23 * SS)
    for c, (title, col, tools) in enumerate(GROUPS):
        x = 72 + c * 600
        panel(d, x, 292, 560, 470)
        text(d, ((x + 26) * SS, 338 * SS), title, fh, col)
        rule(d, x + 26, 358, 508)
        for i, t in enumerate(tools):
            text(d, ((x + 26) * SS, (402 + i * 42) * SS), t, ft, WHITE)
    panel(d, 72, 788, 1776, 130)
    fr, fm = font(REG, 22 * SS), font(MED, 24 * SS)
    text(d, (108 * SS, 828 * SS),
         "The editor's own MCP server, on 127.0.0.1:7269, loopback only, while the editor is running.",
         fr, GREY)
    text(d, (108 * SS, 868 * SS),
         "claude mcp add --transport http sbox http://127.0.0.1:7269/mcp", fm, MCP)
    text(d, (108 * SS, 904 * SS),
         "Search project_ and all 18 come back through search_tools and call_tool.", fr, LEG)


# ---------------------------------------------------------------- 05 install
BLOCKS = [
    ("AI Agent Skill", AI, 72, [
        ("Any agent with a skills directory", None),
        ("cp -r sbox-skill/skills/sbox .claude/skills/", AI),
        ("", None),
        ("Claude Code, the repo is its own marketplace", None),
        ("/plugin marketplace add fobiat/sbox-skill", AI),
        ("/plugin install sbox@sbox-skill", AI),
        ("", None),
        ("Anything else, point your instructions file", None),
        ("at skills/sbox/SKILL.md and it routes itself.", None),
    ]),
    ("MCP Server", MCP, 972, [
        ("Project Settings, package references, add", None),
        ("fobiat.sbox_mcp_server", MCP),
        ("then restart the editor.", None),
        ("", None),
        ("Point an agent at the editor's server", None),
        ("claude mcp add --transport http sbox \\", MCP),
        ("  http://127.0.0.1:7269/mcp", MCP),
        ("", None),
        ("Search project_ and the 18 tools come back.", None),
    ]),
]


def s05(d):
    head(d, "BOTH HALVES", WHITE, "Either one is usable on its own",
         "Install what you need. Nothing to build, no runtime, no dependencies.")
    fh, fm, fb = font(BOLD, 28 * SS), font(REG, 22 * SS), font(MED, 22 * SS)
    for title, col, x, rows in BLOCKS:
        panel(d, x, 292, 876, 496)
        text(d, ((x + 30) * SS, 342 * SS), title, fh, col)
        rule(d, x + 30, 366, 816)
        for i, (ln, c) in enumerate(rows):
            text(d, ((x + 30) * SS, (410 + i * 40) * SS), ln, fb if c else fm, c or GREY)
    panel(d, 72, 812, 1776, 106)
    fr = font(MED, 24 * SS)
    text(d, (108 * SS, 852 * SS),
         "Written against engine 26.08.05. Every API traceable to source at a named version. MIT.",
         fr, GREY)
    text(d, (108 * SS, 892 * SS),
         "github.com/fobiat/sbox-skill      fobiat.dev", fr, AI)


# ---------------------------------------------------------------- loading
def loading(path, w=1920, h=1080):
    im, d = canvas(w, h, SS, bar=False)
    icon(d, (w / 2 - 160) * SS, (h / 2 - 268) * SS, 5 * SS)
    text(d, (w / 2 * SS, (h / 2 + 136) * SS), "s&box", font(BOLD, 54 * SS), GREY, -1.5 * SS, "ms")
    text(d, (w / 2 * SS, (h / 2 + 200) * SS), "MCP Server", font(BOLD, 54 * SS), WHITE, -1.5 * SS, "ms")
    text(d, (w / 2 * SS, (h / 2 + 252) * SS), "+ AI Agent Skill", font(BOLD, 34 * SS), MCP, -1.0 * SS, "ms")
    text(d, (w / 2 * SS, (h / 2 + 308) * SS), "fobiat", font(MED, 26 * SS), GREEN, 0, "ms")
    f = font(MED, 22 * SS)
    text(d, (72 * SS, (h - 64) * SS), "github.com/fobiat/sbox-skill", f, LEG)
    text(d, ((w - 72) * SS - width(f, "fobiat.dev"), (h - 64) * SS), "fobiat.dev", f, GREEN)
    d.rectangle([0, (h - 6) * SS, w * SS, h * SS], fill=GREEN)
    save(im, w, h, path)


# ---------------------------------------------------------------- thumbnails
def thumb_tall(path, w=512, h=910):
    im, d = canvas(w, h, SS, barh=6)
    icon(d, 128 * SS, 214 * SS, 4 * SS)
    text(d, (256 * SS, 574 * SS), "s&box", font(BOLD, 62 * SS), GREY, -1.5 * SS, "ms")
    text(d, (256 * SS, 644 * SS), "MCP Server", font(BOLD, 62 * SS), WHITE, -1.5 * SS, "ms")
    text(d, (256 * SS, 700 * SS), "+ AI Agent Skill", font(BOLD, 33 * SS), MCP, -1.0 * SS, "ms")
    text(d, (256 * SS, 754 * SS), "fobiat", font(MED, 26 * SS), GREEN, 0, "ms")
    text(d, (256 * SS, 802 * SS), "github.com/fobiat/sbox-skill", font(MED, 22 * SS), GREY, 0, "ms")
    save(im, w, h, path)


def thumb_wide(path, w=910, h=512):
    im, d = canvas(w, h, SS, barh=6)
    icon(d, 96 * SS, 140 * SS, 3.5 * SS)
    text(d, (376 * SS, 240 * SS), "s&box", font(BOLD, 60 * SS), GREY, -1.5 * SS)
    text(d, (376 * SS, 308 * SS), "MCP Server", font(BOLD, 60 * SS), WHITE, -1.5 * SS)
    text(d, (378 * SS, 360 * SS), "+ AI Agent Skill", font(BOLD, 31 * SS), MCP, -1.0 * SS)
    text(d, (378 * SS, 410 * SS), "fobiat", font(MED, 25 * SS), GREEN)
    text(d, (378 * SS, 452 * SS), "github.com/fobiat/sbox-skill", font(MED, 21 * SS), GREY)
    save(im, w, h, path)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    shot("01-problem.png", s01)
    shot("02-agent-skill.png", s02)
    shot("03-agent-skill-in-use.png", s03)
    shot("04-mcp-server.png", s04)
    shot("05-install.png", s05)
    loading(os.path.join(OUT, "loading-screen.png"))
    thumb_tall("assets/brand/thumb-tall.png")
    thumb_wide("assets/brand/thumb-wide.png")
