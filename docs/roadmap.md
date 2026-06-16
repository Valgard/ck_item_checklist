# ItemChecklist — Future Roadmap

A living done/planned ledger (started 2026-06-04). Tracks each iteration's
status — DONE entries link to `docs/iteration-history.md`; the rest are the
remaining backlog.

- **Iter-10 — DONE** (see `docs/iteration-history.md`).
- **Iter-11 — DONE** (see `docs/iteration-history.md`). Note: implemented via
  native TextDataBlock generation + `LocalizationGenerator.cs`, **not** CoreLib
  `LocalizationModule` (which is deprecated).
- **Iter-11.5 — DONE** (see `docs/iteration-history.md`). Always-on top-right HUD
  discovery counter (non-modal `UIelement`; HUD-layer + explicit visibility).
- **Iter-11.6 — DONE** (see `docs/iteration-history.md`). Load-screen visibility fix:
  shared `WorldState.IsInPlayableWorld` (`isInGame && isSceneHandlerReady &&
  !Manager.load.IsLoading()`) replaces the unreliable `player != null` gate on both
  the HUD and the F1 open-guard. Closes the **loading-screen** half of Iter-15 below.
- **Iter-12 -- real pixel-art sprites. DONE** (see `docs/iteration-history.md`).
  Replaced every Item Browser placeholder sprite with own pixel-art authored in
  Pixaki, generated into a single `ui_checklist` sheet; rewired all prefab refs
  (zero IB references remain), deleted the dev-only `Art/Bridge/` folder.
- **Iter-13 -- `DropdownWidget` prefab extraction. DONE** (see
  `docs/iteration-history.md`). Extracted the dropdown skeleton into one shared
  `Dropdown.prefab` chrome, consumed by **both** Sort (nested instance) and
  FacetedFilter (**prefab variant**) — the hand-copied skeleton no longer exists
  twice in the window. Nested prefabs + variants round-trip through the
  ModBuilder→AssetBundle pipeline (proven). A minimal `IPopupToggle` seam unified
  the two toggle classes so the chrome carries one shared toggle type. The
  unified-field header redesign (Toggle+AscDesc in one dark `Field` background)
  was explicitly deferred to its own visual iteration.
- **Iter-14.1 -- search-caret alignment. DONE** (see `docs/iteration-history.md`).
  The caret sat a few px too low and flush against the text.
  `TextInputField.Update()` overwrites the caret GO's world X/Y every frame, so
  the offset cannot live on that GO: a child GO (`CaretSprite`) now carries the
  caret `SpriteRenderer` at a constant `localPosition` (+1px up to centre, +2px
  right for a small gap). The caret was also shortened 8px->7px via the sprite's
  existing vertical 9-slice (SR Sliced draw mode, size 2x7) -- a pure-prefab
  change, no sheet/generator touch. **Corrected state note:** the sprite swap
  (1x1 `white_pixel` -> the painted 2x8 `Caret` sheet sprite) and the
  `{0.8,6,1}` scale-hack removal were already done in Iter-12; only the
  position/height remained for 14.1.
- **Iter-14.2 -- code refactor / optimisations.** General C# cleanup (e.g. unify
  the `ButtonUIElement` subclasses' guarded `OnLeftClicked` into one abstract
  base). Open scope -- gets its own brainstorm. Kept off the Iter-13
  `DropdownWidget` extraction terrain. Carries two deferred renames from Iter-18:
  the `FacetedFilterWidget` class -> `FilterWidget` (the prefab is already
  `Filter.prefab`), and the Filter variant's checkbox-row GO is now
  `CheckboxTemplate` (an Iter-13 `RowTemplate` naming leftover, fixed in Iter-18).
- **Iter-15 (tentative) -- F1 guard misses cutscenes.** *(Loading-screen half DONE
  in Iter-11.6.)* The F1 toggle in `ItemChecklistMod.Update` now blocks opening
  during both load screens via `WorldState.IsInPlayableWorld`, but it does **not**
  yet block during in-game **cutscenes/intro**, so F1 can still pop the checklist
  over those. Remaining fix = a cutscene / input-locked flag (needs ILSpy + sandbox
  verification). Bugfix follow-up to Iter-4's toggle guard.
- **Iter-16 (tentative) -- pet/creature discovery.** The bake blanket-excludes
  `ObjectType.Creature`/`Critter`, so tamed pets/critters never get a row -- same
  bug class as the Iter-7.1 NonUsable fix. IB keeps anything with `PetCD`
  (`ObjectUtility.cs:390`) and craftable non-cattle creatures (`CraftingCD &&
  !CattleCD`, `:393`); a fix would mirror those, still dropping wild mobs.
  `PugDatabase.HasComponent<T>` is sandbox-safe here. Sibling to Iter-7.1.
- **Iter-17 (tentative) -- per-variation/skin tracking.** The bake collapses every
  family to its `variation == 0` entry (`ItemCatalog.cs:130`), so colour/skin/state
  variants never get their own row. CK tracks discovery per `(objectID, variation)`
  and IB exposes `ignoreVariation` (`ObjectUtility.cs:422`); we hardwired "ignore
  variation" to keep a one-tick-per-item checklist. Revisit only with a UI story
  for grouping/expanding variants. Distinct from the Iter-7.1 catalog fix.
- **Iter-18 -- combobox header + skeleton chrome. DONE** (see
  `docs/iteration-history.md`). The header is now one cohesive `Display` field:
  the caret moved inside it (the separate `ToggleButton` GO + its button-bg are
  gone), and the sort `AscDesc` toggle moved into the `Display` too (Sort-only).
  This also completed the Iter-13 extraction: `Dropdown.prefab` is now a pure
  **skeleton** (`Field/Display` + empty `Popup/RowContainer`), consumed by **two
  sibling variants** -- `Sort.prefab` (adds `DropdownWidget` + `RowTemplate` +
  in-Display `AscDesc`) and `Filter.prefab` (renamed from `FacetedFilter.prefab`,
  adds its own templates). Pure-prefab, **zero behavioural C#**. The
  `FacetedFilterWidget` class rename -> `FilterWidget` was deferred to Iter-14.2.

See `git log` for canonical per-iter merge points and `docs/superpowers/specs/`
for design docs.
