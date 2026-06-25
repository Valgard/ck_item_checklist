using System;
using System.Collections.Generic;

namespace ItemChecklist
{
    /// <summary>
    /// In-memory mirror of <c>CharacterData.discoveredObjects2</c> for the
    /// currently active character. Keys are packed (objectId, variation)
    /// tuples — see <see cref="PackKey"/>.
    ///
    /// Populated by:
    /// <list type="bullet">
    ///   <item><see cref="CharacterDataDiscoverySnapshot"/> on save-load
    ///     (Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>)</item>
    ///   <item><see cref="SaveManagerDiscoveryHook"/> on every new pickup
    ///     (Harmony postfix on <c>SaveManager.SetObjectAsDiscovered</c>)</item>
    /// </list>
    /// Read-only from the consumer's perspective — no public mutator. Both
    /// hook classes are in the same assembly so they can call the
    /// <c>internal</c> mutators.
    ///
    /// Persistence is handled by CK itself (the character save's
    /// <c>discoveredObjects2</c> serialization). We never write to
    /// PlayerPrefs or any file.
    /// </summary>
    public sealed class DiscoveredState
    {
        private static readonly DiscoveredState _instance = new DiscoveredState();
        public static DiscoveredState Instance => _instance;

        private readonly HashSet<long> keys = new HashSet<long>();

        // Iter-17: O(1) "is ANY variation of this objectId discovered?" — backs the
        // cattle placeholder row (reachable regardless of which colour is found first).
        private readonly HashSet<int> objectIds = new HashSet<int>();

        /// <summary>
        /// Pack an (objectId, variation) pair into a single long key.
        /// Upper 32 bits: objectId. Lower 32 bits: variation (as uint, to
        /// preserve sign-bit identity since CookedFoodCD encodes via
        /// <c>(primary &lt;&lt; 16) | secondary</c>).
        /// </summary>
        public static long PackKey(int objectId, int variation) =>
            ((long)objectId << 32) | (uint)variation;

        // Symmetric unpack of PackKey (Iter-16.1: PetCollection serialises by id+skin).
        public static int KeyObjectId(long key) => (int)(key >> 32);
        public static int KeyVariation(long key) => (int)(uint)key;

        public int Count => keys.Count;
        public bool IsDiscovered(int objectId, int variation) =>
            keys.Contains(PackKey(objectId, variation));

        // Iter-17: true iff any (objectId, *) pair is discovered. O(1) — backs the
        // cattle placeholder's collected-test (IsCollected hot path, per-row render).
        public bool IsDiscoveredAnyVariation(int objectId) => objectIds.Contains(objectId);

        /// <summary>Discovered variations of one objectId (cold path — the bake-time
        /// cattle loop; ≤6 cattle ids × once per bake). Scans the packed-key set.</summary>
        public List<int> DiscoveredVariationsOf(int objectId)
        {
            var result = new List<int>();
            foreach (var k in keys)
                if (KeyObjectId(k) == objectId) result.Add(KeyVariation(k));
            return result;
        }

        /// <summary>The full packed discovered set — a future generic
        /// runtime-variation loop would iterate this once per bake (no consumer in
        /// Iter-17; the sweep found no non-cattle runtime variations).</summary>
        public IEnumerable<long> DiscoveredKeys() => keys;

        /// <summary>Raised when a single new (objectId, variation) is added.</summary>
        public event Action<int, int> Discovered;
        /// <summary>Raised after any mutation (Snapshot or AddOne).</summary>
        public event Action Changed;

        internal void AddOne(int objectId, int variation)
        {
            if (keys.Add(PackKey(objectId, variation)))
            {
                objectIds.Add(objectId);                       // Iter-17
                UnityEngine.Debug.Log(
                    $"[ItemChecklist] AddOne: ({objectId}, {variation}) (total {keys.Count})");
                Discovered?.Invoke(objectId, variation);
                Changed?.Invoke();
            }
        }

        internal void Snapshot(IEnumerable<long> packedKeys)
        {
            keys.Clear();
            objectIds.Clear();                                 // Iter-17
            foreach (var k in packedKeys) { keys.Add(k); objectIds.Add(KeyObjectId(k)); }
            Changed?.Invoke();
        }
    }
}
