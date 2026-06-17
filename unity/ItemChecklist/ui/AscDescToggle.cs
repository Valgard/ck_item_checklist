using System;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// A two-state action button: ascending (asc glyph) <-> descending (desc
    /// glyph). Click flips the state and fires onToggled(ascending).
    /// </summary>
    public sealed class AscDescToggle : ClickButton
    {
        public SpriteRenderer glyph;     // shows asc/desc sprite
        public Sprite ascSprite;         // ui_icon_sort_order_asc
        public Sprite descSprite;        // ui_icon_sort_order_desc

        private Action<bool> _onToggled;
        private bool _ascending = true;

        public void Configure(bool ascending, Action<bool> onToggled)
        {
            _onToggled = onToggled;
            _ascending = ascending;
            Apply();
        }

        protected override void OnClick()
        {
            _ascending = !_ascending;
            Apply();
            _onToggled?.Invoke(_ascending);
        }

        private void Apply()
        {
            if (glyph != null) glyph.sprite = _ascending ? ascSprite : descSprite;
        }
    }
}
