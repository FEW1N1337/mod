using UnityEngine;

namespace DreamCar.Effects
{
    public class HeadlightController : MonoBehaviour
    {
        public Light[] headlights;
        public Light[] tailLights;
        public MeshRenderer headlightGlow;
        public string emissivePropertyName = "_EmissionColor";
        public Color headlightEmissive = new Color(2f, 2f, 1.6f);
        public Color tailEmissive = new Color(1.5f, 0f, 0f);
        public bool autoAtNight = true;
        public bool forceOn;

        MaterialPropertyBlock _mpb;
        int _emissiveId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _emissiveId = Shader.PropertyToID(emissivePropertyName);
        }

        void Update()
        {
            bool on = forceOn;
            if (autoAtNight)
            {
                float sunY = RenderSettings.sun ? RenderSettings.sun.transform.forward.y : -1f;
                if (sunY > -0.05f) on = true;
            }
            SetOn(on);
        }

        public void SetOn(bool on)
        {
            foreach (var l in headlights) if (l) l.enabled = on;
            foreach (var l in tailLights) if (l) l.enabled = on;
            if (headlightGlow)
            {
                headlightGlow.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissiveId, on ? headlightEmissive : Color.black);
                headlightGlow.SetPropertyBlock(_mpb);
            }
        }
    }
}
