using UnityEngine;
using UnityEngine.Audio;
using DreamCar.Audio;

namespace DreamCar.Settings
{
    // Grafik kalitesi, ses seviyeleri, kontrol hassasiyeti — PlayerPrefs persist + UI callback'leri.
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }
        public AudioMixer mixer;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Apply();
        }

        public int QualityLevel
        {
            get => PlayerPrefs.GetInt("gfx.quality", QualitySettings.GetQualityLevel());
            set { PlayerPrefs.SetInt("gfx.quality", value); QualitySettings.SetQualityLevel(value); }
        }

        public int TargetFps
        {
            get => PlayerPrefs.GetInt("gfx.fps", 60);
            set { PlayerPrefs.SetInt("gfx.fps", value); Application.targetFrameRate = value; }
        }

        // Anahtar adları AudioBus'ta tanımlı — iki taraf aynı yeri okusun.
        public float MasterVolume
        {
            get => PlayerPrefs.GetFloat(AudioBus.MasterKey, AudioBus.MasterDefault);
            set { PlayerPrefs.SetFloat(AudioBus.MasterKey, value); PushAudio(); }
        }

        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat(AudioBus.MusicKey, AudioBus.MusicDefault);
            set { PlayerPrefs.SetFloat(AudioBus.MusicKey, value); PushAudio(); }
        }

        public float SfxVolume
        {
            get => PlayerPrefs.GetFloat(AudioBus.SfxKey, AudioBus.SfxDefault);
            set { PlayerPrefs.SetFloat(AudioBus.SfxKey, value); PushAudio(); }
        }

        public float SteeringSensitivity
        {
            get => PlayerPrefs.GetFloat("input.steer", 1f);
            set => PlayerPrefs.SetFloat("input.steer", value);
        }

        // Sürüş yardımcıları. Varsayılan AÇIK: mobil dokunmatik kontrolde
        // araç yardımsız kolayca savruluyor, yeni oyuncu için açık gelmeli.
        // DrivingAssists bunları FixedUpdate'te değil, Start/Refresh'te okuyor.
        public bool AbsEnabled
        {
            get => PlayerPrefs.GetInt("assist.abs", 1) == 1;
            set => PlayerPrefs.SetInt("assist.abs", value ? 1 : 0);
        }

        public bool TractionControlEnabled
        {
            get => PlayerPrefs.GetInt("assist.tc", 1) == 1;
            set => PlayerPrefs.SetInt("assist.tc", value ? 1 : 0);
        }

        public bool StabilityControlEnabled
        {
            get => PlayerPrefs.GetInt("assist.esp", 1) == 1;
            set => PlayerPrefs.SetInt("assist.esp", value ? 1 : 0);
        }

        void SetMixerLinear(string param, float value01)
        {
            if (!mixer) return;
            float db = value01 <= 0.0001f ? -80f : Mathf.Log10(value01) * 20f;
            mixer.SetFloat(param, db);
        }

        // Mixer yalnızca ATANMIŞ ve parametreleri EXPOSE EDİLMİŞSE kullanılabilir.
        // Expose edilmemiş bir parametreye SetFloat sessizce başarısız olur; bunu
        // GetFloat ile önden yakalayıp AudioBus yoluna düşüyoruz, yoksa sürgüler
        // yine hiçbir şey yapmazdı.
        bool _mixerWarned;

        bool MixerUsable()
        {
            if (!mixer) return false;

            bool usable = mixer.GetFloat("Master", out _)
                       && mixer.GetFloat("Music", out _)
                       && mixer.GetFloat("SFX", out _);

            if (!usable && !_mixerWarned)
            {
                _mixerWarned = true;
                Debug.LogWarning(
                    "[GameSettings] AudioMixer atanmış ama Master/Music/SFX parametreleri " +
                    "expose edilmemiş. Ses sürgüleri mixer yerine AudioBus üzerinden " +
                    "çalışacak. Mixer'ı kullanmak istiyorsan üç Volume parametresini " +
                    "sağ tık → Expose ile aç ve tam olarak Master/Music/SFX adlarını ver.");
            }

            return usable;
        }

        void PushAudio()
        {
            bool useMixer = MixerUsable();

            // İki yoldan yalnızca biri devrede — aksi halde kısma iki kez uygulanır.
            AudioBus.Configure(useMixer);
            AudioBus.Set(MasterVolume, MusicVolume, SfxVolume);

            if (!useMixer) return;
            SetMixerLinear("Master", MasterVolume);
            SetMixerLinear("Music", MusicVolume);
            SetMixerLinear("SFX", SfxVolume);
        }

        public void Apply()
        {
            QualitySettings.SetQualityLevel(QualityLevel);
            Application.targetFrameRate = TargetFps;
            PushAudio();
        }
    }
}
