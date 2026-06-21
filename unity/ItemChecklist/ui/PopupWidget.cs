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

        // --- Iter-24 scroll (manual translate; gated by _scrollActive) ---
        // Dormant until a prefab caps the height: the Dropdown skeleton sets
        // MaxPopupHeight = 4.2 (6 rows). MaxPopupHeight <= 0 means "no cap" — and
        // a newly-added serialized float is absent from legacy prefab YAML (Unity
        // deserialises it to 0), so existing popups stay uncapped → today's centred
        // behaviour, build-neutral.
        public float MaxPopupHeight = 0f;              // 0 = no cap; a prefab overrides (skeleton 4.2u)
        public GameObject scrollMask;                  // popup-local SpriteMask GO (skeleton); null on legacy prefabs
        public PopupScrollHandle scrollHandle;         // hand-rolled handle (skeleton); nullable
        public float WheelStep = 0.7f;                 // one row per wheel notch (Task 3 calibration)

        protected bool _scrollActive;
        protected float _scrollOffset;   // [0, contentH - viewportH]; 0 = top
        protected float _contentH;       // last laid-out stack height
        protected float _viewportH;      // min(contentH, MaxPopupHeight) = mask height when scrolling

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

        /// <summary>Fit the panel to the laid-out row stack, capped at MaxPopupHeight
        /// and top-aligned to the authored top edge. Below the cap the rowContainer is
        /// centred (today's behaviour); at/above the cap it is top-aligned and the
        /// scroll chrome (mask + handle) is engaged. rowCount = number of rows the
        /// subclass laid out this rebuild.</summary>
        protected void AutoSizePopup(int rowCount)
        {
            if (rowCount <= 0) return;
            _contentH = rowCount * rowSpacing;
            bool capped = MaxPopupHeight > 0f;                       // <= 0 → no cap (legacy popups)
            _viewportH = capped ? Mathf.Min(_contentH, MaxPopupHeight) : _contentH;
            _scrollActive = capped && _contentH > MaxPopupHeight + 1e-4f;

            // Panel: top-aligned to the authored top edge, grows downward, bounded by the cap.
            if (popupPanel != null)
                popupPanel.transform.localPosition = new Vector3(
                    popupPanel.transform.localPosition.x, _topY - _viewportH / 2f, popupPanel.transform.localPosition.z);
            if (_panel != null)
                _panel.size = new Vector2(_panel.size.x, _viewportH);

            if (rowContainer != null)
            {
                if (_scrollActive)
                {
                    // Top-align row 0 to the mask top, then apply the scroll offset.
                    _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, _contentH - _viewportH);
                    rowContainer.localPosition = new Vector3(
                        rowContainer.localPosition.x, RowTopY + _scrollOffset, rowContainer.localPosition.z);
                }
                else
                {
                    _scrollOffset = 0f;
                    rowContainer.localPosition = new Vector3(
                        rowContainer.localPosition.x,
                        (FirstRowOffset + (rowCount - 1) / 2f) * rowSpacing,   // unchanged centring
                        rowContainer.localPosition.z);
                }
            }

            // Gate the scroll chrome fully off when the content fits (D7).
            if (scrollMask != null && scrollMask.activeSelf != _scrollActive)
                scrollMask.SetActive(_scrollActive);
            if (scrollHandle != null)
            {
                scrollHandle.SetActiveScrolling(_scrollActive);
                if (_scrollActive) scrollHandle.Sync(_scrollOffset, _contentH, _viewportH);
            }
        }

        /// <summary>rowContainer.y that puts row 0's top flush to the mask top edge.
        /// Rows lay out downward from y=0 under rowContainer; the offset is the
        /// authored top edge minus half a row (calibrated in-game in Task 3).</summary>
        protected float RowTopY => _topY - rowSpacing / 2f;

        public void TogglePopup() => SetOpen(!_open);

        protected void SetOpen(bool open)
        {
            _open = open;
            if (open) _scrollOffset = 0f;          // Iter-24: always open at the top (D9)
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
