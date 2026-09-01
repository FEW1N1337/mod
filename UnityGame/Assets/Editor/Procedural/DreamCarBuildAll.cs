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
                "• Texture ve materyaller\n" +
                "• 5 araç prefab'ı (mesh dahil)\n" +
                "• Araç kataloğu\n" +
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

            try
            {
                EditorUtility.DisplayProgressBar("DreamCar", "Texture'lar üretiliyor…", 0.05f);
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

            EditorUtility.DisplayDialog("DreamCar — Hazır",
                "Her şey üretildi.\n\n" +
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
