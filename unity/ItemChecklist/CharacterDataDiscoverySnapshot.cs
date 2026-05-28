using System.Collections.Generic;
using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>.
    ///
    /// <para>Timing reality (verified live 2026-05-24): CK fires this for
    /// ALL character slots on boot / char-select / world-load. EVERY fire
    /// happens BEFORE <c>Manager.main.player</c> is spawned. We cannot
    /// filter live; we cache and resolve later.</para>
    ///
    /// <para>Cache key: <c>characterGuid</c> from <c>CharacterData</c>.
    /// The active-character resolution happens in
    /// <see cref="ItemChecklistMod.Update"/> via the
    /// <c>SaveManagerActiveSelectHook.ActiveGuid</c> set by the
    /// sequential <c>SetCharacterId</c> → file-read → <c>JsonOverwrite</c>
    /// → <c>OnAfterDeserialize</c> path.</para>
    ///
    /// <para><b>History:</b> the original Iter-3.6 cache keyed on
    /// <c>playerName</c> from <c>CharacterCustomization</c> — vulnerable
    /// to same-name collisions on multi-char saves ("last deserialized
    /// 'Hans' wins"). Subsequent migration to <c>characterGuid</c>
    /// removes the collision risk. Earlier attempts to use sandbox-banned
    /// alternatives all failed: <c>PlayerController.characterGuid</c>
    /// does not exist, <c>Manager.saves.GetCharacterGuid()</c> is
    /// instance-access-banned, <c>HarmonyLib.Traverse</c> on the private
    /// <c>characterData[]</c> field gives 1 type + 3 member illegal refs,
    /// and <c>EntityManager.HasComponent&lt;CharacterGuidCD&gt;</c> gives
    /// 1 namespace + 1 type + 1 member illegal refs (all verified live
    /// 2026-05-24). The breakthrough was reading
    /// <c>__instance.characterGuid</c> directly off the patched-instance
    /// — a plain field access that is sandbox-safe.</para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        /// <summary>characterGuid → packed (objectId, variation) keys,
        /// populated as CK deserializes each character slot. Read by
        /// <see cref="ItemChecklistMod.Update"/> once the active player
        /// spawns and <c>SaveManagerActiveSelectHook.ActiveGuid</c>
        /// is set.</summary>
        internal static readonly Dictionary<string, long[]> Cache = new Dictionary<string, long[]>();

        [HarmonyPostfix]
        static void After(CharacterData __instance)
        {
            if (__instance == null || __instance.discoveredObjects2 == null) return;

            string guid = __instance.characterGuid;
            if (string.IsNullOrEmpty(guid)) return;

            int count = __instance.discoveredObjects2.Count;
            long[] packedKeys;
            if (count == 0)
            {
                packedKeys = System.Array.Empty<long>();
            }
            else
            {
                packedKeys = new long[count];
                for (int i = 0; i < count; i++)
                {
                    var record = __instance.discoveredObjects2[i];
                    packedKeys[i] = DiscoveredState.PackKey((int) record.objectID, record.variation);
                }
            }

            // Cache by guid for later lookup.
            Cache[guid] = packedKeys;

            // Active-char detection: if SetCharacterId(id) was just called,
            // this deserialize is for the active char (per CK's sequential
            // code path: SetCharacterId → file read → JsonOverwrite →
            // OnAfterDeserialize on that specific instance).
            if (SaveManagerActiveSelectHook.AwaitingActiveDeserialize)
            {
                SaveManagerActiveSelectHook.ActiveGuid = guid;
                SaveManagerActiveSelectHook.AwaitingActiveDeserialize = false;
            }
        }
    }
}
