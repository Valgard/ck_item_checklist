using System.Collections.Generic;
using PugMod;
using Unity.Collections;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Bake-built cattle facts (Iter-16.3 / Iter-17). Identifies the farm-livestock
    /// subset of <c>ObjectType.Creature</c> via the <c>CattleCD</c> marker, folds each
    /// baby form into its adult species, and (Iter-17) reads each species' full colour
    /// palette.
    ///
    /// The baby→adult link is **structural**, not name-based:
    /// <c>BreedStateAuthoring.babyType</c> is statically authored on the adult prefab
    /// (→ <c>BreedStateCD.babyType</c>), so an adult carries <c>BreedStateCD</c> whose
    /// <c>babyType</c> is its baby's <c>ObjectID</c>; a baby carries no
    /// <c>BreedStateCD</c>. A cattle id is therefore a baby ⇔ it appears as some
    /// adult's <c>babyType</c>. Verified in-game (1.2.1.4): 6 adults
    /// (Cow/Goat/RolyPoly/Turtle/Dodo/Camel) + 6 babies, all <c>icon=True</c>.
    ///
    /// **Iter-17 colour palette.** CK exposes no per-species colour count, BUT the
    /// authored breeding-outcome list IS the full colour set: each cattle prefab's
    /// <c>ObjectPropertiesCD</c> carries a <c>PossibleChildVariation[]</c> under property
    /// id <see cref="BreedVariationPropertyId"/>; its distinct <c>.Variation</c> values
    /// are the species' colours. Verified in-game (1.2.1.4): every species = {0,1,2,3,4}
    /// (5 colours), and all wild-caught colours fall within it (sandbox-safe read,
    /// <c>safetyCheck=True</c>). This is the enumeration the pet-style fixed-slot rows need.
    /// </summary>
    internal static class CattleRegistry
    {
        // Property id of the breeding-outcome list on a cattle's ObjectPropertiesCD
        // (Pug.Other GetChildVariation reads the same hash). Each entry is a
        // BreedStateCD.PossibleChildVariation { int Variation; float AccumulatedProbability }.
        private const int BreedVariationPropertyId = 239678920;

        private static readonly HashSet<int> s_cattle = new HashSet<int>();
        private static readonly Dictionary<int, int> s_babyToAdult = new Dictionary<int, int>();
        private static readonly Dictionary<int, List<int>> s_colours = new Dictionary<int, List<int>>();

        /// <summary>Rebuild from the static object DB. Call once per bake, before Loop 1.</summary>
        public static void Build()
        {
            s_cattle.Clear();
            s_babyToAdult.Clear();
            s_colours.Clear();
            if (PugDatabase.objectsByType == null) return;
            foreach (var od in PugDatabase.objectsByType.Keys)
            {
                if (od.variation != 0) continue;
                if (!PugDatabase.HasComponent<CattleCD>(od)) continue;
                s_cattle.Add((int)od.objectID);
                if (PugDatabase.TryGetComponent<BreedStateCD>(od, out var bs)
                    && bs.babyType != ObjectID.None)
                    s_babyToAdult[(int)bs.babyType] = (int)od.objectID;
                s_colours[(int)od.objectID] = ReadColours(od);
            }
        }

        // Read the species' colour set from the authored PossibleChildVariation[] list.
        // Sandbox-safe (verified). Returns the sorted distinct variation values, or an
        // empty list if the property is absent (caller falls back).
        private static List<int> ReadColours(ObjectDataCD od)
        {
            var result = new List<int>();
            if (PugDatabase.TryGetComponent<Pug.Properties.ObjectPropertiesCD>(od, out var props)
                && props.TryGetList(BreedVariationPropertyId,
                    out NativeArray<BreedStateCD.PossibleChildVariation> list,
                    (AllocatorManager.AllocatorHandle)Allocator.Temp))
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < list.Length; i++)
                    if (seen.Add(list[i].Variation)) result.Add(list[i].Variation);
                result.Sort();
            }
            return result;
        }

        /// <summary>True for any CattleCD-marked species (adult or baby).</summary>
        public static bool IsCattle(int objectId) => s_cattle.Contains(objectId);

        /// <summary>True for a baby form (folded into its adult — no own catalog row).</summary>
        public static bool IsBaby(int objectId) => s_babyToAdult.ContainsKey(objectId);

        /// <summary>The adult species' ObjectID for a baby; the id unchanged otherwise.</summary>
        public static int AdultOf(int objectId)
            => s_babyToAdult.TryGetValue(objectId, out var adult) ? adult : objectId;

        /// <summary>Iter-17: the species' full colour set (sorted distinct variations),
        /// or an empty list if the authored list was absent.</summary>
        public static IReadOnlyList<int> ColoursOf(int objectId)
            => s_colours.TryGetValue(objectId, out var c) ? c : System.Array.Empty<int>();
    }
}
