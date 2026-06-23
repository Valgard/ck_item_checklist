#!/usr/bin/env python3
"""Render per-font glyph-rect debug overlays from glyph_metrics.json.

Run dump_log_to_json.py first to produce glyph_metrics.json.

PNG has no layers, so each layer is written as its own transparent PNG (atlas-sized,
stack them in your editor) plus a flattened preview:

  <face>_L1_bg.png        #2CE8F4 (cyan), opaque        — bottom
  <face>_L2_charDims.png  #3CF42C/#24921A green checkerboard of the nominal cell per glyph
  <face>_L3_rects.png     #E53BDF (magenta) glyphData[i].rect, transparent
  <face>_L4_atlas.png     the atlas PNG (white glyphs), transparent — top
  <face>_preview_x6.png   all four flattened, 6x nearest-upscaled (quick look)

Stack order bottom->top: L1 -> L2 -> L3 -> L4. Empty glyphs (space, width/height<=0)
are skipped in both the charDims and rect layers.
Unity texture coords have origin bottom-left -> flip Y for PIL (top-left).
Atlas PNGs are read from this dir as <tex>_raw.png.
"""
import json, sys, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
JSON = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "glyph_metrics.json")
ATLAS_DIR = HERE
OUT_DIR = os.path.join(HERE, "grids")
os.makedirs(OUT_DIR, exist_ok=True)

BG = (0x2C, 0xE8, 0xF4, 255)
GREEN_L = (0x3C, 0xF4, 0x2C, 255)   # light green
GREEN_D = (0x24, 0x92, 0x1A, 255)   # dark green (checkerboard)
RECT = (0xE5, 0x3B, 0xDF, 255)
CLEAR = (0, 0, 0, 0)

doc = json.load(open(JSON, encoding="utf-8"))
for face, f in sorted(doc["fonts"].items()):
    W, H = f["atlas"]
    cdx, cdy = f["charDims"]

    bg = Image.new("RGBA", (W, H), BG)

    cd = Image.new("RGBA", (W, H), CLEAR)
    dcd = ImageDraw.Draw(cd)
    for g in f["glyphs"]:
        x, y, rw, rh = g["rect"]
        if rw <= 0 or rh <= 0:  # skip empty glyphs (space) like the rect layer
            continue
        top = H - y - cdy
        col = x // cdx if cdx else 0
        row = top // cdy if cdy else 0
        fill = GREEN_L if (col + row) % 2 == 0 else GREEN_D
        dcd.rectangle([x, top, x + cdx - 1, top + cdy - 1], fill=fill)

    rects = Image.new("RGBA", (W, H), CLEAR)
    drc = ImageDraw.Draw(rects)
    for g in f["glyphs"]:
        x, y, rw, rh = g["rect"]
        if rw <= 0 or rh <= 0:
            continue
        top = H - y - rh
        drc.rectangle([x, top, x + rw - 1, top + rh - 1], fill=RECT)

    atlas_path = os.path.join(ATLAS_DIR, f"{f['tex']}_raw.png")
    note = ""
    if os.path.exists(atlas_path):
        atlas = Image.open(atlas_path).convert("RGBA")
        if atlas.size != (W, H):
            note = f" (atlas {atlas.size} != {W}x{H}; blank atlas layer)"
            atlas = Image.new("RGBA", (W, H), CLEAR)
    else:
        note = f" (atlas missing: {atlas_path}; blank atlas layer)"
        atlas = Image.new("RGBA", (W, H), CLEAR)

    bg.save(os.path.join(OUT_DIR, f"{face}_L1_bg.png"))
    cd.save(os.path.join(OUT_DIR, f"{face}_L2_charDims.png"))
    rects.save(os.path.join(OUT_DIR, f"{face}_L3_rects.png"))
    atlas.save(os.path.join(OUT_DIR, f"{face}_L4_atlas.png"))

    flat = bg.copy()
    flat.alpha_composite(cd)
    flat.alpha_composite(rects)
    flat.alpha_composite(atlas)
    flat.resize((W * 6, H * 6), Image.NEAREST).save(os.path.join(OUT_DIR, f"{face}_preview_x6.png"))

    print(f"{face}: {len(f['glyphs'])} glyphs, {W}x{H}, charDims={f['charDims']} -> 4 layer PNGs{note}")
print("written to", OUT_DIR)
