using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Polls the local player's <c>ContainedObjectsBuffer</c> on a fixed
    /// interval, diffs against the last snapshot, and forwards every newly
    /// appearing objectID to <see cref="ChecklistState.SetOwned"/> with
    /// source <see cref="OwnSource.Pickup"/>.
    ///
    /// <para><b>Why polling instead of Harmony-patching the pickup path:</b>
    /// per <c>docs/research/spike-1-pickup-hook-target.md</c>, pickup work
    /// runs inside Burst-compiled DOTS jobs (PickUpItemSystem's PickUpJob,
    /// StartPickUpJob, …) which Harmony cannot intercept.
    /// <c>BurstDisabler</c> only exposes the managed OnUpdate, but the
    /// actual add-to-inventory writes live inside the Burst jobs. Polling
    /// the buffer is robust against game updates, has no Burst
    /// complications, and catches EVERY pickup source (ground pickup,
    /// take-from-chest, crafting output, vendor-buy, drop-from-mob)
    /// without enumerating them.</para>
    ///
    /// <para><b>Latency:</b> at <see cref="PollIntervalSec"/> seconds,
    /// the worst case between pickup and checklist update is ~0.5 s. For
    /// a non-real-time checklist this is invisible to the player.</para>
    ///
    /// <para><b>Event-storm safety:</b> a player who picks up 10 items
    /// inside one poll window produces one <see cref="Poll"/> call that
    /// fires <c>SetOwned</c> 10 times, hence up to 10 <c>StateChanged</c>
    /// events. <c>ChecklistStore</c>'s 1-second save debounce coalesces
    /// the burst into a single flush — no disk thrash.</para>
    ///
    /// <para><b>Pass-1 buffer-layout caveat (shared with InitialContainerScanner):</b>
    /// reads only <c>playerInventoryHandler.inventoryEntity</c>. If
    /// equipment slots (helm/breast/ring/…) live on separate inventory
    /// entities, items equipped directly without ever passing through the
    /// main inventory will NOT trigger a poll event. Same Live-Verification
    /// TODO as InitialContainerScanner.</para>
    /// </summary>
    public sealed class InventoryPoller
    {
        private const float PollIntervalSec = 0.5f;

        private readonly ChecklistState state;
        private readonly HashSet<int> lastSnapshot = new HashSet<int>();
        private float nextPollTime;

        public InventoryPoller(ChecklistState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Call once from the IMod Update loop. Internally rate-limits to
        /// PollIntervalSec — safe to call every frame.
        /// </summary>
        public void Tick(float currentTime)
        {
            if (currentTime < nextPollTime) return;
            nextPollTime = currentTime + PollIntervalSec;
            Poll();
        }

        /// <summary>
        /// Force an immediate poll, ignoring the interval. Used after a
        /// fresh InitialScan to seed the snapshot so the first Tick
        /// doesn't fire "pickup" events for items the scan already
        /// counted.
        /// </summary>
        public void Reseed()
        {
            lastSnapshot.Clear();
            CollectCurrentInventoryIds(lastSnapshot);
        }

        private void Poll()
        {
            try
            {
                var current = new HashSet<int>();
                CollectCurrentInventoryIds(current);

                foreach (var id in current)
                {
                    if (!lastSnapshot.Contains(id))
                        state.SetOwned(id, true, OwnSource.Pickup);
                }

                lastSnapshot.Clear();
                lastSnapshot.UnionWith(current);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] InventoryPoller.Poll failed: {e.Message}");
            }
        }

        private static void CollectCurrentInventoryIds(HashSet<int> sink)
        {
            if (Manager.main == null || Manager.main.player == null) return;

            var handler = Manager.main.player.playerInventoryHandler;
            if (handler == null) return;

            var entity = handler.inventoryEntity;
            if (entity == Entity.Null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            if (!em.HasBuffer<ContainedObjectsBuffer>(entity)) return;

            var buf = em.GetBuffer<ContainedObjectsBuffer>(entity);
            for (int i = 0; i < buf.Length; i++)
            {
                int id = (int) buf[i].objectData.objectID;
                if (id != 0) sink.Add(id);
            }
        }
    }
}
