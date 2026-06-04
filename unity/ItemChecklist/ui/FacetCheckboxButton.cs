using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>One checkbox option row in the faceted filter popup. Carries an
    /// opaque member id (assigned by FacetedFilterWidget) and reports clicks back
    /// to the owning widget, which flips the corresponding model dimension.</summary>
    public sealed class FacetCheckboxButton : ButtonUIElement
    {
        public FacetedFilterWidget owner;
        public int memberId;          // index into the widget's flat member table
        public SpriteRenderer checkMark;   // ui_icon_requirement (shown when checked)

        public void SetChecked(bool on)
        {
            if (checkMark != null) checkMark.enabled = on;
        }

        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            if (owner != null) owner.OnMemberClicked(memberId);
        }
    }
}
