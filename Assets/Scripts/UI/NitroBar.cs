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
            if (nitro && fill) fill.fillAmount = Mathf.Clamp01(nitro.nitroAmount / nitro.maxNitroAmount);
        }

        static void Add(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> cb)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(cb);
            trigger.triggers.Add(e);
        }
    }
}
