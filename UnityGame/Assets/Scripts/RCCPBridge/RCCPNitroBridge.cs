using UnityEngine;

#if RCCP_INSTALLED
using RCCP;
#endif

namespace DreamCar.RCCPBridge
{
    // Mevcut DreamCar.Effects.CarNitro yerine RCCP_Nos'u kullan. NitroBar UI aynı kalır —
    // reflection yerine ortak arayüz sunar: nitroAmount 0..100, SetInput(held).
    public class RCCPNitroBridge : MonoBehaviour
    {
        public float nitroAmount = 100f;
        public float maxNitroAmount = 100f;

#if RCCP_INSTALLED
        RCCP_Nos _nos;

        void Awake() => _nos = GetComponent<RCCP_Nos>();

        void Update()
        {
            if (!_nos) return;
            nitroAmount = Mathf.Clamp(_nos.NoS * 100f, 0f, maxNitroAmount);
        }

        public void SetInput(bool held)
        {
            if (!_nos) return;
            _nos.nosInUse = held && nitroAmount > 0.5f;
        }
#else
        public void SetInput(bool held) { }
#endif
    }
}
