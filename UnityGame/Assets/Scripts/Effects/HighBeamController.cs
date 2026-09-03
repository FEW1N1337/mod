using UnityEngine;

namespace DreamCar.Effects
{
    // HeadlightController'a paralel: uzun huzme (high beam) toggle.
    // Işık range/intensity 2x + parlak emissive beam mesh.
    public class HighBeamController : MonoBehaviour
    {
        public Light[] headlights;
        public Renderer beamMesh;
        public string emissivePropertyName = "_EmissionColor";
        public Color beamEmissive = new Color(4f, 4f, 3f);
        public float rangeMultiplier = 2f;
        public float intensityMultiplier = 1.8f;
        public KeyCode toggleKey = KeyCode.H;

        MaterialPropertyBlock _mpb;
        int _emissiveId;
        bool _on;
        float[] _baseRange, _baseIntensity;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _emissiveId = Shader.PropertyToID(emissivePropertyName);
            _baseRange = new float[headlights.Length];
            _baseIntensity = new float[headlights.Length];
            for (int i = 0; i < headlights.Length; i++)
            {
                if (!headlights[i]) continue;
                _baseRange[i] = headlights[i].range;
                _baseIntensity[i] = headlights[i].intensity;
            }
        }

        void Update() { if (Input.GetKeyDown(toggleKey)) Toggle(); }

        public void Toggle() => SetOn(!_on);

        public void SetOn(bool on)
        {
            _on = on;
            for (int i = 0; i < headlights.Length; i++)
            {
                if (!headlights[i]) continue;
                headlights[i].range = _baseRange[i] * (on ? rangeMultiplier : 1f);
                headlights[i].intensity = _baseIntensity[i] * (on ? intensityMultiplier : 1f);
            }
            if (beamMesh)
            {
                beamMesh.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissiveId, on ? beamEmissive : Color.black);
                beamMesh.SetPropertyBlock(_mpb);
                beamMesh.enabled = on;
            }
        }
    }
}
