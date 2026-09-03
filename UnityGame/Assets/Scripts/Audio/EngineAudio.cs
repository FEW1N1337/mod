using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Audio
{
    // Motor sesi: gaz seviyesine + hıza göre pitch. İdle + gaz iki loop'u karıştırır.
    // RequireComponent zorunluluğu kaldırıldı: sürücü artık IDriveInput uygulayan
    // herhangi bir bileşen olabilir (RCCP adapteri dahil) ve Unity bir arayüz tipini
    // RequireComponent ile zorunlu tutamaz.
    public class EngineAudio : MonoBehaviour
    {
        public AudioSource idleLoop;
        public AudioSource revLoop;
        public float idlePitch = 0.8f;
        public float maxPitch = 2.4f;
        public AnimationCurve throttleVolume = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
        public AnimationCurve idleVolumeVsSpeed = AnimationCurve.Linear(0f, 1f, 60f, 0.1f);

        IDriveInput _car;

        // Uzak araçta sürücü bileşeni kapalı ve Rigidbody kinematik, yani
        // _car.SpeedKmh her karede 0 dönüyor: motor sesi rölantiye kilitleniyor
        // ve diğer oyuncuların araçları hızlanıp yavaşlarken hiç ses
        // değiştirmiyordu. Ağdan gelen değerleri CarNetworkSync tutuyor.
        CarNetworkSync _net;

        void Awake()
        {
            _car = GetComponent<IDriveInput>();
            _net = GetComponent<CarNetworkSync>();
            if (idleLoop) { idleLoop.loop = true; if (!idleLoop.isPlaying) idleLoop.Play(); }
            if (revLoop) { revLoop.loop = true; if (!revLoop.isPlaying) revLoop.Play(); }
        }

        void Update()
        {
            // RequireComponent(CarController) kaldırıldı — o attribute sürücünün
            // varlığını garanti ediyordu. Artık garanti yok: RCCP'li bir araçta
            // arayüzü kimse sağlamıyorsa burası her karede NullReferenceException
            // fırlatırdı.
            if (_car == null) return;

            bool remote = _net && _net.IsRemote;
            float speedKmh = remote ? _net.RemoteSpeedKmh : _car.SpeedKmh;
            float throttleRaw = remote ? _net.RemoteThrottle : _car.ThrottleInput;

            float speedT = Mathf.Clamp01(speedKmh / Mathf.Max(1f, _car.TopSpeedKmh));
            float throttle = Mathf.Clamp01(Mathf.Abs(throttleRaw));
            float pitch = Mathf.Lerp(idlePitch, maxPitch, Mathf.Max(speedT, throttle));

            // Sesini her karede kendisi yazıyor — SFX çarpanını burada uygular.
            // (AudioBus'a kaydolsaydı taban seviyesi yanlış anda yakalanırdı.)
            float sfx = AudioBus.SfxScale;

            if (idleLoop)
            {
                idleLoop.pitch = Mathf.Lerp(0.9f, 1.1f, throttle);
                idleLoop.volume = idleVolumeVsSpeed.Evaluate(speedKmh) * sfx;
            }
            if (revLoop)
            {
                revLoop.pitch = pitch;
                revLoop.volume = throttleVolume.Evaluate(throttle) * sfx;
            }
        }
    }
}
