using Photon.Pun;
using UnityEngine;

namespace DreamCar.Customization
{
    // CarPaint'in üstüne emissive/HDR ekler: parlak boya, rainbow modu opsiyonel.
    // MaterialPropertyBlock ile _EmissionColor set eder (URP Lit + Bloom ile parlar).
    [RequireComponent(typeof(CarPaint))]
    public class CarPaintHDR : MonoBehaviourPun
    {
        public Renderer[] paintRenderers;
        public string emissivePropertyName = "_EmissionColor";
        public bool emissiveEnabled;
        public Color emissiveColor = Color.white;
        [Range(0f, 4f)] public float intensity = 1.5f;
        public bool rainbow;
        public float rainbowSpeed = 0.25f;

        MaterialPropertyBlock _mpb;
        int _emissiveId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _emissiveId = Shader.PropertyToID(emissivePropertyName);
            if (paintRenderers == null || paintRenderers.Length == 0)
            {
                var paint = GetComponent<CarPaint>();
                if (paint != null) paintRenderers = paint.paintRenderers;
            }
        }

        void Update()
        {
            Color c = emissiveEnabled ? (rainbow
                ? Color.HSVToRGB(Mathf.Repeat(Time.time * rainbowSpeed, 1f), 1f, 1f)
                : emissiveColor) * intensity : Color.black;

            foreach (var r in paintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissiveId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void ApplyAndSync(bool on, Color color, float intens, bool rainbowOn)
        {
            emissiveEnabled = on;
            emissiveColor = color;
            intensity = intens;
            rainbow = rainbowOn;

            if (photonView && photonView.IsMine)
            {
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { "paint.hdr.on", on },
                    { "paint.hdr.color", ColorUtility.ToHtmlStringRGB(color) },
                    { "paint.hdr.intensity", intens },
                    { "paint.hdr.rainbow", rainbowOn }
                };
                photonView.Owner?.SetCustomProperties(props);
            }
        }
    }
}
