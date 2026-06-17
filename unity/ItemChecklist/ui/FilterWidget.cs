using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Multi-select, sectioned filter popup. Closed: the header shows
    /// "Filter" / "Filter (N)" with N = active member count. Open: gray section
    /// headers + checkbox rows, plus action rows ("Clear all") rendered from a
    /// dedicated <see cref="actionTemplate"/> (a clickable row with its own glyph,
    /// no checkbox). OR within a section, AND across sections — the semantics live
    /// in ItemListViewModel; this widget only renders state and reports clicks.
    /// Reuses the Iter-5 clickable-control pattern (SpriteRenderer + 3D
    /// BoxCollider + ButtonUIElement) via FilterCheckboxButton.
    ///
    /// A member with an **empty section** string is an ACTION row (e.g.
    /// "Clear all"): drawn from <see cref="actionTemplate"/>, no checkbox state —
    /// its <c>toggle()</c> just runs the action.
    /// </summary>
    public sealed class FilterWidget : UIelement, IPopupToggle
    {
        // Editor-wired serialized fields.
        public PugText headerLabel;            // "Filter" / "Filter (N)"
        public SpriteRenderer caret;
        public GameObject popupPanel;          // toggled container
        public Transform rowContainer;         // parent for cloned rows + headers
        public GameObject checkboxTemplate;    // one FilterCheckboxButton checkbox row (inactive)
        public GameObject headerTemplate;      // one section-header PugText row (inactive)
        public GameObject actionTemplate;      // one action row: glyph + label, no checkbox (inactive)
        public Sprite caretClosed;
        public Sprite caretOpen;
        public float rowSpacing = 0.7f;

        private SpriteRenderer _panel;              // cached popup bg, auto-sized to the row count
        private float _topY;                        // authored popup top edge (prefab popup.y + size/2), captured once

        // One row in the flat member table. An empty section marks an action row.
        private struct Member
        {
            public string section;
            public string label;
            public Func<bool> isOn;
            public Action toggle;
        }

        private static bool IsAction(Member m) => string.IsNullOrEmpty(m.section);

        private readonly List<Member> _members = new List<Member>();
        private readonly List<FilterCheckboxButton> _rowPool = new List<FilterCheckboxButton>();      // checkbox (filter) rows
        private readonly List<FilterCheckboxButton> _actionPool = new List<FilterCheckboxButton>();   // action rows
        private readonly List<PugText> _headerPool = new List<PugText>();
        private Action _onAnyChange;   // refresh header count after a toggle
        private Action _clearAll;
        private bool _open;
        private bool _armed;

        /// <summary>(Re)build the member table from the model + a label provider,
        /// then lay out the popup. Called from WireControls.</summary>
        public void Configure(IList<(string section, string label, Func<bool> isOn, Action toggle)> members,
            Func<int> activeCount, Action clearAll)
        {
            _members.Clear();
            if (_panel == null && popupPanel != null)
            {
                _panel = popupPanel.GetComponent<SpriteRenderer>();
                // capture the authored top edge from the prefab (popup.y + half height) so it stays editable there
                if (_panel != null) _topY = popupPanel.transform.localPosition.y + _panel.size.y / 2f;
            }
            foreach (var m in members)
                _members.Add(new Member { section = m.section, label = m.label, isOn = m.isOn, toggle = m.toggle });
            _onAnyChange = () => RenderHeader(activeCount);
            _clearAll = clearAll;
            EnsurePools();
            RenderHeader(activeCount);
            RebuildList();
            SetOpen(false);
            // Wire owner on all child toggle buttons (header + caret) at runtime —
            // the chrome's shared DropdownToggleButtons drive this widget via the
            // IPopupToggle contract. Serialization-free, robust across the variant
            // boundary (mirrors DropdownWidget.Configure).
            foreach (var tb in GetComponentsInChildren<DropdownToggleButton>(includeInactive: true))
                tb.owner = this;
        }

        private void EnsurePools()
        {
            if (rowContainer == null) return;
            int normalCount = 0, actionCount = 0;
            foreach (var m in _members) { if (IsAction(m)) actionCount++; else normalCount++; }

            GrowButtonPool(_rowPool, checkboxTemplate, normalCount);
            GrowButtonPool(_actionPool, actionTemplate, actionCount);

            // section-header pool: over-allocate (one per member + 1) rather than
            // counting distinct sections up front.
            if (headerTemplate != null)
                for (int i = _headerPool.Count; i < _members.Count + 1; i++)
                {
                    var go = UnityEngine.Object.Instantiate(headerTemplate, rowContainer);
                    _headerPool.Add(go.transform.Find("Label")?.GetComponent<PugText>());
                }
        }

        private void GrowButtonPool(List<FilterCheckboxButton> pool, GameObject template, int target)
        {
            if (template == null) return;
            for (int i = pool.Count; i < target; i++)
            {
                var go = UnityEngine.Object.Instantiate(template, rowContainer);
                var btn = go.GetComponent<FilterCheckboxButton>();
                if (btn == null) { UnityEngine.Object.Destroy(go); continue; }
                btn.owner = this;
                pool.Add(btn);
            }
        }

        private void RenderHeader(Func<int> activeCount)
        {
            int n = activeCount != null ? activeCount() : 0;
            if (headerLabel != null)
            {
                headerLabel.maxWidth = 0f;   // no word-wrap: CK PugFont wrap IndexOutOfRange-crashes on long (localised) labels, aborting ShowUI
                headerLabel.Render(n > 0 ? ItemChecklist.Loc.F("ItemChecklist-Filters/HeaderCount", n) : ItemChecklist.Loc.T("ItemChecklist-Filters/Header"));
            }
        }

        /// <summary>Lay out section headers + checkbox/action rows top-to-bottom.</summary>
        private void RebuildList()
        {
            int pos = 0, rowIdx = 0, actionIdx = 0, headerIdx = 0;
            string lastSection = null;

            for (int m = 0; m < _members.Count; m++)
            {
                bool action = IsAction(_members[m]);

                // New (non-empty) section → place a gray header row first.
                if (!action && _members[m].section != lastSection)
                {
                    lastSection = _members[m].section;
                    if (headerIdx < _headerPool.Count && _headerPool[headerIdx] != null)
                    {
                        var ht = _headerPool[headerIdx];
                        ht.transform.parent.localPosition = new Vector3(0f, -(pos * rowSpacing), 0f);
                        if (!ht.transform.parent.gameObject.activeSelf) ht.transform.parent.gameObject.SetActive(true);
                        ht.maxWidth = 0f;         // no word-wrap (see RenderHeader)
                        ht.Render(lastSection);   // colour set on the headerTemplate PugText style in the prefab (gray)
                        headerIdx++; pos++;
                    }
                }

                FilterCheckboxButton btn;
                if (action)
                {
                    if (actionIdx >= _actionPool.Count) continue;
                    btn = _actionPool[actionIdx++];
                }
                else
                {
                    if (rowIdx >= _rowPool.Count) break;
                    btn = _rowPool[rowIdx++];
                }
                if (btn == null) continue;

                if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                btn.memberId = m;
                btn.transform.localPosition = new Vector3(0f, -(pos * rowSpacing), 0f);
                var label = btn.transform.Find("Label")?.GetComponent<PugText>();
                if (label != null)
                {
                    label.maxWidth = 0f;   // no word-wrap (see RenderHeader)
                    label.Render((action ? "" : "  ") + _members[m].label);   // indent filter members
                }
                if (!action) btn.SetChecked(_members[m].isOn());   // action rows carry no checkbox state
                pos++;
            }

            // Hide unused pooled rows / actions / headers.
            for (int i = rowIdx; i < _rowPool.Count; i++)
                if (_rowPool[i].gameObject.activeSelf) _rowPool[i].gameObject.SetActive(false);
            for (int i = actionIdx; i < _actionPool.Count; i++)
                if (_actionPool[i].gameObject.activeSelf) _actionPool[i].gameObject.SetActive(false);
            for (int i = headerIdx; i < _headerPool.Count; i++)
            {
                var p = _headerPool[i]?.transform.parent;
                if (p != null && p.gameObject.activeSelf) p.gameObject.SetActive(false);
            }

            // Auto-size the popup to the actual row count (headers + checkbox/action rows):
            // rows sit at -(pos)*rowSpacing, so their centre is at -(n-1)/2*rowSpacing.
            int n = pos;
            if (n > 0)
            {
                float h = n * rowSpacing;   // no extra padding — panel hugs the row stack
                if (rowContainer != null)
                    rowContainer.localPosition = new Vector3(
                        rowContainer.localPosition.x, (n - 1) / 2f * rowSpacing, rowContainer.localPosition.z);
                if (popupPanel != null)     // top-align: keep the authored top edge, panel grows downward
                    popupPanel.transform.localPosition = new Vector3(
                        popupPanel.transform.localPosition.x, _topY - h / 2f, popupPanel.transform.localPosition.z);
                if (_panel != null)
                    _panel.size = new Vector2(_panel.size.x, h);
            }
        }

        public void OnMemberClicked(int memberId)
        {
            if (memberId < 0 || memberId >= _members.Count) return;
            _members[memberId].toggle();
            RebuildList();          // re-sync every checkbox visual across both pools
            _onAnyChange?.Invoke();
        }

        public void ClearAll()
        {
            _clearAll?.Invoke();
            RebuildList();
            _onAnyChange?.Invoke();
        }

        public void TogglePopup() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
            if (open) RebuildList();   // reflect external changes (e.g. re-bake)
        }

        // Click-outside-to-close (identical pattern to DropdownWidget.LateUpdate).
        private void LateUpdate()
        {
            if (!_open) { _armed = false; return; }
            if (!_armed) { _armed = true; return; }
            if (Input.GetMouseButtonDown(0)) SetOpen(false);
        }
    }
}
