# Değişiklik Günlüğü

Bu proje [Semantic Versioning](https://semver.org/lang/tr/) benzeri bir sürümleme kullanır.
Oyun henüz `0.x` — yani API'ler kırılabilir.

---

## [0.9.0] — İlerleme: sürücü seviyesi ve günlük görevler

### Eklendi
- **Sürücü seviyesi (XP)**: XP oyuncunun ömür-boyu istatistiklerinden
  türetiliyor (mesafe, para, yarış, drift, süre) — her ödül noktasına ayrı
  kanca yok. Menüde seviye rozeti + XP çubuğu, seviye atlama ödülü ve toast.
- **Günlük görevler**: her gün 3 görev (mesafe/yarış/para/süre), güne göre
  deterministik. İlerleme stat deltalarından; tamamlanınca para + bonus XP.
- **Denetçi kontrolleri**: ana menüde DriverProfile/MissionSystem zorunlu.

---

## [0.8.0] — Oynanış: araç sözleşmeleri, modifikasyon, sürüş yardımcıları

0.7.0'dan bu yana eklenen oyuncu-görünür sistemler. Ayrıntılı gerekçeler
README'nin 24–27. bölümlerinde.

### Eklendi
- **Araç sözleşmeleri (Faz 1)**: `IVehicleStats` (okuma), `IVehicleAuthority`
  (sahiplik), `VehicleStatSheet` (istatistik değiştiriciler). Nitro artık üst
  hızı doğrudan alana yazmıyor; turbo/lastik gibi ikinci bir değiştirici
  geldiğinde birbirini ezmiyor.
- **Modifikasyon sistemi (Faz 5)**: 11 slot, 47 parça — boya, cam filmi, jant,
  spoiler, neon, motor, turbo, egzoz, lastik, fren, süspansiyon. Ana menüde
  "Modifiye" ekranı, garajda canlı önizleme. Kayıt araç başına; görsel modlar
  ağ üzerinden diğer oyunculara yansıyor. Boya bugüne kadar hiç çağrılmayan
  ölü bir sistemdi; artık bir slot.
- **Sürüş yardımcıları (Faz 3)**: ABS, patinaj denetimi, ESP, diferansiyel,
  aero. Ayarlardan açılıp kapanıyor, HUD'da müdahale göstergesi. Faz 1'de
  yazılıp tüketicisiz kalan `VehicleTelemetry`'nin tüketicisi.
- **Grafik**: her yüzeye normal harita, Game sahnesinde eksik olan gölgeler,
  düz yerine gradyan ortam ışığı.

### Düzeltildi
- `CarPaint.Apply` hiçbir arayüzden çağrılmıyordu — araç rengi
  değiştirilemiyordu.
- Game sahnesinde yönlü ışığın gölgesi kapalıydı (kodla eklenen ışığın
  varsayılanı `None`); şehir gölgesiz render ediliyordu.
- Yüzeylerin normal haritası yoktu; SSAO/bloom açıkken bile her şey plastik
  görünüyordu.

---

## [0.7.0] — Prosedürel varlıklar, native iOS, testler

Bu sürüme kadar oyun kod olarak hazırdı ama görsel/işitsel varlık yoktu; sahne
gri küplerden ibaretti. Artık her şey kodla üretiliyor.

### Eklendi
- **Prosedürel araç üretici** (`Editor/Procedural/ProceduralCarGenerator.cs`)
  5 gövde tipi (Sedan, Hatchback, Sport Coupe, SUV, Pickup). Gövde, uzunluk boyunca
  tanımlı kesitlerin loft edilmesiyle üretilir — kaput eğimi, kabin yükselmesi,
  bagaj düşüşü gerçek bir siluet oluşturur. Tekerlek mesh'i lastik + jant + 5 kol
  içerir. Her prefab tüm oyun bileşenleriyle (fizik, ağ, ışık, ses, hasar, boya)
  bağlı olarak çıkar.
- **Prosedürel mesh kütüphanesi** (`Editor/Procedural/MeshBuilder.cs`)
  Loft, superellipse kesit, silindir, disk, kutu ilkelleri.
- **Prosedürel texture ve materyaller** (`Editor/Procedural/ProceduralTextures.cs`)
  Asfalt, kaldırım, bina cephesi (gündüz/gece pencere varyantı), yol çizgisi, çim.
  Materyaller URP ve Built-in pipeline'ın ikisinde de çalışır.
- **Prosedürel şehir üretici** (`Editor/Procedural/ProceduralCityGenerator.cs`)
  6x6 blok ızgara, yollar, kaldırımlar, merkeze doğru yükselen bina silueti,
  sokak lambaları, trafik waypoint halkası, 8 yarış checkpoint'i, 16 spawn
  noktası, çalışır benzin istasyonu.
- **Prosedürel UI sprite'ları** (`Editor/Procedural/ProceduralUISprites.cs`)
  9-slice yuvarlak panel, pill buton, daire, halka, gradyan, chevron, dişli,
  kupa ve damalı bayrak ikonları.
- **Sentezlenmiş ses** (`Scripts/Audio/ProceduralEngineAudio.cs`)
  Motor sesi additive synthesis ile üretilir: harmonik serisi + silindir sayısına
  bağlı patlama zarfı + filtrelenmiş gürültü. Lastik çığlığı bant geçirgen
  gürültü, korna iki kare dalga. Klipler kusursuz döngü için tam sayıda çevrim içerir.
- **iOS native köprü** (`Plugins/iOS/DreamCarNative.mm`)
  Taptic Engine haptikleri (impact/notification/selection), App Tracking
  Transparency izni, düşük güç modu ve termal durum okuma.
- **Saf mantık katmanı** (`Scripts/Util/GameMath.cs` + asmdef)
  Süre/mesafe biçimleme, streak çarpanı, tamir ve yakıt fiyatı, vites seçimi,
  kilometre saati açısı, hile tespiti eşikleri, token bucket, kalite kademesi,
  rich-text kırpma. Unity paketlerine bağlı değil.
- **EditMode testleri** (`Tests/EditMode/GameMathTests.cs`)
  40+ test. CI'daki `compile-check` job'u artık gerçekten bir şey doğruluyor.
- `LICENSE` (MIT) ve bu `CHANGELOG.md`.

### Değişti
- `LeaderboardScreen`, `StatsScreen`, `LoginStreak`, `RepairPanel`,
  `RefuelStationPanel`, `GearBox`, `SpeedometerNeedle`, `CheatDetector`,
  `QualityAutoDetect`, `RichChatUI` artık kendi kopya mantıkları yerine
  `GameMath` çağırıyor — testler bu yüzden gerçek kod yollarını doğruluyor.

---

## [0.6.0] — Kritik altyapı, bulut, moderasyon

### Eklendi
- **Yeniden bağlanma** (`Network/ReconnectionManager.cs`) — mobilde kopan
  bağlantıda üstel geri çekilme ile `ReconnectAndRejoin`; bilerek çıkış ayrımı.
- **Eksik 5 ekran** — Ayarlar, Liderlik, Başarımlar, Coin Mağazası, İstatistik.
  Backend'leri vardı, paneli yoktu.
- **İstatistik takibi** — `Core/PlayerStats.cs` + `Core/StatsTracker.cs`.
- **Müzik sistemi** — `Audio/MusicManager.cs`, çift kaynak crossfade playlist.
- **Loading screen** — progress + dönen ipuçları.
- **Object pooling** — `Core/ObjectPool.cs`, `TrafficSpawner` buna geçti.
- **Crash reporting** — `Backend/CrashReporter.cs`, 40 satır breadcrumb.
- **Tam profil cloud save** — `Backend/PlayFabCloudSave.cs`. Garaj, plaka, boya,
  ayarlar, streak, istatistikler. Araçlarda birleşim, istatistiklerde max stratejisi.
- **Voice HUD** + oyuncu bazlı yerel susturma.
- **Push + yerel bildirim** — FCM ve günlük ödül hatırlatıcısı.
- **Localization JSON'a taşındı** — `Resources/Localization/{tr,en}.json`, ~90 anahtar.
- **CAS ad mediation** — `AdsManager` önce CAS'ı dener, sonra Unity Ads'e düşer.
- **Chat spam koruması** — token bucket + tekrar tespiti + kademeli susturma.
- **Hile tespiti** — NaN pozisyon, teleport, imkânsız hız.
- **Ağ interest management** — mesafeye göre serialization rate ve renderer culling.
- **Cihaz uyarlama** — RAM/VRAM/çekirdek puanına göre kalite ve render scale.
- **Remote config** — PlayFab TitleData, offline cache'li.
- **Deep link** — `dreamcar://room/...` ve `ref/...`, paylaşım linki üretimi.
- **Haptik** — 7 stil, çarpma şiddetine göre otomatik seviye.
- **Bölge seçimi** — 10 Photon bölgesi, kalıcı seçim.
- Analytics kapsamı 4 çağrıdan 12+ event'e çıktı.

---

## [0.5.0] — Editor wizard, refuel UI, CI

### Eklendi
- **Setup wizard** (`Editor/DreamCarSetup.cs`) — Car prefab, MainMenu ve Game
  sahnelerini tüm bileşenler bağlı olarak tek tıkla üretir.
- **Refuel UI** — HUD yakıt barı, istasyon paneli, ödeme akışı.
- **GitHub Actions** — Linux'ta derleme kontrolü, self-hosted macOS'ta iOS build
  ve `.ipa` artifact. Repo variable'ları ile kapalı başlar.

---

## [0.4.0] — Görsel eksikler, yayın esasları, sosyal

### Eklendi
- Sinyaller + dörtlü flaşör, uzun huzme, dikiz aynası, cam sileceği, tamir paneli.
- Pause menü, günlük ödül + streak, ban listesi, şikayet, küfür filtresi.
- PlayFab başarımlar, davet kodu, arkadaş listesi, beraber oynananlar.
- Gizlilik politikası ekranı, destek e-postası, mağaza puanlama popup'ı.

---

## [0.3.0] — RCCP köprüsü, oyun modları, haritalar, PlayFab

### Eklendi
- `IDriveInput` arayüzü — fizik motoru değiştirilebilir hale geldi.
- RCCP köprü katmanı (5 script), define ile korumalı.
- Oyun modları: Free Roam, Race, Drift.
- Harita sistemi: tek sahne + weather/time-of-day varyantları.
- Genişletilmiş oda oluşturucu (mod + harita + şifre + görünürlük).
- PlayFab: kimlik, para senkronu, liderlik tablosu, sunucu doğrulamalı satın alma.
- Cruise control, kokpit kamerası, trafik spawner, HDR boya, iki parçalı plaka,
  müzikal korna, zengin sohbet, garaj karuseli.

---

## [0.2.0] — Ekonomi, yarış, sosyal temel

### Eklendi
- Para, araç envanteri, katalog, mağaza.
- Şifreli oda (`pWd` custom property), oyuncu listesi, kick.
- Plaka, yarış checkpoint sistemi, drift skoru, liderlik tablosu.
- Voice scaffold, emote, korna.
- IAP, reklam, analytics, KVKK onayı, tutorial.
- Kamera modları, hava durumu, minimap, ping, toast bildirimleri.
- Vites, yakıt, benzin istasyonu, hasar.
- TR/EN yerelleştirme, oyun ayarları.

---

## [0.1.0] — İlk iskelet

### Eklendi
- WheelCollider tabanlı araç fiziği, kamera takibi.
- Photon PUN 2 lobi, oda, pozisyon senkronu.
- Sohbet, takma ad, mobil dokunmatik kontroller.
- URP + post-processing, balata kızarması, nitro, drift dumanı, farlar,
  motor sesi, gündüz/gece döngüsü, trafik AI'ı, araç boyası.
- iOS build hedefi ve üç dağıtım yolu (Xcode 7-gün, TestFlight, sideload).
