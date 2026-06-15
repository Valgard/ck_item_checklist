using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Reusable dropdown. Closed: the header shows the selected option. Open: a
    /// flush list of the NON-selected options sits directly under the header.
    /// Configure with labels + selected index + onSelected callback. Iter-7 uses
    /// it for the sort mode; Iter-8 reuses it for the discovery filter.
    /// </summary>
    public sealed class DropdownWidget : UIelement
    {
        // Editor-wired serialized fields.
        public PugText selectedLabel;          // header: shows the current option text
        public DropdownToggleButton toggle;    // open/close button (carries the caret)
        public SpriteRenderer caret;           // ui_group_expand/collapse glyph
        public GameObject popupPanel;          // toggled container holding the option rows
        public Transform rowContainer;         // parent for cloned option rows
        public GameObject rowTemplate;         // one option-row prefab (inactive)
        public Sprite caretClosed;             // ui_group_expand (collapsed)
        public Sprite caretOpen;               // ui_group_collapse (expanded)
        public float rowSpacing = 0.7f;        // compact option spacing (NOT the big list RowHeight)

        private SpriteRenderer _panel;              // cached popup bg, auto-sized to the option count
        private float _topY;                        // authored popup top edge (prefab popup.y + size/2), captured once

        private readonly List<DropdownOptionButton> _rows = new List<DropdownOptionButton>();
        private readonly List<PugText> _rowLabels = new List<PugText>();
        private readonly List<SpriteRenderer> _rowSelectedMarks = new List<SpriteRenderer>();
        private string[] _labels = Array.Empty<string>();
        private Action<int> _onSelected;
        private int _selected;
        private bool _open;
        private bool _armed;   // click-outside-close arming: skip the frame the popup opened

        public void Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
        {
            _onSelected = onSelected;
            if (_panel == null && popupPanel != null)
            {
                _panel = popupPanel.GetComponent<SpriteRenderer>();
                // capture the authored top edge from the prefab (popup.y + half height) so it stays editable there
                if (_panel != null) _topY = popupPanel.transform.localPosition.y + _panel.size.y / 2f;
            }
            _labels = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++) _labels[i] = labels[i];
            EnsurePool(Mathf.Max(0, _labels.Length - 1));
            _selected = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, _labels.Length - 1));
            RenderHeader();
            RebuildList();
            SetOpen(false);
            // Wire owner on ALL child toggle buttons (header Display + caret
            // ToggleButton), not just the serialized `toggle` field. The prefab
            // owner-ref is fragile across the Dropdown.prefab boundary — extracting
            // the chrome nulled the header toggle's serialized owner (Iter-13
            // regression). Wiring at runtime keeps every clickable chrome element
            // live regardless of how the consumer prefab/variant references it.
            foreach (var tb in GetComponentsInChildren<DropdownToggleButton>(includeInactive: true))
                tb.owner = this;
        }

        private void EnsurePool(int n)
        {
            if (rowTemplate == null || rowContainer == null) return;
            for (int i = _rows.Count; i < n; i++)
            {
                var go = UnityEngine.Object.Instantiate(rowTemplate, rowContainer);
                var btn = go.GetComponent<DropdownOptionButton>();
                if (btn == null)
                {
                    Debug.LogError("[ItemChecklist] DropdownWidget rowTemplate is missing a DropdownOptionButton component");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }
                btn.owner = this;
                _rows.Add(btn);
                _rowLabels.Add(FindLabel(go));
                _rowSelectedMarks.Add(FindSelectedMark(go));
            }
        }

        private void RenderHeader()
        {
            if (selectedLabel != null && _selected >= 0 && _selected < _labels.Length)
            {
                selectedLabel.maxWidth = 0f;   // no word-wrap: CK PugFont wrap IndexOutOfRange-crashes on long (localised) labels
                selectedLabel.Render(_labels[_selected]);
            }
        }

        /// <summary>Lay out the NON-selected options flush under the header.</summary>
        private void RebuildList()
        {
            int pos = 0;
            for (int opt = 0; opt < _labels.Length; opt++)
            {
                if (opt == _selected) continue;          // selected lives in the header
                if (pos >= _rows.Count) break;
                var btn = _rows[pos];
                btn.index = opt;                          // clicking selects this option
                if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                btn.transform.localPosition = new Vector3(0f, -((pos + 1) * rowSpacing), 0f);
                if (_rowLabels[pos] != null) { _rowLabels[pos].maxWidth = 0f; _rowLabels[pos].Render(_labels[opt]); }
                if (_rowSelectedMarks[pos] != null) _rowSelectedMarks[pos].enabled = false;
                pos++;
            }
            for (int i = pos; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf) _rows[i].gameObject.SetActive(false);

            // Auto-size the popup to the actual option count (robust to label changes):
            // centre the options on the popup origin + fit the panel height to the stack.
            // Options sit at -(pos+1)*rowSpacing, so their centre is at -(n+1)/2*rowSpacing.
            int n = pos;
            if (n > 0)
            {
                float h = n * rowSpacing;   // no extra padding — panel hugs the option stack
                if (rowContainer != null)
                    rowContainer.localPosition = new Vector3(
                        rowContainer.localPosition.x, (n + 1) / 2f * rowSpacing, rowContainer.localPosition.z);
                if (popupPanel != null)     // top-align: keep the authored top edge, panel grows downward
                    popupPanel.transform.localPosition = new Vector3(
                        popupPanel.transform.localPosition.x, _topY - h / 2f, popupPanel.transform.localPosition.z);
                if (_panel != null)
                    _panel.size = new Vector2(_panel.size.x, h);
            }
        }

        public void SelectOption(int optionIndex)
        {
            _selected = optionIndex;
            RenderHeader();
            RebuildList();
            SetOpen(false);
            _onSelected?.Invoke(_selected);
        }

        public void TogglePopup() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (popupPanel != null) popupPanel.SetActive(open);
            if (caret != null) caret.sprite = open ? caretOpen : caretClosed;
        }

        // Click-outside-to-close. Runs in LateUpdate (after CK's UIMouse has
        // processed clicks this frame): an option/toggle click has already set
        // _open=false via SelectOption/TogglePopup, so only a genuine OUTSIDE
        // click reaches here with _open still true. The _armed guard skips the
        // frame the popup was opened on, so the opening click doesn't close it.
        private void LateUpdate()
        {
            if (!_open) { _armed = false; return; }
            if (!_armed) { _armed = true; return; }
            if (Input.GetMouseButtonDown(0))
                SetOpen(false);
        }

        // Template child lookup — child names must match the prefab authoring.
        private static PugText FindLabel(GameObject row) =>
            row.transform.Find("Label")?.GetComponent<PugText>();
        private static SpriteRenderer FindSelectedMark(GameObject row) =>
            row.transform.Find("SelectedMark")?.GetComponent<SpriteRenderer>();
    }
}
