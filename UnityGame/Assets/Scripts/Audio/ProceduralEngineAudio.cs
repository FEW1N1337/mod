using UnityEngine;

namespace DreamCar.Audio
{
    // Ses dosyası olmadan motor/lastik/korna sesi üretir. Additive synthesis:
    // temel frekans + harmonikler + patlama zarfı + gürültü. Üretilen klipler
    // kusursuz döngü olacak şekilde tam sayıda çevrim içerir, böylece tık sesi olmaz.
    //
    // EngineAudio bu klipleri RPM'e göre pitch'ler; burada sadece ham malzeme üretilir.
    public class ProceduralEngineAudio : MonoBehaviour
    {
        [Header("Kaynaklar")]
        public AudioSource idleSource;
        public AudioSource revSource;
        public AudioSource screechSource;
        public AudioSource hornSource;
        public AudioSource nitroSource;
        public AudioSource crashSource;

        [Header("Motor karakteri")]
        [Tooltip("Silindir sayısı — patlama sıklığını belirler, sesin karakterini değiştirir.")]
        public int cylinders = 4;
        [Tooltip("Rölanti temel frekansı (Hz). Düşük = daha kalın motor.")]
        public float idleFundamental = 42f;
        [Tooltip("Gaz açık temel frekans (Hz).")]
        public float revFundamental = 96f;
        [Range(0f, 1f)] public float roughness = 0.35f;

        [Header("Üretim")]
        public int sampleRate = 44100;
        public bool generateOnAwake = true;

        const float TwoPi = Mathf.PI * 2f;

        void Awake()
        {
            if (generateOnAwake) Generate();
        }

        public void Generate()
        {
            if (idleSource && idleSource.clip == null)
                idleSource.clip = BuildEngineClip("engine_idle", idleFundamental, 0.55f, harmonics: 7);

            if (revSource && revSource.clip == null)
                revSource.clip = BuildEngineClip("engine_rev", revFundamental, 0.85f, harmonics: 11);

            if (screechSource && screechSource.clip == null)
                screechSource.clip = BuildScreechClip("tire_screech");

            if (hornSource && hornSource.clip == null)
                hornSource.clip = BuildHornClip("horn");

            if (nitroSource && nitroSource.clip == null)
                nitroSource.clip = BuildNitroClip("nitro");

            if (crashSource && crashSource.clip == null)
                crashSource.clip = BuildCrashClip("crash");
        }

        // --- Motor ---
        // Gerçek bir motor: krank çevrimi başına `cylinders/2` patlama. Bunu, harmonik
        // serisinin üzerine periyodik bir zarf bindirerek taklit ediyoruz.
        AudioClip BuildEngineClip(string name, float fundamental, float drive, int harmonics)
        {
            // Kusursuz döngü için tam sayıda çevrim al.
            int cycles = Mathf.Max(8, Mathf.RoundToInt(fundamental / 6f));
            int samples = Mathf.RoundToInt(sampleRate * cycles / fundamental);
            var data = new float[samples];

            float firingPerCycle = Mathf.Max(1, cylinders / 2);
            var rng = new System.Random(name.GetHashCode());

            // Harmonik genlikleri: tek harmonikler baskın → daha "motor" bir ton.
            var amplitudes = new float[harmonics + 1];
            for (int h = 1; h <= harmonics; h++)
            {
                float odd = (h % 2 == 1) ? 1f : 0.55f;
                amplitudes[h] = odd / Mathf.Pow(h, 1.15f);
            }

            // Her harmoniğe küçük sabit faz kayması — faz hizalanmasından doğan
            // yapay "buzz" sesini kırar.
            var phases = new float[harmonics + 1];
            for (int h = 1; h <= harmonics; h++) phases[h] = (float)rng.NextDouble() * TwoPi;

            float noiseState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float cyclePos = (t * fundamental) % 1f;

                // Patlama zarfı — çevrim içinde firingPerCycle kez tepe yapar.
                float firing = (cyclePos * firingPerCycle) % 1f;
                float envelope = Mathf.Pow(1f - firing, 2.2f) * 0.75f + 0.25f;

                float sample = 0f;
                for (int h = 1; h <= harmonics; h++)
                    sample += Mathf.Sin(TwoPi * fundamental * h * t + phases[h]) * amplitudes[h];

                sample *= envelope;

                // Alçak geçirgen filtrelenmiş gürültü — mekanik pürüz.
                float white = (float)rng.NextDouble() * 2f - 1f;
                noiseState = Mathf.Lerp(noiseState, white, 0.08f);
                sample += noiseState * roughness * envelope;

                // Yumuşak doyum — sert klip yerine tüpvari sıkıştırma.
                data[i] = Mathf.Clamp(SoftClip(sample * drive), -1f, 1f) * 0.55f;
            }

            NormalizeAndFadeLoopSeam(data);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Lastik çığlığı ---
        // Bant geçirgen gürültü + hafif frekans kayması.
        AudioClip BuildScreechClip(string name)
        {
            int samples = sampleRate; // 1 sn döngü
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());

            float lp = 0f, hp = 0f, prev = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;

                // Bant geçirgen: alçak geçirgen sonra yüksek geçirgen
                lp = Mathf.Lerp(lp, white, 0.35f);
                hp = lp - prev;
                prev = lp;

                // Yavaş frekans modülasyonu — sabit "shhh" yerine canlı çığlık
                float wobble = 1f + Mathf.Sin(TwoPi * 3.5f * t) * 0.25f;
                float tone = Mathf.Sin(TwoPi * 1150f * wobble * t) * 0.22f;

                data[i] = Mathf.Clamp(hp * 2.6f + tone, -1f, 1f) * 0.45f;
            }

            NormalizeAndFadeLoopSeam(data);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Korna ---
        // İki kare dalga (majör üçlü aralığı) + attack/release zarfı.
        AudioClip BuildHornClip(string name)
        {
            float duration = 0.7f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];

            const float f1 = 440f;   // A4
            const float f2 = 554.4f; // C#5

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / samples;

                float attack = Mathf.Clamp01(progress / 0.04f);
                float release = Mathf.Clamp01((1f - progress) / 0.20f);
                float envelope = attack * release;

                float a = SquareWave(f1, t) * 0.5f;
                float b = SquareWave(f2, t) * 0.38f;

                data[i] = SoftClip((a + b) * envelope) * 0.5f;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Nitro ---
        // Basınçlı gaz kaçağı: geniş bantlı, boğuk bir "psssh". Lastik çığlığından
        // farkı, keskin tonal bileşenin (1150 Hz) hiç olmaması; onun yerine iki
        // kademeli alçak geçirgen ile yumuşatılmış gürültü ve yavaş türbülans var.
        AudioClip BuildNitroClip(string name)
        {
            int samples = sampleRate; // 1 sn döngü
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());

            float lp1 = 0f, lp2 = 0f, prev = 0f, rumble = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;

                // İki kademeli alçak geçirgen — çığlığa göre belirgin şekilde boğuk.
                lp1 = Mathf.Lerp(lp1, white, 0.18f);
                lp2 = Mathf.Lerp(lp2, lp1, 0.30f);

                // Zayıf yüksek geçirgen: DC kaymasını atar ama bandı geniş bırakır.
                float hp = lp2 - prev * 0.85f;
                prev = lp2;

                // Çok yavaş ikinci katman — tüpün alt gövde uğultusu.
                rumble = Mathf.Lerp(rumble, white, 0.02f);

                // Türbülans dalgalanması. 2 ve 5 Hz, 1 sn'lik döngüde tam sayıda
                // çevrim yapar; böylece dikişte genlik atlaması olmaz.
                float turbulence = 1f
                                 + Mathf.Sin(TwoPi * 2f * t) * 0.18f
                                 + Mathf.Sin(TwoPi * 5f * t) * 0.10f;

                float sample = hp * 3.4f + rumble * 0.9f;
                data[i] = SoftClip(sample * turbulence) * 0.5f;
            }

            NormalizeAndFadeLoopSeam(data);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Çarpma ---
        // Tek atış (DÖNGÜ DEĞİL): keskin attack + üstel decay. İki katman —
        // (a) geniş bantlı gürültü patlaması: sac/cam,
        // (b) ~70 Hz sinüs "thud": gövdenin ağırlığı, hızlı sönümlü.
        AudioClip BuildCrashClip(string name)
        {
            float duration = 0.45f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());

            float lp = 0f, prev = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;

                // ~4 ms attack, ardından iki ayrı hızda üstel sönüm.
                float attack = Mathf.Clamp01(t / 0.004f);
                float noiseDecay = Mathf.Exp(-t * 11f);
                float thudDecay = Mathf.Exp(-t * 26f);

                // (a) Metal katman: alçak geçirgen gövde + türevinden tiz çıtırtı.
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, white, 0.55f);
                float bright = lp - prev;
                prev = lp;
                float metal = (lp * 0.55f + bright * 1.6f) * noiseDecay;

                // (b) Thud: hafif düşen perde (70 Hz'den aşağı) darbe hissini artırır.
                float thud = Mathf.Sin(TwoPi * 70f * t - TwoPi * 18f * t * t) * thudDecay * 0.9f;

                data[i] = SoftClip((metal * 1.8f + thud) * attack);
            }

            // Tek atış olduğu için döngü dikişi harmanlanmaz — o işlem baş ile sonu
            // karıştırır ve patlamanın attack'ini kirletirdi. Sadece tepe normalizasyonu.
            NormalizePeak(data, 0.92f);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- Yardımcılar ---

        // ProceduralMusic de bu DSP yardımcılarını kullanıyor — sentez kodunu
        // ikinci kez yazmak, iki ayrı yerde ayrışan iki döngü dikişi demek olurdu.
        internal static float SquareWave(float frequency, float t)
        {
            // Yumuşatılmış kare — saf kare dalganın tiz aliasing'ini azaltır.
            float phase = (t * frequency) % 1f;
            float raw = phase < 0.5f ? 1f : -1f;
            float smooth = Mathf.Sin(TwoPi * frequency * t);
            return Mathf.Lerp(smooth, raw, 0.65f);
        }

        internal static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        // Tepe değerini hedefe çeker. Tek atış klipler için tek başına yeterlidir —
        // döngü dikişi harmanlaması istenmediğinde bu çağrılır.
        internal static void NormalizePeak(float[] data, float target)
        {
            float peak = 0f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak <= 0.0001f) return;

            float gain = target / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        // Tepe değerini normalize eder ve döngü dikişini kısa çapraz geçişle gizler.
        internal static void NormalizeAndFadeLoopSeam(float[] data)
        {
            NormalizePeak(data, 0.9f);

            int fade = Mathf.Min(256, data.Length / 8);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                int tail = data.Length - fade + i;
                float blended = Mathf.Lerp(data[tail], data[i], k);
                data[tail] = blended;
            }
        }
    }
}
