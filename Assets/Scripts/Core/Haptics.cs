using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DreamCar.Core
{
    // Titreşim/dokunsal geri bildirim hiç yoktu. Çarpışma, nitro, buton, başarım gibi
    // anlarda kısa haptic verir. iOS'ta native Taptic Engine, diğerlerinde Handheld.Vibrate.
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
            // Android'de granular haptic için native plugin gerek; basit titreşim yeterli.
            if (style is Style.Heavy or Style.Failure) Handheld.Vibrate();
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

#if UNITY_IOS && !UNITY_EDITOR
        // Bu bindings native tarafta bir .mm dosyası ister. Yoksa çağrı sessizce
        // EntryPointNotFoundException verir; try/catch ile yutuyoruz.
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
    }
}
