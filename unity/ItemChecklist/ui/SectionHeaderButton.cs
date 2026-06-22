using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Clickable filter section header (Iter-24 collapse). Clicking it toggles its
    /// section's collapsed state in the owning <see cref="FilterWidget"/>. Extends
    /// <see cref="ClickButton"/> for the 3D-collider click machinery; the section term
    /// + owner are bound at runtime by <c>FilterWidget.RebuildList</c> (no serialized
    /// cross-ref). The section is keyed on the stable loc <b>term</b> (not the rendered
    /// label), so collapse state survives a language change.
    /// </summary>
    public sealed class SectionHeaderButton : ClickButton
    {
        public SpriteRenderer caret;     // expand/collapse glyph (driven by FilterWidget)

        private string _term;
        private FilterWidget _owner;

        public void Bind(string sectionTerm, FilterWidget owner)
        {
            _term = sectionTerm;
            _owner = owner;
        }

        protected override void OnClick()
        {
            if (_owner != null && _term != null) _owner.ToggleSection(_term);
        }
    }
}
