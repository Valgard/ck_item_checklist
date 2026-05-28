# Changelog

All notable changes to this mod will be documented in this file. The format
is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
without strict adherence — entries describe what shipped per iteration, not
every commit.

## [0.1.0] — 2026-05-28

First documented release. Mod was in dev-only state through iterations
1 through 3.6; this changelog starts at Iter-3.7 (variation-aware
cooked-food tracking) and will accumulate forward.

### Added

- **Variation-aware Cooked-Food tracking.** Each concrete `(ingredient1,
  ingredient2)`-permutation is now a separate discovery token in the
  checklist (e.g. Mushroom-Soup ≠ Tomato-Soup), mirroring CK's own
  per-permutation tracking. Catalog grows from ~1750 to ~10720 entries
  (15 base recipes × 3 tier-items × 3160 symmetric ingredient pairs,
  filtered by `IsIngredientObsolete`).
- **Discovery-quote display in window title:** `"Item Checklist — N / M
  (X.Y%)"` where N = discovered count, M = catalog total. Percent
  rendered in current locale (DE: `0,3%` / EN: `0.3%`).
- **Live title-refresh on discovery events.** Title counter updates
  immediately when the player cooks a new permutation, without
  window-reopen.
- **Performance instrumentation** via `[ItemChecklist] PERF` log entries
  in `Catalog.Bake()` and `Window.SpawnRows()`. Used to validate the
  Iter-3.8-trigger-thresholds (`bake > 8s` / `spawn > 2s`); both well
  under thresholds at launch (`bake-total=384ms`, `spawn=768-905ms`).

### Changed

- **`DiscoveredState` key schema:** `HashSet<int>` → `HashSet<long>` with
  packed `(objectId, variation)` keys via new `PackKey(int, int)` helper.
- **`ItemCatalog.Bake()` two-loop architecture:** standard items (Loop 1,
  unchanged from Iter-3.6 except `IsCookedFood()`-skip) + new
  α-enumeration (Loop 2) for cooked-food permutations using
  `CookedFoodCD.GetPrimaryIngredient` + `CookingIngredientCD.turnsIntoFood`.
- **Removed Iter-3.6 DE-locale TrimStart-workaround** (no longer needed
  since family-items with variation=0 are filtered out via
  `IsCookedFood()` before name resolution).

### Notes

- **Save-compatible with Iter-3.6 saves.** CK's `discoveredObjects2`
  serialization has always included the `variation` field; Iter-3.6
  ignored it on read, Iter-3.7 uses it. No migration code needed.
- **Tier-3 (Rare/Epic) tracking is included but empirically unverified
  for live cooking events** (the verifier-character had insufficient
  cooking-talent for RNG-rolling Rare/Epic at test time). Conservative
  assumption: each cooking pair can yield 3 distinct discovery tokens
  (Base/Rare/Epic). If the assumption is wrong, unreachable tier-entries
  remain grayed-out — functionally correct, cosmetically suboptimal.

### Iter-3.8 follow-ups (not in this release)

- **Persistent-row lifecycle for window-open latency.** Current spawn is
  ~800ms on 10720 GameObjects per Window-Open; subsequent-opens could
  drop to ~5ms by skipping `ClearRows`+respawn when the window only
  toggles visibility. Architecturally: `HideUI` keeps rows alive,
  `ShowUI` guards `SpawnRows` on `_spawnedRows.Count == 0`, `ClearRows`
  restricted to `RebindRows` (after Loc-Change re-bake) and `OnDestroy`.
- **Mod-aware origin aggregation** for cooked-food permutations: a
  Vanilla family produced from a Mod-ingredient currently shows
  origin="Vanilla". Aggregation `OR(i1.modOrigin, i2.modOrigin,
  output.modOrigin)` would be more accurate.
- **Empirical Tier-3 verification:** Once a high-cooking-talent character
  is available, re-run discovery test to confirm Rare/Epic items are
  individually discoverable (= × 3 assumption holds) vs. only-Base
  (= × 3 over-counts catalog by factor 3).
