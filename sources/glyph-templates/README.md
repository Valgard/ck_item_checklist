# CK Font Glyph Templates & Tooling (Iter-25)

Reference material + reproducibility tooling for the Iter-25 thinTiny accented-glyph
fix. The **committed source of truth** is elsewhere — `sources/thinTiny_full.pixaki`
(the hand-drawn master) + `unity/ItemChecklist/Art/thinTiny_glyphs.png` (the generated
sheet) + the `Glyphs[,]` array in `unity/ItemChecklist/ThinTinyGlyphPatch.cs`. This
directory only re-derives them.

**Tracked vs. gitignored:** the `*.py` scripts are tracked (our tools). The extracted
CK atlases (`rrs*_raw.png`, `*_view.png`, …), `glyph_metrics.json`, and `grids/` are
**gitignored** — they are Pugstorm game assets / derived data, reference only.

## Atlas → FontFace map (CK 1.2.1.4)

| Face | Atlas (`texName`) | Size | Glyphs | has `ö`/`Ä`/`ß`? |
|---|---|---|:---:|:---:|
| **`thinTiny`** | `rrs5` | 256×40 | 114 | no (digits-only face) |
| `thinSmall` | `rrsthin8` | 257×144 | 331 | yes |
| `thinMedium` | `rrs10thin` | 513×192 | 331 | yes |
| `boldSmall` | `rrs8` | 257×144 | 331 | yes |
| `boldMedium` | `rrs10` | 513×192 | 331 | yes |
| `boldLarge` | `rrs12b` | 514×192 | 212 | yes |
| `boldHuge` | `rrs18` | 641×432 | 341 | yes |
| `buttonFont` | `buttonfont_new` | 339×161 | 90 | — (controller glyphs) |

## Reproduction pipeline

```
(diagnostic build dumps font tables)  →  Player.log
   dump_log_to_json.py   Player.log         →  glyph_metrics.json   (Pugstorm data, gitignored)
   pixaki_to_glyphs.py   thinTiny_full.pixaki + glyph_metrics.json
                                            →  Art/thinTiny_glyphs.png + C# Glyphs[,] array (stdout)
   build_glyph_grids.py  glyph_metrics.json →  grids/  (debug overlays: bg + charDims + rects + atlas)
```

**`glyph_metrics.json` requires a re-dump.** It holds CK's per-face glyph rects +
codePoints, which can only be read from a runtime diagnostic dump (CK's `PugFont`
MonoBehaviours have no TypeTree, so UnityPy can't read them statically). The Iter-25
diagnostic block was removed when the fix shipped — see `dump_log_to_json.py`'s header
for how to re-add it if the glyph set ever needs regenerating.

## Pixaki master layers (`thinTiny_full.pixaki`)

4 layers + a thinSmall reference: **Atlas** = the glyph sprites, **Rects** = the
per-glyph advance width (full charDims height 10, glyph-specific width), **charDims** =
the nominal-cell checkerboard, **Background** = cyan. Extraction maps each codepoint
to its thinSmall cell (col = x//8, row from top). See `docs/research/pixaki-format.md`.
