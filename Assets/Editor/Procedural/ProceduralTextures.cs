#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // Texture ve materyalleri kodla üretir — dışarıdan PNG gerekmez.
    // Perlin gürültü + prosedürel desen ile asfalt, kaldırım, bina cephesi,
    // araba boyası, cam, lastik, jant materyalleri.
    public static class ProceduralTextures
    {
        const string TextureFolder = "Assets/Generated/Textures";
        const string MaterialFolder = "Assets/Generated/Materials";

        [MenuItem("DreamCar/Procedural/Generate Textures & Materials")]
        public static void GenerateAll()
        {
            EnsureFolders();

            SaveTexture(BuildAsphalt(512), "asphalt");
            SaveTexture(BuildSidewalk(256), "sidewalk");
            SaveTexture(BuildBuildingFacade(256, 512), "facade_day");
            SaveTexture(BuildBuildingFacade(256, 512, night: true), "facade_night");
            SaveTexture(BuildRoadMarking(128), "road_marking");
            SaveTexture(BuildGrass(256), "grass");

            // Ana menü garajı. Sahne bugüne kadar tamamen boştu (yalnızca kamera
            // ve UI), o yüzden bu iki dokunun karşılığı da yoktu.
            SaveTexture(BuildGarageFloor(512), "garage_floor");
            SaveTexture(BuildGarageWall(256), "garage_wall");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Procedural] Texture ve materyaller üretildi.");
        }

        // ---------------------------------------------------------- Texture'lar

        // Asfalt: çok ölçekli gürültü + ince çakıl taneleri.
        public static Texture2D BuildAsphalt(int size)
        {
            var tex = NewTexture(size, size, "asphalt");
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size, ny = (float)y / size;

                float coarse = Mathf.PerlinNoise(nx * 8f, ny * 8f);
                float fine = Mathf.PerlinNoise(nx * 48f, ny * 48f);
                float grain = Mathf.PerlinNoise(nx * 180f, ny * 180f);

                float v = 0.16f + coarse * 0.06f + fine * 0.05f + grain * 0.05f;

                // Seyrek açık çakıl taneleri
                if (grain > 0.82f) v += 0.10f;

                pixels[y * size + x] = new Color(v, v * 0.99f, v * 1.02f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Kaldırım: kare taş deseni + fuga çizgileri.
        public static Texture2D BuildSidewalk(int size)
        {
            var tex = NewTexture(size, size, "sidewalk");
            var pixels = new Color[size * size];
            int tile = size / 4;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int lx = x % tile, ly = y % tile;
                bool joint = lx < 3 || ly < 3;

                float nx = (float)x / size, ny = (float)y / size;
                float noise = Mathf.PerlinNoise(nx * 40f, ny * 40f) * 0.06f;

                float v = joint ? 0.34f : 0.56f + noise;
                pixels[y * size + x] = new Color(v, v * 0.985f, v * 0.96f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Garaj zemini: koyu kare fayans ızgarası + iki kırmızı vurgu bandı.
        //
        // Fayans deseni bilerek kontrastlı: garaj aydınlatması tepeden geliyor
        // ve düz bir zemin o ışıkta ölçeksiz görünüyor — ızgara hem derinlik
        // hem araç boyutu algısı veriyor.
        public static Texture2D BuildGarageFloor(int size)
        {
            var tex = NewTexture(size, size, "garage_floor");
            var pixels = new Color[size * size];
            int tile = size / 8;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int lx = x % tile, ly = y % tile;
                bool joint = lx < 2 || ly < 2;

                float nx = (float)x / size, ny = (float)y / size;
                float grain = Mathf.PerlinNoise(nx * 90f, ny * 90f) * 0.035f;

                // Kırmızı bantlar: fayans sütunlarından ikisi. Referans zeminde
                // olduğu gibi mekâna yön veriyor.
                int col = x / tile;
                bool red = col == 2 || col == 5;

                Color c;
                if (red)
                {
                    float v = joint ? 0.26f : 0.44f + grain;
                    c = new Color(v, v * 0.24f, v * 0.22f, 1f);
                }
                else
                {
                    float v = joint ? 0.13f : 0.24f + grain;
                    c = new Color(v, v * 1.02f, v * 1.06f, 1f);
                }
                pixels[y * size + x] = c;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Garaj duvarı: dikey lamel panel. Lamel genişliği hafif değişiyor —
        // eşit aralıklı çizgi deseni yakından bakınca yapay duruyor.
        public static Texture2D BuildGarageWall(int size)
        {
            var tex = NewTexture(size, size, "garage_wall");
            var pixels = new Color[size * size];
            var rng = new System.Random(7);

            // Lamel sınırlarını önceden çıkar: her sütun için hangi lamelde
            // olduğunu ve o lamelin tonunu biliyoruz.
            var shade = new float[size];
            int x0 = 0;
            while (x0 < size)
            {
                int w = 10 + rng.Next(6);
                float tone = 0.82f + (float)rng.NextDouble() * 0.36f;
                for (int x = x0; x < Mathf.Min(size, x0 + w); x++)
                    shade[x] = (x == x0 || x == x0 + w - 1) ? tone * 0.55f : tone;
                x0 += w;
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size, ny = (float)y / size;
                float grain = Mathf.PerlinNoise(nx * 30f, ny * 120f) * 0.05f;
                float v = (0.30f + grain) * shade[x];
                pixels[y * size + x] = new Color(v * 1.06f, v * 0.94f, v * 0.78f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Bina cephesi: kat kat pencere ızgarası. Gece modunda pencerelerin bir kısmı yanar.
        public static Texture2D BuildBuildingFacade(int width, int height, bool night = false)
        {
            var tex = NewTexture(width, height, night ? "facade_night" : "facade_day");
            var pixels = new Color[width * height];

            const int windowsAcross = 4;
            const int floors = 10;
            int cellW = width / windowsAcross;
            int cellH = height / floors;
            int marginX = Mathf.Max(3, cellW / 5);
            int marginY = Mathf.Max(3, cellH / 4);

            var rng = new System.Random(night ? 1337 : 42);
            var litWindows = new bool[windowsAcross * floors];
            for (int i = 0; i < litWindows.Length; i++) litWindows[i] = rng.NextDouble() > 0.45;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int cx = x / cellW, cy = y / cellH;
                int lx = x % cellW, ly = y % cellH;

                bool isWindow = lx >= marginX && lx < cellW - marginX &&
                                ly >= marginY && ly < cellH - marginY;

                float nx = (float)x / width, ny = (float)y / height;
                float concreteNoise = Mathf.PerlinNoise(nx * 20f, ny * 20f) * 0.05f;
                Color concrete = new Color(0.44f + concreteNoise, 0.43f + concreteNoise, 0.42f + concreteNoise, 1f);

                if (!isWindow) { pixels[y * width + x] = concrete; continue; }

                int windowIndex = Mathf.Clamp(cy * windowsAcross + cx, 0, litWindows.Length - 1);
                if (night)
                {
                    pixels[y * width + x] = litWindows[windowIndex]
                        ? new Color(1f, 0.88f, 0.55f, 1f)
                        : new Color(0.06f, 0.07f, 0.10f, 1f);
                }
                else
                {
                    // Gündüz: camda gökyüzü yansıması gradyanı
                    float refl = 0.35f + (1f - (float)ly / cellH) * 0.35f;
                    pixels[y * width + x] = new Color(refl * 0.55f, refl * 0.68f, refl * 0.86f, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Yol çizgisi: ortadan kesikli beyaz şerit.
        public static Texture2D BuildRoadMarking(int size)
        {
            var tex = NewTexture(size, size, "road_marking");
            var pixels = new Color[size * size];
            int stripeHalf = Mathf.Max(2, size / 32);
            int center = size / 2;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inStripe = Mathf.Abs(x - center) < stripeHalf;
                bool dashOn = (y / (size / 4)) % 2 == 0;
                pixels[y * size + x] = inStripe && dashOn
                    ? new Color(0.92f, 0.90f, 0.84f, 1f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D BuildGrass(int size)
        {
            var tex = NewTexture(size, size, "grass");
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size, ny = (float)y / size;
                float patch = Mathf.PerlinNoise(nx * 6f, ny * 6f);
                float blade = Mathf.PerlinNoise(nx * 90f, ny * 90f);
                float g = 0.28f + patch * 0.14f + blade * 0.08f;
                pixels[y * size + x] = new Color(g * 0.45f, g, g * 0.35f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------- Materyaller

        public static Material CreatePaintMaterial(string name, Color color)
        {
            var mat = NewMaterial(name);
            SetColor(mat, color);
            SetFloat(mat, "_Metallic", 0.75f);
            SetFloat(mat, "_Smoothness", 0.82f);
            SetFloat(mat, "_Glossiness", 0.82f);
            mat.EnableKeyword("_EMISSION");
            SetColorProperty(mat, "_EmissionColor", Color.black);
            SaveMaterial(mat, name);
            return mat;
        }

        // Plaka: matbaa beyazı, yansımasız. Dokuyu LicensePlate çalışma anında
        // _BaseMap'e yazıyor (oyuncunun plaka metni).
        public static Material CreatePlateMaterial()
        {
            const string name = "car_plate";
            var mat = NewMaterial(name);
            SetColor(mat, Color.white);
            SetFloat(mat, "_Metallic", 0f);
            SetFloat(mat, "_Smoothness", 0.25f);
            SetFloat(mat, "_Glossiness", 0.25f);
            SaveMaterial(mat, name);
            return mat;
        }

        public static Material CreateGlassMaterial(string name)
        {
            var mat = NewMaterial(name);
            SetColor(mat, new Color(0.06f, 0.08f, 0.11f, 0.72f));
            SetFloat(mat, "_Metallic", 0.1f);
            SetFloat(mat, "_Smoothness", 0.95f);
            SetFloat(mat, "_Glossiness", 0.95f);
            MakeTransparent(mat);
            SaveMaterial(mat, name);
            return mat;
        }

        public static Material CreateTireMaterial()
        {
            var mat = NewMaterial("tire");
            SetColor(mat, new Color(0.055f, 0.055f, 0.06f));
            SetFloat(mat, "_Metallic", 0f);
            SetFloat(mat, "_Smoothness", 0.22f);
            SetFloat(mat, "_Glossiness", 0.22f);
            SaveMaterial(mat, "tire");
            return mat;
        }

        public static Material CreateRimMaterial()
        {
            var mat = NewMaterial("rim");
            SetColor(mat, new Color(0.68f, 0.70f, 0.74f));
            SetFloat(mat, "_Metallic", 0.9f);
            SetFloat(mat, "_Smoothness", 0.78f);
            SetFloat(mat, "_Glossiness", 0.78f);
            mat.EnableKeyword("_EMISSION");
            SetColorProperty(mat, "_EmissionColor", Color.black);
            SaveMaterial(mat, "rim");
            return mat;
        }

        public static Material CreateEmissiveMaterial(string name, Color emission)
        {
            var mat = NewMaterial(name);
            SetColor(mat, Color.white);
            mat.EnableKeyword("_EMISSION");
            SetColorProperty(mat, "_EmissionColor", emission);
            SaveMaterial(mat, name);
            return mat;
        }

        public static Material CreateTexturedMaterial(string name, string textureName,
                                                      float metallic = 0f, float smoothness = 0.3f,
                                                      Vector2 tiling = default)
        {
            var mat = NewMaterial(name);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{textureName}.png");
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (tiling != default)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling);
                    if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", tiling);
                }
            }
            SetFloat(mat, "_Metallic", metallic);
            SetFloat(mat, "_Smoothness", smoothness);
            SetFloat(mat, "_Glossiness", smoothness);
            SaveMaterial(mat, name);
            return mat;
        }

        // ---------------------------------------------------------- Yardımcılar

        // URP varsa Lit, yoksa Built-in Standard — proje hangi pipeline'daysa çalışır.
        static Material NewMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { name = name };
        }

        static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        static void SetColorProperty(Material mat, string prop, Color c)
        {
            if (mat.HasProperty(prop)) mat.SetColor(prop, c);
        }

        static void SetFloat(Material mat, string prop, float value)
        {
            if (mat.HasProperty(prop)) mat.SetFloat(prop, value);
        }

        static void MakeTransparent(Material mat)
        {
            // URP
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            // Built-in
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static Texture2D NewTexture(int width, int height, string name) =>
            new(width, height, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };

        static void SaveTexture(Texture2D tex, string name)
        {
            EnsureFolders();
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            Object.DestroyImmediate(tex);
        }

        static void SaveMaterial(Material mat, string name)
        {
            EnsureFolders();
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = mat.shader;
                existing.CopyPropertiesFromMaterial(mat);
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(mat, path);
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Assets/Generated", TextureFolder, MaterialFolder })
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }
    }
}
#endif
