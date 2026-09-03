# DreamCar (Unity Araba Oyunu — MVP İskelet)

Dream Road Online tarzı online çok oyunculu araba oyunu için Unity 6 iskeleti. Photon PUN 2 + WheelCollider + ücretsiz asset yaklaşımı.

Bu klasör commit edilmiş iskelettir — Unity Editor'de açıp aşağıdaki adımları izleyerek çalışır bir prototipe getireceksin. Sonra iOS için `.ipa` build alıp cihaza yükleyebilirsin (App Store, TestFlight, AltStore, veya jailbreak).

> **iOS için Mac + Xcode gerekli.** Unity `.ipa` doğrudan üretmez — Unity Xcode projesi çıkarır, Xcode `.ipa`'ya derler. Windows/Linux'ta iOS build alamazsın. Android için böyle bir kısıt yok.

---

## 0) Hızlı başlangıç — dört adım

Aşağıdaki uzun bölümler *referans*. Sıfırdan oynanabilir hale getirmek için tek yol yeterli:

1. **Projeyi aç** (bölüm 1)
2. **TextMeshPro kaynaklarını içe aktar**: `Window → TextMeshPro → Import TMP Essential Resources` → **Import**.
   Unity bunu ilk açılışta sorabilir; sormazsa elle yap. **`BUILD EVERYTHING` bunu kontrol ediyor ve
   eksikse durup sana söylüyor** — çünkü bu klasör olmadan oyundaki HİÇBİR yazı görünmez
   (başlıklar, buton etiketleri, hız göstergesi, sohbet, para). Yazı tipi projeye ait bir varlık,
   depoda tutulmuyor.
3. **Photon PUN 2'yi import et + App Id gir** (bölüm 2) — çok oyunculu için tek zorunlu dış paket
4. Menüden **`DreamCar → BUILD EVERYTHING`**

Son adım şunları kodla üretir: texture ve materyaller, 5 araç prefab'ı, araç kataloğu, UI sprite'ları, uygulama ikonları ve açılış ekranı, post-processing profilleri, MainMenu + Game sahneleri, prosedürel şehir, **8 harita sahnesi** ve harita kataloğu, Build Settings. Birkaç dakika sürer.

Bitince `Assets/Scenes/MainMenu.unity` aç → **Play**.

**Türkçe karakterler kutu (□) görünüyorsa:** Project penceresinde
`Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF` varlığını seç,
Inspector'da **Atlas Population Mode**'u `Dynamic` yap. Statik atlas yalnızca ASCII
içeriyor; dinamik mod eksik glifleri (ş ğ ı İ ç ö ü) çalışma anında ekliyor.

**Sohbet çalışmıyorsa:** sahne PhotonView'larının ViewID'sini PUN, sahne kaydedilirken
atıyor. Sohbet paneli betikle kurulduğu için bu atamanın yapıldığını Editor dışından
doğrulayamıyorum. Çözüm tek adım: Photon import edildikten sonra `MainMenu.unity` ve
`Game.unity`'yi aç ve kaydet (Ctrl+S).

> **Dış asset satın almana gerek yok.** Modeller, texture'lar, sesler, ikonlar — hepsi kodla üretiliyor (bölüm 14). Bölüm 3 ve 4 elle kurulum anlatıyor; bunlar `BUILD EVERYTHING` öncesinden kalma, kendi asset'ini getirmek istersen diye duruyor. Normal akışta ikisini de atla.

---

## 1) Aç

1. **Unity Hub** kur (unity.com/download).
2. **Unity 6 LTS** (6000.0.30f1 veya üzeri) yükle. Hub "Missing Editor Version" uyarısı verirse daha yeni bir 6000.x ile açmak sorun değil: depoda tek bir sahne veya prefab dosyası yok, her şey C# ve üretici betiklerden ibaret, o yüzden sürüm yükseltmesi hiçbir varlığı bozamaz. Modülleri: **iOS Build Support** (Mac'te zorunlu). Windows/Linux'ta yalnızca geliştirme yapabilirsin — iOS `.ipa` build için Mac + Xcode şart.
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

## 3) Ücretsiz asset'leri import et *(opsiyonel — atlayabilirsin)*

> `BUILD EVERYTHING` araç ve harita üretiyor. Bu bölüm yalnızca **kendi** modelini getirmek istersen geçerli.

Kendi asset'ini kullanacaksan: bir araba modeli + bir harita/zemin.

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

## 4) Sahneleri ve prefab'ı kur *(opsiyonel — `BUILD EVERYTHING` bunu yapıyor)*

> Bu bölüm elle kurulumu anlatır. `DreamCar → BUILD EVERYTHING` aynı sonucu tek tıkla üretir. Buraya yalnızca ne üretildiğini anlamak ya da elle müdahale etmek istersen bak.

### 4a) Car prefab (network spawn için)

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
3. Project penceresinde bir RCCP araç **prefab'ı** seç
   (genelde `Assets/Realistic Car Controller Pro/Resources/Vehicles/` altında).
4. Menü: **`DreamCar → RCCP → Seçili RCCP aracını DreamCar aracına çevir`**

Hepsi bu. Araç `Assets/Resources/Car_rccp_<ad>.prefab` olarak kaydedilir,
araç kataloğuna eklenir ve ana menüdeki **Araçlar** mağazasında görünür.
Satın alıp seçtikten sonra odaya girince o araçla doğarsın.

Dönüştürücünün otomatik yaptıkları:

- `RCCP_INSTALLED` define'ını ekler (Standalone + Android + iOS) — elle
  eklemene gerek yok. RCCP'nin varlığı zaten doğrulanmış oluyor.
- **Köprü:** `RCCPCarAdapter` (asıl olan) + nitro, hasar, parça, parıltı köprüleri.
- **Ağ:** `PhotonView` + `CarNetworkSync` (observed component bağlı).
- **Oynanış:** drift skoru, vites, yakıt, hasar, istatistik, cruise control.
- **Ses:** prosedürel motor sesi, lastik çığlığı, korna, çarpma.
- **Görsel:** boya (en büyük mesh gövde kabul edilir), ön/arka plaka, drift dumanı
  ve fren izi (her `WheelCollider` için).
- **Kamera çapaları:** kaput, tampon, kokpit, üst — prefabın gerçek sınırlarından
  hesaplanır, sabit sayı yazılmaz.
- **Emote:** üç emote + araç üstü baloncuk.

Bilerek **eklenmeyenler** ve sebepleri:

| Eklenmeyen | Neden |
|---|---|
| `CarController` + kendi WheelCollider'larımız | Fizik RCCP'nin; iki sürücü aynı Rigidbody'yi süremez |
| `CarNitro` | `RCCPNitroBridge` RCCP'nin kendi NOS'una gidiyor |
| `WheelGlow` | RCCP kendi parıltısını getiriyor; `RCCPWheelGlowBridge` bizimkini kapatıyor |
| Far / sinyal / uzun huzme | RCCP'nin kendi ışık sistemi var; ikisi aynı `Light`'ları sürerse titrer |

> Prosedürel üretilen beş araç yerinde kalır — RCCP araçları onların **yanına**
> gelir, yerine değil. Oyuncu mağazadan seçer. `BUILD EVERYTHING` tekrar
> çalıştırıldığında dönüştürülmüş araç katalogda korunur.

> **Lisans:** RCCP ve ondan türetilen prefab'lar **public** depoya konamaz
> (Asset Store EULA). Dönüştürme kendi makinende çalışır; çıktı yalnızca
> private depoya commit edilmeli.

> **Köprüler RCCP tipine doğrudan bağlanmaz.** Bu kod RCCP'nin gerçek API'si görülmeden yazıldı; adlar tahmindi ve yanlış tahmin, define eklendiği anda projenin derlenmemesi demekti. Bunun yerine tip ve üye adları çalışma anında aranıyor (`RCCPReflection`). Bir ad tutmazsa proje yine derlenir; Console'a **RCCP'nin gerçek üye adlarını listeleyen** bir uyarı düşer. O listeyi geliştiriciye gönder, köprü adları tek turda düzeltilir.
>
> Zorunlu olan yalnızca gaz ve direksiyon. Nitro, hasar ve parça düşürme köprüleri bulunamazsa uyarı basıp devre dışı kalır — oyun çalışmaya devam eder.

**RCCP Tuner ayrı bir eklentidir**, temel RCCP'nin kurulu olmasını gerektirir ve tek başına çalışmaz. Görsel özelleştirme (body kit, spoiler, jant) ekler. Şart değil; köprü katmanı temel RCCP'ye göre yazıldı, Tuner ayrı bir entegrasyon işidir.

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
- `AudioMixerGroup` alanı opsiyonel — mixer kurarsan müzik grubunu buraya ata

#### Ses seviyeleri — `Audio/AudioBus.cs`

**AudioMixer kurmak zorunda değilsin.** Ses sürgüleri kutudan çalışır.

`.mixer` varlığı script ile üretilemiyor (Unity'nin public API'si yok), bu yüzden sistem mixer olmadan da tam çalışacak şekilde yazıldı. İki mod var ve **yalnızca biri** aynı anda devrede olur — ses asla iki kez kısılmaz:

| Durum | Ne olur |
|---|---|
| `GameSettings.mixer` boş (varsayılan) | **Master** → `AudioListener.volume` (motor tarafında global çarpan). **Music** ve **SFX** → `AudioBus` çarpanları; sesi üreten script'ler kendi taban seviyelerine uygular |
| Mixer atanmış **ve** `Master`/`Music`/`SFX` expose edilmiş | Klasik mixer yolu — `AudioBus` çarpanları 1 döner, devre dışı kalır |
| Mixer atanmış ama parametreler expose **edilmemiş** | Konsola uyarı düşer, otomatik `AudioBus` yoluna dönülür (sessizce ölmez) |

Yeni bir ses kaynağı eklerken:

- Seviyesini **her karede kendin yazıyorsan** (motor, lastik gibi) → `* AudioBus.SfxScale` ile çarp
- Seviyesini **bir kez ayarlayıp `Play()` ediyorsan** → `Awake()`'te `AudioBus.RegisterSfx(kaynak)`, `OnDestroy()`'da `AudioBus.Unregister(kaynak)`

Mixer'ı yine de kullanmak istersen: **Assets → Create → Audio Mixer** → `Master`, `Music`, `SFX` grupları → her grubun Volume parametresini sağ tık → **Expose** ve **tam olarak** bu adları ver → `GameSettings.mixer` alanına sürükle. Sistem otomatik mixer yoluna geçer.

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

## 14) v0.7 — Prosedürel varlıklar (3D, texture, ses, UI kodla üretiliyor)

Buraya kadar oyun **kod olarak** hazırdı ama sahne gri küplerden ibaretti; hiç
3D model, texture, ses veya UI grafiği yoktu. Artık hepsi kodla üretiliyor —
dışarıdan telifli varlık indirmene gerek yok.

### 14a) Tek komutla her şey

```
Menü → DreamCar → BUILD EVERYTHING (sıfırdan oynanabilir hale getir)
```

Sırayla texture'ları, UI sprite'larını, 5 aracı, kataloğu, iki sahneyi ve
şehri üretir; Build Settings'i ayarlar. Tek tıklama, ~1-2 dakika.

Tek tek çalıştırmak istersen: `DreamCar → Procedural → …` altındaki komutlar.

### 14b) Araçlar — `Editor/Procedural/ProceduralCarGenerator.cs`

Gövde **loft** ile üretilir: uzunluk boyunca kesitler (genişlik, yükseklik,
merkez yüksekliği) tanımlanır, aralarına yüzey gerilir. Kaput eğimi, kabin
yükselmesi ve bagaj düşüşü bu kesitlerden doğal olarak çıkar.

| Araç | Karakter | Fiyat |
|---|---|---|
| Sedan | Dengeli, uzun kabin | 0 (başlangıç) |
| Hatchback | Kısa, dik arka | 25.000 ₺ |
| Sport Coupe | Alçak, geniş iz, 235 km/h | 85.000 ₺ |
| SUV | Yüksek, ağır, büyük tekerlek | 60.000 ₺ |
| Pickup | Uzun, açık kasa profili | 48.000 ₺ |

Her prefab şunlarla **bağlı** çıkar: 4 WheelCollider + süspansiyon, CarController
aks yapılandırması, PhotonView + sync, farlar/stoplar (gerçek Light + emissive),
sinyaller, nitro, hasar, yakıt, vites, boya, HDR boya, kokpit kamerası, motor/
lastik/korna ses kaynakları, kamera bağlantı noktaları, istatistik takibi.

Tekerlek mesh'i lastik + jant yüzü + 5 jant kolu içerir; jant ayrı child olduğu
için `WheelGlow` fren ısısını oraya basar.

### 14c) Şehir — `Editor/Procedural/ProceduralCityGenerator.cs`

6×6 blok ızgara (~450×450 m):

- Yollar ve kaldırımlar tek mesh'te birleştirilmiş (draw call az)
- Binalar merkeze doğru yükselen siluet oluşturur; çatı katlarıyla kırılma
- Sokak lambaları (her ikinci direkte gerçek ışık — performans dengesi)
- 24 waypoint'lik kapalı trafik halkası, iki şeride bölünmüş
- 8 yarış checkpoint'i (0 numaralı bitiş çizgisi)
- 16 spawn noktası merkez meydanda
- Çalışır benzin istasyonu (saçak, pompalar, dolum tetikleyicisi, aydınlatma)
- `TrafficSpawner` üretilen araç prefab'larına otomatik bağlanır

### 14d) Texture'lar — `Editor/Procedural/ProceduralTextures.cs`

Perlin gürültü ve prosedürel desenle: asfalt (çok ölçekli gürültü + çakıl),
kaldırım (taş + fuga), bina cephesi (kat ızgarası; **gece varyantında**
pencerelerin bir kısmı yanar), yol çizgisi, çim.

Materyaller URP ve Built-in pipeline'ın **ikisinde de** çalışır — shader
bulunamazsa Standard'a düşer.

### 14e) Ses — `Scripts/Audio/ProceduralEngineAudio.cs`

Ses dosyası yok, hepsi **sentezleniyor**:

- **Motor**: harmonik serisi (tek harmonikler baskın) + silindir sayısına bağlı
  patlama zarfı + alçak geçirgen filtrelenmiş gürültü + yumuşak doyum. Rölanti ve
  gaz için iki ayrı klip; `EngineAudio` bunları RPM'e göre pitch'ler.
- **Lastik çığlığı**: bant geçirgen gürültü + yavaş frekans modülasyonu
- **Korna**: iki kare dalga (A4 + C#5) + attack/release zarfı

Klipler **tam sayıda çevrim** içerir ve dikişte çapraz geçiş uygulanır — döngüde
tık sesi olmaz.

### 14f) UI sprite'ları — `Editor/Procedural/ProceduralUISprites.cs`

9-slice yuvarlak köşeli panel (köşeler bozulmadan esner), pill buton, daire,
halka, gradyan, chevron oku, dişli/kupa/bayrak ikonları. Hepsi antialiaslı.

### 14g) iOS native köprü — `Plugins/iOS/DreamCarNative.mm`

`Haptics.cs` ve `KVKKConsent.cs` bunları çağırıyordu ama karşılığı yoktu:

- Taptic Engine: impact (light/medium/heavy), notification (success/warning/error),
  selection — generator'lar önceden hazırlanır, gecikme düşük
- App Tracking Transparency izni
- Düşük güç modu ve termal durum okuma (kalite düşürmek için kullanılabilir)

Dosya `Assets/Plugins/iOS/` altında olduğu için Unity Xcode projesine otomatik ekler.

> ATT penceresinin çıkması için Player Settings → iOS → **User Tracking Usage
> Description** alanının dolu olması gerekir.

### 14h) Testler — `Tests/EditMode/GameMathTests.cs`

CI'daki `compile-check` job'u artık gerçekten bir şey doğruluyor: 40+ EditMode testi.

Test edilebilirlik için saf mantık `Scripts/Util/GameMath.cs` içinde ayrı bir
assembly'ye alındı (Unity paketlerine bağlı değil, PUN/PlayFab kurulu olmadan da
derlenir). `LeaderboardScreen`, `StatsScreen`, `LoginStreak`, `RepairPanel`,
`RefuelStationPanel`, `GearBox`, `SpeedometerNeedle`, `CheatDetector`,
`QualityAutoDetect` ve `RichChatUI` artık kendi kopya mantıkları yerine bunu
çağırıyor — yani testler gerçek kod yollarını doğruluyor.

Kapsam: süre/mesafe biçimleme, streak çarpanı, tamir ve yakıt fiyatı, vites
seçimi, kilometre saati açısı, hile tespiti eşikleri, token bucket, kalite
kademesi, superellipse geometrisi, zararlı rich-text kırpma.

### 14i) Görsel kalite hakkında dürüst not

Bu prosedürel varlıklar **stilize low-poly** görünür. Oyun çalışır, tutarlı ve
oynanabilir görünür — ama profesyonel modellenmiş bir oyunun görsel kalitesinde
**değildir**. Sonradan yükseltmek istersen:

1. Asset Store'dan araç modeli al
2. Üretilen prefab'ı aç, `Body` child'ının `MeshFilter`'ındaki mesh'i değiştir
3. WheelCollider konumlarını yeni modele göre ayarla

Tüm oyun kodu (fizik, ağ, ışık, hasar, boya) olduğu gibi çalışmaya devam eder —
sadece görsel katman değişir.

---

## 15) v0.8 — Android desteği (artık iki platform)

Proje başta iOS'a hedeflenmişti. Oyun kodunun %95'i zaten platform bağımsızdı;
eksik olan entegrasyon noktaları tamamlandı.

### 15a) Neler değişti

| Alan | Önce | Şimdi |
|---|---|---|
| Yerel bildirim | Sadece iOS | Android kanal kaydı, POST_NOTIFICATIONS izni (API 33+), zamanlı bildirim |
| Haptik | Android'de kaba `Handheld.Vibrate()` | Vibrator servisi üstünden gerçek desenler, genlik kontrolü, API 26/31 yolları |
| Unity Ads | Tek `iosGameId` | `iosGameId` + `androidGameId`, platforma göre otomatik seçim |
| CAS mediation | Tek `iosManagerId` | İki platform ID'si |
| CI | Sadece iOS (Mac gerekli) | **Ayrıca ücretsiz Linux'ta Android APK** |

### 15b) Android haptik

`Core/Haptics.cs` artık ayrı bir Java plugin dosyası olmadan çalışıyor — JNI köprüsü
C# içinden `AndroidJavaObject` ile kuruluyor:

- **API 31+**: `VibratorManager` → `getDefaultVibrator` (Android 12 bu yolu zorunlu kılıyor)
- **API 26+**: `VibrationEffect.createOneShot` / `createWaveform`, genlik kontrolü
  destekleniyorsa kullanılır, yoksa `DEFAULT_AMPLITUDE`'a düşer
- **API 25 ve altı**: düz süre tabanlı `vibrate()`

iOS'un ayrık haptic tipleri (success/warning/failure) Android'de kısa titreşim
desenleriyle taklit ediliyor.

### 15c) Android bildirimleri

`Notifications/LocalNotificationScheduler.cs` iki platformu da kapsıyor:

- Android bildirim **kanalı** otomatik kaydediliyor (`dreamcar_reminders`)
- Android 13+ için `POST_NOTIFICATIONS` izni isteniyor
- Günlük ödül hatırlatması iki platformda da aynı mantıkla planlanıyor
- `GetLaunchNotificationId()` — uygulama bildirime tıklanarak açıldıysa hangisi olduğunu döner

> Bildirim ikonları: Player Settings → Android → **Notification Icons** altında
> `icon_0` (small) ve `icon_1` (large) tanımlanmalı. Tanımlanmazsa bildirim
> görünür ama ikonsuz olur.

### 15d) Android build — iOS'tan çok daha kolay

`.github/workflows/unity-android-build.yml` **ücretsiz Linux runner'da** tamamen çalışır.
Mac, Xcode veya Apple Developer üyeliği gerekmez.

**Kurulum:** Sadece `UNITY_LICENSE` secret'ı + `UNITY_CI_ENABLED=true` variable'ı.
Detay: `.github/workflows/CI_SETUP.md` §1.

**Her push'ta:** APK üretilir, Actions sekmesinden artifact olarak inilir.
Telefona at, "bilinmeyen kaynaklara izin ver", kur. Bitti.

**Play Store için:** Keystore oluşturup 4 secret ekle, sonra Actions →
Run workflow → ".aab üret" kutusunu işaretle. Adımlar CI_SETUP.md §1e'de.

### 15e) Editor'de Android'e geçmek

```
File → Build Settings → Android → Switch Platform
```

Player Settings'te ayarlanacaklar:
- **Package Name**: `com.few1n.dreamcar` (iOS bundle ID'siyle aynı olabilir)
- **Minimum API Level**: Android 7.0 (API 24)
- **Target API Level**: 34 veya üzeri (Play Store zorunlu)
- **Scripting Backend**: IL2CPP
- **Target Architectures**: ARM64 (Play Store zorunlu, ARMv7 opsiyonel)
- **Internet Access**: Require

### 15f) Hangi platformdan başlamalısın

**Android'den.** Sebep:

| | Android | iOS |
|---|---|---|
| Test cihazına kurma | APK'yı at, kur | Mac + Xcode + sertifika |
| Geliştirici ücreti | $25 (tek seferlik) | $99/yıl |
| CI maliyeti | Ücretsiz | Kendi Mac'in |
| İnceleme süresi | Saatler | Günler |

iOS kodu hazır ve duruyor — Android'de oyunu oturttuktan sonra iOS'a geçmek
sadece platform değiştirip build almak olacak.

---

## 16) v0.9 — Prosedürel haritalar + grafik kademeleri

### 16a) 8 harita, kodla üretiliyor

`DreamCar → Maps → Generate ALL Maps` (veya `BUILD EVERYTHING` içinde otomatik) sekiz ayrı sahne üretir: **Pist, Otoyol, Çöl, Orman, Kar, Liman, Arazi, Köy.**

Her harita şu zincirden geçer:

1. **Yol hattı** — Catmull-Rom spline. `Circuit` (kapalı pist), `Highway` (uzun düz), `Winding` (dolambaçlı) düzenlerinden biri. Spline ham parametreleştirmede virajlarda nokta yığar, bu yüzden yoğun bölünüp **eşit aralıkla yeniden örneklenir**.
2. **Yol yüzeyi** — kesit lofting ile asfalt + banket + bariyer + orta çizgi. Viraj eğriliğinden **kamber** türetilir, sınırlanır ve 3 geçiş yumuşatılır.
3. **Arazi** — fraktal Perlin (+ opsiyonel ridge noise). **Yol çevresinde düzleştirilir** ve dışa doğru harmanlanır — yoksa yol havada asılı kalır. Her arazi köşesi yalnızca yakınındaki yol örneklerini test eder (uzamsal hash).
4. **Proplar** — ağaç, çam, kaya, kaktüs, bina, konteyner, vinç, ev, ambar, bariyer, lamba. Arketipe göre kural tabanlı serpiştirilir.
5. **Oynanış** — checkpoint zinciri, trafik yolu, spawn noktaları, ışıklandırma/sis, GraphicsTuner.

Harita kataloğu her arketip için **gündüz / gece / yağmur** varyantı üretir → 8 × 3 = **24 seçilebilir harita**.

### 16b) Kasmadan iyi görüntü

Ormanda ~1200 prop var. Prop başına GameObject mobilde ölümcül olurdu, bu yüzden:

- **GPU instancing** (`InstancedPropRenderer`) — ~1200 nesne → ~8 draw call. Mesafeye göre eleme + LOD, çizim tamponları önceden ayrılmış.
- **Collider'lar ayrı** — yalnızca çarpılabilecek büyük proplar collider taşır, MeshRenderer'sız.
- **Kendi shader'ımız** (`DreamCarVertexLit`) — URP/Lit vertex renklerini yok sayıyor, ama arazi bantlarımız ve prop tonlarımız orada. Ayrıca `_BaseColor` instancing buffer'ında olmadan instance başına renk verilemiyor. Yarım-lambert + SH ambient + sis + gölge dökümü; specular ve normal map yok.
- **Kalite kademeleri** (`GraphicsTuner`) — cihaz puanına göre Low/Mid/High. Çizim mesafesi, gölge, uzak kırpma, piksel ışık sayısı ve post-processing profili değişir. Düşük kademede sis **bilerek** yoğunlaştırılır: agresif elemenin kesme çizgisini gizler.

### 16c) Post-processing

`Assets/Generated/PostProcessing` altında üç URP Volume Profile üretilir; `GraphicsTuner` cihaza göre seçer:

| Profil | İçerik |
|---|---|
| `PostFX_Low` | Yalnızca Color Adjustments — ek render geçişi yok. En zayıf cihazlarda post-processing kameradan **tamamen** kapatılır |
| `PostFX_Mid` | + Bloom (yarım çözünürlük, HQ filtre kapalı), Vignette |
| `PostFX_High` | + ACES Tonemapping, Motion Blur (CameraOnly/Low), Chromatic Aberration |

---

## 17) Kalan iterasyonlar (v1.0+)

- Bomb modu (bomba pas mini oyunu) — *kalıcı kapsam dışı*
- Sürücü avatarı — *kalıcı kapsam dışı*
- RCCP Tuner ($52) — visual customization (body kit, decal, spoiler)
- Photon Server Plugin (self-host) — fizik doğrulama
- Voice chat aktifleştirme (Photon Voice import + Recorder + Speaker prefab kurulumu)
- Push notification için Firebase console kurulumu (kod hazır, `#if FIREBASE_MESSAGING`)
- Marka-özgür gerçek 3D asset entegrasyonu (istersen — prosedürel varlıklar yayına yeterli)

---

## 18) v1.0 — Oynanabilirlik: ekonomi döngüsü, kurtarma, çevrimdışı test

Bu turda düzeltilenler "eksik özellik" değil, **oyunu bitirilemez kılan
kilitlenmelerdi**. Ayarları buradan bulabilirsin.

### 18a) Serbest sürüşte gelir — `GameModes/FreeRoamMode.cs`

`PlayerMoney.Add`'i çağıran her yer (yarış, drift modu, günlük ödül,
referans, başarım, reklam, PlayFab senkronu) ya başka bir moddaydı ya oyun
dışında. Ama **varsayılan mod serbest sürüş**: hızlı oyun ve lobiden kurulan
odalar `mode` özelliğini hiç yazmıyor, `GameModeManager` de yazılmamış değeri
`0` (= Free) okuyor. Oyuncu 5.000 ₺ ile başlıyor, ikinci araç 25.000 ₺ —
yalnızca sürerek aradaki farkı **asla kapatamıyordu**.

Ödül iki kaynaktan geliyor:

| Alan | Varsayılan | RemoteConfig anahtarı |
|---|---|---|
| `moneyPerKilometre` | 120 ₺/km | `freeroam.moneyPerKm` |
| `rewardPerThousandDriftPoints` | 5 ₺ / 1.000 puan | `freeroam.driftPerThousand` |
| `toastEvery` | 250 ₺ (bildirim eşiği) | — |
| `tickSeconds` | 5 sn (ödeme aralığı) | — |

Mesafe kendi sayacımızdan değil, `StatsTracker`'ın zaten `PlayerStats`'e
akıttığı değerden okunuyor — o, ışınlanma sıçramalarını (>50 m) eliyor ve
yalnızca sahibi olunan araçta çalışıyor.

Kablolama `GameModeManager.AddComponent` üzerinden: Editor'de atanacak
hiçbir alan yok, yani "yazılmış ama hiç bağlanmamış" durumuna düşemez.

### 18b) Yakıt tüketimi — `Vehicle/FuelSystem.cs`

Eski değerler (`base 0.05 + gaz*0.4`) tam gazda saniyede 0,45 litre
harcıyordu: **60 litrelik depo 133 saniyede** bitiyordu. Depoyu doldurmak
1.500 ₺, başlangıç parası 5.000 ₺ ve gelir yok — üçüncü depodan sonra
parasız ve yakıtsız kalınıyordu.

Yeni: `baseDrainPerSecond = 0.006`, `throttleDrainMultiplier = 0.028`.
Tam gazda ~29 dakika, rölantide ~2,8 saat. 100 km/h'te kilometre başına
~1,22 litre ≈ 30 ₺ yakıta karşılık 120 ₺ kazanç — döngü artı bakiyeli.

### 18c) Kurtarma — `Vehicle/CarRescue.cs`

"Respawn", "Reset", "Flip" proje genelinde **sıfır sonuç** veriyordu. Takla
atan, haritadan düşen veya istasyondan uzakta yakıtı biten araç sonsuza
kadar kalakalıyordu; tek çıkış odadan çıkmaktı.

- Altında zemin varsa **yerinde doğrultuyor** (yön korunur) — uzun bir
  yolculuğu geri almak cezalandırıcı olurdu
- Zemin yoksa (haritadan düşme) en yakın doğma noktasına alıyor
- Depo boşsa `emergencyFuelLiters` (6 L) bedava veriyor, yoksa doğrultmanın
  anlamı olmazdı
- Ters durup duran araç `autoRescueSeconds` (5 sn) sonra kendiliğinden
  kurtarılıyor; `fallY` (-60) altına düşen anında
- `cooldownSeconds` (3 sn) ışınlanarak ilerlemeyi engelliyor

HUD'da sağ üstte **Kurtar** butonu var (`CarActionButtons.rescueButton`).
Bileşen her üç araç üreticisine de ekleniyor: prosedürel araçlar, sahne
kurulumu ve RCCP dönüştürücüsü.

### 18d) Çevrimdışı test modu — `Network/RoomManager.cs`

Bir harita sahnesini Editor'de doğrudan Play'e basarak açmak hiçbir şey
yapmıyordu: `PhotonNetwork.InRoom` false olduğu için araç doğmuyordu. Bu,
`BUILD EVERYTHING` bittiğinde açık olan sahne olduğu için herkesin gördüğü
ilk ekran — ve Photon App Id girilene kadar ana menüden de girilemediği için
sürüşü denemenin hiçbir yolu yoktu.

`RoomManager` artık `offlineFallbackDelay` (1,5 sn) bekleyip hâlâ bağlantı
yoksa PUN'ın çevrimdışı moduna geçip yerel bir oda kuruyor. Çevrimiçi oyunu
kesmez: lobiden gelindiğinde sahne zaten odadayken yükleniyor. Kapatmak için
`allowOfflineFallback = false`.

### 18e) Sessiz kalmayan teşhisler

- **Photon App Id boşsa** `ConnectUsingSettings` sessizce `false` dönüyordu;
  oyuncu hiç dolmayan bir oda listesine bakıyordu. Artık bağlanmadan önce
  kontrol ediliyor ve Console'a adım adım ne yapılacağı yazılıyor —
  özellikle Photon panelinde SDK'nın varsayılan `Fusion` yerine `Pun / Pun 2`
  seçilmesi gerektiği.
- **Ana menüde AudioListener yoktu** — menü tamamen sessizdi. Kamera
  `new GameObject(...) + AddComponent<Camera>()` ile kurulduğu için Unity'nin
  hazır kamera nesnesinin aksine listener eklenmiyordu.
- **Davet linki soğuk başlangıçta ölüydü**: `TryJoinPending()`'in tek
  çağıranı `Awake()` içinden gelen `Parse()`'tı ve o anda Photon bağlı
  olmadığı için hep `false` dönüyordu. `PhotonConnector.OnJoinedLobby`
  artık yeniden deniyor.
- **HUD'da para göstergesi** eklendi — serbest sürüş artık sürerken
  kazandırdığı için bakiye ekranda olmalı.
- **Oyunda hiç müzik yoktu.** `MusicManager` eksiksiz bir sistemdi (crossfade,
  shuffle, `AudioBus` seviyesi) ve iki sahneye de ekleniyordu, ama parça
  dizilerine hiçbir yerden atama yapılmıyordu ve depoda tek bir ses dosyası
  yok — sistem sessizce hiçbir şey yapmıyordu. Üstelik `Play(Playlist)`'in
  sınıf dışında sıfır çağıranı vardı, yani parçalar eklense bile oyun içi
  liste asla çalmazdı.

  `Audio/ProceduralMusic.cs` artık çalışma anında döngülenebilir parçalar
  sentezliyor (22050 Hz mono, ~16 sn, menü için sakin / sürüş için tempolu;
  DSP yardımcıları `ProceduralEngineAudio`'dan yeniden kullanılıyor). İlk
  parça `Awake`'te, kalanlar sonraki karelere yayılıyor — açılış donmuyor.
  Liste değişimini `MusicManager` kendisi `SceneManager.sceneLoaded`'dan
  dinliyor, Editor'de atanacak alan yok.

  **Prosedürel müzik yer tutucu kalitesindedir** — ambient pad gibi duyulur.
  Değiştirmek için `~Bootstrap → MusicManager` üzerindeki `menuTracks` /
  `gameplayTracks` dizilerine gerçek klipleri ata; diziler doluyken
  `ProceduralMusic` kendini devre dışı bırakıyor, kod düzenlemesi gerekmiyor.
- **Sohbet, sahne PhotonView'ının ViewID'si atanmadığı için ölüydü.** Sahne
  görünümlerinin kimliğini PUN sahne kaydedilirken atar; betikle eklenen
  görünümde bu gerçekleşmezse ViewID `0` kalır, PUN görünümü kaydetmez ve
  `photonView.RPC(...)` sessizce düşer. `RichChatUI` ile `ChatUI` mesajı
  yalnızca o RPC ile gönderiyor — yani sohbet tamamen sessiz kalırdı.
  Kimlik artık `DreamCarSetup` içinde açıkça atanıyor. (Araç prefablarındaki
  görünümler etkilenmiyor: onlar `PhotonNetwork.Instantiate` ile doğup
  kimliklerini çalışma anında alıyor.)

### 18f) Kablolama denetçisi — `Editor/DreamCarValidator.cs`

Bu projenin baskın hata ailesi tek bir şey: **sistem yazılmış, görünüşte tam,
ama hiçbir yere bağlanmamış ve sessizce hiçbir şey yapmıyor.** Ne derleme
hatası ne çalışma anı istisnası veriyor — sadece olması gereken olmuyor.
Yukarıdaki maddelerin hepsi bu aileden. Projede bunu yakalayan hiçbir
otomatik denetim yoktu.

`BUILD EVERYTHING`'in sonunda kendiliğinden koşuyor; elle de çalıştırılır:
**`DreamCar → Doğrulama → Sahneleri denetle`**.

Hata olarak raporladıkları (hepsi tek anlamlı):

| Kontrol | Olmazsa |
|---|---|
| Sahne başına tam 1 `AudioListener` | 0 = sahne tamamen sessiz, 2+ = bozuk ses |
| `EventSystem` | Hiçbir arayüz butonu çalışmaz |
| `MainCamera` etiketli kamera | `Camera.main` null — kamera takibi, kamera modları, minimap ölü |
| `Canvas` üzerinde `GraphicRaycaster` | Arayüz dokunma almaz |
| Sahne `PhotonView`'ında ViewID ≠ 0 | RPC'ler sessizce düşer (sohbet) |
| `CarCatalog` dolu + her prefab `Resources` altında | Araç seçilince doğmaz |
| `MapCatalog`'daki her sahne Build Settings'te | `LoadLevel` sessizce başarısız, oyuncular boş odada asılı kalır |
| URP varlığı atanmış | Bütün yüzeyler macenta |

Ayrıca `DreamCar.*` bileşenlerindeki **boş referans alanlarını** bilgi olarak
listeliyor. Bunlar hata değil — çoğu alan bilerek opsiyonel — ama bu ailenin
bıraktığı iz tam olarak bu, o yüzden gözle taranabilir tek bir liste hâlinde
veriliyor.

---

## 19) v1.1 — Kurulabilir APK, grafik kalitesi, backend'in görünürlüğü

### 19a) Depo tek başına oynanabilir oyun üretemiyordu

Android CI iş akışı eksiksizdi ama çalıştırılsa **boş bir uygulama**
üretirdi. Çünkü depoda şunlar yok:

| Yol | Depodaki dosya |
|---|---|
| `Assets/Scenes/` | **0** — MainMenu, Game, 8 harita, hiçbiri |
| `Assets/Generated/` | **0** — URP varlığı, mesh, materyal, katalog |
| `ProjectSettings/` | **1** — yalnızca `ProjectVersion.txt` |

Yani Build Settings sahne listesi, Graphics'teki URP ataması, Player Settings
(IL2CPP, ARM64, yatay yön, Active Input Handling) ve "MainCamera" etiketi de
depoda değil. Hepsi `BUILD EVERYTHING` ile makinede üretilip orada kalıyor.
Bunlar `.gitignore`'da değil — hiç commit edilmemişler.

**Çözüm: CI de üretiyor.** `Editor/CI/DreamCarCI.cs` batch-mode giriş
noktası; her iki build workflow'u `buildMethod` olarak bunu çağırıyor:

1. `DreamCarBuildAll.GenerateAll()` — üretim sırası tek kaynakta, kopyalanmıyor
2. `DreamCarValidator.Run()` — sorun varsa **build kesiliyor** (boş APK
   yüklemektense CI'da kırmızı görmek doğru)
3. `BuildPipeline.BuildPlayer`

İkili varlıklar git'e girmiyor, depo şişmiyor ve üretim zinciri her build'de
gerçekten sınanmış oluyor.

**APK almak için depoda yapılması gerekenler** (Settings → Secrets and
variables → Actions):

- **Variables**: `UNITY_CI_ENABLED` = `true`
- **Secrets**: `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE` (kişisel
  lisans `.ulf` dosyasının içeriği)
- Actions → "Unity Android Build" → Run workflow → artifact'ı indir, kur

CI dosyaları taşınırken **varsayılan daldan** okunuyor; workflow
değişikliklerinin `main`'de olması gerekiyor.

### 19b) ACES tonemapping LDR'de derecelendiriliyordu

`ProceduralPostProcessing` profillere `TonemappingMode.ACES` kuruyor. Ama URP
varlığında `colorGradingMode` varsayılanda **`LowDynamicRange`** kalıyordu:
sinyal derecelendirmeden önce kırpılıyor, ACES eğrisi kırpılmış veriye
uygulanıyor ve sonuç soluk çıkıyordu. `supportsHDR = true` demenin faydası
tam burada kayboluyordu — parlak gökyüzü, far hüzmesi ve metalik araç boyası
HDR'ın vermesi gereken derinliği hiç almıyordu. Hiçbir hata vermiyordu.

Ayrıca açılanlar: `colorGradingLutSize` 64 (bantlaşma), gölge kademesi 2→4 ve
mesafe 150, yumuşak gölgeler, **anizotropik filtreleme** (yol kaplaması sığ
açıdan görüldüğü için bu oyunda en çok göze çarpan ayar), LOD bias 1.5,
gerçek zamanlı yansıma probları.

**Sınır:** render ayarları sonuna kadar açıldı, ama "Dream Road gibi
görünmenin" önündeki engel ayar değil **içerik**. Araçlar, binalar ve dokular
prosedürel; elle modellenmiş varlıklarla aynı görünmezler. Bunu kapatmanın
tek yolu gerçek 3D varlık almak — RCCP'nin fizikte yaptığını görselde
yapacak muadili.

### 19c) Backend katmanının tamamı derlemeye girmiyordu

`PLAYFAB_INSTALLED` sembolü **10 dosyayı** koruyor: kimlik doğrulama, bulut
kayıt, liderlik tablosu, başarım senkronu, envanter, para senkronu, arkadaş
listesi, referans sistemi, oyuncu şikâyeti. Ve bu sembolü projede **hiçbir
yer tanımlamıyordu.** RCCP için dönüştürücü `RCCP_INSTALLED`'ı otomatik
ekliyor; PlayFab'in muadili yoktu. SDK kurulsa bile hiçbir şey değişmezdi.

`DreamCar → Backend → PlayFab kurulumunu doğrula` artık SDK'yı reflection ile
arayıp sembolü kendisi ekliyor, yoksa ne yapılacağını adım adım yazıyor.
Denetçi de durumu her koşuda bildiriyor.

**Oyun PlayFab'siz tam oynanır** — para, istatistik ve araçlar `PlayerPrefs`'te.
Eksik olan cihazlar arası kalıcılık ve sosyal taraf; yani "oyun çalışmıyor"
değil, "ilerleme telefon değişince kaybolur".

---

## 20) v1.2 — iOS / Android mağaza gereklilikleri

### 20a) iOS'ta ATT çağrısı uygulamayı ÇÖKERTİRDİ

`Plugins/iOS/DreamCarNative.mm` içinde ATT kodu yazılmış ve `KVKKConsent`
onu çağırıyor (`Decide → RequestATT → _RequestTracking`). Ama
**`NSUserTrackingUsageDescription` Info.plist'e hiç girmiyordu.**

Bu eksik meta veri değil, **çökme sebebi**: iOS, o anahtar yokken
`requestTrackingAuthorization` çağıran uygulamayı anında sonlandırır. Yani
oyuncunun KVKK onayında "Kabul ediyorum"a basması uygulamayı öldürürdü.
Native dosyanın kendi yorumu "Player Settings'ten doldur" diyordu, ama o
alan boştu ve kontrol eden hiçbir şey yoktu.

`Editor/iOS/DreamCarIOSPostBuild.cs` artık build sonrası Info.plist'e
yazıyor: ATT açıklaması, Photon UDP için `NSAppTransportSecurity` ve iPad
yatay yön kilidi.

### 20b) `PrivacyInfo.xcprivacy` yoktu

Apple 1 Mayıs 2024'ten beri zorunlu tutuyor; olmadan App Store Connect
yüklemeyi reddediyor.

Manifest **build anında üretiliyor**, depoda sabit durmuyor — içeriği hangi
derleme sembollerinin aktif olduğuna bağlı:

| Sembol | Manifeste eklenen |
|---|---|
| `CAS_INSTALLED` / `UNITY_ADS` | `NSPrivacyTracking = true`, reklam verisi |
| `PLAYFAB_INSTALLED` | Cihaz kimliği (hesaba bağlı, izleme değil) |
| `UNITY_ANALYTICS` | Ürün etkileşimi |
| *(her zaman)* | `NSPrivacyAccessedAPICategoryUserDefaults`, sebep `CA92.1` |

Sabit bir dosya ilk sembol değişiminde yalan söylemeye başlardı; Apple
yanlış beyanı eksik beyan kadar reddediyor.

### 20c) Android hedef API sabit 34'te çakılıydı

Play Store hedef API eşiğini her yıl ağustosta yükseltiyor. Sabit yazılan
her sayı bir sonraki ağustosta yayını engelliyor ve bunu ancak yükleme
reddedilince fark ediyorsun.

`targetSdkVersion` artık `AndroidApiLevelAuto` — kurulu Android SDK'nın en
yükseğini kullanıyor, yani SDK'yı güncellemek hedefi de güncelliyor.
Workflow'daki sabit `androidTargetSdkVersion: AndroidApiLevel34` kaldırıldı.

### 20d) Denetçi bunları da kontrol ediyor

`DreamCarValidator` artık platform ayarlarına da bakıyor: Android ARM64
açık mı, scripting backend IL2CPP mi, renk uzayı Linear mı (hata olarak);
Apple Team ID ve sabitlenmiş hedef API (bilgi olarak).

---

## 21) v1.3 — Arayüz düzeni

Referans olarak Dream Road: Online'ın üç ekranı alındı. Taklit edilen şey
**düzen kuralları** — garaj karuseli, oda satırı sütunları, analog gösterge.
Bunlar bu türün ortak dili. Logo, araç modelleri ve dokular kopyalanmadı.

### 21a) Lobi oda satırı tek satıra sıkışmıştı

`LobbyUI` şunu yazıyordu: `"Oda  (3/6)"`. Hangi harita, gündüz mü gece mi,
şifreli mi — hiçbiri görünmüyordu. Oysa **üçü de Photon'a zaten
yayımlanıyordu** (`RoomPassword.Lobby`), yalnızca gösterilmiyordu.

Satır artık dört sütun: kilit · oda adı · harita + zaman · doluluk.
Harita adı `MapDefinition.displayName`'den geliyor ve zaman dilimini zaten
içeriyor ("Otoyol (Gece)"). Şifreli odada `icon_lock` beliriyor — oyuncu
artık neden giremediğini görüyor. Listenin üstüne isim filtresi eklendi.

### 21b) Garaj kenarda duruyordu

Araç `x = -600`'de, ekranın kenarındaydı; ortası boştu. Artık ortada, oklar
iki yanında, adı ve fiyatı altında, SEÇ ve OYNA yan yana.

### 21c) HUD yeniden yerleştirildi — ve **dokuz çakışma** çıktı

Gösterge alt ortadaydı: baş parmakların gaz/frene gittiği yerin tam üstünde
ve göz sürekli yoldan aşağı inmek zorundaydı. Sağ üste alındı, vites rakamı
kadranın içine girdi, hız yazısı küçülüp altına indi.

Taşıma sırasında yerleşimi programla denetledim ve **eskiden beri var olan**
çakışmalar ortaya çıktı:

| Çakışma | Sonucu |
|---|---|
| Kurtar ↔ Duraklat | Aynı köşede üst üste — biri diğerine basılıyordu |
| Sohbet giriş kutusu ↔ direksiyon pedi | Sohbete dokunmak direksiyonu çeviriyordu |
| Sinyal/dörtlü/emote ↔ direksiyon pedi | Üç buton pedin alanının içindeydi |
| Minimap ↔ nitro butonu + nitro/yakıt çubukları | Minimap üçünün üstüne biniyordu |
| Yarış sıralaması ↔ gaz pedalı | Sıralama pedalın üstündeydi |
| Tamir fiyatı ↔ tamir çubuğu | Yazı çubuğun üstüne biniyordu |

Yeni düzen: gösterge sağ üstte, ikon şeridi (kamera/korna/sinyal/dörtlü/
emote/**Kurtar**) üst şeridin altında yatay, minimap sol üstte, sohbet
girişi pedalların ve pedin arasındaki boş banda alındı.

Ana menüde de aynı denetim **21 çakışma** buldu (dokuz nav butonu garajın ve
OYNA'nın üstündeydi); iki temiz sıraya dizildi.

**Doğrulama otomatik:** her iki ekranın yerleşimi 1920×1080 referansında
program tarafından taranıyor — HUD 33 eş-zamanlı öğe, ana menü 19 öğe,
ikisinde de sıfır çakışma ve sıfır ekran taşması.

---

## 22) v1.4 — Garajda araç görünüyor

`CarDefinition.thumbnail` alanı vardı. **İki** tüketicisi vardı:

- `GarageCarousel.cs:60` → `thumbnail.sprite = def.thumbnail`
- `ShopUI.cs:41` → satırdaki `Icon` çocuğuna atıyor

Ama **projede o alana hiçbir yerden atama yoktu.** Sonuç: garajın ortasındaki
760×340'lık alan boş bir dikdörtgen, mağaza satırlarında da araç görseli yok.
Oklar, ad, fiyat — hepsi yerinde, araç yok.

Yine aynı aile: alan var, okuyan var (hem de iki tane), **yazan yok** ve
hiçbir hata çıkmıyor.

`Editor/Procedural/ProceduralCarThumbnails.cs` her araç prefabından editör
zamanında bir görsel render ediyor:

- Sahneden uzakta (y = −5000) örnekleniyor — üretim sırasında açık olan
  sahnede arazi ve şehir duruyor, kadraja onlar girmesin
- Kamera mesafesi `Renderer.bounds`'tan hesaplanıyor; sabit sayı yazmak
  farklı boyuttaki araçlarda kimini kırpar kimini nokta bırakırdı
- 3/4 önden bakış, saydam arka plan (alfa 0) — panelin üstünde temiz durur
- PNG olarak `Assets/Generated/CarThumbs/` altına, `ProceduralUISprites.Save`
  ile aynı içe aktarma yolundan

`BUILD EVERYTHING` içinde **katalogdan hemen sonra** koşuyor: hem prefabı hem
`CarDefinition` varlığını gerektiriyor, ikisi de o noktada var. RCCP
dönüştürücüsüyle eklenen araçlar da küçük resim alıyor, çünkü `BuildCatalog`
dışarıdan gelen tanımları koruyor.

Denetçi artık küçük resmi olmayan aracı hata olarak bildiriyor.

**Canlı 3B önizleme neden değil:** `GarageCarousel.previewMount` bilerek
boş — araç prefabı `PhotonView` ve `Rigidbody` taşıyor, menü sahnesinde
odaya bağlı olmadan doğurmak hata üretiyor. Editörde render alıp sprite
kaydetmek o sorunu hiç doğurmuyor.

**Sınır:** bu, "araç görünmüyor"u "araç görünüyor"a çeviriyor. Dream Road'un
garajı modellenmiş bir mekân, elle yapılmış araç modelleri ve dokular;
bizimkiler prosedürel. Aradaki fark ayarla değil **gerçek 3B varlıkla**
kapanır — RCCP'nin fizikte yaptığının görsel karşılığı.

---

## 23) v1.5 — Ana menüde gerçek bir garaj

Geçen bölümde "aradaki fark ancak satın alınmış 3B varlıkla kapanır"
demiştim. **Eksikti.** Asıl sebep şuydu:

`CreateMainMenu` sahneye **hiç 3B içerik koymuyordu** — yalnızca bir kamera
ve UI. Garaj diye bir mekân, aydınlatma, zemin: hiçbiri yoktu. Referans
görüntünün oluşmamasının sebebi model kalitesi değil, sahnenin boş olmasıydı.

Ve gereken her yapı taşı zaten projedeydi; garaj için hiç kullanılmamıştı:
`MeshBuilder`, `ProceduralTextures.CreateTexturedMaterial`,
`ProceduralCityGenerator.CreateMeshObject`, hatta
`GarageCarousel.turntableDegPerSecond` bile.

**`Editor/Procedural/ProceduralGarage.cs`** odayı kuruyor: zemin, üç duvar
(ön açık — kamera oradan bakıyor), tavan, tavan ışıkları ve ortada aracın
döneceği pivot. İki yeni doku: `garage_floor` (fayans ızgarası + kırmızı
vurgu bandı) ve `garage_wall` (dikey lamel).

Işıklar hem **emissive şerit** hem **gerçek `Light`**: yalnızca emissive
koyarsak araç aydınlanmaz (URP'de emissive yüzey gerçek zamanlı ışık
yaymıyor), yalnızca `Light` koyarsak kaynak görünmez. Öne ayrıca yumuşak bir
dolgu ışığı — tepeden ışıkla aracın ön yüzü karanlıkta kalıyordu ve kamera
tam oradan bakıyor.

Kapalı oda olduğu için yansıma probu `boxProjection = true` ile çalışıyor
(açık dünyadaki probun aksine, orada kutu izdüşümü yanlış olurdu).

### Önizleme prefabı — eski engelin çözümü

`GarageCarousel.previewMount` bilerek boş bırakılmıştı: araç prefabı
`PhotonView` ve `Rigidbody` taşıyor, menüde odaya bağlı olmadan doğurmak
hata üretiyor. **O gerekçe hâlâ geçerli** — çözüm önizlemeden vazgeçmek
değil, prefabı ayırmaktı.

`ProceduralCarGenerator` artık her araç için `Preview_<id>.prefab` de
üretiyor: yalnızca `MeshFilter` + `MeshRenderer`. Rigidbody, WheelCollider,
PhotonView, collider ve bütün MonoBehaviour'lar atılıyor; far ışıkları da
(menüde sahneyi yıkıyorlardı). `CarDefinition.previewPrefabName` alanı
bunu gösteriyor — dize kuralıyla çözmek sessizce bozulabilecek bir varsayım
olurdu.

Garajın ortasındaki **düz panel kaldırıldı**: arkasında gerçek araç
dönerken önünde gri bir dikdörtgen durması onu tamamen gizlerdi. Küçük
resimler mağaza satırlarında kullanılmaya devam ediyor (`ShopUI`).

Denetçi artık önizleme prefabının çözüldüğünü de kontrol ediyor. Menü
yerleşimi düz panel kalktıktan sonra yeniden tarandı: 18 öğe, sıfır çakışma.

**Kalan sınır:** araç *modellerinin kendisi*. Bizimkiler prosedürel, kutumsu
gövdeler; referanstaki elle modellenmiş. Garaj mekânı artık kod işi, araç
modeli hâlâ varlık işi.

---

## 24) v1.6 — Faz 1: araç sözleşmeleri

Mimari belgesindeki 15 fazlık yol haritasının ilk fazı. Kod yazmadan önce
üzerinde anlaşılması gereken **arayüzler** burada.

### Neden arayüz yazmakla başlanıyor

Şartnamedeki sistemlerin çoğu aynı üç şeye dokunuyor: motor gücü, üst hız,
tutuş. Turbo da, lastik seti de, süspansiyon ayarı da, nitro da. Bunların
hepsi bugünkü yöntemle — bileşenin alanına doğrudan yazarak — eklenirse
**birbirlerini ezerler ve bu hiç hata basmaz.**

Bu artık varsayım değil, projede duran bir örnek: `CarNitro` üst hızı
`_car.topSpeedKmh` alanına yazıyor, bırakınca `Awake`'te okuduğu değere
geri döndürüyordu. Tek değiştirici varken doğru. Turbo yükseltmesi üst hızı
220'ye çıkardığı gün, o sırada nitroya basıp bırakan oyuncunun aracı
`Awake`'teki 180'e düşüyor ve **satın alınan turbo sessizce kayboluyor.**

### Eklenen sözleşmeler

| Dosya | Ne yapar |
|---|---|
| `Scripts/Car/IVehicleStats.cs` | Araçtan **okunan** her şey: hız, devir, vites, tork, yakıt, tekerlek kayması |
| `Scripts/Car/IVehicleAuthority.cs` | "Bu araç benim mi, bu sayıya güvenilir mi" — tek noktada |
| `Scripts/Car/VehicleStatSheet.cs` | İstatistik değiştiricileri: `(temel + Σ ekleme) × Π çarpan` |
| `Scripts/Customization/ICustomizationModule.cs` | Modifikasyon alt sistemi sözleşmesi + `ItemId` + `VehicleContext` |

`IDriveInput` (yazma tarafı) zaten vardı — sözleşme takımı artık tam:
**yaz** (`IDriveInput`), **oku** (`IVehicleStats`), **kime ait**
(`IVehicleAuthority`), **değiştir** (`VehicleStatSheet`).

### Uygulamalar

| Dosya | Ne yapar |
|---|---|
| `Scripts/Car/VehicleTelemetry.cs` | `IVehicleStats`'in tek uygulaması |
| `Scripts/Car/VehicleAuthority.cs` | `IVehicleAuthority`'nin tek uygulaması — Photon'a bakan tek araç bileşeni |

`VehicleTelemetry` sürücüden bağımsız: verinin tamamı WheelCollider'lardan,
`GearBox`'tan, `FuelSystem`'den ve `IDriveInput` arayüzünden geliyor. RCCP de
WheelCollider kullandığı için **aynı bileşen iki fizik sağlayıcısıyla da
çalışıyor** — ikinci bir uygulama yazmak gerekmedi.

`VehicleAuthority` bugün `IsServerVerified => false` döndürüyor. Bu bir
eksiklik değil, PUN 2'nin tasarımı: istemci otoriter. Faz 10'daki göçte
değişecek tek satır orası, ve o gün "hangi sayıya güveniyorduk" sorusu
grep'lenebilir olacak.

### Bağlanan yerler

Sözleşmeler yazılıp bırakılmadı — bu projenin baskın hata ailesi tam olarak
bu. Dört gerçek tüketici var:

- `CarController` → tork, üst hız, fren, direksiyon açısı, downforce, kütle ve
  tutuş artık **tablo üzerinden** hesaplanıyor. Alanların hiçbirine çalışma
  anında yazılmıyor.
- `CarNitro` → `topSpeedKmh` alanına yazmayı bıraktı, `"nitro"` kaynak adıyla
  tabloya değiştirici koyuyor. Bırakınca yalnızca kendi katkısı kalkıyor.
- `FuelSystem` → tüketim `VehicleStat.FuelDrain` üzerinden geçiyor; motor
  yükseltmeleri buraya yazacak.
- `DreamCarValidator` → her araç prefabında üç bileşenin varlığını denetliyor.
  Yoksa araç **çalışır ama sessizce eksilir** — denetçinin var olma sebebi.

Üç araç kurulum noktasının üçü de (`ProceduralCarGenerator`, `DreamCarSetup`,
`RCCPCarConverter`) yeni bileşenleri ekliyor.

### Dürüst sınır

Devir (`EngineRpm`) **gerçek bir motor eğrisi değil**: vites bandı içindeki
ilerlemeden türetiliyor. Otomatik şanzımanın davranışı zaten bu, gösterge ve
motor sesi için doğru sonucu veriyor — ama fizik kararı buna dayandırılmamalı.
Gerçek tork eğrisi Faz 3'te motor modeliyle gelecek ve tek noktadan değişecek.

---

## 25) v1.7 — Grafik: normal haritalar, gölge, gradyan ortam ışığı

Üç kusur bulundu; üçü de **hata basmıyordu, yalnızca düz görünüyordu.**

### 1. Hiçbir yüzeyin normal haritası yoktu

Asfalt, kaldırım, bina cephesi, çim, garaj zemini ve duvarı — hepsi düz
albedo dokusuydu. Asfaltın taneleri, kaldırımın derzi, cephedeki pencere
girintisi ve fayans fugası yalnızca **renk** farkıydı; ışık onlara hiç
tepki vermiyordu. SSAO, bloom ve ACES tonemapping açık olmasına rağmen
yüzeylerin plastik görünmesinin sebebi buydu.

Her doku için artık albedodan **normal haritası türetiliyor** (parlaklık =
yükseklik, Sobel). Örnekleme sarmalı yapılıyor, yoksa her döşeme sınırında
dikiş izi çıkardı. Cephenin normali gündüz dokusundan üretilip ikisine de
veriliyor — gece dokusunda pencereler ışıklı, parlaklığı yükseklik saymak
pencereleri dışarı çıkıntı yapardı.

Materyal tarafında kritik satır **`EnableKeyword("_NORMALMAP")`**: URP Lit
normal haritayı yalnızca bu keyword açıksa örnekliyor. Sadece `_BumpMap`'e
doku atamak hiçbir şey yapmaz — doku Inspector'da durur, hata basılmaz,
yüzey yine dümdüz render edilir.

Normal haritalar ada göre bulunuyor (`asphalt` → `asphalt_n`), bu yüzden
**mevcut materyal çağrılarının hiçbiri değişmedi.**

### 2. Game sahnesinde hiç gölge yoktu

`AddComponent<Light>()` ile kurulan ışığın `shadows` varsayılanı `None`.
Hiyerarşi menüsünden "Directional Light" eklendiğinde Unity bir preset
uyguluyor ve gölge açık geliyor; kodla eklenince gelmiyor.

Sahne kurulumu kodla yapıldığı için şehir bugüne kadar **tamamen gölgesiz**
render ediliyordu: araçlar zemine oturmuyor, binaların hiçbiri yere gölge
düşürmüyordu. Sekiz harita sahnesinde gölge vardı (`ProceduralMapGenerator`
o satırı içeriyordu), ana şehirde yoktu.

Denetçiye kalıcı bir kontrol eklendi: sahnedeki yönlü ışıkların hepsinin
gölgesi kapalıysa hata veriyor.

### 3. Ortam ışığı düzdü

`AmbientMode.Flat`'te gökyüzüne bakan yüzey de, yere bakan da, yandaki duvar
da aynı ortam ışığını alıyordu. Gölgeler vardı ama gölgesiz taraflar eşit
aydınlıktaydı; araç ve proplar zemine oturmuyor, her şey aynı düzlemde
yüzüyor gibi görünüyordu.

`Trilight`'a geçildi: yukarı bakan yüzeye gökyüzü rengi, aşağı bakana yerden
yansıyan koyu renk. **Maliyeti sıfır** — ikisi de tek bir küresel harmonik,
ek render geçişi yok. Araç kaportasının üstü ile altı arasındaki fark
buradan geliyor.

Menüde ayrıca ortam ışığı koyulaştırıldı: garaj kapalı bir mekân, aydınlatma
tavan şeritlerinden gelmeli. Varsayılan açık gri ortamda duvarlar ışık
kaynağı olmadan da aydınlanıyor ve mekân "içeride" görünmüyordu.

### Değişmeyen

Zaten doğru olanlara dokunulmadı: SSAO, HDR, MSAA 4x, ACES tonemapping,
HDR renk derecelendirme, 4 gölge kademesi, yansıma probları, kalite kademesi
başına post-processing profilleri. Bunlar önceki turlarda kurulmuştu.

---

## 26) v1.8 — Faz 5: araç modifikasyon sistemi

Dream Road tarzı bir oyunun can damarı. Altyapısı Faz 1'de kurulmuştu
(`ICustomizationModule`, `ItemId`, `VehicleContext`, `VehicleStatSheet`) ama
**hiçbir modül yoktu** — yani sözleşmelerin tüketicisi yoktu. Bu faz onu
kapatıyor.

### Bir düzeltme

Önceki turda "cam filmi var" demiştim. **Yoktu.** `WindowTint` diye bir tip
aramada yalnızca kendi yazdığım arayüz dosyasının yorumunda geçiyordu.
`Customization/` klasöründe boya, HDR boya ve plaka vardı; modifikasyon
diye bir sistem hiç yoktu.

### Bulunan ölü sistem: boya

`CarPaint` baştan beri projede duruyor — MaterialPropertyBlock'la boyayı
değiştiriyor, Photon üzerinden diğer oyunculara bildiriyor, PlayerPrefs'e
kaydediyor. Ama **`Apply()`'ı hiçbir arayüzden çağıran yok.** Oyuncunun
aracının rengini değiştirmesinin hiçbir yolu yoktu; `car.color` anahtarı
hiç yazılmadığı için `LoadFromPrefs` her zaman varsayılan kırmızıyı
döndürüyordu. Hata basmıyor, sadece hiçbir şey yapmıyor — bu projenin
baskın hata ailesi.

Boya artık modifikasyon ekranının bir slotu.

### Sistem

| Dosya | Görev |
|---|---|
| `Scripts/Economy/ModCatalog.cs` | Parça kataloğu (`ModItem`: slot, fiyat, renk, istatistik) |
| `Scripts/Customization/ModSave.cs` | Sahiplik (oyuncu başına) + takılı (ARAÇ başına) |
| `Scripts/Customization/CustomizationRuntime.cs` | Bir araç örneğine takılı modüllerin tamamı |
| `Scripts/Customization/CarCustomization.cs` | Araç üzerindeki tek bileşen: yükle, kaydet, ağa bildir |
| `Scripts/Customization/Modules/` | 11 modül |
| `Scripts/UI/ModShopUI.cs` | Modifikasyon ekranı |
| `Editor/Procedural/ProceduralModCatalog.cs` | 11 slot, 47 parça üretiyor |

**11 slot:** boya · cam filmi · jant · spoiler · neon · motor · turbo ·
egzoz · lastik · fren · süspansiyon.

### Üç karar ve nedenleri

**1. Modüller MonoBehaviour DEĞİL, düz C# sınıfı.**
Garaj önizlemesi `Preview_<id>.prefab` kullanıyor ve `SavePreviewPrefab`
o prefabdaki **bütün MonoBehaviour'ları atıyor**. Modüller bileşen olsaydı
menüde hiç çalışmazlardı: oyuncu cam filmi satın alır, garajda hiçbir şey
değişmezdi. Düz sınıf olunca aynı kod hem gerçek araçta hem önizlemede
koşuyor; araca erişim `VehicleContext` üzerinden.

**2. Kayıt araç başına.**
`mod.<carId>.<slot>`. İkinci aracını alan oyuncu birincinin cam filmiyle
görmemeli. `CarPaint`'in global anahtarı (`car.color`) tam bu sorunu
yaşıyordu; ona `externallyManaged` bayrağı eklendi — yönetici varsa kendi
yüklemesini yapmıyor, yoksa hangisinin kazandığı Unity'nin bileşen sırasına
kalırdı.

**3. Yeni geometri prefabda KAPALI çocuk olarak duruyor.**
Spoiler (3 varyant) ve neon şeridi `ProceduralCarGenerator` tarafından
prefaba kapalı olarak konuyor; modül yalnızca `SetActive` yapıyor. Çalışma
anında mesh üretmek her araç doğuşunda — odadaki her uzak oyuncu için de —
tahsisat demekti. Ayrıca `SavePreviewPrefab` bileşenleri siliyor ama
GameObject'leri değil, yani kapalı çocuklar önizlemede de duruyor.

### Ağ

Sahip tek bir Photon Custom Property yazıyor:
`car.mods = "tint=mod.tint.2;rim=mod.rim.1"`. Uzak istemci ayrıştırıp
**yalnızca görünen** modülleri uyguluyor — uzak araçta fizik simüle
edilmiyor, tork değiştirmenin karşılığı yok. Slot başına ayrı anahtar
yazmak yerine tek dize: hem ucuz hem atomik, yarım uygulanmış görünüm
oluşmuyor.

### Denge

Yükseltmelerin **bedeli** var: turbo yakıt tüketimini artırıyor
(`FuelDrain`), süspansiyon aracı alçaltıyor. Bedelsiz yükseltme ekonomiyi
anlamsızlaştırır — oyuncu her şeyi alır ve seçim kalmaz. Motor 5. seviyede
toplam +%45 tork; daha agresif değerler serbest sürüşü kontrol edilemez
yapıyor çünkü `CarController`'ın direksiyon kısma eğrisi 140 km/s'e ayarlı.

Her slotta ücretsiz bir "geri dön" seçeneği var (boyada "Fabrika"), yoksa
sade renge dönmek için tekrar ödemek gerekirdi.

### Denetçi

Katalog ile kod arasındaki sözleşme sessizce kopabiliyor. Dört yeni kontrol:

- `Resources/ModCatalog.asset` var ve boş değil
- Katalogdaki her slotun `CustomizationRuntime.Factories` içinde modülü var
  (yoksa o slottaki her parça satın alınır ve hiçbir şey yapmaz)
- `childName` alanı olan her ürün için araç prefabında o çocuk var
- Araç prefabında `CarCustomization` var

### Dürüst sınır

- **Body kit yok.** Gövde mesh'i `preset.sections`'tan loft ediliyor; body
  kit gerçek bir gövde varyantı demek ve prosedürel üreticinin yeniden
  yazılması olur. Ayrı bir faz.
- **Jantın şekli değişmiyor**, rengi ve yüzeyi değişiyor.
- **Önizlemede neon ışığı yok**, yalnızca emissive şerit —
  `SavePreviewPrefab` bütün `Light`'ları siliyor (menüde araç farlarının
  sahneyi yıkmaması için). Oyunda ışık havuzu var.
- **İstatistikler istemci tarafında.** `IsServerVerified` false; projenin
  geri kalanıyla (PlayerPrefs'teki para) aynı güven seviyesi. Sunucu
  otoriter ekonomi Faz 10.

---

## 27) v1.9 — Faz 3: sürüş yardımcıları (ABS · TC · ESP · diferansiyel · aero)

Mimari belgesindeki Faz 3. İki nedenle yapıldı.

### Faz 1'de kendi bıraktığım boşluğu kapatıyor

`IVehicleStats` / `VehicleTelemetry` (tekerlek başına `forwardSlip` /
`sidewaysSlip` veren okuma sözleşmesi) Faz 1'de **tam bu modüller okusun
diye** yazılmıştı — ama hiçbir tüketicisi yoktu. `TireScreechAudio`,
`DriftSmoke`, `WheelGlow` hepsi `WheelCollider.GetGroundHit`'i doğrudan
okuyordu, telemetriyi değil. Yani telemetri de "yazılmış, tam görünen, hiç
çağrılmayan" ailesindeydi. `DrivingAssists` ona ilk gerçek tüketicisini
veriyor.

### Sistem

| Dosya | Görev |
|---|---|
| `Scripts/Vehicle/DrivingAssists.cs` | ABS, TC, ESP, diferansiyel, aero — hepsi tek bileşende |
| `Scripts/Car/CarController.cs` | Aks döngüsünde per-wheel tork/fren modülasyonu |
| `Scripts/Car/VehicleTelemetry.cs` | `IndexOf(WheelCollider)` eklendi |
| `Scripts/Settings/GameSettings.cs` | `AbsEnabled` / `TractionControlEnabled` / `StabilityControlEnabled` |
| `Scripts/UI/SettingsScreen.cs` | Üç toggle + anlık tazeleme |
| `Scripts/UI/AssistTelltale.cs` | Müdahale anında yanan HUD göstergesi |

- **ABS**: tekerlek kilitleniyorsa (çok negatif `forwardSlip`) fren
  basıncını darbeli bırakıyor. El freni HARİÇ — drift el frenle yapılıyor,
  ABS onu bozmamalı.
- **TC**: çekiş tekerleği patinaj yapıyorsa motoru kısıyor.
- **ESP**: istenen yaw (bisiklet modeli) ile gerçek yaw'ı karşılaştırıp
  az dönüşte iç arka, aşırı dönüşte dış ön tekerleği frenliyor.
- **Diferansiyel**: LSD benzeri — patinaj yapan tekerleğe torku kısıp tutan
  tekerlekte bırakıyor.
- **Aero**: hız-kare orantılı sürükleme, yüksek hızda düz çizgi kararlılığı.

### CarController tek otorite kalıyor

`DrivingAssists` tekerleklere **doğrudan yazmıyor**. `CarController` her fizik
adımında `BeginStep`'i çağırıyor, sonra aks döngüsünde `ModulateMotor` /
`ModulateBrake`'ten geçiriyor, `EndStep` ile telltale bayraklarını topluyor.
FuelSystem'in `EngineCutoff` deseniyle aynı mantık: dış bileşen veri verir,
yazan CarController'dır. `DrivingAssists` yoksa davranış birebir eskisi.

### Görünürlük ve kontrol

- HUD'da kilometre saatinin altında **ABS/TC/ESP göstergesi** müdahale anında
  yanıyor — yardımcı sessizce çalışırsa oyuncu var olduğunu bilmez.
- Ayarlar ekranı **iki sütuna** yeniden yerleşti; sağ sütunda üç yardımcı
  toggle'ı. Değiştirilince sahnedeki araçlar anında tazeleniyor.
- Varsayılan **açık**: mobil dokunmatikte araç yardımsız kolay savruluyor.

### Denetçi

- CarController'lı araç prefabında `DrivingAssists` var mı (yoksa sessizce
  hiç çalışmaz).
- `DrivingAssists` için `IVehicleStats` (VehicleTelemetry) var mı.

### Dürüst sınır

- **Yalnızca kendi `CarController`'ımızda.** RCCP kendi ABS/TC/ESP'ini
  getiriyor; RCCP'li araçta `DrivingAssists` Awake'te kendini kapatıyor
  (reflection ile güvenilir per-wheel tork müdahalesi yapılamaz).
- **Simülasyon sınıfı fizik değil**: WheelCollider tabanlı, eşikli bir
  model — mobil ve Dream Road için doğru his, gerçek bir ECU/darbe frekansı
  modeli değil.
- **ESP tek tekerlek frenlemesiyle**; gerçek ESP motor torkunu da yönetir,
  burada onu TC ile bölüşüyor.

---

## Dosya haritası

| Dosya | Görev |
|---|---|
| `Scripts/Car/CarController.cs` | WheelCollider tabanlı fizik |
| `Scripts/Vehicle/DrivingAssists.cs` | ABS · TC · ESP · diferansiyel · aero |
| `Scripts/UI/AssistTelltale.cs` | Sürüş yardımcısı HUD göstergesi |
| `Scripts/Economy/ModCatalog.cs` | Modifikasyon parça kataloğu |
| `Scripts/Customization/ModSave.cs` | Parça sahipliği + araç başına takılı parçalar |
| `Scripts/Customization/CustomizationRuntime.cs` | Bir araca takılı modüllerin tamamı |
| `Scripts/Customization/CarCustomization.cs` | Araç üzerindeki modifikasyon yöneticisi |
| `Scripts/Customization/Modules/` | 11 modifikasyon modülü |
| `Scripts/UI/ModShopUI.cs` | Modifikasyon ekranı (canlı garaj önizlemesi) |
| `Scripts/Car/IVehicleStats.cs` | Araçtan okunan telemetri sözleşmesi |
| `Scripts/Car/IVehicleAuthority.cs` | Sahiplik ve sunucu güveni sözleşmesi |
| `Scripts/Car/VehicleStatSheet.cs` | İstatistik değiştiricileri (nitro, turbo, lastik) |
| `Scripts/Car/VehicleTelemetry.cs` | `IVehicleStats` uygulaması — RCCP ile de çalışır |
| `Scripts/Car/VehicleAuthority.cs` | `IVehicleAuthority` uygulaması — Photon'a bakan tek araç bileşeni |
| `Scripts/Customization/ICustomizationModule.cs` | Modifikasyon modülü sözleşmesi + `ItemId` |
| `Scripts/Car/CarCameraFollow.cs` | Kamera takibi |
| `Scripts/Car/CarNetworkSync.cs` | Photon position/rotation sync |
| `Scripts/Vehicle/CarRescue.cs` | Takla / düşme / yakıtsızlıktan kurtarma (§18c) |
| `Scripts/Audio/ProceduralMusic.cs` | Çalışma anında müzik sentezi (§18e) |
| `Editor/DreamCarValidator.cs` | Sahne kablolama denetimi (§18f) |
| `Editor/CI/DreamCarCI.cs` | Batch-mode üretim + build (§19a) |
| `Editor/Procedural/ProceduralCarThumbnails.cs` | Araç küçük resimleri (§22) |
| `Editor/Procedural/ProceduralGarage.cs` | Ana menü garaj sahnesi (§23) |
| `Editor/DreamCarPlayFabSetup.cs` | PLAYFAB_INSTALLED define'ı (§19c) |
| `Editor/iOS/DreamCarIOSPostBuild.cs` | Info.plist + gizlilik manifesti (§20) |
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
| `Scripts/GameModes/FreeRoamMode.cs` | Serbest sürüş + km/drift başına kazanç (§18a) |
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
- **Photon App Id oluştururken SDK seçimi**: "Select Photon SDK" mutlaka **PUN**, sürüm **PUN 2** olmalı. Varsayılan olarak **Fusion** seçili gelir ve Fusion App Id'si PUN ile ÇALIŞMAZ — bağlantı sessizce kurulmaz.
- **Paket sürümleri ve Unity 6.5+**: Unity 6.5'te `TreeView` / `TreeViewItem` / `TreeViewState` API'leri kullanımdan kaldırılıp **hata** seviyesine (CS0619) çıkarıldı. Bu, eski sürüme sabitlenmiş Unity paketlerini kırıyor — projeyi 6000.6 ile açtığımızda sırayla `com.unity.collab-proxy` (180 hata) ve `com.unity.inputsystem` 1.11.2 (88 hata) patladı. Manifest'teki sürümler bu yüzden güncel tutulmalı. Input System'de düzeltmeler 1.15.0 ve 1.18.0'da geldi; manifest 1.20.0'a sabitli.
  Yüzlerce `CS0619` hatası görürsen ve yol `Library/PackageCache/...` ile başlıyorsa hata **senin kodunda değil**: ilgili paketi `Window → Package Manager` üzerinden güncelle, sonra `Packages/manifest.json`'daki yeni sürümü depoya işle.
- **`CS0246: The type or namespace name 'InputAction' could not be found`** (RCCP_InputManager.cs): RCCP yeni Input System'i zorunlu kılıyor. `com.unity.inputsystem` manifest'e eklendi.
  **Bunun kritik bir yan etkisi var:** projede iki girdi API'si yan yana çalışıyor — RCCP yeni sistemi, bizim kodumuz (dokunmatik sürüş, duraklatma, kamera) eski Input Manager'ı kullanıyor. `Edit → Project Settings → Player → Other Settings → **Active Input Handling** → **Both**` olmak zorunda. Yalnızca "New" seçilirse `Input.GetTouch` çağrılarımız çalışma anında istisna atar ve dokunmatik sürüş tamamen ölür. `BUILD EVERYTHING` bunu otomatik ayarlıyor, ama etkinleşmesi için Unity'nin yeniden başlatılması gerekiyor.
- **Yüzlerce `CS0619: 'TreeView' is obsolete` hatası, hepsi `Library/PackageCache/com.unity.collab-proxy/.../PlasticSCM/`**: Unity Version Control (Plastic SCM) paketi Unity 6000.6 ile uyumsuz. Bu proje Git kullanıyor, o paket hiç gerekmiyor — `Packages/manifest.json`'dan kaldırıldı. Kendi projende hâlâ varsa `Window → Package Manager → In Project → Version Control → Remove`.
  Aynı temizlikte kaldırılan diğer kullanılmayan paketler: `com.unity.postprocessing` (PPv2 — biz URP'nin Volume sistemini kullanıyoruz, ikisi bir arada çakışma kaynağı), `com.unity.cinemachine`, `com.unity.timeline`, `com.unity.visualscripting`. Beşinin de kodda sıfır kullanımı vardı.
- **"The name 'ScreenCapture' does not exist in the current context"** (RCCP_PhotoMode.cs): `com.unity.modules.screencapture` built-in modülü kapalı. Depodaki `Packages/manifest.json`'da artık ekli; eski bir klondaysan `Window → Package Manager → Packages: Built-in → Screen Capture → Enable`.
  Genel kural: Asset Store asset'leri (RCCP gibi `Assets/` altına düz `.cs` olarak gelenler) ihtiyaç duydukları built-in modülü bildiremez — UPM paketlerinin aksine. Bir asset `CS0103: The name 'X' does not exist` veriyorsa önce ilgili modülün açık olup olmadığına bak.
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
