using System.Collections.Generic;
using Pug.Automation; // MineableCD
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Reads the live ECS world each refresh: classifies inventory entities, writes
    /// the contents of currently-loaded counted containers into the per-(x,z) ledger,
    /// and returns the merged possession view (carried + live-or-remembered storage).
    /// </summary>
    internal static class PossessionScanner
    {
        // Pick the world holding the inventories (ServerWorld in SP) by max count of
        // ContainedObjectsBuffer entities — never hardcode the name.
        private static World ResolveWorld()
        {
            World best = null;
            // Iter-43: start at 0, not -1. At -1 a world with ZERO ContainedObjectsBuffer entities
            // still won the max-count pick, so a wrong-world selection was possible and equally
            // silent. There is no legitimate case for picking an empty one: the player entity
            // itself carries a ContainedObjectsBuffer, so the real world always has >= 1 once a
            // character exists. No candidate now means "no world yet" — reported by the caller.
            int bestCount = 0;
            foreach (var w in World.All)
            {
                if (w == null || !w.IsCreated)
                    continue;
                using var q = w.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ContainedObjectsBuffer>());
                int n = q.CalculateEntityCount();
                if (n > bestCount)
                {
                    bestCount = n;
                    best = w;
                }
            }
            return best;
        }

        /// <summary>Update the ledger from the live world and return the merged view.
        /// `allowPrune` MUST be false until the world has been stably loaded for a
        /// grace period: right after a world load/teleport the chunks stream in
        /// asynchronously, so a container near the player may be absent from the query
        /// for a few seconds — pruning then would wrongly delete real (just-not-yet-
        /// streamed) storage and overwrite the persisted file with the loss.</summary>
        public static PossessionView Scan(PossessionLedger ledger, PetCollection pets, float radius, bool allowPrune)
        {
            bool diag = ModConfig.Diagnostics;
            float dT0 = diag ? Time.realtimeSinceStartup : 0f;
            var world = ResolveWorld();
            if (world == null)
            {
                // Iter-43: was a bare early-return that logged NOTHING, ever, even under
                // diagnostics — every row then shows an em-dash and the Possession counter reads
                // 0 / M with no explanation anywhere. Display-only (with no world there is no
                // scan, and the prune is skipped via havePlayer), so one warning per session is
                // the right weight — not a durable incident.
                if (!_worldNullWarned)
                {
                    _worldNullWarned = true;
                    Debug.LogWarning(
                        "[ItemChecklist] possession scan skipped: no ECS world resolved. Owned counts will "
                            + "read 0 until a world is available. (Harmless on the main menu / during a load.)"
                    );
                }
                return PossessionView.Empty;
            }
            var em = world.EntityManager;
            float dTWorld = diag ? Time.realtimeSinceStartup : 0f;

            // COUNT PATHS — the canonical numbering, used by every comment here and in
            // docs/architecture.md § possession. Do not renumber:
            //   #1 = carried        (the player's own ContainedObjectsBuffer; never persisted)
            //   #2 = container contents (AddBuffer — legitimate stored possession)
            //   #3 = the placed object itself (AddOne — gated by IsWorldNature)
            // #2 and #3 both write the SAME per-tile dict in the ledger, which records no
            // provenance — that missing distinction is what Iter-42 below is about.

            // Iter-42: the Iter-28 one-time world-nature eviction used to run here, gated on a
            // `WorldNaturePruned` ledger flag. It was REMOVED because it destroyed real possession:
            // the flag lived only in memory (Serialize never wrote it), so a ledger freshly read
            // from disk always started `false` and the "one-time" sweep ran on EVERY world load —
            // and `PruneByPredicate` cannot tell path #3 (the placed object, which SHOULD be
            // evicted) from path #2 (the same id STORED in a chest, legitimate possession), since
            // both land in the same per-tile dict. So every load wiped stored nature (measured on a
            // real save: 21 ids / 2677 units, incl. 1129 stored Stalagmite + 598 Mushroom). At base
            // the live scan wrote it straight back — invisible; loading FAR from base left it gone
            // until the player returned, and the next autosave persisted the loss.
            // Removing it is safe, not merely a lesser evil: the Iter-28 write gate below keeps
            // path-#3 nature out of the ledger at the source (with one narrow exception — the
            // locked-chest / boss-statue branch AddOnes before `info` is fetched, so it never
            // consults IsWorldNature; those ids are deliberately owned furniture), and the
            // Iter-31/41 v2→v3 discard migrations dropped every pre-gate ledger, so no v3 ledger
            // can hold a path-#3 nature backlog.
            // If an id is ever ADDED to the blacklist, existing ledgers keep their path-#3 entries
            // for it (an over-count — the safe direction, never a loss) until either mechanism
            // clears them on a later visit: on a tile that is still observed, `SetLiveContainer`
            // replaces the tile's whole dict, so the id is simply not written back (the dominant
            // case); on a tile that held nothing else, `PruneStaleNear` drops the tile once the
            // player is within PruneRadius of THAT tile and it is anchor-covered. Neither deletes
            // by id — and note the prune needs the tile to stay anchor-covered, so if its
            // workbench is gone (or AnchorRadius was lowered past it) the entry simply persists.

            // Anchors = WORKBENCHES + the crafting stations standing within a workbench's
            // radius. Iter-31: a base is SEEDED by a workbench — the first thing a player
            // builds and what a real base is built around; CK places none in world structures.
            // Around it, the base's other stations (seed extractors, campfires, furnaces) also
            // anchor — but ONLY when near a workbench, so the SAME campfire in a workbench-less
            // abandoned camp does NOT anchor, and the camp's loot chest + surrounding nature/
            // boulders stop counting as owned. Replaces the old "any CraftingCD + ≥2-cluster"
            // heuristic, which mistook world structures (campfire + seed extractor) for bases.
            // Validated against a real save: 11 workbenches all at base, 0 in any remote cluster.
            var stations = new List<Vector2>();
            var workbenches = new List<Vector2>();
            using (
                var anchorQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<CraftingCD>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<ObjectDataCD>()
                )
            )
            using (var anchorEnts = anchorQuery.ToEntityArray(Allocator.TempJob))
            {
                for (int i = 0; i < anchorEnts.Length; i++)
                {
                    var od = em.GetComponentData<ObjectDataCD>(anchorEnts[i]);
                    if (od.objectID == ObjectID.Player)
                        continue;
                    var p = em.GetComponentData<LocalTransform>(anchorEnts[i]).Position;
                    var v = new Vector2(p.x, p.z);
                    stations.Add(v);
                    if (PossessionClassifier.IsWorkbench((int)od.objectID))
                        workbenches.Add(v);
                }
            }

            // A station anchors the base iff it stands within AnchorRadius of a workbench (a
            // workbench is trivially within 0 of itself, so it always anchors). The link is to
            // WORKBENCHES only — never station→station — so the base cannot chain out to a far
            // structure. No workbench loaded → no base here → nothing counted.
            float wr2 = radius * radius;
            var anchors = new List<Vector2>();
            if (workbenches.Count > 0)
                foreach (var s in stations)
                    if (WithinAnchor(workbenches, s.x, s.y, wr2))
                        anchors.Add(s);
            float dTAnchors = diag ? Time.realtimeSinceStartup : 0f;

            // ALL placed entities (not just containers) so the placed object itself
            // counts — a workbench/torch/decoration is owned even with no inventory.
            using var objQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>());
            using var ents = objQuery.ToEntityArray(Allocator.TempJob);
            // Bulk-copy the two components read for EVERY entity (chunk-sequential memcpy)
            // instead of a per-entity GetComponentData random chunk lookup in the loop.
            // The three arrays are index-aligned: same query, captured back-to-back with no
            // structural change between, so ents[i]/ods[i]/xforms[i] are the same entity.
            // Per-entity `em` access then remains only for the player + gate-passers
            // (HasComponent/GetBuffer/PetOwner), i.e. the gated minority — not all N.
            using var ods = objQuery.ToComponentDataArray<ObjectDataCD>(Allocator.TempJob);
            using var xforms = objQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var carried = new Dictionary<int, int>();
            var carriedAux = new Dictionary<long, int>(); // Iter-41: live carried + active-pet skins/colours
            var auxScan = new Dictionary<long, Dictionary<long, int>>(); // per-tile remembered aux (pets/cattle/paint)
            var liveKeys = new HashSet<long>();
            // Iter-43: tiles where a CONTAINER entity was actually observed this scan. Only those
            // can confirm a tile's stored contents, so only they may shrink the remembered dict —
            // see PossessionLedger.SetLiveContainer.
            var containerTiles = new HashSet<long>();
            float r2 = radius * radius;
            Vector2 playerPos = default;
            bool havePlayer = false;

            // Accumulate per tile (x,z): multiple counted entities can share a tile
            // (e.g. a torch standing on a mannequin's tile). Their contents MERGE — an
            // earlier SetLiveContainer must not be overwritten by the next entity on the
            // same tile (that lost the mannequin's displayed armor → counted 0).
            var scan = new Dictionary<long, Dictionary<int, int>>();
            int dNear = 0;

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var od = ods[i]; // bulk array, not GetComponentData(e)
                int id = (int)od.objectID;
                var pos = xforms[i].Position; // bulk array, not GetComponentData(e)

                if (od.objectID == ObjectID.Player)
                {
                    if (em.HasComponent<ContainedObjectsBuffer>(e))
                        AddBuffer(em, e, carried, carriedAux);
                    // Iter-16.1: the active/summoned pet is a live entity, NOT in the
                    // player's ContainedObjectsBuffer — count it explicitly so it isn't
                    // undercounted (the Iter-20-deferred Terrier 7-vs-8 bug).
                    if (em.HasComponent<PetOwnerCD>(e))
                    {
                        var owner = em.GetComponentData<PetOwnerCD>(e);
                        if (
                            owner.PetEntity != Entity.Null
                            && em.Exists(owner.PetEntity)
                            && em.HasComponent<PetCD>(owner.PetEntity)
                            && em.HasComponent<ObjectDataCD>(owner.PetEntity)
                        )
                        {
                            var pod = em.GetComponentData<ObjectDataCD>(owner.PetEntity);
                            var pcd = em.GetComponentData<PetCD>(owner.PetEntity);
                            int skin = InventoryHandler.TryGetExtraInventoryData<PetSkinCD>(pcd.inventoryAuxDataIndex, out var sd) ? sd.skinIndex : 0;
                            long pk = DiscoveredState.PackKey((int)pod.objectID, skin);
                            carriedAux[pk] = (carriedAux.TryGetValue(pk, out var pc) ? pc : 0) + 1;
                        }
                    }
                    playerPos = new Vector2(pos.x, pos.z);
                    havePlayer = true;
                    continue;
                }

                // Cheap range gate first → DB/type checks only for near-anchor entities.
                if (!WithinAnchor(anchors, pos.x, pos.z, r2))
                    continue;
                if (diag)
                    dNear++;

                long key = PossessionLedger.Key(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));

                // Iter-16.3/17: a live cattle animal is a Creature ECS entity (not
                // PlaceablePrefab), so it fails the furniture gate below. Near a workbench anchor
                // it is "in my pen" → count it, credited per (ADULT species, colour variation) so
                // each of the species' 5 colour slots shows its own owned count (a baby calf ticks
                // the adult at its colour). Wild animals roam far from any anchor → excluded by
                // WithinAnchor above. Spoiler-gated on per-colour discovery (OwnedCount).
                // Iter-41: a penned cow WANDERS, so keying its colour aux by its transient tile
                // accumulated a stale entry per visited tile (a per-colour over-count in the
                // 48..~91 ring, where old tiles are neither pruned nor reconciled). Key it by its
                // NEAREST loaded ANCHOR tile instead — a stable per-pen location (anchors don't
                // move) — so a moving cow maps to the SAME tile each scan (SetLiveAux replaces, no
                // accumulation) while still riding the per-tile remember/persist/prune: the anchor
                // tile stays remembered while away, and is reconciled/pruned once the pen empties
                // and the player is near it.
                if (em.HasComponent<CattleCD>(e))
                {
                    long cckey = DiscoveredState.PackKey(CattleRegistry.AdultOf(id), od.variation);
                    var a = TileAux(auxScan, NearestAnchorTile(anchors, pos.x, pos.z, key));
                    a[cckey] = (a.TryGetValue(cckey, out var cc) ? cc : 0) + 1;
                    continue;
                }

                // Locked chests + boss statues: count the placed OBJECT as owned, but
                // NOT its contents. A locked chest is placeable furniture the player
                // owns, yet its loot is unknown until opened; a boss statue is typed
                // NonUsable + not Mineable so it would fail the generic filter below.
                if (PossessionClassifier.IsLockedChest(id) || PossessionClassifier.IsBossStatue(id))
                {
                    AddOne(Tile(scan, key), id);
                    continue;
                }

                // `ObjectType.PlaceablePrefab` + near-anchor is the "placed furniture I
                // own" gate. MineableCD is NOT required — some owned placeables (a
                // training Dummy, a WayPoint) are removed via a menu, not by mining, and
                // would otherwise be missed. Type 800 already excludes DroppedItem(0),
                // NPCs(900), TheCore(0); stations fall out via !CraftingCD below.
                var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                if (info == null || (int)info.objectType != PossessionClassifier.PlaceablePrefab)
                    continue;
                if (diag)
                    DiagRecordPlaced(id, info);

                // A placed object within range. Iter-28: count the object itself ONLY when it
                // is NOT world-spawned nature. Wild nature (bushes/grass/kelp/stalagmites/ruins)
                // is excluded here so it never enters the ledger (the autosave-serialize spike);
                // it still counts via container contents (AddBuffer below) or carried. Walls/
                // torches/furniture/trophies/waypoints are kept. Stations' transient input/
                // output slots are NOT counted (the !CraftingCD guard).
                bool owned = !PossessionClassifier.IsWorldNature(id, info);
                bool isContainer = !em.HasComponent<CraftingCD>(e) && em.HasComponent<ContainedObjectsBuffer>(e);
                if (!owned && !isContainer)
                    continue; // wild nature with no storage → skip
                var tile = Tile(scan, key);
                if (owned)
                    AddOne(tile, id);
                // Iter-17: a painted/coloured placeable carries its paint colour in variation →
                // also credit per (id, colour) for the per-colour slot (live-only). variation 0
                // = base item, already counted by AddOne. Tile floors/walls aren't individual
                // entities → never reach here → "—".
                if (owned && od.variation != 0)
                {
                    long cck = DiscoveredState.PackKey(id, od.variation);
                    var a = TileAux(auxScan, key);
                    a[cck] = (a.TryGetValue(cck, out var pcc) ? pcc : 0) + 1;
                }
                if (isContainer)
                {
                    containerTiles.Add(key);
                    AddBuffer(em, e, tile, TileAux(auxScan, key));
                }
            }

            // Iter-43: a tile's remembered contents may only SHRINK when this scan actually
            // confirmed them — a container entity observed here AND past the streaming grace.
            // Otherwise (e.g. only the co-located torch was observed, its chunk still loaded while
            // the chest's is not) the remembered ids are kept. `droppedUnits` counts what a
            // confirmed shrink removed; it is reported below, because an unseen deletion is what
            // made Iter-42 invisible for a month.
            // Iter-43: capture the pre-mutation ledger size so the DIAG line can report the
            // TRANSITION rather than just the endpoint. `Containers.Count` is O(1) so the tile
            // count is always available; the pair sum costs an iteration and is diag-only.
            int lcBefore = ledger.Containers.Count;
            int lpBefore = 0;
            if (diag)
                foreach (var c in ledger.Containers)
                    lpBefore += c.Value.Count;

            int droppedUnits = 0,
                shrunkTiles = 0;
            foreach (var kv in scan)
            {
                int dropped = ledger.SetLiveContainer(kv.Key, kv.Value, allowShrink: allowPrune && containerTiles.Contains(kv.Key));
                if (dropped > 0)
                {
                    droppedUnits += dropped;
                    shrunkTiles++;
                }
                liveKeys.Add(kv.Key);
            }
            // Iter-43: publish ONLY tiles that actually produced aux. `TileAux(auxScan, key)` is
            // evaluated as an ARGUMENT at the AddBuffer call above, so auxScan gains a key for
            // EVERY container tile whether or not any aux was added — and `SetLiveAux` REPLACES,
            // so writing an empty dict is a deletion in all but name, performed here UNGATED while
            // the acknowledged deletion paths below are allowPrune-gated. That bypassed the gate
            // in exactly the case its comment describes: a tile with a co-located chest IS in
            // auxScan (empty), so ClearAux never fired for it and the empty write had already
            // wiped the remembered aux — during the post-load grace, when the cattle's archetype
            // chunk may simply not have streamed in yet. Skipping empties routes every aux removal
            // through the one gated path below.
            foreach (var kv in auxScan)
            {
                if (kv.Value.Count == 0)
                    continue;
                ledger.SetLiveAux(kv.Key, kv.Value, allowShrink: allowPrune && containerTiles.Contains(kv.Key));
                liveKeys.Add(kv.Key);
            }

            // Iter-41: a live tile re-observed WITHOUT aux this scan must drop any stale remembered
            // aux — a mobile penned cattle that moved off a tile still kept live by a co-located
            // chest/placeable (whose non-container path never refreshes that tile's aux). Only LIVE
            // (observed) tiles are reconciled, so remembered-away aux is preserved. Prevents a
            // per-colour over-count that would not self-heal. Gated on allowPrune for the same
            // reason as PruneStaleNear below: during the post-load streaming grace a co-located
            // creature may not have streamed in yet, and this is a DELETION path — deleting its
            // remembered aux then would be the unsafe direction (a transient over-count is safer).
            // Iter-43: "present but empty" counts as absent — see the skip above; without this the
            // predicate would read "aux was observed here" for a tile that produced none.
            if (allowPrune)
                foreach (var key in liveKeys)
                    if (!auxScan.TryGetValue(key, out var observedAux) || observedAux.Count == 0)
                        ledger.ClearAux(key);

            // Self-heal: drop a remembered container/aux tile the player is close enough to that
            // it is DEFINITELY still OBSERVABLE (within PruneRadius) yet was not seen this snapshot
            // — genuinely destroyed/emptied. Iter-41 grounded PruneRadius in the CK code AND an
            // in-game measurement (they disagree, and the smaller one governs):
            //   • Hard load floor (decompile, Pug.Base PLAYER_DISTANCE_TO_LOAD/UNLOAD): the server
            //     force-loads chunks within 200 tiles of the player and unloads past 300 — NOT
            //     shrinkable by any setting (defaultSimDistance/SimulationDistance are dead) — so a
            //     placed container within 200 is a live, queryable entity.
            //   • BUT empirically (DIAG maxSeen/minGhost) base containers left the *observed* scan
            //     set at ~91-115 — well below 200, matching no named constant (best explanation:
            //     DOTS archetype-chunk unload granularity + camera-frame offset). The prune infers
            //     "unobserved ⇒ destroyed", so it must stay below where observation is RELIABLE
            //     (~91), NOT below the 200 load radius. The old 180 conflated "loaded" (200) with
            //     "observed" (~91) and pruned loaded-but-unobserved containers in the 91-180 band
            //     as the player walked away — the K collapse this iteration fixes.
            // 48 sits well below BOTH (~91 observed dropout and the 200 hard floor) and far exceeds
            // destruction range (you must stand next to a container to destroy it). It is
            // INDEPENDENT of AnchorRadius (which only shares the 48 *default* and is user-settable
            // to 96): the prune must stay under the observed-dropout regardless of AnchorRadius.
            const float PruneRadius = 48f; // the "loaded" half (player-near); the closure is the "anchor-covered" half
            int prunedTiles = 0;
            if (allowPrune && havePlayer)
                prunedTiles = ledger.PruneStaleNear(
                    playerPos.x,
                    playerPos.y,
                    PruneRadius,
                    liveKeys,
                    key => WithinAnchor(anchors, PossessionLedger.KeyX(key), PossessionLedger.KeyZ(key), r2)
                );

            // Iter-43: one anomaly report, chosen to be false-positive-free. A confirmed shrink is
            // NORMAL — emptying a chest legitimately drops its whole content — so neither a unit
            // threshold nor "any shrink" can be the trigger. But losing units on MANY tiles inside
            // a single 3 s scan is not normal play: nobody empties five chests at once, while the
            // Iter-42 sweep hit exactly 5 tiles in one pass. That shape is the signal.
            if (shrunkTiles >= 5)
                PossessionIncidentStore.Record(
                    PossessionIncidentStore.Shrink,
                    PossessionIncidentStore.Shrink + ":session",
                    "tiles=" + shrunkTiles + " units=" + droppedUnits + " ledgerC=" + lcBefore + "->" + ledger.Containers.Count,
                    $"{droppedUnits} owned unit(s) dropped from {shrunkTiles} container tiles in a single scan. "
                        + "That is expected if you just emptied several containers at once — otherwise it may be a "
                        + "tracking bug; please report this file."
                );

            // Iter-16.1: any skin currently owned (carried/active/container) is collected
            // forever. Iterate the live carried aux + every scanned tile's aux.
            if (pets != null)
            {
                void MarkFrom(Dictionary<long, int> a)
                {
                    foreach (var kv in a)
                        if (kv.Value > 0)
                        {
                            int oid = DiscoveredState.KeyObjectId(kv.Key);
                            int sub = DiscoveredState.KeyVariation(kv.Key);
                            // Iter-41: aux now also carries cattle/paint keys; only pet-skin keys
                            // belong in the PetCollection ledger (cattle/paint use CK discovery).
                            if (ItemChecklistMod.Catalog != null && ItemChecklistMod.Catalog.IsPetSkinEntry(oid, sub))
                                pets.MarkCollected(oid, sub);
                        }
                }
                MarkFrom(carriedAux);
                foreach (var kv in auxScan)
                    MarkFrom(kv.Value);
            }

            ledger.SetCarried(carried);
            ledger.SetCarriedAux(carriedAux);
            float dTLoop = diag ? Time.realtimeSinceStartup : 0f;
            var view = ledger.BuildView(liveKeys);
            if (diag)
            {
                float dTEnd = Time.realtimeSinceStartup;
                int lc = 0,
                    lp = 0;
                foreach (var c in ledger.Containers)
                {
                    lc++;
                    lp += c.Value.Count;
                }
                Debug.Log(
                    $"[ItemChecklist] DIAG scan total={(dTEnd - dT0) * 1000f:F1}ms "
                        + $"(world={(dTWorld - dT0) * 1000f:F1} setup={(dTAnchors - dTWorld) * 1000f:F1} "
                        + $"loop={(dTLoop - dTAnchors) * 1000f:F1} build={(dTEnd - dTLoop) * 1000f:F1}) "
                        + $"interval={ModConfig.ScanIntervalSeconds:F0}s dt={(_lastScanRt > 0f ? dT0 - _lastScanRt : 0f):F2}s "
                        // Iter-43: report the TRANSITION, not just the endpoint. The old line
                        // printed ledgerC/pairs only after every mutation, so a collapse was
                        // visible solely by hand-diffing two consecutive lines and the pre-scan
                        // value was never shown at all. A single line reading
                        // "ledgerC=505->505 lostUnits=2677" would have made Iter-42 self-evident
                        // on the first far-from-base load instead of costing a month.
                        + $"ledgerC={lcBefore}->{lc} pairs={lpBefore}->{lp} pruned={prunedTiles} "
                        + $"shrunk={shrunkTiles} lostUnits={droppedUnits} "
                        + $"ents={ents.Length} near={dNear} anchors={anchors.Count}"
                );
                _lastScanRt = dT0; // Iter-38.1: anchor the next scan's dt=
                DiagDumpObjectsOnce();
            }
            return view;
        }

        // --- Diagnostics (config-gated via ModConfig.Diagnostics; default off, zero
        // overhead when off). The scan logs per-scan timing + ledger size, the save logs
        // serialize/write (PossessionStore), and once per launch the distinct counted placed
        // objects are dumped with their tags + IsWorldNature verdict — so a `nature=False`
        // placeable that is obviously wild nature (leaking past the blacklist in a new biome)
        // is visible and its tag/ID can be added to PossessionClassifier.
        private static bool _diagObjectsDumped;
        private static bool _worldNullWarned; // Iter-43: one warning per session, not per scan
        private static float _lastScanRt; // Iter-38.1: realtime of the previous diag-logged scan, for the dt= cadence field
        private static readonly Dictionary<int, (int count, string sig)> _diagObjects = new();

        private static void DiagRecordPlaced(int id, ObjectInfo info)
        {
            if (_diagObjectsDumped)
                return;
            if (_diagObjects.TryGetValue(id, out var r))
            {
                _diagObjects[id] = (r.count + 1, r.sig);
                return;
            }
            string tags = "";
            if (info.tags != null)
                foreach (var t in info.tags)
                    tags += (int)t + ",";
            bool craft = info.requiredObjectsToCraft != null && info.requiredObjectsToCraft.Count > 0;
            bool nature = PossessionClassifier.IsWorldNature(id, info);
            _diagObjects[id] = (1, $"craft={craft} nature={nature} tags=[{tags}]");
        }

        private static void DiagDumpObjectsOnce()
        {
            if (_diagObjectsDumped || _diagObjects.Count == 0)
                return;
            _diagObjectsDumped = true;
            foreach (var kv in _diagObjects)
                Debug.Log($"[ItemChecklist] DIAG placed id={kv.Key} count={kv.Value.count} {kv.Value.sig}");
            Debug.Log($"[ItemChecklist] DIAG placed distinct={_diagObjects.Count} (nature=True ones are excluded from path #3)");
        }

        private static bool WithinAnchor(List<Vector2> anchors, float x, float z, float r2)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                float dx = anchors[i].x - x,
                    dz = anchors[i].y - z;
                if (dx * dx + dz * dz <= r2)
                    return true;
            }
            return false;
        }

        private static void AddBuffer(EntityManager em, Entity e, Dictionary<int, int> totals, Dictionary<long, int> aux)
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(e);
            for (int j = 0; j < buf.Length; j++)
            {
                var item = buf[j];
                if (item.objectID == ObjectID.None)
                    continue;
                int id = (int)item.objectID;

                // Iter-16.3: a caged animal in storage is the cattle ObjectID + auxData
                // (verified in-game: a caged RolyPoly appears as objectID 1303). Credit the
                // ADULT species (folding a caged baby); non-stackable → 1 per slot.
                if (PugDatabase.HasComponent<CattleCD>(item.objectID))
                {
                    int adult = CattleRegistry.AdultOf(id);
                    totals[adult] = (totals.TryGetValue(adult, out var cc) ? cc : 0) + 1;
                    continue;
                }

                // Iter-16.1: a pet item carries its skin in PetSkinCD aux data. Tally
                // per-(objectId, skinIndex) so each skin's owned count is separate.
                if (PugDatabase.HasComponent<PetCD>(item.objectID))
                {
                    int skin = InventoryHandler.TryGetExtraInventoryData<PetSkinCD>(item, out var sd) ? sd.skinIndex : 0;
                    long pk = DiscoveredState.PackKey(id, skin);
                    aux[pk] = (aux.TryGetValue(pk, out var pc) ? pc : 0) + 1;
                }
                // `amount` is double-purposed: stack size for stackable items, but
                // DURABILITY for equipment (tools/armor). So a single full-durability
                // hat would otherwise count as e.g. 50. Mirror CK's GetTotalAmount:
                // stackable → amount, non-stackable → 1 per occupied slot. Look up
                // stackability at variation 0 — it does not vary by variation, and a
                // non-existent (objectID, variation) combo returns null (which would
                // wrongly fall through to the durability branch for some skins).
                var slotInfo = PugDatabase.GetObjectInfo(item.objectID, 0);
                int add = (slotInfo != null && slotInfo.isStackable) ? (item.amount > 0 ? item.amount : 1) : 1;
                totals[id] = (totals.TryGetValue(id, out var c) ? c : 0) + add;
            }
        }

        private static Dictionary<int, int> Tile(Dictionary<long, Dictionary<int, int>> scan, long key)
        {
            if (!scan.TryGetValue(key, out var d))
            {
                d = new Dictionary<int, int>();
                scan[key] = d;
            }
            return d;
        }

        private static Dictionary<long, int> TileAux(Dictionary<long, Dictionary<long, int>> auxScan, long key)
        {
            if (!auxScan.TryGetValue(key, out var d))
            {
                d = new Dictionary<long, int>();
                auxScan[key] = d;
            }
            return d;
        }

        // Iter-41: tile of the anchor nearest to (x,z), used to give a WANDERING penned cow a
        // STABLE per-pen aux tile (its own tile changes every scan → per-tile accumulation). The
        // cattle branch only runs after WithinAnchor passed, so `anchors` is non-empty there;
        // `fallback` (the cow's own tile) is a defensive default only.
        private static long NearestAnchorTile(List<Vector2> anchors, float x, float z, long fallback)
        {
            long best = fallback;
            float bestD = float.MaxValue;
            for (int i = 0; i < anchors.Count; i++)
            {
                float dx = anchors[i].x - x,
                    dz = anchors[i].y - z;
                float d = dx * dx + dz * dz;
                if (d < bestD)
                {
                    bestD = d;
                    best = PossessionLedger.Key(Mathf.RoundToInt(anchors[i].x), Mathf.RoundToInt(anchors[i].y));
                }
            }
            return best;
        }

        private static void AddOne(Dictionary<int, int> tile, int id) => tile[id] = (tile.TryGetValue(id, out var c) ? c : 0) + 1;
    }
}
