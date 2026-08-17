# DreamCar — Manuel Test Rehberi

Bu belge 85 script'i Unity Editor'de + iOS cihazda adım adım doğrulamak için. Yayına çıkmadan önce tüm bölümleri tik atmalısın. Hata bulursan `README.md § Sorun giderme`'ye bak.

**Test ortamı**:
- Mac (Xcode kurulu, gerçek cihaz varsa idealdir)
- Unity Hub + Unity 6 LTS + iOS Build Support
- PhotonAppId (PUN 2 dashboard)
- Opsiyonel: PlayFab Title ID, RCCP asset

**Renk kodu**:
- 🔴 = kritik (yayın engelleyici)
- 🟡 = önemli (kullanıcı fark eder)
- 🟢 = nice-to-have

---

## Bölüm 0 — Kurulum smoke testi

Her sıfır durumda 1 kez.

- [ ] 🔴 Unity Hub → Add Project → `UnityGame/` klasörünü aç. Import bitene kadar bekle (~2-5 dk).
- [ ] 🔴 Konsolda **kırmızı hata yok**. Uyarılar (sarı) tolerable.
- [ ] 🔴 Package Manager → PUN 2 - FREE Asset Store'dan import edilmiş.
- [ ] 🔴 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` → **App Id PUN** dolu.
- [ ] 🟡 PlayFab SDK Asset Store'dan import + `PlayFabAuth.titleId` dolu + Scripting Define'a `PLAYFAB_INSTALLED` eklendi.
- [ ] 🟢 RCCP import + Scripting Define'a `RCCP_INSTALLED` eklendi.

Sonuç doğrulama:
```
Editor → Play → Console'da:
[Photon] Connected to master. Region=eu
[PlayFab] Login OK: <ID>   (sadece PlayFab kuruluysa)
```

---

## Bölüm 1 — MainMenu

MainMenu sahnesini aç, Play'e bas.

- [ ] 🔴 KVKKConsent popup ilk açılışta çıkıyor. "Kabul" → bir daha çıkmıyor.
- [ ] 🔴 Nickname input çalışıyor, boşsa `PlayerXXXX` otomatik.
- [ ] 🔴 Status "Connecting..." → "Online" (max 5 sn).
- [ ] 🔴 "Play" butonu Online olunca aktif.
- [ ] 🟡 Tutorial ilk açılışta çıkıyor, "İleri" butonuyla adımlar geçiyor, sonda gizleniyor.
- [ ] 🟡 DailyReward popup ilk açılışta çıkıyor + `+500 ₺` toast'u geliyor.
- [ ] 🟡 Ertesi gün (test için: sistem saatini +1 gün) → streak `2. gün` yazıyor.
- [ ] 🟢 GarageCarousel — sol/sağ ok araç değiştiriyor, seçili araç thumbnail + isim güncel. Turntable dönüşü var.
- [ ] 🟢 Referral kodun görünüyor (8 karakter). "Kod Kullan" alanına başka kod gir → toast çıkıyor.

---

## Bölüm 2 — Oda oluşturma (Advanced Room Creator)

- [ ] 🔴 "Oda Kur" → RoomCreatorUI paneli açılıyor.
- [ ] 🔴 Oda adı boş bırakılabilir (`Room-XXXX` otomatik).
- [ ] 🔴 Şifre isteğe bağlı.
- [ ] 🟡 Mod dropdown 3 seçenek: Free Roam / Race / Drift.
- [ ] 🟡 Harita dropdown MapCatalog'dan doldu (en az 3 varyant: City / City Night / City Rainy).
- [ ] 🟡 Max oyuncu slider 2-16 arası, label "N oyuncu" güncelleniyor.
- [ ] 🟡 Visible toggle default: on.
- [ ] 🔴 Create → Game sahnesine geçiliyor, araç spawn oluyor.

---

## Bölüm 3 — Sürüş fiziği (single player)

Game sahnesinde:

- [ ] 🔴 Klavye: W ileri, S geri, A/D dön, Space handbrake — hepsi çalışıyor.
- [ ] 🟡 Mobil emülasyon: UI throttle/brake butonları basılı tutulunca çalışıyor. Ekran sol yarısında touch drag → direksiyon.
- [ ] 🔴 Kamera araba arkasından takip ediyor, dönüşte smooth.
- [ ] 🟡 Hız HUD (`InGameHUD`) doğru km/h gösteriyor.
- [ ] 🟡 SpeedometerNeedle iğnesi dönüyor.
- [ ] 🟡 GearBox otomatik vites: dur→N, geri hareket→R, hızlanınca 1→2→3...
- [ ] 🟡 FuelSystem yakıt her sn azalıyor. Gaz basınca daha hızlı azalıyor.
- [ ] 🟡 RefuelStation trigger'a girince "Depo dolduruldu" toast + para düşüyor.

RCCP moduysa (`RCCP_INSTALLED` define):
- [ ] 🟢 RCCPCarAdapter aktif → `_rccp.overrideInputs=true` (Debugger'da doğrula).
- [ ] 🟢 Sürüş hissi RCCP fiziği (daha ağır/gerçekçi).

---

## Bölüm 4 — Görsel efektler

Sürerken:

- [ ] 🟡 Nitro butonu basılı tut → NitroBar boşalıyor, egzoz alevi çıkıyor, +boost hız artıyor.
- [ ] 🟡 WheelGlow: sert fren + drift → balata kızıla döner (Bloom açıksa parlar).
- [ ] 🟡 DriftSmoke: sürtünme başlayınca lastikten duman çıkar, skid trail iz bırakır.
- [ ] 🟡 TireScreechAudio: kaymada ses.
- [ ] 🟡 EngineAudio: gaz basınca pitch yükselir.
- [ ] 🟡 HeadlightController: DayNightCycle gece'ye girince farlar otomatik yanar.
- [ ] 🟡 HighBeamController: `H` tuşu → far uzun huzme + range 2x.
- [ ] 🟡 TurnSignals: sol/sağ sinyal butonu → 0.5 sn aralıklarla blink. Aynı odadaki 2. instance'ta da yanıp söner (RPC sync).
- [ ] 🟡 Hazard (dörtlü) butonu → ikisi birden.
- [ ] 🟡 CarPaintHDR: garage'da emissive toggle → aracın boyası glow verir (Bloom).
- [ ] 🟢 CarPaintHDR rainbow modu → renk sürekli değişir.
- [ ] 🟢 LicensePlate/SplitLicensePlate: plaka texture oluşuyor. Split modda 2 parça (34 | ABC 123).

---

## Bölüm 5 — Kamera modları

- [ ] 🟡 `V` tuşu (veya UI buton) → Chase → Hood → Bumper → Interior → Free → Cinematic → tekrar Chase.
- [ ] 🟡 Interior modunda direksiyon steer input'una göre döner, kokpit UI görünür.
- [ ] 🟡 Free modda Mouse X ile aracın etrafında dön.
- [ ] 🟡 Cinematic modda kamera aracın etrafında yavaş orbital dönüş.
- [ ] 🟡 RearViewMirror: sağ üst köşede küçük ayna → arka manzarayı gösteriyor.

---

## Bölüm 6 — Hava ve zaman

- [ ] 🟡 DayNightCycle: 10 dk (default dayLengthSeconds) içinde gün-gece geçişi. Güneş dönüyor.
- [ ] 🟡 Weather.SetType(Rain) çağrısıyla yağmur partikülleri başlıyor.
- [ ] 🟡 WindshieldWipers: Rain aktifken otomatik başlıyor (level 2). Clear'da duruyor.
- [ ] 🟡 MapSelector: RoomCreator'dan `City Night` seç → sahne yüklenince güneş -0.5Y, ambient koyu (TimeOfDayPreset uygulandı).
- [ ] 🟡 `City Rainy` seçilince Weather.Rain otomatik + wipers otomatik başlar.

---

## Bölüm 7 — Yarış modu

RoomCreator → Mod=Race → oluştur.

- [ ] 🔴 Sahne yüklenince "3", "2", "1", "GO!" toast'ları geliyor.
- [ ] 🔴 Sahnede en az 3 Checkpoint + 1 FinishLine (isFinishLine=true) prefab konumlanmış olmalı.
- [ ] 🔴 Checkpoint'leri sırayla geç → sıradaki numaraya atlamadıkça sonraki sayılmaz.
- [ ] 🔴 FinishLine geçince lap sayacı +1, LeaderboardUI güncellenir.
- [ ] 🔴 3 tur bitince (`totalLaps=3`) "kazandın +1000 ₺" toast + `PlayerMoney` +1000.
- [ ] 🟡 PlayFab Achievements: ilk yarış → "İlk Zafer" unlock toast + PlayFab dashboard `raceWins` +1.
- [ ] 🟡 RateAppPopup: 5. yarış sonunda popup çıkıyor.

---

## Bölüm 8 — Drift modu

RoomCreator → Mod=Drift → oluştur.

- [ ] 🔴 3 dakika timer başlıyor.
- [ ] 🟡 Handbrake ile kay → skor artıyor (DriftScore.Current gösterilebilir HUD'a bağlanırsa).
- [ ] 🟡 Grip'e girince skor bank'a eklenir, OnCombo tetiklenir.
- [ ] 🟡 Bank 1000 geçince PlayFabAchievements `driftScore` update.
- [ ] 🔴 3 dk bitince "Drift bitti: N puan → +M ₺" toast + PlayerMoney artar.

---

## Bölüm 9 — Free Roam modu

RoomCreator → Mod=Free → oluştur.

- [ ] 🔴 Kural yok, timer yok, spawn olur, sürersin. Console'da hata yok.

---

## Bölüm 10 — Multiplayer (2 instance)

Bir tane Editor'de Play, bir tane File → Build → macOS/Windows standalone → çalıştır. İkisi de MainMenu'de aynı Photon region'a bağlansın.

- [ ] 🔴 Instance A oda kurar → Instance B'nin LobbyUI'sında oda ismi görünür.
- [ ] 🟡 Şifreli oda kur → B tarafında satırda kilit ikonu (LobbyUI'da RoomPassword.IsPasswordProtected check).
- [ ] 🔴 B odaya katılır → aynı Game sahnesine geçer.
- [ ] 🔴 İkisi de birbirinin aracını görür (CarNetworkSync).
- [ ] 🟡 Sürüş smooth interpolate olur (titreme yok — `interpSpeed` doğru).
- [ ] 🔴 Chat: A yaz → B görür. Emoji token (`:grin:` gibi) sprite'a dönüşür (sprite atlas varsa).
- [ ] 🟡 Küfür yaz → `****` olarak gider.
- [ ] 🟡 A boya değiştirir → B'de de değişir (Photon custom properties).
- [ ] 🟡 A plaka değiştirir → B'de de değişir.
- [ ] 🟡 A nitro basar → B tarafında da alev/hız değişimi görünür.
- [ ] 🟡 A sinyal atar → B'de blink görünür.
- [ ] 🟡 A korna basar → B'de ses çıkar (aynı odada).
- [ ] 🟡 PlayerListPanel: her iki client'ta ikisi de listelenir, master ★ ikonu görünür.
- [ ] 🔴 A (master) B'yi kick eder → B odadan atılır.
- [ ] 🟡 A B'yi ban'ler → B tekrar odaya girmeye çalışınca otomatik atılır.
- [ ] 🟡 ReportPlayer: A B'yi rapor eder → PlayFab dashboard'da event görünür.
- [ ] 🟡 PlayedWithList: A odadan çıkınca ana menüde B görünür + "Arkadaş Ekle" butonu.

---

## Bölüm 11 — Ekonomi & garaj & mağaza

- [ ] 🔴 PlayerMoney: yarış kazan → +1000. HUD'da güncellenir.
- [ ] 🔴 CarInventory: default araç sahip. ShopUI'de diğer araçlar "Satın Al" butonu.
- [ ] 🔴 Yeterli para varsa "Satın Al" → araç owned olur, para düşer.
- [ ] 🔴 Satın alınan araç GarageCarousel'de görünür, "Seç" → aktif olur.
- [ ] 🟡 PlayFab varsa: money PlayFab UserData'ya push edilir (2 sn debounce).
- [ ] 🟡 PlayFab varsa: PlayFab dashboard'dan money manuel değiştir → uygulama yeniden açılınca değer aktarılır.
- [ ] 🟡 Server-authoritative satın alma: PlayFabInventoryBridge.useServerAuthoritativePurchase=true → satın alma CloudScript üstünden validate edilir. Client PlayerData'yı doğrudan değiştirerek hile denenemez.

---

## Bölüm 12 — Hasar & tamir

- [ ] 🟡 Yüksek hızda duvara çarp → CarDamage.health azalır.
- [ ] 🟡 Duman particle çıkmaya başlar.
- [ ] 🟡 RepairPanel: hasar bar %'sini gösterir, fiyat "500 ₺" gibi bir değer.
- [ ] 🟡 Tamir Et → onay → para düşer, health = maxHealth, duman durur.
- [ ] 🟢 RCCP_Damage ile: gerçek mesh deformation görünür (BoneCracker demo aracı testi).
- [ ] 🟢 RCCPDetachableBridge: health<30 iken bumper/kapı düşer (RCCP_DetachablePart component'i varsa).

---

## Bölüm 13 — Pause menu

- [ ] 🔴 `Esc` → panel açılır, oyun donar (`Time.timeScale=0`).
- [ ] 🔴 "Devam" → panel kapanır, oyun akmaya devam eder.
- [ ] 🟡 "Ayarlar" → GameSettings paneli açılır (volume, quality, sensitivity).
- [ ] 🟡 "Odadan Çık" → PhotonNetwork.LeaveRoom.
- [ ] 🔴 "Ana Menü" → MainMenu sahnesine döner.

---

## Bölüm 14 — Achievements + Referral + Friends

- [ ] 🟡 Achievements: AchievementCatalog'da tanımlı bir başarımın koşulunu sağla → toast + `+moneyReward` para.
- [ ] 🟡 Referral: MainMenu'de kendi kodun görünür. İkinci hesap aç, o kodu gir → iki tarafa da toast + para.
- [ ] 🟡 Friends: PlayFab dashboard'dan iki hesap → birinden diğerini `AddByPlayFabId` çağır → friend listede görünür.
- [ ] 🟢 Friend odaya girince "arkadaşın <isim> <oda>'ya girdi" bildirim (henüz kod yok, v0.5).

---

## Bölüm 15 — Monetizasyon (IAP + Ads)

**Unity IAP test modu**:
- [ ] 🟡 Package Manager → In App Purchasing import + Services aktif.
- [ ] 🟡 UNITY_PURCHASING define eklenmiş.
- [ ] 🟡 Store butonu → "50k coin al" → test purchase → PlayerMoney +50000.

**Unity Ads test modu**:
- [ ] 🟡 Advertisement Legacy paketi import + Services aktif + testMode=true.
- [ ] 🟡 "Reklam izle → 5000 ₺ al" butonu → test video → PlayerMoney +5000.

---

## Bölüm 16 — Ayarlar & lokalizasyon

- [ ] 🟡 GameSettings: Kalite dropdown → hemen QualitySettings.SetQualityLevel çağrılır.
- [ ] 🟡 FPS input 30/60/120 → Application.targetFrameRate güncellenir.
- [ ] 🟡 Master/Music/SFX slider → AudioMixer parametreleri değişir.
- [ ] 🟡 Sensitivity slider → PlayerPrefs kaydolur.
- [ ] 🟡 LocalizationManager: dil = TR → tüm LocalizedText komponentleri Türkçe.
- [ ] 🟡 Dil değiştir → EN → hepsi anında değişir.

---

## Bölüm 17 — Yayın metadata

- [ ] 🔴 KVKKConsent → "Gizlilik Politikası" butonu → PrivacyPolicyScreen açılır (URL veya inline metin).
- [ ] 🔴 Ayarlar → "Destek" → mailto: link açılır, konu doğru dolmuş.
- [ ] 🟡 5. yarış sonrasında RateAppPopup çıkar. "Evet" → App Store link (iOS: `itms-apps://...`).
- [ ] 🟡 "Hayır" → feedback panel açılır, yazıp gönder → toast.
- [ ] 🟡 "Bir daha sorma" → PlayerPrefs flag saved → popup bir daha çıkmaz.

---

## Bölüm 18 — iOS build & cihaz testi

Mac + Xcode gerekli.

- [ ] 🔴 File → Build Settings → iOS → Switch Platform. Bekle (~10-20 dk asset re-import).
- [ ] 🔴 Player Settings → Bundle ID benzersiz (`com.<isim>.dreamcar`).
- [ ] 🔴 Player Settings → Scripting Backend IL2CPP + ARM64.
- [ ] 🔴 Player Settings → Target Minimum iOS 13.0.
- [ ] 🔴 Build → `Builds/iOS/` klasörüne yaz. Unity Xcode projesi üretir.
- [ ] 🔴 Xcode → `Unity-iPhone.xcodeproj` aç → Signing & Capabilities → Team seç.
- [ ] 🔴 iPhone bağlı → hedef cihaz olarak seç → Play (▶).
- [ ] 🔴 iPhone'da Ayarlar → VPN & Cihaz Yönetimi → geliştirici sertifikanı **güven**.
- [ ] 🔴 Uygulama açılıyor, açılır açılmaz kapanmıyor.
- [ ] 🔴 MainMenu'ye ulaşıyor, PhotonConnector "Online" oluyor.
- [ ] 🟡 Mobil dokunmatik gaz/fren/steering çalışıyor.
- [ ] 🟡 Multiplayer test: Mac'te Editor + iPhone'da app → aynı odaya gir → birbirini gör.
- [ ] 🟡 iOS ATT (App Tracking Transparency) izin popup çıkıyor (KVKKConsent kabul sonrası).
- [ ] 🟢 Sıcaklık: 20 dk sürekli oyunda cihaz aşırı ısınmıyor.
- [ ] 🟢 Batarya: 30 dk oyun < %15 tüketim (target).

---

## Bölüm 19 — Regresyon kontrol listesi

Her v0.X release öncesi:

- [ ] 🔴 Console'da kırmızı hata yok (tüm bölümlerin sonu).
- [ ] 🔴 Konsolda "NullReferenceException" yok.
- [ ] 🔴 Chat, oda kurma, sürüş, spawn, chat, yarış — hepsi hala çalışıyor.
- [ ] 🔴 Photon disconnect sonrası reconnect çalışıyor.
- [ ] 🟡 Memory profiler: 10 dk sürüşte < 500 MB RAM.
- [ ] 🟡 FPS: iPhone 12+ üzerinde stabil 60. Alt cihazlarda ≥ 30.
- [ ] 🟡 Build size: `.ipa` < 200 MB.

---

## Bölüm 20 — Yayın öncesi App Store kontrol listesi

- [ ] 🔴 App icon 1024×1024 PNG hazır.
- [ ] 🔴 5 screenshot (6.7 inç iPhone + iPad).
- [ ] 🔴 App açıklaması TR + EN yazılmış.
- [ ] 🔴 Keywords doldu (100 karakter).
- [ ] 🔴 Kategori seçildi (Games → Racing).
- [ ] 🔴 Yaş sınırı: 12+.
- [ ] 🔴 Privacy Policy URL yayında.
- [ ] 🔴 Support URL yayında (mailto: hariç).
- [ ] 🔴 App Store Connect'te "Prepare for Submission".
- [ ] 🔴 IAP ürünleri App Store Connect'te tanımlı, incelemeye hazır.
- [ ] 🟡 App Preview video (opsiyonel ama +30% conversion).
- [ ] 🟡 In-app events tanımlı (release event).

---

## Sonuç

Tüm bölümler ✅ olduğunda yayına hazırsın. Kritik ✅ olmadan Play/App Store'a çıkma. 🟡 eksikse "beta" etiketiyle çıkabilirsin. 🟢 sonra iterasyonlarla eklenir.

**Sık karşılaşılan hatalar**:
- "Photon disconnected" → AppId yanlış veya region değişti
- "The type or namespace name X" → PUN 2 / PlayFab / RCCP import edilmemiş
- iOS'ta ilk açılışta crash → IL2CPP + link.xml eksik
- Chat çalışmıyor → ChatUI GameObject'te PhotonView yok
