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
    /// BoxCollider + ButtonUIElement) via FilterCheckboxButton. The popup chrome
    /// (open/close, click-outside, auto-size) lives in <see cref="PopupWidget"/>.
    ///
    /// A member with an **empty section** string is an ACTION row (e.g.
    /// "Clear all"): drawn from <see cref="actionTemplate"/>, no checkbox state —
    /// its <c>toggle()</c> just runs the action.
    /// </summary>
    public sealed class FilterWidget : PopupWidget
    {
        // Editor-wired serialized fields (chrome fields inherited from PopupWidget).
        public PugText headerLabel;            // "Filter" / "Filter (N)"
        public GameObject checkboxTemplate;    // one FilterCheckboxButton checkbox row (inactive)
        public GameObject headerTemplate;      // one section-header PugText row (inactive)
        public GameObject actionTemplate;      // one action row: glyph + label, no checkbox (inactive)

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

        // Filter starts members at popup row 0 (the header is a count label, not a row).
        protected override int FirstRowOffset => 0;

        // Reflect external changes (e.g. re-bake) whenever the popup opens.
        protected override void OnPopupOpened() => RebuildList();

        // Iter-24 collapse: closed-set keyed on the stable section TERM (Task 5), so it survives
        // a language change. Empty = all sections open (the default). static = session-stable
        // (mirrors the Sort-mode / filter-dimension state), resets on game restart.
        private static readonly System.Collections.Generic.HashSet<string> _closedSections
            = new System.Collections.Generic.HashSet<string>();

        public static bool IsSectionOpen(string term) => !_closedSections.Contains(term);

        // Section-caret X: clear of the scrollbar when it shows, at the panel edge when it hides.
        private const float CaretXWithScrollbar = 2.65f;
        private const float CaretXFullWidth = 2.9f;

        /// <summary>Toggle a section's collapsed state + re-lay out (members shown/hidden).</summary>
        public void ToggleSection(string term)
        {
            if (term == null) return;
            if (!_closedSections.Remove(term)) _closedSections.Add(term);
            RebuildList();
        }

        /// <summary>(Re)build the member table from the model + a label provider,
        /// then lay out the popup. Called from WireControls.</summary>
        public void Configure(IList<(string section, string label, Func<bool> isOn, Action toggle)> members,
            Func<int> activeCount, Action clearAll)
        {
            _members.Clear();
            EnsurePanel();
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
                if (btn == null)
                {
                    Debug.LogError("[ItemChecklist] FilterWidget template is missing a FilterCheckboxButton component");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }
                btn.owner = this;
                pool.Add(btn);
            }
        }

        private void RenderHeader(Func<int> activeCount)
        {
            int n = activeCount != null ? activeCount() : 0;
            if (headerLabel != null)
                headerLabel.RenderNoWrap(n > 0 ? ItemChecklist.Loc.F("ItemChecklist-Filters/HeaderCount", n) : ItemChecklist.Loc.T("ItemChecklist-Filters/Header"));
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
                        var headerGo = ht.transform.parent.gameObject;
                        headerGo.transform.localPosition = new Vector3(0f, -(pos * rowSpacing), 0f);
                        if (!headerGo.activeSelf) headerGo.SetActive(true);
                        ht.RenderNoWrap(ItemChecklist.Loc.T(lastSection));   // colour set on the headerTemplate PugText style in the prefab (gray)
                        // Iter-24 collapse: bind the click toggle + reflect the caret state.
                        var hb = headerGo.GetComponent<SectionHeaderButton>();
                        if (hb != null)
                        {
                            hb.Bind(lastSection, this);
                            if (hb.caret != null) hb.caret.sprite = IsSectionOpen(lastSection) ? caretOpen : caretClosed;
                        }
                        headerIdx++; pos++;
                    }
                }

                // Collapsed section: the header is shown, but its member rows are skipped.
                if (!action && !IsSectionOpen(_members[m].section)) continue;

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
                    label.RenderNoWrap((action ? "" : "  ") + _members[m].label);   // indent filter members
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

            AutoSizePopup(pos);

            // Iter-24: shift the section carets right to the panel edge when no scrollbar is
            // shown (content fits), and keep them clear of it when it is. _scrollActive is valid
            // only after AutoSizePopup, so this runs as a post-pass over the placed headers.
            float caretX = _scrollActive ? CaretXWithScrollbar : CaretXFullWidth;
            for (int i = 0; i < headerIdx; i++)
            {
                var hb = _headerPool[i]?.transform.parent.GetComponent<SectionHeaderButton>();
                if (hb != null && hb.caret != null)
                {
                    var t = hb.caret.transform;
                    t.localPosition = new Vector3(caretX, t.localPosition.y, t.localPosition.z);
                }
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
    }
}
