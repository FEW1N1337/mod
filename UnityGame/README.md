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

## 6) Sonraki adımlar (bu iskelette YOK)

- Ekonomi (para, garaj) — `Assets/Scripts/Economy/PlayerMoney.cs`
- Araba özelleştirme (renk, plaka)
- Voice chat (Photon Voice)
- Birden fazla harita
- Anti-cheat (Photon sunucu tarafı validation)
- Grafik iyileştirme (URP, post-processing)

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
| `Scripts/Game/GameBootstrap.cs` | Her sahnede setup |

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
