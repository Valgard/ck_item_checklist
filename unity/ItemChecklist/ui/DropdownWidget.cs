using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Reusable dropdown (Sort). Closed: the header shows the selected option. Open:
    /// a flush list of the NON-selected options sits directly under the header.
    /// Configure with labels + selected index + onSelected callback. The popup chrome
    /// (open/close, click-outside, auto-size) lives in <see cref="PopupWidget"/>.
    /// </summary>
    public sealed class DropdownWidget : PopupWidget
    {
        // Editor-wired serialized fields (chrome fields inherited from PopupWidget).
        public PugText selectedLabel;          // header: shows the current option text
        public DropdownToggleButton toggle;    // open/close button (carries the caret)
        public GameObject rowTemplate;         // one option-row prefab (inactive)

        private readonly List<DropdownOptionButton> _rows = new List<DropdownOptionButton>();
        private readonly List<PugText> _rowLabels = new List<PugText>();
        private readonly List<SpriteRenderer> _rowSelectedMarks = new List<SpriteRenderer>();
        private string[] _labels = Array.Empty<string>();
        private Action<int> _onSelected;
        private int _selected;

        // Sort reserves popup row 0 for the header-shown selected option.
        protected override int FirstRowOffset => 1;

        public void Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
        {
            _onSelected = onSelected;
            EnsurePanel();
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
                selectedLabel.RenderNoWrap(_labels[_selected]);
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
                btn.transform.localPosition = new Vector3(0f, -((pos + FirstRowOffset) * rowSpacing), 0f);
                if (_rowLabels[pos] != null) _rowLabels[pos].RenderNoWrap(_labels[opt]);
                if (_rowSelectedMarks[pos] != null) _rowSelectedMarks[pos].enabled = false;
                pos++;
            }
            for (int i = pos; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf) _rows[i].gameObject.SetActive(false);

            AutoSizePopup(pos);
        }

        public void SelectOption(int optionIndex)
        {
            _selected = optionIndex;
            RenderHeader();
            RebuildList();
            SetOpen(false);
            _onSelected?.Invoke(_selected);
        }

        // Template child lookup — child names must match the prefab authoring.
        private static PugText FindLabel(GameObject row) =>
            row.transform.Find("Label")?.GetComponent<PugText>();
        private static SpriteRenderer FindSelectedMark(GameObject row) =>
            row.transform.Find("SelectedMark")?.GetComponent<SpriteRenderer>();
    }
}
