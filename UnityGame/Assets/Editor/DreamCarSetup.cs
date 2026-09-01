#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Events;   // AddPersistentListener
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DreamCar.Car;
using DreamCar.Effects;
using DreamCar.Audio;
using DreamCar.Vehicle;
using DreamCar.Customization;
using DreamCar.InputSystemMobile;
using DreamCar.Network;
using DreamCar.UI;
using DreamCar.Environment;
using DreamCar.CameraModes;
using DreamCar.Economy;
using DreamCar.Rewards;
using DreamCar.Moderation;
using DreamCar.Social;
using DreamCar.Backend;
using DreamCar.AppMeta;
using DreamCar.Game;
using DreamCar.Maps;

namespace DreamCar.EditorTools
{
    // Manual scene/prefab setup wizard. Ships Car.prefab + MainMenu.unity + Game.unity
    // + Build Settings entries with all 85 runtime scripts pre-wired. Menu:
    //   DreamCar → Setup → …
    public static class DreamCarSetup
    {
        const string ResourcesFolder = "Assets/Resources";
        const string ScenesFolder = "Assets/Scenes";
        const string CarPrefabPath = "Assets/Resources/Car.prefab";
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string GamePath = "Assets/Scenes/Game.unity";

        [MenuItem("DreamCar/Setup/Run All (Prefab + Scenes + Build Settings)")]
        public static void RunAll()
        {
            CreateCarPrefab();
            CreateMainMenu();
            CreateGameScene();
            AddScenesToBuildSettings();
            EditorUtility.DisplayDialog("DreamCar Setup",
                "Tamamlandı!\n\n" +
                "1) Assets/Resources/Car.prefab\n" +
                "2) Assets/Scenes/MainMenu.unity\n" +
                "3) Assets/Scenes/Game.unity\n" +
                "4) Build Settings güncellendi\n\n" +
                "MainMenu sahnesini aç ve Play'e bas.",
                "OK");
        }

        // ---------------------------------------------------------- Car prefab
        [MenuItem("DreamCar/Setup/Create Car Prefab")]
        public static void CreateCarPrefab()
        {
            EnsureFolder(ResourcesFolder);
            var car = new GameObject("Car");

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(car.transform, false);
            body.transform.localScale = new Vector3(1.8f, 0.8f, 4f);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());

            var rb = car.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;

            var chassis = car.AddComponent<BoxCollider>();
            chassis.size = new Vector3(1.8f, 0.8f, 4f);
            chassis.center = new Vector3(0f, 0.6f, 0f);

            var wheels = new (string name, Vector3 pos, bool front)[]
            {
                ("FL", new Vector3(-0.9f, 0.3f,  1.4f), true),
                ("FR", new Vector3( 0.9f, 0.3f,  1.4f), true),
                ("RL", new Vector3(-0.9f, 0.3f, -1.4f), false),
                ("RR", new Vector3( 0.9f, 0.3f, -1.4f), false),
            };
            var front = new System.Collections.Generic.List<(WheelCollider col, Transform mesh)>();
            var rear = new System.Collections.Generic.List<(WheelCollider col, Transform mesh)>();

            foreach (var w in wheels)
            {
                var pivot = new GameObject(w.name);
                pivot.transform.SetParent(car.transform, false);
                pivot.transform.localPosition = w.pos;
                var wc = pivot.AddComponent<WheelCollider>();
                wc.mass = 20f; wc.radius = 0.35f; wc.wheelDampingRate = 0.25f;
                wc.suspensionDistance = 0.25f;
                var s = wc.suspensionSpring; s.spring = 35000f; s.damper = 4500f; s.targetPosition = 0.5f;
                wc.suspensionSpring = s;

                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mesh.name = w.name + "_Mesh";
                Object.DestroyImmediate(mesh.GetComponent<CapsuleCollider>());
                mesh.transform.SetParent(car.transform, false);
                mesh.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
                mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                (w.front ? front : rear).Add((wc, mesh.transform));
            }

            // CarController + axles
            var cc = car.AddComponent<CarController>();
            cc.axles = new[]
            {
                new CarController.AxleInfo { leftWheel = front[0].col, rightWheel = front[1].col,
                                             leftMesh  = front[0].mesh, rightMesh = front[1].mesh,
                                             steering = true, motor = false },
                new CarController.AxleInfo { leftWheel = rear[0].col,  rightWheel = rear[1].col,
                                             leftMesh  = rear[0].mesh, rightMesh = rear[1].mesh,
                                             steering = false, motor = true },
            };

            // Photon sync
            var pv = car.AddComponent<Photon.Pun.PhotonView>();
            var sync = car.AddComponent<CarNetworkSync>();
            pv.ObservedComponents = new System.Collections.Generic.List<Component> { sync };
            pv.Synchronization = Photon.Pun.ViewSynchronization.UnreliableOnChange;

            // Gameplay components — CarNitro ve CarDamage'ın ses alanları aşağıdaki
            // Audio bloğunda doldurulur, o yüzden referansları burada saklıyoruz.
            var nitro = car.AddComponent<CarNitro>();
            var damage = car.AddComponent<CarDamage>();
            car.AddComponent<CarPaint>();
            car.AddComponent<CruiseControl>();
            car.AddComponent<GearBox>();
            car.AddComponent<FuelSystem>();
            var horn = car.AddComponent<Emote.HornController>();
            car.AddComponent<Core.StatsTracker>();

            // Audio
            var engineSrcIdle = car.AddComponent<AudioSource>();
            engineSrcIdle.loop = true; engineSrcIdle.spatialBlend = 1f; engineSrcIdle.volume = 0.6f;
            var engineSrcRev = car.AddComponent<AudioSource>();
            engineSrcRev.loop = true; engineSrcRev.spatialBlend = 1f; engineSrcRev.volume = 0f;
            var engine = car.AddComponent<EngineAudio>();
            engine.idleLoop = engineSrcIdle;
            engine.revLoop = engineSrcRev;

            var screechSrc = car.AddComponent<AudioSource>();
            screechSrc.loop = true; screechSrc.spatialBlend = 1f; screechSrc.playOnAwake = false;
            var screech = car.AddComponent<TireScreechAudio>();
            screech.wheels = new[] { front[0].col, front[1].col, rear[0].col, rear[1].col };
            screech.loop = screechSrc;

            // Korna kaynağı — HornController'ın horn alanı boş kalırsa korna sessizdir.
            var hornSrc = car.AddComponent<AudioSource>();
            hornSrc.spatialBlend = 1f; hornSrc.playOnAwake = false;
            horn.horn = hornSrc;

            // Nitro döngüsü ve çarpma sesi. Taban volume sıfır bırakılamaz: CarNitro ve
            // CarDamage bu kaynakları Awake'te AudioBus.RegisterSfx ile kaydeder ve o
            // andaki volume'u "taban seviye" kabul eder — 0 olursa hep kısık kalır.
            var nitroSrc = car.AddComponent<AudioSource>();
            nitroSrc.loop = true; nitroSrc.spatialBlend = 1f; nitroSrc.playOnAwake = false;
            nitroSrc.volume = 0.5f;
            nitroSrc.rolloffMode = AudioRolloffMode.Linear; nitroSrc.maxDistance = 50f;
            nitro.nitroLoop = nitroSrc;

            var crashSrc = car.AddComponent<AudioSource>();
            crashSrc.spatialBlend = 1f; crashSrc.playOnAwake = false;
            crashSrc.volume = 0.8f;
            crashSrc.rolloffMode = AudioRolloffMode.Linear; crashSrc.maxDistance = 70f;
            damage.crashSfx = crashSrc;

            // Prosedürel sentezleyici — klip dosyası gerektirmez. Prosedürel araç
            // üretecindekiyle aynı kurulum: bağlanmayan kaynak için klip üretilmez,
            // yani o ses tamamen sessiz kalır.
            var synth = car.GetComponent<ProceduralEngineAudio>();
            if (!synth) synth = car.AddComponent<ProceduralEngineAudio>();
            synth.idleSource = engineSrcIdle;
            synth.revSource = engineSrcRev;
            synth.screechSource = screechSrc;
            synth.hornSource = hornSrc;
            synth.nitroSource = nitroSrc;
            synth.crashSource = crashSrc;

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(car, CarPrefabPath, out bool ok);
            Object.DestroyImmediate(car);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ok) Debug.LogError("[DreamCarSetup] Prefab save failed at " + CarPrefabPath);
            else Debug.Log("[DreamCarSetup] Car prefab created at " + CarPrefabPath);
        }

        // ---------------------------------------------------------- MainMenu scene
        [MenuItem("DreamCar/Setup/Create MainMenu Scene")]
        public static void CreateMainMenu()
        {
            EnsureFolder(ScenesFolder);
            // Sprite'lar olmadan bütün arayüz düz dikdörtgene düşer. BUILD
            // EVERYTHING zinciri bunları sahnelerden önce üretiyor, ama bu menü
            // tek başına da çağrılabiliyor.
            EnsureUiSprites();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem + camera
            new GameObject("EventSystem").AddComponent<EventSystem>().gameObject.AddComponent<StandaloneInputModule>();
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 1f, -10f);

            // Bootstrap
            var boot = new GameObject("~Bootstrap");
            boot.AddComponent<GameBootstrap>();
            boot.AddComponent<PhotonConnector>();
            boot.AddComponent<ReconnectionManager>();
            // Harita kataloğu olmadan lobi seçilen haritayı çözemez ve her zaman
            // sabit "Game" sahnesini yükler — sekiz harita sahnesi hiç açılmazdı.
            boot.AddComponent<LobbyManager>().mapCatalog =
                Procedural.Maps.ProceduralMapGenerator.LoadMapCatalog();
            boot.AddComponent<LoginStreak>();
            boot.AddComponent<BanList>();
            boot.AddComponent<PlayerMoney>();
            // catalog hiç atanmıyordu: garaj boş görünüyor, hiçbir araç satın
            // alınamıyor ve RoomManager aktif aracı bulamayıp hep varsayılana
            // düşüyordu. Araç ekonomisinin tamamı buna bağlı.
            boot.AddComponent<CarInventory>().catalog =
                Procedural.ProceduralCarGenerator.LoadCatalog();
            boot.AddComponent<Core.PlayerStats>();
            boot.AddComponent<Core.ObjectPool>();
            boot.AddComponent<CrashReporter>();
            boot.AddComponent<MusicManager>();
            boot.AddComponent<ChatProfanityFilter>();
            boot.AddComponent<Localization.LocalizationManager>();
            boot.AddComponent<PlayFabCloudSave>();
            boot.AddComponent<Notifications.PushNotificationsManager>();
            boot.AddComponent<Notifications.LocalNotificationScheduler>();
            boot.AddComponent<Monetization.CASAdsManager>();
            boot.AddComponent<Voice.PlayerVoiceMute>();
            boot.AddComponent<RemoteConfig>();
            boot.AddComponent<Core.DeepLinkManager>();
            boot.AddComponent<Core.Haptics>();
            boot.AddComponent<ChatRateLimiter>();
            boot.AddComponent<DreamCar.Settings.QualityAutoDetect>();
            // GameSettings hiçbir sahneye eklenmiyordu. Ona bağlı olan HER ŞEY
            // "if (GameSettings.Instance…)" ile korunduğu için sessizce hiçbir iş
            // görmüyordu: Ayarlar ekranındaki sürgüler, direksiyon hassasiyeti
            // (MobileTouchInput), kalite tercihinin kaydı (QualityAutoDetect) ve
            // ayarların buluta yazılması (PlayFabCloudSave). Ses sürgüleri
            // tesadüfen çalışıyordu — AudioBus tercihleri kendi
            // [RuntimeInitializeOnLoadMethod]'uyla yüklüyor.
            boot.AddComponent<DreamCar.Settings.GameSettings>();
            boot.AddComponent<PlayFabAuth>();
            boot.AddComponent<PlayFabMoneySync>();
            boot.AddComponent<PlayFabInventoryBridge>();
            boot.AddComponent<PlayFabLeaderboards>();
            // Katalog olmadan hiçbir başarım değerlendirilemez: EvaluateForStat
            // katalogdaki tanımlar üzerinde dönüyor.
            boot.AddComponent<PlayFabAchievements>().catalog =
                Procedural.ProceduralAchievements.Load();
            boot.AddComponent<ReferralSystem>();
            boot.AddComponent<PlayFabFriends>();
            boot.AddComponent<PlayedWithList>();
            boot.AddComponent<RateAppPopup>();
            boot.AddComponent<Monetization.IAPManager>();
            boot.AddComponent<Monetization.AdsManager>();

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Varsayılan 0 = yalnızca genişliğe göre ölçekle. 2340x1080 bir telefonda
            // ölçek 1.22 olur ve görünür dikey aralık ±540 yerine ±443'e iner —
            // ekranların "Kapat" butonları ekran dışında kalırdı. 1 (yükseklik) ise
            // iPad 4:3'te yatay kenarları kırpar. 0.5 ikisinin ortası.
            scaler.matchWidthOrHeight = 0.5f;

            // Main panel
            var mainPanel = MakeUiChild(canvasGo, "MainPanel");
            var titleLabel = MakeText(mainPanel, "TitleLabel", "DreamCar", new Vector2(0f, 400f), 96);
            var nickInput = MakeInputField(mainPanel, "NicknameInput", "Kullanıcı adı", new Vector2(0f, 200f));
            var playBtn = MakeButton(mainPanel, "PlayButton", "OYNA", new Vector2(0f, 40f), key: "play");
            var statusText = MakeText(mainPanel, "StatusText", "Bağlanıyor…", new Vector2(0f, -100f), 32);

            var mainMenuUI = mainPanel.AddComponent<MainMenuUI>();
            mainMenuUI.nicknameInput = nickInput;
            mainMenuUI.playButton = playBtn;
            mainMenuUI.statusText = statusText;

            // --- Garaj ---
            // GarageCarousel yazılmıştı ama hiçbir sahneye eklenmiyordu: oyuncu araç
            // satın alabiliyor ama satın aldığını SEÇEMİYORDU, hep başlangıç aracıyla
            // oynuyordu. Sol sütuna kuruluyor; nav butonları y=-300'de, burası boş.
            var garageThumbGo = new GameObject("GarageThumb", typeof(RectTransform), typeof(Image));
            garageThumbGo.transform.SetParent(mainPanel.transform, false);
            var garageThumbRt = garageThumbGo.GetComponent<RectTransform>();
            garageThumbRt.anchoredPosition = new Vector2(-600f, 140f);
            garageThumbRt.sizeDelta = new Vector2(360f, 200f);
            var garageThumb = garageThumbGo.GetComponent<Image>();
            Skin(garageThumb, "panel", Palette.Surface);
            garageThumb.preserveAspect = true;

            var garagePrev = MakeChevronButton(mainPanel, "GaragePrev", new Vector2(-820f, 140f), pointRight: false);
            garagePrev.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 120f);
            var garageNext = MakeChevronButton(mainPanel, "GarageNext", new Vector2(-380f, 140f), pointRight: true);
            garageNext.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 120f);

            var garageName = MakeText(mainPanel, "GarageName", "-", new Vector2(-600f, 10f), 36);
            var garagePrice = MakeText(mainPanel, "GaragePrice", "-", new Vector2(-600f, -50f), 28);
            var garageSelect = MakeButton(mainPanel, "GarageSelect", "Seç", new Vector2(-600f, -140f), key: "select");

            var garage = mainPanel.AddComponent<GarageCarousel>();
            garage.prevButton = garagePrev;
            garage.nextButton = garageNext;
            garage.selectButton = garageSelect;
            garage.nameLabel = garageName;
            garage.priceOrOwnedLabel = garagePrice;
            garage.thumbnail = garageThumb;
            // previewMount bilerek boş: 3B önizleme araç prefabını Instantiate ederdi,
            // o prefab PhotonView ve Rigidbody taşıyor — menü sahnesinde odaya bağlı
            // olmadan doğurmak hata üretir. Alan zaten null kontrolüyle korunuyor.

            // Lobby panel (inactive by default)
            var lobbyPanel = MakeUiChild(canvasGo, "LobbyPanel");
            lobbyPanel.SetActive(false);
            mainMenuUI.lobbyPanel = lobbyPanel;

            var lobbyTitle = MakeText(lobbyPanel, "LobbyTitle", "Odalar", new Vector2(-500f, 400f), 64);
            var quickJoinBtn = MakeButton(lobbyPanel, "QuickJoinButton", "Hızlı Katıl", new Vector2(400f, 400f), key: "room.quick_join");
            var createNameInput = MakeInputField(lobbyPanel, "CreateRoomInput", "Yeni oda adı", new Vector2(400f, 300f));
            var createBtn = MakeButton(lobbyPanel, "CreateButton", "OLUŞTUR", new Vector2(400f, 200f), key: "room.create");
            // Düz bir RectTransform'du: satırların hepsi üst üste biniyor ve liste
            // taşınca kaydırılamıyordu. Diğer ekranlarla aynı kaydırılabilir kabı kullan.
            var roomListParent = MakeListContainer(lobbyPanel, "RoomList",
                new Vector2(-400f, 0f), new Vector2(600f, 700f));

            var lobbyUI = lobbyPanel.AddComponent<LobbyUI>();
            lobbyUI.createRoomInput = createNameInput;
            lobbyUI.createButton = createBtn;
            lobbyUI.quickJoinButton = quickJoinBtn;
            lobbyUI.roomListParent = roomListParent;
            // roomEntryPrefab hiç atanmıyordu: Refresh() erken çıkıyor, oda listesi
            // hep boş kalıyor ve oyuncu hiçbir odaya giremiyordu.
            lobbyUI.roomEntryPrefab = MakeRoomEntryTemplate(lobbyPanel, "RoomEntryTemplate");

            // MainMenuUI.OnPlay lobiyi açıp MainPanel'i KAPATIYOR. Sekiz navigasyon
            // butonunun hepsi MainPanel'in çocuğu ve lobide geri dönüş yoktu:
            // OYNA'ya bir kez basan oyuncu ayarlara, garaja, araç mağazasına,
            // liderliğe ve istatistiklere o oturum boyunca bir daha ulaşamıyordu.
            // (MainMenuUI kendini de kapattığı için kendi kendine geri gelemiyor.)
            var lobbyBack = MakeButton(lobbyPanel, "BackButton", "‹ Geri", Vector2.zero, key: "back");
            AnchorCorner(lobbyBack.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(150f, 70f), new Vector2(220f, 100f));
            UnityEventTools.AddBoolPersistentListener(lobbyBack.onClick, lobbyPanel.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(lobbyBack.onClick, mainPanel.SetActive, true);

            // Toast stack
            // toastRoot güvenli alana oturan tam ekran kap (MakeUiChild ekliyor);
            // yığın onun ALTINDA ayrı bir çocuk olmalı — anchor'larını burada
            // ezseydik SafeAreaFitter her karede geri yazardı.
            var toastRoot = MakeUiChild(canvasGo, "ToastStack");
            var toastColumn = new GameObject("ToastColumn", typeof(RectTransform));
            toastColumn.transform.SetParent(toastRoot.transform, false);
            var toastRt = toastColumn.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.5f, 0f); toastRt.anchorMax = new Vector2(0.5f, 0f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.anchoredPosition = new Vector2(0f, 140f);
            toastRt.sizeDelta = new Vector2(900f, 400f);
            var toastLayout = toastColumn.AddComponent<VerticalLayoutGroup>();
            toastLayout.spacing = 8f;
            toastLayout.childAlignment = TextAnchor.LowerCenter;
            toastLayout.childControlHeight = false; toastLayout.childForceExpandHeight = false;
            toastLayout.childControlWidth = true;   toastLayout.childForceExpandWidth = true;

            var toast = boot.AddComponent<ToastNotification>();
            toast.stackParent = toastRt;
            // toastPrefab hiç atanmıyordu: ShowInternal ilk satırda erken çıkıyor ve
            // projedeki bütün ToastNotification.Show() çağrıları sessizce düşüyordu.
            toast.toastPrefab = MakeToastTemplate(canvasGo, "ToastTemplate");

            // Ek ekranlar (Ayarlar / Liderlik / Başarımlar / Mağaza / İstatistik)
            BuildSecondaryScreens(canvasGo, mainPanel, boot);

            // Loading overlay
            BuildLoadingScreen(canvasGo, boot);

            // Reconnect overlay
            BuildReconnectOverlay(canvasGo, boot);

            // Save
            EditorSceneManager.SaveScene(scene, MainMenuPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DreamCarSetup] MainMenu scene created at " + MainMenuPath);
        }

        // ---------------------------------------------------------- Game scene
        [MenuItem("DreamCar/Setup/Create Game Scene")]
        public static void CreateGameScene()
        {
            EnsureFolder(ScenesFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem'i BuildGameplayUI kuruyor (harita sahneleri de aynı
            // metottan alsın diye oraya taşındı).

            // Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(100f, 1f, 100f);

            // Sun
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CarCameraFollow>();
            var cameraModes = camGo.AddComponent<CameraModeController>();
            cameraModes.follow = follow;

            // Spawn points
            for (int i = 0; i < 4; i++)
            {
                var sp = new GameObject("SpawnPoint_" + i);
                float angle = i * 90f;
                sp.transform.position = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 3f, 1f,
                                                    Mathf.Sin(angle * Mathf.Deg2Rad) * 3f);
                sp.transform.rotation = Quaternion.Euler(0f, angle + 90f, 0f);
            }
            var spawns = new Transform[4];
            for (int i = 0; i < 4; i++) spawns[i] = GameObject.Find("SpawnPoint_" + i).transform;

            // Bootstrap
            var boot = new GameObject("~Bootstrap");
            boot.AddComponent<GameBootstrap>();
            boot.AddComponent<PhotonConnector>();
            boot.AddComponent<ReconnectionManager>();
            boot.AddComponent<PlayerMoney>();
            // catalog hiç atanmıyordu: garaj boş görünüyor, hiçbir araç satın
            // alınamıyor ve RoomManager aktif aracı bulamayıp hep varsayılana
            // düşüyordu. Araç ekonomisinin tamamı buna bağlı.
            boot.AddComponent<CarInventory>().catalog =
                Procedural.ProceduralCarGenerator.LoadCatalog();
            boot.AddComponent<Core.PlayerStats>();
            boot.AddComponent<Core.ObjectPool>();
            boot.AddComponent<CrashReporter>();
            boot.AddComponent<MusicManager>();
            boot.AddComponent<ChatProfanityFilter>();
            boot.AddComponent<Localization.LocalizationManager>();
            boot.AddComponent<PlayFabCloudSave>();
            boot.AddComponent<Notifications.PushNotificationsManager>();
            boot.AddComponent<Notifications.LocalNotificationScheduler>();
            boot.AddComponent<Monetization.CASAdsManager>();
            boot.AddComponent<Voice.PlayerVoiceMute>();
            boot.AddComponent<RemoteConfig>();
            boot.AddComponent<Core.DeepLinkManager>();
            boot.AddComponent<Core.Haptics>();
            boot.AddComponent<ChatRateLimiter>();
            boot.AddComponent<DreamCar.Settings.QualityAutoDetect>();
            // GameSettings hiçbir sahneye eklenmiyordu. Ona bağlı olan HER ŞEY
            // "if (GameSettings.Instance…)" ile korunduğu için sessizce hiçbir iş
            // görmüyordu: Ayarlar ekranındaki sürgüler, direksiyon hassasiyeti
            // (MobileTouchInput), kalite tercihinin kaydı (QualityAutoDetect) ve
            // ayarların buluta yazılması (PlayFabCloudSave). Ses sürgüleri
            // tesadüfen çalışıyordu — AudioBus tercihleri kendi
            // [RuntimeInitializeOnLoadMethod]'uyla yüklüyor.
            boot.AddComponent<DreamCar.Settings.GameSettings>();
            boot.AddComponent<PlayFabAuth>();
            // Katalog olmadan hiçbir başarım değerlendirilemez: EvaluateForStat
            // katalogdaki tanımlar üzerinde dönüyor.
            boot.AddComponent<PlayFabAchievements>().catalog =
                Procedural.ProceduralAchievements.Load();
            boot.AddComponent<RateAppPopup>();

            var roomManager = boot.AddComponent<RoomManager>();
            roomManager.spawnPoints = spawns;

            // FX'siz Weather hiçbir şey yapmaz; yağmur/kar partikülleri ve ses
            // döngüsü burada kurulup alanlarına bağlanır.
            var weather = boot.AddComponent<Weather>();
            Procedural.ProceduralWeather.Attach(boot, weather);
            boot.AddComponent<DayNightCycle>().sun = sun;
            // catalog hiç atanmıyordu: ApplyForRoom ilk guard'da dönüyor ve harita
            // varyantı (gündüz/gece/yağmur) hiç uygulanmıyordu. Katalog henüz
            // üretilmemişse null kalır — BUILD EVERYTHING zinciri haritaları
            // sahnelerden önce üretiyor, o yüzden normal akışta dolu gelir.
            boot.AddComponent<MapSelector>().catalog =
                Procedural.Maps.ProceduralMapGenerator.LoadMapCatalog();

            // Oda içi bileşenler — sadece Game sahnesinde anlamlı.
            AddReflectionProbe(boot, extent: 600f, height: 50f);

            boot.AddComponent<NetworkInterestManager>();
            // Oynanış UI'sinin tamamı — HUD, kontroller, duraklatma, sohbet…
            // Harita sahneleri de aynı metodu çağırıyor (aşağıdaki nota bak).
            BuildGameplayUI(boot);

            EditorSceneManager.SaveScene(scene, GamePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DreamCarSetup] Game scene created at " + GamePath);
        }

        // Oynanış UI'sinin TAMAMI burada kurulur: HUD, minimap, toast yığını,
        // sohbet, dokunmatik sürüş kontrolleri, nitro ve yakıt barları, yakıt
        // istasyonu paneli, duraklatma menüsü ve oyun içi ayarlar ekranı.
        //
        // NEDEN AYRI BİR METOT: Bu blok eskiden CreateGameScene'in gövdesinin
        // içindeydi, yani yalnızca Game.unity'ye kuruluyordu. Oysa oyun normal
        // akışta Game.unity'yi HİÇ yüklemiyor — LobbyManager odadaki "map"
        // özelliğine bakıp harita sahnesini açıyor (LobbyManager.ResolveSceneName)
        // ve RoomCreatorUI o özelliği her zaman yazıyor.
        //
        // Harita sahnelerinde ise Canvas da, EventSystem de, MobileTouchInput da
        // yoktu. Sonuç: oyuncu oda kuruyor, harita seçiyor, araç doğuyor, kamera
        // onu takip ediyor — ve gaz pedalı, direksiyon, hız göstergesi,
        // duraklatma yok. Hiçbir hata da basılmıyordu, çünkü
        // RoomManager.SpawnLocalCar bulamadığı MobileTouchInput'u "if (input)"
        // ile sessizce geçiyor.
        //
        // Artık tek kaynak: hem CreateGameScene hem ProceduralMapGenerator bunu
        // çağırıyor. İki ayrı kopya tutmak kaçınılmaz olarak ayrışırdı.
        public static void BuildGameplayUI(GameObject boot)
        {
            EnsureUiSprites();

            // EventSystem olmadan HİÇBİR buton çalışmaz — harita sahnelerinde
            // hiç kurulmuyordu. Sahneye özel ve DontDestroyOnLoad değil.
            if (!UnityEngine.Object.FindFirstObjectByType<EventSystem>())
                new GameObject("EventSystem").AddComponent<EventSystem>()
                    .gameObject.AddComponent<StandaloneInputModule>();

            // Aynı şekilde sahneye özel: hile tespiti yalnızca Game.unity'de vardı.
            if (!boot.GetComponent<CheatDetector>()) boot.AddComponent<CheatDetector>();

            // Canvas (HUD)
            var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Varsayılan 0 = yalnızca genişliğe göre ölçekle. 2340x1080 bir telefonda
            // ölçek 1.22 olur ve görünür dikey aralık ±540 yerine ±443'e iner —
            // ekranların "Kapat" butonları ekran dışında kalırdı. 1 (yükseklik) ise
            // iPad 4:3'te yatay kenarları kırpar. 0.5 ikisinin ortası.
            scaler.matchWidthOrHeight = 0.5f;

            // Speed HUD
            // YERLEŞİM NOTU: buradaki her şey KÖŞEYE hizalanır, merkeze göre
            // mutlak konuma değil. Mutlak konum 16:9'da doğru görünüp 19.5:9
            // telefonda ekran dışına taşıyordu — görünür dikey yarı-aralık
            // ±540 değil ±489, 21:9'da ±454, üstüne bir de SafeAreaFitter
            // kırpıyor. Yakıt çubuğu (0,-500) bu yüzden bugünkü her modern
            // telefonda görünmüyordu.
            var hudPanel = MakeUiChild(canvasGo, "HUDPanel");

            // Analog kilometre saati. SpeedometerNeedle yazılmıştı ve hiçbir
            // sahneye eklenmiyordu (Util.GameMath.SpeedometerAngle yalnızca
            // onun için var); HUD'da hız sadece bir sayıydı.
            var gaugeGo = new GameObject("SpeedGauge", typeof(RectTransform), typeof(Image));
            gaugeGo.transform.SetParent(hudPanel.transform, false);
            var gaugeImg = gaugeGo.GetComponent<Image>();
            Skin(gaugeImg, "ring", Palette.AccentDim);
            gaugeImg.raycastTarget = false;
            AnchorTo(gaugeGo.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(280f, 280f));

            // İğne göstergenin MERKEZİNDE duruyor ama pivotu tabanında: dönüş
            // ekseni merkez olsun diye. sizeDelta yüksekliği yarıçapı belirliyor.
            var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
            needleGo.transform.SetParent(gaugeGo.transform, false);
            var needleRt = needleGo.GetComponent<RectTransform>();
            needleRt.anchorMin = new Vector2(0.5f, 0.5f);
            needleRt.anchorMax = new Vector2(0.5f, 0.5f);
            needleRt.pivot = new Vector2(0.5f, 0.08f);
            needleRt.anchoredPosition = Vector2.zero;
            needleRt.sizeDelta = new Vector2(10f, 120f);
            var needleImg = needleGo.GetComponent<Image>();
            Skin(needleImg, "pill", Palette.Accent);
            needleImg.raycastTarget = false;

            // car alanı serileştirilemiyor (arayüz tipi); bileşen yerel aracı
            // kendisi buluyor.
            hudPanel.AddComponent<SpeedometerNeedle>().needle = needleRt;

            var speedText = MakeText(hudPanel, "SpeedText", "0 km/h", Vector2.zero, 44);
            AnchorTo(speedText.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(240f, 70f));

            var playerCountText = MakeText(hudPanel, "PlayerCount", "0/16", Vector2.zero, 32);
            AnchorCorner(playerCountText.GetComponent<RectTransform>(),
                         new Vector2(1f, 1f), new Vector2(260f, 190f), new Vector2(360f, 60f));

            var roomNameText = MakeText(hudPanel, "RoomName", "-", Vector2.zero, 32);
            roomNameText.alignment = TextAlignmentOptions.MidlineLeft;
            AnchorCorner(roomNameText.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(330f, 70f), new Vector2(600f, 70f));
            // Sağ ALT köşe artık gaz/fren/el freni için ayrıldı; çıkış butonu orada
            // kalsaydı gaz pedalıyla çakışırdı. Sağ üste, köşeye sabitlendi.
            var leaveBtn = MakeButton(hudPanel, "LeaveButton", "Çıkış", Vector2.zero);
            AnchorCorner(leaveBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 1f), new Vector2(160f, 70f), new Vector2(240f, 100f));

            var hud = hudPanel.AddComponent<InGameHUD>();
            hud.speedText = speedText;
            hud.playerCountText = playerCountText;
            hud.roomNameText = roomNameText;
            hud.leaveButton = leaveBtn;

            // --- Tamir paneli ---
            // RepairPanel hiçbir sahneye eklenmiyordu, dolayısıyla
            // CarDamage.OnDamaged'ın hiçbir abonesi yoktu: hasar birikiyor,
            // oyuncu ne görüyor ne tamir edebiliyordu (Util.GameMath.RepairPrice
            // yalnızca bunun için var).
            var repairBg = new GameObject("RepairBG", typeof(RectTransform), typeof(Image));
            repairBg.transform.SetParent(hudPanel.transform, false);
            Skin(repairBg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);
            AnchorCorner(repairBg.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                         new Vector2(260f, 400f), new Vector2(280f, 18f));

            var repairFillGo = new GameObject("RepairFill", typeof(RectTransform), typeof(Image));
            repairFillGo.transform.SetParent(repairBg.transform, false);
            var repairFill = repairFillGo.GetComponent<Image>();
            Skin(repairFill, "pill", Palette.Good);
            repairFill.type = Image.Type.Filled;
            repairFill.fillMethod = Image.FillMethod.Horizontal;
            var repairFillRt = repairFillGo.GetComponent<RectTransform>();
            repairFillRt.anchorMin = Vector2.zero; repairFillRt.anchorMax = Vector2.one;
            repairFillRt.offsetMin = Vector2.zero; repairFillRt.offsetMax = Vector2.zero;

            var repairPrice = MakeText(hudPanel, "RepairPrice", "-", Vector2.zero, 22);
            AnchorCorner(repairPrice.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                         new Vector2(430f, 400f), new Vector2(160f, 40f));

            var repairBtn = MakeButton(hudPanel, "RepairButton", "Tamir", Vector2.zero, key: "repair");
            AnchorCorner(repairBtn.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                         new Vector2(260f, 320f), new Vector2(220f, 90f));

            var repair = hudPanel.AddComponent<RepairPanel>();
            repair.healthFill = repairFill;
            repair.priceLabel = repairPrice;
            repair.repairButton = repairBtn;

            // --- Vites / drift / sıralama göstergeleri ---
            // Vites etiketi, drift skoru+combo ve drift seansı sayacı
            // hesaplanıyordu ama HİÇBİRİNİN ekran tüketicisi yoktu; yarış
            // sıralama tablosu (LeaderboardUI) da hiçbir sahneye eklenmiyordu.
            var gearLabel = MakeText(hudPanel, "GearLabel", "N", Vector2.zero, 44);
            // Göstergenin sağına: (0,200) iğnenin tam üstüne denk geliyordu.
            AnchorTo(gearLabel.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(215f, 130f), new Vector2(120f, 70f));

            var driftLabel = MakeText(hudPanel, "DriftScore", "", Vector2.zero, 36);
            AnchorTo(driftLabel.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(520f, 60f));

            var driftTimer = MakeText(hudPanel, "DriftTimer", "", Vector2.zero, 40);
            AnchorTo(driftTimer.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(260f, 60f));

            var driveHud = hudPanel.AddComponent<DriveHud>();
            driveHud.gearLabel = gearLabel;
            driveHud.driftLabel = driftLabel;
            driveHud.driftTimerLabel = driftTimer;

            // Sıralama tablosu sağ üstte, minimap'in altında. Etiket AYRI bir
            // çocuk: LeaderboardUI yarış modu dışında etiketi gizliyor ve
            // bileşen etiketin üzerinde olsaydı kendini kapatıp bir daha
            // açılamazdı.
            var lbText = MakeText(hudPanel, "RaceStandings", "", Vector2.zero, 24);
            lbText.alignment = TextAlignmentOptions.TopRight;
            AnchorCorner(lbText.GetComponent<RectTransform>(),
                         new Vector2(1f, 1f), new Vector2(230f, 800f), new Vector2(420f, 200f));
            var standings = hudPanel.AddComponent<Race.LeaderboardUI>();
            standings.label = lbText;

            // --- Araç aksiyonları (kamera / korna / sinyal / emote) ---
            // Bu sistemlerin kodu ve RPC altyapısı yazılmıştı ama hiçbirini
            // ÇAĞIRAN YOKTU: korna butonu yoktu, sinyal yoktu, emote yoktu ve
            // oyunda tek kamera açısı vardı (Cycle yalnızca KeyCode.V'ye bağlı,
            // mobilde ulaşılamaz). Hedef bileşenler araç prefabında ve araç
            // odaya girilince doğuyor, o yüzden kalıcı listener kurulamıyor —
            // CarActionButtons çağrıyı çalışma anında yerel araca iletiyor.
            //
            // Sol kenarda dikey sütun: sağ alt pedallara, sol alt direksiyon
            // pedine ayrılmış durumda.
            var actions = hudPanel.AddComponent<CarActionButtons>();

            var camBtn = MakeIconButton(hudPanel, "CameraButton", "", Vector2.zero, "icon_camera");
            AnchorCorner(camBtn.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 340f), new Vector2(120f, 110f));
            actions.cameraButton = camBtn;

            var hornBtn = MakeIconButton(hudPanel, "HornButton", "", Vector2.zero, "icon_horn");
            AnchorCorner(hornBtn.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 460f), new Vector2(120f, 110f));
            actions.hornButton = hornBtn;

            var sigLeft = MakeChevronButton(hudPanel, "SignalLeft", Vector2.zero, pointRight: false);
            AnchorCorner(sigLeft.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 580f), new Vector2(120f, 110f));
            actions.signalLeftButton = sigLeft;

            var sigRight = MakeChevronButton(hudPanel, "SignalRight", Vector2.zero, pointRight: true);
            AnchorCorner(sigRight.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 700f), new Vector2(120f, 110f));
            actions.signalRightButton = sigRight;

            var hazardBtn = MakeIconButton(hudPanel, "HazardButton", "", Vector2.zero, "icon_hazard");
            AnchorCorner(hazardBtn.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 820f), new Vector2(120f, 110f));
            actions.hazardButton = hazardBtn;

            var emoteBtn = MakeIconButton(hudPanel, "EmoteButton", "", Vector2.zero, "icon_emote");
            AnchorCorner(emoteBtn.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(100f, 940f), new Vector2(120f, 110f));
            actions.emoteButton = emoteBtn;

            var pingText = MakeText(hudPanel, "PingText", "-- ms", Vector2.zero, 24);
            AnchorCorner(pingText.GetComponent<RectTransform>(),
                         new Vector2(1f, 1f), new Vector2(260f, 250f), new Vector2(360f, 50f));
            hudPanel.AddComponent<PingIndicator>().label = pingText;

            // Minimap — 8 harita var, yön bulma olmadan oyuncu kayboluyor.
            // Ayrı bir ortografik kamera RenderTexture'a çizer, RawImage onu gösterir.
            var minimapCamGo = new GameObject("MinimapCamera");
            var minimapCam = minimapCamGo.AddComponent<Camera>();
            minimapCam.orthographic = true;
            // Minimap.Start() ortographic'i açıyor ama boyutu ayarlamıyor; varsayılan
            // 5 birim bir araba oyunu için çok dar, yol bile sığmaz.
            minimapCam.orthographicSize = 90f;
            minimapCam.nearClipPlane = 1f;
            minimapCam.farClipPlane = 400f;
            minimapCam.clearFlags = CameraClearFlags.SolidColor;
            minimapCam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
            // RenderTexture'a çiziyor — HDR ve MSAA burada sadece maliyet.
            minimapCam.allowHDR = false;
            minimapCam.allowMSAA = false;

            var minimapCamData = minimapCamGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                              ?? minimapCamGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // Minimap'te post-processing ve gölge hem çirkin durur hem ikinci kez ödenir.
            minimapCamData.renderPostProcessing = false;
            minimapCamData.renderShadows = false;

            var minimapGo = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage));
            minimapGo.transform.SetParent(hudPanel.transform, false);
            var minimapRt = minimapGo.GetComponent<RectTransform>();
            // (760,200) merkeze göreydi: iPad 4:3'te görünür yatay yarı-aralık
            // ±831 ve minimap'in sağ kenarı 890'a düşüyordu — ekran dışında.
            AnchorCorner(minimapRt, new Vector2(1f, 1f),
                         new Vector2(170f, 560f), new Vector2(260f, 260f));

            var minimap = minimapGo.AddComponent<Minimap>();
            minimap.minimapCamera = minimapCam;
            minimap.minimapImage = minimapGo.GetComponent<RawImage>();
            minimap.height = 80f;
            // minimap.target çalışma anında bağlanır: araç odaya girildiğinde
            // PhotonNetwork.Instantiate ile doğuyor (RoomManager.SpawnLocalCar).

            // Toast stack
            // toastRoot güvenli alana oturan tam ekran kap (MakeUiChild ekliyor);
            // yığın onun ALTINDA ayrı bir çocuk olmalı — anchor'larını burada
            // ezseydik SafeAreaFitter her karede geri yazardı.
            var toastRoot = MakeUiChild(canvasGo, "ToastStack");
            var toastColumn = new GameObject("ToastColumn", typeof(RectTransform));
            toastColumn.transform.SetParent(toastRoot.transform, false);
            var toastRt = toastColumn.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.5f, 0f); toastRt.anchorMax = new Vector2(0.5f, 0f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.anchoredPosition = new Vector2(0f, 140f);
            toastRt.sizeDelta = new Vector2(900f, 400f);
            var toastLayout = toastColumn.AddComponent<VerticalLayoutGroup>();
            toastLayout.spacing = 8f;
            toastLayout.childAlignment = TextAnchor.LowerCenter;
            toastLayout.childControlHeight = false; toastLayout.childForceExpandHeight = false;
            toastLayout.childControlWidth = true;   toastLayout.childForceExpandWidth = true;

            var toast = boot.AddComponent<ToastNotification>();
            toast.stackParent = toastRt;
            // toastPrefab hiç atanmıyordu: ShowInternal ilk satırda erken çıkıyor ve
            // projedeki bütün ToastNotification.Show() çağrıları sessizce düşüyordu.
            toast.toastPrefab = MakeToastTemplate(canvasGo, "ToastTemplate");

            // Chat
            // Sohbet eskiden sol ALTTAYDI ve direksiyon pediyle çakışıyordu:
            // ControlsPanel hiyerarşide ChatPanel'den SONRA geldiği için pedin
            // yarı saydam Image'i ışını yiyordu ve sohbet kutusuna hiç
            // odaklanılamıyordu; üstelik oraya dokunmak direksiyonu çeviriyordu.
            // Sürüş kontrollerinin bulunmadığı sol ÜST bölgeye taşındı.
            var chatPanel = MakeUiChild(canvasGo, "ChatPanel");

            var chatMessages = MakeText(chatPanel, "ChatMessages", "", Vector2.zero, 24);
            chatMessages.alignment = TextAlignmentOptions.BottomLeft;
            AnchorCorner(chatMessages.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(510f, 430f), new Vector2(620f, 420f));

            var chatInput = MakeInputField(chatPanel, "ChatInput", "Mesaj…", Vector2.zero);
            AnchorCorner(chatInput.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(460f, 700f), new Vector2(500f, 70f));

            var chatSend = MakeButton(chatPanel, "ChatSend", "Gönder", Vector2.zero);
            AnchorCorner(chatSend.GetComponent<RectTransform>(),
                         new Vector2(0f, 1f), new Vector2(820f, 700f), new Vector2(200f, 70f));
            // RichChatUI bir MonoBehaviourPun ve RPC'lerini bu görünüm üzerinden
            // atıyor, o yüzden PhotonView şart.
            //
            // DİKKAT — Editor'de DOĞRULANMASI GEREKEN NOKTA: sahne
            // PhotonView'larının ViewID'sini PUN, sahne kaydedilirken kendi
            // atıyor. Betikle AddComponent edilen bir görünümün bu atamayı
            // aldığını buradan doğrulayamıyorum (Photon henüz projede yok).
            // Sohbet çalışmazsa çözümü tek adım: Photon import edildikten sonra
            // sahneyi açıp kaydetmek, ya da PUN'un "Update PhotonViews in Scene"
            // menüsünü çalıştırmak. README §Sohbet'e de yazıldı.
            chatPanel.AddComponent<Photon.Pun.PhotonView>();
            var richChat = chatPanel.AddComponent<RichChatUI>();
            richChat.inputField = chatInput;
            richChat.sendButton = chatSend;
            richChat.messagesText = chatMessages;

            // Controls (mobile touch)
            // Sürüş kontrolleri ekranın köşelerine sabitlenir. Merkeze göre sabit
            // piksel konumu kullanılırsa her en-boy oranında başka yere düşerler.
            //
            // Eski yerleşimde fren (700,-450) ile el freni (550,-400) ÇAKIŞIYORDU
            // (x 560..690, y -440..-410); hiyerarşide sonra gelen el freni üstte
            // kaldığı için frenin o köşesine basmak el frenini çekiyordu.
            //
            // Boyutlar da büyütüldü: 80 referans birim ≈ 5.3 mm, Apple HIG 44pt
            // (~7.6 mm) ve Android 48dp (~9 mm) minimumlarının altındaydı.
            var ctrlPanel = MakeUiChild(canvasGo, "ControlsPanel");

            var throttleBtn = MakeButton(ctrlPanel, "Throttle", "▲", Vector2.zero);
            AnchorCorner(throttleBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 0f), new Vector2(-150f, 250f), new Vector2(230f, 190f));

            var brakeBtn = MakeButton(ctrlPanel, "Brake", "▼", Vector2.zero);
            AnchorCorner(brakeBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 0f), new Vector2(-150f, 80f), new Vector2(230f, 150f));

            var handbrakeBtn = MakeButton(ctrlPanel, "Handbrake", "⛔", Vector2.zero);
            AnchorCorner(handbrakeBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 0f), new Vector2(-390f, 80f), new Vector2(170f, 150f));

            var steeringPad = new GameObject("SteeringPad", typeof(RectTransform), typeof(Image));
            steeringPad.transform.SetParent(ctrlPanel.transform, false);
            var padImg = steeringPad.GetComponent<Image>();
            Skin(padImg, "ring", Palette.AccentDim);
            // MobileTouchInput direksiyonu EventSystem'den değil ham
            // Input.GetTouch ile okuyor; pedin ışın hedefi olmasına gerek yok
            // ve açık kalırsa altındaki her şeyi bloke ediyor.
            padImg.raycastTarget = false;
            var padRt = steeringPad.GetComponent<RectTransform>();
            AnchorCorner(padRt, new Vector2(0f, 0f), new Vector2(380f, 230f), new Vector2(700f, 420f));

            var touch = ctrlPanel.AddComponent<DreamCar.InputSystemMobile.MobileTouchInput>();
            touch.throttleButton = throttleBtn;
            touch.brakeButton = brakeBtn;
            touch.handbrakeButton = handbrakeBtn;
            touch.steeringPad = padRt;

            // Nitro bar
            var nitroPanel = MakeUiChild(canvasGo, "NitroPanel");
            var nitroBg = new GameObject("NitroBG", typeof(RectTransform), typeof(Image));
            nitroBg.transform.SetParent(nitroPanel.transform, false);
            var nitroBgRt = nitroBg.GetComponent<RectTransform>();
            // Sağ alta, pedalların üstüne. Eski yeri (-800,-450) hem 21:9'da
            // ekran dışıydı hem direksiyon pedinin TAM İÇİNDEYDİ.
            AnchorCorner(nitroBgRt, new Vector2(1f, 0f),
                         new Vector2(150f, 560f), new Vector2(280f, 24f));
            Skin(nitroBg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);

            var nitroFillGo = new GameObject("NitroFill", typeof(RectTransform), typeof(Image));
            nitroFillGo.transform.SetParent(nitroBg.transform, false);
            var nitroFill = nitroFillGo.GetComponent<Image>();
            Skin(nitroFill, "pill", Palette.Accent);
            nitroFill.type = Image.Type.Filled;
            nitroFill.fillMethod = Image.FillMethod.Horizontal;
            var nitroFillRt = nitroFillGo.GetComponent<RectTransform>();
            nitroFillRt.anchorMin = Vector2.zero; nitroFillRt.anchorMax = Vector2.one;
            nitroFillRt.offsetMin = Vector2.zero; nitroFillRt.offsetMax = Vector2.zero;

            // "NOS" butonu eski yerinde direksiyon pedinin İÇİNDE kalıyordu:
            // MobileTouchInput ham dokunma okuduğu için NOS'a basmak aynı anda
            // bir direksiyon sürüklemesi başlatıyordu. Pedalların yanına alındı.
            var nitroBtn = MakeButton(nitroPanel, "NitroButton", "NOS", Vector2.zero);
            AnchorCorner(nitroBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 0f), new Vector2(150f, 460f), new Vector2(230f, 130f));
            var nitroBar = nitroPanel.AddComponent<NitroBar>();
            nitroBar.fill = nitroFill;
            nitroBar.nitroButton = nitroBtn;

            // Fuel meter
            var fuelPanel = MakeUiChild(canvasGo, "FuelPanel");
            var fuelBg = new GameObject("FuelBG", typeof(RectTransform), typeof(Image));
            fuelBg.transform.SetParent(fuelPanel.transform, false);
            var fuelBgRt = fuelBg.GetComponent<RectTransform>();
            // (−800,−500): 16:9'dan uzun HER telefonda ekranın altında kalıyordu.
            AnchorCorner(fuelBgRt, new Vector2(1f, 0f),
                         new Vector2(150f, 610f), new Vector2(280f, 18f));
            Skin(fuelBg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);

            var fuelFillGo = new GameObject("FuelFill", typeof(RectTransform), typeof(Image));
            fuelFillGo.transform.SetParent(fuelBg.transform, false);
            var fuelFill = fuelFillGo.GetComponent<Image>();
            Skin(fuelFill, "pill", Palette.Good);
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Horizontal;
            var fuelFillRt = fuelFillGo.GetComponent<RectTransform>();
            fuelFillRt.anchorMin = Vector2.zero; fuelFillRt.anchorMax = Vector2.one;
            fuelFillRt.offsetMin = Vector2.zero; fuelFillRt.offsetMax = Vector2.zero;

            var fuelLabel = MakeText(fuelPanel, "FuelPct", "100%", Vector2.zero, 20);
            AnchorCorner(fuelLabel.GetComponent<RectTransform>(),
                         new Vector2(1f, 0f), new Vector2(370f, 610f), new Vector2(140f, 40f));
            var fuelMeter = fuelPanel.AddComponent<FuelMeter>();
            fuelMeter.fill = fuelFill;
            fuelMeter.percentLabel = fuelLabel;

            // Refuel station panel
            // SetActive(false) buradan AŞAĞIYA, bileşen eklenip alanları
            // doldurulduktan SONRAYA alındı. Eskiden panel bileşen eklenmeden
            // kapatılıyordu; sahne kapalı halde kaydedilince RefuelStationPanel.Awake
            // hiç koşmuyor, Instance null kalıyor ve RefuelStation onu hiç
            // bulamıyordu. Paneli açabilecek tek şey panelin kendisiydi —
            // benzin istasyonu paneli oyunda ASLA açılmıyordu.
            var refuelPanel = MakeUiChild(canvasGo, "RefuelStationPanel", modal: true);
            var refuelTitle = MakeText(refuelPanel, "RefuelTitle", "Benzin İstasyonu", new Vector2(0f, 200f), 48, key: "fuel.station");
            var refuelPrice = MakeText(refuelPanel, "RefuelPrice", "-- ₺", new Vector2(0f, 100f), 40);
            var refuelPct = MakeText(refuelPanel, "RefuelPct", "0%", new Vector2(0f, 20f), 32);

            var refuelFillBg = new GameObject("RefuelFillBG", typeof(RectTransform), typeof(Image));
            refuelFillBg.transform.SetParent(refuelPanel.transform, false);
            Skin(refuelFillBg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);
            var rfBgRt = refuelFillBg.GetComponent<RectTransform>();
            rfBgRt.anchoredPosition = new Vector2(0f, -20f);
            rfBgRt.sizeDelta = new Vector2(500f, 30f);

            var refuelFillGo = new GameObject("RefuelFill", typeof(RectTransform), typeof(Image));
            refuelFillGo.transform.SetParent(refuelFillBg.transform, false);
            var refuelFill = refuelFillGo.GetComponent<Image>();
            // Skin ÖNCE gelmeli: sprite'ın 9-slice kenarı olduğu için type'ı
            // Sliced'a çekiyor ve sonradan çağrılırsa Filled'ı ezer — çubuk
            // yakıt seviyesinden bağımsız olarak hep dolu görünürdü.
            Skin(refuelFill, "pill", Palette.Good);
            refuelFill.type = Image.Type.Filled;
            refuelFill.fillMethod = Image.FillMethod.Horizontal;
            var rfFillRt = refuelFillGo.GetComponent<RectTransform>();
            rfFillRt.anchorMin = Vector2.zero; rfFillRt.anchorMax = Vector2.one;
            rfFillRt.offsetMin = Vector2.zero; rfFillRt.offsetMax = Vector2.zero;

            var refuelPay = MakeButton(refuelPanel, "PayButton", "Öde ve Doldur", new Vector2(-120f, -120f), key: "fuel.pay_and_fill");
            var refuelCancel = MakeButton(refuelPanel, "CancelButton", "İptal", new Vector2(120f, -120f), key: "cancel");

            // Bileşen PANELİN ÜZERİNDE DEĞİL, her zaman açık olan Canvas'ta duruyor.
            // Panelin kendisinde olsaydı: panel kapalı kaydedilir → kapalı objede
            // Awake hiç koşmaz → Instance null kalır → RefuelStation onu hiç bulamaz
            // → paneli açacak tek şey panelin kendisi olurdu. Bileşenin zaten ayrı
            // bir "panel" alanı var, tam da bunun için.
            var refuelPanelScript = canvasGo.AddComponent<RefuelStationPanel>();
            refuelPanelScript.panel = refuelPanel;
            refuelPanelScript.fuelFill = refuelFill;
            refuelPanelScript.fuelPercentLabel = refuelPct;
            refuelPanelScript.priceLabel = refuelPrice;
            refuelPanelScript.payButton = refuelPay;
            refuelPanelScript.cancelButton = refuelCancel;
            refuelPanel.SetActive(false);

            // Pause menu
            var pausePanel = MakeUiChild(canvasGo, "PauseMenu", modal: true);
            pausePanel.SetActive(false);
            var pauseTitle = MakeText(pausePanel, "PauseTitle", "Duraklat", new Vector2(0f, 300f), 64, key: "pause");
            // Aralık 110 → 140: buton yüksekliği 80'den 120'ye çıktı, eski
            // aralıkta üst üste binerlerdi.
            var resumeBtn = MakeButton(pausePanel, "Resume", "Devam", new Vector2(0f, 190f), key: "pause.resume");
            var settingsBtn = MakeButton(pausePanel, "Settings", "Ayarlar", new Vector2(0f, 50f), key: "settings");
            var leaveRoomBtn = MakeButton(pausePanel, "LeaveRoom", "Odadan Çık", new Vector2(0f, -90f), key: "pause.leave_room");
            var mainMenuBtn = MakeButton(pausePanel, "MainMenu", "Ana Menü", new Vector2(0f, -230f), key: "pause.main_menu");
            // Ayarlar ekranı Game sahnesinde hiç kurulmuyordu (BuildSecondaryScreens
            // yalnızca ana menüde çağrılıyor), bu yüzden duraklatma menüsündeki
            // "Ayarlar" butonu sessizce hiçbir şey yapmıyordu: oyun içinde ses ve
            // hassasiyet ayarlanamıyordu.
            var gameSettingsScreen = BuildSettingsScreen(canvasGo);

            // Aynı gerekçe: bileşen kapalı panelin üzerindeyken Update() hiç
            // koşmuyor, yani Escape (Android'de geri tuşu) duraklatmayı açmıyordu
            // ve Start() koşmadığı için panel içindeki dört butonun listener'ları
            // ilk açılışa kadar bağlanmıyordu. Canvas her zaman açık.
            var pauseScript = canvasGo.AddComponent<PauseMenu>();
            pauseScript.panel = pausePanel;
            pauseScript.settingsPanel = gameSettingsScreen.panel;
            pauseScript.resumeButton = resumeBtn;
            pauseScript.settingsButton = settingsBtn;
            pauseScript.leaveRoomButton = leaveRoomBtn;
            pauseScript.mainMenuButton = mainMenuBtn;

            // --- Oyuncu listesi + rapor akışı ---
            // PlayerListPanel ve ReportPlayer hiçbir sahnede yoktu:
            // oyuncuyu atmanın, raporlamanın ya da sesini susturmanın hiçbir
            // yolu bulunmuyordu. ReportPlayer.Open(Player), PlayerVoiceMute'un
            // ToggleMute/IsMuted'ı ve BanList akışı hep bu panele bağlıydı.
            // Sohbet ve sesli sohbet olan bir oyunda rapor akışı mağaza
            // incelemesi için de gerekiyor.
            var playersPanel = MakeUiChild(canvasGo, "PlayersScreen", modal: true);
            playersPanel.SetActive(false);
            MakeText(playersPanel, "Title", "Oyuncular", new Vector2(0f, 420f), 64);
            var playersList = MakeListContainer(playersPanel, "List",
                new Vector2(0f, 0f), new Vector2(900f, 620f));
            var playersClose = MakeButton(playersPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(playersClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var reportPanel = MakeUiChild(canvasGo, "ReportScreen", modal: true);
            reportPanel.SetActive(false);
            MakeText(reportPanel, "Title", "Oyuncuyu Bildir", new Vector2(0f, 300f), 56, key: "report");
            var reportReason = MakeDropdown(reportPanel, "ReasonDropdown", new Vector2(0f, 150f));
            var reportDetail = MakeInputField(reportPanel, "DetailField", "Açıklama (isteğe bağlı)", new Vector2(0f, 40f));
            var reportSubmit = MakeButton(reportPanel, "Submit", "Gönder", new Vector2(-180f, -100f));
            var reportCancel = MakeButton(reportPanel, "Cancel", "İptal", new Vector2(180f, -100f), key: "cancel");

            var report = canvasGo.AddComponent<Moderation.ReportPlayer>();
            report.panel = reportPanel;
            report.reasonDropdown = reportReason;
            report.detailField = reportDetail;
            report.submitButton = reportSubmit;
            report.cancelButton = reportCancel;

            // Bileşen Canvas'ta: panel kapalı başlıyor ve kapalı objede
            // OnEnable/Refresh koşmazdı.
            var playerList = canvasGo.AddComponent<PlayerListPanel>();
            playerList.listParent = playersList;
            playerList.entryPrefab = MakePlayerRowTemplate(playersPanel, "PlayerRowTemplate");
            playerList.report = report;

            // PauseMenu'den açılıyor: oyun içinde başka bir giriş noktası yok.
            var playersBtn = MakeButton(pausePanel, "Players", "Oyuncular", new Vector2(0f, -370f));
            UnityEventTools.AddBoolPersistentListener(playersBtn.onClick, playersPanel.SetActive, true);
            UnityEventTools.AddBoolPersistentListener(playersClose.onClick, playersPanel.SetActive, false);

            // PauseMenu yalnızca Escape tuşunu dinliyor. Android'de geri tuşu bunu
            // karşılar ama iOS'ta karşılığı yok — duraklatma menüsü iPhone'da
            // tamamen erişilemezdi. HUD'a bir buton ekliyoruz.
            // Persistent listener şart: onClick.AddListener sahneye serialize edilmez.
            var pauseBtn = MakeButton(hudPanel, "PauseButton", "❚❚", Vector2.zero);
            AnchorCorner(pauseBtn.GetComponent<RectTransform>(),
                         new Vector2(1f, 1f), new Vector2(400f, 70f), new Vector2(120f, 100f));
            UnityEventTools.AddPersistentListener(pauseBtn.onClick, pauseScript.Toggle);
        }

        // ---------------------------------------------------------- Build Settings
        [MenuItem("DreamCar/Setup/Add Scenes To Build Settings")]
        public static void AddScenesToBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
            Debug.Log("[DreamCarSetup] Build Settings updated: MainMenu, Game");
        }

        // ---------------------------------------------------------- Secondary screens
        // Ayarlar / Liderlik / Başarımlar / Coin Mağazası / İstatistik panelleri.
        // Hepsi kapalı başlar; ana menüye açma butonları eklenir.
        // Ayarlar ekranı iki sahnede de gerekiyor: ana menüde nav butonundan,
        // oyun içinde duraklatma menüsünden açılıyor. Tek yerden kurulur.
        static SettingsScreen BuildSettingsScreen(GameObject canvasGo)
        {
            var settingsPanel = MakeUiChild(canvasGo, "SettingsScreen", modal: true);
            settingsPanel.SetActive(false);
            MakeText(settingsPanel, "Title", "Ayarlar", new Vector2(0f, 420f), 64);
            var qualityDd = MakeDropdown(settingsPanel, "QualityDropdown", new Vector2(0f, 300f));
            var fpsDd = MakeDropdown(settingsPanel, "FpsDropdown", new Vector2(0f, 210f));
            var masterSl = MakeSlider(settingsPanel, "MasterSlider", new Vector2(0f, 120f));
            var musicSl = MakeSlider(settingsPanel, "MusicSlider", new Vector2(0f, 50f));
            var sfxSl = MakeSlider(settingsPanel, "SfxSlider", new Vector2(0f, -20f));
            var steerSl = MakeSlider(settingsPanel, "SteeringSlider", new Vector2(0f, -90f));
            var steerVal = MakeText(settingsPanel, "SteeringValue", "1.0x", new Vector2(340f, -90f), 28);
            var langDd = MakeDropdown(settingsPanel, "LanguageDropdown", new Vector2(0f, -170f));

            // Gizlilik politikası ve destek bağlantısı hiçbir sahnede yoktu.
            // PrivacyPolicyScreen ve SupportEmailLink yazılmış ama hiç
            // eklenmiyordu; tr.json'da "settings.privacy" ve "settings.support"
            // anahtarları zaten bunlar için duruyordu. Gizlilik politikasına
            // uygulama içinden ulaşılabilmesi mağaza incelemesinin şartı.
            var privacyBtn = MakeButton(settingsPanel, "PrivacyButton", "Gizlilik",
                                        new Vector2(-160f, -260f), key: "settings.privacy");
            var supportBtn = MakeButton(settingsPanel, "SupportButton", "Destek",
                                        new Vector2(160f, -260f), key: "settings.support");

            var privacyPanel = MakeUiChild(canvasGo, "PrivacyPolicyScreen", modal: true);
            privacyPanel.SetActive(false);
            MakeText(privacyPanel, "Title", "Gizlilik Politikası", new Vector2(0f, 430f), 56,
                     key: "settings.privacy");
            var privacyBody = MakeText(privacyPanel, "Body", "", new Vector2(0f, 20f), 26);
            privacyBody.alignment = TextAlignmentOptions.TopLeft;
            var privacyBodyRt = privacyBody.GetComponent<RectTransform>();
            privacyBodyRt.sizeDelta = new Vector2(900f, 700f);
            var privacyOpen = MakeButton(privacyPanel, "OpenLink", "Web'de Aç", new Vector2(-180f, -400f));
            var privacyClose = MakeButton(privacyPanel, "Close", "Kapat", new Vector2(180f, -400f), key: "close");

            // Bileşenler Canvas'ta: panelleri kendileri kapatıyor ve kapalı bir
            // objede Start() hiç koşmazdı.
            var privacy = canvasGo.AddComponent<AppMeta.PrivacyPolicyScreen>();
            privacy.panel = privacyPanel;
            privacy.bodyLabel = privacyBody;
            // Metin boş kalırsa ekran "politika eklenmemiş" yazıyordu.
            // Assets/Resources/PrivacyPolicy.txt bir ŞABLON: uygulamanın
            // gerçekte ne topladığını listeliyor ama şirket adı, adres,
            // e-posta ve saklama süresi köşeli parantezle boş bırakılmış.
            // Yayından önce doldurulması gerekiyor.
            privacy.fallbackText = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/PrivacyPolicy.txt");
            privacy.openLinkButton = privacyOpen;
            privacy.closeButton = privacyClose;
            UnityEventTools.AddPersistentListener(privacyBtn.onClick, privacy.Show);

            var support = canvasGo.AddComponent<AppMeta.SupportEmailLink>();
            support.supportButton = supportBtn;
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var settingsClose = MakeButton(settingsPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(settingsClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var settings = settingsPanel.AddComponent<SettingsScreen>();
            settings.panel = settingsPanel;
            settings.closeButton = settingsClose;
            settings.qualityDropdown = qualityDd;
            settings.fpsDropdown = fpsDd;
            settings.masterSlider = masterSl;
            settings.musicSlider = musicSl;
            settings.sfxSlider = sfxSl;
            settings.steeringSensitivitySlider = steerSl;
            settings.steeringValueLabel = steerVal;
            settings.languageDropdown = langDd;
            return settings;
        }

        // boot: ~Bootstrap. PlayedWithList ve RateAppPopup oraya ekleniyor ama
        // UI referansları burada kuruluyor.
        static void BuildSecondaryScreens(GameObject canvasGo, GameObject mainPanel, GameObject boot)
        {
            // --- Ayarlar ---
            var settings = BuildSettingsScreen(canvasGo);

            // --- Liderlik ---
            var lbPanel = MakeUiChild(canvasGo, "LeaderboardScreen", modal: true);
            lbPanel.SetActive(false);
            var lbTitle = MakeText(lbPanel, "Title", "En İyi Tur", new Vector2(0f, 420f), 64);
            var lbRaceTab = MakeButton(lbPanel, "RaceTab", "Yarış", new Vector2(-160f, 320f), key: "mode.race");
            var lbDriftTab = MakeButton(lbPanel, "DriftTab", "Drift", new Vector2(160f, 320f), key: "mode.drift");
            var lbList = MakeListContainer(lbPanel, "List", new Vector2(0f, -30f), new Vector2(900f, 620f));
            var lbLoading = MakeText(lbPanel, "Loading", "Yükleniyor…", new Vector2(0f, 0f), 32).gameObject;
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var lbClose = MakeButton(lbPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(lbClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var leaderboard = lbPanel.AddComponent<LeaderboardScreen>();
            leaderboard.panel = lbPanel;
            leaderboard.closeButton = lbClose;
            leaderboard.raceTabButton = lbRaceTab;
            leaderboard.driftTabButton = lbDriftTab;
            leaderboard.listParent = lbList;
            leaderboard.rowPrefab = MakeRowPrefabTemplate(lbPanel, "RowTemplate", 3);
            leaderboard.titleLabel = lbTitle;
            leaderboard.loadingIndicator = lbLoading;

            // --- Başarımlar ---
            var achPanel = MakeUiChild(canvasGo, "AchievementsScreen", modal: true);
            achPanel.SetActive(false);
            MakeText(achPanel, "Title", "Başarımlar", new Vector2(0f, 420f), 64);
            var achSummary = MakeText(achPanel, "Summary", "0 / 0", new Vector2(0f, 340f), 32);
            var achList = MakeListContainer(achPanel, "List", new Vector2(0f, -30f), new Vector2(900f, 620f));
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var achClose = MakeButton(achPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(achClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var achievements = achPanel.AddComponent<AchievementsScreen>();
            achievements.panel = achPanel;
            achievements.closeButton = achClose;
            achievements.listParent = achList;
            achievements.rowPrefab = MakeRowPrefabTemplate(achPanel, "RowTemplate", 3);
            achievements.summaryLabel = achSummary;
            // catalog atanmazsa Refresh() ilk guard'da dönüyor ve ekran hep boş kalıyordu.
            achievements.catalog = Procedural.ProceduralAchievements.Load();

            // --- Coin mağazası ---
            var shopPanel = MakeUiChild(canvasGo, "CoinShopScreen", modal: true);
            shopPanel.SetActive(false);
            MakeText(shopPanel, "Title", "Mağaza", new Vector2(0f, 420f), 64);
            var shopBalance = MakeText(shopPanel, "Balance", "0 ₺", new Vector2(0f, 340f), 40);
            var adBtn = MakeButton(shopPanel, "WatchAdButton", "Reklam İzle", new Vector2(0f, -200f), key: "watch_ad");
            var adReward = MakeText(shopPanel, "AdReward", "+5.000 ₺", new Vector2(0f, -280f), 28);
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var shopClose = MakeButton(shopPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(shopClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var coinShop = shopPanel.AddComponent<CoinShopScreen>();
            coinShop.panel = shopPanel;
            coinShop.closeButton = shopClose;
            coinShop.balanceLabel = shopBalance;
            coinShop.watchAdButton = adBtn;
            coinShop.adRewardLabel = adReward;

            // packs boştu: mağazada satın alınabilecek hiçbir paket görünmüyordu,
            // yalnızca "Reklam İzle" vardı. Ürün kimlikleri IAPManager'ın mağaza
            // tarafında tanımlayacağı kimliklerle birebir aynı olmalı.
            var packDefs = new[]
            {
                ("coins_small",  "50.000 ₺",    "₺29,99",  -60f),
                ("coins_medium", "150.000 ₺",   "₺74,99",   10f),
                ("coins_large",  "500.000 ₺",   "₺199,99",  80f),
            };

            var packs = new System.Collections.Generic.List<CoinShopScreen.CoinPack>();
            foreach (var (productId, label, price, y) in packDefs)
            {
                var btn = MakeButton(shopPanel, "Pack_" + productId, $"{label}   {price}",
                                     new Vector2(0f, y));
                btn.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 110f);
                packs.Add(new CoinShopScreen.CoinPack
                {
                    productId = productId,
                    displayName = label,
                    priceLabel = price,
                    buyButton = btn,
                });
            }
            coinShop.packs = packs.ToArray();

            // --- İstatistik ---
            var statsPanel = MakeUiChild(canvasGo, "StatsScreen", modal: true);
            statsPanel.SetActive(false);
            MakeText(statsPanel, "Title", "İstatistikler", new Vector2(0f, 420f), 64);
            var stats = statsPanel.AddComponent<StatsScreen>();
            stats.panel = statsPanel;
            stats.distanceLabel = MakeStatRow(statsPanel, "Mesafe", 300f);
            stats.driveTimeLabel = MakeStatRow(statsPanel, "Süre", 230f);
            stats.topSpeedLabel = MakeStatRow(statsPanel, "En Yüksek Hız", 160f);
            stats.racesLabel = MakeStatRow(statsPanel, "Yarış", 90f);
            stats.winsLabel = MakeStatRow(statsPanel, "Galibiyet", 20f);
            stats.winRateLabel = MakeStatRow(statsPanel, "Kazanma Oranı", -50f);
            stats.bestDriftLabel = MakeStatRow(statsPanel, "En İyi Drift", -120f);
            stats.moneyEarnedLabel = MakeStatRow(statsPanel, "Kazanılan Para", -190f);
            stats.carsOwnedLabel = MakeStatRow(statsPanel, "Araç", -260f);
            stats.crashesLabel = MakeStatRow(statsPanel, "Çarpışma", -330f);
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            stats.closeButton = MakeButton(statsPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(stats.closeButton.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            // --- Oda kurucu ---
            // Hiçbir sahneye eklenmiyordu: şifreli oda, mod ve harita seçimi yazılmıştı
            // ama oyuncu bunlara hiç ulaşamıyordu, yalnızca LobbyUI'nin isimsiz
            // CreateRoom'u vardı.
            //
            // DİKKAT: RoomCreatorUI.Start() bu alanların HEPSİNİ null kontrolsüz
            // kullanıyor (ClearOptions, minValue, onClick). Biri eksik kalırsa
            // ana menü açılışta NullReferenceException atar.
            var createPanel = MakeUiChild(canvasGo, "RoomCreatorScreen", modal: true);
            createPanel.SetActive(false);
            MakeText(createPanel, "Title", "Oda Kur", new Vector2(0f, 420f), 64);

            var rcName = MakeInputField(createPanel, "NameInput", "Oda adı", new Vector2(0f, 300f));
            var rcPass = MakeInputField(createPanel, "PasswordInput", "Şifre (boş = herkese açık)", new Vector2(0f, 210f));
            var rcMode = MakeDropdown(createPanel, "ModeDropdown", new Vector2(0f, 120f));
            var rcMap = MakeDropdown(createPanel, "MapDropdown", new Vector2(0f, 40f));
            var rcSlider = MakeSlider(createPanel, "MaxPlayersSlider", new Vector2(0f, -50f));
            var rcSliderLabel = MakeText(createPanel, "MaxPlayersLabel", "10 oyuncu", new Vector2(380f, -50f), 28);
            var rcVisible = MakeToggle(createPanel, "VisibleToggle", "Oda listesinde görünsün", new Vector2(0f, -130f), true);
            var rcCreate = MakeButton(createPanel, "CreateButton", "ODAYI KUR", new Vector2(0f, -240f), key: "room.create");
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var rcClose = MakeButton(createPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(rcClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var roomCreator = createPanel.AddComponent<RoomCreatorUI>();
            roomCreator.nameInput = rcName;
            roomCreator.passwordInput = rcPass;
            roomCreator.modeDropdown = rcMode;
            roomCreator.mapDropdown = rcMap;
            roomCreator.maxPlayersSlider = rcSlider;
            roomCreator.maxPlayersLabel = rcSliderLabel;
            roomCreator.visibleToggle = rcVisible;
            roomCreator.createButton = rcCreate;
            roomCreator.mapCatalog = Procedural.Maps.ProceduralMapGenerator.LoadMapCatalog();

            // --- Bölge seçici ---
            // PhotonConnector RegionSelector.SavedRegion'ı okuyor ama o değeri yazacak
            // ekran hiç kurulmuyordu: herkes varsayılan bölgede kalıyordu. Türkiye'den
            // oynayan biri için yanlış bölge doğrudan yüksek ping demek.
            var regionPanel = MakeUiChild(canvasGo, "RegionScreen", modal: true);
            regionPanel.SetActive(false);
            MakeText(regionPanel, "Title", "Sunucu Bölgesi", new Vector2(0f, 420f), 64);
            var regionCurrent = MakeText(regionPanel, "Current", "-", new Vector2(0f, 300f), 32);
            var regionDd = MakeDropdown(regionPanel, "RegionDropdown", new Vector2(0f, 180f));
            var regionApply = MakeButton(regionPanel, "ApplyButton", "Uygula", new Vector2(0f, 40f));
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var regionClose = MakeButton(regionPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(regionClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var region = regionPanel.AddComponent<RegionSelector>();
            region.panel = regionPanel;
            region.dropdown = regionDd;
            region.currentRegionLabel = regionCurrent;
            region.applyButton = regionApply;
            region.closeButton = regionClose;

            // --- Araç mağazası ---
            // ShopUI hiçbir sahneye eklenmiyordu: oyunda araç satın almanın HİÇBİR
            // yolu yoktu. GarageCarousel yalnızca sahip olunan aracı seçiyor, satın
            // alma yapmıyor. Para birikiyor ama harcanacak yer yoktu.
            var carShopPanel = MakeUiChild(canvasGo, "CarShopScreen", modal: true);
            carShopPanel.SetActive(false);
            MakeText(carShopPanel, "Title", "Araçlar", new Vector2(0f, 420f), 64);
            var carShopMoney = MakeText(carShopPanel, "Money", "0 ₺", new Vector2(0f, 340f), 40);
            var carShopList = MakeListContainer(carShopPanel, "List", new Vector2(0f, -30f), new Vector2(900f, 620f));
            // "Kapat" panelin ALT KENARINA sabitlendi. Mutlak y (-320…-430)
            // 19.5:9 telefonda (±489) ve 21:9'da (±454) ekran dışında kalıyordu.
            var carShopClose = MakeButton(carShopPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(carShopClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            var carShop = carShopPanel.AddComponent<ShopUI>();
            carShop.catalog = Procedural.ProceduralCarGenerator.LoadCatalog();
            carShop.listParent = carShopList;
            carShop.moneyLabel = carShopMoney;
            carShop.entryPrefab = MakeCarShopRowTemplate(carShopPanel, "RowTemplate");

            // --- Ana menüdeki açma butonları ---
            // DİKKAT: onClick.AddListener çalışma anında listener ekler ve sahneye
            // SERIALIZE EDİLMEZ. Sahne kaydedilip build'de yüklendiğinde bu beş
            // butonun hiçbiri çalışmazdı — ana menü navigasyonunun tamamı ölüydü.
            // Editörden kurulan bağlantılar kalıcı (persistent) olmak zorunda.
            // Konumlar MainPanel'in ALT KENARINA sabitlendi: mutlak y -300/-400
            // 21:9 telefonda (görünür yarı-aralık ±454) ikinci sırayı ekran
            // dışına taşıyordu.
            var navSettings = MakeIconButton(mainPanel, "NavSettings", "Ayarlar", Vector2.zero, "icon_gear", key: "settings");
            AnchorTo(navSettings.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(-600f, 290f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navSettings.onClick, settings.Open);
            var navLeaderboard = MakeIconButton(mainPanel, "NavLeaderboard", "Liderlik", Vector2.zero, "icon_trophy", key: "leaderboard");
            AnchorTo(navLeaderboard.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(-300f, 290f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navLeaderboard.onClick, leaderboard.Open);
            var navAchievements = MakeIconButton(mainPanel, "NavAchievements", "Başarımlar", Vector2.zero, "icon_flag", key: "achievements");
            AnchorTo(navAchievements.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(0f, 290f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navAchievements.onClick, achievements.Open);
            var navShop = MakeIconButton(mainPanel, "NavShop", "Mağaza", Vector2.zero, "icon_coin", key: "shop");
            AnchorTo(navShop.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(300f, 290f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navShop.onClick, coinShop.Open);
            var navStats = MakeIconButton(mainPanel, "NavStats", "İstatistik", Vector2.zero, "icon_chart", key: "stats.title");
            AnchorTo(navStats.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(600f, 290f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navStats.onClick, stats.Open);

            // İkinci sıra — birinci sıra beş butonla dolu.
            // RoomCreatorUI'de Open/Close yok, panel alanı da yok — paneli doğrudan
            // açıp kapatıyoruz. GameObject.SetActive kalıcı listener hedefi olabiliyor.
            var navCreate = MakeIconButton(mainPanel, "NavCreateRoom", "Oda Kur", Vector2.zero, "icon_plus", key: "room.create");
            AnchorTo(navCreate.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(-150f, 160f), new Vector2(280f, 120f));
            UnityEventTools.AddBoolPersistentListener(navCreate.onClick, createPanel.SetActive, true);
            UnityEventTools.AddBoolPersistentListener(rcClose.onClick, createPanel.SetActive, false);
            var navRegion = MakeIconButton(mainPanel, "NavRegion", "Bölge", Vector2.zero, "icon_globe");
            AnchorTo(navRegion.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(150f, 160f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navRegion.onClick, region.Open);

            // ShopUI'de Open/Close yok — paneli doğrudan açıp kapatıyoruz.
            var navCarShop = MakeIconButton(mainPanel, "NavCarShop", "Araçlar", Vector2.zero, "icon_car", key: "garage");
            AnchorTo(navCarShop.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(450f, 160f), new Vector2(280f, 120f));
            UnityEventTools.AddBoolPersistentListener(navCarShop.onClick, carShopPanel.SetActive, true);
            UnityEventTools.AddBoolPersistentListener(carShopClose.onClick, carShopPanel.SetActive, false);

            // --- Sosyal: beraber oynadıkların + referans kodu ---
            // PlayedWithList her iki sahnede ~Bootstrap'e ekleniyordu ama
            // listParent/entryPrefab null olduğu için Refresh() ilk satırda
            // dönüyordu: veri toplanıyor, hiçbir yerde gösterilmiyordu.
            // ReferralSystem de kod üretiyor ama Redeem() ve ShareReferral()
            // proje genelinde hiç çağrılmıyordu — kod ne girilebiliyor ne
            // paylaşılabiliyordu.
            var socialPanel = MakeUiChild(canvasGo, "SocialScreen", modal: true);
            socialPanel.SetActive(false);
            MakeText(socialPanel, "Title", "Sosyal", new Vector2(0f, 420f), 64);

            var refCodeLabel = MakeText(socialPanel, "MyCode", "-", new Vector2(0f, 330f), 40);
            var refShare = MakeButton(socialPanel, "ShareCode", "Kodu Paylaş", new Vector2(-180f, 240f));
            var refInput = MakeInputField(socialPanel, "RedeemInput", "Referans kodu", new Vector2(-120f, 150f));
            var refRedeem = MakeButton(socialPanel, "RedeemButton", "Kullan", new Vector2(230f, 150f), key: "referral.redeem");

            MakeText(socialPanel, "PlayedWithTitle", "Beraber oynadıkların", new Vector2(0f, 60f), 32);
            var playedList = MakeListContainer(socialPanel, "PlayedWithList",
                new Vector2(0f, -180f), new Vector2(820f, 380f));

            var socialClose = MakeButton(socialPanel, "Close", "Kapat", Vector2.zero, key: "close");
            AnchorTo(socialClose.GetComponent<RectTransform>(),
                     new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(300f, 120f));

            // Bileşen Canvas'ta, panelde DEĞİL: SocialScreen.Start() paneli
            // kapatıyor ve panelin üzerinde olsaydı ilk açılışta Start o anda
            // koşup paneli hemen geri kapatırdı.
            var social = canvasGo.AddComponent<SocialScreen>();
            social.panel = socialPanel;
            social.myCodeLabel = refCodeLabel;
            social.shareButton = refShare;
            social.redeemInput = refInput;
            social.redeemButton = refRedeem;
            social.closeButton = socialClose;

            // PlayedWithList tekili ~Bootstrap'te; UI referanslarını ona veriyoruz.
            var playedWith = boot.GetComponent<PlayedWithList>();
            if (playedWith)
            {
                playedWith.listParent = playedList;
                playedWith.entryPrefab = MakeRowPrefabTemplate(socialPanel, "PlayedWithRow", 1, "Ekle");
            }

            var navSocial = MakeIconButton(mainPanel, "NavSocial", "Sosyal", Vector2.zero, "icon_emote", key: "played_with");
            AnchorTo(navSocial.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                     new Vector2(-450f, 160f), new Vector2(280f, 120f));
            UnityEventTools.AddPersistentListener(navSocial.onClick, social.Open);

            // --- KVKK / GDPR onayı (ilk açılış) ---
            // KVKKConsent hiçbir sahneye eklenmiyordu: onay hiç sorulmuyor,
            // HasConsent kalıcı olarak false kalıyor ve analytics/reklam
            // yüklemesinin dayandığı karar hiç verilmiyordu. Türkiye ve AB
            // için zorunlu, Apple ATT akışı da buna bağlı.
            var consentPanel = MakeUiChild(canvasGo, "ConsentDialog", modal: true);
            consentPanel.SetActive(false);
            MakeText(consentPanel, "Title", "Veri Kullanımı", new Vector2(0f, 300f), 56);
            var consentBody = MakeText(consentPanel, "Body",
                "Hesabını cihazlar arası taşıyabilmek, çok oyunculu oturumu yürütmek ve " +
                "hataları düzeltmek için cihaz kimliği, takma adın ve oyun ilerlemen " +
                "işleniyor. Ayrıntı: Ayarlar → Gizlilik.",
                new Vector2(0f, 100f), 30);
            consentBody.GetComponent<RectTransform>().sizeDelta = new Vector2(880f, 260f);
            var consentAccept = MakeButton(consentPanel, "Accept", "Kabul ediyorum", new Vector2(-200f, -120f));
            var consentReject = MakeButton(consentPanel, "Reject", "Hayır", new Vector2(200f, -120f));

            // Bileşen Canvas'ta: Start() paneli kendisi açıp kapatıyor.
            var consent = canvasGo.AddComponent<Consent.KVKKConsent>();
            consent.dialog = consentPanel;
            consent.acceptButton = consentAccept;
            consent.rejectButton = consentReject;

            // --- Günlük ödül ---
            // DailyReward hiçbir sahneye eklenmiyordu. Onun da ötesinde:
            // LoginStreak.RegisterLoginToday()'in TEK çağıranı bu bileşen, yani
            // giriş serisi de kalıcı olarak 0'da kalıyordu — ve
            // LocalNotificationScheduler alınamayacak bir ödül için her akşam
            // hatırlatma kuruyordu.
            var dailyPanel = MakeUiChild(canvasGo, "DailyRewardPopup", modal: true);
            dailyPanel.SetActive(false);
            MakeText(dailyPanel, "Title", "Günlük Ödül", new Vector2(0f, 220f), 64, key: "daily.title");
            var dailyAmount = MakeText(dailyPanel, "Amount", "+0 ₺", new Vector2(0f, 90f), 56);
            var dailyStreak = MakeText(dailyPanel, "Streak", "1. gün", new Vector2(0f, 10f), 32);
            var dailyClaim = MakeButton(dailyPanel, "ClaimButton", "Al", new Vector2(0f, -120f), key: "daily.claim");

            // Bileşen her zaman açık olan Canvas'ta: paneli kendisi açıp
            // kapatıyor ve kapalı bir objede Start() hiç koşmazdı.
            var daily = canvasGo.AddComponent<Rewards.DailyReward>();
            daily.popup = dailyPanel;
            daily.amountLabel = dailyAmount;
            daily.streakLabel = dailyStreak;
            daily.claimButton = dailyClaim;

            // --- Puan verme popup'ı ---
            // RateAppPopup iki sahnede de ~Bootstrap'e ekleniyordu ve SEKİZ
            // alanının hiçbiri atanmıyordu: her yolu "if (x)" ile korunduğu
            // için tamamen sessiz bir no-op'tu.
            var ratePanel = MakeUiChild(canvasGo, "RateAppPopup", modal: true);
            ratePanel.SetActive(false);
            var ratePrompt = MakeText(ratePanel, "Prompt", "Oyunu beğendin mi?", new Vector2(0f, 160f), 48);
            var rateYes = MakeButton(ratePanel, "Yes", "Evet", new Vector2(-200f, 20f));
            var rateNo = MakeButton(ratePanel, "No", "Pek değil", new Vector2(120f, 20f));
            var rateNever = MakeButton(ratePanel, "Never", "Bir daha sorma", new Vector2(0f, -120f));

            var feedbackPanel = MakeUiChild(canvasGo, "FeedbackPanel", modal: true);
            feedbackPanel.SetActive(false);
            MakeText(feedbackPanel, "Title", "Neyi düzeltelim?", new Vector2(0f, 220f), 48);
            var feedbackField = MakeInputField(feedbackPanel, "FeedbackField", "Görüşün…", new Vector2(0f, 60f));
            var feedbackSend = MakeButton(feedbackPanel, "Send", "Gönder", new Vector2(0f, -80f));

            var rate = boot.GetComponent<AppMeta.RateAppPopup>();
            if (rate)
            {
                rate.popup = ratePanel;
                rate.prompt = ratePrompt;
                rate.yesButton = rateYes;
                rate.noButton = rateNo;
                rate.neverButton = rateNever;
                rate.feedbackPanel = feedbackPanel;
                rate.feedbackField = feedbackField;
                rate.feedbackSendButton = feedbackSend;
            }
        }

        static void BuildLoadingScreen(GameObject canvasGo, GameObject boot)
        {
            // KENDİ Canvas'ı — ana menü Canvas'ının çocuğu DEĞİL.
            // Eskiden panel MainMenu Canvas'ının altındaydı ve LoadingScreen
            // bileşeni ~Bootstrap ile DontDestroyOnLoad ediliyordu: sahne
            // değişince bileşen yaşıyor ama gösterdiği UI yok ediliyordu.
            // Yükleme ekranı tam olarak sahne geçişinde lazım, yani her zaman
            // öldüğü anda. (Bileşen Awake'te bu kökü de DDOL ediyor.)
            var loadingCanvasGo = new GameObject("~LoadingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var loadingCanvas = loadingCanvasGo.GetComponent<Canvas>();
            loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Her şeyin üstünde çizilsin.
            loadingCanvas.sortingOrder = 100;
            var loadingScaler = loadingCanvasGo.GetComponent<CanvasScaler>();
            loadingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            loadingScaler.referenceResolution = new Vector2(1920f, 1080f);
            loadingScaler.matchWidthOrHeight = 0.5f;

            var panel = MakeUiChild(loadingCanvasGo, "LoadingScreen");
            var bg = panel.AddComponent<Image>();
            bg.color = Palette.PanelBg;
            var cg = panel.AddComponent<CanvasGroup>();
            panel.SetActive(false);

            MakeText(panel, "Title", "Yükleniyor", new Vector2(0f, 120f), 64);
            var tip = MakeText(panel, "Tip", "", new Vector2(0f, -180f), 30);
            var pct = MakeText(panel, "Percent", "0%", new Vector2(0f, -40f), 36);

            var barBg = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(panel.transform, false);
            Skin(barBg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchoredPosition = new Vector2(0f, 20f);
            barBgRt.sizeDelta = new Vector2(700f, 26f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            var fill = fillGo.GetComponent<Image>();
            Skin(fill, "pill", Palette.Accent);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

            var loading = boot.AddComponent<LoadingScreen>();
            loading.panel = panel;
            loading.progressFill = fill;
            loading.progressLabel = pct;
            loading.tipLabel = tip;
            loading.canvasGroup = cg;
        }

        static void BuildReconnectOverlay(GameObject canvasGo, GameObject boot)
        {
            var overlay = MakeUiChild(canvasGo, "ReconnectOverlay");
            var bg = overlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            overlay.SetActive(false);
            var status = MakeText(overlay, "Status", "Yeniden bağlanıyor…", Vector2.zero, 40);

            var reconnect = boot.GetComponent<ReconnectionManager>();
            if (reconnect)
            {
                reconnect.reconnectingOverlay = overlay;
                reconnect.statusLabel = status;
            }
        }

        // ------------------------------------------------------ Görünüm (tema)

        // Renkler dosyanın her yerine dağılmıştı ve aynı işi gören yüzeyler
        // farklı renkteydi: (0.15,0.15,0.2,0.9), (1,1,1,0.06), (1,1,1,0.1),
        // (0.25,0.75,1), (0.2,0.7,1)… Bir arayüzü "tasarlanmış" gösteren şeyin
        // büyük kısmı bu tutarlılık.
        static class Palette
        {
            public static readonly Color PanelBg     = new(0.07f, 0.08f, 0.12f, 0.96f);
            public static readonly Color Surface     = new(1f, 1f, 1f, 0.07f);
            public static readonly Color SurfaceDeep = new(0f, 0f, 0f, 0.55f);
            public static readonly Color Stroke      = new(1f, 1f, 1f, 0.16f);
            public static readonly Color ButtonBg    = new(0.16f, 0.18f, 0.26f, 0.95f);
            public static readonly Color Accent      = new(0.24f, 0.72f, 1f, 1f);
            public static readonly Color AccentDim   = new(0.24f, 0.72f, 1f, 0.35f);
            public static readonly Color Good        = new(0.36f, 0.85f, 0.48f, 1f);
            public static readonly Color TextDim     = new(1f, 1f, 1f, 0.45f);
        }

        const string UiSpriteFolder = "Assets/Generated/UI";

        // ProceduralUISprites on beş sprite üretiyor (yuvarlak panel, kapsül,
        // daire, halka, gradyan, chevron, sekiz ikon) ve bu dosya bunların
        // HİÇBİRİNE referans vermiyordu: her Image varsayılan beyaz kareyle
        // doğup düz bir renge boyanıyordu. Yani ekrandaki her buton, panel,
        // sürgü ve kart köşesi keskin, kenarlıksız bir dikdörtgendi.
        static Sprite Ui(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiSpriteFolder}/{name}.png");

        // Sprite yoksa SESSİZCE düz renge düşer. Sahne kurulum menüleri tek tek
        // de çalıştırılabiliyor; sprite'lar üretilmeden çağrılırsa arayüz
        // bozulmamalı, sadece sade kalmalı.
        static Image Skin(Image img, string spriteName, Color tint)
        {
            if (!img) return img;
            img.color = tint;

            var sprite = Ui(spriteName);
            if (!sprite) return img;

            img.sprite = sprite;
            // Kenarlıksız bir sprite'a Sliced vermek Unity'de her karede uyarı
            // bastırır; 9-slice kenarı olan panel/kapsül dilimlenir, daire ve
            // ikonlar olduğu gibi çizilir.
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        // BUILD EVERYTHING zinciri sprite'ları sahnelerden önce üretiyor, ama
        // "Create MainMenu Scene" tek başına da çağrılabiliyor.
        static void EnsureUiSprites()
        {
            if (Directory.Exists(UiSpriteFolder) &&
                Directory.GetFiles(UiSpriteFolder, "*.png").Length > 0) return;
            Procedural.ProceduralUISprites.GenerateAll();
        }

        // ---------------------------------------------------------- Helpers
        static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        // modal=true → panele tam ekran, ışın hedefi açık bir arka plan konur.
        //
        // Bunsuz hiçbir tam ekran panel ALTINDAKİNİ ENGELLEMİYORDU: duraklatma
        // menüsü ya da ayarlar açıkken gaz/fren/el freni butonlarına basılmaya
        // devam edilebiliyordu. Aynı sebeple duraklatma ve ayarlar panelleri
        // üst üste çiziliyor, ayarların dil açılır menüsü tam olarak "Ana Menü"
        // butonunun üzerine denk geliyordu; opak arka plan bunu da çözüyor
        // (ayarlar hiyerarşide sonra geldiği için üstte çizilir).
        static GameObject MakeUiChild(GameObject parent, string name, bool modal = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            if (modal)
            {
                // Arka plan panelin KENDİSİNDE değil ayrı bir çocukta: panelin
                // üzerine Image koymak SafeAreaFitter'ın kırptığı alanı boyar ve
                // çentik bandı boş kalır. Bu çocuk güvenli alanı taşarak tüm
                // ekranı kaplasın diye offsetleri elle geri açıyoruz.
                var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(go.transform, false);
                bg.transform.SetAsFirstSibling();
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = new Vector2(-200f, -200f);
                bgRt.offsetMax = new Vector2(200f, 200f);
                var bgImg = bg.GetComponent<Image>();
                bgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
                bgImg.raycastTarget = true;

                // Başlık bandı. gradient_fade üretiliyordu ve hiç
                // kullanılmıyordu; düz bir arayüzü katmanlı göstermenin en ucuz
                // yolu bu. Yoksa sessizce atlanır.
                var grad = Ui("gradient_fade");
                if (grad)
                {
                    var band = new GameObject("HeaderBand", typeof(RectTransform), typeof(Image));
                    band.transform.SetParent(go.transform, false);
                    band.transform.SetSiblingIndex(1);   // arka planın hemen üstü
                    var bandRt = band.GetComponent<RectTransform>();
                    bandRt.anchorMin = new Vector2(0f, 1f);
                    bandRt.anchorMax = new Vector2(1f, 1f);
                    bandRt.pivot = new Vector2(0.5f, 1f);
                    bandRt.offsetMin = new Vector2(0f, -190f);
                    bandRt.offsetMax = Vector2.zero;
                    var bandImg = band.GetComponent<Image>();
                    bandImg.sprite = grad;
                    bandImg.color = Palette.AccentDim;
                    bandImg.raycastTarget = false;
                }
            }

            // Çentik / Dynamic Island / ev göstergesi kenarları yiyor. Kontroller
            // köşelere sabitlendiği için tam oraya denk geliyorlar; panelleri güvenli
            // alana oturtuyoruz. Doğrudan Canvas'a değil panellere uygulanır ki
            // Canvas'ın kendi ölçekleme davranışı bozulmasın.
            go.AddComponent<SafeAreaFitter>();
            return go;
        }

        // key verilirse etikete LocalizedText takılır.
        //
        // Yerelleştirme katmanı tamamen ölüydü: LocalizationManager.T()'nin
        // proje genelinde SIFIR çağrısı vardı ve LocalizedText hiçbir yere
        // eklenmiyordu, yani SetLanguage'in yenileme döngüsü boş küme geziyordu.
        // tr.json ve en.json doğru yükleniyor, okuyan yoktu: Ayarlar'dan
        // İngilizce seçmek tercihi kaydediyor ve arayüz Türkçe kalıyordu.
        static TMP_Text MakeText(GameObject parent, string name, string content,
                                 Vector2 anchoredPos, int fontSize, string key = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(600f, 100f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            if (!string.IsNullOrEmpty(key))
                go.AddComponent<DreamCar.Localization.LocalizedText>().key = key;
            return t;
        }

        static Button MakeButton(GameObject parent, string name, string label,
                                 Vector2 anchoredPos, string key = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            // 80 referans birim 1080p bir telefonda ~34 dp'ye denk geliyor;
            // Android'in 48 dp ve Apple'ın 44 pt minimumlarının altında. Sürüş
            // kontrolleri daha önce büyütülmüştü, geri kalan 25+ buton
            // varsayılanda kalmıştı.
            rt.sizeDelta = new Vector2(280f, 120f);
            Skin(go.GetComponent<Image>(), "pill", Palette.ButtonBg);
            var t = MakeText(go, "Label", label, Vector2.zero, 36, key);
            var tRt = t.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        // Etiketin soluna ikon koyan buton. Ana menüdeki sekiz navigasyon
        // butonu düz metindi; ikon hem tanınmayı hızlandırıyor hem arayüzü
        // "kart" hissine yaklaştırıyor.
        static Button MakeIconButton(GameObject parent, string name, string label,
                                     Vector2 anchoredPos, string iconName, string key = null)
        {
            var btn = MakeButton(parent, name, label, anchoredPos, key);
            var sprite = Ui(iconName);
            if (!sprite) return btn;   // sprite'lar üretilmemiş: düz butonla kal

            // Etiket boşsa (yalnızca ikon olan HUD butonları) ikon ortalanır;
            // sola dayalı bırakılsaydı 120 birimlik kare butonda kaçık dururdu.
            bool iconOnly = string.IsNullOrEmpty(label);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(btn.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(iconOnly ? 0.5f : 0f, 0.5f);
            iconRt.anchorMax = new Vector2(iconOnly ? 0.5f : 0f, 0.5f);
            iconRt.anchoredPosition = iconOnly ? Vector2.zero : new Vector2(46f, 0f);
            iconRt.sizeDelta = iconOnly ? new Vector2(64f, 64f) : new Vector2(46f, 46f);
            var img = icon.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            // İkon dekoratif: ışın hedefi açık kalırsa butonun kendi tıklamasını
            // yemez ama gereksiz raycast maliyeti çıkarır.
            img.raycastTarget = false;

            // Etiketi ikonun sağına kaydır, yoksa üst üste binerler.
            var labelRt = iconOnly ? null : btn.transform.Find("Label") as RectTransform;
            if (labelRt)
            {
                labelRt.offsetMin = new Vector2(78f, labelRt.offsetMin.y);
                labelRt.offsetMax = new Vector2(-16f, labelRt.offsetMax.y);

                // İkon 78 birim yer aldı: 280 genişlikte etikete ~186 kalıyor
                // ve "Başarımlar" 36 punto ile oraya sığmıyor. Otomatik
                // küçültme, kısa etiketleri küçültmeden uzunları sığdırıyor.
                var label = labelRt.GetComponent<TMP_Text>();
                if (label)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 20f;
                    label.fontSizeMax = 36f;
                    // enableWordWrapping Unity 6'da [Obsolete]; sarma zaten
                    // 120 birim yükseklikte sorun değil, taşma da otomatik
                    // küçültmeden sonra nadiren oluşuyor.
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            return btn;
        }

        // "◀"/"▶" karakterleri yazı tipinde yoksa boş kutu çizilir. Chevron
        // sprite'ı zaten üretiliyordu ve hiç kullanılmıyordu.
        static Button MakeChevronButton(GameObject parent, string name,
                                        Vector2 anchoredPos, bool pointRight)
        {
            var btn = MakeButton(parent, name, "", anchoredPos);
            var sprite = Ui("chevron");
            if (!sprite)
            {
                var fallback = btn.GetComponentInChildren<TMP_Text>();
                if (fallback) fallback.text = pointRight ? ">" : "<";
                return btn;
            }

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(btn.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(48f, 48f);
            // Sprite sağa bakıyor; sola bakan için 180° döndür.
            iconRt.localRotation = Quaternion.Euler(0f, 0f, pointRight ? 0f : 180f);
            var img = icon.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return btn;
        }

        static Toggle MakeToggle(GameObject parent, string name, string label,
                                 Vector2 anchoredPos, bool isOn)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(520f, 70f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(go.transform, false);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0f, 0.5f); boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = new Vector2(35f, 0f);
            boxRt.sizeDelta = new Vector2(46f, 46f);
            Skin(box.GetComponent<Image>(), "panel", Palette.ButtonBg);

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(box.transform, false);
            var checkRt = check.GetComponent<RectTransform>();
            checkRt.anchorMin = Vector2.zero; checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(8f, 8f); checkRt.offsetMax = new Vector2(-8f, -8f);
            Skin(check.GetComponent<Image>(), "circle", Palette.Good);

            var text = MakeText(go, "Label", label, new Vector2(60f, 0f), 28);
            text.alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = isOn;
            return toggle;
        }

        static TMP_Dropdown MakeDropdown(GameObject parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            // 60 birim ≈ 25 dp; açılır menü kapalı hali de dokunma hedefi.
            rt.sizeDelta = new Vector2(480f, 90f);
            Skin(go.GetComponent<Image>(), "panel_outline", Palette.Surface);

            var label = MakeText(go, "Label", "", Vector2.zero, 28);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(14f, 0f); labelRt.offsetMax = new Vector2(-40f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            // Template — Unity Dropdown açılır listesi bunu klonlar.
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            template.SetActive(false);
            var tplRt = template.GetComponent<RectTransform>();
            tplRt.anchorMin = new Vector2(0f, 0f); tplRt.anchorMax = new Vector2(1f, 0f);
            tplRt.pivot = new Vector2(0.5f, 1f);
            tplRt.anchoredPosition = new Vector2(0f, 2f);
            // Satır 80 birim: dört satır sığsın (220 üçü bile göstermiyordu).
            tplRt.sizeDelta = new Vector2(0f, 320f);
            Skin(template.GetComponent<Image>(), "panel", Palette.PanelBg);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(template.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 80f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f); itemRt.anchorMax = new Vector2(1f, 0.5f);
            // Açılır menü satırı 56 → 80 birim (~23 dp → ~34 dp).
            itemRt.sizeDelta = new Vector2(0f, 80f);

            var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            var itemBgRt = itemBg.GetComponent<RectTransform>();
            itemBgRt.anchorMin = Vector2.zero; itemBgRt.anchorMax = Vector2.one;
            itemBgRt.offsetMin = Vector2.zero; itemBgRt.offsetMax = Vector2.zero;
            Skin(itemBg.GetComponent<Image>(), "panel", Palette.Surface);

            var itemLabel = MakeText(item, "Item Label", "Option", Vector2.zero, 26);
            var itemLabelRt = itemLabel.GetComponent<RectTransform>();
            itemLabelRt.anchorMin = Vector2.zero; itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(16f, 0f); itemLabelRt.offsetMax = new Vector2(-16f, 0f);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg.GetComponent<Image>();

            var scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var dd = go.GetComponent<TMP_Dropdown>();
            dd.template = tplRt;
            dd.captionText = label;
            dd.itemText = itemLabel;
            return dd;
        }

        static Slider MakeSlider(GameObject parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(480f, 36f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f); bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            Skin(bg.GetComponent<Image>(), "pill", Palette.SurfaceDeep);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f); faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = Vector2.zero; faRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            Skin(fillGo.GetComponent<Image>(), "pill", Palette.Accent);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = Vector2.zero; haRt.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            // 32x36 ≈ 15 dp — projedeki en küçük dokunma hedefiydi.
            handleRt.sizeDelta = new Vector2(64f, 64f);
            Skin(handle.GetComponent<Image>(), "circle", Color.white);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        // ScrollRect + Content — liste satırlarının ebeveyni Content olur.
        static Transform MakeListContainer(GameObject parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent.transform, false);
            var rt = scrollGo.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Skin(scrollGo.GetComponent<Image>(), "panel", Palette.SurfaceDeep);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            return content.transform;
        }

        // Liste satırı şablonu — sahnede kapalı durur, script Instantiate eder.
        // Toast satırı şablonu — sahnede kapalı durur, ToastNotification Instantiate eder.
        // Bir RectTransform'u ekran köşesine sabitler. corner: (0,0) sol-alt,
        // (1,0) sağ-alt, (0,1) sol-üst, (1,1) sağ-üst. offset o köşeden içeri doğru
        // (x sağa, y yukarı pozitif) — sağ/üst köşelerde işareti otomatik çevrilir.
        // Kenara/köşeye göre hizalar ama offset işaretine dokunmaz. AnchorCorner
        // x işaretini köşeye göre ters çeviriyor; bu, kenar ORTASINA hizalarken
        // (anchor.x = 0.5) yanlış: oradaki negatif x gerçekten "sola" demek.
        static void AnchorTo(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void AnchorCorner(RectTransform rt, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = corner;
            rt.anchorMax = corner;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(
                corner.x > 0.5f ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x),
                corner.y > 0.5f ? -Mathf.Abs(offset.y) : Mathf.Abs(offset.y));
        }


        // Metalik yüzeyler rengini neredeyse tamamen yansımadan alır. Sahnede
        // yansıtacak bir şey yoksa araç boyası parlak değil, koyu ve mat görünür —
        // "araba gibi durmuyor" hissinin başlıca sebebi budur.
        //
        // Gerçek zamanlı prob seçildi çünkü gün/gece döngüsü ve hava durumu
        // gökyüzünü değiştiriyor; fırınlanmış prob o değişimi yakalayamaz.
        // Maliyeti düşürmek için yüzler zamana yayılıyor ve GraphicsTuner düşük
        // kademede probu tamamen kapatıyor.
        static void AddReflectionProbe(GameObject parent, float extent, float height)
        {
            var go = new GameObject("~ReflectionProbe");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            // Altı yüzü tek karede çizmek pahalı; yüz başına bölerek yayıyoruz.
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;          // mobil için yeterli, 256 iki kat bellek
            probe.hdr = true;
            probe.shadowDistance = 0f;       // yansımada gölge gerekmiyor
            probe.cullingMask = ~0;
            probe.size = new Vector3(extent, height * 2f, extent);
            probe.boxProjection = false;     // açık dünya; kutu izdüşümü yanlış olur
            probe.importance = 1;
        }

        // Araç mağazası satırı. ShopUI.Refresh sırayla GetComponentsInChildren<TMP_Text>
        // okuyor: [0] isim, [1] fiyat. Buton etiketi hiyerarşide sonra geldiği için
        // [2] oluyor ve o iki indeksi bozmuyor.
        static GameObject MakeCarShopRowTemplate(GameObject parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent.transform, false);
            row.SetActive(false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 96f);
            Skin(row.GetComponent<Image>(), "panel", Palette.Surface);
            row.GetComponent<LayoutElement>().preferredHeight = 96f;

            // ADI BELLİ bir ikon çocuğu. ShopUI eskiden
            // GetComponentInChildren<Image>() ile satırın KÖK arka planını
            // buluyor ve araç küçük resmini bütün satıra yayıyordu.
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(row.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.02f, 0.12f);
            iconRt.anchorMax = new Vector2(0.13f, 0.88f);
            iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
            var iconImg = icon.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = Palette.TextDim;

            var nameText = MakeText(row, "Text0", "", Vector2.zero, 30);
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.15f, 0f); nameRt.anchorMax = new Vector2(0.45f, 1f);
            nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            var priceText = MakeText(row, "Text1", "", Vector2.zero, 28);
            var priceRt = priceText.GetComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(0.46f, 0f); priceRt.anchorMax = new Vector2(0.70f, 1f);
            priceRt.offsetMin = Vector2.zero; priceRt.offsetMax = Vector2.zero;
            priceText.alignment = TextAlignmentOptions.MidlineLeft;

            var buy = MakeButton(row, "BuyButton", "Satın Al", Vector2.zero);
            var buyRt = buy.GetComponent<RectTransform>();
            buyRt.anchorMin = new Vector2(0.72f, 0.15f); buyRt.anchorMax = new Vector2(0.97f, 0.85f);
            buyRt.offsetMin = Vector2.zero; buyRt.offsetMax = Vector2.zero;

            return row;
        }

        // Oyuncu listesi satırı: ad + üç ADI BELLİ buton. PlayerListPanel
        // butonları adıyla arıyor — GetComponentInChildren<Button>() hep
        // ilkini döndürürdü ve üç işlev tek butona düşerdi.
        static GameObject MakePlayerRowTemplate(GameObject parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent.transform, false);
            row.SetActive(false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 84f);
            Skin(row.GetComponent<Image>(), "panel", Palette.Surface);
            row.GetComponent<LayoutElement>().preferredHeight = 84f;

            var label = MakeText(row, "Name", "", Vector2.zero, 28);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.03f, 0f); labelRt.anchorMax = new Vector2(0.44f, 1f);
            labelRt.offsetMin = Vector2.zero; labelRt.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;

            AddRowButton(row, "MuteButton", "Sustur", 0.46f, 0.62f);
            AddRowButton(row, "ReportButton", "Bildir", 0.64f, 0.80f);
            AddRowButton(row, "KickButton", "At", 0.82f, 0.97f);
            return row;
        }

        static void AddRowButton(GameObject row, string name, string label, float xMin, float xMax)
        {
            var btn = MakeButton(row, name, label, Vector2.zero);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0.15f);
            rt.anchorMax = new Vector2(xMax, 0.85f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = btn.GetComponentInChildren<TMP_Text>();
            if (t) { t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = 26f; }
        }

        static GameObject MakeToastTemplate(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            go.SetActive(false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 72f);
            Skin(go.GetComponent<Image>(), "pill", Palette.PanelBg);
            go.GetComponent<LayoutElement>().preferredHeight = 72f;

            var label = MakeText(go, "Label", "", Vector2.zero, 30);
            var lRt = label.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(20f, 0f); lRt.offsetMax = new Vector2(-20f, 0f);
            label.alignment = TextAlignmentOptions.Midline;
            return go;
        }

        // Oda listesi satırı — kök Button taşımalı: LobbyUI.Refresh
        // GetComponent<Button>() arıyor, GetComponentInChildren değil.
        static GameObject MakeRoomEntryTemplate(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                    typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            go.SetActive(false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 76f);
            Skin(go.GetComponent<Image>(), "panel", Palette.Surface);
            go.GetComponent<LayoutElement>().preferredHeight = 76f;

            var label = MakeText(go, "Label", "", Vector2.zero, 28);
            var lRt = label.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(20f, 0f); lRt.offsetMax = new Vector2(-20f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return go;
        }

        // buttonLabel verilirse satırın sağına bir buton eklenir (PlayedWithList
        // "arkadaş ekle" butonunu GetComponentInChildren<Button>() ile arıyor ve
        // şablonda buton olmadığı için hiç bulamıyordu).
        static GameObject MakeRowPrefabTemplate(GameObject parent, string name, int textCount,
                                                string buttonLabel = null)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent.transform, false);
            row.SetActive(false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 64f);
            Skin(row.GetComponent<Image>(), "panel", Palette.Surface);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;

            // Aynı gerekçe: AchievementsScreen ikonu adıyla bulsun, yoksa
            // GetComponentInChildren<Image>() satırın arka planını döndürüyor
            // ve kilitli başarımlarda BÜTÜN satır griye boyanıyordu.
            var rowIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            rowIcon.transform.SetParent(row.transform, false);
            var rowIconRt = rowIcon.GetComponent<RectTransform>();
            rowIconRt.anchorMin = new Vector2(0.02f, 0.15f);
            rowIconRt.anchorMax = new Vector2(0.11f, 0.85f);
            rowIconRt.offsetMin = Vector2.zero; rowIconRt.offsetMax = Vector2.zero;
            var rowIconImg = rowIcon.GetComponent<Image>();
            rowIconImg.preserveAspect = true;
            rowIconImg.color = Palette.TextDim;

            float[] anchors = { 0.02f, 0.14f, 0.72f };
            float[] widths = { 0.10f, 0.56f, 0.26f };
            for (int i = 0; i < textCount; i++)
            {
                var t = MakeText(row, "Text" + i, "", Vector2.zero, 26);
                var tRt = t.GetComponent<RectTransform>();
                float a = i < anchors.Length ? anchors[i] : 0.02f;
                float w = i < widths.Length ? widths[i] : 0.3f;
                tRt.anchorMin = new Vector2(a, 0f);
                tRt.anchorMax = new Vector2(a + w, 1f);
                tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
                t.alignment = i == textCount - 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            }

            if (!string.IsNullOrEmpty(buttonLabel))
            {
                var btn = MakeButton(row, "RowButton", buttonLabel, Vector2.zero);
                var btnRt = btn.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0.5f);
                btnRt.anchorMax = new Vector2(1f, 0.5f);
                btnRt.anchoredPosition = new Vector2(-90f, 0f);
                btnRt.sizeDelta = new Vector2(150f, 52f);
            }

            return row;
        }

        // "Etiket ............ değer" satırı; değer TMP_Text'i döner.
        static TMP_Text MakeStatRow(GameObject parent, string label, float y)
        {
            var caption = MakeText(parent, label + "Caption", label, new Vector2(-220f, y), 30);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            var value = MakeText(parent, label + "Value", "-", new Vector2(220f, y), 30);
            value.alignment = TextAlignmentOptions.MidlineRight;
            return value;
        }

        static TMP_InputField MakeInputField(GameObject parent, string name, string placeholder, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(500f, 70f);
            Skin(go.GetComponent<Image>(), "panel_outline", Palette.Surface);

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10f, 5f); taRt.offsetMax = new Vector2(-10f, -5f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            var pRt = placeholderGo.GetComponent<RectTransform>();
            pRt.anchorMin = Vector2.zero; pRt.anchorMax = Vector2.one;
            pRt.offsetMin = Vector2.zero; pRt.offsetMax = Vector2.zero;
            var pTxt = placeholderGo.AddComponent<TextMeshProUGUI>();
            pTxt.text = placeholder;
            pTxt.color = Palette.TextDim;
            pTxt.fontSize = 32;
            pTxt.alignment = TextAlignmentOptions.MidlineLeft;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            var tTxt = textGo.AddComponent<TextMeshProUGUI>();
            tTxt.fontSize = 32;
            tTxt.alignment = TextAlignmentOptions.MidlineLeft;

            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = taRt;
            input.textComponent = tTxt;
            input.placeholder = pTxt;
            return input;
        }
    }
}
#endif
