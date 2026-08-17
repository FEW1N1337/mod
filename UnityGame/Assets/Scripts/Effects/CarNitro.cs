using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Effects
{
    // Dream Road'daki CarNitro'ya paralel: nitroAmount 0..100 arasında,
    // basılınca ekstra ileri kuvvet + görsel efekt tetikler.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CarController))]
    public class CarNitro : MonoBehaviour
    {
        public float nitroAmount = 100f;
        public float maxNitroAmount = 100f;
        public float drainPerSecond = 25f;
        public float regenPerSecond = 8f;
        public float boostForce = 15000f;
        public float boostTopSpeedBonusKmh = 60f;

        public ParticleSystem[] exhaustFlames;
        public AudioSource nitroLoop;

        Rigidbody _rb;
        CarController _car;
        bool _active;
        float _originalTopSpeed;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _car = GetComponent<CarController>();
            _originalTopSpeed = _car.topSpeedKmh;
        }

        public void SetInput(bool held)
        {
            if (held && nitroAmount > 0.5f)
            {
                if (!_active) StartBoost();
                _active = true;
            }
            else if (_active)
            {
                StopBoost();
                _active = false;
            }
        }

        void StartBoost()
        {
            _car.topSpeedKmh = _originalTopSpeed + boostTopSpeedBonusKmh;
            foreach (var p in exhaustFlames) if (p) p.Play();
            if (nitroLoop && !nitroLoop.isPlaying) nitroLoop.Play();
        }

        void StopBoost()
        {
            _car.topSpeedKmh = _originalTopSpeed;
            foreach (var p in exhaustFlames) if (p) p.Stop();
            if (nitroLoop && nitroLoop.isPlaying) nitroLoop.Stop();
        }

        void FixedUpdate()
        {
            if (_active)
            {
                nitroAmount = Mathf.Max(0f, nitroAmount - drainPerSecond * Time.fixedDeltaTime);
                if (nitroAmount <= 0f) { StopBoost(); _active = false; }
                else _rb.AddForce(transform.forward * boostForce, ForceMode.Force);
            }
            else
            {
                nitroAmount = Mathf.Min(maxNitroAmount, nitroAmount + regenPerSecond * Time.fixedDeltaTime);
            }
        }
    }
}
