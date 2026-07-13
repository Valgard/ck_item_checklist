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

        /// <summary>
        /// Disable the PugText word-wrap path on this single-line field.
        ///
        /// CK's <c>TextInputField.Awake</c> sets
        /// <c>pugText.maxWidth = maxWidth + (dontAllowNewLines ? 1 : 0)</c> — for this
        /// field 7.5 + 1 = 8.5. Any <c>pugText.Render()</c> with <c>maxWidth &gt; 0</c>
        /// then runs CK's <c>PugFont.AddNewLinesToLinesExceedingMaxWidth</c>, whose
        /// word-wrap indexes <c>text[num3 - 1]</c> out of range on certain input,
        /// throwing <see cref="System.IndexOutOfRangeException"/> *every frame* while
        /// typing (a pre-existing CK bug, reproduced on stock too; silent to the player
        /// but log-spammy). A single-line field (<c>dontAllowNewLines: 1</c>) must never
        /// word-wrap, so force <c>pugText.maxWidth = 0</c>. Visual width is unaffected:
        /// <c>TextInputField.TrimTextToFitRestrictions</c> still clips overflowing
        /// characters via the field's own <c>maxWidth</c> (7.5), independent of the
        /// PugText word-wrap. Done in Awake (not LateUpdate) so it takes effect before
        /// the first render — covers <see cref="SyncFrom"/> restoring a long prior search
        /// on open. Nothing rewrites <c>pugText.maxWidth</c> per frame, so one write holds.
        /// </summary>
        private new void Awake()
        {
            base.Awake();
            if (pugText != null) pugText.maxWidth = 0f;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            Iter26FocusProbe.DetectFrame(this);   // THROWAWAY iter-26 focus-race probe — remove with the fix
            string current = GetInputText() ?? "";
            if (current == _lastPushed) return;
            _lastPushed = current;
            var model = ItemChecklistMod.ListView;
            if (model != null) model.SearchText = current;
        }

        /// <summary>
        /// Update the hint (placeholder) text shown when the field is empty.
        /// Forwards to the base class's inherited <c>hintText</c> PugText so the
        /// hint re-localises whenever the window re-wires controls.
        /// </summary>
        public void SetHint(string text) { if (hintText != null) hintText.Render(text); }

        /// <summary>
        /// Set the field text to <paramref name="text"/> WITHOUT triggering a push
        /// back to the model. Used by the window to sync the field to the model's
        /// current SearchText on open and after a re-bake (when a fresh
        /// ItemListViewModel replaces the old one). Resets the change-detection cache
        /// so the synced value is not re-pushed on the next frame.
        /// </summary>
        public void SyncFrom(string text)
        {
            text ??= "";
            SetInputText(text);
            _lastPushed = text;
        }
    }
}
