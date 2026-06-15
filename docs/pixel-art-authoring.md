# Drawing Sprites for ItemChecklist

Every UI sprite in ItemChecklist is original pixel art, authored in **Pixaki**
in one master file and packed into a single sheet by a deterministic generator.
This is the forward workflow — **draw → generate → wire → build**. There is no
game-sprite bridging and no Item Browser placeholder art; the one-time migration
that established this is history (see `docs/iteration-history.md`, Iter-12).

## 1. The pipeline at a glance

```
sources/Item checklist sprites.pixaki     (the master — you edit this)
            │
            ▼   utils/pixaki_to_sheet.py   (deterministic generator)
            │
unity/ItemChecklist/Art/UI/ui_checklist.png + .png.meta   (the sheet — committed)
            │
            ▼   {fileID: <internalID>, guid: <sheet guid>, type: 3}
            │
unity/ItemChecklist/Prefabs/*.prefab       (reference sub-sprites)
```

All three inputs are **versioned** (`.pixaki`, the generator, the sheet+`.meta`),
so any fresh checkout or worktree has everything needed to redraw. The generator
is deterministic and idempotent: the same `.pixaki` plus the same config tables
always produce a byte-identical sheet and `.meta`.

## 2. Where things live

| Path | Role | Versioned |
|---|---|---|
| `sources/Item checklist sprites.pixaki` | the art master — **edit this** | yes |
| `utils/pixaki_to_sheet.py` | the generator (+ `test_pixaki_to_sheet.py`) | yes (in `core_keeper/utils`) |
| `unity/ItemChecklist/Art/UI/ui_checklist.png` | generated sheet (GUID `b00ef63bae8086014b9ce2e32387bcb8`, 26 sprites) | yes |
| `unity/ItemChecklist/Art/UI/ui_checklist.png.meta` | slice data — the contract prefabs depend on | yes |

`Art/` no longer contains a `Bridge/` folder or any game-sprite references — the
sheet is the single visual source. The current sheet holds 26 sprites — see
**§12** for the full catalog with sizes.

## 3. Core Keeper pixel conventions

CK renders UI at **16 pixels per unit** (`spritePixelsToUnits: 16`) with **point
filtering** (`filterMode: 0`) and **no mipmaps**. When drawing in Pixaki:

1. **Canvas at the exact target pixel size** — never draw large and downscale.
2. **No anti-aliasing / nearest-neighbour only** — soft edges become mud pixels.
3. **Transparent background; opaque pixels genuinely opaque** — a fully
   transparent "frame" renders invisible (the Iter-6 placeholder bug).
4. **Export is automatic** — Pixaki stores each layer's drawing inside the
   `.pixaki`; the generator reads them directly, so there is no manual PNG export.
5. **Match CK's visual *style*, not its art** — limited palette, 1px outlines,
   the inventory/crafting chrome look. (A local reference library can be
   extracted from the game with the helper preserved in git history, Iter-12;
   keep it out of git — add an `Art/extracted/` ignore — and never ship it:
   Pugstorm's art, personal-use EULA.)

## 4. How the generator reads your `.pixaki`

A `.pixaki` is a zip with `document.json` (layer tree) + per-layer drawing PNGs.
The generator turns layers into sprites by these rules:

- **One visible, named, drawing-bearing layer = one candidate sprite.** The
  layer's name becomes the sprite name.
- **Hidden layers are skipped**, and so is every layer inside an excluded
  top-level group. The excluded groups (`EXCLUDE_TOP` in the generator) are
  composition / preview / parking groups, not real sprites:
  `Outsorted`, `Background`, `Search Field Complete`, `Dropdown Complete`.
  Put work-in-progress or reference layers under one of these (or hide them).
- **Pixel-dedup:** layers with byte-identical pixels collapse to a single
  sub-sprite. Reuse a motif freely across the mockup — it is packed once.
- **Name disambiguation:** if the same base name appears at two different
  sizes, the generator appends ` WxH` (e.g. `Icon Sort Asc 8x8` /
  `Icon Sort Asc 6x6`). A unique base name stays bare.
- **`internalID` = a signed 32-bit int derived from `SHA1(final sprite name)`**
  (first 4 bytes, little-endian). This is the `fileID` a prefab references. It
  is **stable as long as the name is stable** — which is what makes regeneration
  safe and is also why renaming needs care (§7).

## 5. 9-slice borders (driven by config tables, not the Sprite Editor)

The generator writes each sub-sprite's `border` from two tables — you do **not**
slice borders by hand in Unity:

- **`SLICED`** — base names that are 9-sliced chrome. Membership gives the
  default border `{1,1,1,1}` (left, bottom, right, top, in px). Add a new chrome
  sprite's base name here.
- **`BORDER_OVERRIDE[(base, w, h)]`** — an explicit border that overrides the
  default for a specific sprite + size, e.g. `("Window",16,16): (4,4,4,4)` or
  `("Entry Selected",8,8): (3,3,3,3)`.

Everything not in `SLICED` (icons, carets, glyphs) gets `{0,0,0,0}` (Simple).

Drawing rules for a 9-slice sprite — `border {left,bottom,right,top}`:

- **Corners** (border-sized) never stretch → keep corner detail ≤ border px.
- **Edges** stretch along one axis → keep them constant/tileable along it.
- **Centre** stretches both ways → keep it flat (solid or transparent).

The generator packs sprites into a 128px-wide sheet with a **2px gutter**, so
9-slice tiling never samples a neighbour's pixels — you do not add padding for
bleed yourself.

## 6. Padding sub-cell art (`PAD` table)

Sprites live on a notional pixel grid (mostly 8×8 and 16×16 cells). A drawing
smaller than its cell is padded up to the cell so it 9-slices / aligns correctly.
`PAD[disambiguated name] = (target_w, target_h, anchor)`, where `anchor` is:

- `"bottom"` — centred on x, flush to the bottom (e.g. the 1px option separator
  `Entry Background 8x1` → `8×8`),
- a `(left, top)` tuple — explicit offset (e.g. the `Unknown Object` `?` glyph
  `6×11` → `16×16` at `(5, 3)`: horizontally centred, top-biased to match item
  icons),
- omitted / top-left otherwise.

Keyed by the **disambiguated** name (before any rename).

## 7. Renaming a sprite (`RENAME` table)

Because `internalID = hash(name)`, **renaming a sprite changes its `internalID`
and breaks every prefab reference to it.** To rename safely:

1. Add the rename to `RENAME` (keyed by the disambiguated name; applied after
   naming, so size-disambiguation stays intact), e.g.
   `"Panel": "Option Panel"`.
2. Regenerate — the new `internalID` follows the **final** name.
3. Update each prefab `m_Sprite` / `caret*` / `*Sprite` `fileID` to the new
   `internalID`.

Always pass **`--guid`** so the *sheet* GUID stays constant — only the changed
sub-sprite `internalID`s move, not the whole sheet reference.

## 8. Workflow: add or change a sprite

1. **Draw / edit** the layer in `sources/Item checklist sprites.pixaki` — named,
   visible, and *not* inside an `EXCLUDE_TOP` group.
2. If the drawing is **sub-cell**, add a `PAD` entry (§6).
3. If it is **9-slice chrome**, add its base name to `SLICED`, and a
   `BORDER_OVERRIDE` entry if the border isn't `{1,1,1,1}` (§5).
4. If you **renamed** it, add a `RENAME` entry and plan the prefab rewire (§7).
5. **Regenerate** the sheet (§9).
6. Read the new/changed `internalID` from the generator's stdout (or
   `--mapping-out`), then wire the prefab ref:
   `m_Sprite: {fileID: <internalID>, guid: b00ef63bae8086014b9ce2e32387bcb8, type: 3}`.
7. **Build** (`../utils/build.sh`) and verify in-game (§10).
8. If you touched a config table, update `utils/test_pixaki_to_sheet.py`.

## 9. Regenerate command

Run from the mod repo root. **Copy the current `.meta` to a temp template
first** — the generator reuses the template's texture-import header/tail
verbatim, and if the template path equals the output `.meta` it self-truncates
before reading (the script opens the output `.meta` for writing first):

```bash
cp unity/ItemChecklist/Art/UI/ui_checklist.png.meta /tmp/ui_checklist.template.meta

python3 ../utils/pixaki_to_sheet.py \
    "sources/Item checklist sprites.pixaki" \
    unity/ItemChecklist/Art/UI/ui_checklist.png \
    --meta-template /tmp/ui_checklist.template.meta \
    --guid b00ef63bae8086014b9ce2e32387bcb8 \
    --mapping-out /tmp/ui_checklist.map.json
```

The texture-import settings (PPU 16, point filter, no compression,
`textureType: Sprite`) are inherited from the template header — the generator
only manages the `guid`, the `spriteSheet.sprites[]` list, and `nameFileIdTable`.
`--mapping-out` writes a `{guid, name_to_internal_id}` JSON for wiring prefabs.

## 10. Verify

- **Generator stdout** lists every sprite and its `internalID` — the live source
  of truth for what the sheet contains and what prefabs should reference.
- **Build + load:** `../utils/build.sh`, then grep the runtime log —
  `grep -iE "error CS|Build complete|Install complete|CompileFailed" Player.log`.
  A clean Editor build can still `CompileFailed` in the runtime sandbox.
- **In-game visual check.** The calibration loop (margins, slicing, alignment)
  runs **inline in the main session**, not via subagents — it needs the live
  CrossOver window, and a batchmode build locks the project.

## 11. `Art/` layout & versioning

There is **no `.gitignore` entry for anything under `Art/`** — the whole folder
is tracked. Today it contains only `Art/UI/`:

- `Art/UI/ui_checklist.png` + `.png.meta` — the generated sheet and its slice
  contract; both committed. Editing only the `.pixaki` and regenerating keeps
  these in sync.
- `Art/UI/mask_sprite.png` + `.png.meta` — the 1×1 clip-mask sprite (tracked).
- `Art/Bridge/` (the old Item Browser placeholders) and `Art/extracted/` (the
  game-sprite reference library) **no longer exist** — `Bridge/` was deleted in
  Iter-12, and neither has a `.gitignore` line. If you re-create a reference
  library, add an explicit ignore (e.g. an `Art/extracted/` line) so Pugstorm's
  art is never committed or shipped.

Because the sheet `.meta` is versioned, prefab references resolve from tracked
data — no gitignored dependency, no cross-worktree copy hack for the real art.

## 12. Current sprite catalog

The 26 sprites in `ui_checklist.png` as of this writing — name, native pixel
size, 9-slice border `{left, bottom, right, top}` (or *simple* = no border), and
the `internalID` prefabs reference. **Generated data, not hand-maintained:** it
mirrors `Art/UI/ui_checklist.png.meta`; the generator prints the same list (name
+ `internalID`) on every run. Refresh it after adding/renaming a sprite.

Sizes follow CK's grid: **8×8** chrome tiles (thin `{1,1,1,1}` 9-slice room),
**4×8** scrollbar parts, **16×16** for the window frame and the item-sized
Unknown Object, and a few small **6×6 / 6×4 / 2×8 / 2×2** glyphs and the caret.

| Sprite | Size | Border | internalID |
|---|---|---|---|
| `Button Background` | 8×8 | `{1,1,1,1}` | -1477534581 |
| `Button Close` | 6×4 | simple | -1597693367 |
| `Button Open` | 6×4 | simple | 1907094303 |
| `Caret` | 2×8 | `{0,1,0,1}` | -278155363 |
| `Checkbox empty` | 6×6 | `{1,1,1,1}` | -1417026918 |
| `Checkbox filled slash` | 2×2 | simple | -49361062 |
| `Clear` | 6×6 | simple | -1767661967 |
| `Entry Background 8x8` | 8×8 | `{1,1,1,1}` | -1059655147 |
| `Entry Selected` | 8×8 | `{3,3,3,3}` | -2004837774 |
| `Entry Toggled` | 8×8 | `{1,1,1,1}` | 853627131 |
| `Field Background` | 8×8 | `{1,1,1,1}` | -1386956172 |
| `Icon Sort` | 8×8 | simple | 1827009517 |
| `Icon Sort Asc 6x6` | 6×6 | simple | 838748097 |
| `Icon Sort Asc 8x8` | 8×8 | simple | 1242656705 |
| `Icon Sort Desc 6x6` | 6×6 | simple | -944694907 |
| `Icon Sort Desc 8x8` | 8×8 | simple | -1910095811 |
| `Icon filter` | 8×8 | simple | -41939125 |
| `Option Background 8x8` | 8×8 | `{1,1,1,1}` | -761834842 |
| `Option Panel` | 8×8 | `{1,1,1,1}` | -637329406 |
| `Rarity Border` | 8×8 | `{1,1,1,1}` | 742996088 |
| `Scrollbar Background` | 4×8 | `{1,1,1,1}` | 698883113 |
| `Scrollbar Handler` | 4×8 | `{1,1,1,1}` | 1522153644 |
| `Scrollbar Highlight` | 4×8 | `{1,1,1,1}` | 383107104 |
| `Scrollbar Selector` | 4×8 | `{1,3,1,3}` | 1612575814 |
| `Unknown Object` | 16×16 | simple | 60782178 |
| `Window` | 16×16 | `{4,4,4,4}` | -1497309375 |

The item icon itself and the Ancient Coin glyph are **not** in this sheet — they
are the game's own object icons, assigned at runtime in `ItemRow` (the icon slot + 
coin column), so there is nothing to draw for them.

### Regenerate this table

```bash
python3 - <<'PY'
import re
meta = open("unity/ItemChecklist/Art/UI/ui_checklist.png.meta").read()
rows = re.findall(r'- serializedVersion: 2\n\s+name: (.+?)\n.*?width: (\d+)\n'
                  r'\s+height: (\d+).*?border: \{x: (\d+), y: (\d+), z: (\d+), '
                  r'w: (\d+)\}.*?internalID: (-?\d+)', meta, re.S)
for name, w, h, l, b, r, t, iid in sorted(rows):
    bd = f"{{{l},{b},{r},{t}}}" if any(map(int, (l, b, r, t))) else "simple"
    print(f"| `{name.strip()}` | {w}x{h} | {bd} | {iid} |")
PY
```

