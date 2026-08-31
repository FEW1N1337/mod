#if UNITY_EDITOR
using System.IO;
using UnityEditor;
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

            // Gameplay components
            car.AddComponent<CarNitro>();
            car.AddComponent<CarDamage>();
            car.AddComponent<CarPaint>();
            car.AddComponent<CruiseControl>();
            car.AddComponent<GearBox>();
            car.AddComponent<FuelSystem>();
            car.AddComponent<Emote.HornController>();
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
            boot.AddComponent<LobbyManager>();
            boot.AddComponent<LoginStreak>();
            boot.AddComponent<BanList>();
            boot.AddComponent<PlayerMoney>();
            boot.AddComponent<CarInventory>();
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
            boot.AddComponent<PlayFabAuth>();
            boot.AddComponent<PlayFabMoneySync>();
            boot.AddComponent<PlayFabInventoryBridge>();
            boot.AddComponent<PlayFabLeaderboards>();
            boot.AddComponent<PlayFabAchievements>();
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

            // Main panel
            var mainPanel = MakeUiChild(canvasGo, "MainPanel");
            var titleLabel = MakeText(mainPanel, "TitleLabel", "DreamCar", new Vector2(0f, 400f), 96);
            var nickInput = MakeInputField(mainPanel, "NicknameInput", "Kullanıcı adı", new Vector2(0f, 200f));
            var playBtn = MakeButton(mainPanel, "PlayButton", "OYNA", new Vector2(0f, 40f));
            var statusText = MakeText(mainPanel, "StatusText", "Bağlanıyor…", new Vector2(0f, -100f), 32);

            var mainMenuUI = mainPanel.AddComponent<MainMenuUI>();
            mainMenuUI.nicknameInput = nickInput;
            mainMenuUI.playButton = playBtn;
            mainMenuUI.statusText = statusText;

            // Lobby panel (inactive by default)
            var lobbyPanel = MakeUiChild(canvasGo, "LobbyPanel");
            lobbyPanel.SetActive(false);
            mainMenuUI.lobbyPanel = lobbyPanel;

            var lobbyTitle = MakeText(lobbyPanel, "LobbyTitle", "Odalar", new Vector2(-500f, 400f), 64);
            var quickJoinBtn = MakeButton(lobbyPanel, "QuickJoinButton", "Hızlı Katıl", new Vector2(400f, 400f));
            var createNameInput = MakeInputField(lobbyPanel, "CreateRoomInput", "Yeni oda adı", new Vector2(400f, 300f));
            var createBtn = MakeButton(lobbyPanel, "CreateButton", "OLUŞTUR", new Vector2(400f, 200f));
            var roomListParent = new GameObject("RoomList");
            var rlRt = roomListParent.AddComponent<RectTransform>();
            rlRt.SetParent(lobbyPanel.transform, false);
            rlRt.anchoredPosition = new Vector2(-400f, 0f);
            rlRt.sizeDelta = new Vector2(600f, 700f);

            var lobbyUI = lobbyPanel.AddComponent<LobbyUI>();
            lobbyUI.createRoomInput = createNameInput;
            lobbyUI.createButton = createBtn;
            lobbyUI.quickJoinButton = quickJoinBtn;
            lobbyUI.roomListParent = roomListParent.transform;

            // Toast stack
            var toastRoot = MakeUiChild(canvasGo, "ToastStack");
            var toast = boot.AddComponent<ToastNotification>();
            toast.stackParent = toastRoot.GetComponent<RectTransform>();

            // Ek ekranlar (Ayarlar / Liderlik / Başarımlar / Mağaza / İstatistik)
            BuildSecondaryScreens(canvasGo, mainPanel);

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

            // EventSystem
            new GameObject("EventSystem").AddComponent<EventSystem>().gameObject.AddComponent<StandaloneInputModule>();

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
            boot.AddComponent<CarInventory>();
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
            boot.AddComponent<PlayFabAuth>();
            boot.AddComponent<PlayFabAchievements>();
            boot.AddComponent<RateAppPopup>();

            var roomManager = boot.AddComponent<RoomManager>();
            roomManager.spawnPoints = spawns;

            boot.AddComponent<Weather>();
            boot.AddComponent<DayNightCycle>().sun = sun;
            boot.AddComponent<MapSelector>();

            // Canvas (HUD)
            var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Speed HUD
            var hudPanel = MakeUiChild(canvasGo, "HUDPanel");
            var speedText = MakeText(hudPanel, "SpeedText", "0 km/h", new Vector2(-800f, -400f), 48);
            var playerCountText = MakeText(hudPanel, "PlayerCount", "0/16", new Vector2(800f, 400f), 32);
            var roomNameText = MakeText(hudPanel, "RoomName", "-", new Vector2(-800f, 400f), 32);
            var leaveBtn = MakeButton(hudPanel, "LeaveButton", "Çıkış", new Vector2(800f, -400f));

            var hud = hudPanel.AddComponent<InGameHUD>();
            hud.speedText = speedText;
            hud.playerCountText = playerCountText;
            hud.roomNameText = roomNameText;
            hud.leaveButton = leaveBtn;

            var pingText = MakeText(hudPanel, "PingText", "-- ms", new Vector2(700f, 400f), 24);
            hudPanel.AddComponent<PingIndicator>().label = pingText;

            // Toast stack
            var toastRoot = MakeUiChild(canvasGo, "ToastStack");
            var toast = boot.AddComponent<ToastNotification>();
            toast.stackParent = toastRoot.GetComponent<RectTransform>();

            // Chat
            var chatPanel = MakeUiChild(canvasGo, "ChatPanel");
            var chatInput = MakeInputField(chatPanel, "ChatInput", "Mesaj…", new Vector2(-400f, -400f));
            var chatSend = MakeButton(chatPanel, "ChatSend", "Gönder", new Vector2(0f, -400f));
            var chatMessages = MakeText(chatPanel, "ChatMessages", "", new Vector2(-200f, -200f), 24);
            var chatPv = chatPanel.AddComponent<Photon.Pun.PhotonView>();
            var richChat = chatPanel.AddComponent<RichChatUI>();
            richChat.inputField = chatInput;
            richChat.sendButton = chatSend;
            richChat.messagesText = chatMessages;

            // Controls (mobile touch)
            var ctrlPanel = MakeUiChild(canvasGo, "ControlsPanel");
            var throttleBtn = MakeButton(ctrlPanel, "Throttle", "▲", new Vector2(700f, -300f));
            var brakeBtn = MakeButton(ctrlPanel, "Brake", "▼", new Vector2(700f, -450f));
            var handbrakeBtn = MakeButton(ctrlPanel, "Handbrake", "⛔", new Vector2(550f, -400f));
            var steeringPad = new GameObject("SteeringPad", typeof(RectTransform), typeof(Image));
            steeringPad.transform.SetParent(ctrlPanel.transform, false);
            var padImg = steeringPad.GetComponent<Image>();
            padImg.color = new Color(1f, 1f, 1f, 0.05f);
            var padRt = steeringPad.GetComponent<RectTransform>();
            padRt.anchoredPosition = new Vector2(-500f, -350f);
            padRt.sizeDelta = new Vector2(600f, 400f);

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
            nitroBgRt.anchoredPosition = new Vector2(-800f, -450f);
            nitroBgRt.sizeDelta = new Vector2(280f, 24f);
            nitroBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var nitroFillGo = new GameObject("NitroFill", typeof(RectTransform), typeof(Image));
            nitroFillGo.transform.SetParent(nitroBg.transform, false);
            var nitroFill = nitroFillGo.GetComponent<Image>();
            nitroFill.color = new Color(0.2f, 0.7f, 1f);
            nitroFill.type = Image.Type.Filled;
            nitroFill.fillMethod = Image.FillMethod.Horizontal;
            var nitroFillRt = nitroFillGo.GetComponent<RectTransform>();
            nitroFillRt.anchorMin = Vector2.zero; nitroFillRt.anchorMax = Vector2.one;
            nitroFillRt.offsetMin = Vector2.zero; nitroFillRt.offsetMax = Vector2.zero;

            var nitroBtn = MakeButton(nitroPanel, "NitroButton", "NOS", new Vector2(-800f, -380f));
            var nitroBar = nitroPanel.AddComponent<NitroBar>();
            nitroBar.fill = nitroFill;
            nitroBar.nitroButton = nitroBtn;

            // Fuel meter
            var fuelPanel = MakeUiChild(canvasGo, "FuelPanel");
            var fuelBg = new GameObject("FuelBG", typeof(RectTransform), typeof(Image));
            fuelBg.transform.SetParent(fuelPanel.transform, false);
            var fuelBgRt = fuelBg.GetComponent<RectTransform>();
            fuelBgRt.anchoredPosition = new Vector2(-800f, -500f);
            fuelBgRt.sizeDelta = new Vector2(280f, 18f);
            fuelBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var fuelFillGo = new GameObject("FuelFill", typeof(RectTransform), typeof(Image));
            fuelFillGo.transform.SetParent(fuelBg.transform, false);
            var fuelFill = fuelFillGo.GetComponent<Image>();
            fuelFill.type = Image.Type.Filled;
            fuelFill.fillMethod = Image.FillMethod.Horizontal;
            var fuelFillRt = fuelFillGo.GetComponent<RectTransform>();
            fuelFillRt.anchorMin = Vector2.zero; fuelFillRt.anchorMax = Vector2.one;
            fuelFillRt.offsetMin = Vector2.zero; fuelFillRt.offsetMax = Vector2.zero;

            var fuelLabel = MakeText(fuelPanel, "FuelPct", "100%", new Vector2(-620f, -500f), 20);
            var fuelMeter = fuelPanel.AddComponent<FuelMeter>();
            fuelMeter.fill = fuelFill;
            fuelMeter.percentLabel = fuelLabel;

            // Refuel station panel
            var refuelPanel = MakeUiChild(canvasGo, "RefuelStationPanel");
            refuelPanel.SetActive(false);
            var refuelTitle = MakeText(refuelPanel, "RefuelTitle", "Benzin İstasyonu", new Vector2(0f, 200f), 48);
            var refuelPrice = MakeText(refuelPanel, "RefuelPrice", "-- ₺", new Vector2(0f, 100f), 40);
            var refuelPct = MakeText(refuelPanel, "RefuelPct", "0%", new Vector2(0f, 20f), 32);

            var refuelFillBg = new GameObject("RefuelFillBG", typeof(RectTransform), typeof(Image));
            refuelFillBg.transform.SetParent(refuelPanel.transform, false);
            refuelFillBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var rfBgRt = refuelFillBg.GetComponent<RectTransform>();
            rfBgRt.anchoredPosition = new Vector2(0f, -20f);
            rfBgRt.sizeDelta = new Vector2(500f, 30f);

            var refuelFillGo = new GameObject("RefuelFill", typeof(RectTransform), typeof(Image));
            refuelFillGo.transform.SetParent(refuelFillBg.transform, false);
            var refuelFill = refuelFillGo.GetComponent<Image>();
            refuelFill.type = Image.Type.Filled;
            refuelFill.fillMethod = Image.FillMethod.Horizontal;
            refuelFill.color = new Color(0.4f, 0.9f, 0.4f);
            var rfFillRt = refuelFillGo.GetComponent<RectTransform>();
            rfFillRt.anchorMin = Vector2.zero; rfFillRt.anchorMax = Vector2.one;
            rfFillRt.offsetMin = Vector2.zero; rfFillRt.offsetMax = Vector2.zero;

            var refuelPay = MakeButton(refuelPanel, "PayButton", "Öde ve Doldur", new Vector2(-120f, -120f));
            var refuelCancel = MakeButton(refuelPanel, "CancelButton", "İptal", new Vector2(120f, -120f));

            var refuelPanelScript = refuelPanel.AddComponent<RefuelStationPanel>();
            refuelPanelScript.panel = refuelPanel;
            refuelPanelScript.fuelFill = refuelFill;
            refuelPanelScript.fuelPercentLabel = refuelPct;
            refuelPanelScript.priceLabel = refuelPrice;
            refuelPanelScript.payButton = refuelPay;
            refuelPanelScript.cancelButton = refuelCancel;

            // Pause menu
            var pausePanel = MakeUiChild(canvasGo, "PauseMenu");
            pausePanel.SetActive(false);
            var pauseTitle = MakeText(pausePanel, "PauseTitle", "Duraklat", new Vector2(0f, 300f), 64);
            var resumeBtn = MakeButton(pausePanel, "Resume", "Devam", new Vector2(0f, 150f));
            var settingsBtn = MakeButton(pausePanel, "Settings", "Ayarlar", new Vector2(0f, 40f));
            var leaveRoomBtn = MakeButton(pausePanel, "LeaveRoom", "Odadan Çık", new Vector2(0f, -70f));
            var mainMenuBtn = MakeButton(pausePanel, "MainMenu", "Ana Menü", new Vector2(0f, -180f));
            var pauseScript = pausePanel.AddComponent<PauseMenu>();
            pauseScript.panel = pausePanel;
            pauseScript.resumeButton = resumeBtn;
            pauseScript.settingsButton = settingsBtn;
            pauseScript.leaveRoomButton = leaveRoomBtn;
            pauseScript.mainMenuButton = mainMenuBtn;

            EditorSceneManager.SaveScene(scene, GamePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DreamCarSetup] Game scene created at " + GamePath);
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
        static void BuildSecondaryScreens(GameObject canvasGo, GameObject mainPanel)
        {
            // --- Ayarlar ---
            var settingsPanel = MakeUiChild(canvasGo, "SettingsScreen");
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
            var settingsClose = MakeButton(settingsPanel, "Close", "Kapat", new Vector2(0f, -320f));

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

            // --- Liderlik ---
            var lbPanel = MakeUiChild(canvasGo, "LeaderboardScreen");
            lbPanel.SetActive(false);
            var lbTitle = MakeText(lbPanel, "Title", "En İyi Tur", new Vector2(0f, 420f), 64);
            var lbRaceTab = MakeButton(lbPanel, "RaceTab", "Yarış", new Vector2(-160f, 320f));
            var lbDriftTab = MakeButton(lbPanel, "DriftTab", "Drift", new Vector2(160f, 320f));
            var lbList = MakeListContainer(lbPanel, "List", new Vector2(0f, -30f), new Vector2(900f, 620f));
            var lbLoading = MakeText(lbPanel, "Loading", "Yükleniyor…", new Vector2(0f, 0f), 32).gameObject;
            var lbClose = MakeButton(lbPanel, "Close", "Kapat", new Vector2(0f, -420f));

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
            var achPanel = MakeUiChild(canvasGo, "AchievementsScreen");
            achPanel.SetActive(false);
            MakeText(achPanel, "Title", "Başarımlar", new Vector2(0f, 420f), 64);
            var achSummary = MakeText(achPanel, "Summary", "0 / 0", new Vector2(0f, 340f), 32);
            var achList = MakeListContainer(achPanel, "List", new Vector2(0f, -30f), new Vector2(900f, 620f));
            var achClose = MakeButton(achPanel, "Close", "Kapat", new Vector2(0f, -420f));

            var achievements = achPanel.AddComponent<AchievementsScreen>();
            achievements.panel = achPanel;
            achievements.closeButton = achClose;
            achievements.listParent = achList;
            achievements.rowPrefab = MakeRowPrefabTemplate(achPanel, "RowTemplate", 3);
            achievements.summaryLabel = achSummary;

            // --- Coin mağazası ---
            var shopPanel = MakeUiChild(canvasGo, "CoinShopScreen");
            shopPanel.SetActive(false);
            MakeText(shopPanel, "Title", "Mağaza", new Vector2(0f, 420f), 64);
            var shopBalance = MakeText(shopPanel, "Balance", "0 ₺", new Vector2(0f, 340f), 40);
            var adBtn = MakeButton(shopPanel, "WatchAdButton", "Reklam İzle", new Vector2(0f, -200f));
            var adReward = MakeText(shopPanel, "AdReward", "+5.000 ₺", new Vector2(0f, -280f), 28);
            var shopClose = MakeButton(shopPanel, "Close", "Kapat", new Vector2(0f, -420f));

            var coinShop = shopPanel.AddComponent<CoinShopScreen>();
            coinShop.panel = shopPanel;
            coinShop.closeButton = shopClose;
            coinShop.balanceLabel = shopBalance;
            coinShop.watchAdButton = adBtn;
            coinShop.adRewardLabel = adReward;

            // --- İstatistik ---
            var statsPanel = MakeUiChild(canvasGo, "StatsScreen");
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
            stats.closeButton = MakeButton(statsPanel, "Close", "Kapat", new Vector2(0f, -430f));

            // --- Ana menüdeki açma butonları ---
            var navSettings = MakeButton(mainPanel, "NavSettings", "Ayarlar", new Vector2(-600f, -300f));
            navSettings.onClick.AddListener(settings.Open);
            var navLeaderboard = MakeButton(mainPanel, "NavLeaderboard", "Liderlik", new Vector2(-300f, -300f));
            navLeaderboard.onClick.AddListener(leaderboard.Open);
            var navAchievements = MakeButton(mainPanel, "NavAchievements", "Başarımlar", new Vector2(0f, -300f));
            navAchievements.onClick.AddListener(achievements.Open);
            var navShop = MakeButton(mainPanel, "NavShop", "Mağaza", new Vector2(300f, -300f));
            navShop.onClick.AddListener(coinShop.Open);
            var navStats = MakeButton(mainPanel, "NavStats", "İstatistik", new Vector2(600f, -300f));
            navStats.onClick.AddListener(stats.Open);
        }

        static void BuildLoadingScreen(GameObject canvasGo, GameObject boot)
        {
            var panel = MakeUiChild(canvasGo, "LoadingScreen");
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.07f, 1f);
            var cg = panel.AddComponent<CanvasGroup>();
            panel.SetActive(false);

            MakeText(panel, "Title", "Yükleniyor", new Vector2(0f, 120f), 64);
            var tip = MakeText(panel, "Tip", "", new Vector2(0f, -180f), 30);
            var pct = MakeText(panel, "Percent", "0%", new Vector2(0f, -40f), 36);

            var barBg = new GameObject("BarBG", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(panel.transform, false);
            barBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchoredPosition = new Vector2(0f, 20f);
            barBgRt.sizeDelta = new Vector2(700f, 26f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.25f, 0.75f, 1f);
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

        // ---------------------------------------------------------- Helpers
        static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        static GameObject MakeUiChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        static TMP_Text MakeText(GameObject parent, string name, string content, Vector2 anchoredPos, int fontSize)
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
            return t;
        }

        static Button MakeButton(GameObject parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(280f, 80f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var t = MakeText(go, "Label", label, Vector2.zero, 36);
            var tRt = t.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        static TMP_Dropdown MakeDropdown(GameObject parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(480f, 60f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

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
            tplRt.sizeDelta = new Vector2(0f, 220f);
            template.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

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
            contentRt.sizeDelta = new Vector2(0f, 56f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f); itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, 56f);

            var itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBg.transform.SetParent(item.transform, false);
            var itemBgRt = itemBg.GetComponent<RectTransform>();
            itemBgRt.anchorMin = Vector2.zero; itemBgRt.anchorMax = Vector2.one;
            itemBgRt.offsetMin = Vector2.zero; itemBgRt.offsetMax = Vector2.zero;
            itemBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

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
            bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

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
            fillGo.GetComponent<Image>().color = new Color(0.25f, 0.75f, 1f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = Vector2.zero; haRt.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(32f, 36f);
            handle.GetComponent<Image>().color = Color.white;

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
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

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
        static GameObject MakeRowPrefabTemplate(GameObject parent, string name, int textCount)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent.transform, false);
            row.SetActive(false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 64f);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;

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
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

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
            pTxt.color = new Color(1f, 1f, 1f, 0.4f);
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
