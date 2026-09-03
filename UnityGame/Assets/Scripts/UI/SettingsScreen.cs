using DreamCar.Localization;
using DreamCar.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // GameSettings zaten değerleri tutuyordu ama hiçbir ekran ona bağlı değildi.
    // Bu ekran o boşluğu kapatır: kalite, FPS, ses seviyeleri, direksiyon hassasiyeti, dil.
    public class SettingsScreen : MonoBehaviour
    {
        [Header("Panel")]
        public GameObject panel;
        public Button closeButton;

        [Header("Grafik")]
        public TMP_Dropdown qualityDropdown;
        public TMP_Dropdown fpsDropdown;

        [Header("Ses")]
        public Slider masterSlider;
        public Slider musicSlider;
        public Slider sfxSlider;

        [Header("Kontrol")]
        public Slider steeringSensitivitySlider;
        public TMP_Text steeringValueLabel;

        [Header("Sürüş yardımcıları")]
        public Toggle absToggle;
        public Toggle tractionToggle;
        public Toggle stabilityToggle;

        [Header("Dil")]
        public TMP_Dropdown languageDropdown;

        readonly string[] _languageCodes = { "tr", "en" };
        static readonly int[] FpsOptions = { 30, 60, 120 };

        bool _applying;

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
            BuildDropdowns();
            LoadFromSettings();
            Hook();
        }

        void BuildDropdowns()
        {
            if (qualityDropdown)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            }
            if (fpsDropdown)
            {
                fpsDropdown.ClearOptions();
                fpsDropdown.AddOptions(new System.Collections.Generic.List<string> { "30 FPS", "60 FPS", "120 FPS" });
            }
            if (languageDropdown)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "Türkçe", "English" });
            }
        }

        void LoadFromSettings()
        {
            var s = GameSettings.Instance;
            if (s == null) return;

            _applying = true;
            if (qualityDropdown) qualityDropdown.value = Mathf.Clamp(s.QualityLevel, 0, QualitySettings.names.Length - 1);
            if (fpsDropdown) fpsDropdown.value = System.Array.IndexOf(FpsOptions, s.TargetFps) is var i && i >= 0 ? i : 1;
            if (masterSlider) masterSlider.value = s.MasterVolume;
            if (musicSlider) musicSlider.value = s.MusicVolume;
            if (sfxSlider) sfxSlider.value = s.SfxVolume;
            if (steeringSensitivitySlider)
            {
                steeringSensitivitySlider.minValue = 0.3f;
                steeringSensitivitySlider.maxValue = 2f;
                steeringSensitivitySlider.value = s.SteeringSensitivity;
            }
            if (absToggle) absToggle.isOn = s.AbsEnabled;
            if (tractionToggle) tractionToggle.isOn = s.TractionControlEnabled;
            if (stabilityToggle) stabilityToggle.isOn = s.StabilityControlEnabled;

            if (languageDropdown && LocalizationManager.Instance != null)
                languageDropdown.value = System.Array.IndexOf(_languageCodes, LocalizationManager.Instance.current) is var li && li >= 0 ? li : 0;

            UpdateSteeringLabel();
            _applying = false;
        }

        void Hook()
        {
            // Kullanıcı grafiği elle ayarladıysa QualityAutoDetect bir daha karışmasın.
            if (qualityDropdown) qualityDropdown.onValueChanged.AddListener(v =>
            {
                if (_applying || !GameSettings.Instance) return;
                GameSettings.Instance.QualityLevel = v;
                QualityAutoDetect.MarkUserOverride();
            });
            if (fpsDropdown) fpsDropdown.onValueChanged.AddListener(v =>
            {
                if (_applying || !GameSettings.Instance) return;
                GameSettings.Instance.TargetFps = FpsOptions[Mathf.Clamp(v, 0, FpsOptions.Length - 1)];
                QualityAutoDetect.MarkUserOverride();
            });
            if (masterSlider) masterSlider.onValueChanged.AddListener(v => { if (!_applying && GameSettings.Instance) GameSettings.Instance.MasterVolume = v; });
            if (musicSlider) musicSlider.onValueChanged.AddListener(v => { if (!_applying && GameSettings.Instance) GameSettings.Instance.MusicVolume = v; });
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { if (!_applying && GameSettings.Instance) GameSettings.Instance.SfxVolume = v; });
            if (steeringSensitivitySlider) steeringSensitivitySlider.onValueChanged.AddListener(v =>
            {
                if (_applying) return;
                if (GameSettings.Instance) GameSettings.Instance.SteeringSensitivity = v;
                UpdateSteeringLabel();
            });
            if (languageDropdown) languageDropdown.onValueChanged.AddListener(v =>
            {
                if (_applying || LocalizationManager.Instance == null) return;
                LocalizationManager.Instance.SetLanguage(_languageCodes[Mathf.Clamp(v, 0, _languageCodes.Length - 1)]);
            });

            // Yardımcı toggle'ları: değer GameSettings'e yazılıyor ve o an
            // sahnedeki araçların DrivingAssists'i tazeleniyor — oyuncu duraklama
            // menüsünden değiştirip devam ettiğinde etki anında görünsün.
            if (absToggle) absToggle.onValueChanged.AddListener(v =>
            {
                if (_applying || !GameSettings.Instance) return;
                GameSettings.Instance.AbsEnabled = v;
                RefreshAssists();
            });
            if (tractionToggle) tractionToggle.onValueChanged.AddListener(v =>
            {
                if (_applying || !GameSettings.Instance) return;
                GameSettings.Instance.TractionControlEnabled = v;
                RefreshAssists();
            });
            if (stabilityToggle) stabilityToggle.onValueChanged.AddListener(v =>
            {
                if (_applying || !GameSettings.Instance) return;
                GameSettings.Instance.StabilityControlEnabled = v;
                RefreshAssists();
            });
        }

        // Sahnedeki tüm araçların yardımcılarını yeni ayarla tazeler. Yardımcılar
        // ayarı FixedUpdate'te değil önbellekten okuyor, o yüzden bu itme gerekli.
        static void RefreshAssists()
        {
            foreach (var a in FindObjectsByType<DreamCar.Vehicle.DrivingAssists>(
                         FindObjectsSortMode.None))
                if (a) a.Refresh();
        }

        void UpdateSteeringLabel()
        {
            if (steeringValueLabel && steeringSensitivitySlider)
                steeringValueLabel.text = steeringSensitivitySlider.value.ToString("0.0") + "x";
        }

        public void Open() { if (panel) panel.SetActive(true); LoadFromSettings(); }
        public void Close() { if (panel) panel.SetActive(false); }
    }
}
