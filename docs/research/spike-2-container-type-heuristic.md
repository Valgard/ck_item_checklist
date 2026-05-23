# Spike #2 — Container Type Heuristic

**Date:** 2026-05-23
**Status:** Hypothesis derived from symbol-table analysis. Live-verification TODO.

## Question

Which ECS component combination distinguishes
(a) player-placed entities from world spawns, AND
(b) storage containers from crafting stations,
so `InitialContainerScanner` can correctly scope its Pass-2 query
**without** hard-coding a prefab allowlist?

## Method

Symbol-table grep on `Pug.ECS.Components.dll`, `Pug.Objects.dll`,
`Pug.Other.dll`, `Interaction.CoreKeeper.Components.dll`.

## Findings

### The inventory data ECS component is `ContainedObjectsBuffer`

```
|ContainedObjectsBuffer
B\Assets\Scripts\ECS\Components\Inventory\ContainedObjectsBuffer.cs
```

DynamicBuffer-shaped, one entry per inventory slot. Every entity with
storage carries it (chests, mannequins, pedestals, crafting stations
all included). This is the **read source** for Pass 2.

There is also `ExtraInventoryCD` (a CD-suffixed component data struct)
that some entities additionally have — likely a secondary inventory
(e.g. mannequin equipment slots) — and serialized variants
(`ContainedObjectsSerializedBuffer`).

### World-spawned chest types are individually identifiable

```
AncientChest, BossChest, EerieChest, MoldChest,
LockedChest, NonPaintableChest, SeaBiomeChest
```

These are concrete chest prefabs/components for world-generated chests.
They live in `Assets/Scripts/Objects/Item/*Chest.cs`. None of them is the
player-built chest variant — those are presumably bare `Chest` /
`PaintableChest` / `DoubleChest`.

### Crafting-station marker: `CraftingHandler` component

```
|CraftingHandler
)\Assets\Scripts\Player\CraftingHandler.cs
activeCraftingHandler, playerCraftingHandler (backing fields)
```

`CraftingHandler` exists as a managed handler class. Its corresponding
ECS-side marker is probably `CraftingCD` or `CraftingHandlerCD` — exact
name needs live verification, but the **presence** of the component
distinguishes crafting stations from pure storage.

### Player-placed marker: NOT found in this DLL pass

No component named `PlayerPlaced`, `PlacedByPlayer`, `BuildPlaced`, or
similar in the searched DLLs. Two plausible explanations:

1. **The marker exists but lives elsewhere** (e.g. `WorldPatcher.dll`,
   `Pug.Builder.Runtime.dll`). Worth a deeper grep before falling back
   to (2).
2. **No explicit marker — distinguished by spawn origin alone.** World
   chests are emplaced by the world-gen pipeline with the
   concrete-type components above; player-built entities don't get those
   concrete types — they just get the generic `ContainedObjectsBuffer`.
   In this model, the exclusion list IS the world-spawn types.

## Hypothesis (Phase 1)

Query for Pass 2:

```csharp
em.CreateEntityQuery(
    new EntityQueryDesc {
        All  = new[] { ComponentType.ReadOnly<ContainedObjectsBuffer>() },
        None = new[] {
            ComponentType.ReadOnly<CraftingHandlerCD>(),         // exclude crafting stations
            ComponentType.ReadOnly<LockedChestCD>(),             // exclude world-spawned chests
            ComponentType.ReadOnly<AncientChestCD>(),
            ComponentType.ReadOnly<BossChestCD>(),
            ComponentType.ReadOnly<EerieChestCD>(),
            ComponentType.ReadOnly<MoldChestCD>(),
            ComponentType.ReadOnly<SeaBiomeChestCD>(),
            ComponentType.ReadOnly<NonPaintableChestCD>(),       // not sure if exclude or include — verify
        }
    });
```

**Mod-transparency note:** even with the exclusion list, this heuristic
remains mod-transparent for *new* container types added by other mods —
they will appear in the query result automatically because they carry
`ContainedObjectsBuffer` and don't carry the world-spawn-specific
components.

**Caveat:** if a player-built chest happens to carry one of the excluded
components (e.g. `NonPaintableChest`?), it will be missed. This is the
single most likely false-negative case and needs verification.

## Live-Verification TODOs (USER)

- [ ] **Deeper grep for `PlayerPlaced`.** Run on the broader DLL set
  (`WorldPatcher.dll`, `Pug.Builder.Runtime.dll`, `Pug.Mods.dll`):
  ```bash
  strings *.dll | grep -iE "(PlayerPlaced|PlacedBy|BuiltBy|BuildPlace)"
  ```
  If a clean marker exists, replace the exclusion-list heuristic with
  a positive `PlayerPlaced` requirement — much cleaner.
- [ ] **Probe player-built chest in-game.** Place a wooden chest in a
  world, run the diagnostic ECS-component log (see spec §C2.2):
  ```csharp
  var components = em.GetComponentTypes(chestEntity);
  Debug.Log(string.Join(", ", components.ToArray().Select(t => t.GetManagedType().Name)));
  ```
  Compare to a world-spawned chest. The diff IS the heuristic.
- [ ] **Verify `NonPaintableChest`.** Is this a player-craftable chest
  type, or world-only? If craftable, remove from exclusion list.
- [ ] **Confirm component names with `CD` suffix.** Pugstorm's ECS naming
  convention may differ. The IL might reveal `CraftingHandlerComponent`,
  `Crafting_CD`, or just `CraftingHandler`. Adjust as found.

## Implication for the plan

- `InitialContainerScanner.cs` (Plan Task E1.2) needs the verified
  component names. The placeholder block in the plan stays but with the
  exclusion-list as the working hypothesis.
- Allow the implementation to start with the exclusion list, then narrow
  the heuristic on first live-test feedback.
