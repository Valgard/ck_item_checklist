using System;
using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Iter-44: everything ONE world tile remembers, in one record.
    /// <para>Until Iter-43 this lived in two parallel per-tile dictionaries
    /// (<c>_containers</c> / <c>_auxContainers</c>) that every reader, writer, pruner,
    /// serializer and parser had to keep in step BY HAND at ~10 sites — key unions in the
    /// prune and in <c>Serialize</c>, two merge loops in <c>BuildView</c>, twin accumulators +
    /// twin flush loops + a reconcile pass in <c>PossessionScanner</c>. The Iter-43 review's
    /// C-1 was the bill for that coupling: the two dimensions were given the SAME correctness
    /// predicate although they have DIFFERENT producers, so the aux half could never shrink.
    /// One record makes the two dimensions separately expressible while removing the union
    /// bookkeeping entirely.</para>
    /// <para><see cref="Contents"/> is objectID → count, written by scan paths #2
    /// (<c>AddBuffer</c>, a container's contents) and #3 (<c>AddOne</c>, the placed object
    /// itself). <see cref="Aux"/> is <c>PackKey(id, secondDim)</c> → count for the sub-variant
    /// axes (pet skins, cattle colours, paint colours). Both are owned by the ledger: a
    /// publisher's dictionaries are copied in, never adopted (see
    /// <see cref="PossessionLedger.Publish"/>).</para>
    /// </summary>
    internal sealed class TileEntry
    {
        public readonly Dictionary<int, int> Contents = new Dictionary<int, int>();
        public readonly Dictionary<long, int> Aux = new Dictionary<long, int>();

        /// <summary>Nothing remembered here any more — the ledger drops such a tile so an
        /// emptied tile does not linger as a live key that the prune can never reach.</summary>
        public bool IsEmpty => Contents.Count == 0 && Aux.Count == 0;
    }

    /// <summary>
    /// Iter-44: what one scan actually SAW on one tile — evidence, not permission.
    /// <para><strong>Why this replaces a bool.</strong> Iter-43 passed
    /// <c>allowShrink: allowPrune &amp;&amp; containerTiles.Contains(key)</c> to BOTH the
    /// contents and the aux writer. A bool cannot express WHICH evidence justified WHICH
    /// dimension, and that is not a style question but the C-1 defect: <c>containerTiles</c> is filled
    /// only for entities with a <c>ContainedObjectsBuffer</c> and no <c>CraftingCD</c>, while aux
    /// has three producers with different tile keys — pet skins from a container buffer (a
    /// container tile, fine), penned-cattle colours keyed by the pen's NEAREST ANCHOR tile (a
    /// station/workbench tile, which HAS <c>CraftingCD</c> and is therefore NEVER in
    /// <c>containerTiles</c>), and placed-paintable colours keyed by the placeable's own tile
    /// (a container tile only by coincidence). So for cattle and paint the flag was
    /// structurally always false: a pen losing its last animal of a colour, or a placeable
    /// repainted A→B, kept the stale key forever — serialized, surviving restarts, permanently
    /// inflating the Iter-36 owned counter K in violation of Iter-41's "own ≥1 right now"
    /// contract. The caller now reports observations; the ledger derives the permissions.</para>
    /// <para><strong>The rules</strong> (<see cref="MayShrinkContents"/> /
    /// <see cref="MayShrinkAux"/>): a dimension may shrink iff we are past the streaming grace
    /// AND either its own producer was observed on this tile, or the tile is DEFINITELY
    /// observable. The second disjunct is <see cref="PossessionLedger.PruneStaleNear"/>'s own
    /// premise, reused one granularity down. Inside the 48-tile + anchor-covered envelope this
    /// codebase ALREADY infers "unobserved ⇒ destroyed" — that is exactly what
    /// <c>PruneStaleNear</c> does there, and it deletes the WHOLE tile. Dropping only the
    /// unconfirmed ids of a tile is strictly smaller than that, so it adds no new risk.</para>
    /// <para><strong>Why a bare "the tile was observed at all" test would NOT be sound:</strong>
    /// it re-opens Iter-43's I4. Two entities on one tile (Iter-20's wall torch standing on a
    /// mannequin's tile) live in DIFFERENT DOTS archetype chunks — a container has a
    /// <c>ContainedObjectsBuffer</c>, a torch does not — so they leave the observed set
    /// INDEPENDENTLY, measured at ~91-115 tiles (Iter-41). Seeing only the torch at ~95 tiles
    /// must not discard the mannequin's armour. That case is 2× outside the 48-tile envelope,
    /// so <see cref="DefinitelyObservable"/> is false there and the remembered ids are kept —
    /// the protection I4 bought is preserved in full.</para>
    /// </summary>
    internal struct TileObservation
    {
        /// <summary>An entity with a <c>ContainedObjectsBuffer</c> (and no <c>CraftingCD</c>)
        /// was seen on this tile this scan, so its stored contents were confirmed.</summary>
        public bool ContainerObserved;

        /// <summary>This tile produced aux this scan (a stored pet, a penned/caged animal
        /// credited to this anchor tile, or a placed painted object).</summary>
        public bool AuxProducerObserved;

        /// <summary><c>dist(player, tile) &lt;= PruneRadius</c> AND the tile is covered by a
        /// loaded anchor — i.e. anything still standing here WOULD have been observed.</summary>
        public bool DefinitelyObservable;

        /// <summary>The caller's <c>allowPrune</c>: the world has been stably loaded long
        /// enough that "absent" means "gone" rather than "not streamed in yet".</summary>
        public bool PastGrace;

        internal bool MayShrinkContents => PastGrace && (ContainerObserved || DefinitelyObservable);

        internal bool MayShrinkAux => PastGrace && (AuxProducerObserved || DefinitelyObservable);
    }

    /// <summary>What a <see cref="PossessionLedger.Publish"/> call removed. Iter-43 reported
    /// only content units; aux removals reached NO detector at all, violating its own "count
    /// and report every deletion" rule. Both are surfaced now.</summary>
    internal struct TilePublishResult
    {
        /// <summary>Owned units that vanished from this tile's remembered contents.</summary>
        public int DroppedUnits;

        /// <summary>Remembered aux keys whose count was reduced or removed.</summary>
        public int DroppedAuxKeys;
    }

    /// <summary>
    /// Per-tile possession store keyed by world tile (x,z). Remembered tiles survive across
    /// snapshots (and are persisted); carried is transient (always live, never persisted).
    /// BuildView merges: every remembered tile contributes its contents; an item is
    /// "remembered" if it appears only in tiles NOT observed this snapshot.
    /// </summary>
    internal sealed class PossessionLedger
    {
        // Iter-44: ONE dict. Was two parallel dicts (_containers + _auxContainers) whose key
        // sets had to be unioned by hand wherever both mattered — see TileEntry's docstring.
        private readonly Dictionary<long, TileEntry> _tiles = new Dictionary<long, TileEntry>();

        private Dictionary<int, int> _carried = new Dictionary<int, int>();

        // Iter-41: the LIVE portion of the aux axes (carried inventory + the active/summoned
        // pet). Never persisted; added on top of the remembered per-tile aux in BuildView.
        private Dictionary<long, int> _auxCarried = new Dictionary<long, int>();

        /// <summary>Remembered tiles. Diagnostics/reporting only — O(1).</summary>
        public int TileCount => _tiles.Count;

        /// <summary>Remembered (tile, objectID) CONTENT pairs — the historical <c>pairs=</c>
        /// DIAG field, which counted contents only. O(tiles); keep it behind the diag gate.
        /// </summary>
        public int PairCount
        {
            get
            {
                int n = 0;
                foreach (var pair in _tiles)
                    n += pair.Value.Contents.Count;
                return n;
            }
        }

        public static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public static int KeyX(long key) => (int)(key >> 32);

        public static int KeyZ(long key) => (int)(uint)key;

        public void SetCarried(Dictionary<int, int> carried) => _carried = carried ?? new Dictionary<int, int>();

        public void SetCarriedAux(Dictionary<long, int> aux) => _auxCarried = aux ?? new Dictionary<long, int>();

        /// <summary>
        /// Publish one tile's freshly observed state. Replaces Iter-43's
        /// <c>SetLiveContainer</c> + <c>SetLiveAux</c> + <c>ClearAux</c>: one call per tile per
        /// scan, both dimensions, and the ledger — not the caller — decides what may shrink
        /// (see <see cref="TileObservation"/> for the rules and their justification).
        /// <para><strong>Semantics.</strong> No previous entry → the observation is stored as
        /// the tile's state, zero drops. Otherwise, per remembered id/aux key: a HIGHER (or
        /// equal) observed count always wins; a LOWER one — including absent, i.e. 0 — either
        /// shrinks (counted in the returned <see cref="TilePublishResult"/>) or is refused and
        /// the remembered value kept, exactly as the rule for that dimension says. Ids the scan
        /// saw for the first time are simply added. An entry that ends up
        /// <see cref="TileEntry.IsEmpty"/> is removed.</para>
        /// <para><strong>An empty observed aux dict is NOT a deletion request.</strong> It goes
        /// through the same rule as any other observation. This subsumes the removed
        /// <c>ClearAux</c> (whose job — dropping stale aux on a tile re-observed without aux —
        /// is now just "aux producer not observed, tile definitely observable ⇒ may shrink")
        /// AND the scanner's "skip empty aux dicts" workaround, which existed only because the
        /// old ungated empty write WAS a deletion.</para>
        /// <para><strong>Ownership.</strong> The caller keeps its dictionaries; this method
        /// copies values in and never stores or mutates the passed references. Iter-43's
        /// <c>SetLiveContainer</c> wrote remembered entries INTO the caller's <c>contents</c>
        /// and then adopted that same instance — an observable side effect, since the scanner
        /// re-reads its accumulators afterwards (<c>MarkFrom</c> walks <c>auxScan</c> to mark
        /// the persistent pet collection). Either dictionary may be <c>null</c>, meaning "this
        /// tile produced none of that dimension".</para>
        /// </summary>
        /// <returns>What was removed — 0/0 when nothing was. The caller surfaces both numbers;
        /// an unreported deletion is what made Iter-42 invisible for a month.</returns>
        public TilePublishResult Publish(long key, Dictionary<int, int> contents, Dictionary<long, int> aux, TileObservation obs)
        {
            var result = new TilePublishResult();
            bool haveContents = contents != null && contents.Count > 0;
            bool haveAux = aux != null && aux.Count > 0;

            if (!_tiles.TryGetValue(key, out var entry))
            {
                if (!haveContents && !haveAux)
                    return result; // nothing remembered, nothing observed → do not create an empty entry
                entry = new TileEntry();
                CopyInto(contents, entry.Contents);
                CopyInto(aux, entry.Aux);
                _tiles[key] = entry;
                return result;
            }

            result.DroppedUnits = MergeContents(entry.Contents, contents, obs.MayShrinkContents);
            result.DroppedAuxKeys = MergeAux(entry.Aux, aux, obs.MayShrinkAux);
            if (entry.IsEmpty)
                _tiles.Remove(key);
            return result;
        }

        private static void CopyInto(Dictionary<int, int> src, Dictionary<int, int> dst)
        {
            if (src == null)
                return;
            foreach (var kv in src)
                dst[kv.Key] = kv.Value;
        }

        private static void CopyInto(Dictionary<long, int> src, Dictionary<long, int> dst)
        {
            if (src == null)
                return;
            foreach (var kv in src)
                dst[kv.Key] = kv.Value;
        }

        // Merge an observation into a tile's remembered contents, in place.
        // Two passes because a Dictionary must not be structurally modified while enumerated:
        // pass 1 reads the remembered ids and decides drop-vs-restore (allocating nothing in
        // the steady state, where every observed count matches), pass 2 overlays the
        // observation, skipping the ids pass 1 chose to preserve.
        // Returns the UNITS lost — the Iter-42 detector's input.
        private static int MergeContents(Dictionary<int, int> remembered, Dictionary<int, int> observed, bool mayShrink)
        {
            int droppedUnits = 0;
            List<int> restore = null; // remembered ids the scan did not confirm → keep
            List<int> drop = null; // remembered ids a confirmed scan no longer sees at all
            foreach (var kv in remembered)
            {
                int now = 0;
                if (observed != null)
                    observed.TryGetValue(kv.Key, out now);
                if (now >= kv.Value)
                    continue; // the observation is at least as large → it wins outright
                if (mayShrink)
                {
                    droppedUnits += kv.Value - now;
                    if (now <= 0)
                        (drop ??= new List<int>()).Add(kv.Key);
                }
                else
                    (restore ??= new List<int>()).Add(kv.Key);
            }
            if (observed != null)
                foreach (var kv in observed)
                    if (restore == null || !restore.Contains(kv.Key))
                        remembered[kv.Key] = kv.Value;
            if (drop != null)
                for (int i = 0; i < drop.Count; i++)
                    remembered.Remove(drop[i]);
            return droppedUnits;
        }

        // Same shape as MergeContents, on the aux axis.
        // Iter-44 fixes the asymmetry the Iter-43 review flagged: `SetLiveAux` restored only
        // keys that were ABSENT from the observation, so a colour going 3→0 restored the stale
        // 3 while 3→1 recorded 1 — the same evidence, two different answers. The per-key COUNT
        // comparison below is the contents rule, applied identically.
        // Returns the number of aux KEYS reduced or removed (units are meaningless here: an aux
        // count is "how many of this skin/colour", so the key is the thing that can go stale).
        private static int MergeAux(Dictionary<long, int> remembered, Dictionary<long, int> observed, bool mayShrink)
        {
            int droppedKeys = 0;
            List<long> restore = null;
            List<long> drop = null;
            foreach (var kv in remembered)
            {
                int now = 0;
                if (observed != null)
                    observed.TryGetValue(kv.Key, out now);
                if (now >= kv.Value)
                    continue;
                if (mayShrink)
                {
                    droppedKeys++;
                    if (now <= 0)
                        (drop ??= new List<long>()).Add(kv.Key);
                }
                else
                    (restore ??= new List<long>()).Add(kv.Key);
            }
            if (observed != null)
                foreach (var kv in observed)
                    if (restore == null || !restore.Contains(kv.Key))
                        remembered[kv.Key] = kv.Value;
            if (drop != null)
                for (int i = 0; i < drop.Count; i++)
                    remembered.Remove(drop[i]);
            return droppedKeys;
        }

        // Iter-42: the Iter-28 `WorldNaturePruned` flag + `PruneByPredicate(Func<int,bool>)`
        // one-time world-nature eviction lived here and were REMOVED — an id-predicate sweep over
        // the ledger cannot distinguish a placed wild object from the same id legitimately STORED
        // in a chest (both are plain entries in the same per-tile dict), so it deleted real
        // possession on every load (the flag was never serialized, so "one-time" never held).
        // Rationale + the measured damage: see the note at the top of `PossessionScanner.Scan`.

        /// <summary>Self-heal: drop a remembered tile that WOULD be counted this scan if a
        /// container were still there — i.e. it is DEFINITELY loaded (within <paramref name="radius"/>
        /// of the player) AND anchor-covered (<paramref name="coveredByLoadedAnchor"/>: a loaded
        /// workbench anchor covers it, the same WithinAnchor gate the scan uses) — yet nothing was
        /// observed there ⇒ destroyed/emptied. BOTH halves are required (Iter-41): the small player
        /// <paramref name="radius"/> guarantees the chunk is loaded — distance beyond it is not
        /// enough because a container's chunk can unload while a co-located workbench stays (mode 2,
        /// what wrecked the old 180) — and the anchor cover guarantees a present container would
        /// have passed the scan's WithinAnchor gate — a base container can be player-near yet lose
        /// cover when its workbench just crossed the ~91 observation dropout (mode 1). A real
        /// destruction is always both (you stand next to the container; its workbench is
        /// co-located), so nothing legitimate is missed. Collect-then-remove to avoid mutating
        /// during iteration.
        /// <para>Iter-44: the hand-built union of the two per-tile dicts' key sets is gone — one
        /// dict means aux-only tiles (penned cattle at an anchor tile) are covered by construction.
        /// </para></summary>
        /// <returns>Iter-43: how many tiles were dropped, so the caller can surface it — this
        /// deletion used to be entirely unreported, even under diagnostics.</returns>
        public int PruneStaleNear(float px, float pz, float radius, HashSet<long> liveKeys, Func<long, bool> coveredByLoadedAnchor)
        {
            float r2 = radius * radius;
            List<long> drop = null;
            foreach (var pair in _tiles)
            {
                long key = pair.Key;
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
                _tiles.Remove(k);
            return drop.Count;
        }

        public PossessionView BuildView(HashSet<long> liveKeys)
        {
            var totals = new Dictionary<int, int>(_carried); // carried first (always live)
            var liveItems = new HashSet<int>(_carried.Keys);
            var anyItem = new HashSet<int>(_carried.Keys);

            // Iter-41: aux = live carried/active + all remembered per-tile aux (same merge as
            // totals). A base-stored/penned/painted entity whose tile is not loaded this
            // snapshot keeps its last-seen aux count → stable while away.
            var aux = new Dictionary<long, int>(_auxCarried);

            foreach (var pair in _tiles)
            {
                bool live = liveKeys.Contains(pair.Key);
                foreach (var kv in pair.Value.Contents)
                {
                    totals[kv.Key] = (totals.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value;
                    anyItem.Add(kv.Key);
                    if (live)
                        liveItems.Add(kv.Key);
                }
                foreach (var kv in pair.Value.Aux)
                    aux[kv.Key] = (aux.TryGetValue(kv.Key, out var a) ? a : 0) + kv.Value;
            }

            // Remembered = present somewhere but not in any live source. Kept available
            // for callers even though the current UI does not surface it.
            var remembered = new HashSet<int>();
            foreach (var id in anyItem)
                if (!liveItems.Contains(id))
                    remembered.Add(id);

            return new PossessionView(totals, remembered, aux);
        }

        // --- Iter-40: reverse-index (location surfacing) ---
        // The objectId→count collapse in BuildView throws away location; these read
        // the same remembered tiles the other way. Tiles are packed long keys
        // (decode with KeyX/KeyZ) — NOT ValueTuple, which is unproven sandbox surface.
        // Remembered (currently-unloaded) tiles are included: an unloaded chunk is
        // frozen in SP, so a remembered tile is the true last state (Iter-41). Carried
        // is tile-less and intentionally absent.

        /// <summary>Every container tile currently holding <paramref name="objectId"/>
        /// (count >= 1), as packed (x,z) keys. Empty when nothing is stored.</summary>
        public List<long> TilesHolding(int objectId)
        {
            var keys = new List<long>();
            foreach (var pair in _tiles)
                if (pair.Value.Contents.TryGetValue(objectId, out var c) && c >= 1)
                    keys.Add(pair.Key);
            return keys;
        }

        /// <summary>How many container tiles hold <paramref name="objectId"/> — the
        /// allocation-free count used by the trackable gate and the tooltip hint.</summary>
        public int CountTilesHolding(int objectId)
        {
            int n = 0;
            foreach (var pair in _tiles)
                if (pair.Value.Contents.TryGetValue(objectId, out var c) && c >= 1)
                    n++;
            return n;
        }

        // --- Persistence (remembered tiles only; carried / live-aux never persisted) ---
        // v3 line format: "x,z|<id:count,...>|<packedKey:count,...>" — segment 1 = the tile's
        // contents (id->count), segment 2 = its aux breakdown (PackKey(id, secondDim)->count:
        // pet skins, cattle/paint colours). Either segment may be empty. Exactly two '|' per
        // data line.
        // Iter-44: NO schema bump. A v3 line already IS a TileEntry (two segments, one per
        // dimension), so folding the two in-memory dicts into one record is a pure in-memory
        // refactor — same bytes out, same bytes in, no migration, no player re-scan.

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
            foreach (var pair in _tiles)
            {
                var entry = pair.Value;
                if (entry.IsEmpty)
                    continue;
                var cPart = new List<string>();
                foreach (var kv in entry.Contents)
                    cPart.Add(kv.Key + ":" + kv.Value);
                var aPart = new List<string>();
                foreach (var kv in entry.Aux)
                    aPart.Add(kv.Key + ":" + kv.Value);
                // Two '|': contents segment | aux segment (either may be empty).
                lines.Add(KeyX(pair.Key) + "," + KeyZ(pair.Key) + "|" + string.Join(",", cPart) + "|" + string.Join(",", aPart));
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
            _tiles.Clear();
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
                var entry = new TileEntry();
                foreach (var pair in seg[1].Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    if (int.TryParse(pair.Substring(0, colon), out int id) && int.TryParse(pair.Substring(colon + 1), out int cnt) && cnt >= 1)
                        entry.Contents[id] = cnt;
                }
                foreach (var pair in seg[2].Split(','))
                {
                    int colon = pair.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    if (long.TryParse(pair.Substring(0, colon), out long pk) && int.TryParse(pair.Substring(colon + 1), out int cnt) && cnt >= 1)
                        entry.Aux[pk] = cnt;
                }
                if (!entry.IsEmpty)
                    _tiles[key] = entry;
            }
            return _tiles.Count;
        }
    }
}
