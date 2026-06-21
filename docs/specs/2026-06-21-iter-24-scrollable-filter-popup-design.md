# Iter-24 — Scrollable + Collapsible Filter Popup — Design

Date: 2026-06-21
Status: Approved (brainstorming), pending implementation plan
Game version: Core Keeper 1.2.1.4

## Context & problem

The Filter popup (`FilterWidget : PopupWidget`, opened from the window header)
renders **every section fully expanded at once**. `PopupWidget.AutoSizePopup`
grows the panel downward from a fixed authored top edge (`_topY`) and hugs the
row stack with **no height cap and no clipping**. As the catalog grew — Iter-16.1
added the **Pets** category, Iter-16.2 added **Krabbeltiere (Critters)** — the
section list got taller and now overflows the window viewport.

Rough row census today (sections + headers + action):

| Section | Member rows |
|---|---|
| Discovery | 2 |
| Category | ~12 (10-bucket taxonomy + Pets + Critters) |
| Rarity | 5 (Poor…Epic) |
| Craftable | 2 |
| Possession | 2 |
| 5 section headers + "Clear all" action | 6 |
| **Total** | **~29 rows × 0.7u ≈ 20u** |

The window content area is only ~13.75u tall (camera viewport 16.875u), so the
fully-expanded popup overflows by roughly a third.

The frozen roadmap (`docs/roadmap.md` Iter-24) framed the fix as *"make the popup
pane scrollable (clip + scroll its RowContainer, mirroring the main list's
UIScrollWindow/SpriteMask pattern)"*. Brainstorming refined this: the roadmap
line predates the Pets/Critters growth, and pure scroll is not the only lever.

## Goal

The filter popup must **never overflow the viewport**, and every filter row must
remain reachable. Two layers combine:

- **Scroll (primary):** the popup is clipped to a reduced max height and scrolls.
- **Collapse (convenience):** sections are collapsible so the user can shorten
  the popup by hiding dimensions they don't care about.

## Decisions (settled in brainstorming)

- **D1 — Both layers, scroll-primary.** With default-all-open (D3) and a reduced
  cap (D4), scrolling is the *normal* case, not an exception; collapse is a
  shortening convenience layered on top.
- **D2 — Collapse is multi-open, NOT a strict accordion.** Several sections may
  be open simultaneously.
- **D3 — Default: all sections open.** Matches today's behaviour on first open.
- **D4 — Reduced max height.** `MaxPopupHeight` is a serialized field (declared
  on `PopupWidget`) whose **default lives on the `Dropdown` skeleton prefab,
  set to the height of 6 entries** (6 × `rowSpacing` 0.7u ≈ **4.2u**), down from
  the effective ~13.75u ceiling. With ~29 filter rows this makes scrolling the
  normal case. Because it is a serialized prefab field, **each variant
  (`Sort.prefab`, `Filter.prefab`) can override the cap** without code — the
  variant inherits the skeleton default unless it sets its own. Tunable in-game.
- **D5 — Scroll lives in the Dropdown *base*, via manual translate (Weg 2), so
  both variants inherit it.** See § Architecture / "Why manual translate".
- **D6 — Collapse logic stays Filter-specific** (Sort has no sections).
- **D7 — Scroll fully deactivates when content fits.** `_scrollActive =
  contentH > MaxPopupHeight`; when false the entire scroll path is off (see
  § The `_scrollActive` gate).
- **D8 — "Clear all" scrolls in-flow** (not pinned — YAGNI).
- **D9 — Scroll resets to top** when the popup opens.
- **D10 — Collapse state is `static` per session**, like the existing Sort-mode
  and filter-dimension state (survives popup open/close, resets on game restart).

## Architecture

### Component placement

| Concern | Lives in | Rationale |
|---|---|---|
| Scroll *mechanism* (cap, translate, wheel, handle math, `_scrollActive`) | **`PopupWidget`** (base C#) | generic; dormant unless content overflows |
| Collapse / accordion *logic* | **`FilterWidget`** | Sort has no sections (D6) |
| SpriteMask + scrollbar GO subtree | **`Dropdown.prefab`** (skeleton) | Weg 2 has no Awake self-disable trap → both variants inherit (D5) |
| Collapse state (set of open sections) | `static` in `FilterWidget` | session-stable (D10) |

This respects the Iter-13/18 chrome rule: the skeleton carries only pure
GameObjects/sprites/colliders that variants *add to*, never *remove from*. Sort
inherits the scroll chrome but never engages it (D7), so no variant ever needs a
`m_IsActive:0` deactivation override (avoiding the Iter-18 dangling-override
trap).

### Why manual translate (Weg 2), not CK's `UIScrollWindow` (Weg 1)

Two reasons reuse of the main-list `UIScrollWindow` path was rejected:

1. **The Awake self-disable invariant blocks base placement.**
   `UIScrollWindow.Awake` sets itself `enabled = false` **permanently** if its
   serialized `scrollable` reference does not resolve to an `IScrollable`
   component on the *same* GameObject. The Dropdown skeleton deliberately has no
   widget component (widgets are added by the Sort/Filter variants), so a
   `UIScrollWindow` placed in the skeleton would have an unresolved `scrollable`
   and self-disable on load. Manual translate has no such component.
2. **The popup needs no virtualization.** `UIScrollWindow` + `IScrollable`
   exists to virtualize the main list's ~10,900 entries into a ~5-row pool. The
   popup has ≤~29 real row GameObjects; translating a small fixed stack under a
   mask is the right-sized tool.

### Scroll mechanism (in `PopupWidget`)

- **Cap.** `AutoSizePopup` clamps the panel height:
  `h = min(rowCount * rowSpacing, MaxPopupHeight)`. The panel still top-aligns to
  the authored `_topY` and grows downward (existing logic), just bounded.
- **`_scrollActive` gate.** `_scrollActive = (rowCount * rowSpacing) >
  MaxPopupHeight`, recomputed each `AutoSizePopup`. See its own section below.
- **Two `rowContainer` positioning modes — the load-bearing reconciliation.**
  Today `AutoSizePopup` **centres** the stack:
  `rowContainer.localPosition.y = (FirstRowOffset + (rowCount−1)/2f) * rowSpacing`.
  Scrolling instead needs **top-alignment + a scroll offset** (row 0 flush to the
  mask top, the stack translated up by `scrollOffset`). So `AutoSizePopup`
  **branches on `_scrollActive`:**
  - `_scrollActive == false` → today's centring formula (unchanged behaviour).
  - `_scrollActive == true` → top-align row 0 to the mask top, then
    `rowContainer.localPosition.y = baseTopY + scrollOffset`, with `scrollOffset`
    clamped to `[0, contentH − cap]` (0 = top). The centring formula is **not**
    applied in this mode.
- **Translate driver.** When active, `scrollOffset` is moved by:
  - **Mouse wheel:** `Input.mouseScrollDelta.y` applied only while the cursor is
    over the popup (bounds check against the panel rect), clamped to the range.
  - **Draggable handle:** a hand-rolled handle (see below).
- **Reset on open.** `SetOpen(true)` (or `OnPopupOpened`) zeroes `scrollOffset`
  so the popup always opens at the top (D9).

### The `_scrollActive` gate (D7)

When `_scrollActive == false` the **entire scroll path is stilled**:

- SpriteMask GO → inactive (no stencil write).
- Scrollbar GO → inactive (handle hidden).
- No wheel handling, no `LateUpdate` translate.
- `rowContainer` at its base position; panel hugs the stack exactly as today.

Consequence: **mask-in-mask only engages on genuine overflow.** Sort's 3-row
popup and a collapsed/short Filter popup have *zero* mask interaction and render
exactly as they do today — which also de-risks the Phase-0 tracer, since the
risky nested-mask path is gated behind a condition that rarely fires.

### Clipping — SpriteMask (primary) with a culling fallback

- **Primary:** a popup-local `SpriteMask`, top-aligned to `_topY`, height =
  `MaxPopupHeight`. Popup rows render `maskInteraction = VisibleInsideMask`
  within a **dedicated sorting range above** the window's existing mask range
  (40..55), so the window mask does not also clip the popup and vice-versa.
- **The one real risk — nested SpriteMask sorting ranges.** Whether two
  SpriteMasks with distinct custom ranges cleanly isolate is not reliably
  predictable from static prefab reading (cf. the Iter-5 scrollbar's three
  load-bearing mask facts). **Phase 0 is a throwaway tracer** (mirroring how
  Iter-13 first proved nested prefabs survive the ModBuilder→AssetBundle
  round-trip): a dummy mask + ~30 dummy rows, verified in-game to clip + scroll,
  **before** wiring the real filter content.
- **Fallback (if the nested mask proves intractable):** row-granularity culling —
  deactivate rows whose scrolled position falls outside `[0, cap]`. No mask, dead
  simple; cost is that boundary rows pop in/out at row granularity instead of
  pixel-clipping. Acceptable for a filter popup, but only as a fallback.

### Hand-rolled scrollbar handle

CK's `ScrollBar.UpdateScrollBarPosition` is driven by `UIScrollWindow.LateUpdate`,
which we don't use (Weg 2). So the handle is hand-rolled in `PopupWidget`
(~25 LoC):

- **Size** ∝ `cap / contentH` (visible ratio), with a sane minimum.
- **Position** ∝ `scrollOffset / (contentH − cap)`.
- **Drag:** while the handle's `leftClickIsHeldDown` (the `ButtonUIElement`
  signal already used by the mod's clickable controls), map the mouse Y delta to
  a scroll-offset delta, clamped to range.
- Self-hides via the `_scrollActive` gate (the scrollbar GO is inactive when no
  overflow), so Sort never shows a handle.

### Collapse / accordion (in `FilterWidget`)

- **`headerTemplate` prefab restructure (a distinct authoring task).** The
  section header is currently a plain PugText row. Making it clickable means the
  template GO must gain a **3D `BoxCollider`** (`!u!65` — CK `UIMouse` raycasts
  in 3D), a **caret `SpriteRenderer`** (expand/collapse glyph, the existing
  `caretOpen`/`caretClosed` sprites), and a **`ClickButton`-derived component**
  whose `OnClick` toggles that section. All pooled header clones inherit it.
- **Stable section key — NOT the localized label.** `Member.section` is the
  **localized display string** (`RebuildList` renders it directly via
  `RenderNoWrap(lastSection)`), so keying collapse state on it would **break on
  language change** (a loc-change re-bakes → new strings → state lost/mismatched).
  Collapse state must key on a **stable section identifier** (a section enum / id
  carried alongside the label), decoupled from the rendered text.
- **State:** a `static HashSet<SectionId>` of open sections (or a closed-set),
  default = all open (D3, D10); survives re-bake/loc-change because the key is
  language-independent.
- **`RebuildList`** skips the member rows of closed sections (headers always
  render); `AutoSizePopup` is fed the reduced visible-row count, so the cap +
  `_scrollActive` gate operate on the *currently visible* stack. Collapsing
  enough sections drops `contentH` below the cap → scroll deactivates.

## Edge cases & integration points

- **A — Click-outside-close must become genuinely *outside* (blocking).**
  `PopupWidget.LateUpdate` currently closes the popup on **any** left mouse-down
  while open+armed (`if (Input.GetMouseButtonDown(0)) SetOpen(false)`). Iter-24
  adds two **inside** interactions — the **section-header collapse click** and the
  **handle drag-start** — both of which are mouse-downs that must **not** close
  the popup. The close logic must gain a **bounds check against the popup panel
  rect**: only a mouse-down *outside* the panel closes. (The code comment claims
  inside clicks "already set `_open=false`", but that only holds for Sort's
  option clicks; Filter's `OnMemberClicked` does not close, so by the current
  code a checkbox click likely closes the Filter popup too.) **Phase 0 must
  observe the current checkbox-click behaviour first**, then the bounds check
  fixes header-click, handle-drag, *and* (incidentally) checkbox toggling.
- **E — Pooled-row mask membership.** Rows are cloned at runtime from
  `checkboxTemplate` / `headerTemplate` / `actionTemplate`, so each **template**
  must already carry the popup-mask `maskInteraction` (`VisibleInsideMask`) and a
  sorting order inside the popup-mask range, or the clones won't be clipped.
  PugText children follow the main-list rule (`style.sortingLayer = "GUI"`). The
  **scrollbar sprites need `maskInteraction: None`** (the Iter-5 fact) so the
  popup mask does not clip the handle/track away.
- **F — Mouse-wheel ownership.** The main list also scrolls on the wheel
  (`UIScrollWindow`). While the popup is open **and** the cursor is over it, the
  wheel must drive the popup *only* — the main list must not also scroll. The
  cursor-over-popup bounds check (used for the wheel driver) is the gate;
  verify the main list stays put when scrolling the popup.
- **G — Existing sorting-order map.** Iter-12 set footer order 50, popup BG 54,
  and all PugText at `orderInLayer 9999` on the `"GUI"` layer. The new
  popup-mask range (above the window mask's 40..55) interacts with these — the
  popup BG, rows, scrollbar, and footer ordering must be re-verified in Phase 0,
  not assumed from static reading.

## Calibration values (tuned in-game)

- `MaxPopupHeight`: serialized on the `Dropdown` skeleton prefab, **default =
  6 entries** (6 × 0.7u ≈ **4.2u**), down from the ~13.75u effective ceiling.
  Final value tunable in-game.
- Popup-mask sorting range, handle min-size, wheel step: calibrated in Phase 0 /
  in-game, following the Iter-5/Iter-9 precedents.

## Out of scope

- Strict single-open accordion (rejected in favour of D2 multi-open).
- Pinning "Clear all" outside the scroll region (D8).
- Reworking Sort's popup — it inherits the dormant scroll chrome unchanged.
- Touching the main list's `UIScrollWindow`/virtualization.

## Testing (CK 7-phase, no unit-test framework)

0. **Mask tracer** — throwaway dummy mask + ~30 dummy rows; verify in-game that
   the popup clips to the cap and scrolls (wheel + handle) before real wiring.
   Also record the **current** checkbox-click behaviour (does it close the
   popup?) so edge-case A's bounds-check fix is measured, not assumed; sanity-
   check the order map (edge-case G).
1. Build → grep `Player.log` for `error CS|Build complete|CompileFailed`
   (Editor compile ≠ sandbox pass).
2. In-game (fake-ID dev build), all sections open: popup clips to `MaxPopupHeight`,
   scrolls smoothly via wheel and via handle drag; handle size/position correct.
   **Header click and handle drag-start do NOT close the popup** (edge-case A);
   **checkbox toggle keeps the popup open** after the bounds-check fix.
3. **Wheel ownership** (edge-case F): wheel over the open popup scrolls the popup
   only — the main list stays put.
4. Collapse sections: popup shortens; once content < cap, scroll fully
   deactivates (no handle, no mask, container at base) per `_scrollActive`.
   Collapse state **survives a language change** (stable section key, edge-case C).
5. Reset-on-open: reopening the popup starts at the top.
6. Sort popup unchanged: no handle, no mask interaction, renders as before;
   click-outside still closes it.
7. No pool leak across repeated open/close (PugText `Clear()` on teardown path
   intact); collapse state persists within the session, resets on restart.
