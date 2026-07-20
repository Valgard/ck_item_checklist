using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Iter-40: directional HUD arrow ring pointing at the tracked item's holding
    /// containers — one SpriteRenderer per target tile, on a ring around the player-sprite
    /// origin, rotated to its XZ-plane bearing. Copy-adapt of caveling-divining-rod's
    /// ArrowRingRenderer (itself the structural twin of ItemChecklistHud, Iter-11.5).
    /// KEY DIVERGENCE from the divining-rod: NO radar distance-fade AND no arrive-hide —
    /// per user in-game feedback the arrow must stay visible at ANY distance, including
    /// right on the chest (the player still wants to see it point at the exact tile);
    /// and it mirrors the divining-rod ring: the POSITION sits at a fixed ring radius (never
    /// touched by scale) while the SIZE lerps with distance INDEPENDENTLY (near normal, far
    /// smaller) — keeping position and scale orthogonal is what makes it read right.</summary>
    public class TrackerHud : UIelement
    {
        public GameObject hudRoot;   // Editor-wired: the child GO toggled by the visibility gate.

        // Iter-40: the arrow sprite, Editor-wired in the prefab to the ui_checklist
        // "Tracker Arrow" sub-sprite (runtime-pooled SpriteRenderers can't be
        // individually prefab-wired, so one shared serialized reference feeds them all).
        public Sprite arrowSprite;

        public static TrackerHud Instance { get; private set; }

        // Iter-40 (CDR parity): the POSITION ring radius, never touched by scale. Calibrated in-game
        // to 1.4 world units — a tighter ring hugging the player (CDR uses 4; the user prefers closer).
        private const float RingRadius = 1.4f;

        // Iter-40 (CDR parity): distance scale-lerp, INDEPENDENT of position. near = 1.0 (normal,
        // the cap), far = 0.6, lerped over ScaleRefDistance tiles and saturated beyond (CDR values).
        private const float ScaleNear = 1.0f;
        private const float ScaleFar = 0.6f;
        private const float ScaleRefDistance = 30f;

        // Iter-40: the sprite is authored pointing a certain way; if in-game the arrows
        // are rotated off, set this (e.g. -90 for an up-pointing sprite). Calibrated in-game.
        private const float ArrowRotationOffsetDeg = 0f;

        // Iter-40 (ground-truth fix 2026-07-20): the ring-centre offset from the HUD anchor,
        // matching CK's FIXED camera look-offset so the ring sits exactly on the player-sprite.
        // CK renders the player a little above screen centre; the HUD anchor (hudRoot) sits at
        // world origin = screen centre, so a constant nudge aligns them. uiCamera world units;
        // calibrated in-game. (Why constant, not projected: measured only two cameras — the
        // game world is XZ, the HUD is the uiCamera's XY plane; they are separate coordinate
        // spaces with no clean WorldToViewportPoint between them — the uiCamera maps the
        // player's world-Y≈0 to a constant vp.y=0.5 and cannot see the game-world position.)
        private const float PlayerHudOffsetX = 0f;
        private const float PlayerHudOffsetY = 0.6f;

        private readonly List<SpriteRenderer> _arrowPool = new List<SpriteRenderer>(8);
        private int _hudLayer;

        protected void Awake()
        {
            Instance = this;
            _hudLayer = LayerMask.NameToLayer("HUD");
            if (arrowSprite == null)
                Debug.LogError("[ItemChecklist] TrackerHud.arrowSprite is not wired in the prefab. " +
                               "Arrows will be blank — assign the ui_checklist 'Tracker Arrow' sub-sprite.");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        protected override void LateUpdate()
        {
            if (hudRoot != null)
            {
                // Explicit visibility (never CalcGameplayUITargetScaleMultiplier — (0,0,0) for
                // mod HUDs, per Iter-11.5). Shown only while actually tracking, in gameplay.
                // Gate on WorldState.IsInPlayableWorld (the sibling ItemChecklistHud's gate),
                // NOT isInGame && player != null: the player exists at OnOccupied while the load
                // screen is still up and survives the exit transition (Iter-11.6/15), so a raw
                // player-null check lets the arrows flash over a teleport / Save-&-Quit fade.
                bool show =
                    WorldState.IsInPlayableWorld &&
                    !Manager.ui.isAnyInventoryShowing &&
                    !Manager.menu.IsAnyMenuActive() &&
                    ModConfig.Enabled &&
                    ModConfig.LocateEnabled &&
                    ItemTracker.IsActive;
                if (hudRoot.activeSelf != show) hudRoot.SetActive(show);
            }
            base.LateUpdate();
        }

        /// <summary>Per-frame from ItemChecklistMod.Update. One arrow per target world
        /// position (a holding container tile mapped to float3(x, 0, z)). No-op while hudRoot
        /// is inactive.</summary>
        public void Render(float3 playerWorldPos, IReadOnlyList<float3> targets)
        {
            if (hudRoot == null || !hudRoot.activeSelf) return;
            EnsurePoolSize(targets.Count);

            // Iter-40: the ring centres on the player-sprite via a constant offset (see
            // PlayerHudOffsetX/Y) — NOT a per-frame camera projection (measured impossible:
            // game-world XZ and HUD XY are separate spaces). hudRoot@origin already renders
            // ~on the player; this nudge absorbs CK's fixed camera look-offset so the arrow's
            // distance-from-player stays constant regardless of the target bearing.
            Vector3 ringCenter = new Vector3(PlayerHudOffsetX, PlayerHudOffsetY, 0f);

            for (int i = 0; i < _arrowPool.Count; i++)
            {
                var sr = _arrowPool[i];
                if (i >= targets.Count)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }
                float3 delta = targets[i] - playerWorldPos;
                // CK is XZ-plane (y is height, ~0 for ground) — bearing MUST be
                // atan2(delta.z, delta.x) (empirically verified, divining-rod 2026-06-07);
                // HUD-Y is sin(bearing), not delta.z.
                float distance = math.length(delta);
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                float bearing = math.atan2(delta.z, delta.x);
                // Iter-40: POSITION = player-centered ring, fixed radius, never touched by scale.
                sr.transform.localPosition = ringCenter + new Vector3(
                    math.cos(bearing) * RingRadius, math.sin(bearing) * RingRadius, 0f);
                sr.transform.localRotation = Quaternion.Euler(0, 0, math.degrees(bearing) + ArrowRotationOffsetDeg);
                // SIZE lerps with distance, INDEPENDENTLY of position (near = 1.0, far = 0.6).
                float scale = math.lerp(ScaleNear, ScaleFar, math.saturate(distance / ScaleRefDistance));
                sr.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = sr.color; c.a = 1f; sr.color = c;   // no radar fade — full opacity
            }
        }

        /// <summary>Hide all arrows immediately (idempotent).</summary>
        public void Hide()
        {
            for (int i = 0; i < _arrowPool.Count; i++)
                if (_arrowPool[i].gameObject.activeSelf) _arrowPool[i].gameObject.SetActive(false);
        }

        private void EnsurePoolSize(int needed)
        {
            while (_arrowPool.Count < needed)
            {
                var go = new GameObject($"TrackArrow_{_arrowPool.Count}");
                go.transform.SetParent(hudRoot.transform, false);
                go.layer = _hudLayer;   // HUD layer (27) — uiCamera draws it during plain gameplay
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = arrowSprite;
                _arrowPool.Add(sr);
            }
        }
    }
}
