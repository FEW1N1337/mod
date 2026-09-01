using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Effects
{
    // Dream Road'daki CarNitro'ya paralel: nitroAmount 0..100 arasında,
    // basılınca ekstra ileri kuvvet + görsel efekt tetikler.
    // CarController'a RequireComponent KOYMUYORUZ. Bu bileşen bizim WheelCollider
    // denetleyicimize özel (üst hızı geçici olarak yükselterek boost veriyor, arayüzde
    // yazılabilir üst hız yok). Ama zorunlu tutulursa RCCP'li bir araca eklendiğinde
    // Unity CarController'ı da ekler ve iki sürücü aynı Rigidbody'yi sürer.
    // RCCP modunda nitro RCCPNitroBridge üzerinden RCCP'nin kendi NOS'una gider;
    // bu bileşen orada sessizce devre dışı kalır.
    [RequireComponent(typeof(Rigidbody))]
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
            if (_car) _originalTopSpeed = _car.topSpeedKmh;
            else enabled = false;   // RCCP'li araç: nitro köprü üzerinden yürüyor

            // Seviyesi bir kez ayarlanıp Play() ediliyor — SFX sürgüsüne AudioBus bağlar.
            DreamCar.Audio.AudioBus.RegisterSfx(nitroLoop);
        }

        void OnDestroy() => DreamCar.Audio.AudioBus.Unregister(nitroLoop);

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
