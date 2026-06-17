namespace ItemChecklist.UI
{
    /// <summary>
    /// Base for the mod's clickable UI controls. Centralises the uniform Iter-7
    /// click prologue — guard on <c>canBeClicked</c>, then call base — so every
    /// subclass only implements its action via <see cref="OnClick"/>. Adds no
    /// serialized fields, so it is prefab-neutral and never referenced by a prefab
    /// (abstract → never instantiated, no fileID concern). Each concrete control
    /// keeps its own file (one MonoBehaviour per file) and its 3D BoxCollider +
    /// empty spritesShown* lists per the ButtonUIElement notes in CLAUDE.md.
    /// </summary>
    public abstract class ClickButton : ButtonUIElement
    {
        public sealed override void OnLeftClicked(bool mod1, bool mod2)
        {
            if (!canBeClicked) return;
            base.OnLeftClicked(mod1, mod2);
            OnClick();
        }

        /// <summary>The subclass's click action. Runs only when clickable.</summary>
        protected abstract void OnClick();
    }
}
