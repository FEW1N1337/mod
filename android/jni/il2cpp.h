// FEW1N Mod — Android port
// il2cpp çekirdek katmanı. iOS Tweak.xm'deki few1n_* helper mantığının C++ karşılığı.
// Oyun (DreamRoad, Unity 6, metadata v39) libil2cpp.so içindeki il2cpp_* export'larını
// dlsym ile çözer; her şey İSİMLE bulunur (offset drift'ine bağışık — iOS ile aynı yaklaşım).
#pragma once
#include <cstdint>
#include <cstddef>
#include <string>

namespace il2 {

// ---- Ham il2cpp API tipleri (opak pointer'lar) ----
using Il2Object   = void;   // Il2CppObject*
using Il2Class    = void;   // Il2CppClass*
using Il2Method   = void;   // MethodInfo*
using Il2Type     = void;   // Il2CppType* / System.Type nesnesi
using Il2Domain   = void;
using Il2Image    = void;
using Il2Array    = void;   // Il2CppArray*
using Il2String   = void;   // Il2CppString*

// libil2cpp.so yüklenene kadar bekler, sonra il2cpp_* export'larını dlsym eder.
// Oyun il2cpp'yi init edene kadar (il2cpp_domain_get boş dönmez) bekler. Başarıda true.
bool WaitAndInit();
bool Ready();

// ---- İsimle çözüm (few1n_classAnyImage / resolveOn / methodBySig karşılıkları) ----
Il2Class*  ClassByName(const char* ns, const char* name);   // tüm image'larda ara (obf-safe)
Il2Method* MethodByName(Il2Class* klass, const char* method, int argc);
Il2Method* StaticMethod(const char* ns, const char* cls, const char* method, int argc);
int        FieldOffset(Il2Class* klass, const char* field, int fallback);
Il2Type*   TypeOf(Il2Class* klass);   // typeof(Class) -> FindObjectsOfType için Type nesnesi

// ---- Çağrı (i_runtime_invoke sarıcısı) ----
// obj=nullptr statik metod. args = void*[] (value-type arg'lar için &deger). NULL exc.
Il2Object* Invoke(Il2Method* m, void* obj, void** args);

// Crash-guard'lı çağrı (SIGSEGV/SIGBUS yakalar; iOS few1n_guardedInvoke karşılığı).
// crashed doluysa çağrı segfault etti demektir; NULL döner.
Il2Object* GuardedInvoke(Il2Method* m, void* obj, void** args, bool* crashed);

// ---- Değer kutusu / dizi / string ----
Il2String* NewString(const char* utf8);
Il2Array*  NewArray(Il2Class* elemClass, size_t count);
void       ArraySet(Il2Array* arr, size_t index, void* ref);   // referans tip elemanı yaz
Il2Object* Box(Il2Class* klass, void* valuePtr);

// il2cpp array eleman erişimi (Il2CppArray layout: veri @ +0x20, length @ +0x18)
static constexpr uintptr_t kArrayDataOffset = 0x20;
static constexpr uintptr_t kArrayLenOffset  = 0x18;
int  ArrayCount(Il2Array* arr);
void* ArrayGet(Il2Array* arr, int index);   // referans tip elemanı oku

// ---- Bellek güvenli okuma (few1n_memOk / rdPtr / rdI32) ----
bool  MemOk(void* p);
void* RdPtr(void* base, uintptr_t off);
int   RdI32(void* base, uintptr_t off, int def);

// ---- Unity Object bulucular (FindObjectsOfType / FindObjectOfType inactive dahil) ----
// FindObjectsOfType<T>() -> aktif nesneler (dizi). typeObj = TypeOf(klass).
Il2Array* FindObjectsOfType(Il2Type* typeObj);
// Pasif dahil tekil bulucu (Unity 6: FindObjectsByType(Type, FindObjectsInactive) yolu da denenir).
Il2Object* FindByTypeIncludingInactive(Il2Type* typeObj);

// Crash-guard init (thread-local jmp buf + sigaction). Loader başlarken bir kez.
void InstallCrashGuard();

} // namespace il2
