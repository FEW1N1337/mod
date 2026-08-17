using UnityEngine;

#if RCCP_INSTALLED
using RCCP;
#endif

namespace DreamCar.RCCPBridge
{
    // Belirli hasar eşiği aşılınca RCCP_DetachablePart bileşenlerini "detach" eder.
    // Bumper, kapı, spoiler düşme davranışı.
    public class RCCPDetachableBridge : MonoBehaviour
    {
        [Range(0f, 100f)] public float detachAtHealth = 30f;
        public RCCPDamageBridge damageBridge;

        bool _detached;

        void Awake()
        {
            if (!damageBridge) damageBridge = GetComponent<RCCPDamageBridge>();
            if (damageBridge != null) damageBridge.OnDamaged += OnHealthChanged;
        }

        void OnDestroy()
        {
            if (damageBridge != null) damageBridge.OnDamaged -= OnHealthChanged;
        }

        void OnHealthChanged(float health)
        {
            if (_detached || health > detachAtHealth) return;
            _detached = true;
#if RCCP_INSTALLED
            foreach (var p in GetComponentsInChildren<RCCP_DetachablePart>())
                if (p) p.DetachPart();
#endif
        }
    }
}
