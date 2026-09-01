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

            // Ana menüde sekiz navigasyon butonu var, elimizde üç ikon vardı.
            // Kalan beşi burada; hepsi aşağıdaki Icon() + SDF yardımcılarıyla
            // çiziliyor, yeni altyapı gerekmiyor.
            Save(Car(96, Color.white), "icon_car");
            Save(Coin(96, Color.white), "icon_coin");
            Save(Chart(96, Color.white), "icon_chart");
            Save(Globe(96, Color.white), "icon_globe");
            Save(Plus(96, Color.white), "icon_plus");

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

        // --- Yeni ikonlar (normalize edilmiş 0..1 uzayda SDF) ---
        //
        // Gear/Trophy/Flag her biri kendi piksel döngüsünü taşıyor çünkü onlar
        // önce yazıldı. Aşağıdakiler ortak bir Icon() + birkaç SDF ilkesi
        // kullanıyor: kenar yumuşatma tek yerde, her ikon birkaç satır.

        // coverage(p) 0..1 arası kapsama döndürür; doğrudan alfa olur.
        static Texture2D Icon(int size, Color color, System.Func<Vector2, float> coverage)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Piksel MERKEZİ örnekleniyor (+0.5), aksi halde şekiller yarım
                // piksel kayar ve simetrik ikonlar simetrik çizilmez.
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float a = Mathf.Clamp01(coverage(p));
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Kenar yumuşatma genişliği — normalize uzayda ~1.5 piksel.
        static float Aa(int size) => 1.5f / size;

        static float Disc(Vector2 p, Vector2 c, float r, float aa)
            => Mathf.Clamp01((r - Vector2.Distance(p, c)) / aa);

        static float Ring(Vector2 p, Vector2 c, float r, float width, float aa)
            => Mathf.Clamp01((width * 0.5f - Mathf.Abs(Vector2.Distance(p, c) - r)) / aa);

        // Uçları yuvarlatılmış kalın çizgi.
        static float Bar(Vector2 p, Vector2 a, Vector2 b, float width, float aa)
            => Mathf.Clamp01((width * 0.5f - SegmentDistance(p, a, b)) / aa);

        static float Box(Vector2 p, float x0, float y0, float x1, float y1, float aa)
        {
            float dx = Mathf.Min(p.x - x0, x1 - p.x);
            float dy = Mathf.Min(p.y - y0, y1 - p.y);
            return Mathf.Clamp01(Mathf.Min(dx, dy) / aa);
        }

        static float Union(float a, float b) => Mathf.Max(a, b);
        static float Subtract(float a, float b) => Mathf.Min(a, 1f - b);

        // Yandan araba silueti — garaj / araç mağazası butonu.
        static Texture2D Car(int size, Color color)
        {
            float aa = Aa(size);
            return Icon(size, color, p =>
            {
                // Gövde + kabin birlikte; tekerlekler ayrı, üstlerinde gövdeden
                // oyulmuş boşluk yok — silueti okunur tutuyor.
                float body  = Box(p, 0.08f, 0.30f, 0.92f, 0.50f, aa);
                float cabin = Box(p, 0.30f, 0.50f, 0.70f, 0.68f, aa);
                // Kabinin ön camını eğimli göstermek için köşeyi kırp.
                float slant = Mathf.Clamp01(((0.78f - p.x) - (p.y - 0.50f) * 0.9f) / aa);
                cabin = Mathf.Min(cabin, slant);

                float wheelL = Disc(p, new Vector2(0.29f, 0.26f), 0.115f, aa);
                float wheelR = Disc(p, new Vector2(0.71f, 0.26f), 0.115f, aa);
                float hubL   = Disc(p, new Vector2(0.29f, 0.26f), 0.045f, aa);
                float hubR   = Disc(p, new Vector2(0.71f, 0.26f), 0.045f, aa);

                float shape = Union(Union(body, cabin), Union(wheelL, wheelR));
                return Subtract(shape, Union(hubL, hubR));
            });
        }

        // Madeni para — coin mağazası butonu.
        static Texture2D Coin(int size, Color color)
        {
            float aa = Aa(size);
            var c = new Vector2(0.5f, 0.5f);
            return Icon(size, color, p =>
            {
                float disc = Disc(p, c, 0.40f, aa);
                float rim  = Ring(p, c, 0.31f, 0.045f, aa);
                // Ortada dikey çubuk + iki yatay kol: para birimi işareti hissi
                // veriyor ve gerçek bir para birimi sembolünü taklit etmiyor.
                float stem = Bar(p, new Vector2(0.50f, 0.30f), new Vector2(0.50f, 0.70f), 0.075f, aa);
                float armA = Bar(p, new Vector2(0.38f, 0.54f), new Vector2(0.62f, 0.60f), 0.055f, aa);
                float armB = Bar(p, new Vector2(0.38f, 0.44f), new Vector2(0.62f, 0.50f), 0.055f, aa);
                return Subtract(disc, Union(rim, Union(stem, Union(armA, armB))));
            });
        }

        // Sütun grafiği — istatistik butonu.
        static Texture2D Chart(int size, Color color)
        {
            float aa = Aa(size);
            return Icon(size, color, p =>
            {
                float axis = Bar(p, new Vector2(0.14f, 0.16f), new Vector2(0.88f, 0.16f), 0.06f, aa);
                float b1 = Box(p, 0.22f, 0.20f, 0.36f, 0.48f, aa);
                float b2 = Box(p, 0.43f, 0.20f, 0.57f, 0.78f, aa);
                float b3 = Box(p, 0.64f, 0.20f, 0.78f, 0.62f, aa);
                return Union(axis, Union(b1, Union(b2, b3)));
            });
        }

        // Meridyenli küre — bölge seçici butonu.
        static Texture2D Globe(int size, Color color)
        {
            float aa = Aa(size);
            var c = new Vector2(0.5f, 0.5f);
            const float r = 0.38f;
            return Icon(size, color, p =>
            {
                float outline = Ring(p, c, r, 0.06f, aa);

                // Dikey meridyen: bir elipsin kenarı. Noktayı x ekseninde
                // gererek daireye çeviriyoruz, sonra aynı halka testini
                // uyguluyoruz — ayrı bir elips SDF'ine gerek yok.
                var stretched = new Vector2(c.x + (p.x - c.x) / 0.42f, p.y);
                float meridian = Ring(stretched, c, r, 0.055f, aa);
                // Halkanın dışına taşan kısmı kırp.
                meridian = Mathf.Min(meridian, Disc(p, c, r, aa));

                float eq  = Bar(p, new Vector2(0.13f, 0.50f), new Vector2(0.87f, 0.50f), 0.05f, aa);
                float par = Bar(p, new Vector2(0.20f, 0.665f), new Vector2(0.80f, 0.665f), 0.045f, aa);
                float par2 = Bar(p, new Vector2(0.20f, 0.335f), new Vector2(0.80f, 0.335f), 0.045f, aa);
                float lines = Mathf.Min(Union(eq, Union(par, par2)), Disc(p, c, r, aa));

                return Union(outline, Union(meridian, lines));
            });
        }

        // Artı — oda kur butonu.
        static Texture2D Plus(int size, Color color)
        {
            float aa = Aa(size);
            return Icon(size, color, p =>
            {
                float h = Bar(p, new Vector2(0.20f, 0.50f), new Vector2(0.80f, 0.50f), 0.16f, aa);
                float v = Bar(p, new Vector2(0.50f, 0.20f), new Vector2(0.50f, 0.80f), 0.16f, aa);
                return Union(h, v);
            });
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
