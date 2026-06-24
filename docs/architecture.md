# ItemChecklist Architecture

This document describes the design and data flow of the ItemChecklist mod
beyond what fits in the mod's `CLAUDE.md` overview.

## UI Architecture

ItemChecklist uses Core Keeper's native `SpriteRenderer`-based UI stack,
mediated by CoreLib's `UserInterfaceModule`. This is the only viable approach
in CK — uGUI (Canvas/Image) is structurally incompatible because CK's
`UIMouse` does a `Physics.Raycast` in the UI layer and Canvas elements have no
Collider. See `CLAUDE.md § Mod-Specific Gotchas` for the structural
explanation and the survey of 10 CK UI mods (all use SpriteRenderer).

### Mod Load → Registration → Mount → Open → Close

**Mod load + registration (EarlyInit → ModObjectLoaded):**

1. `IMod.EarlyInit` — `UserInterfaceModule.LoadSubmodule()`. Must precede
   any `RegisterModUI` call.
2. `IMod.ModObjectLoaded` — called once per loaded mod `GameObject` (from the
   AssetBundle). Routing is a **name whitelist** (Iter-13): only
   `ItemChecklistWindow` is registered as a modal UI via
   `UserInterfaceModule.RegisterModUI`; `ItemChecklistHUD` is captured for lazy
   instantiation (see § Mount (non-modal)); everything else is logged and
   skipped. The building-block prefabs (`Dropdown` skeleton, `Sort`, `Filter`) are
   nested inside the window and never opened standalone, so they must not be registered
   as modal UIs. (Since Iter-13 the `Dropdown` chrome is component-less and no
   longer arrives as a top-level loaded object anyway, but the whitelist makes
   the routing explicit and defensive.)
3. CoreLib's postfix on `UIManager.Init` instantiates the prefab into
   `UIManager.chestInventoryUI.transform.parent` — this is the canonical
   CK UI mount point used by every IModUI implementation.

**Open path (F1 → visible window):**

4. F1 is a toggle (Iter-4). The keybind (wired since Iter-1 via the CoreLib
   `ControlMappingModule`, polled in `IMod.Update`) drives a 3-way branch
   keyed off *real visibility* (`ItemChecklistWindow.Instance.Root.activeSelf`,
   **not** CoreLib's `currentInterface`, which the auto-hide patch can leave
   transiently stale):
   - window visible → close via
     `Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false)` (the same
     path Escape/E use — see Close path);
   - else a Vanilla menu/inventory is open, a text field/chat is focused,
     or the world is not playable
     (`Manager.menu.IsAnyMenuActive()` / `Manager.ui.isPlayerInventoryShowing`
     / `Manager.input.textInputIsActive` / chat focus /
     `!WorldState.IsInPlayableWorld`) → ignore, so F1 never opens on top of one;
   - else → `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`.
5. CoreLib calls `ItemChecklistWindow.ShowUI()`.
6. `ShowUI` delegates to `ItemChecklistContent.PopulateContent` (Iter-3.8):
   it lazily grows the fixed row pool (`EnsurePool`), sets the catalog count
   (`SetCount`), reflection-wires the scrollable + invokes `UpdateScrollHeight`,
   then `ResetScroll` + forced `RefreshVisible`. No per-entry instantiation
   happens anymore — the persistent ~N-row pool is recycled by the scroll
   system. See § Viewport Virtualization and the UIScrollWindow Reference
   below.

**Close path (Escape / E / F1 → hidden window):**

7. Player presses Escape or E, **or** F1 while the window is open →
   `HideAllInventoryAndCraftingUI` is called (F1 via the Iter-4 toggle,
   with `forceClose: false` to mirror `PlayerController.CloseAnyOpenInventory`).
8. CoreLib's postfix on `HideAllInventoryAndCraftingUI` calls
   `IModUI.HideUI()` for every registered mod UI **and** clears
   `UserInterfaceModule.currentInterface` (via `ClearModUIData`). Clearing
   it releases the player from menu-state — a bare `HideUI()` on the F1-close
   path would leave `currentInterface` dangling and freeze movement.
9. `ItemChecklistWindow.HideUI()` calls `root.SetActive(false)` only
   (Iter-3.8) — the persistent row pool is **not** destroyed on close. The
   `PugText.Clear()` pool-teardown (the old per-destroy leak fix) moved to
   `ItemChecklistContent.OnDestroy`, which runs only on full pool teardown.

**Mutual exclusion with Vanilla menus (Iter-4):** `InventoryOpenAutoHidePatch`
postfixes `UIManager.OnPlayerInventoryOpen` — the single funnel every Vanilla
inventory/crafting/vendor open routes through (plain TAB via
`PlayerController.OpenPlayerInventory`; chests/stations/vendors via wrappers
that all delegate to it). If the checklist is visible when a Vanilla menu
opens, it calls a **bare** `HideUI()` (not `HideAllInventoryAndCraftingUI`,
which would re-close the just-opening menu). The briefly-dangling
`currentInterface` is harmless: the Vanilla menu covers it and its close
clears it, and the F1 toggle reads `Root.activeSelf`, not `currentInterface`.
This coherence holds **only** while `ShowWithPlayerInventory == false` — that
early-returns `OpenModUI` before `OnPlayerInventoryOpen`, so opening the
checklist does not trip its own postfix.

**Cursor/WASD-block/Escape handling:** all handled by CoreLib and CK's
`isAnyInventoryShowing` postfix chain — zero patches for those. *Iter-4's own*
contribution to the patch set is one Harmony postfix (the mutual-exclusion patch
above) — this is not a running total. The mod now ships ~12 top-level Harmony
patch/hook files across later iters (Iter-9 suppression patches, the Iter-24
`MainListWheelSuppressPatch`, the discovery/save hooks, …).

### Pattern Matrix (10 surveyed CK UI mods)

| Approach | Mods using it |
|---|---|
| SpriteRenderer + Layer GUI + UIelement | 10 / 10 |
| uGUI (Canvas, Image, Text) | 0 / 10 |
| CoreLib UserInterfaceModule | ~3 (BookMod, DummyMod, ItemChecklist) |
| Custom Harmony-based open/close | ~7 |

Production reference implementations:
- **limoka/BookMod** — ~145 IMod LoC + ~162 UI LoC, uses UserInterfaceModule
- **limoka/DummyMod** — ~87 IMod LoC + ~84 UI LoC, minimal template

---

## UIScrollWindow Reference

This section captures the decompile findings for `UIScrollWindow` (in
`Pug.Other.dll`). Key behavioral facts that affect ItemChecklist are also
summarized in `CLAUDE.md § CK Decompile References`; this section gives the
full internal picture for anyone implementing a new `IScrollable` or
diagnosing scroll behavior.

### Awake Logic

`UIScrollWindow.Awake()` copies its **serialized public `scrollable` field** to
its private `_scrollable` — so the prefab's `scrollable` reference (pointing at
`ItemChecklistContent` on the same GameObject) is the single source of truth for
the wiring. If that field does not resolve to an `IScrollable`, the scroll window
**permanently disables itself** (`enabled = false`) and stops processing input.

Because `Awake` does the copy itself, the mod no longer reflects into the private
`_scrollable` (R4, Iter-14.2): the former `API.Reflection.SetValue(MiScrollable, …)`
calls were redundant and have been removed (`MiScrollable` cache + `System.Linq`
dropped with them). All that remains after a content change is the
`UpdateScrollHeight` + `ResetScroll` rewire — see § Post-Content-Spawn Sequence.

### UpdateScrollHeight

```
scrollHeight = scrollable.TotalHeight - scrollWindow.windowHeight
```

Called via reflection (`API.Reflection.Invoke(MiUpdateScrollHeight, ...)`).
Must be called after content changes before `SetScrollValue`, or the scroll
range is stale.

### SetScrollValue Semantics (inverted)

`SetScrollValue(float normalizedScrollValue)`:

- `1f` → **top** of list: lerps content `localY` toward `minScrollPos = 0`.
- `0f` → **bottom** of list: lerps content `localY` toward `ScrollHeight`
  (content shifted up by full scroll height).

This is counter-intuitive. Iter-3 passed `0f` and caused rows to overlap
the title element (content shifted ~20 units up). **Always use `1f` or
`ResetScroll()` to go to the top.**

`ResetScroll()` is a public method equivalent to `SetScrollValue(1f)` — use
it as the canonical "go to top" call.

### Post-Content-Spawn Sequence

After spawning or replacing scroll content, rewire the scroll height. Since R4
(Iter-14.2) the scrollable wiring comes from the prefab's serialized `scrollable`
field (copied by `UIScrollWindow.Awake`), so the sequence collapsed from the
three-call ItemBrowser `EntriesList.SetEntries` pattern (SetValue +
UpdateScrollHeight + SetScrollValue) to one helper, `RewireScrollHeight()`:

```csharp
private void RewireScrollHeight()
{
    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);  // private → reflection
    scrollWindow.ResetScroll();   // == SetScrollValue(1f): go to top
}
```

Order still matters: `UpdateScrollHeight` **before** `ResetScroll`.

### LateUpdate Scroll Processing

`UIScrollWindow.LateUpdate` applies the current scroll delta to
`content.localPosition.y`. It reads mouse scroll wheel input only when the
cursor is inside the window bounds (checked via `bounds.Contains`).

### IScrollable Contract

```csharp
public interface IScrollable
{
    void UpdateContainingElements(float scroll);  // per-frame recycle callback
    bool IsBottomElementSelected { get; }
    bool IsTopElementSelected { get; }
    float GetCurrentWindowHeight();
    float TotalHeight { get; }        // negative: grows downward from 0
}
```

**Key implementation notes:**

- `UpdateContainingElements(float scroll)` is the per-frame callback the
  scroll system invokes with the current scroll offset. IB's `EntriesList`
  treats it as a no-op (content manages itself), but ItemChecklist's
  `ItemChecklistContent` uses it as the **viewport recycle driver** — see
  § Viewport Virtualization. Do not mistake it for arg-less.
- `TotalHeight` is negative and grows more negative as rows are added.
  Row placement formula: `row.localY = TotalHeight` before adding each
  row's height. TotalHeight starts at 0 and goes negative.
- `GetCurrentWindowHeight()` returns the window's clipping height in world
  units (used by `UpdateScrollHeight` formula).
- Hierarchy comparison:
  - **IB:** `ScrollWindow` → `ScrollContainer` → individual row GameObjects
  - **ItemChecklist:** `RowsContainer` → `Content` → individual row GameObjects
  Both hierarchies work; the key is that `Content` is the object whose
  `localY` is manipulated by the scroll system.

---

## Viewport Virtualization (Iter-3.8)

The catalog grows to ~10,910 entries. The pre-Iter-3.8 design instantiated one
`ItemRow` GameObject per entry on every open (`SpawnRows`), which froze the
window ~905 ms. Iter-3.8 replaced that with a fixed-size pool of row
GameObjects recycled as the user scrolls, so the GameObject count is bounded
by the *visible* window, not the catalog size. Open latency dropped from
~905 ms to ~0–7 ms.

**Why hand-built — CK ships no recycler template.** `CookBookUI` (CK's own
cooked-food browser) is **not** a viewport recycler: it builds a *fixed* pool
of `MAX_ROWS × MAX_COLUMNS` slots (50×5 = 250) once and breaks at
`num >= itemSlots.Count`, so entries past slot 250 are simply never shown. It
scrolls by translating the whole pool under the clip mask, recycling nothing.
That is fine for ≤250 recipes but unusable for ~10,910 entries. No CK class
recycles rows by index, so ItemChecklist implements its own on top of the
`IScrollable` contract.

### Pool model

`ItemChecklistContent` (the `IScrollable` implementor, which sits on
`scrollingContent`) owns a fixed pool of

```
N = ceil(windowHeight / RowHeight) + 4     // RowHeight = 1.5 since Iter-9 (read from the prefab background at Init); see § Item-Row Layout (Iter-9)
```

row GameObjects. The pool only ever *grows* — `EnsurePool` grows toward
`ComputePoolSize()` and never early-returns short (an early-return bug would
leave the pool undersized after a window resize). Pool rows are children of
`scrollingContent` and are positioned at content-local `-(idx * RowHeight)`.

### Recycle driver — `UpdateContainingElements(float scroll)`

The scroll system invokes `IScrollable.UpdateContainingElements(scroll)` every
frame with the current scroll offset. `ItemChecklistContent` uses it as the
recycle driver:

```
firstIndex = floor(scroll / RowHeight)
if firstIndex == _lastFirstIndex: return    // per-frame no-op guard
_lastFirstIndex = firstIndex
// rebind each of the N pool rows to catalog entry (firstIndex + i)
```

The `_lastFirstIndex` guard skips the rebind when the first-visible index has
not changed since the last frame (most frames). A separate `RefreshVisible()`
forces an unconditional rebind (it resets `_lastFirstIndex = -1` first), used
on window-open, on a discovery event, and on re-bake — cases where the catalog
*contents* changed even though the scroll offset did not.

### Full-height reporting

Only ~N row GameObjects exist, but the scrollbar / scroll range must reflect
the whole catalog. `ItemChecklistContent.GetCurrentWindowHeight()` returns

```
count * RowHeight
```

i.e. the height the full catalog *would* occupy. `UIScrollWindow.UpdateScrollHeight`
(`scrollHeight = TotalHeight - windowHeight`) then computes a scroll range that
spans every entry, so scrolling reaches the last row even though no GameObject
exists for it until it scrolls into view.

### Open/close lifecycle

`ItemChecklistWindow` no longer spawns or destroys rows; it delegates to
`ItemChecklistContent.PopulateContent`:

1. `EnsurePool` — lazily grows the pool to `ComputePoolSize()` (grows-only).
2. `SetCount(catalogCount)` — records the full entry count for height reporting.
3. `RewireScrollHeight()` — invoke `UpdateScrollHeight` (reflection) then
   `ResetScroll()` (see § Post-Content-Spawn Sequence). Order matters:
   `UpdateScrollHeight` **before** `ResetScroll`. Since R4 (Iter-14.2) the
   scrollable wiring is no longer re-applied here — it comes from the prefab's
   serialized `scrollable` field, which `UIScrollWindow.Awake` copies itself.
4. `RefreshVisible()` — forced rebind so the pool shows the correct entries.

`ItemChecklistContent.Awake` no longer self-registers into the scroll window
(R4): the former self-registration was an ineffective guard (it checked the
public `scrollable`, not the private `_scrollable`), and the prefab field already
supplies the wiring. `DefaultExecutionOrder(-100)` is kept defensively; `Awake`
now only caches the `UIScrollWindow` reference.

`HideUI` no longer destroys anything — it calls `root.SetActive(false)` only,
so the pool survives across opens. The `PugText.Clear()` pool-teardown (the
former per-destroy leak fix) moved to `ItemChecklistContent.OnDestroy`, which
runs only on full pool teardown. Because rows are no longer destroyed per
close, the old main-menu-PugText-blanking symptom can no longer occur.

> **Load-bearing invariant:** `UIScrollWindow.Awake` sets `enabled = false`
> *permanently* if its serialized `scrollable` reference does not resolve to a
> component on the same GameObject. The doc-comment recording this in the code
> is load-bearing — do not remove it.

### Prefab geometry invariant

For the first and last rows to sit flush against the window edges:

- `UIScrollWindow.windowHeight` **must equal** the SpriteMask height.
- The mask top **must align** to row 0's top.

To grow the mask **top-only** (keeping the bottom edge fixed), change both the
scale and the position in a 2:1 ratio:

```
mask.scale.y    += X
mask.localPos.y += X / 2
```

Changing only one of the two shifts the whole mask instead of extending it
upward. This was settled by empirical 4-build calibration, not by a static
reading of the prefab — the Iter-3.8 values were `windowHeight = 6.5`,
mask `scale.y = 6.5`, mask `localPos.y = -2.0`.

**Iter-9 enlargement.** The window was sized to near-fullscreen with a thin
**uniform border matching CK's inventory margin** (0.25 world-units). Final
values (committed): background + root collider `29.5 × 16.375` (mask width 28.5), `windowHeight =
13.75`, mask `scale.y = 13.75` / `localPos.y = -5.625` (`windowHeight` ==
mask `scale.y`, preserving the flush invariant), `RowsContainer.y = 3.75`, row
width `27.5`. The window is centered in the camera's **30 × 16.875-unit viewport**
(measured once via a temporary log of `Manager.camera.uiCamera.orthographicSize`
= `8.4375`, aspect 16:9; `world_height = 2·orthoSize`, `world_width = height·aspect`).
**No runtime sizing logic:** CK's orthographic UI camera shows a constant world
area regardless of resolution, and CK has no UI-scale option, so a fixed prefab
size is "fullscreen with border" on every resolution (empirically confirmed).
A later Iter-9 slice changed `RowHeight` from the pre-Iter-9 `2.5` to **`1.5`**
(now read from the row prefab background at `Init` — see § Item-Row Layout
(Iter-9)), so the recycled pool auto-grows via `ComputePoolSize` to fit the
denser rows. Whole-row flush (row 0's top pinned to the mask top) was settled in
the § Item-Row Layout slice.

### Scrollbar (Iter-5)

The window prefab now wires CK's native `ScrollBar` + `ScrollBarHandle` into
`UIScrollWindow.scrollBar`. This is a **pure prefab change — no mod C#**: once
`scrollBar` is set, `UIScrollWindow.LateUpdate` calls `UpdateScrollbar()` every
frame the scroll position or height changes, which calls
`ScrollBar.UpdateScrollBarPosition(normalizedPosition)` — driving handle
sizing, position, **and** mouse-wheel sync. (Verified against Item Browser,
whose working scrollbar has zero scrollbar C#.) Mouse-wheel scrolling already
worked before; the new wiring just makes the handle reflect it.

**Prefab subtree** (under the window `root` GO): `ScrollBar` GO (holds the
`ScrollBar` component; localPos `(4.9, -0.5, 0)`) → `ScrollBarRoot` child
(= `ScrollBar.root`, the GO CK toggles) → `ScrollBarTrack` (track
`SpriteRenderer` = `ScrollBar.background`, size `(0.25, 6.5)` = viewport
height) + `ScrollBarHandle` (`ScrollBarHandle` + 3D `BoxCollider` =
`ScrollBar.handle`) → `ScrollBarHandleSprite` (= `handleSpriteRenderer`) +
`ScrollBarHandleSelected` (= `optionalSelectedMarker`). Sprites are the
`ui_scrollbar_*` sub-sprites of the `ui_classic` atlas.

**Three load-bearing facts (proven during Iter-5 builds):**

1. **`maskInteraction: None` on every scrollbar `SpriteRenderer`.** They sit at
   sorting orders 46/47/48 inside the SpriteMask custom range (40..55), so
   without `maskInteraction = None` the row mask would clip them. `None`
   exempts them entirely.
2. **`handleCollider` is a 3D `BoxCollider`** (`!u!65`), not `BoxCollider2D` —
   CK's `UIMouse` does a 3D `Physics.Raycast`. Size `(0.5, 1.25, 4)`; the `y`
   is overwritten each frame by `ScrollBar.UpdateHandleSize`, the `z: 4` is
   raycast depth.
3. **`ButtonUIElement.LateUpdate` toggles GameObject *activity*** of
   `spritesShownUnpressed` (active when `!leftClickIsHeldDown`) and
   `spritesShownPressed` (active when held). The same GO must never be in both
   lists (the pressed loop runs last and wins, so it would only show while
   held → handle vanishes at idle). With a single handle sprite, both lists are
   left **empty**: `handleSpriteRenderer` is rendered independently by
   `ScrollBar` and stays always-visible, while the selected-border shows on
   hover/selection via `optionalSelectedMarker` (toggled by
   `OnSelected`/`OnDeselected`).

For hand-wiring the CK component `m_Script` refs (portable fileID, install-local
guid), see the `project-corekeeper-script-fileid-derivation` memory. Scroll
arrows stay unwired (`arrowUp`/`arrowDown` = `{fileID: 0}`); track-position
fine-tuning + real sprites are deferred to Iter-12 (pixel-art).

---

## Data Architecture

### ItemCatalog Three-Loop Bake (Iter-3.7 / Iter-16.1)

`ItemCatalog.Bake()` runs once per world-load, triggered from the
`PlayerController.OnOccupied` coroutine (after
`ClientWorldStateSystem.HasRunAtLeastOnce`).

**Pre-cache phase** (before the loops):

```
turnsIntoMap: ObjectID → ObjectID   (ingredientLookup[entity].turnsIntoFood)
tierMap:      ObjectID → (base, rare, epic)  (from CookedFoodCD fields)
```

**Loop 1 — Standard items:**

```
for objectId in PugDatabase.objectsByType.Keys:
    if IsCookedFood(objectId): skip   // handled by Loop 2
    emit CatalogEntry(objectId, variation=0)
```

**Loop 2 — Cooked-food α-enumeration:**

```
for i1 in ingredients:
    for i2 in ingredients where i2 >= i1:   // upper triangle, symmetric
        family = turnsIntoMap[GetPrimaryIngredient(i1, i2)]
        var = GetFoodVariation(i1, i2)
        for tier in [base, rare, epic]:
            emit CatalogEntry(tier_objectId, variation=var)
```

**Resulting catalog size:** ~10,910 entries (~1240 standard + ~9480
cooked-food permutations: 3160 pairs × 3 tiers).

**Expected bake time:** < 200 ms on a typical machine (empirically ~384 ms
on this machine for the full ~10,910-entry bake). Bake time is independent
of the Iter-3.8 open/render-time work: Iter-3.8 virtualized the row
*rendering* (the open-latency fix — see § Viewport Virtualization), not the
catalog bake. The bake still runs once per world-load in the
`PlayerController.OnOccupied` coroutine.

### DiscoveredState Packed-Long Key Schema

`DiscoveredState` stores discovered items as `HashSet<long>` where each
key is a packed long: `(objectId << 32) | (uint)variation`.

**Design rationale:**

- GC-free: boxing-free value type, no `(int, int)` tuple allocation per
  lookup.
- Negative-variation-safe: casting to `uint` before OR-ing prevents sign
  extension from corrupting the upper 32 bits.
- Trivially debuggable: `key >> 32` = objectId, `(int)(uint)key` = variation.

**API evolution:**

| Version | Type | Notes |
|---|---|---|
| Iter-3.6 | `HashSet<int>` | objectId only, no variation support |
| Iter-3.7+ | `HashSet<long>` | packed `(objectId, variation)` |

`PackKey(int objectId, int variation)` is the canonical factory. Never
construct the key inline — go through `PackKey` so the casting logic
stays in one place.

### Cooked-Food α-Enumeration

The α-algorithm derives entirely from `InventoryUtility.cs:~1626`
(`turnsIntoFood` lookup) and `CookedFoodCD` (variation + tiers).

**Pick-family derivation:**

For ingredient pair `(i1, i2)`, the result family is:
```
primary   = CookedFoodCD.GetPrimaryIngredient(i1, i2)
family    = ingredientLookup[primary.prefabEntity].turnsIntoFood
variation = CookedFoodCD.GetFoodVariation(i1, i2)
           = (primaryObjectId << 16) | secondaryObjectId
```

**Symmetry:** `GetFoodVariation(a, b) == GetFoodVariation(b, a)` because
`GetPrimaryIngredient` uses a deterministic tiebreaker (Seed 87931 RNG).
The upper-triangle iteration `(i2 >= i1)` safely deduplicates symmetric
pairs.

**Three-tier multiplication:**

Each `(family, variation)` tuple maps to three `CatalogEntry` rows:
base family ObjectID, `CookedFoodCD.rareVersion`, `CookedFoodCD.epicVersion`.

**Empirical validation:** runtime spike (May 2026) confirmed 3160 unique
pairs × 3 tiers = 9480 cooked-food entries, matching the expected catalog
size.

**`ObjectIDExtensions.IsCookedFood`** range: `objectId ∈ [9500, 9599]`
(max 100 family-item slots). Loop 1 uses this to skip standard-item
enumeration of cooked-food ObjectIDs — they are emitted by Loop 2 instead.

---

## Rarity Colouring (Iter-6)

Each row surfaces its CK rarity on two axes: a tinted item name (all
rarities) and a rarity border around the icon (Uncommon and above). The
rarity is a **distinct axis** from the Iter-3.7 cooked-food Base/Rare/Epic
tiers — each cooked-food tier carries its own `ObjectInfo.rarity`.

### Data flow

1. **Bake** — `ItemCatalog.Entry` carries a `Rarity Rarity` field, resolved
   from `PugDatabase.GetObjectInfo(objectId, variation).rarity` via a
   `rarityCache` that mirrors the existing `iconCache` (populated in both
   bake loops, read at the single `new Entry(...)` site). UI-independent: the
   bake never touches `Manager.ui`.
2. **Rebind** — `ItemChecklistContent.Rebind` resolves the colour per visible
   row: `Manager.ui.GetSlotBorderRarityColor(entry.Rarity,
   useDefaultColorForCommon: true, defaultColor: _defaultLabelColor)`. With
   `true`, Common/Poor return `_defaultLabelColor` (the label's prefab default,
   captured once from the first pool row) → no visible tint; Uncommon+ return
   `slotBorderRarityColors[(int)(rarity + 1)]`. The resolved `Color` and the
   `Rarity` enum are passed to `ItemRow.Bind`.
3. **Paint** — `ItemRow.Bind` sets `label.SetTempColor(colour,
   keepColorOnStart: true)` (after `Render`; see `gotchas.md § PugText tint`)
   and toggles `rarityBorder.enabled = rarity >= Rarity.Uncommon`, colouring it
   with the same `Color`. `ItemRow` stays decoupled from `Manager.ui` — it
   paints the colour it is handed.

`enum Rarity` (`Pug.Base.dll`): `Poor = -1, Common, Uncommon, Rare, Epic,
Legendary`; the colour-list index is `(int)(rarity + 1)`.

### Border SpriteRenderer

The `RarityBorder` child of the `ItemRow` prefab uses **`maskInteraction: 1`
(VisibleInsideMask)** — it scrolls and clips *with* the rows (the opposite of
the Iter-5 scrollbar's `None`). Sorting order 49 places the hollow frame above
the icon (order 48). It defaults to `m_Enabled: 0` (hidden until `Bind` proves
rarity ≥ Uncommon). The sprite is the placeholder `ui_rarity_border` (a white
1-px hollow frame, tinted at runtime), rendered as a proper **9-slice**
(`spriteBorder {1,1,1,1}` in its `.meta`, `m_DrawMode: 1`) so the 1-px ring
stays a thin fixed-pixel frame at any `m_Size` instead of thickening with the
sprite. Real pixel-art (a designed border in place of the tinted white ring)
remains Iter-12 (pixel-art) polish.

---

## List View-Model, Sorting & Filtering (Iter-7 / Iter-8 / Iter-10)

> **Iter-10 superseded the Iter-7/8 sort and filter option set.** The
> `ItemListViewModel` contract (the `int[] Order` indirection, `Recompute`,
> `OnResultsChanged`, `SearchText`) is unchanged; only the available sort
> modes and the filter mechanism were redesigned. This section documents the
> current (post-Iter-10) state throughout; historical notes from Iter-7/8 are
> preserved inline where they explain surviving code.

### ItemListViewModel

`ItemListViewModel` (revived from the orphaned `FilterAndSearchModel.cs`,
renamed) owns the **display-order indirection**: an `int[] Order` array where
`Order[displayIndex]` gives the catalog index for that display position. It
decouples row rendering from catalog order.

**`Recompute()`** rebuilds `Order` from scratch:

1. Collects *visible* catalog indices by applying the four active filter
   dimensions (AND across dimensions, OR within) and the name search.
2. Sorts by the active `SortMode` comparator (ascending):
   - **Name** — `DisplayName` (InvariantCultureIgnoreCase — locale-aware, "Ü"
     under U, not after Z)
   - **Rarity** — `(int)Rarity` ascending (Poor → Legendary)
   - **Level** — `Entry.Level` ascending (0 = no level, clusters at the low end)
   - **Value** — `Entry.SellValue` ascending (0 = unsellable, clusters low)
3. Tiebreak: `DisplayName` (InvariantCultureIgnoreCase) then catalog index —
   total order, stable under reversal.
4. **Descending** = reverse the sorted list.

**Static per-session state:** `s_mode` (`SortMode`) and `s_ascending` (`bool`)
survive window close/reopen and a catalog re-bake; reset on game restart.

**Recompute triggers:**

- Sort mode or direction change via the dropdown / toggle callbacks.
- Any filter dimension toggle or `ClearAllFilters`.
- Name search text change.
- Re-bake: `ItemChecklistMod.ListView` is reassigned (static, `internal set`)
  after each bake, which constructs a fresh `ItemListViewModel` and calls
  `Recompute()`.
- Window open: `PopulateContent` calls `model.Recompute()` unconditionally so
  discoveries that happened while the window was closed (the normal case — the
  player can't pick items up with the window open) are reflected immediately.

**`ItemChecklistContent` reads through `Order`:** `Rebind` at display index
`displayIdx` resolves `catalog.GetByIndex(model.Order[displayIdx])`.
`_count` comes from `model.Count`.

---

### Sort modes — data sources (Iter-10)

`enum SortMode { Name, Rarity, Level, Value }` (replaces the Iter-7
`{ Name, Rarity, Found, Category }` — Found and Category are now filter
dimensions).

**Level** — `Entry.Level` is baked from `PugDatabase.TryGetComponent<LevelCD>(od,
out var lvl) ? lvl.level : 0`. **Do NOT use `ObjectInfo.level`** — that field
is dead/legacy and is read nowhere in the game (confirmed via ILSpy decompile;
ItemBrowser's `ObjectUtility.GetBaseLevel` also goes through `LevelCD`). 0
means "no level data"; rows with Level 0 display `—` and cluster at the low
end of a level sort.

**Value** — `Entry.SellValue` is baked via `ItemCatalog.ComputeSellValue`, a
faithful port of ItemBrowser's `ObjectUtility.GetValue` (sell mode):

- `CantBeSoldAuthoring` component present OR `rarity == Legendary` → 0
  (unsellable; renders `—`).
- `info.sellValue >= 0` → use the explicit value directly.
- `info.sellValue < 0` → **auto-compute** from rarity base (`GetRaritySellValue`)
  + crafting ingredients (+ cooked-food ingredient recursion) + a deterministic
  `objectID`-seeded ±10 % jitter. `sellValue == -1` is CK's "auto-compute"
  sentinel — it does **not** mean unsellable.

`Entry.SellValue` is always ≥ 0 after bake (0 = unsellable; > 0 = computed
coin value). The `—` display guard in `ItemRow.Bind` is `sellValue > 0`.

---

### Faceted filter model (Iter-10)

`ItemListViewModel` holds four static `HashSet` dimensions (survive reopen +
re-bake; reset on process restart):

```
s_discovery  HashSet<bool>          true = discovered
s_rarity     HashSet<Rarity>
s_category   HashSet<ItemCategory>
s_craft      HashSet<bool>          true = craftable
```

`Recompute` applies each as a `continue` predicate: `if (set.Count > 0 &&
!set.Contains(value)) continue`. An empty set is no constraint (all items pass).
Semantics: OR within each set (the item's value must be in the set), AND across
sets. `ActiveFilterCount` is the total member count across all four sets; the
header shows `Filter (N)` when N > 0.

**`ItemCategory` / `ItemCategories.Of`:** a 10-bucket taxonomy mapping
`ObjectType` → `ItemCategory` enum:

| Bucket | `ObjectType` values mapped |
|---|---|
| Weapons | `MeleeWeapon`, `RangeWeapon`, `SummoningWeapon`, `ThrowingWeapon`, `BeamWeapon` |
| ArmorAccessories | `Helm`, `BreastArmor`, `PantsArmor`, `Necklace`, `Ring`, `Offhand`, `Bag`, `Lantern`, `Pouch` |
| Tools | `Shovel`, `Hoe`, `CastingItem`, `MiningPick`, `PaintTool`, `FishingRod`, `BugNet`, `Sledge`, `RoofingTool`, `DrillTool`, `WaterCan`, `Bucket`, `Seeder` |
| Food | `Eatable` |
| Placeables | `PlaceablePrefab` |
| Materials | `NonUsable`, `UniqueCraftingComponent` |
| Valuables | `Valuable` |
| KeyItems | `KeyItem` |
| Instruments | `Instrument` |
| Other | everything else (catch-all) |

**`IsCraftable`** — baked as `info.requiredObjectsToCraft != null &&
info.requiredObjectsToCraft.Count > 0`.

**`IsFiltered`** — `ActiveFilterCount > 0 || searchText.Trim().Length > 0`.
Drives the `· N shown` title suffix. Distinct from `Count != catalog.Count`
(a fully-completed "Discovered" filter has `Count == catalog.Count` yet is
still filtered).

---

### Shared Dropdown chrome (Iter-13)

Both the Sort dropdown and the Filter share one reusable prefab,
`Prefabs/Dropdown.prefab` — a pure **skeleton chrome** (Iter-18): a `Field`
container with `Display` (combobox slot holding `DisplayIcon`, `DisplayLabel`,
and the `Caret` inside it) and an empty `Popup` (panel + `RowContainer`). The
skeleton carries geometry/sprites/colliders and the `Display`'s
`DropdownToggleButton` (the sole open/close target — the separate `ToggleButton`
GO was removed in Iter-18), but **no** root widget component, **no** option
template, and **no** asc/desc toggle. Each consumer is a **prefab variant** that
adds only what differs:

- **Sort** = `Sort.prefab` (variant of `Dropdown.prefab`) + a root
  `DropdownWidget` + an option `RowTemplate` + an `AscDesc` toggle that lives
  inside the `Display` (Sort-only). The `AscDesc` and the `Display` open/close
  toggle are two 3D `BoxCollider`s in one field; the `AscDesc` collider's
  `m_Center.z` is pulled forward (`-0.1`) so CK's `UIMouse` raycast hits it
  first (the Iter-9 ClearButton z-stagger precedent).
- **Filter** = `Filter.prefab` (variant of `Dropdown.prefab`, renamed from
  `FacetedFilter.prefab` in Iter-18) + a root `FilterWidget` + the
  checkbox/header/action templates, with the filter glyph in the leading slot.

The window nests instances of **both** variants (0 direct bare-`Dropdown` refs).

The toggle is shared via **`IPopupToggle { TogglePopup() }`** — implemented by both
`DropdownWidget` and `FilterWidget`. `DropdownToggleButton.owner` is typed
`IPopupToggle` and **wired at runtime** in each widget's `Configure`
(`GetComponentsInChildren<DropdownToggleButton>` → `tb.owner = this`), not
serialized — a serialized cross-prefab owner ref is fragile (extracting the chrome
nulled it and broke header-click). This let one toggle class serve both widgets;
the former `FacetToggleButton` was deleted. (The chrome was proven to round-trip
through the ModBuilder→AssetBundle pipeline as a nested prefab + variant.)

### FilterWidget (Iter-10, prefab variant since Iter-13, renamed Iter-14.2)

`FilterWidget : PopupWidget` (renamed from `FacetedFilterWidget` in Iter-14.2;
see § PopupWidget shared base) — a multi-select sectioned popup. Replaces the
Iter-8 `DropdownWidget`-based filter; since Iter-13 it is a **prefab variant of
the shared `Dropdown.prefab` skeleton** (`Filter.prefab`, renamed from
`FacetedFilter.prefab` in Iter-18; see above). The popup chrome (open/close
state, click-outside auto-close, auto-size) lives in the `PopupWidget` base; this
section covers only the filter-specific deltas (header, templates, pools,
`RebuildList`).

**Closed state:** the `Display` `PugText` shows `"Filter"` or `"Filter (N)"`.
The `Display`'s single `DropdownToggleButton` (with the caret now inside the
Display) opens/closes the popup.

**Open state:** a popup panel showing gray section headers + checkbox rows
+ action rows. Section headers are separate inactive `headerTemplate` rows,
rendered via `_headerPool` (PugText gray, color set on the template's
`PugText.style` in the prefab). Checkbox rows come from `_rowPool`
(clones of `checkboxTemplate`), action rows from `_actionPool` (clones
of `actionTemplate`). Three distinct pools — checkbox and action rows
have different visuals (action rows have a glyph but no checkbox state).

**Member table:** `WireControls` calls `Configure` with a flat list of
`(section, label, isOn, toggle)` entries. An **empty `section`** marks an
action row (no section header rendered, drawn from `_actionPool`). Currently
one action row: `("", "Clear all", …)` — its `toggle` calls `ClearAll()`.

**`RebuildList`:** lays out section headers + pool rows top-to-bottom,
positioning each at `-(pos * rowSpacing)`. After every click (`OnMemberClicked`
or `ClearAll`), `RebuildList` is called unconditionally to re-sync every
checkbox visual in both pools.

**Open/close + click-outside:** inherited from `PopupWidget` (`TogglePopup`/
`SetOpen`, the `_armed`-guarded click-outside `LateUpdate`). Filter overrides
`OnPopupOpened() => RebuildList()` so the popup is rebuilt at open time.

**Companion files:** `FilterCheckboxButton : ClickButton` (one checkbox
row — holds `memberId` index + `checkMark SpriteRenderer`; implements `OnClick()`
to call `owner.OnMemberClicked(memberId)`, the guard-first prologue living in
`ClickButton`), each in its own `.cs` file (one MonoBehaviour per file rule).
The header toggle uses the shared `DropdownToggleButton` (via `IPopupToggle`) —
the old `FacetToggleButton` was removed in Iter-13.

---

### PopupWidget (shared base, Iter-14.2)

`abstract PopupWidget : UIelement, IPopupToggle` (`ui/PopupWidget.cs`, ~95 LoC)
consolidates the popup machinery that `DropdownWidget` (Sort) and `FilterWidget`
(Filter) previously duplicated. Both now derive from it; each subclass keeps only
its own header, templates, pools, and `RebuildList`.

The base carries:

- **Chrome serialized fields** — `caret` / `caretClosed` / `caretOpen` /
  `popupPanel` / `rowContainer` / `rowSpacing`. Moving these to the base is
  prefab-neutral: Unity deserialises inherited public fields **by name**, so the
  existing prefab wiring resolves unchanged (all six keys are present on both
  `Sort.prefab` and `Filter.prefab`).
- **State** — `_open`, `_armed`, plus the captured `_panel` (popup `SpriteRenderer`)
  and `_topY` (authored popup top edge) via `EnsurePanel()`.
- **Open/close** — `TogglePopup()` / `SetOpen(bool)`; `SetOpen(true)` calls the
  virtual `OnPopupOpened()` hook (default no-op for Sort; `FilterWidget` overrides
  it to `RebuildList()`).
- **Click-outside-to-close** — a single `LateUpdate` (now a proper `override`,
  clearing the long-standing CS0114 hide warning; it intentionally does **not**
  call `base.LateUpdate()`). The `_armed` guard skips the frame the popup opened
  on, so the opening click does not immediately re-close it.
- **Parametrized auto-size** — `AutoSizePopup(int rowCount)` centres the
  `rowContainer` and fits the panel height to the laid-out row stack, top-aligned
  to the authored top edge captured in `_topY`.

**★ The `FirstRowOffset` contract** (the most non-obvious design rationale of the
refactor). The single thing that genuinely differs between the two popups is the
slot offset of the first laid-out row. Captured ONCE as the abstract
`protected abstract int FirstRowOffset { get; }`:

- **Sort** (`DropdownWidget`) is a true **combobox** — the selected option is shown
  in the header and OMITTED from the popup, so popup row 0 is reserved for it →
  `FirstRowOffset = 1`.
- **Filter** (`FilterWidget`) header is a count label; every member is a real popup
  row → `FirstRowOffset = 0`.

`FirstRowOffset` feeds BOTH the subclass's row layout AND `AutoSizePopup`'s
`rowContainer` centring, so the two cannot drift (the one-row-offset trap, the R3
hazard). It is invisible to the Editor compile — wrong values surface only in-game
as a mis-positioned or clipped popup.

---

### Popup Scroll & Collapse (Iter-24)

Once Pets (Iter-16.1) + Critters (Iter-16.2) grew the Category list to ~29 rows,
the auto-sized Filter popup overflowed the viewport. Iter-24 adds two layers, both
in the shared `PopupWidget` base / `Dropdown` skeleton so any variant inherits
them: **scroll** (cap + clip + translate) and, Filter-specific, **collapse**
(clickable section headers).

**Scroll by manual translate ("Weg 2"), NOT CK's `UIScrollWindow`.** Two reasons
the main-list machinery is wrong here:

1. `UIScrollWindow.Awake` permanently self-disables (`enabled = false`) if its
   serialized `scrollable` does not resolve to an `IScrollable` on the *same*
   GameObject at load (see § Awake Logic). The `Dropdown` skeleton is deliberately
   component-less (Iter-18), so a skeleton `UIScrollWindow` would self-disable.
2. `UIScrollWindow` exists to virtualize ~10,910 catalog rows; the popup has ≤~29
   *real* row GameObjects — virtualization is overkill.

Instead `PopupWidget` caps the panel to `MaxVisibleRows × rowSpacing` (serialized
field; **base default 6 rows**, per-variant overridable — and absent from legacy
prefab YAML, so the "no cap" sentinel is deliberately `MaxVisibleRows <= 0`, which
deserializes to the safe value, see `gotchas.md § Serialized-field zero-default
sentinel`), clips with a popup-local `SpriteMask`, and translates
`rowContainer.localPosition.y` within `[0, contentH − cap]` by mouse wheel + a
hand-rolled draggable handle (`PopupScrollHandle`). The mask GameObject **and** the
scrollbar subtree (track + `PopupScrollHandle`) live in the `Dropdown` skeleton and
are inherited by both variants as inactive GOs; the base **runtime-discovers** their
references (`popupPanel.GetComponentInChildren<SpriteMask>(true)` /
`GetComponentInChildren<PopupScrollHandle>(true)`) rather than carrying serialized
cross-prefab refs (the Iter-13 runtime-wire rule — a first draft used a stripped-stub
`scrollMask` ref and was replaced). Sort inherits the chrome but never wires it;
its `RowTemplate` was nevertheless baked scroll-ready (`maskInteraction=1` + the
band), so it auto-scrolls if it ever exceeds 6 modes.

**The popup SpriteMask sorting band — 56..63, ABOVE the window mask 40..55.** The
window's `ContentsMask` covers only the list area (custom range **40..55**). The
popup's rows sit *above* the list's top edge, so if they shared 40..55 the window
mask would clip the popup's upper rows. The popup mask therefore uses a band cleanly
**above** 55 — **56..63** — which the window mask never touches; only the popup mask
clips the popup rows. PugText `orderInLayer` is freely settable in prefab YAML (the
footer-at-50 precedent, `gotchas.md § All PugText sits on the GUI sorting layer`), so
the popup labels were pulled from the default 9999 into the band. **Two clipping
requirements per renderer** (both must hold or a sprite is unclipped/invisible):
(1) `maskInteraction = VisibleInsideMask` (`m_MaskInteraction: 1` on a SpriteRenderer;
`style: maskInteraction` on a PugText) — the default `0`/None ignores all masks;
(2) the renderer's sorting order lies inside the mask's custom range (here 56..63).
The row-template prefabs carry both; clones inherit them, so setting the templates
suffices.

**Orthographic UI-camera cursor read — one mechanic, two problems.**
`Manager.camera.uiCamera.ScreenToWorldPoint(Input.mousePosition)` gives the cursor in
world space; the uiCamera is orthographic, so world X/Y are z-independent (no z
calibration). Comparing against the panel's world rect
(`popupPanel.position ± panel.size/2`) yields "cursor over popup," which serves BOTH
**click-outside-to-close** (gap-A — the old `LateUpdate` closed on any mouse-down, so
an inside click on a checkbox / section header / handle wrongly closed the popup) AND
**wheel-ownership** (scroll only when the cursor is over the popup). `Manager.camera`
is sandbox-safe (verified in-game).

**Wheel-ownership — Harmony prefix on `UIScrollWindow.UpdateScroll` (gap-F).** CK's
`UIScrollWindow.UpdateScroll` (from its `LateUpdate`) reads the wheel via
`Manager.input.GetScrollValue()` and scrolls when the cursor is inside the window.
Since the popup overlays the list, wheeling over the popup would scroll both. Fix: a
Harmony **prefix** on `UIScrollWindow.UpdateScroll` (`MainListWheelSuppressPatch.cs`)
that returns false (skips the stock scroll for that frame) while an open popup owns
the wheel (`PopupWidget.OpenPopupCapturesWheel()` — a static check reading the live
cursor via the orthographic read above). The condition is computed fresh inside the
prefix, so there is no LateUpdate-ordering staleness; only the main list is affected
(the popup uses no `UIScrollWindow`). Harmony runs in trusted `0Harmony.dll` →
sandbox-safe.

**Collapse (Filter-specific).** Section headers became clickable
(`SectionHeaderButton : ClickButton`, a 3D collider + caret glyph on the
`headerTemplate`). A `static HashSet<string>` closed-set keyed on the **stable loc
term** (not the resolved label, see `conventions.md § Stable section key`) records
which sections are collapsed — language-change-safe. Multiple sections open at once,
all open by default. `RebuildList` always renders a section's header (binding its
toggle + caret state) but skips the member rows of collapsed sections; `AutoSizePopup`
then operates on the reduced visible count, so collapsing enough rows deactivates the
scrollbar entirely. Section carets shift X in a post-`AutoSizePopup` pass — clear of
the scrollbar when it shows, at the panel edge when it hides.

---

### Sort UI Components

All sort controls live in the header strip. Every clickable uses a **3D
`BoxCollider`** (`!u!65`) and every `SpriteRenderer` uses `m_MaskInteraction: 0`
(None) + the `"GUI"` sorting layer.

#### DropdownWidget (sort)

`DropdownWidget : PopupWidget` (since Iter-14.2; was `: UIelement, IPopupToggle`)
— reusable single-select dropdown (mod-authored, not CK-native). Used for the
**Sort** dropdown, as a nested instance of the shared `Dropdown.prefab` chrome
(see § Shared Dropdown chrome). The popup chrome (open/close, click-outside,
auto-size) lives in the `PopupWidget` base; below are the Sort-specific deltas.
`FirstRowOffset = 1` (the selected option is shown in the header, not the popup).

**API:**
```csharp
Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
```

**Header:** the selected option label shown at all times. A
`DropdownToggleButton` + caret button opens/closes the popup on click.

**Popup:** lists only the *non-selected* options, flush under the header at
`-(pos + 1) * rowSpacing`. `EnsurePool` clones `rowTemplate`; `RebuildList`
lays them out. Selecting fires `onSelected` and re-`Configure`s.

**Click-outside-to-close:** inherited from `PopupWidget` (the `_armed`-guarded
`LateUpdate`); Sort does not override `OnPopupOpened()`.

#### DropdownToggleButton / DropdownOptionButton

Each in its **own `.cs` file**. Both subclass `ButtonUIElement`. See
`gotchas.md § Multiple MonoBehaviours in one file`. Since Iter-13
`DropdownToggleButton.owner` is an **`IPopupToggle`** (DropdownWidget *or*
FilterWidget), wired at runtime in `Configure` — so one toggle class
drives either widget from the shared chrome.

#### AscDescToggle

`AscDescToggle : ButtonUIElement` — flips `ItemListViewModel.s_ascending`,
swaps the asc/desc glyph, triggers `Recompute()` + `RefreshVisible()`.

---

### ButtonUIElement Click Pattern — `ClickButton` base (Iter-14.2)

**ItemChecklist convention — guard FIRST, then base.** Until Iter-14.2 every
clickable repeated the same `OnLeftClicked` prologue (guard `canBeClicked`, call
base). Iter-14.2 hoisted it into `abstract ClickButton : ButtonUIElement`, whose
`sealed override OnLeftClicked` runs the prologue once and then calls
`protected abstract OnClick()`:

```csharp
public abstract class ClickButton : ButtonUIElement
{
    public sealed override void OnLeftClicked(bool mod1, bool mod2)
    {
        if (!canBeClicked) return;       // guard first: when not clickable, OnClick is NOT run
        base.OnLeftClicked(mod1, mod2);
        OnClick();
    }
    protected abstract void OnClick();   // the subclass's action
}
```

The five clickable controls extend `ClickButton` and implement only `OnClick()`:
`DropdownOptionButton`, `DropdownToggleButton`, `AscDescToggle`,
`ClearSearchButton`, `FilterCheckboxButton`. The abstract base is prefab-neutral
(never instantiated → no `m_Script`/`fileID` ref; subclass `fileID 11500000`
unchanged).

**Required prefab rules (same as ScrollBarHandle):**

- **3D `BoxCollider`** (`!u!65`): CK `UIMouse` raycasts in 3D.
- **`m_MaskInteraction: 0`** on every `SpriteRenderer`.
- **`"GUI"` sorting layer**.
- **Leave `spritesShownUnpressed` / `spritesShownPressed` empty**.

---

### Sprites — Sheet Atlases (Iter-7)

ItemBrowser's `ui_icon.png` and `ui_group.png` are **multiple-mode sheet
atlases** (`textureType: 8`, `spriteMode: 2`) carrying named sub-sprites:
`ui_icon_sort`, `ui_icon_sort_order_asc`, `ui_icon_sort_order_desc`,
`ui_icon_filter`, `ui_icon_clear_search`, `ui_group_expand`,
`ui_group_collapse`, and others.

Reference sub-sprites by `{fileID: <internalID>, guid: <atlas guid>, type: 3}`.
Bundle inclusion is by dependency-pull. Do NOT copy individual PNGs from the
atlas (see `gotchas.md § Bridge sprite trap`).

Button backgrounds use `ui_scrollbar_handle` (`~{1,1}` `m_Size` for correct
9-slice reading). Slot/list backgrounds use `ui_slot_background`.

---

## Filter & Search (Iter-8 + Iter-10)

Iter-8 introduced the search field and the original discovery-filter dropdown.
Iter-10 replaced the filter dropdown with `FilterWidget` (named
`FacetedFilterWidget` until Iter-14.2; see § Faceted filter model above). The
search field and its focus model are unchanged.

### SearchBar : TextInputField

The search field is **CK-native**, not uGUI. `SearchBar` subclasses
`TextInputField` (`Pug.Other.dll`): PugText rendering, the blinking caret
(`CharacterMarkBlinker`), click-to-focus, and WASD-suppression are all
inherited. `OnLeftClicked` (base) calls `Manager.input.SetActiveInputField(this)`.
Our subclass only:
- polls `GetInputText()` in `LateUpdate`, pushing changes to `model.SearchText`
  (with a `_lastPushed` change-cache so unchanged frames don't re-`Recompute`);
- exposes `SyncFrom(text)` — sets the field text **and** `_lastPushed` so the
  window can sync the field to the model on open / after a re-bake.

**Option A semantics:** search matches the real `DisplayName` of every entry
(discovered and undiscovered alike); undiscovered matches still render `???`.
This is the deliberate choice — the item appears with its spoiler-guard
rendering intact.

The orphaned `UnityInputFieldAdapter` (uGUI `InputField` wrapper) was deleted
in Iter-8.

### Focus model — staying focused while typing

Set **`dontDeactivateOnDeselect = true`** to stay focused off-hover (CK
selection is hover-based). `ItemChecklistWindow.HideUI()` calls
`searchBar.Deactivate(false)` (guarded by `inputIsActive`) — every close path
funnels through `HideUI`, so closing always frees gameplay input (WASD).

### ClearSearchButton

`ClearSearchButton : ButtonUIElement` (guard-first). On click: `ResetText()` +
clears `model.SearchText`. Glyph `ui_icon_clear_search`.

### Control re-wire on re-bake

`WireControls()` is called after `PopulateContent()` on every open. It
tracks `_wiredModel` and unsubscribes its `OnResultsChanged` before
subscribing the new one, so the discarded model is not retained by a dangling
delegate after a re-bake.

### Search-field prefab structure (the "Display" container)

```
SearchField   (SearchBar + 3D BoxCollider = click/focus target; no SpriteRenderer)
├─ Display    (SpriteRenderer: ui_classic 9-slice slot, Sprites-Default mat, GUI/order 52)
│  ├─ Text    (PugText, pugText)
│  └─ Hint    (PugText, hintText, "Search…")
├─ SelectedMarker
├─ Caret      (CharacterMarkBlinker; TextInputField writes its world X/Y per frame)
│  └─ CaretSprite (painted 2x8→7px-tall Caret sheet sprite SpriteRenderer at a
│                  constant localPosition offset; child so the nudge survives the
│                  per-frame parent reposition — Iter-14.1)
└─ ClearButton (ClearSearchButton + ui_icon_clear_search + 3D BoxCollider)
```

The `Display` was produced by duplicating the dropdown's `Display` (inheriting
its correct sprite/material/sorting/9-slice) and **stripping** its
`DropdownToggleButton` + `BoxCollider` (else the leftover button hijacks clicks
and fires the original dropdown's popup).

---

## Row Level / Value Columns (Iter-10)

Each `ItemRow` now has `levelText`, `valueText`, and `coinIcon`
(`SpriteRenderer`) fields alongside the existing `label`, `icon`, `checkmark`
etc.

`ItemRow.Bind` receives `int level` and `int sellValue` alongside the other
rebind params:

- **Level:** `Lv N` if `isDiscovered && level > 0`; `—` otherwise.
- **Value:** `sellValue.ToString()` if `isDiscovered && sellValue > 0`; `—`
  otherwise.
- **Coin icon:** `coinIcon.enabled = isDiscovered && sellValue > 0`. The sprite
  is loaded once per session via `CoinSprite()` — `PugDatabase.GetObjectInfo(
  ObjectID.AncientCoin, 0).smallIcon ?? .icon`. All rows share the same
  `Sprite` reference (static field `s_coinSprite`, resolved on first call).

The `—` string is a true em-dash (`U+2014`); the PugText pixel-font renders it
as a hyphen/minus (`U+002D`) — cosmetic only.

---

## Shortcut-Panel & HUD Suppression (Iter-9)

While the checklist is open, three things that would otherwise show over it are
suppressed; all are scoped to `ItemChecklistWindow.Instance.Root.activeSelf` and
release automatically when the window closes/auto-hides (so a vanilla inventory
sees normal behaviour).

**Why they appear at all:** CoreLib forces `UIManager.isAnyInventoryShowing ==
true` for any mod UI (it patches the aggregate getter; per-UI `isShowing` getters
stay unpatched). That makes CK treat the checklist like an inventory — enabling
the keyboard-shortcuts panel's toggle key (S) and keeping the HUD's
inventory-context elements live.

| Target | Mechanism | File |
|---|---|---|
| `ShortCutsWindow` ("Tastenkürzel" panel) | `LateUpdate` **prefix** → `__instance.HideUI()` + `return false`. **Load-bearing:** runs every frame, so it beats the S-key toggle (which `ShortcutsCanBeToggled` does *not* gate). | `ShortCutsWindowSuppressPatch.cs` |
| `InventoryShortCutsButton` ("?" prompt) | `ShortcutsCanBeToggled` **postfix** → `__result = false`. Gates the prompt visuals (`UpdateVisuals`), so the prompt disappears. | `InventoryShortCutsButtonSuppressPatch.cs` |
| Top-left HUD (health/food/ability bars) | `ShowUI` → `Manager.ui.TemporarilyDisableGameplayUI()`; `HideUI` → `EnableTemporarilyDisabledGameplayUI()` (guarded for the Awake-time `HideUI`). | `ui/ItemChecklistWindow.cs` |
| Bottom-right button hints (Tab/E…) | `InGameButtonHintsUI.LateUpdate` **prefix** → forces the **public** `container` GameObject inactive + `return false` (the stock `LateUpdate` re-asserts `container.SetActive(showKeyHints)` every frame, so a one-shot hide is overwritten). | `InGameButtonHintsSuppressPatch.cs` |

**Decompile facts (verified against this install's `Pug.Other.dll`):**
`ShortCutsWindow.LateUpdate` is a `protected override` declared on the type (so
the patch binds); `HideUI()` is `public` (`root.SetActive(false)`).
`InventoryShortCutsButton.ShortcutsCanBeToggled()` is `public static bool` and
drives only the prompt visuals — **not** the S keybind (`ToggleInventoryShortcuts`
checks `isAnyInventoryShowing` directly), which is why the panel needs the
per-frame `LateUpdate` prefix. `TemporarilyDisableGameplayUI()` flips a private
**runtime** scale-multiplier field (CK's own RadicalMenu-open mechanism, ~51 HUD
elements self-scale to zero) — **not** `Manager.prefs.hideInGameUI`, which
`SetDirty()`s to disk. All four patch targets are sandbox-safe (Harmony
attributes run in trusted `0Harmony.dll`; the HUD calls are public `Manager.ui`
methods).


## Item-Row Layout (Iter-9)

- **RowHeight is a single source of truth, read from the prefab.**
  `ItemChecklistContent.Init` reads `RowHeight` from the row prefab's
  `background` SpriteRenderer (`size.y`, authoritative in Sliced draw mode);
  `ItemRow.RowHeight` is only a compile-time fallback. Change the row background
  `m_Size.y` in `ItemRow.prefab` alone and the pool size, row spacing and total
  scroll height all follow.
- **Flush is RowHeight-independent.** Rows are placed by
  `y = MaskTopLocalY - RowHeight*(displayIdx + 0.5)`: row 0's TOP is pinned to the
  fixed `MaskTopLocalY` (1.25, the content-local mask top) and each centre is
  offset by `RowHeight/2`, so the list start/end stay flush for any row height
  (windowHeight = maskHeight and content = count*RowHeight do the rest). Replaces
  the old `-(displayIdx*RowHeight)`, which only stayed flush at the original 2.5.
- **Checklist checkbox.** `ItemRow.Bind` enables the empty checkbox sprite
  (`checkmark`) on **every** row; discovered rows additionally enable a
  `checkFill` child (the `ui_icon_requirement` glyph, sorting order above the box)
  -- empty box = todo, box+check = done.
- **Side margins + scrollbar.** Window content is symmetric +/-14.2; the row
  background spans `[-14.2, 13.95]` so it ends at the scrollbar's left edge
  instead of running under it. Header field heights are 0.7 (contents centred);
  the search field's `selectedMarker` is a full-field controller-focus highlight
  rendered in front of the field background.


## HUD Counter (Iter-11.5)

A permanent top-right readout mirroring the window footer's discovered/total
counter — the mod's first **non-modal** UI.

### Mount (non-modal)
`ItemChecklistHud : UIelement` lives in `Prefabs/ItemChecklistHUD.prefab`. It is
NOT a CoreLib mod UI: `ItemChecklistMod.ModObjectLoaded` routes the prefab by
GameObject name (`"ItemChecklistHUD"`) into a `hudPrefab` field instead of
`UserInterfaceModule.RegisterModUI`, and `ItemChecklistMod.Update` lazily
`Instantiate`s it once `Manager.ui.chestInventoryUI` exists, parented under
`chestInventoryUI.transform.parent` (the `IngameUI` root that also holds CK's
health/hotbar HUD). One persistent instance, like `PlayerHealthBarUI`.

### Rendering — HUD layer + z (the crux)
CoreLib has no always-on HUD API. Two facts make a static child of `IngameUI`
actually render (both proven only in-game — see `docs/gotchas.md`):
- **Unity layer 27 ("HUD")**, not 5 ("UI"). The uiCamera draws the HUD layer
  during gameplay (`CameraManager.ShowHUD` toggles `1 << ObjectLayerID.HUD` in its
  cullingMask); layer 5 is only drawn for modal UIs that CoreLib's open-path
  activates.
- **local z = 10** (world z ≈ 0). The `IngameUI` parent sits at world z = -10;
  CoreLib moves modal UIs to `initialInterfacePosition` (z = 10) when shown. A
  static element left at the parent origin is outside the uiCamera frustum.

### Visibility — explicit, not the scale-multiplier idiom
`Manager.ui.CalcGameplayUITargetScaleMultiplier()` returns `(0,0,0)` for this mod
HUD (it is not a drop-in scale source — using it zeroes the element). `LateUpdate`
instead toggles `hudRoot` active by explicit signals (`ItemChecklistHud.cs:56`):
`WorldState.IsInPlayableWorld && !Manager.ui.isAnyInventoryShowing &&
!Manager.menu.IsAnyMenuActive()`. `isAnyInventoryShowing` (the CoreLib-patched
aggregate) covers the player inventory, crafting, **and** the checklist window.
The leading gate is the shared `WorldState.IsInPlayableWorld` predicate
(`isInGame && isSceneHandlerReady && !Manager.load.IsLoading()`; Iter-15 also
appends `!cutsceneIsPlaying` to suppress the intro cutscene), which Iter-11.6
substituted for the original `Manager.main.player != null` term: contrary to the
earlier assumption, `player != null` does **not** suppress the world-load screen
(the player object is instantiated at `PlayerController.OnOccupied` *while the
load screen is still up* and survives the exit-to-menu transition, so it is true
across both load screens). See `docs/gotchas.md § Manager.main.player != null does
NOT suppress a load screen (Iter-11.6)`.

### Content
Icon = `ui_slot_toggled_border` box + `ui_icon_requirement` tick (0.7 scale, a
child `IconFill`), the discovered-row checkbox look. Text = a `PugText` rendering
`ProgressFormat.Counter(discovered, total)` — the same shared helper the window
footer (`FormatTitle`) uses, so the two never drift. Re-rendered on
`DiscoveredState.Changed` and after each bake (world-load + loc-change hooks),
never per frame (`PugText.Render` rebuilds glyph SpriteRenderers).

## Possession (Iter-20) + Discovery Gate (Iter-21)

A second completion axis beside discovery: per row, how many of an item the player
currently **owns**. The checkbox + "done" tick tint **blue** when owned ≥ 1, a
right-aligned owned-count column shows the number, and an "In / Not in possession"
filter section sits under Discovery. Goal: completionists who want "own ≥ 1 of every
item", not just "discovered every item".

### The `possession/` package (read live from the ECS world)

| Class | Responsibility |
|---|---|
| `PossessionScanner` | Resolves the inventory world (`World.All`, max `ContainedObjectsBuffer` count = ServerWorld in SP), then scans **all** placed `ObjectDataCD` entities. **Possession = carried + base storage.** Carried = the player's whole `ContainedObjectsBuffer` (always live; includes the 0–9 equipment slots). Base storage = placed furniture/display within `AnchorRadius` of a **clustered** crafting-station anchor; contents read from the entity buffer. Counts the placed object itself, not just contents. |
| `PossessionLedger` | Keys containers by world tile `(x,z)` (packed `long`), merges `carried + per-container` in `BuildView`, and marks items present only in not-currently-observed containers as "remembered" (check ownership while away from base). |
| `PossessionStore` | Persists the ledger per character GUID via `API.ConfigFilesystem` (hand-rolled ASCII, sandbox-safe). |
| `PossessionConfig` | Exposes `AnchorRadius`. |
| `PossessionClassifier` | Type/ID predicates: `PlaceablePrefab` = 800 is the ownership gate; locked chests + boss statues count the **object** but not contents (loot unknown / boss chest is carriable). |
| `PossessionView` | The immutable per-refresh snapshot: `Count(objectId)` → owned total. `ItemChecklistMod.Possession` holds the current one (refreshed on open + a throttled interval). |

**Cluster filter — "your base".** A station only anchors if part of a **cluster**
(≥ 1 other station within `ClusterRadius` = 16 tiles) — a real base packs stations
together; a lone outpost / boss-arena / NPC station does not. This stops a remote
world container (e.g. a Copper Key in Ghorm's spawn arena) from counting as owned.

**Durability vs stack.** `ContainedObjectsBuffer.amount` is double-purposed (stack
size for stackables, **durability** for equipment). Mirror CK's `GetTotalAmount`:
stackable → amount, non-stackable → 1 per slot; look up stackability at **variation
0** (a non-existent `(objectID, variation)` returns null and would wrongly take the
durability branch).

**Persistence rides CK's own save.** `SaveManagerWriteCharacterHook` (Harmony
postfix on `SaveManager.WriteCharacter(int)`) persists the ledger in **lockstep**
with CK's character-file write — it fires on autosave **and** "Save & Quit", unlike
the GUID-clear save that a clean quit never triggers. Symmetric to the
`CharacterData.OnAfterDeserialize` load hook.

### Discovery gate (Iter-21)

Possession is **spoiler-gated behind discovery**. An undiscovered (`???`) row
already em-dashes Level/Value (the Iter-10 spoiler guard); showing an owned count
there is the same kind of spoiler — and produced the incoherent "owned but never
discovered" state for **world-spawned placed objects** (a Core WayPoint the player
never picked up still counts in the world scan).

Single chokepoint: `ItemChecklistMod.OwnedCount(int objectId, int variation)` returns
`Possession.Count(objectId)` only when
`DiscoveredState.Instance.IsDiscovered(objectId, variation)` (else 0, also the safe
default before the discovery snapshot loads). Both read sites route through it —
`ItemChecklistContent` (owned column + blue tint) and `ItemListViewModel` (the
In/Not-in-possession filter) — so a `???` row can never show possession, by
construction. It gates on the **same** discovered flag that drives `???`-vs-name.

> The gate is correct regardless of *why* a row is undiscovered. The distinct
> variation-keyed-discovery case — a family discovered only at a non-0 variation
> would still render `???` because the row checks `IsDiscovered(objectId, 0)` — is a
> separate concern deferred to **Iter-17** (per-variation tracking).

## Row-Hover Tooltips (Iter-22)

Hovering a checklist row shows CK's native item tooltip (name / description /
stats) plus an inventory-slot hover highlight, for an arbitrary catalog item that
exists in no live inventory.

### CK's tooltip is selection-driven, not entity-driven

`UIMouse.UpdateHoverText` (`Pug.Other.dll` ~356342) reads
`Manager.ui.currentSelectedUIElement` and calls its four hover virtuals on the
selected `UIelement`:

- `GetHoverTitle()` → `TextAndFormatFields`
- `GetHoverDescription()` → `List<TextAndFormatFields>`
- `GetHoverStats(bool)` → `List<TextAndFormatFields>`
- `GetContainedObject()` → `ContainedObjectsBuffer`

There is **no live ECS entity** anywhere in the path. To show a tooltip for an
arbitrary item, a `UIelement` need only return a `ContainedObjectsBuffer`
wrapping a synthetic `ObjectDataCD { objectID, variation, amount = 1,
variationUpdateCount = 0, auxDataIndex = 0 }`. Precedent: Item Browser's
`ItemBrowserSlot : SlotUIBase` does exactly this for catalog items not in any
inventory.

### Selection is re-derived from the raycast every frame

`UIMouse.UpdateMouseUIInput()` (~355773) re-runs `Physics.RaycastNonAlloc`
against `UILayerMask` every frame and `TrySelectNewElement` — so
`currentSelectedUIElement` is owned by the raycast, and a manually-assigned
selection is clobbered on the next frame. Selectability of a `UIelement`
requires a 3D collider **on the same GameObject** (where `UIMouse`'s
`GetComponent<UIelement>()` resolves), on the **UI layer**, passing
`isVisibleOnScreen` (active + enabled + non-zero lossy scale). There is **no**
`isSelectable` flag — presence of the collider on a visible UI-layer GO *is* the
gate. (Reusable for any future CK-UI hover/selection work, e.g. Iter-17.)

### ItemChecklist wiring

Each `ItemRow` (already a `UIelement`) captures `_objectId` / `_ckVariation` /
`_nameKnown` on `Bind` and overrides the four hover virtuals, delegating them to
**one shared `TooltipSlot : SlotUIBase`** held by `ItemChecklistContent` and
injected into each pooled row via `SetTooltipHelper`. The `TooltipSlot` is fed
the row's `(objectId, ckVariation)` and produces the title/description/stats; the
rows themselves carry no tooltip logic beyond forwarding.

- A prefab-authored 3D `BoxCollider` sits on the **row root** (the GO the
  `UIelement` lives on) so `UIMouse`'s raycast hover-selects the row.
- The hover **highlight** is a prefab-authored `SpriteRenderer` driven per-frame
  from the row's `LateUpdate` (see below) — not from the one-shot
  `OnSelected`/`OnDeselected` callbacks.

### Awake-skip helper (`TooltipSlot`)

`SlotUIBase.GetHoverStats(ContainedObjectsBuffer, bool, bool)` (~327477) is an
**instance** method, so a `SlotUIBase` instance is required. A bare
`new GameObject().AddComponent<TooltipSlot>()` NREs in `SlotUIBase.Awake` on
`animator.enabled` → `TooltipSlot` overrides `Awake` to an **empty body**. A
throwaway spike confirmed that an Awake-skipped helper returns title + description
+ stats correctly **without** `base.world` or the serialized slot fields — no
prefab instantiation of the helper is needed (a coin gave title+desc with no
stats; a Copper Sword gave `statLines = 2`).

### Tooltip is cursor-anchored, not element-anchored

`UIMouse` positions the tooltip relative to the `pointer` transform (~357077),
so the selected element's own transform position is irrelevant — the off-screen
proxy nature of the shared `TooltipSlot` does not matter.

### Spoiler model (mechanism)

The tooltip is gated **inside the four overrides**, not on the collider: an
undiscovered (`???`) row keeps its collider always enabled, so it still
hover-**highlights** (revealing nothing), but its overrides return only a
minimal localized `??? - not yet discovered` placeholder instead of the real
item. `_nameKnown` therefore gates only the *tooltip content*, never the
collider or the highlight. (The decision rationale is in `conventions.md § UI
Code Conventions`.)

### Per-frame highlight (mechanism)

The highlight is driven each frame from the row's `LateUpdate`:

```
show = (Manager.ui.currentSelectedUIElement == this) && PointerInViewport();
if (highlight.enabled != show) highlight.enabled = show;   // idempotent guard
```

Driving it per-frame (rather than from `OnSelected`/`OnDeselected`) clears the
highlight the instant the cursor leaves the row — a one-shot deselect can be
skipped when the cursor moves straight onto an overlay without a selection
change. `PointerInViewport()` is the viewport gate (see `gotchas.md § Item Rows &
Hover`).

## Pet-Skin Collection (Iter-16.1)

A design-level summary of the per-skin pet collection feature. The deep CK data
model is verified in the `reference_ck_pet_critter_discovery_model` memory; the
build narrative is the Iter-16.1 entry in `docs/iteration-history.md`. This
section captures only the architecture-shaping facts (one skin = one
collectible, riding a mod-owned ledger).

### Why pets were already in the catalog

The bake excludes `NonObtainable` / `Creature` / `PlayerType` (`Critter` is kept
since Iter-16.2 behind an icon-guard). The
relevant `ObjectType` enum values are `PlaceablePrefab = 800`, `Critter = 801`,
**`Pet = 802`**, `Creature = 900`, `PlayerType = 6000` — so **`Pet` (802) is
not on the exclusion list** and pets were always in the catalog (the roadmap's
"relax the Creature filter" premise was wrong — the third such mis-guess after
the Iter-21 waypoint case). The real gap was per-skin tracking, not the bake.

### The CK pet model (skin = orthogonal aux data)

CK force-zeroes pet discovery to variation 0 (`SaveManager.SetObjectAsDiscovered`
zeroes `variation` for `PetCD` objects), so CK records a pet only at
`(objectID, 0)` and tracks **no per-skin discovery**. The skin is orthogonal aux
data: `PetSkinCD.skinIndex`, an `[InventoryAuxDataComponent]` in the world-global
`InventoryAuxDataSystem`, assigned randomly on hatch (`rng.NextInt(maxSkins)`).
All skins of a pet share one ObjectID and render as gradient recolors of the one
base icon. The real skin count is
`Manager.ui.petInfosTable.GetPetSkinInfo(id).skins.Count` (more reliable than
`PetCD.maxSkins`). CK ships no native skin-collection system → the feature is
mod-owned.

Reading `skinIndex` is **sandbox-safe**:
`InventoryHandler.TryGetExtraInventoryData<PetSkinCD>` covers carried and
in-chest pets; the active summoned pet is reached via the player's
`PetOwnerCD.PetEntity` → `PetCD.inventoryAuxDataIndex` → the same lookup (fixing
the Iter-20-deferred active-pet undercount, since a summoned pet is a live entity
outside the possession scan's `ContainedObjectsBuffer`).

### Catalog keying — Bake Loop 3

`ItemCatalog.Bake` Loop 3 emits one `Entry` per `(petObjectID, skinIndex)`, with
`skinIndex` stored in the entry's **`Variation`** slot and `IsPetSkin: true`. The
per-skin names are unique, so these rows bypass Loop 1's name-conflict pass. (See
the three-loop overview in § ItemCatalog Three-Loop Bake — Loop 3 is the pet axis.)

### Pet collection ledger (not CK `DiscoveredState`)

Pet rows route through a mod-owned `PetCollection` ledger instead of CK's
skin-blind `DiscoveredState`:

| Class | Responsibility |
|---|---|
| `PetCollection` | Persistent **ever-owned** ledger of collected `(objectID, skinIndex)` keys (`IsCollected`). A skin stays collected after the pet leaves inventory — collection is "ever owned," not "currently owned." |
| `PetCollectionStore` | Per-character-GUID persistence to `petskins-<guid>.txt` via `API.ConfigFilesystem` (hand-rolled ASCII, sandbox-safe), saved on the `SaveManager.WriteCharacter` hook — the same Iter-20 mechanism. |

The `ItemChecklistMod.OwnedCount(objectId, variation)` chokepoint branches:
pet-skin rows resolve via `PetCollection.IsCollected` + `PossessionView.CountSkin`
(per-skin live count); normal rows keep the `DiscoveredState` + `Count` path.

### Decoupled name vs. detail display

`ItemRow.Bind` decouples two booleans that are one-and-the-same for normal items:

- **`nameKnown`** — the *species* is discovered (CK's variation-0 discovery flag).
- **`showDetails`** — *this skin* is collected (the `PetCollection` ledger).

A known-but-uncollected skin shows the species **name** but the unknown icon and
`—`; a normal item sets both bools equal to its discovered flag (zero behaviour
change for non-pet rows).

### Why a surgical material-swap, not the SlotUIBase path (B3)

Item Browser renders item icons through CK's native `SlotUIBase` /
`ItemSlotsUIContainer`, which deliver the gradient recolor
(`Manager.ui.ApplyAnyIconGradientMap`) **and** hover tooltips for free.
ItemChecklist is a raw-`SpriteRenderer` column list (not a slot grid), so it
cannot reuse `SlotUIBase` wholesale — the gradient is applied by a **surgical
per-skin material swap** instead (see `ui/PetSkinIcon.cs` and
`docs/gotchas.md § Gradient-recolor shader`). Iter-22 later added row-hover
tooltips **without** porting the list onto `SlotUIBase`: it kept the
raw-`SpriteRenderer` rows and drove CK's native tooltip from one shared
`TooltipSlot : SlotUIBase` helper the rows delegate to (see § Row-Hover Tooltips
(Iter-22)) — so the gradient material-swap and the tooltips coexist, neither
needing the full slot-grid port.

## Runtime Glyph Injection (Iter-25)

The chrome labels (Sort/Filter/header) render in `thinTiny`, CK's reduced
**digits-only** face — so accented characters never had a real glyph. Iter-25
inserts 85 mod-authored accented glyphs into the live `thinTiny` PugFont at
runtime.

### The rrs* font family

`PugText.style.fontFace` is an enum `TextManager.FontFace`; each face is a
separate `PugFont` ScriptableObject with its own atlas in the `rrs*` family,
resolved by `Manager.text.GetFont(fontFace)`:

| FontFace | Atlas | Size | Glyphs |
|---|---|---|---|
| `thinTiny` | `rrs5` | 256×40 | 114 — CK's reduced **digits-only** face (no accents) |
| `thinSmall` | `rrsthin8` | — | 331 |

A char absent from a face's `codePoints` is **not** an error: `PugFont.GetGlyphData`
runs a fallback chain (button → thinTiny → chinese → japanese → korean → `?`),
so a missing `ö` resolves from the **chinese** font (CJK metric) and renders
deformed with no log warning. See `docs/gotchas.md § Font / Glyphs`.

### `ThinTinyGlyphPatch.InsertOnce()`

Idempotent (`_done` guard — runs once per session). Steps:

- Load the sheet via `AssetBundle.LoadAsset<Sprite>("Assets/ItemChecklist/Art/thinTiny_glyphs.png")`
  (a runtime-only bundle asset — referenced by no prefab; see
  `docs/gotchas.md § Font / Glyphs`).
- Clone the face's `glyphData[]` array and append 85 entries.
- Per glyph `i` at `baseIdx + i`:
  - `codePoints[(char)code] = baseIdx + i` (the first `GetGlyphData` branch
    wins before fallback).
  - `gd.volatileSprite = Sprite.Create(tex, rect2, pivot, 16f, 0, SpriteMeshType.FullRect)`.
  - `gd.rect = RectInt(x, y, w, h)` — the **un-padded** rect, where `w` = the
    **advance width**.

The `rect2`/centered-pivot construction must replicate `PugFont.InitCodePoints`
**exactly** — outline-padded source rect (`y+1`, `h-1`, then `x-1`/`w+2`
guarded) plus a centered pivot — or glyphs shift up-right. This is the
load-bearing detail; the full trap is in `docs/gotchas.md § Font / Glyphs`.

### Anchor

`InsertOnce` runs when `Manager.text` is ready, called from
`ItemCatalogWorldLoadHook.BakeWhenReady()` after the player-ready `WaitUntil`
and **before** `Catalog.Bake()` — the `OnOccupied` anchor.

### The 3-layer Pixaki glyph pipeline

A second art pipeline distinct from the `ui_checklist` UI sheet. The master is
`sources/thinTiny_full.pixaki` (a 4-layer doc: Background / charDims / Rects /
Atlas, plus a thinSmall reference). Extraction convention:

- **Atlas layer = sprite** (the glyph pixels).
- **Rects layer = advance width**.
- **thinSmall arrangement = char / codepoint cell**.

85 glyphs = full Western-European + partial Eastern-European/Cyrillic/typography.
See `docs/research/pixaki-format.md` and the
`reference-ck-pugfont-architecture` memory.
