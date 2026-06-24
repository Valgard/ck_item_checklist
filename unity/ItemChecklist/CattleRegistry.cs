using System.Collections.Generic;
using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Bake-built cattle facts (Iter-16.3). Identifies the farm-livestock subset of
    /// <c>ObjectType.Creature</c> via the <c>CattleCD</c> marker, and folds each baby
    /// form into its adult species.
    ///
    /// The baby→adult link is **structural**, not name-based:
    /// <c>BreedStateAuthoring.babyType</c> is statically authored on the adult prefab
    /// (→ <c>BreedStateCD.babyType</c>), so an adult carries <c>BreedStateCD</c> whose
    /// <c>babyType</c> is its baby's <c>ObjectID</c>; a baby carries no
    /// <c>BreedStateCD</c>. A cattle id is therefore a baby ⇔ it appears as some
    /// adult's <c>babyType</c>. Verified in-game (1.2.1.4): 6 adults
    /// (Cow/Goat/RolyPoly/Turtle/Dodo/Camel) + 6 babies, all <c>icon=True</c>.
    /// </summary>
    internal static class CattleRegistry
    {
        private static readonly HashSet<int> s_cattle = new HashSet<int>();
        private static readonly Dictionary<int, int> s_babyToAdult = new Dictionary<int, int>();

        /// <summary>Rebuild from the static object DB. Call once per bake, before Loop 1.</summary>
        public static void Build()
        {
            s_cattle.Clear();
            s_babyToAdult.Clear();
            if (PugDatabase.objectsByType == null) return;
            foreach (var od in PugDatabase.objectsByType.Keys)
            {
                if (od.variation != 0) continue;
                if (!PugDatabase.HasComponent<CattleCD>(od)) continue;
                s_cattle.Add((int)od.objectID);
                if (PugDatabase.TryGetComponent<BreedStateCD>(od, out var bs)
                    && bs.babyType != ObjectID.None)
                    s_babyToAdult[(int)bs.babyType] = (int)od.objectID;
            }
        }

        /// <summary>True for any CattleCD-marked species (adult or baby).</summary>
        public static bool IsCattle(int objectId) => s_cattle.Contains(objectId);

        /// <summary>True for a baby form (folded into its adult — no own catalog row).</summary>
        public static bool IsBaby(int objectId) => s_babyToAdult.ContainsKey(objectId);

        /// <summary>The adult species' ObjectID for a baby; the id unchanged otherwise.</summary>
        public static int AdultOf(int objectId)
            => s_babyToAdult.TryGetValue(objectId, out var adult) ? adult : objectId;
    }
}
