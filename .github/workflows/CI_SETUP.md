# DreamCar CI Kurulumu

İki workflow var:

| Workflow | Runner | Maliyet | Çıktı |
|---|---|---|---|
| `unity-android-build.yml` | Linux (hosted) | **Ücretsiz** | `.apk` / `.aab` |
| `unity-ios-build.yml` | macOS (self-hosted) | Kendi Mac'in | Xcode projesi → `.ipa` |

**Android çok daha kolay** — sadece Unity lisansı yeterli, Mac/Xcode/Apple üyeliği
gerekmez. Önce Android'i çalıştır, iOS'u sonra ekle.

## 1. Unity lisansı (compile-check + ios-build ortak)

### 1a) Activation dosyası al

Yerel makinede repo klonlanmış olarak:

```bash
gh workflow run activation.yml   # veya manuel: game-ci/unity-request-activation-file@v2 çağıran mini workflow
```

Alternatif — mini workflow ekleyip bir kere çalıştır:

```yaml
name: Get Unity activation file
on: workflow_dispatch
jobs:
  activation:
    runs-on: ubuntu-latest
    steps:
      - uses: game-ci/unity-request-activation-file@v2
        id: getManualLicenseFile
        with:
          unityVersion: 6000.0.30f1
      - uses: actions/upload-artifact@v4
        with:
          name: Unity_v6000.0.30f1.alf
          path: ${{ steps.getManualLicenseFile.outputs.filePath }}
```

Actions → çalıştır → artifact'ı indir → `.alf` dosyası.

### 1b) `.alf` → `.ulf` dönüşümü

1. [license.unity3d.com/manual](https://license.unity3d.com/manual) → Unity hesabınla giriş.
2. `.alf` dosyasını yükle → "personal license" seç → `Unity_v6000.0.30f1.ulf` indir.

### 1c) GitHub secret'ları

Repo → Settings → Secrets and variables → Actions → New secret:

| Secret | Değer |
|---|---|
| `UNITY_LICENSE` | `.ulf` dosyasının **tam içeriği** (paste as-is) |
| `UNITY_EMAIL` | Unity hesap e-postan |
| `UNITY_PASSWORD` | Unity hesap şifren |

### 1d) Variable'lar (secret değil, plain)

Repo → Settings → Secrets and variables → Actions → **Variables** sekmesi → New variable:

| Variable | Değer | Açıklama |
|---|---|---|
| `UNITY_CI_ENABLED` | `true` | CI'yı toplu açar/kapatır |
| `IOS_BUILD_ENABLED` | `true` | iOS build job'unu ayrıca kontrol |

`UNITY_CI_ENABLED=false` → tüm job'lar skip. Boş bırakırsan default: skip.

## 1e) Android — bu kadar, hazırsın

`UNITY_LICENSE` + `UNITY_CI_ENABLED=true` yeterli. Push at → Actions sekmesinde
**Unity Android Build** job'u çalışır → artifact olarak `.apk` iner.

Telefona kurmak için: artifact'ı indir → zip'i aç → `.apk`'yı telefona at →
Ayarlar'dan "bilinmeyen kaynaklara izin ver" → kur.

### Android imzalama (sadece Play Store için)

Kendi telefonuna kurmak için gerekmez. Play Store'a yükleyeceksen imzalı `.aab` lazım:

**Keystore oluştur** (bir kez, bilgisayarında):
```bash
keytool -genkeypair -v -keystore dreamcar.keystore \
  -alias dreamcar -keyalg RSA -keysize 2048 -validity 10000
```
> Bu dosyayı ve şifreni **kaybetme**. Kaybedersen aynı uygulamayı bir daha
> güncelleyemezsin — Play Store yeni paket adı ister.

**Base64'e çevir:**
```bash
base64 -i dreamcar.keystore -o keystore.b64
cat keystore.b64 | pbcopy    # macOS
cat keystore.b64 | xclip -selection clipboard   # Linux
```

**Secret'lar:**

| Secret | Değer |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | `keystore.b64` içeriği |
| `ANDROID_KEYSTORE_PASS` | Keystore şifresi |
| `ANDROID_KEYALIAS_NAME` | `dreamcar` (yukarıdaki `-alias` değeri) |
| `ANDROID_KEYALIAS_PASS` | Alias şifresi |

`.aab` üretmek için: Actions → Unity Android Build → **Run workflow** →
"Play Store için .aab üret" kutusunu işaretle.

---

## 2. iOS build için ek secret'lar

Sadece `ios-build` job'u için. `compile-check` bunlarsız da çalışır.

### 2a) Apple sertifikası (`.p12`)

1. Xcode → Preferences → Accounts → Manage Certificates → **iOS Distribution** (yoksa +).
2. Keychain Access'te sertifikayı bul → sağ tık → **Export "..."** → `.p12` seç → şifre ver.
3. Terminal:
   ```bash
   base64 -i dist.p12 -o dist.p12.b64
   cat dist.p12.b64 | pbcopy
   ```

| Secret | Değer |
|---|---|
| `APPLE_CERT_P12` | `dist.p12.b64` içeriği (clipboard'daki base64 string) |
| `APPLE_CERT_P12_PASSWORD` | Export sırasında verdiğin şifre |

### 2b) Provisioning profile

1. [developer.apple.com](https://developer.apple.com) → Certificates, Identifiers & Profiles → Profiles.
2. Bundle ID (`com.few1n.dreamcarclone`) için **Ad Hoc** (test için) veya **App Store** (release için) profile oluştur.
3. `.mobileprovision` indir.
4. Terminal:
   ```bash
   base64 -i dreamcar.mobileprovision -o profile.b64
   cat profile.b64 | pbcopy
   ```

| Secret | Değer |
|---|---|
| `APPLE_PROVISION_PROFILE` | `.mobileprovision` base64 |

### 2c) App Store Connect API key (opsiyonel — Fastlane upload için)

TestFlight'a otomatik yükleme istersen:

| Secret | Değer |
|---|---|
| `APPSTORE_KEY_ID` | App Store Connect → Users and Access → Keys → yeni key → Key ID |
| `APPSTORE_ISSUER_ID` | Aynı sayfadaki Issuer ID |
| `APPSTORE_KEY_P8` | `.p8` dosya base64 |

Bu secret'lar mevcut workflow'da kullanılmıyor — TestFlight upload eklerken referans olsun diye.

## 3. Self-hosted macOS runner ekle

`ios-build` job `[self-hosted, macos-dreamcar]` label'ını arar. Bir Mac'i runner olarak eklemek için:

1. Repo → Settings → Actions → Runners → **New self-hosted runner** → macOS ARM64 seç.
2. Mac'te önerilen komutları çalıştır (`./config.sh` → label'a `macos-dreamcar` ekle).
3. `./run.sh` → runner idle bekler.

Alternatif: **CircleCI macos** veya **MacinCloud** kullanıp workflow'u tetikle. Bu yaml şu an sadece self-hosted destekliyor; ihtiyaç olursa MacinCloud SSH step'i ekleyebiliriz.

## 4. exportOptions.plist ayarı

`.github/exportOptions.plist` — default `ad-hoc`. **App Store Connect** upload için `method` = `app-store` yap.

## 5. Doğrulama

- [ ] Repo → Actions sekmesi → workflow listede görünüyor
- [ ] Push at → **compile-check** job'u yeşil (~5-10 dk)
- [ ] Artifact `editmode-results` indirilebiliyor
- [ ] Self-hosted Mac runner online (Actions → Runners)
- [ ] IOS_BUILD_ENABLED=true ve push at → **ios-build** yeşil (~15-25 dk)
- [ ] Artifact `DreamCar-iOS-{sha}` içinde `.ipa` var
- [ ] `.ipa` cihaza yüklenebiliyor (Xcode Devices veya `ideviceinstaller -i`)

## 6. Sorun giderme

- **compile-check kırmızı: "License activation failed"** → `UNITY_LICENSE` içeriği yanlış (paste sırasında kırpılmış olabilir). Yeniden `.ulf`'yi tam kopyala.
- **compile-check kırmızı: EditMode test failed** → Repo'da hiç EditMode test yok, sadece derleme validasyonu. Console log'a bak — genelde script hatası.
- **ios-build queue'da bekliyor** → Self-hosted runner offline veya label yanlış. `./run.sh` çalışıyor mu kontrol et.
- **Xcode archive fail: "No profiles"** → Bundle ID + provisioning profile eşleşmiyor. Profile'ın Bundle ID'sini Player Settings'teki ile karşılaştır.
- **codesign fail: "no identity found"** → `.p12` sertifikası eksik veya kirli. Yeniden export edip base64'e çevir.

## 7. Maliyet

- Linux hosted runner: **ücretsiz** public repo'da; private'ta ilk 2000 dk/ay ücretsiz.
- Self-hosted Mac runner: **kendi makinen** (elektrik + amortisman).
- MacinCloud: **$30/ay** dedicated Mac Mini.
- CircleCI macOS: **saat başı ücret** (~$0.10/dk).
