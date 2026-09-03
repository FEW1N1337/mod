using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Customization
{
    // Modifikasyon kaydı. İki ayrı şey saklıyor ve ikisini karıştırmamak önemli:
    //
    //   SAHİPLİK — hangi parçalar satın alındı. Oyuncu başına, araçtan bağımsız.
    //   TAKILI   — hangi parça hangi ARACIN hangi slotunda. Araç başına.
    //
    // Araç başına olması şart: oyuncu ikinci aracını aldığında birincinin cam
    // filmiyle görmemeli. CarPaint bugün global anahtar kullanıyor (car.color)
    // ve tam olarak bu sorunu yaşıyor — yeni sistem o hatayı tekrarlamıyor.
    //
    // Depolama PlayerPrefs. Projenin geri kalanıyla aynı güven seviyesi:
    // cihazda düz metin, sunucu doğrulaması yok (bkz. IVehicleAuthority.
    // IsServerVerified). Sunucu otoriter ekonomi Faz 10.
    public static class ModSave
    {
        const string OwnedKey = "mod.owned";

        static string EquipKey(string carId, string slot) => $"mod.{carId}.{slot}";

        public static ItemId Equipped(string carId, string slot)
        {
            if (string.IsNullOrEmpty(carId) || string.IsNullOrEmpty(slot)) return ItemId.None;
            return new ItemId(PlayerPrefs.GetString(EquipKey(carId, slot), ""));
        }

        public static void SetEquipped(string carId, string slot, ItemId id)
        {
            if (string.IsNullOrEmpty(carId) || string.IsNullOrEmpty(slot)) return;
            if (id.IsNone) PlayerPrefs.DeleteKey(EquipKey(carId, slot));
            else PlayerPrefs.SetString(EquipKey(carId, slot), id.Value);
            PlayerPrefs.Save();
        }

        public static bool Owns(ItemId id)
        {
            if (id.IsNone) return false;
            return OwnedSet().Contains(id.Value);
        }

        public static void AddOwned(ItemId id)
        {
            if (id.IsNone) return;
            var set = OwnedSet();
            if (!set.Add(id.Value)) return;
            PlayerPrefs.SetString(OwnedKey, string.Join(",", set));
            PlayerPrefs.Save();
        }

        public static List<string> OwnedIds() => new List<string>(OwnedSet());

        // Buluttan gelen sahiplik listesini yerelle birleştirir (union).
        // CarInventory.MergeOwnedFromCloud ile aynı gerekçe: çevrimdışı satın
        // alınan parça kaybolmasın.
        public static void MergeOwnedFromCloud(List<string> cloudOwned)
        {
            if (cloudOwned == null || cloudOwned.Count == 0) return;
            var set = OwnedSet();
            bool changed = false;
            foreach (var id in cloudOwned)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (set.Add(id.Trim())) changed = true;
            }
            if (!changed) return;
            PlayerPrefs.SetString(OwnedKey, string.Join(",", set));
            PlayerPrefs.Save();
        }

        // Ağ için tek dize: "tint=mod.tint.2;rim=mod.rim.1"
        // Photon Custom Properties'e tek anahtar yazmak, slot başına ayrı anahtar
        // yazmaktan hem ucuz hem atomik — yarım uygulanmış bir görünüm oluşmuyor.
        public static string Serialize(string carId, IEnumerable<string> slots)
        {
            var parts = new List<string>();
            foreach (var slot in slots)
            {
                var id = Equipped(carId, slot);
                if (!id.IsNone) parts.Add(slot + "=" + id.Value);
            }
            return string.Join(";", parts);
        }

        public static Dictionary<string, ItemId> Deserialize(string packed)
        {
            var result = new Dictionary<string, ItemId>();
            if (string.IsNullOrEmpty(packed)) return result;

            foreach (var part in packed.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0 || eq == part.Length - 1) continue;
                result[part.Substring(0, eq)] = new ItemId(part.Substring(eq + 1));
            }
            return result;
        }

        static HashSet<string> OwnedSet()
        {
            var set = new HashSet<string>();
            foreach (var s in PlayerPrefs.GetString(OwnedKey, "").Split(','))
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
            return set;
        }
    }
}
