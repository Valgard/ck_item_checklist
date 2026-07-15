using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>Immutable per-snapshot lookup: objectID → owned total, plus whether the
    /// storage portion of that total is only "remembered" (no contributing container loaded
    /// this snapshot). Carried is always live. Iter-41: pet-skin and cattle/paint colour
    /// counts share ONE packed aux dict (PackKey(id, secondDim) → count), fed from the
    /// ledger's remembered aux containers merged with the live carried/active aux — so a
    /// base-stored pet / penned animal / placed painted item stays counted while away.</summary>
    internal sealed class PossessionView
    {
        public static readonly PossessionView Empty =
            new PossessionView(new Dictionary<int, int>(), new HashSet<int>(), new Dictionary<long, int>());

        private readonly Dictionary<int, int> _totals;
        private readonly HashSet<int> _remembered;
        private readonly Dictionary<long, int> _aux;   // PackKey(id, skin|variation) → owned count

        public PossessionView(Dictionary<int, int> totals, HashSet<int> remembered, Dictionary<long, int> aux)
        {
            _totals = totals;
            _remembered = remembered;
            _aux = aux ?? new Dictionary<long, int>();
        }

        public int Count(int objectId) => _totals.TryGetValue(objectId, out var c) ? c : 0;
        public bool IsRemembered(int objectId) => _remembered.Contains(objectId);

        // Pet-skin rows (secondDim = skinIndex).
        public int CountSkin(int objectId, int skinIndex)
            => _aux.TryGetValue(DiscoveredState.PackKey(objectId, skinIndex), out var c) ? c : 0;

        // Cattle colours + placed paintable furniture (secondDim = variation).
        public int CountColour(int objectId, int variation)
            => _aux.TryGetValue(DiscoveredState.PackKey(objectId, variation), out var c) ? c : 0;
    }
}
