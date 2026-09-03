using UnityEngine;

namespace DreamCar.Environment
{
    // Ses dosyası olmadan yağmur uğultusu üretir.
    //
    // Neden runtime: AudioClip.Create + SetData ile üretilen klip bir varlık
    // değildir; sahneye gömülemez, sahne kaydedilince kaybolur. Bu yüzden klip
    // editörde değil, oyun açılırken burada sentezlenir — ProceduralEngineAudio
    // motor sesleri için aynı yolu izliyor.
    //
    // Sentez: geniş bantlı gürültünün bant geçirgen filtrelenmişi ("şşş") +
    // alçak gövde uğultusu + seyrek damla tıkırtıları. Kusursuz döngü için
    // NormalizeAndFadeLoopSeam ile dikiş çapraz geçişle gizlenir.
    //
    // Ses seviyesine dokunmaz: taban seviye AudioSource üzerinde durur, onu
    // AudioBus (Weather.Awake'teki RegisterSfx) yönetir.
    [DefaultExecutionOrder(-100)]
    public class ProceduralWeatherAudio : MonoBehaviour
    {
        [Tooltip("Klibin atanacağı kaynak. Boşsa aynı objedeki AudioSource kullanılır.")]
        public AudioSource target;

        [Header("Üretim")]
        public int sampleRate = 44100;
        [Tooltip("Döngü uzunluğu (saniye). Uzun döngü daha az tekrar hissi verir, daha çok bellek yer.")]
        [Range(1f, 6f)] public float loopSeconds = 2.5f;
        [Tooltip("Damla tıkırtılarının sıklığı. 0 = sadece düz uğultu.")]
        [Range(0f, 1f)] public float dropletDensity = 0.5f;
        public bool generateOnAwake = true;

        const float TwoPi = Mathf.PI * 2f;

        void Awake()
        {
            if (generateOnAwake) Generate();
        }

        public void Generate()
        {
            if (!target) target = GetComponent<AudioSource>();
            if (!target || target.clip != null) return;   // elle atanmış klip varsa ona dokunma

            target.clip = BuildRainClip("weather_rain_loop");
        }

        // Yağmur: tek bir gürültü kaynağından iki filtre çıkarılır.
        //   lpMid → tiz sızıntı (yakın damlaların "şşş"si)
        //   lpLow → gövde uğultusu (uzaktaki yağmurun homurtusu)
        // Bant geçirgen = lpMid - lpLow; ikisi ayrı ayrı karıştırılır.
        AudioClip BuildRainClip(string name)
        {
            int samples = Mathf.Max(sampleRate / 2, Mathf.RoundToInt(sampleRate * loopSeconds));
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());

            float lpMid = 0f, lpLow = 0f;

            // Tek damla için sönümlenen sinüs — kısa, tiz bir "tık".
            float dropAmp = 0f, dropPhase = 0f, dropStep = 0f;
            double dropChance = 0.0022 * dropletDensity;

            for (int i = 0; i < samples; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;

                lpMid = Mathf.Lerp(lpMid, white, 0.42f);
                lpLow = Mathf.Lerp(lpLow, lpMid, 0.05f);
                float hiss = lpMid - lpLow;

                if (dropAmp < 0.02f && rng.NextDouble() < dropChance)
                {
                    dropAmp = 0.6f + (float)rng.NextDouble() * 0.4f;
                    dropStep = TwoPi * (900f + (float)rng.NextDouble() * 1700f) / sampleRate;
                    dropPhase = 0f;
                }

                float drop = 0f;
                if (dropAmp > 0.0001f)
                {
                    dropPhase += dropStep;
                    drop = Mathf.Sin(dropPhase) * dropAmp * 0.22f;
                    dropAmp *= 0.9991f;   // ~30 ms sönüm
                }

                // Yavaş sağanak dalgalanması. Tampon boyunca tam sayıda çevrim
                // olduğu için döngü dikişinde kopukluk yaratmaz.
                float gust = 1f + Mathf.Sin(TwoPi * 3f * i / samples) * 0.12f;

                data[i] = Mathf.Clamp((hiss * 2.4f + lpLow * 0.9f + drop) * gust, -1f, 1f) * 0.6f;
            }

            NormalizeAndFadeLoopSeam(data);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Tepe değerini normalize eder ve döngü dikişini kısa çapraz geçişle gizler.
        static void NormalizeAndFadeLoopSeam(float[] data)
        {
            float peak = 0f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0.0001f)
            {
                float gain = 0.9f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= gain;
            }

            int fade = Mathf.Min(256, data.Length / 8);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                int tail = data.Length - fade + i;
                data[tail] = Mathf.Lerp(data[tail], data[i], k);
            }
        }
    }
}
