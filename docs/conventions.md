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

**superpowers specs/plans live only in the worktree.** The
`superpowers:brainstorming` / `writing-plans` skills default their spec and
plan output to `docs/superpowers/`, which is **gitignored** in this repo (to
avoid colliding with that default location). Because the worktree's
`docs/superpowers/` is not tracked, the per-iter spec and plan must be copied
back to the main checkout **before** `git worktree remove` destroys the
worktree — otherwise they are lost. Treat copying `docs/superpowers/specs/`
and `docs/superpowers/plans/` to main as part of worktree teardown.

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

### Throwaway test-scaffold pattern

To accelerate a manual in-game test, it is acceptable to layer a *throwaway*
debug scaffold *in front of* the reviewed core — then remove it via
`git restore` before merge so the merged code stays byte-identical to the
reviewed version.

Iter-3.8 example: reaching the end of the ~10720-entry list by scrolling takes
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

**Recovery:** if Core Keeper hangs on the loading screen (quit-deadlock in
`ModManager` — symptom: `Exit blocked by ModManager` in Player.log), use:

```bash
pkill -KILL -f "Core Keeper"   # GNU pgrep/pkill -f variant; macOS-safe
```

Do not use the normal quit path — it blocks and the process must be killed.

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
    IPopupToggle.cs               toggle-owner seam shared by dropdown + facet toggles (Iter-13)
    DropdownToggleButton.cs       dropdown header toggle button (Iter-7)
    DropdownOptionButton.cs       dropdown popup option button (Iter-7)
    AscDescToggle.cs              ascending/descending direction toggle (Iter-7)
    SearchBar.cs                  TextInputField subclass — name search (Iter-8)
    ClearSearchButton.cs          clears the search field (Iter-8)
    ItemCategory.cs               category taxonomy (ObjectType -> bucket) (Iter-10)
    FacetedFilterWidget.cs        sectioned multi-select filter dropdown (Iter-10)
    FacetCheckboxButton.cs        filter checkbox row button (Iter-10)
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
  Editor/
    ItemChecklist.Editor.asmdef   editor-only assembly for the CLI helpers
    CLIBuildHelper.cs             -> core_keeper/utils/ (shared symlink, gitignored)
    CLIPublishHelper.cs           -> core_keeper/utils/ (shared symlink, gitignored)
    LocalizationGenerator.cs      -> core_keeper/utils/ (shared symlink, gitignored)
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

**Naming patterns:**

- `*Hook.cs` — top-level Harmony patches
- `*Patch.cs` — top-level Harmony patches (toggle/suppression behaviour)
- `*Snapshot.cs` — top-level initial-state readers
- `ui/*.cs` — IModUI, IScrollable, and row logic
- `Editor/*.cs` — editor-only build/publish helpers (in separate `.asmdef`)

## Prefab Authoring Conventions

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
