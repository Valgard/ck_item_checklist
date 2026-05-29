# ItemChecklist Gotchas

Non-obvious traps that have caused real bugs in this codebase. Read these
before changing UI, prefabs, or layer assignments.

## UI / Scroll

### SetScrollValue(0f) = BOTTOM, not top

`UIScrollWindow.SetScrollValue(0f)` scrolls to the **bottom** of the list,
not the top. Iter-3 passed `0f` and rows overlapped the title element (content
shifted ~20 units up).

**Correct:** `scrollWindow.SetScrollValue(1f)` or `scrollWindow.ResetScroll()`
for top-of-list. Never pass `0f` unless you specifically want the bottom.

See `docs/architecture.md § SetScrollValue Semantics` for the lerp math
explanation.

### UiController / VirtualScrollList are deleted — do not recreate

`VirtualScrollList.cs`, `UiController.cs`, and `ItemRowView.cs` were
permanently deleted in Iter-2. They were replaced by CK's native
`UIScrollWindow` + `IModUI` pattern + `ItemRow.cs`.

Do not recreate — the old uGUI-based recycler is structurally incompatible
with CK's `Physics.Raycast`-based `UIMouse`. Any Canvas-derived component
is invisible to CK's input system. See `CLAUDE.md § Mod-Specific Gotchas`
(uGUI structural failure) for the full explanation.

## Mod Loading

### Opening the in-game Mods menu wipes the fake-ID dev install

If the in-game **Mods menu** is opened while a fake-ID local dev build is
installed, the mod.io client syncs subscriptions against the real catalog,
finds no entry for the fake ID, and **deletes the local files + ZIP**. The
game must then be restarted without the mod.

**Safe actions:** game start, world load, gameplay — none of these trigger
the sync. **Only the Mods menu** triggers it.

**Recovery:** re-run the install script:
```bash
source .envrc && ../utils/build.sh
```
This rebuilds and re-installs all three fake-ID locations.

**Two-step scenario** (subscribing to a real mod on mod.io): the new
subscription lands only when the Mods menu is opened — the same sync that
applies the subscription wipes every fake-ID mod. Plan for it as a two-step:
open the menu, let the change land, then rebuild each fake-ID mod.

See the parent `CLAUDE.md § Fake-ID dev install` for the full fake-ID
mechanism.

## SpriteMask Clipping (Iter-3.5b Aborted-Iter Lessons)

The Iter-3.5b iteration was aborted after pre-flight discovered the
following structural blockers. Documenting them prevents re-attempts.

### "UI" sorting layer does not exist — the named layer is "GUI"

There is **no** named sorting layer `"UI"` in
`CoreKeeperModSDK/ProjectSettings/TagManager.asset`. The sorting layer used
by CK UI elements is named `"GUI"` (uniqueID `1241602095`).

Layer 5 in Unity's tag-layer system is called `"UI"`, but that is a
**tag-layer** (used for `Physics.Raycast` filtering), not a sorting layer.
`"GUI"` (sorting layer) and Layer 5 (tag-layer) are entirely separate
concepts.

Iter-3.5b was designed assuming a `"UI"` sorting layer and was aborted when
Task 1+2 pre-flight revealed the layer does not exist. Always verify sorting
layer uniqueIDs against `TagManager.asset` before hardcoding them into prefab
YAML.

### Pure-runtime SpriteMask cannot cover a mixed Default/GUI renderer stack

A `SpriteMask` with a Custom Sorting-Layer Range of `40..55` only clips
renderers that are **already in that sorting layer**. If any `SpriteRenderer`
is in `"Default"` (order 0) and `PugText`s resolve to `"GUI"` (sentinel
`int.MinValue`), a mask set to GUI range `40..55` clips nothing in `"Default"`.

**Solution (Iter-3.5c approach):** prefab-edit ALL renderers — both
`SpriteRenderer` components and `PugText.style.sortingLayer` fields — to
layer `"GUI"` with `orderInLayer` values within `40..55` **before** installing
the mask. A pure-runtime approach cannot bypass this requirement.

PugText YAML grep pattern: `sortingLayer:` / `orderInLayer:` (NOT
`m_SortingLayer` / `m_SortingOrder` — those are SpriteRenderer YAML keys).

### mask_sprite.png must use spritePixelsToUnits: 1, not 16

The `SpriteMask` sprite (`mask_sprite.png`, a 1×1 white PNG) **must** have
`spritePixelsToUnits: 1` in its `.meta` file. With the SDK default `PPU=16`,
the sprite geometry is `0.0625` units. Applying a Transform scale of `(11, 6)`
produces a `0.69 × 0.375` unit mask instead of the intended `11 × 6` window
coverage — the mask is essentially invisible.

Always set `spritePixelsToUnits: 1` for any mask sprite that needs to cover
a large screen area in CK's `1/16`-unit grid.

### Texture2D + Sprite.Create runtime mask approach was aborted

The Iter-3.5b plan was to generate the mask sprite at runtime via
`new Texture2D(1, 1)` + `Sprite.Create`. This approach was aborted because
the render-domain problem (mixed `"Default"` / `"GUI"` layers) cannot be
solved without prefab edits regardless of how the sprite is created.

Do not revisit this approach without first ensuring all renderers are
consolidated into the same sorting layer. The sprite-creation mechanism is
not the problem — the layer separation is.

### PugText.style has no SetSortingLayer setter — direct field write required

`PugText` has a public `SetOrderInLayer(int)` method but **no public setter
for `sortingLayer`**. To set the sorting layer on a `PugText` at runtime,
write `style.sortingLayer` directly — `PugText.style` is a public field:

```csharp
pugText.style.sortingLayer = 1241602095;  // "GUI" uniqueID
pugText.style.orderInLayer = 48;
```

In prefab YAML, use `sortingLayer:` / `orderInLayer:` keys. Do not use
`m_SortingLayer` / `m_SortingOrder` — those are `SpriteRenderer` YAML keys
and are silently ignored on a `PugText` component.
