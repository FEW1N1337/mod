#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
                "• MainMenu ve Game sahneleri\n" +
                "• Prosedürel şehir\n" +
                "• Build Settings\n\n" +
                "Mevcut MainMenu.unity ve Game.unity ÜZERİNE YAZILIR.\n" +
                "Devam edilsin mi?",
                "Evet, üret", "İptal");

            if (!proceed) return;

            try
            {
                EditorUtility.DisplayProgressBar("DreamCar", "Texture'lar üretiliyor…", 0.10f);
                ProceduralTextures.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "UI sprite'ları üretiliyor…", 0.20f);
                ProceduralUISprites.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "Araçlar üretiliyor…", 0.35f);
                ProceduralCarGenerator.GenerateAll();

                EditorUtility.DisplayProgressBar("DreamCar", "Araç kataloğu kuruluyor…", 0.50f);
                ProceduralCarGenerator.BuildCatalog();

                EditorUtility.DisplayProgressBar("DreamCar", "Ana menü sahnesi…", 0.60f);
                DreamCarSetup.CreateMainMenu();

                EditorUtility.DisplayProgressBar("DreamCar", "Oyun sahnesi…", 0.72f);
                DreamCarSetup.CreateGameScene();

                EditorUtility.DisplayProgressBar("DreamCar", "Şehir üretiliyor…", 0.85f);
                ProceduralCityGenerator.GenerateCity();

                // Şehir aktif sahneye eklendi — Game sahnesi olarak kaydet.
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                EditorUtility.DisplayProgressBar("DreamCar", "Build Settings…", 0.95f);
                DreamCarSetup.AddScenesToBuildSettings();

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
                "3) MainMenu sahnesini aç ve Play'e bas\n\n" +
                "Ses ayarlarının çalışması için bir AudioMixer\n" +
                "oluşturup Master/Music/SFX parametrelerini\n" +
                "expose etmen gerekiyor (README §11d).",
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
