#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // TMP "Essential Resources" güvencesi.
    //
    // Bu proje bütün metinlerini kodla kuruyor (AddComponent<TextMeshProUGUI>).
    // TMP ise yazı tipini TMP_Settings.defaultFontAsset üzerinden alıyor ve o
    // varlık "Assets/TextMesh Pro/" klasöründe duruyor — pakete değil, projeye
    // aittir ve depoda YOKTU. Kaynaklar yüklenmeden kurulan her metin fontsuz
    // doğar ve HİÇBİR ŞEY çizmez: başlıklar, buton etiketleri, hız göstergesi,
    // sohbet, para — hepsi boş. Hata da basılmaz.
    //
    // Unity projeyi ilk açtığında normalde "Import TMP Essentials" penceresini
    // gösterir, ama bu kullanıcının tıklamasına bağlı; üretim zinciri bunu
    // bekleyemez. Burada önce sessizce içe aktarmayı deniyoruz, olmazsa
    // kullanıcıyı net bir talimatla durduruyoruz.
    public static class ProceduralTextMeshPro
    {
        const string PackageFileName = "TMP Essential Resources.unitypackage";

        // TMP_Settings.defaultFontAsset'e DOĞRUDAN DOKUNMA: kaynaklar yokken
        // TMP_Settings.instance null döner ve property zinciri
        // NullReferenceException atar. Resources.Load null-güvenlidir.
        public static bool ResourcesPresent()
        {
            var settings = Resources.Load<TMPro.TMP_Settings>("TMP Settings");
            if (settings == null) return false;
            return TMPro.TMP_Settings.defaultFontAsset != null;
        }

        // true → kaynaklar hazır, üretime devam edilebilir.
        // false → kullanıcıya ne yapacağı söylendi, çağıran DURMALI.
        public static bool EnsureResources()
        {
            if (ResourcesPresent()) return true;

            // 1) Sessiz içe aktarma. Paket, çözümlenmiş TMP/uGUI paketinin
            //    içinde bir .unitypackage olarak geliyor.
            string pkg = FindEssentialPackage();
            if (!string.IsNullOrEmpty(pkg))
            {
                AssetDatabase.ImportPackage(pkg, false);
                AssetDatabase.Refresh();
                if (ResourcesPresent()) return true;
            }

            // 2) Olmadıysa TMP'nin kendi içe aktarma penceresini aç. Menü yolu
            //    sürümler arası değişebildiği için birkaç aday deneniyor.
            //    ExecuteMenuItem yalnızca pencereyi AÇAR — içe aktarmayı
            //    kullanıcı tamamlar, o yüzden bu çağrıdan sonra da hazır olmaz.
            foreach (var path in new[]
                     {
                         "Window/TextMeshPro/Import TMP Essential Resources",
                         "Window/TextMeshPro/Import TMP Essential Resources...",
                         "Window/TextMeshPro/Import TMP Essentials"
                     })
            {
                if (EditorApplication.ExecuteMenuItem(path)) break;
            }

            EditorUtility.DisplayDialog(
                "DreamCar — TextMeshPro kaynakları eksik",
                "Bu projedeki BÜTÜN yazılar TextMeshPro ile çiziliyor ve TMP'nin\n" +
                "varsayılan yazı tipi henüz projeye aktarılmamış.\n\n" +
                "Bu haliyle üretilirse oyunda hiçbir yazı GÖRÜNMEZ\n" +
                "(başlıklar, buton etiketleri, hız göstergesi, sohbet…).\n\n" +
                "YAPILACAK:\n" +
                "1) Açılan pencerede \"Import TMP Essentials\" düğmesine bas\n" +
                "   (açılmadıysa: Window → TextMeshPro → Import TMP Essential Resources)\n" +
                "2) İçe aktarma bitince bu komutu TEKRAR çalıştır\n\n" +
                "Üretim burada durduruldu.",
                "Tamam");

            return false;
        }

        static string FindEssentialPackage()
        {
            // Gömülü paketler "Packages/" altında, kayıt defterinden çözümlenenler
            // "Library/PackageCache/" altında durur. TMP, Unity 6'da
            // com.unity.ugui içine taşındı; ada göre değil dosya adına göre
            // arıyoruz ki iki yerleşim de bulunsun.
            foreach (var root in new[] { "Packages", "Library/PackageCache" })
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var hits = Directory.GetFiles(root, PackageFileName, SearchOption.AllDirectories);
                    if (hits.Length > 0) return hits[0];
                }
                catch (IOException) { /* erişilemeyen alt dizin — diğer köke bak */ }
                catch (System.UnauthorizedAccessException) { }
            }
            return null;
        }

        [MenuItem("DreamCar/Yardım/TextMeshPro kaynaklarını kontrol et", priority = 101)]
        static void CheckMenu()
        {
            if (ResourcesPresent())
                EditorUtility.DisplayDialog("DreamCar",
                    "TextMeshPro kaynakları hazır — yazılar görünecek.", "Tamam");
            else
                EnsureResources();
        }
    }
}
#endif
