using UnityEngine;
using DreamCar.Car;
using DreamCar.Economy;

namespace DreamCar.Vehicle
{
    // Yakıt: gaz basıldıkça azalır. Sıfırlanınca motor gücü keser.
    // Refuel istasyonu (trigger volume) ile dolar — ücret PlayerMoney'den düşer.
    [RequireComponent(typeof(CarController))]
    public class FuelSystem : MonoBehaviour
    {
        public float capacity = 60f;
        public float current = 60f;
        public float baseDrainPerSecond = 0.05f;
        public float throttleDrainMultiplier = 0.4f;
        public float pricePerLiter = 25f;

        CarController _car;

        public float Percent => Mathf.Clamp01(current / capacity);
        public bool IsEmpty => current <= 0.01f;

        void Awake() => _car = GetComponent<CarController>();

        void Update()
        {
            if (!_car) return;
            float drain = baseDrainPerSecond + Mathf.Abs(_car.throttleInput) * throttleDrainMultiplier;
            current = Mathf.Max(0f, current - drain * Time.deltaTime);
            // Eskiden doğrudan _car.throttleInput/_car.brakeInput'a yazıyordu; MobileTouchInput
            // her karede Move() ile aynı alanları ezdiği ve Update sırası garanti olmadığı için
            // yakıt bitince çoğu zaman hiçbir şey olmuyordu. Artık CarController FixedUpdate'te
            // okuduğu kesme bayrağını set ediyoruz.
            _car.engineCutoff = IsEmpty;
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
