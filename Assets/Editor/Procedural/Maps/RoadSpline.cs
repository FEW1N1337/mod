#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Yol merkez çizgisi. Catmull-Rom spline — kontrol noktalarından geçer,
    // bu yüzden yol tam istediğin yerden geçer (Bezier'de öyle değil).
    // Örnekleme sonucu: konum + teğet + eğim (banking) bilgisi.
    public class RoadSpline
    {
        public struct Sample
        {
            public Vector3 position;
            public Vector3 forward;   // ilerleme yönü (normalize)
            public Vector3 right;     // sağ yön (normalize, yatay)
            public float distance;    // başlangıçtan itibaren yol boyu mesafe
            public float curvature;   // 1/yarıçap — viraj sertliği (banking için)
        }

        readonly List<Vector3> _points = new();
        public bool closed;

        public IReadOnlyList<Vector3> ControlPoints => _points;
        public int Count => _points.Count;

        public RoadSpline(bool closed = false) => this.closed = closed;

        public RoadSpline Add(Vector3 p) { _points.Add(p); return this; }
        public RoadSpline Add(float x, float z, float y = 0f) { _points.Add(new Vector3(x, y, z)); return this; }

        public RoadSpline AddRange(IEnumerable<Vector3> points)
        {
            _points.AddRange(points);
            return this;
        }

        // Kapalı halka: merkez etrafında, yarıçapı gürültüyle bozulmuş poligon.
        // Aynı seed her zaman aynı pisti üretir.
        public static RoadSpline Circuit(Vector3 center, float radius, int corners,
                                         float irregularity, int seed, float heightAmplitude = 0f)
        {
            var rng = new System.Random(seed);
            var spline = new RoadSpline(closed: true);

            for (int i = 0; i < corners; i++)
            {
                float angle = 2f * Mathf.PI * i / corners;
                // Yarıçapı ±irregularity kadar oynat — daire yerine organik pist
                float r = radius * (1f + ((float)rng.NextDouble() * 2f - 1f) * irregularity);
                float y = heightAmplitude > 0f
                    ? Mathf.PerlinNoise(i * 0.35f, seed * 0.017f) * heightAmplitude
                    : 0f;

                spline.Add(center + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
            }
            return spline;
        }

        // Uzun otoyol: hafif viraj ve rampa, ileri doğru uzanır.
        public static RoadSpline Highway(Vector3 start, float length, int segments,
                                         float curviness, int seed, float heightAmplitude = 0f)
        {
            var spline = new RoadSpline(closed: false);
            float step = length / segments;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // Yanal kayma: iki farklı frekansta gürültü → tekdüze olmayan viraj
                float lateral = (Mathf.PerlinNoise(t * 2.2f, seed * 0.013f) - 0.5f) * curviness
                              + (Mathf.PerlinNoise(t * 6.5f, seed * 0.029f) - 0.5f) * curviness * 0.35f;
                float y = heightAmplitude > 0f
                    ? (Mathf.PerlinNoise(t * 3.1f, seed * 0.041f) - 0.5f) * heightAmplitude
                    : 0f;

                spline.Add(start + new Vector3(lateral, y, i * step));
            }
            return spline;
        }

        // Dolambaçlı dağ/orman yolu: sert virajlar, belirgin yükselti.
        public static RoadSpline Winding(Vector3 center, float extent, int segments,
                                         int seed, float heightAmplitude)
        {
            var spline = new RoadSpline(closed: true);

            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                // İki harmonik → sekiz/böbrek şeklinde organik hat
                float r = extent * (0.62f
                    + Mathf.PerlinNoise(Mathf.Cos(angle) * 1.5f + seed * 0.01f,
                                        Mathf.Sin(angle) * 1.5f) * 0.55f);
                float y = Mathf.PerlinNoise(Mathf.Cos(angle) + seed * 0.03f,
                                            Mathf.Sin(angle) + seed * 0.03f) * heightAmplitude;

                spline.Add(center + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
            }
            return spline;
        }

        // --- Örnekleme ---

        // Spline'ı yaklaşık `spacing` metre aralıkla örnekler.
        public List<Sample> Sample(float spacing)
        {
            var samples = new List<Sample>();
            if (_points.Count < 2) return samples;

            // Önce yoğun örnekle, sonra eşit mesafeye yeniden dağıt — aksi halde
            // viraj içinde noktalar sıkışır, düzlükte seyrelir.
            var dense = new List<Vector3>();
            int segments = closed ? _points.Count : _points.Count - 1;
            const int subdivisions = 24;

            for (int i = 0; i < segments; i++)
                for (int s = 0; s < subdivisions; s++)
                    dense.Add(CatmullRom(i, (float)s / subdivisions));

            if (!closed) dense.Add(_points[^1]);

            // Eşit aralıklı yeniden örnekleme
            float accumulated = 0f;
            float total = 0f;
            var resampled = new List<Vector3> { dense[0] };

            for (int i = 1; i < dense.Count; i++)
            {
                float segLength = Vector3.Distance(dense[i - 1], dense[i]);
                total += segLength;
                accumulated += segLength;

                while (accumulated >= spacing)
                {
                    accumulated -= spacing;
                    float f = 1f - accumulated / Mathf.Max(0.0001f, segLength);
                    resampled.Add(Vector3.Lerp(dense[i - 1], dense[i], Mathf.Clamp01(f)));
                }
            }

            // Teğet, sağ vektör ve eğrilik hesapla
            float travelled = 0f;
            for (int i = 0; i < resampled.Count; i++)
            {
                Vector3 prev = resampled[Wrap(i - 1, resampled.Count)];
                Vector3 curr = resampled[i];
                Vector3 next = resampled[Wrap(i + 1, resampled.Count)];

                Vector3 forward = (next - prev);
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                // Eğrilik: ardışık teğetler arası açı / mesafe
                Vector3 t1 = (curr - prev); t1.y = 0f;
                Vector3 t2 = (next - curr); t2.y = 0f;
                float angle = Vector3.SignedAngle(t1, t2, Vector3.up) * Mathf.Deg2Rad;
                float arc = Mathf.Max(0.001f, t2.magnitude);
                float curvature = angle / arc;

                if (i > 0) travelled += Vector3.Distance(resampled[i - 1], curr);

                samples.Add(new Sample
                {
                    position = curr,
                    forward = forward,
                    right = right,
                    distance = travelled,
                    curvature = curvature,
                });
            }

            return samples;
        }

        Vector3 CatmullRom(int segmentIndex, float t)
        {
            int n = _points.Count;
            Vector3 p0 = _points[Wrap(segmentIndex - 1, n)];
            Vector3 p1 = _points[Wrap(segmentIndex, n)];
            Vector3 p2 = _points[Wrap(segmentIndex + 1, n)];
            Vector3 p3 = _points[Wrap(segmentIndex + 2, n)];

            if (!closed)
            {
                // Uçlarda komşu yoksa kendini tekrarla — spline uçlarda savrulmasın
                if (segmentIndex - 1 < 0) p0 = p1;
                if (segmentIndex + 1 >= n) p2 = p1;
                if (segmentIndex + 2 >= n) p3 = p2;
            }

            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        int Wrap(int i, int n)
        {
            if (closed) return ((i % n) + n) % n;
            return Mathf.Clamp(i, 0, n - 1);
        }
    }
}
#endif
