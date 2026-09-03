using UnityEngine;
using DreamCar.Car;
using DreamCar.Settings;

namespace DreamCar.Vehicle
{
    // Sürüş yardımcıları: ABS, patinaj denetimi (TC), ESP, diferansiyel, aero.
    //
    // BU BİLEŞEN VehicleTelemetry'NİN TÜKETİCİSİDİR. Faz 1'de yazdığım
    // IVehicleStats / VehicleTelemetry (tekerlek başına slip veren okuma
    // sözleşmesi) tam olarak bunun için vardı ama hiçbir yerden okunmuyordu —
    // yani telemetri de "yazılmış ama çağrılmayan" ailesindeydi. Bu bileşen ona
    // tüketicisini veriyor.
    //
    // YALNIZCA BİZİM CarController'IMIZLA çalışır. RCCP kendi ABS/TC/ESP'ini
    // getiriyor ve ona reflection ile güvenilir per-wheel tork müdahalesi
    // yapamayız; RCCP'li araçta bu bileşen Awake'te kendini kapatıyor.
    //
    // CarController tek fizik otoritesidir: bu bileşen tekerleklere DOĞRUDAN
    // yazmaz. CarController her fizik adımında BeginStep'i çağırır, sonra aks
    // döngüsünde ModulateBrake/ModulateMotor'dan geçirir. FuelSystem'in
    // EngineCutoff bayrağıyla aynı desen — dış bileşen veri verir, yazan
    // CarController'dır.
    [RequireComponent(typeof(Rigidbody))]
    public class DrivingAssists : MonoBehaviour
    {
        [Header("ABS")]
        // Tekerlek bu kadar kilitliyse (negatif forwardSlip) fren bırakılır.
        public float absSlipThreshold = 0.35f;
        [Range(0f, 1f)] public float absReleaseFactor = 0.15f;

        [Header("Patinaj denetimi (TC)")]
        // Çekiş tekerleği bu kadar patinaj yapıyorsa (pozitif forwardSlip) motor kısılır.
        public float tcSlipThreshold = 0.40f;
        [Range(0f, 1f)] public float tcCutFactor = 0.25f;

        [Header("ESP")]
        // Gerçek yaw ile istenen yaw arası bu kadar (rad/s) fark aşılınca müdahale.
        public float espYawThreshold = 0.25f;
        public float espBrakeTorque = 900f;
        // Çok düşük hızda ESP kapalı: manevra ve park savrulma sayılmasın.
        public float espMinSpeedKmh = 25f;

        [Header("Diferansiyel (LSD benzeri)")]
        // Patinaj yapan tekerleğe tork bu orana kadar kısılır; tutan tekerlekte kalır.
        [Range(0f, 1f)] public float lsdMinFactor = 0.4f;

        [Header("Aero")]
        // Hız-kare orantılı sürükleme; yüksek hızda düz çizgi kararlılığı.
        public float aeroDragCoefficient = 0.9f;

        // Telltale için: son adımda hangi yardımcı müdahale etti?
        [System.Flags]
        public enum Assist { None = 0, Abs = 1, Tc = 2, Esp = 4 }
        public Assist ActiveIntervention { get; private set; }

        Rigidbody _rb;
        IVehicleStats _stats;
        VehicleTelemetry _telemetry;
        CarController _controller;

        // Tekerlek rolleri COM'a göre bir kez sınıflanıyor (ESP doğru tekerleği
        // frenlesin diye). WheelCollider referansından bakılıyor.
        WheelCollider[] _wheels;
        bool[] _isFront;
        bool[] _isLeft;

        // Ayar bayrakları önbellekte: FixedUpdate'te PlayerPrefs okumamak için.
        bool _abs = true, _tc = true, _esp = true;

        // BeginStep'te bir kez hesaplanan adım durumu.
        Assist _stepFlags;

        // ESP kararı BeginStep'te bir kez veriliyor; per-wheel çağrılar yalnızca
        // "bu tekerlek frenlenecek mi" diye bakıyor.
        bool _espActive;
        bool _espBrakeFront;
        bool _espBrakeLeft;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _stats = GetComponent<IVehicleStats>();
            _telemetry = GetComponent<VehicleTelemetry>();
            _controller = GetComponent<CarController>();

            // RCCP'li araç: kendi yardımcıları var, biz devre dışıyız.
            if (_controller == null) { enabled = false; return; }

            _wheels = GetComponentsInChildren<WheelCollider>(true);
            ClassifyWheels();
        }

        void OnEnable() => Refresh();

        // Ayar değişince (duraklama menüsü kapanınca) çağrılıyor. Bayraklar
        // önbellekte tutuluyor; FixedUpdate her karede PlayerPrefs okumamalı.
        public void Refresh()
        {
            var s = GameSettings.Instance;
            if (s == null) return;   // varsayılanlar açık kalır
            _abs = s.AbsEnabled;
            _tc = s.TractionControlEnabled;
            _esp = s.StabilityControlEnabled;
        }

        void ClassifyWheels()
        {
            _isFront = new bool[_wheels.Length];
            _isLeft = new bool[_wheels.Length];
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (!_wheels[i]) continue;
                Vector3 local = transform.InverseTransformPoint(_wheels[i].transform.position);
                _isFront[i] = local.z >= 0f;
                _isLeft[i] = local.x < 0f;
            }
        }

        // CarController aks döngüsünden ÖNCE çağırıyor. Adım genelindeki durumu
        // burada bir kez hesaplıyoruz; per-wheel çağrılar bunu okuyor.
        public void BeginStep(float steerAngleDeg, float speedKmh)
        {
            _stepFlags = Assist.None;

            // --- ESP: istenen yaw vs gerçek yaw ---
            _espActive = false;
            if (_esp && speedKmh > espMinSpeedKmh)
            {
                // Bisiklet modeli yaklaşımı: istenen yaw hızı ≈ v * tan(δ) / L.
                // L sabit bir makul dingil mesafesi (2.6 m) — tam doğruluk
                // gerekmez, işaret ve büyüklük yeter.
                float v = speedKmh / 3.6f;
                float desiredYaw = v * Mathf.Tan(steerAngleDeg * Mathf.Deg2Rad) / 2.6f;
                float actualYaw = _rb.angularVelocity.y;

                if (Mathf.Abs(desiredYaw - actualYaw) > espYawThreshold)
                {
                    _espActive = true;

                    // İki durumlu oyun sezgisi:
                    //   az dönüş  = araç istenenden AZ dönüyor (|gerçek|<|istenen|)
                    //   aşırı dönüş = araç istenenden ÇOK dönüyor
                    bool understeer = Mathf.Abs(actualYaw) < Mathf.Abs(desiredYaw);

                    // İstenen dönüş yönü (pozitif yaw = sola).
                    bool turningLeft = desiredYaw > 0f;

                    if (understeer)
                    {
                        // Burnu içeri sok: İÇ ARKA tekerleği frenle.
                        _espBrakeLeft = turningLeft;
                        _espBrakeFront = false;
                    }
                    else
                    {
                        // Savrulmayı kes: DIŞ ÖN tekerleği frenle.
                        _espBrakeLeft = !turningLeft;
                        _espBrakeFront = true;
                    }
                }
            }

            // --- Aero: hız-kare sürükleme, ileri eksende ---
            if (aeroDragCoefficient > 0f)
            {
                float v = _rb.linearVelocity.magnitude;
                if (v > 0.1f)
                    _rb.AddForce(-_rb.linearVelocity.normalized * aeroDragCoefficient * v * v);
            }
        }

        // Fren torkunu modüle eder: ABS + ESP. El freni payı BU METODA GELMİYOR
        // (CarController ayrı ekliyor) — drift el frenle yapılıyor, ABS onu
        // bozmamalı.
        public float ModulateBrake(WheelCollider wheel, float requestedBrake)
        {
            int i = ResolveIndex(wheel);
            if (i < 0) return requestedBrake;

            float brake = requestedBrake;

            // --- ABS: kilitlenen tekerlekte fren bırak ---
            if (_abs && requestedBrake > 1f)
            {
                var t = _stats.GetWheel(i);
                if (t.grounded && t.forwardSlip < -absSlipThreshold)
                {
                    brake *= absReleaseFactor;
                    _stepFlags |= Assist.Abs;
                }
            }

            // --- ESP: BeginStep'te seçilen tek tekerleği frenle ---
            if (_espActive && _isLeft[i] == _espBrakeLeft && _isFront[i] == _espBrakeFront)
            {
                brake += espBrakeTorque;
                _stepFlags |= Assist.Esp;
            }

            return brake;
        }

        // Motoru modüle eder: TC + diferansiyel.
        public float ModulateMotor(WheelCollider wheel, float requestedMotor)
        {
            int i = ResolveIndex(wheel);
            if (i < 0 || Mathf.Approximately(requestedMotor, 0f)) return requestedMotor;

            var t = _stats.GetWheel(i);
            float motor = requestedMotor;

            // --- TC: patinaj yapan çekiş tekerleğinde motoru kıs ---
            if (_tc && t.grounded && t.forwardSlip > tcSlipThreshold)
            {
                motor *= tcCutFactor;
                _stepFlags |= Assist.Tc;
            }

            // --- Diferansiyel (LSD benzeri): patinaj arttıkça o tekerleğe
            // giden torku kıs. Açık diferansiyel patinayan tekerleğe torku
            // KAYBEDER; LSD/kilitli bunu engeller. Oyun hissi için tutan
            // tekerlekte tork kalsın istiyoruz.
            if (t.grounded && t.forwardSlip > 0f)
            {
                float slipT = Mathf.Clamp01(t.forwardSlip / (tcSlipThreshold * 2f));
                float lsd = Mathf.Lerp(1f, lsdMinFactor, slipT);
                motor *= lsd;
            }

            return motor;
        }

        // BeginStep'te toplanan bayraklar telltale'a burada aktarılıyor:
        // per-wheel çağrılar bittikten sonra CarController EndStep çağırıyor.
        public void EndStep() => ActiveIntervention = _stepFlags;

        int ResolveIndex(WheelCollider wheel)
        {
            if (_telemetry != null) return _telemetry.IndexOf(wheel);
            // Telemetri yoksa yardımcılar slip okuyamaz — güvenli taraf: no-op.
            return -1;
        }
    }
}
