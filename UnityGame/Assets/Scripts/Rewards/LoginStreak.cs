using System;
using UnityEngine;

namespace DreamCar.Rewards
{
    // Ardışık gün sayacı. 24-48 saat "grace" — üstündeyse streak sıfırlanır.
    public class LoginStreak : MonoBehaviour
    {
        public static LoginStreak Instance { get; private set; }

        const string StreakKey = "streak.count";
        const string LastLoginKey = "streak.lastLoginUtc";

        public int Streak { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Recalculate();
        }

        void Recalculate()
        {
            Streak = PlayerPrefs.GetInt(StreakKey, 0);
        }

        public int RegisterLoginToday()
        {
            var nowUtc = DateTime.UtcNow.Date;
            string lastRaw = PlayerPrefs.GetString(LastLoginKey, "");
            DateTime last = DateTime.MinValue;
            DateTime.TryParse(lastRaw, out last);

            if (last == nowUtc) return Streak;

            int daysBetween = last == DateTime.MinValue ? int.MaxValue : (int)(nowUtc - last.Date).TotalDays;
            Streak = Util.GameMath.NextStreak(Streak, daysBetween);

            PlayerPrefs.SetInt(StreakKey, Streak);
            PlayerPrefs.SetString(LastLoginKey, nowUtc.ToString("o"));
            PlayerPrefs.Save();
            return Streak;
        }

        public float MultiplierFor(int streak) => Util.GameMath.StreakMultiplier(streak);
    }
}
