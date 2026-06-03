using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// While the ItemChecklist window is open, ESC must CLOSE the window — not open
    /// the pause menu. CK routes ESC to two independent consumers on the same key:
    /// PlayerController's CANCEL handler (closes an open inventory/UI) and
    /// MenuManager (opens PAUSE), the latter gated only by
    /// <c>MenuManager.IsPauseDisabled()</c>. That gate relies on UIManager's
    /// two-frame <c>inventoryWasActiveThisFrame</c> latch. On the FIRST open, F1
    /// (polled in IMod.Update) can open the window and ESC fire before any
    /// UIManager.Update latched the state; the CANCEL handler then closes the window
    /// (flipping isAnyInventoryShowing false) in the same frame, so IsPauseDisabled
    /// sees neither the flag nor the latch, returns false, and the pause menu opens
    /// over the just-hidden window — leaving a tangled state. Forcing IsPauseDisabled
    /// true while our window is open closes the race at the decision point, using the
    /// same open-state signal the F1 toggle trusts (<c>Root.activeSelf</c>); when the
    /// window is closed this releases and pause works normally.
    ///
    /// <para>Verified via decompile: <c>MenuManager.IsPauseDisabled</c> is private
    /// (patched by string name); <c>TemporarilyDisableGameplayUI</c> is NOT involved
    /// (it only sets the HUD scale multiplier).</para>
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "IsPauseDisabled")]
    internal static class PauseSuppressWhileChecklistOpenPatch
    {
        [HarmonyPostfix]
        static void After(ref bool __result)
        {
            var w = ItemChecklistWindow.Instance;
            if (w != null && w.Root != null && w.Root.activeSelf)
                __result = true;   // window open → ESC closes it, never opens pause
        }
    }
}
