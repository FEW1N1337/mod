#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DreamCar.Car;
using DreamCar.Economy;

namespace DreamCar.EditorTools.Procedural
{
    // Modifikasyon kataloğunu üretir. ProceduralCarGenerator.BuildCatalog'un
    // deseni: her çalıştırmada varlıklar yeniden yazılır, katalog listesi
    // yeniden kurulur.
    //
    // KATALOG RESOURCES ALTINDA. Araç prefabı çalışma anında
    // PhotonNetwork.Instantiate ile doğuyor ve kimse alanlarını doldurmuyor;
    // sahneye bağlı bir katalog referansı o prefabda daima null kalırdı.
    // CarCatalog sahneden bağlanabiliyor çünkü onu okuyan CarInventory
    // ~Bootstrap'te duruyor — modifikasyon kataloğunu okuyan ise aracın
    // kendisi.
    public static class ProceduralModCatalog
    {
        const string ItemFolder = "Assets/Generated/Mods";
        const string CatalogFolder = "Assets/Resources";
        const string CatalogPath = CatalogFolder + "/ModCatalog.asset";

        [MenuItem("DreamCar/Procedural/Modifikasyon kataloğunu üret")]
        public static void GenerateAllInteractive()
        {
            GenerateAll();
            EditorUtility.DisplayDialog("DreamCar",
                "Modifikasyon kataloğu üretildi.\n\n" + CatalogPath, "Tamam");
        }

        public static void GenerateAll()
        {
            EnsureFolders();

            var items = new List<ModItem>();
            items.AddRange(BuildPaints());
            items.AddRange(BuildTints());
            items.AddRange(BuildRims());
            items.AddRange(BuildSpoilers());
            items.AddRange(BuildNeons());
            items.AddRange(BuildEngines());
            items.AddRange(BuildTurbos());
            items.AddRange(BuildTires());
            items.AddRange(BuildBrakes());
            items.AddRange(BuildSuspensions());
            items.AddRange(BuildExhausts());

            var catalog = ScriptableObject.CreateInstance<ModCatalog>();
            catalog.items = items;

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Mod] Katalog: {CatalogPath} ({items.Count} parça)");
        }

        // ------------------------------------------------------------- Görsel

        // Boya. İlk renk BEDAVA ve fabrika rengine yakın: modifikasyon
        // ekranındaki her slotta ücretsiz bir "geri dön" seçeneği olmalı,
        // yoksa oyuncu bir kez boyadıktan sonra sade renge dönmek için tekrar
        // para ödemek zorunda kalır.
        static IEnumerable<ModItem> BuildPaints()
        {
            var colors = new (string name, Color c, float metallic, float smooth, long price)[]
            {
                ("Fabrika",       new Color(0.62f, 0.10f, 0.12f), 0.80f, 0.85f,      0),
                ("Gece Siyahı",   new Color(0.03f, 0.03f, 0.04f), 0.85f, 0.92f,  2_500),
                ("Buz Beyazı",    new Color(0.90f, 0.91f, 0.93f), 0.60f, 0.88f,  2_500),
                ("Sedef Gri",     new Color(0.38f, 0.40f, 0.44f), 0.90f, 0.86f,  3_500),
                ("Yarış Kırmızı", new Color(0.78f, 0.06f, 0.07f), 0.75f, 0.90f,  5_000),
                ("Kobalt Mavi",   new Color(0.06f, 0.24f, 0.72f), 0.80f, 0.90f,  5_000),
                ("Zümrüt",        new Color(0.04f, 0.42f, 0.24f), 0.82f, 0.90f,  6_500),
                ("Kum Beji",      new Color(0.72f, 0.63f, 0.44f), 0.55f, 0.72f,  6_500),
                ("Mat Antrasit",  new Color(0.14f, 0.15f, 0.17f), 0.15f, 0.22f, 12_000),
                ("Şampanya",      new Color(0.80f, 0.70f, 0.42f), 1.00f, 0.94f, 20_000),
            };

            int level = 1;
            foreach (var c in colors)
            {
                var item = New($"mod.paint.{level}", "paint", c.name, c.price, level);
                item.color = c.c;
                item.metallic = c.metallic;
                item.smoothness = c.smooth;
                item.effectSummary = c.metallic < 0.3f ? "Mat" : "Metalik";
                Save(item);
                yield return item;
                level++;
            }
        }

        // Cam filmi: renk aynı (kurum siyahı), koyuluk alfada. Renkli film
        // bilerek yok — gerçekte de yasal değil ve oyunun tonuna uymuyor.
        static IEnumerable<ModItem> BuildTints()
        {
            var levels = new (string name, float alpha, long price)[]
            {
                ("Hafif Film",  0.55f,  1_500),
                ("Orta Film",   0.35f,  3_000),
                ("Koyu Film",   0.20f,  6_000),
                ("Limuzin",     0.08f, 12_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.tint.{level}", "tint", l.name, l.price, level);
                item.color = new Color(0.06f, 0.07f, 0.09f);
                item.alpha = l.alpha;
                item.effectSummary = $"Cam geçirgenliği %{Mathf.RoundToInt(l.alpha * 100f)}";
                Save(item);
                yield return item;
                level++;
            }
        }

        static IEnumerable<ModItem> BuildRims()
        {
            var colors = new (string name, Color c, float metallic, float smooth, long price)[]
            {
                ("Gümüş Jant",   new Color(0.78f, 0.79f, 0.82f), 0.95f, 0.80f,  2_000),
                ("Antrasit",     new Color(0.16f, 0.17f, 0.19f), 0.70f, 0.55f,  3_500),
                ("Siyah Mat",    new Color(0.05f, 0.05f, 0.06f), 0.20f, 0.25f,  4_500),
                ("Bronz",        new Color(0.52f, 0.34f, 0.16f), 0.90f, 0.72f,  7_000),
                ("Altın",        new Color(0.85f, 0.68f, 0.24f), 1.00f, 0.88f, 15_000),
            };

            int level = 1;
            foreach (var c in colors)
            {
                var item = New($"mod.rim.{level}", "rim", c.name, c.price, level);
                item.color = c.c;
                item.metallic = c.metallic;
                item.smoothness = c.smooth;
                item.effectSummary = "Görsel";
                Save(item);
                yield return item;
                level++;
            }
        }

        // Spoiler prefabdaki KAPALI çocuğu açıyor; childName o çocuğun adı ve
        // ProceduralCarGenerator.SpoilerChildName ile aynı kaynaktan geliyor.
        // İki yerde ayrı dize yazsaydık biri değişince diğeri sessizce
        // eşleşmez olurdu — denetçi bunu ayrıca kontrol ediyor.
        static IEnumerable<ModItem> BuildSpoilers()
        {
            var variants = new (string name, long price, float downforce)[]
            {
                ("Alçak Spoiler",  6_000,  40f),
                ("Sport Spoiler", 12_000,  90f),
                ("Yarış Kanadı",  24_000, 170f),
            };

            int level = 1;
            foreach (var v in variants)
            {
                var item = New($"mod.spoiler.{level}", "spoiler", v.name, v.price, level);
                item.childName = ProceduralCarGenerator.SpoilerChildName(level);
                item.statA = VehicleStat.Downforce;
                item.statAAdd = v.downforce;
                item.effectSummary = $"+{v.downforce:0} bastırma kuvveti";
                Save(item);
                yield return item;
                level++;
            }
        }

        static IEnumerable<ModItem> BuildNeons()
        {
            var colors = new (string name, Color c)[]
            {
                ("Mavi Neon",   new Color(0.20f, 0.55f, 1.00f)),
                ("Mor Neon",    new Color(0.66f, 0.25f, 1.00f)),
                ("Yeşil Neon",  new Color(0.25f, 1.00f, 0.45f)),
                ("Kırmızı Neon",new Color(1.00f, 0.20f, 0.22f)),
                ("Turuncu Neon",new Color(1.00f, 0.52f, 0.12f)),
            };

            int level = 1;
            foreach (var c in colors)
            {
                var item = New($"mod.neon.{level}", "neon", c.name, 5_000, level);
                item.color = c.c;
                item.effectSummary = "Görsel";
                Save(item);
                yield return item;
                level++;
            }
        }

        // --------------------------------------------------------- İstatistik

        static IEnumerable<ModItem> BuildEngines()
        {
            // Çarpanlar bilerek ölçülü: 5. seviyede toplam +%45 tork.
            // Daha agresif değerler serbest sürüşü kontrol edilemez yapıyor —
            // CarController'ın direksiyon kısma eğrisi 140 km/s'e ayarlı.
            var levels = new (float mul, float topSpeedAdd, long price)[]
            {
                (1.08f, 6f,   8_000),
                (1.16f, 12f, 18_000),
                (1.25f, 20f, 35_000),
                (1.34f, 28f, 60_000),
                (1.45f, 38f, 100_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.engine.{level}", "engine", $"Motor Seviye {level}", l.price, level);
                item.statA = VehicleStat.MotorTorque;
                item.statAMul = l.mul;
                item.useStatB = true;
                item.statB = VehicleStat.TopSpeed;
                item.statBAdd = l.topSpeedAdd;
                item.effectSummary = $"+%{Mathf.RoundToInt((l.mul - 1f) * 100f)} güç, +{l.topSpeedAdd:0} km/s";
                Save(item);
                yield return item;
                level++;
            }
        }

        // Turbo'nun BEDELİ var: yakıt tüketimi artıyor. Bedelsiz yükseltme
        // ekonomiyi anlamsızlaştırır — oyuncu her şeyi alır ve seçim kalmaz.
        static IEnumerable<ModItem> BuildTurbos()
        {
            var levels = new (float torqueMul, float fuelMul, long price)[]
            {
                (1.12f, 1.18f, 15_000),
                (1.22f, 1.34f, 32_000),
                (1.35f, 1.55f, 70_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.turbo.{level}", "turbo", $"Turbo Seviye {level}", l.price, level);
                item.statA = VehicleStat.MotorTorque;
                item.statAMul = l.torqueMul;
                item.useStatB = true;
                item.statB = VehicleStat.FuelDrain;
                item.statBMul = l.fuelMul;
                item.effectSummary =
                    $"+%{Mathf.RoundToInt((l.torqueMul - 1f) * 100f)} güç, " +
                    $"+%{Mathf.RoundToInt((l.fuelMul - 1f) * 100f)} yakıt";
                Save(item);
                yield return item;
                level++;
            }
        }

        static IEnumerable<ModItem> BuildTires()
        {
            var levels = new (string name, float gripMul, long price)[]
            {
                ("Yaz Lastiği",    1.10f,  5_000),
                ("Performans",     1.22f, 14_000),
                ("Yarış Lastiği",  1.35f, 30_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.tire.{level}", "tire", l.name, l.price, level);
                item.statA = VehicleStat.Grip;
                item.statAMul = l.gripMul;
                item.effectSummary = $"+%{Mathf.RoundToInt((l.gripMul - 1f) * 100f)} tutuş";
                Save(item);
                yield return item;
                level++;
            }
        }

        static IEnumerable<ModItem> BuildBrakes()
        {
            var levels = new (string name, float mul, long price)[]
            {
                ("Sport Balata",   1.15f,  6_000),
                ("Havalandırmalı", 1.30f, 16_000),
                ("Seramik Fren",   1.50f, 40_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.brake.{level}", "brake", l.name, l.price, level);
                item.statA = VehicleStat.BrakeTorque;
                item.statAMul = l.mul;
                item.effectSummary = $"+%{Mathf.RoundToInt((l.mul - 1f) * 100f)} fren gücü";
                Save(item);
                yield return item;
                level++;
            }
        }

        // alpha = alçaltma miktarı (SuspensionModule bunu böyle okuyor).
        static IEnumerable<ModItem> BuildSuspensions()
        {
            var levels = new (string name, float drop, float gripMul, long price)[]
            {
                ("Sport Süspansiyon", 0.35f, 1.06f, 10_000),
                ("Coilover",          0.65f, 1.14f, 25_000),
                ("Yarış Kiti",        0.90f, 1.22f, 55_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.suspension.{level}", "suspension", l.name, l.price, level);
                item.alpha = l.drop;
                item.statA = VehicleStat.Grip;
                item.statAMul = l.gripMul;
                item.effectSummary =
                    $"−%{Mathf.RoundToInt(l.drop * 100f)} yükseklik, " +
                    $"+%{Mathf.RoundToInt((l.gripMul - 1f) * 100f)} tutuş";
                Save(item);
                yield return item;
                level++;
            }
        }

        // smoothness = ses sertliği (ExhaustModule bunu böyle okuyor).
        static IEnumerable<ModItem> BuildExhausts()
        {
            var levels = new (string name, float torqueMul, float sound, long price)[]
            {
                ("Sport Egzoz",   1.05f, 0.40f,  7_000),
                ("Çift Çıkış",    1.10f, 0.70f, 18_000),
                ("Yarış Egzozu",  1.16f, 1.00f, 42_000),
            };

            int level = 1;
            foreach (var l in levels)
            {
                var item = New($"mod.exhaust.{level}", "exhaust", l.name, l.price, level);
                item.statA = VehicleStat.MotorTorque;
                item.statAMul = l.torqueMul;
                item.smoothness = l.sound;
                item.effectSummary = $"+%{Mathf.RoundToInt((l.torqueMul - 1f) * 100f)} güç, sert ses";
                Save(item);
                yield return item;
                level++;
            }
        }

        // ------------------------------------------------------------ Yardımcı

        static ModItem New(string id, string slot, string displayName, long price, int level)
        {
            var item = ScriptableObject.CreateInstance<ModItem>();
            item.id = id;
            item.slot = slot;
            item.displayName = displayName;
            item.price = price;
            item.level = level;
            return item;
        }

        static void Save(ModItem item)
        {
            string path = $"{ItemFolder}/{item.id.Replace('.', '_')}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ModItem>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(item, path);
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Assets/Generated", ItemFolder, CatalogFolder })
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
#endif
