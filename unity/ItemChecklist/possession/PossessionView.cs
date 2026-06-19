using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>Immutable per-snapshot lookup: objectID → owned total, plus whether
    /// the storage portion of that total is only "remembered" (no contributing
    /// container loaded this snapshot). Carried is always live.</summary>
    internal sealed class PossessionView
    {
        public static readonly PossessionView Empty =
            new PossessionView(new Dictionary<int, int>(), new HashSet<int>());

        private readonly Dictionary<int, int> _totals;
        private readonly HashSet<int> _remembered;

        public PossessionView(Dictionary<int, int> totals, HashSet<int> remembered)
        {
            _totals = totals;
            _remembered = remembered;
        }

        public int Count(int objectId) => _totals.TryGetValue(objectId, out var c) ? c : 0;
        public bool IsRemembered(int objectId) => _remembered.Contains(objectId);
    }
}
