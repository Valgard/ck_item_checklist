using System.Collections.Generic;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>Applies a pet skin's gradient recolor to the row icon SpriteRenderer,
    /// mirroring CK's inventory-slot pet rendering: build a Texture2D from the skin's
    /// GradientMapDataBlock, set it as the material's _GradientMap, enable the
    /// USE_GRADIENT_MAP keyword. Skin 0 / missing gradient (or a material without the
    /// keyword) → plain base sprite with the keyword disabled. Requires the asmdef to
    /// reference ScriptableData.dll (GradientMapDataBlock lives there).</summary>
    internal static class PetSkinIcon
    {
        private static readonly Dictionary<object, Texture2D> s_cache = new Dictionary<object, Texture2D>();

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
            if (grad == null || !grad.hasData) { Disable(icon); return; }

            if (!s_cache.TryGetValue(grad, out var tex) || tex == null)
            {
                tex = new Texture2D(grad.textureWidth, 1, TextureFormat.ARGB32, mipChain: false);
                var px = new Color32[tex.width];
                for (int i = 0; i < tex.width; i++) px[i] = grad.GetPixel(i);
                tex.SetPixels32(px);
                tex.Apply();
                s_cache[grad] = tex;
            }

            var mat = icon.material;   // per-renderer instance (Unity auto-instantiates)
            mat.EnableKeyword("USE_GRADIENT_MAP");
            mat.SetTexture("_GradientMap", tex);
        }

        public static void Disable(SpriteRenderer icon)
        {
            if (icon != null && icon.sharedMaterial != null)
                icon.material.DisableKeyword("USE_GRADIENT_MAP");
        }
    }
}
