using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>The header `[Filter (N)]` button — toggles the faceted popup.</summary>
    public sealed class FacetToggleButton : ButtonUIElement
    {
        public FacetedFilterWidget owner;
        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            if (owner != null) owner.TogglePopup();
        }
    }
}
