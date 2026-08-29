// FEW1N Mod — Android port · özellik katmanı (ilk parti)
// Sunucu-bağımsız, iOS'ta il2cpp ile ÇALIŞTIĞI kanıtlanmış özellikler.
#pragma once

namespace feat {

// Menü durumları (menü tarafından yazılır, Tick tarafından uygulanır)
extern bool  g_speedOn;
extern float g_speedMult;   // 0.1 .. 5.0
extern bool  g_godMode;

// Her karede (eglSwapBuffers hook'tan) çağrılır — aktif toggle'ları uygular.
void Tick();

// Anlık uygulama (menü etkileşiminden)
void ApplySpeed();          // Time.timeScale = g_speedOn ? g_speedMult : 1
void ApplyGodModeOnce();    // HR_PlayerHandler canCrash=false + damage=0 (bir kez)

} // namespace feat
