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
        //
        // Unity arayüz alanlarını serialize etmiyor, yani bu alanı Editor'den atamak
        // mümkün değil — atansa bile prefab'a kaydedilmez. Bileşen aracın ÜZERİNDE
        // olduğu için Awake'te kendisi buluyor; dışarıdan atanması gerekmiyor.
        public IDriveInput car;
        public float maxWheelAngle = 450f;
        public float wheelLerpSpeed = 12f;
        public GameObject cockpitUI;

        float _wheelZ;

        void Awake()
        {
            // Kendi kendine bağlan: aksi halde direksiyon hiç dönmezdi. Editör
            // üreticisi bu alanı atıyordu ama arayüz serileştirilmediği için o
            // atama çalışma anına hiç ulaşmıyordu.
            if (car == null) car = GetComponent<IDriveInput>();
        }

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
