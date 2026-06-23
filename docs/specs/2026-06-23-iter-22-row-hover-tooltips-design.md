# Iter-22 — Row-Hover Tooltips — Design

**Date:** 2026-06-23
**Status:** Approved (brainstorming) — retention ADR-gated, decide at merge
**Branch (planned):** `iter-22`

## Problem

Hovering a checklist row shows nothing. The user wants the item tooltip that
CK's normal inventory slots show on hover — name, description, stats — so a row
can be inspected without owning the item or opening it elsewhere. Requested
2026-06-21; logged tentative pending a feasibility + spoiler measurement.

## Feasibility (measured, 2026-06-23)

CK's tooltip system is **selection-driven and data-driven, not entity-driven**.
`UIMouse.UpdateHoverText` (`Pug.Other` ~356342) reads
`Manager.ui.currentSelectedUIElement` and calls its virtual `UIelement`
methods — `GetHoverTitle()`, `GetHoverDescription()`, `GetHoverStats(bool)`,
`GetContainedObject()` — and renders the result itself. It needs **no live ECS
entity**: only a selected `UIelement` that returns a `ContainedObjectsBuffer`
carrying an arbitrary `ObjectDataCD(objectID, variation)`.

`UIMouse.UpdateMouseUIInput` (`Pug.Other` ~355773) **re-derives the selection
every frame** from a 3D `Physics.Raycast` against `ObjectLayerID.UILayerMask`
(~355871): the nearest visible (`isVisibleOnScreen`) `UIelement` collider under
the cursor becomes `currentSelectedUIElement` via `TrySelectNewElement`; when the
cursor leaves, `DeselectAnySelectedUIElement()` clears it. The tooltip text is
positioned **cursor-relative** (~357077), not relative to the selected element's
transform.

Working precedent: ItemBrowser's `ItemBrowserSlot : SlotUIBase` overrides exactly
these virtuals to render tooltips for **catalog items not in any inventory** — the
same "virtual item" situation as the checklist. The whole path is read-only
(`PugDatabase` lookups); no `System.IO`, reflection-emit, or `Manager.saves`
access — sandbox-safe.

## Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| Q1 | Spoiler gate for undiscovered (`???`) rows | **No tooltip now**; minimal "??? — not yet discovered" tooltip logged as a later UX option | Leak-proof, consistent with existing spoiler gates (em-dashed Level/Value, hidden icon). The minimal variant is a clearly isolated extension point. |
| Q2 | Hover/selection wiring | **Native (Approach A)** — rows become selectable slots; `UIMouse` raycasts, selects, and renders the tooltip | The per-frame raycast re-derivation is the *engine* of this approach, not an obstacle: zero manual selection-state code. Mirrors the ItemBrowser/`SlotUIBase` precedent. |
| D1 | `ItemRow` base type | **Stays `UIelement`** + delegates to a shared `RowTooltipFormatter` (not `ItemRow : SlotUIBase`) | A2 would inherit `SlotUIBase`'s icon/amount/marker machinery and its expected serialized fields, colliding with the custom row layout (NullRefs, double icons). A1 keeps the row slim and the risk low. |
| D2 | Hover highlight | **Subtle highlight** on the active row | Mirrors CK inventory slots; a readability aid on a dense list (which row the tooltip refers to). Selection-marker infra already exists in the mod; easily toggled. |

**Unified spoiler rule:** the tooltip (and the highlight) appear **iff the row
shows a real name** (`_nameKnown` — the same flag that drives `???`-vs-name). This
covers every case with one predicate: undiscovered → inert; pet species known but
this skin uncollected → name visible → species tooltip shows (no spoiler — skins
are recolors of one shared `ObjectID`).

## Architecture (Approach A, native)

Each `ItemRow` (already a `UIelement`) gains a **3D `BoxCollider`** on the UI
layer and stores its current `(objectId, variation, nameKnown)` at `Bind`. CK then
does everything:

1. **Hover** — the per-frame raycast hits the row's collider (row is
   `isVisibleOnScreen`: active + non-zero scale).
2. **Select / deselect** — `TrySelectNewElement` selects the row; leaving the row
   calls `DeselectAnySelectedUIElement()` → tooltip + highlight vanish natively.
3. **Render** — `UpdateHoverText` calls the row's four hover virtuals; the row
   delegates computation to the shared formatter; CK renders name/description/
   stats/rarity at the cursor.

Our surface area: (a) a collider per row, (b) `(objectId, variation, nameKnown)`
fields on the row, (c) four virtual overrides delegating to the formatter,
(d) a subtle highlight gated on `_nameKnown`.

## Components

1. **`ItemRow` (modified)** — adds a 3D `BoxCollider` (UI layer; sized to the row
   background like the existing `RowHeight` read; Z pulled forward for the raycast,
   per the `ButtonUIElement`/`ScrollBarHandle` convention). Adds persistent
   `_objectId`, `_variation`, `_nameKnown`, set in `Bind`. Overrides
   `GetContainedObject`/`GetHoverTitle`/`GetHoverDescription`/`GetHoverStats`,
   each delegating to the shared `RowTooltipFormatter` for its
   `(objectId, variation)`. Sets `collider.enabled = _nameKnown` in `Bind` so
   undiscovered rows are fully inert. Shows the highlight only when `_nameKnown`.
2. **`RowTooltipFormatter` (new, one shared instance)** — a thin `SlotUIBase`-backed
   helper that, given `(objectId, variation)`, produces the `ContainedObjectsBuffer`
   plus the title/description/stats `TextAndFormatFields` following the ItemBrowser
   recipe. One instance owned by `ItemChecklistContent`, reused by every row — so a
   row never carries slot machinery itself. Returns `null` on an invalid
   `(objectId, variation)` (→ no tooltip, no throw).
3. **Prefab change** — `ItemRow.prefab`: add the collider only; the 11 existing
   child renderers/texts are untouched.
4. **Pool integration (`ItemChecklistContent`)** — `Bind` (runs every recycle)
   updates `_objectId/_variation/_nameKnown` + `collider.enabled`; the virtuals read
   these live fields, so there is no accumulated hover state to reset. A shrinking
   pool deactivates the row GameObject (and its collider) → no stray raycast hits.

## Data flow (per frame)

1. `UIMouse` raycast hits the row collider → row becomes `currentSelectedUIElement`
   → highlight on.
2. `UpdateHoverText` → `row.GetContainedObject/Title/Description/Stats` → row
   delegates to `RowTooltipFormatter` with `(objectId, variation)` →
   `TextAndFormatFields` → CK renders the tooltip at the cursor.
3. Cursor leaves the row → `DeselectAnySelectedUIElement()` → tooltip + highlight
   off.

## Edge cases (each an in-game test point)

- **Pool recycling** — `Bind` refreshes the fields + `collider.enabled` on every
  rebind; virtuals read live fields → never a stale tooltip.
- **Tooltip leak under open popups** — with a Sort/Filter popup open, no row tooltip
  may show through it. Native protection holds only if the popup background carries a
  nearer collider — **verify in-game**; fallback: suppress row tooltips while a
  `PopupWidget` is open (open-state already tracked).
- **Search-field focus** — mere hovering does not steal focus (only `interactDown`
  deactivates the active input field, ~356112). *Clicking* a row would deactivate the
  search field — a minor, acceptable change (rows have no own click action).
- **Window closed / not in playable world** — `HideUI` deactivates the rows →
  colliders inactive → no tooltips. Native.
- **Pet-skin rows** — gated on species discovery (the `_nameKnown` flag), not on
  skin-collected; a known species shows its tooltip even for an uncollected skin.

## Error handling & sandbox

- Formatter guards an invalid `(objectId, variation)` → returns `null`, no exception.
- All read-only `PugDatabase` lookups; no banned APIs (`System.IO`, reflection-emit,
  `Manager.saves`). **Editor compile ≠ sandbox pass** — Phase-1 smoke test must
  confirm `safetyCheck=True`, 0 `CompileFailed`.

## Testing (CK 7-phase, in-game, fake-ID 9999997)

1. Clean sandbox compile (`safetyCheck=True`, 0 `CompileFailed`).
2. Hover a discovered item row → tooltip (name/description/stats) at the cursor +
   row highlight.
3. Hover a `???` row → fully inert (no tooltip, no highlight).
4. Hover a pet-skin row (species known, skin uncollected) → species tooltip.
5. Scroll, then hover recycled rows → correct per-item tooltip (no stale data).
6. No tooltip leaks through an open Sort/Filter popup.
7. Search focus survives hovering; tooltip vanishes on leave / window close.

## Out of scope / deferred

- Minimal "??? — not yet discovered" tooltip (Q1 later-UX option) — extension point:
  enable the collider for `!_nameKnown` + a minimal title override.
- Approach A→B reconsideration if the native selection UX proves undesirable.
- Iter-17 per-variation/skin tracking is unrelated.

## References

- CK tooltip path: `Pug.Other` `UIMouse.UpdateHoverText` ~356342,
  `UpdateMouseUIInput` ~355773 (raycast ~355871), `TrySelectNewElement` ~356116;
  `UIManager.OnUIElementSelected` ~273416, `DeselectAnySelectedUIElement` ~273433;
  `UIelement` hover virtuals ~357960. (decompile: `~/Projects/checkouts/CoreKeeperDecompile/Pug.Other.decompiled.cs`)
- Precedent: `~/Projects/checkouts/ItemBrowser/.../Browser/ItemBrowserSlot.cs`.
- Mod row code: `unity/ItemChecklist/ui/ItemRow.cs`,
  `unity/ItemChecklist/ui/ItemChecklistContent.cs`;
  spoiler flag: `DiscoveredState.IsDiscovered`.
