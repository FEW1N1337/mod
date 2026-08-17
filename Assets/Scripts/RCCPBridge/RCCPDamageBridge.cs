using System;
using UnityEngine;

#if RCCP_INSTALLED
using RCCP;
#endif

namespace DreamCar.RCCPBridge
{
    // RCCP_Damage'i CarDamage API'sine yansıtır. UI (health bar, toast) aynı event'e abone olur.
    public class RCCPDamageBridge : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float health;
        public event Action<float> OnDamaged;

#if RCCP_INSTALLED
        RCCP_Damage _dmg;

        void Awake()
        {
            _dmg = GetComponent<RCCP_Damage>();
            health = maxHealth;
        }

        void Update()
        {
            if (!_dmg) return;
            float dmgRatio = Mathf.Clamp01(_dmg.damage / Mathf.Max(0.01f, _dmg.maximumDamage));
            float newHealth = maxHealth * (1f - dmgRatio);
            if (Mathf.Abs(newHealth - health) > 0.5f)
            {
                health = newHealth;
                OnDamaged?.Invoke(health);
            }
        }

        public void Repair()
        {
            if (_dmg) _dmg.repairNow = true;
            health = maxHealth;
            OnDamaged?.Invoke(health);
        }
#else
        public void Repair() => health = maxHealth;
#endif
    }
}
