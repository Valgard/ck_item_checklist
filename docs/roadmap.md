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
- **Iter-26 (tentative) -- search-field focus race fix.** Iter-20's mitigation (run the
  scan + `ListView.Refresh()` **before** `OpenModUI` so the rebind doesn't race the
  search field's focus-init) is **incomplete**: the race **recurred during Iter-17** —
  after a list refresh the caret blinks but keystrokes are swallowed until another
  widget is clicked (workaround: click any other widget first, then the search field).
  No exceptions are logged when it happens (a focus/timing-ordering issue, not a crash).
  Re-investigate the open-time refresh/focus ordering — likely a deeper fix than the
  Iter-20 reorder: e.g. defer the search field's `SetActiveInputField` to the frame
  *after* the post-open refresh settles, or re-assert focus once `ListView.Refresh()`
  has rebound the rows. See `docs/gotchas.md § Per-Variation Tracking (Iter-17) →
  Search-field focus race`.
- **Iter-27 -- possession-scan perf (in-base stutter). DONE** (see
  `docs/iteration-history.md`). The 3s `PossessionScanner.Scan` iterated ~1300
  loaded-world entities reading `ObjectDataCD` + `LocalTransform` via a **per-entity
  `GetComponentData`** (random chunk lookup) for each. A throwaway PERF probe measured
  it in a built-up base: the per-entity `loop` phase dominated, with total-scan spikes
  of **~21ms** — past the 16.7ms@60fps frame budget, so one dropped frame every 3s =
  the reported periodic in-base stutter (CPU-bound, not GC — the alloc/`build` phase
  stayed small). Fix = **F1 only**: copy the two universally-read components in bulk via
  `ToComponentDataArray` (chunk-sequential memcpy) and index `ods[i]`/`xforms[i]` in the
  loop (index-aligned with `ents[]`); per-entity `em` access stays only for the player +
  the gated near-anchor minority. Measured after: **MAX 21.5→9.6ms** (under budget),
  loop avg 3.49→1.82ms (−48%), p99 16.4→6.6ms, same entity/anchor counts (behaviour-
  neutral). F3 (cache `ResolveWorld`), F2 (anchor spatial-hash) and F4 (reuse alloc
  buffers) were all **measured unnecessary** — `world`/`setup` were 0.26/0.12ms and only
  44 anchors. Pure behavioural C# (one query split + two loop reads); no prefab/art.
- **Iter-28 -- possession scan: exclude world nature. DONE** (see
  `docs/iteration-history.md`). A *second*, distinct stutter from Iter-27: peaks that grow
  with session length and persist away from base. On-disk evidence: the possession ledger
  had grown to **5503 entries / 89 KB**, ~90% **world-spawned nature** (bushes/grass/kelp/
  stalagmites/lilies/ruins) counted as "owned" by Iter-20's place-object path and remembered
  forever. The real peak was **not the scan** but the **autosave `Serialize()`** of that
  89 KB ledger (12–37ms main-thread spike, also pushing CK's host sim over budget). **No
  object-level signal separates wild nature from placed objects** in CK (cat/stack/icon/
  craft/sell/tags/DontDropSelfCD/Diggable/Destructible all collide: Stalagmite ≡
  CavelingFloorTile, GraveTree ≡ WayPoint), proven over three in-game probe rounds — so the
  filter is a **curated tag+ObjectID blacklist** (`PossessionClassifier.IsWorldNature`: tags
  Greenery/Destructible/CattleKelpFood/Ruins + a short tag-less-straggler ID list, editable).
  Gated on **path #1 only** (placed-object count) — container contents + carried untouched,
  so nature stored in a chest still counts and remember-from-afar is preserved. A **one-time
  `PruneByPredicate` at first scan** evicts the pre-existing backlog (`PruneStaleNear`'s
  180-tile window was far too slow), ledger **5503→~520**, save spike + host-overrun warnings
  gone. Verified smooth in-game (1.2.1.5). Process lesson: a runaway background decompile-grep
  ate CPU through every test and confounded the "is it smooth?" signal for several rounds —
  kill stray background jobs before measuring perf.
- **Iter-30 -- config-gated possession diagnostic log. DONE** (see
  `docs/iteration-history.md`). A permanent, default-OFF diag (`PossessionConfig.Diagnostics`,
  a second `API.Config` key) so a recurring stutter or blacklist gap is captured without a
  throwaway probe: per-scan timing + ledger size, per-save serialize/write, and a one-time
  dump of every counted placed object with its `IsWorldNature` verdict. Zero overhead when
  off. Drove all of Iter-31.
- **Iter-31 -- possession scope: anchor the base on workbenches. DONE** (see
  `docs/iteration-history.md`). Two new post-Iter-28 symptoms (residual save-write hitch +
  "lag spikes outside base") traced — by parsing the real savegame ledger, **not** inference —
  to one root cause: the scan anchored on **any `CraftingCD`**, so world structures the player
  explored (abandoned-camp campfires, a vault's seed extractor) passed the ≥2-cluster filter
  and anchored their loot chests + surrounding nature/boulders as "owned" (~90 of 523 entries
  were remote world loot 337–693 tiles from base — GlowingCoral, world-chest GoldBar/armor/
  keys). The discriminator is **semantic**: a base is built around a **Workbench** (CK places
  none in world structures; verified 11 at base / 0 remote). Anchors = workbenches + the
  stations within a workbench's radius (link workbench→station only, a single workbench
  suffices → cluster filter gone). + a 64-bit-FNV save-write-skip, a `#icl-ledger-v2` one-time
  migration (discard polluted pre-fix ledgers), and the near-base OreBoulder blacklist.
  Measured: ledger 523→403 / 0 remote, save-skip dominates, scan ~1ms outside base,
  host-overrun 4 vs 626. The "outside-base spike" itself was **not ItemChecklist** — isolation
  (disable via `state.json`) confirmed *Enemy Health Bars* (per-enemy rendering); the first-pass
  ComputeBuffer-GC theory was a red herring (those warnings fire only at process shutdown).
- **Iter-29 (tentative; largely obsoleted by Iter-31 — the workbench-anchor fix shrank the
  ledger and killed the remote churn, so the scan is now ~1ms outside base / ~4–5ms at base
  with no felt hitch; kept only as a nice-to-have) -- chunk / time-slice the possession scan.** The 3s
  `PossessionScanner.Scan` iterates the full ~2000-entity loaded-world set on a single tick;
  a ~9.6ms scan on one frame every 3s is a felt micro-hitch even "under budget". Spread one
  pass across several ticks so no single tick does the whole scan. **Not a regression** (1.0.2
  had the identical scan and was smooth) — a nice-to-have smoothness pass. Real complexity to
  weigh: hold the entity snapshot across frames (Persistent allocator + disposal, not
  TempJob), accumulate `liveKeys` across all chunks **before** `PruneStaleNear` runs (it
  assumes a full pass), and handle world-unload mid-pass. Measure the current scan first; the
  simpler lever (raise the interval 3s→5–6s) may suffice. Requested 2026-06-28.
- **Iter-32 -- cooked dishes double-counted in discovery. DONE** (see
  `docs/iteration-history.md`). **Root-caused by measurement, not the roadmap's guess.** A
  golden-ingredient recipe's `CookingIngredientCD.turnsIntoFood` points straight at a
  **Rare** family ID (e.g. `CookedPuddingRare` 9551), whose `CookedFoodCD.rareVersion`
  **self-references** (9551→9551) — so Loop 2's tier fan-out emitted that
  `(rareId, variation)` via BOTH the base and the rare branch (same baseFamily → same food
  variation), duplicating the catalog row so the dish counted twice in `N / M`. Fix: a
  `HashSet<long> seenKeys` in `AddCookedEntry` skips the repeat `(objectID, variation)`. The
  roadmap's `epicVersion == rareVersion` guess was **ruled out by measurement**
  (`epicEqRare=0`, `baseEqRare=11`; a `producedBy` probe pinned the base+rare collision on
  the same baseFamily). Measured in-game (1.2.1.5): 11 affected families, 858 duplicate keys;
  `accepted` 11030→10172, catalog 11119→10265, `dupKeys` 858→0; a freshly cooked dish now
  raises `N` by 1 (was 2). Pure behavioural C#; no prefab/art touch.
- **Iter-33 (tentative) -- cooked-food tier reachability / possible phantom rows.**
  Surfaced during Iter-32 (user question 2026-07-12). Loop 2 emits **all three** tiers
  (base/rare/epic) for **every** food variation, but CK's cook logic
  (`Pug.Other:324035`) gates the tier on ingredient rarity: `flag = (an ingredient is a
  Rare-rarity Flower or Legendary)` drives rare/epic. So a variation cooked with a golden
  flower (flag=true) may never yield a *base* tier, and a no-golden variation may never
  yield *epic* — if so, the catalog holds unreachable "phantom" tier rows that stay `???`
  forever, making 100 % unreachable (the Iter-16.4 bug class). **Not yet measured** — needs
  a diagnostic probe that, per variation, logs which tiers CK can actually produce (mirror
  the `flag` + `num3/num4/num5` logic) before deciding whether the α-enumeration must be
  tier-gated. Note: `GetFoodVariation` encodes ingredients only; the tier is a separate
  objectID swap (`rareVersion`/`epicVersion`), so variation ≠ tier. Requested 2026-07-12.
- **Iter-34 (tentative) -- keybind rebind row missing under "Mods" in the controls menu.**
  User-reported 2026-07-12: ItemChecklist's toggle keybind (default F1) has **no rebind row
  under the "Mods" section** of Core Keeper's Controls settings, so the player cannot remap
  it from the menu (the bind still works and is technically rebindable — Iter-23 — but the UI
  entry is absent). **Prüfen warum, dann fixen.** Context: the action is registered via CoreLib
  `ControlMappingModule.AddKeyboardBind` in `IMod.EarlyInit`, with the display name coming from
  the loc term `ControlMapper/ItemChecklist-ToggleChecklistPC` (Iter-11). **Not yet
  investigated** — hypotheses to *verify*, not assume (the standing "measure, don't guess"
  lesson): (a) CoreLib version/API drift — the mod pins `UserInterfaceModule` 4.0.4; the
  `ControlMappingModule` registration may now need a category/section argument (or a separate
  call) to surface the bind under the "Mods" header, or the API that populated it changed;
  (b) the `ControlMapper/...` display-name term fails to resolve → CK's control menu creates
  no visible row (parallels the Iter-25 missing-glyph / Iter-11 loc-term class); (c) a
  registration-timing/order issue between `AddKeyboardBind` and when CK's Controls UI builds
  its section list. **First step:** reproduce in-game, then read a **working reference mod that
  DOES show a rebind row under "Mods"** (the project's standing "read the working mod before
  decompile-guessing" rule) + inspect CoreLib `ControlMappingModule` — before touching code.
  Requested 2026-07-12.
- **Iter-35 (tentative) -- foreign-mod item shows raw objectID as name + missing loc term.**
  User-reported 2026-07-12 (screenshot): a discovered item from **another installed mod** —
  *ChestsGalore*'s `WorkbenchChestExtra`, **objectID 32773** — renders its **name as the raw
  number "32773"** in the checklist row, while its Iter-22 hover tooltip shows CK's
  missing-term placeholder **`missing: Items/ChestsGalore:WorkbenchChestExtra`**. Icon, owned
  count and the discovery tick all resolve correctly — **only the name is broken.** This
  confirms `ItemCatalog.Bake` (Loop 1 over `PugDatabase.objectsByType`) includes **other
  mods' registered items** — correct for a completionist catalog — but their localized names
  don't resolve. **Prüfen warum, dann fixen.** The bug is already half-localized by the two
  disagreeing render paths: the **row label** (ICL's `GetObjectName` port, baked) falls back
  to `objectID.ToString()`, whereas CK's **native tooltip** path falls back to `missing:
  <term>` — so ICL's bake-time name resolution diverges from CK's own. **Not yet
  investigated** — hypotheses to *verify*, not assume: (a) the term is **mod-namespaced**
  (`Items/ChestsGalore:WorkbenchChestExtra`, note the `ChestsGalore:` segment) and ICL's
  resolution / `GetObjectName(localize:true)` path mishandles that form; (b) ChestsGalore
  simply ships **no term** for the item (a *foreign-mod* bug) → the real ICL fix is a **better
  fallback than the raw objectID**: show the term tail, an "Unknown"/`???`-style placeholder,
  or exclude un-nameable foreign items; (c) a **bake-timing** race — baked before the other
  mod's I2 terms load (cf. the Iter-11 loc-term class + the language-change re-bake). **Design
  question underneath it:** should ICL include foreign-mod items at all, and if so with what
  name policy (include-with-fallback vs. exclude vs. a config toggle)? **First step:**
  reproduce with ChestsGalore installed, dump what `GetObjectName(buf, true)` / `(buf, false)`
  return for objectID 32773 at bake time vs. later, then decide the policy before coding.
  Requested 2026-07-12.
- **Iter-36 (tentative) -- counter toggle: discovery count vs. in-possession count.**
  User-requested 2026-07-12: the always-on HUD counter currently shows **how many items are
  discovered** (`N / M`); add a **setting that switches the counter between that discovery
  number and a "how many items you currently own" number**. Both axes already exist in the mod
  — this is a **display-source switch**, not new tracking. Grounded facts (verified 2026-07-12):
  - **Current counter = one source of truth.** Both the HUD (`ItemChecklistHud.Refresh`,
    `ui/ItemChecklistHud.cs:76`) and the window footer (`ItemChecklistWindow.FormatTitle`,
    `ui/ItemChecklistWindow.cs:215`) render `ProgressFormat.Counter(CollectedCatalogCount(),
    total)`. `CollectedCatalogCount()` (`ItemChecklistMod.cs:123`) is the discovery numerator
    `N`; `total` is `Catalog.Count` (`M`).
  - **The possession numerator does NOT exist yet.** Possession is only exposed *per row*
    (`ItemChecklistMod.OwnedCount(objectId, variation)`, `ItemChecklistMod.cs:66`,
    **spoiler-gated behind discovery since Iter-21** → returns 0 for `???` rows), and the
    In/Not-in-possession filter counts `OwnedCount >= 1` ad-hoc (`ItemListViewModel.cs:133`).
    The clean implementation adds an **`OwnedCatalogCount()` twin of `CollectedCatalogCount()`**
    — tally catalog entries with `OwnedCount(entry.ObjectId, entry.Variation) >= 1` — so the
    possession numerator uses the exact same spoiler-gated chokepoint the rows/filter already do
    (no drift; an undiscovered-but-world-scanned item is not counted, matching Iter-21).
  - **Settings wiring is already in place** (ICL is a Mod Settings Menu consumer). Add the
    widget to the existing builder in `ItemChecklistMod.Init` (`ItemChecklistMod.cs:227-232`,
    `ModSettings.Section(this)....Slider(...).Toggle(...).Build()`) + a handle/property in
    `ModConfig.cs`, read live in `ItemChecklistHud.Refresh` (and the footer, if in scope).
  - **Live-refresh caveat.** The HUD re-renders on `DiscoveredState.Changed` + a post-
    possession-scan nudge (Iter-16.4). A possession-mode counter must (a) also refresh when the
    3s possession scan changes the owned tally (the Iter-16.4 nudge already fires; confirm it
    covers the aggregate), and (b) re-render **immediately when the setting is toggled in-menu**
    (the framework's config-changed path — verify how ModSettingsMenu signals a value change to
    the consumer, or poll the handle in the HUD's `LateUpdate`).
  - **Loc:** a new setting label + option labels go in `localization/localization.yaml`
    (`ItemChecklist-Config` namespace, EN+DE).
  **Open design questions (decide with the user before coding — do NOT assume):**
  (1) **Widget shape** — a `.Toggle` (Discovery ↔ Possession, two states) vs. a `.Choice`
      (Discovery / Possession / possibly "Both", e.g. `"N / M  ·  owned K"`). The request says
      "umschalten" (toggle), so a 2-state Toggle is the literal reading — confirm whether a
      third "both" option is wanted.
  (2) **Scope** — does the switch affect **only the always-on HUD** (the request literally says
      "der Zähler", which most naturally means the HUD readout) or **also the window footer**?
      Both share `ProgressFormat.Counter`, so either is easy; it is a product decision.
  (3) **Denominator in possession mode** — keep `M = Catalog.Count` (owned-of-all-items,
      `K / M`) or use `M = CollectedCatalogCount()` (owned-of-discovered, `K / N`)? The
      former is the more natural completionist reading; flag the choice.
  **First step:** confirm (1)-(3) with the user, then build `OwnedCatalogCount()` + the widget.
  Requested 2026-07-12.

> **Out-of-sequence numbering is intentional.** Iteration numbers are assigned both
> sequentially-by-merge and topic-reserved, so a DONE iter can sit before lower-numbered
> tentative ones (e.g. Iter-16.1 done, Iter-16.2/17 still open) — timing ≠ number. See
> `docs/conventions.md § Branch + Commit Conventions`.

See `git log` for canonical per-iter merge points. Design docs: retained
(ADR-gated) specs live under `docs/specs/`; transient plans + brainstorming scratch
under `docs/superpowers/` (gitignored).
