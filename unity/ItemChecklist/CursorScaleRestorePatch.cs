using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// While the ItemChecklist window is open, the HUD is hidden via
    /// <c>UIManager.TemporarilyDisableGameplayUI()</c> (it sets the gameplay-UI
    /// scale multiplier to zero, which ~50 HUD elements apply to their
    /// <c>localScale</c> each frame). CK's custom cursor sprite
    /// (<c>Manager.ui.mouse.pointerSR</c>) is one of those elements:
    /// <c>UIMouse.LateUpdate</c> scales it by the very same multiplier, so the
    /// cursor would shrink to zero and the window become unclickable.
    ///
    /// <para>The multiplier is a single global value with no per-element opt-out,
    /// so we let the disable hide the whole HUD and then restore <b>only</b> the
    /// cursor here: a postfix that runs after <c>UIMouse.LateUpdate</c> re-set the
    /// scale, forcing it back to visible while our window is open. <c>pointerSR</c>
    /// is a public field (no reflection). Released when the window closes
    /// (<c>Root.activeSelf</c> false) — the cursor returns to stock behaviour and
    /// HideUI re-enables the gameplay UI.</para>
    /// </summary>
    [HarmonyPatch(typeof(UIMouse), "LateUpdate")]
    internal static class CursorScaleRestorePatch
    {
        [HarmonyPostfix]
        static void After(UIMouse __instance)
        {
            var w = ItemChecklistWindow.Instance;
            if (w == null || w.Root == null || !w.Root.activeSelf) return;
            if (__instance.pointerSR != null)
                __instance.pointerSR.transform.localScale = UnityEngine.Vector3.one;
        }
    }
}
