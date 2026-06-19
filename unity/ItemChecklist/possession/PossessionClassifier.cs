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
    }
}
