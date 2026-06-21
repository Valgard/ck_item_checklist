using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Recolors the row icon for a pet skin, mirroring CK's inventory-slot pet
    /// rendering. The gradient-capable UI shader is "Amplify/UISpriteColorReplace" — the
    /// exact shader Item Browser uses (`UserInterfaceUtility.GetUISpriteColorReplaceMaterial`
    /// = `new Material(Shader.Find("Amplify/UISpriteColorReplace"))`); it carries the
    /// `_GradientMap` property + `USE_GRADIENT_MAP` keyword. The mod's default icon
    /// material does NOT, so the keyword alone is a no-op — the icon needs a material on
    /// this shader.
    ///
    /// One SHARED material per skin (keyword on + its gradient texture), assigned via
    /// `sharedMaterial`. SpriteRenderer feeds `_MainTex` per-renderer from the sprite, so
    /// one skin material recolors every row showing that skin onto its own base sprite —
    /// no per-row material instances. Non-pet / uncollected rows are restored to the
    /// pristine base material captured at pool build (so they stay byte-identical to
    /// before this iteration). If the shader can't be found, falls back to the plain
    /// base sprite (degraded, not broken).</summary>
    internal static class PetSkinIcon
    {
        private static readonly int GradientMapId = Shader.PropertyToID("_GradientMap");
        private const string Keyword = "USE_GRADIENT_MAP";

        private static Shader s_shader;
        private static bool s_shaderResolved;
        private static Material s_baseMaterial;   // pristine prefab icon material (non-gradient)
        private static readonly Dictionary<object, Material> s_skinMats = new Dictionary<object, Material>();
        private static readonly Dictionary<object, Texture2D> s_texCache = new Dictionary<object, Texture2D>();

        /// <summary>Capture the pristine icon material once, before any recolor, so
        /// non-pet rows can be restored exactly. Called from the pool build.</summary>
        public static void CaptureBase(SpriteRenderer icon)
        {
            if (s_baseMaterial == null && icon != null) s_baseMaterial = icon.sharedMaterial;
        }

        private static Shader GradShader()
        {
            if (!s_shaderResolved)
            {
                s_shaderResolved = true;
                s_shader = Shader.Find("Amplify/UISpriteColorReplace");
                if (s_shader == null)
                    Debug.LogWarning("[ItemChecklist] gradient shader 'Amplify/UISpriteColorReplace' not found — pet skins fall back to plain icons");
            }
            return s_shader;
        }

        public static void Apply(SpriteRenderer icon, int petObjectId, int skinIndex, Sprite baseSprite)
        {
            if (icon == null) return;
            icon.sprite = baseSprite;
            icon.color = Color.white;

            var table = Manager.ui != null ? Manager.ui.petInfosTable : null;
            var info = table != null ? table.GetPetSkinInfo((ObjectID)petObjectId) : null;
            if (info == null || info.skins == null || skinIndex < 0 || skinIndex >= info.skins.Count)
            { Disable(icon); return; }

            var grad = info.skins[skinIndex].primaryGradientMap;
            var shader = GradShader();
            if (grad == null || !grad.hasData || shader == null) { Disable(icon); return; }

            if (!s_skinMats.TryGetValue(grad, out var mat) || mat == null)
            {
                if (!s_texCache.TryGetValue(grad, out var tex) || tex == null)
                {
                    tex = new Texture2D(grad.textureWidth, 1, TextureFormat.ARGB32, mipChain: false);
                    var px = new Color32[tex.width];
                    for (int i = 0; i < tex.width; i++) px[i] = grad.GetPixel(i);
                    tex.SetPixels32(px);
                    tex.Apply();
                    s_texCache[grad] = tex;
                }
                mat = new Material(shader);
                mat.EnableKeyword(Keyword);
                mat.SetTexture(GradientMapId, tex);
                s_skinMats[grad] = mat;
            }
            icon.sharedMaterial = mat;
        }

        /// <summary>Restore the pristine (non-gradient) base material — for non-pet rows
        /// and uncollected pet skins (which show the unknown icon).</summary>
        public static void Disable(SpriteRenderer icon)
        {
            if (icon != null && s_baseMaterial != null && icon.sharedMaterial != s_baseMaterial)
                icon.sharedMaterial = s_baseMaterial;
        }
    }
}
