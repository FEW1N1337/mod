// FEW1N Mod — Android port · Chat özellikleri
// ChatManager.fzi()/get_Instance() -> instance -> Send(string). (iOS ile aynı yol.)
#include "features.h"
#include "il2cpp.h"
#include <cstdio>
#include <cstring>

namespace feat {

static void* ChatInstance() {
    static void* getter = nullptr; static void* chatClass = nullptr;
    if (!chatClass) chatClass = il2::ClassByName("", "ChatManager");
    if (!chatClass) return nullptr;
    if (!getter) {
        const char* names[] = { "fzi", "get_Instance", "Instance", nullptr };
        for (int i = 0; names[i]; i++) { getter = il2::MethodByName(chatClass, names[i], 0); if (getter) break; }
    }
    if (!getter) return nullptr;
    bool cr = false;
    return il2::GuardedInvoke(getter, nullptr, nullptr, &cr);
}

static void SendChat(const char* text) {
    static void* mSend = nullptr; static void* chatClass = nullptr;
    if (!chatClass) chatClass = il2::ClassByName("", "ChatManager");
    if (!mSend && chatClass) mSend = il2::MethodByName(chatClass, "Send", 1);
    if (!mSend) return;
    void* inst = ChatInstance();
    if (!il2::MemOk(inst)) return;
    void* s = il2::NewString(text);
    if (!s) return;
    void* a[1] = { s }; bool cr = false;
    il2::GuardedInvoke(mSend, inst, a, &cr);
}

// TMP rich-text renk sarımı: <color=#RRGGBB>text</color>
static void BuildMsg(char* out, size_t outsz) {
    if (g_chatColorOn) {
        int r = (int)(g_chatColor[0]*255), gg = (int)(g_chatColor[1]*255), b = (int)(g_chatColor[2]*255);
        snprintf(out, outsz, "<color=#%02X%02X%02X>%s</color>", r, gg, b, g_chatMsg);
    } else {
        snprintf(out, outsz, "%s", g_chatMsg);
    }
}

void ChatTick() {
    char buf[256];
    if (g_actSendChat) {
        BuildMsg(buf, sizeof(buf));
        if (buf[0]) SendChat(buf);
        g_actSendChat = false;
    }
    if (g_spamChat) {
        static int cnt = 0;
        if (++cnt >= (g_spamDelayFrames > 5 ? g_spamDelayFrames : 5)) {
            cnt = 0;
            BuildMsg(buf, sizeof(buf));
            if (buf[0]) SendChat(buf);
        }
    }
}

} // namespace feat
