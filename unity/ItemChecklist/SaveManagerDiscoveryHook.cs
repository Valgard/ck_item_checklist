using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.SetObjectAsDiscovered</c>. CK returns
    /// <c>true</c> from this method iff the discovery was new (the HashSet.Add
    /// returned true). On every true result, mirror the objectID into
    /// <see cref="DiscoveredState"/>.
    ///
    /// <para>Iter-33 safety net: if the newly discovered item is a cooked-food epic
    /// that <see cref="ItemCatalog"/> suppressed as unreachable, it is a
    /// reachability-model violation (CK's tier gate changed, or a non-cooking source
    /// now exists). Record it durably via <see cref="PhantomViolationStore"/> (survives
    /// Player.log rotation) and warn once — never swallow it.</para>
    ///
    /// Hook validated live on 2026-05-24: 7 pickups -> 7 postfix calls,
    /// no Harmony errors.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetObjectAsDiscovered))]
    internal static class SaveManagerDiscoveryHook
    {
        [HarmonyPostfix]
        static void After(ObjectDataCD objectData, bool __result)
        {
            if (!__result) return;
            int objectId  = (int) objectData.objectID;
            int variation = objectData.variation;

            // Iter-33: a suppressed cooked-food epic phantom should never be discovered
            // (unreachable by cooking, the only source). If one is, persist it durably and
            // warn once (PhantomViolationStore.Record owns the dedup + the file write).
            var cat = ItemChecklistMod.Catalog;
            if (cat != null
                && cat.IsSuppressedCookedPhantom(objectId, variation)
                && PhantomViolationStore.Record(objectId, variation))
            {
                UnityEngine.Debug.LogWarning(
                    $"[ItemChecklist] Iter-33 invariant violated: cooked-food phantom " +
                    $"({objectId},{variation}) was discovered — believed unreachable " +
                    $"(flag=false epic). Recorded to mods/ItemChecklist/phantom-violations.txt. " +
                    $"CK's tier gate may have changed or a non-cooking source now exists.");
            }

            DiscoveredState.Instance.AddOne(objectId, variation);
        }
    }
}
