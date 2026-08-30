// FEW1N Mod — Android port · ImGui overlay
// eglSwapBuffers hook (Dobby) ile oyunun üstüne menü çizer; AInputQueue_getEvent hook ile
// dokunmayı ImGui'ye besler. Bu dosya en platform-özel/kırılgan kısım — cihazda logcat ile ayarlanır.
#include "features.h"
#include "il2cpp.h"
#include <EGL/egl.h>
#include <android/input.h>
#include <android/log.h>
#include <dlfcn.h>
#include <time.h>
#include "And64InlineHook.hpp"
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
    ImGui::SetNextWindowSize(ImVec2(430, 440), ImGuiCond_FirstUseEver);
    ImGui::Begin("FEW1N Mod (Android)", &g_menuOpen, ImGuiWindowFlags_NoCollapse);

    ImGui::TextColored(ImVec4(0.3f, 0.9f, 1.0f, 1.0f), "il2cpp: %s", il2::Ready() ? "HAZIR" : "bekleniyor...");

    if (ImGui::BeginTabBar("few1n_tabs")) {
        if (ImGui::BeginTabItem("Arac")) {
            ImGui::Checkbox("Oyun Hizi", &feat::g_speedOn);
            ImGui::SliderFloat("Hiz x", &feat::g_speedMult, 0.1f, 5.0f, "%.2f");
            ImGui::Checkbox("GodMode (canCrash=0)", &feat::g_godMode);
            ImGui::SameLine(); ImGui::Checkbox("Fly (hover)", &feat::g_fly);
            ImGui::SameLine(); ImGui::Checkbox("NoClip", &feat::g_noClip);
            ImGui::Separator();
            if (ImGui::Button("Zipla"))  feat::g_actJump = true;
            ImGui::SameLine(); if (ImGui::Button("Boost")) feat::g_actBoost = true;
            ImGui::SameLine(); if (ImGui::Button("Dondur")) feat::g_actFreeze = true;
            if (ImGui::Button("Yukari Isinlan")) feat::g_actTpUp = true;
            ImGui::Separator();
            if (ImGui::Button("Konum Kaydet")) feat::g_actSavePos = true;
            ImGui::SameLine(); if (ImGui::Button("Kayitli Konuma Isinlan")) feat::g_actLoadPos = true;
            ImGui::SliderFloat("Zipla gucu", &feat::g_jumpForce, 5.0f, 40.0f, "%.0f");
            ImGui::SliderFloat("Isinla mesafe", &feat::g_tpUpDist, 5.0f, 60.0f, "%.0f");
            ImGui::EndTabItem();
        }
        if (ImGui::BeginTabItem("Tuning")) {
            ImGui::Checkbox("Sonsuz Nitro", &feat::g_infNitro);
            ImGui::Checkbox("Max Motor (deneysel)", &feat::g_maxEngine);
            ImGui::Checkbox("Hasar Yok", &feat::g_noDamage);
            ImGui::TextDisabled("RCCP bilesenleri lokal — sadece sende gecerli");
            ImGui::EndTabItem();
        }
        if (ImGui::BeginTabItem("ESP")) {
            ImGui::Checkbox("ESP Ac", &feat::g_espOn);
            ImGui::Checkbox("Isimler", &feat::g_espNames);
            ImGui::Checkbox("Mesafe", &feat::g_espDist);
            ImGui::TextDisabled("Araclara kutu cizer (Camera.WorldToScreen)");
            ImGui::EndTabItem();
        }
        if (ImGui::BeginTabItem("Chat")) {
            ImGui::InputText("Mesaj", feat::g_chatMsg, sizeof(feat::g_chatMsg));
            if (ImGui::Button("Gonder")) feat::g_actSendChat = true;
            ImGui::SameLine(); ImGui::Checkbox("Spam", &feat::g_spamChat);
            ImGui::SliderInt("Spam arasi (kare)", &feat::g_spamDelayFrames, 10, 300);
            ImGui::Checkbox("Renkli", &feat::g_chatColorOn);
            if (feat::g_chatColorOn) ImGui::ColorEdit3("Renk", feat::g_chatColor);
            ImGui::EndTabItem();
        }
        if (ImGui::BeginTabItem("Sunucu-Limitli")) {
            ImGui::TextWrapped("Asagidaki ozellikler bu oyunun SUNUCUSU tarafindan engelli — "
                "client'tan (mod'dan) ZORLANAMAZ. iOS'ta da kanitlandi:");
            ImGui::BulletText("Oyuncu ATMA (kick) — sadece gercek master + kooperatif, oda ici yok");
            ImGui::BulletText("Harita HERKESTE degistir — master authority");
            ImGui::BulletText("GERCEK master ol / oda kur-kilitle-sifre — sunucu reddeder");
            ImGui::Separator();
            ImGui::TextDisabled("Sahte 'calisiyor' gostermiyoruz. DoS/paket saldirisi da yok.");
            ImGui::EndTabItem();
        }
        ImGui::EndTabBar();
    }

    ImGui::Separator();
    ImGui::TextDisabled("3 parmak dokun = menu ac/kapa");
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
        feat::Tick();   // aktif özellikleri uygula (il2cpp GuardedInvoke ile korumalı)
        ImGui_ImplOpenGL3_NewFrame();
        // ImGui_ImplAndroid_NewFrame() ÇAĞRILMIYOR: null-window'da ANativeWindow_getWidth(NULL)
        // -> SIGSEGV. Onun yaptıklarını elle yapıyoruz (DisplaySize + DeltaTime). Girdi zaten
        // ImGui_ImplAndroid_HandleInputEvent ile besleniyor (pencere gerektirmez).
        ImGuiIO& io = ImGui::GetIO();
        io.DisplaySize = ImVec2((float)g_scrW, (float)g_scrH);
        struct timespec ts; clock_gettime(CLOCK_MONOTONIC, &ts);
        double now = ts.tv_sec + ts.tv_nsec / 1e9;
        static double s_last = 0;
        io.DeltaTime = (s_last > 0 && now > s_last) ? (float)(now - s_last) : (1.0f/60.0f);
        s_last = now;
        ImGui::NewFrame();
        feat::DrawESP();                 // ESP (foreground draw list — menü kapalı olsa da çizer)
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
        // ZARARSIZ: event'i finish etmiyor / null yapmıyoruz — oyunun input kuyruğunu BOZMA
        // (bozarsak çökme). Menü ImGui ile çalışır; dokunma oyuna da gider (kabul edilebilir).
    }
    return r;
}

// Export edilmiş sembolü çöz (eglSwapBuffers/AInputQueue_getEvent ikisi de export -> dlsym yeter)
static void* ResolveSym(const char* lib, const char* sym) {
    void* h = dlopen(lib, RTLD_NOLOAD | RTLD_NOW);
    if (!h) h = dlopen(lib, RTLD_NOW);
    return h ? dlsym(h, sym) : nullptr;
}

void Overlay_InstallHooks() {
    static bool s_installed = false;   // çift-kurulum guard (yedek — main.cpp zaten tek thread)
    if (s_installed) { LOGI("hook'lar zaten kurulu, atlandi"); return; }
    s_installed = true;
    void* egl = ResolveSym("libEGL.so", "eglSwapBuffers");
    if (egl) {
        A64HookFunction(egl, (void*)hook_eglSwapBuffers, (void**)&orig_eglSwapBuffers);
        LOGI("eglSwapBuffers hooked @ %p", egl);
    } else LOGI("eglSwapBuffers bulunamadi");

    void* ge = ResolveSym("libandroid.so", "AInputQueue_getEvent");
    if (ge) {
        A64HookFunction(ge, (void*)hook_AInputQueue_getEvent, (void**)&orig_AInputQueue_getEvent);
        LOGI("AInputQueue_getEvent hooked @ %p", ge);
    } else LOGI("AInputQueue_getEvent bulunamadi");
}
