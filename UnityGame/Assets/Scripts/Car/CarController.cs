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

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMassOffset;

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
            float steer = maxSteeringAngle * steerFactor * steerInput;

            float motor = maxMotorTorque * throttleInput;
            if (SpeedKmh > topSpeedKmh) motor = 0f;
            float brake = maxBrakeTorque * brakeInput;
            float hand = handbrake ? maxBrakeTorque : 0f;

            // Yakıt bitti: gaz kesilir, fren uygulanır. Input alanlarına değil buraya
            // bakıyoruz ki her karede input yazan MobileTouchInput bunu ezemesin.
            if (engineCutoff) { motor = 0f; brake = maxBrakeTorque; }

            foreach (var axle in axles)
            {
                if (axle.steering)
                {
                    axle.leftWheel.steerAngle = steer;
                    axle.rightWheel.steerAngle = steer;
                }
                if (axle.motor)
                {
                    axle.leftWheel.motorTorque = motor;
                    axle.rightWheel.motorTorque = motor;
                }
                axle.leftWheel.brakeTorque = brake + hand;
                axle.rightWheel.brakeTorque = brake + hand;

                SyncMesh(axle.leftWheel, axle.leftMesh);
                SyncMesh(axle.rightWheel, axle.rightMesh);
            }

            _rb.AddForce(-transform.up * downForce * _rb.linearVelocity.magnitude);
        }

        static void SyncMesh(WheelCollider col, Transform mesh)
        {
            if (!mesh || !col) return;
            col.GetWorldPose(out Vector3 p, out Quaternion r);
            mesh.SetPositionAndRotation(p, r);
        }
    }
}
