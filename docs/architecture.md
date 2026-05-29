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
2. `IMod.ModObjectLoaded` — `UserInterfaceModule.RegisterModUI(windowPrefab)`.
   Called once when the mod's `GameObject` (from the AssetBundle) is
   available.
3. CoreLib's postfix on `UIManager.Init` instantiates the prefab into
   `UIManager.chestInventoryUI.transform.parent` — this is the canonical
   CK UI mount point used by every IModUI implementation.

**Open path (F1 → visible window):**

4. F1 hotkey (Iter-4 — deferred) calls
   `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`.
5. CoreLib calls `ItemChecklistWindow.ShowUI()`.
6. `ShowUI` triggers `SpawnRows()`: iterates `ItemCatalog.AllEntries`,
   instantiates `ItemRow.prefab` for each entry, calls `ItemRow.Bind(entry,
   discoveredState)`, then wires the scroll list (see UIScrollWindow
   Reference below).

**Close path (Escape → hidden window):**

7. Player presses Escape → `HideAllInventoryAndCraftingUI` is called.
8. CoreLib's postfix on `HideAllInventoryAndCraftingUI` calls
   `IModUI.HideUI()` for every registered mod UI.
9. `ItemChecklistWindow.HideUI()` calls `ClearRows()` — Destroys all row
   GameObjects (with `pugText.Clear()` pre-destroy leak fix) and resets scroll.

**Auto-hide/cursor/WASD-block/Escape handling:** all handled by CoreLib
and CK's `isAnyInventoryShowing` postfix chain. Zero additional Harmony
patches needed for any of these.

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

`UIScrollWindow.Awake()` calls `GetComponent<IScrollable>()`. If the result
is `null`, the scroll window **permanently disables itself** — `enabled = false`
and the component stops processing input. The `IScrollable` implementor
(`ItemChecklistContent`) must be on the same `GameObject` as
`UIScrollWindow`, or `Awake` must fire after the component is added.

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

After spawning or replacing scroll content, call in this order (ItemBrowser
`EntriesList.SetEntries` pattern):

```csharp
API.Reflection.SetValue(MiScrollable, scrollWindow, scrollableImpl);
API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
scrollWindow.SetScrollValue(1f);  // or scrollWindow.ResetScroll()
```

### LateUpdate Scroll Processing

`UIScrollWindow.LateUpdate` applies the current scroll delta to
`content.localPosition.y`. It reads mouse scroll wheel input only when the
cursor is inside the window bounds (checked via `bounds.Contains`).

### IScrollable Contract

```csharp
public interface IScrollable
{
    void UpdateContainingElements();  // no-op in IB — content manages itself
    bool IsBottomElementSelected { get; }
    bool IsTopElementSelected { get; }
    float GetCurrentWindowHeight();
    float TotalHeight { get; }        // negative: grows downward from 0
}
```

**Key implementation notes (ItemBrowser reference):**

- `UpdateContainingElements` is a no-op in IB's `EntriesList` — do not
  try to use it for layout updates.
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

## Data Architecture

### ItemCatalog Two-Loop Bake (Iter-3.7)

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

**Resulting catalog size:** ~10720 entries (~1240 standard + ~9480
cooked-food permutations: 3160 pairs × 3 tiers).

**Expected bake time:** < 200 ms on a typical machine. If bake time
exceeds 500 ms, consider the Iter-3.8 async-bake strategy.

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
