#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // UI için sprite üretir — dışarıdan görsel gerekmez. 9-slice yuvarlak köşeli panel,
    // buton, daire (nitro/yakıt göstergesi), gradyan, ikonlar.
    public static class ProceduralUISprites
    {
        const string Folder = "Assets/Generated/UI";

        [MenuItem("DreamCar/Procedural/Generate UI Sprites")]
        public static void GenerateAll()
        {
            EnsureFolder();

            Save(RoundedRect(96, 96, 22, fill: Color.white, border: default, borderWidth: 0), "panel", border: 24);
            Save(RoundedRect(96, 96, 22, fill: new Color(1f, 1f, 1f, 0.14f),
                             border: new Color(1f, 1f, 1f, 0.55f), borderWidth: 3), "panel_outline", border: 24);
            Save(RoundedRect(96, 96, 46, fill: Color.white, border: default, borderWidth: 0), "pill", border: 46);
            Save(Circle(128, Color.white), "circle");
            Save(CircleRing(128, Color.white, 0.72f), "ring");
            Save(VerticalGradient(8, 256, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f)), "gradient_fade");
            Save(Chevron(64, Color.white), "chevron");
            Save(Gear(96, Color.white), "icon_gear");
            Save(Trophy(96, Color.white), "icon_trophy");
            Save(Flag(96, Color.white), "icon_flag");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Procedural] UI sprite'ları üretildi: " + Folder);
        }

        // --- Şekiller ---

        // Yuvarlak köşeli dikdörtgen. borderWidth > 0 ise çerçeve çizer.
        static Texture2D RoundedRect(int w, int h, int radius, Color fill, Color border, int borderWidth)
        {
            var tex = New(w, h);
            var px = new Color[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectDistance(x, y, w, h, radius);

                // d < 0 içeride, 0 kenarda. Antialias için 1 piksellik geçiş.
                float alpha = Mathf.Clamp01(0.5f - d);

                Color c = fill;
                if (borderWidth > 0)
                {
                    float inner = RoundedRectDistance(x, y, w, h, radius) + borderWidth;
                    float borderAlpha = Mathf.Clamp01(0.5f - d) - Mathf.Clamp01(0.5f - inner);
                    c = Color.Lerp(fill, border, Mathf.Clamp01(borderAlpha));
                }

                px[y * w + x] = new Color(c.r, c.g, c.b, c.a * alpha);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Yuvarlak dikdörtgene işaretli mesafe (negatif = iç).
        static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float halfW = cx - radius;
            float halfH = cy - radius;

            float dx = Mathf.Abs(x + 0.5f - cx) - halfW;
            float dy = Mathf.Abs(y + 0.5f - cy) - halfH;

            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius + radius; // radius zaten halfW/H'den düşüldü
        }

        static Texture2D Circle(int size, Color color)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            float r = size * 0.5f - 1f;
            float c = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float alpha = Mathf.Clamp01(r - d + 0.5f);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D CircleRing(int size, Color color, float innerRatio)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            float outer = size * 0.5f - 1f;
            float inner = outer * innerRatio;
            float c = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D VerticalGradient(int w, int h, Color top, Color bottom)
        {
            var tex = New(w, h);
            var px = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / (h - 1));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ">" oku — carousel butonları için.
        static Texture2D Chevron(int size, Color color)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            float thickness = size * 0.14f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // İki çizgi parçası: (0.3,0.15)→(0.7,0.5) ve (0.7,0.5)→(0.3,0.85)
                var p = new Vector2((float)x / size, (float)y / size);
                float d1 = SegmentDistance(p, new Vector2(0.32f, 0.16f), new Vector2(0.68f, 0.5f));
                float d2 = SegmentDistance(p, new Vector2(0.68f, 0.5f), new Vector2(0.32f, 0.84f));
                float d = Mathf.Min(d1, d2) * size;
                float alpha = Mathf.Clamp01(thickness - d);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Dişli ikonu — ayarlar butonu.
        static Texture2D Gear(int size, Color color)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            float c = size * 0.5f;
            float outer = size * 0.40f;
            float inner = size * 0.30f;
            float hole = size * 0.14f;
            const int teeth = 8;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var v = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = v.magnitude;
                float angle = Mathf.Atan2(v.y, v.x);

                // Diş dalgası: açıya göre yarıçap salınır
                float wave = Mathf.Cos(angle * teeth);
                float radius = Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, wave * 0.5f + 0.5f));

                float alpha = Mathf.Clamp01(radius - d + 0.5f) * Mathf.Clamp01(d - hole + 0.5f);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Kupa ikonu — başarımlar butonu.
        static Texture2D Trophy(int size, Color color)
        {
            var tex = New(size, size);
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((float)x / size, (float)y / size);
                bool inside =
                    // kase
                    (p.y > 0.45f && p.y < 0.82f &&
                     Mathf.Abs(p.x - 0.5f) < Mathf.Lerp(0.28f, 0.20f, (0.82f - p.y) / 0.37f)) ||
                    // gövde
                    (p.y > 0.28f && p.y <= 0.45f && Mathf.Abs(p.x - 0.5f) < 0.06f) ||
                    // taban
                    (p.y > 0.18f && p.y <= 0.28f && Mathf.Abs(p.x - 0.5f) < 0.22f) ||
                    // kulplar
                    (p.y > 0.58f && p.y < 0.76f &&
                     (Mathf.Abs(p.x - 0.24f) < 0.05f || Mathf.Abs(p.x - 0.76f) < 0.05f));

                px[y * size + x] = inside ? color : new Color(color.r, color.g, color.b, 0f);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Damalı bayrak — yarış modu ikonu.
        static Texture2D Flag(int size, Color color)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            int squares = 4;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((float)x / size, (float)y / size);

                bool pole = Mathf.Abs(p.x - 0.20f) < 0.035f && p.y > 0.12f;
                bool cloth = p.x > 0.22f && p.x < 0.86f && p.y > 0.52f && p.y < 0.88f;

                if (pole) { px[y * size + x] = color; continue; }

                if (cloth)
                {
                    int cx = (int)((p.x - 0.22f) / (0.64f / squares));
                    int cy = (int)((p.y - 0.52f) / (0.36f / (squares / 2)));
                    bool dark = (cx + cy) % 2 == 0;
                    px[y * size + x] = dark ? color : new Color(color.r, color.g, color.b, color.a * 0.25f);
                    continue;
                }

                px[y * size + x] = new Color(color.r, color.g, color.b, 0f);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // --- Yardımcılar ---

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static Texture2D New(int w, int h) =>
            new(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        static void Save(Texture2D tex, string name, int border = 0)
        {
            EnsureFolder();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            if (border > 0)
            {
                // 9-slice: köşeler bozulmadan esner.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(border, border, border, border);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }

        static void EnsureFolder()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
        }
    }
}
#endif
