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
        // Somut sürücü tipi yerine IDriveInput: RCCP ile sürülen araçta bizim WheelCollider
        // denetleyicimiz yok, RCCPCarAdapter var; kokpit kamerası ikisiyle de çalışmalı.
        // Not: Unity arayüz alanlarını serialize etmez — bu alan Inspector'da görünmez,
        // araç doğduğunda koddan atanmalı (kamera araçtan ayrı bir GameObject'te).
        public IDriveInput car;
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
            // "!car" kısayolu yok: arayüz referansı UnityEngine.Object değil.
            if (!steeringWheel || car == null) return;
            float target = -car.SteerInput * maxWheelAngle;
            _wheelZ = Mathf.Lerp(_wheelZ, target, Time.deltaTime * wheelLerpSpeed);
            steeringWheel.localEulerAngles = new Vector3(0f, 0f, _wheelZ);
        }
    }
}
