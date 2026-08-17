using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Vehicle
{
    // 1. şahıs kokpit kamerası — dashboard anchor + direksiyon rotation. CameraModeController
    // "Interior" moduna geçince aktif olur; steer input'una göre direksiyon döner.
    public class InteriorCamera : MonoBehaviour
    {
        public Transform cameraAnchor;
        public Transform steeringWheel;
        public CarController car;
        public float maxWheelAngle = 450f;
        public float wheelLerpSpeed = 12f;
        public GameObject cockpitUI;

        float _wheelZ;

        public void SetActive(bool on)
        {
            if (cockpitUI) cockpitUI.SetActive(on);
        }

        void LateUpdate()
        {
            if (!steeringWheel || !car) return;
            float target = -car.SteerInput * maxWheelAngle;
            _wheelZ = Mathf.Lerp(_wheelZ, target, Time.deltaTime * wheelLerpSpeed);
            steeringWheel.localEulerAngles = new Vector3(0f, 0f, _wheelZ);
        }
    }
}
