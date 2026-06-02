namespace ItemChecklist.UI
{
    /// <summary>
    /// Clears the search box. Subclasses CK's ButtonUIElement (Iter-7 click
    /// pattern: guard on canBeClicked, then call base). Wired to the SearchBar
    /// in the window prefab; on click it empties the field's PugText and the
    /// shared model's SearchText. (The SearchBar's own LateUpdate would also
    /// push "" on the next frame, but we set it directly for immediacy.)
    /// </summary>
    public sealed class ClearSearchButton : ButtonUIElement
    {
        public SearchBar searchBar;   // Editor-wired

        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            if (searchBar != null) searchBar.ResetText();
            var model = ItemChecklistMod.ListView;
            if (model != null) model.SearchText = "";
        }
    }
}
