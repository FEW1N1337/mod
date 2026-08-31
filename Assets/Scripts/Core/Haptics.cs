using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DreamCar.Core
{
    // Dokunsal geri bildirim. iOS'ta native Taptic Engine (Plugins/iOS/DreamCarNative.mm),
    // Android'de AndroidJavaObject üstünden Vibrator servisi — ayrı bir Java plugin
    // dosyası gerekmez, JNI köprüsü C# içinden kurulur.
    public class Haptics : MonoBehaviour
    {
        public static Haptics Instance { get; private set; }

        public enum Style { Light, Medium, Heavy, Success, Warning, Failure, Selection }

        const string PrefKey = "haptics.enabled";
        public float minIntervalSeconds = 0.05f;

        float _lastPlayTime;

        public bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID && !UNITY_EDITOR
            InitAndroid();
#endif
        }

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            DisposeAndroid();
#endif
        }

        public static void Play(Style style)
        {
            if (Instance == null) return;
            Instance.PlayInternal(style);
        }

        void PlayInternal(Style style)
        {
            if (!Enabled) return;
            if (Time.unscaledTime - _lastPlayTime < minIntervalSeconds) return;
            _lastPlayTime = Time.unscaledTime;

#if UNITY_IOS && !UNITY_EDITOR
            TriggerIos(style);
#elif UNITY_ANDROID && !UNITY_EDITOR
            TriggerAndroid(style);
#endif
        }

        // Çarpışma şiddetine göre otomatik seviye seçer.
        public static void PlayImpact(float impulseMagnitude)
        {
            if (impulseMagnitude < 200f) return;
            var style = impulseMagnitude > 4000f ? Style.Heavy
                      : impulseMagnitude > 1200f ? Style.Medium
                      : Style.Light;
            Play(style);
        }

        // ---------------------------------------------------------- iOS
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _HapticImpact(int intensity);
        [DllImport("__Internal")] static extern void _HapticNotification(int type);
        [DllImport("__Internal")] static extern void _HapticSelection();

        void TriggerIos(Style style)
        {
            try
            {
                switch (style)
                {
                    case Style.Light:     _HapticImpact(0); break;
                    case Style.Medium:    _HapticImpact(1); break;
                    case Style.Heavy:     _HapticImpact(2); break;
                    case Style.Success:   _HapticNotification(0); break;
                    case Style.Warning:   _HapticNotification(1); break;
                    case Style.Failure:   _HapticNotification(2); break;
                    case Style.Selection: _HapticSelection(); break;
                }
            }
            catch (System.EntryPointNotFoundException)
            {
                // Native plugin eklenmemiş — sessizce geç.
            }
        }
#endif

        // ---------------------------------------------------------- Android
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _vibrator;
        AndroidJavaClass _vibrationEffectClass;
        bool _supportsAmplitude;
        int _apiLevel;

        void InitAndroid()
        {
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _apiLevel = version.GetStatic<int>("SDK_INT");

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                if (_apiLevel >= 31)
                {
                    // Android 12+: VibratorManager üstünden alınmalı.
                    using var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                    _vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                }
                else
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_apiLevel >= 26)
                {
                    _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    _supportsAmplitude = _vibrator != null && _vibrator.Call<bool>("hasAmplitudeControl");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Android vibrator alınamadı: " + e.Message);
                _vibrator = null;
            }
        }

        void DisposeAndroid()
        {
            _vibrator?.Dispose();
            _vibrationEffectClass?.Dispose();
            _vibrator = null;
            _vibrationEffectClass = null;
        }

        // Stilleri süre + genlik çiftine çevirir. Android'de iOS'un ayrık haptic
        // tipleri yok; kısa titreşim desenleriyle taklit ediyoruz.
        void TriggerAndroid(Style style)
        {
            if (_vibrator == null) return;

            try
            {
                switch (style)
                {
                    case Style.Light:     OneShot(12, 60); break;
                    case Style.Medium:    OneShot(22, 140); break;
                    case Style.Heavy:     OneShot(38, 255); break;
                    case Style.Selection: OneShot(8, 40); break;

                    // Bildirim tipleri: çok kısa desenler
                    case Style.Success:   Pattern(new long[] { 0, 14, 60, 14 }, 160); break;
                    case Style.Warning:   Pattern(new long[] { 0, 24, 90, 24 }, 200); break;
                    case Style.Failure:   Pattern(new long[] { 0, 34, 70, 34, 70, 34 }, 255); break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Android titreşim hatası: " + e.Message);
            }
        }

        void OneShot(long milliseconds, int amplitude)
        {
            if (_apiLevel >= 26 && _vibrationEffectClass != null)
            {
                // Cihaz genlik desteklemiyorsa DEFAULT_AMPLITUDE (-1) kullan.
                int effectiveAmplitude = _supportsAmplitude ? Mathf.Clamp(amplitude, 1, 255) : -1;
                using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, effectiveAmplitude);
                _vibrator.Call("vibrate", effect);
            }
            else
            {
                // API 25 ve altı: sadece süre.
                _vibrator.Call("vibrate", milliseconds);
            }
        }

        void Pattern(long[] timings, int amplitude)
        {
            if (_apiLevel >= 26 && _vibrationEffectClass != null)
            {
                if (_supportsAmplitude)
                {
                    var amplitudes = new int[timings.Length];
                    for (int i = 0; i < timings.Length; i++)
                        amplitudes[i] = i % 2 == 1 ? Mathf.Clamp(amplitude, 1, 255) : 0;

                    using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", timings, amplitudes, -1);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", timings, -1);
                    _vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                _vibrator.Call("vibrate", timings, -1);
            }
        }
#endif
    }
}
