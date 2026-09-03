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
            ValidateModCatalog(errors, notes);
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

            // 3b) Yönlü ışık ve GÖLGE. Kodla eklenen Light'ın shadows varsayılanı
            //     None — hiyerarşi menüsünden eklenen ışığın aksine. Sahne
            //     kurulumu kodla yapıldığı için Game sahnesi uzun süre tamamen
            //     gölgesiz render edildi ve bu hiçbir uyarı üretmedi. Araçların
            //     zemine oturmaması, binaların yere gölge düşürmemesi buradan
            //     geliyordu.
            var directionals = All<Light>().Where(l => l.type == LightType.Directional).ToArray();
            if (directionals.Length == 0)
                notes.Add($"{name}: yönlü ışık yok — sahne yalnızca ortam ışığıyla aydınlanıyor.");
            else if (directionals.All(l => l.shadows == LightShadows.None))
                errors.Add($"{name}: yönlü ışığın GÖLGESİ KAPALI — hiçbir nesne " +
                           "gölge düşürmez, her şey düz görünür.");

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

            // 5b) Ana menüde ilerleme arayüzü. DriverProfile ~Bootstrap'te XP'yi
            //     PlayerStats'ten türetiyor ama rozet/görev ekranı menüde olmazsa
            //     oyuncu seviyesini hiç göremez — sistem çalışır ama görünmez.
            if (name == "MainMenu")
            {
                if (All<DreamCar.Core.DriverProfile>().Length == 0)
                    errors.Add($"{name}: DriverProfile YOK (~Bootstrap) — sürücü " +
                               "seviyesi hiç hesaplanmaz.");
                if (All<DreamCar.Progression.MissionSystem>().Length == 0)
                    errors.Add($"{name}: MissionSystem YOK — günlük görevler çalışmaz.");
                if (All<DreamCar.UI.DriverLevelBadge>().Length == 0)
                    notes.Add($"{name}: DriverLevelBadge yok — seviye rozeti görünmez.");
                if (All<DreamCar.UI.MissionPanel>().Length == 0)
                    notes.Add($"{name}: MissionPanel yok — görev ekranı açılamaz.");
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
                    var carPrefab = Resources.Load<GameObject>(def.resourcePrefabName);
                    if (carPrefab == null)
                        errors.Add($"CarDefinition '{def.id}': Resources/{def.resourcePrefabName} " +
                                   "bulunamadı — o araç seçilince doğmaz.");
                    else
                        CheckVehicleContracts(def.id, carPrefab, errors);

                    // Küçük resim yoksa garajda ve mağazada BOŞ bir dikdörtgen
                    // görünür. Hata vermez, sadece araç görünmez — tam olarak
                    // bu denetimin var olma sebebi olan sessiz kusur.
                    if (def.thumbnail == null)
                        errors.Add($"CarDefinition '{def.id}': küçük resmi yok — " +
                                   "mağaza satırında boş kare görünür " +
                                   "(DreamCar → Procedural → Araç küçük resimlerini üret).");

                    // Menü garajındaki 3B önizleme buna bağlı. Çözülmezse
                    // garajın ortası boş kalır — yine sessizce.
                    if (string.IsNullOrEmpty(def.previewPrefabName) ||
                        Resources.Load<GameObject>(def.previewPrefabName) == null)
                        errors.Add($"CarDefinition '{def.id}': önizleme prefabı yok " +
                                   $"('{def.previewPrefabName}') — menü garajında araç görünmez.");
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

        // Faz 1 sözleşmeleri araç prefabında duruyor mu?
        //
        // Bu üç bileşen olmadan araç ÇALIŞIR ama sessizce eksilir: nitro üst hız
        // bonusu vermez (VehicleStatSheet yoksa Set çağrısı atlanır), telemetriye
        // dayanan hiçbir sistem veri bulamaz ve "bu araç benim mi" sorusu her
        // bileşende yeniden yanıtlanır. Hiçbiri hata basmaz — tam olarak bu
        // denetimin var olma sebebi.
        static void CheckVehicleContracts(string carId, GameObject prefab, List<string> errors)
        {
            if (prefab.GetComponent<DreamCar.Car.VehicleStatSheet>() == null)
                errors.Add($"Araç '{carId}': VehicleStatSheet yok — nitro ve " +
                           "yükseltmelerin istatistik etkisi sessizce kaybolur.");

            if (prefab.GetComponent<DreamCar.Car.IVehicleStats>() == null)
                errors.Add($"Araç '{carId}': IVehicleStats sağlayan bileşen yok " +
                           "(VehicleTelemetry) — devir, vites, tekerlek kayması okunamaz.");

            if (prefab.GetComponent<DreamCar.Car.IVehicleAuthority>() == null)
                errors.Add($"Araç '{carId}': IVehicleAuthority sağlayan bileşen yok " +
                           "(VehicleAuthority) — sahiplik sorusu tek noktadan yanıtlanamaz.");

            if (prefab.GetComponent<Customization.CarCustomization>() == null)
                errors.Add($"Araç '{carId}': CarCustomization yok — satın alınan " +
                           "modifikasyonların hiçbiri bu araçta görünmez.");

            // Sürüş yardımcıları YALNIZCA kendi CarController'ımızda çalışıyor;
            // RCCP'li araçta bileşen kendini kapatıyor. Bu yüzden bileşeni
            // yalnızca CarController varsa şart koşuyoruz.
            if (prefab.GetComponent<DreamCar.Car.CarController>() != null)
            {
                if (prefab.GetComponent<DreamCar.Vehicle.DrivingAssists>() == null)
                    errors.Add($"Araç '{carId}': CarController var ama DrivingAssists yok — " +
                               "ABS/TC/ESP hiç çalışmaz (sessizce).");

                // DrivingAssists slip verisini IVehicleStats'ten okuyor; yoksa
                // hiçbir müdahale yapamaz.
                if (prefab.GetComponent<DreamCar.Car.IVehicleStats>() == null)
                    errors.Add($"Araç '{carId}': DrivingAssists için IVehicleStats yok " +
                               "(VehicleTelemetry) — yardımcılar tekerlek kaymasını okuyamaz.");
            }
        }

        // Modifikasyon kataloğu ile kod arasındaki sözleşme.
        //
        // Üç şey birbirine bağlı ve üçü de sessizce kopabiliyor:
        //   • katalogdaki slot adı ↔ modülün Slot değeri
        //   • ürünün childName'i ↔ araç prefabındaki kapalı çocuk
        //   • kataloğun kendisi ↔ Resources altındaki konumu
        // Herhangi biri eşleşmezse oyuncu parçayı satın alır, "Tak"a basar ve
        // HİÇBİR ŞEY olmaz. Hata da basılmaz.
        static void ValidateModCatalog(List<string> errors, List<string> notes)
        {
            var catalog = Resources.Load<Economy.ModCatalog>("ModCatalog");
            if (catalog == null)
            {
                errors.Add("Resources/ModCatalog.asset YOK — modifikasyon ekranı " +
                           "tamamen boş görünür (DreamCar → BUILD EVERYTHING).");
                return;
            }

            if (catalog.items == null || catalog.items.Count == 0)
            {
                errors.Add("ModCatalog BOŞ — modifikasyon ekranında hiçbir parça listelenmez.");
                return;
            }

            var knownSlots = new HashSet<string>(Customization.CustomizationRuntime.KnownSlots());
            var missingSlots = new HashSet<string>();

            // Araç prefabları: childName kontrolü için hepsine bakıyoruz, çünkü
            // spoiler geometrisi yalnızca prosedürel üretilen araçlarda var.
            var carPrefabs = new List<GameObject>();
            var carCatalog = FindFirst<Economy.CarCatalog>();
            if (carCatalog != null && carCatalog.cars != null)
                foreach (var def in carCatalog.cars)
                {
                    if (def == null || string.IsNullOrEmpty(def.resourcePrefabName)) continue;
                    var prefab = Resources.Load<GameObject>(def.resourcePrefabName);
                    if (prefab != null) carPrefabs.Add(prefab);
                }

            foreach (var item in catalog.items)
            {
                if (item == null) continue;

                if (!knownSlots.Contains(item.slot))
                {
                    // Slot başına tek hata: on ürünlü bir slot on satır basmasın.
                    if (missingSlots.Add(item.slot))
                        errors.Add($"ModCatalog: '{item.slot}' slotunun MODÜLÜ YOK " +
                                   "(CustomizationRuntime.Factories) — o slottaki her parça " +
                                   "satın alınır ve hiçbir şey yapmaz.");
                    continue;
                }

                if (string.IsNullOrEmpty(item.childName)) continue;

                foreach (var prefab in carPrefabs)
                    if (FindChild(prefab.transform, item.childName) == null)
                        notes.Add($"'{item.displayName}' parçası '{prefab.name}' aracında " +
                                  $"'{item.childName}' nesnesini bulamıyor — o araçta görünmez.");
            }

            // Kodda olup katalogda hiç ürünü olmayan slot: hata değil, ama
            // sekme boş görünür.
            foreach (var slot in knownSlots)
                if (catalog.InSlot(slot).Count == 0)
                    notes.Add($"'{slot}' slotunda hiç parça yok — sekmesi boş açılır.");
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
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
