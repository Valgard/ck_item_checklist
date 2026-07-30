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

        /// <summary>Iter-44: a header line carrying the entry count.
        /// <para>This store's lines are ~8 bytes, delimiter-free and interchangeable
        /// (<c>id:skin</c>), so a file truncated exactly at a line boundary parses as a perfectly
        /// valid SHORTER file — roughly a 1-in-8 chance per truncation, on the one store that has
        /// no second source to rebuild from. A declared count is the only cheap way to see it. A
        /// file WITHOUT the header is a pre-Iter-44 file and is accepted unchecked, so no
        /// migration and no lost collection; it gains the header on the next save.</para></summary>
        internal const string Header = "#icl-petskins-v1";

        // Format: header, then one "objectId:skinIndex" per line (ASCII).
        public string Serialize()
        {
            var lines = new List<string>(_collected.Count + 1) { Header + " n=" + _collected.Count };
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
        /// <returns>How much the file failed to account for: unparseable lines, plus one if a
        /// declared entry count does not match what was parsed. Any value > 0 means the file is
        /// damaged, and the caller must treat the result as a FAILED load.</returns>
        public int LoadFrom(string text)
        {
            _collected.Clear();
            Dirty = false;
            if (string.IsNullOrEmpty(text))
                return 0;
            int skipped = 0;
            int declared = -1; // -1 = no header ⇒ a pre-Iter-44 file, nothing to check against
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue; // a trailing newline is not damage
                if (line[0] == '#')
                {
                    int at = line.IndexOf("n=");
                    if (at >= 0 && int.TryParse(line.Substring(at + 2), out int d))
                        declared = d;
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon > 0 && int.TryParse(line.Substring(0, colon), out int id) && int.TryParse(line.Substring(colon + 1), out int skin))
                    _collected.Add(DiscoveredState.PackKey(id, skin));
                else
                    skipped++;
            }
            // The boundary-truncation detector. Without it a file cut cleanly between two lines is
            // indistinguishable from a shorter one, and every skin past the cut is gone silently.
            if (declared >= 0 && declared != _collected.Count)
                skipped++;
            return skipped;
        }
    }
}
