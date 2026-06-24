# ItemChecklist Code & Process Conventions

## Branch + Commit Conventions

**Branch naming:** `iter-<n>[.<m>[-letter]]`, e.g. `iter-3-7`, `iter-3-5c`.
The letter suffix marks a within-iteration pivot (e.g. `iter-3-5b` was an
aborted approach, `iter-3-5c` was the successful re-design).

**Iteration numbering runs two logics — timing ≠ number.** Numbers are assigned
*both* sequentially-by-merge (3.5 → 7 → 9 → 10 → 11 …) *and* topic-reserved
(15/16/17/18 were named tentative themes with no fixed build order). So a reserved
number can be built out of sequence: Iter-18 was the reserved number for the
combobox-header redesign and got built before 14.2/15/16/17 ("Iter-18 pulled
forward"). A statement about *when* something is done ("wedge it between 14.1 and
14.2") is about timing, not about renumbering it.

**Commit types** (conventional commit style with scopes):

| Type | When to use |
|---|---|
| `feat` | New user-visible behavior (e.g. a new iter's core feature) |
| `fix` | Bug fix — regression or correctness issue |
| `refactor` | Internal restructure without behavior change |
| `docs` | Documentation-only change |
| `wip` | In-progress checkpoint (within a plan-branch) |

WIP commits in plan-branches document the development story (e.g.
`wip(spike): first decompile pass on UIScrollWindow`). They are kept
individually — no squash before merge.

**Merge policy:** ff-merge to main; no squash; no merge commits. Each commit
stays `git log`/`git blame`/`git bisect`-friendly. Spec and plan are committed
as early, separate commits on each branch.

**Force-push:** after a rebase of a pushed feature branch, force-push with
`--force-with-lease`.

**New `.cs` → commit the source first, the `.cs.meta` in a follow-up `chore`.**
Unity writes a new script's `.cs.meta` GUID carrier only on the next import after
the `.cs` is added (see `docs/gotchas.md § Generated .meta trails its .cs by one
build`), so it does not exist yet when the source is first authored. Commit the
`.cs` first; once a build has run and the `.cs.meta` appears, add it in a follow-up
`chore` commit (a single new file together with the source is also fine if the meta
already exists by commit time).

**Hand-editing an `.asmdef` `precompiledReferences` is the correct way to add a
game-DLL reference — not a package-manager violation.** Unity `.asmdef`s have no
package manager, so adding a DLL (e.g. `"ScriptableData.dll"` for
`GradientMapDataBlock`, Iter-16.1; `"PugSprite.dll"` for `SpriteObject`, Iter-9) to
`precompiledReferences` by hand is the sanctioned method — it does **not** breach
the global "use the package manager for dependencies" rule, which targets package
ecosystems (npm/uv/cargo), not Unity asmdefs.

### Committing around Unity prefab reserialization

Unity rewrites a whole `.prefab` on every Editor save — GameObject blocks get
reordered and trailing whitespace is normalised — so a functional one-line change
arrives mixed with dozens of cosmetic diff hunks that belong to no feature. Four
techniques keep the history clean (all used in the Iter-12 extension):

- **Working-tree swap for hunk-level splitting.** `git apply --cached` is
  unreliable here: the cosmetic reorder interleaves with functional hunks, and a
  single `m_Children` hunk often mixes two features. Instead reconstruct the file
  *content* per commit and let git recompute the diff — save the full version
  aside (`cp file /tmp/full`), check out HEAD (`git show HEAD:file > file`),
  re-apply only commit A's edits, commit; then restore the full version and
  commit B. No hand-counted patch offsets, no risk of mangling the YAML.
- **HEAD-rebuild to strip reserialization noise.** To commit *only* the
  functional change with zero cosmetic churn, rebuild the target file **from
  HEAD** applying only the functional edits — everything untouched stays
  byte-identical to HEAD and never appears in the diff. This removes the
  whitespace/reorder noise by construction (cleaner than reverting it back out of
  the Editor-saved version).
- **Prove functional equivalence with a sorted-value hash.** To confirm a
  rebuilt/noise-stripped prefab is identical to the in-game-verified version,
  `grep` the functional lines (`m_Sprite`/`m_Size`/`m_Father`/`m_Enabled`/
  `m_SortingOrder`/sprite fileIDs), **sort**, and hash. Equal hashes prove the
  functional multiset is unchanged — sorting drops GameObject-order noise and the
  grep skips whitespace-only lines. Always check the match *count* against the
  expected count: a `\S+:` grep silently misses names containing spaces.
- **Separate the functional commit from the canonical-format commit.** Keep
  feature commits noise-free (reviewable), then — if the repo should match Unity's
  canonical serialization so the next Editor save yields no phantom diff — make a
  dedicated `style(prefab): …` commit that re-applies the Editor's trailing
  spaces / block order. Functional change and format reconciliation stay separate.

## Worktree Conventions

**Setup:** every new worktree needs only the gitignored `.envrc` copied from
the main checkout:

```bash
cp .envrc .worktrees/<branch>/.envrc
```

`.envrc` carries the machine-local build env (`UNITY_BIN`, `SDK_PATH`, etc.)
and is never tracked in git. `unity/ItemChecklist/Art/` no longer needs copying:
since Iter-12 the pixel-art sheet under `Art/UI/` is **tracked**, so
`git worktree add` checks it out automatically (the old gitignored `Art/Bridge/`
placeholder sprites were deleted in Iter-12).

**Building from a worktree — use `direnv exec`, not the `.envrc` manual fallback.**
`build.sh` requires the env vars (`UNITY_BIN`, `SDK_PATH`, `MOD_INSTALL_PATH`, …),
but the manual `source .envrc && ../utils/build.sh` fallback documented in `.envrc`
is **broken inside a worktree**: its `source ../.envrc` resolves to
`.worktrees/.envrc`, which does not exist (the parent `core_keeper/.envrc` is three
levels up, not one). Robust invocation from the worktree:

```bash
cd .worktrees/<branch>
direnv allow .
direnv exec . bash -c '../../../utils/build.sh 2>&1 | tee "${MOD_INSTALL_PATH%/}/build.log" | tail -45'
```

`direnv exec` walks the real `source_up` chain (worktree → mod → parent
`core_keeper/.envrc`); `MOD_INSTALL_PATH` resolves to the *same* shared ModLoader
install path as main (the running game loads from there), and `build.sh` re-runs
`link.sh` so the SDK symlinks re-point at this worktree. Note the **three** `..`
levels to `utils/build.sh`.

If you must build from a **bare shell without `direnv`** (e.g. an automated/tool
shell), reproduce the `source_up` chain by hand by sourcing the parent
`core_keeper/.envrc` **by absolute path first**, then the worktree `.envrc` (whose
`$PWD`-relative `MOD_*` exports then resolve to the worktree): `source
/abs/path/core_keeper/.envrc && source ./.envrc && ../../../utils/build.sh`. The
worktree `.envrc`'s own `source ../.envrc` fallback does **not** work here — from
`.worktrees/<branch>` it resolves to the nonexistent `.worktrees/.envrc`.

**superpowers specs → `docs/specs/` (tracked); plans → `docs/superpowers/plans/`
(gitignored). Author both in the MAIN checkout, not the worktree.** Per the global
policy the per-iter **spec** is written to `docs/specs/YYYY-MM-DD-<slug>-design.md`
— a **tracked** location, and a `PostToolUse` hook enforces it (it rejects specs
written to `docs/superpowers/specs/` and tells you to move them). The **plan** stays
under `docs/superpowers/plans/`, which is **gitignored** scratch. Author and edit
both in the **main checkout**: `git worktree add` only checks out *tracked* files,
so anything written to the worktree's gitignored `docs/superpowers/` is lost on
`git worktree remove` (the tracked `docs/specs/` spec would survive, but keep
authoring in main for consistency). **Spec retention is ADR-gated** — commit the
`docs/specs/` spec only when the change warrants an ADR; for a routine fix/iter,
discard it after the ff-merge (do not leave it as an untracked file under the
tracked `docs/specs/`).

**Before destroying a worktree:**

```bash
# Verify CWD is NOT inside the worktree
cd /path/to/main-repo-root
git worktree remove .worktrees/<branch>
```

Destroying a worktree while the shell CWD is inside it can block the shell
session irreversibly.

**Build verification:** after install, grep `Player.log` for the expected log
markers (e.g. `Loading mod with ID`, `ItemChecklist: Bake complete`). Do not
trust build output alone — a build duration under 60 s or an install mtime
older than the source file is suspicious and warrants a re-run.

## Testing Conventions

Each iter ends with a structured acceptance test. The canonical 7-phase
structure (established in Iter-3.6/3.7):

| Phase | What to check | Zero-tolerance? |
|---|---|---|
| 1. Sandbox compile | Player.log grep for `CompileFailed` — must be zero | Yes |
| 2. First-open regression | Visual baseline from previous iter must be intact | Yes (1-strike) |
| 3. Pool-leak regression | Multi-open `F1 → ESC → F1` × N; main-menu PugTexts must not go blank | Yes |
| 4. Feature verification | The new iter's core addition works as specified | Yes |
| 5. Layout side-effects | Nothing looks worse than before (scroll, clipping, row spacing) | Yes |
| 6. Localization check | Switch language in CK settings; checklist rebakes with new names | Yes if loc work was done |
| 7. Cooked-food spot-check | Open checklist; verify cooked-food entries appear and tick on pickup | Yes if food work was done |

**Failure budget:** 3 sandbox-compile attempts (Phase 1) before escalating to
a fresh batchmode build from a clean state. 1-strike for Phase 2 and later.

**Player.log snapshot discipline.** `Player.log` is per-launch — each CK start
overwrites it and rotates the prior session to `Player-prev.log`, so **two** extra
launches drop the test session out of both. After an in-game smoke test, do NOT
relaunch CK before the log has been grepped (especially when the assistant reads it
out-of-band). Either leave the game closed until the log is read, or snapshot it
immediately (`cp Player.log Player.<tag>.log` — the bottle already uses
`.pre-iterNN-task-test` snapshots).

**Verify WHICH build served the test (fake-ID dev vs. published mod.io).** This
machine usually has BOTH the fake-ID dev install *and* the published mod.io listing
subscribed (e.g. ItemChecklist: dev `9999997` + published `6112539`). Before
trusting a smoke-test result — especially a *negative* one ("0 crashes",
"bug gone") — confirm the **dev build with your fix actually compiled and ran**, not
the published copy without it. In `Player.log`: there is exactly one
`Loading mod with ID <FAKE_ID>` line and one `Successfully compiled <Mod>` line for
the build that activated; the published listing appears only in the subscription
enumeration (`loaded mod <Mod> from mod.io (<DisplayName>)`) and is **not** activated
with an ID. Had the unfixed published copy served the feature, the old symptom would
re-appear — making a "0 hits" grep meaningless. (Mirrors the global
`mod-install-topology-from-log` memory: read runtime state from the log, not from
`.envrc`.) A clean Editor build proves only *compile + install landing*; a
runtime-only fix is validated only by the in-game smoke test on the correct build.

### Throwaway test-scaffold pattern

To accelerate a manual in-game test, it is acceptable to layer a *throwaway*
debug scaffold *in front of* the reviewed core — then remove it via
`git restore` before merge so the merged code stays byte-identical to the
reviewed version.

Iter-3.8 example: reaching the end of the list (~10,720 entries at Iter-3.8;
~10,910 today) by scrolling takes
too long to verify flush-geometry on the last row. A throwaway
`DebugDiscoveredOnly` index-remap was added in front of the recycler
(`catalogIdx = _useMap ? _indexMap[idx] : idx`), making any slice of the list
reachable in seconds while exercising the identical flush-geometry path. The
scaffold lived only in uncommitted working-tree edits; the reviewed row logic
was already committed, so a `git restore` of the modified `.cs` removed the
scaffold cleanly without touching reviewed code. (The same remap can later
seed the real Iter-8 discovered-only filter.)

Rule: scaffolds never get committed onto a story commit. Add them to the
working tree, use them, then `git restore` before the iter's ff-merge.

### Temp-`Debug.Log` measurement technique

To get exact in-game numbers that cannot be read from the prefab (camera
`orthographicSize` / aspect for the viewport size, dropdown / text width via
`PugText.localCharacterEndPositions`), drop a one-shot throwaway `Debug.Log`
that prints the value, read it from `Player.log`, then remove the log. Same
discipline as the throwaway scaffold above — it never reaches a story commit.
**ILSpy-verify the accessor first** (field vs. property, exact name): a
sandbox-banned member would `CompileFailed` the whole mod, so confirm the
member is sandbox-safe in the decompiled DLL before adding the log.

### Spike + counter-probe — a single positive case can be ambiguous

A spike that proves a mechanism "works" on **one** sample can be lying by
omission when that sample is a degenerate case. Iter-22's `GetHoverStats` spike
on a coin (a stat-**less** item, `statLines = -1`) returned empty stats —
indistinguishable from "the call is broken." Adding a stat-**bearing**
counter-probe (a Copper Sword, `statLines = 2`) proved the path returns stats
*systematically*, not by accident. Rule: when a spike's positive result could
also be produced by a no-op, add a counter-probe whose expected output **differs**
before claiming the mechanism works (the Iter-21-probe two-detector precedent).

**Recovery:** if Core Keeper hangs on the loading screen (quit-deadlock in
`ModManager` — symptom: `Exit blocked by ModManager` in Player.log), use:

```bash
pkill -KILL -f "Core Keeper"   # GNU pgrep/pkill -f variant; macOS-safe
```

Do not use the normal quit path — it blocks and the process must be killed.

### Catalog inclusion/exclusion: trust the in-game size delta, not a decompile count

When relaxing or tightening an `ItemCatalog.Bake` filter (the Iter-7.1 /
Iter-16.2 pattern), a decompile is **inference, not ground truth** — it has been
wrong three times on critters alone (the "KilledEnemiesBuffer-only" miss, the
Iter-16.1 pet guess, and Iter-16.2's "~15 at 9800–9819" that omitted 5 Fireflies
and mis-flagged real IDs as gaps). Discipline:

1. Predict the catalog-size delta and gate the iteration on it. The bake's
   `ItemCatalog baked: N items` log line is the **trip-wire** — if the delta is
   off (e.g. `+25` against an expected `+15`), STOP; do not proceed.
2. Resolve the discrepancy with a **throwaway in-game probe that enumerates every
   entry the predicate admits** (ObjectID + localized name + icon presence →
   `Player.log`), plus one or two live-play observations (e.g. "is it catchable /
   already in a chest?"). That is what separates legitimate entries from
   permanent-`???` ghost rows — a decompile count cannot.

The size-delta is the trip-wire; the enumeration + live play is the resolver.
Catalog-specific application of the global `verify-empirically` rule.

## File Layout

Canonical layout under `unity/ItemChecklist/` (source of truth is the git
tree):

```
unity/ItemChecklist/
  ItemChecklistMod.cs             IMod bootstrap (EarlyInit/Init/Update/Shutdown)
  ItemCatalog.cs                  catalog bake + lookup
  ItemCatalogLocChangeHook.cs     Harmony patch — re-bake on language change
  ItemCatalogWorldLoadHook.cs     Harmony patch — kick bake on world load (OnOccupied)
  DiscoveredState.cs              in-memory mirror of CK discovery state
  SaveManagerDiscoveryHook.cs     Harmony patch on SaveManager.SetObjectAsDiscovered
  SaveManagerActiveSelectHook.cs  Harmony patch for active-character resolution
  SaveManagerWriteCharacterHook.cs  Harmony patch — persist possession ledger on character save (Iter-20)
  CharacterDataDiscoverySnapshot.cs  initial-state reader on OnAfterDeserialize
  PascalCaseSplitter.cs           pure utility (display-name fallback formatting)
  Loc.cs                          localisation helpers (Loc.T / Loc.F) (Iter-11)
  ProgressFormat.cs               shared discovered/total counter string — footer + HUD (Iter-11.5)
  InventoryOpenAutoHidePatch.cs   Harmony patch — auto-hide on Vanilla menu open (Iter-4)
  CursorScaleRestorePatch.cs      Harmony patch — restore cursor scale while open (Iter-9)
  InGameButtonHintsSuppressPatch.cs       Harmony patch — hide button-hint prompts (Iter-9)
  InventoryShortCutsButtonSuppressPatch.cs  Harmony patch — hide inventory shortcuts button (Iter-9)
  ShortCutsWindowSuppressPatch.cs Harmony patch — suppress help panel (Iter-9)
  PauseSuppressWhileChecklistOpenPatch.cs  Harmony patch — block ESC->pause race (Iter-9)
  MainListWheelSuppressPatch.cs   Harmony prefix on UIScrollWindow.UpdateScroll — give wheel to an open popup (Iter-24)
  ThinTinyGlyphPatch.cs           Harmony-anchored runtime accented-glyph insert into thinTiny (Iter-25)
  WorldState.cs                   shared IsInPlayableWorld predicate (HUD + F1 guard) (Iter-11.6)
  ItemChecklist.asmdef            runtime assembly definition
  ui/
    ItemChecklistWindow.cs        IModUI implementation (UIelement subclass)
    ItemChecklistHud.cs           non-modal always-on top-right HUD counter (UIelement) (Iter-11.5)
    ItemRow.cs                    row MonoBehaviour (Bind API)
    ItemChecklistContent.cs       IScrollable implementation (viewport recycler)
    ItemListViewModel.cs          order/filter/search view model (Iter-7/8)
    SortMode.cs                   sort-mode enum + comparators (Iter-7)
    DropdownWidget.cs             reusable dropdown (sort/filter) (Iter-7/8)
    PopupWidget.cs                abstract popup base shared by DropdownWidget/FilterWidget (Iter-14.2; scroll + collapse Iter-24)
    PopupScrollHandle.cs          draggable scrollbar handle for the popup scroll (Iter-24)
    IPopupToggle.cs               toggle-owner seam shared by dropdown + filter toggles (Iter-13)
    ClickButton.cs                abstract base for click controls (sealed prologue + OnClick) (Iter-14.2)
    PugTextExtensions.cs          PugText.RenderNoWrap helper — single home of maxWidth=0f (Iter-14.2)
    DropdownToggleButton.cs       dropdown header toggle button (Iter-7)
    DropdownOptionButton.cs       dropdown popup option button (Iter-7)
    AscDescToggle.cs              ascending/descending direction toggle (Iter-7)
    SearchBar.cs                  TextInputField subclass — name search (Iter-8)
    ClearSearchButton.cs          clears the search field (Iter-8)
    ItemCategory.cs               category taxonomy (ObjectType -> bucket) (Iter-10)
    FilterWidget.cs               sectioned multi-select filter dropdown (Iter-10, renamed Iter-14.2)
    FilterCheckboxButton.cs       filter checkbox row button (Iter-10, renamed Iter-14.2)
    SectionHeaderButton.cs        clickable filter section header — collapse/expand (Iter-24)
    PetSkinIcon.cs                gradient skin-icon material (Amplify/UISpriteColorReplace) (Iter-16.1)
    TooltipSlot.cs                shared SlotUIBase helper feeding CK native tooltips for rows (Iter-22)
  possession/                     possession scan/ledger/persist package (Iter-20)
    PossessionScanner.cs          live ECS scan: carried + clustered-base storage
    PossessionLedger.cs           per-(x,z) tile ledger; merge + "remembered" remotes
    PossessionStore.cs            per-character-GUID persistence (API.ConfigFilesystem)
    PossessionView.cs             immutable per-refresh snapshot (Count(objectId))
    PossessionConfig.cs           AnchorRadius + tuning
    PossessionClassifier.cs       type/ID predicates (PlaceablePrefab, locked chests, boss statues)
    PetCollection.cs              persistent ever-owned per-(objectID,skinIndex) ledger (Iter-16.1)
    PetCollectionStore.cs         per-GUID persistence — petskins-<guid>.txt (Iter-16.1)
  Localization/
    Generated/                    build-generated .asset TextDataBlocks (gitignored)
  Prefabs/
    ItemChecklistWindow.prefab    window hierarchy
    ItemChecklistHUD.prefab       always-on HUD counter (checkbox icon + PugText) (Iter-11.5)
    ItemRow.prefab                row template (recycled by scroll list)
    Dropdown.prefab               shared dropdown skeleton chrome (Field/Display + empty Popup) (Iter-13/18)
    Sort.prefab                   sort dropdown — variant of Dropdown.prefab (+ DropdownWidget, in-Display AscDesc) (Iter-18)
    Filter.prefab                 faceted filter — variant of Dropdown.prefab (renamed from FacetedFilter.prefab, Iter-18)
  Art/UI/                         tracked pixel-art sheet ui_checklist.png + mask_sprite.png (Iter-12)
  Art/thinTiny_glyphs.png         bundle sprite sheet (textureType:8 spriteMode:1) — runtime accented glyphs (Iter-25)
  Editor/
    ItemChecklist.Editor.asmdef   editor-only assembly for the CLI helpers
    CLIBuildHelper.cs             -> core_keeper/utils/ (shared symlink, gitignored)
    CLIPublishHelper.cs           -> core_keeper/utils/ (shared symlink, gitignored)
    LocalizationGenerator.cs      -> core_keeper/utils/ (shared symlink, gitignored)
    ItemChecklist_modio.asset     mod.io modId carrier (real ID 6112539) — read by CLIPublishHelper
    logo.png                      mod.io listing logo (uploaded on publish)
    SDK_SETUP.md                  SDK setup boilerplate (in-tree doc)
```

The three Editor helpers (`CLIBuildHelper.cs`, `CLIPublishHelper.cs`,
`LocalizationGenerator.cs`) are **not** committed in this repo. ItemChecklist was
the pilot for the shared-helper pattern (now adopted by every mod): the real
files live in `core_keeper/utils/` (namespace `CoreKeeperModUtils`) and are
symlinked into `Editor/` by `link.sh`. The symlinks (and
`Localization/Generated/`) are therefore gitignored.

The **source** localisation YAML is **not** under `unity/`: it lives at
repo-root `localization/localization.yaml` (the `.envrc:LOC_YAML` path),
deliberately outside the `unity/ItemChecklist/` tree so `ModBuilder` does not
pack it into the AssetBundle. `LocalizationGenerator` reads it and writes the
per-language `.asset` TextDataBlocks into `unity/ItemChecklist/Localization/
Generated/` (the `.envrc:LOC_OUT` path) at build time.

The glyph-sheet reproducibility tooling also lives outside `unity/`:
`sources/glyph-templates/*.py` (tracked extraction/debug tools that regenerate
`Art/thinTiny_glyphs.png` from `sources/thinTiny_full.pixaki`, Iter-25). The
intermediate atlases and `glyph_metrics.json` they emit are gitignored — only
the `.py` tools and the `.pixaki` master are tracked.

**Naming patterns:**

- `*Hook.cs` — top-level Harmony patches
- `*Patch.cs` — top-level Harmony patches (toggle/suppression behaviour)
- `*Snapshot.cs` — top-level initial-state readers
- `ui/*.cs` — IModUI, IScrollable, and row logic
- `Editor/*.cs` — editor-only build/publish helpers (in separate `.asmdef`)

## UI Code Conventions

**Click-button convention — extend `ClickButton`, implement only `OnClick()`.**
New clickable controls extend `abstract ClickButton : ButtonUIElement` (not
`ButtonUIElement` directly). The base does the uniform guard-first prologue once
in a `sealed override OnLeftClicked` (`if (!canBeClicked) return;` →
`base.OnLeftClicked(…)` → `OnClick()`); the subclass implements only
`protected override void OnClick()`. The five controls follow this:
`DropdownOptionButton`, `DropdownToggleButton`, `AscDescToggle`,
`ClearSearchButton`, `FilterCheckboxButton`. Mechanism + prefab rules (3D
`BoxCollider`, empty `spritesShown*`) in `docs/architecture.md § ButtonUIElement
Click Pattern`.

**UI label rendering — route single-line labels through `RenderNoWrap`.**
Render every localised single-line label (dropdown/option/row/section labels) via
`PugText.RenderNoWrap` (`ui/PugTextExtensions.cs`) — the single home of
`maxWidth = 0f`. Order is load-bearing: `maxWidth = 0` MUST precede `Render`, or
CK's PugFont word-wrap path `IndexOutOfRange`-crashes on long (German) labels and
aborts `ShowUI`. Do not re-inline the `maxWidth = 0f; Render(…)` pair. See
`docs/gotchas.md § PugFont.Render crashes`.

**Diagnostics symmetry — no silent prefab-wiring failure.** Parallel pool
builders that depend on a template's button component must fail loudly when the
component is missing: both `DropdownWidget.EnsurePool` and
`FilterWidget.GrowButtonPool` `Debug.LogError` when their template lacks its
expected button. Silent wiring gaps are the hardest bug class here (they survive
a clean Editor compile and a successful build) — surface them in the log.

**Subclassing a CK component — `private new void Awake()` + explicit `base.Awake()`.**
CK component lifecycle methods (`Awake`, …) are **non-virtual** (`protected void
Awake()`), so a subclass cannot `override` them — hide with `private new void
Awake()` and call `base.Awake()` explicitly (Unity dispatches the message once, to
the most-derived method, so the base body runs only if you call it). Use this when
a subclass must adjust state the base `Awake` sets: `SearchBar` (Iter-19) overrides
`TextInputField.Awake` to reset `pugText.maxWidth = 0f` *after* `base.Awake()` wrote
the value that triggers CK's word-wrap crash (see `docs/gotchas.md § Search Field /
Header`). `Awake` is the right hook (not `LateUpdate`) when the correction must hold
before the first render and nothing rewrites the value per frame.

**Spoiler-gate the content, not the interaction (Iter-22).** When a hover/select
control must stay interactive on an undiscovered (`???`) row but must not leak the
item, gate the **payload** (the tooltip text in the four hover overrides), not the
**collider**. Keep the collider always enabled so every row — including `???` —
still hover-**highlights** (the highlight reveals nothing → spoiler-safe), and
return a minimal localized placeholder (`??? - not yet discovered`) for
undiscovered rows instead of the real item. Mechanism in `docs/architecture.md
§ Row-Hover Tooltips`.

**Drive transient hover/selection visuals per-frame in `LateUpdate`, not the
one-shot `OnSelected`/`OnDeselected` (Iter-22).** A highlight set from
`OnSelected` and cleared from `OnDeselected` can leave a **stuck** highlight: the
cursor can move onto an overlay without firing a selection change, so the deselect
is skipped. Recompute the visual every frame instead
(`highlight = selected && PointerInViewport()`), with an idempotent
`if (highlight.enabled != show)` guard. `UIelement.LateUpdate` is `protected
virtual`, so it is safely overridable (call `base.LateUpdate()` as the base
contract requires).

**Match the exact CK virtual signature before overriding — grep the decompile
first (Iter-22).** A CK override must mirror the base signature *exactly* (e.g.
`UIelement.OnDeselected(bool playEffect = true)`, `GetHoverStats(bool)`); a
near-miss compiles as a *new* method and the override silently never binds, or
`CS0115` ("no suitable method found to override"). Confirm the signature against
the decompiled DLL before writing the override — a grep is cheaper than a failed
build. (Extends the standing "mirror CK internals, don't approximate" maxim.)

## Input / Keybind Conventions

**One toggle, one mechanism — the rebindable Rewired action is the sole trigger.**
The checklist toggle is registered once via CoreLib
`ControlMappingModule.AddKeyboardBind(ToggleActionName, defaultKeyCode: F1)` in
`IMod.EarlyInit`, and polled in `IMod.Update` as
`rewiredPlayer.GetButtonDown(ToggleActionName)`. Do **not** add a parallel raw
`Input.GetKeyDown(KeyCode.F1)` OR-term: the action's `defaultKeyCode` already
covers default-F1, so a raw read adds nothing but a hardcoded phantom opener that
survives the player rebinding the key in the game's input settings (the bound key
works, yet old F1 keeps opening too). Iter-23 removed exactly such a raw fallback
(see `docs/iteration-history.md`); the misleading comment that had called it a
"diagnostic fallback" is the cautionary case in `docs/gotchas.md § Lying comments
misdirect diagnosis`.

## Prefab Authoring Conventions

**Runtime-`AddComponent` vs. prefab-author — by whether the component needs
calibration (Iter-22).** An **invisible, no-calibration** component (a hover
`BoxCollider`: no size-tuning, no sorting, no 9-slice) is fine to `AddComponent`
at runtime — and doing so **avoids** the silent-fail traps of prefab-wiring a
component (a `fileID 0` serialized ref, a missing `m_Script`, a renamed field key
deserializing to null). A **visible** component that needs size / sorting-layer /
9-slice tuning (the hover `SpriteRenderer` highlight) must be **authored in the
prefab** for Editor control. **But consistency can override the split:** once the
prefab is already open to author the visible part, move the invisible collider in
too rather than split one feature across both worlds — Iter-22 ended with **both**
the highlight and the collider prefab-authored.

**A CK *runtime* asset cannot be referenced by a mod prefab (Iter-22).** A
serialized field on a mod prefab can only point at assets **in the mod's
AssetBundle**. A CK runtime asset — e.g.
`Manager.ui.GetCraftingUITheme(...).slotHoverSprite` (the inventory-slot hover
sprite) — is not in the bundle, so a prefab ref to it bundles broken / null.
Either assign it at **runtime** from `Manager.ui` (sandbox-safe) or supply your
**own** sprite in the bundle. Iter-22 supplied its own highlight sprite.

**Abstract MonoBehaviour bases are prefab-neutral — a named exception to the
one-MonoBehaviour-per-file / `fileID 11500000` rule.** Unity serialises inherited
public fields **by name**, and an abstract base is never instantiated, so it needs
no `m_Script`/`fileID` reference in any prefab. Hoisting shared serialized fields
(or shared logic) into an `abstract` base — `ClickButton`, `PopupWidget` — does
**not** require a prefab edit: the prefab keeps referencing the concrete subclass
(`fileID 11500000`), and the inherited fields resolve by their unchanged names.
This is the documented exception to "one MonoBehaviour per file, each with its own
`fileID`."

**Class-rename / field-rename procedure.** To rename a MonoBehaviour class,
`git mv` the `.cs` **and** its `.cs.meta` **together** — the meta carries the GUID,
and prefab refs are `m_Script: {fileID: 11500000, guid: <meta-guid>}`, so the
class **name appears nowhere** in the prefab; a GUID-preserving rename is
prefab-neutral (verified Iter-14.2: `FacetedFilterWidget`→`FilterWidget`,
`FacetCheckboxButton`→`FilterCheckboxButton`, refs held). **CAVEAT:** renaming a
*serialized field* is different — the prefab YAML stores the field **by key**, so
a field rename DOES need a matching prefab YAML field-key edit (a mismatch
deserialises **silently to null**, no compile error). Verify the field key with
`utils/prefab_query.py` before the build (Iter-14.2: `facetedFilter`→`filter` on
the window prefab).

**Re-parent a prefab component without breaking references.** To move a
component to a different GameObject while keeping every reference to it intact,
**keep the component's `fileID`** and only repoint its `m_GameObject`: add the
component's `fileID` to the new GO's `m_Component` list and remove it from the
old GO's list. References elsewhere (other components' serialized fields) are by
`fileID`, so they survive untouched. Iter-14.1 re-homed the caret
`SpriteRenderer` onto a child `CaretSprite` GO this way, so
`CharacterMarkBlinker.sr` needed no rewire (see the Iter-14.1 entry in
`docs/iteration-history.md`).

**Inspect prefab structure with the parser, not grep.**
`utils/prefab_query.py <prefab> tree [Name]` prints the GameObject hierarchy
(from a named GO, or all roots when omitted), marking `[inactive]` GOs. Added
Iter-13 to verify prefab-**variant** structure, where grep/awk over the variant
YAML is unreliable (variant fileID reassignment + stripped-object stubs defeat
line-by-line greps). Use the structured loader/`tree` output instead — see
`docs/gotchas.md`.

**Skeleton + one-level sibling variants for "same chrome, different contents."**
When several UI elements share one chrome but differ in contents, author one base
**skeleton** prefab plus one prefab **variant** per consumer (one level deep,
is-a) — not a window-level pile of instance overrides. Unity's three composition
mechanisms map onto relationships: Nested prefabs = composition (has-a); Variants
= inheritance (is-a, **one level** — deep variant chains are a smell); Instance
Overrides = the fragile situational last-mile (invisible until you inspect the
instance, and they break silently when the base changes). A large instance-override
pile carrying *substantial structure* is the anti-pattern to avoid — push that
structure into a variant instead. Realised in Iter-18 as the `Sort.prefab` /
`Filter.prefab` sibling variants of `Dropdown.prefab` (see `docs/architecture.md
§ Shared Dropdown chrome`).

**Base-wire a shared mechanism; runtime-discover the inherited skeleton chrome it
drives.** When a feature spans a C# base class and the skeleton/variant chrome it
operates on (Iter-24 popup scroll + collapse), split it so each piece lives where it
belongs and nothing relies on a fragile serialized cross-prefab ref:

- **Mechanism (C#)** → the `abstract` base (`PopupWidget`: cap, translate, wheel,
  handle math, scroll-active gate). It is dormant until a per-variant prefab value
  activates it (the `0`-sentinel cap, below), so every variant inherits it for free
  and behaviour-neutrally.
- **Chrome GameObjects** (the popup `SpriteMask`, the scrollbar subtree) → the base
  **skeleton** prefab; both variants inherit them as inactive GOs.
- **References to those inherited GOs** → **runtime-discovered** in the base
  (`popupPanel.GetComponentInChildren<SpriteMask>(true)` /
  `<PopupScrollHandle>(true)`), **not** serialized cross-prefab refs. This is the
  Iter-13 runtime-wire rule (extracting chrome nulls a stripped-stub cross-ref); an
  Iter-24 first draft used a serialized `scrollMask` stub and was replaced by the
  `GetComponentInChildren` discovery.
- **A tunable value** (the row cap) → a base C# default that a serialized prefab
  field can still override per variant without touching code — but pick its
  zero-default to mean "off" (see `docs/gotchas.md § Serialized-field zero-default
  sentinel`).
- **What stays per-variant prefab data:** the row-template `maskInteraction = 1` +
  band orders. Templates differ per variant (Sort = option rows, Filter =
  checkbox/header/action rows) and cannot move onto the component-less skeleton.

**Stable section/state key = the loc TERM, not the resolved label.** A persistent UI
state set keyed over sections (Iter-24's collapse closed-set) must key on the loc
**term** (`"ItemChecklist-Filters/SecDiscovery"`), not the localized display string —
a language change re-bakes strings, so a label-keyed set silently loses/mismatches its
entries. Pass the term through and resolve it with `Loc.T(term)` only at render time; a
`static HashSet<string>` on the term is language-change-safe (and needs less code than
a parallel `SectionId` enum). Iter-24 de-resolved `Member.section` from `Loc.T(...)`
back to the raw term for exactly this.

**A layout offset belongs on the window instance position, not duplicated as child
overrides.** A layout decision ("Filter cluster flush to the scrollbar") is a
window-level position → put it on the prefab *instance* in the window, not as
per-child x-overrides inside the variant. Iter-18 offset only the `Display` child and
forgot the `Popup` child — a desync that surfaced only when the popup gained a visible
body (Iter-24). Clean fix: zero the child x-overrides (the variant becomes
structurally identical to Sort) and move the **instance** position in the window by
the same amount.

**Verify every serialized ref against the expected component, not the GO name.**
After each prefab save, check every widget serialized field against the expected
component's GUID/fileID (`utils/prefab_query.py` / GUID grep). Silent wiring gaps
— e.g. `DropdownWidget.rowTemplate == fileID 0`, which makes `EnsurePool`
early-return and the popup renders empty — survive a clean Editor compile **and** a
successful batchmode build; an unwired component "isn't broken," it just "isn't
wired." Companion rule: trust the field→fileID over the GO name (an Iter-18 GO
named `RowTemplate` was actually the filter's `checkboxTemplate`; deleting by name
would have broken it). This is the prefab-YAML form of "verify against reality, not
the name," extending the "Editor compile ≠ sandbox pass" caution.

Deleting an inherited GameObject from a base prefab can leave a variant with a
dangling, target-less override — see `docs/gotchas.md § Dangling prefab-variant
overrides`.

## Documentation Conventions

- **Derive reference tables from the artifact; don't hand-maintain them.** A doc
  table that mirrors generated data (e.g. the sprite catalog in
  `pixel-art-authoring.md`, mirroring `ui_checklist.png.meta`) goes stale the
  moment the artifact changes. Mark such a table as generated and carry the
  one-liner that regenerates it, so a reader refreshes it from the source of
  truth instead of trusting a frozen copy.
