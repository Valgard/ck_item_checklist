using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// While the ItemChecklist window is open, hide the bottom-right in-game
    /// button-hint row (Tab / E / … prompts). This complements
    /// <c>UIManager.TemporarilyDisableGameplayUI()</c> (called from
    /// <see cref="UI.ItemChecklistWindow.ShowUI"/>), which scales the top-left HUD
    /// to zero but does <b>not</b> cover these hints — they live on
    /// <c>InGameButtonHintsUI</c>, whose <c>LateUpdate</c> re-asserts
    /// <c>container.SetActive(Manager.prefs.showKeyHints)</c> every frame. A one-shot
    /// hide would be overwritten, so this prefix forces the container inactive and
    /// skips the stock driver while our window is up. Verified against the decompile:
    /// <c>InGameButtonHintsUI.container</c> is a <b>public</b> GameObject (no
    /// reflection needed) and <c>LateUpdate</c> is declared on the type (so the patch
    /// binds). Released the moment our window closes / auto-hides
    /// (<c>Root.activeSelf</c> false), so the hints behave normally otherwise.
    /// </summary>
    [HarmonyPatch(typeof(InGameButtonHintsUI), "LateUpdate")]
    internal static class InGameButtonHintsSuppressPatch
    {
        [HarmonyPrefix]
        static bool Before(InGameButtonHintsUI __instance)
        {
            var w = ItemChecklistWindow.Instance;
            if (w != null && w.Root != null && w.Root.activeSelf)
            {
                if (__instance.container != null && __instance.container.activeSelf)
                    __instance.container.SetActive(false);
                return false;          // skip the stock re-assertion this frame
            }
            return true;               // otherwise: stock behaviour untouched
        }
    }
}
