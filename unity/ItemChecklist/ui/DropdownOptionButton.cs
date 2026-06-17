namespace ItemChecklist.UI
{
    /// <summary>Click target for one option row inside the popup.</summary>
    public sealed class DropdownOptionButton : ClickButton
    {
        public DropdownWidget owner;
        public int index;
        protected override void OnClick()
        {
            if (owner != null) owner.SelectOption(index);
        }
    }
}
