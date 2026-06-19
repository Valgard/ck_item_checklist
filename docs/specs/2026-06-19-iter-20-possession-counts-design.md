# Iter-20 — Possession Counts (per-row "how many do I own") — Design

Status: **design, spike-verified** (2026-06-19, branch `iter-20`)

## 1. Problem & intent

ItemChecklist today tracks **discovery** (lifetime: "have I ever encountered
item X"). This iteration adds a **second completeness axis: possession** — for
players who don't just want "I've discovered everything" but "I own **at least
one** of every item." Each row gains a count of how many of that item the player
currently owns; the threshold of interest is ≥1 (own at least one), the number is
the detail.

This reframes the original roadmap entry: it is **not** "mirror crafting-station
nearby-pull". Crafting *range* is irrelevant to ownership; the crafting **station**
re-enters only as a spatial *ownership anchor* (§5).

## 2. Goals / non-goals

**Goals**
- Per-row possession count: carried inventory + the player's base storage/display
  furniture.
- Available **anywhere** (including away from base) via a persisted, per-character
  "last-known" ledger — not only while storage chunks are loaded.
- Provenance: distinguish a **live** value (storage currently loaded) from a
  **last-known/remembered** value.
- Exclude everything that isn't the player's own storage (dropped items, NPC
  shops, world/boss/locked chests, dungeon & music-puzzle containers, processing
  stations' transient slots, engine entities).

**Non-goals (deferred to v2)**
- Exact **location** display ("which chest, where") — the per-container ledger
  holds coordinates, but surfacing them needs a tooltip/detail panel.
- A monotonic "owned ✓" completion tick + a second HUD/footer "owned K/M" stat.
- Per-variation possession (stays collapsed to `variation == 0`, like discovery).
- Cross-machine / wild-mob inventories, multiplayer remote-base aggregation.

## 3. Possession semantics (the model)

> **Possession(item) = (count in the local player's carried inventory, always
> live) + (count across all player storage/display furniture within radius R of a
> player crafting-station anchor, live when its chunk is loaded, else last-known
> from the persisted ledger).**

- **Carried** is always trustworthy (the player entity is always loaded).
- **Storage/display** is observed opportunistically: whenever a qualifying
  container is loaded (player's ~200-tile keep-loaded bubble, §9), its contents
  refresh the ledger entry keyed by world position. Away from base, the last-known
  value is shown.
- "Base detection" is **not** required and **not** attempted — CK has no base
  concept. The crafting-station anchor (§5) does the base-vs-world discrimination.

## 4. Data model

- **`PossessionLedger`** — per active character GUID. A map
  `containerKey (int x, int z) → { objectID → count }`, where the key is the
  container's world tile position (XZ plane; stable across sessions, unlike ECS
  entity ids). Persisted (§8).
- **Per-snapshot live pass** builds, from the loaded ECS world, the set of
  qualifying containers + their current contents.
- **Merge for display:** for each ledger key, use the live contents if that
  container is loaded this snapshot, else the stored contents. Then overwrite the
  stored entry for every live container (so persistence reflects the latest
  observation — **self-healing**: a removed/emptied container corrects on its next
  load; a destroyed one corrects when its chunk is re-entered).
- **Carried** is summed separately each snapshot (never persisted — always live).
- **Display total per item** = carried + Σ(ledger, merged).
- **Liveness flag per item** = is *any* contributing storage container loaded this
  snapshot (→ "live"), else the storage part is "remembered".

The classification (§5) reads per-entity ECS components (`CraftingCD`,
`MineableCD`) + the object's `objectType`; the ledger is keyed by container, not
by catalog item.

## 5. Classification (data-validated against a real base — §12)

Run an `EntityQuery` over the world for `ContainedObjectsBuffer + ObjectDataCD`,
then per entity:

| Bucket | Rule | Examples (verified) |
|---|---|---|
| **Carried** | `objectID == Player` (6000) **and** it is the local player's entity | the player's backpack/hotbar |
| **Anchor** | has runtime `CraftingCD` **and** `objectID != Player` | Furnace, GlassSmelter, SmelterKiln, CookingPot, TableSaw, SalvageAndRepairStation, EggIncubator (boss statues too — harmless) |
| **Counted container** | has `ContainedObjectsBuffer`, **no** `CraftingCD`, `objectType == PlaceablePrefab` (800), **`MineableCD`** present, objectID **not** in the `Locked*Chest` family, **and** within radius R of an Anchor | every open chest incl. InventoryChest/InventoryLarvaHiveChest/InventoryAncientChest, CopperChest, IronChest, **GlurchChest** (boss); Mannequin, Dresser, WoodTable, Pedestal/StonePedestal/SkeletonPedestal |
| **Excluded** | everything else | DroppedItem (loose drops, dominant near base — `NonUsable`=0), Caveling/SlimeMerchant (NPC shops — `Creature`=900), TheCore (`NonUsable`=0), **`Locked*Chest` family** (contents unknown until opened); stations + player fall out via `CraftingCD` |

Key insights baked into the rules (all from the §12 spike, against a real base):
- **`CraftingCD` is dual-purpose:** present → it's a *station* (an anchor; its own
  transient input/output slots are **not** counted). Absent → it's storage/display.
- **The player carries `CraftingCD`** (hand-crafting) → special-cased out of
  "anchor" and handled as carried.
- **`ObjectType.PlaceablePrefab` (800) + `MineableCD` is the positive furniture
  filter.** `PlaceablePrefab` excludes DroppedItem/TheCore (`NonUsable`=0) and NPC
  merchants (`Creature`=900); `MineableCD` ("can be mined / picked up") is the
  "you can own & move it" guard — and it is exactly what admits **boss/locked
  chests** (minable, placeable). **Craftability is NOT used:** it was the v1
  candidate, but boss/locked chests are legitimate player storage yet their
  craftable flag is unreliable, and the spike showed every furniture item is
  `PlaceablePrefab` regardless. `Pug.Automation.StorageCD` is likewise **not** a
  chest marker (false on every container — an automation/conveyor concept).
- **Craftable-but-not-yours** (e.g. an ordinary chest/pedestal in a music-puzzle
  room — those rooms contain normal chests and pedestals, same ObjectIDs as yours)
  is removed by the **anchor**, not by type.
- **Locked chests are excluded** — their contents are unknown to the player until
  opened, so counting them would be wrong. There is **no runtime lock component**;
  the signal is the ObjectID, since opening a locked chest **swaps its ObjectID**
  from the `Locked*Chest` family (`LockedCopper/Iron/Scarlet/Octarine/Galaxite/
  Solarite/ReluciteChest` = 210/213/216/219/222/225/950; the boss
  `LockedPrince/Queen/KingChest` = 181/182/183) to its `Unlocked*`/normal
  counterpart — which then counts normally. Implement as a static ObjectID set.
  (Empirically a still-locked chest exposes **0** buffer items, so this is mostly a
  correctness/future-proof safeguard, not a current count change.)
- **Name-resolution gotcha (decompile):** map `(int)objectID` against the
  **`ObjectID` enum range only** (`Pug.Base`, ~lines 2099–4535). Other enums
  (`AreaLevel`, `ObjectType`) reuse the same integers — `100` is `Obsidian` in
  `AreaLevel` but `InventoryChest` in `ObjectID` — so a first-match-across-file
  lookup mislabels chests as walls/effects (it briefly fooled this design).

## 6. Anchor & range

- **Anchor = the union of all player crafting stations** (entities with
  `CraftingCD`, excluding the player). A container counts if it is within radius R
  of **any** anchor. Coverage scales automatically with base size (more stations →
  more coverage); multi-base and off-Core bases are covered for free.
- **R is decoupled from CK's crafting range** (~10–15 tiles, too small for large
  bases) and **configurable** via `API.Config`, default generous (~40–60 tiles).
  The only cost of a large R is occasionally counting a world container that sits
  *adjacent* to the base — rare, and the config dials it.
- **No transitive station-chaining.** Geometrically it adds zero container
  coverage over the union at the same R (a container is within R of a station or
  it isn't; linking stations to each other moves no point). Its only real use —
  a cluster-significance filter to reject a lone field station — is YAGNI for v1
  and penalises single-station bases. (If a lone outpost station ever pulls in
  foreign loot in practice, a cluster filter is a clean additive follow-up.)

## 7. Display (v1)

- A **third right-aligned column** on `ItemRow`, beside the Iter-10 Level and Value
  columns: the possession count (carried + storage, merged).
- **Liveness marker:** when the storage portion is "remembered" (no contributing
  container loaded this snapshot), render the count dimmed / with a small marker so
  the player knows it may be stale. Live values render normally.
- **Spoiler guard:** undiscovered rows show `—` (you cannot meaningfully "own" an
  undiscovered item — acquiring it discovers it), consistent with the Lv/Value
  columns.
- Row layout is already dense (icon, name, checkbox, rarity border, Lv, Value +
  coin). The count column reuses the established right-aligned `PugText` pattern;
  exact width/placement is a calibration detail for the plan.

## 8. Persistence (spike-verified)

- **`API.ConfigFilesystem`** (trusted `PugMod.SDK.Runtime.dll` wrapper, sandbox-
  exempt) — `Read(path)→byte[]`, `Write(path, byte[])`, `DirectoryExists`,
  `CreateDirectory`. Verified end-to-end in-game.
- The mod config subdir is **not** auto-created: `CreateDirectory("ItemChecklist")`
  before the first `Write` (else a path error — verified). Root resolves under
  `…/Steam/<id>/mods/`.
- **Serialization is hand-rolled ASCII** (`id:count;` lines, char↔byte loops) — no
  `System.IO`, no `JsonUtility`, no `Encoding`, no `StringBuilder` — to stay
  trivially inside the sandbox (verified round-trip `match=True`).
- **One file per character GUID** (the mod already resolves the active GUID —
  `CharacterDataDiscoverySnapshot` + the Harmony state-machine).
- **Write cadence:** not every change — debounce (e.g. on window close + periodic /
  on world unload). Load on character activation.

## 9. ECS access (spike-verified)

- **World:** inventories live in the **`ServerWorld`** (single-player local host).
  Pick the world by **max count** of `ContainedObjectsBuffer` entities (the
  caveling-divining-rod pattern), never hardcode the world name.
- **Pattern (sandbox-safe, `safetyCheck=True` confirmed):**
  `world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<…>())` →
  `ToEntityArray(Allocator.TempJob)` → `GetComponentData<ObjectDataCD/LocalTransform>` /
  `HasComponent<CraftingCD>` / `GetBuffer<ContainedObjectsBuffer>`. All run from
  plain mod code (no Harmony routing needed), exactly as caveling-divining-rod does.
- **`ContainedObjectsBuffer`** exposes `objectID`, `amount`, `variation` per slot
  (skip `objectID == None`). **`ObjectDataCD.objectID`**, **`LocalTransform.Position`**
  (XZ horizontal, Y = height).
- Mobile-entity guards (`EnemyCD`/`CritterCD`) are kept defensively though the
  craftable filter already excludes them; near-base runs showed 0 of each.

## 10. Performance

- Viewport virtualization (Iter-3.8) means ≤9 `ItemRow`s exist at once — counts are
  never computed for all ~10,800 entries.
- The live pass enumerates loaded inventory entities **once per snapshot** (≈80–130
  at a base — cheap) into an `objectID → count` aggregate; each visible row does an
  O(1) lookup. Snapshot refresh on: window open, `DiscoveredState.Changed`-style
  triggers, and a throttled interval (storage contents change without our hooks).
- No per-frame ECS enumeration; no per-row queries.

## 11. Edge cases / known limitations

- **Remote single-stash** far from any station does not count (false negative) —
  players cluster storage at stations; acceptable, dial R if needed.
- **World container adjacent to base** within R counts (rare false positive) —
  dialable via R.
- **Away-from-base staleness:** consumed-while-away items keep their last-known
  count until the container's chunk is re-entered (self-heals).
- **First launch before visiting base:** persisted ledger primes the display
  immediately; a brand-new character with no ledger shows storage as 0/remembered
  until first observation.
- **CattleFeedTray counts (decided).** Its contents are the player's possession
  while present, even though cattle consume them over time — consistent with the
  live snapshot (the count simply drops as feed is consumed). No special-casing.
  A small ObjectID denylist remains the lever *only* if some future
  placeable-with-inventory turns out to be genuine non-possession.

## 12. Spike results (verified facts this design rests on)

In-game on 1.2.1.4, fake-ID dev build 9999997, against a real base:
- `ItemChecklist … passed code security verification`, `safetyCheck=True` — the
  full ECS surface + `API.ConfigFilesystem` compile inside the sandbox.
- `world='ServerWorld' inventoryEntities=131`; `GetBuffer` read buffers without
  error. Two probe rounds: a coarse one (crafting=13, enemy=0, critter=0) and a
  per-entity fingerprint dump (`type`/`CraftingCD`/`StorageCD`/`MineableCD`).
- Taxonomy harvested → the §5 table. The discriminator landed on
  **`PlaceablePrefab` (800) + `MineableCD`** (not craftability): every furniture
  item — chests incl. **boss/locked** (Glurch/Locked Iron/Copper) and the
  `Inventory*Chest` family, Mannequin ×many, pedestals, Dresser, WoodTable — was
  `type=800, MineableCD=true`; DroppedItem/TheCore were `type=0`, merchants
  `type=900`. `StorageCD` (Pug.Automation) was `false` on everything → useless.
- **Corrected a v1 misread:** chests that first looked like "Obsidian/effect
  entities holding items" were a decompile name-collision — `objectID` 100/101/103
  are `InventoryChest`/`InventoryLarvaHiveChest`/`InventoryAncientChest` in the
  `ObjectID` enum, not `Obsidian`/minion-FX in `AreaLevel`. They are real chests
  and correctly counted. (Hence the §5 name-resolution gotcha.)
- Persistence: `CreateDirectory` then raw `byte[]` round-trip `ok=True` + hand-
  rolled string round-trip `match=True`.
- Player keep-loaded radius: `KeepAreaLoadedCD` Immediate=200 / Start=250 tiles →
  the whole base is loaded when home; partial-load is rare (so the per-container
  ledger's merge-correctness is a minor benefit; its real justification is
  provenance + self-healing).

## 13. Testing

Per project convention (no unit framework): build (`utils/build.sh`) + in-game
smoke test + `Player.log` grep (`error CS|CompileFailed|safetyCheck`). Acceptance
checklist for the implementation: counts correct for carried + base storage;
Mannequin/pedestal items counted; DroppedItem/merchant/locked-chest excluded;
value persists across a game restart and shows away from base with a "remembered"
marker; config radius tunable; no per-frame log/NRE.
