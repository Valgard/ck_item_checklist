using System;
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
        private readonly Dictionary<long, Dictionary<int, int>> _containers = new Dictionary<long, Dictionary<int, int>>();
        private Dictionary<int, int> _carried = new Dictionary<int, int>();

        public IReadOnlyDictionary<long, Dictionary<int, int>> Containers => _containers;

        public static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public static int KeyX(long key) => (int)(key >> 32);

        public static int KeyZ(long key) => (int)(uint)key;

        public void SetCarried(Dictionary<int, int> carried) => _carried = carried ?? new Dictionary<int, int>();

        // Iter-41: parallel per-tile REMEMBERED aux breakdown (PackKey(id, secondDim) → count):
        // pet skins in stored/carried inventories, penned/caged cattle colours, placed
        // paintable-furniture colours. Same remember+prune model as _containers; _auxCarried
        // is the live (carried + active pet) portion, never persisted.
        private readonly Dictionary<long, Dictionary<long, int>> _auxContainers = new Dictionary<long, Dictionary<long, int>>();
        private Dictionary<long, int> _auxCarried = new Dictionary<long, int>();

        public void SetCarriedAux(Dictionary<long, int> aux) => _auxCarried = aux ?? new Dictionary<long, int>();

        /// <summary>Publish a tile's freshly observed aux. <paramref name="allowShrink"/> false
        /// keeps remembered keys the caller did not re-observe (Iter-43; see
        /// <see cref="SetLiveContainer"/> for the full reasoning — same hazard, same rule).</summary>
        public void SetLiveAux(long key, Dictionary<long, int> aux, bool allowShrink)
        {
            if (!allowShrink && _auxContainers.TryGetValue(key, out var prevAux))
                foreach (var kv in prevAux)
                    if (!aux.ContainsKey(kv.Key))
                        aux[kv.Key] = kv.Value;
            _auxContainers[key] = aux;
        }

        // Iter-41: drop a live tile's remembered aux when it was re-observed this scan WITHOUT
        // aux (a mobile cattle moved off a tile still kept live by a co-located chest/placeable).
        public void ClearAux(long key) => _auxContainers.Remove(key);

        /// <summary>Publish a tile's freshly observed contents.
        /// <para><strong>Iter-43 — this used to be a bare `_containers[key] = contents`, i.e. an
        /// unconditional, ungated DELETE of whatever the previous scan knew.</strong> Two producers
        /// write a tile's dict for DIFFERENT entities: `AddOne` (the placed object) and `AddBuffer`
        /// (a container's contents). A container (has `ContainedObjectsBuffer`) and a torch (does
        /// not) necessarily sit in different DOTS archetype chunks, so per Iter-41 they leave the
        /// observed set INDEPENDENTLY (~91-115 tiles). Iter-20 documents this exact co-location — a
        /// wall torch on a mannequin's tile. So at ~95 tiles the scan could see only the torch,
        /// build `{torch:1}`, and silently discard the mannequin's four armour pieces; the tile is
        /// in `liveKeys`, so `PruneStaleNear` skips it (and would refuse past 48 anyway). Same loss
        /// shape as Iter-42, no predicate involved, and — unlike `PruneStaleNear`'s three
        /// conditions + `allowPrune` — with zero conditions and no gate.</para>
        /// <para><paramref name="allowShrink"/> is the fix, and it is deliberately NOT just
        /// "past the streaming grace": grace-only merging would fix loading far from base but not
        /// the walk-away case above, which happens in normal play. The caller passes true only when
        /// this tile's contents were actually CONFIRMED this scan — i.e. a container entity was
        /// observed here AND the world is past the grace. Otherwise remembered ids the caller did
        /// not re-observe are kept (a transient over-count — the direction this codebase has
        /// repeatedly chosen, cf. Iter-41's ClearAux gate and Iter-42).</para>
        /// <para>Known residue: two containers sharing one tile, only one observed, shrinks the
        /// unobserved one. Retiring that needs real provenance in the stored record (a per-tile
        /// split of placed-object vs container-sourced counts), which is a schema change.</para>
        /// </summary>
        /// <returns>Units that vanished from this tile's remembered contents — 0 whenever nothing
        /// was dropped. The caller surfaces the total; that number is the Iter-42 detector.</returns>
        public int SetLiveContainer(long key, Dictionary<int, int> contents, bool allowShrink)
        {
            if (!_containers.TryGetValue(key, out var prev))
            {
                _containers[key] = contents;
                return 0;
            }
            int dropped = 0;
            foreach (var kv in prev)
            {
                contents.TryGetValue(kv.Key, out var now);
                if (now >= kv.Value)
                    continue;
                if (allowShrink)
                    dropped += kv.Value - now;
                else
                    contents[kv.Key] = kv.Value; // unconfirmed → keep what we remembered
            }
            _containers[key] = contents;
            return dropped;
        }

        // Iter-42: the Iter-28 `WorldNaturePruned` flag + `PruneByPredicate(Func<int,bool>)`
        // one-time world-nature eviction lived here and were REMOVED — an id-predicate sweep over
        // the ledger cannot distinguish a placed wild object from the same id legitimately STORED
        // in a chest (both are plain entries in the same per-tile dict), so it deleted real
        // possession on every load (the flag was never serialized, so "one-time" never held).
        // Rationale + the measured damage: see the note at the top of `PossessionScanner.Scan`.

        /// <summary>Self-heal: drop a remembered container/aux tile that WOULD be counted this scan
        /// if a container were still there — i.e. it is DEFINITELY loaded (within `radius` of the
        /// player) AND anchor-covered (`coveredByLoadedAnchor`: a loaded workbench anchor covers it,
        /// the same WithinAnchor gate the scan uses) — yet nothing was observed there ⇒ destroyed/
        /// emptied. BOTH halves are required (Iter-41): the small player `radius` guarantees the
        /// chunk is loaded — distance beyond it is not enough because a container's chunk can unload
        /// while a co-located workbench stays (mode 2, what wrecked the old 180) — and the anchor
        /// cover guarantees a present container would have passed the scan's WithinAnchor gate — a
        /// base container can be player-near yet lose cover when its workbench just crossed the ~91
        /// observation dropout (mode 1). A real destruction is always both (you stand next to the
        /// container; its workbench is co-located), so nothing legitimate is missed. Collect-then-
        /// remove to avoid mutating during iteration.</summary>
        /// <returns>Iter-43: how many tiles were dropped, so the caller can surface it — this
        /// deletion used to be entirely unreported, even under diagnostics.</returns>
        public int PruneStaleNear(float px, float pz, float radius, HashSet<long> liveKeys, Func<long, bool> coveredByLoadedAnchor)
        {
            float r2 = radius * radius;
            List<long> drop = null;
            var keys = new HashSet<long>(_containers.Keys);
            keys.UnionWith(_auxContainers.Keys); // Iter-41: aux-only tiles (penned cattle) too
            foreach (var key in keys)
            {
                if (liveKeys.Contains(key))
                    continue;
                float dx = KeyX(key) - px,
                    dz = KeyZ(key) - pz;
                if (dx * dx + dz * dz > r2)
                    continue; // not player-near → chunk not guaranteed loaded → keep
                if (!coveredByLoadedAnchor(key))
                    continue; // loaded but no loaded anchor would observe it → not destroyed → keep
                (drop ??= new List<long>()).Add(key);
            }
            if (drop == null)
                return 0;
            foreach (var k in drop)
            {
                _containers.Remove(k);
                _auxContainers.Remove(k);
            }
            return drop.Count;
        }

        public PossessionView BuildView(HashSet<long> liveKeys)
        {
            var totals = new Dictionary<int, int>(_carried); // carried first (always live)
            var liveItems = new HashSet<int>(_carried.Keys);
            var anyItem = new HashSet<int>(_carried.Keys);

            foreach (var pair in _containers)
            {
                bool live = liveKeys.Contains(pair.Key);
                foreach (var kv in pair.Value)
                {
                    totals[kv.Key] = (totals.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value;
                    anyItem.Add(kv.Key);
                    if (live)
                        liveItems.Add(kv.Key);
                }
            }

            // Remembered = present somewhere but not in any live source. Kept available
            // for callers even though the current UI does not surface it.
            var remembered = new HashSet<int>();
            foreach (var id in anyItem)
                if (!liveItems.Contains(id))
                    remembered.Add(id);

            // Iter-41: aux = live carried/active + all remembered aux containers (same merge
            // as totals). A base-stored/penned/painted entity whose tile is not loaded this
            // snapshot keeps its last-seen aux count → stable while away.
            var aux = new Dictionary<long, int>(_auxCarried);
            foreach (var pair in _auxContainers)
            foreach (var kv in pair.Value)
                aux[kv.Key] = (aux.TryGetValue(kv.Key, out var a) ? a : 0) + kv.Value;

            return new PossessionView(totals, remembered, aux);
        }

        // --- Iter-40: reverse-index (location surfacing) ---
        // The objectId→count collapse in BuildView throws away location; these read
        // the same remembered _containers the other way. Tiles are packed long keys
        // (decode with KeyX/KeyZ) — NOT ValueTuple, which is unproven sandbox surface.
        // Remembered (currently-unloaded) tiles are included: an unloaded chunk is
        // frozen in SP, so a remembered tile is the true last state (Iter-41). Carried
        // is tile-less and intentionally absent.

        /// <summary>Every container tile currently holding <paramref name="objectId"/>
        /// (count >= 1), as packed (x,z) keys. Empty when nothing is stored.</summary>
        public List<long> TilesHolding(int objectId)
        {
            var keys = new List<long>();
            foreach (var pair in _containers)
                if (pair.Value.TryGetValue(objectId, out var c) && c >= 1)
                    keys.Add(pair.Key);
            return keys;
        }

        /// <summary>How many container tiles hold <paramref name="objectId"/> — the
        /// allocation-free count used by the trackable gate and the tooltip hint.</summary>
        public int CountTilesHolding(int objectId)
        {
            int n = 0;
            foreach (var pair in _containers)
                if (pair.Value.TryGetValue(objectId, out var c) && c >= 1)
                    n++;
            return n;
        }

        // --- Persistence (remembered storage + aux only; carried / live-aux never persisted) ---
        // v3 line format: "x,z|<id:count,...>|<packedKey:count,...>" — segment 1 = container
        // contents (id->count), segment 2 = the per-tile aux breakdown
        // (PackKey(id, secondDim)->count: pet skins, cattle/paint colours). Either segment may
        // be empty. Exactly two '|' per data line.

        // Iter-31: ledgers written before the workbench-anchor fix are polluted with remote
        // world-structure loot (camps/ruins anchored by their campfires/seed-extractors were
        // counted as bases). A version marker on line 1 lets LoadFrom discard any pre-fix file
        // exactly once; the base then re-scans and repopulates cleanly. The marker has no '|'
        // so the per-line parser skips it like any non-data line.
        // internal (Iter-43): PossessionStore names it when reporting a discard, so the expected
        // marker in the incident record cannot drift from the one actually enforced here.
        internal const string VersionMarker = "#icl-ledger-v3";

        public string Serialize()
        {
            var lines = new List<string> { VersionMarker };
            var keys = new HashSet<long>(_containers.Keys);
            keys.UnionWith(_auxContainers.Keys);
            foreach (var key in keys)
            {
                _containers.TryGetValue(key, out var cont);
                _auxContainers.TryGetValue(key, out var aux);
                bool hasC = cont != null && cont.Count > 0;
                bool hasA = aux != null && aux.Count > 0;
                if (!hasC && !hasA)
                    continue;
                var cPart = new List<string>();
                if (hasC)
                    foreach (var kv in cont)
                        cPart.Add(kv.Key + ":" + kv.Value);
                var aPart = new List<string>();
                if (hasA)
                    foreach (var kv in aux)
                        aPart.Add(kv.Key + ":" + kv.Value);
                // Two '|': container segment | aux segment (either may be empty).
                lines.Add(KeyX(key) + "," + KeyZ(key) + "|" + string.Join(",", cPart) + "|" + string.Join(",", aPart));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Parse a serialized ledger, replacing everything currently held.
        /// <para>Iter-43 made the outcome reportable. Discarding a whole file is a total
        /// data-loss event, and it used to happen with NO log while <c>Load</c> reported success
        /// — so a truncated or corrupted file (Wine, power loss, disk full) was indistinguishable
        /// from a legitimate version migration, and looked byte-for-byte like the Iter-42 symptom.
        /// The caller logs and records what happened; only the count can tell it apart.</para>
        /// </summary>
        /// <returns>Tiles parsed, or <c>-1</c> when the version marker did not match and the
        /// whole file was therefore discarded.</returns>
        public int LoadFrom(string text)
        {
            _containers.Clear();
            _auxContainers.Clear();
            if (string.IsNullOrEmpty(text))
                return 0;
            // Compare the FIRST LINE exactly, not a prefix: `StartsWith("#icl-ledger-v3")` would
            // also accept a future "#icl-ledger-v30" and then parse it under the wrong schema.
            int nl = text.IndexOf('\n');
            string firstLine = (nl < 0 ? text : text.Substring(0, nl)).Trim();
            if (firstLine != VersionMarker)
                return -1; // discard (pre-v3 migration, or a corrupt file — the caller reports it)
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var seg = line.Split('|');
                if (seg.Length != 3)
                    continue; // v3 line has exactly two '|'
                var xz = seg[0].Split(',');
                if (xz.Length != 2 || !int.TryParse(xz[0], out int x) || !int.TryParse(xz[1], out int z))
                    continue;
                long key = Key(x, z);

                // Iter-43: reject non-positive counts at the PARSE boundary. A hand-edited or
                // truncated file could carry `id:0` / `id:-5`, which BuildView would have summed
                // as-is while the Iter-40 reverse index defensively filtered `>= 1` — two readers
                // disagreeing about what "present" means. Enforcing it here makes them agree.
                var cont = new Dictionary<int, int>();
                foreach (var pair in seg[1].Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    if (int.TryParse(pair.Substring(0, colon), out int id) && int.TryParse(pair.Substring(colon + 1), out int cnt) && cnt >= 1)
                        cont[id] = cnt;
                }
                if (cont.Count > 0)
                    _containers[key] = cont;

                var aux = new Dictionary<long, int>();
                foreach (var pair in seg[2].Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    if (long.TryParse(pair.Substring(0, colon), out long pk) && int.TryParse(pair.Substring(colon + 1), out int cnt) && cnt >= 1)
                        aux[pk] = cnt;
                }
                if (aux.Count > 0)
                    _auxContainers[key] = aux;
            }
            return _containers.Count;
        }
    }
}
