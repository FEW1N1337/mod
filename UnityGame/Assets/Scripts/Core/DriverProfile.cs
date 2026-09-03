using System;
using UnityEngine;
using DreamCar.Economy;
using DreamCar.Util;

namespace DreamCar.Core
{
    // Sürücü seviyesi ve XP.
    //
    // XP TEK BİR YERDEN türetiliyor: PlayerStats'in ömür boyu değerleri +
    // görevlerden gelen bonus. Her ödül noktasına ayrı XP kancası takmıyoruz —
    // o yaklaşım "bir yerde vermeyi unutmak" hatasının ta kendisi ve bu projede
    // defalarca çıktı. Bütün gelir zaten PlayerStats'e aktığı için seviye
    // otomatik ilerliyor.
    public class DriverProfile : MonoBehaviour
    {
        public static DriverProfile Instance { get; private set; }

        // UI bunu dinliyor (rozet + XP çubuğu).
        public event Action OnChanged;
        // Seviye atlayınca (yeni seviye). Toast + ödül buna bağlı.
        public event Action<int> OnLevelUp;

        const string BonusKey = "xp.bonus";
        const string LastLevelKey = "xp.lastLevel";

        // Seviye başına para ödülü: seviye × bu. PlayerMoney üzerinden veriliyor.
        public long LevelUpRewardPerLevel = 1500;

        long _bonusXp;
        int _lastLevel;
        bool _processing;

        public long TotalXp { get; private set; }
        public int Level { get; private set; } = 1;
        public float LevelProgress01 { get; private set; }
        public long XpIntoLevel { get; private set; }
        public long XpForNextLevel { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bonusXp = (long)PlayerPrefs.GetInt(BonusKey, 0);
            _lastLevel = PlayerPrefs.GetInt(LastLevelKey, 1);
        }

        void OnEnable()
        {
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged += Recompute;
            Recompute();
        }

        void OnDisable()
        {
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged -= Recompute;
        }

        // Start'ta PlayerStats hazır değilse OnEnable aboneliği kaçabilir; garanti.
        void Start()
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnChanged -= Recompute;
                PlayerStats.Instance.OnChanged += Recompute;
            }
            Recompute();
        }

        // Görevler tamamlanınca bonus XP ekliyor.
        public void AddBonusXp(int amount)
        {
            if (amount <= 0) return;
            _bonusXp += amount;
            PlayerPrefs.SetInt(BonusKey, (int)Mathf.Clamp(_bonusXp, 0, int.MaxValue));
            PlayerPrefs.Save();
            Recompute();
        }

        void Recompute()
        {
            // Yeniden giriş koruması: seviye atlama ödülü PlayerMoney.Add çağırıyor,
            // o da PlayerStats.OnChanged'i tetikleyip buraya geri dönüyor. Bayrak
            // olmadan seviye atlama sonsuz döngüye girerdi.
            if (_processing) return;
            _processing = true;
            try
            {
                var st = PlayerStats.Instance;
                long statXp = st == null ? 0 : GameMath.DriverXpFromStats(
                    st.TotalDistanceMeters, st.TotalMoneyEarned,
                    st.RacesFinished, st.RacesWon, st.BestDriftScore, st.TotalDriveSeconds);

                TotalXp = statXp + _bonusXp;
                Level = GameMath.LevelForXp(TotalXp);
                LevelProgress01 = GameMath.LevelProgress(TotalXp);
                XpIntoLevel = TotalXp - GameMath.XpForLevel(Level);
                XpForNextLevel = GameMath.XpForLevel(Level + 1) - GameMath.XpForLevel(Level);

                OnChanged?.Invoke();

                if (Level > _lastLevel)
                {
                    // Birden fazla seviye birden atlanmış olabilir (görev bonusu
                    // büyükse): her biri için ödül ver.
                    for (int lv = _lastLevel + 1; lv <= Level; lv++)
                    {
                        OnLevelUp?.Invoke(lv);
                        if (PlayerMoney.Instance != null)
                            PlayerMoney.Instance.Add(LevelUpRewardPerLevel * lv);
                    }
                    _lastLevel = Level;
                    PlayerPrefs.SetInt(LastLevelKey, _lastLevel);
                    PlayerPrefs.Save();
                }
            }
            finally
            {
                _processing = false;
            }
        }
    }
}
