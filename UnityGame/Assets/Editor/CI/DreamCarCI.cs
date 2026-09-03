#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using DreamCar.EditorTools.Procedural;

namespace DreamCar.EditorTools.CI
{
    // DEPO TEK BAŞINA OYNANABİLİR BİR OYUN ÜRETEMİYORDU.
    //
    // Android CI iş akışı eksiksizdi — imzalama, artifact yükleme, telefona
    // kurma talimatı, hepsi yazılmıştı. Ama çalıştırılsa BOŞ BİR UYGULAMA
    // üretirdi, çünkü depoda şunların hiçbiri yok:
    //
    //   Assets/Scenes/      → 0 dosya  (MainMenu, Game, 8 harita — hiçbiri)
    //   Assets/Generated/   → 0 dosya  (URP varlığı, mesh, materyal, katalog)
    //   ProjectSettings/    → 1 dosya  (yalnızca ProjectVersion.txt)
    //
    // Yani Build Settings sahne listesi, Graphics'teki URP ataması, Player
    // Settings (IL2CPP, ARM64, yatay yön, Active Input Handling) ve "MainCamera"
    // etiketi de depoda değil. Hepsi BUILD EVERYTHING ile kullanıcının
    // makinesinde üretilip orada kalıyor.
    //
    // Bunlar .gitignore'da değil — hiç commit edilmemişler.
    //
    // ÇÖZÜM: CI de üretsin. Projenin bütün felsefesi zaten "her şey
    // prosedürel"; ikili varlıkları git'e koymak yerine CI'ın klonlayıp üretip
    // build alması hem tutarlı, hem depo şişmiyor, hem de üretim zinciri her
    // build'de gerçekten sınanmış oluyor.
    //
    // game-ci/unity-builder bu metodu buildMethod olarak çağırıyor ve kendi
    // argümanlarını komut satırından geçiriyor.
    public static class DreamCarCI
    {
        public static void GenerateAndBuild()
        {
            // 1) Üret. Sıra DreamCarBuildAll'da tek kaynak olarak duruyor —
            //    burada kopyalanmıyor, çünkü iki yerde ayrı yazılsaydı er geç
            //    ayrışırdı ve o sıra kritik (haritalar sahnelerden önce,
            //    katalog sahnelerden önce, Build Settings en sonda).
            Log("Prosedürel üretim başlıyor…");
            if (!DreamCarBuildAll.GenerateAll())
            {
                Fail("Üretim tamamlanamadı — büyük ihtimalle TextMeshPro temel " +
                     "kaynakları çözümlenemedi. Yukarıdaki log'a bak.");
                return;
            }

            // 2) Denetle. Boş ya da bozuk bir APK'yı sessizce yüklemektense
            //    CI'da kırmızı görmek doğru — bu oyunun hata ailesi zaten
            //    "sessizce çalışmıyor" olduğu için burada susmak en kötüsü.
            Log("Sahne denetimi…");
            int problems = DreamCarValidator.Run(showDialog: false);
            if (problems > 0)
            {
                Fail($"Denetim {problems} sorun buldu — build alınmadı. Ayrıntılar yukarıda.");
                return;
            }

            // 3) Build al.
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("Build Settings'te etkin sahne yok — üretim sahneleri eklemedi.");
                return;
            }

            var target = ResolveTarget();
            string path = ResolveOutputPath(target);

            Log($"Build: {target} → {path}  ({scenes.Length} sahne)");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Log($"Sonuç: {summary.result} · {summary.totalErrors} hata · " +
                $"{summary.totalSize / (1024 * 1024)} MB · {summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build başarısız: {summary.result}");
                return;
            }

            Log("Build tamam.");
            EditorApplication.Exit(0);
        }

        // --- unity-builder argümanları ---
        //
        // Aksiyon şunları geçiriyor: -buildTarget, -customBuildPath,
        // -customBuildName, -projectPath, -logFile. Değerleri kendimiz
        // okuyoruz ki aksiyonun iç sınıflarına bağlanmayalım — o sınıflar
        // sürüm değiştirdiğinde derleme hatası olurdu.

        static BuildTarget ResolveTarget()
        {
            string raw = Arg("-buildTarget");
            if (!string.IsNullOrEmpty(raw) &&
                System.Enum.TryParse(raw, ignoreCase: true, out BuildTarget parsed))
                return parsed;

            // Argüman yoksa Editor'ün o anki hedefi — elle çalıştırmayı da
            // mümkün kılıyor.
            return EditorUserBuildSettings.activeBuildTarget;
        }

        static string ResolveOutputPath(BuildTarget target)
        {
            string custom = Arg("-customBuildPath");
            if (!string.IsNullOrEmpty(custom))
            {
                // unity-builder bazen klasör, bazen tam dosya yolu veriyor.
                bool hasExtension = System.IO.Path.HasExtension(custom);
                if (hasExtension) return custom;
                return System.IO.Path.Combine(custom, DefaultFileName(target));
            }

            return System.IO.Path.Combine("build", target.ToString(), DefaultFileName(target));
        }

        static string DefaultFileName(BuildTarget target)
        {
            string name = Arg("-customBuildName");
            if (string.IsNullOrEmpty(name)) name = "DreamCar";

            switch (target)
            {
                case BuildTarget.Android:
                    return name + (EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk");
                case BuildTarget.iOS:
                    return name;                     // iOS bir Xcode projesi klasörü üretir
                case BuildTarget.StandaloneWindows64:
                    return name + ".exe";
                default:
                    return name;
            }
        }

        static string Arg(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        static void Log(string message) => Debug.Log("[CI] " + message);

        // Çağıran her yerde hemen ardından return var: EditorApplication.Exit
        // süreci sonlandırıyor ama derleyici bunu bilmiyor ve bazı bağlamlarda
        // çıkış anında gerçekleşmiyor — akışın devam etmesi, hata bildirilmişken
        // yine de build almaya çalışmak demek olurdu.
        static void Fail(string message)
        {
            Debug.LogError("[CI] " + message);
            // Exit(1) olmadan unity-builder build'i başarılı sayardı ve boş bir
            // artifact yüklerdi — sessiz başarısızlık tam olarak kaçındığımız şey.
            EditorApplication.Exit(1);
        }
    }
}
#endif
