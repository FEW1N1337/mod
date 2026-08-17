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

            int daysBetween = (int)(nowUtc - last.Date).TotalDays;
            if (daysBetween == 1) Streak += 1;
            else if (daysBetween > 1 || last == DateTime.MinValue) Streak = 1;

            PlayerPrefs.SetInt(StreakKey, Streak);
            PlayerPrefs.SetString(LastLoginKey, nowUtc.ToString("o"));
            PlayerPrefs.Save();
            return Streak;
        }

        public float MultiplierFor(int streak)
        {
            if (streak >= 7) return 3f;
            if (streak >= 3) return 2f;
            return 1f;
        }
    }
}
