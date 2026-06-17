using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>One checkbox option row in the filter popup. Carries an
    /// opaque member id (assigned by FilterWidget) and reports clicks back
    /// to the owning widget, which flips the corresponding model dimension.</summary>
    public sealed class FilterCheckboxButton : ClickButton
    {
        public FilterWidget owner;
        public int memberId;          // index into the widget's flat member table
        public SpriteRenderer checkMark;   // ui_icon_requirement (shown when checked)

        public void SetChecked(bool on)
        {
            if (checkMark != null) checkMark.enabled = on;
        }

        protected override void OnClick()
        {
            if (owner != null) owner.OnMemberClicked(memberId);
        }
    }
}
