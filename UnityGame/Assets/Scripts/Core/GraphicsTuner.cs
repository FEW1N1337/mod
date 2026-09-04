using UnityEngine;
using UnityEngine.Rendering;
using DreamCar.Settings;

namespace DreamCar.Core
{
    // "En iyi grafik" ile "kasmasın" arasındaki dengeyi cihaza göre kurar.
    // QualityAutoDetect donanımı puanlar; bu bileşen o puana göre çizim mesafesi,
    // gölge kalitesi ve sis yoğunluğunu ayarlar.
    //
    // Sahnede tek kopya yeterli — harita üreticisi Bootstrap'e ekler.
    [DefaultExecutionOrder(-50)]
    public class GraphicsTuner : MonoBehaviour
    {
        [System.Serializable]
        public class Tier
        {
            [Tooltip("Prop çizim mesafesi çarpanı.")]
            public float propDistanceScale = 1f;
            [Tooltip("Gölgelerin görüneceği maksimum mesafe (metre). 0 = gölge kapalı.")]
            public float shadowDistance = 90f;
            public LightShadows sunShadows = LightShadows.Soft;
            public ShadowQuality shadowQuality = ShadowQuality.All;
            [Tooltip("Kameranın uzak kırpma düzlemi.")]
            public float farClip = 1200f;
            [Tooltip("Sis yoğunluğu çarpanı — düşük cihazda sis artar, uzak eleme gizlenir.")]
            public float fogDensityScale = 1f;
            public int pixelLightCount = 4;
            [Tooltip("Bu kademede kullanılacak post-processing profili.")]
            public VolumeProfile postProfile;
            [Tooltip("Post-processing tamamen kapatılsın mı (en düşük cihazlar).")]
            public bool disablePostProcessing;
            [Tooltip("Gerçek zamanlı yansıma probu çalışsın mı. Araç boyası metalik " +
                     "olduğu için rengini büyük ölçüde yansımadan alır — kapatılırsa " +
                     "araba matlaşır, ama düşük cihazlarda prob pahalıdır.")]
            public bool realtimeReflections = true;
        }

        public Tier low = new()
        {
            propDistanceScale = 0.55f,
            shadowDistance = 0f,
            sunShadows = LightShadows.None,
            shadowQuality = ShadowQuality.Disable,
            farClip = 550f,
            fogDensityScale = 1.9f,
            pixelLightCount = 1,
            disablePostProcessing = true,   // en zayıf cihazlarda tek geçiş bile pahalı
            realtimeReflections = false,
        };

        public Tier mid = new()
        {
            propDistanceScale = 0.85f,
            shadowDistance = 70f,
            sunShadows = LightShadows.Hard,
            shadowQuality = ShadowQuality.HardOnly,
            farClip = 850f,
            fogDensityScale = 1.3f,
            pixelLightCount = 2,
        };

        public Tier high = new()
        {
            propDistanceScale = 1.25f,
            shadowDistance = 130f,
            sunShadows = LightShadows.Soft,
            shadowQuality = ShadowQuality.All,
            farClip = 1400f,
            fogDensityScale = 1f,
            pixelLightCount = 4,
        };

        [Tooltip("Elle kademe seç (test için). Kapalıysa cihazdan tespit edilir.")]
        public bool overrideTier;
        public QualityAutoDetect.Tier forcedTier = QualityAutoDetect.Tier.High;

        public QualityAutoDetect.Tier ActiveTier { get; private set; }

        float _baseFogDensity;
        bool _capturedFog;

        void Start() => Apply();

        public void Apply()
        {
            ActiveTier = ResolveTier();
            var t = ActiveTier switch
            {
                QualityAutoDetect.Tier.Low => low,
                QualityAutoDetect.Tier.Mid => mid,
                _ => high,
            };

            // Prop çizim mesafesi
            foreach (var renderer in FindObjectsByType<InstancedPropRenderer>(FindObjectsSortMode.None))
                renderer.distanceScale = t.propDistanceScale;

            // Gölgeler
            QualitySettings.shadows = t.shadowQuality;
            QualitySettings.shadowDistance = t.shadowDistance;
            QualitySettings.pixelLightCount = t.pixelLightCount;

            var sun = RenderSettings.sun;
            if (sun != null) sun.shadows = t.sunShadows;

            // Kamera uzak kırpma + post-processing
            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = t.farClip;
                ApplyPostProcessing(cam, t);
            }

            // Yansıma probu: metalik boya rengini yansımadan aldığı için görsel etkisi
            // büyük, ama gerçek zamanlı prob her tazelemede sahneyi 6 kez çiziyor.
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                if (probe.mode != UnityEngine.Rendering.ReflectionProbeMode.Realtime) continue;
                probe.enabled = t.realtimeReflections;
            }

            // Sis: düşük cihazda yoğunlaştır — uzaktakiler elenirken kesme görünmesin
            if (!_capturedFog) { _baseFogDensity = RenderSettings.fogDensity; _capturedFog = true; }
            RenderSettings.fogDensity = _baseFogDensity * t.fogDensityScale;

            Debug.Log($"[GraphicsTuner] Kademe={ActiveTier} propMesafe={t.propDistanceScale:0.00} " +
                      $"gölge={t.shadowDistance:0}m farClip={t.farClip:0}");
        }

        // Sahnedeki global Volume'un profilini kademeye göre değiştirir.
        // Volume yoksa oluşturur — harita üreticisi ayrıca eklemek zorunda kalmasın.
        void ApplyPostProcessing(Camera cam, Tier t)
        {
            // URP kamerasında post-processing anahtarı ayrı; kapalıysa profil boşuna
            var cameraData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = !t.disablePostProcessing;

            if (t.disablePostProcessing) return;
            if (t.postProfile == null) return;

            var volume = FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                var go = new GameObject("~PostProcessVolume");
                go.transform.SetParent(transform, false);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0f;
            }

            volume.sharedProfile = t.postProfile;
        }

        QualityAutoDetect.Tier ResolveTier()
        {
            if (overrideTier) return forcedTier;

            // Detect()'i doğrudan çağırıyoruz: bu bileşen execution order -50 ile
            // QualityAutoDetect.Start()'tan önce koşar, yani DetectedTier henüz
            // hesaplanmamış olurdu.
            var detector = FindAnyObjectByType<QualityAutoDetect>();
            if (detector != null) return detector.Detect();

            // Detector sahnede yoksa doğrudan donanımdan hesapla
            int tier = Util.GameMath.QualityTier(
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize,
                SystemInfo.processorCount,
                Screen.width * Screen.height);
            return (QualityAutoDetect.Tier)tier;
        }
    }
}
