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

## 8) Kalan iterasyonlar

- Sunucu tarafı anti-cheat (Photon Server Plugin — self-host)
- Arkadaş sistemi, davet
- Cloud save (PlayFab / Firebase Realtime DB)
- Push notification (günlük ödül)
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
