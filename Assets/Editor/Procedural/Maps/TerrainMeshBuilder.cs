#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Yola uyan arazi üretir. Kritik nokta: yol çevresinde arazi yolun yüksekliğine
    // düzleşir — yoksa yol havada asılı kalır veya tepenin içinde kaybolur.
    public static class TerrainMeshBuilder
    {
        public class Settings
        {
            public float extent = 800f;          // merkezden kenara mesafe
            public int resolution = 140;         // kenar başına hücre (140 → ~20k üçgen)
            public float heightAmplitude = 45f;
            public float noiseScale = 0.0022f;
            public int octaves = 4;
            public float ridgeSharpness = 0f;    // 0 = yumuşak tepe, 1 = sivri sırt
            public float flatRadius = 26f;       // yol merkezinden bu mesafeye kadar tam düz
            public float blendRadius = 90f;      // buraya kadar kademeli geçiş
            public int seed = 12345;

            // Yükseklik bandına göre renklendirme (vertex color)
            public Color lowColor = new(0.24f, 0.42f, 0.18f);
            public Color midColor = new(0.36f, 0.44f, 0.24f);
            public Color highColor = new(0.55f, 0.53f, 0.48f);
            public float colorBandScale = 1f;
        }

        public static Mesh Build(Settings s, List<RoadSpline.Sample> roadSamples, out float[,] heights)
        {
            int res = Mathf.Max(8, s.resolution);
            int verts = res + 1;
            float step = s.extent * 2f / res;
            heights = new float[verts, verts];

            // Yol örneklerini kaba bir ızgaraya yerleştir — her arazi noktası için
            // tüm örnekleri taramak yerine yalnızca komşu hücrelere bakılır.
            var lookup = BuildRoadLookup(roadSamples, s.extent, s.blendRadius);
            float cell = Mathf.Max(1f, s.blendRadius);

            var mb = new MeshBuilder();
            var colors = new Color[verts * verts];

            for (int z = 0; z < verts; z++)
            for (int x = 0; x < verts; x++)
            {
                float wx = -s.extent + x * step;
                float wz = -s.extent + z * step;

                float natural = FractalNoise(wx, wz, s);
                float height = natural * s.heightAmplitude;

                // Yola yakınlık
                if (roadSamples != null && roadSamples.Count > 0)
                {
                    FindNearestRoad(lookup, cell, s.extent, wx, wz,
                                    out float distance, out float roadHeight);

                    if (distance < s.blendRadius)
                    {
                        // flatRadius içinde tamamen yol yüksekliği, sonra yumuşak geçiş
                        float t = Mathf.InverseLerp(s.flatRadius, s.blendRadius, distance);
                        t = Mathf.SmoothStep(0f, 1f, t);
                        height = Mathf.Lerp(roadHeight, height, t);
                    }
                }

                heights[x, z] = height;

                // Renk: yüksekliğe göre bant + eğime göre kaya
                float band = Mathf.InverseLerp(0f, s.heightAmplitude * s.colorBandScale, height);
                Color c = band < 0.5f
                    ? Color.Lerp(s.lowColor, s.midColor, band * 2f)
                    : Color.Lerp(s.midColor, s.highColor, (band - 0.5f) * 2f);
                colors[z * verts + x] = c;

                mb.AddVertex(new Vector3(wx, height, wz), Vector3.up,
                             new Vector2((float)x / res * 24f, (float)z / res * 24f));
            }

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i0 = z * verts + x;
                int i1 = i0 + 1;
                int i2 = i0 + verts;
                int i3 = i2 + 1;
                mb.AddTriangle(i0, i2, i1);
                mb.AddTriangle(i1, i2, i3);
            }

            var mesh = mb.ToMesh("terrain", recalculateNormals: true);
            mesh.SetColors(colors);
            return mesh;
        }

        // Çok oktavlı Perlin. ridgeSharpness > 0 ise sırt gürültüsüne kayar.
        static float FractalNoise(float x, float z, Settings s)
        {
            float sum = 0f, amplitude = 1f, frequency = s.noiseScale, norm = 0f;

            for (int o = 0; o < s.octaves; o++)
            {
                float n = Mathf.PerlinNoise(x * frequency + s.seed * 0.37f,
                                            z * frequency + s.seed * 0.71f);
                if (s.ridgeSharpness > 0f)
                {
                    float ridge = 1f - Mathf.Abs(n * 2f - 1f);
                    n = Mathf.Lerp(n, ridge * ridge, s.ridgeSharpness);
                }
                sum += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        // --- Yol yakınlık araması ---

        static Dictionary<long, List<RoadSpline.Sample>> BuildRoadLookup(
            List<RoadSpline.Sample> samples, float extent, float cellSize)
        {
            var map = new Dictionary<long, List<RoadSpline.Sample>>();
            if (samples == null) return map;

            float cell = Mathf.Max(1f, cellSize);
            foreach (var sample in samples)
            {
                long key = CellKey(sample.position.x, sample.position.z, extent, cell);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<RoadSpline.Sample>();
                    map[key] = list;
                }
                list.Add(sample);
            }
            return map;
        }

        static long CellKey(float x, float z, float extent, float cell)
        {
            int cx = Mathf.FloorToInt((x + extent) / cell);
            int cz = Mathf.FloorToInt((z + extent) / cell);
            return ((long)cx << 32) ^ (uint)cz;
        }

        static void FindNearestRoad(Dictionary<long, List<RoadSpline.Sample>> lookup,
                                    float cell, float extent, float x, float z,
                                    out float distance, out float height)
        {
            distance = float.MaxValue;
            height = 0f;

            int cx = Mathf.FloorToInt((x + extent) / cell);
            int cz = Mathf.FloorToInt((z + extent) / cell);

            // 3x3 komşuluk yeterli: arama yarıçapı hücre boyutuna eşit
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                long key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
                if (!lookup.TryGetValue(key, out var list)) continue;

                foreach (var sample in list)
                {
                    float ddx = sample.position.x - x;
                    float ddz = sample.position.z - z;
                    float d2 = ddx * ddx + ddz * ddz;
                    if (d2 >= distance * distance) continue;

                    distance = Mathf.Sqrt(d2);
                    height = sample.position.y;
                }
            }
        }

        // Verilen noktadaki arazi yüksekliği — prop yerleştirirken kullanılır.
        public static float SampleHeight(float[,] heights, Settings s, float worldX, float worldZ)
        {
            int res = s.resolution;
            float step = s.extent * 2f / res;

            float fx = (worldX + s.extent) / step;
            float fz = (worldZ + s.extent) / step;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, res);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, res);
            int x1 = Mathf.Min(x0 + 1, res);
            int z1 = Mathf.Min(z0 + 1, res);

            float tx = Mathf.Clamp01(fx - x0);
            float tz = Mathf.Clamp01(fz - z0);

            float h00 = heights[x0, z0], h10 = heights[x1, z0];
            float h01 = heights[x0, z1], h11 = heights[x1, z1];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }
    }
}
#endif
