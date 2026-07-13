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
- **Iter-33 -- cooked-food tier reachability (phantom epic rows). DONE** (see
  `docs/iteration-history.md`). Loop 2 emitted all three tiers (base/rare/epic) per food
  variation, but CK gates the achievable tier on ingredient rarity (`flag`: a Rare-rarity
  Flower or any Legendary ingredient — the Rare check is `FlowerCD`-gated, the Legendary check
  type-agnostic), and cooking is the **only** source of cooked food (verified: 0 of the 45
  cooked IDs in `LootTableBank.asset` / any merchant / drop). So epic is reachable only when
  flag=true → **most epic rows were unreachable phantoms** stuck at `???`, making 100 %
  unattainable (the Iter-16.4 bug class). Measured (throwaway probe, 1.2.1.5): 3003 variations,
  **2145 phantom epics** (flag=false), 858 reachable (flag=true), golden-base ⟺ flag=true
  **exactly** (0 anomalies; 858 = Iter-32's 858 golden dup-keys). Fix: `CookedEpicReachable`
  mirrors CK's `flag`, gating Loop 2's epic emit; catalog **10265 → 8120** (−2145), ~6,006
  cooked dishes remain, 100 % attainable. + a durable, self-healing safety net: if a suppressed
  phantom is ever discovered (a future CK gate change / new source), `PhantomViolationStore`
  persists it to `mods/ItemChecklist/phantom-violations.txt` + warns once, and a world-load
  `SweepDiscoveredPhantoms` re-derives `discovered ∩ suppressed` so a failed real-time write
  self-heals next load. Pure behavioural C# + one new file; no prefab/art. Requested + done
  2026-07-12.
- **Iter-34 -- keybind rebind row: give the mod its own control-mapping category. DONE**
  (see `docs/iteration-history.md`). **Re-framed by the in-game screenshot:** the row was
  never *missing* — it rendered as a loose, **header-less** row at the top of Controls > Mods,
  not grouped under a mod-named heading like CoreLib's own "Core Library" or PlacementPlus's
  "PlacementPlus". Root cause (verified against CoreLib **4.0.5's real source**, not the stale
  4.0.4 decompile): the F1 toggle registered with `categoryId: -1`, CoreLib's default **"Mods"**
  bucket, whose sub-section header CoreLib **deliberately suppresses** — `ControlMappingModule`
  sets `_showActionCategoryName = categoryName != "Mods"` (the top-level tab is already "Mods").
  A **named** category gets `true` → a header + a description (CK derives the terms as
  `ControlMapper/<Category>Category` and `.../<Category>Description` via
  `ControlMappingMenu.GetCategoryLabelLocaKey`). Fix: register the toggle under a named
  **"ItemChecklist"** category (`ControlMappingModule.AddNewCategory` before `AddKeyboardBind`)
  + two loc terms `ControlMapper/ItemChecklistCategory` ("Item Checklist" / "Item-Checkliste")
  and `.../ItemChecklistDescription` ("Item Checklist controls" / "Steuerung der
  Item-Checkliste"), EN+DE. CoreLib migrates the persisted action to the new category id (103)
  on load. **Refuted en route** (the roadmap's own hypotheses): (a) *not* a CoreLib patch break
  — `ControlMappingMenu.Initialize` + `_mappingLayoutData` still exist in 1.2.1.5 and the
  Harmony injection is sound (no patch-fail logged); (b) *not* a loc-resolution failure — the
  action term already rendered. Pure behavioural C# (one `AddNewCategory` call) + 2 loc terms;
  no prefab/art. Verified in-game (1.2.1.5, fake-ID 9999997): `safetyCheck=True`, 0
  `CompileFailed`, 0 NRE, `ItemChecklist` category id 103, header + description render localized
  (user-confirmed screenshot). Also updated the shared CoreLib reference checkout from the
  stale 4.0.4 decompile to CoreLib's real 4.0.5 source (GitHub tag `4.0.5`). Requested
  2026-07-12, done 2026-07-13.
- **Iter-35 -- foreign-mod item shows raw objectID as name + "missing:" tooltip. DONE**
  (see `docs/iteration-history.md`). **Two distinct display bugs, one root cause**, all settled
  by an in-game bake probe (the standing lesson held twice more). A bake probe over the 16
  foreign-mod items measured: **12 resolve normally**; **4 return `null` everywhere** — exactly
  ChestsGalore's term-less `Workbench{Chest,DoubleChest}{Extra,Next}` variants (the screenshot's
  named "Chest Workbench" is a *different* object, 32783, which resolves). **Hypothesis (c)
  timing REFUTED** (identical across bakes + language toggles); **(b) confirmed + narrowed** —
  these 4 ship no I2 term. Fix 1 (**derived name**): when `GetObjectName(true)` is empty,
  `FallbackName` derives from the internal name (`ObjectProperties "name"` → strip `Mod:` prefix
  + PascalCase → "Workbench Chest Extra") instead of the numeric objectID, flagging
  `Entry.NameIsFallback`; `ItemRow`'s tooltip then shows that baked name `dontLocalize` (the `???`
  pattern) + suppresses desc/stats, so **both** the row label and the "missing:" tooltip are
  fixed from one name source. Fix 2 (**exclude the internal pages**, per the user): these 4 are
  CoreLib workbench-chain "pages" a base folds in via `WorkbenchDefinition.relatedWorkbenches`. A
  second probe REFUTED the naive "referenced → exclude" filter — the refs are a **MESH** (siblings
  cross-reference the named bases too), so `BuildWorkbenchChainSets` drops a chain member only when
  it is a **leaf** (folds in nothing) **OR term-less**, keeping the named hubs; the root is skipped
  (aggregates via `bindToRootWorkbench`). Catalog **8120 → 8116** (−4). The derived-name net stays
  for legit standalone term-less foreign items of other mods. Verified in-game (1.2.1.5, fake-ID
  9999997): `safetyCheck=True`, `baked: 8116`, 0 `CompileFailed`/NRE, the 4 pages gone, named
  workbenches + all else intact. Requested + done 2026-07-13.
- **Iter-36 -- counter toggle: discovery count vs. in-possession count. DONE**
  (see `docs/iteration-history.md`). A display-source switch (no new tracking): a
  `.Choice<CounterMode>` setting (Discovery/Possession) flips **both** the always-on HUD
  and the window footer between the discovery count `N / M` and an owned count `K / M` —
  denominator `M = Catalog.Count` unchanged in both modes, so the toggle swaps only the
  numerator (Q3). Settled with the user: (Q1) a 2-option Choice over a bool Toggle
  (clearer, extensible to a future "Both"); (Q2) scope = HUD **and** footer.
  `ItemChecklistMod.OwnedCatalogCount()` is the possession numerator twin of
  `CollectedCatalogCount()`, tallied through the **same spoiler-gated `OwnedCount`
  chokepoint** (Iter-21) so `K ≤ N ≤ M` by construction (pet-skin/cattle-colour entries
  route correctly); `CurrentCounterNumerator()` selects by `ModConfig.Mode` and both
  surfaces route through it (one source, no drift). Live refresh via
  `SettingHandle.OnChanged` (immediate on menu toggle — repaints HUD + footer) + a
  mode-aware 3s-scan HUD nudge (tracks the displayed numerator). First `.Choice` consumer
  here (RKC-pattern per-option loc); the nested-enum `CS0102` trap avoided by naming the
  property `Mode`. A Codex spec-review caught the `CS0103`/`CS0102` issues up front; a
  subagent diff-review returned SHIP (2 non-blocking, pre-existing observations — one logged
  as tentative Iter-37 below). Requested 2026-07-12, done 2026-07-13.
- **Iter-37 (tentative) -- HUD counter: redundant repaint after a discovery change.**
  Surfaced by the Iter-36 adversarial review (a non-blocking observation, deferred as
  out of scope). The always-on HUD has two refresh paths: the direct
  `DiscoveredState.Changed -> ItemChecklistHud.Refresh()` subscription (in `Awake`) and
  the change-gated 3s-scan nudge `RefreshHudCounterIfChanged()` (gated on
  `s_lastHudCounter`). The direct path repaints but does **not** update
  `s_lastHudCounter`, so the next 3s scan sees the stale cache, finds the numerator
  "changed", and repaints a **second** time -- one extra `PugText.Render`, harmless.
  **Pre-existing since Iter-16.4** (then `s_lastHudCollected`); Iter-36 only inherits it.
  Fix candidate: update `s_lastHudCounter` after the direct `Refresh()` render (or route
  both paths through one change-gated helper) so the scan nudge does not re-fire.
  Cosmetic (at most one redundant render every few seconds); purely a cleanup, not a
  visible bug. Requested 2026-07-13.
- **Iter-38 (tentative) -- possession-scan interval as a setting.** User-requested
  2026-07-13: expose the possession-scan cadence as a Mod Settings Menu slider so the
  player can trade update freshness against per-scan overhead. Today the cadence is the
  hardcoded `const float PossessionRefreshSeconds = 3f` (`ItemChecklistMod.cs:156`), reset
  onto the countdown timer each cycle in `Update()` (`ItemChecklistMod.cs:305`); the scan is
  ~1-10ms post-Iter-27/28/31, so raising it is purely a freshness knob, not a perf necessity.
  This is the **user-facing form of the "simpler lever" Iter-29 named** ("raise the interval
  3s->5-6s") -- and further obsoletes the Iter-29 time-slicing idea. **Design settled with
  the user (build-ready):**
  - **Slider**, not a Choice/preset or an added disable-toggle -- a 1:1 clone of the
    `anchorRadius` sibling (`ItemChecklistMod.cs:237`, signature `(handle, key, min, max,
    default, step, SliderDisplay)`). Key `scanInterval`, **range 1-30 s, step 1, default 3**
    (default preserves current behaviour), `SliderDisplay.Number` (unit conveyed by the
    label, slider values render without loc).
  - `ModConfig` gains a `SettingHandle<float> _scanIntervalHandle`, a
    `const float DefaultScanInterval = 3f` (the old constant relocates here), and a
    live-read `ScanIntervalSeconds` property (fallback = default); `Bind(...)` takes the new
    handle. `Update()` reads `ModConfig.ScanIntervalSeconds` when resetting `_possessionTimer`
    -- so an in-menu change applies from the next cycle. **No cold-start delay** regardless of
    the ceiling: `_possessionTimer` starts at `0f`, so the first scan still fires on the first
    playable tick and only then resets to the interval.
  - **Loc:** a `scanInterval:` block under `ItemChecklist-Config` in
    `localization/localization.yaml` (EN "Scan interval (seconds)" / DE "Scan-Intervall
    (Sekunden)").
  - **Unchanged by design:** the 8s prune-grace and the ledger persistence (CK
    `WriteCharacter` hook) -- this is purely a scan-frequency knob.
  Pure behavioural C# + one loc block; no prefab/art touch (the Iter-23/34/35 shape).
  Not ADR-worthy -> no committed spec. First step: build the four edits above on an
  `iter-38` branch/worktree, then in-game-verify (clean sandbox compile, slider appears
  under Options -> Mod Settings, a changed value visibly shifts the scan cadence in the
  Iter-30 diagnostic log). Requested 2026-07-13.
- **Iter-39 (tentative) -- "Craftable / Not craftable" filter misclassifies cooked
  dishes.** User-observed 2026-07-13: cooked dishes (Gerichte) land in the **Not
  craftable** bucket of the Iter-10 Craftable filter, although the player *does* make
  them (by cooking). **Mechanism already read from the code (not yet in-game-confirmed):**
  `ItemCatalog` derives `Entry.IsCraftable` at **both** bake sites purely as
  `info.requiredObjectsToCraft != null && info.requiredObjectsToCraft.Count > 0`
  (`ItemCatalog.cs:370` standard Loop-1, `:801` `AddCookedEntry`) -- i.e. strictly "has a
  **workbench recipe**". Cooked food is not produced by a workbench recipe but by the
  **Cooking Pot** combining an ingredient pair (`CookingIngredientCD` /
  `ConvertCookedFoodsSystem`), so a cooked object carries an **empty
  `requiredObjectsToCraft`** -> `IsCraftable = false` for **every** dish, matching the
  symptom exactly. Cross-ref Iter-33: **cooking is the only source** of cooked food, so
  "not craftable" is doubly wrong for them. **Open design question (settle with the user
  before building):** what should the filter's "Craftable" mean -- the current "has a
  workbench recipe", or the broader "the player can *produce* it" (which would fold in
  cooked-via-cooking)? If the latter, the natural fix is to set `isCraftable: true` for
  cooked-food entries at their Loop-2 emit (they are craftable by construction -- every
  emitted `(objectID, variation)` corresponds to a real ingredient pair), leaving the
  `requiredObjectsToCraft` derivation for all standard items untouched. **Verify first**
  (standing lesson): confirm a sample dish reads `IsCraftable=false` in-game, and check
  whether any *other* obtainable-but-recipeless rows are also misfiled by the same
  `requiredObjectsToCraft` definition (pets/critters/cattle are creature families, not
  "craftable", a separate question). Expected pure behavioural C#; no prefab/art touch.
  Requested 2026-07-13.

> **Out-of-sequence numbering is intentional.** Iteration numbers are assigned both
> sequentially-by-merge and topic-reserved, so a DONE iter can sit before lower-numbered
> tentative ones (e.g. Iter-16.1 done, Iter-16.2/17 still open) — timing ≠ number. See
> `docs/conventions.md § Branch + Commit Conventions`.

See `git log` for canonical per-iter merge points. Design docs: retained
(ADR-gated) specs live under `docs/specs/`; transient plans + brainstorming scratch
under `docs/superpowers/` (gitignored).
