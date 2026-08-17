using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Vehicle
{
    // Aracın üstünden arkasına bakan küçük kamera → RenderTexture → sağ üst RawImage.
    // Mesh ayna için: RawImage yerine Renderer.material.SetTexture(_BaseMap, rt).
    public class RearViewMirror : MonoBehaviour
    {
        public Transform car;
        public Camera mirrorCamera;
        public RawImage mirrorImage;
        public Renderer mirrorMesh;
        public string meshTexturePropertyName = "_BaseMap";
        public int textureSize = 256;
        public Vector3 localOffset = new Vector3(0f, 1.4f, -0.2f);

        RenderTexture _rt;

        void Start()
        {
            if (!mirrorCamera) return;
            _rt = new RenderTexture(textureSize, textureSize, 16);
            mirrorCamera.targetTexture = _rt;
            if (mirrorImage) mirrorImage.texture = _rt;
            if (mirrorMesh) mirrorMesh.material.SetTexture(meshTexturePropertyName, _rt);
        }

        void LateUpdate()
        {
            if (!car || !mirrorCamera) return;
            mirrorCamera.transform.position = car.TransformPoint(localOffset);
            mirrorCamera.transform.rotation = Quaternion.LookRotation(-car.forward, car.up);
        }

        void OnDestroy() { if (_rt) _rt.Release(); }
    }
}
