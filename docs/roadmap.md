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
- **Iter-14.1 -- search-caret alignment. DONE** (see `docs/iteration-history.md`). The
  caret sat a few px too low and flush against the text. `TextInputField.Update()`
  overwrites the caret GO's world X/Y every frame, so the offset cannot live on that GO:
  a child GO (`CaretSprite`) now carries the caret `SpriteRenderer` at a constant
  `localPosition` (+1px up to centre, +2px right for a small gap). The caret was also
  shortened 8px->7px via the sprite's existing vertical 9-slice (SR Sliced draw mode,
  size 2x7) -- a pure-prefab change, no sheet/generator touch. **Corrected state note:**
  the sprite swap (1x1 `white_pixel` -> the painted 2x8 `Caret` sheet sprite) and the
  `{0.8,6,1}` scale-hack removal were already done in Iter-12; only the position/height
  remained for 14.1.
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
- **Iter-15 -- F1/HUD over the intro cutscene. DONE** (see `docs/iteration-history.md`).
  Appended `!sceneHandler.cutsceneIsPlaying` to the shared
  `WorldState.IsInPlayableWorld`, suppressing **both** the F1 open-guard and the
  always-on HUD during the spawn-from-Core intro cutscene. One-line behavioural change —
  both consumers already gate on the shared predicate (the Iter-11.6 structure). The
  cutscene fades CK's own HUD via `FadeOutAllGameplayUI()` (not `ShowHUD(false)`), which
  does not cull our layer-27 HUD, hence the explicit gate. `cutsceneIsPlaying` is CK's
  own discovery-path signal; sandbox-safe.
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
- **Iter-19 -- search-field word-wrap crash. DONE** (see `docs/iteration-history.md`).
  `SearchBar` overrides `Awake` to force `pugText.maxWidth = 0` after `base.Awake()`,
  removing the search field's PugText from CK's buggy
  `PugFont.AddNewLinesToLinesExceedingMaxWidth` word-wrap path (per-frame
  `IndexOutOfRangeException`, 127× on main → 0). **Corrected the roadmap's own fix
  candidate:** the prefab `pugText.maxWidth = 0` is a no-op — CK's
  `TextInputField.Awake` rewrites it to `maxWidth + 1 = 8.5` at runtime, so the fix had
  to come from code. Visual width clipping is preserved via the field's own `maxWidth`
  (7.5) through `TrimTextToFitRestrictions` (a char-trim, independent of the word-wrap).
  Pure behavioural C# (one `Awake` override); no prefab/art touch. Same CK PugFont bug
  class the Iter-9 ASCII hint + Iter-11 `RenderNoWrap` labels sidestepped.
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
  Covers full Western-European + partial Eastern-European/Cyrillic/typography. CK's
  thinTiny carries basic Latin but no accents, so inserting the accented glyphs globally
  adds characters without replacing any. Full font architecture in the
  `reference-ck-pugfont-architecture` memory + `core_keeper/docs/pixaki-format.md`.
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
  `docs/iteration-history.md`). A *second*, distinct stutter from Iter-27: peaks that
  grow with session length and persist away from base. On-disk evidence: the possession
  ledger had grown to **5503 entries / 89 KB**, ~90% **world-spawned nature**
  (bushes/grass/kelp/ stalagmites/lilies/ruins) counted as "owned" by Iter-20's
  place-object path and remembered forever. The real peak was **not the scan** but the
  **autosave `Serialize()`** of that 89 KB ledger (12–37ms main-thread spike, also
  pushing CK's host sim over budget). **No object-level signal separates wild nature
  from placed objects** in CK (cat/stack/icon/
  craft/sell/tags/DontDropSelfCD/Diggable/Destructible all collide: Stalagmite ≡
  CavelingFloorTile, GraveTree ≡ WayPoint), proven over three in-game probe rounds — so
  the filter is a **curated tag+ObjectID blacklist**
  (`PossessionClassifier.IsWorldNature`: tags Greenery/Destructible/CattleKelpFood/Ruins
  + a short tag-less-straggler ID list, editable). Gated on **path #3 only**
  (placed-object count; numbered #1 in the Iter-28-era text, unified in Iter-43) —
  container contents + carried untouched, so nature stored in a chest still counts and
  remember-from-afar is preserved. A **one-time `PruneByPredicate` at first scan**
  evicts the pre-existing backlog (`PruneStaleNear`'s 180-tile window was far too slow),
  ledger **5503→~520**, save spike + host-overrun warnings gone. **(That eviction was
  REMOVED in Iter-42: its gate was never serialized so it ran on every load, and the
  sweep could not tell chest-stored nature from placed — see Iter-42.)** Verified smooth
  in-game (1.2.1.5). Process lesson: a runaway background decompile-grep ate CPU through
  every test and confounded the "is it smooth?" signal for several rounds — kill stray
  background jobs before measuring perf.
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
- **Iter-29 -- chunk / time-slice the possession scan. WON'T FIX** (closed 2026-07-13).
  The idea: the 3s `PossessionScanner.Scan` iterates the full loaded-world set on a single
  tick, and a ~9.6ms scan on one frame every 3s is a felt micro-hitch even "under budget";
  spreading one pass across several ticks would smooth it. **Closed as won't-fix — the
  premise no longer holds and the two cheaper levers already shipped:**
  - **Iter-31 removed the cost.** The workbench-anchor fix shrank the ledger and killed the
    remote churn, so the scan is now **~1ms outside base / ~4–5ms at base with no felt
    hitch** — well under the 16.7ms frame budget. Iter-27's bulk `ToComponentDataArray`
    reads had already halved the loop (MAX 21.5→9.6ms); together there is no measurable
    micro-hitch left to slice away.
  - **Iter-38 shipped the "simpler lever" as a real setting.** The fallback this entry itself
    named (raise the interval) is now the player-facing `.Choice<int>` scan-interval preset
    ({1,2,3,5,8,10,15,20,25,30}s), so anyone who wants an even smaller per-scan footprint can
    raise the cadence — no time-slicing needed.
  - **The remaining work is disproportionate.** Time-slicing would mean holding the entity
    snapshot across frames (Persistent allocator + explicit disposal, not TempJob),
    accumulating `liveKeys` across all chunks **before** `PruneStaleNear` runs (it assumes a
    full pass), and handling world-unload mid-pass — real complexity for a smoothness pass
    that is no longer felt. **Never a regression** (1.0.2 had the identical scan and was
    smooth). Requested 2026-06-28, closed 2026-07-13.
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
- **Iter-37 -- HUD counter redundant repaint: dedup into a single change-gated render. DONE**
  (see `docs/iteration-history.md`). Surfaced by the Iter-36 adversarial review. The always-on
  HUD counter had **two refresh disciplines** bridged by a static cache in `ItemChecklistMod`:
  an unconditional `Refresh()` on the direct `DiscoveredState.Changed` path + the mode toggle,
  and a change-gated `RefreshHudCounterIfChanged()` for the 3s scan. The direct path repainted
  without updating `s_lastHudCounter`, so the next scan saw a phantom change and fired a
  redundant second `PugText.Render` (glyph-SR rebuild); pre-existing since Iter-16.4, inherited
  by Iter-36. **Fix (dedup, at the user's prompt): consolidate ownership into `ItemChecklistHud`**
  where the counter is displayed — the cache (`_lastCounter`) and both methods now live there.
  `Refresh()` is the single unconditional render point (bake / loc re-bake / the `Awake` initial
  paint — the **denominator**-changing triggers) and always leaves the cache equal to what is on
  screen; `RefreshIfChanged()` is the **numerator**-gated entry for the recurring triggers (3s
  scan, discovery event, mode toggle). `ItemChecklistMod` loses `s_lastHudCounter`,
  `NoteHudCounterShown` and `RefreshHudCounterIfChanged` entirely. Behaviour-neutral (same numbers
  shown); removes the redundant scan repaint **and** the possession-mode discovery repaint when
  the owned tally is unchanged. Pure behavioural C#; no prefab/art touch. Verified in-game
  (1.2.1.5, fake-ID 9999997): `safetyCheck=True`, 0 `CompileFailed`, 0 NRE, `baked: 8116`.
  Requested + done 2026-07-13.
- **Iter-38 -- possession-scan interval as a setting. DONE** (see
  `docs/iteration-history.md`). Exposes the possession-scan cadence (was the hardcoded
  `const float PossessionRefreshSeconds = 3f`, reset onto `_possessionTimer` each cycle in
  `Update()`) as an in-game Mod settings control, so the player trades owned-tally freshness
  against per-scan overhead (a freshness knob, not a perf necessity -- the user-facing form of
  the "simpler lever" Iter-29 named). **The user changed the widget mid-iter from the planned
  Slider to a `.Choice<int>`** of fixed presets **{1,2,3,5,8,10,15,20,25,30} s, default 3**
  (curated steps, no meaningless in-between values) -- the second `.Choice` consumer after
  Iter-36. Modelled as `Choice<int>` (not an enum): the value **is** the seconds, so the token
  `int.ToString()` is more self-documenting than an `S5`-style enum token, and there's no
  name<->seconds map to maintain. `ModConfig` gains `_scanIntervalHandle` + `DefaultScanInterval
  = 3` + a live-read `ScanIntervalSeconds` (float; int->float) property; `Bind(...)` takes the
  4th handle; `Update()` reads it **fresh each timer reset** (in-menu change applies from the
  next cycle; **no cold-start delay** -- `_possessionTimer` starts at 0f so the first scan fires
  immediately regardless of the ceiling). Per-option loc renders the presets as "1s".."30s"; the
  label is "Scan interval" ("(seconds)" dropped -- the unit rides the values). **Loc gotcha
  (recorded):** the per-option keys must be **unquoted** (`10:` not `"10":`) -- the mod's loc
  generator (`utils/LocalizationGenerator.cs`) is a custom line-parser that does NOT unquote leaf
  keys, so `"10":` bakes term `.../scanInterval/"10"` and mismatches the runtime lookup
  `.../scanInterval/10` (silent fallback to the bare number). Pure behavioural C# + one loc block;
  no prefab/art touch. Verified in-game (1.2.1.5, fake-ID 9999997): `safetyCheck=True`, 0
  `CompileFailed`, 0 NRE, `baked: 8116`; `config.cfg` shows `scanInterval = 3` (Choice bound +
  persisted); the row cycles "1s".."30s" (user-confirmed); the Iter-30 diag corroborated the
  cadence (a 1s window produced ~19 scans vs the ~6-10 a steady 3s would). Requested + done
  2026-07-13.
- **Iter-39 -- "Craftable / Not craftable" filter misclassifies cooked dishes. DONE**
  (see `docs/iteration-history.md`). Cooked dishes (Gerichte) landed in the **Not
  craftable** bucket, although the player produces them by cooking. Root cause (verified
  statically, then in-game): `Entry.IsCraftable` is derived at both bake sites as
  `requiredObjectsToCraft.Count > 0` -- strictly "has a **workbench recipe**". Cooked food
  is produced by the **Cooking Pot** (`CookingIngredientCD` / `ConvertCookedFoodsSystem`),
  not a workbench recipe, so its empty `requiredObjectsToCraft` filed **every** dish as not
  craftable. **Design (settled with the user):** "Craftable" means *"the player can produce
  it"* (broad); label kept, no loc change. **Fix:** unconditional `craftableCache[key] =
  true` in `AddCookedEntry` (`ItemCatalog.cs:801`) -- the method is cooked-food-exclusive and
  (Iter-33) every emitted `(objectID, variation)` is a reachable ingredient pair, so a dish
  is craftable by construction; Loop-1 (`:370`), pets (`:474`) and cattle (`:525`) untouched.
  **Measure-first (throwaway probe, committed-then-reverted):** confirmed all **6006** cooked
  entries read `IsCraftable=false` pre-fix, and that **cooking is the only recipeless
  station-production** -- the sibling not-craftable `ObjectType`s (PlaceablePrefab 364,
  Valuable 126, Eatable 117, NonUsable 65, …) are all gathered/looted/foraged (the user
  ruled farmed crops out of scope: gathering ≠ crafting, no clean signal). Verified in-game
  (1.2.1.5, fake-ID 9999997): `safetyCheck=True`, 0 `CompileFailed`/NRE, `baked: 8116`
  unchanged; the 6006 dishes moved to Craftable (**7300** craftable / **816** not craftable,
  7300+816 = 8116), the residual 816 being exactly creatures + gathered/looted items. Pure
  behavioural C# (one line + comment); no prefab/art/loc touch. Requested + done 2026-07-13.
- **Iter-40 -- locate an owned item in the base. DONE** (see `docs/iteration-history.md`).
  A UI/surfacing feature (not new tracking): for a discovered + owned + stored item, a HUD shows a
  directional arrow per holding container so the player walks straight to it. Left-click a
  trackable row toggles tracking; a tooltip affordance line advertises it ("Click to locate (in N
  chests)" / "stop locating" / "You are carrying it"); the tracked row gets its own
  `ToggledHighlight` SR; a rebindable cancel hotkey (Iter-34 category, unbound default) stops
  tracking. Data from the Iter-20 possession ledger via a `PossessionLedger.TilesHolding` /
  `CountTilesHolding` reverse index; `TrackerHud` copy-adapted from **caveling-divining-rod**'s
  `ArrowRingRenderer` + a new HUD prefab + a Tracker Arrow sheet sprite. **Key discovery:** CK's
  game world (XZ) and HUD (uiCamera XY) are separate coordinate spaces with no clean projection —
  the first `WorldToScreenPoint` attempt made the arrow vanish (diagnosed via runtime `CAMDIAG`);
  the ring instead sits at world origin + a constant `PlayerHudOffsetY=0.6`, and the fix was
  back-ported to CDR. Spoiler-gated (Iter-21 `OwnedCount`), carried-only shows "You are carrying
  it" (no arrow), auto-untrack when the item leaves all chests, no radar-fade (arrows visible at
  any distance). In-game fixes: `ClearUnboundCancelDefault` strips CoreLib's forced `keyCode=None`
  map (CK renders it as literal "None") so the Controls row shows blank; the HUD visibility gate
  uses `WorldState.IsInPlayableWorld` (no arrows over teleport / Save-&-Quit load screens — the
  Iter-11.6/15 bug class, caught by the whole-branch review). Catalog unchanged (8116). Requested
  2026-07-14, done 2026-07-20.
- **Iter-41 -- possession counter `K / M` location-independence. DONE** (see
  `docs/iteration-history.md`). The Iter-36 Possession `K` dropped as the player left base and
  recovered on return. **Root cause measured + code-grounded, overturning the roadmap's own
  "prime suspect":** the baseline (normal containers) drifted too -- `PruneStaleNear` was wiping
  the *remembered* ledger. The old `LoadRadius = 180` (picked as "< ImmediateLoadRadius 200")
  **conflated "loaded" with "observed"**: CK force-loads chunks within `PLAYER_DISTANCE_TO_LOAD =
  200` of the player (`Pug.Base`; not shrinkable by any setting), but base entities leave the
  *observed* scan set at ~91-115 (DOTS ArchetypeChunk granularity -- a prune-off control confirmed
  the prune is the sole cause). **Fix (airtight, two conditions):** prune a remembered tile iff
  `dist(player) <= 48` (loaded) **AND** `coveredByLoadedAnchor` (would-be-observed -- the same
  `WithinAnchor` gate the scan uses) **AND** `notin liveKeys`. **Part B:** the live-only axes
  (pet skins / cattle / paint colours) are now **remembered** in a per-tile aux store
  (`_auxContainers`/`_auxCarried`, unified into one `PossessionView._aux`), persisted via **ledger
  schema v3** (v2->v3 discard migration). Contract settled with the user: `K` = "own >=1 right now,
  wherever stored". A mobile-cattle per-colour over-count (found by the final review) fixed by
  keying penned-cattle aux to the nearest-anchor tile. Verified in-game (1.2.1.5): `K` constant
  ~385, `ledgerC` 402-403 with the base fully unloaded, self-heal intact. The CK load-vs-observe
  distinction lives in the `reference_ck_entity_load_observe_radii` memory + `docs/gotchas.md`.
  Reported 2026-07-15, done 2026-07-17.
- **Iter-42 -- stored world nature evicted on every world load. DONE** (see
  `docs/iteration-history.md`). Loading a save **far from base** dropped already-tracked
  owned items; they only came back after walking to base. **Root-caused from the on-disk
  data with no build** — diffing the ledger against its own `.pugbackup` showed 0 tiles
  removed but 21 ids / **2677 units** gone from 5 tiles' *contents* (1129 stored
  Stalagmite, 598 Mushroom, …), every one an `IsWorldNature` match and no other id
  touched. **Two defects in the Iter-28 one-time eviction:** (1) its `WorldNaturePruned`
  gate was never serialized, so the "one-time" sweep ran on **every** load; (2)
  `PruneByPredicate` cannot tell scan path #3 (the placed wild object, the target) from
  path #2 (the same id STORED in a chest, legitimate possession) — one flat per-tile
  dict serves both. At base the live scan rewrote the contents within one scan interval
  (invisible); far from base the loss stood and the next autosave persisted it. **Fix:
  remove the eviction, the flag and the method** (user's choice over persisting the
  flag) — the Iter-28 *write* gate keeps path-#3 nature out at the source and the
  Iter-31/41 v2/v3 discard migrations dropped every pre-gate ledger, so nothing is left
  for the sweep to clear; stragglers from a future blacklist edit self-heal via
  `PruneStaleNear`. Persisting the flag was rejected: it keeps a mechanism that deletes
  unattributable entries (same loss, once per future blacklist edit) and costs another
  schema bump. (The "exactly two removers" claim first written here was **wrong** —
  `ClearAux` is a third and `SetLiveContainer` removes by replacing an observed tile's
  dict; corrected in Iter-43. The true invariant: no path deletes by id-predicate any
  more.) **General lesson (`docs/gotchas.md`):** a "one-time" cleanup must keep its
  done-mark IN the store, never run a predicate delete over a store without provenance,
  and test remembered-state changes by loading **far from base** — the at-base case
  self-repairs and proves nothing. Verified in-game (1.2.1.5) against the ledger file:
  across a far-from-base load the `.pugbackup` diff is **REMOVED=0 ADDED=0 CHANGED=0**
  (byte-identical, 13783 → 13783; the pre-fix pair lost 2677 units) and all 21 ids are
  back at their original counts; `safetyCheck=True`, 0 `CompileFailed`/NRE. Reported +
  done 2026-07-30.

- **Iter-43 -- the possession subsystem's remaining silent data-loss paths. DONE** (see
  `docs/iteration-history.md`). **Not a user report — this is what the Iter-42 review found** when
  the `review-pr` gate was run retroactively after 1.3.2 shipped. The Iter-42 diff itself came back
  with **0 Critical**, but two of its documentation claims were wrong and four further data-loss
  paths surfaced in surrounding code (none introduced by Iter-42). Fixed in one iteration at the
  user's choice.
  - **Iter-42's own errors:** the count-path numbering contradicted itself (canonical: #1 carried /
    #2 container contents / #3 placed object — the Iter-28-era comments numbered `AddOne` #1 and
    Iter-42 inherited it, so the comment read as "could not tell *carried* from stored", which is
    false); and "exactly two removers" was untrue (`ClearAux` is a third, `SetLiveContainer` removes
    by replacing). Both corrected, the legend now stated once in `Scan`, and the true invariant put
    in its place: **no path deletes by id-predicate any more.**
  - **C1 (critical):** a failed load was indistinguishable from "no file" — four
    outcomes returned a bare empty store, `Read`-returned-null was not even logged, and
    the per-session save-skip cache means the first save of a launch always lands, so
    ~14 bytes replaced ~14 KB and the next autosave took the `.pugbackup` too. Under
    Wine this is real (six IL patches ship for such file APIs). Fix: `Load(guid, out
    StoreLoadStatus)` + `s_ledgerReadOnly`/`s_petsReadOnly`, and `SavePossessionLedger`
    skips a store whose load failed. **`PetCollectionStore` first** — it is the
    unrecoverable one (ever-owned set, no second source, random egg hatch to re-earn).
  - **I2:** the version discard threw a whole ledger away unlogged *while `Load` reported success*,
    so corruption was indistinguishable from a legitimate migration. Now reported with byte count +
    actual first line; marker compared as an exact first line (not `StartsWith`, which would accept
    a future `…v30`); non-positive counts rejected at the parse boundary.
  - **I3:** `TileAux` evaluated as an argument registered every container tile, so the flush wrote
    an empty aux dict — an **ungated** deletion that bypassed the `allowPrune` gate in exactly the
    co-located-chest case that gate was written for.
  - **I4:** `SetLiveContainer` was an unconditional ungated delete of a tile's remembered contents;
    a container and a co-located torch sit in different DOTS archetype chunks and leave the observed
    set independently (~91-115), so seeing only the torch discarded the chest's contents. **The
    shipped rule is stricter than first planned:** shrink only when a container was actually
    observed on that tile AND past the grace — merging during the grace alone would fix loading far
    from base but not walking away.
  - **I5:** nothing destructive was reported. New **`PossessionIncidentStore`**
    (durable, **ungated** by the default-off `Diagnostics`, deduped, 200-line cap) + the
    DIAG line now reports the **transition** (`ledgerC=505->505 lostUnits=2677` would
    have made Iter-42 self-evident at once). Anomaly trigger deliberately chosen
    false-positive-free: units lost on **≥5 tiles in one scan** (a shrink itself is
    normal; nobody empties five chests within 3 s). Plus: the `ResolveWorld` null case
    logged nothing ever, and the world pick started at `-1` so a world with **zero**
    inventory entities could win.
  - **General lesson (`docs/gotchas.md § A load that fails "softly" …`):** a load
    returning an empty store on *failure* is a delayed WRITE bug; return a status and
    make the store read-only; set the failure flag before anything can throw; fix the
    unrecoverable store first; a wholesale-replace write path needs a confirmation
    predicate; count and report every deletion; and pick an anomaly trigger that cannot
    false-positive or ship none. Verified in-game (1.2.1.5) against the ledger file:
    `safetyCheck=True`, 0 `CompileFailed`/NRE, the new file present in both install
    `Scripts/` and the generated manifest; **21/21** Iter-42 nature ids still present
    (Stalagmite 1129); the shrink still honoured (`1001` −50, `1610` −100, `301` +12 —
    the check against this iteration's OWN risk of over-merging into phantom ownership);
    pruning still active (8 tiles removed / 7 added in one interval, retiring the
    suspicion behind the 504→681 tile growth); and `possession-incidents.txt` absent,
    i.e. no false alarm. Reported + done 2026-07-30.

- **Iter-44 -- possession subsystem: the shape, not the next point fix. DONE** (2026-07-31, see
  `docs/iteration-history.md`). The stock-take below was taken on 2026-07-30 and **acted on**; it is
  kept in full because it is the record of what four independent reviewers found and, just as
  usefully, of what they refuted. Everything in it is resolved — see **How it was resolved** at the
  end of the entry. Original framing, unchanged:

- **Iter-44 -- possession subsystem: review backlog + an architectural decision. (the stock-take,
  (opened 2026-07-30, nothing implemented).** A **stock-take**, deliberately taken instead of a
  fifth round of point fixes. Context: Iter-42 fixed a data-loss bug; its `review-pr` gate found
  four more; Iter-43 fixed those and **introduced three new Criticals of the same class**; the
  Iter-43 gate (four reviewers: code / comments / type-design / silent-failure) found them, three
  of the four independently converging on the same root cause. 1.3.3 is published and carries
  them. Nothing below is fixed — this entry exists so the analysis is not lost.

  **Why a point fix is the wrong next move.** The type-design reviewer named the cause twice: a
  `bool` parameter (`allowShrink`) that carries a condition across a semantic boundary, plus two
  parallel per-tile dicts (`_containers`/`_auxContainers`) kept in step by hand at **ten** sites.
  Iter-43 skipped the recommended `TileEntry` refactor and *increased* that coupling by giving both
  dicts the same correctness predicate — C-1 below is the bill for exactly that.

  **Open Criticals (all code-verified by the reviewers, none in-game):**
  - **C-1 — `containerTiles` cannot serve as a universal confirmation predicate; permanent,
    persisted phantom ownership.** `PossessionScanner.cs:320` (contents) and `:342` (aux) share
    `allowPrune && containerTiles.Contains(key)`, but `containerTiles` is filled only in the
    `isContainer` branch (`:296`), and `isContainer` excludes `CraftingCD` entities (`:278`).
    - *aux:* cattle colour aux is keyed by `NearestAnchorTile` (a station/workbench tile — those
      carry `CraftingCD`, so **never** in `containerTiles`) and paint aux by the placeable's own
      tile. So `allowShrink` is structurally always false there: a pen losing its **last** animal
      of one colour, or a placeable repainted A→B, keeps the stale key **forever** (`ClearAux` only
      fires when the observed aux is *empty*). It is serialized, so it survives restarts. Inflates
      `K` (Iter-36 counter) permanently, violating Iter-41's "own >=1 right now" contract.
    - *contents:* any multi-entity tile. Replace a torch with a lantern, pick a chest up off a
      decorated tile, mine a wall next to an observed torch → the removed id is merged back every
      scan, and the tile stays in `liveKeys` so `PruneStaleNear` (`PossessionLedger.cs:129-130`)
      never reaches it. Locked chests / boss statues (`PossessionScanner.cs:254-258`) `AddOne` +
      `continue`, so their tiles are **never** in `containerTiles` — structurally unshrinkable.
    - *knock-on:* `TilesHolding` reads `_containers`, so the Iter-40 tracker draws an arrow to a
      chest that no longer exists and its auto-untrack never fires.
    - **Both reviewers proposed the identical fix, and it needs no schema change:** authorize the
      shrink with `PruneStaleNear`'s own premise — `allowPrune && (containerObservedHere ||
      (dist(player,tile) <= PruneRadius && coveredByLoadedAnchor(tile)))`. Inside 48 tiles and
      anchor-covered, the codebase *already* infers "unobserved ⇒ destroyed". Cost in the I4 case
      it protects: **none** — that case is measured at ~91-115 tiles, i.e. 2x outside the envelope.
      A bare "tile observed at all" test would NOT be sound (it re-opens I4). `PruneStaleNear`
      itself already accepts this exact risk, and this is strictly smaller (drops unconfirmed ids,
      not the whole tile). Applying it to `SetLiveAux` fixes the aux half and also resolves I-8's
      direction inconsistency.
  - **C-2 — `PossessionIncidentStore` destroys its own history on the very fault it reports.**
    `ReadAll()` (`PossessionIncidentStore.cs:145-156`) returns `null` both when the file is absent
    **and** when `Read` fails on a present file — the exact conflation `StoreLoadStatus` was added
    to end, one file deeper. `Record` then writes `Header + line`, **replacing** every accumulated
    incident with a single line, and the trigger is *correlated* with the fault being reported. Fix:
    a `TryReadAll(out text)` that distinguishes absent from unreadable, and refuse to write (keeping
    the already-emitted warning, and not marking the dedup key) when a present file could not be
    read. Inherited in shape from `PhantomViolationStore.cs:58-59` — but that one guards a
    reachability curiosity, this one guards data-loss evidence.
  - **C-3 — the C1 status flag cannot see a damaged-but-parseable file.** Neither parser throws on
    damaged input: `PetCollection.LoadFrom` skips bad lines, `PossessionLedger.LoadFrom` `continue`s
    on four conditions (plus the two new `cnt >= 1` rejections). A file truncated after line 1 →
    parses a **subset** → `status = Loaded` → writable → the next autosave persists the subset and
    the following one takes the `.pugbackup`. Worst on the **unrecoverable** store: 3 of 40 pet
    skins parsed, `MarkCollected` sets `Dirty`, `Save` writes 4 entries over the file — 37
    ever-owned skins gone, nothing logged. Fix candidates: return a skipped/malformed-line count
    and treat `> 0` as `Failed` + an incident (truncation almost always leaves one malformed line,
    so this is nearly free), or write a declared entry count / FNV trailer into the header and treat
    `parsed != declared` as `Failed`.

  **Open Importants:** two load-bearing comments were made FALSE by Iter-43 itself and should be
  corrected first, before anyone reasons from them — `PossessionScanner.cs:98-105` (claims
  `SetLiveContainer`'s dict replacement is "the dominant" self-heal for a newly blacklisted id; I4
  killed exactly that, so a blacklist addition now leaves a **permanent** over-count on any mixed
  tile) and `:236-241` (the Iter-41 note still says "SetLiveAux replaces, no accumulation" — the
  property C-1 removed). Same class: `PossessionClassifier.cs:48-51`, `docs/iteration-history.md`
  Iter-42's "self-heal on the next visit", and `docs/architecture.md:1268`'s unconditional
  "re-observation is itself a removal path". Further: **the anomaly detector is calibrated to the
  wrong failure** — it watches `SetLiveContainer` shrinks, but the *measured* historical
  catastrophe was Iter-41's `ledgerC` 402→0 via `PruneStaleNear`, where only a DIAG line behind the
  default-off flag exists; a `prunedTiles >= max(5, lcBefore/4)` trigger would have caught it. Its
  "cannot false-positive" claim is also wrong three ways (the scan interval is user-settable to
  **30 s**, the 8 s grace batches withdrawals into the first post-grace scan, and playing with
  `ModConfig.Enabled` off desynchronises the ledger while saves continue). **Two of the three new
  signals are one-shots consumed by the benign case:** `_worldNullWarned` is per-process and, per
  Iter-43's own in-game notes, fires on every load — so a genuine mid-play world loss is silent
  forever (fix: reset it on a successful resolve and gate it on `WorldState.IsInPlayableWorld`);
  and the `Shrink` dedup key is `":session"` (`PossessionScanner.cs:399`), so a benign 5-tile
  reorganisation consumes the slot and a later 400-tile collapse is neither written nor logged
  (fix: bucket the key by magnitude). Also: the **200-line cap** degrades the durable channel back
  to the rotating log with no marker, and `Record` returns `true` while writing nothing
  (`PossessionIncidentStore.cs:76-77`); a **read-only session is invisible** (no UI surface reads
  the flags, and the DIAG save lines live inside the `Save` that is skipped) and for pets the
  symptom *looks like* the loss it prevents — every ever-owned skin renders uncollected and `N`
  drops while the disk is fine (the modal window footer, not the HUD, is the right place for an
  `! not saving` marker); `LoadFrom`'s silent drops (I-6) violate Iter-43's own "count and report
  every deletion" rule, which is currently true of neither aux path; and `SetLiveAux`/
  `SetLiveContainer` merge **asymmetrically** (aux restores only absent keys, so a colour going
  3→0 restores the stale 3 while 3→1 correctly records 1) with no dropped-count return.

  **Refuted — do NOT re-investigate:** `_lines` is not double-counted (`LoadKnown` runs once under
  the `_loaded` guard); the `catch` un-marking the dedup key is correct, not a defect (the warning
  is emitted before the write, so a retry can still land); the ≥5-tile detector is **not** dead code
  (confirmed shrinks happen on every normal withdrawal — the verified `1001` −50 / `1610` −100
  deltas); `bestCount` −1→0 is a net improvement, its only cost being a display-only window inside
  the load screen (which is what burns the one-shot warning); `s_ledgerReadOnly`/`s_petsReadOnly` are
  correct and have no bypass (both branches write both flags, the char-switch backstop runs before
  the reset, and `SavePossessionLedger` is the only `Save` call site); all new return values are
  handled and `PruneStaleNear` is behaviour-identical after its restructure; and the `cnt >= 1`
  parse rejection cannot change what an existing file loads to (no writer emits a non-positive
  count), so "no migration needed" holds.

  **The decision to take:** point-fix C-1/C-2/C-3 now (a hotfix that touches exactly the code an
  eventual refactor replaces), or do the structural change first — `TileEntry { Contents, Aux }` +
  a `TileObservation` describing what was actually seen, so the ledger owns the shrink rules and the
  contents/aux dimensions become **separately expressible** (which is what C-1 needs). The reviewer
  also re-confirmed that persisted provenance, if ever wanted, can migrate **losslessly**: make the
  version marker a *set* (v3 ⇒ 3 segments, v4 ⇒ 4), load v3 lines as `provenance = Unknown` with the
  rule "never auto-evict" — no discard, no player re-scan. Reported + open 2026-07-30.

  **How it was resolved** (the user chose the structural change; full narrative in
  `docs/iteration-history.md § Iter-44`):
  - **C-1** — fixed by the rebuild, not by the point fix. One `TileEntry { Contents, Aux
    }` per tile and a single `ApplyScan` entry point; a dimension may shrink only past
    the grace and on evidence for *itself*. `containerTiles` survives only as "this
    tile's container was observed, so its buffer is authoritative", never as a universal
    predicate. One correction to the analysis above: paint was only *usually*
    structurally frozen — a **paintable container** writes its paint aux and adds its
    own tile in the same two branches.
  - **C-2** — `TryReadAll` separates absent from unreadable and a failed read aborts the
    write; the cap marker is written from the write's own result and recognised on load;
    an unreadable history no longer counts as zero lines. The store also refuses to
    rewrite a file that vanished mid-session (a lying `FileExists` was the last
    from-scratch path) and verifies its appends.
  - **C-3** — both parsers report unaccepted data lines and any such line makes the load FAILED. The
    pet file gained a declared entry count, which is the only way to see a cut exactly at a line
    boundary; headerless files stay valid and upgrade on the next save. A zero-byte file is damage.
  - **The Importants** — all done: the false comments corrected (and re-checked by a further review
    round, which found three more), the detector recalibrated (a prune channel per-scan and
    cumulative, thresholds scaled to the configured interval, batched scans suppressed but with an
    override, dedup keyed by GUID and magnitude), `_worldNullWarned` re-armed, the 200-line cap
    writing a `#full` marker and returning `false`, the read-only session visible in the window
    footer, and `LoadFrom`'s silent drops counted.
  - **Found only after the rebuild** and worth recording because none of it was in the
    analysis above: CK's file layer swallows the whole `IOException` class (so writes
    are now verified by reading back, and the FNV cache no longer records unverified
    writes — a poisoning bug latent since Iter-31); "one miss is not evidence" for both
    the merge and the prune; one scan per frame; and cattle colour aux keyed to a
    deterministic anchor tile instead of the anchor nearest the animal, which had been
    hopping ~12 tiles per save interval since Iter-41 and quietly disabling the
    save-write-skip.
  - **Left open at the time, then assessed and acted on:** the three residuals below. Two were done
    in **Iter-45**; the third is deliberately waiting for a measurement.

- **Iter-45 -- possession provenance: stored vs placed, plus a declared tile count
  (ledger v4). DONE** (2026-07-31, see `docs/iteration-history.md`). The two Iter-44
  residuals worth doing, taken together because both are format changes to the same file
  and one schema bump covers both. Assessed first, and **two of my own initial estimates
  were wrong** — I had rated the damage and only guessed the cost:
  - **Provenance was worth more than "the residual it would retire".** Splitting scan
    paths #2 and #3 fixes a *live wrong statement*: `CountTilesHolding` counted any
    remembered tile, and the Iter-40 tooltip renders that as "in N chests" with an arrow
    per tile — so a placed torch claimed a chest that does not exist. That sat in a
    different feature, which is why the first assessment missed it. The same bit also
    restores the ability to evict path-#3 entries specifically, i.e. the blacklist
    self-heal Iter-42 had to remove. Full container IDENTITY still buys almost nothing
    and was NOT built.
  - **The declared count was far cheaper than estimated.** `#n=<tiles>` is a `#` comment line the
    parser already skips — additive, invisible to older readers, no migration. "Expensive because it
    is a format change" was simply wrong about an additive line.
  - The migration is lossless because `placed` is the LAST segment: v3's three fields keep their
    meaning, one parser reads both shapes, and the marker becomes an accepted SET — the scheme the
    Iter-44 review had already confirmed.
  - **The review gate then found the defect that mattered**, both reviewers
    independently and by running the code: the migration ADDED a copy instead of MOVING
    provenance, so every placed object counted double (permanently, for a tile observed
    from beyond the shrink envelope) and the eventual correction was booked as lost
    owned units — which would have written a durable "please report this file" incident
    on every updating player's first base scan and burned that magnitude's dedup slot.
    Fixed subtractively (a v3 count was `stored + placed`, so subtracting the observed
    placed part is exact), uncounted, gated on a per-tile "provenance unknown" flag that
    survives a save by being serialized as a v3-shaped line. Second finding: the naive
    form of the tooltip fix removed the locate arrow for ~99 % of ledger tiles and told
    players they were carrying things they had put down — the arrow was always right,
    only the wording was wrong.
  - **Still open, deliberately:** an **aux trigger channel**. Aux reductions are
    reported (session total in every incident detail, plus the DIAG line) but never
    trigger, so an aux-only regression — the class that opened Iter-44 — would still be
    invisible. The cattle fix removed the dominant benign source (hopping keys), which
    makes a cumulative channel viable for the first time, but there is no measured
    baseline for aux reductions in normal play after that fix. Iter-44's four refuted
    "cannot false-positive" claims are the reason this waits for a number instead of a
    guess: read `auxReduced=`/`shrunkAux=` from a normal diagnostics session, then set
    the threshold. Also still open and unchanged: full per-container identity (buys the
    near-vacuous residual only).

- **Iter-46 -- extract the thinTiny glyph fix into a required dependency (Complete Tiny
  Font). DONE** (2026-08-12, see `docs/iteration-history.md`). Iter-25's runtime
  append-85-glyphs-into-`thinTiny` patch is gone from this mod; a new, separate mod —
  **Complete Tiny Font** — replaces the `thinTiny` atlas wholesale instead (a font swap,
  not a per-glyph append), so the fix now benefits every mod rendering in that face, not
  just this one. ItemChecklist gained a third **required** manifest dependency
  (`CompleteTinyFont`, alongside CoreLib/Mod Settings Menu); with it missing, the loader's
  `ModSorter.SortMods` silently drops ItemChecklist from the load list (`Debug.LogWarning`,
  no in-game dialogue) — `README.md`/`CHANGELOG.md` now name that failure mode. Two doc
  corrections landed alongside, neither caused by this iteration: the catalog bakes
  **8113** items, not the long-stated 8116 (pre-existing staleness from a changed mod set,
  measured fresh from `Player.log`); and Iter-25's "CK never uses thinTiny for prose" claim
  is wrong — 14 shipped assets use it (none of them damage numbers, which render in
  `thinSmall`; CK's own damage-number `thinTiny` code path is dead — it writes a field
  `PugText` rendering never reads). Build-verified (the manifest carries all three
  dependencies as `required: true`); in-game verification, the negative test, and the 1.4.0
  publish are a separate pass.

- **Iter-47 -- honour "hide in-game UI" in both HUD gates. OPEN.** Both HUDs stay on
  screen when the player hides CK's interface, so they are the only thing left on an
  otherwise empty screen. Confirmed in game 2026-08-23 (found while reviewing
  player-coordinates-hud, which had the identical defect and fixed it there).
  `Manager.prefs.hideInGameUI` is not a niche setting: it sits on the regular
  `PlayerInput.InputType.TOGGLE_UI` keybind **and** on an options entry, and CK's own
  gameplay UI honours it via `CalcGameplayUITargetScaleMultiplier()` collapsing to
  `Vector3.zero`. The fix is one term in each of the two visibility expressions --
  `unity/ItemChecklist/ui/ItemChecklistHud.cs:62` and
  `unity/ItemChecklist/ui/TrackerHud.cs:84` -- both of which already read
  `WorldState.IsInPlayableWorld && !isAnyInventoryShowing && !IsAnyMenuActive()`.
  Do **not** switch these gates to `CalcGameplayUITargetScaleMultiplier()` instead: it is
  a global scale, not a per-element one, and it collapses for several unrelated reasons
  at once (hidden UI, fades, load screens) that `WorldState` already covers separately.

- **Iter-48 -- put the HUD row on CK's pixel grid. OPEN.** `ItemChecklistHUD.prefab`'s
  `hudRoot` sits at `y: 7.8`, and its `CounterText` hangs `0.0625` below that, so the
  drawn text centre lands on `7.7375`. CK's UI grid is `1/16 = 0.0625` per pixel, which
  makes those `124.8 px` and `123.8 px` -- both between pixels. The atlas is
  point-filtered and nothing on the prefab snaps positions, so this is the condition
  under which glyph edges go soft. Found 2026-08-23 while calibrating
  player-coordinates-hud against this row; that mod moved its own anchor to `7.8125`
  (125 px) and now sits `0.2 px` above ICL rather than exactly on its line.

  **Whether it is visible is unmeasured** -- 0.8 px of sub-pixel offset may well be
  invisible at every scale factor, and the row has looked fine for many releases. So
  this is a "look at it zoomed in, then decide" item, not a defect. If it does move:
  `7.8125` is the nearest grid value going up (`0.8 px` up) and `7.75` the nearest going
  down (`0.8 px` down), and picking the former restores exact row parity with
  player-coordinates-hud for free.

- **Iter-49 -- gallery screenshots: one set, two capture formats. OPEN.** All seven
  pictures in `sources/` are named in `CK_DISCORD_MEDIA` and are what the mod.io gallery
  shows, but they were taken two different ways. Three are clean 16:9 full-screen frames
  at `3520x1980` -- `hud` (July), `settings` and `controls` (August). The other four --
  `filter`, `filter_selected`, `search`, `sort`, all from the 2026-06-26 gallery refresh
  -- are `4112x2658` window captures with a **174 px black bar at the top and another at
  the bottom**.

  **What is inside those bars is not stale**, which is what makes this cheap rather than
  a re-shoot. The framed content measures `4112x2310` -- already 16:9, and larger than
  the full-screen set. It shows the current UI (the unified `Display` field from Iter-18)
  and the current catalogue: the footer reads `359 / 11119`, the post-Iter-17 figure. Nor
  do these carry a macOS title bar, unlike the window captures in the sibling mods
  (caveling-divining-rod, simple-crafting-pool-extender), whose own roadmaps cover that
  separately. Cutting the two bars off leaves a usable frame.

  `../utils/crop-center-169.sh` is the wrong tool for it, and looks like the right one:
  it takes the centre 16:9 slice at *full height*, so it narrows the width and leaves a
  horizontal bar exactly where it was.

  **To decide.** Crop the four -- costs nothing, keeps what they show -- or retake the
  whole set at one size, which buys a uniform gallery at the price of re-staging seven
  scenes. Cropping still leaves the set at two resolutions (`4112x2310` beside
  `3520x1980`), so whether *that* matters is the question to settle first; if it does,
  cropping is not actually the cheaper path. Secondary, and noticed while measuring
  rather than looked for: the four show a character at 3,2 % discovery, so nearly every
  visible row in them reads `???`.

> **Out-of-sequence numbering is intentional.** Iteration numbers are assigned both
> sequentially-by-merge and topic-reserved, so a DONE iter can sit before lower-numbered
> tentative ones (e.g. Iter-16.1 done, Iter-16.2/17 still open) — timing ≠ number. See
> `docs/conventions.md § Branch + Commit Conventions`.

See `git log` for canonical per-iter merge points. Design docs: retained
(ADR-gated) specs live under `docs/specs/`; transient plans + brainstorming scratch
under `docs/superpowers/` (gitignored).
