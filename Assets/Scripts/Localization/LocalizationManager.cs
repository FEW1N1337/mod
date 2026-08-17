using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace DreamCar.Localization
{
    // Basit key-value çeviri. Şu an TR/EN yerleşik. Genişletmek için AddLanguage.
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }
        public string current = "tr";

        readonly Dictionary<string, Dictionary<string, string>> _dict = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            AddLanguage("tr", new()
            {
                { "play", "Oyna" },
                { "shop", "Mağaza" },
                { "garage", "Garaj" },
                { "settings", "Ayarlar" },
                { "buy", "Satın Al" },
                { "owned", "Sahip" },
                { "room.password", "Şifre" },
                { "room.create", "Oda Kur" },
                { "room.join", "Katıl" },
                { "money.short", "₺" },
                { "race.lap", "Tur" },
                { "race.best_lap", "En İyi Tur" },
                { "nitro", "Nitro" },
                { "fuel", "Yakıt" },
            });
            AddLanguage("en", new()
            {
                { "play", "Play" },
                { "shop", "Shop" },
                { "garage", "Garage" },
                { "settings", "Settings" },
                { "buy", "Buy" },
                { "owned", "Owned" },
                { "room.password", "Password" },
                { "room.create", "Create Room" },
                { "room.join", "Join" },
                { "money.short", "$" },
                { "race.lap", "Lap" },
                { "race.best_lap", "Best Lap" },
                { "nitro", "Nitro" },
                { "fuel", "Fuel" },
            });

            current = PlayerPrefs.GetString("lang", Application.systemLanguage == SystemLanguage.Turkish ? "tr" : "en");
        }

        public void AddLanguage(string code, Dictionary<string, string> entries) => _dict[code] = entries;

        public string T(string key) =>
            _dict.TryGetValue(current, out var d) && d.TryGetValue(key, out var s) ? s : key;

        public void SetLanguage(string code)
        {
            current = code;
            PlayerPrefs.SetString("lang", code);
            PlayerPrefs.Save();
            foreach (var l in FindObjectsByType<LocalizedText>(FindObjectsSortMode.None)) l.Refresh();
        }
    }

    public class LocalizedText : MonoBehaviour
    {
        public string key;
        void Start() => Refresh();
        public void Refresh()
        {
            var t = GetComponent<TMP_Text>();
            if (t && LocalizationManager.Instance) t.text = LocalizationManager.Instance.T(key);
        }
    }
}
