using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Vehicle
{
    // Bir tuşla sabit hız tutar. Aktifken gaz input'unu override eder (hedef hızın altında
    // ise +throttle, üstünde ise 0). Fren/nitro/handbrake basılırsa iptal.
    public class CruiseControl : MonoBehaviour
    {
        // Somut sürücü tipi yerine IDriveInput: araç RCCP ile sürülüyorsa üzerinde bizim
        // WheelCollider denetleyicimiz değil RCCPCarAdapter olur, ikisi de bu arayüzü sunar.
        // Not: Unity arayüz alanlarını serialize etmez, bu alan Inspector'da görünmez —
        // aynı GameObject'teyse Awake kendisi bulur, değilse koddan atanmalı.
        public IDriveInput car;
        public float defaultTargetKmh = 80f;
        public KeyCode toggleKey = KeyCode.C;

        public bool Active { get; private set; }
        public float TargetKmh { get; set; }

        // Arayüz referansı UnityEngine.Object olmadığı için "!car" / "car ? :" kısayolları
        // kullanılamaz; açık null karşılaştırması gerekiyor. GetComponent arayüzleri çözer.
        void Awake() { if (car == null) car = GetComponent<IDriveInput>(); TargetKmh = defaultTargetKmh; }

        public void Toggle() { Active = !Active; if (Active) TargetKmh = Mathf.Max(car != null ? car.SpeedKmh : defaultTargetKmh, defaultTargetKmh); }
        public void Cancel() => Active = false;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        // LateUpdate: MobileTouchInput gaz/fren/direksiyonu Update'te yazıyor ve iki bileşen
        // arasında Update sırası garanti değil — sabit hız Update'te uygulandığında oyuncu
        // input'u tarafından eziliyor, yani hiçbir şey yapmıyordu. LateUpdate her zaman tüm
        // Update'lerden sonra çalışır, yani yazdığımız değer bu kare içinde ezilmez.
        void LateUpdate()
        {
            if (!Active || car == null) return;

            if (car.BrakeInput > 0.01f || car.Handbrake) { Cancel(); return; }

            float diff = TargetKmh - car.SpeedKmh;
            float throttle = Mathf.Clamp(diff * 0.05f, -0.5f, 1f);
            car.Move(throttle, 0f, car.SteerInput, false);
        }
    }
}
