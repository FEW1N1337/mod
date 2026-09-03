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
        VehicleStatSheet _sheet;
        bool _active;

        // Değiştirici kaynağı. Aynı ad hem Set hem Clear'da kullanılıyor;
        // sabit olmasının sebebi yazım hatasının sessizce kalıcı bir nitro
        // bonusu bırakması.
        const string StatSource = "nitro";

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _car = GetComponent<CarController>();
            _sheet = GetComponent<VehicleStatSheet>();
            if (!_car) enabled = false;   // RCCP'li araç: nitro köprü üzerinden yürüyor

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

        // Üst hız bonusu artık CarController.topSpeedKmh alanına YAZILMIYOR.
        // Eski hâlinde bonus, Awake'te okunan değere geri dönerek bırakılıyordu;
        // araca ikinci bir üst hız değiştiricisi (turbo yükseltmesi, lastik seti)
        // geldiği gün nitroyu bırakan oyuncu o yükseltmeyi de kaybederdi.
        // VehicleStatSheet değiştiricileri kaynak adına göre tuttuğu için
        // nitronun kalkması yalnızca nitronun katkısını kaldırıyor.
        void StartBoost()
        {
            if (_sheet) _sheet.Set(StatSource, VehicleStat.TopSpeed, boostTopSpeedBonusKmh);
            if (exhaustFlames != null) foreach (var p in exhaustFlames) if (p) p.Play();
            if (nitroLoop && !nitroLoop.isPlaying) nitroLoop.Play();
        }

        void StopBoost()
        {
            if (_sheet) _sheet.Clear(StatSource);
            if (exhaustFlames != null) foreach (var p in exhaustFlames) if (p) p.Stop();
            if (nitroLoop && nitroLoop.isPlaying) nitroLoop.Stop();
        }

        // Araç yok edilirken veya bileşen kapanırken nitro açık kalmışsa
        // değiştirici tabloda asılı kalmasın.
        void OnDisable()
        {
            if (!_active) return;
            _active = false;
            StopBoost();
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
