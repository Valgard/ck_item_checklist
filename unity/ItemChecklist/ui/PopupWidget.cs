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
        public float rowSpacing = 0.625f;      // compact row spacing (NOT the big list RowHeight); matches the prefab

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
        public int MaxVisibleRows = 6;                 // cap = MaxVisibleRows * rowSpacing; 0 = no cap. Base default → every variant scrolls past it (Sort can override).
        public GameObject scrollMask;                  // popup SpriteMask GO; auto-discovered from the inherited skeleton child if unset (no serialized cross-ref)
        public PopupScrollHandle scrollHandle;         // hand-rolled handle; nullable
        public float WheelStep = 0.625f;               // one row per wheel notch (= rowSpacing)

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
            // Auto-discover the popup SpriteMask (inherited from the Dropdown skeleton) so no
            // variant needs to serialize a fragile cross-prefab ref — the Iter-13 runtime-wire rule.
            if (scrollMask == null && popupPanel != null)
            {
                var sm = popupPanel.GetComponentInChildren<SpriteMask>(includeInactive: true);
                if (sm != null) scrollMask = sm.gameObject;
            }
            // Same runtime-wire for the scrollbar handle (inherited skeleton child); also bind its owner.
            if (scrollHandle == null && popupPanel != null)
            {
                var h = popupPanel.GetComponentInChildren<PopupScrollHandle>(includeInactive: true);
                if (h != null) { scrollHandle = h; h.owner = this; }
            }
        }

        /// <summary>Handle drag: set the scroll offset from the thumb's normalized track position.</summary>
        public void SetScrollFromHandle(float fraction01)   // 0 = top … 1 = bottom
        {
            if (!_scrollActive) return;
            _scrollOffset = Mathf.Clamp01(fraction01) * (_contentH - _viewportH);
            ApplyScroll();
        }

        /// <summary>Cursor's normalized position along a vertical track (0 = top, 1 = bottom),
        /// via the orthographic UI camera (z-independent world X/Y).</summary>
        public float CursorFractionInTrack(Transform track, float trackLength)
        {
            var cam = Manager.camera != null ? Manager.camera.uiCamera : null;
            if (cam == null || trackLength <= 0f || track == null) return 0f;
            Vector3 local = track.InverseTransformPoint(cam.ScreenToWorldPoint(Input.mousePosition));
            return Mathf.Clamp01((trackLength * 0.5f - local.y) / trackLength);
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
            float cap = MaxVisibleRows > 0 ? MaxVisibleRows * rowSpacing : 0f;   // 0 = no cap
            bool capped = cap > 0f;
            _viewportH = capped ? Mathf.Min(_contentH, cap) : _contentH;
            _scrollActive = capped && _contentH > cap + 1e-4f;

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

            // The mask stays active whenever the popup is open (it is a child of popupPanel, so it
            // follows the popup's active state). Sized to the cap, it clips only when content
            // exceeds it and shows everything when it fits — so a short popup never hits the
            // VisibleInsideMask-with-no-active-mask invisibility (docs/gotchas § SpriteMask).
            // Only the scrollbar/handle is gated on actual overflow (D7).
            if (scrollMask != null && !scrollMask.activeSelf) scrollMask.SetActive(true);
            if (scrollHandle != null)
            {
                scrollHandle.SetActiveScrolling(_scrollActive);
                if (_scrollActive) scrollHandle.Sync(_scrollOffset, _contentH, _viewportH);
            }
        }

        /// <summary>rowContainer.y that puts row 0's top flush to the capped panel's
        /// top edge. AutoSizePopup moves the Popup GO itself to (_topY - viewportH/2)
        /// so the centred 9-slice BG sits cap-high + top-aligned; the rows are its
        /// children, so in that moved frame row 0 is flush at viewportH/2 - rowSpacing/2.
        /// (Fine-tuned in-game in Task 3.)</summary>
        protected float RowTopY => _viewportH / 2f - rowSpacing / 2f;

        public void TogglePopup() => SetOpen(!_open);

        // The most-recently-opened popup, for gap-F: the wheel-suppression Harmony patch on
        // UIScrollWindow asks whether an open popup currently owns the wheel (cursor over it).
        private static PopupWidget _openInstance;
        public static bool OpenPopupCapturesWheel() => _openInstance != null && _openInstance._open && _openInstance.PointerOverPanel();

        protected void SetOpen(bool open)
        {
            _open = open;
            if (open) { _scrollOffset = 0f; _openInstance = this; }   // track for gap-F wheel ownership
            else if (_openInstance == this) _openInstance = null;
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
            if (_scrollActive) HandleWheel();
            if (!_armed) { _armed = true; return; }
            // Gap-A: close only on a click genuinely OUTSIDE the popup. The old code closed on
            // ANY mouse-down, so an inside click (checkbox, and — from Iter-24 on — a section
            // header or the scrollbar handle) wrongly closed the popup. A bounds check against
            // the panel rect fixes all three.
            if (Input.GetMouseButtonDown(0) && !PointerOverPanel()) SetOpen(false);
        }

        // Mouse-wheel scroll, but only while the cursor is over THIS popup (gap-F: the main
        // list must not also scroll). One notch = WheelStep (= rowSpacing → one row).
        private void HandleWheel()
        {
            float d = Input.mouseScrollDelta.y;
            if (d == 0f || !PointerOverPanel()) return;
            _scrollOffset = Mathf.Clamp(_scrollOffset - d * WheelStep, 0f, _contentH - _viewportH);
            ApplyScroll();
        }

        /// <summary>Push the current scroll offset to the rows + handle (shared by wheel + handle drag).</summary>
        protected void ApplyScroll()
        {
            if (rowContainer != null)
                rowContainer.localPosition = new Vector3(
                    rowContainer.localPosition.x, RowTopY + _scrollOffset, rowContainer.localPosition.z);
            if (scrollHandle != null) scrollHandle.Sync(_scrollOffset, _contentH, _viewportH);
        }

        /// <summary>True when the UI cursor lies within the popup panel's world rect. The UI
        /// camera is orthographic, so world X/Y is z-independent — no depth calibration needed.
        /// (Manager.camera property access is sandbox-safe by the same precedent as Manager.ui.)</summary>
        protected bool PointerOverPanel()
        {
            if (_panel == null || popupPanel == null || !popupPanel.activeInHierarchy) return false;
            var cam = Manager.camera != null ? Manager.camera.uiCamera : null;
            if (cam == null) return false;
            Vector3 c = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 p = popupPanel.transform.position;
            return Mathf.Abs(c.x - p.x) <= _panel.size.x * 0.5f
                && Mathf.Abs(c.y - p.y) <= _panel.size.y * 0.5f;
        }
    }
}
