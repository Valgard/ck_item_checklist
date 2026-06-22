using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Hand-rolled vertical scrollbar handle for the popup. CK's native <c>ScrollBar</c> is
    /// driven by <c>UIScrollWindow.LateUpdate</c>, which the popup deliberately does NOT use
    /// (Weg 2 — manual translate), so the thumb's size/position/drag are computed here.
    /// Extends <see cref="ClickButton"/> for the 3D-collider click/held machinery; the owning
    /// <see cref="PopupWidget"/> is runtime-wired (no serialized cross-prefab ref).
    /// </summary>
    public sealed class PopupScrollHandle : ClickButton
    {
        public SpriteRenderer handleSprite;   // the thumb (maskInteraction: None so the popup mask doesn't clip it)
        public GameObject scrollbarRoot;       // track + handle container, toggled with overflow
        public float trackLength = 3.75f;      // travel span = cap height (calibrated in-game)
        public float minHandle = 0.6f;         // min thumb height in world units
        public PopupWidget owner;              // runtime-wired by PopupWidget.EnsurePanel

        /// <summary>Show/hide the whole scrollbar (off when the popup fits = no overflow).</summary>
        public void SetActiveScrolling(bool active)
        {
            if (scrollbarRoot != null && scrollbarRoot.activeSelf != active) scrollbarRoot.SetActive(active);
        }

        /// <summary>Thumb height ∝ viewport/content; thumb centre ∝ scroll fraction (0 top … 1 bottom).</summary>
        public void Sync(float scrollOffset, float contentH, float viewportH)
        {
            if (handleSprite == null || contentH <= viewportH) return;
            float h = Mathf.Max(trackLength * (viewportH / contentH), minHandle);
            handleSprite.size = new Vector2(handleSprite.size.x, h);
            float overflow = contentH - viewportH;
            float t = overflow > 0f ? Mathf.Clamp01(scrollOffset / overflow) : 0f;
            float top = trackLength * 0.5f - h * 0.5f;          // thumb-centre travel range
            handleSprite.transform.localPosition = new Vector3(
                handleSprite.transform.localPosition.x, Mathf.Lerp(top, -top, t),
                handleSprite.transform.localPosition.z);
        }

        protected override void OnClick() { }   // a plain click without drag = no-op

        // While the handle is held, map the cursor's track position to the scroll offset.
        private void Update()
        {
            if (owner != null && leftClickIsHeldDown)
                owner.SetScrollFromHandle(owner.CursorFractionInTrack(transform, trackLength));
        }
    }
}
