using PugMod;
using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Top-level UI controller. Builds the window on first <see cref="Toggle"/>
    /// and shows/hides it on subsequent calls. F1 scope: a placeholder window
    /// with the mod name; F2 will replace the placeholder with the
    /// virtualized item list + search + filter + counter.
    ///
    /// <para>The canvas is parented to the game's existing UI camera
    /// (<c>API.Rendering.UICamera</c>) and rendered in
    /// <see cref="RenderMode.ScreenSpaceCamera"/> mode with a high sorting
    /// order so the window draws above the game's HUD.</para>
    /// </summary>
    public sealed class UiController
    {
        private GameObject root;

        public bool IsVisible => root != null && root.activeSelf;

        public void Toggle()
        {
            Debug.Log($"[ItemChecklist] Toggle() called (root={(root == null ? "null" : "exists")})");
            if (root == null)
            {
                try { BuildUi(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ItemChecklist] BuildUi threw: {e}");
                    return;
                }
            }
            if (root == null) { Debug.LogWarning("[ItemChecklist] root is still null after BuildUi"); return; }
            bool wasActive = root.activeSelf;
            root.SetActive(!wasActive);
            Debug.Log($"[ItemChecklist] window {(wasActive ? "hidden" : "shown")}");
        }

        private void BuildUi()
        {
            Debug.Log("[ItemChecklist] BuildUi: starting");
            // API.Rendering.UICamera is a PugCamera (MonoBehaviour). Its
            // GameObject also carries a UnityEngine.Camera which is what
            // Canvas.worldCamera expects. If the UICamera isn't ready yet
            // (very early Toggle press), bail out silently — the next press
            // will retry.
            var uiCamMb = API.Rendering.UICamera;
            if (uiCamMb == null) { Debug.LogWarning("[ItemChecklist] BuildUi: UICamera (PugCamera) is null"); return; }
            var uiCam = uiCamMb.GetComponent<Camera>();
            if (uiCam == null) { Debug.LogWarning("[ItemChecklist] BuildUi: UICamera GameObject has no Camera component"); return; }
            Debug.Log($"[ItemChecklist] BuildUi: UICamera resolved, building canvas");

            // Root canvas, attached to the game's existing UI camera.
            root = new GameObject("ItemChecklist.Root");
            root.transform.SetParent(uiCamMb.transform, worldPositionStays: false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.sortingOrder = 1000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            // Window (single child, anchored to ~70%-95% × 10%-90% of the screen).
            var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(root.transform, worldPositionStays: false);
            var rt = (RectTransform) window.transform;
            rt.anchorMin = new Vector2(0.7f, 0.1f);
            rt.anchorMax = new Vector2(0.95f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            window.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Placeholder text centered in the window.
            var text = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(window.transform, worldPositionStays: false);
            var trt = (RectTransform) text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var label = text.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 24;
            label.text = "Item Checklist\n\n(F2 will populate the list)";
        }
    }
}
