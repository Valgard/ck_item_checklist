using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Hand-rolled scrollbar handle for the popup. CK's native <c>ScrollBar</c> is
    /// driven by <c>UIScrollWindow.LateUpdate</c>, which the popup deliberately does
    /// NOT use (Weg 2 — manual translate), so the handle's size/position/drag are
    /// computed here instead. Stub in Iter-24 Task 2; filled in Task 4.
    /// </summary>
    public sealed class PopupScrollHandle : MonoBehaviour
    {
        /// <summary>Toggle the scrollbar chrome on/off (off when the popup fits).</summary>
        public void SetActiveScrolling(bool active) { }

        /// <summary>Resize + reposition the handle for the current scroll state.</summary>
        public void Sync(float scrollOffset, float contentH, float viewportH) { }
    }
}
