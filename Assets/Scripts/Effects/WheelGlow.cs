using UnityEngine;

namespace DreamCar.Effects
{
    // Fren + drift ısınmasıyla balatanın kızarması. Dream Road'daki RCCP_WheelGlow ile
    // aynı mantık: sıcaklık yükseldikçe emissive rengi kırmızıya doğru gradient.
    [RequireComponent(typeof(Renderer))]
    public class WheelGlow : MonoBehaviour
    {
        public WheelCollider wheel;
        public Gradient glowColor;
        public float heatGainPerBrake = 220f;
        public float heatGainPerSlip = 180f;
        public float coolPerSecond = 60f;
        public float maxTemperature = 900f;
        public float minVisibleTemperature = 120f;
        public string emissivePropertyName = "_EmissionColor";

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        int _emissiveId;
        float _temperature;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _emissiveId = Shader.PropertyToID(emissivePropertyName);

            if (glowColor == null || glowColor.colorKeys.Length == 0)
            {
                glowColor = new Gradient();
                glowColor.SetKeys(
                    new[] {
                        new GradientColorKey(Color.black, 0f),
                        new GradientColorKey(new Color(0.6f, 0.05f, 0f), 0.5f),
                        new GradientColorKey(new Color(1.5f, 0.3f, 0f), 0.85f),
                        new GradientColorKey(new Color(2.5f, 1.2f, 0.2f), 1f),
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
        }

        void Update()
        {
            if (wheel)
            {
                bool braking = wheel.brakeTorque > 100f;
                float slip = 0f;
                if (wheel.GetGroundHit(out WheelHit hit))
                    slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

                if (braking) _temperature += heatGainPerBrake * Time.deltaTime;
                _temperature += slip * heatGainPerSlip * Time.deltaTime;
            }
            _temperature = Mathf.Max(0f, _temperature - coolPerSecond * Time.deltaTime);
            _temperature = Mathf.Min(_temperature, maxTemperature);

            float t = Mathf.InverseLerp(minVisibleTemperature, maxTemperature, _temperature);
            Color c = glowColor.Evaluate(t) * Mathf.LinearToGammaSpace(t);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissiveId, c);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
