#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Spline örneklerinden sürülebilir yol geometrisi üretir:
    // asfalt yüzey, banket, virajda yatırma (banking), bariyer ve şerit çizgileri.
    public static class RoadMeshBuilder
    {
        public class Settings
        {
            public float roadWidth = 14f;
            public float shoulderWidth = 2.5f;
            public float shoulderDrop = 0.12f;      // banket yolun bu kadar altında
            public float maxBankAngle = 8f;         // virajda maksimum yatırma (derece)
            public float bankSensitivity = 22f;     // eğrilik → yatırma katsayısı
            public bool guardrails = true;
            public float guardrailHeight = 0.75f;
            public float guardrailInset = 0.3f;
            public bool centerLine = true;
            public float uvTilesPerMeter = 0.08f;
        }

        public class Result
        {
            public Mesh road;
            public Mesh shoulders;
            public Mesh guardrails;
            public Mesh centerLine;
            public List<RoadSpline.Sample> samples;
        }

        public static Result Build(RoadSpline spline, Settings s, float sampleSpacing = 4f)
        {
            var samples = spline.Sample(sampleSpacing);
            var result = new Result { samples = samples };
            if (samples.Count < 2) return result;

            // Virajda yatırma açısı — eğriliğe bağlı, sınırlı ve yumuşatılmış
            var bank = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++)
                bank[i] = Mathf.Clamp(samples[i].curvature * s.bankSensitivity,
                                      -s.maxBankAngle, s.maxBankAngle);
            Smooth(bank, spline.closed, passes: 3);

            result.road = BuildSurface(samples, bank, s, -s.roadWidth * 0.5f, s.roadWidth * 0.5f,
                                       0f, 0f, spline.closed, s.uvTilesPerMeter, "road_surface");

            result.shoulders = BuildShoulders(samples, bank, s, spline.closed);

            if (s.guardrails)
                result.guardrails = BuildGuardrails(samples, bank, s, spline.closed);

            if (s.centerLine)
                result.centerLine = BuildSurface(samples, bank, s, -0.18f, 0.18f,
                                                 0.02f, 0.02f, spline.closed, 0.5f, "road_centerline");

            return result;
        }

        // Spline boyunca iki kenar arasına şerit gerer. leftOffset/rightOffset
        // merkeze göre yanal konum, leftLift/rightLift dikey kaydırma.
        static Mesh BuildSurface(List<RoadSpline.Sample> samples, float[] bank, Settings s,
                                 float leftOffset, float rightOffset,
                                 float leftLift, float rightLift,
                                 bool closed, float uvScale, string name)
        {
            var mb = new MeshBuilder();
            int count = samples.Count;
            int rings = closed ? count : count - 1;

            for (int i = 0; i < count; i++)
            {
                var sample = samples[i];
                Quaternion roll = Quaternion.AngleAxis(bank[i], sample.forward);
                Vector3 right = roll * sample.right;
                Vector3 up = roll * Vector3.up;

                Vector3 l = sample.position + right * leftOffset + up * leftLift;
                Vector3 r = sample.position + right * rightOffset + up * rightLift;

                float v = sample.distance * uvScale;
                mb.AddVertex(l, up, new Vector2(0f, v));
                mb.AddVertex(r, up, new Vector2(1f, v));
            }

            for (int i = 0; i < rings; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = ((i + 1) % count) * 2 + 1;
                int d = ((i + 1) % count) * 2;
                mb.AddQuad(a, b, c, d);
            }

            return mb.ToMesh(name);
        }

        // Banket: yolun iki yanında, hafif aşağı eğimli şeritler.
        static Mesh BuildShoulders(List<RoadSpline.Sample> samples, float[] bank, Settings s, bool closed)
        {
            var mb = new MeshBuilder();
            int count = samples.Count;
            int rings = closed ? count : count - 1;
            float half = s.roadWidth * 0.5f;
            float outer = half + s.shoulderWidth;

            // Sol banket + sağ banket tek mesh'te (iki ayrı şerit)
            for (int side = 0; side < 2; side++)
            {
                int baseIndex = mb.VertexCount;
                float inner = side == 0 ? -half : half;
                float edge = side == 0 ? -outer : outer;

                for (int i = 0; i < count; i++)
                {
                    var sample = samples[i];
                    Quaternion roll = Quaternion.AngleAxis(bank[i], sample.forward);
                    Vector3 right = roll * sample.right;
                    Vector3 up = roll * Vector3.up;

                    Vector3 innerPoint = sample.position + right * inner;
                    Vector3 edgePoint = sample.position + right * edge - Vector3.up * s.shoulderDrop;

                    float v = sample.distance * s.uvTilesPerMeter;
                    mb.AddVertex(innerPoint, up, new Vector2(0f, v));
                    mb.AddVertex(edgePoint, up, new Vector2(1f, v));
                }

                for (int i = 0; i < rings; i++)
                {
                    int a = baseIndex + i * 2;
                    int b = baseIndex + i * 2 + 1;
                    int c = baseIndex + ((i + 1) % count) * 2 + 1;
                    int d = baseIndex + ((i + 1) % count) * 2;

                    // Sol tarafta sarım yönü ters — normaller yukarı baksın
                    if (side == 0) mb.AddQuad(d, c, b, a);
                    else mb.AddQuad(a, b, c, d);
                }
            }

            return mb.ToMesh("road_shoulders");
        }

        // Bariyer: banketin dış kenarında dikey şerit. Araç uçmasın diye
        // ayrıca MeshCollider'a bağlanacak.
        static Mesh BuildGuardrails(List<RoadSpline.Sample> samples, float[] bank, Settings s, bool closed)
        {
            var mb = new MeshBuilder();
            int count = samples.Count;
            int rings = closed ? count : count - 1;
            float offset = s.roadWidth * 0.5f + s.shoulderWidth - s.guardrailInset;

            for (int side = 0; side < 2; side++)
            {
                int baseIndex = mb.VertexCount;
                float lateral = side == 0 ? -offset : offset;

                for (int i = 0; i < count; i++)
                {
                    var sample = samples[i];
                    Vector3 right = sample.right;
                    Vector3 basePoint = sample.position + right * lateral - Vector3.up * s.shoulderDrop;
                    Vector3 topPoint = basePoint + Vector3.up * s.guardrailHeight;

                    Vector3 normal = side == 0 ? right : -right;
                    float v = sample.distance * 0.15f;

                    mb.AddVertex(basePoint, normal, new Vector2(0f, v));
                    mb.AddVertex(topPoint, normal, new Vector2(1f, v));
                }

                for (int i = 0; i < rings; i++)
                {
                    int a = baseIndex + i * 2;
                    int b = baseIndex + i * 2 + 1;
                    int c = baseIndex + ((i + 1) % count) * 2 + 1;
                    int d = baseIndex + ((i + 1) % count) * 2;

                    if (side == 0) mb.AddQuad(a, b, c, d);
                    else mb.AddQuad(d, c, b, a);
                }
            }

            return mb.ToMesh("road_guardrails");
        }

        // Diziyi yumuşat — banking ani zıplamasın.
        static void Smooth(float[] values, bool wrap, int passes)
        {
            int n = values.Length;
            if (n < 3) return;

            for (int p = 0; p < passes; p++)
            {
                var copy = (float[])values.Clone();
                for (int i = 0; i < n; i++)
                {
                    int prev = wrap ? (i - 1 + n) % n : Mathf.Max(0, i - 1);
                    int next = wrap ? (i + 1) % n : Mathf.Min(n - 1, i + 1);
                    values[i] = (copy[prev] + copy[i] * 2f + copy[next]) * 0.25f;
                }
            }
        }
    }
}
#endif
