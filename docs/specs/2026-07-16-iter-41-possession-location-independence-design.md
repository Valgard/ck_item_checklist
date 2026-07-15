# Iter-41 — Possession counter `K / M` location-independence

**Status:** design approved 2026-07-16. Branch `iter-41`.

## Problem

In the Iter-36 **Possession** counter mode, the owned numerator `K` (HUD + window
footer, via `ItemChecklistMod.OwnedCatalogCount` → `CurrentCounterNumerator`)
**decreases as the player walks away from their base** and recovers on return. It
should be **location-independent**: keeping "own ≥1 of every item" stable from
anywhere is exactly what the Iter-20 persistence layer (the per-`(x,z)`
`PossessionLedger`, persisted via `PossessionStore`, with `BuildView`'s "remembered"
merge) was built for. Reported 2026-07-15.

### Root cause (measured in-game, not assumed)

Diagnostic loop with `ModConfig.Diagnostics` on, Possession mode, `ScanInterval = 1s`,
two full base→far→base walks. HUD `K` = **385 → 40 → 385**; the DIAG `scan` line's
`ledgerC` (remembered container count) and `anchors` (loaded workbench-anchored
stations) tracked **together**:

| Phase | `anchors` | `ledgerC` | HUD `K` |
|---|---|---|---|
| At base (steady) | 46 | ~401–403 | 385 |
| Walking out (values fall together) | 46→42→38→36→… | 401→365→324→236→… | ↓ |
| Base fully unloaded | **0** | **0** | ~40 |
| Walking back | 0→26→36→46 | 0→203→359→401 | ↑ |
| Back at base | 46 | ~401 | 385 |

Two distinct code paths drift, but one **dominates**:

**Part A (dominant, ~90 % of the drop) — `PruneStaleNear` false-positive-prunes the
whole base ledger.** `PruneStaleNear` deletes every remembered container ≤ `LoadRadius
= 180` of the player that was not re-observed this scan, on the premise *"within-180 ⟹
loaded ⟹ if-unobserved-then-destroyed."* **Iter-31 broke that premise:** "observed" now
means "within `AnchorRadius` of a *loaded workbench anchor*" (`WithinAnchor`), not mere
chunk-loading. Walking away, the workbench leaves the anchor set (chunk unload / out of
the scanned near-set) **before** the co-located base containers leave the 180-tile
prune window → those containers are loaded-but-unobserved → wrongly pruned **and the
emptied ledger is persisted**. The only code that removes from `_containers` during steady play
is `PruneStaleNear` (the one-time Iter-28 `PruneByPredicate` nature eviction has already
run), so the `ledgerC` 401→0 collapse is conclusively the prune. It is
**systematic, not a narrow race**: pruning is already well underway at `anchors=36`
(`ledgerC=236`), so a coarse "only prune when `anchors>0`" guard would leave most of the
wipe intact.

**Part B (secondary, real) — live-only axes are never remembered.** Pet-skin rows count
via `Possession.CountSkin`, cattle-colour and paintable-colour slots via
`Possession.CountColour`. Both `_petSkins` and `_colourCounts` are rebuilt **live each
scan** (never persisted, never "remembered"). So a base-stored pet, a penned/caged
animal, or placed painted furniture drops out of `K` the moment its entity leaves the
loaded world — independent of the prune. This is a smaller fraction of the 345-row drop
but part of a complete, contract-faithful fix.

## Contract decision

`K` = **"own ≥1 right now, wherever it is stored"** — current possession,
location-independent (option A of the brainstorm). Rejected: *ever-owned* (monotonic; a
released last copy would stay counted — not current possession) and *live-only* (the
drop is by-design; contradicts the completion metric and the report).

Justified further by an in-game-mechanics fact the brainstorm surfaced: in Core Keeper
single-player, an **unloaded chunk is frozen** (no simulation, no automation, no second
player), so a base container/entity **cannot change while the player is away**. The only
mutation source (the player, at the object) loads and re-scans it in the same tick.
Therefore the "remembered" value is not a stale guess — it is the true current state as
of the last (and only possible) change. There is no staleness cost to contract A.

Invariant preserved: `K ≤ N ≤ M`, `M = Catalog.Count` unchanged; all three axes
(normal / pet-skin / cattle-&paint-colour) must stay stable across a base→far→base walk.

## Design

### Part A — anchor-aware prune

Add the observation criterion to the prune predicate so "should-have-been-observed"
matches Iter-31's actual observation rule. A remembered tile `K` is pruned iff:

```
dist(K, player) ≤ 180          // definitely loaded (load radius ~200) — kept as safety
∧ WithinAnchor(anchors, K, r²) // a loaded workbench covers K → it SHOULD have been scanned
∧ K ∉ liveKeys                 // but it was not → genuinely destroyed
```

`PossessionScanner.Scan` passes the already-computed `anchors` list + `AnchorRadius²`
into `PruneStaleNear` (today it receives only `px, pz, 180, liveKeys`). Self-heal is
preserved: a destroyed base container while the workbench is loaded is still
`WithinAnchor` + `∉ liveKeys` → pruned. The walk-away wipe disappears: no workbench
loaded → `WithinAnchor` false → nothing pruned → the ledger stays remembered.

### Part B — live entities into the remembered ledger

A **parallel, per-tile remembered aux store** beside `_containers` — additive, so the
proven normal-item path (Iter-20/28/31) is untouched (lowest blast radius). The rejected
alternative was unifying `totals` + `petSkins` + `colourCounts` into one packed per-tile
store; it would rewrite the working normal path for no correctness gain.

- **Storage:** per tile a `Dictionary<long,int>` of `PackKey(id, secondDim) → count`,
  where `secondDim` = `skinIndex` for pets, `variation` for cattle/paint. Pet IDs and
  cattle/paint IDs are disjoint, so one packed aux dict per tile suffices.
- **Remembered (into the aux store, at the entity's tile):** stored pets (in chests),
  caged/penned cattle, placed painted furniture. They inherit `SetLiveContainer` + the
  Part A anchor-aware prune verbatim.
- **Live (added on top each scan, like `_carried`):** carried items/pets **and the
  active/summoned pet** — they travel with the player, are live everywhere, and must
  never be remembered.
- **`BuildView`:** `petSkins = live(carried + active) + Σ remembered aux pet entries`;
  `colourCounts` analogously. `CountSkin`/`CountColour` then read remembered+live instead
  of live-only. `OwnedCount` and `OwnedCatalogCount` are unchanged — they already route
  pets/cattle/paint through `CountSkin`/`CountColour`, which now return stable values.

**Mobile penned cattle need no special case — Part A absorbs them.** An animal wandering
T1→T2 within its pen: the scan records it at T2 (`aux[T2]`), and T1 (anchored, empty this
scan) is pruned by Part A in the **same** scan. The per-`(id, colour)` sum over tiles
stays exactly 1 — no double-count, no ghost tiles.

### Part C — migration & persistence

Only `PossessionLedger.Serialize` / `LoadFrom` change; `PossessionStore` is
format-agnostic (it calls `Serialize()`/`LoadFrom()` and hashes/writes) and needs **no**
change. The per-line format is extended to carry the aux data, and the version marker
goes `#icl-ledger-v2` → `#icl-ledger-v3`. `LoadFrom` discards any non-v3 file **once**
(exactly as v2 discarded v1 in Iter-31); the base re-scans clean on the next visit and
auto-migrates every player. Self-healing → no data-loss risk.

## Testing / verification (in-game, inline)

Same measurement loop, with the **inverted** expectation as proof:

1. **`K` stays 385** at base / far away / back (was 385→40→385).
2. **`ledgerC` stays ~401** across the whole walk (no 401→0→401 wipe) — the direct
   evidence the prune no longer over-deletes.
3. A base-stored pet / penned or caged animal / placed painted furniture stays counted in
   `K` while away (Part B).
4. Sandbox: `Successfully compiled ItemChecklist safetyCheck=True`, 0 `CompileFailed`, 0
   NRE; `ItemCatalog baked` unchanged (behavioural change only).
5. Self-heal control: destroy a base chest → `K` drops correctly (Part A still prunes a
   genuinely-destroyed container while the workbench is loaded).
6. Migration: a pre-v3 ledger is discarded once; the base repopulates on the next scan.

The loop runs **inline** (live CrossOver window + shared SDK build lock — not
delegable), per the mod's standing in-game-calibration rule.

## Scope / risk

- Part A: a signature change + one predicate — low risk, high impact.
- Part B: touches the scanner write paths, `BuildView`, and `Serialize`/`LoadFrom`; the
  additive aux store keeps the normal-item merge intact.
- Part C: schema-v3 migration — the pattern is proven (Iter-31 v2).
- Estimated ~3–4 logical commits: (A) anchor-aware prune; (B) aux store + `BuildView`;
  (C) serialization + v3 migration; (V) verification + any DIAG additions.

## Out of scope

- Multiplayer possession (the ledger is single-player / ServerWorld, unchanged).
- Reworking discovery `N` semantics (only the possession `K` path is touched).
- Per-colour possession for **caged** cattle (today `AddBuffer` credits the adult total
  but not `colourCounts`) — a pre-existing gap, not introduced or required here; revisit
  only if the verification surfaces it.
