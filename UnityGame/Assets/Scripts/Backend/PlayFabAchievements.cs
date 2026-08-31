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

        readonly HashSet<string> _unlocked = new();
        readonly Dictionary<string, int> _progress = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocalCache();
        }

        void Start()
        {
            if (PlayFabAuth.Instance != null) PlayFabAuth.Instance.OnLoggedIn += PullProgress;
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
                foreach (var s in r.Statistics) { _progress[s.StatisticName] = s.Value; EvaluateForStat(s.StatisticName, s.Value); }
            }, err => Debug.LogWarning("[Achievements] Pull failed: " + err.ErrorMessage));
#endif
        }

        void LoadLocalCache()
        {
            _unlocked.Clear();
            var raw = PlayerPrefs.GetString("ach.unlocked", "");
            foreach (var s in raw.Split(',')) if (!string.IsNullOrWhiteSpace(s)) _unlocked.Add(s.Trim());
        }

        void SaveLocalCache()
        {
            PlayerPrefs.SetString("ach.unlocked", string.Join(",", _unlocked));
            PlayerPrefs.Save();
        }
    }
}
