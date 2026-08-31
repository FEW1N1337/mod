#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // Prosedürel mesh üretimi için yardımcı. Üçgen ekleme, loft (kesit birleştirme),
    // silindir, disk gibi temel işlemleri toplar.
    public class MeshBuilder
    {
        readonly List<Vector3> _verts = new();
        readonly List<Vector3> _normals = new();
        readonly List<Vector2> _uvs = new();
        readonly List<int> _tris = new();

        public int VertexCount => _verts.Count;

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _verts.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
            return _verts.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            _tris.Add(a); _tris.Add(b); _tris.Add(c);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        // Yüzey normali hesaplanmış bağımsız quad (flat shading).
        public void AddFlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = AddVertex(a, n, new Vector2(0f, 0f));
            int i1 = AddVertex(b, n, new Vector2(1f, 0f));
            int i2 = AddVertex(c, n, new Vector2(1f, 1f));
            int i3 = AddVertex(d, n, new Vector2(0f, 1f));
            AddQuad(i0, i1, i2, i3);
        }

        // İki kapalı halkayı (aynı nokta sayısında) yan yüzeyle birleştirir.
        public void LoftRings(IReadOnlyList<Vector3> ringA, IReadOnlyList<Vector3> ringB, float vA, float vB)
        {
            int n = Mathf.Min(ringA.Count, ringB.Count);
            int baseIndex = VertexCount;

            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                Vector3 next = ringA[(i + 1) % n];
                Vector3 outward = (ringA[i] - Centroid(ringA)).normalized;
                Vector3 along = (ringB[i] - ringA[i]).normalized;
                Vector3 tangent = (next - ringA[i]).normalized;
                Vector3 normal = Vector3.Cross(along, tangent).normalized;
                if (Vector3.Dot(normal, outward) < 0f) normal = -normal;

                AddVertex(ringA[i], normal, new Vector2(u, vA));
                AddVertex(ringB[i], normal, new Vector2(u, vB));
            }

            for (int i = 0; i < n; i++)
            {
                int a = baseIndex + i * 2;
                int b = baseIndex + i * 2 + 1;
                int c = baseIndex + ((i + 1) % n) * 2 + 1;
                int d = baseIndex + ((i + 1) % n) * 2;
                AddQuad(a, b, c, d);
            }
        }

        // Halkayı merkeze bağlayarak kapatır (uç tıkacı).
        public void CapRing(IReadOnlyList<Vector3> ring, Vector3 normal, bool reverse)
        {
            Vector3 center = Centroid(ring);
            int centerIndex = AddVertex(center, normal, new Vector2(0.5f, 0.5f));

            int n = ring.Count;
            int baseIndex = VertexCount;
            for (int i = 0; i < n; i++)
            {
                float ang = 2f * Mathf.PI * i / n;
                AddVertex(ring[i], normal, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }

            for (int i = 0; i < n; i++)
            {
                int a = baseIndex + i;
                int b = baseIndex + (i + 1) % n;
                if (reverse) AddTriangle(centerIndex, b, a);
                else AddTriangle(centerIndex, a, b);
            }
        }

        public static Vector3 Centroid(IReadOnlyList<Vector3> points)
        {
            if (points.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }

        // Superellipse kesit — köşeleri yuvarlatılmış dikdörtgen. exponent büyüdükçe
        // kutuya, küçüldükçe elipse yaklaşır. Araba gövdesi için 3-5 arası iyi.
        public static Vector3[] SuperellipseRing(float z, float halfWidth, float halfHeight,
                                                 float centerY, int segments, float exponent)
        {
            var ring = new Vector3[segments];
            float e = 2f / Mathf.Max(0.5f, exponent);
            for (int i = 0; i < segments; i++)
            {
                float t = 2f * Mathf.PI * i / segments;
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * halfWidth;
                float y = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * halfHeight;
                ring[i] = new Vector3(x, centerY + y, z);
            }
            return ring;
        }

        // Silindir (tekerlek lastiği vb.). Eksen X yönünde.
        public void AddCylinderX(Vector3 center, float radius, float halfLength, int segments, bool caps = true)
        {
            var left = new Vector3[segments];
            var right = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = 2f * Mathf.PI * i / segments;
                float y = Mathf.Cos(t) * radius;
                float z = Mathf.Sin(t) * radius;
                left[i] = center + new Vector3(-halfLength, y, z);
                right[i] = center + new Vector3(halfLength, y, z);
            }
            LoftRings(left, right, 0f, 1f);
            if (caps)
            {
                CapRing(left, Vector3.left, false);
                CapRing(right, Vector3.right, true);
            }
        }

        // Düz disk (jant yüzeyi).
        public void AddDiscX(Vector3 center, float radius, int segments, bool faceLeft)
        {
            var ring = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = 2f * Mathf.PI * i / segments;
                ring[i] = center + new Vector3(0f, Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
            }
            CapRing(ring, faceLeft ? Vector3.left : Vector3.right, !faceLeft);
        }

        // Eksene hizalı kutu.
        public void AddBox(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);

            AddFlatQuad(p001, p101, p111, p011); // +Z
            AddFlatQuad(p100, p000, p010, p110); // -Z
            AddFlatQuad(p101, p100, p110, p111); // +X
            AddFlatQuad(p000, p001, p011, p010); // -X
            AddFlatQuad(p010, p011, p111, p110); // +Y
            AddFlatQuad(p000, p100, p101, p001); // -Y
        }

        public Mesh ToMesh(string name, bool recalculateNormals = false)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0);

            if (recalculateNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
#endif
