# FEW1N Mod Menu

iOS tweak for **DreamRoadMultiplayer** (Unity 6, metadata v39). Ships as a Substrate `.dylib` — install as a `.deb` on jailbroken iOS 14+ or sideload the `.dylib` with ESign/TrollStore.

Latest release version lives in [`VERSION`](VERSION); every push to `template/**` or the workflow rebuilds and drops a fresh `.deb` + `.dylib` under [Releases](../../releases).

## Özellikler

**Araç**
- Fly D-pad (havada sürüş, ekran kumandası)
- GodMode (`HR_PlayerHandler.canCrash = false`)
- No-Clip, Anti-Grav, Düşük Yerçekimi
- Drift Modu, Cruise Control, Hız Sabitleyici
- Emissive parlak boya, sürekli egzoz alevi, sarı balata
- Renk (sabit / rainbow), boyut, plaka değiştirme
- SONSUZ NOS/YAKIT, MAX RPM, Motor Ölmez, Trafik Dursun, Hasar Yok (v2)

**ESP / HUD**
- Araç ESP (mesafe + isim + kutu)
- Hız HUD'u, ARAÇ paneli
- Vidyo isim modu

**Oda**
- Şifre koyma/kaldırma (`pWd` custom property — sunucu tarafı gerçek koruma)
- IsOpen / IsVisible toggle, oda gizleme, hızlı Master olma
- Advanced Custom Room (31 kişi, çöl, saat, drift)
- Odayı yeniden oluştur (isim değişikliği için tek çalışan yöntem)
- Fake online sayısı, hava / harita değişikliği
- Şifre bypass, oda listesi peek

**Chat / AI**
- Groq AI sohbet (`/ai <mesaj>` veya AI Sohbet Modu — prefix'sız otomatik cevap)
- HACKER AI (`/hack <istek>`)
- Chat spam, otomatik karşılama, renkli chat, ASCII/lyrics animasyon
- Emoji sprite testi

**Oyuncu**
- Gerçek Kick (lobbi RPC + teleport crash fallback)
- İsim değiştir, isim hileleri (rozet / görünmez / kayan)

**Diğer**
- Reklam engelleme (AdMob, AppLovin, CAS, UnityAds, IronSource, FBAN, InMobi, Vungle, Chartboost, Mintegral, Pangle, Yandex) — varsayılan KAPALI
- Menu kısayolları, favoriler, restart mod

## Kurulum

### Jailbreak (`.deb`)
Releases sayfasından son `.deb` dosyasını indir, Sileo / Filza ile yükle. `com.TenebryFox.DreamRoadMultiplayer` bundle filter'ı ile sadece oyunda aktif olur.

### Sideload (`.dylib`)
Releases sayfasından `.dylib` dosyasını indir. ESign / TrollStore ile DreamRoadMultiplayer IPA'sına inject et.

## AI (Groq / Grok / OpenAI-uyumlu) Kurulumu

Menuden **AI Ayarları → API Key** ile Groq / xAI Grok / OpenAI-uyumlu bir API key gir. Key `NSUserDefaults` (`com.few1n.dreamroadmod` suite) altında yerel olarak saklanır — repo'ya kesinlikle commit'lenmez, TruffleHog secret scan CI adımı bunu kontrol eder.

Model ve endpoint ayarları da aynı menuden değiştirilir. Varsayılan: Groq `llama-3.1-70b-versatile`.

## Sürüm bump

Tek adım — repo kökündeki `VERSION` dosyasını güncelle:

```
echo 114.10 > VERSION
git add VERSION
git commit -m "Bump to 114.10"
git push
```

CI dosyayı okuyup `template/control`'deki `__VERSION__` placeholder'ını dolduruyor, artifact / release adlarına da otomatik yayıyor.

## Yeni Unity dump geldiğinde offset güncellemesi

Named offset'ler [`template/Offsets.h`](template/Offsets.h) altında toplandı. Yeni bir Unity 6 (veya sonrası) dump geldiğinde:

1. Yeni `dump.cs`'ten ilgili class'ların field offset'lerini oku (HR_PlayerHandler, HR_UI_RoomListLine, HR_PhotonLobbyManager, string header'ı, m_CachedPtr)
2. `template/Offsets.h`'daki `#define`'ları güncelle
3. `Tweak.xm` başındaki OFFSET TABLE yorumundaki fonksiyon offset'lerini de güncelle (bunlar hâlâ inline hex — hook adresleri)
4. `VERSION`'u bump'la, CI derler

`Tweak.xm` içindeki raw hex offset'lerin çoğu generic il2cpp offset'i (0x18 array count, 0x20 array data, 0x10 string header vs.) — hepsini isimlendirmek doğru olmadığından adopt selektiftir. Yeni sınıf için offset ekliyorsan `Offsets.h`'a name'li bir `#define` eklemek yerinde olur.

## Katkı

- Geliştirme branch'i: `claude/**` desenine uyan herhangi bir isim — CI otomatik derler
- Push → `.github/workflows/build.yml` çalışır → TruffleHog scan → Theos build → GitHub Release
- Manuel tetikleme: workflow_dispatch (Actions sekmesi → "Build iOS Mod Menu" → "Run workflow")
- Build cache: Theos `~/theos` altında `actions/cache` ile saklanır; ilk build'den sonrakiler ~30-60 sn hızlanır
- Xcode 15.4 pinli; macOS runner Xcode bump'ı build'i etkilemesin diye

## Mimari notlar

- **Tek dosya**: `template/Tweak.xm` (10K+ satır). Substrate `%ctor` içinden başlıyor, `few1n_poll` UnityFramework base'ini yakalayana kadar 80 deneme her 0.5 sn.
- **Hook motoru**: MSHookFunction (Substrate / ElleKit / libhooker sırayla probe). `safeHook` bir kez fail ederse hepsini atlıyor.
- **Crash guard**: sigjmp + SIGSEGV/SIGBUS handler ile `few1n_memOk` pointer probe. Thread-local (`__thread`), off-thread çakışmaya izin vermiyor.
- **il2cpp erişimi**: `il2cpp_class_from_name` + `il2cpp_runtime_invoke` üzerinden. Field-based mod'lar dump-versiyonuna dayanıklı, offset-based mod'lar Offsets.h'a bağlı.
- **AI**: `few1n_groqAsk` async NSURLSession, Groq endpoint'ine POST. Fallback: `few1n_aiReply` local kural tablosu.

## Lisans

MIT — kişisel/eğitim amaçlı kullanım için. Multiplayer bir oyunda mod menu kullanmak diğer oyuncuların deneyimini bozar; sorumluluğu kendinize aittir.
