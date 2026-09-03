using Photon.Pun;
using UnityEngine;
using DreamCar.Car;
using DreamCar.Vehicle;

namespace DreamCar.Core
{
    // PlayerStats'e veri akıtan bileşen. Araç prefab'ına eklenir; sadece sahibi olan
    // araçta çalışır. Mesafe, süre, en yüksek hız ve çarpışma sayısını biriktirir.
    public class StatsTracker : MonoBehaviour
    {
        public float flushIntervalSeconds = 5f;
        public float minCollisionImpulse = 200f;

        IDriveInput _drive;
        PhotonView _pv;
        Vector3 _lastPosition;
        float _pendingDistance;
        float _pendingTime;
        float _flushTimer;
        bool _tracking;

        void Awake()
        {
            _pv = GetComponent<PhotonView>();
            _drive = GetComponent<IDriveInput>();
            _lastPosition = transform.position;
        }

        void Start()
        {
            _tracking = _pv == null || _pv.IsMine;
            enabled = _tracking;
        }

        void Update()
        {
            if (!_tracking || PlayerStats.Instance == null) return;

            float moved = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;

            // Teleport/spawn sıçramalarını mesafeye yazma.
            if (moved < 50f) _pendingDistance += moved;

            if (_drive != null)
            {
                float speed = _drive.SpeedKmh;
                if (speed > 1f) _pendingTime += Time.deltaTime;
                PlayerStats.Instance.ReportSpeed(speed);
            }

            _flushTimer += Time.deltaTime;
            if (_flushTimer >= flushIntervalSeconds) Flush();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!_tracking || PlayerStats.Instance == null) return;
            if (collision.impulse.magnitude < minCollisionImpulse) return;
            PlayerStats.Instance.ReportCollision();
        }

        void OnDisable() => Flush();
        void OnApplicationPause(bool paused) { if (paused) Flush(); }

        void Flush()
        {
            if (!_tracking || PlayerStats.Instance == null) return;
            _flushTimer = 0f;

            if (_pendingDistance > 0.01f)
            {
                PlayerStats.Instance.AddDistance(_pendingDistance);

                // Başarım tarafına da bildir: PlayFabAchievements.OnDistanceTravelled
                // projede hiçbir yerden çağrılmıyordu, yani distanceMeters istatistiği
                // hiç artmıyor ve mesafeye bağlı başarımlar hiç açılamıyordu.
                var ach = Backend.PlayFabAchievements.Instance;
                if (ach) ach.OnDistanceTravelled(_pendingDistance);

                _pendingDistance = 0f;
            }
            if (_pendingTime > 0.01f)
            {
                PlayerStats.Instance.AddDriveTime(_pendingTime);
                _pendingTime = 0f;
            }
        }
    }
}
