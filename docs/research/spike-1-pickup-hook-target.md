# Spike #1 — Pickup Hook Target

**Date:** 2026-05-23
**Status:** Plan revision recommended. Live-verification TODO.

## Question

Which method should `InventoryAddPatch` Harmony-patch so it fires
whenever an item enters the local player's inventory, regardless of
source (pickup, container-take, drop-from-crafting)?

## Method

Symbol-table grep on `Pug.Other.dll`.

## Findings

### Pickup runs in `PickUpItemSystem` — a Burst-compiled DOTS system

```
__PickUpItemCD_RW_ComponentTypeHandle
__PickUpItemSystem_GatherCanPickupJob_WithDefaultQuery_JobEntityTypeHandle
__PickUpItemSystem_PickUpJob_WithDefaultQuery_JobEntityTypeHandle
__PickUpItemSystem_SetPickedUpItemPredictedJob_WithDefaultQuery_JobEntityTypeHandle
__PickUpItemSystem_StartPickUpJob_WithDefaultQuery_JobEntityTypeHandle
__PickUpItemSystem_UpdatePickUpDistanceJob_WithDefaultQuery_JobEntityTypeHandle
+\Assets\Scripts\ECS\Systems\PickUpSystem.cs
```

The `__PickUpItemSystem_<Job>_WithDefaultQuery_JobEntityTypeHandle`
naming pattern is Burst-generated lookup-table boilerplate — these
jobs **are Burst-compiled**. Harmony cannot patch their `Execute`
bodies.

`BurstDisabler.DisableBurstForSystem<PickUpItemSystem>()` would expose
the managed `OnUpdate`, but the actual add-to-inventory logic lives in
the Burst jobs, not in `OnUpdate`. We can intercept the dispatch but
not the writes.

### Better target: `InventoryUpdateSystem` + `InventoryChangeData`

```
__Inventory_InventoryUpdateSystem_InitializeInventoryJob_WithDefaultQuery_JobEntityTypeHandle
>\Assets\Scripts\ECS\Systems\Inventory\InventoryUpdateSystem.cs
Inventory.InventoryUpdateSystem
Inventory.InventoryUpdateSystem+InventoryInitialized
Inventory|InventoryChangeData
Inventory|InventoryUpdateSystem
inventoryChangeData
inventoryUpdateBuffer
inventoryUpdateBufferEntity
```

`InventoryUpdateSystem` is the managed system that consumes inventory
change events. The presence of `inventoryUpdateBuffer` and
`InventoryChangeData` strongly suggests there's a structured change-event
stream we can subscribe to or read from — much cleaner than patching
into a Burst job.

There's also `Inventory.InventoryUpdateSystem+InventoryInitialized` (a
nested type that looks like an init-completion marker) — useful as a
"first-time inventory ready" signal for the Initial Scan.

### Alternative: passive polling (recommended for robustness)

Both Harmony approaches above carry version-fragility risk. A more
robust approach for a personal-use mod:

**Poll the local player's `ContainedObjectsBuffer` every ~0.5–1.0 sec,
diff against the last snapshot, treat new objectIDs as pickups.**

- ~10–20 slots in player inventory → diff is O(slots), negligible cost.
- Fires on EVERY way an item gets into inventory (pickup, take-from-
  chest, crafting output, etc.) — no need to enumerate the source
  systems.
- Survives game updates that rename internal pickup methods.
- Does not require Burst-disable or any Harmony patch.

The trade-off: ~1-sec latency between pickup and checklist update. For
a non-real-time checklist UI, this is acceptable.

## Recommendation: revise the plan to use polling

The original spec assumed a Harmony patch in `InventoryAddPatch.cs`.
Given the Burst-job complication, the recommendation is:

**Replace `InventoryAddPatch` with `InventoryPoller`** — same role
(observer that calls `state.SetOwned(id, true, Pickup)`), different
mechanism. The pickup-trigger pipeline §3.1 in the spec doesn't
mandate Harmony; the only requirement is "items entering inventory
become owned".

```csharp
public sealed class InventoryPoller
{
    private readonly ChecklistState state;
    private float lastPollTime;
    private const float PollIntervalSec = 0.5f;
    private HashSet<int> lastSnapshot = new();

    public InventoryPoller(ChecklistState state) { this.state = state; }

    public void Tick(float currentTime)
    {
        if (currentTime - lastPollTime < PollIntervalSec) return;
        lastPollTime = currentTime;

        var currentIds = SnapshotLocalPlayerInventoryIds();   // ECS query
        foreach (var id in currentIds)
            if (!lastSnapshot.Contains(id))
                state.SetOwned(id, true, OwnSource.Pickup);
        lastSnapshot = currentIds;
    }

    private HashSet<int> SnapshotLocalPlayerInventoryIds()
    {
        // Read ContainedObjectsBuffer of API.LocalPlayer's entity.
        // Concrete API names per live verification.
        var result = new HashSet<int>();
        // … iterate slots, add non-zero objectIDs …
        return result;
    }
}
```

Called from `ItemChecklistMod.Update()` once per frame, with the
internal interval gate.

## Live-Verification TODOs (USER)

- [ ] **Decide: poll vs. patch.** If poll, no live test needed for the
  pickup mechanism itself — but verify the ECS-query path returns the
  expected slot data (use a single throwaway log: pickup a copper ore,
  check that the next poll log shows it).
- [ ] **Locate `API.LocalPlayer.entity`** or equivalent — the entity ID
  that gives us the player's `ContainedObjectsBuffer`.
- [ ] **Check `InventoryUpdateSystem+InventoryInitialized`.** If it's a
  managed event we can subscribe to ("OnInventoryFirstReady" → trigger
  Initial Scan), it's a clean hook for the world-begin lifecycle of
  Task G1.

## Implication for the plan

- **Plan Task E2 (`InventoryAddPatch.cs`) should be REPLACED** by a new
  task **E2': `InventoryPoller.cs`** with the structure above. Same
  role in `ChecklistRuntime`, different mechanism, no Burst concerns,
  no Harmony attribute.
- The "Burst-disable" step in Plan Task G1 / Init() becomes optional.
- `ChecklistRuntime` exposes `InventoryPoller` instead of relying on
  static patch hooks; `ItemChecklistMod.Update()` calls
  `Instance?.Poller.Tick(Time.unscaledTime)` each frame.
