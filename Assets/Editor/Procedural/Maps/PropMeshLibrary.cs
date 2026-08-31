#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Kind = DreamCar.EditorTools.Procedural.Maps.MapArchetype.PropKind;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Harita süsleri için basit ama tanınabilir mesh'ler. Hepsi düşük poligon —
    // sahnede binlerce kopya olacak.
    public static class PropMeshLibrary
    {
        static readonly Dictionary<Kind, Mesh> Cache = new();

        public static void ClearCache() => Cache.Clear();

        public static Mesh Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out var cached) && cached != null) return cached;

            Mesh mesh = kind switch
            {
                Kind.Tree      => BuildBroadleafTree(),
                Kind.Pine      => BuildPine(),
                Kind.Rock      => BuildRock(),
                Kind.Cactus    => BuildCactus(),
                Kind.Building  => BuildBuilding(),
                Kind.Container => BuildContainer(),
                Kind.Crane     => BuildCrane(),
                Kind.House     => BuildHouse(),
                Kind.Barn      => BuildBarn(),
                Kind.Barrier   => BuildBarrier(),
                Kind.Lamp      => BuildLamp(),
                _              => BuildRock(),
            };

            Cache[kind] = mesh;
            return mesh;
        }

        // Gövde rengi propun kendi materyaline değil, vertex color'a yazılır —
        // tek materyalle çok çeşit elde edilir.
        static Mesh Finish(MeshBuilder mb, List<Color> colors, string name)
        {
            var mesh = mb.ToMesh(name, recalculateNormals: true);
            if (colors != null && colors.Count == mesh.vertexCount) mesh.SetColors(colors);
            return mesh;
        }

        // --- Bitkiler ---

        static Mesh BuildBroadleafTree()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            var bark = new Color(0.32f, 0.23f, 0.15f);
            var leaf = new Color(0.20f, 0.42f, 0.18f);

            int before = mb.VertexCount;
            mb.AddCylinderX(Vector3.zero, 0.16f, 1.6f, 6);  // X ekseninde üretilir
            RotateLastToUpright(mb, before, 1.6f);
            AddColors(colors, mb.VertexCount - before, bark);

            // Yaprak kütlesi: üst üste binmiş üç küre benzeri çokyüzlü
            foreach (var (offsetY, radius) in new[] { (2.6f, 1.5f), (3.5f, 1.15f), (4.2f, 0.75f) })
            {
                before = mb.VertexCount;
                AddIcosphere(mb, new Vector3(0f, offsetY, 0f), radius);
                AddColors(colors, mb.VertexCount - before, leaf);
            }

            return Finish(mb, colors, "prop_tree");
        }

        static Mesh BuildPine()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            var bark = new Color(0.28f, 0.20f, 0.14f);
            var needle = new Color(0.13f, 0.30f, 0.17f);

            int before = mb.VertexCount;
            mb.AddCylinderX(Vector3.zero, 0.14f, 1.2f, 6);
            RotateLastToUpright(mb, before, 1.2f);
            AddColors(colors, mb.VertexCount - before, bark);

            // Üç kademeli koni
            float y = 1.6f;
            foreach (var (radius, height) in new[] { (1.5f, 2.0f), (1.15f, 1.8f), (0.75f, 1.6f) })
            {
                before = mb.VertexCount;
                AddCone(mb, new Vector3(0f, y, 0f), radius, height, 8);
                AddColors(colors, mb.VertexCount - before, needle);
                y += height * 0.55f;
            }

            return Finish(mb, colors, "prop_pine");
        }

        static Mesh BuildCactus()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            var green = new Color(0.24f, 0.42f, 0.24f);

            int before = mb.VertexCount;
            mb.AddCylinderX(new Vector3(0f, 0f, 0f), 0.32f, 1.6f, 8);
            RotateLastToUpright(mb, before, 1.6f);

            // İki kol
            mb.AddBox(new Vector3(-0.55f, 2.0f, 0f), new Vector3(0.9f, 0.34f, 0.34f));
            mb.AddBox(new Vector3(-0.95f, 2.55f, 0f), new Vector3(0.34f, 1.1f, 0.34f));
            mb.AddBox(new Vector3(0.55f, 1.5f, 0f), new Vector3(0.9f, 0.32f, 0.32f));
            mb.AddBox(new Vector3(0.95f, 2.0f, 0f), new Vector3(0.32f, 1.0f, 0.32f));

            AddColors(colors, mb.VertexCount, green);
            return Finish(mb, colors, "prop_cactus");
        }

        static Mesh BuildRock()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();

            // Düzensiz çokyüzlü — köşeleri rastgele itilmiş ikosahedron
            AddIcosphere(mb, Vector3.zero, 1f, jitter: 0.34f, seed: 991);
            AddColors(colors, mb.VertexCount, new Color(0.45f, 0.44f, 0.42f));
            return Finish(mb, colors, "prop_rock");
        }

        // --- Yapılar ---

        static Mesh BuildBuilding()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();

            mb.AddBox(new Vector3(0f, 9f, 0f), new Vector3(12f, 18f, 10f));
            AddColors(colors, mb.VertexCount, new Color(0.48f, 0.48f, 0.50f));

            int before = mb.VertexCount;
            mb.AddBox(new Vector3(0f, 18.6f, 0f), new Vector3(5f, 1.2f, 4f));
            AddColors(colors, mb.VertexCount - before, new Color(0.38f, 0.38f, 0.40f));

            return Finish(mb, colors, "prop_building");
        }

        static Mesh BuildContainer()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            mb.AddBox(new Vector3(0f, 1.3f, 0f), new Vector3(6f, 2.6f, 2.4f));
            // Renk çeşitliliği yerleştirme sırasında tint ile verilir
            AddColors(colors, mb.VertexCount, Color.white);
            return Finish(mb, colors, "prop_container");
        }

        static Mesh BuildCrane()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            var steel = new Color(0.85f, 0.55f, 0.15f);

            // Ayaklar
            foreach (var (x, z) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
                mb.AddBox(new Vector3(x, 8f, z), new Vector3(0.7f, 16f, 0.7f));

            // Üst platform ve bom
            mb.AddBox(new Vector3(0f, 16.5f, 0f), new Vector3(7.5f, 1.4f, 7.5f));
            mb.AddBox(new Vector3(0f, 18.5f, 9f), new Vector3(1.2f, 1.2f, 22f));
            mb.AddBox(new Vector3(0f, 18.5f, -5f), new Vector3(1.0f, 1.0f, 8f));

            AddColors(colors, mb.VertexCount, steel);
            return Finish(mb, colors, "prop_crane");
        }

        static Mesh BuildHouse()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();

            int before = mb.VertexCount;
            mb.AddBox(new Vector3(0f, 1.7f, 0f), new Vector3(7f, 3.4f, 6f));
            AddColors(colors, mb.VertexCount - before, new Color(0.82f, 0.78f, 0.68f));

            before = mb.VertexCount;
            AddGableRoof(mb, new Vector3(0f, 3.4f, 0f), 7.6f, 6.6f, 2.2f);
            AddColors(colors, mb.VertexCount - before, new Color(0.48f, 0.24f, 0.18f));

            return Finish(mb, colors, "prop_house");
        }

        static Mesh BuildBarn()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();

            int before = mb.VertexCount;
            mb.AddBox(new Vector3(0f, 2.4f, 0f), new Vector3(10f, 4.8f, 8f));
            AddColors(colors, mb.VertexCount - before, new Color(0.58f, 0.20f, 0.16f));

            before = mb.VertexCount;
            AddGableRoof(mb, new Vector3(0f, 4.8f, 0f), 10.6f, 8.6f, 3.0f);
            AddColors(colors, mb.VertexCount - before, new Color(0.32f, 0.30f, 0.28f));

            return Finish(mb, colors, "prop_barn");
        }

        static Mesh BuildBarrier()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            mb.AddBox(new Vector3(0f, 0.55f, 0f), new Vector3(3.2f, 1.1f, 0.35f));
            AddColors(colors, mb.VertexCount, Color.white);
            return Finish(mb, colors, "prop_barrier");
        }

        static Mesh BuildLamp()
        {
            var mb = new MeshBuilder();
            var colors = new List<Color>();
            var metal = new Color(0.42f, 0.44f, 0.46f);

            mb.AddBox(new Vector3(0f, 4f, 0f), new Vector3(0.22f, 8f, 0.22f));
            mb.AddBox(new Vector3(0f, 7.9f, -1.1f), new Vector3(0.18f, 0.18f, 2.4f));
            AddColors(colors, mb.VertexCount, metal);

            int before = mb.VertexCount;
            mb.AddBox(new Vector3(0f, 7.7f, -2.1f), new Vector3(0.7f, 0.25f, 0.5f));
            AddColors(colors, mb.VertexCount - before, new Color(1f, 0.95f, 0.75f));

            return Finish(mb, colors, "prop_lamp");
        }

        // --- Geometri yardımcıları ---

        static void AddColors(List<Color> colors, int count, Color c)
        {
            for (int i = 0; i < count; i++) colors.Add(c);
        }

        // AddCylinderX X ekseninde üretiyor; gövde dik olmalı. Kaydırma miktarı
        // silindirin yarı uzunluğu kadar — taban tam yere otursun, havada kalmasın.
        static void RotateLastToUpright(MeshBuilder mb, int fromVertex, float halfLength)
        {
            mb.TransformVertices(fromVertex, v => new Vector3(v.y, v.x + halfLength, v.z));
        }

        static void AddCone(MeshBuilder mb, Vector3 baseCenter, float radius, float height, int segments)
        {
            var ring = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = 2f * Mathf.PI * i / segments;
                ring[i] = baseCenter + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            }
            Vector3 apex = baseCenter + Vector3.up * height;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % segments];
                Vector3 n = Vector3.Cross(b - a, apex - a).normalized;
                int i0 = mb.AddVertex(a, n, new Vector2(0f, 0f));
                int i1 = mb.AddVertex(b, n, new Vector2(1f, 0f));
                int i2 = mb.AddVertex(apex, n, new Vector2(0.5f, 1f));
                mb.AddTriangle(i0, i1, i2);
            }
            mb.CapRing(ring, Vector3.down, true);
        }

        static void AddGableRoof(MeshBuilder mb, Vector3 center, float width, float depth, float height)
        {
            float hw = width * 0.5f, hd = depth * 0.5f;
            Vector3 ridgeA = center + new Vector3(0f, height, -hd);
            Vector3 ridgeB = center + new Vector3(0f, height, hd);

            Vector3 c00 = center + new Vector3(-hw, 0f, -hd);
            Vector3 c10 = center + new Vector3(hw, 0f, -hd);
            Vector3 c01 = center + new Vector3(-hw, 0f, hd);
            Vector3 c11 = center + new Vector3(hw, 0f, hd);

            mb.AddFlatQuad(c00, c01, ridgeB, ridgeA);   // sol eğim
            mb.AddFlatQuad(c11, c10, ridgeA, ridgeB);   // sağ eğim

            // Alınlıklar
            int i0 = mb.AddVertex(c00, Vector3.back, Vector2.zero);
            int i1 = mb.AddVertex(c10, Vector3.back, Vector2.right);
            int i2 = mb.AddVertex(ridgeA, Vector3.back, new Vector2(0.5f, 1f));
            mb.AddTriangle(i0, i1, i2);

            int j0 = mb.AddVertex(c11, Vector3.forward, Vector2.zero);
            int j1 = mb.AddVertex(c01, Vector3.forward, Vector2.right);
            int j2 = mb.AddVertex(ridgeB, Vector3.forward, new Vector2(0.5f, 1f));
            mb.AddTriangle(j0, j1, j2);
        }

        // Basit ikosahedron türevi — jitter ile kaya, jitter'sız yaprak kütlesi.
        static void AddIcosphere(MeshBuilder mb, Vector3 center, float radius,
                                 float jitter = 0f, int seed = 0)
        {
            const float t = 1.618033988749895f;
            var baseVerts = new[]
            {
                new Vector3(-1,  t, 0), new Vector3( 1,  t, 0), new Vector3(-1, -t, 0), new Vector3( 1, -t, 0),
                new Vector3( 0, -1, t), new Vector3( 0,  1, t), new Vector3( 0, -1,-t), new Vector3( 0,  1,-t),
                new Vector3( t,  0,-1), new Vector3( t,  0, 1), new Vector3(-t,  0,-1), new Vector3(-t,  0, 1),
            };
            var faces = new[]
            {
                (0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),
                (1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),
                (3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),
                (4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1),
            };

            var rng = new System.Random(seed);
            var pts = new Vector3[baseVerts.Length];
            for (int i = 0; i < baseVerts.Length; i++)
            {
                float scale = 1f + (jitter > 0f ? ((float)rng.NextDouble() * 2f - 1f) * jitter : 0f);
                pts[i] = center + baseVerts[i].normalized * radius * scale;
            }

            foreach (var (a, b, c) in faces)
            {
                Vector3 pa = pts[a], pb = pts[b], pc = pts[c];
                Vector3 n = Vector3.Cross(pb - pa, pc - pa).normalized;
                int i0 = mb.AddVertex(pa, n, new Vector2(0f, 0f));
                int i1 = mb.AddVertex(pb, n, new Vector2(1f, 0f));
                int i2 = mb.AddVertex(pc, n, new Vector2(0.5f, 1f));
                mb.AddTriangle(i0, i1, i2);
            }
        }
    }
}
#endif
