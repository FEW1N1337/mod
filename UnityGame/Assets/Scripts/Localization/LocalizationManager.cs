using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace DreamCar.Localization
{
    // Çeviriler artık C# içinde hardcoded değil: Resources/Localization/<code>.json
    // dosyalarından yüklenir. Yeni dil eklemek = yeni JSON dosyası koymak.
    // JSON bulunamazsa dahili küçük fallback listesi devreye girer.
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [Tooltip("Resources/Localization/ altında aranacak dil kodları.")]
        public string[] availableLanguages = { "tr", "en" };

        public string current = "tr";
        public event Action OnLanguageChanged;

        readonly Dictionary<string, Dictionary<string, string>> _dict = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAllFromResources();

            string defaultLang = Application.systemLanguage == SystemLanguage.Turkish ? "tr" : "en";
            current = PlayerPrefs.GetString("lang", defaultLang);
            if (!_dict.ContainsKey(current)) current = _dict.ContainsKey("en") ? "en" : defaultLang;
        }

        void LoadAllFromResources()
        {
            _dict.Clear();
            foreach (var code in availableLanguages)
            {
                var loaded = LoadLanguage(code);
                if (loaded != null) _dict[code] = loaded;
            }

            // Hiçbir JSON yoksa minimum fallback — oyun tamamen boş metinle açılmasın.
            if (_dict.Count == 0)
            {
                Debug.LogWarning("[Localization] JSON bulunamadı, fallback kullanılıyor.");
                _dict["tr"] = new Dictionary<string, string>
                {
                    { "play", "Oyna" }, { "shop", "Mağaza" }, { "garage", "Garaj" },
                    { "settings", "Ayarlar" }, { "buy", "Satın Al" }, { "owned", "Sahip" },
                    { "close", "Kapat" }, { "cancel", "İptal" },
                };
                _dict["en"] = new Dictionary<string, string>
                {
                    { "play", "Play" }, { "shop", "Shop" }, { "garage", "Garage" },
                    { "settings", "Settings" }, { "buy", "Buy" }, { "owned", "Owned" },
                    { "close", "Close" }, { "cancel", "Cancel" },
                };
            }
        }

        static Dictionary<string, string> LoadLanguage(string code)
        {
            var asset = Resources.Load<TextAsset>("Localization/" + code);
            if (asset == null) return null;

            LocalizationFile parsed;
            try { parsed = JsonUtility.FromJson<LocalizationFile>(asset.text); }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] {code}.json parse hatası: {e.Message}");
                return null;
            }
            if (parsed?.entries == null) return null;

            var map = new Dictionary<string, string>(parsed.entries.Length);
            foreach (var e in parsed.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                map[e.key] = e.value ?? e.key;
            }
            return map;
        }

        // Çeviri al. Key yoksa: aktif dil → İngilizce → key'in kendisi.
        public string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (_dict.TryGetValue(current, out var active) && active.TryGetValue(key, out var s)) return s;
            if (_dict.TryGetValue("en", out var en) && en.TryGetValue(key, out var fallback)) return fallback;
            return key;
        }

        public bool HasLanguage(string code) => _dict.ContainsKey(code);
        public IEnumerable<string> LoadedLanguages => _dict.Keys;

        public void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || !_dict.ContainsKey(code)) return;
            current = code;
            PlayerPrefs.SetString("lang", code);
            PlayerPrefs.Save();

            foreach (var l in FindObjectsByType<LocalizedText>(FindObjectsSortMode.None)) l.Refresh();
            OnLanguageChanged?.Invoke();
        }

        // Runtime'da ek çeviri enjekte etmek için (örn. sunucudan gelen metinler).
        public void AddOrOverride(string code, string key, string value)
        {
            if (!_dict.TryGetValue(code, out var map))
            {
                map = new Dictionary<string, string>();
                _dict[code] = map;
            }
            map[key] = value;
        }

        [Serializable] class LocalizationFile { public LocalizationEntry[] entries; }
        [Serializable] class LocalizationEntry { public string key; public string value; }
    }

    // TMP_Text bileşenine takılır; key'i çeviriye çevirir.
    public class LocalizedText : MonoBehaviour
    {
        public string key;

        void Start() => Refresh();

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key) || LocalizationManager.Instance == null) return;
            var t = GetComponent<TMP_Text>();
            if (t) t.text = LocalizationManager.Instance.T(key);
        }
    }
}
