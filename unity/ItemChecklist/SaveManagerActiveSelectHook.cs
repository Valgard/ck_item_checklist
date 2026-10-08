using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.SetCharacterId(int id)</c>.
    ///
    /// <para>Pugstorm's sandbox blocks every direct read-path from our
    /// mod code to the active character's GUID (see
    /// <see cref="CharacterDataDiscoverySnapshot"/> for the four
    /// alternatives we tested and found banned). This class implements a
    /// state-machine workaround that needs NO sandbox-blocked APIs in
    /// our code path — only the <c>int id</c> parameter and a string field
    /// on <c>__instance</c> of the OnAfterDeserialize hook.</para>
    ///
    /// <para>How it works — and where it holds (re-checked against the
    /// 1.3.0.5 decompile; core_keeper <c>docs/ck/harmony-and-ecs.md</c>,
    /// "Correlating private state across two methods"):</para>
    /// <list type="number">
    ///   <item>User selects char → <c>StartGame</c> calls
    ///     <c>SaveManager.SetCharacterId(id)</c>, which only stores the id;
    ///     it reads and deserializes nothing. Our postfix sets
    ///     <see cref="AwaitingActiveDeserialize"/>.</item>
    ///   <item><c>StartGame</c> then loads the scene. Nothing couples the two
    ///     methods except that ordering: since 1.3 <c>OnAfterDeserialize</c>
    ///     is only called by Unity, from <c>SaveManager.DecodeJson</c>
    ///     (<c>JsonUtility.FromJsonOverwrite</c>), and the only decode that
    ///     follows a character selection is the network one —
    ///     <c>GetCharacterDataFromSerialized</c>, called solely by the
    ///     ServerSimulation <c>StartGameRPCSystem</c> decoding the character
    ///     data a joining player sent.</item>
    ///   <item>The next <c>OnAfterDeserialize</c> (handled by
    ///     <see cref="CharacterDataDiscoverySnapshot"/>) therefore is the
    ///     active char only in a process that hosts its own world (single
    ///     player, or hosting multiplayer) — its <c>characterGuid</c> string
    ///     becomes <see cref="ActiveGuid"/>. Characters with zero discoveries
    ///     are captured too; there is no count filter.</item>
    /// </list>
    ///
    /// <para><b>Joining a remote or dedicated server — UNVERIFIED IN PLAY.</b>
    /// There the decode runs in the server process, so in this one nothing
    /// consumes the flag: <see cref="ActiveGuid"/> keeps whatever value it
    /// had (null after boot, or the previous session's character, see the
    /// negative-id note below) and the flag leaks to the next deserialize.
    /// Derived from the decompile only; no play test has been done. The
    /// startup decode in <c>SaveManager.Init</c> does not cover it: it reads
    /// every character file once, before any <c>SetCharacterId</c>.</para>
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetCharacterId))]
    internal static class SaveManagerActiveSelectHook
    {
        /// <summary>The active character's GUID (string form), or
        /// <c>null</c> if no char selected (main menu).</summary>
        public static string ActiveGuid { get; internal set; }

        /// <summary>Set true by SetCharacterId postfix, reset to false
        /// by the OnAfterDeserialize postfix once it has captured the
        /// active char's guid.</summary>
        internal static bool AwaitingActiveDeserialize;

        [HarmonyPostfix]
        static void After(int id)
        {
            if (id < 0)
            {
                // Clear state on a negative id. Note: no caller in 1.3.0.5 passes
                // one — returning to the menu resets SaveManager._characterId
                // directly in OnSceneUnload — so this branch does not fire there
                // and ActiveGuid carries over into the next session.
                ActiveGuid = null;
                AwaitingActiveDeserialize = false;
                return;
            }
            // A specific char slot was selected. The next OnAfterDeserialize
            // is for THIS char only when this process hosts the world (its
            // server world decodes the player's data); see the summary.
            AwaitingActiveDeserialize = true;
        }
    }
}
