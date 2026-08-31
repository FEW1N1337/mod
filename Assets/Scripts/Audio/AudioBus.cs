using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Audio
{
    // Ses seviyesi yönlendirmesi.
    //
    // Sorun: GameSettings ses sürgüleri yalnızca bir AudioMixer varlığına yazıyordu.
    // AudioMixer koddan güvenilir şekilde üretilemiyor (public API'si yok), yani
    // kullanıcı Editor'de elle kurana kadar sürgüler hiçbir şey yapmıyordu — üstelik
    // sessizce, hiçbir uyarı vermeden.
    //
    // Çözüm: iki mod.
    //
    //  • Mixer atanmış VE parametreleri expose edilmişse
    //      → GameSettings mixer'a dB yazar. AudioBus çarpanları 1 döner, yani
    //        ses iki kez kısılmaz.
    //
    //  • Mixer yoksa (varsayılan durum)
    //      → Master, AudioListener.volume üzerinden uygulanır. Bu motor tarafında
    //        çalışan global bir çarpan; hiçbir script'in kendi ses seviyesini ezmez.
    //      → Music ve SFX çarpanlarını sesi üreten script'ler kendi taban
    //        seviyelerine uygular. Sesini her karede kendisi yazanlar (motor,
    //        lastik) çarpanı doğrudan okur; bir kez ayarlayıp Play() diyenler
    //        (korna, nitro, yağmur, çarpma) buraya kaydolur.
    //
    // Sonuç: proje hiçbir elle kurulum olmadan tam çalışır.
    public static class AudioBus
    {
        // PlayerPrefs anahtarları burada tanımlı; GameSettings de bunları kullanır,
        // böylece iki taraf farklı anahtar yazma riski kalmaz.
        public const string MasterKey = "audio.master";
        public const string MusicKey  = "audio.music";
        public const string SfxKey    = "audio.sfx";

        public const float MasterDefault = 1f;
        public const float MusicDefault  = 0.8f;
        public const float SfxDefault    = 1f;

        public static float Master { get; private set; } = MasterDefault;
        public static float Music { get; private set; } = MusicDefault;
        public static float Sfx { get; private set; } = SfxDefault;

        // Mixer devredeyse çarpanlar nötrlenir — kısma işini mixer yapar.
        public static bool MixerHandlesVolume { get; private set; }

        public static float MusicScale => MixerHandlesVolume ? 1f : Music;
        public static float SfxScale => MixerHandlesVolume ? 1f : Sfx;

        // Seviye değişince tetiklenir. Çalan sesini canlı güncellemek isteyenler dinler.
        public static event System.Action OnChanged;

        struct Entry
        {
            public AudioSource source;
            public float baseVolume;
        }

        static readonly List<Entry> Registered = new();

        // Editor'de domain reload kapalıyken statikler play oturumları arasında
        // yaşar; eski sahnenin yok olmuş kaynakları listede kalmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            Registered.Clear();
            OnChanged = null;
            MixerHandlesVolume = false;
            Master = MasterDefault;
            Music = MusicDefault;
            Sfx = SfxDefault;
        }

        // Kayıtlı seviyeleri sahne yüklenmeden önce uygular. GameSettings sahnede
        // olmasa da (örneğin Editor'de doğrudan Game sahnesini açınca) sesler
        // oyuncunun seçtiği seviyede başlar.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadSavedLevels()
        {
            Set(PlayerPrefs.GetFloat(MasterKey, MasterDefault),
                PlayerPrefs.GetFloat(MusicKey, MusicDefault),
                PlayerPrefs.GetFloat(SfxKey, SfxDefault));
        }

        public static void Configure(bool mixerHandlesVolume)
        {
            MixerHandlesVolume = mixerHandlesVolume;
        }

        public static void Set(float master, float music, float sfx)
        {
            Master = Mathf.Clamp01(master);
            Music = Mathf.Clamp01(music);
            Sfx = Mathf.Clamp01(sfx);
            Apply();
        }

        public static void Apply()
        {
            // Mixer devredeyken listener'a dokunma — yoksa kısma iki kez uygulanır.
            AudioListener.volume = MixerHandlesVolume ? 1f : Master;

            float scale = SfxScale;
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var entry = Registered[i];
                // Yok edilmiş kaynakları temizle (araç despawn olduğunda birikir).
                if (entry.source == null) { Registered.RemoveAt(i); continue; }
                entry.source.volume = entry.baseVolume * scale;
            }

            OnChanged?.Invoke();
        }

        // Bir kez ayarlanıp Play() edilen SFX kaynakları için. Kaydolduğu andaki
        // ses seviyesi "taban" kabul edilir — bu yüzden Awake/Start'ta, kendi
        // seviyesini yazdıktan sonra çağrılmalı.
        public static void RegisterSfx(AudioSource source)
        {
            if (source == null) return;

            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                if (Registered[i].source == null) { Registered.RemoveAt(i); continue; }
                if (Registered[i].source == source) return;   // zaten kayıtlı
            }

            Registered.Add(new Entry { source = source, baseVolume = source.volume });
            source.volume = source.volume * SfxScale;
        }

        public static void Unregister(AudioSource source)
        {
            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                var entry = Registered[i];
                if (entry.source == null) { Registered.RemoveAt(i); continue; }
                if (entry.source != source) continue;

                // Taban seviyeyi geri yaz: aynı kaynak sonradan yeniden
                // kaydolursa ölçeklenmiş değeri taban sanmasın.
                entry.source.volume = entry.baseVolume;
                Registered.RemoveAt(i);
            }
        }
    }
}
