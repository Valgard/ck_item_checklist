using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.WriteCharacter(int)</c> — CK's actual
    /// character save-file write (<c>characterFiles[saveId].Write(EncodeJson(...))</c>),
    /// which fires on autosave AND "Save &amp; Quit". Persists the possession ledger in
    /// lockstep with CK's own character save, so the ledger reaches disk exactly when
    /// (and only when) the game saves the character — never ahead of it.
    ///
    /// <para>This is the symmetric counterpart to the
    /// <see cref="CharacterDataDiscoverySnapshot"/> / <c>OnAfterDeserialize</c> load
    /// path, and the primary persistence trigger for Iter-20.</para>
    ///
    /// <para>Why this hook exists: the original GUID-clear save only fired when CK
    /// called <c>SaveManager.SetCharacterId(-1)</c>, which a normal "Save &amp; Quit"
    /// does NOT do reliably — so the ledger never reached disk on a clean quit (the
    /// file simply stayed absent, with no save error). Hooking the real write closes
    /// that gap and aligns our cadence with CK's. The no-arg <c>WriteCharacter()</c>
    /// delegates to this <c>int</c> overload, so patching it covers both call paths.</para>
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.WriteCharacter), new[] { typeof(int) })]
    internal static class SaveManagerWriteCharacterHook
    {
        [HarmonyPostfix]
        static void After() => ItemChecklistMod.SavePossessionLedger();
    }
}
