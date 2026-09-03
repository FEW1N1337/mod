#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// Harita üreticisi alt namespace'te — üst namespace'ten adı doğrudan görünmez.
using DreamCar.EditorTools.Procedural.Maps;

namespace DreamCar.EditorTools.Procedural
{
    // Sıfırdan oynanabilir bir yapıya götüren tek komut. Sırayla:
    //   texture → araçlar → katalog → UI sprite → sahneler → şehir → build settings
    //
    // Menü: DreamCar → BUILD EVERYTHING
    public static class DreamCarBuildAll
    {
        [MenuItem("DreamCar/BUILD EVERYTHING (sıfırdan oynanabilir hale getir)", priority = -100)]
        public static void BuildEverything()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "DreamCar — Her Şeyi Üret",
                "Şunlar üretilecek:\n\n" +
                "• Render pipeline (URP) + Player Settings\n" +
                "• Texture ve materyaller\n" +
                "• 5 araç prefab'ı (mesh dahil)\n" +
                "• Araç kataloğu\n" +
                "• Başarım kataloğu (12 başarım)\n" +
                "• UI sprite'ları\n" +
                "• Uygulama ikonları ve açılış ekranı\n" +
                "• Post-processing profilleri (3 kalite kademesi)\n" +
                "• MainMenu ve Game sahneleri\n" +
                "• Prosedürel şehir\n" +
                "• 8 harita sahnesi + harita kataloğu\n" +
                "• Build Settings\n\n" +
                "Mevcut MainMenu.unity, Game.unity ve harita\n" +
                "sahneleri ÜZERİNE YAZILIR.\n" +
                "Haritalar yüzünden birkaç dakika sürebilir.\n" +
                "Devam edilsin mi?",
                "Evet, üret", "İptal");

            if (!proceed) return;

            if (!GenerateAll()) return;

            // Denetim üretimden ve Build Settings adımından SONRA: harita
            // kontrolü o listeyi okuyor, önce koşsaydı üretilen her haritayı
            // "Build Settings'te yok" diye bildirirdi.
            //
            // Bu projenin baskın hata ailesi sessiz: sistem yazılmış ama hiçbir
            // yere bağlanmamış ve ne derleme hatası ne çalışma anı istisnası
            // veriyor — sadece olması gereken olmuyor. Üretimin hemen ardından
            // denetlemek, o aileyi ortaya çıktığı anda görünür kılan tek yol.
            //
            // CI aynı çağrıyı kendi yapıyor ve dönen sayıyla build'i kesiyor
            // (DreamCarCI), o yüzden burada GenerateAll'ın içinde değil.
            int problems = DreamCarValidator.Run(showDialog: false);

            EditorUtility.DisplayDialog("DreamCar — Hazır",
                (problems == 0
                    ? "Her şey üretildi. Denetim temiz.\n\n"
                    : $"Her şey üretildi ama DENETİM {problems} SORUN buldu — Console'a bak.\n\n") +
                "SIRADAKİ ADIMLAR:\n\n" +
                "1) Photon PUN 2'yi Asset Store'dan import et\n" +
                "2) PhotonServerSettings'e App Id gir\n" +
                "3) Assets/Scenes/MainMenu.unity'i aç ve Play'e bas\n" +
                "   (şu an açık olan sahne son üretilen haritadır)\n\n" +
                "Ses sürgüleri kutudan çalışır — AudioMixer kurmak\n" +
                "zorunda değilsin. İstersen kurabilirsin: Master/Music/\n" +
                "SFX parametrelerini expose edip GameSettings.mixer\n" +
                "alanına ata, sistem otomatik ona geçer (README §11d).",
                "Tamam");
        }

        // ÜRETİM ZİNCİRİ — tek kaynak.
        //
        // Hem menüden (BuildEverything) hem de batch-mode CI'dan (DreamCarCI)
        // çağrılıyor. İki yerde ayrı ayrı yazılsaydı sıra er geç ayrışırdı ve
        // bu sıra kritik: haritalar sahnelerden ÖNCE (MapCatalog'a referans
        // veriliyor), katalog sahnelerden ÖNCE, Build Settings EN SONDA.
        //
        // false dönerse üretim yapılmadı ve sebebi kullanıcıya bildirildi.
        public static bool GenerateAll()
        {
            // HER ŞEYDEN ÖNCE: TMP'nin varsayılan yazı tipi projede yoksa
            // kurulacak her metin fontsuz doğar ve hiçbir şey çizmez. Yazısız
            // bir oyun üretip "bitti" demektense burada durmak doğru.
            // Metot kullanıcıya ne yapacağını kendisi söylüyor.
            if (!ProceduralTextMeshPro.EnsureResources()) return false;

            try
            {
                // EN BAŞTA: render pipeline atanmadan üretilen hiçbir şey doğru
                // görünmez. Projede URP varlığı yoksa Unity Built-in pipeline'a
                // düşer ve URP shader'ı kullanan her yüzey macenta render edilir.
                // Renk uzayı da burada Linear'a alınıyor — PBR malzemeler ancak
                // orada doğru görünür.
                EditorUtility.DisplayProgressBar("DreamCar", "Render pipeline kuruluyor…", 0.03f);
                ProceduralRenderPipeline.Setup(confirm: false);

                EditorUtility.DisplayProgressBar("DreamCar", "Texture'lar üretiliyor…", 0.08f);
                ProceduralTextures.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "UI sprite'ları üretiliyor…", 0.12f);
                ProceduralUISprites.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "İkonlar ve açılış ekranı…", 0.18f);
                ProceduralAppIcons.GenerateAll(confirm: false);

                // Post-processing profilleri sahnelerden ÖNCE üretilir; GraphicsTuner
                // sahne kurulurken bunlara referans bağlıyor.
                EditorUtility.DisplayProgressBar("DreamCar", "Post-processing profilleri…", 0.24f);
                ProceduralPostProcessing.GenerateAll(confirm: false);

                EditorUtility.DisplayProgressBar("DreamCar", "Araçlar üretiliyor…", 0.32f);
                ProceduralCarGenerator.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "Araç kataloğu kuruluyor…", 0.42f);
                ProceduralCarGenerator.BuildCatalog();

                // Katalogdan HEMEN SONRA: küçük resimler hem prefabı hem
                // CarDefinition varlığını gerektiriyor, ikisi de bu noktada var.
                // Önce koşsaydı ne render edecek bir prefab ne yazacak bir alan
                // bulurdu.
                //
                // Bunlar olmadan garajın ortası BOŞ bir dikdörtgen: alan var,
                // GarageCarousel okuyor, yazan yoktu.
                EditorUtility.DisplayProgressBar("DreamCar", "Araç küçük resimleri…", 0.45f);
                ProceduralCarThumbnails.GenerateAll();

                // Sahnelerden ÖNCE: hem AchievementsScreen hem PlayFabAchievements bu
                // kataloğa referans veriyor. Sonra üretilseydi bağlanacak varlık henüz
                // olmaz, başarım ekranı boş kalır ve hiçbir başarım değerlendirilmezdi.
                EditorUtility.DisplayProgressBar("DreamCar", "Başarım kataloğu…", 0.46f);
                ProceduralAchievements.Generate();

                // En uzun adım: 8 harita sahnesi. Sahnelerden ÖNCE koşmalı — MapCatalog'u
                // üretiyor ve MainMenu ile Game sahnesindeki MapSelector ona referans
                // veriyor. Sonra koşsaydı bağlanacak varlık henüz var olmazdı ve harita
                // varyantları (gündüz/gece/yağmur) hiç uygulanmazdı.
                EditorUtility.DisplayProgressBar("DreamCar", "Haritalar üretiliyor…", 0.50f);
                ProceduralMapGenerator.GenerateAll(confirm: false);

                EditorUtility.DisplayProgressBar("DreamCar", "Ana menü sahnesi…", 0.70f);
                DreamCarSetup.CreateMainMenu();

                EditorUtility.DisplayProgressBar("DreamCar", "Oyun sahnesi…", 0.76f);
                DreamCarSetup.CreateGameScene();

                EditorUtility.DisplayProgressBar("DreamCar", "Şehir üretiliyor…", 0.84f);
                ProceduralCityGenerator.GenerateCity();

                // Şehir aktif sahneye eklendi — Game sahnesi olarak kaydet.
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                // Bu metot build listesini SIFIRLAYIP MainMenu + Game yazıyor, yani
                // harita adımının eklediklerini siler. Haritalar hemen ardından geri
                // eklenir; o metot mevcut girdileri koruyarak yazıyor.
                EditorUtility.DisplayProgressBar("DreamCar", "Build Settings…", 0.92f);
                DreamCarSetup.AddScenesToBuildSettings();
                ProceduralMapGenerator.AddMapsToBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return true;
        }

        [MenuItem("DreamCar/Yardım/Neler üretildi?", priority = 100)]
        public static void ShowGeneratedSummary()
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("ÜRETİLEN VARLIKLAR\n");

            AppendCount(lines, "Mesh", "Assets/Generated/Meshes", "*.asset");
            AppendCount(lines, "Materyal", "Assets/Generated/Materials", "*.mat");
            AppendCount(lines, "Texture", "Assets/Generated/Textures", "*.png");
            AppendCount(lines, "UI sprite", "Assets/Generated/UI", "*.png");
            AppendCount(lines, "Araç prefab", "Assets/Resources", "Car_*.prefab");
            AppendCount(lines, "Katalog", "Assets/Generated/Catalog", "*.asset");
            AppendCount(lines, "Harita mesh", "Assets/Generated/Maps", "*.asset");
            AppendCount(lines, "Harita sahnesi", "Assets/Scenes/Maps", "*.unity");
            AppendCount(lines, "PostFX profili", "Assets/Generated/PostProcessing", "*.asset");
            AppendCount(lines, "İkon/splash", "Assets/Generated/Branding", "*.png");

            lines.AppendLine("\nHepsi kodla üretildi — telifli dış varlık yok.");
            EditorUtility.DisplayDialog("DreamCar", lines.ToString(), "Tamam");
        }

        static void AppendCount(System.Text.StringBuilder sb, string label, string folder, string pattern)
        {
            int count = System.IO.Directory.Exists(folder)
                ? System.IO.Directory.GetFiles(folder, pattern).Length
                : 0;
            sb.AppendLine($"{label}: {count}");
        }
    }
}
#endif
