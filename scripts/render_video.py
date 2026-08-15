#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : listing videos for sbox.game
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Two 1080p30 explainers, frames drawn with Pillow and encoded by the ffmpeg
#  that imageio-ffmpeg ships, so nothing has to be on PATH.
#
#      pip install imageio-ffmpeg && python scripts/render_video.py
# =============================================================================

import os, sys, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from PIL import Image, ImageDraw
import imageio_ffmpeg
from brand import *

W, H, FPS = 1920, 1080, 30
OUT = "assets/brand/listing"
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()

PANEL_X, PANEL_Y, PANEL_W, PANEL_H = 140, 236, 1640, 700
LINE_H, PAD = 40, 44


class Cast:
    """Builds a per-frame list of console lines."""

    def __init__(self):
        self.frames, self.lines = [], []

    def _snap(self, n=1, cursor=True):
        for _ in range(int(n)):
            self.frames.append((list(self.lines), cursor))

    def type(self, s, col=WHITE, cps=34):
        self.lines.append(["", col])
        n = max(1, round(len(s) / cps * FPS))
        for k in range(1, n + 1):
            self.lines[-1][0] = s[:round(len(s) * k / n)]
            self._snap(1)

    def out(self, s="", col=GREY, hold=0.12):
        self.lines.append([s, col])
        self._snap(hold * FPS)

    def wait(self, secs, cursor=True):
        self._snap(secs * FPS, cursor)

    def blink(self, secs):
        """Cursor on/off at 2 Hz."""
        for i in range(int(secs * FPS)):
            self._snap(1, (i // (FPS // 4)) % 2 == 0)

    def clear(self):
        self.lines = []


def card(d, title, sub, y=None):
    """Centred brand block, used for the intro and the end card."""
    cy = y or H / 2
    icon(d, W / 2 - 90, cy - 250, 2.8)
    text(d, (W / 2, cy + 60), title, font(BOLD, 62), WHITE, -1.5, "ms")
    if sub:
        text(d, (W / 2, cy + 118), sub, font(MED, 28), GREY, 0, "ms")


def end_card(d):
    icon(d, W / 2 - 90, 232, 2.8)
    text(d, (W / 2, 556), "s&box MCP Server", font(BOLD, 58), WHITE, -1.5, "ms")
    text(d, (W / 2, 612), "+ AI Skill", font(BOLD, 36), BLUE, -1.0, "ms")
    text(d, (W / 2, 736), "github.com/fobiat/sbox-skill", font(MED, 32), GREEN, 0, "ms")
    text(d, (W / 2, 790), "fobiat.dev", font(MED, 30), LEG, 0, "ms")


def frame_console(lines, cursor, heading, label):
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 6], fill=GREEN)
    text(d, (PANEL_X, 160), heading, font(BOLD, 40), WHITE, -1.0)
    d.rounded_rectangle([PANEL_X, PANEL_Y, PANEL_X + PANEL_W, PANEL_Y + PANEL_H],
                        radius=16, fill=PANEL, outline=EDGE, width=1)
    cap(d, (PANEL_X + 34, PANEL_Y + 34), 6, GREEN)
    text(d, (PANEL_X + 54, PANEL_Y + 42), label, font(MED, 21), LEG)
    d.rectangle([PANEL_X + 24, PANEL_Y + 62, PANEL_X + PANEL_W - 24, PANEL_Y + 63], fill=EDGE)

    f = font(REG, 26)
    visible = lines[-14:]
    for i, (s, col) in enumerate(visible):
        y = PANEL_Y + 116 + i * LINE_H
        text(d, (PANEL_X + PAD, y), s, f, col)
        if cursor and i == len(visible) - 1:
            x = PANEL_X + PAD + width(f, s)
            d.rectangle([x + 3, y - 20, x + 15, y + 6], fill=GREEN)

    fl = font(MED, 22)
    text(d, (PANEL_X, H - 72), "github.com/fobiat/sbox-skill", fl, LEG)
    text(d, (PANEL_X + PANEL_W - width(fl, "fobiat.dev"), H - 72), "fobiat.dev", fl, GREEN)
    return im


def encode(frames, path, crf=20):
    p = subprocess.Popen(
        [FFMPEG, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
         "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
         "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", str(crf),
         "-preset", "medium", "-movflags", "+faststart", path],
        stdin=subprocess.PIPE)
    for im in frames:
        p.stdin.write(im.tobytes())
    p.stdin.close()
    if p.wait() != 0:
        raise SystemExit("ffmpeg failed")
    print(f"{path}  {len(frames)} frames  {len(frames)/FPS:.1f}s  {W}x{H}@{FPS}")


def still(fn, secs):
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 6], fill=GREEN)
    fn(d)
    return [im] * int(secs * FPS)


# ------------------------------------------------------------------ video 1
def video_ask():
    c = Cast()
    c.wait(0.4)
    c.type("$ claude mcp add --transport http sbox http://127.0.0.1:7269/mcp", WHITE, 46)
    c.out("Added HTTP MCP server sbox", GREEN, 0.7)
    c.out("", GREY, 0.2)
    c.type("> Does CameraComponent.AddHookAfterOpaque still work?", BLUE, 34)
    c.wait(0.7)
    c.out("", GREY, 0.15)
    c.out("  project_find_member  CameraComponent.AddHookAfterOpaque", LEG, 0.8)
    c.out("", GREY, 0.15)
    c.out("  [Obsolete] IDisposable AddHookAfterOpaque( ... )", WHITE, 0.5)
    c.out("  body: => null", RED, 1.4)
    c.out("", GREY, 0.2)
    c.type("It compiles. It returns null. It renders nothing.", GREY, 40)
    c.type("Use AddCommandList( CommandList, Stage, order ).", GREEN, 40)
    c.blink(2.2)

    frames = still(lambda d: card(d, "Ask, do not guess", "The MCP Server, 18 tools on the editor"), 2.0)
    frames += [frame_console(l, cur, "The MCP Server", "127.0.0.1:7269") for l, cur in c.frames]
    frames += still(end_card, 2.6)
    encode(frames, os.path.join(OUT, "video-1-mcp-server.mp4"))


# ------------------------------------------------------------------ video 2
def video_unity():
    c = Cast()
    c.wait(0.4)
    c.type("> Write me an s&box component that moves a cube forward.", BLUE, 40)
    c.out("", GREY, 0.3)
    c.out("Without the AI Skill", LEG, 0.5)
    for ln, col in [("public class Mover : MonoBehaviour   // does not exist", RED),
                    ("{", WHITE),
                    ("    void Update()                    // never called", RED),
                    ("    {", WHITE),
                    ("        transform.position +=        // no such property", RED),
                    ("          Vector3.forward            // it is .Forward", RED),
                    ("          * Time.deltaTime;          // it is Time.Delta", RED),
                    ("    }", WHITE),
                    ("}", WHITE)]:
        c.out(ln, col, 0.26)
    c.wait(1.6)
    c.clear()
    c.out("Same prompt, with the AI Skill installed", LEG, 0.7)
    for ln, col in [("public sealed class Mover : Component", WHITE),
                    ("{", WHITE),
                    ("    [Property] public float Speed { get; set; } = 200f;", GREEN),
                    ("", WHITE),
                    ("    protected override void OnUpdate()", GREEN),
                    ("    {", WHITE),
                    ("        if ( IsProxy ) return;", GREEN),
                    ("        WorldPosition += Vector3.Forward * Speed * Time.Delta;", GREEN),
                    ("    }", WHITE),
                    ("}", WHITE)]:
        c.out(ln, col, 0.26)
    c.wait(1.4)
    c.out("", GREY, 0.2)
    c.type("> Confirm every API you used exists in the references.", BLUE, 40)
    c.wait(0.6)
    c.out("", GREY, 0.15)
    c.out("  Component, OnUpdate, IsProxy, WorldPosition   01_SCENE.md", LEG, 0.45)
    c.out("  Vector3.Forward, Time.Delta                   15_API_CORE.md", LEG, 0.45)
    c.out("  [Property]                                    02_COMPONENTS.md", LEG, 0.7)
    c.out("  Nothing invented.", GREEN, 0.3)
    c.blink(2.2)

    frames = still(lambda d: card(d, "It is not Unity", "The AI Skill, 17 reference files"), 2.0)
    frames += [frame_console(l, cur, "The AI Skill", "skills/sbox/SKILL.md") for l, cur in c.frames]
    frames += still(end_card, 2.6)
    encode(frames, os.path.join(OUT, "video-2-agent-skill.mp4"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    video_ask()
    video_unity()
