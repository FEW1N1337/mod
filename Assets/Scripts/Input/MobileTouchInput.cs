using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DreamCar.Car;

namespace DreamCar.InputSystemMobile
{
    public class MobileTouchInput : MonoBehaviour
    {
        public CarController car;

        [Header("UI buttons (assign in scene)")]
        public Button throttleButton;
        public Button brakeButton;
        public Button handbrakeButton;

        [Header("Steering (touch drag on left half)")]
        public RectTransform steeringPad;
        public float steeringSensitivity = 0.005f;
        public bool useKeyboardFallback = true;

        bool _throttleHeld, _brakeHeld, _handbrakeHeld;
        float _steer;
        int _steerFingerId = -1;
        Vector2 _steerStart;

        void Start()
        {
            HookHold(throttleButton, v => _throttleHeld = v);
            HookHold(brakeButton, v => _brakeHeld = v);
            HookHold(handbrakeButton, v => _handbrakeHeld = v);
        }

        void Update()
        {
            if (!car) return;

            HandleSteerTouch();

            float throttle = _throttleHeld ? 1f : (_brakeHeld ? -1f : 0f);
            float brake = _brakeHeld && car.SpeedKmh > 0.5f ? 1f : 0f;
            float steer = _steer;
            bool hand = _handbrakeHeld;

            if (useKeyboardFallback)
            {
                float kx = UnityEngine.Input.GetAxisRaw("Horizontal");
                float ky = UnityEngine.Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(kx) > 0.01f) steer = kx;
                if (Mathf.Abs(ky) > 0.01f)
                {
                    throttle = ky;
                    brake = ky < 0 && car.SpeedKmh > 0.5f ? 1f : 0f;
                }
                if (UnityEngine.Input.GetKey(KeyCode.Space)) hand = true;
            }

            car.Move(throttle, brake, steer, hand);
        }

        void HandleSteerTouch()
        {
            if (!steeringPad) { _steer = Mathf.MoveTowards(_steer, 0f, Time.deltaTime * 3f); return; }

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var t = UnityEngine.Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && _steerFingerId == -1 &&
                    RectTransformUtility.RectangleContainsScreenPoint(steeringPad, t.position))
                {
                    _steerFingerId = t.fingerId;
                    _steerStart = t.position;
                }
                else if (t.fingerId == _steerFingerId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        _steerFingerId = -1;
                        _steer = 0f;
                    }
                    else
                    {
                        _steer = Mathf.Clamp((t.position.x - _steerStart.x) * steeringSensitivity, -1f, 1f);
                    }
                }
            }

            if (UnityEngine.Input.touchCount == 0 && _steerFingerId != -1)
            {
                _steerFingerId = -1;
                _steer = 0f;
            }
        }

        static void HookHold(Button btn, System.Action<bool> setter)
        {
            if (!btn) return;
            var trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => setter(true));
            trigger.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => setter(false));
            trigger.triggers.Add(up);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => setter(false));
            trigger.triggers.Add(exit);
        }
    }
}
