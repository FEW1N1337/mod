using DreamCar.Vehicle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // HUD yakıt barı. FuelSystem.Percent'i takip eder. Kritik altında kırmızıya döner
    // ve tek seferlik "Yakıt az" toast tetikler.
    public class FuelMeter : MonoBehaviour
    {
        public FuelSystem fuel;
        public Image fill;
        public TMP_Text percentLabel;
        public Color normalColor = new Color(0.4f, 0.9f, 0.4f);
        public Color warningColor = new Color(0.95f, 0.8f, 0.2f);
        public Color criticalColor = new Color(0.95f, 0.25f, 0.2f);
        [Range(0f, 1f)] public float warningThreshold = 0.3f;
        [Range(0f, 1f)] public float criticalThreshold = 0.15f;

        bool _warnedCritical;

        void Update()
        {
            if (!fuel) return;
            float p = fuel.Percent;
            if (fill) fill.fillAmount = p;
            if (percentLabel) percentLabel.text = Mathf.RoundToInt(p * 100f) + "%";

            Color c = p <= criticalThreshold ? criticalColor
                    : p <= warningThreshold ? warningColor
                    : normalColor;
            if (fill) fill.color = c;

            if (p <= criticalThreshold && !_warnedCritical)
            {
                _warnedCritical = true;
                ToastNotification.Show("Yakıt az — istasyona uğra");
            }
            else if (p > criticalThreshold + 0.05f) _warnedCritical = false;
        }
    }
}
