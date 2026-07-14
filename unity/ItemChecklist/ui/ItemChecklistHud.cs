using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Always-on HUD counter (top-right) mirroring the window footer's
    /// discovered/total counter. This is NOT a modal CoreLib UI — it is
    /// instantiated directly by <see cref="ItemChecklistMod"/> and parented
    /// under the in-game HUD root, so it must never be passed to
    /// UserInterfaceModule.RegisterModUI.
    ///
    /// <para>Visibility is explicit (not scale-based): <see cref="hudRoot"/> is
    /// activated only while <see cref="WorldState.IsInPlayableWorld"/> (in-game,
    /// scene-handler ready, no scene load queued — this is what actually
    /// suppresses both the entry and the exit-to-menu load screen; see that
    /// helper for why <c>Manager.main.player != null</c> alone does NOT) AND
    /// <c>!Manager.ui.isAnyInventoryShowing</c> (covers inventory, crafting and
    /// the checklist window) AND <c>!Manager.menu.IsAnyMenuActive()</c>.
    /// <c>Manager.ui.CalcGameplayUITargetScaleMultiplier()</c> — CK's own HUD
    /// idiom — returns (0,0,0) for a mod HUD and is deliberately NOT used.
    /// Render visibility additionally relies on the prefab being on the HUD Unity
    /// layer (27) at local z=10; see docs/gotchas.md § HUD Counter.</para>
    /// </summary>
    public class ItemChecklistHud : UIelement
    {
        // Editor-wired serialized fields (set in ItemChecklistHUD.prefab).
        public GameObject hudRoot;    // the scaled/toggled container
        public PugText counterText;   // renders the "N / M (p.p%)" string

        public static ItemChecklistHud Instance { get; private set; }

        protected void Awake()
        {
            Instance = this;
            // Iter-37: the discovery event is numerator-driven (like the scan), so it routes
            // through the change-gated RefreshIfChanged — not an unconditional Refresh — which
            // also skips the possession-mode repaint when a discovery leaves the owned tally K
            // unchanged. The one-shot initial paint below is unconditional.
            DiscoveredState.Instance.Changed += RefreshIfChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            DiscoveredState.Instance.Changed -= RefreshIfChanged;
            if (Instance == this) Instance = null;
        }

        protected override void LateUpdate()
        {
            if (hudRoot != null)
            {
                // Good-HUD-citizen visibility via explicit, proven signals (the
                // CalcGameplayUITargetScaleMultiplier idiom returns (0,0,0) here —
                // it is not a drop-in scale source for a mod HUD). Hidden during
                // either load screen (WorldState.IsInPlayableWorld) and when the
                // player inventory / crafting / the checklist window (a CoreLib mod
                // UI, covered by the aggregate isAnyInventoryShowing) or any menu is
                // open; shown only while actually playing in a world.
                bool show = WorldState.IsInPlayableWorld
                            && !Manager.ui.isAnyInventoryShowing
                            && !Manager.menu.IsAnyMenuActive()
                            && ItemChecklist.ModConfig.Enabled;
                if (hudRoot.activeSelf != show) hudRoot.SetActive(show);
            }
            base.LateUpdate();
        }

        // Iter-37: the numerator this HUD last painted, so the numerator-driven triggers can skip
        // a no-op PugText.Render (which rebuilds glyph SpriteRenderers). -1 = nothing painted yet
        // → the first Refresh always renders. The change-gate lives HERE, with the counter it
        // describes, rather than as a static field + cross-class setter back in ItemChecklistMod.
        private int _lastCounter = -1;

        /// <summary>Re-render the counter from the current catalog + state. Cheap to call: runs on
        /// discovery / scan / mode / bake changes, never per frame (PugText.Render rebuilds glyph
        /// SpriteRenderers). This is the SINGLE render point — <see cref="RefreshIfChanged"/> and
        /// every unconditional caller (bake / loc re-bake / the Awake initial paint) end here, and
        /// it always leaves <see cref="_lastCounter"/> equal to what is on screen.</summary>
        public void Refresh()
        {
            if (counterText == null) return;
            var catalog = ItemChecklistMod.Catalog;
            int total = (catalog == null) ? 0 : catalog.Count;
            // Iter-36: numerator = the mode-selected count (discovery N or possession K),
            // shared with the window footer via ItemChecklistMod.CurrentCounterNumerator so
            // the two surfaces never drift (ProgressFormat keeps the strings identical).
            int shown = ItemChecklistMod.CurrentCounterNumerator();
            counterText.Render(ProgressFormat.Counter(shown, total));
            _lastCounter = shown;
        }

        /// <summary>Iter-37: repaint only when the displayed numerator actually changed — the entry
        /// point for the numerator-driven triggers (the recurring 3s possession scan, the discovery
        /// event, and the mode toggle), none of which changes the denominator (Catalog.Count).
        /// Denominator-changing callers (bake / loc re-bake) use <see cref="Refresh"/> directly.</summary>
        public void RefreshIfChanged()
        {
            if (ItemChecklistMod.CurrentCounterNumerator() != _lastCounter) Refresh();
        }
    }
}
