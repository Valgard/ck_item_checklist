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

See the parent repo's `../docs/macos-crossover-loader.md § Fake-ID dev install`
for the full fake-ID mechanism.

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

### Back-order boundary makes the lowest renderer invisible (Iter-24)

A renderer whose `m_SortingOrder` equals the mask's `m_BackSortingOrder` (the
band's lower bound) is **not reliably captured** by the mask → it renders invisible.
Iter-24 symptom: with the popup mask back-order at **56**, the row-background
sprites at order 56 vanished while labels/checkboxes at 57–60 showed — the
backgrounds fell exactly on the boundary. Rule: set the mask's `m_BackSortingOrder`
strictly **below** the lowest renderer order you want clipped (the fix lowered it to
55, putting the order-56 backgrounds comfortably inside the band). See
`architecture.md § Popup Scroll & Collapse` for the popup mask band (56..63).

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

See `CLAUDE.md § Conventions` for the canonical `Player.log` grep pattern.

**Corollary — deletion-only fixes are near-zero sandbox risk.** The gate fires on
*additions* of new BCL surface (a newly-referenced `System.IO.*`, reflection-emit,
`System.Diagnostics.Process`, …). A fix that only **removes** a call leaves a
strict subset of an already-passing file, so it cannot introduce new banned surface
and is effectively guaranteed to still pass the runtime sandbox compile. (Iter-23
removed a `UnityEngine.Input.GetKeyDown` read; the remaining code had already passed
every prior iteration's sandbox.)

### Lying comments misdirect diagnosis

A comment that asserts behavior the code does not implement is worse than
obviously-wrong code — it actively misdirects root-cause analysis. Iter-23's
`Update` hotkey poll carried `// … raw Input is the diagnostic fallback`, but the
raw `Input.GetKeyDown(KeyCode.F1)` was a co-equal `||` term firing every frame,
not a gated fallback. The comment steered diagnosis toward "why isn't the fallback
gating?" instead of "this OR-term always fires." Rule: when forming a root cause,
verify the actual code path, never the comment; and when you fix the code, delete
the misleading comment with it so what remains describes real behavior.

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

### `ObjectType.Critter` spans two ID ranges incl. Fireflies (Iter-16.2)

Same shape as NonUsable: Loop 1 used to blanket-`continue` on
`ObjectType.Critter` (801); Iter-16.2 relaxed that to the identical icon-guard,
because caught critters become carriable, discovery-tracked items at their own
ObjectID (`SaveManager.SetObjectAsDiscovered` has **no** Critter special-case).
On game version 1.2.1.4 the static DB holds **25** `ObjectType.Critter`
item-forms, all with inventory icons, across **two** ID ranges:

- **9800–9819** — 20 bug-net critters (`CritterBeetle` … `CritterLarvaVoid`),
  gap-free in the live DB (contradicting a decompile probe that called 9803–9807
  empty and 9813 `CritterCrab2` unused — both are real, e.g. 9813 *Sonnen-Zange*).
- **3500–3504** — 5 Fireflies / Glowbugs (`YellowFirefly` … `PurpleFirefly`,
  German *Glimmkäfer*).

**Catch-path red herring:** the Fireflies are `ObjectType.Critter`-typed but
their `FireflyConverter` adds **`FireflyCD`, not `CritterCD`** — and the Bug
Net's `TryCatchAnyCritters` gates on `CritterCD`. So `CritterCD` is **not** a
complete "net-catchable" predicate; chasing it down the decompile wastes a cycle.
All 25 are obtainable (confirmed in-game: Glimmkäfer ARE net-catchable and sit in
player chests) → all discovery-tracked → no permanent `???` ghost rows. The clean
predicate is just `objectType == Critter && smallIcon == null && icon == null →
continue` (icon-guard, mirroring NonUsable).

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

**Recurs on Editor-authored prefab children too (Iter-22).** Not just runtime
`AddComponent`: an **Editor-authored** prefab child can also come back on Sorting
Layer "Default". Iter-22's `HoverHighlight` SpriteRenderer (authored in the
prefab) had `m_SortingLayerID: 0` while the row renderers are GUI — so it would
render behind the row content and outside the GUI 40..55 mask band. Note the two
distinct "layer" axes: the GameObject's Unity **tag-layer** (5 / "UI", which was
correct) is separate from the sprite **sorting layer** (must be GUID
`1241602095` / "GUI"). Setting one does not set the other — check both.

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

#### Corollary (Iter-24): `VisibleInsideMask` + no active mask = invisible → keep the popup mask active while open

The same rule bit the scrollable Filter popup. Row templates are statically
`VisibleInsideMask` (`m_MaskInteraction: 1`), so gating the popup's `SpriteMask` on a
scroll-active flag would make a short/collapsed popup's rows vanish whenever no
overflow exists (no active mask + `VisibleInsideMask` = invisible). Fix: keep the
mask **active whenever the popup is open** — it is a child of the popup panel and
follows the popup's active state; sized to the cap, it clips only on overflow and
shows everything when content fits. The scroll-active gate then governs only the
scrollbar/handle + wheel-ownership, NOT the mask. See `architecture.md § Popup
Scroll & Collapse`.

### Serialized-field zero-default sentinel: make "off" coincide with 0 (Iter-24)

A newly-added `public` serialized field is **absent** from existing prefab YAML, so
Unity deserializes it to `0` (its C# field initializer does **not** survive — the
serialized value, here the implicit `0`, wins). Choose the "off / no-op" sentinel to
coincide with `0`. Iter-24's `MaxVisibleRows` uses `<= 0` to mean "no cap": a legacy
prefab with no `MaxVisibleRows` line deserializes to `0` → the neutral, behaviour-
preserving state. A non-zero "off" value (e.g. `float.MaxValue` for "unbounded")
would make those same legacy prefabs deserialize to `0` → an *active broken* state
(here: a zero-height cap clipping everything), not a neutral one.

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
`docs/ck-decompile-reference.md`).

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
`SceneHandler.cutsceneIsPlaying` decompile row in
`docs/ck-decompile-reference.md`).

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

**Same trap class, inverse direction (Iter-22):** for a code-driven hover
highlight, toggle the **`SpriteRenderer.enabled`**, keep the **GameObject
active** (`m_IsActive: 1`). The `LateUpdate` flips `highlight.enabled`; if the GO
were inactive, `LateUpdate` would never run to turn it on. `ItemRow.Bind` resets
`highlight.enabled = false` on recycle so a pooled row carries no stuck highlight.

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

### A user-shared macOS screenshot in `NSIRD_…` is unreadable (TCC `EPERM`)
When the user shares a screenshot straight from macOS's screencapture **floating
thumbnail** (before saving it), its path is a protected temp buffer like
`/var/folders/…/TemporaryItems/NSIRD_screencaptureui_<rand>/Bildschirmfoto ….png`.
Both the `Read` tool **and** Bash (`cp`/`cat`) get `Operation not permitted`
(`EPERM`) on it — TCC sandboxes that directory; it is **not** a quoting/space
issue. Don't retry variants. Ask the user to either **save** the shot (click the
thumbnail away / ⌘-save → lands on the Desktop) or drag it into `/tmp/`, then
re-share the path; or have them describe the relevant pixels. (A screenshot the
user pastes as an inline image attachment is fine — this only bites raw
`NSIRD_…` filesystem paths.)

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

## Pet Skins (Iter-16.1)

### Gradient-recolor shader: `Amplify/UISpriteColorReplace`, and the existent-but-wrong-shader no-op
The gradient-capable UI shader CK uses to recolor pet-skin icons is
**`Amplify/UISpriteColorReplace`** — it carries the `_GradientMap` property + the
`USE_GRADIENT_MAP` keyword. The icon needs a `Material` on **this** shader; the
mod's default icon material lacks the property, so enabling the keyword alone is a
silent no-op (the sprite renders, no recolor).

**The trap:** two decompile agents guessed `Radical/SpritesDefault`. That shader
**also exists**, so `Shader.Find("Radical/SpritesDefault")` returned a non-null
(but wrong) shader, masking the failure — the icon still rendered, the keyword was
ignored, and nothing recolored, with no error to point at the cause. Lesson: an
existent-but-wrong shader name silently no-ops; `Shader.Find` returning non-null
proves nothing. The fix came from the **working reference mod** — Item Browser's
`GetUISpriteColorReplaceMaterial()` is literally
`new Material(Shader.Find("Amplify/UISpriteColorReplace"))` — which beat two
decompile guesses. Full recipe (shared per-skin material, keyword + gradient
texture, base-material restore for non-pet rows) in `ui/PetSkinIcon.cs`.

### `GradientMapDataBlock` needs `ScriptableData.dll` in `precompiledReferences`
`GradientMapDataBlock` (the pet-skin gradient source) extends
`ScriptableDataBlock`, which lives in **`ScriptableData.dll`**. The runtime
`.asmdef` already referenced `ScriptableData.Addressables.dll` but **not**
`ScriptableData.dll`, so the first build referencing `GradientMapDataBlock` failed
with `CS0012` (type defined in a not-referenced assembly). Fix: add
`"ScriptableData.dll"` to the asmdef `precompiledReferences`. (Same class as the
Iter-9 `PugSprite.dll` / Iter-11 `ScriptableData.dll`-GUID references — a game type
used directly needs its DLL in `precompiledReferences`.)

### Worktree builds — cwd reset + stale build log
The worktree AssetDatabase-staleness cache-clear (clear
`Library/{SourceAssetDB,ArtifactDB,Artifacts,Bee}` before each worktree build) is
the existing `§ Worktree builds` note / `project-corekeeper-sourceassetdb-reset`
memory; the env-chain `direnv exec` recipe is `docs/conventions.md § Worktree
Conventions`. Iter-16.1 added two cwd/log traps on top of those:

- **A `cd` elsewhere mid-iteration resets the Bash cwd and breaks relative build
  invocations — silently.** Running `cd ~/.claude` to make a memory commit reset
  the cwd from the worktree to `core_keeper`; the next relative build invocation
  then misfired with no obvious error: `tee: /build.log: Read-only file system`
  (empty `MOD_INSTALL_PATH`), `../../../utils/build.sh: No such file`, and a
  **stale** `✓ Build complete` left in the log from the *prior* build. The "build
  succeeded" line was real — for the previous run. Fix: make builds/git
  cwd-independent — use absolute paths, `git -C "$WT"`, and call
  `build.sh "$REPO_ROOT"` with an explicit path arg rather than relying on `$PWD`
  or `../../../`.
- The env-chain fix itself (the copied worktree `.envrc` does `source ../.envrc`,
  which from the worktree resolves to `.worktrees/.envrc`, not `core_keeper/.envrc`)
  is covered in `docs/conventions.md § Worktree Conventions`: build via
  `direnv exec "$WT" bash -c '…'`, which walks the real `source_up` chain
  (worktree → mod → parent).

## Item Rows & Hover (Iter-22)

### Full-row colliders leak hover past the visible viewport
The row hover collider spans the **full row width** (28.15); pooled buffer rows
(+4) and the partially-clipped bottom row extend **under** the window
`ContentsMask` into the header / footer / side margins. A SpriteMask clips the
**sprite**, NOT the **collider** — so a cursor sitting in the header/footer over
empty chrome still raycast-hits a clipped row behind it and fires a phantom
tooltip + highlight. Fix: gate **all four** hover overrides **and** the per-frame
`LateUpdate` highlight on `ItemChecklistContent.PointerInViewport()` — a static
cursor-world-pos vs cached `ContentsMask` world-bounds check (mirrors
`PopupWidget.PointerOverPanel`; the orthographic UI-camera world read of
`§ Popup Scroll & Collapse`). A SpriteMask never constrains hover; the viewport
gate must be explicit.

### The "popup leak" was a FALSE alarm — do not re-add the guard
Concern: an open Sort/Filter popup overlaying the list lets a full-width row
collider behind it leak a tooltip. In-game proved there is **no leak** — the
popup's own elements (closer in the 3D raycast) take the selection, so the
closest-collider arbitration already handles the overlap. A
`PopupWidget.PointerOverOpenPopup` guard was built for this, then **reverted**
entirely. Do **not** re-add it: the raycast distance ordering is the mechanism,
and a redundant bounds guard is dead weight.

### Pet-skin tooltips: `ckVariation` must be 0, not `skinIndex`
`ItemRow.Bind`'s `skinIndex` param is a **skin selector**, not a CK variation —
pets always sit at CK variation 0 (`SaveManager.SetObjectAsDiscovered` force-zeroes
it; see `§ Pet Skins`). The tooltip helper must be fed
`ckVariation = isPetSkin ? 0 : skinIndex`; passing `skinIndex` builds an
**unresolvable** `ObjectDataCD` (CK finds the wrong object / empty tooltip).
Verified in-game on `Eulux (Skin 3)`.

### An Editor-only authoring aid must be force-disabled at runtime
The per-row `ContentMask` SpriteMask is an **Editor authoring aid** (it previews
the per-row clip in the prefab) but is **unwanted at runtime** — the window's
own `ContentsMask` does the real clipping. So it can be left **enabled in the
prefab** for Editor convenience and **force-disabled per row at runtime** in
`EnsurePool`, right after `Instantiate`. Reusable pattern: "Editor-aid component,
left on in the prefab, force off at runtime."

### CK's tooltip localiser does NOT see mod localization terms
Real-item tooltips localize fine because the four hover virtuals return **raw CK
loc keys** (e.g. `Items/AncientCoin`) that CK's own localiser resolves. A
**mod-authored** term (the `??? - not yet discovered` placeholder) is invisible
to that localiser, so the mod must resolve its OWN term first — `Loc.T` →
`API.Localization.GetLocalizedTerm` — and pass the **already-resolved** string
with `dontLocalize` set, or the tooltip shows the raw term key. (The placeholder
strings are ASCII-only — `-`, not `—` — per the no-em-dash / no-ellipsis yaml
rule in `§ PugFont.Render crashes` under Localisation (Iter-11).)

## Font / Glyphs (Iter-25 / Iter-46)

- **Missing thinTiny glyph = silent CJK fallback, not "?".** A char absent from
  `thinTiny.codePoints` does NOT crash or print `?` — `PugFont.GetGlyphData`
  resolves it from the chinese font (CJK metric) → deformed, **no log warning**
  (the glyph IS found, just from the wrong face). This is exactly the "silent,
  no log" class this file captures. See `docs/architecture.md § Runtime Glyph
  Injection`.
- **The fix now lives in a separate mod.** As of Iter-46 this mod no longer
  patches `thinTiny` itself — the required **Complete Tiny Font** mod replaces
  it wholesale. The sprite-pivot convention and the other hard-won traps this
  mod used to document here now live in that mod's own `CLAUDE.md`.

## Cattle (Iter-16.3)

### Cattle colour variants — an owned cattle can read `???` (variation-keyed discovery)

The catalog collapses each family to its `variation == 0` row, but CK tracks
**discovery per `(objectID, variation)`** — and for cattle the variation is the
animal's **colour variant**. So a species the player owns can render `???` on its
var-0 row when it was only ever discovered at a non-0 colour. Measured in-game
(Iter-16.3): the live `SaveManager.SetObjectAsDiscovered` hook logged
`(Cow=1300, var=2)`, and the bake-time membership check read `1300@var0=False,
1302@var0=True, 1303@var0=True` — so the Cow (owned, only seen in colour 2) showed
`???` while the var-0-discovered Goat/RolyPoly showed their names.

This is the deferred **Iter-17** variation-keyed-discovery case (the Iter-21 "H1"),
not a bug in Iter-16.3 — cattle ship one row per species and read CK's native var-0
discovery like any item. **Do not "fix" it with an ever-owned ledger** (that path
was built and removed in Iter-16.3 — it *masks* the symptom by routing collection
through ownership instead of discovery, and produces an incoherent `???`-with-owned-
count state unless the row name is *also* routed through the same flag). The real
fix is per-colour rows using CK's **native** per-variation discovery (cattle, unlike
pet skins, have a native per-variation signal, so no ledger is needed) — Iter-17.
See `docs/architecture.md § Cattle Collection (Iter-16.3)` and the
`reference_ck_cattle_objecttype` memory.

> **Resolved in Iter-17.** Cattle now get one row per colour — 5 fixed slots per
> species, read from each prefab's `ObjectPropertiesCD.PossibleChildVariation[]`
> palette (every species is `{0,1,2,3,4}`). The slots are driven by CK's native
> per-`(id, variation)` discovery (no ledger), and `Entry.IsColourVariant` reveals
> the species name on all slots once any colour is discovered, so the non-0-variation
> `???` symptom is fixed. See `docs/architecture.md § Per-Variation Tracking (Iter-17)`.

## Per-Variation Tracking (Iter-17)

### `RandomObjectEnabler.variations` / `SpriteObject.SpriteAsset.staticVariantCount` are VISUAL-only

While hunting for the cattle colour count, both
`RandomObjectEnabler.variations` and `SpriteObject.SpriteAsset.staticVariantCount`
looked like promising "how many variants does this object have?" sources. They are
**not** — they are sprite/GameObject variant mechanisms (visual randomisation of an
object's appearance) that do **not** set `ObjectDataCD.variation`, so they are
**discovery-irrelevant**: a row is keyed on `(objectID, variation)`, and these never
touch that field. Do not re-chase them as a variant-count source. The real source for
the cattle colour palette was each prefab's
`ObjectPropertiesCD.PossibleChildVariation[]` (property id `239678920`); see
`docs/architecture.md § Per-Variation Tracking`.

### Search-field focus race recurs (Iter-20's fix is incomplete)

When a list refresh races the search field's focus-init (e.g. on open), the caret
blinks but **keystrokes are swallowed** until another widget is clicked. Iter-20
mitigated this by running the scan + `ListView.Refresh()` **before** `OpenModUI`, but
the race **recurred during Iter-17** — so the mitigation is incomplete, not a full fix.
Workaround for the user: click a dropdown (or any widget) first, then click the search
field, and typing works. There are **0 exceptions in the log** when it happens, which
is the tell that it is a focus/timing ordering issue, not a code crash. Still open;
logged to `docs/roadmap.md` for a future re-investigation of the open-time refresh/focus
ordering.

## ECS Scan Performance (Iter-27)

### A synchronous main-thread scan must stay under the frame budget (16.7 ms @ 60 fps)

Any work a mod does **synchronously on the main thread** inside a periodic
`Update`/coroutine tick must finish in **< 16.7 ms** (the 60 fps frame budget) or that
frame is dropped → a visible stutter. The **cadence** of the loop is rarely the
problem; the **per-tick cost** is. Iter-27: the possession refresh runs every 3 s, but a
single scan spiking to ~21 ms blew the budget once every 3 s = "ab und zu ruckelt es",
worst in the base (highest entity density). Diagnose on the **MAX**, not the median
(see `docs/conventions.md § Phase-split PERF probe`): a path whose median fits the
budget but whose worst case exceeds 16.7 ms still hitches.

### Per-entity `GetComponentData` in a hot scan loop is a random-chunk lookup — bulk-read with `ToComponentDataArray`

For a recurring scan over many ECS entities, **do not** call
`em.GetComponentData<T>(entity)` per entity in the loop: each is a random chunk+index
lookup, and over ~1300 entities (Iter-27's loaded-world `ObjectDataCD + LocalTransform`
set) the 2×N lookups were the dominant cost — their cache-cold variance is what produced
the 17–21 ms spikes. Instead **bulk-copy** the components read for *every* entity:

```csharp
using var ents   = q.ToEntityArray(Allocator.TempJob);
using var ods    = q.ToComponentDataArray<ObjectDataCD>(Allocator.TempJob);
using var xforms = q.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
// ents[i] / ods[i] / xforms[i] are index-aligned: same query, captured back-to-back
// with NO structural change between. Index them in the loop, not GetComponentData(e).
```

`ToComponentDataArray` is a chunk-sequential memcpy and is **sandbox-safe** (same
surface as `ToEntityArray`; `safetyCheck=True`). Keep per-entity `em` access
(`HasComponent`/`GetBuffer`/`PetOwnerCD`) only for the **gated minority** that survives
your cheap range/type filter — not for all N. Measured (Iter-27): MAX 21.5→9.6 ms (back
under budget), `loop` phase −48 %, variance collapsed (p90 8.1→3.4 ms), behaviour-neutral
(same entity set, same counts). The rule generalises to any mod reading the live ECS
world on a timer (e.g. an ECS-driven HUD). See `docs/iteration-history.md § Iter-27`,
`docs/architecture.md` (the `PossessionScanner` row), and the
`reference_ck_mod_persistence_and_ecs_access` memory.

## Possession Persistence & World Nature (Iter-28)

### A persisted store that grows unbounded → an autosave `Serialize()` main-thread spike (not the scan)

A mod that persists per-character state in lockstep with CK's save (a Harmony postfix on
`SaveManager.WriteCharacter(int)`) pays the **full serialize cost synchronously on the main
thread at every autosave**. If the persisted structure can grow unbounded, *that* — not the
periodic read/scan — becomes the recurring frame spike. Iter-28: the possession ledger had
grown to 5503 entries / 89 KB, and `PossessionLedger.Serialize()` of it was **12–37 ms**
(serialize 8–24 ms + write 4–13 ms), firing ~10× in a short session. Because it runs on the
main thread it also pushed CK's **host simulation** over its **55 ms** frame budget
(`ServerUpdateFrequencyTracker` warnings: 626/1109 host frames over budget) — felt as
*constant* rubber-banding, distinct from a per-3s scan hitch. **Diagnose the save, not just
the scan:** the Iter-27 PERF data already showed `BuildView` was ~0.8 ms even at 5503 entries,
so the read was never the peak. Budget the autosave serialize like any < 16.7 ms frame op, and
**keep the persisted store small at the source** (the world-nature gate below). A
radius-bounded self-heal (`PruneStaleNear`, then 180 tiles) does **not** retroactively clear a
backlog an old bug accumulated, so Iter-28 paired the source fix with a one-time full eviction
(`PossessionLedger.PruneByPredicate` at the first scan, gated by a `WorldNaturePruned` flag).
**Iter-42 removed that eviction: it was a data-loss bug on two counts** — see the third section
below, which is the durable lesson. The backlog-clearing need was real *then*; it no longer
exists (the Iter-31/41 `v2→v3` discard migrations dropped every pre-gate ledger outright, which
is the safe way to retire a polluted store: throw the whole file away and re-scan, never
selectively delete entries you cannot attribute).

### CK encodes no "world-spawned vs player-placed" signal — don't try to derive it

Iter-28 had to exclude wild nature (bushes/grass/kelp/stalagmites/lilies/ruins) from the
"count the placed object itself" path while keeping placed walls/torches/furniture/trophies/
waypoints. **No object-level signal separates them** — proven over three in-game probe rounds:
`cat`/`stack`/`icon` are uniformly true; `craft` collides (PottedGoldenOrbBush 5589 is
craftable décor → `craft=True` like a torch; Caveling/Slime/Mushroom/Larva trophies are
non-craftable décor → `craft=False` like wild nature); `tags` collide (Stalagmite 5610 and
WaterLily 5614 are **tag-less**, exactly like WayPoint 6514 which must count); and even the
entity-level candidates collide — `DontDropSelfCD` is an `IEnableableComponent` (present on
*everything*; its enabled state isn't sandbox-stable), and `DiggableCD`/`DestructibleObjectCD`
give Stalagmite ≡ CavelingFloorTile 5710 and GraveTree 5622 ≡ WayPoint ≡ Idol 3930 ≡
RuinsPiece 5571. **CK simply does not store the distinction** — it is a real property of CK,
not a search failure. The sanctioned fallback is a **curated, editable tag+ObjectID blacklist**
(`PossessionClassifier.IsWorldNature`): nature tags (`Greenery`/`Destructible`/`CattleKelpFood`/
`Ruins` = stable `ObjectCategoryTag` ints 5/13/33/4) catch the bulk and future tagged nature,
plus a short ObjectID list for the tag-less stragglers. (Iter-20 had removed the `MineableCD`
gate so menu-removable furniture counts — which is what let mineable wild nature in;
`MineableCD` is not a usable discriminator either.) Gate **only the placed-object path** —
container contents + carried are untouched, so nature actually stored in a chest still counts.

### A "one-time" cleanup gated on an unpersisted flag runs on EVERY load (Iter-42)

Two independent defects, both in the Iter-28 eviction above, each sufficient to lose data:

1. **The gate was in-memory only.** `WorldNaturePruned` was a plain `public bool` field on
   `PossessionLedger`; `Serialize()` never wrote it and `LoadFrom()` never set it, so a ledger
   read from disk always started `false` and the "one-time" sweep ran at the first scan of
   **every world load**. **If a cleanup must happen once per store, the "already done" mark has
   to live IN the store** — for this ledger that means the version marker (`#icl-ledger-vN`),
   which is the mechanism the migrations already use.
2. **The predicate could not distinguish what it deleted.** The ledger holds one flat
   `Dictionary<int,int>` per tile, filled by *both* scan path #3 (`AddOne`, the placed object —
   the intended eviction target) and path #2 (`AddBuffer`, container contents — legitimate
   possession). An id-predicate sweep sees only `(tile, id, count)`, so evicting "wild
   Stalagmite" necessarily also evicted **1129 Stalagmite stored in a chest**. The Iter-28
   comment's promise that "legitimately-stored items re-add themselves via the live scan" holds
   **only where the container is observed**, i.e. at base. **Never run a predicate delete over a
   store that does not record provenance** — gate at the write site (as the `IsWorldNature`
   path-#3 gate correctly does) or discard the whole file. (Count-path numbering — #1 carried,
   #2 container contents, #3 the placed object — is defined once at the top of
   `PossessionScanner.Scan`; the Iter-28-era code comments used to number `AddOne` as #1 and
   were corrected in Iter-43.)

**Why it stayed invisible for ~4 weeks, and the test that exposes this bug class.** At base the
next 3 s scan re-observed the containers and `SetLiveContainer` wrote the true contents straight
back, so the deletion was repaired within one scan interval. It only becomes visible when the
world is **loaded far from base**: the containers are unobserved, the deleted entries stay gone
until the player walks back — and the next autosave persists the loss to disk. So for anything
touching remembered/persisted spatial state, **the load-far-from-base case is the test**, not the
load-at-base case; the latter is self-repairing and proves nothing. (Diagnosed with no build at
all, by diffing the ledger against its own `.pugbackup`: 21 ids / 2677 units gone, every one of
them an `IsWorldNature` match and no other — the predicate left its fingerprint in the data.
Cf. § Possession Base Scope & Persistence (Iter-31) below and the
[[feedback_validate_against_savegame]] habit of parsing the real save instead of reasoning
about it.)

### A load that fails "softly" gets persisted over the good file (Iter-43)

The generalisable shape, found by the Iter-42 review in code Iter-42 never touched. A load
returning an **empty store** for a *failure* is not a display bug — it is a delayed **write** bug,
because the next save persists that emptiness over the intact file:

1. `Load` returned a bare empty object for four different outcomes — empty id, no file, `Read`
   returned null (**not even logged**), any exception — so the caller could not tell "new
   character" from "could not read".
2. Nothing checked. The scan then repopulated whatever was live, which **at base looks entirely
   plausible** — the same masking as Iter-42.
3. The write-skip cache is **per-session**, so the first save of a launch always lands: an empty
   store can never hash-match a populated file. ~14 bytes replaced ~14 KB, and the *next* autosave
   overwrote the `.pugbackup` — the last copy.

**Rules that fall out of this, worth applying to any persisted store:**
- **Return a load STATUS, not just data.** "Empty because new" and "empty because broken" must be
  distinguishable at the call site, and a failed load must make the store **read-only** until the
  next successful load. Not saving costs one session; saving costs everything on disk.
- **Set the failure flag BEFORE anything else can throw.** Our `LoadFrom` clears its dicts *before*
  parsing, so a mid-parse throw leaves a **partially** populated store that is indistinguishable
  from a complete one — the worst case, because it looks like success.
- **A silent `null` return is worse than an exception.** The `Read`-returned-null branch had no log
  at all and was the one path no amount of `catch` would have surfaced.
- **Weigh recoverability per store, and fix the unrecoverable one first.** The possession ledger
  self-rebuilds from the player's containers; the pet-skin collection is an *ever-owned* set with
  no second source, so an empty write is final. Same bug, different blast radius.
- **A wholesale-replace write path needs a confirmation predicate.** `_containers[key] = contents`
  deletes everything the previous scan knew. When two producers write one record for *different*
  entities that can leave the observed set independently (a container and a torch sit in different
  DOTS archetype chunks), only the *confirmed* part may shrink it — here: a container entity was
  actually observed on that tile, and the world is past the post-load streaming grace. Gating on
  the grace alone is not enough; that covers loading far from base but not walking away, which is
  the same loss in normal play. **Iter-44 replaced the concrete predicate** (`containerTiles` could
  never be true for cattle/paint aux, so that half could never shrink at all — see the C-1 entry
  below): a removal now needs the grace AND either a container observed on that tile, or the tile to
  be one the scan WOULD have seen anything on (within `PruneRadius` of the player and anchor-covered).
- **Count and report every deletion, even the legitimate ones.** Not one destructive path here
  logged anything, and the diagnostic printed only the *endpoint* after all mutations. Report the
  **transition** (`ledgerC=505->505 lostUnits=2677`) — that one line would have made Iter-42
  self-evident on the first far-from-base load instead of costing a month.
- **Pick an anomaly trigger that cannot false-positive, or don't ship one.** A shrink is normal
  (emptying a chest drops its content), so neither a magnitude threshold nor "any shrink" is usable.
  Trigger on a *shape* that normal play cannot produce — units lost on ≥5 tiles within one 3 s scan.
  A false alarm about data loss is worse than no alarm. **This one was asserted and then refuted
  four ways** — the interval is player-settable to 30 s, the streaming grace batches everything into
  the first scan after it, playing with the mod disabled desynchronises the ledger while saves
  continue, and **CK automation moves items out of chests continuously**, which is precisely the
  benign bulk event on the contents axis that the justification claimed did not exist. Iter-44's
  version scales with the configured interval, suppresses the batched scans (with a much higher
  override bound, because the first post-grace scan is the ONLY one a load-time bug can strike on),
  buckets its dedup key by magnitude and by character, and names automation in the player-facing
  text. The lesson survives, but state such a claim as "no benign event we have MEASURED produces
  this shape" — not as an absolute.
- **Durable beats logged for anything a user must report.** `Player.log` rotates every launch, so a
  warning is gone by the time someone writes the bug report. Persist it (here
  `PossessionIncidentStore` → `mods/ItemChecklist/possession-incidents.txt`) and keep it **ungated**
  by the default-off diagnostics flag — a report that requires prior suspicion reports nothing.

### When four rounds of point fixes each introduce the next bug, the shape is the bug (Iter-42/43 review backlog)

Iter-42 fixed a data-loss bug; its review found four more; Iter-43 fixed those and introduced three
new Criticals **of the same class**; the Iter-43 review found those, with three of four independent
reviewers converging on one root cause. That sequence is itself the finding. The transferable parts,
all paid for:

- **A `bool` that carries a condition across a semantic boundary will be pasted to the wrong side.**
  `allowShrink: allowPrune && containerTiles.Contains(key)` is correct for container contents and
  structurally wrong for aux (cattle colour aux is keyed by an *anchor* tile, and anchors carry
  `CraftingCD`, so they can never be in `containerTiles`). **The two call sites are visually
  identical.** A flag says *what you may do*; it cannot say *which evidence justified it*, so it
  cannot be checked at the place that owns the data. Pass the observation, not the permission.
- **Two collections that must be kept in step by hand will drift the moment they stop being
  symmetric.** `_containers`/`_auxContainers` were hand-paired at ~10 trivially symmetric sites and
  that was survivable; Iter-43 added a **semantic** pairing (one shared correctness predicate whose
  validity depends on which producer wrote the dict) and it was wrong immediately. The line between
  "conventional pairing" and "actively error-prone" is exactly there. **Iter-44 folded both into one
  `TileEntry` per tile.** The site count barely moved — what changed is *which* sites: the ones
  spread across files and interleaved with gating logic (three ledger writers, two flush loops, a
  reconcile pass, two key unions) are gone, and the remainder are adjacent, symmetric, and decide
  nothing. Localisation, not elimination — but the pairs that could disagree about *correctness* no
  longer exist.
- **Calibrate a detector to the failure you MEASURED, not the one you just fixed.** The Iter-43
  anomaly trigger watches the wholesale-replace path (Iter-42's shape). The catastrophe this ledger
  actually suffered was Iter-41's `ledgerC` 402 → 0 through the *prune* — for which there is still
  only a diagnostic line behind a default-off flag. A regression of the measured failure would be
  reported by nothing. **Iter-44 built that channel**, per-scan and cumulative, and the cumulative
  one requires a **net** decline: gross removals are healthy churn (Iter-43's own verification
  measured "8 removed / 7 added in one interval" in a session whose ledger GREW), while the
  historical failure was a net collapse. That distinction paid for itself on the first in-game run —
  60 legitimate prunes in one session, and the net condition correctly stayed silent where a gross
  count would have filed a durable data-loss report.
- **A threshold that depends on a user-settable parameter is not the threshold you documented.**
  "≥5 tiles in one 3 s scan cannot false-positive" was asserted absolutely; the scan interval is a
  player Choice up to **30 s**, the post-load grace batches withdrawals into the first scan after it,
  and playing with the mod disabled desynchronises the ledger while saves continue. Three false-alarm
  paths in a claim of impossibility.
- **A reporting channel must not fail on the fault it reports.** The new durable incident store read
  its own file with a helper that returns `null` both for "absent" and for "present but unreadable" —
  the exact conflation it was built to end, one file deeper — and then rewrote the file from scratch,
  destroying the history. The trigger is *correlated* with the fault being reported, so this is the
  common case for a misbehaving filesystem, not bad luck.
- **A one-shot signal is spent by the benign occurrence.** The "no ECS world" warning is
  once-per-process and fires during every world load, so a genuine mid-play world loss is silent
  forever. Same for a `":session"` dedup key: the first harmless anomaly consumes the slot. Scope a
  one-shot to an *episode*, or bucket the key by severity.
- **Distinguishing "empty because new" from "empty because broken" is not enough — a parser that
  never throws produces a third state.** The status flag added for exactly this catches a throw and
  a null read, but a file truncated after its header parses to a *subset*, reports success, and gets
  written back. Validate the body (a declared count, a checksum, or a skipped-line counter), not just
  the header.
- **Convergent independent reviewers are strong evidence; a single one is a hypothesis.** Three
  reviewers arrived at the same root cause and two proposed the identical fix, having been given the
  suspicion in different words. Conversely each also refuted suspicions of mine — the refutations
  (listed in the roadmap's Iter-44 entry) are as valuable as the findings, because they stop the next
  round from re-investigating settled ground.

### Iter-44: what the structural rebuild taught, on top of the above

- **"The write call did not throw" is not "the write landed."** CK's `StandaloneFilesystem.Write`
  ends in `catch (IOException) { Debug.LogError(...) }` with **no rethrow**, and its inner
  `File.Replace`/`File.Move` retry loop gives up after ten attempts with only a `LogError`. So the
  entire `IOException` class — disk full, a locked file, the Wine faults this project ships six IL
  patches for — is invisible to a mod. Worse, our FNV save-write-skip then cached "the disk holds
  this" for content that was never written, so every later save with unchanged content was **skipped**:
  one poisoned cache entry could suppress saving for a whole session, and for the pet store
  `ClearDirty()` additionally cancelled the retry its own placement after the write was meant to
  guarantee. **Verify by reading back before you record a write as done** — and note this was
  invisible from the mod's own source. It took reading the decompile.
- **One missed observation is not evidence of removal.** A container's chunk can be absent from a
  single ECS query while it still exists; a penned animal can wander out of `AnchorRadius` for one
  scan or vanish briefly during growth churn. Acting on the first miss produced a flickering count
  that froze at the wrong value if the player then left. Requiring the **immediately preceding** scan
  to have missed the same key too costs one interval and removes the whole class. Two details matter:
  the marks must be **per key** (one per tile lets a neighbour's miss spend another key's grace, which
  is routine where several keys share a tile) and **adjacent** (otherwise "the previous scan" means
  "the previous scan that looked at this tile", which can be an hour and a teleport earlier).
- **Apply such a delay to EVERY removal path or it is cosmetic.** The delay first went only into the
  merge. But on most tiles the container is the *only* producer, so when its chunk flickers the tile
  is not in the observed set at all, the merge never runs, and the *prune* takes the whole tile in one
  scan. The rule was protecting the rarer shape and leaving the common one exposed.
- **A multi-call protocol cannot be enforced at compile time in this language subset — so remove the
  protocol.** Three shapes were tried: a permission bool per write (the caller owned a rule it could
  not see), four evidence bools per tile (three derivable, one a duplicated predicate), then
  `BeginScan`/`Publish`/`Prune` over ledger-held state — where a harness found that a publish *after*
  the prune still shrank, because the "is a scan open" flag was a warning trigger and not part of the
  authorization. Patching the condition was not the lesson. With a **single entry point** taking the
  whole snapshot, "no scan is open" and "the prune was skipped" stop being representable.
- **A stable bookkeeping key must not be derived from something that moves.** Cattle colour aux was
  keyed to "the anchor nearest the animal", called stable because anchors do not move. The animal
  does. Measured: ~12 tiles added and ~12 removed per save interval against 11 such tiles in
  existence. That silently broke the Iter-31 save-write-skip for farm bases, and once the miss delay
  existed it also double-counted colours. The fix is a key that does not depend on the moving thing at
  all (the lowest packed anchor key of the scan) — and the "location" was safe to give up because
  nothing reads it.
- **A running game is not a stable measurement subject.** Twice during verification an intermediate
  read produced a wrong conclusion — "no DIAG lines, so diagnostics was off" (the log was still being
  written) and "the in-memory tile count exceeds the file's, so a save was wrongly skipped" (the file
  was four minutes younger than the process). Both would have been written up as bugs. **Quit the
  game before reading the log and the ledger**, or you are comparing two different points in time.
- **Extract the pure-logic core and test it offline; it finds what reading does not.** The ledger and
  the pet collection need no ECS, no Harmony, and no Unity API beyond `Debug.LogWarning` and
  `Vector2`, so ~40 lines of stubs make them runnable outside the game
  (`tests/possession-harness`). It caught two defects that four review agents reading the same code
  did not. Compile the real sources into the harness rather than copying them — a copy drifts, and a
  drifting test is worse than none.
- **A test protocol finds what it asks about; a data diff finds what happened.** The in-game protocol
  asked whether a colour count still flickered *downward* (the regression just fixed). The ledger diff
  showed a drift *upward* plus the hopping tiles — a different defect, in the opposite direction,
  that no protocol step named.

### Iter-45: adding provenance to a persisted record

- **A green test that picked the convenient configuration is worse than no test.** The migration test
  passed a `containers` set, which forces `absenceIsConfirmed` — the ONE case where the provenance
  correction lands atomically. So it asserted a behaviour the shipped code did not have, and both
  reviewers spotted it independently. When writing a test for a new rule, use the shape that occurs
  MOST, not the one that makes the assertion easy: here a placed-only tile, which is ~99 % of the
  ledger per Iter-28's measurement.
- **A migration that ADDS where it should MOVE double-counts, and the correction looks like a loss.**
  Loading a v3 line's contents into `stored` and letting the first observation add to `placed` left
  both populated until the shrink rule expired the stale one — a doubled count meanwhile, and
  permanent for any tile observed from beyond the shrink envelope (ordinary play at a base, since
  anchors reach ~91-115 tiles). Worse, the eventual removal was booked as `DroppedUnits`, so the
  anomaly detector would have written "N owned unit(s) vanished — please report this file" on the
  first post-update scan of every real base. **Re-filing is bookkeeping, not a removal: do it before
  the merges and do not count it.**
- **When one field is the SUM of two, the exact correction is subtraction, not a guess.** A v3 count
  was `stored + placed`, so an id observed as placed accounts for exactly that much of it. 1 + 1 was
  written as 2; observing 1 placed leaves 1, and the chest's copy survives. No heuristic, no
  tolerance — but it must be gated on "this record is still the migrated assumption", because on a
  verified record the same subtraction deletes real data whenever the container happens to be
  unobserved.
- **Carry migration uncertainty in the SHAPE, not in a new field.** An unverified tile is written as
  a v3-shaped three-segment line, so the uncertainty survives a save with no extra format surface
  and no extra parser branch. Without that, a tile the player has not revisited since the update
  hardens into a split nobody verified.
- **Put the new segment LAST and the migration is nearly free.** v4 is `x,z|stored|aux|placed`, so
  v3's three fields keep their meaning and ONE parser reads both. Inserting it in the middle would
  have forced either two parsers or a discard — and a discard costs every player a full re-scan.
- **A mandatory field must be mandatory under the marker that promises it.** The `#n=` count was
  first treated as "absent ⇒ accepted unchecked" for every file. But only a v4 writer emits it, so
  under the v4 marker its absence IS damage — and that absence is exactly what a truncation after
  line 1 produces, which otherwise loaded as a clean, WRITABLE, EMPTY ledger.
- **A damage detector must not double-report.** A malformed line fails the parse AND makes the tile
  count fall short of the declared one; reporting both doubles the number the incident quotes to the
  player as "lines that could not be read". Subtract every line that yielded no tile — including one
  that merged into an existing tile, where the naive check read a concatenated file as damaged
  although the merge path exists to SALVAGE such a file.
- **If a fix removes a capability, the diagnosis was too coarse.** "The tooltip must count containers,
  not everything" reads like the fix for a wrong claim — and silently removed the locate arrow for
  every placed object. The arrow was always correct; only the WORDING was wrong. Split the READ
  (both provenances, for the arrow) from the WORDING (containers only), rather than narrowing both.

## Possession Base Scope & Persistence (Iter-31)

### Workbench = the semantic "is this the player's base?" discriminator

CK has **no base concept** and (per the Iter-28 lesson above) **no world-spawned-vs-placed
signal**, so neither position nor cluster-density tells a base from a world structure: a
fixed radius around the player misses an outbuilding (Iter-20), and "≥ 2 crafting stations
nearby" mis-fires because CK world structures *pack* functional stations — an abandoned camp
has a campfire + cooking pot, a mechanical vault a seed extractor + generator — so they pass a
cluster filter and anchor their loot as "owned". The clean discriminator is **semantic, and
CK does encode it**: a base is built around a **Workbench** (the universal first build), and
CK places **no** workbench in any world structure. Validated against a real save: **11
workbenches, all at the Core base; 0 in any remote cluster.** So anchor on workbenches + the
stations within a workbench's radius (link **workbench→station only**, never station→station,
so the base can't chain out to a far structure; a single workbench suffices → no cluster count
needed). Reusable for any CK mod that needs "does the player own/control this place?" — reach
for a **player-built marker object** (workbench) before any spatial/type heuristic.

### Hash width is the safety knob for a skip-if-unchanged persistence guard

When you elide an expensive write because the serialized content is unchanged (Iter-31:
`PossessionStore` skips the 5–13 ms Wine disk write when the freshly serialized ledger hashes
to the last-written value), a **hash collision = a skipped needed save = silent data loss**,
so the hash width *is* the data-safety margin. Use **FNV-1a/64** (collision ≈ 1/2⁶⁴ per save —
negligible):
- **Not** 32-bit `string.GetHashCode` — 1/2³² is not negligible across a long session of
  saves, and it isn't even stable across runtimes.
- **Not** SHA/MD5 — cryptographic strength is irrelevant (you're hashing your own data, not
  defending against an adversary), it is heavier per byte on a **main-thread** path, it
  **allocates** a `byte[]` (FNV is a zero-alloc `ulong` of pure value-type arithmetic), and
  `System.Security.Cryptography` is exactly the BCL surface the Roslyn sandbox bans — so it
  would `CompileFailed` the mod anyway. FNV has zero BCL dependency.

Record the hash **only after a successful write**, and let the first save (no prior hash) always
land — so the skip path can never drop the initial persist or a write that failed.

### Validate an inference against the actual savegame/ledger, not your own reasoning

The possession ledger on disk (`possession-<guid>.txt`, ASCII `x,z|id:count` per container) is
a **first-class diagnostic data source** — parse it; do not reason about what it "should"
contain. Twice during Iter-31 an "those OreBoulders are inside the 48-tile base radius"
inference was asserted from memory and was **wrong** — parsing the ledger proved the entries
sat **337–693 tiles** from the Core base (remote world content the player had merely explored
past: Sunken-Sea coral/jellyfish, abandoned-camp furniture, a vault's farm seeds, and loot
inside world chests never opened). The on-disk file beats the inference every time the two can
both be reached; let it redirect the fix (here: from "blacklist those boulders" to "the anchor
model itself is wrong"). (Memory: `feedback_validate_against_savegame`.)

### "GarbageCollector disposing of ComputeBuffer" is usually a SHUTDOWN artifact — check WHERE it fires

A frame-hitch correlated with exploring/enemies is easy to misattribute. Iter-31's "lag spike
outside base" looked like a textbook case: the log showed **40× "GarbageCollector disposing of
ComputeBuffer"**, and the leading theory was a GPU leak in *another* mod's bundled render asset
(no `.cs` references `ComputeBuffer`, so it would hide inside a prefab/material bundle, not
source). **Both halves of that theory were wrong** — and how they were caught *is* the lesson:

- **Check the log POSITION of the GC warnings.** All 40 sat in the **last 40 lines** of the log,
  immediately after `Input System module state changed to: Shutdown` — they are the GC's
  **process-exit cleanup** of buffers never explicitly `Release()`d, not mid-play hitches (a
  mid-play GC would *spread* the warnings through the log, not bunch them at the very end).
  Benign. They never touched a gameplay frame.
- **Isolate by toggling + the player's feel, not by a count.** The suspected render mod was
  disabled via `state.json`'s `disabledMods` array (NOT the in-game mod menu — that triggers a
  mod.io resync that deletes fake-ID dev entries; edit `…/mod.io/5289/state.json`, add the modId
  string to `existingUsers/<uid>/disabledMods`, keep it in `subscribedMods`). The next session
  ran **with no spikes** — *that* confirmed the culprit (a per-enemy render mod, by its rendering
  cost), while the ComputeBuffer count was **unchanged at 40**, proving those warnings were never
  the cause.

Lesson: don't promote a GC/leak warning to "the gameplay lag" without checking its **log
position**; and isolate a render mod by **disable + subjective smoothness**, not by a GC count.
(Prove your own innocence first: `grep -rn ComputeBuffer unity/` — ItemChecklist's is zero.)

### Possession scan: "loaded" ≠ "observed" — the prune must key off the observation boundary (Iter-41)

For any spatial "is it still there?" self-heal (`PruneStaleNear`), the load radius is the **wrong**
threshold. Ground truth from the decompile: the player carries
`KeepAreaLoadedCD { KeepLoadedRadius=300, StartLoadRadius=250, ImmediateLoadRadius=200 }`
(`Pug.Base` `PLAYER_DISTANCE_TO_LOAD=200/…START=250/…UNLOAD=300`; `defaultSimDistance`/
`SimulationDistance` are dead → the bubble is **not shrinkable by any setting**). So CK force-loads
chunks within ~200 of the player. **But** the mod's scan resolves the **ServerWorld**, and base
placed-object entities empirically leave the *observed* scan set at only **~91–115** — well below
200 (best explanation: DOTS ArchetypeChunk unload granularity, so a container can leave the query
while its co-located workbench stays; possibly a camera-frame offset). The Iter-41 bug was exactly
this conflation: the prune ran at `LoadRadius = 180` (chosen "< 200 = loaded"), so it deleted
loaded-**but-unobserved** base containers in the 91–180 band as the player walked away, wiping the
remembered ledger (Possession `K` collapsed 385→40).

**Rule:** a "not observed ⇒ destroyed" prune must fire only where a present object *would* be
observed = **loaded AND would-pass-the-scan's-gate**. The Iter-41 airtight form: prune a remembered
tile iff `dist(player) ≤ 48` (loaded — small, below the ~91 dropout AND the 200 floor; also above
destruction range) **AND** `coveredByLoadedAnchor` (the same `WithinAnchor(anchors, …)` gate the
scan uses) **AND** `∉ liveKeys`. Diagnose with the DIAG `maxSeen`/`minGhost` probe + a prune-off
control run (if `K` is then stable, the prune is the sole cause). Full CK constants + file:line in
the `reference_ck_entity_load_observe_radii` memory. Corollary: **mobile** entities (penned cattle)
must not be keyed by their transient tile — key by the nearest **anchor** tile (stable) or they
accumulate a stale aux entry per tile visited in the 48–~91 ring (a self-healing per-colour
over-count).
