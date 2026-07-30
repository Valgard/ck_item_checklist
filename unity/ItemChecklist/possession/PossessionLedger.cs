using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Iter-44: everything ONE world tile remembers, in one record — and the only writer of it.
    /// <para>From Iter-41 through Iter-43 this lived in two parallel per-tile dictionaries
    /// (<c>_containers</c> / <c>_auxContainers</c>) that every reader, writer, pruner,
    /// serializer and parser had to keep in step BY HAND at ~10 sites — key unions in the
    /// prune and in <c>Serialize</c>, two merge loops in <c>BuildView</c>, twin accumulators +
    /// twin flush loops + a reconcile pass in <c>PossessionScanner</c>. Iter-43 did not just
    /// keep that coupling, it deepened it: its C-1 defect was giving both dimensions the SAME
    /// correctness predicate although they have DIFFERENT producers, so the cattle/paint aux
    /// could never shrink at all. One record makes the two dimensions separately expressible
    /// while removing the union bookkeeping entirely.</para>
    /// <para><c>Contents</c> is objectID → count, written by scan paths #2 (<c>AddBuffer</c>, a
    /// container's contents) and #3 (<c>AddOne</c>, the placed object itself). <c>Aux</c> is
    /// <c>PackKey(id, secondDim)</c> → count for the sub-variant axes (pet skins, cattle
    /// colours, paint colours).</para>
    /// <para><strong>Both dictionaries are private and there is no read accessor for them.</strong>
    /// The reads every consumer actually needs are methods here instead. Two reasons, and the
    /// second is the one that bites: it makes the WRITERS enumerable — four methods, each
    /// enforcing "a remembered count is always ≥ 1", where Iter-43 rested that invariant on two
    /// independent arguments; and an <c>IReadOnlyDictionary</c> accessor (tried, reverted) makes
    /// every <c>foreach</c> box a heap enumerator, which at the measured 504-681 remembered tiles
    /// is ~1,400 allocations per scan in <c>BuildView</c> alone — in the one subsystem that has
    /// spent three iterations (27/28/31) on main-thread cost.</para>
    /// </summary>
    internal sealed class TileEntry
    {
        private readonly Dictionary<int, int> _contents = new Dictionary<int, int>();
        private readonly Dictionary<long, int> _aux = new Dictionary<long, int>();

        // Consecutive scans in which a removal was authorized by INFERENCE (the scan would have
        // seen this tile) but not CONFIRMED (no container observed here), and something was in
        // fact missing. A removal on that basis waits for the second one — see
        // PossessionLedger.ApplyScan's "one miss is not evidence" note.
        private int _contentsSoftMisses;
        private int _auxSoftMisses;

        /// <summary>Nothing remembered here any more — the ledger drops such a tile, so an emptied
        /// tile does not linger as a remembered one that the prune skips for as long as something
        /// on it keeps being observed.</summary>
        public bool IsEmpty => _contents.Count == 0 && _aux.Count == 0;

        /// <summary>Remembered (tile, objectID) content pairs on this tile — the <c>cPairs=</c>
        /// DIAG figure.</summary>
        public int ContentPairCount => _contents.Count;

        // --- reads (loops live here so they run against the concrete Dictionary) ---

        /// <summary>Add this tile's contents to a view under construction.</summary>
        public void AccumulateInto(Dictionary<int, int> totals, HashSet<int> anyItem, HashSet<int> liveItems, bool live)
        {
            foreach (var kv in _contents)
            {
                totals[kv.Key] = (totals.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value;
                anyItem.Add(kv.Key);
                if (live)
                    liveItems.Add(kv.Key);
            }
        }

        /// <summary>Add this tile's sub-variant counts to a view under construction.</summary>
        public void AccumulateAuxInto(Dictionary<long, int> aux)
        {
            foreach (var kv in _aux)
                aux[kv.Key] = (aux.TryGetValue(kv.Key, out var a) ? a : 0) + kv.Value;
        }

        /// <summary>Does this tile hold at least one <paramref name="objectId"/>? (Iter-40's
        /// reverse index.)</summary>
        public bool Holds(int objectId) => _contents.TryGetValue(objectId, out var c) && c >= 1;

        /// <summary>Append this tile's v3 line: <c>x,z|&lt;id:count,…&gt;|&lt;packedKey:count,…&gt;</c>,
        /// either segment possibly empty, exactly two '|'.</summary>
        public void AppendTo(List<string> lines, int x, int z)
        {
            var cPart = new List<string>(_contents.Count);
            foreach (var kv in _contents)
                cPart.Add(kv.Key + ":" + kv.Value);
            var aPart = new List<string>(_aux.Count);
            foreach (var kv in _aux)
                aPart.Add(kv.Key + ":" + kv.Value);
            lines.Add(x + "," + z + "|" + string.Join(",", cPart) + "|" + string.Join(",", aPart));
        }

        // --- writes (the only four) ---

        /// <summary>Restore one persisted contents pair. Rejects a non-positive count: no writer
        /// emits one, so it can only come from a hand-edited or damaged file, and the two readers
        /// disagreed about it before (BuildView summed it, the reverse index filtered it). Also
        /// rejects a DUPLICATE id within one file — silently keeping the last of two values would
        /// be an unreported accept in the very method whose job is detecting damage.</summary>
        /// <returns><c>false</c> when the pair was rejected — the caller counts that as damage.</returns>
        public bool TryRestoreContent(int id, int count)
        {
            if (count < 1 || _contents.ContainsKey(id))
                return false;
            _contents[id] = count;
            return true;
        }

        /// <summary>Restore one persisted aux pair. Same rejection rules as
        /// <see cref="TryRestoreContent"/>.</summary>
        public bool TryRestoreAux(long packedKey, int count)
        {
            if (count < 1 || _aux.ContainsKey(packedKey))
                return false;
            _aux[packedKey] = count;
            return true;
        }

        /// <summary>Merge an observation into the remembered contents, in place.
        /// <para>Three loops, because a Dictionary must not be structurally modified while it is
        /// enumerated: loop 1 reads the remembered ids and decides drop-vs-restore (allocating
        /// nothing in the steady state, where every observed count matches), loop 2 overlays the
        /// observation while skipping the ids loop 1 chose to preserve, loop 3 removes the ids a
        /// removal was actually authorized for.</para>
        /// <para>Both permissions are the LEDGER's decision, never the caller's — see
        /// <see cref="PossessionLedger.ApplyScan"/> for what they mean and why there are two.</para></summary>
        /// <returns>The UNITS lost — the Iter-42 detector's input.</returns>
        public int MergeContents(Dictionary<int, int> observed, bool mayShrink, bool absenceIsConfirmed)
        {
            int droppedUnits = 0;
            HashSet<int> restore = null;
            List<int> drop = null;
            bool sawSoftAbsence = false;
            foreach (var kv in _contents)
            {
                int now = Observed(observed, kv.Key);
                if (now >= kv.Value)
                    continue; // the observation is at least as large → it wins outright
                if (!mayShrink)
                {
                    (restore ??= new HashSet<int>()).Add(kv.Key);
                    continue;
                }
                if (now > 0)
                {
                    // Observed, just fewer: direct evidence, applied at once.
                    droppedUnits += kv.Value - now;
                    continue;
                }
                if (absenceIsConfirmed || _contentsSoftMisses >= 1)
                {
                    droppedUnits += kv.Value;
                    (drop ??= new List<int>()).Add(kv.Key);
                }
                else
                {
                    sawSoftAbsence = true;
                    (restore ??= new HashSet<int>()).Add(kv.Key);
                }
            }
            _contentsSoftMisses = sawSoftAbsence ? _contentsSoftMisses + 1 : 0;
            if (observed != null)
                foreach (var kv in observed)
                    if (kv.Value >= 1 && (restore == null || !restore.Contains(kv.Key)))
                        _contents[kv.Key] = kv.Value;
            if (drop != null)
                for (int i = 0; i < drop.Count; i++)
                    _contents.Remove(drop[i]);
            return droppedUnits;
        }

        /// <summary>The same merge on the aux axis.
        /// <para>Iter-44 fixed the asymmetry the Iter-43 review flagged: <c>SetLiveAux</c>
        /// restored only keys that were ABSENT from the observation, so a colour going 3→0
        /// restored the stale 3 while 3→1 recorded 1 — the same evidence, two different
        /// answers. The per-key COUNT comparison below is the contents rule, applied
        /// identically.</para></summary>
        /// <returns>How many remembered aux keys were REDUCED or removed. Units are not
        /// meaningful here: an aux count is "how many of this skin/colour", and it is the key
        /// that can go stale — hence <see cref="TilePublishResult.AuxKeysReduced"/>, not
        /// "dropped".</returns>
        public int MergeAux(Dictionary<long, int> observed, bool mayShrink, bool absenceIsConfirmed)
        {
            int reducedKeys = 0;
            HashSet<long> restore = null;
            List<long> drop = null;
            bool sawSoftAbsence = false;
            foreach (var kv in _aux)
            {
                int now = Observed(observed, kv.Key);
                if (now >= kv.Value)
                    continue;
                if (!mayShrink)
                {
                    (restore ??= new HashSet<long>()).Add(kv.Key);
                    continue;
                }
                if (now > 0)
                {
                    reducedKeys++;
                    continue;
                }
                if (absenceIsConfirmed || _auxSoftMisses >= 1)
                {
                    reducedKeys++;
                    (drop ??= new List<long>()).Add(kv.Key);
                }
                else
                {
                    sawSoftAbsence = true;
                    (restore ??= new HashSet<long>()).Add(kv.Key);
                }
            }
            _auxSoftMisses = sawSoftAbsence ? _auxSoftMisses + 1 : 0;
            if (observed != null)
                foreach (var kv in observed)
                    if (kv.Value >= 1 && (restore == null || !restore.Contains(kv.Key)))
                        _aux[kv.Key] = kv.Value;
            if (drop != null)
                for (int i = 0; i < drop.Count; i++)
                    _aux.Remove(drop[i]);
            return reducedKeys;
        }

        // A non-positive observed count reads as ABSENT, never as a value to store. Every
        // producer emits >= 1 (AddOne increments, AddBuffer uses `amount > 0 ? amount : 1`, the
        // aux accumulators increment), so this is a guard, not a code path — but it is what lets
        // the invariant be a property of the type instead of a property of its callers.
        private static int Observed(Dictionary<int, int> observed, int key) => observed != null && observed.TryGetValue(key, out var n) && n >= 1 ? n : 0;

        private static int Observed(Dictionary<long, int> observed, long key) => observed != null && observed.TryGetValue(key, out var n) && n >= 1 ? n : 0;
    }

    /// <summary>What one <see cref="PossessionLedger.ApplyScan"/> removed. Iter-43 reported only
    /// content units; aux removals reached NO detector at all, violating its own "count and report
    /// every deletion" rule. All of it is surfaced now.</summary>
    internal struct TilePublishResult
    {
        /// <summary>Owned units that vanished from remembered contents.</summary>
        public int DroppedUnits;

        /// <summary>Remembered aux keys whose count was REDUCED or removed — a 3→1 colour change
        /// counts one, the same as 3→0. Deliberately not called "dropped": the key may still be
        /// there with a smaller count.</summary>
        public int AuxKeysReduced;

        /// <summary>Tiles that lost content units, and tiles that lost aux keys. Counted apart
        /// because their normal-play baselines differ by orders of magnitude: repainting a row of
        /// furniture reduces aux keys as a matter of course, while there is no comparable benign
        /// bulk event on the contents axis. Folding them together is what would make a
        /// redecoration trip a data-loss report.</summary>
        public int ShrunkContentTiles,
            ShrunkAuxTiles;

        /// <summary>Whole tiles dropped by the self-heal prune.</summary>
        public int PrunedTiles;
    }

    /// <summary>
    /// Per-tile possession store keyed by world tile (x,z). Remembered tiles survive across
    /// snapshots (and are persisted); carried is transient (always live, never persisted).
    /// <see cref="BuildView"/> merges: every remembered tile contributes its contents; an item is
    /// "remembered" if it appears only in tiles NOT observed this snapshot.
    ///
    /// <para><strong>One scan, one call (Iter-44).</strong> <see cref="ApplyScan"/> takes the whole
    /// snapshot and does everything destructive inside one method body. Three earlier shapes were
    /// tried and each failed the same way in a different place: Iter-43 passed a per-write
    /// <c>bool allowShrink</c> (a permission, so the caller owned a correctness rule it could not
    /// see); the first Iter-44 draft passed four per-tile booleans (three of them derivable, and
    /// one of them the same three-term predicate written out TWICE — an <c>&amp;&amp;</c> chain in the
    /// caller, two early-<c>continue</c>s plus a caller-side gate in the prune, i.e. the worse kind
    /// of duplication, because the two copies did not even look alike);
    /// the second replaced those with a Begin/Publish/Prune protocol over ledger-held state — at
    /// which point a standalone harness found that a publish AFTER the prune still shrank, because
    /// the "is a scan open" flag was a warning trigger and not part of the authorization. The
    /// lesson is not "add the flag to the condition" (that was the patch) but that a multi-call
    /// protocol cannot be enforced at compile time in this language subset, so the shape that has
    /// no protocol is the one to ship: with a single entry point, "no scan is open" and "the prune
    /// was skipped" are not representable at all.</para>
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

        /// <summary>Remembered tiles. Diagnostics/reporting only — O(1).
        /// <para>Iter-44: this counts ALL remembered tiles, where Iter-43's <c>Containers.Count</c>
        /// counted entries in the contents dict — which is not the same thing, since that dict also
        /// held the empty ones planted by a no-op flush. It can therefore read slightly LOWER than the
        /// old field (an empty dict planted by a no-op flush is no longer created at all) and
        /// slightly HIGHER (an aux-only tile — a penned-cattle anchor tile — is now included).
        /// It is the number <see cref="Serialize"/> and the prune operate on, so it is the right
        /// one; just do not compare it directly against the figures in the Iter-41/43 notes.</para></summary>
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
                    n += pair.Value.ContentPairCount;
                return n;
            }
        }

        public static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public static int KeyX(long key) => (int)(key >> 32);

        public static int KeyZ(long key) => (int)(uint)key;

        public void SetCarried(Dictionary<int, int> carried) => _carried = carried ?? new Dictionary<int, int>();

        public void SetCarriedAux(Dictionary<long, int> aux) => _auxCarried = aux ?? new Dictionary<long, int>();

        /// <summary>
        /// Apply one whole scan: merge every observed tile, then self-heal-prune what the scan
        /// would have seen and did not. The single destructive entry point.
        ///
        /// <para><strong>What the caller supplies</strong> is only what it uniquely knows: the two
        /// per-tile accumulators, which of those tiles carried an observed CONTAINER, where the
        /// player is, the scan's own anchor gate, and whether the streaming grace has passed.
        /// Every rule below is derived here.</para>
        ///
        /// <para><strong>The rules.</strong> Nothing is ever removed during the streaming grace
        /// (<paramref name="pastGrace"/>): right after a world load or teleport chunks stream in
        /// asynchronously, so "absent" may just mean "not yet streamed". Past it:
        /// <list type="bullet">
        /// <item><c>Contents</c> may shrink when a container was observed on the tile, OR the scan
        /// WOULD have seen anything standing there (<see cref="ScanWouldSeeTile"/>).</item>
        /// <item><c>Aux</c> may shrink only on the second of those. "Some aux was observed here" is
        /// deliberately NOT evidence: one tile's aux has three producers with different archetypes
        /// (a stored pet via a container buffer, penned-cattle colours keyed to a station tile, a
        /// placed object's paint colour), so seeing one would authorize shrinking another's keys —
        /// C-1's own defect shape, one level down. It is also unnecessary, since the cases C-1 was
        /// about happen with the player at the object.</item>
        /// <item><strong>One miss is not evidence.</strong> A count that is merely LOWER than
        /// remembered is applied at once — the producer was seen, so that is direct evidence. But
        /// an entry that is ABSENT is only removed when the absence is CONFIRMED — a container was
        /// observed on that tile, so its buffer is authoritative — or when the same tile was
        /// unconfirmed-absent on the previous scan too. This is what bounds the two cases where a
        /// single scan legitimately misses a producer that still exists: a co-located container
        /// absent from one scan's query (the residual this rule retires), and a penned animal that
        /// happens to be outside <c>AnchorRadius</c> for one scan, or whose entity is briefly gone
        /// during breeding/growth churn — that one flickered a colour count to 0 and, if the player
        /// then left, froze it there. Two consecutive misses cost one extra scan interval before a
        /// genuine removal lands; the counters live in memory only.</item>
        /// </list>
        /// Why Iter-43's <c>containerTiles</c> could not serve as a universal predicate: it is
        /// filled only for entities with a <c>ContainedObjectsBuffer</c> and no <c>CraftingCD</c>.
        /// A cattle aux tile is a station/workbench tile, which HAS <c>CraftingCD</c>, so the
        /// station never puts it there and the flag was STRUCTURALLY always false for cattle. Paint
        /// is only usually false: a paint aux tile is the placeable's own tile, so for a
        /// non-container placeable it gets there only by co-location with an unrelated chest — but
        /// a PAINTABLE CONTAINER (Iter-17's variants: a painted chest or barrel, variation ≠ 0 with
        /// a <c>ContainedObjectsBuffer</c>) writes its paint aux and adds its tile in the same two
        /// branches, for the same entity. So that one aux subset was not frozen under the old rule.
        /// Everywhere else the stale colour key survived restarts, permanently inflating the
        /// Iter-36 owned counter K against Iter-41's "own ≥1 right now" contract.
        /// </para>
        ///
        /// <para><strong>What this does to Iter-43's I4 protection.</strong> I4 was: a container
        /// and a co-located torch (Iter-20's wall torch on a mannequin's tile) sit in DIFFERENT
        /// DOTS archetype chunks, since only one has a <c>ContainedObjectsBuffer</c>. Iter-41
        /// measured base containers leaving the observed scan set at ~91-115 tiles; that they
        /// leave it INDEPENDENTLY of a co-located entity is the same best-explanation inference as
        /// the band itself (chunk-granular unload), not a separate measurement. The I4 case as
        /// measured stays protected: at ~95 tiles the tile is far outside <c>PruneRadius</c>, so
        /// <see cref="ScanWouldSeeTile"/> is false. Inside 48 tiles the "one miss is not evidence"
        /// rule takes over, so a container missing from a single scan no longer costs its
        /// contents at all — that residual is retired rather than merely bounded. What remains is
        /// a container absent from TWO consecutive scans while the player stands within 48 tiles;
        /// it would still lose its unconfirmed ids, and retiring that needs real provenance in the
        /// stored record (which container contributed which id), i.e. a schema change.</para>
        ///
        /// <para>The same trigger reaches the AUX that container contributed to its tile — a stored
        /// pet's skin key, a caged animal's colour (<c>AddBuffer</c> writes them to the chest's own
        /// tile). This is the one place the new rule is WEAKER for aux than Iter-43's, which
        /// required the container to be observed; it is now the tile-level premise, delayed by the
        /// two-miss rule. Same exposure, same self-repair.</para>
        ///
        /// <para><strong>Ownership.</strong> The caller keeps its dictionaries; values are copied
        /// in and the passed references are never stored or mutated. Both Iter-43 writers wrote
        /// remembered entries INTO the caller's dictionary and then adopted that same instance;
        /// for <c>SetLiveAux</c> that was an observable side effect, since the scanner re-reads its
        /// aux accumulator afterwards (<c>MarkFrom</c> walks it to mark the persistent pet
        /// collection, and so marked remembered-but-unobserved skins as currently owned).</para>
        ///
        /// <para><strong>Precondition.</strong> Observed counts are ≥ 1; a non-positive one is read
        /// as absence rather than stored (<see cref="TileEntry"/>).</para>
        /// </summary>
        /// <param name="contents">Per-tile observed contents (the scanner's <c>scan</c>).</param>
        /// <param name="aux">Per-tile observed sub-variant counts (the scanner's <c>auxScan</c>).</param>
        /// <param name="containerTiles">Tiles where a container entity was observed, so its stored
        /// contents were confirmed. The one fact the scanner uniquely knows.</param>
        /// <param name="havePlayer">False on the main menu, or in a scan where the player entity
        /// never turned up in the query. <see cref="ScanWouldSeeTile"/> is then false everywhere, so
        /// aux cannot shrink and the prune does nothing. Contents on a tile whose CONTAINER was
        /// observed can still shrink, and that is deliberate: a buffer we actually read is evidence
        /// about that container regardless of where the player is. (Said explicitly because the
        /// natural shorthand — "no player, nothing is removed" — is false, and was written here
        /// before a reviewer checked it.)</param>
        /// <param name="pruneRadius">The distance within which "not observed" may be read as
        /// "gone" — see <see cref="ScanWouldSeeTile"/>. NOT <c>AnchorRadius</c>.</param>
        /// <param name="coveredByLoadedAnchor">The scan's own WithinAnchor gate, so the ledger asks
        /// the identical question about a tile that the scan would have asked.</param>
        /// <param name="liveKeys">FILLED here with every observed tile key; the caller needs it for
        /// <see cref="BuildView"/>, and the prune skips exactly these.</param>
        /// <returns>Everything that was removed. The caller surfaces all of it; an unreported
        /// deletion is what made Iter-42 invisible for a month.</returns>
        public TilePublishResult ApplyScan(
            Dictionary<long, Dictionary<int, int>> contents,
            Dictionary<long, Dictionary<long, int>> aux,
            HashSet<long> containerTiles,
            bool havePlayer,
            Vector2 player,
            float pruneRadius,
            Func<long, bool> coveredByLoadedAnchor,
            bool pastGrace,
            HashSet<long> liveKeys
        )
        {
            var result = new TilePublishResult();
            if (contents == null || aux == null || liveKeys == null)
            {
                Debug.LogWarning("[ItemChecklist] ApplyScan called with a null accumulator — the ledger is left untouched.");
                return result;
            }
            if (coveredByLoadedAnchor == null)
            {
                // A programming error, not a runtime state. Report it (this codebase's rule is not
                // to absorb) and then make the scan completely non-destructive — `pastGrace` is
                // cleared as well as `havePlayer`, because clearing only the latter would still let
                // an OBSERVED CONTAINER shrink its tile, and then this message would be false. The
                // merge still runs, so observations are recorded; only removal is off.
                Debug.LogWarning("[ItemChecklist] ApplyScan got no anchor predicate — nothing will be pruned or shrunk this scan.");
                havePlayer = false;
                pastGrace = false;
            }

            _player = player;
            _pruneR2 = pruneRadius * pruneRadius;
            _coveredByLoadedAnchor = coveredByLoadedAnchor;
            _havePlayer = havePlayer;

            // One publish per observed tile, over the union of both accumulators' keys.
            foreach (var pair in contents)
                liveKeys.Add(pair.Key);
            foreach (var pair in aux)
                liveKeys.Add(pair.Key);

            foreach (var key in liveKeys)
            {
                contents.TryGetValue(key, out var tileContents);
                aux.TryGetValue(key, out var tileAux);
                bool containerObserved = containerTiles != null && containerTiles.Contains(key);

                bool existed = _tiles.TryGetValue(key, out var entry);
                if (!existed)
                    entry = new TileEntry();

                // Nothing remembered here yet ⇒ nothing can shrink ⇒ the expensive "would the
                // scan have seen this tile" test is not evaluated at all. Same during the grace.
                // That matters: it scans every anchor (~44 at a built-up base), per tile.
                bool mayShrinkContents = false,
                    mayShrinkAux = false,
                    absenceIsConfirmed = false;
                if (existed && pastGrace)
                {
                    bool wouldSee = ScanWouldSeeTile(key);
                    mayShrinkContents = containerObserved || wouldSee;
                    mayShrinkAux = wouldSee;
                    absenceIsConfirmed = containerObserved;
                }

                int units = entry.MergeContents(tileContents, mayShrinkContents, absenceIsConfirmed);
                int auxKeys = entry.MergeAux(tileAux, mayShrinkAux, absenceIsConfirmed);
                result.DroppedUnits += units;
                result.AuxKeysReduced += auxKeys;
                if (units > 0)
                    result.ShrunkContentTiles++;
                if (auxKeys > 0)
                    result.ShrunkAuxTiles++;

                if (entry.IsEmpty)
                {
                    if (existed)
                        _tiles.Remove(key);
                    // else: never planted. Iter-43's `SetLiveContainer` stored the caller's empty
                    // dict here, because `Tile(scan, key)` creates one as an argument side effect —
                    // so a tile that produced nothing became a remembered tile, inflating the
                    // ledger count and joining liveKeys, which shielded it from the prune for as
                    // long as whatever put it in liveKeys kept being observed. (The aux half was
                    // NOT affected: Iter-43's flush explicitly skipped empty aux dicts — that was
                    // its own I3 fix. `TileAux`-as-an-argument caused the I3 deletion, not this.)
                }
                else if (!existed)
                    _tiles[key] = entry;
            }

            result.PrunedTiles = pastGrace && havePlayer ? PruneStaleNear(liveKeys) : 0;

            // Release the caller's closure: it captures the anchor list, which must not outlive
            // the scan that built it.
            _coveredByLoadedAnchor = null;
            _havePlayer = false;
            return result;
        }

        // Scan-local geometry, valid only inside ApplyScan. Not a protocol: nothing outside that
        // method body reads them, and both destructive paths are inside it.
        private Vector2 _player;
        private float _pruneR2;
        private bool _havePlayer;
        private Func<long, bool> _coveredByLoadedAnchor;

        /// <summary>
        /// Would this scan have observed anything standing on <paramref name="key"/>? True iff the
        /// tile is within <c>PruneRadius</c> of the player (so its chunk is force-loaded) AND
        /// covered by a loaded anchor (so a present entity would have passed the scan's own
        /// WithinAnchor gate). This is the ONE definition; both the per-tile merge and the prune
        /// read it, where Iter-44's first draft had the caller re-derive it as a duplicated
        /// three-term expression.
        /// <para>BOTH halves are required (Iter-41): the small player radius guarantees the chunk
        /// is loaded — distance alone is not enough, because a container's chunk can unload while
        /// a co-located workbench stays (mode 2, what wrecked the old 180) — and the anchor cover
        /// guarantees a present container would have been in scope, since a base container can be
        /// player-near yet lose cover when its workbench just crossed the ~91 observation dropout
        /// (mode 1).</para>
        /// </summary>
        private bool ScanWouldSeeTile(long key)
        {
            if (!_havePlayer)
                return false;
            float dx = KeyX(key) - _player.x,
                dz = KeyZ(key) - _player.y;
            if (dx * dx + dz * dz > _pruneR2)
                return false;
            return _coveredByLoadedAnchor(key);
        }

        // Iter-42: the Iter-28 `WorldNaturePruned` flag + `PruneByPredicate(Func<int,bool>)`
        // one-time world-nature eviction lived here and were REMOVED — an id-predicate sweep over
        // the ledger cannot distinguish a placed wild object from the same id legitimately STORED
        // in a chest (both are plain entries in the same per-tile dict), so it deleted real
        // possession on every load (the flag was never serialized, so "one-time" never held).
        // Rationale + the measured damage: see the note at the top of `PossessionScanner.Scan`.

        /// <summary>Self-heal: drop every remembered tile the scan WOULD have seen something on
        /// (<see cref="ScanWouldSeeTile"/>) yet saw nothing on at all ⇒ destroyed/emptied. A real
        /// destruction always satisfies both halves of that test (you stand next to the container;
        /// its workbench is co-located), so nothing legitimate is missed. Collect-then-remove, to
        /// avoid mutating during iteration.
        /// <para>Iter-44: the hand-built union of the two per-tile dicts' key sets is gone — one
        /// dict means aux-only tiles (penned cattle at an anchor tile) are covered by
        /// construction.</para>
        /// <para><strong>How this differs from a merge shrink.</strong> The two act on DISJOINT
        /// tile sets: every observed key is in <paramref name="liveKeys"/>, and this skips those.
        /// So the prune only ever deletes tiles the scan did not see at all, while a merge only
        /// ever shrinks tiles it did see. They share a premise, not a population — which is why
        /// "the merge is strictly smaller than the prune, so it adds no new risk" (written in
        /// Iter-44's first draft) does not hold as an argument. Note also that the prune has no
        /// "one miss is not evidence" delay: a tile with NOTHING observed on it is a different
        /// claim from a tile where something was seen but one producer was missing.</para></summary>
        /// <returns>Iter-43: how many tiles were dropped, so the caller can surface it — this
        /// deletion used to be entirely unreported, even under diagnostics.</returns>
        private int PruneStaleNear(HashSet<long> liveKeys)
        {
            List<long> drop = null;
            foreach (var pair in _tiles)
            {
                long key = pair.Key;
                if (liveKeys.Contains(key))
                    continue; // observed this scan → not stale
                if (!ScanWouldSeeTile(key))
                    continue; // out of range, or no loaded anchor would have observed it → keep
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
                pair.Value.AccumulateInto(totals, anyItem, liveItems, liveKeys.Contains(pair.Key));
                pair.Value.AccumulateAuxInto(aux);
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
                if (pair.Value.Holds(objectId))
                    keys.Add(pair.Key);
            return keys;
        }

        /// <summary>How many container tiles hold <paramref name="objectId"/> — the
        /// allocation-free count used by the trackable gate and the tooltip hint.</summary>
        public int CountTilesHolding(int objectId)
        {
            int n = 0;
            foreach (var pair in _tiles)
                if (pair.Value.Holds(objectId))
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

        private static bool _emptyEntryWarned;

        public string Serialize()
        {
            var lines = new List<string>(_tiles.Count + 1) { VersionMarker };
            foreach (var pair in _tiles)
            {
                var entry = pair.Value;
                if (entry.IsEmpty)
                {
                    // Unreachable by construction (ApplyScan neither creates nor keeps an empty
                    // entry, LoadFrom drops one) — so if it ever fires, the invariant broke and
                    // this guard is silently dropping a tile. Say so once. Static on purpose: it
                    // guards a broken invariant in the CODE, which is broken for every character
                    // alike, not a per-character runtime state like the world-null warning.
                    if (!_emptyEntryWarned)
                    {
                        _emptyEntryWarned = true;
                        Debug.LogWarning(
                            $"[ItemChecklist] ledger held an EMPTY tile entry at ({KeyX(pair.Key)},{KeyZ(pair.Key)}) — skipped while serializing."
                        );
                    }
                    continue;
                }
                entry.AppendTo(lines, KeyX(pair.Key), KeyZ(pair.Key));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Parse a serialized ledger, replacing everything currently held.
        /// <para>Iter-43 made the outcome reportable. Discarding a whole file is a total
        /// data-loss event, and it used to happen with NO log while <c>Load</c> reported success
        /// — so a truncated or corrupted file (Wine, power loss, disk full) was indistinguishable
        /// from a legitimate version migration, and looked byte-for-byte like the Iter-42 symptom.
        /// The caller logs and records what happened; only the count can tell it apart.</para>
        /// <para>Iter-44 (review C-3) adds <paramref name="skipped"/>. This parser cannot throw on
        /// damaged input — it <c>continue</c>s past anything it does not like — so a file truncated
        /// mid-write parsed into a SUBSET and the caller still reported success, left the store
        /// writable, and the next autosave persisted the subset. Truncation almost always leaves
        /// exactly one malformed line, so counting the skips is a near-free detector. Empty lines
        /// and '#' lines are not damage and are not counted.</para>
        /// <para>Lines are <c>Trim()</c>ed. Not cosmetic: without it a file re-saved with CRLF
        /// (which the incident messages invite players to copy and inspect) leaves a lone '\r' in
        /// the empty aux segment of nearly every line, so <em>almost every line</em> counts as
        /// damage and a perfectly healthy character goes permanently read-only.</para>
        /// </summary>
        /// <param name="skipped">Data lines that could not be fully accepted (malformed
        /// coordinates, wrong segment count, or any rejected id:count pair). Any value > 0 means
        /// the file is damaged — the caller must treat the load as FAILED.</param>
        /// <returns>Tiles parsed, or <c>-1</c> when the version marker did not match and the
        /// whole file was therefore discarded.</returns>
        public int LoadFrom(string text, out int skipped)
        {
            skipped = 0;
            _tiles.Clear();
            if (string.IsNullOrEmpty(text))
                return 0;
            // Compare the FIRST LINE exactly, not a prefix: `StartsWith("#icl-ledger-v3")` would
            // also accept a future "#icl-ledger-v30" and then parse it under the wrong schema.
            int nl = text.IndexOf('\n');
            string firstLine = (nl < 0 ? text : text.Substring(0, nl)).Trim();
            if (firstLine != VersionMarker)
                return -1; // discard (pre-v3 migration, or a corrupt file — the caller reports it)
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var seg = line.Split('|');
                if (seg.Length != 3)
                {
                    skipped++; // a v3 line has exactly two '|'
                    continue;
                }
                var xz = seg[0].Split(',');
                if (xz.Length != 2 || !int.TryParse(xz[0], out int x) || !int.TryParse(xz[1], out int z))
                {
                    skipped++;
                    continue;
                }
                long key = Key(x, z);

                // Iter-44: merge into an existing entry instead of replacing it. Serialize emits
                // unique keys, so this only matters for a hand-edited, concatenated or partially
                // rewritten file — where Iter-44's OWN first draft (`_tiles[key] = entry`) silently
                // discarded the first line's half. Iter-43's two independent dicts had kept both,
                // so this is a self-caught regression of the refactor, not an inherited one.
                if (!_tiles.TryGetValue(key, out var entry))
                    entry = new TileEntry();

                bool lineOk = true;
                foreach (var pair in seg[1].Split(','))
                {
                    if (pair.Length == 0)
                        continue; // an empty segment is legal ("x,z||…")
                    int colon = pair.IndexOf(':');
                    if (
                        colon <= 0
                        || !int.TryParse(pair.Substring(0, colon), out int id)
                        || !int.TryParse(pair.Substring(colon + 1), out int cnt)
                        || !entry.TryRestoreContent(id, cnt)
                    )
                        lineOk = false;
                }
                foreach (var pair in seg[2].Split(','))
                {
                    if (pair.Length == 0)
                        continue;
                    int colon = pair.IndexOf(':');
                    if (
                        colon <= 0
                        || !long.TryParse(pair.Substring(0, colon), out long pk)
                        || !int.TryParse(pair.Substring(colon + 1), out int cnt)
                        || !entry.TryRestoreAux(pk, cnt)
                    )
                        lineOk = false;
                }
                // A line with valid coordinates but nothing in either segment carries no
                // information; Serialize never emits one, so it is damage like any other.
                if (!lineOk || entry.IsEmpty)
                    skipped++;
                if (entry.IsEmpty)
                    _tiles.Remove(key);
                else
                    _tiles[key] = entry;
            }
            return _tiles.Count;
        }
    }
}
