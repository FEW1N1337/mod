using UnityEngine;
using DreamCar.Car;

namespace DreamCar.UI
{
    // Analog kilometre saati iğnesi. UI ise RectTransform.eulerAngles.z döner,
    // 3D mesh ise transform.localEulerAngles.
    public class SpeedometerNeedle : MonoBehaviour
    {
        public CarController car;
        public RectTransform needle;
        public float minAngle = 220f;
        public float maxAngle = -40f;
        public float smoothing = 8f;

        float _current;

        void Update()
        {
            if (!car || !needle) return;

            float target = Util.GameMath.SpeedometerAngle(car.SpeedKmh, car.topSpeedKmh, minAngle, maxAngle);
            _current = Mathf.LerpAngle(_current, target, Time.deltaTime * smoothing);
            needle.localEulerAngles = new Vector3(0f, 0f, _current);
        }
    }
}
