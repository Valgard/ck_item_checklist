using System.Collections.Generic;
using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// MonoBehaviour on the Content GameObject (= UIScrollWindow.scrollingContent).
    /// Owns a small fixed pool of ItemRow GameObjects and recycles them as the
    /// scroll position changes, so only the rows visible in the viewport exist
    /// instead of one GameObject per catalog entry (~10720).
    ///
    /// Load-bearing prerequisite: the window prefab MUST keep UIScrollWindow's
    /// serialized `scrollable` field pointing at this component. UIScrollWindow.Awake
    /// copies `scrollable` → its private `_scrollable` (or, if it is not an
    /// IScrollable, sets enabled=false PERMANENTLY — a disabled UIScrollWindow never
    /// runs LateUpdate, silently stopping scroll-recycle). That prefab wiring is the
    /// single source of truth: as of Iter-14.2 the mod no longer reflects into
    /// `_scrollable` itself. DefaultExecutionOrder(-100) is retained defensively;
    /// Awake now only caches the UIScrollWindow reference.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ItemChecklistContent : UIelement, IScrollable
    {
        // Runtime value is read from the row prefab's background SpriteRenderer in
        // Init() (single source of truth); this compile-time default is the fallback.
        public float RowHeight = ItemRow.RowHeight;

        // The next two are derived from the ContentsMask in Init() (single source of
        // truth, like RowHeight from the row bg); these compile-time values are the
        // fallback if the mask is not found. Derivation + invariant: see Init().

        // Viewport (mask) height; used only if UIScrollWindow.windowHeight is
        // unavailable/zero.
        private float _fallbackWindowHeight = 13.75f;

        // Content-local y of the visible window (mask) top. Row 0's TOP edge is
        // pinned here so the list start/end stay flush for ANY RowHeight (Rebind
        // offsets each row centre by RowHeight/2).
        private float _maskTopLocalY = 1.25f;

        private readonly List<ItemRow> _pool = new List<ItemRow>();
        private GameObject _rowPrefab;
        private UIScrollWindow _scrollWindow;
        private int _count;            // reported entry count (catalog.Count)
        private int _lastFirstIndex = -1;
        private TooltipSlot _tooltipHelper;   // Iter-22: one shared hover-data helper
        // Iter-22: the window viewport (ContentsMask) for the row-hover bounds check —
        // row colliders extend past the mask, so a cursor in the header/footer/margin
        // must not hover a clipped row. One window → static.
        private static Transform s_viewportMask;

        // Iter-6: the label's prefab default colour, used for Common/Poor names
        // (GetSlotBorderRarityColor returns this for them). Captured once from
        // the first pooled row's PugText (whose getter returns style.color before
        // any tint), so a future prefab style change is picked up automatically.
        private Color _defaultLabelColor = Color.white;
        private bool _defaultLabelColorCaptured;

        public int PoolSize => _pool.Count;

        private void Awake()
        {
            _scrollWindow = GetComponentInParent<UIScrollWindow>(true);
        }

        /// <summary>Idempotent: stores the row prefab and derives RowHeight from its
        /// background sprite (single source of truth — see the RowHeight field).</summary>
        public void Init(GameObject rowPrefab)
        {
            _rowPrefab = rowPrefab;
            // Read the row height straight from the prefab's background SpriteRenderer
            // so changing the row bg height in the prefab alone re-spaces the whole
            // list. size.y is authoritative only in Sliced/Tiled draw mode (in Simple
            // it returns the sprite's native size), so guard on that; otherwise keep
            // the compile-time ItemRow.RowHeight fallback.
            var proto = rowPrefab != null ? rowPrefab.GetComponent<ItemRow>() : null;
            if (proto != null && proto.background != null
                && proto.background.drawMode != SpriteDrawMode.Simple)
                RowHeight = proto.background.size.y;
            if (_scrollWindow == null)
                _scrollWindow = GetComponentInParent<UIScrollWindow>(true);

            // Derive the viewport top + height from the ContentsMask (single source
            // of truth, like RowHeight from the row bg). The mask is a 1x1 sprite
            // (PPU 1) scaled to viewport size, so its height = localScale.y and its
            // top edge (content-local) = localPosition.y + localScale.y/2 — both hold
            // because Content sits at the mask's shared-parent origin (localPosition
            // 0, scale 1). Reading the mask (which never scrolls) keeps both values in
            // sync when it is moved/resized in the prefab. The ContentsMask is a
            // sibling; its name must match the prefab authoring (Find-by-name avoids a
            // first-in-mod SpriteMask type reference the sandbox might reject).
            var mask = transform.parent != null ? transform.parent.Find("ContentsMask") : null;
            if (mask != null)
            {
                _maskTopLocalY = mask.localPosition.y + mask.localScale.y / 2f;
                _fallbackWindowHeight = mask.localScale.y;
                s_viewportMask = mask;
            }
        }

        /// <summary>Iter-22: true when the cursor is within the visible list viewport
        /// (the ContentsMask world bounds). Row colliders extend past the mask (the +4
        /// buffer rows and the bottom row clipped by the mask), so the row-hover
        /// overrides + highlight gate on this — a cursor in the header/footer/margin must
        /// not hover a clipped row. Mirrors PopupWidget.PointerOverPanel.</summary>
        public static bool PointerInViewport()
        {
            if (s_viewportMask == null) return false;
            var cam = Manager.camera != null ? Manager.camera.uiCamera : null;
            if (cam == null) return false;
            Vector3 c = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 p = s_viewportMask.position;
            Vector3 s = s_viewportMask.lossyScale;
            return Mathf.Abs(c.x - p.x) <= s.x * 0.5f
                && Mathf.Abs(c.y - p.y) <= s.y * 0.5f;
        }

        /// <summary>
        /// Instantiate the row pool up to the size the current viewport needs.
        /// Idempotent and grows-only: a no-op once the pool already covers
        /// ComputePoolSize(), but tops up if a later call needs more rows (e.g.
        /// the first build happened before windowHeight was known).
        /// </summary>
        public void EnsurePool()
        {
            if (_rowPrefab == null) return;
            int target = ComputePoolSize();
            for (int k = _pool.Count; k < target; k++)
            {
                var go = Object.Instantiate(_rowPrefab, transform);
                // Iter-22: the per-row ContentMask is an Editor-only authoring aid (turn
                // it on to preview clipping while editing the prefab); force it off at
                // runtime so it can be left enabled in the prefab without affecting the
                // game. The window's ContentsMask does the real clipping.
                var editorMask = go.transform.Find("ContentMask");
                if (editorMask != null) editorMask.gameObject.SetActive(false);
                _pool.Add(go.GetComponent<ItemRow>());
            }
            if (!_defaultLabelColorCaptured && _pool.Count > 0
                && _pool[0] != null && _pool[0].label != null)
            {
                _defaultLabelColor = _pool[0].label.color;   // PugText.color getter → style.color
                _defaultLabelColorCaptured = true;
            }
            // Iter-16.1: capture the pristine icon material once (before any pet-skin
            // recolor) so non-pet rows can be restored exactly.
            if (_pool.Count > 0 && _pool[0] != null)
                PetSkinIcon.CaptureBase(_pool[0].icon);

            // Iter-22: one shared hover-data helper, injected into every pooled row.
            if (_tooltipHelper == null)
            {
                var go = new GameObject("TooltipHelper");
                go.transform.SetParent(transform, false);
                _tooltipHelper = go.AddComponent<TooltipSlot>();
            }
            foreach (var row in _pool)
                if (row != null) row.SetTooltipHelper(_tooltipHelper);
        }

        private int ComputePoolSize()
        {
            float wh = (_scrollWindow != null && _scrollWindow.windowHeight > 0f)
                ? _scrollWindow.windowHeight
                : _fallbackWindowHeight;
            return Mathf.CeilToInt(wh / RowHeight) + 4;   // +4 buffer: 2 partial/spare rows top + bottom (denser rows since RowHeight 1.5)
        }

        /// <summary>Set the total entry count; drives reported height + scrollbar.</summary>
        public void SetCount(int count) => _count = count;

        /// <summary>
        /// Forced rebind of the visible window, bypassing the frame-saving guard.
        /// Used by every non-scroll trigger (open, discovery, re-bake) where the
        /// index may be unchanged but the binding is stale.
        /// </summary>
        public void RefreshVisible()
        {
            _lastFirstIndex = -1;
            UpdateContainingElements(transform.localPosition.y);
        }

        // IScrollable ---------------------------------------------------------

        public void UpdateContainingElements(float scroll)
        {
            int first = ClampFirstIndex(Mathf.FloorToInt(scroll / RowHeight));
            if (first == _lastFirstIndex) return;
            Rebind(first);
        }

        public bool IsBottomElementSelected() => false;
        public bool IsTopElementSelected() => false;
        public float GetCurrentWindowHeight() => _count * RowHeight;

        // ---------------------------------------------------------------------

        private int ClampFirstIndex(int first)
        {
            if (_count <= 0) return 0;
            return Mathf.Clamp(first, 0, Mathf.Max(0, _count - 1));
        }

        private void Rebind(int first)
        {
            _lastFirstIndex = first;
            var catalog = ItemChecklistMod.Catalog;
            var model = ItemChecklistMod.ListView;
            var state = DiscoveredState.Instance;
            for (int k = 0; k < _pool.Count; k++)
            {
                var row = _pool[k];
                if (row == null) continue;
                int displayIdx = first + k;
                if (catalog == null || model == null || state == null || displayIdx >= _count)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    continue;
                }
                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                // Pin row 0's TOP to _maskTopLocalY (flush) regardless of RowHeight:
                // centre = top - RowHeight/2, each row RowHeight below the previous.
                row.transform.localPosition = new Vector3(0f, _maskTopLocalY - RowHeight * (displayIdx + 0.5f), 0f);
                int catalogIdx = model.Order[displayIdx];
                var entry = catalog.GetByIndex(catalogIdx);
                // CK-authoritative rarity colour. useDefaultColorForCommon: true →
                // Common/Poor resolve to the label's normal colour (no visible tint),
                // Uncommon+ get slotBorderRarityColors[(int)(rarity+1)].
                Color rarityColor = Manager.ui.GetSlotBorderRarityColor(
                    entry.Rarity, useDefaultColorForCommon: true, defaultColor: _defaultLabelColor);
                // Iter-16.1: pet skins decouple name (species discovered, CK var-0) from
                // detail/icon (this skin collected, mod ledger); normal items use the same
                // discovered flag for both. nameKnown gates only the ???/tooltip; the
                // "collected" detail flag (Iter-16.4) routes through the shared chokepoint
                // so the row tick, the Discovery filter, and the N/M counter cannot drift.
                // Iter-17: cattle colour slots are species-gated like pet skins — show the
                // species name on ALL slots once ANY colour is discovered (a cattle may be
                // first met at a non-0 colour, so the gate is "any variation", not var 0).
                // The per-colour collected tick still routes through IsCollected below.
                bool nameKnown = entry.IsPetSkin
                    ? state.IsDiscovered(entry.ObjectId, 0)
                    : entry.IsCattle
                        ? state.IsDiscoveredAnyVariation(entry.ObjectId)
                        : state.IsDiscovered(entry.ObjectId, entry.Variation);
                bool showDetails = ItemChecklistMod.IsCollected(entry.ObjectId, entry.Variation);
                int owned = ItemChecklistMod.OwnedCount(entry.ObjectId, entry.Variation);
                row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, nameKnown, showDetails,
                    rarityColor, entry.Rarity, entry.Level, entry.SellValue,
                    owned, entry.IsPetSkin, entry.Variation);
            }
        }

        private void OnDestroy()
        {
            // PugText shared-pool hygiene (IB BasicEntriesListRenderer.ClearList):
            // release pool resources before the GameObjects are torn down.
            foreach (var row in _pool)
            {
                if (row == null) continue;
                foreach (var pugText in row.GetComponentsInChildren<PugText>(true))
                    pugText.Clear();
            }
            _pool.Clear();
        }
    }
}
