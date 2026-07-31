// Minimal stand-ins so PossessionLedger.cs / PetCollection.cs compile outside Unity.
namespace UnityEngine
{
    internal static class Debug
    {
        public static void LogWarning(string s) => System.Console.WriteLine("[warn] " + s);
    }

    internal struct Vector2
    {
        public float x,
            y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
}

namespace ItemChecklist
{
    // Verified against unity/ItemChecklist/DiscoveredState.cs:43-48.
    internal static class DiscoveredState
    {
        public static long PackKey(int objectId, int variation) => ((long)objectId << 32) | (uint)variation;

        public static int KeyObjectId(long key) => (int)(key >> 32);

        public static int KeyVariation(long key) => (int)(uint)key;
    }
}

namespace ItemChecklist.Possession
{
    internal sealed class PossessionView
    {
        public readonly System.Collections.Generic.Dictionary<int, int> Totals;
        public readonly System.Collections.Generic.HashSet<int> Remembered;
        public readonly System.Collections.Generic.Dictionary<long, int> Aux;

        public PossessionView(
            System.Collections.Generic.Dictionary<int, int> totals,
            System.Collections.Generic.HashSet<int> remembered,
            System.Collections.Generic.Dictionary<long, int> aux
        )
        {
            Totals = totals;
            Remembered = remembered;
            Aux = aux;
        }
    }
}
