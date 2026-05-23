using System;
using System.Collections.Generic;

namespace ItemChecklist
{
    /// <summary>
    /// Records the source that caused a SetOwned mutation. Currently used
    /// only for logging; does not affect resulting state.
    /// </summary>
    public enum OwnSource
    {
        Pickup,
        InitialScan,
        Manual,
        Restore
    }

    /// <summary>
    /// In-memory checklist for a single (world × player) pairing. The
    /// single mutator <see cref="SetOwned"/> enforces the invariant that
    /// <c>owned ⊆ discovered</c> and that <c>discovered</c> is monotonically
    /// non-decreasing (a manual uncheck does not "un-discover" the item).
    ///
    /// Pure .NET — no Unity dependencies — so this class is unit-testable
    /// without an Editor harness.
    /// </summary>
    public sealed class ChecklistState
    {
        private readonly HashSet<int> owned = new HashSet<int>();
        private readonly HashSet<int> discovered = new HashSet<int>();

        public int OwnedCount => owned.Count;
        public int DiscoveredCount => discovered.Count;

        public bool IsOwned(int id) => owned.Contains(id);
        public bool IsDiscovered(int id) => discovered.Contains(id);

        public event Action<int, bool> OwnedChanged;     // (id, newValue)
        public event Action<int> DiscoveredAdded;        // id
        public event Action StateChanged;                // any mutation (Store auto-save trigger)

        /// <summary>
        /// Sole mutation entry point. Idempotent if the new value equals
        /// the current value (no events raised). Always updates discovered
        /// to true if value is true; never clears discovered when value is
        /// false.
        /// </summary>
        public void SetOwned(int id, bool value, OwnSource source)
        {
            if (value)
            {
                bool addedToOwned = owned.Add(id);
                bool addedToDiscovered = discovered.Add(id);

                if (addedToOwned) OwnedChanged?.Invoke(id, true);
                if (addedToDiscovered) DiscoveredAdded?.Invoke(id);

                if (addedToOwned || addedToDiscovered) StateChanged?.Invoke();
            }
            else
            {
                bool removedFromOwned = owned.Remove(id);
                if (removedFromOwned)
                {
                    OwnedChanged?.Invoke(id, false);
                    StateChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Internal-only loader for Restore source. Bypasses event raising
        /// to avoid spurious StateChanged storms during world-load.
        /// </summary>
        internal void LoadFromSnapshot(IEnumerable<int> ownedIds, IEnumerable<int> discoveredIds)
        {
            owned.Clear();
            discovered.Clear();
            foreach (var id in ownedIds) owned.Add(id);
            foreach (var id in discoveredIds) discovered.Add(id);
        }

        /// <summary>
        /// Snapshot accessors for ChecklistStore — return sorted defensive
        /// copies so the store can serialize without races against runtime
        /// mutations.
        /// </summary>
        public int[] SnapshotOwned()
        {
            var a = new int[owned.Count];
            owned.CopyTo(a);
            Array.Sort(a);
            return a;
        }

        public int[] SnapshotDiscovered()
        {
            var a = new int[discovered.Count];
            discovered.CopyTo(a);
            Array.Sort(a);
            return a;
        }
    }
}
