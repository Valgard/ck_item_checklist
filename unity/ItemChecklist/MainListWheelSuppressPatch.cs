using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// Iter-24 gap-F (wheel ownership): while a dropdown/filter popup owns the mouse
    /// wheel — i.e. it is open AND the cursor is over its panel — suppress CK's
    /// <see cref="UIScrollWindow"/> scroll step so the wheel scrolls ONLY the popup,
    /// not the main list behind it.
    ///
    /// <para>The popup does not use a <c>UIScrollWindow</c> (Iter-24 scrolls it by
    /// manual translate — "Weg 2"), so this patch only ever affects the main list.
    /// The condition is computed fresh inside the prefix (it reads the current cursor
    /// position), so there is no LateUpdate-ordering staleness. Returning false skips
    /// the stock <c>UpdateScroll</c> for that frame; the next frame, once the popup
    /// closes or the cursor leaves it, scrolling resumes normally.</para>
    ///
    /// <para>Harmony runs in trusted <c>0Harmony.dll</c>, so patching is sandbox-safe;
    /// the ownership check routes through <see cref="PopupWidget.OpenPopupCapturesWheel"/>
    /// (→ the already sandbox-verified <c>Manager.camera</c> cursor read).</para>
    /// </summary>
    [HarmonyPatch(typeof(UIScrollWindow), "UpdateScroll")]
    internal static class MainListWheelSuppressPatch
    {
        [HarmonyPrefix]
        static bool Before() => !PopupWidget.OpenPopupCapturesWheel();
    }
}
