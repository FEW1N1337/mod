// FEW1N Mod — Android port · ortak state + Tick dispatcher
#include "features.h"
#include "il2cpp.h"

namespace feat {

// ---- Global state tanımları ----
bool  g_speedOn = false;   float g_speedMult = 1.0f;
bool  g_godMode = false;
bool  g_actJump = false;   float g_jumpForce = 12.0f;
bool  g_actBoost = false;  float g_boostForce = 2.5f;
bool  g_actFreeze = false;
bool  g_actTpUp = false;   float g_tpUpDist = 15.0f;
bool  g_actSavePos = false; bool g_actLoadPos = false;
bool  g_infNitro = false;
bool  g_maxEngine = false;
bool  g_noDamage = false;

// Modül tick'leri (feat_vehicle.cpp / feat_tuning.cpp)
void VehicleTick();
void TuningTick();

void Tick() {
    if (!il2::Ready()) return;
    VehicleTick();
    TuningTick();
}

} // namespace feat
