using System;
using System.Collections.Generic;
using DreamCar.Economy;
using DreamCar.Social;
using DreamCar.UI;
using UnityEngine;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    // Yerel event'lerden statistic günceller ve threshold aşıldığında unlock verir.
    // Unlock durumu PlayerPrefs cache + PlayFab statistic'te tutulur.
    public class PlayFabAchievements : MonoBehaviour
    {
        public static PlayFabAchievements Instance { get; private set; }
        public AchievementCatalog catalog;

        const string UnlockedKey = "ach.unlocked";
        const string ProgressKey = "ach.progress.v1";

        readonly HashSet<string> _unlocked = new();
        readonly Dictionary<string, int> _progress = new();

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocalCache();
        }

        void Start()
        {
            if (PlayFabAuth.Instance != null)
            {
                PlayFabAuth.Instance.OnLoggedIn += PullProgress;
                // Login daha önce tamamlandıysa event bir daha gelmez; sunucudaki
                // ilerleme hiç okunmazdı.
                if (PlayFabAuth.Instance.IsLoggedIn) PullProgress();
            }
        }

        public void OnRaceFinished(bool won)
        {
            Increment("raceRuns", 1);
            if (won) Increment("raceWins", 1);
        }

        public void OnDriftBank(int totalBank) => Report("driftScore", totalBank);
        public void OnCarPurchased() => Increment("carsBought", 1);
        public void OnDistanceTravelled(float meters) => Increment("distanceMeters", Mathf.RoundToInt(meters));

        void Increment(string statistic, int amount)
        {
            _progress[statistic] = (_progress.TryGetValue(statistic, out int v) ? v : 0) + amount;
            Report(statistic, _progress[statistic]);
            EvaluateForStat(statistic, _progress[statistic]);
        }

        void Report(string statistic, int value)
        {
            _progress[statistic] = value;
            SaveLocalCache();
#if PLAYFAB_INSTALLED
            var req = new UpdatePlayerStatisticsRequest
            {
                Statistics = new List<StatisticUpdate> { new StatisticUpdate { StatisticName = statistic, Value = value } }
            };
            PlayFabClientAPI.UpdatePlayerStatistics(req, null,
                err => Debug.LogWarning("[Achievements] Stat push failed: " + err.ErrorMessage));
#endif
            EvaluateForStat(statistic, value);
        }

        void EvaluateForStat(string statistic, int value)
        {
            if (!catalog) return;
            foreach (var def in catalog.achievements)
            {
                if (!def || def.statistic != statistic) continue;
                if (_unlocked.Contains(def.id)) continue;
                if (value >= def.threshold) Unlock(def);
            }
        }

        void Unlock(AchievementDefinition def)
        {
            _unlocked.Add(def.id);
            SaveLocalCache();
            if (def.moneyReward > 0 && PlayerMoney.Instance != null) PlayerMoney.Instance.Add(def.moneyReward);
            Monetization.Analytics.Event("achievement_unlocked", new()
            {
                { "id", def.id },
                { "reward", def.moneyReward },
            });
            ToastNotification.Show($"🏆 {def.displayName}  +{def.moneyReward:N0} ₺");
        }

        void PullProgress()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetPlayerStatistics(new GetPlayerStatisticsRequest(), r =>
            {
                if (r.Statistics == null) return;
                foreach (var s in r.Statistics)
                {
                    // Buluttaki değeri düz atamak, çevrimdışıyken biriken ilerlemeyi
                    // geri alıyordu; PlayerStats.FromJson ile aynı kural: büyüğü kazanır.
                    int local = _progress.TryGetValue(s.StatisticName, out int v) ? v : 0;
                    int merged = Mathf.Max(local, s.Value);
                    _progress[s.StatisticName] = merged;
                    EvaluateForStat(s.StatisticName, merged);
                }
                SaveLocalCache();
            }, err => Debug.LogWarning("[Achievements] Pull failed: " + err.ErrorMessage));
#endif
        }

        // Bulut senkronu PlayerPrefs'teki listeyi güncelledikten sonra bellek içi
        // kopyayı tazelemek için (yoksa sonraki SaveLocalCache buluttan geleni ezer).
        public void ReloadLocalCache() => LoadLocalCache();

        void LoadLocalCache()
        {
            _unlocked.Clear();
            var raw = PlayerPrefs.GetString(UnlockedKey, "");
            foreach (var s in raw.Split(',')) if (!string.IsNullOrWhiteSpace(s)) _unlocked.Add(s.Trim());

            // İlerleme sayaçları hiç saklanmıyordu: SDK yokken (veya çevrimdışı)
            // _progress her açılışta sıfırdan başlıyor, kümülatif başarımlar
            // (mesafe, yarış sayısı) eşiğe hiç ulaşmıyordu.
            _progress.Clear();
            var progressRaw = PlayerPrefs.GetString(ProgressKey, "");
            if (string.IsNullOrEmpty(progressRaw)) return;
            try
            {
                var cache = JsonUtility.FromJson<ProgressCache>(progressRaw);
                if (cache?.keys == null || cache.values == null) return;
                for (int i = 0; i < cache.keys.Count && i < cache.values.Count; i++)
                    _progress[cache.keys[i]] = cache.values[i];
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Achievements] İlerleme cache'i okunamadı: " + e.Message);
            }
        }

        void SaveLocalCache()
        {
            PlayerPrefs.SetString(UnlockedKey, string.Join(",", _unlocked));

            var cache = new ProgressCache { keys = new List<string>(), values = new List<int>() };
            foreach (var kv in _progress) { cache.keys.Add(kv.Key); cache.values.Add(kv.Value); }
            PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(cache));

            PlayerPrefs.Save();
        }

        [Serializable] class ProgressCache { public List<string> keys; public List<int> values; }
    }
}
