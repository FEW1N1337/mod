using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamCar.Voice
{
    // VoiceChatController sadece API'ydi; ekranda karşılığı yoktu.
    // Push-to-talk butonu (basılı tut = konuş) + mute toggle + konuşma göstergesi.
    public class VoiceHUD : MonoBehaviour
    {
        public VoiceChatController controller;
        public Button pushToTalkButton;
        public Button muteToggleButton;
        public Image talkingIndicator;
        public Color idleColor = new Color(1f, 1f, 1f, 0.35f);
        public Color talkingColor = new Color(0.3f, 1f, 0.4f, 1f);
        public Color mutedColor = new Color(1f, 0.35f, 0.3f, 1f);

        bool _held;
        bool _muted;

        void Start()
        {
            if (!controller) controller = FindAnyObjectByType<VoiceChatController>();

            if (pushToTalkButton)
            {
                var trigger = pushToTalkButton.gameObject.GetComponent<EventTrigger>()
                              ?? pushToTalkButton.gameObject.AddComponent<EventTrigger>();
                Add(trigger, EventTriggerType.PointerDown, _ => SetHeld(true));
                Add(trigger, EventTriggerType.PointerUp, _ => SetHeld(false));
                Add(trigger, EventTriggerType.PointerExit, _ => SetHeld(false));
            }

            if (muteToggleButton) muteToggleButton.onClick.AddListener(ToggleMute);
            UpdateIndicator();
        }

        void Update()
        {
            // Klavye fallback (editor testi): V basılı tut.
            if (Input.GetKeyDown(KeyCode.T)) SetHeld(true);
            if (Input.GetKeyUp(KeyCode.T)) SetHeld(false);
        }

        void SetHeld(bool held)
        {
            if (_held == held) return;
            _held = held;
            if (controller) controller.SetPushToTalk(held && !_muted);
            UpdateIndicator();
        }

        void ToggleMute()
        {
            _muted = !_muted;
            if (controller) controller.ToggleMute();
            if (_muted) SetHeld(false);
            UpdateIndicator();
        }

        void UpdateIndicator()
        {
            if (!talkingIndicator) return;
            talkingIndicator.color = _muted ? mutedColor : (_held ? talkingColor : idleColor);
        }

        static void Add(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(cb);
            trigger.triggers.Add(e);
        }
    }
}
