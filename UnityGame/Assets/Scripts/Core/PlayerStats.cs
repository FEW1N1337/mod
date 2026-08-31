using System;
using UnityEngine;

namespace DreamCar.Core
{
    // Oyuncunun kümülatif istatistikleri. PlayerPrefs'te tutulur, PlayFabCloudSave
    // (v0.6) bunu buluta da yazacak. StatsScreen ve achievement threshold'ları okur.
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }
        public event Action OnChanged;

        const string P = "stats.";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // --- Sayaçlar ---
        public float TotalDistanceMeters { get => Get("distance"); private set => Set("distance", value); }
        public float TotalDriveSeconds   { get => Get("driveTime"); private set => Set("driveTime", value); }
        public float TopSpeedKmh         { get => Get("topSpeed");  private set => Set("topSpeed", value); }
        public int   RacesFinished       { get => (int)Get("races"); private set => Set("races", value); }
        public int   RacesWon            { get => (int)Get("wins");  private set => Set("wins", value); }
        public int   BestDriftScore      { get => (int)Get("drift"); private set => Set("drift", value); }
        public long  TotalMoneyEarned    { get => (long)Get("earned"); private set => Set("earned", (float)value); }
        public int   CarsOwned           { get => (int)Get("cars");  private set => Set("cars", value); }
        public int   CollisionCount      { get => (int)Get("crashes"); private set => Set("crashes", value); }

        // --- Kayıt API'si ---
        public void AddDistance(float meters)
        {
            if (meters <= 0f) return;
            TotalDistanceMeters += meters;
            Flush();
        }

        public void AddDriveTime(float seconds)
        {
            if (seconds <= 0f) return;
            TotalDriveSeconds += seconds;
            Flush();
        }

        public void ReportSpeed(float kmh)
        {
            if (kmh <= TopSpeedKmh) return;
            TopSpeedKmh = kmh;
            Flush();
        }

        public void ReportRaceFinished(bool won)
        {
            RacesFinished += 1;
            if (won) RacesWon += 1;
            Flush();
        }

        public void ReportDriftScore(int score)
        {
            if (score <= BestDriftScore) return;
            BestDriftScore = score;
            Flush();
        }

        public void AddMoneyEarned(long amount)
        {
            if (amount <= 0) return;
            TotalMoneyEarned += amount;
            Flush();
        }

        public void SetCarsOwned(int count)
        {
            if (count == CarsOwned) return;
            CarsOwned = count;
            Flush();
        }

        public void ReportCollision()
        {
            CollisionCount += 1;
            Flush();
        }

        // --- Cloud save köprüsü (v0.6 PlayFabCloudSave kullanır) ---
        public string ToJson() => JsonUtility.ToJson(new Snapshot
        {
            distance = TotalDistanceMeters,
            driveTime = TotalDriveSeconds,
            topSpeed = TopSpeedKmh,
            races = RacesFinished,
            wins = RacesWon,
            drift = BestDriftScore,
            earned = TotalMoneyEarned,
            cars = CarsOwned,
            crashes = CollisionCount,
        });

        public void FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            Snapshot s;
            try { s = JsonUtility.FromJson<Snapshot>(json); }
            catch { return; }
            if (s == null) return;

            // Buluttaki değer daha büyükse onu al (kayıp önleme).
            TotalDistanceMeters = Mathf.Max(TotalDistanceMeters, s.distance);
            TotalDriveSeconds   = Mathf.Max(TotalDriveSeconds, s.driveTime);
            TopSpeedKmh         = Mathf.Max(TopSpeedKmh, s.topSpeed);
            RacesFinished       = Mathf.Max(RacesFinished, s.races);
            RacesWon            = Mathf.Max(RacesWon, s.wins);
            BestDriftScore      = Mathf.Max(BestDriftScore, s.drift);
            TotalMoneyEarned    = Math.Max(TotalMoneyEarned, s.earned);
            CarsOwned           = Mathf.Max(CarsOwned, s.cars);
            CollisionCount      = Mathf.Max(CollisionCount, s.crashes);
            Flush();
        }

        [Serializable]
        class Snapshot
        {
            public float distance, driveTime, topSpeed;
            public int races, wins, drift, cars, crashes;
            public long earned;
        }

        // --- Depolama ---
        static float Get(string key) => PlayerPrefs.GetFloat(P + key, 0f);
        static void Set(string key, float v) => PlayerPrefs.SetFloat(P + key, v);

        void Flush()
        {
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
