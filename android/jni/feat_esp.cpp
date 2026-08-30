// FEW1N Mod — Android port · ESP/HUD
// Camera.main.WorldToScreenPoint + FindObjectsOfType(CarDriveSystem) -> araç pozisyonları.
// ImGui foreground draw list ile kutu/isim/mesafe çizer. overlay render thread'inden çağrılır.
#include "features.h"
#include "il2cpp.h"
#include "imgui.h"
#include <cstdio>
#include <cmath>

namespace feat {

static void* g_camGetMain = nullptr, *g_w2s = nullptr;
static void* g_rbGetPosE = nullptr;
static void* g_cdsClass = nullptr, *g_cdsType = nullptr; static int g_cdsRbOff = -1;

static void EnsureEsp() {
    if (g_w2s) return;
    void* cc = il2::ClassByName("UnityEngine", "Camera");
    if (cc) { g_camGetMain = il2::MethodByName(cc, "get_main", 0);
              g_w2s = il2::MethodByName(cc, "WorldToScreenPoint", 1); }
    void* rbc = il2::ClassByName("UnityEngine", "Rigidbody");
    if (rbc) g_rbGetPosE = il2::MethodByName(rbc, "get_position", 0);
    g_cdsClass = il2::ClassByName("", "CarDriveSystem");
    if (g_cdsClass) { g_cdsType = il2::TypeOf(g_cdsClass);
                      g_cdsRbOff = il2::FieldOffset(g_cdsClass, "<jyt>k__BackingField", 0x48); }
}

static bool GetPos(void* rb, Vec3* out) {
    *out = {0,0,0};
    if (!rb || !g_rbGetPosE) return false;
    bool cr = false; void* box = il2::GuardedInvoke(g_rbGetPosE, rb, nullptr, &cr);
    if (cr || !box) return false;
    *out = *(Vec3*)((uintptr_t)box + 0x10); return true;
}

static bool WorldToScreen(void* cam, const Vec3* world, Vec3* screen) {
    if (!cam || !g_w2s) return false;
    void* a[1] = { (void*)world }; bool cr = false;
    void* box = il2::GuardedInvoke(g_w2s, cam, a, &cr);
    if (cr || !box) return false;
    *screen = *(Vec3*)((uintptr_t)box + 0x10);
    return true;
}

void DrawESP() {
    if (!g_espOn || !il2::Ready()) return;
    EnsureEsp();
    if (!g_camGetMain || !g_w2s || !g_cdsType) return;

    bool cr = false;
    void* cam = il2::GuardedInvoke(g_camGetMain, nullptr, nullptr, &cr);
    if (cr || !il2::MemOk(cam)) return;

    // Local kamera pozisyonu (mesafe için) — Camera transform yerine local rb pos kullanabiliriz;
    // basitlik için mesafeyi screen.z (derinlik) üzerinden göstermiyoruz, world mesafe hesaplarız.
    ImDrawList* dl = ImGui::GetForegroundDrawList();
    float sh = ImGui::GetIO().DisplaySize.y;

    void* arr = il2::FindObjectsOfType(g_cdsType);
    int n = il2::ArrayCount(arr);
    for (int i = 0; i < n && i < 32; i++) {
        void* cds = il2::ArrayGet(arr, i);
        if (!il2::MemOk(cds) || g_cdsRbOff <= 0) continue;
        void* rb = il2::RdPtr(cds, g_cdsRbOff);
        if (!il2::MemOk(rb)) continue;
        Vec3 wp; if (!GetPos(rb, &wp)) continue;
        Vec3 sp; if (!WorldToScreen(cam, &wp, &sp)) continue;
        if (sp.z <= 0.0f) continue;           // kameranın arkasında
        float x = sp.x;
        float y = sh - sp.y;                   // Unity alt-sol -> ImGui üst-sol
        // basit kutu (mesafeye göre boyut)
        float box = 2200.0f / (sp.z + 1.0f);
        if (box < 12) box = 12; if (box > 260) box = 260;
        ImU32 col = IM_COL32(255, 60, 60, 230);
        dl->AddRect(ImVec2(x - box*0.5f, y - box), ImVec2(x + box*0.5f, y), col, 0, 0, 2.0f);
        if (g_espDist) {
            char b[32]; snprintf(b, sizeof(b), "%.0fm", sp.z);
            dl->AddText(ImVec2(x - box*0.5f, y + 2), IM_COL32(255,255,0,255), b);
        }
        if (g_espNames) {
            dl->AddText(ImVec2(x - box*0.5f, y - box - 16), IM_COL32(255,255,255,255), "oyuncu");
        }
    }
}

} // namespace feat
