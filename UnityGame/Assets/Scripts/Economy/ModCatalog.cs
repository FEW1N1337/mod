using System.Collections.Generic;
using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Economy
{
    // Satın alınabilir tüm modifikasyon parçalarının merkezi kataloğu.
    // CarCatalog'un deseni: ScriptableObject, prosedürel üretilir
    // (ProceduralModCatalog), çalışma anında yalnızca okunur.
    [CreateAssetMenu(menuName = "DreamCar/Mod Catalog", fileName = "ModCatalog")]
    public class ModCatalog : ScriptableObject
    {
        public List<ModItem> items = new();

        public ModItem Find(string id) => items.Find(i => i && i.id == id);

        // Bir slottaki ürünler, seviyeye göre sıralı. UI listeyi böyle çiziyor.
        public List<ModItem> InSlot(string slot)
        {
            var result = new List<ModItem>();
            foreach (var i in items) if (i && i.slot == slot) result.Add(i);
            result.Sort((a, b) => a.level.CompareTo(b.level));
            return result;
        }

        // Katalogda geçen slot adları, ilk görülme sırasıyla. Sekmeler bundan
        // üretiliyor — sabit bir slot listesi tutmak, katalog değişince sessizce
        // eksik sekme bırakırdı.
        public List<string> Slots()
        {
            var result = new List<string>();
            foreach (var i in items)
                if (i && !string.IsNullOrEmpty(i.slot) && !result.Contains(i.slot))
                    result.Add(i.slot);
            return result;
        }
    }

    [CreateAssetMenu(menuName = "DreamCar/Mod Item", fileName = "Mod_")]
    public class ModItem : ScriptableObject
    {
        public string id = "mod.none";

        // Modülün Slot değeriyle AYNI dize olmak zorunda. Denetçi bunu kontrol
        // ediyor: eşleşmezse ürün satın alınır, takılır ve hiçbir şey olmaz —
        // bu projenin baskın hata ailesi.
        public string slot = "tint";

        public string displayName = "-";
        public long price = 1000;

        // Slot içindeki sıra ve "seviye 3 turbo" gibi gösterim için.
        public int level = 1;

        [Header("Görsel (renk kullanan modüller)")]
        public Color color = Color.white;
        [Range(0f, 1f)] public float alpha = 1f;
        [Range(0f, 1f)] public float metallic = 0.5f;
        [Range(0f, 1f)] public float smoothness = 0.5f;

        // Spoiler gibi hazır geometri açan modüller için: araç prefabında
        // KAPALI duran çocuğun adı. Boşsa modül geometri aramaz.
        [Header("Geometri")]
        public string childName = "";

        // İstatistik etkisi. VehicleStatSheet'e (temel + add) × mul olarak
        // gidiyor; kaynak adı modülün slot'u.
        [Header("İstatistik etkisi")]
        public VehicleStat statA = VehicleStat.MotorTorque;
        public float statAAdd;
        public float statAMul = 1f;

        public bool useStatB;
        public VehicleStat statB = VehicleStat.TopSpeed;
        public float statBAdd;
        public float statBMul = 1f;

        // UI'da "+%12 güç" gibi tek satır özet. Prosedürel üretici dolduruyor.
        [Header("Gösterim")]
        [TextArea(1, 2)] public string effectSummary = "";
    }
}
