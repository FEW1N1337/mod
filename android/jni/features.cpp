// FEW1N Mod — Android port · özellik implementasyonu (ilk parti)
#include "features.h"
#include "il2cpp.h"
#include <android/log.h>
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "FEW1N", __VA_ARGS__)

namespace feat {

bool  g_speedOn   = false;
float g_speedMult = 1.0f;
bool  g_godMode   = false;

// ---- Oyun Hızı: UnityEngine.Time.set_timeScale(float) (statik) ----
void ApplySpeed() {
    static void* mSet = nullptr;
    if (!mSet) mSet = il2::StaticMethod("UnityEngine", "Time", "set_timeScale", 1);
    if (!mSet) return;
    float v = g_speedOn ? g_speedMult : 1.0f;
    void* args[1] = { &v };
    bool cr = false;
    il2::GuardedInvoke(mSet, nullptr, args, &cr);
}

// ---- GodMode: HR_PlayerHandler.canCrash=false + damage=0 ----
// Alan offset'leri İSİMLE çözülür (canCrash, damage). Instance her respawn'da değişir → her tick bul.
static void* g_phType = nullptr;
static int   g_offCanCrash = -1;
static int   g_offDamage   = -1;

void ApplyGodModeOnce() {
    void* phClass = il2::ClassByName("", "HR_PlayerHandler");
    if (!phClass) return;
    if (!g_phType)   g_phType = il2::TypeOf(phClass);
    if (g_offCanCrash < 0) g_offCanCrash = il2::FieldOffset(phClass, "canCrash", 0x38);
    if (g_offDamage   < 0) g_offDamage   = il2::FieldOffset(phClass, "damage",   0x3C);
    if (!g_phType) return;

    void* inst = il2::FindByTypeIncludingInactive(g_phType);
    if (!il2::MemOk(inst)) return;
    // canCrash = false
    if (g_offCanCrash > 0 && il2::MemOk((void*)((uintptr_t)inst + g_offCanCrash)))
        *(unsigned char*)((uintptr_t)inst + g_offCanCrash) = 0;
    // damage = 0
    if (g_offDamage > 0 && il2::MemOk((void*)((uintptr_t)inst + g_offDamage)))
        *(float*)((uintptr_t)inst + g_offDamage) = 0.0f;
}

// ---- Her kare (render thread) ----
void Tick() {
    if (!il2::Ready()) return;
    static bool prevSpeedState = false; static float prevMult = -1;
    if (g_speedOn != prevSpeedState || g_speedMult != prevMult) {
        ApplySpeed();
        prevSpeedState = g_speedOn; prevMult = g_speedMult;
    }
    if (g_godMode) ApplyGodModeOnce();   // her kare yeniden uygula (oyun resetleyebilir)
}

} // namespace feat
