using UnityEngine;
using UnityEngine.UI;
using DreamCar.Effects;

namespace DreamCar.UI
{
    public class NitroBar : MonoBehaviour
    {
        public CarNitro nitro;
        public Image fill;
        public Button nitroButton;

        float _nextScan;

        void Start()
        {
            if (nitroButton)
            {
                var trigger = nitroButton.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                              ?? nitroButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                Add(trigger, UnityEngine.EventSystems.EventTriggerType.PointerDown, _ => { if (nitro) nitro.SetInput(true); });
                Add(trigger, UnityEngine.EventSystems.EventTriggerType.PointerUp, _ => { if (nitro) nitro.SetInput(false); });
                Add(trigger, UnityEngine.EventSystems.EventTriggerType.PointerExit, _ => { if (nitro) nitro.SetInput(false); });
            }
        }

        void Update()
        {
            // nitro alanı Editor'de bağlanamıyor: CarNitro yerel araçta ve araç odaya
            // girilince doğuyor. Bağlanmadığı için hem bar hep boş kalıyor hem de NOS
            // butonunun PointerDown callback'i (nitro null olduğundan) hiçbir şey yapmıyordu.
            if (!nitro && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f; // her karede tarama mobilde pahalı
                foreach (var n in FindObjectsByType<CarNitro>(FindObjectsSortMode.None))
                {
                    var pv = n.GetComponent<Photon.Pun.PhotonView>();
                    if (pv && pv.IsMine) { nitro = n; break; }
                }
            }

            if (nitro && fill && nitro.maxNitroAmount > 0f)
                fill.fillAmount = Mathf.Clamp01(nitro.nitroAmount / nitro.maxNitroAmount);
        }

        static void Add(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> cb)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(cb);
            trigger.triggers.Add(e);
        }
    }
}
