using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Customization
{
    // Katalogdaki bir parçanın kimliği. Neden düz string değil:
    //
    // Modifikasyon kimlikleri üç yere birden gidiyor — kayıt (PlayerPrefs/sunucu),
    // ağ (diğer oyuncular aracı doğru görsün) ve mağaza. Üçü arasında düz string
    // dolaştırmak, "boş" durumunun bir yerde "", başka yerde null, başka yerde
    // "none" olmasıyla sonuçlanır ve bu hatalar sessizdir. Tek bir tip, tek bir
    // "yok" tanımı veriyor.
    [System.Serializable]
    public struct ItemId : System.IEquatable<ItemId>
    {
        [SerializeField] string value;

        public ItemId(string value) { this.value = string.IsNullOrEmpty(value) ? null : value; }

        public static readonly ItemId None = new ItemId(null);
        public bool IsNone => string.IsNullOrEmpty(value);
        public string Value => value ?? "";

        public bool Equals(ItemId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;

        public static bool operator ==(ItemId a, ItemId b) => a.Equals(b);
        public static bool operator !=(ItemId a, ItemId b) => !a.Equals(b);
    }

    // Bir modülün araca erişimi. Modül GetComponent aramaz — ne bulacağı
    // söylenir. Böylece modül test edilebilir ve araç hiyerarşisinin şeklinden
    // bağımsız kalır.
    public sealed class VehicleContext
    {
        public GameObject Root { get; }
        public Transform Body { get; }
        public IDriveInput Drive { get; }
        public IVehicleStats Stats { get; }
        public IVehicleAuthority Authority { get; }
        public VehicleStatSheet StatSheet { get; }

        public VehicleContext(GameObject root, Transform body, IDriveInput drive,
                              IVehicleStats stats, IVehicleAuthority authority,
                              VehicleStatSheet statSheet)
        {
            Root = root;
            Body = body;
            Drive = drive;
            Stats = stats;
            Authority = authority;
            StatSheet = statSheet;
        }

        // Araç üzerindeki standart bileşenlerden bağlam kurar. Araç prefabları
        // DreamCarSetup / ProceduralCarGenerator / RCCPCarConverter tarafından
        // aynı bileşen setiyle kuruluyor, bu yüzden tek yardımcı üçüne de yeter.
        public static VehicleContext FromVehicle(GameObject root)
        {
            if (root == null) return null;
            return new VehicleContext(
                root,
                root.transform,
                root.GetComponent<IDriveInput>(),
                root.GetComponent<IVehicleStats>(),
                root.GetComponent<IVehicleAuthority>(),
                root.GetComponent<VehicleStatSheet>());
        }
    }

    // Tek bir modifikasyon alt sistemi: jant, lastik, süspansiyon, turbo, egzoz,
    // body kit, spoiler, neon, cam filmi, boya, plaka...
    //
    // Modüller birbirini TANIMAZ. Aralarındaki tek ortak nokta VehicleStatSheet:
    // istatistik değiştiren her modül kendi Slot adını kaynak olarak kullanır, bu
    // yüzden iki modül aynı istatistiği değiştirdiğinde birbirini ezmez.
    //
    // Bir modifikasyonun üç ayrı işi var ve arayüz bunları bilerek ayırıyor:
    //   • görseli değiştirmek  → Apply içinde mesh/materyal
    //   • istatistiği değiştirmek → Apply içinde StatSheet.Set(Slot, ...)
    //   • kaydedilmek → Current, kayıt ve ağ katmanı tarafından okunur
    // Üçü tek yönteme karışırsa parça takılıp çıkarıldığında istatistik geride
    // kalır; bu, modifikasyon sistemlerinin en sık hatası.
    public interface ICustomizationModule
    {
        // Katalog ve kayıt anahtarı: "wheel", "tire", "turbo", "spoiler", "neon"...
        // Araç başına slot başına EN FAZLA BİR modül.
        string Slot { get; }

        // Şu an takılı parça. Hiçbiri takılı değilse ItemId.None.
        ItemId Current { get; }

        // Parçayı tak. Aynı slotta zaten bir parça varsa modül önce onu kaldırmak
        // zorunda — çağıran taraf iki kez Remove çağırmakla yükümlü değil.
        void Apply(VehicleContext vehicle, ItemId id);

        // Parçayı çıkar: görsel geri alınır, StatSheet.Clear(Slot) çağrılır,
        // Current ItemId.None olur.
        void Remove(VehicleContext vehicle);
    }
}
