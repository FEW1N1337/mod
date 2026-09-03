#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace DreamCar.EditorTools.IOS
{
    // iOS build'inin App Store'a girebilmesi ve ÇÖKMEMESİ için gereken iki şey
    // hiç yapılmıyordu.
    //
    // 1) NSUserTrackingUsageDescription — Info.plist'e HİÇ GİRMİYORDU.
    //
    //    Bu bir "eksik meta veri" değil, ÇÖKME sebebi. iOS, bu anahtar
    //    yokken ATTrackingManager.requestTrackingAuthorization çağıran
    //    uygulamayı anında sonlandırır. Kodda çağrı var:
    //    KVKKConsent.Decide → RequestATT → _RequestTracking
    //    (Plugins/iOS/DreamCarNative.mm:100). Yani oyuncunun KVKK onayında
    //    "Kabul ediyorum"a basması uygulamayı öldürürdü.
    //
    //    Native dosyanın kendi yorumu "Player Settings'ten doldur" diyordu ama
    //    o alan hiç doldurulmamıştı ve bunu kontrol eden hiçbir şey yoktu —
    //    projenin baskın hata ailesinin aynısı: yol tamamen döşenmiş, sonundaki
    //    tek anahtar eksik ve hiçbir uyarı yok.
    //
    // 2) PrivacyInfo.xcprivacy — Apple 1 Mayıs 2024'ten beri zorunlu tutuyor.
    //    Olmadan App Store Connect yüklemeyi reddediyor.
    //
    // Manifest build anında ÜRETİLİYOR, depoda sabit durmuyor: içeriği hangi
    // derleme sembollerinin aktif olduğuna bağlı. Reklam SDK'sı derlemeye
    // girmiyorsa "izleme yapıyoruz" demek yanlış beyan olur; PlayFab yoksa
    // cihaz kimliği toplanmıyor demektir. Sabit bir dosya, ilk sembol
    // değişiminde yalan söylemeye başlardı.
    public static class DreamCarIOSPostBuild
    {
        const string TrackingUsage =
            "Sana daha alakalı reklamlar gösterebilmek için kullanılıyor. " +
            "İzin vermezsen oyun aynı şekilde çalışır, reklamlar yalnızca " +
            "kişiselleştirilmez.";

        [PostProcessBuild(1000)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

#if UNITY_IOS
            try
            {
                PatchInfoPlist(pathToBuiltProject);
                AddPrivacyManifest(pathToBuiltProject);
            }
            catch (System.Exception e)
            {
                // Build'i patlatmıyoruz — Xcode projesi üretildi, eksik olan
                // meta veri. Ama sessiz de geçmiyoruz: bu iki adım olmadan
                // uygulama ya çöker ya da mağazaya giremez.
                Debug.LogError("[iOS] Post-build adımı başarısız: " + e +
                               "\nInfo.plist ve PrivacyInfo.xcprivacy ELLE kontrol edilmeli.");
            }
#else
            Debug.LogWarning(
                "[iOS] Post-build betiği iOS build desteği kurulu değilken derlendi; " +
                "Info.plist ve gizlilik manifesti yazılamadı.");
#endif
        }

#if UNITY_IOS
        static void PatchInfoPlist(string pathToBuiltProject)
        {
            string path = Path.Combine(pathToBuiltProject, "Info.plist");
            if (!File.Exists(path))
            {
                Debug.LogError("[iOS] Info.plist bulunamadı: " + path);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var root = plist.root;

            // ATT açıklaması — bunsuz ATT çağrısı uygulamayı sonlandırır.
            root.SetString("NSUserTrackingUsageDescription", TrackingUsage);

            // Photon UDP bazı kısıtlı ağlarda ATS'e takılıyor (README §Sorun giderme).
            var ats = root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);

            // Araç oyunu — yatay. Player Settings'te ayarlı ama Info.plist'te
            // de tutarlı olmalı, yoksa iPad'de dikey açılabiliyor.
            var orientations = root.CreateArray("UISupportedInterfaceOrientations~ipad");
            orientations.AddString("UIInterfaceOrientationLandscapeLeft");
            orientations.AddString("UIInterfaceOrientationLandscapeRight");

            plist.WriteToFile(path);
            Debug.Log("[iOS] Info.plist güncellendi (ATT açıklaması + ATS + yön).");
        }

        static void AddPrivacyManifest(string pathToBuiltProject)
        {
            const string fileName = "PrivacyInfo.xcprivacy";
            string fullPath = Path.Combine(pathToBuiltProject, fileName);
            File.WriteAllText(fullPath, BuildManifestXml(), new UTF8Encoding(false));

            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string targetGuid = proj.GetUnityMainTargetGuid();
            string fileGuid = proj.AddFile(fullPath, fileName, PBXSourceTree.Source);
            proj.AddFileToBuild(targetGuid, fileGuid);

            proj.WriteToFile(projPath);
            Debug.Log("[iOS] PrivacyInfo.xcprivacy üretildi ve Xcode hedefine eklendi.");
        }
#endif

        // Manifest içeriği aktif derleme sembollerine göre kuruluyor: binary ne
        // yapıyorsa manifest onu beyan etmeli. Yanlış beyan, eksik beyandan
        // daha kötü — Apple ikisini de reddediyor.
        static string BuildManifestXml()
        {
            bool ads = IsDefined("CAS_INSTALLED") || IsDefined("UNITY_ADS");
            bool playFab = IsDefined("PLAYFAB_INSTALLED");
            bool analytics = IsDefined("UNITY_ANALYTICS");

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " +
                          "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
            sb.AppendLine("<plist version=\"1.0\">");
            sb.AppendLine("<dict>");

            // Izleme yalnızca reklam SDK'sı gerçekten derlemedeyse.
            sb.AppendLine("  <key>NSPrivacyTracking</key>");
            sb.AppendLine(ads ? "  <true/>" : "  <false/>");
            sb.AppendLine("  <key>NSPrivacyTrackingDomains</key>");
            sb.AppendLine("  <array/>");

            sb.AppendLine("  <key>NSPrivacyCollectedDataTypes</key>");
            sb.AppendLine("  <array>");

            if (playFab)
            {
                // PlayFabAuth: SystemInfo.deviceUniqueIdentifier ile hesap kimliği
                // üretiyor (PlayFabAuth.cs:40). Hesaba bağlı, ama izleme değil.
                AppendDataType(sb, "NSPrivacyCollectedDataTypeDeviceID",
                               linked: true, tracking: false,
                               purpose: "NSPrivacyCollectedDataTypePurposeAppFunctionality");
            }

            if (analytics)
            {
                AppendDataType(sb, "NSPrivacyCollectedDataTypeProductInteraction",
                               linked: playFab, tracking: ads,
                               purpose: "NSPrivacyCollectedDataTypePurposeAnalytics");
            }

            if (ads)
            {
                AppendDataType(sb, "NSPrivacyCollectedDataTypeAdvertisingData",
                               linked: false, tracking: true,
                               purpose: "NSPrivacyCollectedDataTypePurposeThirdPartyAdvertising");
            }

            sb.AppendLine("  </array>");

            // Erişilen API'ler. PlayerPrefs (= NSUserDefaults) proje genelinde
            // kullanılıyor — para, istatistik, ayarlar, araç sahipliği hepsi
            // orada. CA92.1: yalnızca uygulamanın kendi verisi için.
            sb.AppendLine("  <key>NSPrivacyAccessedAPITypes</key>");
            sb.AppendLine("  <array>");
            sb.AppendLine("    <dict>");
            sb.AppendLine("      <key>NSPrivacyAccessedAPIType</key>");
            sb.AppendLine("      <string>NSPrivacyAccessedAPICategoryUserDefaults</string>");
            sb.AppendLine("      <key>NSPrivacyAccessedAPITypeReasons</key>");
            sb.AppendLine("      <array><string>CA92.1</string></array>");
            sb.AppendLine("    </dict>");
            sb.AppendLine("  </array>");

            sb.AppendLine("</dict>");
            sb.AppendLine("</plist>");
            return sb.ToString();
        }

        static void AppendDataType(StringBuilder sb, string type, bool linked, bool tracking, string purpose)
        {
            sb.AppendLine("    <dict>");
            sb.AppendLine("      <key>NSPrivacyCollectedDataType</key>");
            sb.AppendLine($"      <string>{type}</string>");
            sb.AppendLine("      <key>NSPrivacyCollectedDataTypeLinked</key>");
            sb.AppendLine(linked ? "      <true/>" : "      <false/>");
            sb.AppendLine("      <key>NSPrivacyCollectedDataTypeTracking</key>");
            sb.AppendLine(tracking ? "      <true/>" : "      <false/>");
            sb.AppendLine("      <key>NSPrivacyCollectedDataTypePurposes</key>");
            sb.AppendLine($"      <array><string>{purpose}</string></array>");
            sb.AppendLine("    </dict>");
        }

        static bool IsDefined(string symbol)
        {
            try
            {
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS, out string[] defines);
                return System.Array.IndexOf(defines, symbol) >= 0;
            }
            catch { return false; }
        }
    }
}
#endif
