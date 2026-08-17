using UnityEngine;
using DreamCar.Effects;

namespace DreamCar.RCCPBridge
{
    // RCCP kendi WheelGlow'unu getirir. Bu bridge, RCCP varsa bizim custom WheelGlow.cs'i
    // devre dışı bırakır (çift emissive olmasın).
    public class RCCPWheelGlowBridge : MonoBehaviour
    {
        void Awake()
        {
#if RCCP_INSTALLED
            foreach (var g in GetComponentsInChildren<WheelGlow>())
                g.enabled = false;
#endif
        }
    }
}
