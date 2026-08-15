#!/usr/bin/env python3
# =============================================================================
#  s&box Skill : brand drawing kit
#
#  Author   : fobiat (Kyle Tarff) <kyle@fobiat.dev>
#  Links    : https://fobiat.dev/   https://github.com/fobiat
#  Licence  : MIT, see LICENSE at the repository root.
#
#  Shared palette, type and primitives for render_listing.py and render_video.py.
#  The mark here is the same geometry as assets/brand/icon-bare.svg; edit both or
#  the raster and vector art drift.
#
#  Needs Pillow. Prefers JetBrains Mono, falls back to Consolas then DejaVu Sans
#  Mono, so a machine without the first still renders something legible.
# =============================================================================

import os
from PIL import Image, ImageDraw, ImageFont

BG, PANEL, EDGE = "#181818", "#202020", "#2E2E2E"
GREEN, BLUE, GREY, LEG, WHITE = "#2BB88E", "#00A8E8", "#9E9E9E", "#656565", "#EEF3F1"
RED, TILE, RULE = "#E5484D", "#202020", "#3E3E3E"

# Green is the AI Agent Skill, blue is the MCP Server, everywhere.
AI, MCP = GREEN, BLUE

_USER_FONTS = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Microsoft", "Windows", "Fonts")
_SYS_FONTS = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")

_FAMILIES = {
    "bold": ("JetBrainsMono-Bold.ttf", "consolab.ttf", "DejaVuSansMono-Bold.ttf"),
    "medium": ("JetBrainsMono-Medium.ttf", "consola.ttf", "DejaVuSansMono.ttf"),
    "regular": ("JetBrainsMono-Regular.ttf", "consola.ttf", "DejaVuSansMono.ttf"),
}


def _resolve(weight):
    for name in _FAMILIES[weight]:
        for d in (_USER_FONTS, _SYS_FONTS, "/usr/share/fonts/truetype/dejavu"):
            p = os.path.join(d, name)
            if os.path.exists(p):
                return p
    raise SystemExit(f"no {weight} monospace font found, install JetBrains Mono")


BOLD, MED, REG = _resolve("bold"), _resolve("medium"), _resolve("regular")

_cache = {}


def font(path, size):
    k = (path, int(size))
    if k not in _cache:
        _cache[k] = ImageFont.truetype(path, int(size))
    return _cache[k]


def cap(d, pt, r, fill):
    x, y = pt
    d.ellipse([x - r, y - r, x + r, y + r], fill=fill)


def seg(d, a, b, w, fill):
    d.line([a, b], fill=fill, width=int(round(w)))
    cap(d, a, w / 2, fill)
    cap(d, b, w / 2, fill)


def icon(d, ox, oy, s):
    """The 64x64 mark, translated to (ox,oy) and scaled by s."""
    P = lambda x, y: (ox + x * s, oy + y * s)
    d.rounded_rectangle([P(0, 0), P(64, 64)], radius=10.7 * s, fill=TILE)
    d.rounded_rectangle([P(0.5, 0.5), P(63.5, 63.5)], radius=10.2 * s,
                        outline=RULE, width=max(1, int(round(s))))
    for x1, y1, x2, y2 in ((23.34, 44.5, 13.81, 50), (40.66, 44.5, 50.19, 50)):
        seg(d, P(x1, y1), P(x2, y2), 3 * s, LEG)
    seg(d, P(32, 29.5), P(32, 21.5), 3.4 * s, BLUE)
    d.polygon([P(32, 12.5), P(37.5, 22), P(26.5, 22)], fill=BLUE)
    w, (cx, cy) = 2.6 * s, P(32, 39.5)
    rb = 8 * s + w / 2
    d.arc([cx - rb, cy - rb, cx + rb, cy + rb], 90, 360, fill=GREEN, width=int(round(w)))
    cap(d, P(32, 47.5), w / 2, GREEN)
    cap(d, P(40, 39.5), w / 2, GREEN)
    d.ellipse([P(29, 36.5), P(35, 42.5)], fill=GREEN)


def width(f, s, track=0.0):
    return sum(f.getlength(c) for c in s) + track * max(0, len(s) - 1)


def text(d, xy, s, f, fill, track=0.0, anchor="ls"):
    """Draw s from a baseline. anchor: ls (left) or ms (centred)."""
    x, y = xy
    if anchor == "ms":
        x -= width(f, s, track) / 2
    asc = f.getmetrics()[0]
    if track == 0:
        d.text((x, y - asc), s, font=f, fill=fill)
        return x + f.getlength(s)
    for c in s:
        d.text((x, y - asc), c, font=f, fill=fill)
        x += f.getlength(c) + track
    return x


def spans(d, xy, parts, f):
    """parts: list of (string, colour), drawn inline from a baseline."""
    x, y = xy
    for s, col in parts:
        x = text(d, (x, y), s, f, col)
    return x


def wrap(s, f, maxw):
    out, line = [], ""
    for word in s.split():
        trial = (line + " " + word).strip()
        if f.getlength(trial) > maxw and line:
            out.append(line)
            line = word
        else:
            line = trial
    if line:
        out.append(line)
    return out


def canvas(w, h, ss=1, bar=True, barh=8):
    im = Image.new("RGBA", (w * ss, h * ss), BG)
    d = ImageDraw.Draw(im)
    if bar:
        d.rectangle([0, 0, w * ss, barh * ss], fill=GREEN)
    return im, d


def footer(d, w, h, ss=1, size=23, pad=72, rule=True):
    """Both links, on every screen."""
    f = font(MED, size * ss)
    y = (h - pad) * ss
    if rule:
        d.rectangle([pad * ss, y - 44 * ss, (w - pad) * ss, y - 44 * ss + max(1, ss)], fill=EDGE)
    text(d, (pad * ss, y), "github.com/fobiat/sbox-skill", f, LEG)
    text(d, ((w - pad) * ss - width(f, "fobiat.dev"), y), "fobiat.dev", f, GREEN)


def save(im, w, h, path):
    if im.size != (w, h):
        im = im.resize((w, h), Image.LANCZOS)
    im.save(path)
    print(f"{path}  {w}x{h}")
