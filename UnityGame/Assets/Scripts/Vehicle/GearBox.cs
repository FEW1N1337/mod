using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Vehicle
{
    // Otomatik şanzıman: hıza göre vites belirler, HUD'a gösterir.
    //
    // Somut sürücü tipi yerine IDriveInput kullanılıyor: RCCP ile sürülen araçta bizim
    // WheelCollider denetleyicimiz yok, RCCPCarAdapter var. Bu yüzden o bileşeni şart
    // koşan [RequireComponent(typeof(...))] kaldırıldı — RCCP'li araçta boş bir sürücü
    // bileşeni eklenmesine ve iki denetleyicinin çakışmasına yol açardı.
    public class GearBox : MonoBehaviour
    {
        public float[] gearSpeedLimits = { 30f, 60f, 100f, 140f, 180f, 220f };
        public int currentGear;
        public bool isReverse;

        IDriveInput _car;
        // GetComponent arayüzleri de çözer; hangi somut sürücü varsa onu buluruz.
        void Awake() => _car = GetComponent<IDriveInput>();

        void Update()
        {
            // RequireComponent kalktığı için sürücünün varlığı artık garanti değil; arayüz
            // referansı UnityEngine.Object olmadığından açık null kontrolü yapıyoruz.
            if (_car == null) return;

            // Eski hali: isReverse = throttle < -0.05 && SpeedKmh < 2. Geri giderken hız
            // 2 km/s'yi geçer geçmez vites "R" olmaktan çıkıp ileri vitese atlıyordu.
            // Geri vites, gaz negatif kaldığı sürece korunmalı; ileri gaz veya duruş bozar.
            if (_car.ThrottleInput < -0.05f && (isReverse || _car.SpeedKmh < 2f)) isReverse = true;
            else if (_car.ThrottleInput > 0.05f || _car.SpeedKmh < 0.5f) isReverse = false;
            currentGear = isReverse ? 0 : Util.GameMath.GearForSpeed(_car.SpeedKmh, gearSpeedLimits);
        }

        public string GearLabel => isReverse ? "R" : (currentGear == 0 ? "N" : currentGear.ToString());
    }
}
