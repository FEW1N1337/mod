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
            float steer = maxSteeringAngle * steerInput;
            float motor = maxMotorTorque * throttleInput;
            if (SpeedKmh > topSpeedKmh) motor = 0f;
            float brake = maxBrakeTorque * brakeInput;
            float hand = handbrake ? maxBrakeTorque : 0f;

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
