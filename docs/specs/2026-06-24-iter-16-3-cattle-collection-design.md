# Iter-16.3 — Cattle (Nutztiere) Collection — Design

Date: 2026-06-24
Status: Approved (brainstorming), pending implementation plan
Game version: Core Keeper 1.2.1.4

## Context & premise

Farm livestock (Moolin/Bambuck/Strolly-Poly = internal `Cow`/`Goat`/`RolyPoly`,
plus `Turtle`/`Dodo`/`Camel`) currently get **no checklist row**. Their
`ObjectType` is `Creature` (900), which `ItemCatalog.Bake` Loop 1 skips at
`ItemCatalog.cs:191` (alongside `NonObtainable`/`PlayerType`). This is the third
creature-family gap after Iter-16.1 (pets) and Iter-16.2 (critters) — and the
same `Iter-7.1`-style "a whole `ObjectType` is blanket-excluded" shape.

**Roadmap premise corrected by the decompile** (the standing "creature typing
has been wrong 3×" lesson — verified against `~/Projects/checkouts/
CoreKeeperDecompile/`, v1.2.1.4):

- The roadmap/memory named **only** `1300/1302/1303` as "the base cattle". The
  `ObjectID` enum (`Pug.Base`) and the `Pug.Objects` class hierarchy show the
  real roster is **larger**: six adult species, each with a baby form.
- There is **no usable persistent "this animal is tamed/owned" component flag.**
  The in-game leash (`ObjectID.Leash = 7`) is a *held leading tool*:
  `EntityUtility.PutLeashOnEntity` is driven from the equipped-item path
  (`Pug.Other` ~404639, owner = the wielding player) and `LeashedCD.leashedToEntity`
  is reset to `Entity.Null` when leading stops (~114187). So `LeashedCD` marks
  *currently being led*, not ownership. (An earlier "leashed = tamed" inference
  was wrong — would have been the 4th wrong creature guess.)
- Cattle can be **named** (`NameCD`) but naming is optional → not an ownership
  marker either.

## The CK cattle model (from decompile; in-game gates listed in § Verification)

- **Marker component `CattleCD`** — empty `struct CattleCD : IComponentData {}`
  (`Pug.ECS.Components`), assigned by `CattleConverter` to every prefab carrying
  `CattleAuthoring` (`Pug.ECS.Conversion` ~3756, also adds `LeashedCD`). This is
  the clean discriminator, exactly analogous to `PetCD`. `PugDatabase.HasComponent
  <CattleCD>(od)` is self-determining — **no ObjectID needs hardcoding.**
- **Full roster** (all `: Cattle` in `Pug.Objects`, all carry `CattleCD`):
  | Adult | ObjectID | Baby | ObjectID |
  |---|---|---|---|
  | Cow (Moolin) | 1300 | CowBaby | 1304 |
  | Goat (Bambuck) | 1302 | GoatBaby | 1305 |
  | RolyPoly | 1303 | RolyPolyBaby | 1306 |
  | Turtle | 1307 | TurtleBaby | 1308 |
  | Dodo | 1309 | DodoBaby | 1310 |
  | Camel | 1311 | CamelBaby | 1312 |

  (`CattleFeedTray = 1301` is a `Table`, not cattle; the `Cattle Transport Box`
  is `ObjectID.CattleCage`. Both are ordinary items, out of this family.)
- **Adult→baby link is structural, statically authored, bake-readable.**
  `BreedStateAuthoring.babyType` (`Pug.ECS.Authoring` 3799) →
  `BreedStateConverter` writes `BreedStateCD { babyType = authoring.babyType }`
  (`Pug.ECS.Conversion` 6781). So an **adult** carries `BreedStateCD` whose
  `ObjectID babyType` points at *its* baby; a **baby** carries no `BreedStateCD`
  (babies don't breed). `BreedStateCD` lives in the same `Pug.ECS.Components.dll`
  the mod already references for `PetCD`/`LevelCD` — readable at bake via
  `PugDatabase.TryGetComponent<BreedStateCD>`, **no new asmdef reference**.
- **Possible native discovery:** `CanBeDiscoveredAuthoring { distanceToDiscover
  = 5f }` exists (`Pug.ECS.Authoring`). If the cattle prefab carries the resulting
  `CanBeDiscoveredCD`, CK discovery-tracks cattle by proximity — which would let
  the discovery axis work natively with zero new code. **To be confirmed in-game**
  (gate b); the design is robust either way (§ Discovery axis).

## Goal

Each **adult cattle species** is an individually trackable collectible (6 rows).
Babies are **folded into their adult species**, not standalone rows: owning a
baby (caged or penned) counts toward the adult. A new `Cattle` / `Nutztiere`
filter category groups them.

## Design

### 1. Catalog — bake relaxation + structural baby fold

In `ItemCatalog.Bake` Loop 1, relax the `Creature` exclusion the same way
Iter-16.2 relaxed `Critter`:

```
- if (info.objectType == ObjectType.Creature) continue;
+ if (info.objectType == ObjectType.Creature
+     && !PugDatabase.HasComponent<CattleCD>(od)) continue;
```

Keep the defensive icon-guard (`smallIcon == null && icon == null → drop`) so an
icon-less stub never becomes a ghost row (the Iter-7.1/16.2 pattern).

**Baby fold (structural, no name parsing).** Before/within the bake, build a
baby→adult map in one pass over the cattle set:

1. For every `CattleCD` ObjectID `od` (variation 0) where
   `PugDatabase.TryGetComponent<BreedStateCD>(od, out var bs)` and
   `bs.babyType` is a real ObjectID → record `babyToAdult[bs.babyType] =
   od.objectID` and add `bs.babyType` to `babyTypeSet`.
2. A cattle ObjectID is a **baby ⇔ it ∈ `babyTypeSet`** → **skip its catalog
   row** (folded into the adult).

Result: **6 adult species rows** (Cow/Goat/RolyPoly/Turtle/Dodo/Camel). The map
also drives possession crediting (§ 2). If a baby ever fails to resolve to an
adult (no `BreedStateCD` references it), it falls back to its **own** row — never
silently dropped.

`Level`/`Value` need no special-casing: cattle carry no `LevelCD` and no sell
value, so the existing em-dash logic renders both `—` (the Iter-16.2 critter
path already proves this for `LevelCD`-less entities).

### 2. Possession axis — reuse the Iter-20 base scan (user's chosen mechanism)

Cattle ownership uses the **same** mechanism the checklist already uses for
chests / pedestals / mannequins / furniture: the `PossessionScanner` base-cluster
scan (Iter-20). A live cattle animal is a `Creature` ECS entity that **already**
carries `ObjectDataCD` + `LocalTransform`, so it appears in the scanner's existing
`objQuery`; today it is filtered out because it is neither carried nor a
near-anchor `PlaceablePrefab`.

Add one branch (analogous to the Iter-16.1 `PetOwnerCD.PetEntity` special-case):

- For a scanned entity `e` with `em.HasComponent<CattleCD>(e)` that sits **near a
  clustered crafting anchor** (the existing Iter-20 base-proxy radius/cluster
  test) → count it as owned. Credit the **adult** ObjectID via `babyToAdult`
  (a baby calf in the pen ticks the adult row).
- The **caged form in inventory/storage** (the `Cattle Transport Box`'s contained
  cattle = `ObjectID` + auxData) is already counted by the carried/container
  buffer path; route it through `babyToAdult` too so a caged baby credits the
  adult.

**Wild animals are excluded for free:** a wild animal roams far from any base
anchor → fails the cluster proximity test; and it is never an inventory item. The
inherited Iter-20 limitation (a *clustered foreign* base also anchors) carries
over unchanged — no new problem.

### 3. Filter category — `Cattle` / `Nutztiere`

Mirror the Iter-16.2 critter category wiring exactly:

- `ItemCategory` enum: add `Cattle` (placed beside `Pets`/`Critters`).
- Category resolution: carry an explicit **`isCattle` flag** on the bake `Entry`
  (set when `HasComponent<CattleCD>`), and short-circuit to `ItemCategory.Cattle`
  before the `ObjectType` switch. This is preferred over mapping
  `ObjectType.Creature → Cattle` inside `ItemCategory.Of`: cattle are the *only*
  `Creature`s that reach the catalog today, but the flag stays correct even if a
  future non-cattle `Creature` is ever admitted, and it keeps the cattle decision
  at the single point that already knows (`CattleCD`).
- `ItemCategories.All[]` + one `localization.yaml` line each for `en`/`de`
  (`Cattle` / `Nutztiere`).

### 4. Discovery axis — verify, then native-or-ledger (robust to the unknown)

Whether CK discovery-tracks `Creature`-typed cattle is the one make-or-break
unknown (gate b). The design tolerates either outcome:

- **If the cattle prefab has `CanBeDiscoveredCD`** (CK tracks it) → use native
  discovery: rows show `???` until the player has been near/obtained one, exactly
  like critters. **Zero new discovery code.**
- **If not** → gate "discovered" on a mod-owned **ever-owned ledger** fed by the
  possession scan (own it ⇒ collected), mirroring Iter-16.1's `PetCollection`
  (persisted via the `SaveManager.WriteCharacter` hook). **Guarantees no
  permanent `???` ghost rows.**

The ledger path is the fallback only; the spec does not pre-build it unless
verification requires it. Either way the possession (blue owned count / tick)
uses the § 2 scan.

## Scope

**In scope:** the bake relaxation + structural baby fold; the `PossessionScanner`
cattle branch (live near-anchor + caged-in-inventory, both credited to the adult);
the `Cattle`/`Nutztiere` category + loc; discovery-axis verification and (only if
needed) the ever-owned ledger.

**Out of scope / explicitly deferred:**

- **Per-baby and per-individual rows** — a baby is folded into its adult; named
  individuals share the adult ObjectID row. (Per-variation tracking remains
  Iter-17.)
- **True base detection** — the Iter-20 clustered-foreign-base limitation is
  inherited unchanged.
- `CattleFeedTray` (1301) and `CattleCage` ordinary items already flow through
  the normal item path — not part of this iteration.

## Verification gates (in-game, v1.2.1.4, own worktree, after Iter-16.4 merge)

Creature typing has been wrong 3× — each of these is *measured*, not assumed:

- **(a) Roster:** which ObjectIDs actually carry `CattleCD`, and which carry
  `BreedStateCD` with a valid `babyType` → confirm the 6-adult / 6-baby fold and
  that `babyTypeSet` collapses exactly the baby rows. (A throwaway bake-probe
  enumerating `CattleCD` + `BreedStateCD.babyType`, like the Iter-16.1/16.2
  probes.)
- **(b) Discovery:** does a cattle prefab carry `CanBeDiscoveredCD` / does
  `SetObjectAsDiscovered` fire for cattle? → decides native-vs-ledger (§ 4).
- **(c) Possession spatial test:** penned animals sit near the base anchor and
  register; wild animals (far from base) do **not** → validates the § 2 scan and
  the "wild never counts" guarantee.

## Coordination / build discipline

- Spec + plan authored in the **main tree** (a worktree's gitignored plan is lost
  on `git worktree remove`).
- Implementation runs in a dedicated `.worktrees/iter-16-3` worktree. The first
  build/in-game step waits until the parallel **Iter-16.4** session has merged and
  the shared `CoreKeeperModSDK` build lock (`UnityLockfile`) is free — both
  iterations touch `ItemCatalog.Bake`, and all mods share one SDK clone.
- In-game-calibration runs **inline** (not subagent-driven): it needs the live
  CrossOver window + the build lock.

## Risks

- **Baby-fold correctness** hinges on every baby being referenced by exactly one
  adult's `BreedStateCD.babyType`. Mitigated by the fallback (unreferenced baby →
  own row) and gate (a).
- **Discovery axis** is the make-or-break unknown; mitigated by the native-or-
  ledger fallback (§ 4) so the feature ships regardless of gate (b)'s outcome.
- **Possession spatial proxy** inherits Iter-20's foreign-clustered-base edge;
  accepted, unchanged.

## Outcome (2026-06-25) — this design was superseded mid-implementation

The shipped Iter-16.3 differs from § 4 above. In-game verification (gate b) first
read `CanBeDiscoveredCD` as absent → the **ledger path** here was built. But a later
discovery probe proved CK **does** discover cattle, via the inventory-pickup path and
**per `(objectID, variation)`** — the variation being the animal's **colour variant**.
That makes the ever-owned ledger a *mask* over the variation-keyed-discovery limitation
rather than a fix. The ledger (`CattleCollection` + store + chokepoint routing) was
therefore **deliberately removed**, and cattle now ship **critter-like**: catalog
admission + `Cattle`/`Nutztiere` category + a possession scan branch (penned + caged,
adult-folded), flowing through CK's **native** `(objectID, var0)` discovery. Per-colour
tracking — the proper fix, needing **no** ledger (native per-variation discovery exists,
unlike pets) — is deferred to **Iter-17**. The structural baby-fold (§ 1, via
`BreedStateCD.babyType`) and the 6-adult/6-baby roster were confirmed exactly. Full
narrative: `docs/iteration-history.md § Iter-16.3`.
