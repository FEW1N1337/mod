# DreamCar (Unity Araba Oyunu — MVP İskelet)

Dream Road Online tarzı online çok oyunculu araba oyunu için Unity 6 iskeleti. Photon PUN 2 + WheelCollider + ücretsiz asset yaklaşımı.

Bu klasör commit edilmiş iskelettir — Unity Editor'de açıp aşağıdaki adımları izleyerek çalışır bir prototipe getireceksin. Sonra iOS için `.ipa` build alıp cihaza yükleyebilirsin (App Store, TestFlight, AltStore, veya jailbreak).

> **iOS için Mac + Xcode gerekli.** Unity `.ipa` doğrudan üretmez — Unity Xcode projesi çıkarır, Xcode `.ipa`'ya derler. Windows/Linux'ta iOS build alamazsın.

---

## 1) Aç

1. **Unity Hub** kur (unity.com/download).
2. **Unity 6 LTS** (6000.0.30f1 veya üzeri) yükle. Modülleri: **iOS Build Support** (Mac'te zorunlu). Windows/Linux'ta yalnızca geliştirme yapabilirsin — iOS `.ipa` build için Mac + Xcode şart.
3. Hub → **Add project from disk** → bu `UnityGame/` klasörünü seç.
4. İlk açılışta Unity paketleri indirir (birkaç dakika).

---

## 2) Photon PUN 2'yi kur

1. Unity Editor → **Window → Package Manager → My Assets** (Asset Store'dan `PUN 2 - FREE` ücretsiz import edilmeli — [buradan](https://assetstore.unity.com/packages/tools/network/pun-2-free-119922) hesabınla "Add to My Assets" de).
2. Package Manager'da PUN 2'yi seç → **Import**.
3. Import bitince PUN Wizard açılır: [id.photonengine.com](https://id.photonengine.com) → ücretsiz hesap → **Create a new app** → *Photon PUN* seç → **AppId**'yi kopyala.
4. Wizard'a yapıştır **veya** manuel: `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` → **App Id PUN** alanına yapıştır.
5. Region: EU/US/Asia'dan uygunu (İstanbul için "eu").

Ücretsiz tier: 20 CCU (aynı anda 20 oyuncu). Sonra ücretli.

---

## 3) Ücretsiz asset'leri import et

Zorunlu: bir araba modeli + bir harita/zemin.

**Araba modeli (birini seç, ücretsiz):**
- Asset Store — "ARCADE: FREE Racing Car" (Mena)
- Asset Store — "Simple Free Car" (SICS Games)
- Sketchfab CC-BY modelleri (.fbx indir, Assets/'a sürükle)

**Harita (birini seç, ücretsiz):**
- Asset Store — "Low Poly Racing Track FREE"
- Asset Store — "3D FREE Modular Kit"
- Basit `Plane` (10.000 unit) + Cube duvarlar (prototip için yeterli)

**UI:** TextMeshPro (Unity dahili — ilk açtığında "Import TMP Essentials" popup'ında Import de).

---

## 4) Sahneleri ve prefab'ı kur

### 4a) Car prefab (ZORUNLU — network spawn için)

1. Araba modelini sahneye sürükle.
2. GameObject'e **Rigidbody** ekle (Mass ~1200).
3. Arabanın altına 4 boş child GameObject ekle: `FL`, `FR`, `RL`, `RR`. Her birine **WheelCollider** ekle, tekerlek meshlerinin konumuna hizala.
4. Ana GameObject'e ekle:
   - `CarController` (Axles: 2 tane — Front (steering=true), Rear (motor=true). Her aksa left/right wheel collider ve mesh referansları)
   - `PhotonView` (Observed Components: aşağıdaki `CarNetworkSync` script'i sürükle; Observe Option: Unreliable On Change)
   - `CarNetworkSync`
5. GameObject'i **`Assets/Resources/` klasörüne** sürükle ve adını **`Car`** koy. (Photon PhotonNetwork.Instantiate ismi `Resources/` altından arar.)
6. Sahnedeki instance'ı sil.

### 4b) MainMenu.unity

1. File → New Scene → Basic (Built-in) → Save As `Assets/Scenes/MainMenu.unity`.
2. Boş GameObject: `~Bootstrap` → `GameBootstrap` script'i ekle.
3. **Canvas** (UI → Canvas): Panel + TMP_InputField (nickname) + Button ("Play") + TMP_Text (status).
4. Canvas altında ikinci Panel: `LobbyPanel` (başlangıçta inactive) → TMP_InputField (room name) + Create Button + Quick Join Button + Scroll View (Content parent = roomListParent) + `RoomEntry` prefab (Button + TMP_Text child).
5. Bir GameObject'e `MainMenuUI` script'i, alanları bağla. Aynı Canvas'a `LobbyUI` script'i, alanları bağla.

### 4c) Game.unity

1. New Scene → Save As `Assets/Scenes/Game.unity`.
2. Zemin: Plane (Scale 100x1x100) + araba modelinin haritası.
3. Boş GameObject: `~Bootstrap` → `GameBootstrap` script.
4. Boş GameObject: `~RoomManager` → `RoomManager` script. Boş `SpawnPoint` child'ları oluştur, `Spawn Points` array'ine koy.
5. Main Camera → `CarCameraFollow` script (target boş kalır, RoomManager runtime'da bağlar).
6. Canvas (Screen Space Overlay):
   - `HUD` panel: TMP_Text (speed), TMP_Text (playerCount), TMP_Text (roomName), Button (leave) → `InGameHUD` script.
   - `ChatPanel`: TMP_InputField + Send Button + TMP_Text (messages) → `ChatUI` script. **ChatUI GameObject'e PhotonView ekle**, Observed Components boş bırak (sadece RPC için).
   - `ControlsPanel`: Throttle/Brake/Handbrake butonları + boş `SteeringPad` RectTransform (ekranın sol yarısı) → `MobileTouchInput` script, alanları bağla.

### 4d) Build Settings (iOS)

1. File → Build Settings → Add Open Scenes (sırasıyla: MainMenu, Game).
2. Platform: **iOS** → **Switch Platform**.
3. Player Settings:
   - Company: `few1n`
   - Product Name: `DreamCar`
   - Bundle Identifier: `com.few1n.dreamcarclone` (Xcode'da signing için birebir bu olacak)
   - Target minimum iOS Version: **13.0** (Unity 6 önerisi)
   - Target Device: **iPhone Only** (istersen Universal)
   - Architecture: **ARM64** (App Store zorunlu, Unity iOS'ta zaten default)
   - Scripting Backend: **IL2CPP** (iOS zorunlu, değiştirilemez)
   - Camera Usage Description / Microphone Usage Description: boş bırak (voice chat eklersen "Sesli sohbet için mikrofon" yaz)
   - Requires Persistent WiFi: **On** (multiplayer için)
4. **iOS için özel: Assets/Plugins/iOS/** klasörü otomatik oluşur (Unity native plugin'leri buraya koyar).

---

## 5) Test

**Editor'de tek başına:**
1. MainMenu sahnesini aç → Play.
2. Nickname gir → status "Online" olunca "Play" bas → oda oluştur.
3. Game sahnesine geçince araba spawn olmalı, WSAD/ok tuşları ile sürebilmelisin.

**İki instance ile multiplayer testi (iOS build almadan):**
1. Mac Player Support kuruluysa File → Build & Run → macOS Standalone. Yoksa Windows'ta PC Standalone.
2. Build'i aç + Editor'de aynı anda Play → aynı oda ismine katıl → birbirinizi görmelisin.

**iOS cihazda (üç seçenek — hangisini kullanacağın Apple Developer üyeliğine bağlı):**

**A) Ücretsiz Apple ID + Xcode (7 gün geçerli, sideload)**
1. Mac → Xcode kur → Preferences → Accounts → kendi Apple ID'nle giriş.
2. Unity Editor → File → Build → çıktıyı `Builds/iOS/` klasörüne yaz (Unity Xcode projesi üretir, `.ipa` değil).
3. Xcode'da `Unity-iPhone.xcodeproj` aç.
4. Signing & Capabilities → Team: kendi Apple ID'n → Bundle ID'yi benzersiz yap (`com.<senin adın>.dreamcar` gibi — Apple aynı bundle ID'yi ücretsiz hesapla paylaşmıyor).
5. iPhone'u USB ile Mac'e bağla → Xcode'da hedef cihaz olarak seç → Play (▶) tuşu.
6. iPhone'da Ayarlar → VPN & Cihaz Yönetimi → geliştirici sertifikanı **güven**.
7. 7 gün sonra yeniden derleyip yüklemen gerekir.

**B) TestFlight (Apple Developer $99/yıl)**
1. developer.apple.com'da hesap aç.
2. App Store Connect'te uygulama kaydı oluştur.
3. Xcode → Product → Archive → Distribute App → App Store Connect → Upload.
4. TestFlight sekmesinde build'i test kullanıcılarına aç.
5. Play Store'a değil **App Store**'a yayına çıkarma da aynı archive'den → "Prepare for Submission".

**C) Jailbreak / sideload (Apple Developer'sız `.ipa`)**
> Bu repo zaten `template/Tweak.xm` içinde jailbreak tweak barındırıyor — cihaz muhtemelen jailbroken.
1. Xcode'da Product → Archive → Distribute App → **Development** → **Export → Development-signed** yerine **Ad Hoc** veya **Enterprise** seç (uygun sertifikan varsa). Yoksa Xcode'un `.app` çıktısını `Payload/` klasörüne koy → zip'le → `.ipa` uzantısı ver (unsigned).
2. Jailbroken cihazda: AppSync Unified (Cydia/Sileo) → **Filza**/Sileo ile `.ipa`'yı yükle. Ya da Mac'te `ideviceinstaller -i DreamCar.ipa`.
3. Alternatif (jailbreak yok): **AltStore** veya **Sideloadly** (ücretsiz Apple ID ile 7 gün resign).

**Uyarı**: Multiplayer test için Photon sunucusu → Photon Dashboard'daki region'ın (örn. "eu") oyuncularınkiyle aynı olması gerekli. Region'ı `PhotonConnector.preferredRegion = "eu"` ile sabitleyebilirsin.

---

## 6) Dream Road tarzı grafik / his

**Yasal uyarı**: Dream Road Online'ın modellerini, texture'larını, logolarını, marka adlarını, haritasını, UI grafiklerini, seslerini kopyalamak IP ihlali. Aynı *tarz*a yaklaşmak için aşağıdaki plan.

### 6a) Render pipeline — URP + post-processing
`manifest.json`'da `com.unity.render-pipelines.universal` zaten var. Editor'de:
1. Assets → Create → Rendering → URP Asset (with Universal Renderer).
2. Edit → Project Settings → Graphics → Scriptable Render Pipeline Settings = az önce oluşturduğun URP asset.
3. Ana kameraya **Camera** component'inde: Rendering → Post Processing = **On**.
4. Sahneye Volume (Global) → Profile → Add Override: **Bloom** (Intensity 1.2, Threshold 0.9), **Color Adjustments** (Contrast +10, Saturation +15), **Vignette** (Intensity 0.25), **Motion Blur** (Intensity 0.3), **Tonemapping** (ACES).
5. Balata kızarması, farlar, nitro alevi HDR emissive kullanır — URP + Bloom şart.

### 6b) Bu iskelette EKLİ olan Dream Road-benzeri sistemler
| Script | Ne yapar (Dream Road eşdeğeri) |
|---|---|
| `Effects/WheelGlow.cs` | Fren/kayma ile balata kızarır. Dream Road'daki `RCCP_WheelGlow` mantığı (temperature → gradient emissive). |
| `Effects/CarNitro.cs` | Nitro (0-100), boost force, egzoz alevi, top speed bonusu. Dream Road'daki `CarNitro` eşdeğeri. |
| `Effects/DriftSmoke.cs` | Kayma anında lastik dumanı + skid trail. |
| `Effects/HeadlightController.cs` | Gece otomatik far + tail emissive. |
| `Audio/EngineAudio.cs` | Idle + rev loop, RPM'e göre pitch/volume. |
| `Audio/TireScreechAudio.cs` | Kayma sesi. |
| `Environment/DayNightCycle.cs` | Güneş rotasyonu, ambient gradient. |
| `Traffic/TrafficCar.cs` | Waypoint takipli trafik AI (Dream Road'daki trafik gibi). |
| `Customization/CarPaint.cs` | Renk + metallic + smoothness, Photon custom properties ile diğer oyunculara yansır. |
| `UI/SpeedometerNeedle.cs` | Analog kilometre iğnesi. |
| `UI/NitroBar.cs` | Nitro barı + basılı tut butonu. |

### 6c) Görsel yakınlık için ihtiyacın olan ücretli/ücretsiz asset'ler

**Fizik (çok önemli — WheelCollider ≠ Dream Road hissi):**
- **Realistic Car Controller Pro (RCCP)** — Asset Store, ~$150. Dream Road birebir bunu kullanıyor. Aldığında `CarController.cs`'i çıkar, RCCP'nin `RCC_CarControllerV3`'ünü kullan. `WheelGlow.cs`, `CarNitro.cs`, `EngineAudio.cs` zaten uyumlu API'yle yazıldı.
- Ücretsiz alternatif: RCCP değil ama iyi — **NWH Vehicle Physics 2** (ücretsiz sürümü mevcut).

**Araba modelleri (ücretsiz / uygun fiyatlı, marka-özgür):**
- Asset Store — "Vehicle Pack" (Nolan)
- Asset Store — "Cars Pack Pro" (~$30)
- Sketchfab (CC-BY): "generic sport coupe", "compact hatchback" arayabilirsin — marka logolu olanları kullanma.

**Harita — şehir/otoyol (Dream Road İstanbul benzeri):**
- Asset Store — "City Scene 01/02" (Vertex Studio) — Türk/Avrupa cadde tarzı
- Asset Store — "Urban City Pack" (~$50)
- Ücretsiz: "Modular Roads" + kendi düzenin
- **Yol yapmak için**: EasyRoads3D Free (Asset Store, ücretsiz) — spline çizerek yol oluşturursun.

**Skybox:**
- Ücretsiz: "AllSky Free - 10 Sky / Skybox Set"

**UI font/ikon:**
- Google Fonts (SIL Open Font License) — "Inter", "Rubik", "Bebas Neue" — Türkçe karakter destekler, ticari kullanım serbest.
- Font Awesome Free — nitro/gaz/fren ikonları.

**Ses:**
- Freesound.org (CC0 filtresi) — motor sesi, lastik screech, korna
- Asset Store — "Free Sound Effects Pack" (ATMOS Sound Design)

### 6d) Görsel iyileştirme sırası (önerdiğim rota)
1. **URP + post-processing** (Adım 6a) — 2 saat, en büyük görsel sıçrama.
2. **RCCP satın al** ($150) — sürüş hissi Dream Road'a %90 yaklaşır.
3. **Şehir haritası asset'i** ($30-50) — placeholder plane atılır.
4. Sahnedeki her arabaya `WheelGlow`, `DriftSmoke`, `HeadlightController`, `EngineAudio`, `TireScreechAudio` bağla.
5. Ana sahneye `DayNightCycle` + directional light + Volume ekle.
6. HUD'a `SpeedometerNeedle` + `NitroBar` bağla.
7. Trafik sistemi: birkaç generic araba modeline `TrafficCar` bileşeni + waypoint zinciri kur.

---

## 7) Genişletilmiş sistemler (v0.2 — eklendi)

Bu iskelet artık aşağıdaki Dream Road-tarzı sistemleri de içeriyor:

**Ekonomi & garaj** (`Assets/Scripts/Economy/`)
- `PlayerMoney.cs` — PlayerPrefs + Photon custom prop (leaderboard için görünür)
- `CarCatalog.cs` — ScriptableObject araç kataloğu + `CarDefinition` (fiyat, ikon, stat)
- `CarInventory.cs` — Sahip olunan araçlar, aktif araç
- `ShopUI.cs` — Mağaza listesi + satın al butonu

**Odalar & moderasyon** (`Assets/Scripts/Network/`)
- `RoomPassword.cs` — Şifreli oda (Dream Road'daki `pWd` custom property)
- `PlayerListPanel.cs` — Oyuncu listesi + ping + master client kick butonu

**Kişiselleştirme** (`Assets/Scripts/Customization/`)
- `LicensePlate.cs` — Plaka texture (Dream Road'daki `PlateVariant.Change`), Photon prop sync

**Yarış modu** (`Assets/Scripts/Race/`)
- `Checkpoint.cs`, `RaceManager.cs`, `DriftScore.cs`, `LeaderboardUI.cs`

**Voice chat + emote** (`Assets/Scripts/Voice/`, `Emote/`)
- `VoiceChatController.cs` — Photon Voice 2 (PHOTON_VOICE_DEFINED define ile)
- `EmoteSystem.cs` — RPC ile korna/el sallama/alkış
- `HornController.cs`

**Monetizasyon & yayın** (`Assets/Scripts/Monetization/`, `Consent/`, `Tutorial/`)
- `IAPManager.cs` — Unity IAP (para paketleri, VIP subscription) — UNITY_PURCHASING define
- `AdsManager.cs` — Unity Ads rewarded video — UNITY_ADS define
- `Analytics.cs` — Firebase/Unity Analytics scaffold
- `KVKKConsent.cs` — KVKK/GDPR + iOS ATT (App Tracking Transparency) izin
- `TutorialManager.cs` — İlk açılış step-by-step rehber

**Kamera & çevre** (`Assets/Scripts/CameraModes/`, `Environment/`)
- `CameraModeController.cs` — Chase / Hood / Bumper / Free / Cinematic geçişi
- `Weather.cs` — Rain / Snow + `_GlobalWetness` shader property

**HUD ekstra** (`Assets/Scripts/UI/`)
- `Minimap.cs` — Top-down kamera → RenderTexture → RawImage
- `PingIndicator.cs`
- `ToastNotification.cs` — Kısa bilgi mesajları

**Araç sistemleri** (`Assets/Scripts/Vehicle/`)
- `GearBox.cs` — Otomatik şanzıman + vites göstergesi (R/N/1/2/…)
- `FuelSystem.cs` — Yakıt sarfiyatı + boşalınca motor keser
- `RefuelStation.cs` — Trigger volume, para düşer, depo dolar
- `CarDamage.cs` — Çarpma birikimi + duman + tamir

**Lokalizasyon & ayarlar** (`Assets/Scripts/Localization/`, `Settings/`)
- `LocalizationManager.cs` — TR/EN yerleşik, `LocalizedText` component
- `GameSettings.cs` — Grafik kalite, FPS, master/music/sfx volume, direksiyon hassasiyeti

### Paket bağımlılıkları (opsiyonel — sadece import edince aktif olurlar)

| Sistem | Gerekli paket | Define symbol |
|---|---|---|
| Voice chat | Photon Voice 2 (Asset Store) | `PHOTON_VOICE_DEFINED` |
| IAP | com.unity.purchasing (Package Manager) | `UNITY_PURCHASING` (auto) |
| Ads | com.unity.ads (Package Manager) | `UNITY_ADS` (auto) |
| Firebase Analytics | Firebase SDK (Google) | `FIREBASE_ANALYTICS` |

Define eklemek için: Project Settings → Player → **Other Settings** → **Scripting Define Symbols**.

---

## 8) v0.3 — Dream Road parity (RCCP + PlayFab + oyun modu + harita sistemi)

Bu iterasyon Dream Road'un gözlemlenebilir mimarisiyle kod düzeyinde fonksiyonel parity kurar. Grafik/asset kopyalanmaz — mekanikler kod olarak yazılır, sen kendi görsel varlıklarını sağlarsın.

### 8a) RCCP kurulumu (opsiyonel, önerilen)

Fizik motoru olarak RCCP'yi (Realistic Car Controller Pro, BoneCracker Games) kullanmak istersen:

1. Unity Asset Store → **Realistic Car Controller Pro** al ($50 civarı).
2. Editor'de import et.
3. Player Settings → Other Settings → **Scripting Define Symbols** → `RCCP_INSTALLED` ekle.
4. Test aracına RCCP prefab'ını hazırla (BoneCracker demo aracı temel alınabilir).
5. Aynı GameObject'e ek olarak `RCCPCarAdapter` + `RCCPNitroBridge` + `RCCPDamageBridge` + `RCCPDetachableBridge` bileşenlerini ekle. Bunlar RCCP API'sini bizim `IDriveInput` arayüzümüz altında sarar; `MobileTouchInput`, `NitroBar`, damage HUD hepsi RCCP moduyla otomatik çalışır.
6. `RCCPWheelGlowBridge` bileşeni de eklenirse bizim `WheelGlow.cs`'i devre dışı bırakır (çift emissive olmasın).

RCCP alma → adapter aktif; almazsan mevcut `CarController` (WheelCollider tabanlı) çalışmaya devam eder. Her ikisi de `IDriveInput` arayüzünü uyguladığı için oyunun geri kalanı fizik motorundan bağımsız.

### 8b) PlayFab backend

Anti-cheat için sunucu tarafı para/satın alma doğrulaması PlayFab ile yapılır.

1. Unity Asset Store → **PlayFab SDK** (ücretsiz) import et.
2. [developer.playfab.com](https://developer.playfab.com) → hesap → yeni Title oluştur → **Title ID** kopyala.
3. Sahnede `PlayFabAuth` bileşenine Title ID'yi gir.
4. Player Settings → Scripting Define Symbols → `PLAYFAB_INSTALLED` ekle.
5. `PlayFabMoneySync` bileşenini `PlayerMoney` ile aynı GameObject'e ekle → otomatik login sonrası money PlayFab'dan geri yüklenir, her değişiklik 2 sn debounce ile server'a yazılır.
6. **Server tarafı validation kodları**: `Assets/Scripts/Backend/PlayFabCloudScriptStubs.md` içindeki üç handler'ı (`addMoney`, `buyCar`, `submitRaceResult`) PlayFab dashboard → Automation → Revisions → paste + Deploy.
7. `PlayFabInventoryBridge` bileşeni `CarInventory` ile aynı GameObject'e → araç satın alma CloudScript üstünden validate edilir (client hile ile araç alamaz).
8. Leaderboard: PlayFab dashboard → Game Manager → Leaderboards → `raceBestLap` (Maximum), `driftScore` (Maximum) tanımla.

### 8c) Oyun modu sistemi

Dream Road'daki `Drift` / `Free` / `Race` modları (Bomb hariç, v0.4'e bırakıldı).

- **Room custom property `mode`** ile hangi mod yazılır (0=Free, 1=Race, 2=Drift).
- Sahne yüklendiğinde `GameModeManager` otomatik uygun `GameModeBase` bileşenini spawn eder.
- **Race**: 3-2-1-GO sayacı, `RaceManager` bağlanır, bitişte `PlayerMoney` ödül.
- **Drift**: 3 dakikalık oturum, `DriftScore` bank'ından her 1000 puana ödül.
- **Free**: Kural yok.

### 8d) Harita sistemi (1 harita + 3 varyant)

Tek gerçek Unity sahnesi + weather/time-of-day preset ile 3 varyant.

1. Editor → Assets → Create → DreamCar → **Map Definition** ile 3 SO oluştur:
   - `Map_City` — sceneName=`Game`, weather=Clear, timeOfDay=0.5
   - `Map_CityNight` — sceneName=`Game`, weather=Clear, timeOfDay=0.85
   - `Map_CityRainy` — sceneName=`Game`, weather=Rain, timeOfDay=0.5
2. Assets → Create → DreamCar → **Map Catalog** → içine 3 map SO'yu sürükle.
3. `RoomCreatorUI` bileşeninde `mapCatalog` alanına catalog'u ata.
4. Sahnedeki `MapSelector` bileşenine de aynı catalog + `Weather` ve `DayNightCycle` referansları.
5. Room creator'dan varyant seçince aynı sahne yüklenir ama preset uygulanır → 3 farklı görünüm, tek asset yükü.

Yeni harita eklemek için: (a) Editor'de yeni sahne, Build Settings'e ekle, (b) yeni `MapDefinition` SO oluştur, catalog'a ekle.

### 8e) Advanced Room Creator UI

`RoomCreatorUI.cs` — mevcut basit oda oluşturmanın yerine genişletilmiş form:
- TMP_InputField (oda adı, şifre)
- TMP_Dropdown (mod: Free/Race/Drift)
- TMP_Dropdown (harita: MapCatalog'dan otomatik doldurulur)
- Slider (max oyuncu 2-16)
- Toggle (görünür/gizli)
- Create butonu → `RoomPassword.CreateWithPassword(name, password, maxPlayers, mode, mapId, visible)`

`RoomOptions.CustomRoomProperties` = `{ pWd, mode, map }` — hepsi lobby'de görünür → oda listesinde ikon çıkarılabilir.

### 8f) Dream Road ekstra sistemleri

- `Vehicle/CruiseControl.cs` — `C` tuşu (varsayılan) ile sabit hız tutucu.
- `Vehicle/InteriorCamera.cs` — 1. şahıs kokpit; steer input'una göre direksiyon döner. `CameraModeController`'ın yeni `Interior` moduyla otomatik açılır. `V` tuşu mod cycle.
- `Vehicle/TrafficSpawner.cs` — Waypoint chain'ler üzerinde belirli aralıklarla trafik aracı spawn'lar, oyuncu uzaklaşınca despawn (pool). `HR_TrafficSettings` eşdeğeri.
- `Customization/CarPaintHDR.cs` — Emissive/HDR boya, rainbow modu opsiyonel. URP + Bloom ile parlar. Photon prop sync.
- `Customization/SplitLicensePlate.cs` — 2 parçalı plaka (34 | FEW 1337). Dream Road'daki `disableSplit@0x29` mekaniği.
- `Emote/AirHorn.cs` — Ritmik korna, 4 farklı nota deseni. RPC ile herkes duyar.
- `UI/RichChatUI.cs` — TMP rich text + emoji sprite atlas (`:grin:` → sprite). Zararlı `<size=9999%>` tag'ları filtrelenir.
- `UI/GarageCarousel.cs` — Ana menü/garage'da sol/sağ ok butonlarıyla araç değiştirme + 3D turntable preview.
- `Environment/TimeOfDayPreset.cs` — SO. `DayNightCycle`'a instant snapshot uygular (freeze).

### 8g) Yeni bileşenlerin sahneye bağlanması

MainMenu sahnesine ek:
- Boş GameObject → `PlayFabAuth` (Title ID yaz)
- Aynı objeye → `PlayFabMoneySync`, `PlayFabInventoryBridge`, `PlayFabLeaderboards`
- Garage paneli için `GarageCarousel` + 3D preview mount
- Room creator paneli için `RoomCreatorUI` (MapCatalog referansı)

Game sahnesine ek:
- `RoomManager` zaten `GameModeManager` + `MapSelector` otomatik spawn/uygular
- Araç prefab'ına `CruiseControl`, `InteriorCamera` (varsa kokpit anchor), `CarPaintHDR`, `SplitLicensePlate`, `AirHorn`, `RCCPCarAdapter` (RCCP moduysa)
- Sahneye `TrafficSpawner` + lane waypoint zincirleri

### 8h) Sorun giderme (v0.3)

- **`RCCP_INSTALLED` tanımlı ama derleme hatası**: RCCP namespace'i `RCCP` mi yoksa `BCG.RCCP` mi kontrol et. Farklıysa `#if RCCP_INSTALLED` bloklarındaki `using` satırını güncelle.
- **PlayFab login başarısız**: Title ID doğru mu? İnternet var mı? Console'daki `ErrorMessage`'a bak.
- **PlayFab CloudScript "handler not found"**: Revision deploy edildi mi (dashboard → Revisions listesinde aktif olmalı)?
- **Oyun modu spawn olmuyor**: Room custom property `mode` yazılmamış → RoomCreatorUI kullan (eski manual `LobbyManager.CreateRoom` mode yazmaz).
- **Harita varyantı geceye geçmiyor**: `MapSelector.applyMapPreset` true mu? Sahnede `DayNightCycle` var mı?

---

## 9) v0.4 — Görsel eksikler + yayın esasları + sosyal + AppMeta

Dream Road parity max için 18 yeni script + minik modifler.

### 9a) Paket A — Görsel eksikler (`Effects/`, `Vehicle/`)

- `Effects/TurnSignals.cs` — Sol/sağ sinyal + dörtlü flaşör. RPC ile sync. Emissive + Light 0.5 sn blink. Prefab'a bağla, `.Left()` / `.Right()` / `.Hazard()` metotlarını buton onClick'ine at.
- `Effects/HighBeamController.cs` — `H` tuşu ile uzun huzme. Range/intensity 2x + beam mesh emissive.
- `Effects/WindshieldWipers.cs` — Iki wiper mesh sin-curve animasyon. `Weather.Rain` aktifken otomatik başlar. 3 hız seviyesi.
- `Vehicle/RearViewMirror.cs` — Küçük kamera + RenderTexture. Sağ üst RawImage veya gerçek ayna mesh'ine bind.
- `Vehicle/RepairPanel.cs` — Hasar bar + Tamir butonu. Fiyat hasar oranından hesaplanır, `PlayerMoney` düşer, `CarDamage.Repair()` çağrılır.

### 9b) Paket B — Yayın esasları (`UI/`, `Rewards/`, `Moderation/`)

- `UI/PauseMenu.cs` — `Esc` → `Time.timeScale=0`, panel açılır. "Devam / Ayarlar / Odadan Çık / Ana Menü".
- `Rewards/DailyReward.cs` + `Rewards/LoginStreak.cs` — İlk açılışta bugün ödül alındı mı bak. Streak +1 → 3. günden itibaren 2x, 7. günden itibaren 3x çarpan.
- `Moderation/BanList.cs` — Master ban'lediğinde PlayerPrefs'e yazılır, aynı UserId odaya girmeye çalışırsa otomatik kick.
- `Moderation/ReportPlayer.cs` — Sebep dropdown + PlayFab CloudScript `submitReport` çağrısı.
- `UI/ChatProfanityFilter.cs` — TR + EN dahili küfür listesi, `Resources/ProfanityList.txt` ile genişletilir. `RichChatUI` gönderirken çağırılır (bu turda entegre edildi).

### 9c) Paket C — Sosyal (`Social/`, `Backend/`)

- `Backend/PlayFabAchievements.cs` + `Social/AchievementCatalog.cs` — SO ile achievement tanımla (statistic + threshold), unlock'ta toast + para ödülü. RaceManager yarış bitince, DriftScore combo'da otomatik bildirim (bu turda entegre edildi).
- `Social/ReferralSystem.cs` — 8 karakter unique kod. CloudScript `redeemReferral` ile iki tarafa bonus.
- `Social/PlayFabFriends.cs` — Nickname veya PlayFabId ile arkadaş ekle, listele, çıkar.
- `UI/PlayedWithList.cs` — Son 20 oyuncu (UserId, nickname, oda) PlayerPrefs cache. Ana menüde "Beraber oynadıkların" listesi, her satırda "Arkadaş Ekle" butonu.

### 9d) Yayın metadata (`AppMeta/`)

- `AppMeta/PrivacyPolicyScreen.cs` — URL varsa `Application.OpenURL`, yoksa dahili TextAsset panel içinde. KVKKConsent'ten link bağla.
- `AppMeta/SupportEmailLink.cs` — Ayarlar → Destek → `mailto:` (versiyon + cihaz + PlayFabId otomatik konu).
- `AppMeta/RateAppPopup.cs` — N yarış (default 5) sonra popup. Evet → App Store review link (`itms-apps://`). Hayır → geri bildirim. "Bir daha sorma" flag.

### 9e) CloudScript ek handler'lar

`Assets/Scripts/Backend/PlayFabCloudScriptStubs.md` içine iki yeni handler eklendi: `submitReport` (24h cooldown ile spam engel), `redeemReferral` (kod eşleşme + iki tarafa bonus). Dashboard'a revision olarak deploy et.

### 9f) Sahne bağlama özeti

MainMenu sahnesine ek:
- `DailyReward` (popup panel + amount/streak label + Claim button)
- `LoginStreak` (görünmez singleton)
- `ReferralSystem` (görünmez) + UI: kod göster + input + "Kullan" button
- `PlayedWithList` (listParent + entryPrefab)
- `PlayFabFriends` (görünmez)
- `PlayFabAchievements` (Catalog SO referansı)
- `AchievementCatalog.asset` (Editor'de doldur: id, isim, statistic, threshold, ödül)
- `RateAppPopup` (popup + feedback panel + iosAppId)
- `PrivacyPolicyScreen` (KVKK ekranından link)
- `SupportEmailLink` (Ayarlar → Destek)
- `BanList` (görünmez singleton)

Game sahnesine ek:
- Araç prefab'ına: `TurnSignals`, `HighBeamController`, `WindshieldWipers`
- HUD'a: `RearViewMirror`, `RepairPanel`, `PauseMenu`, `ReportPlayer`
- `ChatProfanityFilter` (görünmez singleton — RichChatUI otomatik bulur)

---

## 10) v0.5 — Editor Setup Wizard + Refuel UI + CI

### 10a) Sahne/prefab tek tıkla kurulum

Yeni: `Assets/Editor/DreamCarSetup.cs`. Unity Editor'de menü:

- **DreamCar → Setup → Run All** — hepsini tek seferde yapar.
- **DreamCar → Setup → Create Car Prefab** — `Assets/Resources/Car.prefab` (Rigidbody + 4 WheelCollider + tekerlek mesh'leri + CarController axles + PhotonView + CarNetworkSync + Nitro + Damage + Paint + CruiseControl + GearBox + FuelSystem + Engine/Screech Audio + HornController).
- **DreamCar → Setup → Create MainMenu Scene** — `Assets/Scenes/MainMenu.unity` (Canvas + nickname input + Play button + Lobby panel + Toast + PhotonConnector + PlayerMoney + PlayFab + Referral + BanList + ChatProfanityFilter + RateAppPopup + IAP/Ads + LoginStreak).
- **DreamCar → Setup → Create Game Scene** — `Assets/Scenes/Game.unity` (Plane + Sun + Camera + CarCameraFollow + CameraModeController + 4 spawn point + RoomManager + Weather + DayNightCycle + MapSelector + HUD Canvas: speed/ping/room/leave + Chat + Controls panel + Nitro bar + Fuel meter + Refuel station panel + Pause menu + Toast).
- **DreamCar → Setup → Add Scenes To Build Settings** — MainMenu + Game Build Settings'e eklenir.

Kullanım: yeni Unity aç → hiç sahne dokunmadan → menü → Run All → PhotonAppId'yi PhotonServerSettings'e yapıştır → Play.

**Not**: 3D asset gelmez; araç yerine cube+cylinder placeholder. Kendi araba modelini prefab'a swap edeceksin.

### 10b) Refuel UI (Vehicle/UI)

- `UI/FuelMeter.cs` — HUD yakıt barı. FuelSystem.Percent'i takip eder, %30 altında sarı, %15 altında kırmızı + tek seferlik "Yakıt az" toast.
- `UI/RefuelStationPanel.cs` — İstasyon trigger'ına girince açılan panel. Doldurma fiyatı = eksik litre × pricePerLiter. "Öde ve Doldur" → PlayerMoney düşer, TryFillTank, kapan. "İptal" veya trigger'dan çıkış → kapan.
- `Vehicle/RefuelStation.cs` — Otomatik doldurma yerine sadece panel açar. Sadece owner araç için (PhotonView.IsMine).

### 10c) GitHub Actions CI

`.github/workflows/unity-ios-build.yml` — iki job:

- **compile-check** — Linux hosted runner, GameCI unity-test-runner ile derleme + EditMode test validasyonu. Ücretsiz.
- **ios-build** — Self-hosted macOS runner (label `macos-dreamcar`), Unity iOS build → Xcode archive → `.ipa` artifact. Sertifika secret'ları gerekli.

Detaylı kurulum: `.github/workflows/CI_SETUP.md` — Unity lisansı `.alf`→`.ulf`, Apple `.p12` + provisioning profile base64, self-hosted runner ekleme, sorun giderme.

`Variables → UNITY_CI_ENABLED=true` ve `IOS_BUILD_ENABLED=true` set edene kadar workflow skip. Böylece PR açtığında yanlışlıkla tetiklenmez.

### 10d) Kalan v0.6+ işleri

- Bomb modu (sen atlamıştın)
- Sürücü avatarı (sen atlamıştın)
- CAS ad mediation (Unity Ads yeterli değilse)
- Cloud save extended (garaj/plaka/renk hepsi PlayFab UserData'ya)
- Voice chat kurulum rehberi (Photon Voice import)
- Push notification (Firebase / OneSignal)
- Analytics event coverage genişletme
- Localization JSON asset bazlı hale getirme

---

## 11) v0.6a — Kritik altyapı (reconnect, ekranlar, müzik, pooling, crash)

Bu paket, "script var ama oyuncunun göreceği karşılığı yok" boşluklarını ve mobilde oyunu bozan eksikleri kapatır.

### 11a) Yeniden bağlanma — `Network/ReconnectionManager.cs`

Mobilde bağlantı sürekli kopar (telefon uykuya girer, 4G↔WiFi geçişi, tünel). Önceden `OnDisconnected` sadece log basıyordu; oyuncu odadan düşüyor ve geri dönemiyordu.

- Üstel geri çekilme ile 6 deneme (2s → 4s → 8s… max 30s)
- Oda biliniyorsa `ReconnectAndRejoin()`, yoksa `Reconnect()`, o da olmazsa `ConnectUsingSettings()`
- "Bilerek çıkış" ayrımı var: `InGameHUD` ve `PauseMenu` çıkış butonları `MarkUserInitiatedLeave()` çağırır → boşuna yeniden bağlanmaya çalışmaz
- Kurtarılamaz sebeplerde (auth hatası, ban, CCU dolu) denemez
- Ekranda "Yeniden bağlanıyor… (2/6)" overlay'i

### 11b) Eksik 5 ekran

Backend'leri vardı, paneli yoktu. Hepsi `DreamCarSetup` wizard'ı tarafından MainMenu'ye kuruluyor ve ana menüye açma butonları ekleniyor.

| Ekran | Ne gösterir |
|---|---|
| `UI/SettingsScreen.cs` | Kalite, FPS, master/müzik/SFX ses, direksiyon hassasiyeti, dil — `GameSettings`'e canlı bağlı |
| `UI/LeaderboardScreen.cs` | Sekmeli: En İyi Tur / Drift Skoru. `PlayFabLeaderboards`'tan çeker, süreyi `1:23.45` formatına çevirir |
| `UI/AchievementsScreen.cs` | `AchievementCatalog` listesi, kilitli/açık ayrımı, ilerleme özeti `7 / 20` |
| `UI/CoinShopScreen.cs` | IAP coin paketleri + "reklam izle para kazan" (60 sn cooldown) |
| `UI/StatsScreen.cs` | Mesafe, süre, en yüksek hız, yarış/galibiyet/oran, en iyi drift, kazanılan para, araç, çarpışma |

### 11c) İstatistik takibi — `Core/PlayerStats.cs` + `Core/StatsTracker.cs`

Achievement'lar `OnDistanceTravelled` gibi çağrılar bekliyordu ama kimse çağırmıyordu. Artık:

- `PlayerStats` — kümülatif sayaçlar, PlayerPrefs persist, `ToJson()`/`FromJson()` ile cloud save'e hazır (v0.6b bunu kullanacak)
- `StatsTracker` — araç prefab'ına eklenir, sadece sahibi olan araçta çalışır. Mesafe/süre/en yüksek hız/çarpışma toplar, 5 sn'de bir flush eder. Teleport sıçramalarını mesafeye yazmaz
- `RaceManager`, `DriftScore`, `PlayerMoney` artık `PlayerStats`'e rapor ediyor

### 11d) Müzik sistemi — `Audio/MusicManager.cs`

Oyunda hiç müzik yoktu. İki `AudioSource` ile crossfade yapan playlist:

- `Playlist.Menu` / `Playlist.Gameplay` iki ayrı liste
- Shuffle, parça bitince otomatik sıradaki, `crossfadeSeconds` ile yumuşak geçiş
- `AudioMixerGroup` alanına müzik grubunu ata → `GameSettings.MusicVolume` slider'ı etkiler

> **AudioMixer asset repoda yok** — Unity'de script ile `.mixer` oluşturulamıyor. Editor'de: **Assets → Create → Audio Mixer** → içine `Master`, `Music`, `SFX` grupları ekle → her grubun Volume parametresini expose et ve **tam olarak** `Master` / `Music` / `SFX` adlarını ver → `GameSettings.mixer` alanına sürükle. Bu yapılmazsa ses slider'ları sessizce hiçbir şey yapmaz.

### 11e) Loading screen — `UI/LoadingScreen.cs`

Sahne geçişleri sert kesmeydi. Artık progress bar + dönen ipuçları + fade in/out.

- `ShowForPhotonLoad()` — `PhotonNetwork.LevelLoadingProgress` takip eder (master `LoadLevel` çağırdığında)
- `LoadScene(name)` — normal async yükleme, `allowSceneActivation` ile %100'de bekletip geçer
- 8 hazır ipucu metni (drift, nitro, yakıt, kamera modu, tamir, şifreli oda, günlük ödül, checkpoint)

### 11f) Object pooling — `Core/ObjectPool.cs`

`TrafficSpawner` sürekli Instantiate/Destroy yapıyordu → mobilde GC spike ve takılma.

- Prefab başına stack, `prewarm` listesi ile önden doldurma, `maxSize` ile şişme koruması
- `Spawn(prefab, pos, rot)` / `Despawn(go)` / `DespawnAfter(go, sec)`
- `IPooled` arayüzü — havuzdan çıkarken/girerken state sıfırlamak isteyen bileşenler uygular
- `TrafficSpawner` artık pool varsa onu kullanıyor, yoksa eski yola düşüyor

### 11g) Crash reporting — `Backend/CrashReporter.cs`

Cihazda çökerse hiçbir kayıt yoktu.

- Yakalanmamış exception + `LogType.Exception`/`Error` yakalar
- Son 40 log satırını "breadcrumb" olarak tutar → çökmeden önce ne olduğu görünür
- Rapor içeriği: sürüm, cihaz, OS, RAM, PlayFabId, aktif sahne, mesaj, stack, breadcrumb'lar
- `FIREBASE_CRASHLYTICS` define'ı varsa Crashlytics'e yollar; yoksa PlayerPrefs'e yazar → `CrashReporter.ConsumePendingReport()` ile destek e-postasına eklenebilir

### 11h) Wizard güncellendi

`DreamCar → Setup → Run All` artık şunları da kuruyor: ReconnectionManager + overlay, PlayerStats, ObjectPool, CrashReporter, MusicManager, LoadingScreen paneli, 5 ekran + ana menü nav butonları, araç prefab'ına StatsTracker.

---

## 12) v0.6b — Cloud save, voice HUD, push, analytics, localization JSON, CAS

### 12a) Tam profil cloud save — `Backend/PlayFabCloudSave.cs`

`PlayFabMoneySync` sadece parayı taşıyordu. Artık profilin tamamı buluta gidiyor:

- Garaj (sahip olunan araçlar + aktif araç), plaka (split ve tek parça), boya (renk/metallic/smoothness), ayarlar (ses/kalite/FPS/hassasiyet), streak, davet kodu, açılmış başarımlar, dil, **istatistikler**
- 5 sn debounce; uygulama arka plana atılınca veya kapanınca anında flush
- **Merge stratejisi**: araçlar bulut ∪ yerel (offline satın alma kaybolmaz), istatistikler max(bulut, yerel)
- `CarInventory.OwnedCarIds()` ve `MergeOwnedFromCloud()` eklendi

Yeni cihaz testi: `player.customId` PlayerPref'ini ikinci cihaza kopyala → login → tüm profil gelir.

### 12b) Voice chat HUD — `Voice/VoiceHUD.cs`, `Voice/PlayerVoiceMute.cs`

`VoiceChatController` API'ydi, ekranı yoktu.

- `VoiceHUD` — push-to-talk butonu (basılı tut), mute toggle, konuşma göstergesi renk değiştirir. Editor testi için `T` tuşu
- `PlayerVoiceMute` — oyuncu bazlı **yerel** susturma, UserId bazlı PlayerPrefs'te kalıcı. `PlayerListPanel` satırlarına bağlanabilir

Photon Voice 2 kurulumu: Asset Store import → sahneye `Recorder` → araç prefab'ına `Speaker` → Define'a `PHOTON_VOICE_DEFINED`.

### 12c) Push + yerel bildirim — `Notifications/`

- `PushNotificationsManager.cs` — Firebase Cloud Messaging. Token alınca PlayerPrefs'e yazıp cloud save'i tetikler → sunucu hedefli bildirim gönderebilir. `data.link` payload'ı ile deep link event'i. Define: `FIREBASE_MESSAGING`
- `LocalNotificationScheduler.cs` — Sunucusuz. Günlük ödül alınmadıysa saat 20:00'ye bildirim kurar; alındıysa yarına atar. `ScheduleIn(delay, title, body)` ile özel bildirim. Paket: `com.unity.mobile.notifications`

iOS kurulumu: Firebase konsolunda APNs Auth Key yükle, `GoogleService-Info.plist` projeye ekle.

### 12d) Analytics kapsamı genişletildi

Önceden 4 çağrı vardı, şimdi 12+ event: `login`, `car_spawn`, `race_start`, `race_finished`, `car_purchased`, `achievement_unlocked`, `daily_reward_claimed`, `referral_redeemed`, `player_banned`, `player_reported`, `ad_shown`, `ad_completed`, `ad_failed`, `iap`.

### 12e) Localization JSON'a taşındı

Çeviriler artık `Assets/Resources/Localization/tr.json` ve `en.json` dosyalarında (~90 anahtar). C# içinde hardcoded değil.

- Yeni dil eklemek: `es.json` koy + `LocalizationManager.availableLanguages` dizisine `"es"` ekle
- Fallback zinciri: aktif dil → İngilizce → anahtarın kendisi
- JSON yoksa dahili minik liste devreye girer (oyun boş metinle açılmaz)
- `AddOrOverride(code, key, value)` ile runtime'da sunucudan gelen metin enjekte edilebilir

JSON formatı:
```json
{ "entries": [ { "key": "play", "value": "Oyna" } ] }
```

### 12f) CAS ad mediation — `Monetization/CASAdsManager.cs`

Tek ağ (Unity Ads) yerine waterfall: AdMob + AppLovin MAX + IronSource + Unity Ads + Vungle.

- `AdsManager.ShowRewarded()` artık önce CAS'ı dener, hazır değilse Unity Ads'e, o da yoksa simülasyona düşer — **çağıran kod değişmedi**
- Kurulum: Package Manager → git URL `https://github.com/cleveradssolutions/CAS-Unity.git` → Assets → CleverAdsSolutions → Settings → iOS Manager ID → Define `CAS_INSTALLED`

---

## 13) v0.6c — Spam koruması, anti-cheat, bant genişliği, cihaz uyarlama

### 13a) Chat spam koruması — `Moderation/ChatRateLimiter.cs`

Küfür filtresi vardı, flood engeli yoktu.

- **Token bucket**: 4 mesaj burst, sonra saniyede 0.5 token
- **Tekrar tespiti**: aynı mesaj 20 sn içinde 3 kez → ceza
- **Kademeli susturma**: 10 sn → 20 → 40 … max 5 dk. 2 dk sessiz kalınca kademe sıfırlanır
- `RichChatUI.Send()` içine bağlandı; engellendiğinde sebep toast'la gösterilir

### 13b) Hile tespiti — `Moderation/CheatDetector.cs`

PlayFab yalnızca parayı koruyordu, fizik tarafı açıktı.

- Uzak oyuncuların pozisyonunu 0.5 sn'de bir örnekler
- **NaN/Infinity pozisyon** → anında ihlal (bu paketler client çökertmek için kullanılır)
- **Teleport** (tek örnekte >120 m) ve **imkânsız hız** (>400 km/h) → strike
- 5 strike → `cheat_suspected` analytics event + toast; `masterAutoKick` açıksa master kick eder (varsayılan **kapalı** — yanlış pozitif riski)
- Temiz örneklerde strike geri sayılır

> Bu client-side sezgisel bir katman, kesin çözüm değil. Gerçek koruma Photon Server Plugin ister.

### 13c) Bant genişliği — `Network/NetworkInterestManager.cs`

500 m uzaktaki araç da her frame sync ediliyordu.

- Mesafe kademeleri: yakın (80 m) / orta (200 m) / uzak — `PhotonNetwork.SerializationRate` 20/10/4'e ayarlanır
- 400 m'den uzaktaki araçların `Renderer`'ları kapatılır (hem GPU hem görsel gürültü kazancı)
- `RoomManager` spawn'da yerel aracı bu manager'a bildirir

### 13d) Cihaz uyarlama — `Settings/QualityAutoDetect.cs`

Eski iPhone'da da full quality açılıyordu.

- RAM + VRAM + çekirdek + ekran pikseli üzerinden puanlar → Low / Mid / High
- Kalite seviyesi + hedef FPS (30/60) + render scale (0.7 / 0.85 / 1.0) uygular
- **Kullanıcı Ayarlar'dan değiştirdiyse bir daha karışmaz** (`MarkUserOverride`, SettingsScreen'den çağrılıyor)

### 13e) Remote config — `Backend/RemoteConfig.cs`

Ödül/fiyat değiştirmek için app güncellemesi gerekiyordu.

- PlayFab **TitleData**'dan anahtar-değer çeker, PlayerPrefs'e cache'ler (ağ yoksa son değerlerle çalışır)
- Kullanım: `RemoteConfig.GetLong("race.win_reward", 1000)` — anahtar yoksa default'a düşer
- `GetInt / GetLong / GetFloat / GetBool / GetString`

PlayFab dashboard → **Content → Title Data** → anahtar ekle, deploy gerektirmez.

### 13f) Deep link + davet paylaşımı — `Core/DeepLinkManager.cs`

- `dreamcar://room/<oda>?pwd=<şifre>` ve `dreamcar://ref/<kod>` şemaları
- Soğuk başlatma (`Application.absoluteURL`) ve çalışırken gelen link (`deepLinkActivated`) ikisi de yakalanır
- `ShareCurrentRoom()` / `ShareReferral()` → linki panoya kopyalar + toast
- Web fallback: `https://<host>/room/<oda>` — aynı parser her ikisini de çözer

iOS kurulumu: Xcode → Info → URL Types → URL Schemes → `dreamcar`

### 13g) Haptik — `Core/Haptics.cs`

- 7 stil: Light / Medium / Heavy / Success / Warning / Failure / Selection
- `Haptics.PlayImpact(impulse)` çarpma şiddetine göre otomatik seviye seçer — `CarDamage`'a bağlandı
- Ayarlardan kapatılabilir (`Haptics.Instance.Enabled`)
- iOS'ta gerçek Taptic Engine için `Assets/Plugins/iOS/Haptics.mm` gerekir; yoksa çağrı sessizce yutulur

### 13h) Bölge seçimi — `UI/RegionSelector.cs`

`preferredRegion` kodda vardı ama ekranı yoktu, herkes default bölgeye düşüyordu.

- 10 Photon bölgesi + "Otomatik (en iyi ping)" seçeneği
- Seçim PlayerPrefs'e yazılır; `PhotonConnector.Connect()` artık önce bu değeri okur
- Bölge değişince otomatik yeniden bağlanır (ReconnectionManager'a "bilerek çıkış" bildirilir)

---

## 14) Kalan iterasyonlar (v0.7+)

- Bomb modu (bomba pas mini oyunu)
- Sürücü avatarı (TurnTheGameOn IKAvatarDriver eşdeğeri)
- Ek haritalar (asset geldikçe: 9 harita × 6 varyant = 54)
- RCCP Tuner ($52 indirim) — visual customization (body kit, decal, spoiler)
- Refuel station UI (fuel meter + ücret onay panel)
- Push notification (Firebase Messaging / OneSignal — günlük ödül hatırlatma)
- Photon Server Plugin (self-host) — fizik doğrulama
- CAS ad mediation (multi-network)
- Voice chat aktifleştirme (Photon Voice import + Recorder + Speaker prefab kurulumu)
- Marka-özgür gerçek 3D asset entegrasyonu (senin işin)

---

## Dosya haritası

| Dosya | Görev |
|---|---|
| `Scripts/Car/CarController.cs` | WheelCollider tabanlı fizik |
| `Scripts/Car/CarCameraFollow.cs` | Kamera takibi |
| `Scripts/Car/CarNetworkSync.cs` | Photon position/rotation sync |
| `Scripts/Input/MobileTouchInput.cs` | Dokunmatik + klavye input |
| `Scripts/Network/PhotonConnector.cs` | Master bağlantısı, singleton |
| `Scripts/Network/LobbyManager.cs` | Oda listesi/oluştur/katıl |
| `Scripts/Network/RoomManager.cs` | Oda içi spawn |
| `Scripts/Network/NicknameManager.cs` | PlayerPrefs persist |
| `Scripts/UI/MainMenuUI.cs` | Ana menü |
| `Scripts/UI/LobbyUI.cs` | Oda listesi UI |
| `Scripts/UI/InGameHUD.cs` | Hız/oyuncu HUD |
| `Scripts/UI/ChatUI.cs` | RPC chat |
| `Scripts/UI/SpeedometerNeedle.cs` | Analog kilometre iğnesi |
| `Scripts/UI/NitroBar.cs` | Nitro fill + basılı tut butonu |
| `Scripts/Effects/WheelGlow.cs` | Fren/kayma balata kızarması (RCCP_WheelGlow eşdeğeri) |
| `Scripts/Effects/CarNitro.cs` | Nitro sistemi (CarNitro eşdeğeri) |
| `Scripts/Effects/DriftSmoke.cs` | Lastik dumanı + skid trail |
| `Scripts/Effects/HeadlightController.cs` | Farlar + tail emissive |
| `Scripts/Audio/EngineAudio.cs` | Idle+rev motor loop, RPM pitch |
| `Scripts/Audio/TireScreechAudio.cs` | Lastik screech sesi |
| `Scripts/Environment/DayNightCycle.cs` | Güneş + ambient gradient |
| `Scripts/Traffic/TrafficCar.cs` | Waypoint trafik AI |
| `Scripts/Customization/CarPaint.cs` | Boya (renk/metallic/smoothness) + Photon custom prop sync |
| `Scripts/Game/GameBootstrap.cs` | Her sahnede setup |
| `link.xml` | IL2CPP stripping için Photon namespace preserve |
| **v0.3 dosyaları** | |
| `Scripts/Car/IDriveInput.cs` | Fizik motor bağımsız input arayüzü |
| `Scripts/RCCPBridge/RCCPCarAdapter.cs` | RCCP_CarController → IDriveInput sarımı |
| `Scripts/RCCPBridge/RCCPNitroBridge.cs` | RCCP_Nos ↔ NitroBar UI köprü |
| `Scripts/RCCPBridge/RCCPWheelGlowBridge.cs` | Custom WheelGlow'u devre dışı bırakır |
| `Scripts/RCCPBridge/RCCPDamageBridge.cs` | RCCP_Damage ↔ CarDamage API köprü |
| `Scripts/RCCPBridge/RCCPDetachableBridge.cs` | Hasar eşiğinde parça düşürme |
| `Scripts/GameModes/GameMode.cs` | Enum + abstract base |
| `Scripts/GameModes/FreeRoamMode.cs` | Kuralsız serbest sürüş |
| `Scripts/GameModes/RaceMode.cs` | 3-2-1-GO + tur + ödül |
| `Scripts/GameModes/DriftMode.cs` | 3 dk drift oturumu + ödül |
| `Scripts/GameModes/GameModeManager.cs` | Room prop'a göre mod spawn |
| `Scripts/Maps/MapDefinition.cs` | SO — sahne + weather + TOD |
| `Scripts/Maps/MapCatalog.cs` | SO — tüm haritalar |
| `Scripts/Maps/MapSelector.cs` | Sahne yüklendiğinde preset uygular |
| `Scripts/UI/RoomCreatorUI.cs` | Genişletilmiş oda oluşturucu (mode+map+password) |
| `Scripts/UI/RichChatUI.cs` | TMP rich text + emoji sprite chat |
| `Scripts/UI/GarageCarousel.cs` | Ok butonlarıyla araç değiştirme + 3D preview |
| `Scripts/Backend/PlayFabAuth.cs` | Anonim CustomID login |
| `Scripts/Backend/PlayFabMoneySync.cs` | Money cloud persist |
| `Scripts/Backend/PlayFabLeaderboards.cs` | Race + drift leaderboard |
| `Scripts/Backend/PlayFabInventoryBridge.cs` | Server-authoritative araç satın alma |
| `Scripts/Backend/PlayFabCloudScriptStubs.md` | Dashboard'a yapıştırılacak JS handler'lar |
| `Scripts/Vehicle/CruiseControl.cs` | Sabit hız tutucu |
| `Scripts/Vehicle/InteriorCamera.cs` | 1. şahıs kokpit + direksiyon |
| `Scripts/Vehicle/TrafficSpawner.cs` | Waypoint chain trafik pool |
| `Scripts/Customization/CarPaintHDR.cs` | Emissive/rainbow boya |
| `Scripts/Customization/SplitLicensePlate.cs` | 2 parçalı plaka |
| `Scripts/Emote/AirHorn.cs` | Ritmik korna 4 nota deseni |
| `Scripts/Environment/TimeOfDayPreset.cs` | SO — DayNightCycle snapshot |

---

## Sorun giderme

- **"The type or namespace name 'Photon' could not be found"**: PUN 2 import edilmemiş — Adım 2'yi tekrar yap.
- **Oda yaratıp katılamama**: AppId boş → PhotonServerSettings kontrol et.
- **Araba dönmüyor**: WheelCollider'ların Y ekseni doğru mu, Rigidbody mass 1200 mü, center of mass -0.6 Y mi.
- **Multiplayer'da diğer araba titriyor**: `CarNetworkSync.interpSpeed` değerini artır (12 → 20).
- **iOS'ta uygulama açılır açılmaz kapanıyor**: Xcode'da Console → sinyal (SIGABRT/SIGSEGV) bak. Genelde Photon AppId boş, veya IL2CPP stripping code'u yemiş. `Assets/link.xml` ile Photon namespace'ini preserve et:
  ```xml
  <linker>
    <assembly fullname="PhotonRealtime" preserve="all"/>
    <assembly fullname="PhotonUnityNetworking" preserve="all"/>
  </linker>
  ```
- **iOS Xcode signing hatası "No provisioning profile"**: Bundle ID'nin benzersiz olduğundan ve Team seçili olduğundan emin ol. Ücretsiz hesapla aynı bundle ID başka biri tarafından kullanılamaz.
- **iOS'ta multiplayer bağlanmıyor ama Editor'de çalışıyor**: Photon UDP portları için `Info.plist` → `NSAppTransportSecurity`'ye `NSAllowsArbitraryLoads = true` ekle (Unity iOS build'e ayarlarda ekletebilirsin), veya PhotonServerSettings'te "Protocol: Udp" kalsın (default). Cihaz VPN veya kısıtlı ağdaysa da olmaz.
