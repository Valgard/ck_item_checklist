# ItemChecklist — Future Roadmap

Frozen 2026-06-04. The backlog of planned iterations (Iter-12 onward).

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
- **Iter-13 -- `DropdownWidget` prefab extraction.** Extract the widget into a
  standalone/nested prefab for true reuse.
- **Iter-14 -- code refactor / optimisations + search caret vertical alignment.**
  General C# cleanup; plus the search caret sits a few px too low --
  `TextInputField` forces `characterMarkBlinker.transform.position =
  pugText.position` every frame, so the fix is to move the white_pixel into a child
  GO with a +Y offset and rewire `CharacterMarkBlinker.sr`.
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

See `git log` for canonical per-iter merge points and `docs/superpowers/specs/`
for design docs.
