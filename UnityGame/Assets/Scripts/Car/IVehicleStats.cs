using UnityEngine;

namespace DreamCar.Car
{
    // Tek bir tekerleğin anlık durumu. ABS, patinaj denetimi, ESP ve lastik sesi
    // hepsi aynı üç sayıya bakıyor; her biri kendi başına WheelCollider aramasın
    // diye tek bir yapıda topluyoruz.
    //
    // Alan adları Unity'nin WheelHit yapısıyla bilerek aynı: forwardSlip pozitifse
    // tekerlek patinaj yapıyor, negatifse kilitleniyor.
    public struct WheelTelemetry
    {
        public bool grounded;
        public float rpm;
        public float forwardSlip;
        public float sidewaysSlip;
        public float steerAngleDeg;
        public bool isDriven;
        public bool isSteered;
    }

    // Araçtan OKUNAN her şey. IDriveInput yazma tarafı, bu okuma tarafı.
    //
    // Ayrı iki arayüz olmasının sebebi: HUD, telemetri, başarım ve ses sistemleri
    // yalnızca okur — onlara Move() verebilen bir referans vermek, ileride yanlışlıkla
    // aracı sürebilecekleri anlamına gelirdi. Sürüş yardımcıları (ABS/TC/ESP) ise
    // ikisini birden alır: buradan okur, IDriveInput'a yazar.
    //
    // Uygulayan taraf VehicleTelemetry; hem kendi CarController'ımızla hem
    // RCCPCarAdapter ile çalışır çünkü ikisi de WheelCollider kullanıyor.
    public interface IVehicleStats
    {
        float SpeedKmh { get; }
        float TopSpeedKmh { get; }

        // Türetilmiş devir. Gerçek bir motor eğrisi değil — vites bandı içindeki
        // ilerlemeden hesaplanıyor (bkz. VehicleTelemetry). Gösterge ve ses için
        // yeterli, fizik kararı için kullanılmamalı.
        float EngineRpm { get; }
        float IdleRpm { get; }
        float RedlineRpm { get; }

        // 0 = boş/rölanti, -1 = geri, 1..n = ileri vitesler.
        int Gear { get; }
        string GearLabel { get; }

        // FixedUpdate'te tekerleklere gerçekten yazılan tork (Nm). Nitro ve
        // yükseltmeler sonrası değeri — istenen değil, uygulanan.
        float DriveTorqueNm { get; }

        float FuelLitres { get; }
        float FuelCapacityLitres { get; }

        int WheelCount { get; }
        WheelTelemetry GetWheel(int index);

        // En az bir tekerlek yerdeyse true. CarRescue ve ESP buna bakar.
        bool IsGrounded { get; }
    }
}
