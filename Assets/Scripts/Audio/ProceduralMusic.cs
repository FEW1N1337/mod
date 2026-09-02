using System.Collections;
using UnityEngine;

namespace DreamCar.Audio
{
    // Oyunda HİÇ MÜZİK YOKTU.
    //
    // MusicManager eksiksiz bir sistem — iki AudioSource ile crossfade,
    // shuffle, playlist, AudioBus üzerinden seviye, mixer desteği — ve iki
    // sahneye de ekleniyor. Ama menuTracks/gameplayTracks dizilerine proje
    // genelinde hiçbir yerden atama yapılmıyordu ve depoda tek bir ses dosyası
    // yok. PlayCurrent boş diziyi "tracks.Length == 0" ile koruyor, yani
    // patlamıyor: sessizce hiçbir şey yapmıyordu. Sistemin tamamı ölüydü.
    //
    // Neden çalışma anında üretim: AudioClip.Create + SetData ile üretilen klip
    // bir VARLIK DEĞİLDİR — sahneye gömülemez, sahne kaydedilince kaybolur.
    // ProceduralEngineAudio (motor, korna, çığlık) ve ProceduralWeatherAudio
    // (yağmur) aynı yolu izliyor; bu dosya o yerleşik deseni takip ediyor,
    // ayrı bir yol icat etmiyor.
    //
    // DÜRÜST UYARI: prosedürel müzik yer tutucu kalitesindedir. Ambient pad
    // gibi duyulur, prodüksiyon yapılmış bir soundtrack gibi değil. Sessizlikten
    // iyi ve projenin geri kalanıyla (doku, mesh, motor sesi) tutarlı; yayına
    // çıkarken lisanslı müzikle değiştirilmesi beklenir. Aşağıdaki "dizi boşsa
    // doldur" kuralı bunu tek alanlık bir işe indiriyor: MusicManager'ın
    // dizilerine gerçek klipler atandığı anda bu bileşen kenara çekilir.
    // [RequireComponent] BİLEREK yok: MusicManager yinelenen kopyayı kendi
    // Awake'inde Destroy(this) ile siliyor ve Unity, ona bağımlı bir bileşen
    // varken "Can't destroy MusicManager because ProceduralMusic depends on it"
    // hatası basardı — her sahne geçişinde Console'a. Eksik MusicManager zaten
    // aşağıda sessizce ele alınıyor.
    [DefaultExecutionOrder(-50)]   // MusicManager.Start()'tan (Play çağırır) ÖNCE
    public class ProceduralMusic : MonoBehaviour
    {
        [Header("Üretim")]
        [Tooltip("Müzik pad'i motor sesinin 44100'üne ihtiyaç duymuyor; 22050 belleği yarıya indiriyor.")]
        public int sampleRate = 22050;

        [Tooltip("Döngü uzunluğu (saniye). 16 sn ≈ parça başına 700 KB.")]
        public float loopSeconds = 16f;

        [Tooltip("Menü ve oyun içi liste başına üretilecek parça sayısı.")]
        public int tracksPerPlaylist = 2;

        MusicManager _music;

        void Awake()
        {
            // Her iki sahneye de bir MusicManager ekleniyor, ama tekil olan
            // hayatta kalıyor: ikinci sahnede yerel MusicManager kendi Awake'inde
            // yok ediliyor. Parçaları ona doldurmak boşa iş olurdu — hayatta
            // kalan tekil varsa onu hedefliyoruz.
            _music = MusicManager.Instance ? MusicManager.Instance : GetComponent<MusicManager>();
            if (!_music) return;

            // Zaten dolu (önceki sahnede üretilmiş ya da gerçek klipler atanmış):
            // yeniden sentezlemenin anlamı yok. Devre dışı bırakınca Start() da
            // çağrılmıyor, coroutine hiç başlamıyor.
            if (!IsEmpty(_music.menuTracks) && !IsEmpty(_music.gameplayTracks))
            {
                enabled = false;
                return;
            }

            // İLK parça hemen: MusicManager.Start() birazdan Play(Menu) çağıracak
            // ve elinde çalacak bir şey olmalı. Kalanlar açılışı kilitlememek için
            // sonraki karelere yayılıyor — telefonda 16 saniyelik dört klibi tek
            // karede sentezlemek gözle görülür bir donma olurdu.
            if (IsEmpty(_music.menuTracks))
                _music.menuTracks = new[] { Build("music_menu_0", calm: true, seed: 1) };
        }

        void Start() => StartCoroutine(BuildRest());

        IEnumerator BuildRest()
        {
            if (!_music) yield break;

            if (IsEmpty(_music.gameplayTracks))
            {
                yield return null;
                _music.gameplayTracks = new[] { Build("music_drive_0", calm: false, seed: 11) };
            }

            for (int i = 1; i < Mathf.Max(1, tracksPerPlaylist); i++)
            {
                yield return null;
                _music.menuTracks = Append(_music.menuTracks,
                    Build($"music_menu_{i}", calm: true, seed: 1 + i * 7));

                yield return null;
                _music.gameplayTracks = Append(_music.gameplayTracks,
                    Build($"music_drive_{i}", calm: false, seed: 11 + i * 7));
            }
        }

        static bool IsEmpty(AudioClip[] a) => a == null || a.Length == 0;

        static AudioClip[] Append(AudioClip[] a, AudioClip clip)
        {
            if (a == null || a.Length == 0) return new[] { clip };
            var next = new AudioClip[a.Length + 1];
            a.CopyTo(next, 0);
            next[a.Length] = clip;
            return next;
        }

        // --- Sentez ---------------------------------------------------------
        //
        // Üç katman: akorlu pad (gövde), bas figürü (nabız) ve filtrelenmiş
        // gürültü perküsyon (hareket). Menü listesi sakin ve yavaş, oyun içi
        // listesi daha tempolu ve parlak.
        //
        // Akorlar doğal minör bir dizinin dereceleri; rastgele nota seçmek
        // uyumsuz aralıklar üretirdi. Döngü uzunluğu tam bar sayısına
        // yuvarlanıyor, yoksa dikiş ritmin ortasına düşer.
        AudioClip Build(string name, bool calm, int seed)
        {
            var rng = new System.Random(seed);

            float bpm = calm ? 76f : 104f;
            float beat = 60f / bpm;
            float barLength = beat * 4f;
            int bars = Mathf.Max(2, Mathf.RoundToInt(loopSeconds / barLength));
            float duration = bars * barLength;

            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];

            // A doğal minör: A C D E F G — kök frekansları (Hz)
            float[] roots = { 110.00f, 130.81f, 146.83f, 164.81f, 174.61f, 196.00f };
            var progression = new int[bars];
            progression[0] = 0;                                   // her zaman kökten başla
            for (int b = 1; b < bars; b++) progression[b] = rng.Next(roots.Length);

            float lp = 0f, prevNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int bar = Mathf.Min(bars - 1, (int)(t / barLength));
                float root = roots[progression[bar]];

                // Bar içinde yumuşak giriş/çıkış — akor değişimi tıklamasın
                float barPos = (t - bar * barLength) / barLength;
                float barEnv = Mathf.Clamp01(barPos / 0.08f) * Mathf.Clamp01((1f - barPos) / 0.12f);

                // 1) Pad — kök + minör üçlü + beşli, hafif detune ile canlılık
                float pad = 0f;
                pad += Mathf.Sin(2f * Mathf.PI * root * t) * 0.5f;
                pad += Mathf.Sin(2f * Mathf.PI * root * 1.2f * t + 0.4f) * 0.32f;   // minör 3'lü
                pad += Mathf.Sin(2f * Mathf.PI * root * 1.5f * t + 0.9f) * 0.28f;   // 5'li
                pad += Mathf.Sin(2f * Mathf.PI * root * 2.01f * t) * 0.16f;         // detune oktav
                pad *= barEnv * (calm ? 0.55f : 0.42f);

                // 2) Bas — vuruş başına kısa zarf, kökün bir oktav altı
                float beatPos = (t / beat) % 1f;
                float bassEnv = Mathf.Pow(1f - beatPos, calm ? 3.5f : 2.2f);
                float bass = Mathf.Sin(2f * Mathf.PI * root * 0.5f * t) * bassEnv * 0.45f;

                // 3) Perküsyon — filtrelenmiş gürültü, offbeat'te vurgulu
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, white, calm ? 0.05f : 0.13f);
                float hp = lp - prevNoise;
                prevNoise = lp;
                float hatPos = (t / (beat * 0.5f)) % 1f;
                float hat = hp * Mathf.Pow(1f - hatPos, 9f) * (calm ? 0.10f : 0.26f);

                data[i] = ProceduralEngineAudio.SoftClip(pad + bass + hat) * 0.7f;
            }

            // Dikişi gizle — döngü başı/sonu arasında duyulur bir "tık" kalmasın.
            ProceduralEngineAudio.NormalizeAndFadeLoopSeam(data);

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
