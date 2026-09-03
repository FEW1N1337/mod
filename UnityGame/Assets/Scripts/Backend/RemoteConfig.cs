using System;
using System.Collections.Generic;
using UnityEngine;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    // Ödül miktarı, fiyat, cooldown gibi ayarları değiştirmek için app güncellemesi
    // gerekiyordu. Bu manager PlayFab TitleData'dan anahtar-değer çeker; kod her yerde
    // RemoteConfig.GetInt("race.win_reward", 1000) şeklinde okur.
    public class RemoteConfig : MonoBehaviour
    {
        public static RemoteConfig Instance { get; private set; }

        public event Action OnFetched;
        public bool IsFetched { get; private set; }

        const string CacheKey = "remoteconfig.cache.v1";

        readonly Dictionary<string, string> _values = new();

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCache();
        }

        void Start()
        {
#if PLAYFAB_INSTALLED
            if (PlayFabAuth.Instance != null) PlayFabAuth.Instance.OnLoggedIn += Fetch;
            else Fetch();
#else
            // SDK yokken OnLoggedIn hiç tetiklenmez; sahnede PlayFabAuth varsa Fetch
            // hiç çağrılmıyor, IsFetched false kalıyor ve OnFetched'i bekleyen ekranlar
            // sonsuza kadar bekliyordu. Cache/default'larla hemen hazır ol.
            Fetch();
#endif
        }

        public void Fetch()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), r =>
            {
                if (r.Data != null)
                {
                    _values.Clear();
                    foreach (var kv in r.Data) _values[kv.Key] = kv.Value;
                    SaveCache();
                }
                IsFetched = true;
                OnFetched?.Invoke();
                Debug.Log($"[RemoteConfig] {_values.Count} anahtar alındı.");
            }, err =>
            {
                IsFetched = true; // cache ile devam
                Debug.LogWarning("[RemoteConfig] Fetch başarısız, cache kullanılıyor: " + err.ErrorMessage);
                OnFetched?.Invoke();
            });
#else
            IsFetched = true;
            OnFetched?.Invoke();
#endif
        }

        // --- Okuma API'si (hepsi default'a düşer) ---
        public static int GetInt(string key, int defaultValue)
        {
            var raw = Raw(key);
            return int.TryParse(raw, out var v) ? v : defaultValue;
        }

        public static long GetLong(string key, long defaultValue)
        {
            var raw = Raw(key);
            return long.TryParse(raw, out var v) ? v : defaultValue;
        }

        public static float GetFloat(string key, float defaultValue)
        {
            var raw = Raw(key);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            var raw = Raw(key);
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            if (bool.TryParse(raw, out var v)) return v;
            return raw == "1";
        }

        public static string GetString(string key, string defaultValue)
        {
            var raw = Raw(key);
            return string.IsNullOrEmpty(raw) ? defaultValue : raw;
        }

        static string Raw(string key)
        {
            if (Instance == null) return null;
            return Instance._values.TryGetValue(key, out var v) ? v : null;
        }

        // --- Cache (ilk açılışta ağ yoksa son bilinen değerlerle çalış) ---
        void SaveCache()
        {
            var wrapper = new CacheWrapper { keys = new List<string>(), values = new List<string>() };
            foreach (var kv in _values) { wrapper.keys.Add(kv.Key); wrapper.values.Add(kv.Value); }
            PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }

        void LoadCache()
        {
            var raw = PlayerPrefs.GetString(CacheKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            try
            {
                var wrapper = JsonUtility.FromJson<CacheWrapper>(raw);
                if (wrapper?.keys == null || wrapper.values == null) return;
                for (int i = 0; i < wrapper.keys.Count && i < wrapper.values.Count; i++)
                    _values[wrapper.keys[i]] = wrapper.values[i];
            }
            catch (Exception e)
            {
                // Sessiz yutma yerine iz bırak: bozuk cache default'lara düşürür.
                Debug.LogWarning("[RemoteConfig] Cache okunamadı, yok sayılıyor: " + e.Message);
            }
        }

        [Serializable] class CacheWrapper { public List<string> keys; public List<string> values; }
    }
}
