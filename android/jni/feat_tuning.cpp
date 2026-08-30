// FEW1N Mod — Android port · Tuning (RCCP) özellikleri
// RCCP bileşenleri lokal simülasyon — bulunan tüm örneklere uygulanır (senkron olmaz, lokal).
#include "features.h"
#include "il2cpp.h"

namespace feat {

// Bir RCCP tipini bul, her örnekte float/bool alan yaz (isimle çözülen offset).
static void ApplyToAll(const char* cls, const char* f1, int off1def, float v1,
                       const char* f2 = nullptr, int off2def = -1, float v2 = 0,
                       bool f2IsBool = false) {
    void* klass = il2::ClassByName("", cls);   // ClassByName kendi içinde cache'li
    if (!klass) return;
    void* type = il2::TypeOf(klass);
    if (!type) return;
    int o1 = il2::FieldOffset(klass, f1, off1def);
    int o2 = f2 ? il2::FieldOffset(klass, f2, off2def) : -1;

    void* arr = il2::FindObjectsOfType(type);
    int n = il2::ArrayCount(arr);
    for (int i = 0; i < n && i < 64; i++) {
        void* inst = il2::ArrayGet(arr, i);
        if (!il2::MemOk(inst)) continue;
        if (o1 > 0 && il2::MemOk((void*)((uintptr_t)inst + o1))) *(float*)((uintptr_t)inst + o1) = v1;
        if (f2 && o2 > 0 && il2::MemOk((void*)((uintptr_t)inst + o2))) {
            if (f2IsBool) *(unsigned char*)((uintptr_t)inst + o2) = (v2 != 0) ? 1 : 0;
            else          *(float*)((uintptr_t)inst + o2) = v2;
        }
    }
}

void TuningTick() {
    // Sonsuz nitro: amount yüksek + regenerateTime=0 (anında dolar)
    if (g_infNitro)
        ApplyToAll("RCCP_Nos", "amount", 0x40, 100.0f, "regenerateTime", 0x4C, 0.0f);
    // Max motor (deneysel): overrideEngineRPM=true + engineRPM yüksek
    if (g_maxEngine)
        ApplyToAll("RCCP_Engine", "engineRPM", 0x3C, 8000.0f, "overrideEngineRPM", 0x38, 1.0f, true);
    // Hasar yok: maximumDamage=0
    if (g_noDamage)
        ApplyToAll("RCCP_Damage", "maximumDamage", 0x70, 0.0f);
}

} // namespace feat
