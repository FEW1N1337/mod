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
        float _nextScan;

        void Update()
        {
            // fuel alanı Editor'de bağlanamıyor: FuelSystem yerel araçta duruyor ve araç
            // ancak odaya girilince PhotonNetwork.Instantiate ile doğuyor. RoomManager
            // Minimap/MobileTouchInput'u bağlıyor ama FuelMeter'ı unutmuş — bu yüzden
            // yakıt barı oyun boyunca boş kalıyordu. InGameHUD'daki kalıpla kendimiz buluruz.
            if (!fuel && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f; // her karede FindObjectsByType mobilde pahalı
                foreach (var f in FindObjectsByType<FuelSystem>(FindObjectsSortMode.None))
                {
                    var pv = f.GetComponent<Photon.Pun.PhotonView>();
                    if (pv && pv.IsMine) { fuel = f; break; }
                }
            }

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
