using UnityEngine;

namespace DreamCar.Effects
{
    // Tekerlek kayma miktarına göre lastik dumanı + iz.
    public class DriftSmoke : MonoBehaviour
    {
        public WheelCollider wheel;
        public ParticleSystem smoke;
        public TrailRenderer skidTrail;
        public float slipThreshold = 0.4f;

        ParticleSystem.EmissionModule _emit;

        void Awake()
        {
            if (smoke) _emit = smoke.emission;
        }

        void Update()
        {
            if (!wheel) return;

            float slip = 0f;
            bool grounded = wheel.GetGroundHit(out WheelHit hit);
            if (grounded) slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));

            bool active = grounded && slip > slipThreshold;

            if (smoke)
            {
                _emit.rateOverTime = active ? Mathf.Lerp(20f, 120f, slip) : 0f;
                var t = smoke.transform;
                if (active) t.position = hit.point + Vector3.up * 0.05f;
            }

            if (skidTrail) skidTrail.emitting = active;
        }
    }
}
