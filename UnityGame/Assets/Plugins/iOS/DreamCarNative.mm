// DreamCar iOS native köprüsü.
// C# tarafındaki [DllImport("__Internal")] bildirimleri buradaki fonksiyonlara bağlanır:
//   Core/Haptics.cs        → _HapticImpact / _HapticNotification / _HapticSelection
//   Consent/KVKKConsent.cs → _RequestTracking
//
// Bu dosya Assets/Plugins/iOS/ altında olduğu sürece Unity onu Xcode projesine
// otomatik ekler. Ek kurulum gerekmez.

#import <UIKit/UIKit.h>
#import <Foundation/Foundation.h>

#if __has_include(<AppTrackingTransparency/AppTrackingTransparency.h>)
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#define DREAMCAR_HAS_ATT 1
#endif

#if __has_include(<AdSupport/AdSupport.h>)
#import <AdSupport/AdSupport.h>
#endif

extern "C" {

#pragma mark - Haptics

// Taptic Engine generator'larını önceden hazırlamak gecikmeyi azaltır.
// Her çağrıda yeniden oluşturmak yerine tekil örnekleri saklıyoruz.
static UIImpactFeedbackGenerator *g_impactLight = nil;
static UIImpactFeedbackGenerator *g_impactMedium = nil;
static UIImpactFeedbackGenerator *g_impactHeavy = nil;
static UINotificationFeedbackGenerator *g_notification = nil;
static UISelectionFeedbackGenerator *g_selection = nil;

static void DreamCarEnsureGenerators(void)
{
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        if (@available(iOS 10.0, *)) {
            g_impactLight  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            g_impactMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            g_impactHeavy  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            g_notification = [[UINotificationFeedbackGenerator alloc] init];
            g_selection    = [[UISelectionFeedbackGenerator alloc] init];

            [g_impactLight prepare];
            [g_impactMedium prepare];
            [g_impactHeavy prepare];
            [g_notification prepare];
            [g_selection prepare];
        }
    });
}

// intensity: 0 = Light, 1 = Medium, 2 = Heavy
void _HapticImpact(int intensity)
{
    if (@available(iOS 10.0, *)) {
        DreamCarEnsureGenerators();
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator = g_impactMedium;
            if (intensity <= 0)      generator = g_impactLight;
            else if (intensity >= 2) generator = g_impactHeavy;

            [generator impactOccurred];
            [generator prepare]; // bir sonraki çağrı için hazır tut
        });
    }
}

// type: 0 = Success, 1 = Warning, 2 = Error
void _HapticNotification(int type)
{
    if (@available(iOS 10.0, *)) {
        DreamCarEnsureGenerators();
        dispatch_async(dispatch_get_main_queue(), ^{
            UINotificationFeedbackType feedback = UINotificationFeedbackTypeSuccess;
            if (type == 1)      feedback = UINotificationFeedbackTypeWarning;
            else if (type >= 2) feedback = UINotificationFeedbackTypeError;

            [g_notification notificationOccurred:feedback];
            [g_notification prepare];
        });
    }
}

void _HapticSelection(void)
{
    if (@available(iOS 10.0, *)) {
        DreamCarEnsureGenerators();
        dispatch_async(dispatch_get_main_queue(), ^{
            [g_selection selectionChanged];
            [g_selection prepare];
        });
    }
}

#pragma mark - App Tracking Transparency

// iOS 14.5+ reklam takibi izni. Info.plist'e NSUserTrackingUsageDescription
// eklenmemişse sistem izin penceresini göstermez — Unity Player Settings →
// iOS → Other Settings → "User Tracking Usage Description" alanını doldur.
void _RequestTracking(void)
{
#ifdef DREAMCAR_HAS_ATT
    if (@available(iOS 14.5, *)) {
        // Uygulama ön plana tam gelmeden çağrılırsa sistem pencereyi yutar.
        dispatch_async(dispatch_get_main_queue(), ^{
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                NSLog(@"[DreamCar] ATT durumu: %lu", (unsigned long)status);
            }];
        });
        return;
    }
#endif
    NSLog(@"[DreamCar] ATT bu iOS sürümünde kullanılamıyor.");
}

// C# tarafı izin durumunu okumak isterse:
// 0 = notDetermined, 1 = restricted, 2 = denied, 3 = authorized, -1 = desteklenmiyor
int _GetTrackingStatus(void)
{
#ifdef DREAMCAR_HAS_ATT
    if (@available(iOS 14.5, *)) {
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    }
#endif
    return -1;
}

#pragma mark - Yardımcılar

// Cihazın düşük güç modunda olup olmadığı — kalite ayarını düşürmek için kullanılabilir.
bool _IsLowPowerModeEnabled(void)
{
    if (@available(iOS 9.0, *)) {
        return [[NSProcessInfo processInfo] isLowPowerModeEnabled];
    }
    return false;
}

// Termal durum: 0 = nominal, 1 = fair, 2 = serious, 3 = critical
int _GetThermalState(void)
{
    if (@available(iOS 11.0, *)) {
        return (int)[[NSProcessInfo processInfo] thermalState];
    }
    return 0;
}

} // extern "C"
