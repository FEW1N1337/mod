using UnityEngine;

namespace DreamCar.Environment
{
    // Basit gündüz/gece: yönlü ışığı döndürür, RenderSettings.ambient/skybox rengini
    // gün eğrisine göre günlerdirir. URP ile de çalışır.
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        public Light sun;
        public float dayLengthSeconds = 600f;
        [Range(0f, 1f)] public float startTimeOfDay = 0.35f;

        public Gradient sunColorOverDay;
        public Gradient ambientColorOverDay;
        public AnimationCurve sunIntensityOverDay = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        float _t;

        void OnEnable() { _t = startTimeOfDay; if (sun) RenderSettings.sun = sun; EnsureGradients(); }

        void EnsureGradients()
        {
            if (sunColorOverDay == null || sunColorOverDay.colorKeys.Length == 0)
            {
                sunColorOverDay = new Gradient();
                sunColorOverDay.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 0f),
                        new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.25f),
                        new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.5f),
                        new GradientColorKey(new Color(1f, 0.4f, 0.15f), 0.75f),
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 1f),
                    },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            }
            if (ambientColorOverDay == null || ambientColorOverDay.colorKeys.Length == 0)
            {
                ambientColorOverDay = new Gradient();
                ambientColorOverDay.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.06f), 0f),
                        new GradientColorKey(new Color(0.4f, 0.3f, 0.2f), 0.25f),
                        new GradientColorKey(new Color(0.5f, 0.55f, 0.6f), 0.5f),
                        new GradientColorKey(new Color(0.35f, 0.2f, 0.1f), 0.75f),
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.06f), 1f),
                    },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            }
        }

        void Update()
        {
            if (Application.isPlaying && dayLengthSeconds > 1f)
                _t = (_t + Time.deltaTime / dayLengthSeconds) % 1f;

            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler((_t * 360f) - 90f, 170f, 0f);
                sun.color = sunColorOverDay.Evaluate(_t);
                sun.intensity = sunIntensityOverDay.Evaluate(_t);
            }
            RenderSettings.ambientLight = ambientColorOverDay.Evaluate(_t);
        }
    }
}
