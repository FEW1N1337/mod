using System;
using System.Collections.Generic;
using UnityEngine;
using DreamCar.Core;
using DreamCar.Economy;
using DreamCar.Rewards;
using DreamCar.Settings;
using DreamCar.UI;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    // PlayFabMoneySync sadece parayı taşıyordu. Bu manager profilin tamamını
    // (garaj, aktif araç, plaka, boya, ayarlar, streak, istatistikler) tek JSON
    // olarak buluta yazar ve yeni cihazda geri yükler.
    public class PlayFabCloudSave : MonoBehaviour
    {
        public static PlayFabCloudSave Instance { get; private set; }

        const string ProfileKey = "profile.v1";
        const string StatsKey = "stats.v1";

        public float debounceSeconds = 5f;
        // Debounce üst sınırı: değişiklikler debounce'tan daha sık geliyorsa
        // (StatsTracker 5 sn'de bir flush ediyor, debounce da 5 sn) _dirtyTimer
        // her seferinde sıfırlanıp Push hiç çalışmıyordu. Bu süre dolunca
        // beklemeden yazılır.
        public float maxDeferSeconds = 30f;
        public bool verboseLogging;

        float _dirtyTimer;
        float _dirtyAge;
        bool _dirty;
        bool _pulled;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (PlayFabAuth.Instance != null)
            {
                PlayFabAuth.Instance.OnLoggedIn += Pull;
                // Login bu obje sahneye gelmeden tamamlandıysa event bir daha gelmez;
                // Pull hiç çalışmaz, _pulled false kalır ve bulut verisi hiç okunmazdı.
                if (PlayFabAuth.Instance.IsLoggedIn) Pull();
            }
            HookChangeSources();
        }

        void OnDestroy()
        {
            if (PlayFabAuth.Instance != null) PlayFabAuth.Instance.OnLoggedIn -= Pull;
            if (CarInventory.Instance != null) CarInventory.Instance.OnChanged -= MarkDirty;
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged -= MarkDirty;
        }

        void HookChangeSources()
        {
            if (CarInventory.Instance != null) CarInventory.Instance.OnChanged += MarkDirty;
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged += MarkDirty;
        }

        public void MarkDirty()
        {
            if (!_dirty) _dirtyAge = 0f; // yeni bekleme periyodu başlıyor
            _dirty = true;
            _dirtyTimer = 0f;
        }

        void Update()
        {
            if (!_dirty) return;
            _dirtyTimer += Time.unscaledDeltaTime;
            _dirtyAge += Time.unscaledDeltaTime;
            if (_dirtyTimer < debounceSeconds && _dirtyAge < maxDeferSeconds) return;
            if (!CanPush()) return; // bayrak korunur, koşul sağlanınca tekrar denenir
            _dirty = false;
            _dirtyTimer = 0f;
            _dirtyAge = 0f;
            Push();
        }

        // Pull tamamlanmadan Push edilirse buluttaki profil, yeni kurulumun boş
        // yerel değerleriyle ezilir (sessiz veri kaybı). Login yoksa istek zaten
        // hata döner; bu yüzden değişikliği kaybetmeden beklemek gerekiyor.
        bool CanPush() => _pulled && (PlayFabAuth.Instance == null || PlayFabAuth.Instance.IsLoggedIn);

        void OnApplicationPause(bool paused) { if (paused && _dirty && CanPush()) { _dirty = false; Push(); } }
        void OnApplicationQuit() { if (_dirty && CanPush()) { _dirty = false; Push(); } }

        // ---------------------------------------------------------- Push
        public void Push()
        {
#if PLAYFAB_INSTALLED
            var payload = new Dictionary<string, string>
            {
                { ProfileKey, BuildProfileJson() },
            };
            if (PlayerStats.Instance != null) payload[StatsKey] = PlayerStats.Instance.ToJson();

            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest { Data = payload },
                _ => { if (verboseLogging) Debug.Log("[CloudSave] Push OK"); },
                err => Debug.LogWarning("[CloudSave] Push failed: " + err.ErrorMessage));
#else
            if (verboseLogging) Debug.Log("[CloudSave] PlayFab yok — atlandı.");
#endif
        }

        string BuildProfileJson()
        {
            var p = new Profile();

            if (CarInventory.Instance != null)
            {
                p.activeCar = CarInventory.Instance.ActiveCarId;
                p.ownedCars = CarInventory.Instance.OwnedCarIds();
            }

            p.plateLeft = PlayerPrefs.GetString("plate.left", "");
            p.plateRight = PlayerPrefs.GetString("plate.right", "");
            p.plateText = PlayerPrefs.GetString("plate.text", "");
            p.paintColor = PlayerPrefs.GetString("car.color", "");
            p.paintMetallic = PlayerPrefs.GetFloat("car.metallic", 0.8f);
            p.paintSmoothness = PlayerPrefs.GetFloat("car.smoothness", 0.85f);

            if (LoginStreak.Instance != null) p.streak = LoginStreak.Instance.Streak;
            p.referralCode = PlayerPrefs.GetString("referral.myCode", "");
            p.referralRedeemed = PlayerPrefs.GetInt("referral.redeemed", 0) == 1;
            p.unlockedAchievements = PlayerPrefs.GetString("ach.unlocked", "");
            p.language = PlayerPrefs.GetString("lang", "tr");

            if (GameSettings.Instance != null)
            {
                p.master = GameSettings.Instance.MasterVolume;
                p.music = GameSettings.Instance.MusicVolume;
                p.sfx = GameSettings.Instance.SfxVolume;
                p.quality = GameSettings.Instance.QualityLevel;
                p.targetFps = GameSettings.Instance.TargetFps;
                p.steering = GameSettings.Instance.SteeringSensitivity;
            }

            return JsonUtility.ToJson(p);
        }

        // ---------------------------------------------------------- Pull
        public void Pull()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(), r =>
            {
                if (r.Data == null) { _pulled = true; return; }

                if (r.Data.TryGetValue(StatsKey, out var statsRecord) && PlayerStats.Instance != null)
                    PlayerStats.Instance.FromJson(statsRecord.Value);

                if (r.Data.TryGetValue(ProfileKey, out var profileRecord))
                    ApplyProfile(profileRecord.Value);

                _pulled = true;
                if (verboseLogging) Debug.Log("[CloudSave] Pull OK");
            }, err =>
            {
                _pulled = true;
                Debug.LogWarning("[CloudSave] Pull failed: " + err.ErrorMessage);
            });
#else
            _pulled = true;
#endif
        }

        void ApplyProfile(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            Profile p;
            try { p = JsonUtility.FromJson<Profile>(json); }
            catch (Exception e) { Debug.LogWarning("[CloudSave] Profil parse hatası: " + e.Message); return; }
            if (p == null) return;

            // Araçlar: bulut ∪ yerel (offline satın alma kaybolmasın).
            if (CarInventory.Instance != null && p.ownedCars != null)
            {
                CarInventory.Instance.MergeOwnedFromCloud(p.ownedCars, p.activeCar);
            }

            SetIfPresent("plate.left", p.plateLeft);
            SetIfPresent("plate.right", p.plateRight);
            SetIfPresent("plate.text", p.plateText);
            SetIfPresent("car.color", p.paintColor);
            PlayerPrefs.SetFloat("car.metallic", p.paintMetallic);
            PlayerPrefs.SetFloat("car.smoothness", p.paintSmoothness);
            SetIfPresent("referral.myCode", p.referralCode);
            PlayerPrefs.SetInt("referral.redeemed", p.referralRedeemed ? 1 : 0);
            MergeUnlockedAchievements(p.unlockedAchievements);
            SetIfPresent("lang", p.language);

            if (GameSettings.Instance != null && p.targetFps > 0)
            {
                GameSettings.Instance.MasterVolume = p.master;
                GameSettings.Instance.MusicVolume = p.music;
                GameSettings.Instance.SfxVolume = p.sfx;
                GameSettings.Instance.QualityLevel = p.quality;
                GameSettings.Instance.TargetFps = p.targetFps;
                GameSettings.Instance.SteeringSensitivity = p.steering;
                GameSettings.Instance.Apply();
            }

            PlayerPrefs.Save();
            ToastNotification.Show("Profil buluttan yüklendi");
        }

        // Başarım listesini düz üzerine yazmak offline açılan başarımları siliyordu;
        // ayrıca PlayFabAchievements cache'i Awake'te okuduğu için ilk SaveLocalCache
        // çağrısı buluttan geleni geri eziyordu. Union + bellek içi cache tazeleme.
        static void MergeUnlockedAchievements(string cloudList)
        {
            if (string.IsNullOrEmpty(cloudList)) return;
            var set = new HashSet<string>();
            foreach (var s in PlayerPrefs.GetString("ach.unlocked", "").Split(','))
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
            foreach (var s in cloudList.Split(','))
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
            PlayerPrefs.SetString("ach.unlocked", string.Join(",", set));
            if (PlayFabAchievements.Instance != null) PlayFabAchievements.Instance.ReloadLocalCache();
        }

        static void SetIfPresent(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) PlayerPrefs.SetString(key, value);
        }

        [Serializable]
        class Profile
        {
            public string activeCar;
            public List<string> ownedCars;
            public string plateLeft, plateRight, plateText;
            public string paintColor;
            public float paintMetallic = 0.8f, paintSmoothness = 0.85f;
            public int streak;
            public string referralCode;
            public bool referralRedeemed;
            public string unlockedAchievements;
            public string language;
            public float master = 1f, music = 0.8f, sfx = 1f, steering = 1f;
            public int quality, targetFps;
        }
    }
}
