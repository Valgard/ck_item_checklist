# ItemChecklist — Iteration History

Full per-iteration narrative of ItemChecklist's development (Iter-3.5
onward), moved out of `CLAUDE.md` to keep that file focused. See `git log` for
canonical per-iter merge points; retained (ADR-gated) design specs live under
`docs/specs/` (transient plans/scratch under the gitignored `docs/superpowers/`).

As of 2026-06-29: Iter-3.5 through Iter-12 (incl. the 3.x/7.1 point-iters and the
Iter-12 extension), Iter-13, Iter-14.1, Iter-18, Iter-14.2, Iter-15, Iter-19, Iter-20, Iter-21, Iter-16.1, Iter-16.2, Iter-22 (row-hover tooltips), Iter-23, Iter-24, Iter-25 (thinTiny accented-glyph injection), Iter-16.4 (discovery-filter/counter pet-skin fix), Iter-16.3 (cattle collection), Iter-17 (per-variation tracking — cattle pet-model + paintable colour variants), Iter-27 (possession-scan perf — bulk component reads kill the in-base stutter), and Iter-28 (possession scan — exclude world nature via tag+ID blacklist + one-time ledger eviction; kills the autosave-serialize spike), Iter-30 (config-gated possession diagnostic log), and Iter-31 (possession scope — anchor the base on workbenches, killing remote world-structure pollution; + save-write-skip + ledger v2 migration) are DONE on main. Iter-3.8
replaced the per-entry SpawnRows (one GameObject per ~10718 catalog
entries, ~905 ms open freeze) with viewport virtualization: a fixed ~5-row
pool recycled from `IScrollable.UpdateContainingElements`, reporting the
full catalog height via `GetCurrentWindowHeight`. Open latency dropped to
~0-7 ms. Invariant from the geometry fix: `UIScrollWindow.windowHeight`
must equal the SpriteMask height (and the mask top align to row 0's top)
for the first/last rows to sit flush. **Iter-4 (DONE):** F1 is now a real toggle (one key opens and closes), and
the checklist is mutually exclusive with CK's inventory/crafting UI — opening
a Vanilla menu auto-hides it, and F1 won't open it over an open menu. See the
`CoreLib UserInterfaceModule` row above and `docs/architecture.md § UI
Architecture` for the mechanism. **Iter-5 (DONE):** a working, draggable
scrollbar is wired into the window prefab using CK's native `ScrollBar` +
`ScrollBarHandle`, with the Item-Browser bridge scrollbar sprites (track +
handle + selected-border, sub-sprites of the `ui_classic` atlas). **Pure
prefab change — zero C#:** once `UIScrollWindow.scrollBar` references the
`ScrollBar`, CK's `UIScrollWindow.LateUpdate → UpdateScrollbar →
ScrollBar.UpdateScrollBarPosition` drives handle sizing, position, and
mouse-wheel sync itself (verified against Item Browser, which has no scrollbar
C#). Scroll arrows stay unwired (`fileID: 0`); track-position fine-tuning and
real sprites fold into Iter-12 (pixel-art). Two non-obvious facts proven during the build:
the scrollbar SpriteRenderers must use **`maskInteraction: None`** to stay
unclipped by the row SpriteMask (orders 46/47 sit inside the 40..55 mask
range), and `ButtonUIElement.LateUpdate` toggles **GameObject activity** of
`spritesShownUnpressed`/`spritesShownPressed` each frame — a GO must never be
in both lists, and with a single handle sprite both lists stay **empty** so
`handleSpriteRenderer` (rendered by `ScrollBar` itself) is the always-visible
handle, with the selected-border wired only as `optionalSelectedMarker`
(hover/selection highlight). Script-ref rule for hand-wired CK components:
`m_Script.fileID` is a portable class-name MD4 hash, but the `guid` is this
install's `Pug.Other.dll.meta` guid — see the
`project-corekeeper-script-fileid-derivation` memory. **Iter-6 (DONE):**
item rarity colouring — each row's CK rarity (`ObjectInfo.rarity`) shows as a
name tint (all rarities; Common/Poor keep the default text colour, Uncommon+
get their rarity colour) **and** a rarity border around the icon for Uncommon+
(Common/Poor get no border, matching CK's `GetSlotBorderRarityColor`
`useDefaultColorForCommon` grouping). Applies to undiscovered `???` rows too.
`Rarity` baked into `ItemCatalog.Entry` (via a `rarityCache` mirroring
`iconCache`); colour resolved at rebind in `ItemChecklistContent` via
`Manager.ui.GetSlotBorderRarityColor(rarity, useDefaultColorForCommon: true,
defaultColor)`; `ItemRow.Bind` paints it. **Distinct axis** from the Iter-3.7
cooked-food tiers (`CookedFoodCD.rareVersion`/`epicVersion`). Two non-obvious
facts proven in-game (see `docs/gotchas.md`): the label tint must use
`SetTempColor(c, keepColorOnStart: true)` after `Render()` or it blanks on the
first open (PugText `renderOnStart`), and the shipped `ui_rarity_border.png`
placeholder was fully transparent (fixed to a white hollow frame, rendered as a
9-slice via `spriteBorder {1,1,1,1}` so the ring stays thin; real pixel-art
border remains **Iter-12** (pixel-art) polish).
Full mechanism in `docs/architecture.md § Rarity Colouring (Iter-6)`. **Iter-7
(DONE):** runtime-switchable list sorting — four modes (Name, Rarity, Found,
Category/ObjectType), each with ascending/descending direction. A reusable
`DropdownWidget : UIelement` shows the active sort mode in a header and lists
the remaining modes in a popup; an `AscDescToggle : ButtonUIElement` flips
direction. Sort state is static per session (resets on game restart). The view
model `ItemListViewModel` owns the `int[] Order` indirection (display position →
catalog index), runs `Recompute()` on three triggers (mode/direction change,
`DiscoveredState.Changed` only when Mode=Found, and re-bake), and keeps the
filter/search seam (`DiscoveryFilter`, `SearchText`) at no-op defaults for
Iter-8. `ItemCatalog.Entry` gained `ObjectType ObjectType` (via a new
`objectTypeCache`) for the Category comparator. `ItemChecklistMod.ListView` is
(re-)constructed after each bake. Full mechanism in `docs/architecture.md §
List View-Model, Sorting & Filtering (Iter-7 / Iter-8 / Iter-10)`. **Iter-7.1 (DONE):** catalog-completeness
fix — `ItemCatalog.Bake` Loop 1 blanket-excluded `ObjectType.NonUsable` as
"garbage", but CK files raw materials (ores, bars, raw wood, scrap) under that
type, so they were silently missing. Replaced with a narrow guard that drops a
`NonUsable` item only when it has no icon (`smallIcon` and `icon` both null);
verified in-game (1.2.1.4) that the 126 `NonUsable` items are 117 real
materials (all have an icon) + 9 internal engine entities with no icon and no
localized name (territory spawners, `TheCore`, the `DroppedItem` entity,
boss-statue stubs). Catalog 10844 → 10835. IB's full `IsNonObtainable` can't be
reused (needs ECS/registry APIs the sandbox blocks). Full reasoning in
`docs/gotchas.md § Catalog / Bake (Iter-7.1)`. **Iter-8 (DONE):**
runtime discovery filter (All/Discovered/Undiscovered) + name search, wired to
the `ItemListViewModel` filter/search seam Iter-7 left at no-op defaults. Filter
= a second `DropdownWidget` instance (the `SortDropdown` subtree duplicated via a
deterministic fileID slot-remap, `ui_icon_filter` glyph). Search =
`SearchBar : TextInputField` (CK-native — PugText/caret/focus inherited; **not**
uGUI, the orphaned `UnityInputFieldAdapter` was deleted) + a `ClearSearchButton`,
in a 9-slice `Display` slot matching the dropdowns. **Option A** semantics:
search matches the *real* name of all items; undiscovered matches still render
`???`. One-line ViewModel change (dropped the discovered-only guard) +
`IsFiltered`-driven title `· N shown`. Focus persists while typing
(`dontDeactivateOnDeselect = true` + `HideUI` deactivates to free WASD on close).
Hard-won prefab traps (dead default material → invisible, Default-vs-GUI sorting
layer, caret PPU scale, duplicate-and-strip leftover button hijacks clicks) in
`docs/gotchas.md § Search Field / Header (Iter-8)`; full mechanism in
`docs/architecture.md § Filter & Search (Iter-8)`. **Iter-9 (Polish) — DONE (2026-06-04, branch `iter-9`).** A large UI
layout/behaviour pass:
- **Window + suppression:** near-fullscreen window, thin uniform 0.25u border
  (matching CK's inventory margin); **fixed** size — CK's orthographic UI camera
  shows a constant world area (`orthographicSize 8.4375`, 16:9 -> 30x16.875u) with
  no UI-scale option, so a fixed size is correct on every resolution. Help panel
  (`ShortCutsWindow`, toggled by **S**) + "?" prompt suppressed while open via two
  auto-discovered Harmony patches (the `ShortCutsWindow.LateUpdate` **prefix** is
  load-bearing -- `ShortcutsCanBeToggled` only gates the prompt, not the S keybind);
  HUD hidden via `Manager.ui.TemporarilyDisableGameplayUI()` (never
  `Manager.prefs.hideInGameUI`, which persists to disk) + an
  `InGameButtonHintsUI.LateUpdate` prefix; cursor restored via a `UIMouse.LateUpdate`
  postfix; ESC->pause race fixed by forcing `MenuManager.IsPauseDisabled` while open.
- **Header:** Sort/Filter dropdowns sized to the widest entry, right-aligned cluster
  flush to the scrollbar (x 14.2); popups widened to the full dropdown footprint;
  field heights 0.7 (bottom-fixed); square 0.7x0.7 toggle/ascdesc buttons; search
  field to 1/3 width with a full-field controller-focus highlight (the
  `TextInputField.selectedMarker`); footer split (counter right / "N shown" left);
  first-open input-leak fixed (ASCII "..." search hint avoided a thinTiny word-wrap
  crash that aborted ShowUI before CoreLib set currentInterface); the clear button
  at an off-grid x (4.005) to dodge the on-grid point-filter distortion (see
  `docs/gotchas.md` + the `project-corekeeper-sprite-ongrid-distortion` memory) and
  pulled forward in Z (`m_Center.z`) so its click wins the `UIMouse` raycast over
  the field collider.
- **Margins:** window side margins reduced + equalised, content symmetric +/-14.2.
- **Item rows:** height 2.5->1.5; **`RowHeight` read from the row prefab's
  background** at `Init` (single source of truth -- change the bg `m_Size.y` alone
  and the list re-spaces); **checklist-style checkbox** (empty box on every row +
  `ui_icon_requirement` inside when discovered); viewport pool buffer +2->+4;
  **RowHeight-independent flush** (row 0's TOP pinned to a fixed `MaskTopLocalY`,
  each row centre offset by `RowHeight/2`) so list start/end stay flush at any row
  height; row background ends at the scrollbar's left edge (no overlap). Mechanism
  in `docs/architecture.md` / `docs/gotchas.md`.

**Iter-10 (DONE):** sort & filter redesign — supersedes the Iter-7/8 option set.
Four sort modes: **Name**, **Rarity**, **Level**, **Value** (dropped Found + Category
sorts from Iter-7 — both are now filter dimensions, not sort axes). Level is read
from `LevelCD.level` via `PugDatabase.TryGetComponent<LevelCD>` (else 0) — NOT
`ObjectInfo.level`, which is dead/legacy and read nowhere in the game (confirmed via
decompile + IB `ObjectUtility.GetBaseLevel`). Value is a faithful port of IB's
`ObjectUtility.GetValue` (sell mode) in `ItemCatalog.ComputeSellValue`: `sellValue
== -1` is CK's "auto-compute from rarity + ingredients" marker, NOT "unsellable";
truly unsellable = `CantBeSoldAuthoring` OR rarity Legendary → 0 → rendered `—`.
Filter dimension replaced by `FacetedFilterWidget`: a single "Filter (N)" header
dropdown opening a sectioned popup with four OR-within / AND-across dimensions —
**Discovery** (Discovered/Undiscovered), **Category** (10-bucket taxonomy from
`ObjectType` ranges in `ItemCategory`/`ItemCategories.Of`: Weapons/ArmorAccessories/
Tools/Food/Placeables/Materials/Valuables/KeyItems/Instruments/Other), **Rarity**
(Poor…Epic), **Craftable** (Craftable/Not craftable). `ItemListViewModel` holds four
static `HashSet` dimensions; `Recompute` applies each as a `continue` predicate. A
dedicated "Clear all" action row (drawn from `actionTemplate` — its own pool, no
checkbox) resets all dimensions. Three pools in `FacetedFilterWidget`: checkbox rows,
action rows, section-header PugTexts; `RebuildList` re-syncs all checkbox visuals on
every click. Companion files: `FacetCheckboxButton`, `FacetToggleButton`.
Each row now shows right-aligned **Level** (`Lv N`) and **Value** (sell value)
columns + an **Ancient Coin** glyph (`ObjectID.AncientCoin` icon, loaded once via
`PugDatabase.GetObjectInfo` and shared across all rows). Em-dash `—` for level ≤ 0,
value ≤ 0, and undiscovered rows (spoiler guard). Labels are placeholder-English,
structured for Iter-11 (localisation) routing. Three new `ItemCatalog.Entry` fields
baked: `Level`, `SellValue`, `IsCraftable`.

**Iter-11 (DONE):** native localisation via CK's `TextDataBlock` / `ScriptableData`
mechanism. Term strings live in
`localization/localization.yaml` (mod-root, outside `unity/` so ModBuilder does
not pack it into the AssetBundle); the shared Editor helper
`utils/LocalizationGenerator.cs` (namespace `CoreKeeperModUtils`, symlinked by
`link.sh`; at the time gated behind `.envrc:USE_SHARED_EDITOR_HELPERS=1`, since
removed — see below) reads that YAML and
templates raw `.asset` YAML for each language — **Option II: raw asset templating**
— keyed by `utils/ck-language-addresses.json` (13 runtime languages, address→ISO,
runtime-dumped because `LanguageDataBlock`s are runtime-only and the SDK editor API
cannot enumerate them at build time). At runtime, terms are resolved via
`API.Localization.GetLocalizedTerm` through the `Loc.T` / `Loc.F` helpers, with
the raw term key as fallback. EN + DE shipped; adding a language later = add a YAML
language key and rebuild. The F1 keybind display name uses CK's own
`ControlMapper/ItemChecklist-ToggleChecklistPC` term. Language changes re-bake the
catalog (deferred to the next `Update` tick, guarded on `Manager.main.player !=
null`). This mod is the **pilot** for the shared `utils/` editor helpers
(`CLIBuildHelper`, `CLIPublishHelper`, `LocalizationGenerator`); `disable-durability`
and `faster-talents` still used per-mod helpers at the time of Iter-11 (both have
since migrated, and the opt-in flag `USE_SHARED_EDITOR_HELPERS` was then removed —
the shared helpers are now unconditional). Hard-won findings:
`LanguageDataBlock` is runtime-only (no SDK API at build time → Option II), the
`m_Script.guid` for `ScriptableData.dll` is per-SDK-clone-local and must be resolved
via `AssetDatabase.AssetPathToGUID` at generation time (not copied from IB), and
`PugFont` crashes on labels exceeding `maxWidth > 0f` with longer translations →
set `PugText.maxWidth = 0f` on all localised single-line labels. Full details in
`docs/gotchas.md § Localisation (Iter-11)`.

**Iter-11.5 (DONE):** always-on HUD discovery counter — the window footer's
`N / M (p.p%)` mirrored as a permanent top-right HUD readout (above the minimap),
with a checkbox-framed icon (`ui_slot_toggled_border` box + `ui_icon_requirement`
tick at 0.7 scale, like a discovered list row). This is the mod's first
**non-modal** UI: a dedicated `ItemChecklistHud : UIelement` in its own
`Prefabs/ItemChecklistHUD.prefab`, instantiated directly by `ItemChecklistMod`
(routed by GameObject name in `ModObjectLoaded`, **not** via CoreLib
`RegisterModUI`) and parented under `chestInventoryUI.transform.parent`
(`IngameUI`). The counter string comes from a new shared `ProgressFormat.Counter`
helper that the window footer (`FormatTitle`) also adopts — one source of truth,
no drift. Live refresh via `DiscoveredState.Changed` plus both bake hooks
(world-load + loc-change). Three hard-won in-game findings (see
`docs/gotchas.md § HUD Counter (Iter-11.5)`): **(1)** the renderers must sit on
the **HUD Unity layer (27)** — on layer 5 (UI) the uiCamera never draws them during
plain gameplay (the modal window only renders because CoreLib's open-path activates
it); **(2)** content must sit at local **z=10** (world z≈0), the plane CoreLib
positions modal UIs to via `initialInterfacePosition` — at the parent origin
(world z=-10) it is outside the uiCamera frustum (`SpriteRenderer.isVisible ==
false`); **(3)** `Manager.ui.CalcGameplayUITargetScaleMultiplier()` (CK's "native
HUD idiom") returns `(0,0,0)` for a mod HUD here, so visibility is explicit:
`isInGame && Manager.main.player != null` (the player term suppresses the
world-load screen — the Iter-15 bug class) `&& !Manager.ui.isAnyInventoryShowing
&& !Manager.menu.IsAnyMenuActive()` (the CoreLib-patched aggregate
`isAnyInventoryShowing` covers inventory, crafting **and** the checklist window).
Bonus: HUD-layer membership means CK's own `CameraManager.ShowHUD(false)` culls the
counter together with the rest of the gameplay HUD, for free. Full mechanism in
`docs/architecture.md § HUD Counter (Iter-11.5)`.

**Iter-11.6 (DONE):** load-screen visibility fix — the Iter-11.5 HUD showed on the
world-load screen (entering) and lingered on the exit fade to the main menu, because
both it and the Iter-15 F1 open-guard gated on `Manager.main.player != null`,
wrongly believing that suppressed the load screen. It does not: the player object is
instantiated at `PlayerController.OnOccupied` (the bake anchor) *while the load
screen is still up* and survives the exit transition, so `player != null` is true
across **both** load screens. Introduced a shared `WorldState.IsInPlayableWorld`
predicate mirroring CK's own `PlayerController.PlayerInputBlocked` gate
(`isInGame && isSceneHandlerReady && !Manager.load.IsLoading()`); both the HUD
visibility check and the F1 open-guard now use it. Chose `!Manager.load.IsLoading()`
(`loadingQueue != null`) over CK's `IsLoadingAndScreenBlack()` — the latter is only
true while the screen is fully black and would let the HUD flash during the exit
fade-out. This closes the **loading-screen** half of roadmap Iter-15 (the F1-over-
loading-screen bug); the **cutscene/intro** half still needs a separate input-locked
signal and remains open. Verified in-game (1.2.1.4): clean sandbox compile (zero
`CompileFailed`), no per-frame NRE from the HUD `LateUpdate`. Full reasoning in
`docs/gotchas.md § HUD Counter`.

**Iter-12 (real pixel-art sprites) — DONE (2026-06-14, branch `iter-12`).**
Replaced every Item Browser placeholder sprite with original pixel-art authored
in Pixaki. A deterministic generator (`utils/pixaki_to_sheet.py`, TDD) packs the
`.pixaki` layers into one `ui_checklist` sheet (25 sprites, stable internal IDs
from name hashes) and templates the `.png.meta` (sprite rects + 9-slice borders);
the prefabs were rewired off the IB `Art/Bridge/` GUIDs onto the sheet (verified
**zero IB references** remain), and the dev-only `Art/Bridge/` folder was deleted.
The `.pixaki` master is now versioned (`sources/`). After the user re-sliced /
renamed sprites in Unity's Sprite Editor, the generator became **off-limits**
(re-running would overwrite the manual `.meta` edits) — the committed sheet +
prefab are the source of truth. Dropdown popups gained runtime auto-sizing
(`DropdownWidget`/`FacetedFilterWidget` compute panel height/position from the row
count, top edge read from the prefab), and the hardcoded `MaskTopLocalY` /
`FallbackWindowHeight` were derived from the `ContentsMask` (fixing a latent
fallback-height drift). In-game calibration fixed three hard-won issues (all in
`docs/gotchas.md § Sprite Sheet & UI Sorting`): **(1)** the footer status-line
drew over open dropdown popups because all PugText sits on the GUI layer at
`orderInLayer 9999` — lowered the footer to 50 (< popup BG 54); **(2)** the filter
checkbox boxes were invisible because the static box GO (`Checkmark`) had
`m_IsActive: 0` (the code only toggles the fill); **(3)** the selection marker
stretched instead of 9-slicing because its 3px corners needed
`spriteBorder {3,3,3,3}`, not `{1,1,1,1}`. A "fix didn't show" red herring was
traced via mtime (edit newer than the last build = simply not rebuilt), not the
AssetDatabase/symlink cache. Real pixel-art for the remaining placeholder sprites
(rarity border, scrollbar) continues as polish toward 1.0.0.

**Iter-12 extension (2026-06-15, direct on `main`, no separate branch).** A
follow-up pass that *reactivated* the generator and reworked the icon slot:
- **Generator reactivated** (supersedes the "off-limits" note above):
  `utils/pixaki_to_sheet.py` now reproduces the manually-edited sheet —
  `RENAME` / `PAD` / `BORDER_OVERRIDE` tables fold the Sprite-Editor renames,
  9-slice borders, and bottom-anchored padding back into the source, and a
  `--guid` flag keeps the committed sheet GUID stable. The `.pixaki` master is
  the single source of truth again; the sheet is regenerable (TDD, 7 tests green).
- **Unknown Object icon:** the sheet grew 25 → 26 sprites with a dedicated
  16×16 Unknown Object icon (`internalID 60782178`). Undiscovered rows now show
  that sprite (rarity-tinted) in the icon slot instead of a PugText "?"; the
  `Placeholder` GameObject + its PugText were removed (a sprite is cheaper).
- **Native icon sizing + iconOffset:** item icons render at native scale (no
  scale-to-fit) in an enlarged **1.25u** slot, positioned by the game's per-item
  `iconOffset` like CK/IB. To make the offset slot-relative, `Icon` was
  reparented under `IconSlot`; the `IconSlot` background and rarity border were
  enlarged to 1.25u to match.
- A latent inactive `ContentMask` GameObject (parked per-row-mask experiment)
  was committed in isolation. Shipped as five clean logical commits across both
  repos (generator + tests in `core_keeper`; sheet + window-prefab, parked
  `ContentMask`, Unknown-Object feature, native-sizing feature, and a
  canonical-serialization commit in `item-checklist`).

**Iter-14.1 (search-caret alignment) — DONE (2026-06-15, branch `iter-14-1`).**
The blinking search caret sat a few px too low and flush against the text. Root
cause (Pug.Other decompile): `TextInputField.Update()` recomputes the caret
position every frame and writes `characterMarkBlinker.transform.position`
(world X/Y, Z preserved) — so any offset on the caret GO itself is clobbered.
Fix (pure prefab, zero C#): a child GO `CaretSprite` under the caret GO now
carries the caret `SpriteRenderer` at a constant `localPosition`
(`+1px` up to centre, `+2px` right for a small gap); the child inherits the
per-frame parent position and adds the constant nudge. The `SpriteRenderer`
kept its fileID (just re-homed to the child), so `CharacterMarkBlinker.sr`
needed no rewire. The caret was also shortened `8px -> 7px` via the sprite's
existing vertical 9-slice (SR `DrawMode` Sliced, `m_Size` 2x7) — the 1px
top/bottom border caps stay fixed and the uniform middle column compresses
invisibly; no sheet/generator change. All three (height, vertical centre,
horizontal gap) calibrated in-game. **Corrected a stale assumption:** the
roadmap/memory still described the caret as a stretched 1x1 `white_pixel`; in
fact Iter-12 had already swapped it to the painted 2x8 `Caret` sheet sprite and
removed the `{0.8,6,1}` scale hack — only the position/height remained.
**Process note:** several confusing calibration rounds were lost to an
*intermittent* Unity AssetDatabase staleness when building from the worktree
(symlink-target edits silently not re-imported, fresh bundle mtime
notwithstanding); the fix is clearing `Library/{SourceAssetDB,ArtifactDB,Artifacts,Bee}`
to force a reimport. Documented in `docs/gotchas.md § Worktree builds`.

**Iter-13 (Dropdown prefab extraction) — DONE (2026-06-16, branch `iter-13`).**
Made the dropdown genuinely reusable: the header+popup skeleton, formerly
**hand-copied** in the window prefab (Sort cluster + FacetedFilter cluster), now
lives once as a shared `Dropdown.prefab` **chrome**, consumed by **two** sources —
Sort as a nested instance, FacetedFilter as a **prefab variant**. The window no
longer contains a duplicated skeleton (0 concrete `RowTemplate`/`ToggleButton`/
`Popup`/`RowContainer`). Mechanism chosen after a tracer (Task 1) **proved nested
PrefabInstances + variants round-trip through the ModBuilder→AssetBundle
pipeline** — the first nested-prefab use in any mod here (prior art was only
separate-prefab + runtime-instantiation à la `ItemRow`/HUD). The Editor authored
the nested instances/variants (their stripped cross-references are exactly what
hand-YAML gets wrong); the assistant did builds, YAML verification, C#, and docs.

Hard-won points:
- **Reusable widget = C# class was already reusable; the missing piece was the
  PREFAB.** The chrome = pure GameObjects/sprites/colliders; consumers only *add*
  (root widget component, content templates) — never *remove* (Unity variants
  can't remove inherited components), which is why the base omits the
  consumer-specific bits.
- **Serialized cross-prefab `owner` refs are fragile.** Extracting the chrome
  nulled the header `DropdownToggleButton`'s serialized `owner` → header-click
  died (caught in-game, not by the Editor compile). Fix: wire `owner` at runtime
  in `Configure` for *all* child toggles, not just the one serialized `toggle`.
- **One shared toggle type via a minimal `IPopupToggle` seam.** The chrome can
  only carry a single toggle component; `DropdownToggleButton` and
  `FacetToggleButton` differed only in `owner` type. Introduced
  `IPopupToggle { TogglePopup() }` (both widgets implement it), retyped the
  toggle's `owner` to it (runtime-wired), and **deleted `FacetToggleButton`**.
  Narrow, prefab-driven seam — the broad six-subclass unification stays Iter-14.2.
- **Variant = base chrome + additions + deactivations + layout overrides.** The
  FacetedFilter variant adds `FacetedFilterWidget` + checkbox/header/action
  templates, **deactivates** the inherited `AscDescButton` (the filter has no sort
  direction), and overrides the header layout to the original no-asc-desc look
  (Display pos/width + collider, DisplayIcon filter glyph + pos, DisplayLabel pos,
  caret pos) — derived by a systematic diff of the original filter vs the chrome.
- **`ModObjectLoaded` routing** switched to a name **whitelist** (register only
  `ItemChecklistWindow`; capture `ItemChecklistHUD`; log-and-skip building blocks).
  Reducing the chrome to a component-less prefab incidentally dropped it from the
  top-level loaded-objects set anyway.
- The **unified-field header redesign** (Toggle+AscDesc in one dark `Field` bg)
  was deferred to Iter-18 — a visual redesign, out of Iter-13's scope.

`utils/prefab_query.py` gained a `tree` command (GO hierarchy + `[inactive]`
markers) during this iter — invaluable for verifying variant structure (variant
YAML defeats grep/awk; the PyYAML-based loader is reliable). Branch was rebased
onto main (3 intervening doc commits) before merge — linear history, no squash.

**Iter-18 (combobox header + skeleton chrome) — DONE (2026-06-16, branch
`iter-18`).** Merged the dropdown header into a single combobox `Display` field
and completed the Iter-13 chrome extraction. Two user-facing changes: the
`Caret` moved *inside* the `Display` (the separate `ToggleButton` GO and its
button-background sprite are gone — `Display` already carried its own
`DropdownToggleButton`, so it became the sole open/close target), and the sort
`AscDesc` toggle moved into the `Display` too (Sort-only). The asc/desc glyph
took over the leading (`DisplayIcon`) slot — calibrated in-game: `DisplayIcon`
hidden, `AscDesc` repositioned to the icon slot at full scale, its `BoxCollider`
pulled forward (`m_Center.z = -0.1`) so CK's `UIMouse` 3D raycast hits it before
the field's open/close collider (the Iter-9 ClearButton precedent); the shared
`DisplayLabel` shifted left (x -1.3 → -2.5) so it clears the leading glyph —
shared in the skeleton because Sort's `AscDesc` and Filter's glyph occupy the
*same* leading slot, fixing both fields at once.

Structure (the deferred Iter-13 "unified-field" idea, realised as **two sibling
variants** — the idiomatic Unity handling of "derived" prefabs, chosen over a
window-level instance-override pile after weighing prefab-variant best practice):
`Dropdown.prefab` reduced to a pure **skeleton** (`Field/Display{DisplayIcon,
DisplayLabel, Caret}` + empty `Popup/RowContainer`; no widget component, no
templates, no asc/desc); `Sort.prefab` (new variant) adds `DropdownWidget` +
`RowTemplate` + the in-Display `AscDesc`; `FacetedFilter.prefab` renamed to
**`Filter.prefab`** (GUID preserved, file-and-root-GO renamed together via
Unity's "Rename File" prompt) and slimmed to inherit the new skeleton. The window
now nests instances of both variants (0 direct bare-`Dropdown` refs remain).
**Behaviour unchanged** (popup lists the non-selected modes; selecting closes;
mode switch preserves direction); `AscDescToggle.cs` and the `DropdownWidget`
list model untouched. **Pure-prefab, zero behavioural C#** — all serialized refs
(`caret`/`toggle`/`rowTemplate`/`ascDescToggle`) resolve to the re-homed GOs.

Hard-won points (all caught by per-step `prefab_query.py`/GUID verification
before the build — every one would have been a *silent* failure that a clean
Editor compile and even the build would not surface):
- **Three silent wiring gaps in `Sort.prefab`** (the `AscDesc` GO missing, then
  `DropdownWidget.rowTemplate` = `fileID: 0`) — a null `rowTemplate` makes
  `EnsurePool` early-return, so the sort popup would silently be empty. Verified
  by matching widget fields against the expected component GUIDs, not by eye.
- **A leftover GO named `RowTemplate` in the Filter variant was NOT dead weight**
  — it *is* the `checkboxTemplate` (the widget field pointed at its fileID). The
  GUID/fileID cross-check (field → GO) showed the real usage where the name lied;
  it was cosmetically renamed `CheckboxTemplate` (fileID preserved → ref holds).
- **Dangling override:** removing the inherited `AscDescButton` from the base
  left the Filter variant's old "deactivate AscDescButton" `m_IsActive:0`
  modification targeting a now-deleted base fileID. Unity **does not prune**
  target-less modifications (reimport did not clear it) and the Editor cannot
  surface them for removal (no resolvable target) — they had to be **stripped
  directly from the variant YAML** (Editor closed), validated by a PyYAML
  re-parse + the build. A harmless no-op meanwhile (the prefab merge ignores
  unresolvable targets), but exactly the cruft the clean extraction set out to
  avoid. The `FacetedFilterWidget` class rename → `FilterWidget` was deferred to
  Iter-14.2 to keep this iteration prefab-only.

**Iter-14.2 (UI code audit — refactor & consolidation) — DONE (2026-06-17, branch
`iter-14-2`).** A behaviour-neutral C# refactor over the UI layer, consolidating five
duplicated patterns into single sources of truth. Build-gated, in risk order, each its
own commit + in-game smoke test (R1→R2→R4→R5→R3):
- **R1 — `ClickButton` base.** The five `ButtonUIElement` subclasses
  (`DropdownOptionButton`/`DropdownToggleButton`/`AscDescToggle`/`ClearSearchButton`/
  `FilterCheckboxButton`) repeated the identical click prologue (guard `canBeClicked`,
  call base). Hoisted into `abstract ClickButton : ButtonUIElement` whose
  `sealed override OnLeftClicked` runs the prologue then calls `protected abstract
  OnClick()`; subclasses implement only `OnClick`. Prefab-neutral (abstract base never
  referenced; subclass `fileID 11500000` unchanged).
- **R2 — `FacetedFilterWidget`→`FilterWidget` + `FacetCheckboxButton`→
  `FilterCheckboxButton`** (the Iter-18-deferred rename). `git mv` of `.cs`+`.meta`
  together preserved the GUIDs (`a10e0183`/`7a9577`), so `Filter.prefab` and the
  window's nested instance kept resolving; the window's serialized field
  `facetedFilter`→`filter` needed a matching prefab YAML field-key edit, verified with
  `prefab_query.py` before the build (a mismatched field name deserialises silently to
  null). Two stale prose comments also de-Facet-ed.
- **R4 — removed the redundant `_scrollable` reflection.** Decompiling
  `UIScrollWindow.Awake` showed it copies the prefab's serialized `scrollable` field to
  its private `_scrollable` itself, so BOTH the `ItemChecklistContent.Awake`
  self-registration (an *ineffective* guard — Awake checks the public `scrollable`, not
  `_scrollable`) and the two `ItemChecklistWindow` `SetValue` calls were redundant.
  Removed both; the duplicated rewire block collapsed to one `RewireScrollHeight()`
  (UpdateScrollHeight + ResetScroll). Hypothesis-gated: a dedicated scroll smoke test
  (wheel/drag/sort-filter/reopen, 0 `disabling UIScrollWindow`) confirmed it — and a
  **main cross-build** reproduced the exact same scroll behaviour, proving redundancy
  empirically (a conservative-dedup fallback was prepared but not needed).
  `DefaultExecutionOrder(-100)` kept defensively.
- **R5 — `PugText.RenderNoWrap` extension.** The `maxWidth = 0f; Render(...)` pair (the
  PugFont long-label word-wrap crash guard) appeared ~6× across the two widgets; folded
  into one null-safe extension (all `maxWidth = 0f` now lives only there).
- **R3 — `PopupWidget` base** (the main payoff, done last). `DropdownWidget` +
  `FilterWidget` duplicated nearly the whole popup machinery; extracted into
  `abstract PopupWidget : UIelement, IPopupToggle` carrying the chrome serialized fields
  (caret/caretClosed/caretOpen/popupPanel/rowContainer/rowSpacing), `SetOpen`/
  `TogglePopup`, the click-outside `LateUpdate`, and the auto-size. The one-row offset
  that genuinely differs (Sort reserves popup row 0 for the header-shown selected option
  → `FirstRowOffset = 1`; Filter starts members at row 0 → `0`) is captured ONCE as the
  abstract `FirstRowOffset`, feeding both the row layout and `AutoSizePopup` so they
  cannot drift. Filter's open-time rebuild became an `OnPopupOpened()` override. Moving
  the chrome fields to the base is prefab-neutral — Unity deserialises inherited public
  fields by name (all six keys verified present on `Sort.prefab`/`Filter.prefab` before
  the build); `LateUpdate` became a proper `override`, clearing the long-standing CS0114
  hide warning.

**Discovered (out of scope, logged → roadmap):** a pre-existing CK PugFont word-wrap
crash — typing in the search field throws `IndexOutOfRangeException` *per frame*
(`PugFont.AddNewLinesToLinesExceedingMaxWidth ← TextInputField`, the field's
`maxWidth: 7.5`). Empirically confirmed on **main** too (127× with the same input, same
stack), so iter-14-2 neither caused nor worsened it. Net C# **+23 LoC** — three new
*documented* base/helper files offset the consumer shrinkage; the win is structural
(single sources of truth), not raw line count. (Process note: Unity overwrites
`Player.log` per launch, rotating the prior session to `Player-prev.log` — so each
grep is single-session.)

**Iter-15 (F1/HUD over the intro cutscene) — DONE (2026-06-18, branch
`iter-15`).** Closed the cutscene half of the Iter-4/Iter-11.6 toggle-guard work.
The F1 open-guard already blocked both world-load screens (Iter-11.6 via
`WorldState.IsInPlayableWorld`) but not the in-game spawn-from-Core intro
cutscene; F1 could still pop the checklist over it. A latent instance of the same
bug sat on the always-on HUD: the intro cutscene fades CK's HUD via
`Manager.ui.FadeOutAllGameplayUI()` (which only fades CK's *own* registered
gameplay UI), **not** `Manager.camera.ShowHUD(false)` (which would cull our
layer-27 HUD for free), so the ItemChecklist HUD stayed visible during the
cutscene. Fix = one term, `&& !sceneHandler.cutsceneIsPlaying`, appended to the
shared `WorldState.IsInPlayableWorld`; because both the F1 guard and the HUD
already gate on that predicate, the single edit fixed both. `cutsceneIsPlaying`
(public `bool` on `SceneHandler`, delegating to `optionalCutsceneHandler.isPlaying`,
false when no handler) is the canonical signal — CK itself gates a discovery path
on it (Pug.Other ~301674) with the same companions this predicate uses. Rejected
the broader `SendClientInputSystem.PlayerInputBlocked()` (overlaps the menu/
inventory checks the guard already does + per-frame UI-input logic). Sandbox-safe
by precedent (property access on the already-used `Manager.sceneHandler`),
confirmed by a clean Phase-1 compile (zero `CompileFailed`). Pure behavioural
one-liner + docstring/comment hygiene; no prefab/art touch.

**Iter-19 (search-field word-wrap crash) — DONE (2026-06-18, branch `iter-19`).**
Killed the per-frame `IndexOutOfRangeException` thrown while typing in the search
field via CK's `PugFont.AddNewLinesToLinesExceedingMaxWidth ← TextInputField` — a
pre-existing CK bug logged out of scope during Iter-14.2 R5 (reproduced on **main**
too, 127× same stack with the same input; silent to the player but log-spammy).
Root cause (Pug.Other decompile): `TextInputField.Awake` sets
`pugText.maxWidth = maxWidth + (dontAllowNewLines ? 1 : 0)` — for this field
`7.5 + 1 = 8.5` — so every `pugText.Render()` runs the word-wrap path, whose
`text[num3 - 1]` indexes out of range on certain input. A single-line field
(`dontAllowNewLines: 1`) must never word-wrap. Fix = `SearchBar` overrides `Awake`
(`private new void Awake()` — CK's `Awake` is non-virtual; calls `base.Awake()`
then `pugText.maxWidth = 0f`). **Corrected the roadmap's own fix candidate:** the
prefab `pugText.maxWidth = 0` is a no-op because `Awake` rewrites it at runtime, so
the fix had to come from code. **Visual width is preserved**: the field's *own*
`maxWidth` (7.5) still clips overflowing characters via
`TextInputField.TrimTextToFitRestrictions` (a char-trim loop, independent of the
PugText word-wrap) — the two `maxWidth` roles are decoupled. Done in `Awake` (not
`LateUpdate`, which runs *after* the same-frame render) so it holds before the
first render — covers `SyncFrom` restoring a long prior search on open; nothing
rewrites `pugText.maxWidth` per frame, so one write persists. Same CK PugFont bug
class the Iter-9 ASCII search-hint and the Iter-11 `RenderNoWrap` (`maxWidth = 0`)
labels sidestepped — `TextInputField` is the one place the value is reimposed.
Pure behavioural C# (one `Awake` override); no prefab/art touch. Verified in-game
(1.2.1.4, fake-ID dev build 9999997): clean sandbox compile (passed code security
verification, `safetyCheck=True`, zero `CompileFailed`), and typing a long string
produced **0** `IndexOutOfRangeException` (127× on main with the same input). See
`docs/gotchas.md § Search Field / Header` for the mechanism.

**Iter-20 (possession counts) — DONE (2026-06-20, branch `iter-20`).** A second
completion axis beside discovery: each checklist row shows how many of that item
the player currently **owns** (a right-aligned count column), the checkbox + "done"
tick turn **blue** when owned >=1, and a new **"In / Not in possession"** filter
section sits under Discovery. The goal is completionists who want "own >=1 of every
item", not just "discovered every item".

Architecture — a `possession/` package read live from the ECS world each refresh:
- `PossessionScanner` resolves the inventory world (`World.All`, max
  `ContainedObjectsBuffer` count = ServerWorld in SP), then scans all placed
  `ObjectDataCD` entities. **Possession = carried + base storage.** Carried is the
  player's whole `ContainedObjectsBuffer` (always live; includes the 0–9 equipment
  slots, so worn gear counts). Base storage = placed furniture/display within
  `AnchorRadius` of a crafting-station anchor; its contents are read from the
  entity's buffer.
- `PossessionLedger` keys containers by world tile `(x,z)` (packed `long`), merges
  `carried + per-container` in `BuildView`, and marks items present only in
  not-currently-observed containers as "remembered" (the player can check ownership
  while away from base). `PossessionStore` persists it per character GUID via
  `API.ConfigFilesystem` (hand-rolled ASCII, sandbox-safe). `PossessionConfig`
  exposes `AnchorRadius`; `PossessionClassifier` holds the type/ID predicates.

Hard-won correctness fixes (each its own commit, all caught in-game — none by the
Editor compile):
- **Durability vs stack.** `ContainedObjectsBuffer.amount` is double-purposed:
  stack size for stackables, but **durability** for equipment — a full-durability
  hat counted as 50. Mirror CK's `GetTotalAmount`: stackable → amount,
  non-stackable → 1 per slot; look up stackability at **variation 0** (a
  non-existent `(objectID, variation)` returns null and would wrongly take the
  durability branch for some skins).
- **Count placed objects themselves**, not just contents — a workbench / torch /
  decoration is owned with or without an inventory (broadened the query from
  `ContainedObjectsBuffer` to all `ObjectDataCD`).
- **Locked chests + boss statues:** count the placed **object** (owned furniture)
  but **not** contents (loot is unknown until opened; boss "special" chests are
  mineable + carriable so the player does own the chest).
- **Per-tile MERGE.** Multiple counted entities can share a tile (a wall torch
  standing on a mannequin's tile); the old per-entity `SetLiveContainer` overwrote,
  so the mannequin's displayed armor counted 0. Accumulate every entity on a tile
  into one dict (`Tile`/`AddOne`), flush once. Diagnosed from an in-game F-key dump
  that showed `id=110` (Torch) at the exact mannequin tiles.
- **Drop the `MineableCD` gate.** `type==PlaceablePrefab` + near a clustered anchor
  is the ownership test; some owned placeables (a WayPoint) are removed via a menu,
  not by mining, and were wrongly missed.

**Cluster filter — what counts as "your base".** Using "near a crafting station" as
the base proxy mis-fired on a remote container near a *world* station: a Copper Key
in **Ghorm's (boss) unexplored spawn arena** showed as owned because a lone station
there anchored it. Fix: a station only anchors if it is part of a **cluster** (>=1
other station within 16 tiles) — a real base packs stations together; a lone
outpost / boss-arena / NPC station does not. Starter setup (workbench + furnace
placed together) still qualifies. The `(x,z)` ledger's "remembered" model is the
counterpart: a real Copper Key at a far waypoint correctly stays counted while away
(check-anywhere), and self-heals within the load radius (180 tiles) when revisited.

**Persistence — the real fight.** The ledger save was gated on a GUID change to
empty (`SaveManager.SetCharacterId(-1)`), which a normal **"Save & Quit" does NOT
reliably trigger** — so the ledger never reached disk on a clean quit (file simply
stayed absent, no save error; only the discovery-snapshot half of the shared hook,
which fires on char-*select*, was ever exercised). Root-caused by inspecting the
on-disk file (absent) + the log (no save error) + the GUID hook. Fix: a Harmony
postfix on **`SaveManager.WriteCharacter(int)`** — CK's actual character-file write,
firing on autosave **and** "Save & Quit" — persists the ledger in **lockstep** with
CK's own save (never ahead of it; symmetric to the `OnAfterDeserialize` load hook).
All triggers consolidated into one `SavePossessionLedger()`; char-switch + Shutdown
kept as backstops. (A periodic-timer autosave was prototyped first, then rejected in
favour of the CK-aligned hook — saving ahead of CK's save risks a post-crash
desync.)

**Search-field focus regression.** Refreshing the list *after* `OpenModUI` rebound
the rows a second time and raced the search field's focus init — the caret blinked
but keystrokes were swallowed until another widget was clicked. Fixed by running the
scan + `ListView.Refresh()` **before** opening.

**Deferred / known limits:** the mod training **Dummy** is typed `Creature` (900),
so it (and tamed pets) belong to **Iter-16**, not the furniture path; the "Ancient
Chest **(Items/...)**" raw-term display is a follow-up; and a *clustered* foreign
base (NPC village / second base) still anchors — true base detection is unsolved
(CK has no base concept). Verified in-game (1.2.1.4, fake-ID dev build 9999997):
clean sandbox compile, possession counts correct across carried / equipped /
mannequin-displayed / stored, persistence round-trips a base item across a full CK
restart, and the Ghorm false-positive is filtered out.

**Iter-21 (possession spoiler-gated behind discovery) — DONE (2026-06-20, branch
`iter-21`).** Began as the tentative "missing catalog entries (waypoints)" item and
**re-scoped after diagnosis disproved the premise**. The user reported waypoints
never getting a checklist row; the roadmap guessed `ItemCatalog.Bake` dropped them
into an excluded `ObjectType` bucket (most likely `NonObtainable`). The code
contradicted that guess — `PossessionScanner` already recognised a waypoint as
`PlaceablePrefab` (which the catalog does **not** filter) — so the exclusion path was
genuinely unknown and had to be measured, not assumed.

**Diagnostic probe (throwaway, two detectors).** A temporary `RunIter21Probe()` was
folded into `ItemCatalog.Bake` (committed, then reverted before the fix — net-zero on
`ItemCatalog.cs`): **(A)** a bake self-audit re-classifying every `objectsByType` key
with the Loop-1 branch logic, and **(B)** a cross-source diff against the live ECS
world (mirroring `PossessionScanner`'s world resolution) for placed obtainable
entities absent from the bake source. In-game (1.2.1.4) result — the decisive line:
`WayPoint(6514): hasVar0=True … type=PlaceablePrefab icon=has rarity=Epic` → it is
`ACCEPTED`, i.e. **already in the catalog**; (B) reported **0** missing; the 272
`NonObtainable` sampled as genuinely non-obtainable (boss spawn anchors,
`Affix*` projectiles, boss attack entities). **There was no catalog-completeness
bug.**

**Why the waypoint showed `???`.** It *is* in the catalog but rendered undiscovered.
Two hypotheses: **H0** — a world-spawned Core waypoint the player never picked up
(genuinely undiscovered; `???` correct); **H1** — discovered at a non-0 variation
while the row checks variation 0 (`IsDiscovered(objectId, 0)` exact-match miss →
a real variation-keyed-discovery bug). The mine-and-recheck test that would separate
them was unavailable (mining damage too low to harvest a waypoint — itself strong
evidence for H0: never harvestable ⇒ never held ⇒ never discovered ⇒ the owned one is
world-spawned).

**The fix the user chose dissolves H0/H1 for this iteration.** A screenshot showed
the incoherent state directly: an undiscovered (`???`) waypoint row with a **blue**
(owned) checkbox — Iter-20's world scan counts the placed object regardless of
discovery. Decision: **possession is spoiler-gated behind discovery.** This aligns
possession with the existing Iter-10 spoiler guard (Level/Value already em-dashed
for undiscovered rows) and is internally consistent by construction — it ties
possession to the *same* discovered flag that drives `???`-vs-name, so a `???` row
can never show possession, whichever hypothesis is true.

Implementation — one chokepoint: `ItemChecklistMod.OwnedCount(int objectId, int
variation)` returns `Possession.Count(objectId)` only when
`DiscoveredState.Instance.IsDiscovered(objectId, variation)` (else 0, also the safe
default before the discovery snapshot loads). Both prior read sites route through it:
`ItemChecklistContent` (owned column + blue tint) and `ItemListViewModel` (the
In/Not-in-possession filter). Pure behavioural C# (+20/−2 across three files); no
prefab/art touch. Verified in-game (1.2.1.4, fake-ID 9999997): clean sandbox compile
(`ItemChecklist safetyCheck=True`, 0 `CompileFailed`); the undiscovered waypoint row
keeps `???`, drops the blue checkbox and shows `—` for owned, and is excluded from
"In possession" / included in "Not in possession"; a discovered owned item still
shows blue + count. **Deferred:** the H1 variation-keyed-discovery case (a family
discovered only at a non-0 variation still showing `???`) → **Iter-17**
(per-variation tracking); the gate is correct independent of it.

**Iter-16.1 (per-skin pet collection) — DONE (2026-06-21, branch `iter-16-1`).**
**Re-scoped from the frozen roadmap's "pet/creature discovery" guess** (the THIRD
time a roadmap catalog-exclusion guess was wrong — cf. Iter-21 waypoints). The
roadmap said the bake "blanket-excludes Creature/Critter so tamed pets never get a
row." Five decompile probes + in-game observation disproved that: `ObjectType.Pet`
is **802**, NOT `Creature` (900) — and 802 is **not** on the bake's exclusion list,
so **pets were already in the catalog** (confirmed in-game: Subterrier/Eulux/
Glutschweif all present, discovered, with owned counts). The real gap was different
and only surfaced by measuring.

**The verified CK pet model.** Pets always sit at `variation 0` in CK
(`SaveManager.SetObjectAsDiscovered` force-zeroes pet variation); the skin lives in
`PetSkinCD.skinIndex` (world-global inventory aux data, NOT in variation), assigned
**randomly** on hatch (`rng.NextInt(maxSkins)`). All skins of a pet share one
ObjectID. **CK tracks no per-skin discovery** and has no native skin-collection.
Skins render as gradient recolors of one base sprite. (Full facts in the
`reference_ck_pet_critter_discovery_model` memory, the diagnostic probe
`docs/research/iter-16-1-pet-probe.md`, and the retained design spec
`docs/specs/2026-06-21-iter-16-1-pet-skin-collection-design.md`.)

**Decision (the user's framing): each skin is a separate collectible.** So the
single skinless pet row is replaced by one row per skin (`skins.Count` rows; ~63
total replacing 14). Settled design: **D1** collected = "ever-owned" (CK gives
nothing per-skin → a mod-owned `(objectID, skinIndex)` ledger, persisted via the
Iter-20 `PossessionStore` mechanism); **D2** spoiler-consistent display (species
`???` if undiscovered; known species' uncollected skin shows the name + unknown
icon + `—`; collected → skin icon + count); **D3** rows named `"<Pet> (Skin n)"`
(localized suffix; CK has no skin names). A Task-0 throwaway probe drove one
mid-flight correction: Level/Value are **em-dashed for pets** — `LevelCD.level`
(7/10/16) is a prefab tier field, NOT the pet's trainable per-instance gameplay
level (1 in a chest, 8 equipped — caught in-game), and a per-species row cannot show
a per-instance level.

**Architecture (one scan path, three outputs).** `ItemCatalog.Bake` Loop 3 emits
per-`(objectID, skinIndex)` entries (skinIndex in the entry's `Variation` slot,
`IsPetSkin` flag). `PossessionScanner` reads each owned pet's `PetSkinCD.skinIndex`
(carried + containers via `InventoryHandler.TryGetExtraInventoryData`, **plus the
active summoned pet** via `PetOwnerCD.PetEntity` — fixing the Iter-20-deferred
Terrier 7-vs-8 undercount), tallies per skin, and marks the persistent
`PetCollection` ledger. `ItemChecklistMod.OwnedCount` + the row-bind route pet
entries through `PetCollection` (collected) + species discovery (name) instead of
CK's skin-blind `DiscoveredState`.

**Gradient icons — the hard-won part (3 attempts, systematic-debugging).** Recoloring
needs a material on the right shader. Attempt 1 (keyword on the default material) =
no-op. Attempt 2 (`new Material(Shader.Find("Radical/SpritesDefault"))`, a decompile
agent's hedged guess) = still no-op — `Shader.Find` returned a real but **wrong**
shader, masking the error. The fix came from the **working reference, Item Browser**:
its `GetUISpriteColorReplaceMaterial()` is `new Material(Shader.Find("Amplify/
UISpriteColorReplace"))` — that shader carries `_GradientMap` + `USE_GRADIENT_MAP`.
One per-skin material on it (cached, assigned via `sharedMaterial`; base material
restored for non-pet rows) → skins recolor correctly, no cross-row contamination.
Needed an asmdef reference to `ScriptableData.dll` (`GradientMapDataBlock` lives
there — caught by the Task-0 probe's `CS0012`).

**Process notes.** Worktree AssetDatabase staleness bit repeatedly — clear
`Library/{SourceAssetDB,ArtifactDB,Artifacts,Bee}` before every worktree build. A
`cd ~/.claude` for a memory commit reset the shell cwd and mis-targeted one build
(empty `MOD_INSTALL_PATH`); builds/git made cwd-independent thereafter (absolute
paths, `git -C`). A Steam-Cloud-conflict native crash (unrelated) blocked the first
in-game run until Steam Cloud was disabled globally.

**Verified in-game (1.2.1.4, fake-ID 9999997):** clean sandbox compile
(`safetyCheck=True`, 0 `CompileFailed`); per-skin rows with distinct gradient icons;
active pet counted (Terrier 8 not 7); Level/Value `—`; Pets filter category;
spoiler states correct; normal rows unaffected by the material swap. **Not
separately re-verified:** the collected-ledger persistence round-trip (random-skin
egg hatching made it impractical to test on demand) — it rides the Iter-20-proven
`WriteCharacter`/`PossessionStore` mechanism. **Follow-ups logged during this iter:**
Iter-22 (row-hover tooltips), Iter-23 (rebound toggle key ignored — F1 always
opens), Iter-24 (scrollable filter pane).

**Iter-16.2 (critter collection) — DONE (2026-06-21, branch `iter-16-2`).** A pure
Iter-7.1-style bake-filter relaxation: `ItemCatalog.Bake` Loop 1's blanket
`ObjectType.Critter` exclusion became an icon-guarded keep (mirroring the NonUsable
line below it), so the net-catchable critters flow through every existing mechanism
(name/icon/rarity/value, discovery hook, Iter-20 possession scan, row rendering). A
new `Critters` (`Krabbeltiere`) filter category (enum + `All[]` + `ItemCategories.Of`
mapping + one yaml loc line) classifies them. Level/Value/Discovery/Possession needed
**zero new code** — the natural Loop-1 path gives Level `—` (critters carry no
`LevelCD`) and Value when real, and caught critters are ordinary inventory items the
Iter-10/20/21 machinery already handles. Catalog 10885 → **10910**.

**The decompile probe was wrong — a THIRD time on critters** (cf. the earlier
"KilledEnemiesBuffer-only" miss, and Iter-16.1's pet guess). It predicted "~15
catchable, ObjectIDs 9800–9819 with gaps (9803–9807 empty, 9813 unused), ambient
critters runtime-only / never DB objects." A throwaway in-game probe (enumerate every
static `ObjectType.Critter(var0)` with name + icon) found **25**, all with inventory
icons: the full **20** at 9800–9819 (no gaps; 9803–9807 and 9813 are all real, e.g.
`Sonnen-Zange`) **plus 5 Fireflies / Glowbugs** at **3500–3504** (`YellowFirefly`…
`PurpleFirefly`, German `Glimmkäfer`) the probe missed entirely (different ID range).
A decompile detour — Fireflies carry `FireflyCD`, not the `CritterCD` the bug net's
`TryCatchAnyCritters` checks — was a **red herring**: ground truth from the player
settled it (they already had Glimmkäfer **in chests** = obtainable + discovery-tracked,
and confirmed Glimmkäfer **are** bug-net-catchable via a firefly-specific path). So all
25 are legitimate, discovery-trackable, no permanent-`???` ghost rows — the predicate
needed no tightening; `ObjectType.Critter` + icon-guard admits exactly the right set.
The lesson is the project's standing one: **in-game ground truth beats decompile
inference**, especially on critters, where it has now been wrong three times.

Verified in-game (1.2.1.4, fake-ID dev build 9999997): clean sandbox compile
(`safetyCheck=True`, 0 `CompileFailed`), `ItemCatalog baked: 10910 items`,
`Krabbeltiere` category between Pets and Other, ~25 critter rows, Glimmkäfer rows
discovered with owned counts from chests (discovery + possession + loc in one), no
ghost rows. **Not separately stressed:** the Phase-3 pool-leak multi-open (the change
is additive — no row-pool/render touch). The generated loc assets under
`Localization/Generated/` are gitignored build artifacts (regenerated from the yaml
each build), so only the `localization.yaml` line is committed.

**Iter-23 (rebound toggle key ignored — F1 always opens) — DONE (2026-06-21,
branch `iter-23`).** A one-term hotkey-poll fix. `ItemChecklistMod.Update` toggled the
window on `rewiredFired || rawFired`, where `rewiredFired =
rewiredPlayer.GetButtonDown(ToggleActionName)` is the **rebindable** Rewired action
(registered by CoreLib `ControlMappingModule.AddKeyboardBind`, default F1, remappable in
the game's input settings) and `rawFired = Input.GetKeyDown(KeyCode.F1)` was a raw,
hardcoded F1 read. A code comment called the raw read a "diagnostic fallback", but it was
never actually gated to diagnostic-only — so it sat as a co-equal OR term and kept F1
opening the window **even after the player rebound the toggle** (the new key worked via
`rewiredFired`, but old F1 lived on via `rawFired`, so the rebind appeared not to take).
Fix: dropped the `rawFired` term entirely — the Rewired path already covers the default
F1 binding (the registered keybind's default *is* F1), so a fresh install is unaffected
while a rebind now fully takes. Net: removed two locals + one OR operand, leaving
`if (rewiredPlayer != null && rewiredPlayer.GetButtonDown(ToggleActionName))`. Pure
behavioural C# (no prefab/art touch); `using UnityEngine` stays (Debug/Time/Object), no
dead import. Build-verified (clean Editor compile, 0 `error CS`), sandbox-verified
in-game (1.2.1.4, mod.io build: `Successfully compiled ItemChecklist safetyCheck=True`,
0 `CompileFailed`; `[ItemChecklist] Hotkey — opening/closing UI` logs fire), and
behaviourally confirmed by the user (default F1 opens; after rebinding the toggle the
new key opens and F1 no longer does). The branch was rebased onto an intervening main
doc commit (`f350ea6`) before the ff-merge — linear history, no squash.

**Iter-24 (scrollable + collapsible filter popup) — DONE (2026-06-22, branch
`iter-24`).** The Filter popup rendered every section expanded at once and, after
Pets (Iter-16.1) + Critters (Iter-16.2) grew the Category list to ~29 rows, overflowed
the viewport. Brainstorming re-scoped the roadmap's scroll-only framing into a **two-layer
A+C design**: **(A) scroll** the popup, capped to a few rows; **(C) collapse** sections to
shorten it. Both layers live in the shared `PopupWidget` base / `Dropdown` skeleton so any
variant inherits them; collapse is Filter-specific (Sort has no sections). Design spec
retained at `docs/specs/2026-06-21-iter-24-scrollable-filter-popup-design.md`.

**Scroll (A) — manual translate ("Weg 2"), NOT CK's `UIScrollWindow`.** Two reasons the
main-list machinery was rejected: (1) `UIScrollWindow.Awake` permanently self-disables if
its serialized `scrollable` doesn't resolve to an `IScrollable` on the same GO — and the
`Dropdown` skeleton is deliberately component-less (Iter-18), so a skeleton `UIScrollWindow`
would self-disable; (2) the popup has ≤~29 *real* row GOs (no virtualization needed). So
`PopupWidget` caps the panel (`MaxVisibleRows` × `rowSpacing`; **base default 6 rows**,
per-variant overridable), clips with a popup-local `SpriteMask`, and scrolls by translating
`rowContainer`. The mask GO **and** the hand-rolled scrollbar (track + draggable
`PopupScrollHandle`) live in the `Dropdown` skeleton and are **runtime-discovered**
(`GetComponentInChildren`) — no serialized cross-prefab refs (the Iter-13 runtime-wire rule;
a first attempt used a stripped-stub `scrollMask` ref and was replaced). Sort inherits the
chrome but never wires it; **Sort was made scroll-ready** anyway (its `RowTemplate` baked to
`maskInteraction=1` + the band), so it auto-scrolls if it ever exceeds 6 modes.

**Hard-won scroll points (each caught in-game, none by the Editor compile):**
- **Sorting band 56..63, above the window mask (40..55).** The popup rows are lifted into a
  band the window `ContentsMask` does *not* cover (else it would clip the popup's top, which
  sits above the list region), with their own popup mask clipping them. PugText `orderInLayer`
  is freely editable (the footer-at-50 precedent), so labels were pulled from 9999 into the
  band. **Separator regression:** the row-BG at the mask's *back* order (56) fell on the range
  boundary → invisible; fix = lower `m_BackSortingOrder` to 55 so 56 is comfortably inside.
- **`maskInteraction=1` + no active mask = invisible.** So the mask stays active whenever the
  popup is open (sized to the cap; clips only on overflow, shows everything when it fits) —
  *not* gated on `_scrollActive`. Only the scrollbar/handle is overflow-gated.
- **Two `rowContainer` positioning modes.** `AutoSizePopup` moves the Popup GO itself to
  top-align the capped BG, so the rows (its children) need `RowTopY = viewportH/2 −
  rowSpacing/2` in that moved frame — not the centred formula (which stays for the no-cap case).
- **gap-A (click-outside).** The old `LateUpdate` closed on *any* mouse-down, so an inside
  click (checkbox, and now section-header / handle-drag) wrongly closed the popup. Fixed with
  a bounds check via `Manager.camera.uiCamera.ScreenToWorldPoint(Input.mousePosition)` (the UI
  camera is orthographic → world X/Y is z-independent; sandbox-safe, verified). Same helper
  powers wheel ownership.
- **gap-F (wheel ownership).** CK's `UIScrollWindow.UpdateScroll` reads the wheel
  independently, so wheeling over the popup also scrolled the main list. A Harmony **prefix on
  `UIScrollWindow.UpdateScroll`** returns false while an open popup owns the wheel
  (`PopupWidget.OpenPopupCapturesWheel()`, computed fresh per-frame). Harmony runs in trusted
  `0Harmony.dll` → sandbox-safe; only the main list is affected (the popup uses no UIScrollWindow).
- **x-alignment relocate.** The Filter variant shifted its `Display` −0.35 (a window-layout
  decision wrongly living on a child) but not the popup → 0.35 misalignment, exposed once the
  row backgrounds rendered. Cleaned up properly: both child x-overrides removed (Filter now
  structurally == Sort), the offset moved to the **window instance position** (−0.35), where
  layout belongs.

**Collapse (C).** Section headers became clickable (`SectionHeaderButton : ClickButton`,
3D collider + caret glyph on the `headerTemplate`). A **`static HashSet<string>` closed-set**
keyed on the **stable loc term** (Task 5 de-resolved `Member.section` from `Loc.T(...)` to the
raw term, rendered via `Loc.T` in `FilterWidget`) — so collapse survives a language change.
**Multi-open, default all-open.** `RebuildList` always renders a section's header (binding its
toggle + caret state) but skips the member rows of collapsed sections; `AutoSizePopup` then
operates on the reduced visible count, so collapsing enough deactivates the scrollbar. The
section carets shift X dynamically (a post-`AutoSizePopup` pass): clear of the scrollbar when
it shows (`2.65`), at the panel edge when it hides (`2.9`).

**Verified in-game (1.2.1.4, fake-ID 9999997):** clean sandbox compile (`safetyCheck=True`,
0 `CompileFailed`) across all 12 commits, each build+in-game-gated. Filter clips to 6 rows with
separators, scrolls via wheel + draggable handle, click-outside-only closes, wheel doesn't leak
to the main list, sections collapse/expand independently (default open, caret tracks the
scrollbar), Sort unchanged. Caught + logged during the iter: **Iter-25** (small-font umlaut
rendering looks malformed). The corrected process lesson, surfaced by the user mid-iter:
**do low-risk edits first** (lock in certain progress; isolate the uncertain edit last and
alone) — not the risky one first.

**Iter-25 (small-font umlaut rendering) — DONE (2026-06-23, branch `iter-25`).**
**Re-scoped twice by measurement.** The roadmap framed it as "the small font renders
ä/ö/ü malformed; fix the glyphs or switch font face". Diagnosis (a long evidence chain,
each step a build) inverted and then dissolved that framing:

**Root cause.** The chrome labels (Sort/Filter dropdowns, header, footer) render in
`fontFace = thinTiny` (`16777344`); item rows use `thinSmall` (`16777232`) and were always
fine — so the user's correction flipped the initial guess. Each `FontFace` maps to a
separate `PugFont`/atlas in the `rrs*` family (`thinTiny→rrs5` 256×40 **114 glyphs**,
`thinSmall→rrsthin8` 257×144 331, …). **`rrs5` is CK's reduced digits-only face** —
ASCII + a few symbols + ~19 Eastern-European letters, but **no German umlauts** (the
2 visible orphans at index 97/98 turned out to be unmapped `Ç`/`ç` images). For a missing
`ö`, `PugFont.GetGlyphData` runs its fallback chain (`button → thinTiny → chinese →
japanese → korean`) and finds it in the **chinese** font — rendered in CJK metric, hence
"deformed", no `?`, no warning. (Empirically confirmed via the `iter-25 FALLBACK` probe.)
CK itself never uses `thinTiny` for prose — only damage/score numbers.

**Mechanism (proven incrementally, each a build + in-game test).** A runtime glyph
override: `Manager.text.thinTiny.codePoints[c] = idx` + extend `glyphData` + set
`glyphData[idx].volatileSprite`. The first `GetGlyphData` branch wins before any fallback.
Proven first with `codePoints['ö'] = X-glyph index` ("Gewöhnlich"→"GewXhnlich"). Sandbox
allows it (`safetyCheck=True`). Non-ASCII char literals are encoding-unsafe in the Roslyn
sandbox (de-DE/InvariantCulture history) → use `(char)246`, not `'ö'`.

**Glyph pipeline.** The user hand-drew a **full accented set** in Pixaki
(`sources/thinTiny_full.pixaki`), as a 4-layer doc mirroring the iter-25 debug overlay
(Background / charDims / Rects / Atlas + a thinSmall reference layer). Extraction
(`sources/glyph-templates/pixaki_to_glyphs.py`, a reproducibility tool): **Atlas layer =
the sprite**, **Rects layer = the advance width** (the user drew exact glyph rects — full
`charDims` height 10, glyph-specific width), **thinSmall arrangement = the char** (per
codepoint cell). **85 new glyphs** (drawn + missing from thinTiny): full Western-European
+ partial Eastern-European/Cyrillic/typography. Shipped as a single-sprite bundle sheet
(`Art/thinTiny_glyphs.png`, `textureType:8 spriteMode:1` — the ModBuilder sprite-meta
trap); the runtime cuts per-glyph sprites with `Sprite.Create(sheet.texture, …)`.

**The final bug — sprite convention.** First in-game attempt rendered the glyphs shifted
**up-right**. Cause: a naive `pivot = (0,0)`. CK's `PugFont.InitCodePoints` uses a
**centered** pivot plus an outline-padding `rect2` (`y+1, h-1, x-1, w+2`). Replicating
that convention *exactly* fixed it — the project's standing lesson: mirror CK's working
internals, don't approximate. Verified in-game (1.2.1.4, fake-ID 9999997): clean sandbox
compile, `inserted 85 accented glyphs into thinTiny`, "Gewöhnlich"/"Ungewöhnlich"/"Legendär"
render correct umlauts at thinTiny size. **The `.pixaki` format was reverse-engineered**
along the way (`docs/research/pixaki-format.md`). Font architecture: the
`reference-ck-pugfont-architecture` memory.

**Iter-22 (row-hover tooltips) — DONE (2026-06-24, branch `iter-22`).** Hovering a
checklist row now shows CK's native item tooltip (name / description / stats) plus a
slot-hover highlight. Design spec: `docs/specs/2026-06-23-iter-22-row-hover-tooltips-design.md`.

**Feasibility (measured up front).** CK's tooltip is **selection-driven, not
entity-driven**: `UIMouse.UpdateHoverText` reads `Manager.ui.currentSelectedUIElement`
and calls its virtual `UIelement.GetHoverTitle/GetHoverDescription/GetHoverStats/
GetContainedObject`; it needs **no live ECS entity**, only a selected `UIelement` that
returns a `ContainedObjectsBuffer` carrying an arbitrary `ObjectDataCD(objectID,
variation)`. `UIMouse` re-derives the selection every frame from a 3D `Physics.Raycast`
against `UILayerMask` — which is the *engine* of the chosen approach, not an obstacle.
Working precedent: ItemBrowser's `ItemBrowserSlot : SlotUIBase` does exactly this for
catalog items not in any inventory. A throwaway **spike** (committed, then removed in a
later task) de-risked the one unknown — a code-instantiated `SlotUIBase` helper
(Awake overridden to skip its `animator` NRE) returns non-empty title/desc/stats: a coin
gave title+desc (no stats, correct), a Copper Sword gave `statLines=2`. No prefab-helper
fallback needed.

**Architecture.** Each `ItemRow` (already a `UIelement`) carries a 3D `BoxCollider` so
`UIMouse` hover-selects it (the collider sits on the row root, where the `UIelement`
lives, so `UIMouse`'s `GetComponent<UIelement>()` resolves to it) and overrides the four
hover virtuals, delegating to **one shared `TooltipSlot : SlotUIBase`** (owned + injected
by `ItemChecklistContent`) fed the row's `(objectId, ckVariation)`. **Pet rule:** the
`skinIndex` Bind param is a skin selector, not a CK variation — pets sit at CK variation
0, so the helper uses `ckVariation = isPetSkin ? 0 : skinIndex` (else CK finds the wrong
object). Verified in-game on `Eulux (Skin 3)`: a known species with an uncollected skin
shows the species tooltip.

**Spoiler model (settled with the user mid-iter).** Discovered rows show the full
tooltip. Undiscovered (`???`) rows **highlight** on hover (the highlight reveals nothing)
but show only a **minimal `??? - not yet discovered` placeholder** (title + a one-line
hint), never the real item — two new ASCII-safe loc terms (en/de; the yaml's no-em-dash
rule applies, so `-` not `—`). `_nameKnown` therefore gates only the *tooltip content*,
not the collider/highlight.

**Highlight — prefab, not runtime (a deliberate pivot via user feedback).** First built
runtime (collider + a background-colour tint); the tint looked like "a weird frame", and
the user wanted the inventory-slot look. CK slots highlight via `SlotUIBase.hoverBorder`
showing `craftingUITheme.slotHoverSprite`. The user then chose to author the highlight in
the **prefab** for full Editor control, and to keep their own sprite (no runtime sprite
swap). Final split: the user authored a `HoverHighlight` SpriteRenderer child + the
`BoxCollider` in the Editor (wired to new `highlight`/`hoverCollider` serialized fields);
**code only toggles visibility**. One prefab-wiring bug caught before the build by
verification: the highlight's Sorting Layer was **Default** instead of **GUI** (so it
would render behind the row content / out of the mask's 40..55 band) — corrected to GUI.
The highlight is driven **per-frame in `LateUpdate`** (`highlight = selected &&
PointerInViewport`), not the `OnSelected`/`OnDeselected` one-shots, so it clears the
instant the cursor leaves the row.

**Two hard-won hover bugs (both from in-game screenshots, neither by the compile):**
- **Popup "leak" — a non-issue.** Suspected that the full-width row collider under an open
  Sort/Filter popup would leak a tooltip; built a `PopupWidget.PointerOverOpenPopup` guard
  — then in-game proved there is **no leak** (the popup's own elements take the selection),
  so the guard was reverted entirely.
- **Hover outside the list — the real bug.** Row colliders are full-row and extend **past
  the window `ContentsMask`** (the +4 buffer rows + the bottom row clipped by the mask);
  a SpriteMask clips the *sprite*, not the *collider*, so a cursor in the header/footer/
  margin still hover-selected the clipped row behind it. Fix:
  `ItemChecklistContent.PointerInViewport()` — a static cursor-vs-ContentsMask world-bounds
  check (mirrors `PopupWidget.PointerOverPanel`); the four overrides + the `LateUpdate`
  highlight gate on it.

**Editor convenience.** The per-row `ContentMask` (an authoring aid) is force-disabled at
runtime in `EnsurePool`, so it can be left enabled in the prefab without affecting the
game.

**Process note.** Realised inline (not subagent-driven) per the mod's standing rule that
the in-game calibration loop needs the live CrossOver window + build lock; the design
went through several user-driven pivots (runtime→prefab highlight, highlight-on-`???`,
minimal `???` tooltip, viewport gate) caught by in-game screenshots. Verified in-game
(1.2.1.4, fake-ID 9999997) across all commits: `safetyCheck=True`, 0 `CompileFailed`,
0 NRE; discovered + `???` tooltips, highlight on all rows, pet-skin tooltips, no
outside-list hover, no popup leak. **Not separately stress-tested:** the recycled-row
rebind under rapid scroll and the search-focus interplay (both ride existing machinery
and were exercised incidentally during testing).

**Iter-16.4 (discovery filter/count ignores pet skins) — DONE (2026-06-24, branch
`iter-16-4`).** A coherence fix completing the Iter-16.1 per-skin work: the per-skin pet
rows rendered their collected **tick** correctly (`ItemRow.showDetails`), but three
**aggregate** sites still recomputed discovery from the raw, skin-blind `DiscoveredState`
— CK force-zeroes pet variation in `SaveManager.SetObjectAsDiscovered`, so a collected
`(petId, skinIndex > 0)` never lands in `DiscoveredState`. Symptoms (user-reported
2026-06-23): the **Discovered** filter HID collected skins (and **Undiscovered** showed
them); `DiscoveredInView` ("· N shown") under-counted; and the `N / M` counter used
`DiscoveredState.Count` (each species once) against a catalog `M` that counts every skin
row, so **100% was unreachable**.

**Fix = the Iter-21 chokepoint pattern's discovery twin.** Iter-21 funnelled both
possession read-sites through one `ItemChecklistMod.OwnedCount`; this adds the discovery
counterpart:
- `ItemChecklistMod.IsCollected(objectId, variation)` — pet-skin rows → `PetCollection`,
  else `DiscoveredState`. This **is** the `ItemRow.showDetails` flag (the checkbox tick),
  so the filter / in-view count / numerator can never drift from the per-row tick.
- `ItemChecklistMod.CollectedCatalogCount()` — the `N` numerator, tallied over the catalog
  through `IsCollected` (replaces `DiscoveredState.Count`).
- Routed through them: `ItemListViewModel.Recompute` (Discovery filter + `DiscoveredInView`),
  the window footer (`FormatTitle`), the HUD (`ItemChecklistHud.Refresh`), and the
  `ItemChecklistContent` `showDetails` branch — one source of truth for all four.
- Because collecting a new skin fires **no** `DiscoveredState.Changed` (CK has no per-skin
  discovery event), the always-on HUD is nudged after the 3s possession scan when the
  collected tally changes (change-gated so `PugText.Render` doesn't churn); the modal
  window self-heals on its own (it rescans + `RenderStatus` on every open).

**Behaviour-identical for non-pet rows** by construction (`IsCollected == IsDiscovered`
there), so the ~10,885 normal/critter rows are untouched — only pet skins change. The
now-dead injected `DiscoveredState` field + ctor-param on `ItemListViewModel` were removed
(both `new ItemListViewModel(...)` sites updated). Pure behavioural C# (+84/−21 across 7
files); no prefab/art touch. Verified in-game (1.2.1.4, fake-ID 9999997): clean Editor
build (0 `error CS`, 0 `files:[]`), `Successfully compiled ItemChecklist safetyCheck=True`,
0 `CompileFailed`, 0 NRE, `ItemCatalog baked: 10910 items`; the user confirmed all six
behaviours — Discovered now shows collected skins, Undiscovered hides them, `N / M` (footer
+ HUD) counts skins toward 100%, "· N shown" counts skins, normal items/critters unchanged,
and the HUD updates live on collecting a skin.

**Iter-16.3 (cattle / farm-livestock collection) — DONE (2026-06-25, branch
`iter-16-3`).** The third creature family after pets (16.1) and critters (16.2): farm
livestock now gets a checklist row. The iteration **re-scoped twice** — once from the
roadmap's premise, once from its own spec — both times because in-game measurement beat
the decompile/decision-on-paper. Final shape: cattle behave like **critters** (admitted
to the catalog + a new category, flowing through CK's native discovery), with one
species row per animal and babies folded in. Design spec (the now-superseded ledger
design): `docs/specs/2026-06-24-iter-16-3-cattle-collection-design.md`.

**The CK cattle model (measured, not guessed).** Three in-game verification gates were
run up front via throwaway bake/scan probes (committed-then-reverted; the
commit-before-build discipline was forced by a build that silently reverts *uncommitted*
worktree edits):
- **Gate (a) — roster + structural baby-fold.** The marker is the empty
  `struct CattleCD` (assigned by `CattleConverter`), so `PugDatabase.HasComponent
  <CattleCD>` self-determines the set: **6 adults** (Cow=1300/Goat=1302/RolyPoly=1303/
  Turtle=1307/Dodo=1309/Camel=1311) + **6 babies** (1304/05/06/08/10/12) — correcting the
  roadmap's "only 1300/1302/1303". The adult→baby link is **structural**:
  `BreedStateCD.babyType` is statically authored on the adult (`BreedStateAuthoring`), so
  a baby is any `CattleCD` id that is some adult's `babyType` — `CattleRegistry` builds
  that fold at bake, **no name parsing**. Babies are folded into the adult (skip their
  row). Catalog **10910 → 10916** (+6 species).
- **Gate (b) — discovery.** `CanBeDiscoveredCD` is **absent** on every cattle prefab → no
  *proximity* discovery. This drove the spec toward a mod-owned ever-owned ledger. **But
  the probe later proved CK DOES discover cattle** via the inventory-pickup path
  (`DetectUndiscoveredObjectsInInventory` → `SetObjectAsDiscovered`) — **per
  `(objectID, variation)`**, where the variation is the animal's **colour variant**. The
  live hook logged `SetObjectAsDiscovered(Cow=1300, var=2)` and `(Goat=1302, var=0/1)`;
  the bake probe showed `1300@var0=False, 1302@var0=True, 1303@var0=True`. So a species
  discovered only at a non-0 variation (a colour the player owns) reads as undiscovered
  on its var-0 catalog row — the Iter-21 **H1 / Iter-17 variation-keyed-discovery** case,
  here triggered by cattle colour variants.
- **Gate (c) — possession spatial test.** A live cattle is a `Creature` ECS entity with
  `ObjectDataCD`+`LocalTransform`; the Iter-20 scan sees it but the furniture gate drops
  it. The probe confirmed: penned Cow/Goat/RolyPoly near the base → `withinAnchor=True`;
  a wild RolyPoly at (205,−107) → `withinAnchor=False`. A caged animal appears in a
  container buffer as the **animal's** ObjectID (`CAGED id=1303`), confirming the
  "same ID + auxData" model.

**The ledger was built, then deliberately removed.** Acting on gate (b)'s
`canBeDiscovered=False`, the iteration first built a `CattleCollection` ever-owned ledger
(mirroring `PetCollection`) so a penned/owned cattle counts as collected. This worked
in-game (the owned Cow showed its name), **but it masked the variation-keyed-discovery
symptom rather than fixing it** — and it surfaced a one-frame coherence bug: routing
`showDetails` through the ledger while leaving `nameKnown` on raw var-0 discovery made an
owned-but-var≠0-discovered Cow render `???` *with* details + an owned count (the Iter-21
spoiler-leak). After weighing it with the user against the **pet parallel** (a sub-variant
collectible axis the catalog collapses — pets solved it per-skin in 16.1), the call was:
the proper fix is **per-variation splitting (Iter-17)**, which for cattle can use CK's
**native** per-variation discovery directly (unlike pets, which had none and needed the
ledger). So the ledger + all cattle-specific collected routing was **reverted**, leaving:

**Final design (option A — cattle = critter-like).** Cattle flow through CK's native
`(objectID, var0)` discovery exactly like critters/normal items. Goat/RolyPoly
(var-0-discovered) show their names + owned counts; the Cow (discovered only at var 2)
shows a **coherent `???`** — no name, no details, no count (owned-count spoiler-gated on
discovery as usual). The only cattle-specific code is: the bake relaxation + baby-fold
(`CattleRegistry`), the `Cattle`/`Nutztiere` filter category (keyed on `Entry.IsCattle`
since cattle are `Creature`-typed and fall to `Other` otherwise), and a `PossessionScanner`
`CattleCD` branch (live penned cattle near a clustered anchor + caged cattle in buffers,
both credited to the adult; wild excluded by the anchor gate). No ledger, no persistence,
no chokepoint routing. Level/Value `—` (no `LevelCD`/sell value), free via existing logic.

**Deferred to Iter-17 (now with a concrete instance).** Per-variation/colour-variant
tracking — one row per cattle colour, using the native per-variation discovery the gate-b
probe revealed. The shipped Iter-16.3 limitation: a cattle you own but have only
discovered at a non-0 variation shows `???` until then. This is the honest interim, not a
bug. Verified in-game (1.2.1.4, fake-ID 9999997): clean sandbox compile
(`safetyCheck=True`, 0 `CompileFailed`, 0 NRE), `ItemCatalog baked: 10916 items`, the
`Nutztiere` category shows the 6 species (no baby rows), and the user confirmed the
coherent display (named+counted vs. `???`). The standing lesson reinforced a fourth time:
**measure CK creature mechanics in-game; the decompile and the on-paper decision were both
overturned by what the probe logged.**

**Iter-17 (per-variation tracking) — DONE (2026-06-26, branch `iter-17`).** The long-
deferred "colour/skin/state variants get their own row" item. Brainstormed into two
**buckets**, both reshaped by in-game measurement (the design spec
`docs/specs/2026-06-25-iter-17-per-variation-tracking-design.md` was overtaken at almost
every step). Final catalog **10916 → 11119**.

**The discriminator (settled by a Step-1 throwaway sweep).** `PugDatabase.objectsByType`
is keyed on `(objectID, variation)` (`ObjectDataCD.Equals/GetHashCode` include variation).
A discovered `(id, v)` **in** that dict is **DB-authored** (Bucket 1); **absent** = runtime-
assigned (Bucket 2). The sweep overturned the decompile's "Bucket 1 ≈ empty" guess:
**600 DB-authored non-0 keys / 204 objectIds**, ~395 PlaceablePrefab — mixed cosmetic
(paintable decor) + state-junk (chest open/closed, seed growth). And the only Bucket-2
runtime variations are cooked food (Loop 2, already handled) + cattle colours — so the
**generic Bucket-2 loop was measured empty and not built**.

**Bucket 2 — cattle, the value (pet-skin parity).** The catch: CK exposes **no** colour-
count API (no table, no field; `GetObjectInfo(id, v)` falls back to var0 for every v → no
"exists" signal; ItemBrowser never enumerates per-colour). First built **discover-to-reveal**
(rows appear as colours are found; a placeholder keyed on `IsDiscoveredAnyVariation` for
zero-colour species — fixing the Iter-16.3 `???`-on-non-0-variation trap structurally). Then
a probe found the enumeration anyway: each cattle prefab's `Pug.Properties.ObjectPropertiesCD`
carries a **`PossibleChildVariation[]`** list under property id **239678920** (the breeding-
outcome set `Pug.Other.GetChildVariation` rolls from) — read via
`PugDatabase.TryGetComponent<ObjectPropertiesCD>` + `props.TryGetList(239678920, out
NativeArray<BreedStateCD.PossibleChildVariation>, Allocator.Temp)`, **sandbox-safe**
(`safetyCheck=True`). In-game: **every species = {0,1,2,3,4}** (5 colours), and all wild-
**caught** colours fall within it (the user's "caught not bred" worry — the list IS the
species palette, not breeding-only). So discover-to-reveal was **reworked into the pet
model**: `CattleRegistry.ColoursOf` reads the palette; Loop 4 emits ALL 5 colour slots per
species always; `nameKnown` is species-gated (`IsDiscoveredAnyVariation` — all slots named
once any colour is found, `???` otherwise), the per-colour collected tick stays
`IsDiscovered(id, colour)`. The placeholder + a live-re-bake both dropped (fixed slots; CK's
native per-colour discovery fires `Changed` → existing refresh). 6 species × 5 = **30 cattle
rows**. Per-colour possession: the scanner reads a live penned/caged cattle's variation and
credits `colourCounts[(adult, colour)]` (live, like pet skins).

**Bucket 1 — paintable variants.** Lift the Loop-1 `variation != 0` guard, keep var≠0 ONLY
when `PaintableObjectCD` is present (curated: cosmetic colours in, chest/seed state-junk out)
→ **+179 rows**. **Reveal-all** generalised across colour families via a new
`Entry.IsColourVariant` flag (cattle + paintable base/variants): the item name shows on all
slots once any form is known. The 14 **paint-colour names** come from the paintbrushes
(`PaintBrushRed=70 … Teal=83`, each `PaintToolCD.paintIndex` = the variation it applies); the
brush's **enum name** minus the `PaintBrush` prefix is the English colour, localized via 14
own `ItemChecklist-PaintColor` terms (DE "Rot", …) — so "(Farbe 3)" renders "(Rot)". Per-
colour possession for placed paintable furniture mirrors the cattle path (the entity carries
its paint colour in variation); tile floors/walls aren't entities → no count → "—"; the base
(var0) row keeps its objectID-total.

**Process.** Five throwaway in-game probes (variation sweep, breeding-list, missing-row,
each committed-then-reverted per the build-reverts-uncommitted-edits discipline) drove every
decision; the user's screenshots + questions repeatedly redirected the design (discover-to-
reveal → pet model; paint-colours kept as a split; enum names over parsed brush names). The
recurring lesson held a **fifth/sixth time**: the decompile and on-paper plan were overturned
by what the probe logged. Verified in-game (1.2.1.4, fake-ID 9999997): clean sandbox compile
(0 `CompileFailed`/NRE), `baked: 11119 items`, `paint colours mapped: 14`; cattle show 5
named/`???` colour slots with per-colour counts, paintable items show localized paint-colour
slots, base rows counted, reveal-all confirmed. Known limits: a non-paintable DB design
variant (e.g. Stalagmite var1) is dropped by the `PaintableObjectCD` filter; per-colour
furniture possession is live-only and unavailable for tile floors/walls. The Iter-20 search-
focus race recurred (pre-existing, not this iter).

**Iter-27 (possession-scan perf — in-base stutter) — DONE (2026-06-27, branch
`perf-possession-scan`).** A user-reported intermittent stutter "in the base, every few
seconds" — correctly suspected to be the 3s possession refresh. **Measured before
guessing** (systematic-debugging Phase 1): a throwaway PERF probe in `PossessionScanner.Scan`
logged per-scan wall-time split into four phases (world/setup/loop/build) plus entity,
near-anchor and anchor counts, then in-base play (278 scans) was analysed.

**Diagnosis (empirical).** In steady-state base scans (~1300 loaded-world entities, ~840
passing the anchor gate, 44 clustered anchors): `total` median 3.4ms but **p99 16.4ms,
MAX 21.5ms**, and the **`loop` phase dominated** (avg 3.49ms of 4.7ms total; `world` 0.26ms,
`setup` 0.12ms, `build` 0.83ms). The spike mechanism: the loop read `ObjectDataCD` +
`LocalTransform` via a **per-entity `em.GetComponentData<T>(e)`** (a random chunk+index
lookup) for **every** one of the ~1300 entities, on the main thread, synchronously in one
`Update` tick. At 60fps the 16.7ms frame budget is blown by a single ~21ms scan → exactly
one dropped frame every 3s = the reported periodic stutter. **CPU-bound, not GC** — the
managed-allocation `build` phase stayed small and the spikes tracked `loop`/`ents`, not a
periodic GC signature.

**Fix — F1 only.** Copy the two universally-read components in bulk via
`objQuery.ToComponentDataArray<ObjectDataCD>` + `ToComponentDataArray<LocalTransform>`
(chunk-sequential memcpy) and index `ods[i]`/`xforms[i]` in the loop. The three arrays
(`ents[]`/`ods[]`/`xforms[]`) are **index-aligned** — same query, captured back-to-back
with no structural change between. Per-entity `em` access (`HasComponent`/`GetBuffer`/
`PetOwnerCD`) remains only for the player + the gated near-anchor minority, not all N. This
trades ~2×1300 random chunk lookups per scan for two bulk copies + sequential array reads.

**Measured after** (same base, same activity): **MAX 21.5→9.6ms** (now comfortably under
the 16.7ms budget — no dropped frame), `loop` avg 3.49→1.82ms (**−48%**), `total` p90
8.1→3.4ms, p99 16.4→6.6ms; the variance collapsed (the random-chunk-walk that caused the
outliers is gone), and `ents`/`near` were unchanged (~1300/~800) — proving the win is the
**access pattern**, not a changed entity set, i.e. behaviour-neutral. Sandbox-clean
on CK 1.2.1.5 (`Successfully compiled ItemChecklist safetyCheck=True`; `ToComponentDataArray`
passes the Roslyn sandbox, as `ToEntityArray` already did).

**Rejected by measurement** (not built): **F3** caching `ResolveWorld` (`world` only 0.26ms
in steady state; the 3.7ms was a one-time world-load cold start, not the recurring stutter,
and a session-cached `World` ref risks staleness across world reloads), **F2** anchor
spatial-hash (only 44 anchors → the `WithinAnchor` inner loop is negligible; `setup` 0.12ms),
**F4** reusing the per-scan `Dictionary`/`HashSet` buffers vs GC (the `build`/alloc phase was
small and no GC-signature spikes appeared). The standing project lesson held again: the
single, evidence-backed change beat a bundle of plausible-but-unmeasured optimisations. Pure
behavioural C# (one query split + two loop-read swaps); no prefab/art touch.

**Iter-28 (possession scan — exclude world nature) — DONE (2026-06-28, branch `iter-28`).**
A *second*, distinct stutter beyond Iter-27 (the user was explicit: "Iter-27 fully solved
the symptom it described; these are new symptoms"): peaks that worsen the **longer** a
session runs and persist **away from base** — a time-accumulating, location-independent
signature, unlike Iter-27's in-base entity-loop spike.

**Root cause (measured, three escalating probes).** On-disk evidence with no build: the
character's `possession-<guid>.txt` ledger had grown to **5503 entries / 89 KB**, of which
**99 % were single placed objects with no contents** and **~90 % were world-spawned nature**
(Bush 5619 ×1102, Stalagmite 5610 ×988, WaterKelp 5613 ×922, TallGrass 5620 ×864, …).
Iter-20's "count the placed object itself" path counted wild nature near a clustered anchor
as owned, and the per-`(x,z)` ledger **remembered it forever** (`PruneStaleNear` only prunes
within 180 tiles). A throwaway PERF probe pinned the actual peak: **not the scan** — the
Iter-27 data already showed `BuildView` was ~0.8 ms even at 5503 — but the **autosave SAVE**:
`SaveManager.WriteCharacter` → `Serialize()` of the 89 KB ledger was a **12–37 ms main-thread
spike** every autosave, which also pushed CK's *host simulation* over its 55 ms frame budget
(`ServerUpdateFrequencyTracker` warnings: 626/1109 frames over budget). Time-accumulating
(ledger grows) and location-independent (autosave is time-based) — both symptoms explained.

**The discriminator is impossible — the hard lesson.** "Exclude wild nature, keep everything
else" needs to tell a wild Bush/Stalagmite from a placed Wall/Torch/Trophy/WayPoint. Three
in-game probe rounds proved **no object-level signal separates them**: `cat`/`stack`/`icon`
are uniformly true; `craft` collides (the PottedGoldenOrbBush 5589 is craftable décor, the
CavelingSpearmanTrophy/SlimeTrophy 5181/5088 are non-craftable décor); `tags` collide
(Stalagmite and WaterLily are tag-less, exactly like the WayPoint that must count); and the
entity-level candidates `DontDropSelfCD`/`DiggableCD`/`DestructibleObjectCD` collide too
(`DontDropSelfCD` is an `IEnableableComponent` → present on everything; Stalagmite ≡
CavelingFloorTile, GraveTree ≡ WayPoint ≡ Idol ≡ RuinsPiece on every probed column). CK
simply does not encode "world-generated vs player-placed" in object data. So
`PossessionClassifier.IsWorldNature` is the user-chosen answer: a **curated tag + ObjectID
blacklist** — nature tags (`Greenery`/`Destructible`/`CattleKelpFood`/`Ruins`, stable enum
ints 5/13/33/4) catch the bulk and future tagged nature; a short ID list
(Stalagmite/WaterLily/GraveTree/TallLandKelp/CavelingFloorTile/RuinsPiece) catches the
tag-less stragglers. Editable by design.

**The fix — gate + one-time eviction.** The blacklist gates **path #1 only** (the placed-
object `AddOne`); container contents (`AddBuffer`) and carried are untouched, so nature
actually STORED in a chest still counts (verified via a path-#2 probe that showed the user's
chest-stored Stalagmite/ores/armor/seeds were captured independently) and the
remember-from-afar feature is preserved. Because `PruneStaleNear`'s 180-tile window was far
too slow to clear the 5500-entry backlog (it only reached ~3866 after a minute of roaming,
so the save spike persisted), a **one-time `PruneByPredicate(IsWorldNature)` at the first
scan** (where `PugDatabase` is ready) evicts the whole pre-existing backlog at once. Result:
ledger **5503 → ~520 (89 KB → 9 KB)**, save spike gone, host-overrun warnings gone (0 vs 3),
verified **smooth at base** in-game (1.2.1.5).

**Process lesson — the runaway background grep.** For several diagnosis rounds a
decompile-ID `ugrep` I had launched (a per-ID loop over the 120-DLL decompile) **never
terminated** and ate CPU in the background through *every* in-game test, confounding the
"is it smooth?" signal — under CrossOver/Wine a sustained background grep competes directly
with the game. The user's key observation ("it was smooth before with the same mods") pinned
it: the only new variable since 1.0.2 was that grep, not the mod. Killing it + the small
ledger = smooth. **Kill stray background jobs before trusting any in-game perf measurement.**

**Deferred → Iter-29:** chunking/time-slicing the 3s scan so one tick doesn't do the full
~2000-entity pass (a ~9.6 ms scan on one frame every 3 s is a felt micro-hitch even "under
budget"). Not a regression (1.0.2 had the identical scan and was smooth), so it is a
nice-to-have smoothness pass, weighed against its real complexity (holding the entity
snapshot across frames + accumulating `liveKeys` before pruning). Pure behavioural C#
(`PossessionClassifier.IsWorldNature` + `PossessionLedger.PruneByPredicate` + a one-time
scan call); no prefab/art touch.

**Iter-30 (config-gated possession diagnostic log) — DONE (2026-06-29, branch `iter-30`).**
A permanent, default-OFF diagnostic so a recurring possession stutter (or wild nature leaking
past the Iter-28 blacklist in a new biome) can be captured **without** re-adding a throwaway
probe each time. Gated on `PossessionConfig.Diagnostics` — a second `API.Config` key beside
`anchorRadius`, read once at startup; toggle by editing `Possession-diagnostics.json` and
relaunching. When on it logs three things to `Player.log`: the per-scan timing split
(`world/setup/loop/build`) + ledger size; the per-save `serialize`/`write` timing
(`PossessionStore`); and **once per launch** a dump of every distinct counted placed object
with its tags + `IsWorldNature` verdict — a `nature=False` row for an obviously-wild object is
exactly the next blacklist gap to fill. Zero overhead when off (every timing read is a
`diag ? Time.realtimeSinceStartup : 0f` ternary; the object recorder early-returns on a dumped
flag). This tool drove the whole of Iter-31.

**Iter-31 (possession scope — anchor the base on workbenches) — DONE (2026-06-29, branch
`iter-30`).** Two *new* symptoms after Iter-28 (the user was explicit they were new, not an
incomplete fix): (a) a residual save-write hitch even with the small post-Iter-28 ledger, and
(b) "massive lag spikes outside the base". The Iter-30 diag + **parsing the real savegame
ledger** turned both into a single root cause — and the fix is the *true base detection*
Iter-20 had punted on.

**Save-write-skip (the cheap half).** `SaveManager.WriteCharacter` fires on every autosave, but
base storage rarely changes between two; `Serialize()` (~1 ms) is cheap while the
`API.ConfigFilesystem.Write` under Wine is the 5–13 ms main-thread cost. So `PossessionStore`
keeps a **64-bit FNV-1a content hash** of the last written text per character GUID and elides
the write (and the `DirectoryExists` probe) when the hash is unchanged. FNV-1a/64 deliberately:
**not** 32-bit `string.GetHashCode` (a 1/2³² collision would skip a needed save = data loss),
and **not** SHA/MD5 (cryptographic strength we don't need against our own data; heavier per
byte on a main-thread path; allocates a `byte[]`; and `System.Security.Cryptography` is exactly
the BCL surface the Roslyn sandbox locks down — FNV is pure value-type arithmetic, zero BCL
dependency). The first save per character still lands.

**The diag exposed why the skip barely fired (1 of 37 saves): the ledger churned** — the
`containers` count bounced 523→667→707→525 every save. Parsing the actual ledger
(`possession-<guid>.txt`, ASCII `x,z|id:count`) — **not inferring** — showed why: the
OreBoulders the user questioned sat **337–693 tiles from the Core base**, and ~90 of the 523
entries were **remote world content the player merely explored past**: Sunken-Sea nature
(GlowingCoral ×36, JellyFish ×14, KelpTree), abandoned-camp furniture (tent/mattress/campfire/
stool/table), a mechanical vault (electricity generator + seed extractor + farm seeds), ruins
pedestals, **and the loot inside world chests the player never opened** (GoldBar/ScarletBar/
AncientCoin/HunterBreastArmor/keys). I twice asserted the boulders were "within the 48-tile
base radius" from inference — both wrong; the user forced the ledger check that disproved it
(now the [[feedback_validate_against_savegame]] memory).

**Root cause + the semantic discriminator.** The scan anchored on **any `CraftingCD` station**,
and CK world structures *contain* functional stations — an abandoned camp has a campfire, a
vault a seed extractor — so two within 16 tiles passed the old ≥2-station cluster filter and
anchored the structure's loot chest + surrounding nature/boulders as "owned". A type/spatial
blacklist is hopeless here (you cannot blacklist `GoldBar` loot, and CK encodes no
world-vs-placed flag). The user's insight gave the clean discriminator: a base is **built
around a Workbench** — the first thing a player builds — and CK places none in world
structures. **Validated against the save before coding: 11 workbenches, all at the Core base;
0 in any remote cluster.** So anchors became **workbenches + the crafting stations standing
within a workbench's `AnchorRadius`** (the base's own furnaces/campfires/seed-extractors still
count, but the *same* station in a workbench-less camp does not). The link is workbench→station
only, never station→station, so the base can't chain out to a far structure; a single workbench
suffices, so the ≥2-cluster filter is gone (`ClusteredAnchors` deleted). `WorkbenchIds` is the
material-tier progression (Wooden/Copper/Tin/Iron/Scarlet/Octarine/Galaxite/Solarite), editable.

**Ledger v2 migration.** Pre-fix ledgers are polluted with the remote loot and `PruneStaleNear`
(180-tile window) never reaches it, so `PossessionLedger` gained a `#icl-ledger-v2` first-line
marker; `LoadFrom` discards any file lacking it once, and the base re-scans clean —
auto-migrating every player on update. **Near-base wild boulders stay blacklisted:** the
OreBoulder family (`2200-2207, 2209, 2218` + Amber/CrystalMeteor) is added to
`PossessionClassifier.WorldNatureIds` (Iter-28's twin) for deposits *within* the base radius;
MeadowTree (15500) is deliberately **not** blacklisted — a tree near base is plausibly placed
décor (kept), wild trees away are excluded structurally by the workbench gate.

**Measured after (against the savegame, per the lesson):** ledger **523 → 403, 0 remote**
(>100 tiles from Core); save-skip now dominates (`SKIPPED 9` vs `write 3`); scan **~1 ms outside
base** (no workbench → no anchors → nothing counted) and ~4–5 ms at base; host-overrun **4 vs
626** frames. **The "massive lag spike outside base" was confirmed (by isolation, after the
1.0.3 publish) to be *another mod*, not ItemChecklist:** disabling **Enemy Health Bars** (modId
4164578 — it renders one bar per enemy, scaling with the high enemy count while exploring) via
`state.json`'s `disabledMods` made a full session run with **no spikes**, while ItemChecklist's
scan is ~1 ms. The first-pass **"40× ComputeBuffer-GC leak" theory was a red herring** — those
`disposing of ComputeBuffer` warnings all fire at *process shutdown* (exit cleanup), not during
play, and the count was **unchanged (40)** with the mod disabled (the lesson is distilled in
`docs/gotchas.md § "GarbageCollector disposing of ComputeBuffer" is usually a SHUTDOWN artifact`).
Realised across five commits (diag, save-skip, ore blacklist, workbench anchor + ledger v2);
built/sandbox-verified on CK 1.2.1.5.

**Iter-32 (cooked dishes double-counted in discovery) — DONE (2026-07-12, branch
`iter-32`).** A user-reported bug: a cooked dish counted as **two** discovered items in the
`N / M` tally. **Root-caused by measurement, not the roadmap's guess** — and this iteration
is the textbook case for it: the user twice pushed back on a premature hypothesis, which
measurement then overturned each time.

**The bug.** `ItemCatalog.Bake` Loop 2 builds cooked-food permutations by iterating
ingredient pairs (`j >= i`) and, per pair, emitting three tier entries via `AddCookedEntry`:
`(baseFamily, V)`, `(rareVersion, V)`, `(epicVersion, V)` — all sharing the food variation
`V = GetFoodVariation(i1, i2)`. `accepted` is a `List` (not a set) and the final catalog is
built 1:1 from it, so a repeated `(objectID, variation)` duplicates the row; `Count =>
entries.Length` and `CollectedCatalogCount()` then count the dish twice, and discovering it
flips both rows (`N += 2`).

**Three hypotheses; the first two wrong.** (1) The roadmap guessed a discovery-side
double-mirror or `epicVersion == rareVersion`. A throwaway bake probe (committed-then-
reverted) grouping `accepted` by `PackKey` showed **858 duplicate keys, all cooked food,
each exactly ×2, all Rare-tier IDs (9550–9563)** — so it is catalog-side, not discovery-side
(a parallel `SaveManagerDiscoveryHook` probe confirmed CK fires `SetObjectAsDiscovered` per
clean `(objectID, variation)`). (2) But the Epic tier exists as its own ID range
(`CookedSoupEpic` 9575 … `CookedCakeEpic` 9589), so `epicVersion == rareVersion` was
implausible. (3) A second probe — `producedBy`: objectID → the `(branch, baseFamily)` calls
emitting it — nailed the real mechanism: `id=9551 by=[rare(bf=9501), base(bf=9551),
rare(bf=9551)]`. The **same baseFamily 9551** emits `9551` via BOTH its base and rare branch.

**Mechanism.** A **golden ingredient**'s `CookingIngredientCD.turnsIntoFood` points straight
at a **Rare** family ID (9551 `CookedPuddingRare`, not the Base 9501), so a Rare ID appears
as a `baseFamily` in Loop 2; and that Rare family's `CookedFoodCD.rareVersion`
**self-references** (9551→9551). The base branch and the rare branch therefore collide on
`(9551, V)` — same baseFamily ⇒ same food variation ⇒ a real duplicate. Tallies confirmed:
`epicEqRare=0`, `baseEqRare=11` (the 11 golden-capable families). The Epic-tier collisions
(`id=9576 by=[epic(bf=9501), epic(bf=9551)]`) have **different** bf ⇒ different V ⇒ no real
duplicate — hence no Epic double-count. (CK's own cook code at `Pug.Other:324035` confirms
the tier is an objectID swap gated on ingredient rarity, the variation staying constant.)

**Fix.** A `HashSet<long> seenKeys` in `AddCookedEntry` skips a repeat `(objectID, variation)`
before the (otherwise wasted) name/icon/value resolution. Correct regardless of the collision
mechanism, because two `accepted` entries with the same `PackKey` are the same CK item by
definition; no legitimate entry is lost (the legit `rare(bf=Base)` / `epic(bf=…)` rows carry
different variations, so they survive). Pure behavioural C#; no prefab/art touch.

**Measured in-game (1.2.1.5, fake-ID 9999997):** clean sandbox compile (`safetyCheck=True`,
0 `CompileFailed`); `accepted` 11030→10172, catalog `baked` 11119→10265, `dupKeys` 858→0; the
user confirmed a freshly cooked (previously undiscovered) dish now raises `N` by **1**, not 2.
**Process lesson (reinforced):** the two rejected hypotheses were both symptom-plausible; only
the `producedBy` probe — measuring the actual emit calls — settled it. Guessing the cause
would have written a *wrong* root-cause into the commit + docs. **Surfaced for follow-up
(Iter-33):** Loop 2 emits all three tiers per variation, but CK gates the achievable tier on
ingredient rarity, so some tier rows may be unreachable "phantoms" — not yet measured.

**Iter-33 (cooked-food tier reachability — phantom epic rows) — DONE (2026-07-12, branch
`iter-33`).** The Iter-32 follow-up: `ItemCatalog.Bake` Loop 2 emitted **all three** tiers
(base/rare/epic) for **every** food variation, but CK gates the achievable tier on ingredient
rarity — so most epic rows were unreachable "phantoms" stuck at `???` forever, making 100 %
unattainable (the Iter-16.4 bug class). **Measured before deciding**, per the standing lesson.

**Findings (all verified).** (1) **Cooking is the only source of cooked food** — a thorough
data-check of the decompile + the unpacked `resources.assets` (the master `LootTableBank.asset`
= 2813 loot entries, all 5 merchant inventories, start/dungeon inventory) found **zero**
references to any of the 45 cooked-food IDs (base 9500-9514, rare 9550-9564, epic 9575-9589) in
any loot / merchant / drop / reward; the only symbolic `ObjectID.Cooked*` in code is a Pug.Dev
guard that *blocks* spawning them. Positive control: the same grep finds neighbouring loot IDs
(9400-9406, 9600) but none in the cooked ranges. (2) **The tier is a Cooking-skill bonus roll**
(`Pug.Other:324006` managed + `~412405` ECS, structurally identical), gated by a per-variation
`flag` (deterministic): `flag = (an ingredient is a Rare-rarity Flower) OR (any ingredient is
Legendary)` — the Rare check is `FlowerCD`-gated, the Legendary check type-agnostic (so a
Rare-rarity *fish/meat* does NOT enable epic, only a Legendary one does). The base dish is the
normal cook output; rare is reachable in both flag branches; **epic is reachable only when
flag=true**; and `turnsIntoFood` never targets an epic ID, so epic is exclusively the flag-gated
bonus. ⇒ **the phantom set = `(epicVersion, V)` for every variation with flag=false.**

**Probe (throwaway, committed-then-reverted).** A bake probe mirroring CK's `flag` measured
in-game (1.2.1.5): **variations=3003, epicPhantom(flag=false)=2145, epicReachable(flag=true)=858**,
and the golden⇔flag cross-check came out **perfect** — `goldenBase&flagTrue=858`,
`goldenBase&flagFalse=0`, all 858 flag=true variations golden-based. So a golden-ingredient
variation ⟺ flag=true ⟺ epic-reachable, without exception (and 858 = Iter-32's 858 golden
dup-keys — three independent measurements converge). No base/rare phantom (both always reachable);
the roadmap's "golden flower may never yield base" worry dissolved — for golden variations
`turnsIntoFood` is a Rare ID (Iter-32), so the emitted "base" IS the rare-tier normal output.

**Fix (Variant A — don't emit phantoms).** `ItemCatalog` gained `CookedEpicReachable(variation)`
(a faithful mirror of CK's `flag`); Loop 2 emits the epic tier only when it returns true, else
records the key in a `suppressedCookedPhantoms` set. Catalog **10265 → 8120** (−2145); ~6,006
cooked dishes remain, and 100 % is now attainable.

**Safety net (durable + self-healing), added at the user's direction.** A suppressed phantom
should never be discovered (it is unreachable). If one ever is — a future CK gate change or a new
non-cooking source — it must be *heard*: `SaveManagerDiscoveryHook` routes a discovered suppressed
phantom into `PhantomViolationStore.Record`, which persists it durably to
`mods/ItemChecklist/phantom-violations.txt` (`API.ConfigFilesystem`, hand-ASCII, mirroring
`PossessionStore`) with the decoded ingredients, deduped cross-session, and logs one WARNING.
**Durable, not Player.log** (which rotates every launch → useless for a remote reporter). And
because a real-time write can fail while the discovery hook fires only once per key, a
**self-healing world-load re-check** (`ItemCatalog.SweepDiscoveredPhantoms`, fired at both
bake-completion and discovery-snapshot-apply — NOT the possession scan) re-derives violations as
`(discovered ∩ suppressed)` every load, so a lost write self-heals next load from CK's persisted
discovery.

**Verified in-game (1.2.1.5, fake-ID 9999997).** Clean sandbox compile (`safetyCheck=True`, 0
`CompileFailed`, 0 NRE) across all commits; `baked: 8120`; a throwaway injection proved the durable
write (correct path / format / dedup); the self-heal sweep ran at both triggers and correctly
recorded nothing (2145 suppressed × 415 discovered = 0 intersection — a true negative). Pure
behavioural C# + one new file (`PhantomViolationStore.cs`); no prefab/art. **Process:** measure-first
(brainstorm → spec → plan), hybrid inline-execution with a per-code-task adversarial review; the
durability + self-heal halves were user-directed mid-flight (a brief drift toward a possession-scan
trigger was corrected to the world-load anchors).

**Iter-34 (keybind rebind row — own control-mapping category) — DONE (2026-07-13, branch
`iter-34`).** A user-reported "no rebind row under Mods" that the **in-game screenshot re-framed**
before any code changed: the row was never missing — ItemChecklist's F1 toggle rendered as a
loose, **header-less** row at the top of Controls > Mods, while CoreLib's own commands ("Core
Library") and PlacementPlus's ("PlacementPlus") each sat under a mod-named section header. The
user's diagnosis was exact: *not* mis-sorted — **missing the mod-name as a heading** (and a
description).

**Root cause (verified against CoreLib 4.0.5's REAL source).** The investigation first chased the
roadmap's hypotheses — a broken Harmony patch or a failed loc term — and **refuted both**: in game
1.2.1.5, `ControlMappingMenu.Initialize()` and its `_mappingLayoutData` field still exist
(decompiled from the current `Pug.ControlMapping.dll`), CoreLib 4.0.5's `ControlMappingPatch`
prefix (`[HarmonyPatch(typeof(ControlMappingMenu), "Initialize")]`) binds fine, and the Player.log
logged no patch failure while the action term already rendered. The real cause is a single CoreLib
line: `ControlMappingModule.AddNewCategory_Internal` sets `_showActionCategoryName = (categoryName
!= "Mods")`. ItemChecklist registered its keybind with `categoryId: -1` — CoreLib's **default
"Mods" bucket** — whose sub-section header is therefore **deliberately suppressed** (the top-level
Controls tab is already named "Mods", so a redundant "Mods" sub-header is hidden). A mod that calls
`AddNewCategory("<Name>")` gets `_showActionCategoryName = true` → CK's `ControlMappingMenu` renders
a header + description via `GetCategoryLabelLocaKey(name, getName) = "ControlMapper/" + name +
(getName ? "Category" : "Description")`. So CoreLib's "CoreLib" category shows "Core Library" /
"Core Library Commands" and ItemChecklist's "Mods" bind showed nothing.

**Fix.** `ItemChecklistMod.EarlyInit` now calls `int catId =
ControlMappingModule.AddNewCategory("ItemChecklist")` before `AddKeyboardBind(..., categoryId:
catId)`. Two new loc terms under `ControlMapper:` in `localization.yaml` — `ItemChecklistCategory`
("Item Checklist" / "Item-Checkliste") and `ItemChecklistDescription` ("Item Checklist controls" /
"Steuerung der Item-Checkliste"), EN+DE. The category description is a **category-level** label
(like CoreLib's "Core Library Commands"), not an action description — future-proof if a second bind
is ever added (the single current toggle happens to match, but the abstraction is the category).
CoreLib migrates the persisted action to the new category on load (`ChangeActionCategory`);
`KeyBindsCategories.json` gains `"ItemChecklist":[103,103]`. A pre-existing orphan action
`ItemChecklist.Toggle` (an old dev-only name, id 1009) stays in the "Mods" bucket but does not
render (no element map); it only affects this machine's persisted state, not a fresh install. Pure
behavioural C# (one `AddNewCategory` call) + 2 loc terms; no prefab/art touch.

**Reference maintenance (user-directed).** The shared decompile checkout's CoreLib reference was
stale (4.0.4, `BUILD_FOR` game 1.2.1.1) while the runtime loads **4.0.5** (`BUILD_FOR` 1.2.1.3, on
game 1.2.1.5). Since CoreLib is open source, the decompile was superseded by its **real 4.0.5
source** — cloned from GitHub tag `4.0.5` into
`~/Projects/checkouts/CoreKeeperDecompile/CoreLib-source-4.0.5/` (CoreLib also ships the identical
source in its mod.io download at `…/mod.io/5289/mods/3177992_7845185/Scripts/`, which is how the
exact runtime version was read during the investigation).

**Verified in-game (1.2.1.5, fake-ID 9999997).** `Successfully compiled ItemChecklist
safetyCheck=True`, 0 `CompileFailed`, 0 NRE; `Added New Category: ItemChecklist` + action enabled;
the user confirmed (screenshot, EN) the toggle now sits under an "Item Checklist" header with the
"Item Checklist controls" subtitle. **Process:** the whole diagnosis hinged on the in-game
screenshot — the standing lesson (in-game ground truth beats static analysis) held yet again: the
static mechanism looked correct at every step, and only the screenshot revealed the symptom was
"header-less", not "missing".

**Iter-35 (foreign-mod item name — derived name + chain-page exclusion) — DONE (2026-07-13,
branch `iter-35`).** A user-reported "an item from another mod shows as the raw number 32773"
that split into **two distinct display bugs sharing one root cause**, and — like Iter-32/34 —
every framing shift came from **in-game measurement**, not decompile inference.

**The two bugs (the user corrected my initial conflation).** *ChestsGalore*'s `WorkbenchChestExtra`
(objectID 32773) rendered (1) its **row label** as the bare number "32773", and (2) its **Iter-22
hover tooltip** as CK's `missing: Items/ChestsGalore:WorkbenchChestExtra`. Two different render
paths: the row label is ICL's baked `localizedNames[key]`; the tooltip delegates to CK-native
`SlotUIBase.GetHoverTitle()` via `TooltipSlot`. Both had to be fixed.

**Decompile said "no term"; the screenshot said otherwise; the probe settled it.** The decompile
chain looked conclusive — `GetObjectName(true)` → `LocalizationManager.GetTranslation("Items/" +
name)` returns `null` for a missing term, and `PugText.ProcessText` renders a null as
`"missing: " + term`. So hypothesis (b) "ChestsGalore ships no term". But the **user's screenshots**
showed CK's own crafting UI resolving "Chest Workbench" / "Double Chest Workbench" fine — refuting
"no terms". A **throwaway bake probe** (committed-then-reverted) dumping, per foreign-mod item at
bake time, `GetObjectName(true/false)`, `dontLocalize`, `ObjectProperties "name"`, and
`GetLocalizedTerm` for the internal / raw / numeric keys, measured the truth over 16 foreign items:
- **12 resolve normally** (Double Chest, Magic Chest, the Large*Pouches, Beating Dummy, Root
  Workbench, **Chest Workbench = 32783**, Double Chest Workbench, Magic Chest Workbench, and ICL's
  own Caveling Divining Rod) — ICL already displayed these correctly.
- **4 return `null` everywhere** — exactly the `WorkbenchChestExtra` (32773), `WorkbenchChestNext`
  (32777), `WorkbenchDoubleChestExtra` (32774), `WorkbenchDoubleChestNext` (32771) variants.
- The screenshot's "Chest Workbench" is objectID **32783** (`WorkbenchChest`) — a **different**
  object from the buggy 32773. CK resolves 32783 because it *has* a term; 32773 has none.
- **Timing (c) refuted:** the 32773 line was bit-identical across 3 bakes at t=92/181/184 s
  (spanning two in-game language toggles) — the term is unresolvable at *every* point, not a race.
- **The internal name IS available:** `ObjectProperties "name"` = "ChestsGalore:WorkbenchChestExtra"
  (ItemBrowser's `GetInternalName` source — but IB uses it only for sort/search; IB's *visible*
  fallback is the numeric "id:variation", so ICL goes one better).

**Fix 1 — derived name (both render paths, one source).** `ItemCatalog.FallbackName`: when
`GetObjectName(true)` is empty, derive the name from the internal name (strip the `Mod:` prefix +
any CoreLib `$$N` suffix, then PascalCase-split → "Workbench Chest Extra") instead of the numeric
objectID; record the key in `fallbackNameKeys` → `Entry.NameIsFallback`. `ItemRow.GetHoverTitle`
returns that baked name `dontLocalize` for a `NameIsFallback` row (the same pattern the `???` rows
already use) instead of delegating to CK's "missing:" path, and `GetHoverDescription`/`GetHoverStats`
return null so no "missing:" leaks. One name feeds both label and tooltip.

**Fix 2 — exclude the internal chain pages (user-requested refinement).** The user asked what these
"Extra/Next" items even are: CoreLib workbench-chain **pages**. A base workbench folds in
continuation objects via `WorkbenchDefinition.relatedWorkbenches` (`includeCraftedObjectsFromBuildings`)
to present one unified crafting UI (the I/II/III tabs) — the Next/Extra are machinery, not
collectibles, hence unnamed. The user wanted to exclude *only* the chain, not all term-less items
(which could filter a legit foreign item). A **second verification probe** dumped the actual
`relatedWorkbenches` graph and **REFUTED the naive "referenced → exclude" filter**: the refs are a
**MESH** — ChestsGalore's siblings cross-reference each other, so `WorkbenchMagicChest` and
`WorkbenchDoubleChest` list the **named bases** `WorkbenchChest`/`WorkbenchDoubleChest` as related.
A "member of any relatedWorkbenches" filter would have dropped "Chest Workbench" (excludeSet came
out as 6 IDs, including the 2 named bases). The precise, verified rule (`BuildWorkbenchChainSets` +
a Loop-1 `continue`): a **chain member** (referenced by some non-root `relatedWorkbenches`) is
dropped when it is a **leaf** (its own `relatedWorkbenches` empty → folds in nothing) **OR is
term-less** (per the user, to also catch a hypothetical middle page). The named bases are hubs
*with* a name → kept; the CoreLib root workbench is skipped (it aggregates via the
`bindToRootWorkbench` flag, not `relatedWorkbenches`). The term-less test is confined to chain
members, so a legit standalone term-less foreign item of another mod is untouched and still gets
its derived name (Fix 1). Verified exclude-set = exactly `{32771, 32773, 32774, 32777}`; reading
`LoadedMod.Assets.OfType<WorkbenchDefinition>()` is sandbox-safe (`WorkbenchDefinition` is in the
already-referenced `CoreLib` assembly, namespace `CoreLib.Submodule.Entity`).

**Verified in-game (1.2.1.5, fake-ID 9999997).** `Successfully compiled ItemChecklist
safetyCheck=True`, 0 `CompileFailed`, 0 NRE, 0 `BuildWorkbenchChainSets threw`; catalog **8120 →
8116** (the 4 pages gone); the user confirmed the label + tooltip fix on the term-less items and
that the named workbenches (Chest/Double Chest/Magic Chest/Root Workbench) + all other foreign
items are intact. Two clean commits (derived name; chain-page exclusion). Pure behavioural C#; no
prefab/art touch. **Process:** two throwaway probes, both of which overturned a plausible
decompile/source hypothesis — the "measure first, in-game ground truth beats static analysis"
lesson held for (by this project's count) the seventh and eighth time; the second probe
specifically prevented shipping a filter that would have deleted the named "Chest Workbench".
