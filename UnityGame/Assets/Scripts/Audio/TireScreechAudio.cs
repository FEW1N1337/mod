using UnityEngine;

namespace DreamCar.Audio
{
    public class TireScreechAudio : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public AudioSource loop;
        public float slipThreshold = 0.4f;
        public float maxVolume = 0.8f;

        void Awake()
        {
            if (loop) loop.loop = true;
        }

        void Update()
        {
            float maxSlip = 0f;
            foreach (var w in wheels)
            {
                if (w && w.GetGroundHit(out WheelHit hit))
                {
                    float s = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                    if (s > maxSlip) maxSlip = s;
                }
            }
            if (!loop) return;

            if (maxSlip > slipThreshold)
            {
                if (!loop.isPlaying) loop.Play();
                // Seviyeyi her karede kendisi yazıyor — SFX çarpanı burada uygulanır.
                loop.volume = Mathf.Lerp(0f, maxVolume, Mathf.InverseLerp(slipThreshold, 1.5f, maxSlip))
                              * AudioBus.SfxScale;
                loop.pitch = 1f + (maxSlip - slipThreshold) * 0.3f;
            }
            else if (loop.isPlaying)
            {
                loop.volume = Mathf.MoveTowards(loop.volume, 0f, Time.deltaTime * 3f);
                if (loop.volume <= 0.01f) loop.Stop();
            }
        }
    }
}
