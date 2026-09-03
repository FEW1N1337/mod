using UnityEngine;

namespace DreamCar.Car
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour, IDriveInput
    {
        public float TopSpeedKmh => topSpeedKmh;
        public float ThrottleInput => throttleInput;
        public float BrakeInput => brakeInput;
        public float SteerInput => steerInput;
        public bool Handbrake => handbrake;
        public bool EngineCutoff { get => engineCutoff; set => engineCutoff = value; }

        [System.Serializable]
        public class AxleInfo
        {
            public WheelCollider leftWheel;
            public WheelCollider rightWheel;
            public Transform leftMesh;
            public Transform rightMesh;
            public bool motor;
            public bool steering;
        }

        [Header("Wheels")]
        public AxleInfo[] axles;

        [Header("Drive")]
        public float maxMotorTorque = 1500f;
        public float maxBrakeTorque = 3000f;
        public float maxSteeringAngle = 30f;
        public float topSpeedKmh = 180f;

        [Header("Handling")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.6f, 0f);
        public float downForce = 80f;

        // Yüksek hızda tam direksiyon açısı aracı anında takla attırıyordu (WheelCollider
        // steerAngle doğrudan yazıldığı için hız süzgeci yok). Açıyı hızla birlikte kısıyoruz:
        // dururken tam açı, steerFalloffSpeedKmh ve üstünde minSteeringFactor kadarı.
        // topSpeedKmh yerine ayrı bir eşik: CarNitro topSpeedKmh'yi runtime'da değiştiriyor,
        // nitro basınca direksiyonun aniden ağırlaşmaması için bağımsız olmalı.
        public float steerFalloffSpeedKmh = 140f;
        [Range(0.1f, 1f)] public float minSteeringFactor = 0.35f;

        // Yakıt bitince motoru kesmek için FuelSystem bunu set eder. FuelSystem eskiden
        // doğrudan throttleInput/brakeInput alanlarına yazıyordu ama MobileTouchInput her
        // karede Move() ile o alanları eziyordu ve Update sırası garanti değil — kesme bu
        // yüzden çoğu karede hiçbir şey yapmıyordu. Karar FixedUpdate'te veriliyor.
        [HideInInspector] public bool engineCutoff;

        [Header("Runtime input (0..1 / -1..1)")]
        [Range(-1f, 1f)] public float throttleInput;
        [Range(0f, 1f)] public float brakeInput;
        [Range(-1f, 1f)] public float steerInput;
        public bool handbrake;

        Rigidbody _rb;
        public float SpeedKmh => _rb ? _rb.linearVelocity.magnitude * 3.6f : 0f;

        // Nitro, turbo, lastik ve süspansiyon gibi her şey bu tablo üzerinden
        // etki ediyor; aşağıdaki alanların hiçbiri çalışma anında yazılmaz.
        // Gerekçesi VehicleStatSheet'in başında.
        VehicleStatSheet _sheet;

        // Sürüş yardımcıları (ABS/TC/ESP/diferansiyel/aero). Opsiyonel: yoksa
        // davranış birebir eskisi. Yazan hâlâ biziz; yardımcı yalnızca
        // tekerlek başına tork/fren değerini modüle ediyor.
        DreamCar.Vehicle.DrivingAssists _assists;
        float _baseMass;
        WheelFrictionCurve[] _baseForward;
        WheelFrictionCurve[] _baseSideways;
        WheelCollider[] _allWheels;
        float _appliedGrip = 1f;

        // Tabloya kimse dokunmadıysa Evaluate çağırmaya da gerek yok.
        float Stat(VehicleStat stat, float baseValue)
            => _sheet != null ? _sheet.Evaluate(stat, baseValue) : baseValue;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMassOffset;

            _sheet = GetComponent<VehicleStatSheet>();
            _assists = GetComponent<DreamCar.Vehicle.DrivingAssists>();
            _baseMass = _rb.mass;
            CacheWheelFriction();

            // Prefab'ta ayarlanmamışsa güvenli varsayılanlar. 180+ km/s'te (50 m/s) araç
            // 0.02 sn'lik fizik adımında 1 metreden fazla yol alıyor; Discrete çarpışma
            // tespitiyle ince duvarlardan/bariyerlerden geçip gidiyor. Interpolation kapalıyken
            // de görüntü fizik frekansında titriyor. Zaten ayarlanmışsa dokunmuyoruz.
            // Kinematik gövdeler Continuous modları desteklemez, onları atlıyoruz.
            if (!_rb.isKinematic && _rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (_rb.interpolation == RigidbodyInterpolation.None)
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Move(float throttle, float brake, float steer, bool hand)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            brakeInput = Mathf.Clamp01(brake);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            handbrake = hand;
        }

        void FixedUpdate()
        {
            // Direksiyon açısı hızla birlikte kısılır — bkz. steerFalloffSpeedKmh yorumu.
            float steerFactor = Mathf.Lerp(1f, minSteeringFactor,
                Mathf.InverseLerp(0f, Mathf.Max(1f, steerFalloffSpeedKmh), SpeedKmh));
            float steer = Stat(VehicleStat.SteeringAngle, maxSteeringAngle) * steerFactor * steerInput;

            float torqueLimit = Stat(VehicleStat.MotorTorque, maxMotorTorque);
            float speedLimit = Stat(VehicleStat.TopSpeed, topSpeedKmh);
            float brakeLimit = Stat(VehicleStat.BrakeTorque, maxBrakeTorque);

            float motor = torqueLimit * throttleInput;
            if (SpeedKmh > speedLimit) motor = 0f;
            float brake = brakeLimit * brakeInput;
            float hand = handbrake ? brakeLimit : 0f;

            ApplyGripIfChanged();

            // Yakıt bitti: gaz kesilir, fren uygulanır. Input alanlarına değil buraya
            // bakıyoruz ki her karede input yazan MobileTouchInput bunu ezemesin.
            if (engineCutoff) { motor = 0f; brake = brakeLimit; }

            // Yardımcılar adım durumunu (ESP yaw hatası, aero) döngüden ÖNCE
            // bir kez hesaplasın; per-wheel çağrılar bunu okuyor.
            if (_assists && _assists.enabled) _assists.BeginStep(steer, SpeedKmh);

            foreach (var axle in axles)
            {
                if (axle.steering)
                {
                    axle.leftWheel.steerAngle = steer;
                    axle.rightWheel.steerAngle = steer;
                }
                if (axle.motor)
                {
                    axle.leftWheel.motorTorque = ModulateMotor(axle.leftWheel, motor);
                    axle.rightWheel.motorTorque = ModulateMotor(axle.rightWheel, motor);
                }

                // El freni yardımcıdan GEÇMİYOR: drift el frenle yapılıyor,
                // ABS onu bozmamalı. Servis freni ayrı modüle ediliyor.
                axle.leftWheel.brakeTorque = ModulateBrake(axle.leftWheel, brake) + hand;
                axle.rightWheel.brakeTorque = ModulateBrake(axle.rightWheel, brake) + hand;

                SyncMesh(axle.leftWheel, axle.leftMesh);
                SyncMesh(axle.rightWheel, axle.rightMesh);
            }

            if (_assists && _assists.enabled) _assists.EndStep();

            _rb.AddForce(-transform.up * Stat(VehicleStat.Downforce, downForce) * _rb.linearVelocity.magnitude);

            // Kütle her karede yazılmıyor: Rigidbody.mass'e yazmak atalet tensörünü
            // yeniden hesaplatıyor, sabit değerle bile boşuna maliyet.
            float mass = Stat(VehicleStat.Mass, _baseMass);
            if (!Mathf.Approximately(_rb.mass, mass)) _rb.mass = mass;
        }

        float ModulateMotor(WheelCollider wheel, float requestedMotor)
            => _assists && _assists.enabled ? _assists.ModulateMotor(wheel, requestedMotor) : requestedMotor;

        float ModulateBrake(WheelCollider wheel, float requestedBrake)
            => _assists && _assists.enabled ? _assists.ModulateBrake(wheel, requestedBrake) : requestedBrake;

        void CacheWheelFriction()
        {
            _allWheels = GetComponentsInChildren<WheelCollider>(true);
            _baseForward = new WheelFrictionCurve[_allWheels.Length];
            _baseSideways = new WheelFrictionCurve[_allWheels.Length];
            for (int i = 0; i < _allWheels.Length; i++)
            {
                if (!_allWheels[i]) continue;
                _baseForward[i] = _allWheels[i].forwardFriction;
                _baseSideways[i] = _allWheels[i].sidewaysFriction;
            }
        }

        // Tutuş, sürtünme eğrisinin stiffness'ı üzerinden uygulanıyor. Lastik ve
        // süspansiyon modülleri (Faz 5) buraya yazacak. Değer değişmediyse eğrilere
        // dokunulmuyor: WheelFrictionCurve atamak eğriyi yeniden derliyor.
        void ApplyGripIfChanged()
        {
            float grip = Stat(VehicleStat.Grip, 1f);
            if (Mathf.Approximately(grip, _appliedGrip) || _allWheels == null) return;
            _appliedGrip = grip;

            for (int i = 0; i < _allWheels.Length; i++)
            {
                if (!_allWheels[i]) continue;
                var f = _baseForward[i];
                f.stiffness = _baseForward[i].stiffness * grip;
                _allWheels[i].forwardFriction = f;

                var sf = _baseSideways[i];
                sf.stiffness = _baseSideways[i].stiffness * grip;
                _allWheels[i].sidewaysFriction = sf;
            }
        }

        // Uzak (başka oyuncuya ait) araçlarda Rigidbody kinematik, WheelCollider
        // simüle edilmiyor ve bu bileşen KAPALI — yani FixedUpdate hiç
        // koşmuyor. Tekerlek mesh'leri bu yüzden ne dönüyor ne kırılıyordu:
        // diğer oyuncular donmuş tekerlekli kutular gibi kayıyordu.
        // CarNetworkSync ağdan gelen hız ve direksiyonla burayı çağırıyor.
        //
        // Mesh'ler aracın kökünün çocuğu (WheelCollider'ın değil) ve doğru
        // yerel konumda duruyorlar, o yüzden yalnızca yerel DÖNÜŞ yazılıyor.
        float _remoteSpinDegrees;

        public void ApplyRemoteVisuals(float speedKmh, float steerInput01, float deltaTime)
        {
            if (axles == null || axles.Length == 0) return;

            float radius = 0.34f;
            if (axles[0].leftWheel) radius = Mathf.Max(0.05f, axles[0].leftWheel.radius);

            // ω = v / r  (rad/s) → dereceye çevir.
            _remoteSpinDegrees += (speedKmh / 3.6f) / radius * Mathf.Rad2Deg * deltaTime;
            _remoteSpinDegrees = Mathf.Repeat(_remoteSpinDegrees, 360f);

            float steerDegrees = maxSteeringAngle * Mathf.Clamp(steerInput01, -1f, 1f);

            foreach (var axle in axles)
            {
                float s = axle.steering ? steerDegrees : 0f;
                SetRemoteWheel(axle.leftMesh, s);
                SetRemoteWheel(axle.rightMesh, s);
            }
        }

        void SetRemoteWheel(Transform mesh, float steerDegrees)
        {
            if (!mesh) return;
            mesh.localRotation = Quaternion.Euler(_remoteSpinDegrees, steerDegrees, 0f);
        }

        static void SyncMesh(WheelCollider col, Transform mesh)
        {
            if (!mesh || !col) return;
            col.GetWorldPose(out Vector3 p, out Quaternion r);
            mesh.SetPositionAndRotation(p, r);
        }
    }
}
