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
            isReverse = _car.throttleInput < -0.05f && _car.SpeedKmh < 2f;
            if (isReverse) { currentGear = 0; return; }

            for (int i = 0; i < gearSpeedLimits.Length; i++)
            {
                if (_car.SpeedKmh < gearSpeedLimits[i]) { currentGear = i + 1; return; }
            }
            currentGear = gearSpeedLimits.Length;
        }

        public string GearLabel => isReverse ? "R" : (currentGear == 0 ? "N" : currentGear.ToString());
    }
}
