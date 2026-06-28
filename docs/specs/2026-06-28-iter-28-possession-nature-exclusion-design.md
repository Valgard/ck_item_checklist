# Iter-28 — Possession scan: exclude world nature (perf)

**Status:** design approved 2026-06-28. Branch `iter-28`.

## Problem

ItemChecklist 1.0.2 still shows frame **peaks**, worsening the **longer** a session
runs and present **even away from the base** — a different signature than the in-base
spike Iter-27 fixed. Iter-27 (bulk `ToComponentDataArray` instead of per-entity
`GetComponentData`) completely solved its symptom (the in-base entity-loop spike); this
is a **new, separate** cost that the short Iter-27 measurement did not surface.

### Root cause (on-disk evidence, no build needed)

The active character's persisted possession ledger
(`possession-<guid>.txt`) holds **5502 containers / ~5820 item-pairs / 89 KB**. **99 %**
of those entries are a single placed object with **no stored contents**, and **~90 %** are
**world-spawned nature**, dominated by:

| ObjectID | Name | Entries |
|---|---|---|
| 5619 | Bush | 1102 |
| 5610 | Stalagmite | 988 |
| 5613 | WaterKelp | 922 |
| 5620 | TallGrass | 864 |
| 5612 | NatureDestructible | 423 |
| 5614 | WaterLily | 224 |

Mechanism: Iter-20 added "count the placed object itself" (path #1, `AddOne(tile, id)`)
so a placed workbench/torch is owned even with no inventory. That path counts **any**
`ObjectType.PlaceablePrefab` within `AnchorRadius` of a **clustered** crafting-station
anchor — which wrongly includes **world nature growing around the base**. The per-`(x,z)`
ledger then **remembers it forever**: `PruneStaleNear` only drops entries within 180 tiles
of the player, so the ledger grows **unbounded** over playtime. That growth drives, all
**independent of location**: (a) the persistent managed-heap footprint, (b) the
save-time `Serialize` (O(ledger), an 89 KB string + 5502 list entries built at **each**
autosave), and (c) the per-scan handling.

**Important honesty note (verify, don't assume):** the Iter-27 PERF probe measured
`BuildView` (the ledger-iteration `build` phase) at only **~0.8 ms even at 5502
containers** — so the 16.7 ms peak is **not** pure `BuildView` compute. The
long-session, location-independent signature is consistent with **GC** (the scan
allocates fresh dictionaries/hashsets every 3 s; over hours that drives garbage-collection
pauses) and/or the **save-serialize spike**. The exact peak driver is **confirmed
empirically** (see Design step 3), not assumed.

## Requirements

**Counted as possession:**
- Carried inventory (unchanged).
- Container/chest **contents** (`AddBuffer`, path #2) — unchanged. World nature counts
  **only here**, i.e. when actually stored as an item.
- **Placed player objects**: walls, furniture, torches, decorations — anything with an
  item form the player placed (Iter-20 path #1 stays for these).

**Not counted:**
- World-spawned nature (Bush, TallGrass, WaterKelp, Stalagmite, WaterLily,
  NatureDestructible, …) as a **world object**.

**Preserved:**
- The persistent "remember possession at far/other bases" feature **for real objects** —
  the mod is published and other users rely on multi-base remembering. Excluding nature
  must not remove or weaken it.

Behaviour-neutral except the intended change: world nature is no longer counted as owned.

## Design

1. **One gate** in `PossessionScanner`, on path #1 (the `AddOne` that counts the placed
   object itself): count the placed object only when it is a **real player-placeable /
   obtainable item**. Carried and container-contents (`AddBuffer`, path #2) are
   untouched, so a nature item actually stored in a chest still counts, and the
   remember-ledger keeps storing real far-base objects. World nature never enters the
   ledger.

2. **Discriminator determined by a throwaway probe (implementation step 1) — not guessed.**
   CK object internals have been mis-guessed repeatedly here; the predicate is chosen
   against a hard **acceptance criterion**:
   - MUST **include**: Wall, Torch, furniture (player-placed item-form objects).
   - MUST **exclude**: Bush, TallGrass, WaterKelp, Stalagmite, WaterLily,
     NatureDestructible.

   The probe logs, for the near-anchor counted entities, the candidate signals so the
   right predicate is picked empirically:
   - (a) **catalog membership** — the objectID is a tracked obtainable `ItemCatalog` row;
   - (b) a **placeable-from-item component** (e.g. `PlaceableObjectCD`) present;
   - (c) the object has an **inventory item form** (carriable / same ID as an item).

   Sandbox-safe (`PugDatabase` / `Manager` reads only); marked `// PROBE`,
   committed-before-build, removed before the fix lands.

3. **Verify the long-session peak with the same probe.** Capture, over long-session and
   away-from-base play: ledger size (containers + item-pairs), the phase split
   (`world`/`setup`/`loop`/`build`) + total with MAX/percentiles, and **save-serialize
   timing**, plus a GC indicator if a sandbox-safe one is available (else infer GC from
   total-vs-phases divergence). Confirm `total` MAX drops **< 16.7 ms** (60 fps budget)
   after the gate. **If a residual GC component remains**, reduce per-scan garbage by
   **reusing the scanner's working collections** across scans (clear instead of
   reallocate every 3 s) — a follow-on applied **only if measured necessary** (YAGNI).

4. **Existing bloated ledger.** Once nature stops being counted, the 5502 stale entries
   are no longer re-observed and `PruneStaleNear` drops those within 180 tiles as the
   player roams the base. If self-clean is too slow in practice, add a **one-time
   prune-on-load** of entries whose objectID no longer passes the gate. Decide during
   implementation from the measured self-clean speed.

5. **Scope:** `unity/ItemChecklist/possession/` only (`PossessionScanner`, possibly
   `PossessionClassifier` for the predicate). No prefab/art. No discovery/catalog change.

## Testing / Verification (project 7-phase conventions)

- **Sandbox compile**: `Successfully compiled ItemChecklist safetyCheck=True`,
  0 `CompileFailed` (CK 1.2.1.5).
- **Predicate**: probe confirms the chosen gate includes Wall/Torch/furniture and
  excludes Bush/TallGrass/WaterKelp/Stalagmite/WaterLily/NatureDestructible.
- **Ledger shrinks**: 5502 → a few hundred real owned objects after the gate (re-inspect
  the on-disk `possession-<guid>.txt`).
- **Possession correctness**: a placed wall/torch/furniture still shows owned; a wild bush
  no longer counts; a nature item **stored in a chest** still counts; carried unchanged.
- **Perf**: long-session / away-from-base PERF run shows `total` MAX < 16.7 ms; before/after
  read across the `Player.log` ↔ `Player-prev.log` rotation.
- **Feature preserved**: a real object stored at a far base still shows owned while away
  (remember-ledger intact for real objects).

## Out of scope (YAGNI / explicit non-goals)

- No cap / LRU / size limit on the ledger — fixing the over-counting bounds it naturally;
  real placed furniture across multiple bases stays manageable.
- No removal or weakening of the remember-from-afar feature.
- No catalog/discovery changes.
- `BuildView` caching / incremental rebuild — not pursued unless the step-3 measurement
  shows ledger iteration (not GC/serialize/over-counting) is the actual peak, which the
  Iter-27 data already argues against.
