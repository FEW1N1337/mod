using UnityEngine;
using DreamCar.Car;
using DreamCar.Economy;

namespace DreamCar.Vehicle
{
    // Yakıt: gaz basıldıkça azalır. Sıfırlanınca motor gücü keser.
    // Refuel istasyonu (trigger volume) ile dolar — ücret PlayerMoney'den düşer.
    //
    // Somut sürücü tipine değil IDriveInput arayüzüne bağlıyız: araç RCCP paketiyle
    // sürülüyorsa üzerinde bizim WheelCollider denetleyicimiz yok, RCCPCarAdapter var.
    // Bu yüzden [RequireComponent(typeof(...))] kaldırıldı — o bileşeni zorunlu tutmak
    // RCCP'li araçta bileşenin kendiliğinden eklenmesine ve iki sürücünün çakışmasına
    // yol açardı. Arayüzü kim sağlıyorsa onunla çalışırız.
    public class FuelSystem : MonoBehaviour
    {
        public float capacity = 60f;
        public float current = 60f;

        // TÜKETİM AYARI — eski değerler oyunu oynanamaz kılıyordu.
        //
        // Eskiden base 0.05 + gaz*0.4 idi: tam gazda saniyede 0.45 litre, yani
        // 60 litrelik depo 133 SANİYEDE bitiyordu. Oyuncu iki dakika sürüp
        // kalıyordu. Üstüne depoyu doldurmak 60 × 25 = 1.500 ₺; başlangıç
        // parası 5.000 ₺. Serbest sürüşte hiçbir gelir kaynağı da olmadığı
        // için oyuncu üçüncü depodan sonra parasız ve yakıtsız kalıyordu —
        // geri dönüşü olmayan bir kilitlenme.
        //
        // Yeni değerler tam gazda 60 / 0.034 ≈ 1.765 saniye (yaklaşık 29
        // dakika) veriyor; rölantide depo ~2,8 saat gidiyor. 100 km/h'te tam
        // gaz kilometre başına ~1,22 litre = ~30 ₺; serbest sürüş kilometre
        // başına 120 ₺ ödüyor (FreeRoamMode), yani döngü artı bakiyeli.
        public float baseDrainPerSecond = 0.006f;
        public float throttleDrainMultiplier = 0.028f;
        public float pricePerLiter = 25f;

        IDriveInput _car;

        public float Percent => Mathf.Clamp01(current / capacity);
        public bool IsEmpty => current <= 0.01f;

        // GetComponent arayüzleri de çözer; hangi somut sürücü varsa onu buluruz.
        void Awake() => _car = GetComponent<IDriveInput>();

        void Update()
        {
            // Arayüz referansı UnityEngine.Object değil, bu yüzden "if (!_car)" kısayolu yok;
            // açıkça null karşılaştırması yapıyoruz.
            if (_car == null) return;
            float drain = baseDrainPerSecond + Mathf.Abs(_car.ThrottleInput) * throttleDrainMultiplier;
            current = Mathf.Max(0f, current - drain * Time.deltaTime);
            // Eskiden doğrudan gaz/fren alanlarına yazıyordu; MobileTouchInput her karede
            // Move() ile aynı alanları ezdiği ve Update sırası garanti olmadığı için yakıt
            // bitince çoğu zaman hiçbir şey olmuyordu. Artık sürücünün fizik adımında
            // okuduğu kesme bayrağını (EngineCutoff) set ediyoruz.
            _car.EngineCutoff = IsEmpty;
        }

        public bool TryRefuel(float liters)
        {
            long price = (long)Mathf.Ceil(liters * pricePerLiter);
            if (PlayerMoney.Instance == null || !PlayerMoney.Instance.TrySpend(price)) return false;
            current = Mathf.Min(capacity, current + liters);
            return true;
        }

        public bool TryFillTank()
        {
            float needed = capacity - current;
            return needed > 0.01f && TryRefuel(needed);
        }
    }
}
