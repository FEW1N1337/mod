using UnityEngine;
using UnityEngine.Audio;

namespace DreamCar.Settings
{
    // Grafik kalitesi, ses seviyeleri, kontrol hassasiyeti — PlayerPrefs persist + UI callback'leri.
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }
        public AudioMixer mixer;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
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

        public float MasterVolume
        {
            get => PlayerPrefs.GetFloat("audio.master", 1f);
            set { PlayerPrefs.SetFloat("audio.master", value); SetMixerLinear("Master", value); }
        }

        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat("audio.music", 0.8f);
            set { PlayerPrefs.SetFloat("audio.music", value); SetMixerLinear("Music", value); }
        }

        public float SfxVolume
        {
            get => PlayerPrefs.GetFloat("audio.sfx", 1f);
            set { PlayerPrefs.SetFloat("audio.sfx", value); SetMixerLinear("SFX", value); }
        }

        public float SteeringSensitivity
        {
            get => PlayerPrefs.GetFloat("input.steer", 1f);
            set => PlayerPrefs.SetFloat("input.steer", value);
        }

        void SetMixerLinear(string param, float value01)
        {
            if (!mixer) return;
            float db = value01 <= 0.0001f ? -80f : Mathf.Log10(value01) * 20f;
            mixer.SetFloat(param, db);
        }

        public void Apply()
        {
            QualitySettings.SetQualityLevel(QualityLevel);
            Application.targetFrameRate = TargetFps;
            SetMixerLinear("Master", MasterVolume);
            SetMixerLinear("Music", MusicVolume);
            SetMixerLinear("SFX", SfxVolume);
        }
    }
}
