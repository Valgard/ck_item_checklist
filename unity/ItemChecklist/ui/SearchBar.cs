using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Search box for the checklist. Subclasses CK's native TextInputField
    /// (Pug.Other.dll): PugText rendering, the caret (CharacterMarkBlinker),
    /// focus, and gameplay-input suppression are all inherited. The base
    /// OnLeftClicked calls Manager.input.SetActiveInputField(this); we only
    /// poll the text each frame and push changes to the shared view-model.
    /// Item Browser's SearchBar is the reference; its controller-deselect,
    /// double-click "highlight results", and snap-point navigation are
    /// intentionally omitted (single-player mouse+keyboard only — YAGNI).
    /// </summary>
    public sealed class SearchBar : TextInputField
    {
        private string _lastPushed = "";

        protected override void LateUpdate()
        {
            base.LateUpdate();
            string current = GetInputText() ?? "";
            if (current == _lastPushed) return;
            _lastPushed = current;
            var model = ItemChecklistMod.ListView;
            if (model != null) model.SearchText = current;
        }
    }
}
