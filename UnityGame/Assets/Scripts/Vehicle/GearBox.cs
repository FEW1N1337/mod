using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Vehicle
{
    // Otomatik şanzıman: hıza göre vites belirler, HUD'a gösterir.
    [RequireComponent(typeof(CarController))]
    public class GearBox : MonoBehaviour
    {
        public float[] gearSpeedLimits = { 30f, 60f, 100f, 140f, 180f, 220f };
        public int currentGear;
        public bool isReverse;

        CarController _car;
        void Awake() => _car = GetComponent<CarController>();

        void Update()
        {
            // Eski hali: isReverse = throttle < -0.05 && SpeedKmh < 2. Geri giderken hız
            // 2 km/s'yi geçer geçmez vites "R" olmaktan çıkıp ileri vitese atlıyordu.
            // Geri vites, gaz negatif kaldığı sürece korunmalı; ileri gaz veya duruş bozar.
            if (_car.throttleInput < -0.05f && (isReverse || _car.SpeedKmh < 2f)) isReverse = true;
            else if (_car.throttleInput > 0.05f || _car.SpeedKmh < 0.5f) isReverse = false;
            currentGear = isReverse ? 0 : Util.GameMath.GearForSpeed(_car.SpeedKmh, gearSpeedLimits);
        }

        public string GearLabel => isReverse ? "R" : (currentGear == 0 ? "N" : currentGear.ToString());
    }
}
