using UnityEngine;

namespace DreamCar.RCCPBridge
{
    // Belirli hasar eşiği aşılınca RCCP'nin kopabilir parçalarını düşürür —
    // tampon, kapı, spoiler.
    //
    // Tipe doğrudan bağlanmıyoruz (bkz. RCCPReflection): parça düşürme metodunun
    // adı sürümler arasında değişebilir ve yanlış tahmin derleme hatası olurdu.
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

            var type = RCCPReflection.FindType("RCCP_DetachablePart");
            if (type == null) return;

            // Metot adı için birkaç makul aday; RCCP tarafında parametresiz olmalı.
            System.Reflection.MethodInfo detach = null;
            foreach (var name in new[] { "DetachPart", "Detach", "Break" })
            {
                detach = type.GetMethod(name, System.Type.EmptyTypes);
                if (detach != null) break;
            }

            if (detach == null)
            {
                RCCPReflection.LogAvailableMembers(type,
                    "Parça düşürme metodu bulunamadı — hasar çalışmaya devam eder, " +
                    "yalnızca parçalar kopmaz.");
                return;
            }

            foreach (var part in GetComponentsInChildren(type))
                if (part) detach.Invoke(part, null);
        }
    }
}
