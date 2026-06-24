# Iter-17 — Per-Variation Tracking — Design

Date: 2026-06-25
Status: Approved (brainstorming), pending implementation plan
Game version: Core Keeper 1.2.1.4

## Context & premise

The catalog collapses every item family to its `variation == 0` row — the
`if (od.variation != 0) continue;` guard in `ItemCatalog.Bake` Loop 1
(`ItemCatalog.cs:169`). Colour/skin/state variants never get their own row.
Iter-17 was the long-standing "per-variation/skin tracking" backlog item,
deferred twice as a concrete instance surfaced (Iter-21 waypoint H1; Iter-16.3
cattle colours). The user chose **full per-variation tracking across the whole
catalog, with flat rows (no grouping/expanding UI)**.

**Premise corrected by measurement (the standing "measure CK variation
mechanics, don't infer from the decompile" lesson — now the 5th time).** Two
decompile/ItemBrowser investigations (`~/Projects/checkouts/CoreKeeperDecompile/`
v1.2.1.4 + `~/Projects/checkouts/ItemBrowser/`) reframed the scope:

- **`PugDatabase.objectsByType` is keyed on `(objectID, variation)`** —
  `Dictionary<ObjectDataCD, ObjectInfo>`, where `ObjectDataCD.Equals/GetHashCode`
  include `variation` (`Pug.ECS.Components` ~3804). The key is populated from
  each `ObjectInfo.variation` (`Pug.Other` ~314119). So removing the bake guard
  **would** expose any DB-authored `variation ≠ 0` entries.
- **…but almost nothing is DB-authored at `variation ≠ 0`.** Standard items are
  authored at `variation = 0`; non-0 variation is assigned **dynamically at
  runtime** for cosmetic/state variants (`ObjectInfo.variationIsDynamic`,
  `variationToToggleTo` — e.g. door open/closed toggles). Lifting the guard
  yields mostly state-toggle junk, not collectibles. Real count unknown without
  an in-game sweep.
- **Cattle colour variants are NOT in the DB.** They are assigned at hatch/spawn,
  then tracked by CK's native `SetObjectAsDiscovered(objectID, variation)`
  per-pair (Iter-16.3 logged `(Cow=1300, var=2)`). There is **no** `(Cow, var2)`
  key in `objectsByType` to enumerate, and **no enumeration API**:
  `PugDatabase.TryGetObjectInfo(id, variation)` silently **falls back to var0**
  when the exact variation is unauthored (`Pug.Other` ~314163), so one cannot
  even probe "does colour 2 exist". ItemBrowser's `ignoreVariation`
  (`ObjectUtility.cs:421`) only **groups**, never enumerates per-variation rows.
- **Magnitude: tiny.** Cooked food (~4.7k, the only large multi-variation family)
  and pet skins are already emitted via the `Variation` slot (Loops 2/3). For
  standard items the variation axis is effectively empty. The only
  player-meaningful, currently-missing per-variation collectible is **cattle
  colours** (~6 species × a few colours, runtime-only).

**Consequence:** "one row per variation" requires bake-time enumeration of the
valid variations. That works for DB-authored variants (lift the guard) but is
**impossible for runtime-assigned cattle colours** — they can only be learned by
observing discovery events. This rules out a pre-listed "N / M colours"
denominator for cattle and drives the discover-to-reveal model below.

## Goal

1. Emit a flat catalog row for every **DB-authored** `variation ≠ 0` entry that
   is a genuine collectible (not a runtime state-toggle). No grouping UI. **(Bucket
   1 — generic, all items.)**
2. Reveal **runtime-assigned variations** (a variation CK discovers but that has
   **no `objectsByType` row**) as flat rows on discovery. **(Bucket 2 — generic.)**
   Cattle colours are the one *known* instance and the special case (no reliable
   var0 anchor → a placeholder); whether any *non-cattle* item also has
   runtime-assigned variations is **measured by the Step-1 sweep** (it may be
   cattle-only, in which case the general path is inert).
3. Keep the global `N / M` completion metric coherent and **100 %-reachable**.

Non-goals: grouping/expanding/parent-child rows (user excluded); a fabricated
variation-count denominator (CK provides none for runtime variations); per-skin
pet changes (Iter-16.1, already done); cooked-food changes (Loop 2, already done).

**The two buckets, and the discriminator.** A discovered `(objectID, variation)`
is **DB-authored** iff `(objectID, variation)` is a key in
`PugDatabase.objectsByType` → Bucket 1 (Part 1 guard-lift emits it). Otherwise the
variation was assigned at runtime (hatch/spawn/dye) and has no DB row → Bucket 2
(Part 2 discover-to-reveal). This is the clean test that routes every variation.

## Design

### Part 1 — Guard-lift for DB-authored variations (sweep-gated)

Remove the `if (od.variation != 0) continue;` guard in Loop 1
(`ItemCatalog.cs:169`) so the loop emits `objectsByType` keys at any variation.
Keep an entry **only** when it is a real distinct collectible, not a runtime
state:

- **`ObjectInfo.variationIsDynamic == false`** — a `true` value means the var0
  DB entry is canonical and non-0 forms are runtime state (door toggles, dynamic
  cosmetics) → skip the non-0 forms.
- **Icon-guard** (Iter-7.1 pattern) — drop icon-less internal entities.
- **Distinct from var0** — a non-0 entry whose name+icon equal its var0 sibling
  adds no information → skip (dedup).

Loop 1 continues to skip cooked food (`IsCookedFood()`, re-emitted Loop 2), pets
(`HasComponent<PetCD>`, re-emitted Loop 3), and now cattle (`HasComponent
<CattleCD>`, handled by Part 2 — Loop 1 no longer emits the var0 cattle species
row). What the guard-lift actually exposes is measured by the Step-1 sweep;
evidence says ≈ none beyond junk the filters catch.

### Part 2 — Runtime-assigned variations via discover-to-reveal

Two shapes, split by whether the item has a reliable var0 anchor:

**(2a) General case — var0-anchored (non-cattle).** An item's var0 DB row already
exists (Loop 1 / Part 1). For each *additional* discovered `(id, v≠0)` whose
`(id, v)` is **not** a key in `objectsByType` (runtime-assigned), emit one extra
"variant" row, seeded from the discovery snapshot and live-added on new discovery.
**No placeholder** — the var0 row anchors the "undiscovered" state. A single pass
over the discovered key set, filtering out DB-authored keys, cooked food, pets,
and cattle. Whether any such items exist at all is a Step-1 sweep finding; if the
sweep shows "cattle only", this loop emits nothing and the design reduces to (2b).
Generic-variant naming uses the same suffix mechanism as cattle; generic-variant
possession defaults to the item's species total gated on discovery (the sweep
tells us what these items are — refine only if a per-variation count is meaningful).

**(2b) Cattle special case — no var0 anchor.**
Replace the Iter-16.3 single var0 cattle species row with:

- **One flat row per discovered `(speciesId, variation)` colour.** At bake,
  seed colour rows from the `DiscoveredState` snapshot — every `(cattleId, *)`
  pair the player has ever discovered. CK persists discovery **natively** per
  `(objectID, variation)` (`CharacterData.discoveredObjects2`), so reloading a
  character re-seeds every previously-discovered colour. **No mod ledger / no
  `API.ConfigFilesystem` persistence / no `WriteCharacter` hook** — this is the
  key difference from pet skins, where CK force-zeroes the variation and the
  `PetCollection` ledger was unavoidable.
- **Live add:** the existing `SaveManagerDiscoveryHook` already mirrors new
  `(objectID, variation)` discoveries into `DiscoveredState`. When the new pair
  is a cattle colour not yet in the catalog → add the row and refresh the list
  (mirrors the Iter-16.4 HUD-nudge-after-change path).
- **Placeholder for a species with zero discovered colours:** exactly **one**
  `???` row, keyed on the **species objectID with an "any-variation" discovery
  test**, NOT on var0. New `DiscoveredState.IsDiscoveredAnyVariation(objectId)`:
  true iff any `(objectId, *)` pair is in the discovered set. While false → one
  placeholder row; once true → the placeholder is gone and the discovered-colour
  row(s) stand in its place.

**Why this fixes the Iter-16.3 bug.** Today the Cow shows `???` because its row
tests `IsDiscovered(1300, 0)`, but the Cow is only ever discovered at var=2.
Keying the placeholder on *any* variation makes every colour discovery
reachable — the "Cow `???` forever" trap is structurally gone.

**Why the denominator self-resolves.** A colour row only **exists once
discovered**, so it is by construction both `N` and `M` (+1/+1, ratio-neutral).
The only *un*discovered cattle rows are the placeholders. Therefore **100 % is
reachable**: every species with ≥1 colour discovered leaves no undiscovered
cattle row. "Complete" honestly means "every species found + every encountered
colour logged" — the checklist never invents a phantom row for an unknown
colour, and `M` only ever grows in lockstep with `N` for colours.

### Possession per colour

The Iter-16.3 possession scanner credits a live/caged cattle to the **adult
species** (`PossessionScanner.cs:137`, `AdultOf(id)`), and `PossessionView.Count`
is keyed by objectID only — i.e. **species-total, colour-agnostic**. With flat
per-colour rows that would show the same species total on every colour row
(misleading: "N of each colour"). Resolution: **per-colour possession**, mirroring
the existing pet-skin mechanism (`PossessionView._petSkins`, a parallel
`Dictionary<long,int>` keyed by `PackKey(objectId, skinIndex)` →
`CountSkin`). A parallel `_cattleColours` dict keyed by `PackKey(adultId,
colourVariation)` is filled by the scanner crediting `(AdultOf(id),
entity.variation)`, and `OwnedCount` routes cattle colour rows through a new
`CountColour(adultId, variation)`. Gate (b) confirms the live/caged entity's
`variation` equals the discovered colour (and that a baby's variation maps onto a
valid adult colour); if the mapping proves unreliable, fall back to em-dashing
possession on cattle colour rows (never species-total). The placeholder row shows
no possession (spoiler-gated: undiscovered ⇒ 0, as today).

### Icon / naming per colour (sweep-gated, never blocking)

Step 1 measures whether a colour variant has a distinct name/icon or whether
`GetObjectInfo(cattleId, varN)` falls back to var0 (→ identical name/icon for all
colours). Fallback if identical: a name suffix (`"<species> — Farbe N"`, a new
`ItemChecklist-General/ColorSuffix` loc term, mirroring the pet `SkinSuffix`) and,
if a tint mechanism exists, an icon tint. Pets recolour via the
`Amplify/UISpriteColorReplace` gradient shader; whether cattle expose a
comparable per-colour tint is **unverified** and is a Step-1 question. The suffix
fallback guarantees the feature is never blocked on the icon question.

## Scope

In:
- Lift the Loop-1 variation guard + the three keep-filters (Part 1).
- `DiscoveredState.IsDiscoveredAnyVariation(objectId)` + cattle placeholder /
  per-colour-row emission seeded from the discovery snapshot (Part 2).
- Live catalog-add on a new cattle-colour discovery + list refresh.
- Colour-row naming (suffix fallback) + icon (sweep-decided).
- Per-colour possession: `_cattleColours` scan key + `CountColour` + OwnedCount
  routing (gate-b-confirmed; em-dash fallback).

Out: grouping UI; cattle colour-count denominator; pet/critter/cooked-food
changes; any **discovery** persistence layer (native CK discovery suffices —
possession persistence is the unchanged Iter-20 ledger).

## Verification gates (in-game, v1.2.1.4, own worktree)

Step 1 is a **throwaway probe** (committed-then-reverted, per the Iter-16.3
build-reverts-uncommitted-edits discipline), exactly the Iter-16.2/16.3 pattern:

- **Gate (a) — variation histogram (Bucket 1) + runtime-variation census
  (Bucket 2).** Sweep `PugDatabase.objectsByType.Keys`; per objectID count
  distinct variations, and for each `variation ≠ 0` log ObjectType, has-icon,
  name, `variationIsDynamic` — decides what Part 1 emits and whether the
  keep-filters suffice. **Also** sweep the loaded `DiscoveredState` keys and log
  every discovered `(id, v≠0)` whose `(id, v)` is **not** an `objectsByType` key
  and is **not** cattle — these are the non-cattle runtime variations that decide
  whether Part 2's general loop (2a) emits anything or stays inert (cattle-only).
- **Gate (b) — cattle colour rendering + possession mapping.** For a known
  cattle id, compare `GetObjectInfo(id, 0)` vs `GetObjectInfo(id, varN)`: distinct
  name/icon, or var0 fallback? Decides icon/naming (distinct icon vs. suffix +
  optional tint). Also: does a live/caged cattle entity's `ObjectDataCD.variation`
  equal the colour CK discovers, and does a baby's variation map onto a valid
  adult colour? Decides per-colour possession (Part 2) vs. the em-dash fallback.

Then implement; verify: clean sandbox compile (`safetyCheck=True`, 0
`CompileFailed`/NRE), `baked: N items` sane vs. main, catch a new cattle colour →
row appears live, reload → colour persists (native snapshot, no ledger), a
zero-colour species shows exactly one `???`, `N / M` coherent and reachable.

## Coordination / build discipline

Shared `CoreKeeperModSDK` build lock — wait/poll, never kill (parent CLAUDE.md).
In-game calibration runs **inline** (`executing-plans`), not subagent-driven —
the live CrossOver window + build lock cannot be delegated. Author spec + plan in
the **main tree**; implement in a `iter-17` worktree under `.worktrees/`.

## Risks

- **Step-1 sweep shows Part 1 exposes meaningful non-junk variants we did not
  anticipate** → re-scope Part 1's filters; the discover-to-reveal core (Part 2)
  is independent and unaffected.
- **No cattle colour tint mechanism exists** → colours share an icon; the name
  suffix still disambiguates rows. Acceptable (honest), not blocking.
- **`IsDiscoveredAnyVariation` cost** — the discovered set is keyed by packed
  `long`; an any-variation test must scan or maintain an objectID side-set.
  Cattle are ≤ 6 ids → a tiny per-bake scan is fine; avoid a per-frame scan.
- **List churn on live colour add** — reuse the Iter-16.4 change-nudge path; do
  not rebuild the whole catalog, just add + refresh.
