#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;          // NamedBuildTarget
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DreamCar.EditorTools
{
    // NEDEN BU DOSYA VAR
    //
    // Bu projenin baskın hata ailesi tek bir şey: sistem YAZILMIŞ, görünüşte
    // tam, ama hiçbir yere BAĞLANMAMIŞ ve sessizce hiçbir şey yapmıyor.
    // Sayılamayacak kadar örnek çıktı — ana menüde AudioListener yokluğu
    // (menü tamamen sessiz), harita sahnelerinde HUD'un hiç kurulmaması
    // (araç doğuyor ama pedal yok), müzik dizilerinin boş olması (sistem tam,
    // çalacak şey yok), Play(Playlist)'in hiç çağrılmaması, davet linkinin
    // hiç yeniden denenmemesi, sahne PhotonView'ının ViewID'siz kalması...
    //
    // Hepsinin ortak özelliği: HİÇBİRİ HATA VERMİYOR. Ne derleme hatası, ne
    // çalışma anı istisnası. Sadece olması gereken şey olmuyor. Bu yüzden de
    // ancak elle, tek tek okuyarak bulunabildiler.
    //
    // Projede bunu yakalayan hiçbir otomatik denetim yoktu. Bu denetçi o
    // boşluğu dolduruyor: BUILD EVERYTHING'in sonunda kendiliğinden koşuyor ve
    // aynı ailenin yeni üyelerini ÜRETİLDİĞİ ANDA yakalıyor.
    //
    // Menü: DreamCar → Doğrulama → Sahneleri denetle
    public static class DreamCarValidator
    {
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string GamePath = "Assets/Scenes/Game.unity";

        [MenuItem("DreamCar/Doğrulama/Sahneleri denetle")]
        public static void ValidateInteractive() => Run(showDialog: true);

        // Bulunan HATA sayısını döner. CI bu sayıyla build'i kesiyor:
        // boş bir APK'yı sessizce yüklemektense kırmızı görmek doğru.
        public static int Run(bool showDialog)
        {
            var errors = new List<string>();
            var notes = new List<string>();

            // Denetim sahneleri açıp kapatıyor; kullanıcının kaydedilmemiş
            // işini sessizce yutmak kabul edilemez.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Doğrulama] Kullanıcı iptal etti.");
                return 0;
            }

            string reopen = SceneManager.GetActiveScene().path;

            foreach (var path in ScenesToCheck())
                ValidateScene(path, errors, notes);

            ValidateProjectWide(errors);
            ValidatePlatform(errors, notes);
            ReportBackend(notes);

            if (!string.IsNullOrEmpty(reopen))
                EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);

            Report(errors, notes, showDialog);
            return errors.Count;
        }

        static IEnumerable<string> ScenesToCheck()
        {
            var list = new List<string>();
            if (System.IO.File.Exists(MainMenuPath)) list.Add(MainMenuPath);
            if (System.IO.File.Exists(GamePath)) list.Add(GamePath);

            // Harita sahneleri: oyunun fiilen yüklediği sahneler bunlar.
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path.StartsWith("Assets/Scenes/Maps/") && System.IO.File.Exists(s.path))
                    list.Add(s.path);

            return list;
        }

        // ------------------------------------------------------ sahne başına

        static void ValidateScene(string path, List<string> errors, List<string> notes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var roots = scene.GetRootGameObjects();

            T[] All<T>() where T : Component =>
                roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

            // 1) AudioListener — 0 ise sahne TAMAMEN sessiz, 2+ ise Unity uyarı
            //    basıyor ve konumsal ses bozuluyor. Ana menüde tam olarak bu
            //    eksikti ve yalnızca bir uyarı olduğu için gözden kaçmıştı.
            int listeners = All<AudioListener>().Length;
            if (listeners == 0)
                errors.Add($"{name}: AudioListener YOK — bu sahnede hiçbir ses duyulmaz.");
            else if (listeners > 1)
                errors.Add($"{name}: {listeners} adet AudioListener var, 1 olmalı.");

            // 2) EventSystem — yoksa HİÇBİR buton çalışmaz.
            if (All<EventSystem>().Length == 0)
                errors.Add($"{name}: EventSystem YOK — hiçbir arayüz butonu çalışmaz.");

            // 3) Camera.main — kamera takibi, kamera modları ve minimap buna bağlı.
            if (!All<Camera>().Any(c => c.CompareTag("MainCamera")))
                errors.Add($"{name}: 'MainCamera' etiketli kamera YOK — Camera.main null olur.");

            // 4) Canvas + GraphicRaycaster — raycaster olmadan arayüz dokunma almaz.
            foreach (var canvas in All<Canvas>())
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (!canvas.GetComponent<GraphicRaycaster>())
                    errors.Add($"{name}: '{canvas.name}' Canvas'ında GraphicRaycaster yok — dokunma almaz.");
            }

            // 5) Sahne PhotonView'ında ViewID 0 — RPC'ler sessizce düşer.
            //    Sohbet tam olarak buna bağlı.
            foreach (var view in All<Photon.Pun.PhotonView>())
            {
                var so = new SerializedObject(view);
                var prop = so.FindProperty("sceneViewId") ?? so.FindProperty("viewIdField");
                if (prop != null && prop.intValue == 0)
                    errors.Add($"{name}: '{view.name}' üzerindeki PhotonView'ın ViewID'si 0 — " +
                               "RPC'leri sessizce düşer (sohbet çalışmaz).");
            }

            // 6) BİLGİ AMAÇLI: DreamCar bileşenlerindeki null referans alanları.
            //    Hata DEĞİL — çoğu alan bilerek opsiyonel. Ama bu oturumdaki
            //    hataların bıraktığı iz tam olarak bu, o yüzden gözle
            //    taranabilir tek bir liste hâlinde veriliyor.
            CollectNullRefs(name, roots, notes);
        }

        static void CollectNullRefs(string sceneName, GameObject[] roots, List<string> notes)
        {
            foreach (var mb in roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (!mb) continue;
                var type = mb.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("DreamCar")) continue;

                var missing = new List<string>();
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;   // yalnızca üst seviye alanlar
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.name == "m_Script") continue;
                    if (it.objectReferenceValue == null) missing.Add(it.name);
                }

                if (missing.Count > 0)
                    notes.Add($"{sceneName}: {type.Name} → boş: {string.Join(", ", missing)}");
            }
        }

        // ------------------------------------------------------- proje geneli

        static void ValidateProjectWide(List<string> errors)
        {
            // Araç kataloğu — boşsa mağaza boş, araç doğmaz.
            var catalog = FindFirst<Economy.CarCatalog>();
            if (catalog == null)
                errors.Add("CarCatalog varlığı bulunamadı — BUILD EVERYTHING çalıştırıldı mı?");
            else if (catalog.cars == null || catalog.cars.Count == 0)
                errors.Add("CarCatalog BOŞ — mağazada araç görünmez.");
            else
                foreach (var def in catalog.cars)
                {
                    if (def == null) continue;
                    if (string.IsNullOrEmpty(def.resourcePrefabName))
                    {
                        errors.Add($"CarDefinition '{def.id}': resourcePrefabName boş.");
                        continue;
                    }
                    // PhotonNetwork.Instantiate prefabı Resources altında ADIYLA arıyor.
                    if (Resources.Load<GameObject>(def.resourcePrefabName) == null)
                        errors.Add($"CarDefinition '{def.id}': Resources/{def.resourcePrefabName} " +
                                   "bulunamadı — o araç seçilince doğmaz.");
                }

            // Harita kataloğu — sahnesi Build Settings'te olmayan harita
            // seçilirse LoadLevel sessizce başarısız olur ve oyuncular boş
            // odada asılı kalır.
            var maps = FindFirst<Maps.MapCatalog>();
            if (maps != null && maps.maps != null)
            {
                var inBuild = new HashSet<string>(EditorBuildSettings.scenes
                    .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)));

                foreach (var def in maps.maps)
                {
                    if (def == null || string.IsNullOrEmpty(def.sceneName)) continue;
                    if (!inBuild.Contains(def.sceneName))
                        errors.Add($"Harita '{def.sceneName}' Build Settings'te YOK — " +
                                   "seçilirse oda yüklenmez.");
                }
            }

            // Render pipeline — atanmamışsa her yüzey macenta render edilir.
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null)
                errors.Add("URP varlığı atanmamış — bütün yüzeyler macenta görünür " +
                           "(DreamCar → Procedural → Setup Render Pipeline).");
        }

        // Mağazaya çıkarken patlayan, ama Editor'de hiçbir belirti vermeyen ayarlar.
        static void ValidatePlatform(List<string> errors, List<string> notes)
        {
            // Android 64-bit: Play Store şartı. ARM32-only bir yükleme reddedilir.
            var arch = PlayerSettings.Android.targetArchitectures;
            if ((arch & AndroidArchitecture.ARM64) == 0)
                errors.Add("Android ARM64 kapalı — Play Store 64-bit zorunlu tutuyor.");

            // IL2CPP olmadan ARM64 seçilemiyor.
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                errors.Add("Android scripting backend IL2CPP değil — ARM64 üretilemez.");

            // Renk uzayı: Gamma'da bütün PBR malzemeler cansız görünür.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                errors.Add("Renk uzayı Linear değil — metalik araç boyası ve aydınlatma yanlış görünür.");

            // ATT çağrısı kodda var (KVKKConsent → _RequestTracking). iOS, bu
            // açıklama yokken ATT çağıran uygulamayı ANINDA SONLANDIRIR.
            // Post-build betiği Info.plist'e yazıyor, ama Player Settings alanı
            // da doluysa Unity kendi de yazar — ikisinden biri yeterli, hiçbiri
            // yoksa çökme kesin.
            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.appleDeveloperTeamID))
                notes.Add("iOS: Apple Developer Team ID boş — imzalı build alırken gerekecek.");

            // Hedef API sabit bir sayıya çakılıysa bir sonraki Play eşiğinde
            // yayın engellenir; Auto kurulu SDK'yı takip ediyor.
            if (PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevelAuto)
                notes.Add($"Android hedef API sabit ({PlayerSettings.Android.targetSdkVersion}). " +
                          "Play eşiği her ağustos yükseliyor; 'Auto' bunu kendiliğinden takip eder.");
        }

        // Backend HATA değil, DURUM: oyun PlayFab'siz tam oynanır (para,
        // istatistik ve araçlar PlayerPrefs'te). Ama katmanın tamamı
        // PLAYFAB_INSTALLED sembolüyle derlemeden çıkarılmış durumdaysa bulut
        // kayıt, liderlik tablosu, başarım senkronu ve arkadaş listesi sessizce
        // yok demektir — bunu hiç söylemeyen bir denetim eksik kalırdı.
        static void ReportBackend(List<string> notes)
        {
            bool sdk = DreamCarPlayFabSetup.IsSdkPresent();
            bool define = DreamCarPlayFabSetup.IsDefineSet();

            if (sdk && define)
            {
                notes.Add("PlayFab: SDK var, PLAYFAB_INSTALLED tanımlı. " +
                          "Title Id'nin dolu olduğundan emin ol (PlayFabAuth.titleId).");
            }
            else if (sdk)
            {
                notes.Add("PlayFab: SDK var ama PLAYFAB_INSTALLED TANIMLI DEĞİL — " +
                          "backend katmanı derlemeye girmiyor. " +
                          "DreamCar → Backend → PlayFab kurulumunu doğrula.");
            }
            else
            {
                notes.Add("PlayFab: SDK kurulu değil — bulut kayıt, liderlik tablosu, " +
                          "başarım senkronu ve arkadaş listesi çalışmaz. " +
                          "Oyun bunsuz da tam oynanır (ilerleme yalnızca cihazda kalır).");
            }
        }

        static T FindFirst<T>() where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }

        // ------------------------------------------------------------- rapor

        static void Report(List<string> errors, List<string> notes, bool showDialog)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DreamCar sahne denetimi ===\n");

            if (errors.Count == 0) sb.AppendLine("Hata bulunamadı.");
            else
            {
                sb.AppendLine($"HATA ({errors.Count}):");
                foreach (var e in errors) sb.AppendLine("  • " + e);
            }

            if (notes.Count > 0)
            {
                sb.AppendLine($"\nBilgi — boş referans alanları ({notes.Count}).");
                sb.AppendLine("Çoğu alan bilerek opsiyonel; bu liste gözle taranmak için.");
                foreach (var n in notes) sb.AppendLine("  · " + n);
            }

            if (errors.Count > 0) Debug.LogError(sb.ToString());
            else Debug.Log(sb.ToString());

            if (!showDialog) return;
            EditorUtility.DisplayDialog("DreamCar — Doğrulama",
                errors.Count == 0
                    ? $"Hata bulunamadı.\n\n{notes.Count} bilgi notu Console'da " +
                      "(boş referans alanları — çoğu normal)."
                    : $"{errors.Count} hata bulundu.\n\nAyrıntılar Console'da.",
                "Tamam");
        }
    }
}
#endif
