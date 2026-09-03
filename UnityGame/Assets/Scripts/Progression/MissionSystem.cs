using System;
using System.Collections.Generic;
using UnityEngine;
using DreamCar.Core;
using DreamCar.Economy;

namespace DreamCar.Progression
{
    public enum MissionType { Distance = 0, Races = 1, Money = 2, DriveTime = 3 }

    // Tek bir günlük görev. PlayerPrefs'e JSON olarak serileştiği için düz
    // alanlı ve [Serializable].
    [Serializable]
    public class Mission
    {
        public MissionType type;
        public double target;
        public double baseline;   // atandığı andaki ömür-boyu istatistik değeri
        public long rewardMoney;
        public int rewardXp;
        public bool claimed;
    }

    // Günlük görevler. Dream Road tarzı bir oyunun "her gün gir" döngüsü.
    //
    // İlerleme, PlayerStats'in ömür-boyu sayaçlarından TÜRETİLİYOR: görev
    // atandığında o anki değer baseline olarak saklanıyor, ilerleme = güncel −
    // baseline. Böylece ayrı bir "görev ilerlemesi" sayacı tutmaya ve onu her
    // ödül noktasına bağlamaya gerek yok — o bağlama işi bu projenin baskın
    // hata kaynağı.
    public class MissionSystem : MonoBehaviour
    {
        public static MissionSystem Instance { get; private set; }

        public event Action OnChanged;

        const string DateKey = "missions.date";
        const string DataKey = "missions.json";
        const int DailyCount = 3;

        [Serializable] class Wrapper { public List<Mission> items = new(); }

        readonly List<Mission> _missions = new();
        public IReadOnlyList<Mission> Missions => _missions;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged += NotifyChanged;
        }

        void OnDisable()
        {
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged -= NotifyChanged;
        }

        void Start()
        {
            EnsureToday();
            // Start'ta abonelik garanti (PlayerStats Awake sırası belirsiz).
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnChanged -= NotifyChanged;
                PlayerStats.Instance.OnChanged += NotifyChanged;
            }
            OnChanged?.Invoke();
        }

        void NotifyChanged() => OnChanged?.Invoke();

        // Bugünün görevleri yüklü mü? Tarih değiştiyse yeniden üret.
        void EnsureToday()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(DateKey, "") == today && Load())
                return;

            Generate(today);
        }

        bool Load()
        {
            var raw = PlayerPrefs.GetString(DataKey, "");
            if (string.IsNullOrEmpty(raw)) return false;
            try
            {
                var w = JsonUtility.FromJson<Wrapper>(raw);
                if (w == null || w.items == null || w.items.Count == 0) return false;
                _missions.Clear();
                _missions.AddRange(w.items);
                return true;
            }
            catch { return false; }
        }

        void Save()
        {
            var w = new Wrapper { items = new List<Mission>(_missions) };
            PlayerPrefs.SetString(DataKey, JsonUtility.ToJson(w));
            PlayerPrefs.Save();
        }

        void Generate(string today)
        {
            _missions.Clear();

            // Güne göre deterministik: aynı gün aynı görevler, yeniden açılışta
            // rerolllanmıyor. Tür seçimi karıştırılıp ilk üçü alınıyor.
            var rng = new System.Random(today.GetHashCode());
            var types = new List<MissionType>
            {
                MissionType.Distance, MissionType.Races, MissionType.Money, MissionType.DriveTime,
            };
            for (int i = types.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (types[i], types[j]) = (types[j], types[i]);
            }

            for (int i = 0; i < DailyCount && i < types.Count; i++)
                _missions.Add(Make(types[i]));

            PlayerPrefs.SetString(DateKey, today);
            Save();
        }

        Mission Make(MissionType type)
        {
            var m = new Mission { type = type, baseline = CurrentStat(type) };
            switch (type)
            {
                case MissionType.Distance:  m.target = 5000; m.rewardMoney = 1500; m.rewardXp = 200; break;
                case MissionType.Races:     m.target = 3;    m.rewardMoney = 2000; m.rewardXp = 300; break;
                case MissionType.Money:     m.target = 3000; m.rewardMoney = 1000; m.rewardXp = 150; break;
                case MissionType.DriveTime: m.target = 600;  m.rewardMoney = 1200; m.rewardXp = 180; break;
            }
            return m;
        }

        // İlgili ömür-boyu istatistik değeri.
        static double CurrentStat(MissionType type)
        {
            var st = PlayerStats.Instance;
            if (st == null) return 0;
            return type switch
            {
                MissionType.Distance  => st.TotalDistanceMeters,
                MissionType.Races     => st.RacesFinished,
                MissionType.Money     => st.TotalMoneyEarned,
                MissionType.DriveTime => st.TotalDriveSeconds,
                _ => 0,
            };
        }

        public double Progress(Mission m) => Math.Max(0, CurrentStat(m.type) - m.baseline);
        public bool IsComplete(Mission m) => !m.claimed && Progress(m) >= m.target;

        public string Describe(Mission m) => m.type switch
        {
            MissionType.Distance  => "5 km sür",
            MissionType.Races     => "3 yarış bitir",
            MissionType.Money     => "3.000 ₺ kazan",
            MissionType.DriveTime => "10 dakika sür",
            _ => "-",
        };

        // Tamamlanan görevin ödülünü ver. UI'daki "Al" butonu çağırıyor.
        public bool Claim(Mission m)
        {
            if (m == null || m.claimed || !IsComplete(m)) return false;
            m.claimed = true;
            Save();

            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(m.rewardMoney);
            if (DriverProfile.Instance != null) DriverProfile.Instance.AddBonusXp(m.rewardXp);

            UI.ToastNotification.Show($"Görev tamam! +{m.rewardMoney:N0} ₺, +{m.rewardXp} XP");
            OnChanged?.Invoke();
            return true;
        }
    }
}
