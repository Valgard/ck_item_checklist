using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Shared chrome for the mod's two popup widgets — Sort (<see cref="DropdownWidget"/>)
    /// and Filter (<see cref="FilterWidget"/>). Carries the open/close state, the
    /// click-outside auto-close, and the popup auto-sizing. The serialized field
    /// names are unchanged from the former per-widget fields, so the prefab Editor
    /// wiring carries over (Unity serialises inherited public fields by name); the
    /// base is abstract → never instantiated, no fileID concern. Subclasses keep
    /// their own header/templates/pools and their own RebuildList.
    /// </summary>
    public abstract class PopupWidget : UIelement, IPopupToggle
    {
        // Shared serialized chrome (inherited by both consumers; the prefab YAML
        // field names match these, so the Editor wiring resolves unchanged).
        public SpriteRenderer caret;           // ui_group_expand/collapse glyph
        public Sprite caretClosed;             // ui_group_expand (collapsed)
        public Sprite caretOpen;               // ui_group_collapse (expanded)
        public GameObject popupPanel;          // toggled container holding the rows
        public Transform rowContainer;         // parent for cloned rows
        public float rowSpacing = 0.7f;        // compact row spacing (NOT the big list RowHeight)

        protected SpriteRenderer _panel;       // cached popup bg, auto-sized to the row count
        protected float _topY;                 // authored popup top edge (popup.y + size/2), captured once
        protected bool _open;
        private bool _armed;                   // click-outside arming: skip the frame the popup opened

        /// <summary>Slot offset of the first laid-out row. Sort reserves row 0 for
        /// the header-shown selected option (=> 1); Filter starts members at row 0
        /// (=> 0). Feeds AutoSizePopup's rowContainer centring AND each subclass's
        /// row layout, so the two cannot drift (the one-row-offset trap).</summary>
        protected abstract int FirstRowOffset { get; }

        /// <summary>Capture the popup SpriteRenderer + its authored top edge once.
        /// Call from each subclass's Configure before the first RebuildList.</summary>
        protected void EnsurePanel()
        {
            if (_panel == null && popupPanel != null)
            {
                _panel = popupPanel.GetComponent<SpriteRenderer>();
                // capture the authored top edge from the prefab (popup.y + half height)
                if (_panel != null) _topY = popupPanel.transform.localPosition.y + _panel.size.y / 2f;
            }
        }

        /// <summary>Centre the rowContainer on its origin and fit the panel height to
        /// the laid-out row stack, top-aligned to the authored top edge. rowCount =
        /// number of rows the subclass laid out this rebuild.</summary>
        protected void AutoSizePopup(int rowCount)
        {
            if (rowCount <= 0) return;
            float h = rowCount * rowSpacing;   // no extra padding — panel hugs the stack
            if (rowContainer != null)
                rowContainer.localPosition = new Vector3(
                    rowContainer.localPosition.x,
                    (FirstRowOffset + (rowCount - 1) / 2f) * rowSpacing,
                    rowContainer.localPosition.z);
            if (popupPanel != null)     // top-align: keep the authored top edge, panel grows downward
                popupPanel.transform.localPosition = new Vector3(
                    popupPanel.transform.localPosition.x, _topY - h / 2f, popupPanel.transform.localPosition.z);
            if (_panel != null)
                _panel.size = new Vector2(_panel.size.x, h);
        }

        public void TogglePopup() => SetOpen(!_open);

        protected void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
            if (open) OnPopupOpened();
        }

        /// <summary>Runs when the popup opens. Default no-op (Sort). Filter overrides
        /// to RebuildList so external changes (e.g. re-bake) reflect on open.</summary>
        protected virtual void OnPopupOpened() { }

        // Click-outside-to-close. Runs after CK's UIMouse processed clicks this
        // frame: an option/toggle click already set _open=false via SetOpen, so only
        // a genuine OUTSIDE click reaches here with _open still true. _armed skips the
        // frame the popup opened on. `override` (vs the former per-widget `private`)
        // clears the CS0114 hide warning; base.LateUpdate() is intentionally NOT
        // called — the widgets never ran UIelement.LateUpdate, so this preserves
        // behaviour exactly.
        protected override void LateUpdate()
        {
            if (!_open) { _armed = false; return; }
            if (!_armed) { _armed = true; return; }
            if (Input.GetMouseButtonDown(0)) SetOpen(false);
        }
    }
}
