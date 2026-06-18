# ItemChecklist Gotchas

Non-obvious traps that have caused real bugs in this codebase. Read these
before changing UI, prefabs, or layer assignments.

## UI / Scroll

### SetScrollValue(0f) = BOTTOM, not top

`UIScrollWindow.SetScrollValue(0f)` scrolls to the **bottom** of the list,
not the top. Iter-3 passed `0f` and rows overlapped the title element (content
shifted ~20 units up).

**Correct:** `scrollWindow.SetScrollValue(1f)` or `scrollWindow.ResetScroll()`
for top-of-list. Never pass `0f` unless you specifically want the bottom.

See `docs/architecture.md § SetScrollValue Semantics` for the lerp math
explanation.

### Window-open guards must check `root.activeSelf`, not `gameObject.activeSelf`

In a CoreLib `IModUI` window, the `Window`/`UIelement` component sits on the
**parent** GameObject, which CoreLib keeps permanently active. Visibility is
carried by the `root` **child** — `HideUI` toggles `root.SetActive(false)`,
not the parent.

Therefore any guard that means "only do this while the window is open" must
check `root.activeSelf`, **not** `gameObject.activeSelf`:

```csharp
if (!root.activeSelf) return;   // correct — root carries visibility
// if (!gameObject.activeSelf)  // WRONG — parent is always active, never gates
```

Gating on `gameObject.activeSelf` silently never fires (the parent is
always-true), so the guarded code runs even while the window is hidden. This
bit a per-frame recycle guard in Iter-3.8.

### UiController / VirtualScrollList are deleted — do not recreate

`VirtualScrollList.cs`, `UiController.cs`, and `ItemRowView.cs` were
permanently deleted in Iter-2. They were replaced by CK's native
`UIScrollWindow` + `IModUI` pattern + `ItemRow.cs`.

Do not recreate — the old uGUI-based recycler is structurally incompatible
with CK's `Physics.Raycast`-based `UIMouse`. Any Canvas-derived component is
invisible to CK's input system. See § uGUI structurally fails in CK below.

### uGUI (Canvas/Image) structurally fails in CK

CK's `UIMouse` does a `Physics.Raycast` in the UI layer. `Canvas`/`Image`
components have no `Collider` and are invisible to that raycast: input passes
straight through and the cursor stays under the window regardless of Canvas
Render Mode. This is not a configuration problem — it is a structural
incompatibility. All 10 surveyed CK UI mods use `SpriteRenderer` + Layer 5 +
`UIelement`; **0** use uGUI. Do not attempt Canvas-based UI.

## Mod Loading

### Opening the in-game Mods menu wipes the fake-ID dev install

If the in-game **Mods menu** is opened while a fake-ID local dev build is
installed, the mod.io client syncs subscriptions against the real catalog,
finds no entry for the fake ID, and **deletes the local files + ZIP**. The
game must then be restarted without the mod.

**Safe actions:** game start, world load, gameplay — none of these trigger
the sync. **Only the Mods menu** triggers it.

**Recovery:** re-run the install script:
```bash
source .envrc && ../utils/build.sh
```
This rebuilds and re-installs all three fake-ID locations.

**Two-step scenario** (subscribing to a real mod on mod.io): the new
subscription lands only when the Mods menu is opened — the same sync that
applies the subscription wipes every fake-ID mod. Plan for it as a two-step:
open the menu, let the change land, then rebuild each fake-ID mod.

See the parent `CLAUDE.md § Fake-ID dev install` for the full fake-ID
mechanism.

## SpriteMask Clipping

Clipping in CK `SpriteRenderer` UI uses a `SpriteMask` with a **Custom
Sorting-Layer Range**. This section gives the working recipe (Iter-3.5c) first,
then the aborted Iter-3.5b lessons that led to it.

### The working recipe (Iter-3.5c)

- **Sorting layer:** `"GUI"` (uniqueID `1241602095` — verify against
  `CoreKeeperModSDK/ProjectSettings/TagManager.asset` before hardcoding).
- **Custom Range:** `FrontOrder = 55`, `BackOrder = 40`. All row renderers
  must have their `sortingOrder` within this range.
- **IB reference orders:** Background=45, Icon=48, Label=49, Placeholder=49,
  Checkmark=50. Row renderers sit between Background and the mask front-order.
- **`mask_sprite.png`:** 1×1 white PNG. **Must** set `spritePixelsToUnits: 1`
  in the `.meta` (NOT the SDK default of 16 — at PPU=16 the sprite is 0.0625
  units and Transform scale produces a tiny mask instead of full window
  coverage).
- **Mask geometry:** place the SpriteMask as a child of RowsContainer. If
  RowsContainer has a Y offset (e.g. `localPosition.y = 1.5`), the mask needs
  the inverse Y offset (`-1.5`) to stay centered on the background.
- **PugText clipping:** `PugText` has no public `SetSortingLayer` setter. Write
  `style.sortingLayer = 1241602095` directly (`PugText.style` is a public
  field). Prefab YAML keys for PugText are `sortingLayer:` / `orderInLayer:`
  (NOT `m_SortingLayer` / `m_SortingOrder` — those are SpriteRenderer YAML keys).
- **Layer pre-condition:** a mask with Custom Range `40..55` only clips
  renderers already in the `"GUI"` sorting layer. If any SpriteRenderer is
  still in `"Default"`, the mask clips nothing for that renderer. Prefab-edit
  ALL renderers to `"GUI"` before installing the mask.

### Aborted Iter-3.5b lessons

The Iter-3.5b iteration was aborted after pre-flight discovered the
following structural blockers. Documenting them prevents re-attempts.

### "UI" sorting layer does not exist — the named layer is "GUI"

There is **no** named sorting layer `"UI"` in
`CoreKeeperModSDK/ProjectSettings/TagManager.asset`. The sorting layer used
by CK UI elements is named `"GUI"` (uniqueID `1241602095`).

Layer 5 in Unity's tag-layer system is called `"UI"`, but that is a
**tag-layer** (used for `Physics.Raycast` filtering), not a sorting layer.
`"GUI"` (sorting layer) and Layer 5 (tag-layer) are entirely separate
concepts.

Iter-3.5b was designed assuming a `"UI"` sorting layer and was aborted when
Task 1+2 pre-flight revealed the layer does not exist. Always verify sorting
layer uniqueIDs against `TagManager.asset` before hardcoding them into prefab
YAML.

### Pure-runtime SpriteMask cannot cover a mixed Default/GUI renderer stack

A `SpriteMask` with a Custom Sorting-Layer Range of `40..55` only clips
renderers that are **already in that sorting layer**. If any `SpriteRenderer`
is in `"Default"` (order 0) and `PugText`s resolve to `"GUI"` (sentinel
`int.MinValue`), a mask set to GUI range `40..55` clips nothing in `"Default"`.

**Solution (Iter-3.5c approach):** prefab-edit ALL renderers — both
`SpriteRenderer` components and `PugText.style.sortingLayer` fields — to
layer `"GUI"` with `orderInLayer` values within `40..55` **before** installing
the mask. A pure-runtime approach cannot bypass this requirement.

PugText YAML grep pattern: `sortingLayer:` / `orderInLayer:` (NOT
`m_SortingLayer` / `m_SortingOrder` — those are SpriteRenderer YAML keys).

### mask_sprite.png must use spritePixelsToUnits: 1, not 16

The `SpriteMask` sprite (`mask_sprite.png`, a 1×1 white PNG) **must** have
`spritePixelsToUnits: 1` in its `.meta` file. With the SDK default `PPU=16`,
the sprite geometry is `0.0625` units. Applying a Transform scale of `(11, 6)`
produces a `0.69 × 0.375` unit mask instead of the intended `11 × 6` window
coverage — the mask is essentially invisible.

Always set `spritePixelsToUnits: 1` for any mask sprite that needs to cover
a large screen area in CK's `1/16`-unit grid.

### Texture2D + Sprite.Create runtime mask approach was aborted

The Iter-3.5b plan was to generate the mask sprite at runtime via
`new Texture2D(1, 1)` + `Sprite.Create`. This approach was aborted because
the render-domain problem (mixed `"Default"` / `"GUI"` layers) cannot be
solved without prefab edits regardless of how the sprite is created.

Do not revisit this approach without first ensuring all renderers are
consolidated into the same sorting layer. The sprite-creation mechanism is
not the problem — the layer separation is.

### PugText.style has no SetSortingLayer setter — direct field write required

`PugText` has a public `SetOrderInLayer(int)` method but **no public setter
for `sortingLayer`**. To set the sorting layer on a `PugText` at runtime,
write `style.sortingLayer` directly — `PugText.style` is a public field:

```csharp
pugText.style.sortingLayer = 1241602095;  // "GUI" uniqueID
pugText.style.orderInLayer = 48;
```

In prefab YAML, use `sortingLayer:` / `orderInLayer:` keys. Do not use
`m_SortingLayer` / `m_SortingOrder` — those are `SpriteRenderer` YAML keys
and are silently ignored on a `PugText` component.

### PugText tint: set colour after Render(), and keepColorOnStart:true (Iter-6)

`PugText.color`'s setter calls `SetTempColor`, which writes the **glyph
SpriteRenderers** that `Render(text)` (re)builds. Two consequences for tinting
a row label:

1. **Set the colour after `Render()`**, not before — a colour applied before
   `Render()` rebuilds the glyphs is discarded (there are no glyphs yet, or they
   get overwritten).
2. **Use `label.SetTempColor(c, keepColorOnStart: true)`, not `label.color =
   c`.** A prefab `PugText` with `renderOnStart: 1` re-renders once on `Start`
   (one frame after a freshly-instantiated row first activates), resetting the
   glyphs to `style.color` and blanking the tint. With `keepColorOnStart: true`
   the PugText re-applies `tmpColor` on that start-render (`if (_keepColorOnStart)
   SetTempColor(tmpColor)` in the decompile). Symptom of getting this wrong: on
   the **first** open after a world-load the tint appears only after several
   seconds (once a discovery-driven `RefreshVisible` re-binds); subsequent opens
   are fine because the rows have already started.

### Bridge placeholder sprite may be fully transparent → renders nothing (Iter-6)

`ui_rarity_border.png` shipped as an 8×8 PNG with **alpha 0 on every pixel** —
a correct `Sprite` import (`textureType: 8`, `spriteMode: 1`) and present in the
AssetBundle, but invisible. A SpriteRenderer pointed at it draws nothing
regardless of size/order/tint. When a hand-authored sprite "doesn't show",
check the actual pixel alpha (`sips` / PIL) before assuming a wiring/order bug.
The visible placeholder is a white 1-px hollow frame (tinted at runtime by the
rarity colour); `ui_slot_border.png` has the right hollow-frame shape but its
`.meta` is `textureType: 0` (the sprite-meta trap) so it is not usable as a
`Sprite` reference without re-importing.

## Sorting / Dropdown (Iter-7)

### Multiple MonoBehaviours in one `.cs` file break prefab wiring

Only the class whose name matches the **filename** gets the Unity-standard
`m_Script.fileID: 11500000`. Any other `MonoBehaviour` class in the same file
gets an MD4-hash fileID — a computed value that is painful to look up and
error-prone to hand-write in prefab YAML.

`DropdownToggleButton` and `DropdownOptionButton` were originally draft-coded
inside `DropdownWidget.cs`. Prefab wiring failed silently (the component was
never bound) until each class was split into its own file:
`DropdownToggleButton.cs`, `DropdownOptionButton.cs`.

**Rule:** one `MonoBehaviour` per `.cs` file. Always.

### Bridge sprite trap: use IB's sheet atlases, not extracted singles

`Art/Bridge/` at one point held individually-extracted PNGs (`ui_icon_sort.png`,
etc.) copied from ItemBrowser with a broken `.meta` (`textureType: 0` →
imported as `Texture2D`). `LoadAsset<Sprite>` returns `null` for a `Texture2D`
asset; the SpriteRenderer silently shows nothing.

ItemBrowser's canonical sources `ui_icon.png` and `ui_group.png` are proper
**multiple-mode sheet atlases** (`textureType: 8`, `spriteMode: 2`) with named
sub-sprites. Copy those atlas files (with their `.meta`) and reference
sub-sprites by `{fileID: <internalID>, guid: <atlas guid>, type: 3}`. Never
extract individual PNGs from an atlas — they lose the sheet-atlas meta.

### `using System;` in a UI file → `Object.Instantiate` is CS0104-ambiguous

`System.Object` and `UnityEngine.Object` both become `Object` when both
namespaces are in scope. The compiler error is:

```
error CS0104: 'Object' is an ambiguous reference between
'UnityEngine.Object' and 'System.Object'
```

**Fix:** qualify the call: `UnityEngine.Object.Instantiate(...)`. Alternatively,
remove `using System;` and replace any `System.*` usage with fully-qualified
names. Files without `using System;` (e.g. `ItemChecklistContent`) are
unaffected.

### Generated `.meta` trails its `.cs` by one build

Unity writes a new script's `.meta` file (the GUID carrier) only on the next
Editor import/build — it is not present until the Editor has seen the file.
A `.cs` committed before a build leaves its `.cs.meta` untracked.

**Rule:** always build once after adding a new `.cs`, then `git add` both the
`.cs` **and** its generated `.cs.meta` together before committing.

### Editor batchmode build ≠ sandbox pass (new APIs)

The Editor compile gate cannot see a RoslynCSharp-sandbox `CompileFailed` —
that surfaces only at game launch. New BCL or Unity API usage added in Iter-7
(e.g. `UnityEngine.Input.GetMouseButtonDown` for click-outside detection) must
be confirmed by actually launching the game and watching `Player.log`, not
just by a green Editor build.

See `CLAUDE.md § Build-verify` for the canonical `Player.log` grep pattern.

### `ui_scrollbar_handle` button background needs `~{1,1}` m_Size to read as raised

9-slicing the narrow 4×8 `ui_scrollbar_handle` sprite with a small or
squished `m_Size` (e.g. `{0.8, 0.7}`) flattens the raised look into a smear.
The raised button effect reads correctly only at approximately `m_Size {1,1}`.
Match the working asc/desc button's transform size when adapting this sprite
for other clickables.

## Catalog / Bake (Iter-7.1)

### `ObjectType.NonUsable` is raw materials, not garbage

`ItemCatalog.Bake` Loop 1 used to `continue` on `ObjectType.NonUsable`, with a
comment calling it "garbage / test fixtures / prefab stubs". **That is wrong.**
Core Keeper assigns `NonUsable` to **raw materials** — ores, bars, raw wood,
scrap, plain Wood, etc. The blanket exclude silently dropped every one of them
from the checklist (user noticed Holz/Kupfererz/Schrott missing). ItemBrowser's
`ObjectUtility.IsNonObtainable` does **not** exclude `NonUsable` at all.

The fix keeps `NonUsable` items and instead drops only the internal engine
entities CK also files under that type. Empirically on game version 1.2.1.4
there are 126 `NonUsable` items: 117 real materials (all carry an icon) and 9
internal entities with **no icon and no localized name** — 4 territory
spawners, the world `TheCore`, the `DroppedItem` entity, and 3 boss-statue
prefab stubs. The guard is therefore `objectType == NonUsable && smallIcon ==
null && icon == null → continue`: icon presence cleanly separates the two
populations, and IB's full `IsNonObtainable` can't be reused here because it
needs ECS/registry APIs the RoslynCSharp sandbox blocks.

**Diagnosing the population:** a throwaway DIAG census (`total/kept/dropped` +
per-entry `nameNoIcon` name logging) in Loop 1, read from `Player.log` after a
world-load, is how the 117-vs-9 split and the 9 names were confirmed before
choosing the icon guard. Stripped before merge.

## Search Field / Header (Iter-8)

### The search input is `TextInputField` (CK-native), NOT uGUI

CK ships `TextInputField` (`Pug.Other.dll`) — a `UIelement,
InputManager.TextInputInterface` that renders through `PugText`, carries a
`CharacterMarkBlinker` caret, and self-activates in
`OnLeftClicked → Manager.input.SetActiveInputField(this)`. Subclass it
(`SearchBar : TextInputField`). The committed-but-orphaned
`UnityInputFieldAdapter` (a `UnityEngine.UI.InputField` wrapper) was the wrong
abstraction — uGUI structurally fails in CK — and was deleted in Iter-8. IB's
`SearchBar : TextInputField` is the canonical reference; its prefab lives in
`ItemBrowserUI.prefab`.

### Freshly-added SpriteRenderers default to a DEAD material → render nothing

Adding a `SpriteRenderer` via "Add Component" in this project assigns material
`guid 274d4544…` — which **does not exist as an asset** (dangling reference). A
SpriteRenderer with a missing material draws nothing, even with a valid sprite,
correct sorting, and opaque colour. Every working window renderer instead uses
Unity's built-in **Sprites-Default** (`fileID: 10754, guid:
0000000000000000f000000000000000, type: 0`). Symptom: object exists, selection
box shows, but nothing renders — in the Editor *and* in-game. Fix: set Material
→ Sprites-Default on every hand-added SpriteRenderer (or duplicate a working
element to inherit it).

### Freshly-added SpriteRenderers default to Sorting Layer "Default", not "GUI"

A new SpriteRenderer lands on sorting layer `0` ("Default"); the whole window is
on **"GUI"** (`m_SortingLayer: 5`, ID `1241602095`). Wrong layer → sorted behind
the panel → invisible. Set Sorting Layer = GUI + an appropriate Order (header
controls ~50–54). Distinct from the material trap above — they often co-occur on
hand-authored renderers and must both be fixed.

### Caret scale: white_pixel is 1×1 px @ PPU 16 → scale UP, not down (SUPERSEDED — historical lore)

> **Superseded since Iter-12.** The caret no longer uses `white_pixel`: Iter-12
> swapped it to the painted 2×8 `Caret` sheet sprite and **removed** the
> `{0.8, 6, 1}` scale hack; Iter-14.1 then sliced it to 2×7 via a vertical
> 9-slice. Kept below only as background on why the original sub-pixel approach
> needed up-scaling.

`white_pixel.png` is 1×1 px at `spritePixelsToUnits: 16` → base size 0.0625
units. A caret built from it needs **up**-scaling to be visible — e.g. Transform
scale `~{0.8, 6, 1}` for a ~0.05 × 0.38-unit bar. A naive `{0.06, 0.4}` yields a
sub-pixel sliver (the caret blinks correctly via `CharacterMarkBlinker.sr`, just
invisibly small). `CharacterMarkBlinker` has one serialized field, `sr` (the
SpriteRenderer it toggles); wire it to the caret's renderer.

### CK text-input deselects on mouse-leave → set `dontDeactivateOnDeselect`

CK's selection is hover-based; leaving the field's collider fires
`OnDeselected → Deactivate`, so typing stops the instant the mouse moves off.
Set **`dontDeactivateOnDeselect = true`** to stay focused off-hover. It then
won't self-deactivate, so deactivate explicitly on window close
(`HideUI → searchBar.Deactivate(false)`, guarded by `inputIsActive`) or a
closed window leaves the input active and **WASD blocked**.

### Duplicate-and-strip a CK widget: remove the leftover button + collider

Duplicating a working widget subtree (e.g. the dropdown's `Display`) to inherit
its correct sprite/material/sorting/9-slice is the safest authoring path — but
you inherit its **function** too. A copied `ButtonUIElement` (here
`DropdownToggleButton`) keeps its `owner` pointing at the *original* widget, so
its leftover 3D collider hijacks clicks and fires the original's action. When
repurposing, remove the `ButtonUIElement` component **and** its `BoxCollider`.

### PugText doesn't render in the Editor (runtime `Render()` only)

`PugText` builds its glyph SpriteRenderers at runtime via `Render()`; in the
Prefab/Scene view it shows nothing. So the Editor is unreliable for previewing
text-bearing UI — verify text in the Game view (build + run). For overlap/click
checks, the **BoxCollider gizmos** are reliable (that *is* what CK's 3D raycast
sees). SpriteRenderer pieces (backgrounds, glyphs) *do* render in the Editor
once their material + sorting layer are correct (see the two traps above).

### `TextInputField` forces its PugText into CK's buggy word-wrap (Iter-19)

Typing in the search field threw `IndexOutOfRangeException` **every frame** via
`PugFont.AddNewLinesToLinesExceedingMaxWidth ← TextInputField`. A pre-existing CK
bug — empirically reproduced on stock and on **main** (127× same stack with the
same input); silent to the player (the UI still filters) but log-spammy.

Root cause (Pug.Other decompile): `TextInputField.Awake` sets
`pugText.maxWidth = maxWidth + (dontAllowNewLines ? 1 : 0)` — for the search field
`7.5 + 1 = 8.5`. Any `pugText.Render()` with `maxWidth > 0` then runs the word-wrap
path, whose `text[num3 - 1]` indexes out of range on certain input. The roadmap's
"set the prefab `pugText.maxWidth = 0`" candidate is a **no-op**: `Awake`
overwrites the prefab value at runtime — the fix must come from code.

A single-line field (`dontAllowNewLines: 1`) must never word-wrap, so `SearchBar`
overrides `Awake` (`private new void Awake()`, calls `base.Awake()`, then
`pugText.maxWidth = 0f`). **Visual width is unaffected**: the field's *own*
`maxWidth` (7.5) still clips overflowing characters via
`TextInputField.TrimTextToFitRestrictions` (a char-trim loop, independent of the
PugText word-wrap). Done in `Awake`, not `LateUpdate`, so it holds before the first
render — covers `SyncFrom` restoring a long prior search on open. Nothing rewrites
`pugText.maxWidth` per frame, so one write persists. Same CK PugFont bug class the
Iter-9 ASCII search-hint and the Iter-11 `RenderNoWrap` (`maxWidth = 0`) labels
sidestepped — `TextInputField` is the one place the value is reimposed. See the
canonical root-cause writeup in `§ PugFont.Render crashes on labels exceeding
maxWidth` (under Localisation (Iter-11)).


## Catalog / Bake (Iter-10)

### `ObjectInfo.level` is dead — use `LevelCD` (Iter-10)

`ObjectInfo` has a `level` field, but it is **not** set by the game and reads
as 0 for every item (legacy field, dead code, not populated by any live system).
**Use `PugDatabase.TryGetComponent<LevelCD>(od, out var lvl) ? lvl.level : 0`**
to get the actual item level. This is the same path ItemBrowser's
`ObjectUtility.GetBaseLevel` takes (confirmed via ILSpy decompile).

Symptom of using `ObjectInfo.level` directly: every item shows level 0 and
the Level sort produces identical values for the whole catalog.

### `sellValue == -1` is "auto-compute", not unsellable (Iter-10)

`ObjectInfo.sellValue == -1` is CK's sentinel for **"compute the sell value
from rarity + crafting ingredients"**. It does **not** mean unsellable. Items
with `sellValue == -1` have a real sell price — it just needs to be derived.

Truly unsellable items are identified by the presence of the
`CantBeSoldAuthoring` component OR by `rarity == Legendary`; their computed
value is 0.

The correct logic (ported from ItemBrowser `ObjectUtility.GetValue`, sell
mode):
1. `HasComponent<CantBeSoldAuthoring>` OR `rarity == Legendary` → 0.
2. `sellValue >= 0` → use directly.
3. `sellValue < 0` → auto-compute: rarity base (`GetRaritySellValue`) + crafting
   ingredients + cooked-food ingredient recursion + objectID-seeded ±10 % jitter.

Symptom of treating `sellValue == -1` as unsellable: the majority of items
show `—` for value and sort as if worth 0.

## Prefab / Editor (Iter-10)

### Prefab opened in Editor isolation renders blank when all SpriteRenderers use `maskInteraction: VisibleInsideMask` and there is no SpriteMask in the prefab

An `ItemRow` prefab open in the Editor (isolated mode) shows all
`SpriteRenderer`s as invisible because they all use `m_MaskInteraction: 1`
(Visible Inside Mask). Outside a parent that owns a `SpriteMask`, the renderers
are always outside any mask's range and therefore invisible.

This is **expected and not a bug.** The rows only render correctly in the
context of the window prefab, which supplies the SpriteMask. Do not change the
`maskInteraction` to `None` to "fix" the Editor preview — that would break the
row clipping at runtime.

Verify row visuals only by building and running the game, not by inspecting the
row prefab in isolation in the Editor.

### grep-by-GUID is unreliable for verifying which sprite a SpriteRenderer uses

A Unity atlas / sprite-sheet asset has one GUID for the whole texture file but
many internal fileIDs — one per named sub-sprite. Grepping a prefab YAML for
a known GUID only tells you the atlas is referenced; it does NOT tell you which
sub-sprite is referenced. Two SpriteRenderers pointing at the same atlas GUID
but different fileIDs show completely different glyphs.

**Use `utils/prefab_query.py`** (a YAML parser) to resolve `{fileID, guid}`
pairs to their named sub-sprite, or compare fileIDs explicitly against the
atlas `.meta` sub-sprite table. Never use `grep <guid>` as a substitute for
verifying the exact sub-sprite selected.

### Unity ILPP "Initial Asset Database Refresh" hang after a batchmode build

Symptom: the next batchmode build after a successful one stalls indefinitely at
`"Initial Asset Database Refresh"` in the Editor log and never proceeds.

Cause: the Unity IL post-processor (ILPP) left a lock or stale cache file in
`Library/Bee/` from the previous build run.

Recovery:
1. Kill the hung Unity Editor process (`pkill -f "Unity"` or via Activity
   Monitor).
2. Delete `<SDK_PATH>/Library/Bee/` (the build cache — safe to delete; Unity
   regenerates it on next build; only deletes build-cache, not project assets).
3. Restart the Unity Hub and re-open the project.

This hang is intermittent and not caused by source changes. If a build that
previously succeeded stops progressing at ILPP, the Bee cache is the first
thing to clear.

## Item Rows & Header (Iter-9)

- **Small point-filtered sprites distort on the 1/16 grid.** A small
  `SpriteRenderer` (e.g. the 5x5 `ui_icon_clear_search` clear button) renders
  distorted (uneven pixel doubling) when its position lands **exactly** on a
  `k/16` world coordinate; any off-grid nudge (even `+0.005`) makes it crisp.
  Resolution-independent (verified across fullscreen/borderless/windowed) -- a
  world/texel rounding ambiguity, not screen sub-pixel. CoreLib's `PixelSnap`
  snaps *onto* `k/16`, so it is counterproductive here. See the
  `project-corekeeper-sprite-ongrid-distortion` memory.
- **Overlapping clickables: UIMouse picks the nearest collider along +Z.**
  `UIMouse` raycasts from `pointer + back*5` along `Vector3.forward` and keeps
  the smallest-distance hit. The clear button's collider sits inside the search
  field's collider; both at `z-center 0` was a tie -> nondeterministic pick (the
  X click sometimes focused the field instead of clearing). Fix: pull the inner
  collider forward (`m_Center.z = -0.5`) so it is always hit first.
- **The thinTiny font crashes on the real ellipsis (U+2026).** Rendering the
  hint "Search<ellipsis>" with the real `...` glyph threw `IndexOutOfRangeException`
  in `PugFont.AddNewLinesToLinesExceedingMaxWidth`, aborting `ShowUI` *before*
  CoreLib set `currentInterface` -- which left `isAnyInventoryShowing` false, so
  CK never blocked world input (clicks + WASD leaked through the open window).
  Use ASCII "..." in the hint string.
- **Do NOT force CK's input latches to fix the first-open input leak.** The real
  root cause of the leak is CoreLib setting `currentInterface` *after* `ShowUI`
  (see the ellipsis crash above) -- let `ShowUI` complete cleanly. Forcing the
  latches (`AnyInventoryOrMapWasActiveThisFrame` / `PlayerInputBlockedThisFrame`)
  over-blocks the ESC / E close path, so the window can't be closed. A
  `WorldInputSuppressWhileChecklistOpenPatch.cs` was built for this and deleted.
- **`TextInputField` re-asserts the caret position every frame.** It sets
  `characterMarkBlinker.transform.position = pugText.position` in its update, so a
  static prefab `localPosition` on the caret GameObject is ignored — the per-frame
  write clobbers it. Iter-14.1 fixed this (pure prefab, zero C#) by moving the caret
  `SpriteRenderer` into a child GameObject `CaretSprite` carrying a constant
  `localPosition` (+1px up to centre, +2px right for a gap): the child inherits the
  parent's per-frame world position and adds the nudge on top. The `SpriteRenderer`
  **kept its fileID** (only re-homed to the child), so `CharacterMarkBlinker.sr`
  needed **no** rewire. (Iter-12 had already swapped the caret off `white_pixel`
  onto the painted 2×8 `Caret` sheet sprite — see the superseded "Caret scale"
  note above; 14.1 also sliced it 8px→7px via the sprite's vertical 9-slice.) The
  intermittent worktree-AssetDatabase staleness that confounded the calibration is
  documented in § Worktree builds.
- **CK pixel fonts blur when Transform-scaled below 1.** To render text smaller,
  use a smaller native font (`thinTiny`, `fontFace` `16777344`) instead of
  scaling a larger font down -- a sub-1 Transform scale produces uneven, blurry
  pixels.
- **`PugSprite.dll` must be in the asmdef `precompiledReferences`.** The
  `CursorScaleRestorePatch` touches `SpriteObject`, which lives in `PugSprite.dll`;
  without the reference the patch fails to compile with `CS0012`.
- **The scrollbar can be reparented in the prefab without breaking wiring.**
  `UIScrollWindow.scrollBar` is a fileID reference, so it survives reparenting
  (e.g. moving the `ScrollBar` into `RowsContainer`). Recompute the scrollbar's
  `localPosition` for the new parent and fix **both** `m_Children` lists (remove
  from the old parent, add to the new one).
- **`PugTextStyle.HorizontalAlignment` enum:** serialized `horizontalAlignment`
  is `left = 0`, `center = 1`, `right = 2`.

## Localisation (Iter-11)

### `LanguageDataBlock`s are runtime-only — the SDK editor cannot enumerate them

`ScriptableDataEditorUtility.GetCachedDataBlocks<LanguageDataBlock>()` and
`AssetDatabase.FindAssets("t:LanguageDataBlock")` both return 0 results — in
`-batchmode` **and** in a fully-loaded interactive Editor (verified across 600
`Update` ticks after project import). `LanguageDataBlock` carries
`[RuntimeInitializeOnScriptableDataLoad]`; the blocks are instantiated only at
game runtime, not at edit time.

Consequence: a TextDataBlock generator cannot use the real CK-SDK localisation
API at build time — there are no `LanguageDataBlock` instances to iterate. The
address→ISO mapping was captured once via a runtime dump
(`ScriptableData.GetDataBlocks<LanguageDataBlock>()` logged from the running
game) and committed to `core_keeper/utils/ck-language-addresses.json` (13
runtime languages; primary = `en`). This is why the generator templates raw
`.asset` YAML (Option II) instead of calling `LanguageDataBlock` methods at
build time.

### `m_Script.guid` for game-DLL MonoBehaviours is per-SDK-clone-local — resolve it dynamically

Generated TextDataBlock `.asset` files must reference `ScriptableData.dll`
via its `.meta` GUID. Copying a foreign value (e.g. Item Browser's
`e853a5af…`) makes the asset fail to bind to the class at build time — it
bundles broken, and at runtime the loader emits `couldn't load
Assets/…/X.asset from asset bundle`, the block is never imported,
`GetLocalizedTerm` returns null, and the UI shows the raw term key instead of
a translated string.

Fix: resolve the GUID dynamically at generation time:

```csharp
string guid = AssetDatabase.AssetPathToGUID(
    "Assets/Plugins/CoreKeeperModSDK/ScriptableData.dll");
```

The `fileID` (`2108018792`) is the portable MD4 class-name hash and is safe to
hardcode. Cross-ref the `project-corekeeper-script-fileid-derivation` memory
for the general rule.

### `PugFont.Render` crashes on labels exceeding `maxWidth` — set `maxWidth = 0f` on all localised single-line labels

`PugFont.Render` calls the internal `AddNewLinesToLinesExceedingMaxWidth` only
when `maxWidth > 0f`. That method throws `IndexOutOfRangeException` on labels
whose text length exceeds the configured `maxWidth` — which English text may
not, but longer translations (e.g. German) routinely do.

Critically, the throw occurs inside `ShowUI()` (via
`FilterWidget.RebuildList` / `DropdownWidget` label renders), which
aborts before CoreLib sets `currentInterface`. The result: the window opens
but cannot be closed with ESC or E — only the mod's own F1 toggle works.

Fix: set `PugText.maxWidth = 0f` on every localised single-line label (filter
rows, section headers, dropdown labels). With `maxWidth == 0f` the wrap path is
never entered and no crash is possible.

As of Iter-14.2 this `maxWidth = 0f; Render(…)` pair lives in **one** null-safe
extension, `PugText.RenderNoWrap` (`ui/PugTextExtensions.cs`) — now the single
home of `maxWidth = 0f`. The order is load-bearing: `maxWidth = 0` MUST precede
`Render` (the extension does this), or the wrap path runs before the guard takes
effect. Route every single-line label through `RenderNoWrap`.

This is the same crash class as the thinTiny ellipsis (U+2026) note in
`§ Item Rows & Header (Iter-9)` and the `TextInputField` word-wrap note in
`§ Search Field / Header (Iter-8)` (Iter-19) — the throw site is identical; the
triggers differ (ellipsis glyph vs. line-length overflow vs. `TextInputField.Awake`
forcing `pugText.maxWidth`). All three are fixed by keeping single-line text away
from the `AddNewLinesToLinesExceedingMaxWidth` code path.

**Also:** never use U+2026 (ellipsis `…`) or U+2014 (em-dash `—`) in term
values — the `LocalizationGenerator` validates term strings and **fails the
build** on either character. Use ASCII `...` and `-` instead.

### Language-change re-bake must be deferred + world-guarded

`I2.Loc.LocalizationManager.OnLocalizeEvent` fires **mid-`DoLocalizeAll`** —
while I2's localization source is only half-rebuilt. Re-baking the item
catalog synchronously inside the handler re-enters that half-rebuilt source
and throws `NullReferenceException` (`PlayerController.GetObjectName` →
`GetObjectName`), one NRE per language. Two-part fix in
`ItemCatalogLocChangeHook`:

1. **Defer.** The hook only sets a `RebakePending` flag. The actual re-bake
   runs from `ItemChecklistMod.Update()` via
   `ItemCatalogLocChangeHook.ProcessPending()` on the next tick — a stable
   frame *after* `DoLocalizeAll` has finished (this also coalesces rapid
   successive language switches into one re-bake).
2. **World-guard.** Skip the re-bake unless `Manager.main.player != null`.
   `ItemChecklistMod.Catalog` is a `new ItemCatalog()` from `Init()`, so it is
   non-null **from mod load, not from a world load**. Without the guard, a
   language switch on the main menu (no ECS world, no player) baked anyway and
   NREd. Consume the pending flag even when the guard skips, so a stale flag
   doesn't re-trigger a bad bake on a later tick.

Verified: cycling languages in the main menu **and** switching EN<->DE
in-world both re-bake cleanly, 0 NREs.

## HUD Counter (Iter-11.5)

### An always-on element must be on the HUD layer (27) at z≈0 — not layer 5 / parent origin
A non-modal `UIelement` parented under `chestInventoryUI.transform.parent`
(`IngameUI`) does **not** render if it copies the modal window's setup (Unity
layer 5 "UI", parent-origin position). Symptom: GameObject active, full alpha,
on-screen, but `SpriteRenderer.isVisible == false`. Two independent runtime-only
reasons (a clean Editor build hides both):

- **Unity layer.** The uiCamera draws the **HUD layer (27)** during gameplay
  (`CameraManager.ShowHUD` toggles `1 << ObjectLayerID.HUD` in its `cullingMask`);
  layer 5 ("UI") is only drawn for modal UIs that CoreLib's open-path activates.
  Put **every** GameObject in the HUD prefab on layer 27.
- **Z plane.** `IngameUI` sits at world z = -10; CoreLib repositions modal UIs to
  `initialInterfacePosition` (z = 10 → world z ≈ 0) when opening. A static element
  left at the parent origin (world z = -10) is outside the uiCamera frustum. Give
  the content local z = 10.

Bonus: being on the HUD layer means `CameraManager.ShowHUD(false)` culls the
element together with the rest of the gameplay HUD, for free.

**Caveat (Iter-15) — this "for free" only covers the `ShowHUD` path.** The
spawn-from-Core **intro cutscene** does NOT call `ShowHUD(false)`; it calls
`Manager.ui.FadeOutAllGameplayUI()` (`CutsceneHandler.StartPlaying`, `Pug.Other`
~364007), which fades only CK's *own* registered gameplay UI, not arbitrary
layer-27 renderers. So a mod's layer-27 HUD stays visible during the intro cutscene
and must be gated explicitly — Iter-15 added `!sceneHandler.cutsceneIsPlaying` to
`WorldState.IsInPlayableWorld` (see the `SceneHandler.cutsceneIsPlaying` row in
`CLAUDE.md § CK Decompile References`).

### `CalcGameplayUITargetScaleMultiplier()` returns (0,0,0) for a mod HUD
CK's own HUD elements set
`localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier()` each frame, but for
a mod HUD mounted as above it returns `(0,0,0)` — used as a scale source it makes
the element invisible. Drive visibility explicitly instead (toggle the root active
on `WorldState.IsInPlayableWorld && !Manager.ui.isAnyInventoryShowing &&
!Manager.menu.IsAnyMenuActive()`).

### `Manager.main.player != null` does NOT suppress a load screen (Iter-11.6)
Iter-11.5 originally gated the HUD on `isInGame && Manager.main.player != null`,
believing `player != null` kept it off the world-load screen. **It does not** — and
the same wrong assumption sat in the Iter-15 F1 guard. The player object is
instantiated at `PlayerController.OnOccupied` (the very anchor that kicks our catalog
bake — see `ItemCatalogWorldLoadHook`), which fires *while the load screen is still
up*, and it survives into the exit-to-menu transition. So `player != null` is true
across **both** load screens (entering and leaving) and suppresses neither: the HUD
flashed on the entry load screen and lingered on the exit fade to the main menu.

The fix (`WorldState.IsInPlayableWorld`) mirrors CK's own gameplay-active gate
(`PlayerController.PlayerInputBlocked`, decompile `Pug.Other` ~line 130335):
`Manager.sceneHandler.isInGame && isSceneHandlerReady && !Manager.load.IsLoading()`
(Iter-15 also appends `!cutsceneIsPlaying`).
Two non-obvious choices:
- **`!Manager.load.IsLoading()`** (`loadingQueue != null`), **not** CK's own
  `IsLoadingAndScreenBlack()`. The latter is only true while the screen is *fully
  black*, so during the **exit fade-out** (screen still partly visible, load already
  queued) it returns false and the HUD would briefly flash. `IsLoading()` is true
  from the moment the load is queued until it completes, covering both directions.
- **`isSceneHandlerReady`** complements it for the few frames where the queue is
  already cleared but the scene is not yet fully set up.

Both signals are the same API category as the already-used `Manager.ui.*` /
`Manager.menu.*` (not `Manager.saves`, not `System.IO`) → sandbox-safe (confirmed
in-game: full `Init`/bake lifecycle ran, zero `CompileFailed`). Cutscenes/intro were
later closed by Iter-15 (the `!sceneHandler.cutsceneIsPlaying` term added to
`WorldState.IsInPlayableWorld` — see the caveat above and the
`SceneHandler.cutsceneIsPlaying` decompile row in `CLAUDE.md`).

### Diagnosing "active but invisible" CK UI — log `isVisible` + `z` + `layer`
When a UI element is active, on-screen and full-alpha but nothing shows, log
three things on the element's `SpriteRenderer` to split "culled by camera" from
"drawn but occluded": **`sr.isVisible`**, **`sr.transform.position.z`** (world Z
vs the uiCamera frustum), and **`sr.gameObject.layer`** (HUD 27 vs UI 5).
`isVisible == false` ⇒ **culled** — wrong Unity layer or outside the frustum
(not occlusion); `isVisible == true` but unseen ⇒ sorting/occlusion. This recipe
is exactly how the two render bugs above (wrong layer, wrong z) were resolved in
two builds instead of guessing.

### `PugText.textString` is a serialized field, not a public C# property
`textString` exists in the prefab YAML (`PugText`'s serialized text) but is **not**
a public C# property — referencing it from mod code is a `CS1061` compile error.
Set the text at runtime via `PugText.Render(string)` (as `ItemChecklistHud.Refresh`
does), never by assigning `textString`.

## Sprite Sheet & UI Sorting (Iter-12)

### All PugText sits on the GUI sorting layer — `orderInLayer` separates it from SpriteRenderers
Every `PugText` defaults to `style.sortingLayer = int.MinValue` and
`style.orderInLayer = 9999`. `int.MinValue` is **not** a real layer — it is a
sentinel: `PugText.Render` resolves it to the **GUI** layer
(`SortingLayer.NameToID("GUI")`), then applies `orderInLayer` verbatim as the
renderer's `sortingOrder` (no runtime reset). So PugText glyphs and
SpriteRenderers live on the **same** GUI layer, and order alone decides who
draws in front — with the default 9999, *every* PugText draws over *every*
SpriteRenderer. Consequence: a dropdown popup (a SpriteRenderer BG at order 54)
cannot cover a footer counter (a PugText at 9999) until the footer's
`orderInLayer` is lowered below the popup BG. `orderInLayer` is freely editable
(`SetOrderInLayer` or the serialized style value); the in-list `ItemRow` labels
already use `49`. Fix applied: `StatusBar`/`ShownLabel` lowered to `50`
(< popup BG 54, > window BG; the popup's own option labels stay at 9999 so they
still draw over the panel). Only elements a popup spatially overlaps need this —
header labels sit above the downward-opening popups and stay at 9999 (they must
stay above their own header BG at 52).

### 9-slice border must equal the sprite's actual corner size, not 1px
A sprite drawn `Sliced` (`m_DrawMode: 1`) with `spriteBorder {1,1,1,1}` keeps
only the outermost 1px ring sharp and **stretches** everything inside. If the
pixel-art corner is thicker than 1px, its inner pixels fall in the stretched
"center" zone and distort. The `Entry Selected` selection marker has **3px
L-shaped corners** (transparent edge-midpoints), so it needs
`spriteBorder {3,3,3,3}` — with `{1,1,1,1}` the corners stretched instead of
9-slicing. Rule: read the sprite's alpha map, measure the corner, set the border
to the corner size. (Verify with PIL: crop the sprite rect from the sheet and
print an alpha map.)

### A static checkbox box GO needs `m_IsActive: 1` — the code only toggles the fill
In `FilterCheckboxButton`, the wired `checkMark` SpriteRenderer is the **fill**
("Checkbox filled slash", shown only when checked via `SetChecked → .enabled`).
The empty **box** itself (a separate child GO, "Checkbox empty") is never touched
by code — it must be statically visible. If that box GO has `m_IsActive: 0`, the
whole checkbox is invisible (an inactive parent also hides its fill child), even
though sprite/material/layer/scale are all correct. Naming trap: the box GO is
named `Checkmark`, not the tick.

### "My fix doesn't show in-game" → compare mtimes before blaming caches
When an external edit (YAML/meta written outside the Editor) doesn't appear in
the build, the first check is **not** AssetDatabase cache or symlink theories —
it is `mtime(edited file)` vs `mtime(CoreKeeperModSDK/Library/SourceAssetDB)`
(a proxy for the last build's AssetDatabase pass). If the edit is newer, it
simply wasn't rebuilt yet (and the loader only re-reads the mod on **game
restart**, so a rebuilt mod still needs a fresh game launch). Editor-made
changes get cached + built; external symlink-target edits are *usually* picked
up on the next `build.sh` AssetDatabase refresh — but **not reliably when
building from a git worktree** (see the next subsection). A language switch is
runtime, not a rebuild — identical-build screenshots can differ only in language.
Only after the mtimes prove a real rebuild happened should you suspect the
symlink/AssetDatabase cache.

### Worktree builds: AssetDatabase intermittently misses symlink-target edits
Building a mod from a **git worktree** (`.worktrees/<branch>`) makes `link.sh`
repoint the SDK `Assets/` symlink into the worktree tree. In that setup Unity's
AssetDatabase **intermittently fails to detect edits made through the symlink** —
and the mtime heuristic above gives a **false all-clear**: `build.sh` still
re-exports the AssetBundle with a fresh mtime, but from the **stale imported**
asset, so `mtime(bundle) > mtime(edit)` holds even though the change never made
it in.

Discovered in Iter-14.1: successive prefab edits (a child-GO `localPosition.x`)
did not appear in-game across several builds with fresh bundle mtimes, while
*earlier* edits in the same session (a `localPosition.y` and a
`DrawMode`/`m_Size` change on the same objects) **had** applied — so it is
intermittent, not a consistent break, and not field-/block-specific.

Fix: force a full reimport by deleting the import caches, then build —
`rm -rf "$SDK_PATH"/Library/{SourceAssetDB,ArtifactDB,Artifacts,Bee}`. During a
tight visual-calibration loop in a worktree, clear them **proactively before
each build**: a slower reimport build is cheaper than another "is it stale or
did the value not work?" round. This is broader than the new-mod `SourceAssetDB`
reset (the `project-corekeeper-sourceassetdb-reset` memory, first-add only) — it
hits **existing** mods purely because the build runs from a worktree. The mtime
check above is still the right *first* step; it just cannot clear the worktree
case, because a stale-content rebuild is indistinguishable from a real one by
mtime alone.

### In-game visual calibration: screencapture → sips crop → image Read
Pixel-level UI placement (margins, flush, caret offset, blink position) is judged
by the assistant capturing the live game window directly — no Claude-specific
feature involved, just Bash + a macOS OS tool + an image-capable `Read`:

1. `screencapture -x /tmp/ck.png` grabs the full screen silently (`-x` = no
   shutter sound). Requires the **game window foregrounded** and macOS
   **screen-recording permission** granted to the terminal/host.
2. `sips -c <h> <w> --cropOffset <top> <left> /tmp/ck.png --out /tmp/ck_crop.png`
   crops to just the UI region of interest (smaller image = sharper pixel
   judgement on `Read`).
3. `Read /tmp/ck_crop.png` — the image-capable Read renders the PNG so placement
   can be eyeballed.

For a **blinking** element (the search caret), a single capture often lands on
the blink-off phase and shows nothing. Take ~5 rapid captures ~180 ms apart to
guarantee catching the blink-on phase — pause with
`perl -e 'select(undef,undef,undef,0.18)'` between captures, **not** `sleep`
(foreground `sleep` is blocked in this environment). Read whichever frame shows
the caret lit.

This loop must run **inline in the main session** (it needs the live CrossOver
window and the build lock) — see `docs/conventions.md`.

## Item Icons (Iter-12 extension)

### CK doesn't scale item icons to fit — it enlarges the slot
Detail icons (tools, weapons) overflow a tight icon slot, but scaling them *down*
to fit makes them tiny: `Sprite.bounds.size` reports the **rect** size (e.g.
40/16 = 2.5u for a 40×40 sprite), not the tight visible bbox, so a fit-to-bounds
scale shrinks by the transparent padding and the visible content ends up far
smaller than the slot. CK's own inventory slots don't scale icons either — the
slot background + rarity border are **1.25u** (20px at PPU 16) and the icon
renders at **native** scale inside. The Iter-12-extension `ItemRow` matches this:
`IconSlot` background + `RarityBorder` `m_Size` are 1.25u, and `ItemRow.Bind`
resets `icon.transform.localScale = Vector3.one` (the viewport pool recycles
rows, so the reset must be per-bind).

### `Sprite.bounds.size` is the rect, not the tight bbox
`Sprite.bounds.size` / `Sprite.rect` always reflect the full sprite rect, never
the tight visible mesh (the tight mesh affects rendering only). You therefore
cannot measure an icon's visible extent from `bounds`; any "fit to visible
content" math off `bounds` is wrong for padded sprites. (`Sprite.rect` /
`bounds` / `pixelsPerUnit` access is sandbox-safe.)

### `iconOffset` is slot-relative — Icon must be a child of IconSlot
CK/IB position an item icon by `icon.transform.localPosition = objectInfo.iconOffset`
(IB `UserInterfaceUtility.ApplyObjectIconTransform`). For that offset to land
right, the icon transform must be a **child of the slot** so `localPosition` is
relative to the slot centre. The Iter-12 extension re-parented `Icon` under
`IconSlot` (base `localPosition = (0,0,0)`); `ItemRow.Bind` sets
`icon.transform.localPosition = PugDatabase.GetObjectInfo((ObjectID)objectId, 0).iconOffset`
for discovered rows and keeps the `?` sprite centred (`Vector3.zero`, no item
offset) for undiscovered rows. As a sibling of the slot, setting
`localPosition = iconOffset` would discard the slot position and snap the icon to
the row origin.

### IB's `ApplyObjectIconTransform` scale path is a dead end for padded sprites
IB also applies `scaleMin = Min(1/iconSize.x, 1/iconSize.y)` (with `iconSize` the
bounds when both dims > 1) — i.e. it *does* scale-to-fit. For ItemChecklist's
40×40 padded sprites that shrinks the small visible tool to a dot (the bounds
include the transparent margin). Rejected in favour of the native-size + 1.25u
slot above; only `iconOffset` (not the scale) was kept.

## Prefab Variants & Nested Prefabs (Iter-13)

### Don't grep/awk variant prefab YAML — use the structured parser
Hand grep/awk over a prefab-**variant**'s YAML is unreliable: variants reassign
`fileID`s and serialize inherited GameObjects as stripped-object stubs, so a
line-by-line search reads a partial, re-keyed view of the structure. During
Iter-13 this caused several false alarms ("AscDescButton still in the base",
"templates tangled", "structure regression"), each disproven by
`prefab_query.py` (`load` / `tree`) or an in-game test. Use the PyYAML-based
parser — `utils/prefab_query.py <prefab> tree [Name]` (see
`docs/conventions.md § Prefab Authoring Conventions`) — not grep archaeology.

### Iter-13 pointers (cross-prefab refs + nested-prefab round-trip)
Two facts proven in Iter-13, documented in full elsewhere — pointers only:
- **Serialized cross-prefab `owner` refs are fragile.** Extracting the dropdown
  chrome nulled the header toggle's serialized `owner` and broke header-click
  (caught in-game, not by the Editor compile). Fix: wire `owner` at runtime.
- **Nested `PrefabInstance`s + variants round-trip through the
  ModBuilder→AssetBundle pipeline** — proven by a tracer before the extraction;
  the first nested-prefab use in any mod here.

Full mechanism: `docs/architecture.md § Shared Dropdown chrome` and the Iter-13
entry in `docs/iteration-history.md`.

### Dangling prefab-variant overrides (Iter-18)
Deleting an inherited GameObject from a **base** prefab leaves any **variant**
that overrode that GameObject with a target-less `m_Modification` — the override
now points at a base `fileID` that no longer exists. Unity ignores unresolvable
modifications at runtime (the prefab merge silently skips them — a harmless
no-op), so it **never prunes** them, and they are **invisible in the Editor**: the
Overrides dropdown only lists modifications with a resolvable target, so there is
no Revert path to click. Reimporting the variant does **not** clear them
(verified); "Force Reserialize Assets" *might*, but it is broad and not
guaranteed. The deterministic fix is to strip the modification block directly from
the variant YAML **with the Editor closed** (concurrent file writes collide with
the Editor's own reserialization), then validate via a PyYAML re-parse
(`utils/prefab_query.py`) + a build. Iter-18 hit this: removing the inherited
`AscDescButton` from the base left `Filter.prefab`'s old "deactivate AscDescButton"
`m_IsActive: 0` modification dangling against the deleted base fileID — stripped by
hand and re-validated.
