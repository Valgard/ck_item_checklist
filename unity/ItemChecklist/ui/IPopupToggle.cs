namespace ItemChecklist.UI
{
    /// <summary>
    /// Implemented by any widget that owns a popup a <see cref="DropdownToggleButton"/>
    /// can open/close. Lets the shared <c>Dropdown.prefab</c> chrome carry ONE toggle
    /// type that both <see cref="DropdownWidget"/> (Sort) and
    /// <see cref="FilterWidget"/> (Filter, as a prefab variant of the chrome)
    /// drive — the toggle's owner is wired at runtime in each widget's Configure.
    /// </summary>
    public interface IPopupToggle
    {
        void TogglePopup();
    }
}
