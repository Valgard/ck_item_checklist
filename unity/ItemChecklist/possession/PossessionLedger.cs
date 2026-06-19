using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Per-container possession store keyed by world tile (x,z). Storage containers
    /// are remembered across snapshots (and persisted); carried is transient (always
    /// live, never persisted). BuildView merges: for each container use its current
    /// contents; an item is "remembered" if it appears only in containers NOT loaded
    /// this snapshot.
    /// </summary>
    internal sealed class PossessionLedger
    {
        private readonly Dictionary<long, Dictionary<int, int>> _containers =
            new Dictionary<long, Dictionary<int, int>>();
        private Dictionary<int, int> _carried = new Dictionary<int, int>();

        public IReadOnlyDictionary<long, Dictionary<int, int>> Containers => _containers;

        public static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        public static int KeyX(long key) => (int)(key >> 32);
        public static int KeyZ(long key) => (int)(uint)key;

        public void SetCarried(Dictionary<int, int> carried) => _carried = carried ?? new Dictionary<int, int>();

        public void SetLiveContainer(long key, Dictionary<int, int> contents) => _containers[key] = contents;

        /// <summary>Self-healing: drop remembered containers that SHOULD be loaded
        /// (within `radius` of the player, i.e. inside the load bubble) yet were not
        /// observed this snapshot — they were destroyed, emptied away, or lost their
        /// anchor. Collect-then-remove to avoid mutating during iteration.</summary>
        public void PruneStaleNear(float px, float pz, float radius, HashSet<long> liveKeys)
        {
            float r2 = radius * radius;
            List<long> drop = null;
            foreach (var key in _containers.Keys)
            {
                if (liveKeys.Contains(key)) continue;
                float dx = KeyX(key) - px, dz = KeyZ(key) - pz;
                if (dx * dx + dz * dz <= r2) { if (drop == null) drop = new List<long>(); drop.Add(key); }
            }
            if (drop != null) foreach (var k in drop) _containers.Remove(k);
        }

        public PossessionView BuildView(HashSet<long> liveKeys)
        {
            var totals = new Dictionary<int, int>(_carried);   // carried first (always live)
            var liveItems = new HashSet<int>(_carried.Keys);
            var anyItem = new HashSet<int>(_carried.Keys);

            foreach (var pair in _containers)
            {
                bool live = liveKeys.Contains(pair.Key);
                foreach (var kv in pair.Value)
                {
                    totals[kv.Key] = (totals.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value;
                    anyItem.Add(kv.Key);
                    if (live) liveItems.Add(kv.Key);
                }
            }

            // Remembered = present somewhere but not in any live source.
            var remembered = new HashSet<int>();
            foreach (var id in anyItem)
                if (!liveItems.Contains(id)) remembered.Add(id);

            return new PossessionView(totals, remembered);
        }

        // --- Persistence (storage containers only; carried is never persisted) ---
        // Format: one line per container "x,z|id:count,id:count".

        public string Serialize()
        {
            var lines = new List<string>();
            foreach (var pair in _containers)
            {
                if (pair.Value.Count == 0) continue;
                var sb = new List<string>(pair.Value.Count);
                foreach (var kv in pair.Value) sb.Add(kv.Key + ":" + kv.Value);
                lines.Add(KeyX(pair.Key) + "," + KeyZ(pair.Key) + "|" + string.Join(",", sb));
            }
            return string.Join("\n", lines);
        }

        public void LoadFrom(string text)
        {
            _containers.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                var xz = line.Substring(0, bar).Split(',');
                if (xz.Length != 2 || !int.TryParse(xz[0], out int x) || !int.TryParse(xz[1], out int z)) continue;
                var contents = new Dictionary<int, int>();
                foreach (var pair in line.Substring(bar + 1).Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon <= 0) continue;
                    if (int.TryParse(pair.Substring(0, colon), out int id)
                        && int.TryParse(pair.Substring(colon + 1), out int cnt))
                        contents[id] = cnt;
                }
                if (contents.Count > 0) _containers[Key(x, z)] = contents;
            }
        }
    }
}
