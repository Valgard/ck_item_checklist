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
- **Iter-16.3 -- cattle (farm livestock) collection. DONE** (see
  `docs/iteration-history.md`). Cattle = a third creature family, shipped **critter-like**
  (option A): admitted to the catalog via a `HasComponent<CattleCD>` bake relaxation +
  a `Cattle`/`Nutztiere` category, flowing through CK's native `(objectID, var0)`
  discovery; a `PossessionScanner` `CattleCD` branch counts penned (live, near-anchor) +
  caged cattle, credited to the adult. **Roster corrected by measurement:** 6 adults
  (Cow/Goat/RolyPoly/Turtle/Dodo/Camel) + 6 babies — not the roadmap's "1300/1302/1303".
  Babies are **folded** into the adult via a structural `BreedStateCD.babyType` map
  (`CattleRegistry`, no name parsing). Catalog 10910 → **10916**. The "row per species vs
  variant" question resolved to **per species**; an ever-owned ledger was built then
  **deliberately removed** once a probe showed CK *does* discover cattle, **per colour
  variation** — so per-variant tracking is the proper fix, deferred to **Iter-17** (see
  there). Shipped limitation: a cattle owned but only discovered at a non-0 variation
  shows `???` until Iter-17. Requested 2026-06-23, done 2026-06-25.
- **Iter-16.4 -- discovery filter/count ignores pet skins. DONE** (see
  `docs/iteration-history.md`). Built exactly to the planned fix shape: the Iter-21
  chokepoint pattern's discovery twin. `ItemChecklistMod.IsCollected(objectId,
  variation)` (pet-skin → `PetCollection`, else `DiscoveredState` — this IS the
  `ItemRow.showDetails` tick) + `CollectedCatalogCount()` (the `N` numerator over the
  catalog) now back the Discovery filter + `DiscoveredInView` (`ItemListViewModel`),
  the window footer + HUD `N / M`, and the `ItemChecklistContent` `showDetails` branch
  — one source of truth, no drift. The always-on HUD is nudged after the possession
  scan when the tally changes (collecting a skin fires no `DiscoveredState.Changed`).
  Behaviour-identical for non-pet rows. The now-dead injected `DiscoveredState`
  field/ctor-param on `ItemListViewModel` were removed. Pure behavioural C# (+84/−21);
  no prefab/art touch. Critters (Iter-16.2) were unaffected (normal CK discovery).
- **Iter-17 -- per-variation/skin tracking. DONE** (see `docs/iteration-history.md`).
  Two **buckets**, both reshaped by in-game measurement (`objectsByType` is
  `(objectID, variation)`-keyed → in-dict = DB-authored, absent = runtime).
  **Bucket 2 (cattle):** the pet-skin split with a native signal. CK exposes no colour-
  count API, but each cattle prefab's `ObjectPropertiesCD.PossibleChildVariation[]`
  (prop 239678920) IS the palette — verified `{0..4}` (5 colours), sandbox-safe.
  `CattleRegistry.ColoursOf` reads it; Loop 4 emits all 5 colour slots/species always,
  `nameKnown` species-gated via `IsDiscoveredAnyVariation` (pet-skin parity, fixes the
  Iter-16.3 `???`-on-non-0 trap); per-colour possession from the live entity's variation.
  **Bucket 1 (placeables):** Loop-1 guard-lift kept behind a `PaintableObjectCD` filter
  (cosmetic colours in, chest/seed state-junk out, +179 rows); reveal-all via
  `Entry.IsColourVariant`; the 14 paint-colour names come from the paintbrushes
  (`PaintToolCD.paintIndex`, enum name minus `PaintBrush`, localized via own
  `ItemChecklist-PaintColor` terms — "(Rot)" not "(Farbe 3)"). Catalog 10916 → 11119.
  The generic Bucket-2 loop (non-cattle runtime variants) was measured empty and not built.
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
- **Iter-22 -- row-hover tooltips. DONE** (see `docs/iteration-history.md`).
  Hovering a row shows CK's native item tooltip + a slot-hover highlight. The
  tooltip is **selection-driven, not entity-driven**: each `ItemRow` (already a
  `UIelement`) gets a 3D collider so CK's `UIMouse` hover-selects it, and overrides
  the four `UIelement` hover virtuals, delegating to one shared `TooltipSlot :
  SlotUIBase` fed an arbitrary `(objectID, ckVariation)` — the Item Browser recipe.
  Spoiler model: discovered rows show the full tooltip; undiscovered (`???`) rows
  highlight but show only a minimal `??? - not yet discovered` placeholder (never
  the real item). Highlight is a prefab-authored SpriteRenderer driven per-frame in
  `LateUpdate`. Hover is gated on a viewport bounds check
  (`ItemChecklistContent.PointerInViewport`) so the full-width row colliders, which
  extend past the window mask, don't hover a clipped row from the header/footer.
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
- **Search-field focus race recurrence (tentative).** Iter-20's mitigation (run the
  scan + `ListView.Refresh()` **before** `OpenModUI` so the rebind doesn't race the
  search field's focus-init) is **incomplete**: the race **recurred during Iter-17** —
  after a list refresh the caret blinks but keystrokes are swallowed until another
  widget is clicked (workaround: click any other widget first, then the search field).
  No exceptions are logged when it happens (a focus/timing-ordering issue, not a crash).
  A future iter should re-investigate the open-time refresh/focus ordering. See
  `docs/gotchas.md § Per-Variation Tracking (Iter-17) → Search-field focus race`.

> **Out-of-sequence numbering is intentional.** Iteration numbers are assigned both
> sequentially-by-merge and topic-reserved, so a DONE iter can sit before lower-numbered
> tentative ones (e.g. Iter-16.1 done, Iter-16.2/17 still open) — timing ≠ number. See
> `docs/conventions.md § Branch + Commit Conventions`.

See `git log` for canonical per-iter merge points. Design docs: retained
(ADR-gated) specs live under `docs/specs/`; transient plans + brainstorming scratch
under `docs/superpowers/` (gitignored).
