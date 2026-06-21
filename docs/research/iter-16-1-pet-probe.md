# Iter-16.1 Task-0 probe findings (2026-06-21, game 1.2.1.4)

Throwaway probe in `ItemCatalog.Bake` (committed, reverted net-zero). Enumerated
every `variation==0` object with `HasComponent<PetCD>`. In-game result:

**14 pets.** `skins.Count` (`Manager.ui.petInfosTable.GetPetSkinInfo(id).skins.Count`)
vs `PetCD.maxSkins` vs `LevelCD.level` vs `ObjectInfo.sellValue`/`rarity`:

| id | name | skins | maxSkins | level | sellValue | rarity |
|---|---|---|---|---|---|---|
| 1228 | Eulux | 8 | 8 | 7 | -1 | Uncommon |
| 1234 | Federninchen | 8 | 8 | 7 | -1 | Uncommon |
| 1225 | Glutschweif | 8 | 8 | 7 | -1 | Uncommon |
| 1222 | Subterrier | 8 | 8 | 7 | -1 | Uncommon |
| 1258 | Elektrohaustier | **-1** | **0** | 7 | -1 | Uncommon |
| 1253 | Arkaner Symbiont | **-1** | **0** | 7 | -1 | Uncommon |
| 1247 | Pheromotte | 8 | 8 | **10** | -1 | Uncommon |
| 1243 | Junior-Lavaschleim | 1 | 1 | 7 | -1 | Uncommon |
| 1240 | Junior-Lilaschleim | 1 | 1 | 7 | -1 | Uncommon |
| 1246 | Prinzenschleim | 1 | 1 | 7 | -1 | Uncommon |
| 1231 | Junior-Orangeschleim | 1 | 1 | 7 | -1 | Uncommon |
| 1237 | Junior-Blauschleim | 1 | 1 | 7 | -1 | Uncommon |
| 1252 | Schmiegeltierchen | 8 | 8 | **16** | -1 | Uncommon |
| 1261 | Gruselohr | 8 | 8 | 7 | -1 | Uncommon |

`ItemCatalog baked: 10836 items`, bake-total 417 ms. No `CompileFailed` (probe ran).

## Conclusions

1. **`skins.Count` == `maxSkins`** where both valid → either works as the per-skin
   row count. Use `skins.Count` when `GetPetSkinInfo != null`, else fall back to
   `max(1, maxSkins)`.
2. **Two pets (1258 Elektrohaustier, 1253 Arkaner Symbiont) have NO `petInfosTable`
   skin info AND maxSkins=0.** The T2 fallback `max(1, maxSkins)` gives them **1 row**
   with the base icon (no gradient — `GetPetSkinInfo` returns null at render). Edge
   case handled, not special-cased.
3. **Level is a REAL per-pet value, not a flat template** — it varies (7 / 10 / 16,
   ≈ the pet's find-area level). The plan's original "Level → `—` for pets" bake-in
   premise (from the user's "all 7" screenshot observation) is **disproved**.
   → **Decision change: KEEP Level for pet rows.**
4. **Value** is `sellValue == -1` (auto-compute) for every pet → uniform **6** for all
   (rarity echo, all Uncommon; `ComputeSellValue` derives `1 + max(0,(int)rarity)*5`
   = 6 for Uncommon). Not pet-specific, but harmless and consistent with every other
   item. → **KEEP Value** (no special suppression); pet rows flow through the normal
   Level/Value path, simplifying T2 (no `0/—` special case).
5. **Per-skin row total ≈ 63** (7 pets × 8 + 7 pets × 1) replacing 14 skinless rows
   (net +49). Modest; viewport virtualization already handles ~10,800.

## Deferred to their tasks (probe could not cover)

- **Gradient icon (T6):** the data path needs an asmdef reference — `primaryGradientMap`
  returns `GradientMapDataBlock : ScriptableDataBlock`, in the **`ScriptableData`**
  assembly the mod runtime `.asmdef` does NOT reference (probe build hit `CS0012` on
  it). T6 must add that reference before the gradient compiles, then verify the
  `USE_GRADIENT_MAP` keyword works on the mod's row-icon material (fallback: plain
  base icon).
- **Active-pet skinIndex read (T4):** needs the ECS world + `PetOwnerCD.PetEntity`,
  verified in T4's in-game check.

## Process note

Worktree AssetDatabase staleness bit again: build #2 compiled stale source (errored
on already-removed lines). Fix = clear `Library/{SourceAssetDB,ArtifactDB,Artifacts,Bee}`
before each worktree build. Also: a Steam-Cloud-conflict native crash (unrelated)
blocked the first in-game run until Steam Cloud was disabled globally.
