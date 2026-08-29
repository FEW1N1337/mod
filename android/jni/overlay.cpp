// FEW1N Mod — Android port · ImGui overlay
// eglSwapBuffers hook (Dobby) ile oyunun üstüne menü çizer; AInputQueue_getEvent hook ile
// dokunmayı ImGui'ye besler. Bu dosya en platform-özel/kırılgan kısım — cihazda logcat ile ayarlanır.
#include "features.h"
#include "il2cpp.h"
#include <EGL/egl.h>
#include <android/input.h>
#include <android/log.h>
#include <dobby.h>
#include "imgui.h"
#include "backends/imgui_impl_opengl3.h"
#include "backends/imgui_impl_android.h"

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, "FEW1N", __VA_ARGS__)

static bool g_imguiInit = false;
static bool g_menuOpen  = true;
static int  g_scrW = 0, g_scrH = 0;

// ---- Menü çizimi ----
static void DrawMenu() {
    ImGui::SetNextWindowPos(ImVec2(40, 120), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSize(ImVec2(340, 300), ImGuiCond_FirstUseEver);
    ImGui::Begin("FEW1N Mod (Android)", &g_menuOpen, ImGuiWindowFlags_NoCollapse);

    ImGui::TextColored(ImVec4(0.3f, 0.9f, 1.0f, 1.0f), "il2cpp: %s", il2::Ready() ? "HAZIR" : "bekleniyor...");
    ImGui::Separator();

    if (ImGui::CollapsingHeader("Arac / Genel", ImGuiTreeNodeFlags_DefaultOpen)) {
        ImGui::Checkbox("Oyun Hizi", &feat::g_speedOn);
        ImGui::SliderFloat("Hiz x", &feat::g_speedMult, 0.1f, 5.0f, "%.2f");
        ImGui::Checkbox("GodMode (canCrash=0)", &feat::g_godMode);
    }

    ImGui::Separator();
    ImGui::TextDisabled("Menu: 3 parmak dokun ile ac/kapa");
    ImGui::End();
}

// ---- eglSwapBuffers hook ----
static EGLBoolean (*orig_eglSwapBuffers)(EGLDisplay, EGLSurface) = nullptr;
static EGLBoolean hook_eglSwapBuffers(EGLDisplay dpy, EGLSurface surface) {
    if (!g_imguiInit) {
        eglQuerySurface(dpy, surface, EGL_WIDTH,  &g_scrW);
        eglQuerySurface(dpy, surface, EGL_HEIGHT, &g_scrH);
        if (g_scrW > 0 && g_scrH > 0) {
            IMGUI_CHECKVERSION();
            ImGui::CreateContext();
            ImGuiIO& io = ImGui::GetIO();
            io.IniFilename = nullptr;
            io.DisplaySize = ImVec2((float)g_scrW, (float)g_scrH);
            io.FontGlobalScale = 2.0f;   // telefonda okunur boyut
            ImGui::StyleColorsDark();
            ImGui::GetStyle().ScaleAllSizes(2.5f);
            ImGui_ImplAndroid_Init(nullptr);
            ImGui_ImplOpenGL3_Init("#version 300 es");
            g_imguiInit = true;
            LOGI("ImGui init: %dx%d", g_scrW, g_scrH);
        }
    }
    if (g_imguiInit) {
        feat::Tick();   // aktif özellikleri uygula (render thread'te, il2cpp'ye bağlı)
        ImGui_ImplOpenGL3_NewFrame();
        ImGui_ImplAndroid_NewFrame();
        // DisplaySize'ı Android backend'ten SONRA zorla (null-window'dan bozulmasın)
        ImGui::GetIO().DisplaySize = ImVec2((float)g_scrW, (float)g_scrH);
        ImGui::NewFrame();
        if (g_menuOpen) DrawMenu();
        ImGui::Render();
        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
    }
    return orig_eglSwapBuffers(dpy, surface);
}

// ---- Girdi hook (AInputQueue_getEvent) ----
static int32_t (*orig_AInputQueue_getEvent)(AInputQueue*, AInputEvent**) = nullptr;
static int g_threeFingerLatch = 0;
static int32_t hook_AInputQueue_getEvent(AInputQueue* queue, AInputEvent** outEvent) {
    int32_t r = orig_AInputQueue_getEvent(queue, outEvent);
    if (r >= 0 && outEvent && *outEvent && g_imguiInit) {
        AInputEvent* ev = *outEvent;
        // 3 parmak = menü aç/kapa
        if (AInputEvent_getType(ev) == AINPUT_EVENT_TYPE_MOTION) {
            size_t pc = AMotionEvent_getPointerCount(ev);
            if (pc >= 3 && !g_threeFingerLatch) { g_menuOpen = !g_menuOpen; g_threeFingerLatch = 1; }
            if (pc < 3) g_threeFingerLatch = 0;
        }
        ImGui_ImplAndroid_HandleInputEvent(ev);
        // Menü açık ve ImGui dokunuşu istiyorsa oyuna GEÇİRME (event'i tüketilmiş say)
        if (g_menuOpen && ImGui::GetIO().WantCaptureMouse) {
            AInputQueue_finishEvent(queue, ev, 1);
            *outEvent = nullptr;
            return -1;   // oyun bu event'i almasın
        }
    }
    return r;
}

void Overlay_InstallHooks() {
    void* egl = (void*)DobbySymbolResolver("libEGL.so", "eglSwapBuffers");
    if (egl) {
        DobbyHook(egl, (void*)hook_eglSwapBuffers, (void**)&orig_eglSwapBuffers);
        LOGI("eglSwapBuffers hooked @ %p", egl);
    } else LOGI("eglSwapBuffers bulunamadi");

    void* ge = (void*)DobbySymbolResolver("libandroid.so", "AInputQueue_getEvent");
    if (ge) {
        DobbyHook(ge, (void*)hook_AInputQueue_getEvent, (void**)&orig_AInputQueue_getEvent);
        LOGI("AInputQueue_getEvent hooked @ %p", ge);
    } else LOGI("AInputQueue_getEvent bulunamadi");
}
