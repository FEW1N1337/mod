using UnityEngine;

namespace DreamCar.RCCPBridge
{
    // RCCP'nin NOS bileşenini bizim nitro arayüzümüze yansıtır: nitroAmount 0..100 ve
    // SetInput(held). NitroBar UI hiçbir şeyden habersiz çalışmaya devam eder.
    //
    // Tipe doğrudan bağlanmıyoruz — bkz. RCCPReflection'daki açıklama. Bu köprü
    // opsiyonel: bulunamazsa uyarı basıp devre dışı kalır, oyunu durdurmaz.
    public class RCCPNitroBridge : MonoBehaviour
    {
        public float nitroAmount = 100f;
        public float maxNitroAmount = 100f;

        Component _nos;
        RCCPReflection.Member _amountMember;
        RCCPReflection.Member _inUseMember;
        bool _wired;

        void Awake()
        {
#if RCCP_INSTALLED
            Wire();
#else
            enabled = false;
#endif
        }

        void Wire()
        {
            var type = RCCPReflection.FindType("RCCP_Nos");
            if (type == null) { enabled = false; return; }

            _nos = GetComponent(type);
            if (_nos == null) { enabled = false; return; }

            _amountMember = RCCPReflection.Member.Resolve(type, "NoS", "nos", "amount", "nosAmount");
            _inUseMember  = RCCPReflection.Member.Resolve(type, "nosInUse", "inUse", "used");

            _wired = _amountMember.Found && _inUseMember.Found;
            if (!_wired)
            {
                RCCPReflection.LogAvailableMembers(type,
                    "Nitro köprüsü bağlanamadı — nitro RCCP tarafında çalışmaya devam eder, " +
                    "yalnızca bizim göstergemiz güncellenmez.");
                enabled = false;
            }
        }

        void Update()
        {
            if (!_wired) return;
            // RCCP 0..1 ölçeğinde tutuyor olabilir; bizim gösterge 0..100.
            nitroAmount = Mathf.Clamp(_amountMember.GetFloat(_nos) * 100f, 0f, maxNitroAmount);
        }

        public void SetInput(bool held)
        {
            if (!_wired) return;
            _inUseMember.SetBool(_nos, held && nitroAmount > 0.5f);
        }
    }
}
