using System.Collections.Generic;
using Pug.Automation;        // MineableCD
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
            int bestCount = -1;
            foreach (var w in World.All)
            {
                if (w == null || !w.IsCreated) continue;
                using var q = w.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ContainedObjectsBuffer>());
                int n = q.CalculateEntityCount();
                if (n > bestCount) { bestCount = n; best = w; }
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
            if (world == null) return PossessionView.Empty;
            var em = world.EntityManager;
            float dTWorld = diag ? Time.realtimeSinceStartup : 0f;

            // Iter-28: one-time eviction of pre-existing world nature from the loaded ledger.
            // The old scan persisted nature into the per-(x,z) ledger (it grew to ~5500
            // entries); PruneStaleNear's 180-tile window is far too slow to clear that backlog,
            // leaving the autosave-serialize spike. Do a full one-time sweep here, where
            // PugDatabase is ready. The scan gate below keeps the ledger nature-free from now
            // on; legitimately-stored nature re-adds itself via container contents.
            if (!ledger.WorldNaturePruned)
            {
                ledger.PruneByPredicate(itemId =>
                    PossessionClassifier.IsWorldNature(itemId, PugDatabase.GetObjectInfo((ObjectID)itemId, 0)));
                ledger.WorldNaturePruned = true;
            }

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
            using (var anchorQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CraftingCD>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ObjectDataCD>()))
            using (var anchorEnts = anchorQuery.ToEntityArray(Allocator.TempJob))
            {
                for (int i = 0; i < anchorEnts.Length; i++)
                {
                    var od = em.GetComponentData<ObjectDataCD>(anchorEnts[i]);
                    if (od.objectID == ObjectID.Player) continue;
                    var p = em.GetComponentData<LocalTransform>(anchorEnts[i]).Position;
                    var v = new Vector2(p.x, p.z);
                    stations.Add(v);
                    if (PossessionClassifier.IsWorkbench((int)od.objectID)) workbenches.Add(v);
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
                    if (WithinAnchor(workbenches, s.x, s.y, wr2)) anchors.Add(s);
            float dTAnchors = diag ? Time.realtimeSinceStartup : 0f;

            // ALL placed entities (not just containers) so the placed object itself
            // counts — a workbench/torch/decoration is owned even with no inventory.
            using var objQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
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
            var carriedAux = new Dictionary<long, int>();      // Iter-41: live carried + active-pet skins/colours
            var auxScan = new Dictionary<long, Dictionary<long, int>>();  // per-tile remembered aux (pets/cattle/paint)
            var liveKeys = new HashSet<long>();
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
                var od = ods[i];                // bulk array, not GetComponentData(e)
                int id = (int)od.objectID;
                var pos = xforms[i].Position;    // bulk array, not GetComponentData(e)

                if (od.objectID == ObjectID.Player)
                {
                    if (em.HasComponent<ContainedObjectsBuffer>(e)) AddBuffer(em, e, carried, carriedAux);
                    // Iter-16.1: the active/summoned pet is a live entity, NOT in the
                    // player's ContainedObjectsBuffer — count it explicitly so it isn't
                    // undercounted (the Iter-20-deferred Terrier 7-vs-8 bug).
                    if (em.HasComponent<PetOwnerCD>(e))
                    {
                        var owner = em.GetComponentData<PetOwnerCD>(e);
                        if (owner.PetEntity != Entity.Null && em.Exists(owner.PetEntity)
                            && em.HasComponent<PetCD>(owner.PetEntity)
                            && em.HasComponent<ObjectDataCD>(owner.PetEntity))
                        {
                            var pod = em.GetComponentData<ObjectDataCD>(owner.PetEntity);
                            var pcd = em.GetComponentData<PetCD>(owner.PetEntity);
                            int skin = InventoryHandler.TryGetExtraInventoryData<PetSkinCD>(
                                pcd.inventoryAuxDataIndex, out var sd) ? sd.skinIndex : 0;
                            long pk = DiscoveredState.PackKey((int)pod.objectID, skin);
                            carriedAux[pk] = (carriedAux.TryGetValue(pk, out var pc) ? pc : 0) + 1;
                        }
                    }
                    playerPos = new Vector2(pos.x, pos.z);
                    havePlayer = true;
                    continue;
                }

                // Cheap range gate first → DB/type checks only for near-anchor entities.
                if (!WithinAnchor(anchors, pos.x, pos.z, r2)) continue;
                if (diag) dNear++;

                long key = PossessionLedger.Key(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));

                // Iter-16.3/17: a live cattle animal is a Creature ECS entity (not
                // PlaceablePrefab), so it fails the furniture gate below. Near a clustered
                // base anchor it is "in my pen" → count it. Iter-17: credited per
                // (ADULT species, colour variation) so each of the species' 5 colour slots
                // shows its own owned count (a baby calf ticks the adult at its colour). Wild
                // animals roam far from any anchor → excluded by WithinAnchor above. The count
                // is spoiler-gated on per-colour discovery (ItemChecklistMod.OwnedCount). Live
                // per-colour, not "remembered" — mirrors pet skins. Verified in-game (1.2.1.4).
                if (em.HasComponent<CattleCD>(e))
                {
                    long cckey = DiscoveredState.PackKey(CattleRegistry.AdultOf(id), od.variation);
                    var a = TileAux(auxScan, key);
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
                if (info == null || (int)info.objectType != PossessionClassifier.PlaceablePrefab) continue;
                if (diag) DiagRecordPlaced(id, info);


                // A placed object within range. Iter-28: count the object itself ONLY when it
                // is NOT world-spawned nature. Wild nature (bushes/grass/kelp/stalagmites/ruins)
                // is excluded here so it never enters the ledger (the autosave-serialize spike);
                // it still counts via container contents (AddBuffer below) or carried. Walls/
                // torches/furniture/trophies/waypoints are kept. Stations' transient input/
                // output slots are NOT counted (the !CraftingCD guard).
                bool owned = !PossessionClassifier.IsWorldNature(id, info);
                bool isContainer = !em.HasComponent<CraftingCD>(e) && em.HasComponent<ContainedObjectsBuffer>(e);
                if (!owned && !isContainer) continue;   // wild nature with no storage → skip
                var tile = Tile(scan, key);
                if (owned) AddOne(tile, id);
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
                if (isContainer) AddBuffer(em, e, tile, TileAux(auxScan, key));
            }


            foreach (var kv in scan) { ledger.SetLiveContainer(kv.Key, kv.Value); liveKeys.Add(kv.Key); }
            foreach (var kv in auxScan) { ledger.SetLiveAux(kv.Key, kv.Value); liveKeys.Add(kv.Key); }

            // Iter-41: a live tile re-observed WITHOUT aux this scan must drop any stale remembered
            // aux — a mobile penned cattle that moved off a tile still kept live by a co-located
            // chest/placeable (whose non-container path never refreshes that tile's aux). Only LIVE
            // (observed) tiles are reconciled, so remembered-away aux is preserved. Prevents a
            // per-colour over-count that would not self-heal. Gated on allowPrune for the same
            // reason as PruneStaleNear below: during the post-load streaming grace a co-located
            // creature may not have streamed in yet, and this is a DELETION path — deleting its
            // remembered aux then would be the unsafe direction (a transient over-count is safer).
            if (allowPrune)
                foreach (var key in liveKeys) if (!auxScan.ContainsKey(key)) ledger.ClearAux(key);

            // Self-heal: drop remembered containers inside the load bubble that we did NOT
            // re-observe AND that a loaded workbench anchor covers (so they should have been
            // scanned). 180 < ImmediateLoadRadius (200) = definitely loaded; the anchor test
            // (Iter-41) prevents pruning a base container whose workbench merely unloaded.
            const float LoadRadius = 180f;
            if (allowPrune && havePlayer)
                ledger.PruneStaleNear(playerPos.x, playerPos.y, LoadRadius, liveKeys,
                    key => WithinAnchor(anchors, PossessionLedger.KeyX(key), PossessionLedger.KeyZ(key), r2));

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
                foreach (var kv in auxScan) MarkFrom(kv.Value);
            }

            ledger.SetCarried(carried);
            ledger.SetCarriedAux(carriedAux);
            float dTLoop = diag ? Time.realtimeSinceStartup : 0f;
            var view = ledger.BuildView(liveKeys);
            if (diag)
            {
                float dTEnd = Time.realtimeSinceStartup;
                int lc = 0, lp = 0;
                foreach (var c in ledger.Containers) { lc++; lp += c.Value.Count; }
                Debug.Log($"[ItemChecklist] DIAG scan total={(dTEnd - dT0) * 1000f:F1}ms " +
                    $"(world={(dTWorld - dT0) * 1000f:F1} setup={(dTAnchors - dTWorld) * 1000f:F1} " +
                    $"loop={(dTLoop - dTAnchors) * 1000f:F1} build={(dTEnd - dTLoop) * 1000f:F1}) " +
                    $"interval={ModConfig.ScanIntervalSeconds:F0}s dt={(_lastScanRt > 0f ? dT0 - _lastScanRt : 0f):F2}s " +
                    $"ledgerC={lc} pairs={lp} ents={ents.Length} near={dNear} anchors={anchors.Count}");
                _lastScanRt = dT0;   // Iter-38.1: anchor the next scan's dt=
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
        private static float _lastScanRt;   // Iter-38.1: realtime of the previous diag-logged scan, for the dt= cadence field
        private static readonly Dictionary<int, (int count, string sig)> _diagObjects = new();

        private static void DiagRecordPlaced(int id, ObjectInfo info)
        {
            if (_diagObjectsDumped) return;
            if (_diagObjects.TryGetValue(id, out var r)) { _diagObjects[id] = (r.count + 1, r.sig); return; }
            string tags = "";
            if (info.tags != null) foreach (var t in info.tags) tags += (int)t + ",";
            bool craft = info.requiredObjectsToCraft != null && info.requiredObjectsToCraft.Count > 0;
            bool nature = PossessionClassifier.IsWorldNature(id, info);
            _diagObjects[id] = (1, $"craft={craft} nature={nature} tags=[{tags}]");
        }

        private static void DiagDumpObjectsOnce()
        {
            if (_diagObjectsDumped || _diagObjects.Count == 0) return;
            _diagObjectsDumped = true;
            foreach (var kv in _diagObjects)
                Debug.Log($"[ItemChecklist] DIAG placed id={kv.Key} count={kv.Value.count} {kv.Value.sig}");
            Debug.Log($"[ItemChecklist] DIAG placed distinct={_diagObjects.Count} (nature=True ones are excluded from path #1)");
        }

        private static bool WithinAnchor(List<Vector2> anchors, float x, float z, float r2)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                float dx = anchors[i].x - x, dz = anchors[i].y - z;
                if (dx * dx + dz * dz <= r2) return true;
            }
            return false;
        }

        private static void AddBuffer(EntityManager em, Entity e, Dictionary<int, int> totals,
            Dictionary<long, int> aux)
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(e);
            for (int j = 0; j < buf.Length; j++)
            {
                var item = buf[j];
                if (item.objectID == ObjectID.None) continue;
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
                int add = (slotInfo != null && slotInfo.isStackable)
                    ? (item.amount > 0 ? item.amount : 1)
                    : 1;
                totals[id] = (totals.TryGetValue(id, out var c) ? c : 0) + add;
            }
        }

        private static Dictionary<int, int> Tile(Dictionary<long, Dictionary<int, int>> scan, long key)
        {
            if (!scan.TryGetValue(key, out var d)) { d = new Dictionary<int, int>(); scan[key] = d; }
            return d;
        }

        private static Dictionary<long, int> TileAux(Dictionary<long, Dictionary<long, int>> auxScan, long key)
        {
            if (!auxScan.TryGetValue(key, out var d)) { d = new Dictionary<long, int>(); auxScan[key] = d; }
            return d;
        }

        private static void AddOne(Dictionary<int, int> tile, int id)
            => tile[id] = (tile.TryGetValue(id, out var c) ? c : 0) + 1;
    }
}
