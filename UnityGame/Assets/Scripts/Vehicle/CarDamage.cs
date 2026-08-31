using UnityEngine;

namespace DreamCar.Vehicle
{
    // Çarpma ile hasar birikir. Mesh deformasyon opsiyonel (skinnedMesh vertex offset).
    [RequireComponent(typeof(Rigidbody))]
    public class CarDamage : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float health;
        public float damageMultiplier = 0.5f;
        public float destroyThreshold = 5f;
        public ParticleSystem smoke;
        public AudioSource crashSfx;

        public System.Action<float> OnDamaged;

        void Awake() => health = maxHealth;

        void OnCollisionEnter(Collision col)
        {
            float impulse = col.impulse.magnitude;
            if (impulse < 200f) return;

            float dmg = impulse * damageMultiplier * 0.01f;
            health = Mathf.Max(0f, health - dmg);
            OnDamaged?.Invoke(health);

            if (crashSfx) crashSfx.Play();
            Core.Haptics.PlayImpact(impulse);

            if (smoke)
            {
                var emit = smoke.emission;
                emit.rateOverTime = Mathf.Lerp(0f, 50f, 1f - health / maxHealth);
                if (!smoke.isPlaying) smoke.Play();
            }
        }

        public void Repair()
        {
            health = maxHealth;
            OnDamaged?.Invoke(health);
            if (smoke) { var emit = smoke.emission; emit.rateOverTime = 0f; }
        }
    }
}
