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
  !Manager.load.IsLoading()`; Iter-15 later appends `!cutsceneIsPlaying`) replaces the
  unreliable `player != null` gate on both the HUD and the F1 open-guard. Closes the
  **loading-screen** half of Iter-15 below.
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
- **Iter-14.2 -- code refactor / optimisations. DONE** (see
  `docs/iteration-history.md`). Five consolidations, build-gated + smoke-tested
  (R1→R2→R4→R5→R3): `ClickButton` base for the five `ButtonUIElement` click
  prologues; `FacetedFilterWidget`→`FilterWidget` + `FacetCheckboxButton`→
  `FilterCheckboxButton` rename (GUID-preserving `git mv` + verified prefab
  field-key edit); removed the redundant `_scrollable` reflection (the prefab
  `scrollable` field is the single source — `UIScrollWindow.Awake` copies it
  itself, confirmed against a main cross-build); `PugText.RenderNoWrap` helper;
  and `PopupWidget` base sharing the Sort/Filter popup machinery (the one-row
  offset captured once as the abstract `FirstRowOffset`). Behaviour-neutral; net
  C# +23 LoC (structural win — single sources of truth — not line count).
- **Iter-15 -- F1/HUD over the intro cutscene. DONE** (see
  `docs/iteration-history.md`). Appended `!sceneHandler.cutsceneIsPlaying` to the
  shared `WorldState.IsInPlayableWorld`, suppressing **both** the F1 open-guard
  and the always-on HUD during the spawn-from-Core intro cutscene. One-line
  behavioural change — both consumers already gate on the shared predicate (the
  Iter-11.6 structure). The cutscene fades CK's own HUD via
  `FadeOutAllGameplayUI()` (not `ShowHUD(false)`), which does not cull our
  layer-27 HUD, hence the explicit gate. `cutsceneIsPlaying` is CK's own
  discovery-path signal; sandbox-safe.
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
- **Iter-19 -- search-field word-wrap crash. DONE** (see
  `docs/iteration-history.md`). `SearchBar` overrides `Awake` to force
  `pugText.maxWidth = 0` after `base.Awake()`, removing the search field's PugText
  from CK's buggy `PugFont.AddNewLinesToLinesExceedingMaxWidth` word-wrap path
  (per-frame `IndexOutOfRangeException`, 127× on main → 0). **Corrected the
  roadmap's own fix candidate:** the prefab `pugText.maxWidth = 0` is a no-op —
  CK's `TextInputField.Awake` rewrites it to `maxWidth + 1 = 8.5` at runtime, so
  the fix had to come from code. Visual width clipping is preserved via the field's
  own `maxWidth` (7.5) through `TrimTextToFitRestrictions` (a char-trim, independent
  of the word-wrap). Pure behavioural C# (one `Awake` override); no prefab/art
  touch. Same CK PugFont bug class the Iter-9 ASCII hint + Iter-11 `RenderNoWrap`
  labels sidestepped.
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

- **Iter-20 -- possession counts. DONE** (see `docs/iteration-history.md`).
  A second completion axis beside discovery: per checklist row, how many of that
  item the player currently **owns** (carried + base storage/display), with the
  checkbox + tick tinted blue when owned >=1 and an "In/Not in possession" filter.
  Possession = the player's carried inventory (always live) + placed furniture and
  storage/display contents within `AnchorRadius` of a **clustered** crafting-station
  anchor; persisted per character as a per-(x,z) ledger (`API.ConfigFilesystem`),
  with remote containers "remembered" so the player can check ownership from
  anywhere. Persistence rides CK's own character save via a `SaveManager.WriteCharacter`
  Harmony hook (the GUID-clear save never fired on a normal "Save & Quit"). The
  cluster filter excludes lone remote stations (a boss arena / outpost) from
  anchoring foreign world loot. **Deferred:** tamed pets + the mod training Dummy
  (typed `Creature`/900) -> Iter-16; the "Ancient Chest (Items/...)" raw-term
  display -> follow-up. **Known limitation:** a *clustered* foreign base (NPC
  village / second base) still anchors -- true base detection is unsolved (CK has no
  base concept).
- **Iter-21 (tentative) -- missing catalog entries (e.g. waypoints).** Some items
  -- waypoints (Wegpunkte) among them -- never get a checklist row. Same bug class
  as Iter-7.1 (NonUsable raw materials) and Iter-16 (Creature/Critter): the
  `ItemCatalog.Bake` filter (`ItemCatalog.cs:157-177`) drops whole `ObjectType`
  buckets (`NonObtainable`/`Creature`/`Critter`/`PlayerType`, plus icon-less
  `NonUsable`), and waypoints likely fall into one of them (most plausibly
  `NonObtainable`, where CK tends to file placeable world prefabs). Notable tension
  for the diagnosis pass: the Iter-20 `PossessionScanner` already recognises a
  waypoint as a `PlaceablePrefab` and counts it in possession
  (`PossessionScanner.cs:120`), so the item is known to the *world scan* but absent
  from the *catalog* -- the exact exclusion path is to be confirmed in-game, not
  assumed. Open questions for the design pass: (1) which `ObjectType` / filter
  branch actually drops the waypoint (decompile + in-game `GetObjectInfo` probe on
  the waypoint ObjectID), (2) whether the fix is a narrow allow-list (like the
  Iter-7.1 icon guard) or a broader rethink of the type-exclusion list, and
  (3) whether other placeable prefabs are silently missing for the same reason --
  audit the full set, don't patch waypoints alone. As with Iter-7.1, IB's full
  `IsNonObtainable` can't be reused (needs ECS/registry APIs the sandbox blocks).
  Not yet elaborated.

See `git log` for canonical per-iter merge points and `docs/superpowers/specs/`
for design docs.
