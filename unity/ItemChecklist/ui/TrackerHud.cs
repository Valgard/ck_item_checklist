using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Iter-40: directional HUD arrow ring pointing at the tracked item's holding
    /// containers — one SpriteRenderer per target tile, on a ring around the player-sprite
    /// origin, rotated to its XZ-plane bearing. Copy-adapt of caveling-divining-rod's
    /// ArrowRingRenderer (itself the structural twin of ItemChecklistHud, Iter-11.5).
    /// KEY DIVERGENCE from the divining-rod: NO radar distance-fade — the tracked target is
    /// intentional and may be far across the map, so arrows stay fully visible at any
    /// distance; an arrow hides only when the player is essentially on its tile.</summary>
    public class TrackerHud : UIelement
    {
        public GameObject hudRoot;   // Editor-wired: the child GO toggled by the visibility gate.

        // Iter-40: the arrow sprite, Editor-wired in the prefab to the ui_checklist
        // "Tracker Arrow" sub-sprite (runtime-pooled SpriteRenderers can't be
        // individually prefab-wired, so one shared serialized reference feeds them all).
        public Sprite arrowSprite;

        public static TrackerHud Instance { get; private set; }

        private const float RingRadius = 1.4f;          // world units on the uiCamera HUD plane
        private const float ArrowScale = 1.0f;
        private const float ArriveHideDistance = 1.5f;  // hide a target's arrow within ~1.5 tiles

        // Iter-40: the sprite is authored pointing a certain way; if in-game the arrows
        // are rotated off, set this (e.g. -90 for an up-pointing sprite). Calibrated in-game.
        private const float ArrowRotationOffsetDeg = 0f;

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
                bool show =
                    Manager.sceneHandler != null && Manager.sceneHandler.isInGame &&
                    Manager.main != null && Manager.main.player != null &&
                    !Manager.ui.isAnyInventoryShowing &&
                    !Manager.menu.IsAnyMenuActive() &&
                    ModConfig.Enabled &&
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
            for (int i = 0; i < _arrowPool.Count; i++)
            {
                var sr = _arrowPool[i];
                if (i >= targets.Count)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }
                float3 delta = targets[i] - playerWorldPos;
                // CK is XZ-plane (y is height, ~0 for ground); distance uses length(delta)
                // since y==0, bearing MUST be atan2(delta.z, delta.x) (empirically verified,
                // divining-rod 2026-06-07). HUD-Y is sin(bearing), not delta.z.
                float distance = math.length(delta);
                if (distance <= ArriveHideDistance)   // arrived: on the tile -> hide this arrow
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                float bearing = math.atan2(delta.z, delta.x);
                sr.transform.localPosition = new Vector3(
                    math.cos(bearing) * RingRadius, math.sin(bearing) * RingRadius, 0f);
                sr.transform.localRotation = Quaternion.Euler(0, 0, math.degrees(bearing) + ArrowRotationOffsetDeg);
                sr.transform.localScale = new Vector3(ArrowScale, ArrowScale, 1f);
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
