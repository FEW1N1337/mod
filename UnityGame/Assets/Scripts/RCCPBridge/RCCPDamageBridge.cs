using System;
using UnityEngine;

namespace DreamCar.RCCPBridge
{
    // RCCP'nin hasar bileşenini bizim CarDamage arayüzümüze yansıtır: health 0..maxHealth
    // ve OnDamaged event'i. Hasar barı ve toast aynı event'e abone kalır.
    //
    // Tipe doğrudan bağlanmıyoruz — bkz. RCCPReflection. Opsiyonel köprü: bulunamazsa
    // uyarı basıp devre dışı kalır, oyun çalışmaya devam eder.
    public class RCCPDamageBridge : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float health;
        public event Action<float> OnDamaged;

        Component _dmg;
        RCCPReflection.Member _damageMember;
        RCCPReflection.Member _maxDamageMember;
        RCCPReflection.Member _repairMember;
        bool _wired;

        void Awake()
        {
            health = maxHealth;
#if RCCP_INSTALLED
            Wire();
#else
            enabled = false;
#endif
        }

        void Wire()
        {
            var type = RCCPReflection.FindType("RCCP_Damage");
            if (type == null) { enabled = false; return; }

            _dmg = GetComponent(type);
            if (_dmg == null) { enabled = false; return; }

            _damageMember    = RCCPReflection.Member.Resolve(type, "damage", "currentDamage", "totalDamage");
            _maxDamageMember = RCCPReflection.Member.Resolve(type, "maximumDamage", "maxDamage");
            _repairMember    = RCCPReflection.Member.Resolve(type, "repairNow", "repair");

            _wired = _damageMember.Found;
            if (!_wired)
            {
                RCCPReflection.LogAvailableMembers(type,
                    "Hasar köprüsü bağlanamadı — RCCP kendi hasarını göstermeye devam eder, " +
                    "yalnızca bizim hasar barımız güncellenmez.");
                enabled = false;
            }
        }

        void Update()
        {
            if (!_wired) return;

            // Maksimum hasar üyesi bulunamadıysa makul bir tavan varsay; oran yine
            // anlamlı kalır, yalnızca ölçek yaklaşık olur.
            float max = _maxDamageMember.Found
                ? Mathf.Max(0.01f, _maxDamageMember.GetFloat(_dmg, 100f))
                : 100f;

            float ratio = Mathf.Clamp01(_damageMember.GetFloat(_dmg) / max);
            float newHealth = maxHealth * (1f - ratio);

            if (Mathf.Abs(newHealth - health) <= 0.5f) return;
            health = newHealth;
            OnDamaged?.Invoke(health);
        }

        public void Repair()
        {
            if (_wired && _repairMember.Found) _repairMember.SetBool(_dmg, true);
            health = maxHealth;
            OnDamaged?.Invoke(health);
        }
    }
}
