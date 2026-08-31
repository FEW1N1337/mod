using UnityEngine;
using DreamCar.Car;

namespace DreamCar.Audio
{
    // Motor sesi: gaz seviyesine + hıza göre pitch. İdle + gaz iki loop'u karıştırır.
    [RequireComponent(typeof(CarController))]
    public class EngineAudio : MonoBehaviour
    {
        public AudioSource idleLoop;
        public AudioSource revLoop;
        public float idlePitch = 0.8f;
        public float maxPitch = 2.4f;
        public AnimationCurve throttleVolume = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
        public AnimationCurve idleVolumeVsSpeed = AnimationCurve.Linear(0f, 1f, 60f, 0.1f);

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();
            if (idleLoop) { idleLoop.loop = true; if (!idleLoop.isPlaying) idleLoop.Play(); }
            if (revLoop) { revLoop.loop = true; if (!revLoop.isPlaying) revLoop.Play(); }
        }

        void Update()
        {
            float speedT = Mathf.Clamp01(_car.SpeedKmh / _car.topSpeedKmh);
            float throttle = Mathf.Clamp01(Mathf.Abs(_car.throttleInput));
            float pitch = Mathf.Lerp(idlePitch, maxPitch, Mathf.Max(speedT, throttle));

            // Sesini her karede kendisi yazıyor — SFX çarpanını burada uygular.
            // (AudioBus'a kaydolsaydı taban seviyesi yanlış anda yakalanırdı.)
            float sfx = AudioBus.SfxScale;

            if (idleLoop)
            {
                idleLoop.pitch = Mathf.Lerp(0.9f, 1.1f, throttle);
                idleLoop.volume = idleVolumeVsSpeed.Evaluate(_car.SpeedKmh) * sfx;
            }
            if (revLoop)
            {
                revLoop.pitch = pitch;
                revLoop.volume = throttleVolume.Evaluate(throttle) * sfx;
            }
        }
    }
}
