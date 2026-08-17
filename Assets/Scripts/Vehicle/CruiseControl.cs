using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Vehicle
{
    // Bir tuşla sabit hız tutar. Aktifken gaz input'unu override eder (hedef hızın altında
    // ise +throttle, üstünde ise 0). Fren/nitro/handbrake basılırsa iptal.
    public class CruiseControl : MonoBehaviour
    {
        public CarController car;
        public float defaultTargetKmh = 80f;
        public KeyCode toggleKey = KeyCode.C;

        public bool Active { get; private set; }
        public float TargetKmh { get; set; }

        void Awake() { if (!car) car = GetComponent<CarController>(); TargetKmh = defaultTargetKmh; }

        public void Toggle() { Active = !Active; if (Active) TargetKmh = Mathf.Max(car ? car.SpeedKmh : defaultTargetKmh, defaultTargetKmh); }
        public void Cancel() => Active = false;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
            if (!Active || !car) return;

            if (car.brakeInput > 0.01f || car.handbrake) { Cancel(); return; }

            float diff = TargetKmh - car.SpeedKmh;
            float throttle = Mathf.Clamp(diff * 0.05f, -0.5f, 1f);
            car.Move(throttle, 0f, car.steerInput, false);
        }
    }
}
