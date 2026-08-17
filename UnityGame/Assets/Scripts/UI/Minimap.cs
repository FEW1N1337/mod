using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Sağ üst köşede top-down bir Camera → RenderTexture → RawImage.
    // Sahnedeki oyuncu araçlarına gizmo/icon spawn'lar.
    public class Minimap : MonoBehaviour
    {
        public Transform target;
        public Camera minimapCamera;
        public RawImage minimapImage;
        public int textureSize = 256;
        public float height = 60f;

        RenderTexture _rt;

        void Start()
        {
            if (!minimapCamera) return;
            _rt = new RenderTexture(textureSize, textureSize, 16);
            minimapCamera.targetTexture = _rt;
            minimapCamera.orthographic = true;
            if (minimapImage) minimapImage.texture = _rt;
        }

        void LateUpdate()
        {
            if (!target || !minimapCamera) return;
            var pos = target.position;
            pos.y = height;
            minimapCamera.transform.position = pos;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        }

        void OnDestroy() { if (_rt) _rt.Release(); }
    }
}
