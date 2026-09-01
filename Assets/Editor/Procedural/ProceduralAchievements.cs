#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using DreamCar.Social;

namespace DreamCar.EditorTools.Procedural
{
    // Başarımlar ekranı boştu: AchievementsScreen bir AchievementCatalog bekliyor,
    // katalog varlığı hiç üretilmiyordu ve alan hiç atanmıyordu. Refresh() ilk
    // guard'da dönüyor, ekran her zaman boş görünüyordu.
    //
    // İstatistik adları PlayFabAchievements'ın GERÇEKTEN raporladığı adlarla
    // eşleşmek zorunda: raceRuns, raceWins, driftScore, carsBought, distanceMeters.
    // Eşleşmezse başarım hiç açılmaz — sessizce çalışmayan sistem tam olarak budur.
    //
    // Menü: DreamCar → Procedural → Generate Achievement Catalog
    public static class ProceduralAchievements
    {
        const string Folder = "Assets/Generated/Catalog";
        public const string CatalogPath = Folder + "/AchievementCatalog.asset";

        // (id, ad, açıklama, istatistik, eşik, ödül)
        static readonly (string id, string name, string desc, string stat, int threshold, long reward)[] Defs =
        {
            ("ach.first_race",    "İlk Yarış",        "İlk yarışını tamamla.",            "raceRuns",       1,      500),
            ("ach.first_win",     "İlk Zafer",        "İlk yarışını kazan.",              "raceWins",       1,     1500),
            ("ach.win_10",        "Pist Kurdu",       "10 yarış kazan.",                  "raceWins",      10,    10000),
            ("ach.win_50",        "Şampiyon",         "50 yarış kazan.",                  "raceWins",      50,    60000),
            ("ach.race_100",      "Kilometre Taşı",   "100 yarışa katıl.",                "raceRuns",     100,    40000),

            ("ach.drift_10k",     "Kayarak Git",      "Tek seferde 10.000 drift puanı.",  "driftScore",  10000,    2500),
            ("ach.drift_100k",    "Drift Ustası",     "Tek seferde 100.000 drift puanı.", "driftScore", 100000,   25000),

            ("ach.car_2",         "Garaj Kuruluyor",  "İkinci aracını satın al.",         "carsBought",     2,     2000),
            ("ach.car_all",       "Koleksiyoncu",     "Beş aracın hepsine sahip ol.",     "carsBought",     5,    50000),

            ("ach.distance_10k",  "Yol Arkadaşı",     "Toplam 10 km yol yap.",            "distanceMeters", 10000,  1000),
            ("ach.distance_100k", "Uzun Yol",         "Toplam 100 km yol yap.",           "distanceMeters",100000,  8000),
            ("ach.distance_1m",   "Sınır Tanımaz",    "Toplam 1000 km yol yap.",          "distanceMeters",1000000,80000),
        };

        [MenuItem("DreamCar/Procedural/Generate Achievement Catalog")]
        public static void GenerateInteractive()
        {
            var catalog = Generate();
            EditorUtility.DisplayDialog("DreamCar",
                catalog != null
                    ? $"{catalog.achievements.Count} başarım üretildi.\n\n{CatalogPath}"
                    : "Başarım kataloğu üretilemedi.",
                "Tamam");
        }

        public static AchievementCatalog Generate()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            // Baştan kur: tanımlar değişince eskiler birikmesin.
            var existing = AssetDatabase.LoadAssetAtPath<AchievementCatalog>(CatalogPath);
            if (existing != null) AssetDatabase.DeleteAsset(CatalogPath);

            var catalog = ScriptableObject.CreateInstance<AchievementCatalog>();
            catalog.name = "AchievementCatalog";
            AssetDatabase.CreateAsset(catalog, CatalogPath);

            foreach (var d in Defs)
            {
                var def = ScriptableObject.CreateInstance<AchievementDefinition>();
                def.name = d.id.Replace('.', '_');
                def.id = d.id;
                def.displayName = d.name;
                def.description = d.desc;
                def.statistic = d.stat;
                def.threshold = d.threshold;
                def.moneyReward = d.reward;

                // Alt varlık olarak kaydediliyor: ayrı dosya olsaydı katalog
                // yeniden üretilince referanslar kopardı.
                AssetDatabase.AddObjectToAsset(def, catalog);
                catalog.achievements.Add(def);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Achievements] {catalog.achievements.Count} başarım → {CatalogPath}");
            return AssetDatabase.LoadAssetAtPath<AchievementCatalog>(CatalogPath);
        }

        public static AchievementCatalog Load() =>
            AssetDatabase.LoadAssetAtPath<AchievementCatalog>(CatalogPath);
    }
}
#endif
