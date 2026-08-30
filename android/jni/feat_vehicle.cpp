// FEW1N Mod — Android port · Araç/Self özellikleri
// Kaynak: iOS Tweak.xm (Time.timeScale, HR_PlayerHandler, CarDriveSystem->Rigidbody pos/vel).
#include "features.h"
#include "il2cpp.h"
#include <android/log.h>
#include <cmath>
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "FEW1N", __VA_ARGS__)

namespace feat {

// ---------- Oyun Hızı (UnityEngine.Time.set_timeScale) ----------
static void ApplySpeed() {
    static void* mSet = nullptr;
    if (!mSet) mSet = il2::StaticMethod("UnityEngine", "Time", "set_timeScale", 1);
    if (!mSet) return;
    float v = g_speedOn ? g_speedMult : 1.0f;
    void* a[1] = { &v }; bool cr = false;
    il2::GuardedInvoke(mSet, nullptr, a, &cr);
}

// ---------- GodMode (HR_PlayerHandler canCrash/damage) ----------
static void ApplyGodMode() {
    static void* phType = nullptr; static int offCrash = -1, offDmg = -1; static void* phClass = nullptr;
    if (!phClass) phClass = il2::ClassByName("", "HR_PlayerHandler");
    if (!phClass) return;
    if (!phType)   phType = il2::TypeOf(phClass);
    if (offCrash < 0) offCrash = il2::FieldOffset(phClass, "canCrash", 0x38);
    if (offDmg   < 0) offDmg   = il2::FieldOffset(phClass, "damage",   0x3C);
    void* inst = il2::FindByTypeIncludingInactive(phType);
    if (!il2::MemOk(inst)) return;
    if (offCrash > 0 && il2::MemOk((void*)((uintptr_t)inst + offCrash))) *(unsigned char*)((uintptr_t)inst + offCrash) = 0;
    if (offDmg   > 0 && il2::MemOk((void*)((uintptr_t)inst + offDmg)))   *(float*)((uintptr_t)inst + offDmg) = 0.0f;
}

// ---------- Yerel araç Rigidbody'si (CarDriveSystem._rigidbody) ----------
static void* g_rbGetPos = nullptr, *g_rbSetPos = nullptr, *g_rbGetVel = nullptr, *g_rbSetVel = nullptr;
static void* g_rbSetGravity = nullptr, *g_rbSetDetect = nullptr;
static void EnsureRbMethods() {
    if (g_rbSetPos) return;
    void* rbc = il2::ClassByName("UnityEngine", "Rigidbody");
    if (!rbc) return;
    g_rbGetPos = il2::MethodByName(rbc, "get_position", 0);
    g_rbSetPos = il2::MethodByName(rbc, "set_position", 1);
    // Unity 6: velocity -> linearVelocity (iOS ile aynı). Eski isim de dene.
    g_rbGetVel = il2::MethodByName(rbc, "get_linearVelocity", 0);
    if (!g_rbGetVel) g_rbGetVel = il2::MethodByName(rbc, "get_velocity", 0);
    g_rbSetVel = il2::MethodByName(rbc, "set_linearVelocity", 1);
    if (!g_rbSetVel) g_rbSetVel = il2::MethodByName(rbc, "set_velocity", 1);
    g_rbSetGravity = il2::MethodByName(rbc, "set_useGravity", 1);
    g_rbSetDetect  = il2::MethodByName(rbc, "set_detectCollisions", 1);
}
static void RbSetBool(void* rb, void* m, bool val) {
    if (!rb || !m) return;
    bool b = val; void* a[1] = { &b }; bool cr = false;
    il2::GuardedInvoke(m, rb, a, &cr);
}

static void* GetLocalRigidbody() {
    static void* cdsType = nullptr; static int rbOff = -1; static void* cdsClass = nullptr;
    if (!cdsClass) cdsClass = il2::ClassByName("", "CarDriveSystem");
    if (!cdsClass) return nullptr;
    if (!cdsType) cdsType = il2::TypeOf(cdsClass);
    if (rbOff < 0) rbOff = il2::FieldOffset(cdsClass, "<jyt>k__BackingField", 0x48);   // _rigidbody (v39)
    void* cds = il2::FindByTypeIncludingInactive(cdsType);
    if (!il2::MemOk(cds) || rbOff <= 0) return nullptr;
    void* rb = il2::RdPtr(cds, rbOff);
    return il2::MemOk(rb) ? rb : nullptr;
}

static bool RbGetPos(void* rb, Vec3* out) {
    *out = {0,0,0};
    if (!rb || !g_rbGetPos) return false;
    bool cr = false;
    void* box = il2::GuardedInvoke(g_rbGetPos, rb, nullptr, &cr);
    if (cr || !box) return false;
    *out = *(Vec3*)((uintptr_t)box + 0x10);   // boxed Vector3 -> +0x10
    return true;
}
static void RbSetPos(void* rb, const Vec3* v) {
    if (!rb || !g_rbSetPos) return;
    void* a[1] = { (void*)v }; bool cr = false;
    il2::GuardedInvoke(g_rbSetPos, rb, a, &cr);
}
static bool RbGetVel(void* rb, Vec3* out) {
    *out = {0,0,0};
    if (!rb || !g_rbGetVel) return false;
    bool cr = false;
    void* box = il2::GuardedInvoke(g_rbGetVel, rb, nullptr, &cr);
    if (cr || !box) return false;
    *out = *(Vec3*)((uintptr_t)box + 0x10);
    return true;
}
static void RbSetVel(void* rb, const Vec3* v) {
    if (!rb || !g_rbSetVel) return;
    void* a[1] = { (void*)v }; bool cr = false;
    il2::GuardedInvoke(g_rbSetVel, rb, a, &cr);
}

static Vec3 g_savedPos = {0,0,0}; static bool g_hasSaved = false;

void VehicleTick() {
    // sürekli uygulanacaklar
    static bool sp = false; static float sm = -1;
    if (g_speedOn != sp || g_speedMult != sm) { ApplySpeed(); sp = g_speedOn; sm = g_speedMult; }
    if (g_godMode) ApplyGodMode();

    // sürekli rigidbody toggle'ları + anlık aksiyonlar
    static bool prevFly = false, prevNoClip = false;
    bool needRb = g_actJump || g_actBoost || g_actFreeze || g_actTpUp || g_actSavePos || g_actLoadPos
                  || g_fly || g_noClip || prevFly || prevNoClip;
    if (!needRb) return;
    EnsureRbMethods();
    void* rb = GetLocalRigidbody();
    if (!rb) { // temizle, tekrar denenir
        g_actJump = g_actBoost = g_actFreeze = g_actTpUp = g_actSavePos = g_actLoadPos = false;
        return;
    }
    // Fly = anti-gravity hover: useGravity=false + düşüşü sıfırla. Kapatınca gravity geri.
    if (g_fly) { RbSetBool(rb, g_rbSetGravity, false);
        Vec3 v; if (RbGetVel(rb,&v)) { if (v.y < 0) v.y = 0; RbSetVel(rb,&v); } }
    else if (prevFly) { RbSetBool(rb, g_rbSetGravity, true); }
    prevFly = g_fly;
    // NoClip = detectCollisions=false. Kapatınca geri.
    if (g_noClip) RbSetBool(rb, g_rbSetDetect, false);
    else if (prevNoClip) RbSetBool(rb, g_rbSetDetect, true);
    prevNoClip = g_noClip;
    if (g_actJump) { Vec3 v; RbGetVel(rb,&v); v.y = g_jumpForce; RbSetVel(rb,&v); g_actJump = false; }
    if (g_actBoost){ Vec3 v; RbGetVel(rb,&v);
        float m = std::sqrt(v.x*v.x+v.z*v.z);
        if (m > 0.5f) { v.x *= g_boostForce; v.z *= g_boostForce; } else { v.y = g_jumpForce; }
        RbSetVel(rb,&v); g_actBoost = false; }
    if (g_actFreeze){ Vec3 z = {0,0,0}; RbSetVel(rb,&z); g_actFreeze = false; }
    if (g_actTpUp){ Vec3 p; if (RbGetPos(rb,&p)) { p.y += g_tpUpDist; RbSetPos(rb,&p);
        Vec3 z={0,0,0}; RbSetVel(rb,&z); } g_actTpUp = false; }
    if (g_actSavePos){ if (RbGetPos(rb,&g_savedPos)) g_hasSaved = true; g_actSavePos = false; }
    if (g_actLoadPos){ if (g_hasSaved) { RbSetPos(rb,&g_savedPos); Vec3 z={0,0,0}; RbSetVel(rb,&z); } g_actLoadPos = false; }
}

} // namespace feat
