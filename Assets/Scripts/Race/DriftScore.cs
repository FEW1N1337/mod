using UnityEngine;

namespace DreamCar.Race
{
    // Yan hız × zaman × açı = drift skoru. Grip'e girince skoru bank'a ekle, ceza ile sıfırla.
    [RequireComponent(typeof(Rigidbody))]
    public class DriftScore : MonoBehaviour
    {
        public float minSideVelocity = 3f;
        public float breakAngle = 90f;
        public System.Action<int> OnScoreChanged;
        public System.Action<int> OnCombo;

        Rigidbody _rb;
        float _currentDrift;
        int _bank;

        public int Bank => _bank;
        public int Current => Mathf.RoundToInt(_currentDrift);

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Yalnızca YEREL araçta çalışmalı. Bileşen artık her araç prefabında
            // olduğu için, guard olmadan uzak oyuncuların driftleri de bizim
            // istatistiğimize ve başarımlarımıza yazılırdı.
            // PhotonView yoksa tek oyunculu/Editor testi sayıyoruz.
            var pv = GetComponent<Photon.Pun.PhotonView>();
            if (pv && !pv.IsMine) enabled = false;
        }

        void FixedUpdate()
        {
            Vector3 vel = _rb.linearVelocity;
            Vector3 fwd = transform.forward;
            float side = Vector3.Dot(vel, transform.right);
            float speed = vel.magnitude;
            float angle = Vector3.Angle(fwd, vel.sqrMagnitude > 0.1f ? vel.normalized : fwd);

            if (Mathf.Abs(side) > minSideVelocity && angle < breakAngle && speed > 5f)
            {
                _currentDrift += Mathf.Abs(side) * angle * Time.fixedDeltaTime;
                OnScoreChanged?.Invoke(Current);
            }
            else if (_currentDrift > 0f)
            {
                _bank += Mathf.RoundToInt(_currentDrift);
                _currentDrift = 0f;
                OnCombo?.Invoke(_bank);
                OnScoreChanged?.Invoke(0);
                var ach = Backend.PlayFabAchievements.Instance;
                if (ach) ach.OnDriftBank(_bank);
                if (Core.PlayerStats.Instance) Core.PlayerStats.Instance.ReportDriftScore(_bank);
            }
        }
    }
}
