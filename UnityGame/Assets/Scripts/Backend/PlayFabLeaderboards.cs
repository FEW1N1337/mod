using System;
using System.Collections.Generic;
using UnityEngine;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    public class PlayFabLeaderboards : MonoBehaviour
    {
        public const string RaceBestLapStat = "raceBestLap";
        public const string DriftScoreStat = "driftScore";

        public void SubmitRaceBestLap(int milliseconds) => Submit(RaceBestLapStat, -milliseconds); // negatif = küçük daha iyi
        public void SubmitDriftScore(int score) => Submit(DriftScoreStat, score);

        void Submit(string stat, int value)
        {
#if PLAYFAB_INSTALLED
            var req = new UpdatePlayerStatisticsRequest
            {
                Statistics = new List<StatisticUpdate> { new StatisticUpdate { StatisticName = stat, Value = value } }
            };
            PlayFabClientAPI.UpdatePlayerStatistics(req, null,
                err => Debug.LogWarning("[PlayFab] Stat update failed: " + err.ErrorMessage));
#endif
        }

        public void FetchTop(string stat, int count, Action<List<(string name, int value)>> onResult)
        {
#if PLAYFAB_INSTALLED
            var req = new GetLeaderboardRequest { StatisticName = stat, StartPosition = 0, MaxResultsCount = count };
            PlayFabClientAPI.GetLeaderboard(req, r =>
            {
                var rows = new List<(string, int)>();
                foreach (var e in r.Leaderboard) rows.Add((e.DisplayName ?? e.PlayFabId, e.StatValue));
                onResult?.Invoke(rows);
            }, err => Debug.LogWarning("[PlayFab] Leaderboard fetch failed: " + err.ErrorMessage));
#else
            onResult?.Invoke(new List<(string, int)>());
#endif
        }
    }
}
