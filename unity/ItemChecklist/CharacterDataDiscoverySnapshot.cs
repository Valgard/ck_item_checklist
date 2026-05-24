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
    /// <para>Cache key: <c>playerName</c> from <c>CharacterCustomization</c>.
    /// The active-character resolution happens in
    /// <see cref="ItemChecklistMod.Update"/> once the player spawns and
    /// exposes <c>playerName</c>.</para>
    ///
    /// <para><b>Same-name trade-off:</b> if two characters share a
    /// display name on the same save, the cache key collides and the
    /// last deserialized "Hans" wins. The cleaner solution would be to
    /// key by the unique <c>characterGuid</c> field, but we have no
    /// sandbox-safe way to map <c>Manager.main.player → characterGuid</c>:
    /// <list type="bullet">
    ///   <item><c>PlayerController</c> does not expose <c>characterGuid</c></item>
    ///   <item><c>Manager.saves.GetCharacterGuid()</c> is sandbox-blocked
    ///     (whole <c>SaveManager</c> instance-access banned)</item>
    ///   <item><c>HarmonyLib.Traverse</c> for private-field reflection is
    ///     ALSO sandbox-banned (verified live 2026-05-24:
    ///     <c>Traverse + Create + Field + GetValue</c> = 1 type + 3
    ///     member illegal refs)</item>
    /// </list>
    /// Acceptable for single-player target use-case. Mitigation: real new
    /// pickups still flow through <see cref="SaveManagerDiscoveryHook"/>
    /// and CK's <c>SetObjectAsDiscovered</c> is the source of truth — so
    /// a wrong initial snapshot self-heals on the next pickup of any
    /// not-yet-cached item.</para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        /// <summary>player-name → discovered objectIDs, populated as CK
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
