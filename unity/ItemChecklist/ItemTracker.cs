namespace ItemChecklist
{
    /// <summary>Iter-40: the single "which item is the player locating right now?"
    /// state. Session-only — a static field, reset to -1 on every world load, never
    /// persisted. One item at a time. Set/cleared by the row click
    /// (<see cref="UI.ItemRow"/>) and the cancel hotkey; read by
    /// <see cref="ItemChecklistMod.Update"/> (to feed <see cref="UI.TrackerHud"/>) and by
    /// <see cref="UI.ItemRow.Bind"/> (to mark the tracked row). Location is per objectID
    /// (the ledger keys container contents by objectID); the variation only distinguishes
    /// which row is the tracked one (pet-skin / cattle-colour rows share an objectID).</summary>
    internal static class ItemTracker
    {
        internal static int TrackedId { get; private set; } = -1;
        internal static int TrackedVariation { get; private set; }

        internal static bool IsActive => TrackedId >= 0;

        internal static bool Matches(int objectId, int variation)
            => TrackedId == objectId && TrackedVariation == variation;

        /// <summary>Track this row, or untrack if it is already the tracked row. Tracking
        /// a different row replaces the previous (one item at a time).</summary>
        internal static void Toggle(int objectId, int variation)
        {
            if (Matches(objectId, variation)) Clear();
            else { TrackedId = objectId; TrackedVariation = variation; }
        }

        internal static void Clear()
        {
            TrackedId = -1;
            TrackedVariation = 0;
        }
    }
}
