using Photon.Pun;
using TMPro;
using UnityEngine;
using DreamCar.Vehicle;

namespace DreamCar.UI
{
    // Sürüş yardımcısı göstergesi (telltale). Bir yardımcı aktif müdahale
    // ederken kısa süre yanıp söner: "ABS", "TC", "ESP".
    //
    // NEDEN VAR: yardımcı sessizce çalışırsa oyuncu var olduğunu bilmez ve
    // "araba neden savrulmuyor / neden patinaj yapmıyor" sorusunun yanıtı
    // görünmez kalır. Gerçek araçların gösterge panelindeki uyarı ışıkları da
    // tam bu iş için var.
    //
    // Yerel aracı NitroBar'la aynı desende kendisi buluyor: DrivingAssists
    // yerel araçta ve araç odaya girince doğuyor, Editor'de bağlanamaz.
    public class AssistTelltale : MonoBehaviour
    {
        public TMP_Text label;

        // Müdahale bitince gösterge bu kadar süre daha yanık kalıyor: tek fizik
        // adımlık müdahaleler göz kırpması gibi geçmesin, okunabilir olsun.
        public float holdSeconds = 0.4f;

        DrivingAssists _assists;
        float _nextScan;
        float _visibleUntil;

        void Start()
        {
            if (label) label.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_assists && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                foreach (var a in FindObjectsByType<DrivingAssists>(FindObjectsSortMode.None))
                {
                    var pv = a.GetComponent<PhotonView>();
                    if (!pv || pv.IsMine) { _assists = a; break; }
                }
            }

            if (!label) return;

            if (_assists && _assists.enabled)
            {
                var active = _assists.ActiveIntervention;
                if (active != DrivingAssists.Assist.None)
                {
                    label.text = Describe(active);
                    _visibleUntil = Time.unscaledTime + holdSeconds;
                }
            }

            bool show = Time.unscaledTime < _visibleUntil;
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
        }

        static string Describe(DrivingAssists.Assist a)
        {
            // Aynı anda birden fazla müdahale olabilir; en kritik olanı öne al.
            if ((a & DrivingAssists.Assist.Esp) != 0) return "ESP";
            if ((a & DrivingAssists.Assist.Tc) != 0) return "TC";
            if ((a & DrivingAssists.Assist.Abs) != 0) return "ABS";
            return "";
        }
    }
}
