// FEW1N Mod — Android port · giriş noktası
// APK'daki smali yaması `System.loadLibrary("few1nmod")` çağırınca JNI_OnLoad tetiklenir.
// Arka plan thread: crash-guard kur → il2cpp hazır olana kadar bekle → overlay hook'larını kur.
#include <jni.h>
#include <pthread.h>
#include <android/log.h>
#include "il2cpp.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "FEW1N", __VA_ARGS__)

void Overlay_InstallHooks();   // overlay.cpp

static void* few1n_thread(void*) {
    LOGI("FEW1N Mod (Android) basladi — il2cpp bekleniyor...");
    il2::InstallCrashGuard();
    if (!il2::WaitAndInit()) { LOGI("il2cpp init BASARISIZ — mod pasif"); return nullptr; }
    Overlay_InstallHooks();
    LOGI("FEW1N Mod hazir (il2cpp + overlay).");
    return nullptr;
}

extern "C" JNIEXPORT jint JNI_OnLoad(JavaVM* vm, void*) {
    LOGI("JNI_OnLoad — FEW1N yukleniyor");
    pthread_t t;
    pthread_create(&t, nullptr, few1n_thread, nullptr);
    pthread_detach(t);
    return JNI_VERSION_1_6;
}

// Yedek: bazı yükleme senaryolarında JNI_OnLoad çağrılmazsa constructor da tetikler.
__attribute__((constructor))
static void few1n_ctor() {
    static bool once = false;
    if (once) return; once = true;
    pthread_t t;
    if (pthread_create(&t, nullptr, few1n_thread, nullptr) == 0) pthread_detach(t);
}
