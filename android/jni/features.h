// FEW1N Mod — Android port · özellik katmanı (ortak state + Vec3)
#pragma once

namespace feat {

struct Vec3 { float x, y, z; };

// ---- Araç / Self ----
extern bool  g_speedOn;      extern float g_speedMult;   // 0.1..5.0 (Time.timeScale)
extern bool  g_godMode;                                  // HR_PlayerHandler canCrash/damage

// Anlık aksiyonlar (menü butonu -> flag; render tick'te uygulanır, il2cpp render thread'te güvenli)
extern bool  g_actJump;      extern float g_jumpForce;   // yukari hiz ekle
extern bool  g_actBoost;     extern float g_boostForce;  // mevcut hiz yonunde it
extern bool  g_actFreeze;                                // araci durdur (hiz=0)
extern bool  g_actTpUp;      extern float g_tpUpDist;    // yukari isinlan
extern bool  g_actSavePos;   extern bool  g_actLoadPos;  // konum kaydet/yukle

// ---- Tuning (RCCP) ----
extern bool  g_infNitro;     // RCCP_Nos amount=max + regenerateTime=0
extern bool  g_maxEngine;    // RCCP_Engine override + maxEngineRPM yuksek
extern bool  g_noDamage;     // RCCP_Damage maximumDamage=0

// Her karede (eglSwapBuffers hook'tan) çağrılır.
void Tick();

} // namespace feat
