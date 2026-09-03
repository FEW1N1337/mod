using UnityEngine;
using DreamCar.Vehicle;

namespace DreamCar.Car
{
    // IVehicleStats'in tek uygulaması.
    //
    // Neden CarController'ın kendisi uygulamıyor: RCCP ile sürülen araçta bizim
    // CarController'ımız yok, RCCPCarAdapter var. Telemetriyi sürücüye gömseydik
    // iki ayrı uygulama yazmak ve ikisini senkron tutmak gerekirdi. Oysa okunan
    // verilerin tamamı sürücüden bağımsız kaynaklardan geliyor: WheelCollider'lar
    // (RCCP de onları kullanıyor), GearBox, FuelSystem ve IDriveInput arayüzü.
    // Bu yüzden tek bileşen ikisiyle de çalışıyor.
    //
    // Bileşen yalnızca OKUR. Hiçbir şeye yazmaz.
    public class VehicleTelemetry : MonoBehaviour, IVehicleStats
    {
        [Header("Devir (türetilmiş)")]
        public float idleRpm = 800f;
        public float redlineRpm = 7000f;

        IDriveInput _drive;
        GearBox _gears;
        FuelSystem _fuel;
        WheelCollider[] _wheels;

        // Tekerlek durumu FİZİK ADIMI başına en fazla bir kez toplanır: ABS,
        // patinaj denetimi, lastik sesi ve fren izi aynı veriyi ayrı ayrı
        // sorgularsa GetGroundHit dört kez koşardı.
        //
        // Önbellek anahtarı Time.frameCount DEĞİL Time.fixedTime: sürüş
        // yardımcıları FixedUpdate'te koşuyor ve bir görüntü karesinde birden
        // fazla fizik adımı olabiliyor. Kare sayısına göre önbelleklenseydi
        // ikinci ve sonraki adımlar ilk adımın kayma değerlerini okurdu.
        // WheelCollider verisi zaten yalnızca fizik adımında değişiyor, doğru
        // granülerlik bu.
        WheelTelemetry[] _telemetry;
        float _telemetryTime = -1f;

        // Hangi tekerlek çekiş, hangisi direksiyon? Anlık motorTorque'a bakmak
        // yanıltıcı olurdu: gaz bırakılmış bir çekiş tekerleği "çekiş değil"
        // görünürdü ve patinaj denetimi onu görmezden gelirdi. Bu yüzden rol
        // bir kez belirleniyor.
        bool[] _driven;
        bool[] _steered;

        void Awake()
        {
            _drive = GetComponent<IDriveInput>();
            _gears = GetComponent<GearBox>();
            _fuel = GetComponent<FuelSystem>();
            _wheels = GetComponentsInChildren<WheelCollider>(true);
            _telemetry = new WheelTelemetry[_wheels.Length];
            _driven = new bool[_wheels.Length];
            _steered = new bool[_wheels.Length];
            ResolveWheelRoles();
        }

        void ResolveWheelRoles()
        {
            // Kendi denetleyicimiz varsa rolleri aks tanımından okuyoruz — kesin bilgi.
            var controller = GetComponent<CarController>();
            if (controller != null && controller.axles != null)
            {
                foreach (var axle in controller.axles)
                {
                    if (axle == null) continue;
                    MarkRole(axle.leftWheel, axle.motor, axle.steering);
                    MarkRole(axle.rightWheel, axle.motor, axle.steering);
                }
                return;
            }

            // RCCP'li araçta aks tanımına erişimimiz yok (RCCP'ye tip bağımlılığı
            // kurmuyoruz, bkz. RCCPCarAdapter). Rol, çalışma anında ilk kez tork
            // veya direksiyon açısı gördüğümüzde işaretleniyor ve bir daha silinmiyor.
            // İlk saniyede eksik, sonrasında doğru.
        }

        void MarkRole(WheelCollider wheel, bool motor, bool steering)
        {
            if (!wheel) return;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != wheel) continue;
                if (motor) _driven[i] = true;
                if (steering) _steered[i] = true;
                return;
            }
        }

        public float SpeedKmh => _drive?.SpeedKmh ?? 0f;
        public float TopSpeedKmh => _drive?.TopSpeedKmh ?? 0f;
        public float IdleRpm => idleRpm;
        public float RedlineRpm => redlineRpm;

        public int Gear
        {
            get
            {
                if (_gears == null) return 1;
                return _gears.isReverse ? -1 : _gears.currentGear;
            }
        }

        public string GearLabel => _gears != null ? _gears.GearLabel : "-";

        // GERÇEK BİR MOTOR EĞRİSİ DEĞİL.
        //
        // Ne CarController'da ne RCCP köprüsünde vites oranı var; GearBox yalnızca
        // hız eşikleri tutuyor. Uydurma oranlar yazmak yerine devri, aracın içinde
        // bulunduğu vites bandındaki ilerlemeden türetiyoruz: vites başında rölanti,
        // vites sonunda kırmızı bölge. Otomatik şanzımanın davranışı zaten budur,
        // gösterge ve motor sesi için doğru sonucu verir.
        //
        // Fizik kararı bu değere DAYANDIRILMAMALI. Gerçek tork eğrisi Faz 3'te
        // motor modeliyle gelecek; o gün burası tek noktadan değişir.
        public float EngineRpm
        {
            get
            {
                float speed = SpeedKmh;
                if (_gears == null || _gears.gearSpeedLimits == null || _gears.gearSpeedLimits.Length == 0)
                {
                    float topFallback = Mathf.Max(1f, TopSpeedKmh);
                    return Mathf.Lerp(idleRpm, redlineRpm, Mathf.Clamp01(speed / topFallback));
                }

                var limits = _gears.gearSpeedLimits;
                int g = Mathf.Clamp(_gears.currentGear, 1, limits.Length);
                float bandStart = g <= 1 ? 0f : limits[g - 2];
                float bandEnd = limits[g - 1];
                if (bandEnd <= bandStart) return idleRpm;

                float t = Mathf.Clamp01((Mathf.Abs(speed) - bandStart) / (bandEnd - bandStart));
                return Mathf.Lerp(idleRpm, redlineRpm, t);
            }
        }

        // İSTENEN değil UYGULANAN tork: WheelCollider'a fiilen yazılmış değer.
        // Nitro, yükseltmeler ve yakıt kesmesi sonrası hâli — telemetri bu yüzden
        // sürücünün niyetini değil sonucu gösteriyor.
        public float DriveTorqueNm
        {
            get
            {
                if (_wheels == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < _wheels.Length; i++)
                    if (_wheels[i]) sum += _wheels[i].motorTorque;
                return sum;
            }
        }

        public float FuelLitres => _fuel ? _fuel.current : 0f;
        public float FuelCapacityLitres => _fuel ? _fuel.capacity : 0f;

        public int WheelCount => _wheels != null ? _wheels.Length : 0;

        public WheelTelemetry GetWheel(int index)
        {
            if (_wheels == null || index < 0 || index >= _wheels.Length) return default;
            SampleWheels();
            return _telemetry[index];
        }

        public bool IsGrounded
        {
            get
            {
                if (_wheels == null) return false;
                SampleWheels();
                for (int i = 0; i < _telemetry.Length; i++)
                    if (_telemetry[i].grounded) return true;
                return false;
            }
        }

        void SampleWheels()
        {
            if (_telemetryTime == Time.fixedTime) return;
            _telemetryTime = Time.fixedTime;

            for (int i = 0; i < _wheels.Length; i++)
            {
                var w = _wheels[i];
                if (!w) { _telemetry[i] = default; continue; }

                if (!Mathf.Approximately(w.motorTorque, 0f)) _driven[i] = true;
                if (!Mathf.Approximately(w.steerAngle, 0f)) _steered[i] = true;

                var t = new WheelTelemetry
                {
                    rpm = w.rpm,
                    steerAngleDeg = w.steerAngle,
                    isDriven = _driven[i],
                    isSteered = _steered[i],
                };

                // GetGroundHit yalnızca tekerlek yerdeyken hit doldurur; yerden
                // kesilmiş tekerlekte kayma değerleri eski karede kalırdı.
                if (w.GetGroundHit(out WheelHit hit))
                {
                    t.grounded = true;
                    t.forwardSlip = hit.forwardSlip;
                    t.sidewaysSlip = hit.sidewaysSlip;
                }

                _telemetry[i] = t;
            }
        }
    }
}
