using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>Mod-owned, persistent "ever-owned" set of pet skins, keyed by
    /// PackKey(petObjectID, skinIndex). CK does not track per-skin discovery (it
    /// force-zeroes pet variation in SetObjectAsDiscovered), so collection is derived
    /// from ownership: once a skin has been observed in the player's possession it
    /// stays collected. Persisted per character GUID via PetCollectionStore.</summary>
    internal sealed class PetCollection
    {
        private readonly HashSet<long> _collected = new HashSet<long>();
        public bool Dirty { get; private set; }
        public void ClearDirty() => Dirty = false;

        public bool IsCollected(int objectId, int skinIndex)
            => _collected.Contains(DiscoveredState.PackKey(objectId, skinIndex));

        /// <summary>Returns true if this skin is newly collected (sets Dirty).</summary>
        public bool MarkCollected(int objectId, int skinIndex)
        {
            if (_collected.Add(DiscoveredState.PackKey(objectId, skinIndex)))
            { Dirty = true; return true; }
            return false;
        }

        // Format: one "objectId:skinIndex" per line (ASCII).
        public string Serialize()
        {
            var lines = new List<string>(_collected.Count);
            foreach (var key in _collected)
                lines.Add(DiscoveredState.KeyObjectId(key) + ":" + DiscoveredState.KeyVariation(key));
            return string.Join("\n", lines);
        }

        public void LoadFrom(string text)
        {
            _collected.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(line.Substring(0, colon), out int id)
                    && int.TryParse(line.Substring(colon + 1), out int skin))
                    _collected.Add(DiscoveredState.PackKey(id, skin));
            }
            Dirty = false;
        }
    }
}
