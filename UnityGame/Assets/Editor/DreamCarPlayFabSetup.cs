#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DreamCar.EditorTools
{
    // BACKEND KATMANININ TAMAMI DERLEMEYE HİÇ GİRMİYORDU.
    //
    // PLAYFAB_INSTALLED sembolü on dosyayı koruyor: kimlik doğrulama, bulut
    // kayıt, liderlik tablosu, başarım senkronu, envanter, para senkronu,
    // arkadaş listesi, referans sistemi, oyuncu şikâyeti. Ve bu sembolü
    // projede HİÇBİR YER tanımlamıyordu.
    //
    // RCCP için bir dönüştürücü yazılıp RCCP_INSTALLED otomatik ekleniyor;
    // PlayFab'in muadili yoktu. Sonuç: kullanıcı SDK'yı kursa bile hiçbir şey
    // değişmezdi, çünkü sembolü elle eklemesi gerekiyordu ve bunu ona söyleyen
    // tek satır bile yoktu. Her dosya #else dalında sessizce
    // Debug.Log("SDK not installed") yazıp geçiyordu.
    //
    // NOT: Oyun PlayFab'siz TAM OYNANABİLİR — para, istatistik ve araçlar
    // PlayerPrefs'te. PlayFab'in getirdiği şey cihazlar arası kalıcılık ve
    // sosyal taraf. Yani bu "oyun çalışmıyor" değil, "ilerlemen telefonu
    // değiştirince kaybolur" meselesi.
    //
    // Menü: DreamCar → Backend → PlayFab kurulumunu doğrula
    public static class DreamCarPlayFabSetup
    {
        const string Define = "PLAYFAB_INSTALLED";

        [MenuItem("DreamCar/Backend/PlayFab kurulumunu doğrula")]
        public static void VerifyInteractive() => Verify(showDialog: true);

        public static bool IsSdkPresent() => FindPlayFabType() != null;

        public static bool IsDefineSet()
        {
            try
            {
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out string[] defines);
                return System.Array.IndexOf(defines, Define) >= 0;
            }
            catch { return false; }
        }

        public static void Verify(bool showDialog)
        {
            bool sdk = IsSdkPresent();

            if (!sdk)
            {
                // Sembol SDK yokken tanımlıysa proje derlenmez — o hâlde temizle.
                if (IsDefineSet()) RemoveDefine();

                string missing =
                    "PlayFab SDK projede bulunamadı.\n\n" +
                    "ARANAN TİP: PlayFab.PlayFabSettings\n\n" +
                    "Bu olmadan çalışmayanlar:\n" +
                    "• Bulut kayıt — ilerleme telefon değişince kaybolur\n" +
                    "• Liderlik tablosu — ekran kalıcı olarak boş\n" +
                    "• Başarım senkronu, arkadaş listesi, oyuncu şikâyeti\n\n" +
                    "OYUN BUNSUZ DA TAM OYNANIR (para/istatistik/araçlar\n" +
                    "cihazda PlayerPrefs'te tutuluyor).\n\n" +
                    "KURMAK İÇİN:\n" +
                    "1) Asset Store → \"PlayFab SDK\" (ücretsiz) → import\n" +
                    "2) developer.playfab.com → Studio → yeni Title oluştur\n" +
                    "3) Title Id'yi ~Bootstrap → PlayFabAuth → titleId alanına yaz\n" +
                    "4) Bu menüyü tekrar çalıştır — define kendiliğinden eklenir";

                Debug.LogWarning("[PlayFab] " + missing);
                if (showDialog) EditorUtility.DisplayDialog("DreamCar — PlayFab", missing, "Tamam");
                return;
            }

            bool already = IsDefineSet();
            if (!already) AddDefine();

            string ok =
                "PlayFab SDK bulundu.\n\n" +
                (already
                    ? $"{Define} zaten tanımlıydı."
                    : $"{Define} eklendi (Standalone + Android + iOS).\n" +
                      "Unity betikleri yeniden derleyecek.") +
                "\n\nSIRADAKİ: ~Bootstrap → PlayFabAuth → titleId alanına\n" +
                "developer.playfab.com'daki Title Id'yi yaz. Boş kalırsa\n" +
                "giriş yapılamaz ve bütün backend çağrıları başarısız olur.";

            Debug.Log("[PlayFab] " + ok);
            if (showDialog) EditorUtility.DisplayDialog("DreamCar — PlayFab", ok, "Tamam");
        }

        // Tipe doğrudan bağlanamayız: SDK yokken derleme hatası olurdu.
        // RCCPReflection ile aynı gerekçe — varlığı çalışma anında sorulur.
        static System.Type FindPlayFabType()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                    if (t != null && t.Namespace == "PlayFab" && t.Name == "PlayFabSettings")
                        return t;
            }
            return null;
        }

        static void AddDefine() => EditDefines(add: true);
        static void RemoveDefine() => EditDefines(add: false);

        static void EditDefines(bool add)
        {
            foreach (var group in new[]
                     {
                         NamedBuildTarget.Standalone,
                         NamedBuildTarget.Android,
                         NamedBuildTarget.iOS,
                     })
            {
                try
                {
                    PlayerSettings.GetScriptingDefineSymbols(group, out string[] defines);
                    var list = new List<string>(defines);

                    if (add)
                    {
                        if (list.Contains(Define)) continue;
                        list.Add(Define);
                    }
                    else
                    {
                        if (!list.Remove(Define)) continue;
                    }

                    PlayerSettings.SetScriptingDefineSymbols(group, list.ToArray());
                    Debug.Log($"[PlayFab] {Define} {(add ? "eklendi" : "kaldırıldı")}: {group.TargetName}");
                }
                catch (System.Exception e)
                {
                    // Bir hedef platform kurulu değilse burası patlar; diğerleri
                    // etkilenmemeli. Elle yol: Player Settings → Scripting Define Symbols.
                    Debug.LogWarning($"[PlayFab] {group.TargetName} için define düzenlenemedi: {e.Message}");
                }
            }
        }
    }
}
#endif
