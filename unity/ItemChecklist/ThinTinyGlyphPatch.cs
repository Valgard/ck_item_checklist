using System;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Iter-25: thinTiny (rrs5) lacks German/Western-European accented glyphs, so CK's
    /// PugFont.GetGlyphData falls them back to the chinese font (CJK metric) -> deformed
    /// umlauts in the chrome labels. This inserts 85 mod-authored glyphs into thinTiny at
    /// runtime: new glyphData entries + codePoints, volatileSprite cut from our bundle
    /// sheet (Sprite.Create on the sheet's texture). The first GetGlyphData branch
    /// (codePoints.TryGetValue) then wins before any fallback. thinTiny is CK's digits-only
    /// font, so adding glyphs is harmless elsewhere. See the reference-ck-pugfont-architecture
    /// memory. Runs once at the OnOccupied bake anchor (Manager.text is ready there).
    /// </summary>
    internal static class ThinTinyGlyphPatch
    {
        private static bool _done;

        // { char code, sheet rect x, y (Unity bottom-left), w, h }. w = advance width.
        private static readonly int[,] Glyphs = new int[,]
        {
            { 161, 240, 70, 3, 10 },
            { 169, 224, 60, 7, 10 },
            { 191, 232, 70, 3, 10 },
            { 192, 0, 70, 3, 10 },
            { 193, 8, 70, 3, 10 },
            { 194, 16, 70, 3, 10 },
            { 195, 24, 70, 3, 10 },
            { 196, 32, 70, 3, 10 },
            { 197, 40, 70, 3, 10 },
            { 198, 208, 70, 5, 10 },
            { 199, 48, 70, 2, 10 },
            { 200, 56, 70, 3, 10 },
            { 201, 64, 70, 3, 10 },
            { 202, 72, 70, 3, 10 },
            { 203, 80, 70, 3, 10 },
            { 204, 96, 70, 3, 10 },
            { 205, 88, 70, 3, 10 },
            { 206, 104, 70, 3, 10 },
            { 207, 112, 70, 3, 10 },
            { 209, 120, 70, 4, 10 },
            { 210, 128, 70, 3, 10 },
            { 211, 136, 70, 3, 10 },
            { 212, 144, 70, 3, 10 },
            { 213, 152, 70, 3, 10 },
            { 214, 160, 70, 3, 10 },
            { 216, 168, 70, 4, 10 },
            { 217, 176, 70, 3, 10 },
            { 218, 184, 70, 3, 10 },
            { 219, 192, 70, 3, 10 },
            { 220, 200, 70, 3, 10 },
            { 223, 224, 70, 4, 10 },
            { 224, 0, 60, 3, 10 },
            { 225, 8, 60, 3, 10 },
            { 226, 16, 60, 3, 10 },
            { 227, 24, 60, 3, 10 },
            { 228, 32, 60, 3, 10 },
            { 229, 40, 60, 3, 10 },
            { 230, 208, 60, 5, 10 },
            { 231, 48, 60, 2, 10 },
            { 232, 56, 60, 3, 10 },
            { 233, 64, 60, 3, 10 },
            { 234, 72, 60, 3, 10 },
            { 235, 80, 60, 3, 10 },
            { 236, 96, 60, 3, 10 },
            { 237, 88, 60, 3, 10 },
            { 238, 104, 60, 3, 10 },
            { 239, 112, 60, 3, 10 },
            { 241, 120, 60, 4, 10 },
            { 242, 128, 60, 3, 10 },
            { 243, 136, 60, 3, 10 },
            { 244, 144, 60, 3, 10 },
            { 245, 152, 60, 3, 10 },
            { 246, 160, 60, 3, 10 },
            { 248, 168, 60, 4, 10 },
            { 249, 176, 60, 3, 10 },
            { 250, 184, 60, 3, 10 },
            { 251, 192, 60, 3, 10 },
            { 252, 200, 60, 3, 10 },
            { 268, 160, 50, 3, 10 },
            { 269, 168, 50, 3, 10 },
            { 280, 88, 40, 3, 10 },
            { 282, 232, 50, 3, 10 },
            { 283, 240, 50, 3, 10 },
            { 338, 216, 70, 5, 10 },
            { 339, 216, 60, 5, 10 },
            { 344, 216, 50, 3, 10 },
            { 345, 224, 50, 3, 10 },
            { 352, 176, 50, 3, 10 },
            { 366, 184, 50, 3, 10 },
            { 367, 192, 50, 3, 10 },
            { 381, 200, 50, 3, 10 },
            { 382, 208, 50, 3, 10 },
            { 1025, 56, 10, 3, 10 },
            { 1028, 32, 10, 4, 10 },
            { 1105, 64, 10, 3, 10 },
            { 1108, 40, 10, 4, 10 },
            { 8211, 104, 110, 5, 10 },
            { 8220, 96, 110, 4, 10 },
            { 8221, 112, 110, 4, 10 },
            { 8222, 88, 110, 4, 10 },
            { 8230, 248, 80, 5, 10 },
            { 8482, 232, 60, 7, 10 },
            { 9787, 0, 100, 7, 10 },
            { 9825, 8, 110, 5, 10 },
            { 9829, 0, 110, 5, 10 },
        };

        public static void InsertOnce()
        {
            if (_done) return;
            try
            {
                var tm = Manager.text;
                var f = tm != null ? tm.thinTiny : null;
                if (f == null || f.codePoints == null || f.glyphData == null) return;

                var bundle = ItemChecklistMod.AssetBundle;
                if (bundle == null) { Debug.LogWarning("[ItemChecklist] iter-25: AssetBundle null, glyph insert skipped"); return; }
                var sheet = bundle.LoadAsset<Sprite>("Assets/ItemChecklist/Art/thinTiny_glyphs.png");
                if (sheet == null) { Debug.LogWarning("[ItemChecklist] iter-25: thinTiny_glyphs sprite not found in bundle"); return; }
                var tex = sheet.texture;

                int n = Glyphs.GetLength(0);
                int baseIdx = f.glyphData.Length;
                var gd = new PugFont.GlyphData[baseIdx + n];
                for (int i = 0; i < baseIdx; i++) gd[i] = f.glyphData[i];

                int inserted = 0;
                for (int i = 0; i < n; i++)
                {
                    int code = Glyphs[i, 0], x = Glyphs[i, 1], y = Glyphs[i, 2], w = Glyphs[i, 3], h = Glyphs[i, 4];
                    // Replicate PugFont.InitCodePoints' sprite convention exactly: outline
                    // padding (y+1, h-1, then x-1, w+2) + a CENTERED pivot. A (0,0) pivot
                    // renders the glyph shifted up-right.
                    var rect2 = new Rect(x, y + 1, w, h - 1);
                    if (rect2.width + rect2.x + 2f < tex.width) { rect2.width += 2f; rect2.x -= 1f; }
                    int num = (int)rect2.width / 2;
                    int num2 = (int)rect2.height / 2;
                    var pivot = new Vector2((float)num / rect2.width, (float)num2 / rect2.height);
                    var sprite = Sprite.Create(tex, rect2, pivot, 16f, 0, SpriteMeshType.FullRect);
                    var g = new PugFont.GlyphData();
                    g.rect = new RectInt(x, y, w, h);   // width = advance
                    g.volatileSprite = sprite;
                    int idx = baseIdx + i;
                    gd[idx] = g;
                    f.codePoints[(char)code] = idx;
                    inserted++;
                }
                f.glyphData = gd;
                _done = true;
                Debug.Log("[ItemChecklist] iter-25: inserted " + inserted + " accented glyphs into thinTiny");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ItemChecklist] iter-25 glyph insert threw: " + ex.Message);
            }
        }
    }
}
