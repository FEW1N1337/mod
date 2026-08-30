// FEW1N Mod — Android port · giriş noktası
// APK'daki smali yaması `System.loadLibrary("few1nmod")` çağırınca JNI_OnLoad tetiklenir.
// Arka plan thread: crash-guard kur → il2cpp hazır olana kadar bekle → overlay hook'larını kur.
#include <jni.h>
#include <pthread.h>
#include <atomic>
#include <android/log.h>
#include "il2cpp.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "FEW1N", __VA_ARGS__)

void Overlay_InstallHooks();   // overlay.cpp

static void* few1n_thread(void*) {
    LOGI("FEW1N Mod basladi — il2cpp bekleniyor...");
    il2::InstallCrashGuard();
    if (!il2::WaitAndInit()) { LOGI("il2cpp init BASARISIZ — mod pasif"); return nullptr; }
    Overlay_InstallHooks();
    LOGI("FEW1N Mod hazir (il2cpp + overlay).");
    return nullptr;
}

// KRITIK (logcat kaniti): JNI_OnLoad VE constructor ikisi de thread aciyordu -> 2 thread ayni anda
// il2cpp cozup hook'lari CIFT kuruyordu -> eglSwapBuffers/AInputQueue cift inline-hook -> COKME.
// Tek thread garantisi: atomic exchange, sadece ILK cagri thread'i acar.
static std::atomic<bool> g_started{false};
static void SpawnOnce() {
    if (g_started.exchange(true)) { LOGI("SpawnOnce: zaten baslatildi, atlandi"); return; }
    pthread_t t;
    if (pthread_create(&t, nullptr, few1n_thread, nullptr) == 0) pthread_detach(t);
}

extern "C" JNIEXPORT jint JNI_OnLoad(JavaVM*, void*) {
    LOGI("JNI_OnLoad — FEW1N yukleniyor");
    SpawnOnce();
    return JNI_VERSION_1_6;
}

// Yedek: bazı yükleme senaryolarında JNI_OnLoad çağrılmazsa constructor tetikler (SpawnOnce guard'lı).
__attribute__((constructor))
static void few1n_ctor() {
    SpawnOnce();
}
