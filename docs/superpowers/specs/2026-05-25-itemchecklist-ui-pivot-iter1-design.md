# ItemChecklist UI Pivot — Iteration 1 Design

**Date:** 2026-05-25
**Status:** Design approved. Pending writing-plans skill invocation.
**Branch:** `initial-impl` (continue forward from HEAD `9197a51`)
**Prerequisite reading:** `docs/research/spike-4-ui-architecture.md`

## Context

Five previous UI pivots on `initial-impl` (commits `7e540b2`…`9197a51`)
attempted variations of uGUI Canvas/Image configurations and all
produced the same set of structural failures: window renders partially
(bottom-left, not centered), 9-slice background does not apply, cursor
appears under the window, WASD input passes through to the player.

Spike #4 established that 0 of 10 successful CK UI mods use uGUI. The
canonical pattern is SpriteRenderer + Unity Layer 5 + `UIelement`
inheritance + `IModUI` interface, mounted via CoreLib's
`UserInterfaceModule` into `UIManager.chestInventoryUI.transform.parent`.
Production references: `limoka/BookMod` and `limoka/DummyMod`
(= "Beating Dummy" from the original mod list).

This iteration is the **first of two** in the pivot:

- **Iteration 1 (this design):** prove the architecture with a minimal
  window — vanilla CraftingUI-theme background plus a single PugText
  title. No item rows, no filter, no search, no scroll.
- **Iteration 2 (future design):** port the existing feature surface
  (filter, search, virtual-scroll item list, item rows) onto the
  validated architecture.

The minimal-first split exists because the previous five pivots each
attempted a full-feature port and could not bisect what specifically
failed. An empty styled window is a cheap, hard go/no-go signal.

## Goals

- Demonstrate that a SpriteRenderer-based window with `UIelement` +
  `IModUI` mounted via CoreLib renders correctly under CK
  (centered, themed background, cursor on top, input blocked).
- Establish the file layout, asmdef references, manifest declarations,
  and code patterns that Iteration 2 builds on without surprise.
- Validate the `Manager.ui.GetCraftingUITheme(...).background` API as
  the source of our 9-slice frame, with a documented fallback to our
  own `ui_classic` atlas sprite.
- Validate the full asset-load and PugText-render pipeline against
  Pugstorm's mod sandbox.

## Non-Goals

- No item rows, no `ItemRow.prefab` rebuild yet (Iter-2).
- No filter bar, no search input, no scroll area (Iter-2).
- No VirtualScrollList port (Iter-2).
- No controller-input testing (Iter-2+).
- No multiplayer testing (mod is client-only by design).
- No new features beyond what existed pre-pivot.

## Decisions Made During Brainstorming

| Question | Decision |
|---|---|
| Pivot scope | **A** — Minimal sanity-check first (this design), then Iter-2 feature port |
| Branch strategy | **A** — Continue forward on `initial-impl` from HEAD `9197a51` |
| Prefab rebuild methodology | **C** — Hybrid: Claude writes YAML skelett, user finishes in Editor |
| Iteration-1 visible content | **B** — Recognizable: theme background + PugText title (no scroll/filter/rows) |
| Old `ui/` file cleanup | **A** — Disconnect from Init path, keep files as Iter-2 reference |

## Architecture

Iteration 1 touches **8 files**. One file is new, six are modified, one
of the modifications happens in a *different* git repo (the shared
CoreKeeperModSDK clone).

| # | File | Operation | Repo |
|---|---|---|---|
| 1 | `CoreKeeperModSDK/Packages/manifest.json` | Modify (add CoreLib git package) | CoreKeeperModSDK |
| 2 | `unity/ItemChecklist/ItemChecklist.asmdef` | Modify (add `"CoreLib"` to references) | item-checklist |
| 3 | `unity/ItemChecklist.asset` (ModBuilderSettings) | Modify (add CoreLib dependency) | item-checklist |
| 4 | `unity/ItemChecklist/ModManifest.json` | Modify (add CoreLib to modDependencies) | item-checklist |
| 5 | `unity/ItemChecklist/ItemChecklistMod.cs` | Modify (LoadSubmodule + ModObjectLoaded + Hotkey + disconnect old spawn) | item-checklist |
| 6 | `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | **New** (IModUI implementation, ~40 LoC) | item-checklist |
| 7 | `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | Replace (uGUI → SpriteRenderer skelett) | item-checklist |
| 8 | `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab.meta` | Replace (only if Unity changes the GUID) | item-checklist |

**Not touched in Iter-1** (deferred to Iter-2 or kept as reference):

- `ItemRow.prefab` — old uGUI prefab; not instantiated in Iter-1.
- `ItemChecklistWindowView.cs`, `UiController.cs`, `ItemRowView.cs`,
  `VirtualScrollList.cs`, `UnityInputFieldAdapter.cs` — old UI code,
  kept as dead-code reference.
- `FilterAndSearchModel.cs`, `DiscoveredState.cs`, `ItemCatalog.cs`,
  `CharacterDataDiscoverySnapshot.cs`, `SaveManagerDiscoveryHook.cs`,
  `SaveManagerActiveSelectHook.cs` — pure mod logic, unchanged.

## Components

### `CoreKeeperModSDK/Packages/manifest.json`

Add one entry to the existing `dependencies` block:

```json
"ck.modding.corelib": "https://github.com/CoreKeeperMods/CoreLib.git?path=/Assets/CoreLibPackage#4.0.4"
```

Version pinned to tag `#4.0.4` per spike-4 recommendation (avoids
`#main` instability). The change affects every mod built against this
SDK clone, but is harmless for mods that don't `using CoreLib`
(disable-durability, faster-talents): the assembly reference becomes
available but unused.

After the manifest edit, Unity Editor must be opened once and allowed
to complete package resolution before the first batchmode build.

### `unity/ItemChecklist/ItemChecklist.asmdef`

Add `"CoreLib"` to the `references` array. Format matches PlacementPlus
asmdef from spike-4 source inspection.

### `unity/ItemChecklist.asset` (ModBuilderSettings)

Add to the `dependencies` block:

```yaml
dependencies:
  - modName: CoreLib
    required: 1
```

This declares CoreLib as a required dependency for the mod.io profile.
On subscription, mod.io's sync pulls CoreLib automatically. Format
verified against `DummyMod.asset` in spike-4.

### `unity/ItemChecklist/ModManifest.json`

Add `"CoreLib"` to the `modDependencies` array. Exact format verified
against `DummyMod/ModManifest.json` at implementation time (may be a
string array or an object with version constraints).

### `unity/ItemChecklist/ItemChecklistMod.cs`

Add imports:

```csharp
using CoreLib;
using CoreLib.Submodule.UserInterface;
```

Add to `EarlyInit()` at the top (before existing bootstrap):

```csharp
CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));
```

Add to `ModObjectLoaded(Object obj)`:

```csharp
if (obj is GameObject go) {
    UserInterfaceModule.RegisterModUI(go);
}
```

Replace the existing hotkey path in `Update()` (or wherever it lives)
with:

```csharp
if (F1Pressed && !Manager.menu.IsAnyMenuActive() &&
    !Manager.input.textInputIsActive &&
    !ReferenceEquals(Manager.input.activeInputField, Manager.ui.chatWindow))
{
    UserInterfaceModule.OpenModUI("ItemChecklist:Window");
}
```

The hotkey is **F1**, preserved from commit `21a2e75`. The
hotkey-guard (`IsAnyMenuActive` + `textInputIsActive` + `chatWindow`)
is preserved unchanged.

**Disconnect:** the old `UiController.BuildUi()` / Instantiate logic
in the Init pathway is commented out (kept in source for Iter-2
reference, but not executed at runtime).

### `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (new)

```csharp
using CoreLib.Submodule.UserInterface.Interface;
using UnityEngine;

namespace ItemChecklist.UI
{
    public class ItemChecklistWindow : UIelement, IModUI
    {
        public GameObject root;
        public SpriteRenderer background;
        public PugText title;

        public GameObject Root => root;
        public bool ShowWithPlayerInventory => false;
        public bool ShouldPlayerCraftingShow => false;

        protected void Awake()
        {
            HideUI();
        }

        public void ShowUI()
        {
            root.SetActive(true);
            ApplyTheme();
        }

        public void HideUI()
        {
            root.SetActive(false);
        }

        private void ApplyTheme()
        {
            var theme = Manager.ui.GetCraftingUITheme("Wood");
            if (theme != null && background != null)
                background.sprite = theme.background;

            if (title != null)
                title.Render("Item Checklist");
        }
    }
}
```

`Awake`/`ShowUI`/`HideUI` are taken 1:1 from `DummyUI.cs`. Theme name
is a placeholder — the actual `GetCraftingUITheme` signature is
verified before implementation (may be enum, may have a different set
of theme names).

### `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (replace)

YAML skelett structure:

```
ItemChecklistWindow                  [Layer 5, Root GameObject]
├── Components:
│   ├── Transform                    (localPosition 0,0,0)
│   ├── ModUIAuthoring               (modInterfaceID = "ItemChecklist:Window";
│   │                                 initialInterfacePosition = 0,0,10)
│   ├── ItemChecklistWindow          (MonoBehaviour, fields wired to children:
│   │                                 root → "root" child,
│   │                                 background → "Background" GO,
│   │                                 title → "Title" GO)
│   └── (UIelement inherited fields, defaults)
│
└── root                             [child GameObject named exactly "root"]
    ├── Components:
    │   └── Transform                (localPosition 0,0,0)
    │
    ├── Background                   [child]
    │   ├── Components:
    │   │   ├── Transform            (localPosition 0,0,0)
    │   │   └── SpriteRenderer       (sprite = EDITOR-TODO,
    │   │                             drawMode = Sliced,
    │   │                             sortingLayer = UI,
    │   │                             sortingOrder = 10)
    │   └── (no children)
    │
    └── Title                        [child]
        ├── Components:
        │   ├── Transform            (localPosition ~ 0, +3, 0; top of window)
        │   └── PugText              (textString = "Item Checklist",
        │                             style = EDITOR-TODO fontFace + alignment,
        │                             sortingLayer = UI,
        │                             sortingOrder = 20)
        └── (no children)
```

**Editor-finish tasks (handed off to user — these are deliberate
hand-off markers, not unresolved design questions):**

- Assign `Background.SpriteRenderer.sprite` (from vanilla theme or our
  Bridge atlas, decided based on theme-API behavior).
- Set `Background.SpriteRenderer.size` (Sliced draw-mode size in
  world units).
- Set `Title.PugText.style.fontFace` and other style fields (copy from
  a vanilla title for consistency).
- Verify pixel-snap on both sprites; add `PixelSnap` component if
  needed.

## Data Flow

### Mod load + UI registration

```
Game launch
  → Pugstorm loader compiles ItemChecklist + CoreLib via Roslyn
  → IMod.EarlyInit() runs:
      - CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule))
          → UserInterfaceModule.SetHooks() applies UIManagerPatch
      - existing bootstrap (AssetBundle ref, Discovery state)
  → Pugstorm loader iterates AssetBundle assets:
      - For each loaded object: IMod.ModObjectLoaded(obj)
          - If GameObject → UserInterfaceModule.RegisterModUI(go)
              - GetComponent<ModUIAuthoring>() succeeds for our window prefab
              - Stored in interfacePrefabs list
              - CoreLib has NOT instantiated anything yet
```

### Window mount (deferred until UIManager exists)

```
Main menu → World load
  → UIManager.Init() runs (vanilla)
  → CoreLib's UIManagerPatch.OnInit (postfix) fires:
      - uiTransform = chestInventoryUI.transform.parent
      - Object.Instantiate(ItemChecklistWindow prefab, uiTransform)
      - localPosition = (0, 0, 10) from ModUIAuthoring default
      - Stored in modInterfaces["ItemChecklist:Window"]
      - Awake() runs on the instance → HideUI() → root.SetActive(false)
```

### Hotkey open

```
Player presses hotkey (F1)
  → IMod.Update() — our code:
      - Guard: not in menu, not in text input, not in chat
      - UserInterfaceModule.OpenModUI("ItemChecklist:Window")
          → UserInterfaceModule.OpenModUI(null, Entity.Null, "ItemChecklist:Window")
          → lookup modInterfaces dict
          → modUI.ShowUI() — our code:
              - root.SetActive(true)
              - ApplyTheme():
                  - theme = Manager.ui.GetCraftingUITheme("Wood")
                  - background.sprite = theme.background (if non-null)
                  - title.Render("Item Checklist")
          → currentInterface = modUI

Vanilla CK reacts to currentInterface != null:
  - UIManager.isAnyInventoryShowing → true (via CoreLib postfix)
  - UIMouse shows cursor (because isAnyInventoryShowing)
  - PlayerController.isInteractionBlocked → true (vanilla logic)
  - WASD blocked, items cannot be used
  - UIMouse Physics-Raycast in Layer 5 finds our SpriteRenderer
    (collider question is in Section "Error Handling")
```

### Escape close

```
Player presses Escape
  → Vanilla UIManager.HideAllInventoryAndCraftingUI() runs
  → CoreLib's UIManagerPatch.OnHide (postfix) fires:
      - For each registered ModUI: modUI.HideUI()
          → our code: root.SetActive(false)
      - ClearModUIData() → currentInterface = null

isAnyInventoryShowing → false
  - Cursor returns to game mode
  - WASD re-enabled
  - Window invisible
```

### What we do not patch

| Behavior | Source |
|---|---|
| Cursor visible during open | Vanilla CK via `isAnyInventoryShowing` postfix |
| WASD blocked during open | Vanilla CK via `isAnyInventoryShowing` postfix |
| Escape closes window | Vanilla CK → CoreLib `HideAllInventoryAndCraftingUI` postfix |
| Hotbar update | Vanilla CK (no patch needed) |

## Error Handling

| # | Risk | Trigger | Symptom | Handling |
|---|---|---|---|---|
| 1 | CoreLib not in SDK manifest | Build time | `CS0246: 'CoreLib' not found` in Editor compile | Build fails — caught before play test. Fix manifest, refresh Editor. |
| 2 | Sandbox blocks `Manager.ui.GetCraftingUITheme` or `theme.background` access | Mod load (Roslyn compile) | `CompileFailed` in `Player.log`, mod does not load | **Fallback in code:** wrap `GetCraftingUITheme` call in try/catch (or null-check); on failure, load sprite from our own ui_classic atlas via `ItemChecklistMod.AssetBundle.LoadAsset<Sprite>("ui_panel")` (verified working in pre-pivot commits). |
| 3 | Background sprite is null despite theme lookup | Show time | Window opens but appears empty | `Log.LogWarning` in `ApplyTheme`; window still visible (title text floats with no frame). Diagnose at next iteration. |
| 4 | Sprite PPU ≠ 16 | Show time | Window renders at wrong scale or blurry | **Pre-test verification:** check `.meta` files of all sprites used by the window. If wrong, re-import with PPU=16 + 1/16 grid snap. |
| 5 | UIMouse Physics-Raycast finds nothing (missing collider) | Show time | Window renders but cursor/WASD pass through | **Pre-YAML verification:** inspect `DummyUI.prefab` and `BookUI.prefab` for `BoxCollider2D` on root or children. If present, include in our skelett. If absent, `UIelement` inheritance alone is sufficient. |
| 6 | Old `ItemRow.prefab` still in AssetBundle; `RegisterModUI` called for it | Load time | `RegisterModUI` finds no `ModUIAuthoring` and returns silently | No action needed (`UserInterfaceModule.RegisterModUI` guards with early return on null). Cleanup deferred to Iter-2. |
| 7 | Disconnected old UiController code accidentally still invoked | Load time | Duplicate window, conflict | Disconnect by removing Init pathway in `ItemChecklistMod.cs`. Old files compile but aren't called. Verify in smoke test. |
| 8 | CoreLib version mismatch with other installed mods (e.g. mod requires CoreLib 3.x) | mod.io subscription time | End-user mod conflict | Not relevant for Iter-1 (dev only). For publishing: declared via `required: 1`, mod.io resolves. |
| 9 | Hotkey triggers while vanilla inventory open | Gameplay | Double window | Hotkey guard from commit `21a2e75` preserved (`IsAnyMenuActive` + `textInputIsActive` + chat-window check). |

## Prerequisites (verification tasks before YAML writing)

These are not error-recovery actions — they are inputs required before
the implementation can start. Each resolves one open question in the
component design above.

1. Inspect `DummyUI.prefab` and `BookUI.prefab` for `BoxCollider2D`
   on the root or any child (decides whether our skelett needs one —
   ties to Risk #5).
2. Verify `Manager.ui.GetCraftingUITheme` signature: string parameter
   or enum? what are the valid theme names? (decides the `ApplyTheme`
   call shape; the `"Wood"` literal in the code sketch is a
   placeholder pending this check).
3. Inspect a vanilla `CraftingUI` prefab to determine a sensible
   initial `SpriteRenderer.size` for our background in world units.
4. Inspect `DummyMod/ModManifest.json` to confirm the exact JSON shape
   of the `modDependencies` declaration (string array vs. object with
   version).

## Testing

### Build + install pipeline

```bash
cd item-checklist
source .envrc
../utils/build.sh    # Unity batchmode build via CLIBuildHelper,
                     # auto-runs install-macos.sh at the end,
                     # populates the 3 Fake-ID locations in CrossOver bottle
# Launch CK manually via CrossOver launcher or Steam-CrossOver integration
```

**Pre-build (one-time after manifest.json change):** open Unity Editor,
wait for package resolution, close Editor. Then first batchmode build.

### Test phases with hard acceptance criteria

#### Phase 1 — Sandbox compile check (title screen)

Goal: prove our code passes Pugstorm's Roslyn sandbox.

Acceptance:
- ✅ Title screen reached without `TitleMenuIncompatibleModWarning`
- ✅ `Player.log` contains `[Item Checklist]: Mod version: …`
- ✅ `Player.log` contains no `CompileFailed` for ItemChecklist
- ✅ `Player.log` contains no `CompileFailed` for CoreLib or other mods
  (cascade check; see `project_corekeeper_compile_fail_cascade`)

Fail handling: stop, diagnose via `Player.log`. Common causes:
- CoreLib not resolved — open Editor, wait for resolution, rebuild
- Sandbox block on `GetCraftingUITheme` — add try/catch + atlas fallback
- Sandbox block on a CoreLib API — fall back to DIY (Option B) — but
  unlikely per spike-4 evidence (BookMod and DummyMod use identical
  APIs in production)

#### Phase 2 — World load + registration

Acceptance:
- ✅ `Player.log` contains `Registering ItemChecklist:Window Modded UI!`
  (CoreLib's RegisterModUI log)
- ✅ `Player.log` contains no errors from our code
- ✅ WASD moves the character normally (negative test: window not yet
  active, input must not be blocked)

#### Phase 3 — Hotkey trigger + visual check (the architecture validation)

Press hotkey (F1).

Acceptance:
- ✅ Window appears **centered** (not bottom-left, not tiny-centered)
- ✅ Window shows a **visible 9-slice background** (wood frame or
  fallback theme — not grey box)
- ✅ Title **"Item Checklist"** legible in **CK pixel font** (not
  default Unity font, not missing-character boxes)
- ✅ Cursor **appears** and is **on top of the window** (window does
  not occlude cursor)
- ✅ WASD **no longer moves the player** (input block active)
- ✅ Mouse click on the window causes no in-game action

Fail handling: bisect using risk table in "Error Handling" (Risks 3-5
are the common causes).

#### Phase 4 — Close + reopen

```
Press Escape → press hotkey again → press Escape again → move player
```

Acceptance:
- ✅ Escape closes the window
- ✅ Cursor disappears, WASD re-enabled
- ✅ Second hotkey press reopens the window, same appearance
- ✅ Second Escape closes, no sticky state

### Definition of Done for Iteration 1

All four phases pass + commit pushed. Then:

- Update memory `item_checklist_ui_pivot_state` to "Iter-1 done,
  Iter-2 pending"
- Update spike-4 doc status to "Iter-1 validated; architecture
  confirmed correct"
- Invoke writing-plans skill for Iter-2 (feature port: ItemRow,
  VirtualScrollList, FilterBar, SearchInput)

### Failure-mode stop condition

An "attempt" = one round-trip of: code change → rebuild → install →
launch CK → observe `Player.log` → diagnose.

If Phase 1 (sandbox compile check) does not pass after **3 attempts**
(e.g., persistent sandbox block on a CoreLib API that we cannot work
around): **stop and re-brainstorm**. That would indicate CoreLib is
not the right path and we should pivot to Option B (DIY SpriteRenderer).
Unlikely per spike-4 evidence (BookMod and DummyMod use the same APIs
in production), but the budget cap exists to avoid open-ended chasing.

## Iteration 2 scope (deferred)

Documented here so future-me knows what's pending:

- Rebuild `ItemRow.prefab` as SpriteRenderer + Layer 5
- Port `ItemRowView.cs` to `ItemRow : MonoBehaviour` (no UIelement
  needed for sub-rows, but verify)
- Port `VirtualScrollList.cs` — investigate whether the recycler
  pattern survives the uGUI→SpriteRenderer transition (positions are
  manual in both, so likely yes)
- Add `rowsRoot` and `filterBar` containers to the window prefab
- Implement search input — investigate CK's native input field
  primitives (likely `TextInputInterface`-compatible)
- Wire filter dropdown
- Delete `UnityInputFieldAdapter.cs` (already unused)
- Delete or rewrite `UiController.cs` (replaced by direct
  Window-class methods)

## References

- `docs/research/spike-4-ui-architecture.md` — research that led to
  this design
- `[[item-checklist-ui-pivot-state]]` — memory snapshot of pivot
  state (will be updated on Iter-1 done)
- `[[corekeeper-ui-pattern]]` — generic CK UI pattern memory
- `[[project_pugstorm_sandbox_rules]]` — sandbox constraints
- `[[project_corekeeper_compile_fail_cascade]]` — Player.log diagnostic
  paths and mod-mod interaction notes
- Reference codebase: `limoka/DummyMod` (= Beating Dummy) at
  `/tmp/ck-ui-research/limoka-CoreKeeperMods/SDK Mods/Assets/Mods/DummyMod/`
  (during research; re-clone via spike-4 source links)
- CoreLib UserInterface docs:
  `/tmp/ck-ui-research/corekeepermods.github.io/v4/modules/user-interface/README.md`
