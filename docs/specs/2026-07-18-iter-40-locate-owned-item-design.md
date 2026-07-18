# Iter-40 — Locate an owned item in the base

Surface **where** a discovered/owned item is stored so the player can walk to
it, instead of hunting through chests. A directional HUD arrow (one per holding
container) points at the item's storage while the player is in normal gameplay.

## Problem

The Iter-20 possession ledger already records, per world tile `(x, z)`, the
contents of every base container (`x,z|id:count,…`, persisted per character).
But nothing surfaces that location to the player: finding where a specific item
is stored means manually grepping the ledger file — the concrete motivation was
locating the Caveling Divining Rod at tile `(28, 1)` on 2026-07-14.

This is a **UI / surfacing** iteration, not new tracking: the data exists. The
only missing pieces are a projection of the ledger by location, a small
"currently tracked item" state, and a HUD that draws arrows toward the holding
tiles.

**Feasibility is grounded, not assumed:**

- `PossessionScanner.cs:168` derives the ledger tile key as
  `PossessionLedger.Key(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z))` — the
  tile key **is** the rounded entity world position. The inverse is therefore
  exact: a ledger tile `(x, z)` maps back to a container world position
  `float3(x, 0, z)`. No new scan is needed to aim an arrow.
- `PossessionScanner.cs:159` already reads the player world position
  (`new Vector2(pos.x, pos.z)`), and the sibling `caveling-divining-rod` reads it
  via `Manager.main.player`. Both ends of the bearing vector already exist.
- `caveling-divining-rod` already ships the entire directional-HUD mechanic
  (`ArrowRingRenderer.Render(float3 playerWorldPos, IReadOnlyList<float3> spots)`
  — pooled arrows on a ring, bearing `atan2(delta.z, delta.x)` in the XZ plane,
  distance-lerped scale/alpha), proven sandbox-safe. `ArrowRingRenderer` is the
  structural twin of ItemChecklist's own `ItemChecklistHud` (both born from the
  Iter-11.5 HUD code).

## Contract decisions

Settled with the user during brainstorming:

- **Presentation = directional HUD arrows.** One arrow per holding container
  (all holding tiles, not just the nearest) — a port of the divining-rod's
  `ArrowRingRenderer`. Chosen over a static tooltip-text line (weak "walk to it"
  UX; a bearing snapshot goes stale the moment you move) and a CK map pin (most
  native but undocumented map-marker system, decompile-heavy, no precedent,
  highest sandbox risk).
- **Trigger = left-click a row toggles tracking**, with the hover tooltip
  carrying the affordance and a highlight marking the tracked row. The row is
  otherwise click-inert (`ItemRow` has no click handler today), so the trigger
  costs no new prefab element or art. A dedicated per-row locate-button was
  rejected (permanent visual noise on an already-dense row for a rarely-used
  action; real Editor/art work); a hover+keybind trigger was rejected (CK's
  selection is hover-based, so the cursor is already on the row — a click is the
  lower-effort confirm than "hover + reach for a key"; also nudges the open
  Iter-26 focus-race territory).
- **One tracked item at a time.** Clicking a row makes it the sole tracked item
  (replacing any previous); clicking it again untracks; clicking another
  trackable row switches. Multiple simultaneous tracked items was rejected —
  arrows from different items are indistinguishable without per-item colour
  coding + a legend (the CK arrow is one sprite and the alpha channel is already
  used by the distance fade), which drifts toward a tracking panel (scope creep).
- **Session-only persistence.** The tracked item is a transient "find X now"
  state; it survives closing the window (arrows show in gameplay) but is cleared
  on a full quit/reload. No persistence code, no ledger schema bump.
- **Global "cancel tracking" hotkey (gameplay).** A rebindable keybind under the
  existing `ItemChecklist` control category (Iter-34) clears tracking without
  reopening the checklist. This serves a state the click path cannot (cursor not
  in the list). **Unbound by default**, assignable in Controls (collision-free;
  the F1 + click-the-tracked-row path is the always-available fallback).
- **Distance number on the arrow: out of MVP** (YAGNI — direction + fade-free
  visibility is the core; a numeric readout is a trivial follow-up).

## Design

Three data-flow building blocks (Parts A–C), the interaction surface (Part D),
and the edge cases (Part E). Everything rides existing mechanics; nothing new is
persisted.

```
Row left-click ─→ s_trackedObjectId = id ─→ (window closes)
                                              │  every frame, in gameplay
   s_ledger.TilesHolding(id) ─→ float3[] ─→ TrackerHud.Render(playerPos, targets)
                                              └─→ one rotating arrow per container
```

### Part A — Reverse-index projection (`TilesHolding`)

`PossessionView` collapses the ledger to `_totals: id→count` and discards
location. Iter-40 adds a projection that reads the **existing**
`PossessionLedger._containers` (`tile → {id→count}`) the other way:

```csharp
// PossessionLedger
public List<(int x, int z)> TilesHolding(int objectId)
```

Returns every container tile whose contents include `objectId` (count ≥ 1). Pure
lookup over the existing structure — no new scan, no new persistence, no schema
change. Carried items (`_carried`, tile-less) are intentionally absent (→ the
"you're carrying it" case in Part E).

Remembered (currently-unloaded) container tiles are included: in single-player an
unloaded chunk is frozen, so a remembered tile is the true last state (Iter-41),
which is exactly the "walk to the far chest" use case.

### Part B — Tracked-item state (session-only)

A static field in `ItemChecklistMod`:

```csharp
static int s_trackedObjectId = -1;   // -1 = nothing tracked
static int s_trackedVariation = 0;   // pet skin / cattle-paint colour dimension
```

Set/cleared by the row click (Part D) and the cancel hotkey. Never persisted;
`-1` on every world load. A small API surface (`TrackedId`, `SetTracked`,
`ClearTracked`, `IsTracked(id, variation)`) so the row, the HUD, and the hotkey
share one source of truth.

For pet-skin / cattle-colour rows the location is per **object**, not per
sub-variant (the ledger tracks container contents by objectID; the aux store
holds colour counts but not per-colour tiles). So tracking a colour variant
locates the base object's containers — acceptable and consistent with "where do I
own one of these".

### Part C — `TrackerHud` (copy-adapt of `ArrowRingRenderer`)

Adapt the divining-rod's `ArrowRingRenderer` into a mod-owned `TrackerHud`
(runtime class in the `ItemChecklist.UI` namespace) rather than promoting it to
shared `utils/` (which holds only editor helpers today; a shared runtime HUD is a
larger, separate move). The two HUDs are already structural twins.

Reused as-is: the pooled `SpriteRenderer`-per-target ring, the XZ-plane bearing
math, the HUD-layer-27 + prefab-z=10 conventions, the lazy-instantiate under
`Manager.ui.chestInventoryUI.transform.parent`, and the five-condition
visibility gate (`isInGame && player != null && !isAnyInventoryShowing &&
!IsAnyMenuActive` — the divining-rod's `DiviningRodActivePatch.IsActive` gate is
replaced by `ItemChecklistMod.IsTracked`, and additionally `ModConfig.Enabled`
so the master toggle also silences it).

Driven per frame from `ItemChecklistMod.Update` (the mod already runs a per-frame
`Update` with a lazy-HUD-instantiate block for the Iter-11.5 counter HUD — the
same pattern):

```csharp
if (IsTracked && playerPos.HasValue) {
    var tiles   = s_ledger.TilesHolding(s_trackedObjectId);
    var targets = tiles.Select(t => new float3(t.x, 0, t.z));   // Part A → world
    TrackerHud.Instance.Render(playerPos.Value, targets);
} else TrackerHud.Instance?.Hide();
```

A new HUD prefab (`ItemChecklistTracker.prefab`) + an arrow sprite are authored in
the Editor (structural work → Editor, per the mod convention). The arrow is a
**dedicated PNG** following the divining-rod precedent (its own `arrow.png` in the
mod's Art folder, `.meta` set to `textureType:8 spriteMode:1` — the ModBuilder
sprite-meta rule; a default `textureType:0` makes `LoadAsset<Sprite>` return null),
authored pointing right (+X) at 0° so `localRotation = Euler(0,0,degrees(bearing))`
aims it.

New assets/files: `TrackerHud.cs`, the HUD prefab + arrow sprite, `TilesHolding`
(one method), the tracked-state fields + API, the `ItemRow.OnLeftClicked`
override, one tooltip loc term set, and the cancel-keybind registration.

### Part D — Interaction & surface

- **Trigger.** `ItemRow.OnLeftClicked` override toggles tracking, guarded on the
  trackable condition (Part E). Clicking a non-trackable row does nothing.
- **Tooltip affordance.** `ItemRow.GetHoverDescription()` appends one
  `dontLocalize` line (own `Loc.T` term, resolved by us — CK's tooltip localiser
  doesn't see mod terms; the Iter-35 mixed-list precedent renders cleanly). The
  line text must be **PugFont-safe** — ASCII only, no em-dash / ellipsis / exotic
  glyphs (CK's PugFont throws `IndexOutOfRange` on an unsupported codepoint; the
  tooltip face is not the Iter-25 accented thinTiny), so a plain prefix, not a
  `▸`/`→` triangle:
  - trackable, not tracked → `Click to locate (in N chests)`
  - the tracked item → `Click to stop locating`
  - owned only carried → `You are carrying it` (no trigger, no arrow)
- **Tracked highlight.** Distinct from the Iter-22 hover highlight (which stays
  hover-driven). Because the row pool recycles, the tracked visual is
  re-evaluated in `ItemRow.Bind`: if the bound row's `(objectId, variation)`
  equals the tracked one, show a persistent marker (subtle background tint or a
  small "tracking" glyph — exact look calibrated in-game, per mod convention).
- **Cancel hotkey.** A rebindable action registered under the `ItemChecklist`
  control category (Iter-34), polled in `Update()` during gameplay; clears
  tracking. Only meaningful while something is tracked. Unbound by default.

### Part E — Edge cases

- **Spoiler-gate (Iter-21 chokepoint).** Only *discovered* rows are trackable; a
  `???` row is inert (no trigger, no hint line) — consistent with the existing
  spoiler logic. Reuses `ItemChecklistMod.IsCollected` / the `OwnedCount`
  chokepoint, never the raw discovery state.
- **Trackable condition (covers carried-vs-stored):**
  - discovered **and** `TilesHolding` non-empty → trackable, arrows;
  - discovered, owned only *carried* (no tile) → not trackable, tooltip "You are
    carrying it";
  - owned == 0 everywhere → not trackable, no hint line.
- **Stale-from-afar (SP).** Remembered (unloaded) tiles are reliable in
  single-player (frozen chunk, Iter-41), so arrows to far remembered containers
  are valid — the core use case. The multiplayer case (another player empties it
  while you are away) is out of scope; the mod is single-player-focused.
- **Self-correction (free).** Iter-40 adds no invalidation of its own — it reads
  the current `TilesHolding` each frame, so the arrows track the possession scan
  automatically. Take the item from a chest → the next scan (3 s, or the Iter-38
  interval setting) shrinks `TilesHolding` → that arrow disappears. When the item
  drops to zero stored everywhere → `TilesHolding` empty → **auto-untrack**
  (`s_trackedObjectId = -1`, arrows gone). No manual cleanup.
- **Distance visibility — the one real divergence from the divining-rod.** The
  rod fades arrows to near-invisible past its 30-tile radar radius (it is a
  proximity radar). Iter-40's target is *intentional* and may be far across the
  map, so its arrow must stay clearly visible at any distance: **no radar fade** —
  constant, or alpha-floored, visibility. When the player is essentially on a
  container tile (≤ ~1–2 tiles), that container's arrow hides (they have arrived).
  This is the one inherited default that does not fit the new purpose.

## Testing / verification (in-game, inline)

Per the mod convention, the in-game calibration loop runs inline (live CrossOver
window + build lock), not subagent-driven.

1. Clean sandbox compile (`safetyCheck=True`, 0 `CompileFailed`, 0 NRE).
2. Track a base-stored item → close F1 → arrows point at each holding chest;
   walk toward one → its arrow rotates correctly (bearing) and the arrow at the
   reached chest hides within ~1–2 tiles.
3. An item stored in multiple chests shows one arrow per chest, all visible.
4. A far (remembered / unloaded) container still shows an arrow at full
   visibility (no radar fade).
5. Take the item → within one scan interval the corresponding arrow drops; empty
   everywhere → auto-untrack.
6. Spoiler: a `???` row is not trackable; a carried-only item shows "You are
   carrying it" and no arrow.
7. Switch tracked item by clicking another row; untrack by clicking the tracked
   row again and by the cancel hotkey.
8. The master `ModConfig.Enabled` off silences the tracker HUD.

## Scope / risk

- Pure surfacing: no new tracking, no persistence, no ledger schema change. The
  ledger, scanner, player-position read, and the whole bearing/pool render
  pattern are reused.
- The riskiest piece — a directional HUD pointing at world targets — is a proven,
  sandbox-safe in-house pattern (`caveling-divining-rod`). The port's only
  behavioural change is the distance-visibility policy (Part E).
- Structural prefab/art work (the HUD prefab + arrow sprite) is done in the
  Editor, per the mod convention that batchmode builds reserialize and drop
  hand-authored objects.

## Out of scope

- A distance number on the arrow (trivial follow-up).
- Multiplayer stale-container correctness (SP-focused mod).
- Tracking multiple items simultaneously / per-item colour coding.
- Persisting the tracked item across sessions.
- A full map-pin integration (rejected in favour of the HUD arrow).
- Per-colour-variant tile locating (locates the base object's containers).
