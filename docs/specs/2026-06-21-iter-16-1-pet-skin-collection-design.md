# Iter-16.1 — Per-Skin Pet Collection — Design

Date: 2026-06-21
Status: Approved (brainstorming), pending implementation plan
Game version: Core Keeper 1.2.1.4

## Context & premise correction

The frozen roadmap (`docs/roadmap.md`) framed Iter-16 as *"the bake
blanket-excludes `ObjectType.Creature`/`Critter`, so tamed pets/critters never
get a row — same bug class as Iter-7.1"*. Empirical investigation (CK decompile
at `~/Projects/checkouts/CoreKeeperDecompile/`, v1.2.1.4) **disproved the pet
half of that premise** — the third time a roadmap catalog-exclusion guess has
been wrong (cf. Iter-21 waypoints):

- `ObjectType` enum (`Pug.Base`): `PlaceablePrefab=800`, `Critter=801`,
  **`Pet=802`**, `Creature=900`, `PlayerType=6000`.
- The bake excludes only `NonObtainable/Creature/Critter/PlayerType`. **`Pet`
  (802) is NOT excluded** → pets are *already* in the catalog (confirmed
  in-game: "Subterrier", "Eulux", "Glutschweif" all appear, discovered, with
  owned counts).

What the roadmap line could not capture is the **real** complexity: CK's pet
data model. That complexity is what makes this a standalone feature iteration,
not a filter tweak — which is exactly why pet/critter was split out as Iter-16.

This spec covers **Iter-16.1 = pets**. Critters are **Iter-16.2** (a genuine
Iter-7.1-style filter relaxation — `Critter` (801) is excluded yet caught
critters *are* discovery-tracked; see Out-of-scope).

## The CK pet model (verified)

- **Variation is always 0 for pets.** `SaveManager.SetObjectAsDiscovered`
  (`Pug.Other` ~363151) force-zeroes `variation` for any `HasComponent<PetCD>`
  object. Carried pet items also sit at variation 0. So CK's
  `discoveredObjects2` only ever records a pet at **(objectID, 0)** — it does
  **not** track which skins the player has seen.
- **Skins are a separate, random aux-data field.** Each pet instance carries
  `PetSkinCD { int skinIndex }`, an `[InventoryAuxDataComponent]` stored in the
  world-global `InventoryAuxDataSystem` (NOT in `variation`, NOT a direct entity
  component). On obtain/hatch the skin is `rng.NextInt(maxSkins)` — there is
  **no native skin-collection/unlock system** in CK.
- **All skins share one ObjectID** (e.g. `PetDog=1222` for every dog skin). The
  skin count per pet = `Manager.ui.petInfosTable.GetPetSkinInfo(id).skins.Count`
  (more reliable than `PetCD.maxSkins`).
- **Skins render as gradient recolors of one base sprite.** Icon = the base
  `ObjectInfo.icon` + a material with `_GradientMap` set to a `Texture2D` built
  from `PetSkinInfo.skins[skinIndex].primaryGradientMap` and the
  `USE_GRADIENT_MAP` shader keyword enabled. There is no per-skin Sprite asset.

## Goal

For a completionist, **each pet skin is an individually collectible item**.
Replace the single skinless (variation-0) pet row with **one row per skin**
(`skins.Count` rows per pet), each separately collectible and counted.

## Feasibility (verified, all sandbox-safe)

- **Read `skinIndex` per instance:** `InventoryHandler.TryGetExtraInventoryData
  <PetSkinCD>(containedObject, out data)` (static) — works for inventory AND
  chest contents (aux data is world-global). The **active/summoned pet**:
  `PetOwnerCD.PetEntity` (player) → `PetCD.inventoryAuxDataIndex` → same lookup.
- **Skin count + gradient:** `Manager.ui.petInfosTable` is reachable;
  `PetSkin.primaryGradientMap` → `GradientMapDataBlock` (pixel access) →
  `Texture2D`.
- **Render:** the row icon `SpriteRenderer`'s material must use CK's
  gradient-capable shader (`_GradientMap` + `USE_GRADIENT_MAP`). This is the one
  genuinely new rendering capability and the main implementation risk (Phase 0
  verifies it in the mod's icon slot).

## Decisions

- **D1 (collected semantics) = "ever-owned", mod-tracked.** A skin counts as
  collected once the player has owned a pet with that `skinIndex`. CK provides
  no per-skin discovery, so the mod maintains its own per-`(objectID, skinIndex)`
  ledger, persisted via the Iter-20 `PossessionStore` mechanism
  (`SaveManager.WriteCharacter` hook). Rejected: "currently own" (check vanishes
  on release) and "all skins discovered with the species" (per-skin checks
  become meaningless). Accepted limitation: **retroactive gap** — skins owned
  and released *before* install are not credited (the data does not exist in CK).
- **D2 (uncollected display) = spoiler-consistent.** Three states per row:
  1. Species undiscovered (`!DiscoveredState.IsDiscovered(petId, 0)`) → full
     `???` name + Unknown-Object icon (existing Iter-12 sprite).
  2. Species discovered, this skin uncollected → **name shown**, skin icon stays
     the Unknown-Object icon, owned shows `—`.
  3. Skin collected → gradient skin icon + owned count.
  Mirrors the established Iter-10/21 spoiler gate. Rejected: greyed real-skin
  preview (spoils skin appearance).
- **D3 (skin row naming) = `"<PetName> (Skin <n>)"`, 1-based.** CK has no
  per-skin names; explicit numbering is required (else the bake's name-conflict
  disambiguation appends raw terms). The `"(Skin {0})"` suffix is a localized
  term (Iter-11 infra). `n = skinIndex + 1`. Name prefix + `skinIndex` keeps a
  pet's skins grouped under Name sort.

Bake-in decisions (no separate question; raise if any should change):

- **Level/Value → `—` for pets.** The columns show generic defaults (in-game
  all pets read Level 7 / Value 6) that are meaningless for pets. Render `—`
  (the existing spoiler-guard em-dash path). **Phase 0 confirms** the values are
  generic before suppressing.
- **New filter category "Pets"** (`ItemCategory.Pets`), `ObjectType.Pet → Pets`
  (today pets fall into `Other`).
- **Sort** keeps a pet's skins adjacent (name prefix shared; `skinIndex`
  tiebreaker).

## Architecture

A single live scan path feeds three outputs (skin-read, owned-count, collected-
ledger) — and that one path also fixes the Iter-20-deferred **active-pet
undercount** (the summoned pet was a live entity outside the scanned inventory
buffer; observed in-game as Terrier 7-shown vs 8-owned).

### 1. Catalog (`ItemCatalog.Bake`)
- Skip pets in Loop 1 (guard `HasComponent<PetCD>(od)`, mirroring the
  `IsCookedFood()` skip) so no skinless var-0 pet row is emitted.
- New **pet-skin loop** (sibling to the cooked-food α-loop): for each pet
  ObjectID, read `petInfosTable.GetPetSkinInfo(id).skins.Count` and emit one
  `Entry` per `skinIndex` with:
  - mod-internal key `(petObjectID, skinIndex)` — `skinIndex` occupies the
    `Variation` field of the entry/`PackKey` (mod-internal reuse; CK's real pet
    variation is always 0, so no collision with CK semantics).
  - `DisplayName = Loc("<PetName> (Skin n)")`, `Rarity` from `ObjectInfo`,
    `Level = 0`, `SellValue = 0` (→ `—`), `IsCraftable` from `ObjectInfo`.
  - a flag marking the entry as a pet-skin (drives gradient-icon render +
    ledger-based collected lookup).

### 2. Possession scan (`possession/`)
- For each owned pet instance (carried, equipment slot, base containers, AND the
  active summoned pet) read `PetSkinCD.skinIndex` and tally by
  `(objectID, skinIndex)`.
- Non-stackable (1 per slot) already handled by Iter-20.

### 3. Pet-skin collected ledger (new, D1)
- `(objectID, skinIndex) → everOwned: bool`. Set during the scan for every
  currently-owned skin; once true, stays true.
- Persisted per character GUID via the existing `PossessionStore` /
  `WriteCharacter` hook (hand-ASCII, sandbox-safe).
- Pet-skin rows route their **collected** flag through this ledger instead of
  `DiscoveredState` (which is blind to skins). Non-pet rows are unchanged.

### 4. Icon rendering (gradient)
- For a collected pet-skin row, build/cache a `Texture2D` from
  `skins[skinIndex].primaryGradientMap`, set it as `_GradientMap` on the row
  icon material, enable `USE_GRADIENT_MAP`. Base sprite = pet `ObjectInfo.icon`.
- Uncollected/species-unknown rows use the existing Unknown-Object icon (no
  gradient).

### Display routing (D2)
A small seam in the row-bind (`ItemChecklistContent`/`ItemRow`) and the spoiler
gate (`ItemChecklistMod.OwnedCount` and the discovered/`???` decision):
- pet-skin row → species discovered? (`DiscoveredState.IsDiscovered(petId,0)`)
  gives name-vs-`???`; this-skin collected? (ledger) gives icon + owned display.
- non-pet row → unchanged (CK `DiscoveredState`).

## Phase 0 — throwaway diagnostic probe (Iter-21 style)

Before building, a committed-then-reverted probe (net-zero on shipped files)
confirms in-game (1.2.1.4):
1. Pet Level/Value are generic defaults (justifies the `—` suppression).
2. `petInfosTable.GetPetSkinInfo(id).skins.Count` returns sane per-pet counts;
   enumerate pet ObjectIDs.
3. `skinIndex` reads correctly for a pet in inventory, in a chest, and as the
   **active summoned pet**.
4. A gradient `Texture2D` set on a mod `SpriteRenderer` material with
   `USE_GRADIENT_MAP` actually recolors the icon in the mod's slot (the main
   render risk).

## Testing

No unit-test framework (per `docs/conventions.md § Testing`): `utils/build.sh` +
Player.log grep (`error CS|Build complete|CompileFailed`, `safetyCheck=True`) +
manual in-game (1.2.1.4, fake-ID dev build). Specific checks:
- one row per skin; no skinless pet row remains.
- collecting a *new* skin flips its row to collected (icon + count) and persists
  across a full CK restart (ledger round-trip via `WriteCharacter`).
- active summoned pet is counted (Terrier shows 8, not 7).
- uncollected skin of a known species shows the pet name + Unknown icon + `—`;
  a fully-unknown pet shows `???`.
- Level/Value render `—` for pets.
- "Pets" filter category lists exactly the pet-skin rows.

## Risks

- **Gradient shader/material** for the row icon slot — the mod SpriteRenderer
  must adopt CK's gradient-capable material; Phase 0 de-risks. Fallback if
  unworkable: collected skins show the plain base pet icon (no recolor) — skins
  still tracked/counted, just visually undistinguished (degraded, not broken).
- **Retroactive gap** (D1) — pre-install released skins uncredited; correct
  going forward.
- **Catalog growth** by `Σ skins` across pets (modest; viewport virtualization
  from Iter-3.8 already handles ~10,800 rows).

## Out of scope

- **Iter-16.2 — critters.** `Critter` (801) is excluded by the bake yet caught
  critters (ObjectIDs 9800–9819, ~15) carry the same ObjectID and ARE
  discovery-tracked (no Critter special-case in `SetObjectAsDiscovered`). A
  genuine Iter-7.1-style filter relaxation; selection of the catchable subset
  (icon-guard vs. component vs. ID range) to be settled there via a probe.
- **Iter-17 — general per-variation/skin tracking** for non-pet items (e.g.
  furniture color variants). This iteration's pet-skin loop + `(objectID,
  variation/index)` keying is a concrete first realization that may inform it.
