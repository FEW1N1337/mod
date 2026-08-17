// RCCP (Realistic Car Controller Pro) — BoneCracker Games. Asset Store'dan import edilir.
// Aktive etmek için: Player Settings → Other Settings → Scripting Define Symbols → RCCP_INSTALLED
using UnityEngine;
using DreamCar.Car;

#if RCCP_INSTALLED
using RCCP;
#endif

namespace DreamCar.RCCPBridge
{
    // RCCP_CarController'ı IDriveInput arayüzü altında sarar. Böylece MobileTouchInput,
    // CarNetworkSync, HUD ve diğer sistemler RCCP'nin varlığından habersiz çalışır.
    [RequireComponent(typeof(Rigidbody))]
    public class RCCPCarAdapter : MonoBehaviour, IDriveInput
    {
        public float overrideTopSpeedKmh = 220f;

        float _throttle, _brake, _steer;
        bool _hand;

#if RCCP_INSTALLED
        RCCP_CarController _rccp;

        void Awake()
        {
            _rccp = GetComponent<RCCP_CarController>();
            if (!_rccp) Debug.LogWarning("[RCCPCarAdapter] RCCP_CarController component eksik.");
        }
#endif

        public void Move(float throttle, float brake, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _brake = Mathf.Clamp01(brake);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _hand = handbrake;

#if RCCP_INSTALLED
            if (!_rccp) return;
            // RCCP input override — sürüş yerine bizim değerlerimizi kullansın.
            _rccp.throttleInput_V = Mathf.Max(0f, _throttle);
            _rccp.brakeInput_V = _brake + (_throttle < 0f ? -_throttle : 0f);
            _rccp.steerInput_V = _steer;
            _rccp.handbrakeInput_V = _hand ? 1f : 0f;
            _rccp.overrideInputs = true;
#endif
        }

        public float SpeedKmh
        {
            get
            {
#if RCCP_INSTALLED
                if (_rccp) return _rccp.speed;
#endif
                var rb = GetComponent<Rigidbody>();
                return rb ? rb.linearVelocity.magnitude * 3.6f : 0f;
            }
        }

        public float TopSpeedKmh => overrideTopSpeedKmh;
        public float ThrottleInput => _throttle;
        public float BrakeInput => _brake;
        public float SteerInput => _steer;
    }
}
