// FEW1N Mod — Android port · il2cpp çekirdek implementasyonu
#include "il2cpp.h"
#include <dlfcn.h>
#include <signal.h>
#include <setjmp.h>
#include <unistd.h>
#include <pthread.h>
#include <android/log.h>
#include <cstring>
#include <unordered_map>

#define LOGI(...) __android_log_print(ANDROID_LOG_INFO,  "FEW1N", __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, "FEW1N", __VA_ARGS__)

namespace il2 {

// ---- il2cpp export fonksiyon pointer'ları ----
static void*   (*i_domain_get)() = nullptr;
static void**  (*i_domain_get_assemblies)(void* domain, size_t* size) = nullptr;
static void*   (*i_assembly_get_image)(void* assembly) = nullptr;
static void*   (*i_class_from_name)(void* image, const char* ns, const char* name) = nullptr;
static size_t  (*i_image_get_class_count)(void* image) = nullptr;
static void*   (*i_image_get_class)(void* image, size_t index) = nullptr;
static const char* (*i_class_get_name)(void* klass) = nullptr;
static const char* (*i_class_get_namespace)(void* klass) = nullptr;
static void*   (*i_class_get_method_from_name)(void* klass, const char* name, int argc) = nullptr;
static void*   (*i_class_get_field_from_name)(void* klass, const char* name) = nullptr;
static size_t  (*i_field_get_offset)(void* field) = nullptr;
static void*   (*i_class_get_type)(void* klass) = nullptr;
static void*   (*i_type_get_object)(void* type) = nullptr;
static void*   (*i_runtime_invoke)(void* method, void* obj, void** params, void** exc) = nullptr;
static void*   (*i_string_new)(const char* str) = nullptr;
static void*   (*i_array_new)(void* elemClass, size_t count) = nullptr;
static void*   (*i_value_box)(void* klass, void* val) = nullptr;
static void*   (*i_thread_attach)(void* domain) = nullptr;
static void*   (*i_thread_current)() = nullptr;

static void*  g_domain = nullptr;
static bool   g_ready  = false;

// ---- Crash guard (thread-local, iOS few1n_jmpBuf karşılığı) ----
static thread_local sigjmp_buf t_jmp;
static thread_local volatile bool t_inProtected = false;
static struct sigaction g_oldSegv, g_oldBus;

static void CrashHandler(int sig, siginfo_t* info, void* uctx) {
    if (t_inProtected) { siglongjmp(t_jmp, 1); }
    // Bizim değilse orijinal handler'a devret
    struct sigaction* oa = (sig == SIGBUS) ? &g_oldBus : &g_oldSegv;
    if (oa->sa_flags & SA_SIGINFO) { if (oa->sa_sigaction) oa->sa_sigaction(sig, info, uctx); }
    else if (oa->sa_handler == SIG_DFL || oa->sa_handler == SIG_IGN) { signal(sig, SIG_DFL); raise(sig); }
    else if (oa->sa_handler) oa->sa_handler(sig);
}

void InstallCrashGuard() {
    struct sigaction sa; memset(&sa, 0, sizeof(sa));
    sa.sa_sigaction = CrashHandler;
    sa.sa_flags = SA_SIGINFO | SA_ONSTACK;
    sigemptyset(&sa.sa_mask);
    sigaction(SIGSEGV, &sa, &g_oldSegv);
    sigaction(SIGBUS,  &sa, &g_oldBus);
    LOGI("crash-guard kuruldu (SIGSEGV/SIGBUS)");
}

// ---- Yardımcı: bir sembolü libil2cpp.so'dan çöz ----
static void* g_handle = nullptr;
template<typename T> static void resolve(T& fp, const char* name) {
    fp = reinterpret_cast<T>(dlsym(g_handle, name));
    if (!fp) LOGE("il2cpp export bulunamadi: %s", name);
}

bool Ready() { return g_ready; }

bool WaitAndInit() {
    // libil2cpp.so yüklenene kadar bekle (oyun kendi yükler)
    for (int i = 0; i < 600 && !g_handle; i++) {   // ~60sn
        g_handle = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
        if (!g_handle) { usleep(100 * 1000); }
    }
    if (!g_handle) { LOGE("libil2cpp.so yuklenmedi (timeout)"); return false; }
    LOGI("libil2cpp.so bulundu, export'lar cozuluyor...");

    resolve(i_domain_get,               "il2cpp_domain_get");
    resolve(i_domain_get_assemblies,    "il2cpp_domain_get_assemblies");
    resolve(i_assembly_get_image,       "il2cpp_assembly_get_image");
    resolve(i_class_from_name,          "il2cpp_class_from_name");
    resolve(i_image_get_class_count,    "il2cpp_image_get_class_count");
    resolve(i_image_get_class,          "il2cpp_image_get_class");
    resolve(i_class_get_name,           "il2cpp_class_get_name");
    resolve(i_class_get_namespace,      "il2cpp_class_get_namespace");
    resolve(i_class_get_method_from_name,"il2cpp_class_get_method_from_name");
    resolve(i_class_get_field_from_name,"il2cpp_class_get_field_from_name");
    resolve(i_field_get_offset,         "il2cpp_field_get_offset");
    resolve(i_class_get_type,           "il2cpp_class_get_type");
    resolve(i_type_get_object,          "il2cpp_type_get_object");
    resolve(i_runtime_invoke,           "il2cpp_runtime_invoke");
    resolve(i_string_new,               "il2cpp_string_new");
    resolve(i_array_new,                "il2cpp_array_new");
    resolve(i_value_box,                "il2cpp_value_box");
    resolve(i_thread_attach,            "il2cpp_thread_attach");
    resolve(i_thread_current,           "il2cpp_thread_current");

    if (!i_domain_get || !i_runtime_invoke || !i_class_from_name) {
        LOGE("kritik il2cpp export'lari eksik"); return false;
    }
    // Oyun il2cpp'yi init edene kadar bekle (domain hazır olana kadar)
    for (int i = 0; i < 600; i++) {
        g_domain = i_domain_get();
        if (g_domain) break;
        usleep(100 * 1000);
    }
    if (!g_domain) { LOGE("il2cpp domain hazir degil (timeout)"); return false; }
    if (i_thread_attach) i_thread_attach(g_domain);   // bu thread'i il2cpp'ye bağla
    g_ready = true;
    LOGI("il2cpp HAZIR (domain=%p)", g_domain);
    return true;
}

// Diğer thread'ler (ör. render thread zaten bağlı ama garanti için)
static void AttachCurrent() {
    if (i_thread_attach && g_domain && i_thread_current && !i_thread_current())
        i_thread_attach(g_domain);
}

// ---- ClassByName: tüm image'larda ara (isimle, obf-safe) + cache ----
static std::unordered_map<std::string, void*> g_classCache;

Il2Class* ClassByName(const char* ns, const char* name) {
    if (!name || !g_ready) return nullptr;
    std::string key = std::string(ns ? ns : "") + ":" + name;
    auto it = g_classCache.find(key);
    if (it != g_classCache.end()) return it->second;

    void* found = nullptr;
    size_t nAsm = 0;
    void** asms = i_domain_get_assemblies(g_domain, &nAsm);
    // 1) Doğrudan class_from_name (namespace biliniyorsa hızlı)
    for (size_t a = 0; a < nAsm && !found; a++) {
        void* img = i_assembly_get_image(asms[a]);
        if (!img) continue;
        void* c = i_class_from_name(img, ns ? ns : "", name);
        if (c) found = c;
    }
    // 2) Tam tarama (namespace tutmadı / obfuscation) — isimle eşle
    if (!found && i_image_get_class_count && i_image_get_class) {
        for (size_t a = 0; a < nAsm && !found; a++) {
            void* img = i_assembly_get_image(asms[a]);
            if (!img) continue;
            size_t cc = i_image_get_class_count(img);
            for (size_t ci = 0; ci < cc; ci++) {
                void* c = i_image_get_class(img, ci);
                if (!c) continue;
                const char* cn = i_class_get_name(c);
                if (cn && strcmp(cn, name) == 0) { found = c; break; }
            }
        }
    }
    g_classCache[key] = found;
    return found;
}

Il2Method* MethodByName(Il2Class* klass, const char* method, int argc) {
    if (!klass || !method) return nullptr;
    return i_class_get_method_from_name(klass, method, argc);
}

Il2Method* StaticMethod(const char* ns, const char* cls, const char* method, int argc) {
    void* k = ClassByName(ns, cls);
    return k ? MethodByName(k, method, argc) : nullptr;
}

int FieldOffset(Il2Class* klass, const char* field, int fallback) {
    if (!klass || !field || !i_class_get_field_from_name || !i_field_get_offset) return fallback;
    void* f = i_class_get_field_from_name(klass, field);
    if (!f) return fallback;
    return (int)i_field_get_offset(f);
}

Il2Type* TypeOf(Il2Class* klass) {
    if (!klass || !i_class_get_type || !i_type_get_object) return nullptr;
    void* t = i_class_get_type(klass);
    return t ? i_type_get_object(t) : nullptr;
}

Il2Object* Invoke(Il2Method* m, void* obj, void** args) {
    if (!m || !i_runtime_invoke) return nullptr;
    return i_runtime_invoke(m, obj, args, nullptr);
}

Il2Object* GuardedInvoke(Il2Method* m, void* obj, void** args, bool* crashed) {
    if (crashed) *crashed = false;
    if (!m || !i_runtime_invoke) { if (crashed) *crashed = true; return nullptr; }
    void* ret = nullptr;
    t_inProtected = true;
    if (sigsetjmp(t_jmp, 1) == 0) {
        ret = i_runtime_invoke(m, obj, args, nullptr);
    } else {
        if (crashed) *crashed = true;   // segfault yakalandı
        ret = nullptr;
    }
    t_inProtected = false;
    return ret;
}

Il2String* NewString(const char* utf8) { return (g_ready && i_string_new) ? i_string_new(utf8) : nullptr; }
Il2Array*  NewArray(Il2Class* k, size_t n) { return (i_array_new && k) ? i_array_new(k, n) : nullptr; }
Il2Object* Box(Il2Class* k, void* v) { return (i_value_box && k) ? i_value_box(k, v) : nullptr; }

void ArraySet(Il2Array* arr, size_t index, void* ref) {
    if (!arr) return;
    *(void**)((uintptr_t)arr + kArrayDataOffset + index * sizeof(void*)) = ref;
}
int ArrayCount(Il2Array* arr) {
    if (!MemOk(arr)) return 0;
    return (int)(*(uintptr_t*)((uintptr_t)arr + kArrayLenOffset));
}
void* ArrayGet(Il2Array* arr, int index) {
    if (!MemOk(arr)) return nullptr;
    return *(void**)((uintptr_t)arr + kArrayDataOffset + (uintptr_t)index * sizeof(void*));
}

// ---- Bellek güvenli okuma ----
bool MemOk(void* p) {
    if (!p) return false;
    bool ok = true;
    t_inProtected = true;
    if (sigsetjmp(t_jmp, 1) == 0) { volatile unsigned char c = *(volatile unsigned char*)p; (void)c; }
    else ok = false;
    t_inProtected = false;
    return ok;
}
void* RdPtr(void* base, uintptr_t off) {
    if (!base) return nullptr;
    void* r = nullptr;
    t_inProtected = true;
    if (sigsetjmp(t_jmp, 1) == 0) r = *(void**)((uintptr_t)base + off);
    else r = nullptr;
    t_inProtected = false;
    return r;
}
int RdI32(void* base, uintptr_t off, int def) {
    if (!base) return def;
    int r = def;
    t_inProtected = true;
    if (sigsetjmp(t_jmp, 1) == 0) r = *(int*)((uintptr_t)base + off);
    else r = def;
    t_inProtected = false;
    return r;
}

// ---- Unity bulucular ----
static void* g_mFindObjectsOfType = nullptr;   // UnityEngine.Object.FindObjectsOfType(Type)
static void* g_mFindObjectOfType  = nullptr;   // FindObjectOfType(Type)

static void EnsureFinders() {
    if (g_mFindObjectsOfType || g_mFindObjectOfType) return;
    void* oc = ClassByName("UnityEngine", "Object");
    if (!oc) return;
    // Unity 6: FindObjectsOfType hâlâ var (deprecated). Bulunamazsa FindObjectsByType denenir.
    g_mFindObjectsOfType = MethodByName(oc, "FindObjectsOfType", 1);
    if (!g_mFindObjectsOfType) g_mFindObjectsOfType = MethodByName(oc, "FindObjectsByType", 2);
    g_mFindObjectOfType  = MethodByName(oc, "FindObjectOfType", 1);
}

Il2Array* FindObjectsOfType(Il2Type* typeObj) {
    if (!typeObj) return nullptr;
    EnsureFinders();
    if (!g_mFindObjectsOfType) return nullptr;
    AttachCurrent();
    void* a[1] = { typeObj };
    bool cr = false;
    return (Il2Array*)GuardedInvoke(g_mFindObjectsOfType, nullptr, a, &cr);
}

Il2Object* FindByTypeIncludingInactive(Il2Type* typeObj) {
    if (!typeObj) return nullptr;
    EnsureFinders();
    // Önce tekil bulucu (aktif), sonra çoğuldan ilk eleman
    if (g_mFindObjectOfType) {
        void* a[1] = { typeObj }; bool cr = false;
        void* r = GuardedInvoke(g_mFindObjectOfType, nullptr, a, &cr);
        if (r) return r;
    }
    Il2Array* arr = FindObjectsOfType(typeObj);
    if (ArrayCount(arr) > 0) return ArrayGet(arr, 0);
    return nullptr;
}

} // namespace il2
