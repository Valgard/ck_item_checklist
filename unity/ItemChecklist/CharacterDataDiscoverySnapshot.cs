using System.Collections.Generic;
using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>.
    ///
    /// <para>Timing reality (verified live 2026-05-24): CK fires this for
    /// ALL ~60 character slots on boot / char-select / world-load.
    /// EVERY fire happens BEFORE <c>Manager.main.player</c> is spawned
    /// (verified: <c>playerReady=False</c> in every DIAG line of the
    /// instrumented build). So we cannot filter by "active player" here.</para>
    ///
    /// <para>Strategy: cache every non-empty deserialized character by name
    /// into <see cref="Cache"/>. The active-character resolution happens
    /// later in <see cref="ItemChecklistMod.Update"/> — when the player
    /// finally spawns, we look up the cached ids by
    /// <c>Manager.main.player.playerName</c> and push them into
    /// <see cref="DiscoveredState"/>.</para>
    ///
    /// <para>Same-name characters last-wins. Acceptable for the single-
    /// player target use-case; if two characters share a display name on
    /// the same save, the snapshot may be wrong by one save's worth of
    /// discoveries until CK re-discovers any missing items live (which it
    /// will on the very next pickup since CK's <c>SetObjectAsDiscovered</c>
    /// is the source of truth).</para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        /// <summary>character-name → discovered objectIDs, populated as CK
        /// deserializes each character slot. Read by
        /// <see cref="ItemChecklistMod.Update"/> once the active player
        /// spawns.</summary>
        internal static readonly Dictionary<string, int[]> Cache = new Dictionary<string, int[]>();

        [HarmonyPostfix]
        static void After(CharacterData __instance)
        {
            if (__instance == null || __instance.discoveredObjects2 == null) return;

            string name;
            try { name = __instance.CharacterCustomization.name.Value; }
            catch { return; }
            if (string.IsNullOrEmpty(name)) return;

            int count = __instance.discoveredObjects2.Count;
            if (count == 0)
            {
                // Empty slot — still cache as empty-array so a no-discoveries
                // character resolves correctly (rather than missing-key).
                Cache[name] = System.Array.Empty<int>();
                return;
            }

            var ids = new int[count];
            for (int i = 0; i < count; i++)
                ids[i] = (int) __instance.discoveredObjects2[i].objectID;
            Cache[name] = ids;
        }
    }
}
