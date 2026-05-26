# ItemChecklist UI Pivot — Iter-3.5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decompile-Spike on `UIScrollWindow` + IB Deep-Analyse, derive concrete scroll-fix hypothesis, implement + verify without regressing Iter-3-wins or producing Iter-3-style layout-side-effects.

**Architecture:** Single-iter combined (Spike + Fix + Test). Spike outputs a research-spec with What/How/Why per API call + ranked fix-hypotheses. User picks one via decision-gate. Fix is implemented in `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (code-only) or `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (prefab-edit) or both, depending on hypothesis.

**Tech Stack:** C# (Unity 6000.0.59f2), PugMod RoslynCSharp sandbox, Harmony 2.x (unchanged), CoreLib 4.0.3 runtime + 4.0.4 SDK build-time, ILSpyCmd CLI for decompile, `../../../utils/build.sh` Unity batchmode pipeline.

**Reference spec:** `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md`

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `.worktrees/iter-3-5/` | Create (worktree) | Isolated workspace for Iter-3.5 |
| `.worktrees/iter-3-5/.envrc` | Create (copy from main-worktree) | Env vars for build (UNITY_BIN, MOD_INSTALL_PATH, etc.) |
| `docs/research/spike-5-uiscrollwindow-decompile.md` | Create | Spike-output: UIScrollWindow internals + IB-deep-analyse + Fix-Hypothesen |
| `/tmp/iter-3-5-spike/UIScrollWindow.cs` | Create (decompile output) | Raw ILSpy-decompiled C# source of UIScrollWindow |
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | Modify (Phase 3, depending on hypothesis) | Scroll-Fix Code-Änderung |
| `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | Modify (Phase 3, conditional) | Optional Prefab-Edit falls Spike Layout-Component nötig zeigt |
| `docs/research/spike-4-ui-architecture.md` | Modify | Iter-3.5 status header update |

---

## Phase 0 — Pre-Flight Setup

### Task 0: Worktree-Setup + Dependencies-Check

**Files:**
- Create: `.worktrees/iter-3-5/` (new git worktree)
- Create: `.worktrees/iter-3-5/.envrc` (copy of main `.envrc`)
- Verify: `unity/ItemChecklist/Art/Bridge/*.png` (must exist in main repo, persistent since 25.5.)
- Verify: `ilspycmd` available (install if needed)
- Verify: `Pug.UnityExtensions.dll` location (SDK PackageCache or CK-Bottle)

- [ ] **Step 1: Verify CK + Unity not running**

```bash
pgrep -f "Core Keeper" || echo "ck-not-running"
pgrep -f "Unity.app/Contents/MacOS/Unity" || echo "editor-not-running"
```

If CK or Unity running: pkill / ask user to close.

- [ ] **Step 2: Create worktree for iter-3-5**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git worktree add .worktrees/iter-3-5 -b iter-3-5
ls .worktrees/iter-3-5
```

Expected: directory exists with all tracked files. Branch `iter-3-5` created from main HEAD (`c3fa944`).

- [ ] **Step 3: Copy `.envrc` into the new worktree**

```bash
cp /Users/valgard/Projects/private/core_keeper/item-checklist/.envrc \
   /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5/.envrc
ls -la /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5/.envrc
```

Per `[[subagent-build-verify-install]]` Memory: `.envrc` is gitignored and missing in fresh worktrees by default. Without it, builds fail silently.

- [ ] **Step 4: Verify Bridge-sprites in main repo (not worktree-local)**

```bash
ls /Users/valgard/Projects/private/core_keeper/item-checklist/unity/ItemChecklist/Art/Bridge/
```

Expected: at least `ui_classic.png`, `ui_stone.png`, `ui_unknown_item.png` etc. (persistent since 25.5.). Since the worktree mirrors via symlink-based `utils/link.sh`, Unity Editor sees these.

If missing: STOP, report BLOCKED. Recovery via Time Machine or `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Art/UserInterface/`.

- [ ] **Step 5: Install ILSpyCmd if missing**

```bash
which ilspycmd || dotnet tool install ilspycmd -g
ilspycmd --version
```

Expected: ilspycmd installed + version printed. Note: `ilspycmd` is a .NET global tool, installed via `dotnet tool install -g`. Requires .NET SDK installed (which Unity SDK setup likely has).

If `dotnet` not found: fallback path is `brew install ilspy` (different binary, slightly different CLI) — document the actually-used command in the spike output.

- [ ] **Step 6: Locate `Pug.UnityExtensions.dll`**

```bash
find /Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK/Library/PackageCache -name "Pug.UnityExtensions.dll" 2>/dev/null | head -5
find "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper" -name "Pug.UnityExtensions.dll" 2>/dev/null | head -5
```

Expected: at least one path. Pick the one from SDK PackageCache (build-time reference). If both empty: STOP, report BLOCKED. The class `UIScrollWindow` may live in a differently-named DLL — try also `Pug.Other.dll`, `Pug.Common.dll`, etc.:

```bash
for dll in $(find /Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK/Library/PackageCache -name "Pug*.dll" 2>/dev/null); do
  ilspycmd -t UIScrollWindow "$dll" >/dev/null 2>&1 && echo "FOUND: $dll"
done
```

Save the resolved path; it is the input for Task 1a.

- [ ] **Step 7: NO commit**

Per project convention: only commit at explicit user approval points (end of Phase 5).

---

## Phase 1 — Spike

### Task 1a: UIScrollWindow Decompile (focused)

**Files:**
- Create: `/tmp/iter-3-5-spike/UIScrollWindow.cs` (raw decompile output)
- Read: `<DLL-path-from-Task-0>` (input)

- [ ] **Step 1: Decompile UIScrollWindow class to temp dir**

```bash
mkdir -p /tmp/iter-3-5-spike
ilspycmd -t UIScrollWindow <DLL-path> -o /tmp/iter-3-5-spike/
ls /tmp/iter-3-5-spike/
```

Expected: `UIScrollWindow.cs` file created. If ilspycmd outputs to a different filename (e.g. `Pug.UnityExtensions.cs`): rename to `UIScrollWindow.cs`.

- [ ] **Step 2: Verify decompile sanity (sanity-check the class is non-trivial)**

```bash
wc -l /tmp/iter-3-5-spike/UIScrollWindow.cs
grep -c "public\|private\|protected" /tmp/iter-3-5-spike/UIScrollWindow.cs
grep -E "SetScrollValue|UpdateScrollHeight|_scrollable|IScrollable" /tmp/iter-3-5-spike/UIScrollWindow.cs | head -20
```

Expected: file is >50 lines, has multiple access-modifiers, mentions `SetScrollValue` + `UpdateScrollHeight` + `_scrollable` etc. If <20 lines or none of these symbols present: ilspycmd matched the wrong class. Re-try with different `-t` filter (e.g. `Pug.UIScrollWindow`).

- [ ] **Step 3: Grep-extract focused methods (raw)**

```bash
echo "=== SetScrollValue ===" > /tmp/iter-3-5-spike/focused.txt
awk '/public.*SetScrollValue/,/^\s*}\s*$/' /tmp/iter-3-5-spike/UIScrollWindow.cs >> /tmp/iter-3-5-spike/focused.txt
echo "" >> /tmp/iter-3-5-spike/focused.txt
echo "=== UpdateScrollHeight ===" >> /tmp/iter-3-5-spike/focused.txt
awk '/public.*UpdateScrollHeight/,/^\s*}\s*$/' /tmp/iter-3-5-spike/UIScrollWindow.cs >> /tmp/iter-3-5-spike/focused.txt
echo "" >> /tmp/iter-3-5-spike/focused.txt
echo "=== _scrollable field + property ===" >> /tmp/iter-3-5-spike/focused.txt
grep -B1 -A3 "_scrollable" /tmp/iter-3-5-spike/UIScrollWindow.cs >> /tmp/iter-3-5-spike/focused.txt
echo "" >> /tmp/iter-3-5-spike/focused.txt
echo "=== Lifecycle: Awake/OnEnable/Update ===" >> /tmp/iter-3-5-spike/focused.txt
for m in Awake OnEnable OnDisable Update LateUpdate; do
  echo "--- $m ---" >> /tmp/iter-3-5-spike/focused.txt
  awk "/(private|protected|public).*[[:space:]]$m\s*\(/,/^\s*}\s*$/" /tmp/iter-3-5-spike/UIScrollWindow.cs >> /tmp/iter-3-5-spike/focused.txt
done
cat /tmp/iter-3-5-spike/focused.txt
```

Save this output for inclusion in the spike-spec.

- [ ] **Step 4: Identify helper-methods called by the focused methods**

Read the focused methods carefully. For every internal method call (e.g. `RecalculateBounds()`, `UpdateClipping()`, `EnsureLayout()`), grep them out of the full class:

```bash
# After identifying e.g. RecalculateBounds + UpdateClipping by reading focused.txt
for helper in RecalculateBounds UpdateClipping EnsureLayout SyncContent; do
  echo "--- helper: $helper ---" >> /tmp/iter-3-5-spike/focused.txt
  awk "/(private|protected|internal).*[[:space:]]$helper\s*\(/,/^\s*}\s*$/" /tmp/iter-3-5-spike/UIScrollWindow.cs >> /tmp/iter-3-5-spike/focused.txt
done
```

Iterate Step 4 until the focused.txt has a self-contained call-graph (all referenced methods are extracted).

- [ ] **Step 5: NO commit yet**

### Task 1b: IB Deep-Analyse

**Files:**
- Read: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/EntriesList.cs`
- Read: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/EntriesListRenderer.cs`
- Read: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/BasicEntriesListRenderer.cs`
- Read: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Prefabs/Browser/ItemBrowserUI.prefab` (or equivalent — the window prefab containing UIScrollWindow)

- [ ] **Step 1: Verify IB source still available**

```bash
ls /tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/
```

Expected: `EntriesList.cs`, `EntriesListRenderer.cs`, `BasicEntriesListRenderer.cs` present. If missing (because `/tmp/` got cleared): re-clone the source per `[[corekeeper-ui-pattern]]` Memory (`git clone https://github.com/moorowl/ItemBrowser /tmp/ck-ui-research/ItemBrowser`).

- [ ] **Step 2: Read EntriesList.cs in full + extract SetEntries() with full call sequence**

Open file in entirety. Focus on:
- `class EntriesList : UIelement, IScrollable` declaration
- `SetEntries(...)` method — every line, including pre-calls before the 3-call sequence we already know
- `OnEnable()` + `Awake()` — what setup happens when the EntriesList becomes active?
- IScrollable interface implementation — what methods are called by UIScrollWindow on EntriesList?

Write findings as a What/How/Why block per relevant API into `docs/research/spike-5-uiscrollwindow-decompile.md`. Example structure for SetEntries:

```markdown
### EntriesList.SetEntries(IEnumerable<TItem> items)

**What:** 
1. Clear-call: `_renderer.ClearList()` — releases prior pooled elements + pugText.Clear()
2. Loop: foreach item, `_renderer.RenderItem(item, container)` — spawns pooled element + binds
3. Wire-call: `API.Reflection.SetValue(MiScrollable, _scrollWindow, this)` — sets the IScrollable reference
4. Layout-update: `API.Reflection.Invoke(MiUpdateScrollHeight, _scrollWindow)` — recalculates content height
5. Scroll-activation: `_scrollWindow.SetScrollValue(scrollProgress)` — activates scroll input listening

**How:** [explain the sequence + why this order]

**Why:** [reasoning derived from IB code-comments + observable behaviour]
```

- [ ] **Step 3: Read EntriesListRenderer.cs in full + extract render lifecycle**

What: RenderItem / FreePooledElement / element lifecycle. How: pool-based spawn, RectTransform/Transform setup, binding callbacks. Why: pool re-use for performance + correct cleanup.

- [ ] **Step 4: Read BasicEntriesListRenderer.cs ClearList()**

(Partially analyzed in Iter-3 — re-confirm the full call sequence including SetActive(wasActive) and FreePooledElement)

- [ ] **Step 5: Read IB Window Prefab YAML — extract UIScrollWindow setup**

```bash
find /tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Prefabs -name "*.prefab" -exec grep -l "UIScrollWindow" {} +
```

Pick one matching prefab. Read its YAML focusing on:
- The GameObject hierarchy around UIScrollWindow
- Components on the parent of UIScrollWindow's content container
- Any layout-helper components (e.g. `RectMask2D`, `VerticalLayoutGroup`, or CK-specific layout components)

Write findings to spike-spec under section "IB Prefab-Layout-Components vs Our Prefab".

- [ ] **Step 6: NO commit yet**

### Task 1c: Cross-Reference + Fix-Hypothesen → Spike-Spec finalize

**Files:**
- Modify: `docs/research/spike-5-uiscrollwindow-decompile.md` (already started in Task 1b — finalize)

- [ ] **Step 1: Cross-reference UIScrollWindow internals vs IB calls**

For each focused-method in `/tmp/iter-3-5-spike/focused.txt`: identify which IB code-path triggers it + with what state. Document mismatches between our usage (Iter-3-attempt) and IB's working usage.

Example cross-reference entry:

```markdown
### Cross-Reference: SetScrollValue side-effects

**What UIScrollWindow.SetScrollValue does internally** (from Task 1a):
[verbatim code or summary]

**What IB does before SetScrollValue** (from Task 1b):
[the pre-calls in EntriesList.SetEntries]

**What our Iter-3-code did before SetScrollValue:**
[just SetValue(_scrollable) + UpdateScrollHeight invoke — missing X, Y, Z]

**Mismatch identified:** [what we missed]
```

- [ ] **Step 2: Derive 1-3 ranked fix-hypotheses**

Each hypothesis must specify:
- Fix-Format: code-only / prefab-edit / both
- Concrete change: exact code diff or prefab YAML field change
- Why this should work: derived from cross-reference findings
- Risk: known side-effects, regression-risk on Iter-3-wins (pugText.Clear pool, layout)
- Implementation effort: small / medium / large

Format in spike-spec as `## Fix-Hypothesen (ranked by likelihood × low-risk)`:

```markdown
### Hypothesis A: [name]
- **Fix:** [exact change]
- **Why:** [reasoning]
- **Risk:** [known caveats]
- **Effort:** [S/M/L]

### Hypothesis B: ...
```

- [ ] **Step 3: Add Reference-Coverage section**

Per `[[reference-analysis-mandatory-when-provided]]` Memory: list explicitly what was analyzed in full (What/How/Why) vs what was deliberately skipped, with reasoning for each skip.

- [ ] **Step 4: NO commit yet**

- [ ] **Step 5: Suggest WIP-commit to user**

Per `[[frequent-wip-commits-for-bisect]]` Memory: at this milestone (Spike complete) surface a WIP-commit suggestion:

```bash
# Suggested WIP commit (user must approve)
git add docs/research/spike-5-uiscrollwindow-decompile.md
git commit -m "WIP: Iter-3.5 spike — UIScrollWindow decompile + IB deep-analyse + fix-hypotheses ranked"
```

Wait for explicit approval before committing.

---

## Phase 2 — User Decision-Gate

### Task 2: Surface ranked fix-hypotheses

- [ ] **Step 1: Read spike-5 doc final state**

```bash
cat docs/research/spike-5-uiscrollwindow-decompile.md
```

Extract the ranked hypothesis list from the "Fix-Hypothesen" section.

- [ ] **Step 2: Present hypotheses via AskUserQuestion**

Construct a question with one option per hypothesis, format per CLAUDE.md rule (text-with-pros-cons-and-recommendation BEFORE tool call, recommended option first). Include the "no-fix-possible" exit option if the spike concluded that:

> "Welche Fix-Hypothese aus dem Spike-5 wollen wir implementieren?"
> - A) Hypothesis-A (ranked highest, Recommended)
> - B) Hypothesis-B
> - C) (if no-fix-possible:) Iter-3.5 SPIKE-ONLY → Pivot zu Iter-3.5b
> - Other (user types own)

Wait for user choice. **DO NOT proceed to Phase 3 without explicit user approval of one hypothesis.**

- [ ] **Step 3: If user picks SPIKE-ONLY exit: skip Phase 3+4, jump to Phase 5 wrap-up with reduced scope (commit + merge spike-spec only, suggest Iter-3.5b brainstorm)**

---

## Phase 3 — Fix-Implementation

(Conditional on Phase 2 outcome — if SPIKE-ONLY, skip this whole phase.)

### Task 3: Implement chosen fix

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (if code-fix in hypothesis)
- Modify: `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (if prefab-edit in hypothesis)

- [ ] **Step 1: Read current state of file(s) the chosen hypothesis touches**

```bash
cat /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5/unity/ItemChecklist/ui/ItemChecklistWindow.cs
```

Verify pre-change baseline matches expected (= Iter-3 final state with `pugText.Clear()` loop in `ClearRows`).

- [ ] **Step 2: Apply the exact code-diff from the chosen hypothesis**

Use the Edit tool with the verbatim before/after blocks from the spike-spec's hypothesis. The hypothesis must specify these exact blocks — if not, treat as ambiguous + return to Phase 2.

- [ ] **Step 3: Verify diff is exactly the planned change**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5
git diff
```

Expected: only the file(s) listed in the hypothesis modified, no unintended changes. If extra diff: STOP, revert + investigate.

- [ ] **Step 4: NO commit yet**

- [ ] **Step 5: For prefab-edit hypotheses: confirm Unity Editor save vs YAML-direct edit**

If the hypothesis is prefab-edit:
- YAML-direct: edit `.prefab` file directly with exact YAML field-change from spike (e.g. `m_DrawMode: 1` → `m_DrawMode: 0`). Verify `git diff` shows only that field.
- Editor-side: requires user to open Unity Editor, make change, save project, verify `git diff` shows the change in the file (per Iter-3 lesson: Editor sometimes doesn't persist changes to file without explicit File→Save Project).

Pick the appropriate path based on the hypothesis description.

---

## Phase 4 — Build + Test

### Task 4: Build + Install + Truncate Logs

- [ ] **Step 1: Verify CK + Unity not running**

```bash
pgrep -f "Core Keeper" && pkill -KILL -f "Core Keeper"; sleep 1
pgrep -f "Unity.app/Contents/MacOS/Unity" || echo "editor-not-running"
```

If Unity Editor running: NEEDS_CONTEXT — ask user to close.

- [ ] **Step 2: Build via utils/build.sh in the iter-3-5 worktree**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5
bash -c "source .envrc && ../../../utils/build.sh"
```

Timeout: 300000ms (5min). Expected: exit 0.

If `.envrc` missing: STOP, BLOCKED — Phase 0 Task 0 Step 3 didn't complete. Re-copy `.envrc`.

- [ ] **Step 3: Verify install picked up the change (per [[subagent-build-verify-install]] Memory)**

For code-fix hypotheses: grep on a unique source-marker from the diff:

```bash
# Example for a code-fix that adds a UpdateLayout() call
INSTALL="$MOD_INSTALL_PATH/$FAKE_MOD_ID""_1/Scripts/ui/ItemChecklistWindow.cs"
grep -c "UpdateLayout" "$INSTALL"  # expected: matches the diff (1 or N)
```

For prefab-edit hypotheses: check installed Bundle mtime is recent + grep the bundle's YAML-extracted form if accessible.

If grep doesn't match: BLOCKED — build false-success (per [[subagent-build-verify-install]] Memory). Manually re-run `bash -c "source .envrc && ../../../utils/build.sh"` and re-verify.

- [ ] **Step 4: Truncate Player.log + Player-prev.log**

```bash
> "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
> "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player-prev.log"
```

Verify both at 0 bytes.

### Task 5: Test Phase 1 — Sandbox Compile Check

- [ ] **Step 1: Ask user to launch Core Keeper to main-menu**

> "Bitte starte Core Keeper und warte bis das Hauptmenü voll geladen ist. Dann melde dich kurz mit 'ready'."

- [ ] **Step 2: Grep Player.log for compile result**

```bash
PLAYER_LOG="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
grep -E "Successfully compiled ItemChecklist|CompileFailed" "$PLAYER_LOG"
grep -E "CompileFailed" "$PLAYER_LOG" | head -5  # cascade check
```

Expected: one line `Successfully compiled ItemChecklist safetyCheck=True`, zero `CompileFailed` lines.

If `CompileFailed ItemChecklist`: read the error line. Per spec failure-mode, allow up to 3 retries (fix sandbox-blocked API + rebuild + relaunch). After 3rd fail: STOP, re-brainstorm.

If `CompileFailed <other-mod>`: cascade per `[[project_corekeeper_compile_fail_cascade]]` Memory — investigate but likely unrelated.

### Task 6: Test Phase 2 — Iter-1+2+3 Regression (First-Open Visual)

- [ ] **Step 1: Ask user to load char + world**

> "Bitte lade einen Charakter + eine Welt. Sobald du im Spiel bist (Hotbar sichtbar), melde dich."

- [ ] **Step 2: Ask user to press F1 once + describe what they see**

> "Drücke F1 einmal. Beschreibe was du siehst: Fenster mit Wood-Theme-Background + Title + Rows generell rendernd (Icons + ???-Placeholders gemischt)? Cursor sichtbar + WASD blockiert?"

Expected user-reportable observations (= Iter-3-PARTIAL-baseline):
- Window centered with Wood-Theme 9-slice background
- Title "Item Checklist" visible at top
- Rows rendering (don't fixate on which specific items appear — Iter-3.6 issue separately)
- Mouse cursor visible
- WASD does not move character

- [ ] **Step 3: Decide outcome**

If clean: proceed to Task 7.

If first-open is broken in a NEW way (e.g. Title gone, background gone, rows not rendering at all): STOP — Phase 2 failure means our fix has broken something Iter-3 had working. Revert the fix (`git checkout unity/ItemChecklist/ui/ItemChecklistWindow.cs` and/or the prefab) and report BLOCKED to user.

### Task 7: Test Phase 3 — Multi-Open Text-Regression

- [ ] **Step 1: Ask user to F1→Escape→F1 cycle three times**

> "Drücke F1 → Escape → F1 → Escape → F1 (3 cycles). Bleiben Title und Row-Texte auf jedem Open sichtbar? Werden sie nicht blank?"

- [ ] **Step 2: Ask user to disconnect to main menu**

> "Beende das Spielsave (Disconnect oder Exit to Main Menu). Sind die Hauptmenü-Buttons (Play / Settings / Quit) noch sichtbar mit Text, oder leer?"

Expected:
- Both checks: texts persist (Iter-3 `pugText.Clear()` fix still working)

- [ ] **Step 3: Decide outcome**

If clean: proceed to Task 8.

If text disappears: STOP — Iter-3-win regressed. Revert + report BLOCKED. This is the spec's 1-strike fail-mode.

### Task 8: Test Phase 4 — Scroll-Fix Verification

- [ ] **Step 1: Ask user to F1 + mouse-wheel scroll**

> "Drücke F1. Bewege Maus übers Fenster + scrolle Mausrad nach unten. Bewegt sich der Inhalt vertikal? Plus: scrolle bis ganz unten — stoppt es da? Plus: scrolle wieder nach oben — stoppt es am Top?"

Expected:
- Mouse-wheel scrolls content vertically
- Scroll bounds respected (no over-scroll)
- Rows outside viewport are clipped

- [ ] **Step 2: Decide outcome**

If clean: proceed to Task 9.

If scroll doesn't activate: STOP — spec Risk-#1 (fix didn't work, Iter-3.5 fail-mode). Per spec: pivot to Iter-3.5b with new Brainstorm.

### Task 9: Test Phase 5 — Layout-Side-Effects Check (0-Tolerance)

- [ ] **Step 1: Ask user to confirm layout integrity**

> "Schau dir das Fenster nochmal an (vielleicht noch offen). Liste startet UNTER dem Titel (NICHT am Window-Top)? Titel ist klar lesbar OHNE Überlagerung durch erste Rows?"

Expected:
- Title-area is unobstructed
- First row is below the title with sensible spacing

- [ ] **Step 2: Decide outcome — 0-tolerance**

If clean: ALL test phases PASS — proceed to Phase 5.

If Layout broken (rows overlap title or start at window-top): STOP IMMEDIATELY. The fix reproduced the Iter-3-SetScrollValue-failure-mode. Per spec: **0-tolerance** — fix must be reverted in full:

```bash
git checkout unity/ItemChecklist/ui/ItemChecklistWindow.cs
git checkout unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab  # if touched
git diff  # should be empty
```

Then return to Phase 2 (Decision-Gate) and try a different hypothesis. Or if all hypotheses exhausted: pivot to Iter-3.5b.

---

## Phase 5 — Wrap-up

### Task 10: Memory + Spike-4 + Project-Memory updates

**Files:**
- Modify: `docs/research/spike-4-ui-architecture.md` (status header)
- Modify: `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md`
- Modify: `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/MEMORY.md` (index entry)

- [ ] **Step 1: Update spike-4 status header**

Add an `**Iter-3.5 DONE**` line to the status block, summarizing the fix + which test phases passed.

- [ ] **Step 2: Update item-checklist-ui-pivot-state memory**

Bump "Was Iter-3 NICHT gelöst hat" section: move Scroll-Bug from "pending" to "fixed in Iter-3.5". Keep Iter-3.6 (DisplayName) and Iter-3.7 (latent 4.0.4) as pending.

- [ ] **Step 3: Update MEMORY.md index entry**

The existing line for `item-checklist-ui-pivot-state` needs the one-line hook updated to reflect Iter-3.5 done.

- [ ] **Step 4: NO commit yet — surface to user in Task 11**

### Task 11: Commit suggestion + FF-merge + Worktree cleanup

- [ ] **Step 1: Surface commit suggestion**

Construct + show the user the proposed commit message + git command. Example:

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs \
        [unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab if touched] \
        docs/research/spike-5-uiscrollwindow-decompile.md \
        docs/research/spike-4-ui-architecture.md \
        docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md \
        docs/superpowers/plans/2026-05-26-itemchecklist-ui-pivot-iter3-5.md
git commit -m "$(cat <<'EOF'
fix(ui): activate mouse-wheel scroll without layout side-effects

Iter-3.5 — closes Iter-2's deferred scroll bug after Iter-3.5 spike.

[Body explaining the chosen hypothesis + why it works without
breaking Iter-3-wins, with references to spike-5 doc.]

One bug remains deferred (see docs/research/spike-4-ui-architecture.md):
- Iter-3.6: DisplayName fallback strategy. Mod-added items without
  localization fall back to ObjectId.ToString(), landing alphabetically
  at the top of the catalog ("3..." < "A...").
EOF
)"
```

Ask user: "Möchtest du den Commit so ausführen, oder soll ich vorher etwas anpassen?"

Wait for explicit user approval. Do NOT run `git commit` without it.

- [ ] **Step 2: If user approves: execute commit**

Run the `git add` + `git commit` as shown. Verify with `git log -1 --stat`.

- [ ] **Step 3: Pre-flight check before worktree-remove**

Per `[[worktree-remove-preflight-check]]` Memory:

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5
git ls-files --others --exclude-standard | head -20  # untracked
git ls-files --others --ignored --exclude-standard | head -20  # gitignored
```

If non-empty (besides expected `.envrc`): STOP, surface findings to user via AskUserQuestion with the 5-option pattern from the memory (discard / copy-to-main / merge-to-other-worktree / commit / abort).

If only `.envrc` is present: proceed (it's a known machine-local file with no value beyond the worktree).

- [ ] **Step 4: FF-merge + cleanup**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git checkout main
git merge --ff-only iter-3-5
git log -1 --stat
git worktree remove .worktrees/iter-3-5
git branch -d iter-3-5
git worktree list
git branch
```

Expected: main now at the iter-3.5 commit, worktree gone, branch deleted.

Surface the post-merge status to the user.

- [ ] **Step 5: Suggest follow-up Iters**

Iter-3.6 (DisplayName) + Iter-3.7 (latent) + Iter-3.8 (asset persistence) remain as pending tasks. Mention them as available next-steps.

---

## SPIKE-ONLY exit path (Phase 2 outcome)

If Phase 2 returns SPIKE-ONLY (no fix-hypothesis viable):

- [ ] **Step S1: Skip Phase 3 + 4 entirely**

- [ ] **Step S2: In Phase 5 reduce commit scope to spike + spec/plan only**

```bash
git add docs/research/spike-5-uiscrollwindow-decompile.md \
        docs/research/spike-4-ui-architecture.md \
        docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md \
        docs/superpowers/plans/2026-05-26-itemchecklist-ui-pivot-iter3-5.md
git commit -m "spike: Iter-3.5 — UIScrollWindow decompile + IB analysis (no-fix-possible)

Spike output documents what UIScrollWindow does internally + how IB
uses it + cross-reference. Concludes: no simple-fix possible with
current Window-Prefab architecture. Iter-3.5b will brainstorm
alternative (DIY scroll or layout restructure)."
```

- [ ] **Step S3: Suggest Iter-3.5b brainstorm**

Surface to user that next step is `superpowers:brainstorming` for Iter-3.5b with the Spike-5 doc as starting input.

---

## Test plan summary

5 test phases, recap for the wrap-up commit message:

| Phase | What it verifies | Pass criterion |
|---|---|---|
| 1. Sandbox compile | Chosen fix is sandbox-permitted | `Successfully compiled ItemChecklist safetyCheck=True` |
| 2. Iter-1+2+3 regression | First-open visuals intact | Window + Theme + Title + Rows + Cursor + WASD-block all OK |
| 3. Multi-open text-regression | Iter-3 `pugText.Clear()` fix still active | Texts persist on F1→Esc→F1 ×3 + main-menu after disconnect |
| 4. Scroll-fix verification | The new fix activates mouse-wheel | Content scrolls vertically; bounds respected; clipping OK |
| 5. Layout-side-effects (0-TOLERANCE) | No Iter-3-SetScrollValue-failure-mode regression | Title-area clear; first row below title |

Failure-mode escalations per task are inlined in the test steps.
