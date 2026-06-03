using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// While the ItemChecklist window is open, suppress CK's ShortCutsWindow
    /// ("Tastenkürzel" help panel). CoreLib forces UIManager.isAnyInventoryShowing
    /// == true for any mod UI, which is what makes the panel relevant over our
    /// window even though it is not a real inventory.
    ///
    /// <para>This prefix is the <b>load-bearing</b> half of the suppression: the
    /// companion <see cref="InventoryShortCutsButtonSuppressPatch"/> only hides the
    /// "?" prompt (it gates ShortCutsWindow's visuals, not the toggle keybind —
    /// CK's ToggleInventoryShortcuts checks isAnyInventoryShowing directly). So the
    /// toggle key can still flip the window on; this prefix force-hides it again on
    /// the very next frame. Returning false skips the stock show-driver entirely
    /// while our window is up. Verified against the Pug.Other.dll decompile:
    /// ShortCutsWindow.LateUpdate is a `protected override` declared on the type
    /// (so the patch binds), and HideUI() (`root.SetActive(false)`) is public.</para>
    ///
    /// <para>Scoped to <c>Root.activeSelf</c>: when a vanilla inventory opens,
    /// InventoryOpenAutoHidePatch hides our window, so this releases and the panel
    /// behaves normally for real inventories.</para>
    /// </summary>
    [HarmonyPatch(typeof(ShortCutsWindow), "LateUpdate")]
    internal static class ShortCutsWindowSuppressPatch
    {
        [HarmonyPrefix]
        static bool Before(ShortCutsWindow __instance)
        {
            var w = ItemChecklistWindow.Instance;
            if (w != null && w.Root != null && w.Root.activeSelf)
            {
                __instance.HideUI();   // root.SetActive(false) — verified public
                return false;          // skip the stock show-driver this frame
            }
            return true;               // otherwise: stock behaviour untouched
        }
    }
}
