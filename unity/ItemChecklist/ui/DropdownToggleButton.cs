using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Click target that toggles its owner's popup. The owner is any
    /// <see cref="IPopupToggle"/> (DropdownWidget or FilterWidget), so the
    /// shared Dropdown.prefab chrome carries ONE toggle type for both.</summary>
    public sealed class DropdownToggleButton : ClickButton
    {
        // Wired at runtime (each widget's Configure), not serialized: the owner
        // lives in a consumer prefab/variant, so a serialized cross-prefab ref is
        // fragile (it broke the header toggle during the chrome extraction).
        public IPopupToggle owner;
        protected override void OnClick()
        {
            if (owner != null) owner.TogglePopup();
        }
    }
}
