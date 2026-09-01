using System;
using System.Globalization;
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
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
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
            // Kayıt UTC yazılıyor, ama düz TryParse dizeyi yerel saate çevirip Kind=Local
            // döndürüyordu: sonuç hiçbir zaman nowUtc'ye (UTC gece yarısı) eşit olmuyordu,
            // yani "aynı gün" kontrolü hiç tutmuyordu. UTC'nin gerisindeki saat dilimlerinde
            // last.Date bir gün geriye kayıp streak aynı gün ikinci kez artıyordu.
            DateTime.TryParse(lastRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out last);

            // Tarih (saat değil) karşılaştırılır — eski kayıtlarda saat bileşeni olabilir.
            if (last.Date == nowUtc) return Streak;

            int daysBetween = last == DateTime.MinValue ? int.MaxValue : (int)(nowUtc - last.Date).TotalDays;
            // Cihaz saati geri alınırsa fark negatif olur. Bu durumda streak'i ne ilerlet
            // ne de sıfırla; kayıt da güncellenmez, saat düzelince doğru fark hesaplanır.
            if (daysBetween < 0) return Streak;
            Streak = Util.GameMath.NextStreak(Streak, daysBetween);

            PlayerPrefs.SetInt(StreakKey, Streak);
            PlayerPrefs.SetString(LastLoginKey, nowUtc.ToString("o"));
            PlayerPrefs.Save();
            return Streak;
        }

        public float MultiplierFor(int streak) => Util.GameMath.StreakMultiplier(streak);
    }
}
