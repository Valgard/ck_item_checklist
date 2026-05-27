# ItemChecklist UI Pivot — Iter-3.5b Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add viewport-clipping to the ItemChecklist UI by porting IB's `SpriteMask` + Sorting-Layer-Custom-Range mechanism 1:1, runtime-materialized — rows that scroll out of the wood-theme rectangle become invisible, while Title/Background and other UI remain unaffected.

**Architecture:** New static helper `ContentsMaskInstaller` owns mask creation (one-shot per window) and per-row `MaskInteraction` propagation (mirrors `ItemBrowserRegistry.AddEntryDisplay`). Two hooks added to `ItemChecklistWindow`: one in `Awake()` (install mask), one in `SpawnRows()` (set `VisibleInsideMask` on every freshly-instantiated row). Mask geometry is runtime-derived from `UIScrollWindow.windowWidth/Height/LocalCenter`; the 1×1 white-rect mask sprite is generated in-memory via `Texture2D` + `Sprite.Create` (no asset roundtrip).

**Tech Stack:** C# (Unity 6000.0.59f2), PugMod RoslynCSharp sandbox, Harmony 2.x (unchanged for this iter), CoreLib 4.0.3 runtime + 4.0.4 SDK build-time, ILSpyCmd CLI for Phase-0 decompile-check, `../../../utils/build.sh` Unity batchmode pipeline, `../../../utils/install-macos.sh` fake-ID dev-install.

**Reference spec:** `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5b-design.md` (commit `f557f80`)

**Memory anchors:** [[reference-analysis-mandatory-when-provided]], [[deep-spike-unfamiliar-internals]], [[subagent-build-verify-install]], [[worktree-remove-preflight-check]], [[frequent-wip-commits-for-bisect]], [[pugstorm-sandbox-rules]], [[pugstorm-modbuilder-sprite-meta]], [[corekeeper-compile-fail-cascade]]

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `.worktrees/iter-3-5b/` | Create (worktree) | Isolated workspace for Iter-3.5b |
| `.worktrees/iter-3-5b/.envrc` | Create (copy from main) | Env vars for build (gitignored, must copy) |
| `unity/ItemChecklist/ui/ContentsMaskInstaller.cs` | Create | Static helper: install `ContentsMask` + `SpriteMask`; per-row `MaskInteraction` recursion; cached 1×1 white sprite |
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | Modify | +Awake-hook (install mask), +SpawnRows-hook (set MaskInteraction on instantiated row), +`_contentsMask` field |
| `docs/research/spike-5-uiscrollwindow-decompile.md` | Modify | Append section closing the "partial — 991 KB" prefab-grep gap (ContentsMask + AddEntryDisplay-Hook findings) |

**Unchanged:** `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`, `unity/ItemChecklist/Prefabs/ItemRow.prefab`, `unity/ItemChecklist/ui/ItemRow.cs`, `unity/ItemChecklist/ui/ItemChecklistContent.cs`.

---

## Phase 0 — Pre-Flight & Worktree

### Task 0: Worktree-Setup + Dependencies-Check

**Files:**
- Create: `.worktrees/iter-3-5b/`
- Create: `.worktrees/iter-3-5b/.envrc`
- Verify: `ilspycmd` on PATH
- Verify: bridge-sprites in main repo (gitignored, must persist)

- [ ] **Step 1: Verify CK + Unity Editor not running** (Unity batchmode build requires both closed)

```bash
pgrep -f "Core Keeper" || echo "ck-not-running"
pgrep -f "Unity.app/Contents/MacOS/Unity" || echo "editor-not-running"
```

If either is running: STOP and ask the user to close the application before continuing. Never `pkill` Unity Editor without explicit user permission — risk of losing unsaved Editor state.

- [ ] **Step 2: Create worktree for `iter-3-5b`**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git worktree add .worktrees/iter-3-5b -b iter-3-5b
ls .worktrees/iter-3-5b
```

Expected: directory contains tracked files. Branch `iter-3-5b` is created from `main` HEAD (`f557f80` after spec commit).

- [ ] **Step 3: Copy `.envrc` into the new worktree** (gitignored; missing in fresh worktrees by default)

```bash
cp /Users/valgard/Projects/private/core_keeper/item-checklist/.envrc \
   /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b/.envrc
ls -la /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b/.envrc
```

Expected: `.envrc` present in worktree. Without it, builds fail silently per [[subagent-build-verify-install]].

- [ ] **Step 4: Verify bridge-sprites persist in main repo** (Art/Bridge/ is gitignored but must exist for `link.sh` symlinks)

```bash
ls /Users/valgard/Projects/private/core_keeper/item-checklist/unity/ItemChecklist/Art/Bridge/
```

Expected: at least `ui_classic.png`, `ui_stone.png`, `ui_unknown_item.png`. If missing: STOP, BLOCKED.

- [ ] **Step 5: Verify `ilspycmd` available**

```bash
which ilspycmd || brew install ilspycmd
ilspycmd --version
```

Expected: `ICSharpCode.Decompiler 10.x` or similar. Used in Task 1 for the public-field check.

- [ ] **Step 6: Commit Pre-Flight checkpoint** (no code change yet; commit only if `.worktrees/` was modified by previous tasks — typically nothing to commit at this point)

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
git status
```

If nothing staged: skip commit. The worktree creation is recorded by git itself.

---

### Task 1: ILSpyCmd Pre-Flight — UIScrollWindow public-access verify

**Files:**
- Output: `/tmp/iter-3-5b-spike/UIScrollWindow.cs` (decompiled)
- No code changes in repo.

- [ ] **Step 1: Locate `Pug.Other.dll`** (contains UIScrollWindow)

```bash
ls "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/Managed/Pug.Other.dll"
```

Expected: file exists. If missing, locate via `find` in the bottle's `Managed/` directory.

- [ ] **Step 2: Decompile UIScrollWindow class**

```bash
mkdir -p /tmp/iter-3-5b-spike
ilspycmd -t UIScrollWindow -o /tmp/iter-3-5b-spike \
  "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/Managed/Pug.Other.dll"
ls /tmp/iter-3-5b-spike/
```

Expected: file `UIScrollWindow.cs` exists (~350 lines). If `ilspycmd` returns multiple output paths, pick the one containing `UIScrollWindow`.

- [ ] **Step 3: Verify public access for the three serialized fields**

```bash
grep -nE '\bwindowHeight\b|\bwindowWidth\b|\bwindowLocalCenter\b' /tmp/iter-3-5b-spike/UIScrollWindow.cs
```

Expected: three lines like `public float windowHeight;`, `public float windowWidth;`, `public Vector2 windowLocalCenter;` (or equivalent with property accessors). If they're `private`: continue to Step 4 for the Reflection fallback decision; otherwise skip Step 4.

- [ ] **Step 4: Decide Public-vs-Reflection** (conditional — only if Step 3 showed private fields)

If fields are private: add the following to `ContentsMaskInstaller.cs` plan (Task 3, Step 1) — add three more `MemberInfo` slots:

```csharp
private static readonly MemberInfo MiWindowHeight = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "windowHeight");
private static readonly MemberInfo MiWindowWidth  = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "windowWidth");
private static readonly MemberInfo MiWindowCenter = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "windowLocalCenter");
```

And replace direct access with `(float)API.Reflection.GetValue(MiWindowHeight, scrollWindow)` etc. in Install(). If they're public (the expected case): no plan changes, proceed with direct access in Task 4.

- [ ] **Step 5: Record finding** in scratch notes (no commit)

```bash
echo "UIScrollWindow public-field check: PASS / FAIL  (date: $(date +%Y-%m-%d))" >> /tmp/iter-3-5b-spike/preflight-notes.txt
```

Used as scratchpad for Task 4 implementation decision.

---

### Task 2: Sorting-Layer + Sorting-Order Pre-Flight

**Files:**
- Read-only: `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`, `unity/ItemChecklist/Prefabs/ItemRow.prefab`
- No code changes.

- [ ] **Step 1: Check Window-Prefab Sorting-Order of Background + Title**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
grep -nE 'm_SortingLayer|m_SortingOrder|m_Name:' unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
```

Expected: each `SpriteRenderer` and `PugText` block lists its `m_SortingLayer` (named) and `m_SortingOrder` (int). Record:
- Background SpriteRenderer order → must be **outside** the `40..55` mask custom-range
- Title PugText order → must be **outside** the `40..55` mask custom-range

If Background or Title order falls inside `40..55`, STOP — the mask would clip them too. Choose a different custom-range (e.g., `60..70`) and update Task 4 accordingly.

- [ ] **Step 2: Check ItemRow-Prefab Sorting-Order**

```bash
grep -nE 'm_SortingLayer|m_SortingOrder|m_Name:' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: row sprites have orders that fall **inside** the `40..55` range (so the mask can clip them). If outside, either adjust row orders via prefab (Editor-roundtrip — costly) OR set them at runtime in `SetMaskInteractionRecursive` (cheap; preferred fallback).

- [ ] **Step 3: Verify CK has a named Sorting-Layer "UI"**

This is checked at runtime in Phase 1 — `SortingLayer.NameToID("UI")`. Cannot be checked statically; record as expected-positive and check Player.log in Phase 1.

- [ ] **Step 4: Record findings in scratch notes**

```bash
echo "Background order:   <fill>"  >> /tmp/iter-3-5b-spike/preflight-notes.txt
echo "Title order:        <fill>"  >> /tmp/iter-3-5b-spike/preflight-notes.txt
echo "Row orders:         <fill>"  >> /tmp/iter-3-5b-spike/preflight-notes.txt
```

---

## Phase 1 — Implementation (Code Tasks)

### Task 3: Create `ContentsMaskInstaller.cs` — Skeleton + Static Sprite Cache

**Files:**
- Create: `unity/ItemChecklist/ui/ContentsMaskInstaller.cs`

- [ ] **Step 1: Write the new file with skeleton + sprite-cache logic**

```csharp
using UnityEngine;

namespace ItemChecklist.UI
{
    // Static helper for installing a SpriteMask (the "ContentsMask") as a
    // sibling of the scrolling content and propagating VisibleInsideMask to
    // every SpriteRenderer + PugText on a freshly-spawned row.
    //
    // Pattern ported 1:1 from ItemBrowser:
    //   - ContentsMask: sibling of scrollingContent, sized to the window viewport
    //   - Custom Sorting-Layer range (40..55) selects which renderers are clipped
    //   - Per-Row MaskInteraction set at instantiate time (ItemBrowserRegistry.AddEntryDisplay)
    public static class ContentsMaskInstaller
    {
        private const float MaskBufferUnits = 0.5f;
        private const int   MaskFrontOrder  = 55;
        private const int   MaskBackOrder   = 40;
        private const float MaskAlphaCutoff = 0.2f;

        private static Texture2D _whiteTex;
        private static Sprite    _whiteSprite;

        public static SpriteMask Install(Transform anchor, UIScrollWindow scrollWindow)
        {
            return null; // implemented in Task 4
        }

        public static void SetMaskInteractionRecursive(GameObject row, SpriteMaskInteraction mode)
        {
            // implemented in Task 5
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;

            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            _whiteSprite = Sprite.Create(
                _whiteTex,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);

            return _whiteSprite;
        }
    }
}
```

- [ ] **Step 2: Build to verify skeleton compiles in the sandbox**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
source .envrc
../../utils/build.sh
```

Expected: `BUILD SUCCEEDED`. The Roslyn sandbox will compile in Phase-1 testing — here the build only validates the Unity-side AssetBundle build and managed assembly.

- [ ] **Step 3: Verify install propagation** (per [[subagent-build-verify-install]])

```bash
grep -l 'ContentsMaskInstaller' "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999999_1/Scripts/*.cs
```

Expected: at least one match (the new file is in the installed Scripts directory). If 0 matches: build did NOT install — investigate before proceeding.

- [ ] **Step 4: Commit WIP**

```bash
git add unity/ItemChecklist/ui/ContentsMaskInstaller.cs unity/ItemChecklist/ui/ContentsMaskInstaller.cs.meta
git commit -m "wip(ui): add ContentsMaskInstaller skeleton + static sprite cache"
```

Note: the `.meta` file is auto-generated by the Unity Editor on first build — if it doesn't exist yet, omit it from `git add` and add it in the next WIP commit.

---

### Task 4: Implement `ContentsMaskInstaller.Install()` — Mask Setup

**Files:**
- Modify: `unity/ItemChecklist/ui/ContentsMaskInstaller.cs:21-24` (the `Install` body)

- [ ] **Step 1: Replace the Install method body with the full implementation**

```csharp
public static SpriteMask Install(Transform anchor, UIScrollWindow scrollWindow)
{
    if (anchor == null || scrollWindow == null) return null;

    var go = new GameObject("ContentsMask");
    go.layer = 5; // UI layer (Unity built-in)
    go.transform.SetParent(anchor, worldPositionStays: false);

    // Window bounds — Pre-Flight (Task 1) verified these are public serialized fields.
    float w = scrollWindow.windowWidth;
    float h = scrollWindow.windowHeight;
    Vector2 center = scrollWindow.windowLocalCenter;

    go.transform.localPosition = new Vector3(center.x, center.y, 0f);
    go.transform.localScale    = new Vector3(w + MaskBufferUnits, h + MaskBufferUnits, 1f);

    var mask = go.AddComponent<SpriteMask>();
    mask.sprite              = GetOrCreateWhiteSprite();
    mask.isCustomRangeActive = true;

    // Sorting-Layer: prefer named "UI"; fallback Default (0) so the mask still works
    // as long as rows live in the same SortingLayer.
    int uiLayerID = SortingLayer.NameToID("UI");
    if (uiLayerID == 0)
    {
        // NameToID returns 0 for both "Default" and unknown names — log so we know.
        Debug.Log("[ItemChecklist] SortingLayer 'UI' not found; falling back to Default (id 0). " +
                  "Clipping still functional if rows share this layer.");
    }
    mask.frontSortingLayerID = uiLayerID;
    mask.frontSortingOrder   = MaskFrontOrder;
    mask.backSortingLayerID  = uiLayerID;
    mask.backSortingOrder    = MaskBackOrder;
    mask.alphaCutoff         = MaskAlphaCutoff;

    return mask;
}
```

- [ ] **Step 2: Build + verify install**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
../../utils/build.sh
grep -c 'ContentsMaskInstaller.Install' "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999999_1/Scripts/ContentsMaskInstaller.cs
```

Expected: BUILD SUCCEEDED; grep returns ≥ 1 (Install method in installed file).

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/ui/ContentsMaskInstaller.cs
git commit -m "wip(ui): implement ContentsMaskInstaller.Install (GameObject + SpriteMask + custom-range)"
```

---

### Task 5: Implement `ContentsMaskInstaller.SetMaskInteractionRecursive()`

**Files:**
- Modify: `unity/ItemChecklist/ui/ContentsMaskInstaller.cs` (the `SetMaskInteractionRecursive` body)

- [ ] **Step 1: Replace the method body with the full implementation**

```csharp
public static void SetMaskInteractionRecursive(GameObject row, SpriteMaskInteraction mode)
{
    if (row == null) return;

    // Mirror ItemBrowserRegistry.AddEntryDisplay (Z66-77): set MaskInteraction on
    // all SpriteRenderer + PugText components in the row's hierarchy. Passing
    // includeInactive=true so newly-instantiated rows hidden behind clipping
    // still get the right interaction mode.
    foreach (var sr in row.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
        sr.maskInteraction = mode;

    foreach (var pt in row.GetComponentsInChildren<PugText>(includeInactive: true))
    {
        // Defensive null-guard on style. IB doesn't have this guard, but in our
        // sandbox a NRE here triggers a CompileFailed cascade per
        // [[corekeeper-compile-fail-cascade]] — guard wins out.
        if (pt.style != null)
            pt.style.maskInteraction = mode;
    }
}
```

- [ ] **Step 2: Build + verify install**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
../../utils/build.sh
grep -c 'SetMaskInteractionRecursive' "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999999_1/Scripts/ContentsMaskInstaller.cs
```

Expected: BUILD SUCCEEDED; grep ≥ 1.

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/ui/ContentsMaskInstaller.cs
git commit -m "wip(ui): implement SetMaskInteractionRecursive (SpriteRenderer + PugText.style loop)"
```

---

### Task 6: Wire `ItemChecklistWindow.Awake()` — install mask

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs:18-31` (add `_contentsMask` field + extend `Awake()`)

- [ ] **Step 1: Add the `_contentsMask` field next to `_spawnedRows`**

Find the existing line:
```csharp
private readonly System.Collections.Generic.List<ItemRow> _spawnedRows = new System.Collections.Generic.List<ItemRow>();
```

Add immediately after:
```csharp
private SpriteMask _contentsMask;
```

- [ ] **Step 2: Extend `Awake()` to install the mask**

Find the existing block:
```csharp
protected void Awake()
{
    HideUI();
}
```

Replace with:
```csharp
protected void Awake()
{
    HideUI();

    // Install ContentsMask once per window-Awake. Anchor at rowsContent.parent
    // (= RowsContainer): sibling of the scrolling Content, so the mask stays
    // stationary while content scrolls underneath. Mirrors IB's
    // ScrollAnchor → [ContentsMask, Scroll] hierarchy.
    if (scrollWindow != null && rowsContent != null && rowsContent.parent != null)
        _contentsMask = ContentsMaskInstaller.Install(rowsContent.parent, scrollWindow);
}
```

- [ ] **Step 3: Build + verify install**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
../../utils/build.sh
grep -c '_contentsMask = ContentsMaskInstaller.Install' "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999999_1/Scripts/ItemChecklistWindow.cs
```

Expected: BUILD SUCCEEDED; grep ≥ 1.

- [ ] **Step 4: Commit WIP**

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "wip(ui): install ContentsMask in ItemChecklistWindow.Awake"
```

---

### Task 7: Wire `ItemChecklistWindow.SpawnRows()` — set MaskInteraction per row

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs:78-88` (inside `SpawnRows()` for-loop)

- [ ] **Step 1: Add the per-row hook inside the for-loop**

Find the existing block:
```csharp
for (int i = 0; i < catalog.Count; i++)
{
    var entry = catalog.GetByIndex(i);
    var go = Object.Instantiate(rowPrefab, rowsContent);
    go.transform.localPosition = new Vector3(0, y, 0);
    var row = go.GetComponent<ItemRow>();
    if (row != null)
        row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, state.IsDiscovered(entry.ObjectId));
    _spawnedRows.Add(row);
    y -= ItemRow.RowHeight;
}
```

Replace with:
```csharp
for (int i = 0; i < catalog.Count; i++)
{
    var entry = catalog.GetByIndex(i);
    var go = Object.Instantiate(rowPrefab, rowsContent);
    go.transform.localPosition = new Vector3(0, y, 0);

    // Mark the freshly-instantiated row as clipped by the ContentsMask.
    // Mirrors ItemBrowserRegistry.AddEntryDisplay (Z66-77): all
    // SpriteRenderer + PugText in the row hierarchy become VisibleInsideMask.
    ContentsMaskInstaller.SetMaskInteractionRecursive(go, SpriteMaskInteraction.VisibleInsideMask);

    var row = go.GetComponent<ItemRow>();
    if (row != null)
        row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, state.IsDiscovered(entry.ObjectId));
    _spawnedRows.Add(row);
    y -= ItemRow.RowHeight;
}
```

- [ ] **Step 2: Build + verify install**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
../../utils/build.sh
grep -c 'SetMaskInteractionRecursive(go, SpriteMaskInteraction.VisibleInsideMask)' "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999999_1/Scripts/ItemChecklistWindow.cs
```

Expected: BUILD SUCCEEDED; grep ≥ 1.

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "wip(ui): apply VisibleInsideMask on row instantiation in SpawnRows"
```

---

## Phase 2 — Test Cycles (7 Phases per Spec)

### Task 8: Phase 1 — Sandbox Compile

**Files:** None modified. Test only.

- [ ] **Step 1: Truncate Player.log + Player-prev.log** (to isolate this cycle's output)

```bash
LOG_DIR="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
: > "$LOG_DIR/Player.log"
: > "$LOG_DIR/Player-prev.log"
ls -la "$LOG_DIR"/Player*.log
```

- [ ] **Step 2: Launch CK and wait for main-menu** (do NOT load a world, do NOT open the in-game Mods menu — per [project CLAUDE.md] the menu wipes fake-ID dev installs)

User-action: launch Core Keeper through CrossOver; wait for the title screen to render.

- [ ] **Step 3: Grep Player.log for ItemChecklist sandbox-compile**

```bash
LOG_DIR="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep -nE 'ItemChecklist|CompileFailed|safetyCheck' "$LOG_DIR/Player.log"
```

Pass: line `Successfully compiled ItemChecklist safetyCheck=True` present; no `CompileFailed` or sandbox-violation. Fail: `CompileFailed` present → 3-strike rule (Spec Risk #1): if all three subsequent strikes fail with the same root cause → re-brainstorm.

- [ ] **Step 4: If FAIL — diagnose**

Sandbox-violations come in two flavors:
- `System.IO` or banned-namespace use → unlikely here (no IO in this iter)
- `SortingLayer.NameToID` or `SpriteMask`-related API blocked → per Risk #1 mitigation, switch to Harmony-patch path

If neither matches: capture the exact error and re-brainstorm.

- [ ] **Step 5: Close CK** (do not load a world; just exit from title screen)

- [ ] **Step 6: Commit cycle-passed marker** (optional, only if useful for bisect)

```bash
git commit --allow-empty -m "test: phase-1 sandbox-compile PASS"
```

---

### Task 9: Phase 2 — Iter-2/3/3.5 Regression (First-Open)

**Files:** None modified. Test only.

- [ ] **Step 1: Truncate logs + launch CK + load a test-world** (the per-CK dev-world used in prior iters)

User-action: launch CK, load test-world, wait for in-game.

- [ ] **Step 2: Press F1** (open ItemChecklist window)

Verify visually:
- Window root visible at expected screen position
- Wood-theme 9-slice background renders
- Title text "Item Checklist" rendered
- Rows render (at least the first few visible)
- Mouse cursor remains visible while window is open
- WASD input blocked while window is open
- `ResetScroll` effect: list always opens at top regardless of previous close-scroll-position

- [ ] **Step 3: Grep Player.log for errors during open**

```bash
LOG_DIR="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep -nE 'NullReferenceException|Exception|ItemChecklist' "$LOG_DIR/Player.log"
```

Pass: only INFO-level ItemChecklist log lines; no NRE or Exception. Fail: 1-strike → STOP, revert (`git reset --hard HEAD~N`).

- [ ] **Step 4: Press F1 again** (close); verify window hides cleanly without log errors

- [ ] **Step 5: Commit cycle-passed marker** (optional)

```bash
git commit --allow-empty -m "test: phase-2 iter-2/3/3.5 regression PASS"
```

---

### Task 10: Phase 3 — Multi-Open Pool Regression

**Files:** None modified. Test only.

- [ ] **Step 1: While still in test-world: F1→Escape→F1 cycle three times**

After each cycle, verify visually:
- All row Labels still render (PugText pool not leaked)
- Main-menu PugTexts (when user exits to main menu) still render

- [ ] **Step 2: Disconnect to main menu**

Verify:
- Main-menu Title and button labels render (regression target from Iter-3 PugText.Clear-fix)
- No "blank text" symptoms

- [ ] **Step 3: Grep Player.log for PugText pool errors**

```bash
grep -nE 'PugText|pool' "$LOG_DIR/Player.log"
```

Pass: only INFO-level pool-debug; no errors. Fail: 1-strike → STOP, revert.

- [ ] **Step 4: Commit cycle-passed marker** (optional)

```bash
git commit --allow-empty -m "test: phase-3 multi-open pool regression PASS"
```

---

### Task 11: Phase 4 — Scroll Regression

**Files:** None modified. Test only.

- [ ] **Step 1: Re-enter test-world, press F1**

- [ ] **Step 2: Mouse-wheel scroll inside the window**

Verify:
- Scroll moves rows vertically (Iter-3.5 `ResetScroll`-fix intact)
- Scroll bounds respected (no run-off bottom/top)
- Close (F1) + re-open (F1): scroll position resets to top (`ResetScroll` triggered in `SpawnRows`)

- [ ] **Step 3: Verify no scroll-side-effect log spam**

```bash
grep -cE 'UIScrollWindow|scroll' "$LOG_DIR/Player.log"
```

Expected: 0 or low count (only INFO).

- [ ] **Step 4: Commit cycle-passed marker** (optional)

```bash
git commit --allow-empty -m "test: phase-4 scroll regression PASS"
```

---

### Task 12: Phase 5 — Clipping Visual-Verification (Iter-3.5b Core)

**Files:** None modified. Test only.

- [ ] **Step 1: Press F1 in test-world (scroll position = top)**

Verify: top rows visible at the top of the wood-theme rectangle.

- [ ] **Step 2: Mouse-wheel scroll DOWN (rows that should clip OFF the top)**

Verify visually:
- Rows that have scrolled above the top wood-theme edge are **invisible** (both Icon SpriteRenderer + Label PugText)
- No partial half-rendered rows above the top edge

- [ ] **Step 3: Continue scrolling — rows clip OFF the bottom**

Verify visually:
- Rows below the bottom wood-theme edge are invisible
- Bottom edge clean

- [ ] **Step 4: 0-Tolerance Pass/Fail**

If any row pokes outside the wood-theme rectangle (top or bottom, icon or label): **STRIKE 1 → revert immediately**:

```bash
git log --oneline -10
# identify last WIP commit before mask hook was wired (Task 6 or earlier)
git reset --hard <commit-before-mask-hooks>
```

Then escalate to **Iter-3.5c** (alternative clipping strategy, see Spec Risk #9: Camera-Clip-Plane or uGUI-RectMask2D pivot).

If clipping works: pass.

- [ ] **Step 5: Optional — Screenshot for the record**

User-action: take a screenshot showing a mid-scroll state with rows clipping at top + bottom.

- [ ] **Step 6: Commit cycle-passed marker**

```bash
git commit --allow-empty -m "test: phase-5 clipping visual-verification PASS"
```

---

### Task 13: Phase 6 — Layout-Side-Effects-Check

**Files:** None modified. Test only.

- [ ] **Step 1: With window open, verify Title is NOT clipped**

The Title sits above the scrolling-region (according to Task 2 Pre-Flight, its sorting-order falls outside `40..55`). It must always render fully — even at extreme scroll positions.

- [ ] **Step 2: Verify Window-Background is NOT clipped**

The 9-slice wood-theme background must render fully (it's behind the mask in z-order but its sorting-order falls outside `40..55`).

- [ ] **Step 3: Close window, verify no leftover artifacts on screen** (mask deactivates with `root.SetActive(false)`)

- [ ] **Step 4: Open vanilla CK UI (e.g., Inventory) and verify it's NOT affected by our mask**

Verify the player's inventory grid + hotbar still render fully when opened with our mod active. If any vanilla UI element is clipped: 0-Tolerance → revert (the Custom-Range is leaking).

- [ ] **Step 5: 0-Tolerance Pass/Fail**

If Title, Background, or vanilla UI is clipped: **STRIKE 1 → revert and re-evaluate Sorting-Order audit from Task 2** (the custom-range overlaps with something we missed).

- [ ] **Step 6: Commit cycle-passed marker**

```bash
git commit --allow-empty -m "test: phase-6 layout-side-effects PASS"
```

---

## Phase 3 — Merge & Cleanup

### Task 14: Merge `iter-3-5b` → `main` + Worktree Cleanup

**Files:** None modified. Git-only.

- [ ] **Step 1: Squash-rebase WIP commits into clean public history** (per global CLAUDE.md "rebase vor merge")

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
git log --oneline main..HEAD
# Identify the WIP + test-marker commits to fold. Plan target: ONE final commit.
git rebase -i main
```

Final commit message:
```
feat(ui): clip rows via SpriteMask + sorting-layer custom-range

Iter-3.5b. Ports IB's ContentsMask pattern 1:1, runtime-materialized:
- ContentsMask GameObject as sibling of the scrolling Content
- 1×1 white-rect Sprite via Texture2D + Sprite.Create (static-cached)
- Custom range Order 40..55 in Sorting-Layer "UI"
- Per-row VisibleInsideMask in SpawnRows() mirroring AddEntryDisplay

7 test phases all PASS (sandbox compile, regression Iter-2/3/3.5,
multi-open pool, scroll, clipping, layout-side-effects).

Spec: docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5b-design.md
```

- [ ] **Step 2: Switch back to `main` worktree and fast-forward merge**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git checkout main
git merge --ff-only iter-3-5b
git log --oneline -5
```

Expected: HEAD now contains the Iter-3.5b commit on `main`. If FF fails: `main` has diverged — STOP and ask the user.

- [ ] **Step 3: Worktree pre-flight check before removal** (per [[worktree-remove-preflight-check]])

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5b
git ls-files --others --exclude-standard
git ls-files --others -i --exclude-standard
```

Expected: ONLY `.envrc` (the gitignored copy we made in Task 0 Step 3). If any other file appears: STOP, ask the user before removing.

**Additional safety per memory:** for any untracked file that the user wants to "throw away", first md5-compare each against the main-repo copy — `existiert auch in main` ≠ `identisch in main`.

- [ ] **Step 4: Change CWD out of the worktree before removing it** (per project CLAUDE.md — deleting a worktree while CWD is inside it permanently blocks the shell session)

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
pwd  # verify NOT inside .worktrees/iter-3-5b
```

- [ ] **Step 5: Remove the worktree + delete the branch** (only after Step 3 passes)

```bash
git worktree remove .worktrees/iter-3-5b
git branch -d iter-3-5b
git worktree list
```

Expected: only the main worktree remains.

- [ ] **Step 6: Update memory** (per [[item-checklist-ui-pivot-state]])

After successful merge, the memory entry at `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md` should be updated:
- Status moves from "Iter-3.5 DONE" to "Iter-3.5b DONE (clipping fix via SpriteMask)"
- Pending list: remove "Iter-3.5b (Clipping — Mask/RectMask2D fehlt)"; Iter-3.6 + Iter-4 + Iter-3.7 remain

The update is suggested to the user; it's an `~/.claude/memory/`-level edit, not a repo commit.

- [ ] **Step 7: Optional — update Spike-5 with the closed gap**

Append a section to `docs/research/spike-5-uiscrollwindow-decompile.md` (back on `main` now):

```markdown
## Addendum (2026-05-27) — Spike-5 gap closed in Iter-3.5b

Spike-5's "Reference Coverage" table marked `ItemBrowserUI.prefab` as
"partial — 991 KB". Iter-3.5b's pre-implementation spike closed that gap:

- IB uses `SpriteMask` + Sorting-Layer-Custom-Range (NOT uGUI-RectMask2D)
- `ContentsMask` is a sibling of `Scroll`, both children of `ScrollAnchor`
- Per-row `MaskInteraction = VisibleInsideMask` is set at runtime in
  `ItemBrowserRegistry.AddEntryDisplay` (Z66-77); display-prefabs
  themselves have `m_MaskInteraction: 0`
- 18/18 IB display-prefabs verified MaskInteraction=0 — the runtime hook
  is the only place this gets enabled

See `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5b-design.md`
for the full pattern + our 1:1 port.
```

Commit:
```bash
git add docs/research/spike-5-uiscrollwindow-decompile.md
git commit -m "docs(spike-5): close ContentsMask gap addendum from iter-3.5b"
```

---

## Spec Coverage Check (Self-Review)

| Spec Section | Covered by Task |
|---|---|
| Context (Spike-5 gap, IB clipping mechanism) | Tasks 1, 2 (Pre-Flight), 14 Step 7 (addendum) |
| Goals: Clipping | Tasks 4, 5, 7 (implementation) + Tasks 12, 13 (visual verification) |
| Goals: Pattern-Treue (IB 1:1) | Tasks 4, 5 (1:1 ports of `ContentsMask` + `AddEntryDisplay`) |
| Goals: Zero-Regression | Tasks 9, 10, 11 (Phases 2, 3, 4) |
| Non-Goals: no prefab edits | enforced by file-structure table (Window/Row prefabs marked unchanged) |
| Architecture: Hierarchy | Task 6 (anchor = `rowsContent.parent`) |
| Components: `ContentsMaskInstaller` API | Tasks 3, 4, 5 (skeleton, Install, SetMaskInteractionRecursive) |
| Components: 2 hooks in `ItemChecklistWindow` | Tasks 6, 7 |
| Data Flow: Awake + ShowUI + Cleanup | Tasks 6, 7; cleanup-via-Object-Tree is automatic |
| Error Handling Edge Case 1 (private fields) | Task 1 Steps 3-4 (Reflection fallback conditional) |
| Error Handling Edge Case 2 (SortingLayer "UI") | Task 4 Step 1 (NameToID + fallback) |
| Error Handling Edge Case 3 (PugText.style null) | Task 5 Step 1 (null-guard) |
| Error Handling Edge Case 5 (Texture2D leak) | Task 3 Step 1 (`_whiteTex` / `_whiteSprite` static cache) |
| Error Handling Edge Case 8 (sorting-order conflict) | Task 2 (Pre-Flight audit) |
| Testing Phases 0-6 | Tasks 0-1-2 (Phase 0) + 8-13 (Phases 1-6) |
| Risks #1-9 | Task 8 (Sandbox), Task 12/13 (0-Tolerance), Task 14 (Worktree-pre-flight), Pre-Flight Tasks 1-2 |
| Worktree-Setup | Task 0 |
| Lessons-driven defaults (WIP commits, grep-on-install) | Throughout — every implementation task ends with WIP commit + grep-on-install per [[subagent-build-verify-install]] |

**No spec gaps found.**

**Placeholder scan:** searched for `TBD`, `TODO`, "appropriate", "similar to" — none found. The only `<fill>` placeholders are in Task 2 Step 4 (scratchpad notes), which is intentional — they're recorded at execution time.

**Type consistency:** `ContentsMaskInstaller.Install(Transform, UIScrollWindow)` → `SpriteMask` used identically in Tasks 4 and 6. `SetMaskInteractionRecursive(GameObject, SpriteMaskInteraction)` used identically in Tasks 5 and 7. `_contentsMask` field type matches Install's return type. ✅
