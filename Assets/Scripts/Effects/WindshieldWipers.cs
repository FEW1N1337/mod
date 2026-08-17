using UnityEngine;
using DreamCar.Environment;

namespace DreamCar.Effects
{
    // Iki wiper mesh sin-curve animasyon. Weather.Rain'e girince otomatik başlar.
    // 3 hız seviyesi (off/slow/mid/fast).
    public class WindshieldWipers : MonoBehaviour
    {
        public Transform leftWiper;
        public Transform rightWiper;
        public Vector3 restEuler = new Vector3(0f, 0f, 0f);
        public Vector3 sweepEuler = new Vector3(0f, 0f, -110f);
        public float[] speedLevels = { 0f, 1.2f, 2.4f, 4.0f };
        public int level;
        public bool autoInRain = true;

        Weather _weather;

        void Awake() => _weather = FindFirstObjectByType<Weather>();

        void Update()
        {
            if (autoInRain && _weather)
                level = _weather.type == Weather.Type.Rain ? Mathf.Max(level, 2) : 0;

            if (level <= 0)
            {
                if (leftWiper) leftWiper.localEulerAngles = restEuler;
                if (rightWiper) rightWiper.localEulerAngles = restEuler;
                return;
            }

            float speed = speedLevels[Mathf.Clamp(level, 0, speedLevels.Length - 1)];
            float t = (Mathf.Sin(Time.time * speed * Mathf.PI) + 1f) * 0.5f;
            Vector3 e = Vector3.Lerp(restEuler, sweepEuler, t);
            if (leftWiper) leftWiper.localEulerAngles = e;
            if (rightWiper) rightWiper.localEulerAngles = e;
        }

        public void Cycle() => level = (level + 1) % speedLevels.Length;
    }
}
