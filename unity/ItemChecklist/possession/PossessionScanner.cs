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
        public static PossessionView Scan(PossessionLedger ledger, float radius, bool allowPrune)
        {
            var world = ResolveWorld();
            if (world == null) return PossessionView.Empty;
            var em = world.EntityManager;

            // Anchors.
            var anchors = new List<Vector2>();
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
                    anchors.Add(new Vector2(p.x, p.z));
                }
            }

            using var invQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ContainedObjectsBuffer>(),
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var ents = invQuery.ToEntityArray(Allocator.TempJob);

            var carried = new Dictionary<int, int>();
            var liveKeys = new HashSet<long>();
            float r2 = radius * radius;
            Vector2 playerPos = default;
            bool havePlayer = false;

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var od = em.GetComponentData<ObjectDataCD>(e);
                int id = (int)od.objectID;

                if (od.objectID == ObjectID.Player)
                {
                    AddBuffer(em, e, carried);
                    var pp = em.GetComponentData<LocalTransform>(e).Position;
                    playerPos = new Vector2(pp.x, pp.z);
                    havePlayer = true;
                    continue;
                }
                if (em.HasComponent<CraftingCD>(e)) continue;
                if (!em.HasComponent<MineableCD>(e)) continue;
                if (PossessionClassifier.IsLockedChest(id)) continue;
                var info = PugDatabase.GetObjectInfo(od.objectID, 0);
                if (info == null || (int)info.objectType != PossessionClassifier.PlaceablePrefab) continue;

                var pos = em.GetComponentData<LocalTransform>(e).Position;
                if (!WithinAnchor(anchors, pos.x, pos.z, r2)) continue;

                int tx = Mathf.RoundToInt(pos.x), tz = Mathf.RoundToInt(pos.z);
                long key = PossessionLedger.Key(tx, tz);
                var contents = new Dictionary<int, int>();
                AddBuffer(em, e, contents);
                ledger.SetLiveContainer(key, contents);
                liveKeys.Add(key);
            }

            // Self-heal: drop remembered containers inside the load bubble that we
            // did NOT re-observe (destroyed / anchor removed). 180 < ImmediateLoadRadius
            // (200), so an in-bubble container is loaded — BUT only once streaming has
            // settled (allowPrune gates the post-load window; see the doc comment).
            const float LoadRadius = 180f;
            if (allowPrune && havePlayer) ledger.PruneStaleNear(playerPos.x, playerPos.y, LoadRadius, liveKeys);

            ledger.SetCarried(carried);
            return ledger.BuildView(liveKeys);
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

        private static void AddBuffer(EntityManager em, Entity e, Dictionary<int, int> totals)
        {
            var buf = em.GetBuffer<ContainedObjectsBuffer>(e);
            for (int j = 0; j < buf.Length; j++)
            {
                var item = buf[j];
                if (item.objectID == ObjectID.None) continue;
                int id = (int)item.objectID;
                int add = item.amount > 0 ? item.amount : 1;
                totals[id] = (totals.TryGetValue(id, out var c) ? c : 0) + add;
            }
        }
    }
}
