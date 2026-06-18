# ItemChecklist — Iteration History

Full per-iteration narrative of ItemChecklist's development (Iter-3.5 through
Iter-14.1), moved out of `CLAUDE.md` to keep that file focused. See `git log` for
canonical per-iter merge points and `docs/superpowers/specs/` for design docs.

As of 2026-06-18: Iter-3.5 through Iter-12 (incl. the 3.x/7.1 point-iters and the
Iter-12 extension), Iter-13, Iter-14.1, Iter-18, Iter-14.2, and Iter-15 are DONE on main. Iter-3.8
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
