using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Core
{
    // Binlerce ağaç/kaya için GameObject başına bir draw call kabul edilemez.
    // Bu bileşen aynı mesh'in tüm kopyalarını GPU instancing ile çizer:
    // 1200 nesne → ~2 draw call. Ayrıca mesafeye göre eler ve LOD uygular.
    //
    // Kullanım: harita üreticisi prop başına GameObject yaratmak yerine
    // matrisleri buraya doldurur.
    [ExecuteAlways]
    public class InstancedPropRenderer : MonoBehaviour
    {
        [System.Serializable]
        public class Batch
        {
            public string label = "props";
            public Mesh mesh;
            public Material material;

            [Tooltip("Bu mesafeden uzaktakiler hiç çizilmez.")]
            public float cullDistance = 320f;
            [Tooltip("Bu mesafeden sonra basitleştirilmiş mesh kullanılır (varsa).")]
            public float lodDistance = 140f;
            public Mesh lodMesh;

            public bool castShadows = false;

            // Editor'de serileştirilen ham veri
            [HideInInspector] public List<Vector3> positions = new();
            [HideInInspector] public List<Quaternion> rotations = new();
            [HideInInspector] public List<Vector3> scales = new();
            [HideInInspector] public List<Color> tints = new();

            // Runtime'da hazırlanan matrisler
            [System.NonSerialized] public Matrix4x4[] matrices;
            [System.NonSerialized] public Vector4[] colors;
            [System.NonSerialized] public float[] sqrRadius;
        }

        public List<Batch> batches = new();

        [Header("Kalite")]
        [Tooltip("Çizim mesafesi çarpanı — düşük cihazlarda 0.6, yüksekte 1.2.")]
        public float distanceScale = 1f;
        [Tooltip("Kaç instance'a kadar tek çağrıda çizilsin (Unity sınırı 1023).")]
        public int batchSize = 511;

        public Transform viewer;

        // Çizim tamponları — her frame yeniden ayrılmaması için sınıf seviyesinde
        Matrix4x4[] _drawMatrices;
        Vector4[] _drawColors;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            _drawMatrices = new Matrix4x4[Mathf.Clamp(batchSize, 1, 1023)];
            _drawColors = new Vector4[_drawMatrices.Length];
            Prepare();
        }

        // Serileştirilmiş listelerden matris dizilerini kurar.
        public void Prepare()
        {
            foreach (var b in batches)
            {
                if (b == null || b.positions == null) continue;

                int n = b.positions.Count;
                b.matrices = new Matrix4x4[n];
                b.colors = new Vector4[n];
                b.sqrRadius = new float[n];

                for (int i = 0; i < n; i++)
                {
                    var pos = b.positions[i];
                    var rot = i < b.rotations.Count ? b.rotations[i] : Quaternion.identity;
                    var scl = i < b.scales.Count ? b.scales[i] : Vector3.one;

                    b.matrices[i] = Matrix4x4.TRS(pos, rot, scl);
                    b.colors[i] = i < b.tints.Count ? (Vector4)b.tints[i] : (Vector4)Color.white;
                }
            }
        }

        void Update()
        {
            var cam = ResolveViewer();
            if (cam == null) return;

            Vector3 eye = cam.position;

            foreach (var b in batches)
            {
                if (b?.mesh == null || b.material == null || b.matrices == null) continue;

                float cull = b.cullDistance * distanceScale;
                float lod = b.lodDistance * distanceScale;
                float cullSqr = cull * cull;
                float lodSqr = lod * lod;

                var shadows = b.castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;

                // Yakın (tam mesh) ve uzak (LOD mesh) ayrı kuyruklara toplanır.
                DrawFiltered(b, eye, 0f, lodSqr, b.mesh, shadows);
                if (lod < cull)
                {
                    var far = b.lodMesh != null ? b.lodMesh : b.mesh;
                    DrawFiltered(b, eye, lodSqr, cullSqr, far, UnityEngine.Rendering.ShadowCastingMode.Off);
                }
            }
        }

        void DrawFiltered(Batch b, Vector3 eye, float minSqr, float maxSqr,
                          Mesh mesh, UnityEngine.Rendering.ShadowCastingMode shadows)
        {
            int count = 0;

            for (int i = 0; i < b.matrices.Length; i++)
            {
                // Matrisin konum sütunu — ayrıca pozisyon listesi taşımaya gerek yok
                Vector3 p = b.matrices[i].GetColumn(3);
                float d2 = (p - eye).sqrMagnitude;
                if (d2 < minSqr || d2 >= maxSqr) continue;

                _drawMatrices[count] = b.matrices[i];
                _drawColors[count] = b.colors[i];
                count++;

                if (count == _drawMatrices.Length)
                {
                    Flush(mesh, b.material, count, shadows);
                    count = 0;
                }
            }

            if (count > 0) Flush(mesh, b.material, count, shadows);
        }

        void Flush(Mesh mesh, Material material, int count,
                   UnityEngine.Rendering.ShadowCastingMode shadows)
        {
            _mpb.Clear();
            _mpb.SetVectorArray(ColorId, _drawColors);

            Graphics.DrawMeshInstanced(
                mesh, 0, material, _drawMatrices, count, _mpb,
                shadows, receiveShadows: false);
        }

        Transform ResolveViewer()
        {
            if (viewer != null) return viewer;
            var cam = Camera.main;
            if (cam != null) { viewer = cam.transform; return viewer; }
#if UNITY_EDITOR
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null) return sceneView.camera.transform;
#endif
            return null;
        }

        // --- Doldurma API'si (Editor üreticisi kullanır) ---
        public Batch GetOrCreateBatch(string label, Mesh mesh, Material material)
        {
            var existing = batches.Find(b => b.label == label);
            if (existing != null) return existing;

            var batch = new Batch { label = label, mesh = mesh, material = material };
            batches.Add(batch);
            return batch;
        }

        public static void AddInstance(Batch b, Vector3 position, Quaternion rotation,
                                       Vector3 scale, Color tint)
        {
            b.positions.Add(position);
            b.rotations.Add(rotation);
            b.scales.Add(scale);
            b.tints.Add(tint);
        }

        public int TotalInstances
        {
            get
            {
                int n = 0;
                foreach (var b in batches) n += b?.positions?.Count ?? 0;
                return n;
            }
        }
    }
}
