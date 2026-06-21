using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>Immutable per-snapshot lookup: objectID → owned total, plus whether
    /// the storage portion of that total is only "remembered" (no contributing
    /// container loaded this snapshot). Carried is always live. The remembered flag
    /// is computed and kept available even though the UI currently renders the count
    /// plainly (no live/remembered marker) — a deliberate data-layer affordance.</summary>
    internal sealed class PossessionView
    {
        public static readonly PossessionView Empty =
            new PossessionView(new Dictionary<int, int>(), new HashSet<int>());

        private readonly Dictionary<int, int> _totals;
        private readonly HashSet<int> _remembered;
        private readonly Dictionary<long, int> _petSkins;   // Iter-16.1: PackKey(objectId, skinIndex) → live count

        public PossessionView(Dictionary<int, int> totals, HashSet<int> remembered,
            Dictionary<long, int> petSkins = null)
        {
            _totals = totals;
            _remembered = remembered;
            _petSkins = petSkins ?? new Dictionary<long, int>();
        }

        public int Count(int objectId) => _totals.TryGetValue(objectId, out var c) ? c : 0;
        public bool IsRemembered(int objectId) => _remembered.Contains(objectId);

        // Iter-16.1: live per-(objectId, skinIndex) count for pet-skin rows (carried +
        // loaded containers + active pet; not "remembered" — the PetCollection ledger
        // carries the durable collected flag).
        public int CountSkin(int objectId, int skinIndex)
            => _petSkins.TryGetValue(DiscoveredState.PackKey(objectId, skinIndex), out var c) ? c : 0;

        public PossessionView WithPetSkins(Dictionary<long, int> petSkins)
            => new PossessionView(_totals, _remembered, petSkins);
    }
}
