#if UNITY_EDITOR
using System.IO;
using System.Reflection;   // bildirim ikonu API'sine sürümden bağımsız erişim
using UnityEditor;
using UnityEditor.Build;   // NamedBuildTarget
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // Uygulama ikonu, splash screen ve bildirim ikonlarını kodla üretir, sonra
    // Player Settings'e otomatik uygular. Mağazaya çıkmak için bunlar zorunlu.
    //
    // Menü: DreamCar → Procedural → Generate App Icons & Splash
    public static class ProceduralAppIcons
    {
        const string Folder = "Assets/Generated/Branding";

        // Marka renkleri — tek yerden değiştir, her şey uyumlu kalır.
        static readonly Color BrandDeep   = new(0.05f, 0.07f, 0.13f);
        static readonly Color BrandMid    = new(0.10f, 0.16f, 0.32f);
        static readonly Color BrandAccent = new(0.98f, 0.32f, 0.18f);
        static readonly Color BrandLight  = new(0.35f, 0.72f, 1.00f);

        [MenuItem("DreamCar/Procedural/Generate App Icons & Splash")]
        public static void GenerateAllInteractive() => GenerateAll(confirm: true);

        // confirm=false → BUILD EVERYTHING zincirinden çağrılırken diyalog açmaz.
        public static void GenerateAll(bool confirm)
        {
            EnsureFolder();

            // iOS App Store 1024×1024 (alfa kanalı OLMAMALI — Apple reddeder)
            SavePng(BuildAppIcon(1024, rounded: false, opaque: true), "icon_1024", isSprite: false);

            // Android adaptive icon: ön plan + arka plan ayrı katman
            SavePng(BuildAdaptiveForeground(432), "icon_adaptive_foreground", isSprite: false);
            SavePng(BuildAdaptiveBackground(432), "icon_adaptive_background", isSprite: false);

            // Genel amaçlı yuvarlatılmış ikon (Editor önizleme, mağaza dışı kullanım)
            SavePng(BuildAppIcon(512, rounded: true, opaque: false), "icon_512", isSprite: false);

            // Splash
            SavePng(BuildSplash(1242, 2208), "splash_portrait", isSprite: false);

            // Android bildirim ikonları
            // Küçük ikon: TEK RENK beyaz siluet + saydam zemin (Android renklendirir)
            SavePng(BuildNotificationSmall(96), "notif_icon_small", isSprite: false);
            SavePng(BuildAppIcon(192, rounded: true, opaque: false), "notif_icon_large", isSprite: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ApplyToPlayerSettings();

            Debug.Log("[Branding] İkonlar üretildi ve Player Settings'e uygulandı: " + Folder);
            if (confirm)
                EditorUtility.DisplayDialog("DreamCar",
                    "İkonlar hazır.\n\n" +
                    "• icon_1024 → App Store (opak, alfa yok)\n" +
                    "• icon_adaptive_* → Android adaptive icon\n" +
                    "• splash_portrait → açılış ekranı\n" +
                    "• notif_icon_small → Android durum çubuğu\n\n" +
                    "Player Settings'e otomatik uygulandı.\n" +
                    "Android bildirim ikonlarını Player Settings →\n" +
                    "Android → Notification Icons altından kontrol et.",
                    "Tamam");
        }

        // ---------------------------------------------------------- İkon
        // Tasarım: koyu gradyan zemin + diyagonal hız çizgileri + araba silueti.
        static Texture2D BuildAppIcon(int size, bool rounded, bool opaque)
        {
            var tex = New(size, size);
            var px = new Color[size * size];

            float corner = size * 0.22f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;

                // Köşegen gradyan
                float g = Mathf.Clamp01((u + (1f - v)) * 0.5f);
                Color c = Color.Lerp(BrandDeep, BrandMid, g);

                // Alt kısımda sıcak vurgu — far ışığı hissi
                float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.22f)) * 2.2f);
                c = Color.Lerp(c, BrandAccent, glow * 0.22f);

                // Diyagonal hız çizgileri
                float stripe = Mathf.Repeat((u * 1.6f + v * 0.9f) * 7f, 1f);
                if (stripe < 0.035f && v > 0.18f && v < 0.86f)
                    c = Color.Lerp(c, BrandLight, 0.13f);

                float alpha = 1f;
                if (rounded)
                {
                    float d = RoundedRectSdf(x + 0.5f, y + 0.5f, size, size, corner);
                    alpha = Mathf.Clamp01(0.5f - d);
                }

                px[y * size + x] = new Color(c.r, c.g, c.b, opaque ? 1f : alpha);
            }

            DrawCarSilhouette(px, size, size, centerY: 0.47f, scale: 0.74f,
                              body: Color.white, accent: BrandAccent, glass: BrandLight);

            // Yol çizgisi
            DrawRoadLine(px, size, size, y01: 0.235f, width01: 0.62f, thickness: Mathf.Max(2, size / 96));

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Android adaptive icon: ön plan katmanı, güvenli alan içinde kalmalı.
        // Sistem bu katmanı kırpar/maskeler, o yüzden araba merkeze ve küçük çizilir.
        static Texture2D BuildAdaptiveForeground(int size)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);

            // Güvenli alan adaptive icon'da merkezdeki ~%66'lık daire.
            DrawCarSilhouette(px, size, size, centerY: 0.5f, scale: 0.52f,
                              body: Color.white, accent: BrandAccent, glass: BrandLight);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D BuildAdaptiveBackground(int size)
        {
            var tex = New(size, size);
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float g = Mathf.Clamp01((u + (1f - v)) * 0.5f);
                Color c = Color.Lerp(BrandDeep, BrandMid, g);

                float stripe = Mathf.Repeat((u * 1.6f + v * 0.9f) * 6f, 1f);
                if (stripe < 0.04f) c = Color.Lerp(c, BrandLight, 0.12f);

                px[y * size + x] = new Color(c.r, c.g, c.b, 1f);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------- Splash
        static Texture2D BuildSplash(int width, int height)
        {
            var tex = New(width, height);
            var px = new Color[width * height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float v = (float)y / height;
                // Dikey gradyan: üstte koyu gece, altta yol
                Color c = Color.Lerp(BrandDeep * 0.7f, BrandMid, Mathf.Pow(v, 0.8f));

                // Ufuk çizgisinde far parıltısı
                float horizon = Mathf.Exp(-Mathf.Pow((v - 0.42f) * 9f, 2f));
                c = Color.Lerp(c, BrandAccent, horizon * 0.18f);

                px[y * width + x] = new Color(c.r, c.g, c.b, 1f);
            }

            // Perspektif yol — aşağı doğru genişleyen iki kenar
            for (int y = 0; y < (int)(height * 0.42f); y++)
            {
                float t = 1f - (float)y / (height * 0.42f);
                float halfWidth = Mathf.Lerp(width * 0.02f, width * 0.48f, Mathf.Pow(t, 1.6f));
                int cx = width / 2;

                for (int x = 0; x < width; x++)
                {
                    float dist = Mathf.Abs(x - cx);
                    if (dist > halfWidth) continue;

                    float edge = Mathf.Clamp01((halfWidth - dist) / (width * 0.02f));
                    var road = new Color(0.09f, 0.09f, 0.11f);
                    px[y * width + x] = Color.Lerp(px[y * width + x], road, edge * 0.92f);
                }

                // Orta kesikli şerit
                if ((y / Mathf.Max(1, (int)(height * 0.022f))) % 2 == 0)
                {
                    int stripeHalf = Mathf.Max(1, (int)(halfWidth * 0.02f));
                    for (int x = cx - stripeHalf; x <= cx + stripeHalf; x++)
                        if (x >= 0 && x < width)
                            px[y * width + x] = new Color(0.85f, 0.82f, 0.72f);
                }
            }

            DrawCarSilhouette(px, width, height, centerY: 0.52f, scale: 0.42f,
                              body: Color.white, accent: BrandAccent, glass: BrandLight);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------- Bildirim ikonu
        // Android durum çubuğu ikonu TEK RENK olmalı: beyaz siluet + saydam zemin.
        // Renkli verirsen Android onu beyaz bloğa çevirir ve çirkin görünür.
        static Texture2D BuildNotificationSmall(int size)
        {
            var tex = New(size, size);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);

            DrawCarSilhouette(px, size, size, centerY: 0.5f, scale: 0.82f,
                              body: Color.white, accent: Color.white, glass: Color.white,
                              monochrome: true);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------- Araba silueti
        // Yandan görünüm: gövde profili + kabin + iki tekerlek.
        static void DrawCarSilhouette(Color[] px, int width, int height,
                                      float centerY, float scale,
                                      Color body, Color accent, Color glass,
                                      bool monochrome = false)
        {
            float cx = width * 0.5f;
            float cy = height * centerY;
            float carLength = width * scale;
            float carHeight = carLength * 0.40f;

            float wheelRadius = carHeight * 0.30f;
            float wheelY = cy - carHeight * 0.22f;
            float frontWheelX = cx + carLength * 0.27f;
            float rearWheelX = cx - carLength * 0.27f;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Araba yerel koordinatlarına çevir (-1..1 uzunluk ekseni)
                float lx = (x + 0.5f - cx) / (carLength * 0.5f);
                float ly = (y + 0.5f - cy) / (carHeight * 0.5f);

                Color? paint = null;

                // Gövde: alt yarısı geniş, üstü kabin eğrisi
                if (Mathf.Abs(lx) <= 1f)
                {
                    float bodyTop = BodyProfile(lx);
                    float bodyBottom = -0.55f;
                    if (ly >= bodyBottom && ly <= bodyTop)
                    {
                        paint = body;

                        // Cam bandı — kabinin içinde
                        float cabinTop = bodyTop - 0.10f;
                        float cabinBottom = 0.12f;
                        bool inCabinX = lx > -0.42f && lx < 0.30f;
                        if (!monochrome && inCabinX && ly > cabinBottom && ly < cabinTop)
                            paint = glass;
                    }
                }

                // Tekerlekler
                float distFront = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(frontWheelX, wheelY));
                float distRear = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(rearWheelX, wheelY));
                float wheelDist = Mathf.Min(distFront, distRear);

                if (wheelDist <= wheelRadius)
                {
                    paint = monochrome ? body
                          : (wheelDist <= wheelRadius * 0.45f ? accent : new Color(0.10f, 0.10f, 0.12f));
                }

                if (paint == null) continue;

                // Kenar yumuşatma için basit alfa: siluet sınırında kısmi karıştırma
                int idx = y * width + x;
                var existing = px[idx];
                var c = paint.Value;
                px[idx] = new Color(c.r, c.g, c.b, Mathf.Max(existing.a, 1f));
            }
        }

        // Gövde üst profili — lx: -1 (arka) … +1 (ön). Kabin ortada yükselir.
        static float BodyProfile(float lx)
        {
            // Bagaj → kabin → kaput eğrisi
            float cabin = Mathf.Exp(-Mathf.Pow((lx + 0.06f) * 2.3f, 2f)) * 0.62f;
            float beltline = 0.16f;
            float nose = Mathf.Clamp01(1f - Mathf.Abs(lx)) * 0.10f;
            return beltline + cabin + nose;
        }

        static void DrawRoadLine(Color[] px, int width, int height, float y01, float width01, int thickness)
        {
            int cy = (int)(height * y01);
            int half = (int)(width * width01 * 0.5f);
            int cx = width / 2;

            for (int t = 0; t < thickness; t++)
            for (int x = cx - half; x <= cx + half; x++)
            {
                if (x < 0 || x >= width) continue;
                int y = cy + t;
                if (y < 0 || y >= height) continue;

                // Uçlarda soluklaşsın
                float fade = 1f - Mathf.Abs(x - cx) / (float)half;
                fade = Mathf.Clamp01(fade * 1.6f);

                int idx = y * width + x;
                var c = Color.Lerp(px[idx], BrandLight, fade * 0.55f);
                px[idx] = new Color(c.r, c.g, c.b, Mathf.Max(px[idx].a, fade));
            }
        }

        static float RoundedRectSdf(float x, float y, int w, int h, float radius)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float dx = Mathf.Abs(x - cx) - (cx - radius);
            float dy = Mathf.Abs(y - cy) - (cy - radius);
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius + radius;
        }

        // ---------------------------------------------------------- Player Settings
        static void ApplyToPlayerSettings()
        {
            var icon1024 = Load("icon_1024");
            if (icon1024 == null) return;

            // iOS: tüm boyutlar tek kaynaktan ölçeklenir.
            ApplyIconsFor(NamedBuildTarget.iOS, icon1024);

            // Android: önce legacy/round setleri tek kaynaktan.
            ApplyIconsFor(NamedBuildTarget.Android, icon1024);

            // Adaptive icon iki KATMANDIR (ön plan + arka plan) ve launcher onu
            // daire/squircle'a kırpar. Kare ikonu tek katman olarak koyarsak
            // köşeler kesilir — bu yüzden üretilmiş katmanları ayrıca bağlıyoruz.
            ApplyAdaptiveIcon();

            // Bildirim ikonları (Mobile Notifications paketi varsa).
            ApplyAndroidNotificationIcons();

            // Splash
            var splash = Load("splash_portrait");
            if (splash != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.backgroundColor = BrandDeep;
                PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/splash_portrait.png");
                if (sprite != null) PlayerSettings.SplashScreen.background = sprite;
            }

            AssetDatabase.SaveAssets();
        }

        // Adaptive icon: her ikon iki katman taşır — 0 = arka plan, 1 = ön plan.
        // SetIcons(...) yalnızca 0. katmanı yazar, o yüzden PlatformIcon üzerinden
        // katman katman set ediyoruz.
        static void ApplyAdaptiveIcon()
        {
            var foreground = Load("icon_adaptive_foreground");
            var background = Load("icon_adaptive_background");
            if (foreground == null || background == null) return;

            // Kind'ı adından buluyoruz: AndroidPlatformIconKind farklı Unity
            // sürümlerinde farklı namespace'te durabiliyor, bu yol sürümden bağımsız.
            PlatformIconKind adaptive = null;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
                if (kind.ToString() == "Adaptive") { adaptive = kind; break; }

            if (adaptive == null) return;

            try
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, adaptive);
                if (icons == null || icons.Length == 0) return;

                foreach (var icon in icons)
                {
                    icon.SetTexture(background, 0);
                    icon.SetTexture(foreground, 1);
                }

                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, adaptive, icons);
                Debug.Log("[Branding] Android adaptive icon katmanları uygulandı.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Branding] Adaptive icon uygulanamadı: {e.Message}");
            }
        }

        // Bildirim ikonları Player Settings'te değil, Mobile Notifications paketinin
        // kendi ayar varlığında tutulur.
        //
        // Bu editör API'si paket sürümleri arasında namespace değiştiriyor
        // (Unity.Notifications / UnityEditor.Notifications), o yüzden doğrudan
        // tipe bağlanmıyoruz — yanlış tahmin, paket kurulduğu anda derleme hatası
        // olurdu. Reflection ile bağlanıp başarısız olursa elle yapılacak adımı
        // yazdırıyoruz. Paket kurulu değilse sessizce atlanır.
        static void ApplyAndroidNotificationIcons()
        {
            var small = Load("notif_icon_small");
            if (small == null) return;

            var managerType = FindType("NotificationSettingsManager");
            if (managerType == null) return;   // paket kurulu değil — normal durum

            try
            {
                var settings = managerType
                    .GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, null);
                if (settings == null) throw new System.Exception("Initialize() null döndü.");

                var list = GetMember(settings, "DrawableResources") as System.Collections.IList;
                if (list == null) throw new System.Exception("DrawableResources okunamadı.");

                var entryType = FindType("DrawableResourceData");
                var iconTypeEnum = FindType("NotificationIconType");
                if (entryType == null || iconTypeEnum == null)
                    throw new System.Exception("Bildirim ikonu tipleri bulunamadı.");

                // icon_0 = küçük (durum çubuğu; TEK RENK beyaz siluet olmalı,
                // yoksa Android gri kare gösterir)
                // icon_1 = büyük (bildirim gövdesi, renkli)
                // Adlar LocalNotificationScheduler'daki SmallIcon/LargeIcon ile eşleşmeli.
                SetNotificationIcon(list, entryType, iconTypeEnum, 0, "Small", small);

                var large = Load("notif_icon_large");
                if (large != null)
                    SetNotificationIcon(list, entryType, iconTypeEnum, 1, "Large", large);

                if (settings is Object asset) EditorUtility.SetDirty(asset);
                Debug.Log("[Branding] Android bildirim ikonları uygulandı (icon_0, icon_1).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    "[Branding] Bildirim ikonları otomatik uygulanamadı: " + e.Message +
                    "\nElle: Project Settings → Mobile Notifications → Android → " +
                    "Notification Icons → iki giriş ekle:\n" +
                    "  icon_0 (Small) = Assets/Generated/Branding/notif_icon_small.png\n" +
                    "  icon_1 (Large) = Assets/Generated/Branding/notif_icon_large.png");
            }
        }

        static void SetNotificationIcon(System.Collections.IList list, System.Type entryType,
                                        System.Type iconTypeEnum, int index,
                                        string enumValue, Texture2D texture)
        {
            while (list.Count <= index)
                list.Add(System.Activator.CreateInstance(entryType));

            var entry = list[index];
            SetMember(entry, "Id", $"icon_{index}");
            SetMember(entry, "Type", System.Enum.Parse(iconTypeEnum, enumValue));
            SetMember(entry, "Asset", texture);
        }

        // --- Reflection yardımcıları ---

        static System.Type FindType(string simpleName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                // Yalnızca bildirim paketinin assembly'lerine bak — 200 assembly
                // taramak yerine adla filtrele.
                if (asm.GetName().Name.IndexOf("Notification", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Bağımlılığı eksik bir assembly GetTypes()'ta patlayabilir — atla.
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                    if (type != null && type.Name == simpleName) return type;
            }
            return null;
        }

        const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        static object GetMember(object target, string name)
        {
            var type = target.GetType();
            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null) return prop.GetValue(target);
            return type.GetField(name, MemberFlags)?.GetValue(target);
        }

        static void SetMember(object target, string name, object value)
        {
            var type = target.GetType();
            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.CanWrite) { prop.SetValue(target, value); return; }

            var field = type.GetField(name, MemberFlags);
            if (field == null) throw new System.Exception($"'{name}' üyesi bulunamadı.");
            field.SetValue(target, value);
        }

        static void ApplyIconsFor(NamedBuildTarget target, Texture2D source)
        {
            try
            {
                var kinds = PlayerSettings.GetSupportedIconKinds(target);
                foreach (var kind in kinds)
                {
                    // Adaptive iki katmanlı — ApplyAdaptiveIcon ayrıca ele alıyor.
                    if (kind.ToString() == "Adaptive") continue;

                    // Modern ikon API'si. Unity 6000.6'da GetSupportedIconKinds
                    // PlatformIconKind döndürüyor ama eski SetIcons/GetIconSizes
                    // hâlâ IconKind bekliyor (CS1503). PlatformIcon üzerinden
                    // gidiyoruz — ApplyAdaptiveIcon ile aynı yol.
                    var icons = PlayerSettings.GetPlatformIcons(target, kind);
                    if (icons == null || icons.Length == 0) continue;

                    foreach (var icon in icons)
                        for (int layer = 0; layer < icon.maxLayerCount; layer++)
                            icon.SetTexture(source, layer);

                    PlayerSettings.SetPlatformIcons(target, kind, icons);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Branding] {target.TargetName} ikonları uygulanamadı: {e.Message}");
            }
        }

        // ---------------------------------------------------------- Yardımcılar
        static Texture2D Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");

        static Texture2D New(int w, int h) =>
            new(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        static void SavePng(Texture2D tex, string name, bool isSprite)
        {
            EnsureFolder();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = isSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            // İkon kaynağı olarak kullanılacaksa okunabilir ve sıkıştırmasız olmalı.
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static void EnsureFolder()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
        }
    }
}
#endif
