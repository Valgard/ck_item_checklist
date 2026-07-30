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

        public bool IsCollected(int objectId, int skinIndex) => _collected.Contains(DiscoveredState.PackKey(objectId, skinIndex));

        /// <summary>Returns true if this skin is newly collected (sets Dirty).</summary>
        public bool MarkCollected(int objectId, int skinIndex)
        {
            if (_collected.Add(DiscoveredState.PackKey(objectId, skinIndex)))
            {
                Dirty = true;
                return true;
            }
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

        /// <summary>Parse a serialized collection, replacing everything currently held.
        /// <para>Iter-44 (review C-3): this parser cannot throw on damaged input — it skipped
        /// every unparseable line silently, so a file truncated mid-write parsed into a SUBSET
        /// and the caller reported success. The store then stayed writable and the next autosave
        /// persisted the subset over the intact file; the one after that took the
        /// <c>.pugbackup</c> too. On an ever-owned set with no second source that is
        /// unrecoverable. Truncation almost always leaves exactly one malformed line, so
        /// counting the skips is a near-free detector.</para></summary>
        /// <returns>How many non-empty lines could NOT be parsed. Any value > 0 means the file
        /// is damaged, and the caller must treat the result as a FAILED load.</returns>
        public int LoadFrom(string text)
        {
            _collected.Clear();
            Dirty = false;
            if (string.IsNullOrEmpty(text))
                return 0;
            int skipped = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue; // a trailing newline is not damage
                int colon = line.IndexOf(':');
                if (colon > 0 && int.TryParse(line.Substring(0, colon), out int id) && int.TryParse(line.Substring(colon + 1), out int skin))
                    _collected.Add(DiscoveredState.PackKey(id, skin));
                else
                    skipped++;
            }
            return skipped;
        }
    }
}
