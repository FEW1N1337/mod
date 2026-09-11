# FEW1N Mod — Android Port (repackaged APK, root'suz)

DreamRoad / Highway Racer (Unity 6 il2cpp, `com.TenebryFox.DreamRoadMultiplayer`) için
Android mod menüsü. iOS `template/Tweak.xm` mod'unun il2cpp mantığı C++'a taşınmış hâli.
Menü ImGui overlay olarak oyunun üstüne çizilir; **3 parmak dokunuş** ile açılır/kapanır.

## ⚠️ Önce oku

- **PairIP'siz APK ŞART.** Play Store sürümü PairIP (Google bütünlük koruması) ile gelir; onu
  repackage edince açılmaz. `repack.sh` başında PairIP kontrolü yapar. PairIP kırma işi bu repoda
  YOKTUR — temiz (PairIP'siz) bir tabanla gelinmeli.
- **Online ban riski:** Modlu istemci Play Integrity / anti-cheat'e takılırsa hesap banı olabilir.
  **Kişisel kullanım** içindir; dağıtmak ayrı bir meseledir ve önerilmez.
- **Sunucu-limitli özellikler (kick, harita-herkeste, oda-master) Android'de de imkânsız** — bu
  Photon sunucu modeli, platform değil (iOS tarafında kanıtlandı). Bu port **sunucu-bağımsız**
  özelliklerle başlar (Hız, GodMode; sonra Uçuş/ESP/tuning).

## Ne var (ilk sürüm)

- **Oyun Hızı** — `UnityEngine.Time.timeScale` (slider 0.1x–5x).
- **GodMode** — `HR_PlayerHandler.canCrash=false` + `damage=0` (her kare uygulanır).
- Altyapı: il2cpp isimle-çözüm katmanı (`il2cpp.cpp`), crash-guard (SIGSEGV/SIGBUS), ImGui menü,
  eglSwapBuffers + AInputQueue hook (Dobby). Yeni özellikler `features.cpp`'ye eklenir.

## 1) `.so` derle (CI — önerilen)

GitHub Actions **Android Build** workflow'u (`.github/workflows/android.yml`) `android/**` push'unda
çalışır ve `libfew1nmod-arm64-v8a` artifact'ı üretir. İndir → `libfew1nmod.so`.

Yerel derlemek istersen (Android NDK r26+ kurulu):
```bash
cmake -B android/build -S android \
  -DCMAKE_TOOLCHAIN_FILE=$NDK/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -DCMAKE_BUILD_TYPE=Release
cmake --build android/build -j4
# -> android/build/libfew1nmod.so
```

## 2) APK'ya göm + imzala

Gerekenler: `apktool`, `zipalign` + `apksigner` (Android build-tools), `keytool`, `python3`.
```bash
cd android
./repack.sh /yol/DreamRoad-PAIRIPSIZ.apk /yol/libfew1nmod.so few1n-modded.apk
```
Script: apktool ile açar → `.so`'yu `lib/arm64-v8a/`'ya koyar → `UnityPlayerActivity.onCreate`'e
`System.loadLibrary("few1nmod")` enjekte eder → yeniden derler → zipalign → yeni keystore ile imzalar.

## 3) Kur & kullan

1. Cihazda **eski/orijinal oyunu KALDIR** (imza farklı, yan yana kurulmaz).
2. `few1n-modded.apk`'yı kur (Ayarlar → bilinmeyen kaynaklara izin).
3. Oyunu aç, sahneye gir. **3 parmakla ekrana dokun** → menü açılır.
4. Hız slider'ı oynat / GodMode aç. Çalışıyorsa il2cpp pipeline'ı tamamdır.

## Sorun giderme (logcat)

```bash
adb logcat -s FEW1N
```
Beklenen satırlar: `JNI_OnLoad`, `libil2cpp.so bulundu`, `il2cpp HAZIR`, `eglSwapBuffers hooked`,
`ImGui init WxH`. Bunlardan biri yoksa:
- `libil2cpp.so yuklenmedi` → loadLibrary enjeksiyonu tetiklenmedi (launcher activity farklı olabilir).
- `eglSwapBuffers bulunamadi` → farklı GL/EGL yolu (Vulkan?); render hook uyarlanır.
- Menü çıkıyor ama dokunmuyor → AInputQueue hook uyarlanır (Unity input yolu).

## Sonraki özellikler (features.cpp'ye eklenecek)

Uçuş/ışınlanma (Rigidbody pos/vel), ESP (Camera.WorldToScreenPoint + FindObjectsOfType),
RCCP tuning (nitro/motor/hasar), selektör. Hepsi iOS'ta çözülü il2cpp yollarının C++ karşılığı.
