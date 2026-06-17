namespace ItemChecklist.UI
{
    /// <summary>
    /// Clears the search box. A <see cref="ClickButton"/> (Iter-7 click pattern:
    /// guard on canBeClicked, then call base — now centralised in the base). Wired
    /// to the SearchBar in the window prefab; on click it empties the field's
    /// PugText and the shared model's SearchText. (The SearchBar's own LateUpdate
    /// would also push "" on the next frame, but we set it directly for immediacy.)
    /// </summary>
    public sealed class ClearSearchButton : ClickButton
    {
        public SearchBar searchBar;   // Editor-wired

        protected override void OnClick()
        {
            if (searchBar != null) searchBar.ResetText();
            var model = ItemChecklistMod.ListView;
            if (model != null) model.SearchText = "";
        }
    }
}
