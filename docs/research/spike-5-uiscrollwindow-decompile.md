# Spike 5 — UIScrollWindow Decompile + IB Deep-Analyse

**Date:** 2026-05-26
**Spec link:** `docs/research/spike-4-ui-architecture.md` (UI architecture reference), plan section Iter-3.5
**Status:** COMPLETE → 2 ranked hypotheses

---

## Context

Iter-3 attempted to enable mouse-wheel scroll in the ItemChecklist UI by adding
`scrollWindow.SetScrollValue(0f)` after `API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow)`
inside `SpawnRows()`. This is ported from ItemBrowser's `EntriesList.SetEntries` pattern.

**Side-effect:** Calling `SetScrollValue(0f)` caused rows to render at the window-top
position, overlapping the Title element. ItemBrowser uses the same 3-call sequence
(`SetValue` → `UpdateScrollHeight` → `SetScrollValue`) in production without this problem.

Two possible explanations:
1. We passed `0f` but IB passes `1f` — the argument convention is inverted.
2. We're missing a prefab setup step that IB has.

This spike decompiles `UIScrollWindow` to understand the exact argument semantics and
deep-analyses IB sources to identify every pre-call step we may be missing.

---

## Methodology

- **Decompile target:** `Pug.Other.dll` from CrossOver bottle
  (`/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/…/Managed/Pug.Other.dll`)
- **Tool:** ILSpyCmd 10.1.0.8386 / ICSharpCode.Decompiler 10.1.0.8386
- **Output:** `/tmp/iter-3-5-spike/UIScrollWindow.decompiled.cs` (360 lines)
- **IB sources analyzed:**
  - `EntriesList.cs` — in full
  - `EntriesListRenderer.cs` — in full
  - `BasicEntriesListRenderer.cs` — in full
  - `ItemBrowserUI.prefab` — UIScrollWindow component blocks + parent hierarchy

---

## Part A — UIScrollWindow Internals

### `_scrollable` field + `Awake`

**What:** `_scrollable` is a private `IScrollable` field. Assigned once in `Awake` from the
serialized `scrollable: MonoBehaviour` field. If `scrollable` does not implement `IScrollable`,
`UIScrollWindow.enabled` is set to `false` permanently (Awake runs only once per lifetime).

**How (decompiled):**
```csharp
private IScrollable _scrollable;
public MonoBehaviour scrollable; // serialized field — wired in Editor

private void Awake()
{
    if (scrollable is IScrollable)
        _scrollable = (IScrollable)scrollable;
    else
    {
        Debug.LogError(scrollable?.ToString() + " does not implement IScrollable, disabling UIScrollWindow");
        base.enabled = false;
    }
    // ... RadicalCreditsMenu special case omitted
}
```

**Why / Side-Effects:** `_scrollable` is the ONLY path through which UIScrollWindow
queries content height and reports containing-element positions. If it is null, all of
`LateUpdate` is skipped. IB overrides this via reflection (`MiScrollable`) to support
dynamically-constructed scroll areas where the scrollable MonoBehaviour is not wired in
the Editor prefab.

---

### `UpdateScrollHeight()`

**What:** Recomputes `ScrollHeight` from `_scrollable.GetCurrentWindowHeight()`.
Must be called after content changes so `SetScrollValue` uses a correct denominator.

**How (decompiled):**
```csharp
private void UpdateScrollHeight()
{
    ScrollHeight = math.max(0f, _scrollable.GetCurrentWindowHeight() - windowHeight);
}
```

`ScrollHeight` is a public auto-property (`{ get; private set; }`). Normally updated each
`LateUpdate` call. IB (and our Iter-3 code) invoke it immediately via reflection to avoid
a one-frame delay before `SetScrollValue` can use the correct height.

**Why / Side-Effects:** None beyond updating the cached `ScrollHeight` property.

---

### `SetScrollValue(float normalizedScrollValue)`

**What:** Converts a `[0..1]` normalized position to an absolute content-Y offset and
applies it by moving `scrollingContent.localPosition.y`.

**How (decompiled):**
```csharp
public void SetScrollValue(float normalizedScrollValue)
{
    if (_scrollable != null)
    {
        float scrollablePosition = math.lerp(ScrollHeight, minScrollPos, normalizedScrollValue);
        SetScrollablePosition(scrollablePosition);
    }
}
```

`SetScrollablePosition` (private) does pixel-perfect rounding and then:
```csharp
private void SetScrollablePosition(float verticalOffsetFromParent)
{
    if (!dontForcePixelPerfect)
        verticalOffsetFromParent = Mathf.Round(verticalOffsetFromParent / 0.0625f) * 0.0625f;
    Vector3 localPosition = scrollingContent.localPosition;
    localPosition.y = verticalOffsetFromParent;
    scrollingContent.localPosition = localPosition;
    _scrollable.UpdateContainingElements(verticalOffsetFromParent);
}
```

**Argument semantics (critical — inverted from intuition):**

| Value | `lerp(ScrollHeight, minScrollPos, t)` result | Meaning |
|-------|----------------------------------------------|---------|
| `1f`  | `minScrollPos` = 0                           | **TOP** of list (first row visible) |
| `0f`  | `ScrollHeight`                               | **BOTTOM** of list (last rows visible) |

With `windowHeight=5`, `RowHeight=2.5`, 10 rows → `GetCurrentWindowHeight()=25` →
`ScrollHeight=20`:
- `SetScrollValue(1f)`: `scrollingContent.localPosition.y = 0` → row-0 at top
- `SetScrollValue(0f)`: `scrollingContent.localPosition.y = 20` → content shifted 20 units **up**
  relative to `RowsContainer`, bringing row-0 to `RowsContainer.localY + 20` world units —
  at approximately the same world-Y as the `Title` element (depending on camera placement),
  which explains the observed overlap.

**Why / Side-Effects:** Directly mutates `scrollingContent.localPosition.y` and calls
`_scrollable.UpdateContainingElements(y)`. Our `UpdateContainingElements` is a no-op
(empty body, matching IB's `EntriesList` implementation). No layout components are touched.

---

### `LateUpdate()`

**What:** Every frame: `UpdateScrollHeight()` → `UpdateScroll()` → conditionally
`UpdateArrows()` + `UpdateScrollbar()`. Mouse/joystick scroll input is processed here.

**How (decompiled, abbreviated):**
```csharp
private void LateUpdate()
{
    if (_scrollable != null)
    {
        UpdateScrollHeight();   // recalculates ScrollHeight every frame
        UpdateScroll();         // reads mouse wheel + joystick, calls MoveScroll()
        if (positionOrHeightChanged)
        {
            UpdateArrows();
            UpdateScrollbar();
        }
    }
}
```

`MoveScroll(float scrollValue)` clamps `scrollingContent.localPosition.y` to `[minScrollPos, ScrollHeight]`
and calls `SetScrollablePosition`. The clamp is what makes mouse-wheel feel correct once
`ScrollHeight` is non-zero. If `ScrollHeight = 0` (content smaller than window), no scrolling occurs.

**Why / Side-Effects:** Mouse-wheel scroll only works IF `_scrollable` is non-null AND
`ScrollHeight > 0`. Both require: (a) `_scrollable` is wired, and (b) `GetCurrentWindowHeight()`
returns a value > `windowHeight`. Without `SetScrollValue` after content spawn, `LateUpdate`
still recalculates `ScrollHeight` on the next frame — but the initial `scrollingContent.localPosition.y`
may be in an undefined state from a previous open.

---

### `ResetScroll()`

**What:** Public helper that calls `SetScrollValue(1f)` — hard-coded to scroll top.

```csharp
public void ResetScroll()
{
    SetScrollValue(1f);
}
```

**Why:** This public method is the canonical way to reset scroll position to top.
Equivalently: `scrollWindow.ResetScroll()` does exactly what we need.

---

## Part B — ItemBrowser Deep-Analyse

### EntriesList.cs — `SetEntries(...)` full call sequence

```csharp
public void SetEntries(ObjectDataCD objectData, List<ObjectEntry> entries, float scrollProgress = 1f)
{
    // 1. Clear previous rows + free pooled UIelements
    ClearList();

    if (entries.Count == 0)
        return;

    // 2. Create renderer for this entry type
    var firstEntry = entries[0];
    _renderer = firstEntry.CreateRenderer();

    // 3. Populate renderer state (SetEntries), returns false if no display component found
    if (!_renderer.SetEntries(this, objectData, entries))
        return;

    // 4. Spawn all row GameObjects into container (sets localPosition, TotalHeight, etc.)
    RenderList();

    // 5. POST-RENDER: wire _scrollable, recompute height, reset scroll
    //    Comment says: "Update scroll height immediately, since it only happens normally every LateUpdate"
    //    Comment says: "Assign scrollable in case Awake hasn't been called on the scroll window yet"
    API.Reflection.SetValue(MiScrollable, scrollWindow, this);   // ← wire IScrollable
    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);   // ← compute ScrollHeight
    scrollWindow.SetScrollValue(scrollProgress);                 // ← reset to top (scrollProgress=1f default)
}
```

**Why `scrollProgress=1f`:** IB always resets to top when new entries are shown.
The parameter exists to restore a previously-remembered scroll position (not used in practice
for the main list — always called without argument, so default 1f applies).

**Why SetValue before SetScrollValue:** `_scrollable` must be non-null for `SetScrollValue`
to execute its `if (_scrollable != null)` guard.

---

### EntriesList.cs — `OnDisable()` + `OnDestroy()`

Both call `ClearList()` → `_renderer?.ClearList()` → frees pooled elements.
IB clears rows on disable, not on enable. Our code follows the same pattern:
`ClearRows()` is called in `HideUI()`.

---

### EntriesList.cs — IScrollable interface implementation

```csharp
public void UpdateContainingElements(float scroll) { }  // no-op
public bool IsBottomElementSelected() => false;
public bool IsTopElementSelected() => false;
public float GetCurrentWindowHeight() => _renderer == null ? 0f : Mathf.Abs(_renderer.TotalHeight) - 1f / 16f;
```

`GetCurrentWindowHeight()` returns the renderer's `TotalHeight` (negative in IB due to
downward layout), with a 1/16-unit trim. Our implementation returns `RowCount * RowHeight`
(positive) — functionally equivalent since `UpdateScrollHeight` uses `math.max(0, height - windowHeight)`.

---

### BasicEntriesListRenderer.cs — `ClearList()`

```csharp
public override void ClearList()
{
    TotalHeight = 0f;

    for (var i = _activePooledElements.Count - 1; i >= 0; i--)
    {
        var element = _activePooledElements[i];
        foreach (var pugText in element.GetComponentsInChildren<PugText>(true))
        {
            var wasActive = pugText.gameObject.activeSelf;
            pugText.Clear();                           // release pool slots
            pugText.gameObject.SetActive(wasActive);   // restore active state
        }
        ItemBrowserAPI.FreePooledElement(element);     // return to pool
    }
    _activePooledElements.Clear();
}
```

Our `ClearRows()` already ports this pattern (confirmed in commit `c3fa944`). Key point:
`TotalHeight` is reset to `0f` before `RenderList()` — our `RowCount` serves the same role.

---

### BasicEntriesListRenderer.cs — `RenderList()` / Row placement

Rows placed with:
```csharp
display.transform.SetParent(_list.container);          // parent = EntriesList.container
display.transform.localPosition = new Vector3(0f, TotalHeight - 0.625f, 0f);
TotalHeight -= displayHeight + ...;                    // TotalHeight goes increasingly negative
```

`container` is a child of `scrollingContent` (Scroll → Container in prefab).
`TotalHeight` starts at `0f` and goes increasingly negative. `GetCurrentWindowHeight()` returns
`Mathf.Abs(TotalHeight)`. This matches our pattern: rows at `y=0, -2.5, -5.0...` relative to
`Content` (our scrollingContent), `GetCurrentWindowHeight()` returns `RowCount * RowHeight` (positive).

---

### ItemBrowserUI.prefab — UIScrollWindow + scroll hierarchy

**UIScrollWindow (EntriesList scroll) configuration:**
```yaml
windowHeight: 10.8125
windowWidth: 5.625
windowLocalCenter: {x: -0.1875, y: -1.125}
minScrollPos: 0
scrollBar: {fileID: 849334545882587259}
cursorMustBeInsideWindowToScroll: 1
anyElementMustBeSelectedToScrollWithController: 1
autoHideScrollbar: 1
centerVertically: 0
```

**Hierarchy:**
```
ScrollAnchor (Transform, localPos: -0.1875, 4.1875, 0)
  └── Scroll (scrollingContent, localPos: 0, 0, 0)
        └── Container (localPos: 0, 0.0625, 0)  ← children = row GameObjects
```

**Notable:** There are NO layout-helper components (`RectMask2D`, `VerticalLayoutGroup`,
`SpriteMask`, or CK-specific clip components) anywhere in the scroll hierarchy. Layout is
100% manual via `localPosition` assignments in `RenderList()`. The IB prefab is structurally
identical to our setup except IB has an extra `Container` child inside `Scroll` —
rows are children of `Container`, not directly of `Scroll` (scrollingContent).

Our hierarchy: `RowsContainer (UIScrollWindow) → Content (scrollingContent, IScrollable) → rows`
IB hierarchy: `ScrollAnchor → Scroll (scrollingContent) → Container (IScrollable.container) → rows`

This is a cosmetic difference; functionally equivalent. Rows being children of a child of
scrollingContent vs. direct children of scrollingContent does not affect
`SetScrollablePosition` (which only moves `scrollingContent`).

---

## Part C — Cross-Reference: What IB does that we don't

### Call sequence comparison

| Step | IB (`EntriesList.SetEntries`) | Our `SpawnRows()` |
|------|-------------------------------|-------------------|
| 1 | `ClearList()` — free pooled elements | `ClearRows()` — destroy instantiated rows ✓ |
| 2 | `_renderer.SetEntries(...)` — populate renderer state | `catalog.GetByIndex(i)` loop — populate `_spawnedRows` ✓ |
| 3 | `RenderList()` — spawn row GameObjects | inline `Instantiate(rowPrefab, ...)` loop ✓ |
| 4 | `API.Reflection.SetValue(MiScrollable, scrollWindow, this)` | `API.Reflection.SetValue(MiScrollable, scrollWindow, content)` ✓ |
| 5 | `API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow)` | `API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow)` ✓ |
| **6** | **`scrollWindow.SetScrollValue(scrollProgress)`** (default `1f`) | **MISSING — not called** |

### Mismatch identified

**Single mismatch: missing `scrollWindow.SetScrollValue(1f)` call after `UpdateScrollHeight`.**

Without this call:
- `scrollingContent.localPosition.y` retains whatever value it had from the previous open.
- On first open: `y = 0.0` (prefab default) — works correctly by coincidence.
- On subsequent opens where the user had scrolled down: `y` remains at the scrolled position from
  the last `SetScrollablePosition` call in `LateUpdate`. First rows appear partially off-screen.
- When the scroll position happens to be `ScrollHeight` (bottom), and a fresh open re-calculates
  a different `ScrollHeight` (e.g., fewer items), the `y` value may be out of `[0, ScrollHeight]`
  range until `LateUpdate` clamps it.

The Iter-3 attempt called `SetScrollValue(0f)` — this is **bottom** of the list (argument
semantics: `lerp(ScrollHeight, minScrollPos, 0f) = ScrollHeight`). With 10 rows and
`RowHeight=2.5`, `ScrollHeight=20`, the content was moved **20 units up** in
`RowsContainer`-local space. Rows that start at `Content.localY=0` end up 20 units above the
`RowsContainer` origin, coinciding in world-space with the `Title` element (which is at
`y=3.0` relative to `root`). This confirms the "overlapping title" symptom.

**No prefab component is missing** — IB has no special layout helpers, masks, or clip
components that we lack. The hierarchy and component setup are structurally equivalent.

---

## Fix-Hypothesen (ranked by likelihood × low-risk)

### Hypothesis A: Replace `SetScrollValue(0f)` with `SetScrollValue(1f)` (= use top-of-list)

- **Fix-Format:** code-only
- **Concrete change in `ItemChecklistWindow.cs`, method `SpawnRows()`, after line 99:**

  Current state (Iter-3.5 worktree — `SetScrollValue` not yet called):
  ```csharp
  API.Reflection.SetValue(MiScrollable, scrollWindow, content);
  API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
  // ← missing call
  ```

  Fix — add one line:
  ```csharp
  API.Reflection.SetValue(MiScrollable, scrollWindow, content);
  API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
  scrollWindow.SetScrollValue(1f);   // ← ADD: reset to top; mirrors IB default scrollProgress=1f
  ```

  Alternatively, using the public convenience method (same effect):
  ```csharp
  scrollWindow.ResetScroll();        // ← equivalent: calls SetScrollValue(1f) internally
  ```

- **Why this should work:** `1f` maps to `lerp(ScrollHeight, minScrollPos, 1f) = minScrollPos = 0`,
  placing `scrollingContent.localPosition.y = 0`. Row-0 is at `Content.y=0`, Content is at
  `localPos (0,0,0)` relative to `RowsContainer` — first row shows at the top of the visible
  window. This is exactly what IB's `SetEntries(scrollProgress=1f default)` achieves.
  On every `ShowUI()` call the scroll resets to top, regardless of where the user had
  scrolled in a previous open.

- **Risk:** None for scroll position correctness. `LateUpdate` continues to process
  mouse-wheel after this call — `ScrollHeight` is already set correctly. No regression
  on Iter-2 wins (PugText pool fix, display layer). No regression on CoreLib module
  integration (the call happens inside `SpawnRows()`, which already runs after
  `UserInterfaceModule` has placed the window).

- **Effort:** XS (1 line)

---

### Hypothesis B: Use `scrollWindow.ResetScroll()` instead of reflection to ensure safety

- **Fix-Format:** code-only (refactor of the 3-call sequence)
- **Concrete change — full replacement of the post-spawn block in `SpawnRows()`:**

  Current (3 lines → stays 3 lines, just add the reset):
  ```csharp
  content.RowCount = _spawnedRows.Count;
  API.Reflection.SetValue(MiScrollable, scrollWindow, content);
  API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
  scrollWindow.SetScrollValue(1f);   // same as Hypothesis A
  ```

  Alternative without the reflection `MiScrollable` assignment (since `ItemChecklistContent.Awake`
  already wires `_scrollable` via reflection, making the `SetValue` call redundant):
  ```csharp
  content.RowCount = _spawnedRows.Count;
  // _scrollable already set by ItemChecklistContent.Awake (DefaultExecutionOrder -100)
  API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
  scrollWindow.SetScrollValue(1f);
  ```

  > Note: Removing the redundant `SetValue` is optional cleanup. Keep it for defensive parity
  > with IB's pattern ("in case Awake hasn't been called yet").

- **Why this should work:** Functionally identical to Hypothesis A. `ResetScroll()` is the
  public API for "go to top" and avoids hardcoding the `1f` meaning.
- **Risk:** Identical to Hypothesis A. `ResetScroll()` calls `SetScrollValue(1f)` — one
  indirection, same outcome.
- **Effort:** XS (1 line)

---

**Recommendation:** Apply Hypothesis A with `scrollWindow.SetScrollValue(1f)` (explicit value).
`ResetScroll()` is equivalent but the explicit `1f` makes the IB parity obvious to future readers.

---

## Reference Coverage

| Source | Coverage | Notes |
|--------|----------|-------|
| `UIScrollWindow.decompiled.cs` (360 lines) | Full | All methods read. Focused extraction: `SetScrollValue`, `UpdateScrollHeight`, `_scrollable`, `Awake`, `LateUpdate`, `SetScrollablePosition`, `MoveScroll`, `ResetScroll` |
| `EntriesList.cs` | Full | `SetEntries` call sequence analyzed line by line; `OnDisable`, `OnDestroy`, IScrollable methods, `LateUpdate` |
| `EntriesListRenderer.cs` | Full | Abstract base; no relevant state, pool lifecycle deferred to `BasicEntriesListRenderer` |
| `BasicEntriesListRenderer.cs` | Full | `ClearList`, `RenderList`, `AddEntry`, `AddDivider` all read; `TotalHeight` tracking confirmed |
| `ItemBrowserUI.prefab` | Partial — UIScrollWindow blocks + parent hierarchy only | File is 991 KB; full read would exceed memory limits. Extracted: UIScrollWindow component field values, scrollingContent/container/scrollable fileID cross-references, parent Transform blocks. The filter panel, history panel, and display prefabs were deliberately skipped — irrelevant to the scroll-reset bug. |
| `EntriesDivider.cs`, `EntriesUnavailableHeader.cs`, `EntriesView.cs`, `SwapCategoryButton.cs`, `EntryDescriptionButton.cs` | Skipped | Not referenced by `SetEntries` call-graph; irrelevant to scroll-position bug |
| `LootTable*` files | Skipped | Separate renderer type; not in primary `BasicEntriesListRenderer` path |
