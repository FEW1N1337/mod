using UnityEngine;

namespace DreamCar.Settings
{
    // Eski iPhone'da da full quality açılıyordu → ısınma ve düşük FPS.
    // İlk açılışta cihazı puanlar ve uygun kalite seviyesini seçer. Kullanıcı
    // Ayarlar'dan değiştirdiyse bir daha karışmaz.
    public class QualityAutoDetect : MonoBehaviour
    {
        const string AppliedKey = "gfx.autodetect.v1";
        const string UserOverrodeKey = "gfx.userOverride";

        [Header("Hedef FPS")]
        public int lowTierFps = 30;
        public int midTierFps = 60;
        public int highTierFps = 60;

        [Header("Dinamik çözünürlük")]
        public bool enableDynamicResolution = true;
        public float lowTierRenderScale = 0.7f;
        public float midTierRenderScale = 0.85f;

        public enum Tier { Low, Mid, High }
        public Tier DetectedTier { get; private set; }

        void Start()
        {
            DetectedTier = Detect();

            // Kullanıcı elle ayar yaptıysa dokunma.
            if (PlayerPrefs.GetInt(UserOverrodeKey, 0) == 1) { ApplyResolutionOnly(); return; }
            if (PlayerPrefs.GetInt(AppliedKey, 0) == 1) { ApplyResolutionOnly(); return; }

            Apply(DetectedTier);
            PlayerPrefs.SetInt(AppliedKey, 1);
            PlayerPrefs.Save();
        }

        public Tier Detect()
        {
            int tier = Util.GameMath.QualityTier(
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize,
                SystemInfo.processorCount,
                Screen.width * Screen.height);
            return (Tier)tier;
        }

        public void Apply(Tier tier)
        {
            int qualityLevels = QualitySettings.names.Length;
            int level = tier switch
            {
                Tier.High => qualityLevels - 1,
                Tier.Mid => Mathf.Clamp(qualityLevels / 2, 0, qualityLevels - 1),
                _ => 0,
            };

            int fps = tier switch
            {
                Tier.High => highTierFps,
                Tier.Mid => midTierFps,
                _ => lowTierFps,
            };

            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.QualityLevel = level;
                GameSettings.Instance.TargetFps = fps;
                GameSettings.Instance.Apply();
            }
            else
            {
                QualitySettings.SetQualityLevel(level);
                Application.targetFrameRate = fps;
            }

            ApplyResolutionOnly();
            Debug.Log($"[QualityAutoDetect] Tier={tier} quality={level} fps={fps} " +
                      $"(mem={SystemInfo.systemMemorySize} gfx={SystemInfo.graphicsMemorySize} cores={SystemInfo.processorCount})");
        }

        void ApplyResolutionOnly()
        {
            if (!enableDynamicResolution) return;

            float scale = DetectedTier switch
            {
                Tier.High => 1f,
                Tier.Mid => midTierRenderScale,
                _ => lowTierRenderScale,
            };
            if (scale < 0.999f)
            {
                int w = Mathf.RoundToInt(Screen.width * scale);
                int h = Mathf.RoundToInt(Screen.height * scale);
                if (w > 0 && h > 0) Screen.SetResolution(w, h, Screen.fullScreen);
            }
        }

        // Ayarlar ekranı kullanıcı elle değiştirdiğinde bunu çağırmalı.
        public static void MarkUserOverride()
        {
            PlayerPrefs.SetInt(UserOverrodeKey, 1);
            PlayerPrefs.Save();
        }
    }
}
