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
    /// <para><see cref="Contents"/> is objectID → count, written by scan paths #2
    /// (<c>AddBuffer</c>, a container's contents) and #3 (<c>AddOne</c>, the placed object
    /// itself). <see cref="Aux"/> is <c>PackKey(id, secondDim)</c> → count for the sub-variant
    /// axes (pet skins, cattle colours, paint colours).</para>
    /// <para><strong>Both dictionaries are private and every write goes through this type.</strong>
    /// The "a remembered count is always ≥ 1" invariant used to rest on two independent
    /// arguments — an explicit test at the parse boundary, and the merge's arithmetic — which
    /// is the same shape as the Iter-43 I2 defect, where two readers disagreed about what
    /// "present" means. One enforcement point now: <see cref="RestoreContent"/> /
    /// <see cref="RestoreAux"/> reject a non-positive count, and both merges treat a
    /// non-positive OBSERVED count as absent rather than writing it.</para>
    /// </summary>
    internal sealed class TileEntry
    {
        private readonly Dictionary<int, int> _contents = new Dictionary<int, int>();
        private readonly Dictionary<long, int> _aux = new Dictionary<long, int>();

        /// <summary>Remembered contents, read-only. Enumerating and <c>TryGetValue</c> are all
        /// any reader needs (view build, serialize, the Iter-40 reverse index).</summary>
        public IReadOnlyDictionary<int, int> Contents => _contents;

        /// <summary>Remembered sub-variant counts, read-only.</summary>
        public IReadOnlyDictionary<long, int> Aux => _aux;

        /// <summary>Nothing remembered here any more — the ledger drops such a tile so an
        /// emptied tile does not linger as a live key that the prune can never reach.</summary>
        public bool IsEmpty => _contents.Count == 0 && _aux.Count == 0;

        /// <summary>Restore one persisted contents pair. Rejects a non-positive count: no writer
        /// emits one, so it can only come from a hand-edited or damaged file, and the two readers
        /// disagreed about it before (BuildView summed it, the reverse index filtered it).</summary>
        /// <returns><c>false</c> when the pair was rejected — the caller counts that as damage.</returns>
        public bool RestoreContent(int id, int count)
        {
            if (count < 1)
                return false;
            _contents[id] = count;
            return true;
        }

        /// <summary>Restore one persisted aux pair. Same rejection rule as
        /// <see cref="RestoreContent"/>.</summary>
        public bool RestoreAux(long packedKey, int count)
        {
            if (count < 1)
                return false;
            _aux[packedKey] = count;
            return true;
        }

        /// <summary>Merge an observation into the remembered contents, in place.
        /// <para>Two passes, because a Dictionary must not be structurally modified while it is
        /// enumerated, plus a third removal loop: pass 1 reads the remembered ids and decides
        /// drop-vs-restore (allocating nothing in the steady state, where every observed count
        /// matches), pass 2 overlays the observation while skipping the ids pass 1 chose to
        /// preserve, pass 3 removes the ids a confirmed observation no longer sees at all.</para>
        /// <para><paramref name="mayShrink"/> is the ledger's decision, never the caller's — see
        /// <see cref="PossessionLedger.Publish"/>.</para></summary>
        /// <returns>The UNITS lost — the Iter-42 detector's input.</returns>
        public int MergeContents(Dictionary<int, int> observed, bool mayShrink)
        {
            int droppedUnits = 0;
            HashSet<int> restore = null; // remembered ids the scan did not confirm → keep
            List<int> drop = null; // remembered ids a confirmed scan no longer sees at all
            foreach (var kv in _contents)
            {
                int now = Observed(observed, kv.Key);
                if (now >= kv.Value)
                    continue; // the observation is at least as large → it wins outright
                if (mayShrink)
                {
                    droppedUnits += kv.Value - now;
                    if (now <= 0)
                        (drop ??= new List<int>()).Add(kv.Key);
                }
                else
                    (restore ??= new HashSet<int>()).Add(kv.Key);
            }
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
        /// restored the stale 3 while 3→1 recorded 1 — the same evidence, two different answers.
        /// The per-key COUNT comparison below is the contents rule, applied identically.</para></summary>
        /// <returns>How many remembered aux keys were REDUCED or removed. Units are not
        /// meaningful here: an aux count is "how many of this skin/colour", and it is the key
        /// that can go stale — hence <see cref="TilePublishResult.AuxKeysReduced"/>, not
        /// "dropped".</returns>
        public int MergeAux(Dictionary<long, int> observed, bool mayShrink)
        {
            int reducedKeys = 0;
            HashSet<long> restore = null;
            List<long> drop = null;
            foreach (var kv in _aux)
            {
                int now = Observed(observed, kv.Key);
                if (now >= kv.Value)
                    continue;
                if (mayShrink)
                {
                    reducedKeys++;
                    if (now <= 0)
                        (drop ??= new List<long>()).Add(kv.Key);
                }
                else
                    (restore ??= new HashSet<long>()).Add(kv.Key);
            }
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
        // the invariant be stated as a property of the type instead of a property of its callers.
        private static int Observed(Dictionary<int, int> observed, int key) => observed != null && observed.TryGetValue(key, out var n) && n >= 1 ? n : 0;

        private static int Observed(Dictionary<long, int> observed, long key) => observed != null && observed.TryGetValue(key, out var n) && n >= 1 ? n : 0;
    }

    /// <summary>What one <see cref="PossessionLedger.Publish"/> call removed. Iter-43 reported
    /// only content units; aux removals reached NO detector at all, violating its own "count
    /// and report every deletion" rule. Both are surfaced now.</summary>
    internal struct TilePublishResult
    {
        /// <summary>Owned units that vanished from this tile's remembered contents.</summary>
        public int DroppedUnits;

        /// <summary>Remembered aux keys whose count was REDUCED or removed — a 3→1 colour change
        /// counts one, the same as 3→0. Deliberately not called "dropped": the key may still be
        /// there with a smaller count.</summary>
        public int AuxKeysReduced;
    }

    /// <summary>
    /// Per-tile possession store keyed by world tile (x,z). Remembered tiles survive across
    /// snapshots (and are persisted); carried is transient (always live, never persisted).
    /// BuildView merges: every remembered tile contributes its contents; an item is
    /// "remembered" if it appears only in tiles NOT observed this snapshot.
    ///
    /// <para><strong>Scan protocol (Iter-44).</strong> <see cref="BeginScan"/> once, then
    /// <see cref="Publish"/> per observed tile, then <see cref="PruneStaleNear"/> — which also
    /// ends the scan. The caller reports only what it uniquely knows (which tiles it saw, and
    /// whether a CONTAINER was among them); everything else — how far the player is, whether a
    /// tile is anchor-covered, whether the streaming grace has passed — is scan state the ledger
    /// holds once. Iter-43 passed a per-write <c>bool allowShrink</c>, and its successor draft
    /// passed four per-tile booleans; three of those were derivable from data the ledger already
    /// had, and one of them (the player-near + anchor-covered test) was a THREE-TERM EXPRESSION
    /// duplicated verbatim in the caller and in <see cref="PruneStaleNear"/>. Two copies of the
    /// predicate that authorizes a destructive decision, in a codebase with no automated tests,
    /// is the drift surface this protocol removes: it now exists exactly once, in
    /// <see cref="ScanWouldSeeTile"/>, and both consumers read it.</para>
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

        /// <summary>What the current scan knows — set once by <see cref="BeginScan"/> instead of
        /// being re-derived per tile. <c>PastGrace</c> in particular is a property of the SCAN
        /// (the world has been stably loaded long enough that "absent" means "gone"), so a
        /// per-tile copy would let two different rules apply within one scan.</summary>
        private struct ScanContext
        {
            public bool Active;
            public bool HavePlayer;
            public float PlayerX,
                PlayerZ,
                Radius2;
            public bool PastGrace;
            public Func<long, bool> CoveredByLoadedAnchor;
        }

        private ScanContext _scan;

        // Per INSTANCE, not static: a new ledger is constructed on every character load, and its
        // protocol is its own. A static flag would let one character's broken scan silence the
        // warning for every character afterwards — the one-shot-consumed-by-an-earlier-case
        // pattern this iteration fixed in three other places.
        private bool _noScanWarned;

        /// <summary>Remembered tiles. Diagnostics/reporting only — O(1).
        /// <para>Iter-44: this counts ALL remembered tiles, where Iter-43's <c>Containers.Count</c>
        /// counted only tiles carrying contents. It can therefore read slightly LOWER than the
        /// old field (an empty dict planted by a no-op flush is no longer created at all) and
        /// slightly HIGHER (an aux-only tile — a penned-cattle anchor tile — is now included).
        /// It is the number <see cref="Serialize"/> and <see cref="PruneStaleNear"/> operate on,
        /// so it is the right one; just do not compare it directly against the figures recorded
        /// in the Iter-41/43 notes.</para></summary>
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

        /// <summary>Open a scan. Everything the shrink and prune rules need, once.</summary>
        /// <param name="havePlayer">False on the main menu / before the player entity exists —
        /// then no tile is "definitely observable" and nothing may shrink.</param>
        /// <param name="radius">The distance within which "not observed" may be read as "gone"
        /// (<c>PruneRadius</c>, 48). See <see cref="ScanWouldSeeTile"/>.</param>
        /// <param name="coveredByLoadedAnchor">The scan's own WithinAnchor gate, so the ledger
        /// asks the identical question the scan would have asked about a tile.</param>
        /// <param name="pastGrace">The caller's <c>allowPrune</c>: chunks stream in asynchronously
        /// after a world load/teleport, so until this is true "absent" may just mean
        /// "not streamed in yet" and NOTHING may be removed.</param>
        public void BeginScan(bool havePlayer, float playerX, float playerZ, float radius, Func<long, bool> coveredByLoadedAnchor, bool pastGrace)
        {
            _scan = new ScanContext
            {
                Active = true,
                HavePlayer = havePlayer && coveredByLoadedAnchor != null,
                PlayerX = playerX,
                PlayerZ = playerZ,
                Radius2 = radius * radius,
                PastGrace = pastGrace,
                CoveredByLoadedAnchor = coveredByLoadedAnchor,
            };
        }

        /// <summary>
        /// Publish one tile's freshly observed state. Replaces Iter-43's
        /// <c>SetLiveContainer</c> + <c>SetLiveAux</c> + <c>ClearAux</c>: one call per tile per
        /// scan, both dimensions, and the ledger — not the caller — decides what may shrink.
        ///
        /// <para><strong>The rules.</strong> A dimension may shrink only past the streaming
        /// grace, and then:
        /// <list type="bullet">
        /// <item><c>Contents</c>: a container was observed on this tile (<paramref name="containerObserved"/>),
        /// OR the tile is one the scan WOULD have seen anything on (<see cref="ScanWouldSeeTile"/>).</item>
        /// <item><c>Aux</c>: only the second test. "Some aux was seen here" is deliberately NOT
        /// evidence — one tile's aux has three producers with different archetypes (a stored pet
        /// via a container buffer, penned-cattle colours keyed to a station tile, a placed
        /// object's paint colour), so seeing one would authorize shrinking another's keys. That is
        /// C-1's own defect shape, one level down, and it is not needed: both cases C-1 was about
        /// — a pen losing its LAST animal of a colour, a placeable repainted A→B — happen with
        /// the player standing at the object, i.e. inside the envelope the second test covers.</item>
        /// </list>
        /// Why Iter-43's <c>containerTiles</c> could not serve as a universal predicate: it is
        /// filled only for entities with a <c>ContainedObjectsBuffer</c> and no <c>CraftingCD</c>.
        /// A cattle aux tile is a station/workbench tile, which HAS <c>CraftingCD</c>, so the
        /// station never puts it there; a paint aux tile is a plain placeable, and lands there
        /// only by co-location with an unrelated chest. Not "impossible", but never for a reason
        /// that has anything to do with that tile's aux — so for cattle and paint the flag was
        /// effectively always false, and a stale colour key survived restarts, permanently
        /// inflating the Iter-36 owned counter K against Iter-41's "own ≥1 right now" contract.
        /// </para>
        ///
        /// <para><strong>What this does to Iter-43's I4 protection — scoped honestly.</strong>
        /// I4 was: a container and a co-located torch (Iter-20's wall torch on a mannequin's
        /// tile) sit in DIFFERENT DOTS archetype chunks, since only one of them has a
        /// <c>ContainedObjectsBuffer</c>. Iter-41 measured base containers leaving the observed
        /// scan set at ~91-115 tiles; that they leave it INDEPENDENTLY of a co-located entity is
        /// the same best-explanation inference as the band itself (chunk-granular unload), not a
        /// separate measurement. Iter-43's answer was to require an observed container, and the
        /// I4 case as measured is still protected: at ~95 tiles the tile is far outside
        /// <c>PruneRadius</c>, so <see cref="ScanWouldSeeTile"/> is false and the remembered ids
        /// are kept. But this rule is WEAKER than "a container was observed here", and the
        /// difference is a real residual, not a rounding error:
        /// <list type="bullet">
        /// <item><strong>Known residual.</strong> A tile whose co-located container is missing
        /// from a single post-grace scan while the player stands within 48 tiles loses that
        /// container's unconfirmed ids. Two containers sharing one tile with only one observed is
        /// the same case. Retiring it needs real provenance in the stored record (which container
        /// contributed which id) — a schema change.</item>
        /// <item><strong>Why it is acceptable.</strong> Inside 48 tiles the loss SELF-REPAIRS: the
        /// container is observed on the next scan and its full contents are rewritten from its
        /// buffer, so the exposure is one scan interval in memory plus, at worst, one persisted
        /// autosave that the following save corrects. That is categorically different from the
        /// pre-Iter-43 unconditional delete, which could lose contents at a distance where
        /// re-observation would not come for minutes.</item>
        /// </list>
        /// Note also that this is NOT the same inference <see cref="PruneStaleNear"/> makes, even
        /// though it shares the premise: the prune fires when NOTHING at all was seen on a tile,
        /// this rule when something was seen but the container was not — and the two operate on
        /// disjoint tile sets, so "strictly smaller than the prune" is not available as a
        /// justification.</para>
        ///
        /// <para><strong>Semantics.</strong> No previous entry → the observation becomes the
        /// tile's state, zero drops. Otherwise, per remembered id/aux key: a HIGHER (or equal)
        /// observed count always wins; a LOWER one — including absent, i.e. 0 — either shrinks
        /// (counted in the returned <see cref="TilePublishResult"/>) or is refused and the
        /// remembered value kept, exactly as the rule for that dimension says. Ids the scan saw
        /// for the first time are simply added. An entry that ends up
        /// <see cref="TileEntry.IsEmpty"/> is removed (and never created).</para>
        ///
        /// <para><strong>An empty observed aux dict is NOT a deletion request.</strong> It goes
        /// through the same rule as any other observation. This subsumes the removed
        /// <c>ClearAux</c> (whose job — dropping stale aux on a tile re-observed without aux — is
        /// now just "the scan would have seen a producer here, and none produced ⇒ may shrink")
        /// AND the scanner's "skip empty aux dicts" workaround, which existed only because the
        /// old ungated empty write WAS a deletion.</para>
        ///
        /// <para><strong>Ownership.</strong> The caller keeps its dictionaries; this method copies
        /// values in and never stores or mutates the passed references. Both Iter-43 writers wrote
        /// remembered entries INTO the caller's dictionary and then adopted that same instance;
        /// for <c>SetLiveAux</c> that was an observable side effect, since the scanner re-reads
        /// its aux accumulator afterwards (<c>MarkFrom</c> walks <c>auxScan</c> to mark the
        /// persistent pet collection, and so marked remembered-but-unobserved skins as currently
        /// owned). Either dictionary may be <c>null</c>, meaning "this tile produced none of that
        /// dimension"; an empty dictionary means the same thing.</para>
        ///
        /// <para><strong>Precondition.</strong> Observed counts are ≥ 1. A non-positive one is
        /// read as absence rather than stored (<see cref="TileEntry"/>).</para>
        /// </summary>
        /// <returns>What was removed — 0/0 when nothing was. The caller surfaces both numbers;
        /// an unreported deletion is what made Iter-42 invisible for a month.</returns>
        public TilePublishResult Publish(long key, Dictionary<int, int> contents, Dictionary<long, int> aux, bool containerObserved)
        {
            var result = new TilePublishResult();
            WarnIfNoScan();
            bool existed = _tiles.TryGetValue(key, out var entry);
            if (!existed)
                entry = new TileEntry();

            // Nothing remembered here yet ⇒ nothing can shrink ⇒ the expensive
            // "would the scan have seen this tile" test is not evaluated at all. Same during the
            // grace. That matters: it is a scan over every anchor (~44 at a built-up base), and
            // the flush runs it per tile.
            // `Active` is part of the condition, not just a warning trigger: without it a publish
            // after the scan closed could still shrink through `containerObserved`, which does not
            // consult the player position at all. Caught by the standalone ledger harness, not by
            // reading — the scanner never publishes after the prune, so it was a latent hole of
            // exactly the kind this protocol exists to close.
            bool mayShrinkContents = false,
                mayShrinkAux = false;
            if (existed && _scan.Active && _scan.PastGrace)
            {
                bool wouldSee = ScanWouldSeeTile(key);
                mayShrinkContents = containerObserved || wouldSee;
                mayShrinkAux = wouldSee;
            }

            result.DroppedUnits = entry.MergeContents(contents, mayShrinkContents);
            result.AuxKeysReduced = entry.MergeAux(aux, mayShrinkAux);

            if (entry.IsEmpty)
            {
                if (existed)
                    _tiles.Remove(key);
                // else: never planted. Iter-43 stored the caller's empty dict here, because
                // `Tile(scan, key)` / `TileAux(auxScan, key)` create one as an argument side
                // effect — so a tile that produced nothing became a remembered tile, inflating
                // the ledger count, joining liveKeys and thereby shielding itself from the prune
                // forever.
            }
            else if (!existed)
                _tiles[key] = entry;
            return result;
        }

        /// <summary>
        /// Would this scan have observed anything standing on <paramref name="key"/>? True iff the
        /// tile is within <c>PruneRadius</c> of the player (so its chunk is force-loaded) AND
        /// covered by a loaded anchor (so a present entity would have passed the scan's own
        /// WithinAnchor gate). This is the ONE definition; <see cref="Publish"/> and
        /// <see cref="PruneStaleNear"/> both read it, and Iter-44's first draft had the caller
        /// re-derive it as a duplicated three-term expression.
        /// <para>BOTH halves are required (Iter-41): the small player radius guarantees the chunk
        /// is loaded — distance alone is not enough, because a container's chunk can unload while
        /// a co-located workbench stays (mode 2, what wrecked the old 180) — and the anchor cover
        /// guarantees a present container would have been in scope, since a base container can be
        /// player-near yet lose cover when its workbench just crossed the ~91 observation dropout
        /// (mode 1).</para>
        /// </summary>
        private bool ScanWouldSeeTile(long key)
        {
            if (!_scan.HavePlayer)
                return false;
            float dx = KeyX(key) - _scan.PlayerX,
                dz = KeyZ(key) - _scan.PlayerZ;
            if (dx * dx + dz * dz > _scan.Radius2)
                return false;
            return _scan.CoveredByLoadedAnchor(key);
        }

        private void WarnIfNoScan()
        {
            if (_scan.Active || _noScanWarned)
                return;
            _noScanWarned = true;
            // Not fatal: with no context nothing is "definitely observable", so nothing shrinks —
            // an over-count, the safe direction. But it means the protocol was broken, and this
            // subsystem's history is one of exactly such conventions going quietly false.
            Debug.LogWarning("[ItemChecklist] PossessionLedger.Publish called outside a scan (no BeginScan) — nothing will be pruned or shrunk this pass.");
        }

        // Iter-42: the Iter-28 `WorldNaturePruned` flag + `PruneByPredicate(Func<int,bool>)`
        // one-time world-nature eviction lived here and were REMOVED — an id-predicate sweep over
        // the ledger cannot distinguish a placed wild object from the same id legitimately STORED
        // in a chest (both are plain entries in the same per-tile dict), so it deleted real
        // possession on every load (the flag was never serialized, so "one-time" never held).
        // Rationale + the measured damage: see the note at the top of `PossessionScanner.Scan`.

        /// <summary>Self-heal, and the end of the scan: drop every remembered tile the scan WOULD
        /// have seen something on (<see cref="ScanWouldSeeTile"/>) yet saw nothing on at all ⇒
        /// destroyed/emptied. A real destruction always satisfies both halves of that test (you
        /// stand next to the container; its workbench is co-located), so nothing legitimate is
        /// missed. Collect-then-remove, to avoid mutating during iteration.
        /// <para>Self-gating: returns 0 during the streaming grace or with no player, so the
        /// caller no longer repeats those conditions. Iter-44: the hand-built union of the two
        /// per-tile dicts' key sets is gone — one dict means aux-only tiles (penned cattle at an
        /// anchor tile) are covered by construction.</para>
        /// <para><strong>How this differs from a <see cref="Publish"/> shrink.</strong> The two
        /// act on DISJOINT tile sets: every published key is in <paramref name="liveKeys"/>, and
        /// this method skips those. So the prune only ever deletes tiles the scan did not see at
        /// all, while a publish only ever shrinks tiles it did see. They share a premise, not a
        /// population — which is why "the publish is strictly smaller, so it adds no new risk"
        /// (written in Iter-44's first draft) does not hold as an argument. See
        /// <see cref="Publish"/>'s residual note.</para></summary>
        /// <returns>Iter-43: how many tiles were dropped, so the caller can surface it — this
        /// deletion used to be entirely unreported, even under diagnostics.</returns>
        public int PruneStaleNear(HashSet<long> liveKeys)
        {
            bool run = _scan.Active && _scan.PastGrace && _scan.HavePlayer;
            List<long> drop = null;
            if (run)
                foreach (var pair in _tiles)
                {
                    long key = pair.Key;
                    if (liveKeys.Contains(key))
                        continue; // observed this scan → not stale
                    if (!ScanWouldSeeTile(key))
                        continue; // out of range, or no loaded anchor would have observed it → keep
                    (drop ??= new List<long>()).Add(key);
                }

            // End of scan: close the context and release the caller's closure (it captures the
            // anchor list, which must not outlive the scan that built it). Every field that can
            // authorize a removal is cleared, so a stale context can only ever be MORE
            // conservative than a live one.
            _scan.Active = false;
            _scan.PastGrace = false;
            _scan.HavePlayer = false;
            _scan.CoveredByLoadedAnchor = null;

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
                {
                    // Unreachable by construction (Publish neither creates nor keeps an empty
                    // entry, LoadFrom drops one) — so if it ever fires, the invariant broke and
                    // this guard is silently dropping a tile. Say so once.
                    if (!_emptyEntryWarned)
                    {
                        _emptyEntryWarned = true;
                        Debug.LogWarning(
                            $"[ItemChecklist] ledger held an EMPTY tile entry at ({KeyX(pair.Key)},{KeyZ(pair.Key)}) — skipped while serializing."
                        );
                    }
                    continue;
                }
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

        private static bool _emptyEntryWarned;

        /// <summary>Parse a serialized ledger, replacing everything currently held.
        /// <para>Iter-43 made the outcome reportable. Discarding a whole file is a total
        /// data-loss event, and it used to happen with NO log while <c>Load</c> reported success
        /// — so a truncated or corrupted file (Wine, power loss, disk full) was indistinguishable
        /// from a legitimate version migration, and looked byte-for-byte like the Iter-42 symptom.
        /// The caller logs and records what happened; only the count can tell it apart.</para>
        /// <para>Iter-44 (review C-3) adds <paramref name="skipped"/>. This parser cannot throw
        /// on damaged input — it <c>continue</c>s past anything it does not like — so a file
        /// truncated mid-write parsed into a SUBSET and the caller still reported success, left
        /// the store writable, and the next autosave persisted the subset. Truncation almost
        /// always leaves exactly one malformed line, so counting the skips is a near-free
        /// detector. Empty lines and '#' lines are not damage and are not counted.</para>
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
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var seg = line.Split('|');
                if (seg.Length != 3)
                {
                    skipped++; // v3 line has exactly two '|'
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
                // rewritten file — where Iter-43's `_tiles[key] = entry` silently discarded the
                // first line's half (two independent dicts used to keep both).
                if (!_tiles.TryGetValue(key, out var entry))
                    entry = new TileEntry();

                bool lineOk = true;
                foreach (var pair in seg[1].Split(','))
                {
                    if (pair.Length == 0)
                        continue; // an empty segment is legal ("x,z||...")
                    int colon = pair.IndexOf(':');
                    if (
                        colon <= 0
                        || !int.TryParse(pair.Substring(0, colon), out int id)
                        || !int.TryParse(pair.Substring(colon + 1), out int cnt)
                        || !entry.RestoreContent(id, cnt)
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
                        || !entry.RestoreAux(pk, cnt)
                    )
                        lineOk = false;
                }
                if (!lineOk)
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
