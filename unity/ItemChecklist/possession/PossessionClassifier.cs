using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Pure classification of inventory entities into the possession buckets
    /// (spec §5). All signals are spike-verified against a real base.
    /// </summary>
    internal static class PossessionClassifier
    {
        // ObjectType.PlaceablePrefab — placed furniture/buildings (chests, pedestals,
        // mannequins, stations). NonUsable=0 (DroppedItem/TheCore), Creature=900 (NPCs).
        public const int PlaceablePrefab = 800;

        // Locked*Chest ObjectIDs: contents are unknown until opened, and opening swaps
        // the ObjectID to the Unlocked/normal variant (which then counts). No runtime
        // lock component exists, so this static set is the signal.
        public static readonly HashSet<int> LockedChestIds = new HashSet<int>
        {
            181, 182, 183,            // LockedPrince/Queen/KingChest (boss)
            210, 213, 216, 219, 222, 225, 950  // LockedCopper/Iron/Scarlet/Octarine/Galaxite/Solarite/ReluciteChest
        };

        public static bool IsLockedChest(int objectId) => LockedChestIds.Contains(objectId);

        // Boss-summon statues (LarvaBossStatue 4101 … ExcavationBossStatue 4109 — a
        // contiguous block). Placeable + in the catalog (discoverable), but typed
        // NonUsable(0) + not Mineable, so the generic furniture filter misses them.
        // Counted explicitly as owned placed objects (no contents — they are stations).
        public static bool IsBossStatue(int objectId) => objectId >= 4101 && objectId <= 4109;
    }
}
