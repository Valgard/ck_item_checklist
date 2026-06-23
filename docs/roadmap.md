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
- **Iter-16.1 -- per-skin pet collection. DONE** (see `docs/iteration-history.md`).
  **Re-scoped:** the roadmap's premise was wrong -- `ObjectType.Pet` (802) is not
  excluded, so pets were already in the catalog. Real work: each pet **skin** is a
  separate collectible. One catalog row per `(objectID, skinIndex)`; a mod-owned
  "ever-owned" `PetCollection` ledger (CK tracks no per-skin discovery) persisted via
  the Iter-20 store; spoiler-consistent display (species name vs `???`, collected vs
  unknown icon); gradient skin icons via the `Amplify/UISpriteColorReplace` shader
  (the Item Browser recipe); active summoned pet now counted (fixes the Iter-20
  Terrier 7-vs-8 undercount); Level/Value em-dashed (LevelCD is a tier field, not the
  trainable per-instance level); new "Pets" filter category.
- **Iter-16.2 -- critter collection. DONE** (see `docs/iteration-history.md`).
  Iter-7.1-style relaxation of the `ObjectType.Critter` bake exclusion to an
  icon-guarded keep; the catchable critters flow through the existing discovery /
  possession / rendering machinery (zero code for Level/Value/Discovery/Possession).
  New `Critters` / `Krabbeltiere` filter category. **The "~15, 9800-9819" probe figure
  was wrong** -- an in-game probe found **25** (the full 20 at 9800-9819 + 5 Fireflies
  at 3500-3504, German `Glimmkäfer`); ground truth (player had them in chests + they
  ARE bug-net-catchable) confirmed all 25 are discovery-trackable, no ghost rows.
  Catalog 10885 -> 10910.
- **Iter-16.3 (tentative) -- cattle (farm livestock) collection.** Farm animals
  (`CattleCD` + `ObjectCategoryTag.Cattle`) are a third creature family beside
  Pets/Critters and currently get **no** checklist row: their `ObjectType` is
  `Creature` (900), which `ItemCatalog.Bake` explicitly skips
  (`ItemCatalog.cs:191`, alongside `NonObtainable`/`PlayerType`). Confirmed
  in-game by the player (has Muhline/Bummbock/Bummelkugler = Moolin/Bambuck/
  Strolly Poly, internal `Cow`/`Goat`/`RolyPoly`, ObjectIDs **1300/1302/1303**;
  babies 1304/1305/1306). Likely an Iter-7.1/16.2-style bake relaxation
  (`Creature` + a `CattleCD` guard, mirroring the `Critter` icon-guard) so the
  three species flow through the existing discovery/possession/rendering
  machinery; a new `Cattle` / `Nutztiere` filter category. **Open design
  question (decide first, à la Iter-16.1 pets):** one row per *species* vs. one
  per *individual/variant* -- caged cattle are stored as the **same** ObjectID
  with non-zero `auxDataIndex` carrying per-animal state (`NameCD`/
  `MealsEatenCD`/`BreedToggleCD`), the pet-skin aux-data pattern, so there is no
  per-variant ObjectID to key on; a per-species row is the natural default.
  **Verify first** (the standing lesson -- creature typing has been wrong 3x):
  confirm the `Creature` ObjectType against the actual game prefab (it lives in
  the per-prefab authoring asset, not the decompile) and that 1300/1302/1303 are
  the only base cattle. Babies and the `CattleFeedTray` (1301) / `CattleCage` (8)
  items are separate questions. Requested 2026-06-23. See the
  `reference_ck_cattle_objecttype` memory.
- **Iter-17 (tentative) -- per-variation/skin tracking.** The bake collapses every
  family to its `variation == 0` entry (the `od.variation != 0` guard in
  `ItemCatalog.Bake`), so colour/skin/state
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
- **Iter-21 -- possession spoiler-gated behind discovery. DONE** (see
  `docs/iteration-history.md`). **Re-scoped** from the tentative "missing catalog
  entries (waypoints)" framing: a throwaway diagnostic probe proved the catalog is
  **complete** -- the WayPoint (ObjectID 6514) *is* baked at variation 0
  (`ACCEPTED`, `type=PlaceablePrefab`, icon present); the cross-source diff found
  **0** obtainable items missing from the bake source; and the 272 `NonObtainable`
  drops are genuinely non-obtainable (boss spawn anchors, projectiles, affixes).
  The real issue: an **undiscovered** (`???`) row still showed an owned count + blue
  checkbox -- the incoherent "owned but never discovered" state for **world-spawned
  placed objects** (a Core WayPoint the player never mined, which the Iter-20 world
  scan counts). Fix: `ItemChecklistMod.OwnedCount(objectId, variation)` returns 0
  unless the row is discovered (the same flag that drives `???`-vs-name), so the
  owned column, blue tint, **and** the In/Not-in-possession filter all treat an
  undiscovered item as not owned -- aligning possession with the existing Iter-10
  spoiler guard (Level/Value already em-dashed when undiscovered). One chokepoint,
  both read sites routed through it. Pure behavioural C# (+20/-2); no prefab/art
  touch. The variation-keyed-discovery question (a family discovered only at a
  non-0 variation would still show `???`) was raised during diagnosis and
  **deferred to Iter-17** -- the gate is correct regardless of that.
- **Iter-22 (tentative) -- row-hover tooltips.** On hovering a checklist row, show
  the item tooltip like CK's normal inventory slots do (name, description, stats).
  CK renders slot tooltips via its own tooltip UI on hover; a row would need to
  feed that tooltip system (or a mod-built equivalent) the row's `(objectID,
  variation)`. Requested 2026-06-21. Scope/feasibility (which tooltip API is
  reachable + spoiler-gating for undiscovered rows) to be measured before design.
- **Iter-23 -- rebound toggle key ignored; F1 always opens. DONE** (see
  `docs/iteration-history.md`). `ItemChecklistMod.Update` polled BOTH the rebindable
  Rewired action (`GetButtonDown(ToggleActionName)`) AND a raw
  `Input.GetKeyDown(KeyCode.F1)` in an OR, so F1 stayed a hardcoded opener even after
  the player rebound the key in settings. Fix: dropped the raw-F1 fallback (it was never
  gated to diagnostic-only) so only the bound action toggles — the Rewired path already
  covers the default F1 binding. Pure behavioural C# (one OR-term removed); no prefab/art
  touch.
- **Iter-24 -- scrollable + collapsible filter popup. DONE** (see
  `docs/iteration-history.md`). Re-scoped from scroll-only into a two-layer **A+C**
  design. **(A) Scroll:** popup capped to `MaxVisibleRows` (default 6) and scrolled by
  **manual translate** (not CK's `UIScrollWindow` — its Awake self-disable + no
  virtualization needed), with a popup `SpriteMask` (band 56..63, above the window
  mask) + a hand-rolled draggable scrollbar. All base-wired in `PopupWidget` /
  `Dropdown` skeleton, **runtime-discovered** (no fragile cross-ref); Sort made
  scroll-ready too. gap-A (bounds-checked click-outside via `Manager.camera`), gap-F
  (Harmony prefix on `UIScrollWindow.UpdateScroll` so the wheel doesn't leak to the
  main list). Filter x-offset relocated from child overrides to the window instance.
  **(C) Collapse:** clickable `SectionHeaderButton` headers, multi-open, default
  all-open, `static` closed-set keyed on the **stable loc term** (survives language
  change); carets shift with the scrollbar. Requested 2026-06-21, done 2026-06-22.
- **Iter-25 -- small-font umlaut rendering. DONE** (see `docs/iteration-history.md`).
  **Re-scoped from "umlauts" to "thinTiny lacks accented glyphs".** The chrome labels
  render in `thinTiny` (= the `rrs5` atlas), CK's reduced **digits-only** face (114
  glyphs, no accents); CK's `PugFont.GetGlyphData` falls a missing `ö` back to the
  **chinese** font (CJK metric) → deformed. Fix: a runtime patch
  (`ThinTinyGlyphPatch.InsertOnce`, at the `OnOccupied` anchor) inserts **85
  mod-authored accented glyphs** into `thinTiny` — new `glyphData` entries +
  `codePoints`, `volatileSprite` cut via `Sprite.Create` from a bundle sheet
  (`Art/thinTiny_glyphs.png`), replicating `PugFont.InitCodePoints`' `rect2`+centered-pivot
  convention exactly. Glyphs hand-drawn in Pixaki (`sources/thinTiny_full.pixaki`),
  extracted 3-layer (Atlas=sprite, Rects=advance width, thinSmall arrangement=char).
  Covers full Western-European + partial Eastern-European/Cyrillic/typography. thinTiny
  is digits-only in CK, so the global insert is harmless. Full font architecture in the
  `reference-ck-pugfont-architecture` memory + `docs/research/pixaki-format.md`.

> **Out-of-sequence numbering is intentional.** Iteration numbers are assigned both
> sequentially-by-merge and topic-reserved, so a DONE iter can sit before lower-numbered
> tentative ones (e.g. Iter-16.1 done, Iter-16.2/17 still open) — timing ≠ number. See
> `docs/conventions.md § Branch + Commit Conventions`.

See `git log` for canonical per-iter merge points. Design docs: retained
(ADR-gated) specs live under `docs/specs/`; transient plans + brainstorming scratch
under `docs/superpowers/` (gitignored).
