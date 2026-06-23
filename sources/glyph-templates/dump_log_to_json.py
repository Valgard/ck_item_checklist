#!/usr/bin/env python3
"""Parse the iter-25 RECTS + CODEPOINTS log dump into glyph_metrics.json.

Usage: dump_log_to_json.py [Player.log] [out_dir]

PREREQUISITE: a diagnostic build that logs the font tables. The Iter-25 diagnostic
block (a temporary RECTS+CODEPOINTS dumper in ItemCatalogWorldLoadHook.BakeWhenReady)
was removed when the fix shipped, so to regenerate glyph_metrics.json you must
re-add a dump loop over Manager.text's faces, e.g.:

    foreach face: for each glyphData[i]: log "iter-25 RECTS <face> tex=.. atlas=WxH
      ppu=.. chardims=X,Y count=N off=I : i:x,y,w,h .."   (chunked, 50/line)
    foreach face: for each codePoints kv: log "iter-25 CODEPOINTS <face> off=I :
      <charcode>=<index> .."  (chunked)

then build, launch CK, load a world, and run this script on the resulting Player.log.

RECTS lines:      "iter-25 RECTS <face> tex=.. atlas=WxH ppu=.. chardims=X,Y count=N off=I : i:x,y,w,h .."
CODEPOINTS lines: "iter-25 CODEPOINTS <face> off=I : <charcode>=<index> .."

rect = glyphData[i].rect = [x,y,w,h] in atlas px (Unity origin bottom-left).
charDims = [x,y] nominal cell (font-wide). Each glyph gets its char(s) from codePoints.
"""
import re, sys, json, os

HERE = os.path.dirname(os.path.abspath(__file__))
LOG = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(
    "~/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/"
    "AppData/LocalLow/Pugstorm/Core Keeper/Player.log")
OUT = sys.argv[2] if len(sys.argv) > 2 else HERE

head = re.compile(r"iter-25 RECTS (\S+) tex=(\S+) atlas=(\d+)x(\d+) ppu=([\d.]+) chardims=(\d+),(\d+) count=(\d+) off=(\d+) :(.*)")
rect = re.compile(r"(\d+):(-?\d+),(-?\d+),(-?\d+),(-?\d+)")
cphead = re.compile(r"iter-25 CODEPOINTS (\S+) off=(\d+) :(.*)")
cp = re.compile(r"(\d+)=(\d+)")

fonts = {}
for line in open(LOG, encoding="utf-8", errors="replace"):
    m = head.search(line)
    if m:
        face, tex, w, h, ppu, cdx, cdy, count, off, body = m.groups()
        f = fonts.setdefault(face, dict(tex=tex, atlas=[int(w), int(h)], pixelsPerUnit=float(ppu),
                                        charDims=[int(cdx), int(cdy)], count=int(count), _rects={}, _cp={}))
        for rm in rect.finditer(body):
            i, x, y, rw, rh = (int(v) for v in rm.groups())
            f["_rects"][i] = [x, y, rw, rh]
        continue
    cm = cphead.search(line)
    if cm:
        face, off, body = cm.groups()
        f = fonts.setdefault(face, dict(_rects={}, _cp={}))
        for c in cp.finditer(body):
            code, idx = int(c.group(1)), int(c.group(2))
            f["_cp"][code] = idx

if not fonts:
    print("No iter-25 lines found in", LOG); sys.exit(1)

for face, f in fonts.items():
    idx_to_chars = {}
    for code, idx in sorted(f["_cp"].items()):
        idx_to_chars.setdefault(idx, []).append(code)
    glyphs = []
    for i in sorted(f["_rects"]):
        chars = idx_to_chars.get(i, [])
        glyphs.append({"i": i, "rect": f["_rects"][i], "chars": chars,
                       "text": "".join(chr(c) for c in chars)})
    f["glyphs"] = glyphs
    f["codePoints"] = {str(code): idx for code, idx in sorted(f["_cp"].items())}
    del f["_rects"]; del f["_cp"]

doc = {"_note": "CK PugFont metrics. rect=[x,y,w,h] in atlas px (Unity origin bottom-left). "
                "charDims=[x,y] nominal cell (font-wide). glyphs[].chars = char codes mapping to that "
                "glyphData index; codePoints = char-code -> index map.",
       "fonts": fonts}
path = os.path.join(OUT, "glyph_metrics.json")
json.dump(doc, open(path, "w"), ensure_ascii=False, indent=1)
print("wrote", path)
for face, f in sorted(fonts.items()):
    print(f"  {face}: atlas={f.get('atlas')} charDims={f.get('charDims')} glyphs={len(f.get('glyphs', []))} codePoints={len(f.get('codePoints', {}))}")
