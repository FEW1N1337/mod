// CleverAdsSolutions (CAS) mediation — AdMob + AppLovin MAX + IronSource + Unity Ads
// + Vungle vb. tek waterfall'da. Tek ağ (Unity Ads) yerine mediation kullanmak eCPM'i
// belirgin artırır.
// Kurulum:
//   1) Package Manager → Add package from git URL:
//      https://github.com/cleveradssolutions/CAS-Unity.git
//   2) Assets → CleverAdsSolutions → Settings → iOS ve Android Manager ID'lerini gir
//      (CAS panelinde her platform için ayrı ID verilir)
//   3) Player Settings → Scripting Define Symbols → CAS_INSTALLED
using System;
using UnityEngine;
using DreamCar.Economy;

#if CAS_INSTALLED
using CAS;
#endif

namespace DreamCar.Monetization
{
    public class CASAdsManager : MonoBehaviour
    {
        public static CASAdsManager Instance { get; private set; }

        [Header("CAS — platform başına ayrı Manager ID")]
        public string iosManagerId = "demo";
        public string androidManagerId = "demo";
        public bool testMode = true;
        public long rewardAmount = 5000;

        string ManagerId =>
#if UNITY_ANDROID
            androidManagerId;
#else
            iosManagerId;
#endif

        public bool IsReady { get; private set; }

#if CAS_INSTALLED
        IMediationManager _manager;
        Action _pendingReward;
#endif

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        void Initialize()
        {
#if CAS_INSTALLED
            _manager = MobileAds.BuildManager()
                .WithManagerId(ManagerId)
                .WithTestAdMode(testMode)
                .WithCompletedListener(OnInitialized)
                .Build();
#else
            Debug.Log("[CAS] SDK yüklü değil — AdsManager Unity Ads yoluna düşecek.");
#endif
        }

#if CAS_INSTALLED
        void OnInitialized(InitialConfiguration config)
        {
            IsReady = true;
            Debug.Log("[CAS] Hazır. Error=" + (config.error ?? "none"));

            _manager.OnRewardedAdCompleted += HandleRewardEarned;
            _manager.OnRewardedAdFailedToShow += HandleFailed;
            _manager.OnRewardedAdClosed += HandleClosed;
        }

        void HandleRewardEarned()
        {
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(rewardAmount);
            Analytics.Event("ad_completed", new() { { "network", "cas" }, { "reward", rewardAmount } });
            _pendingReward?.Invoke();
            _pendingReward = null;
        }

        void HandleFailed(string error)
        {
            Debug.LogWarning("[CAS] Rewarded gösterilemedi: " + error);
            Analytics.Event("ad_failed", new() { { "network", "cas" }, { "error", error } });
            _pendingReward = null;
        }

        void HandleClosed() { _pendingReward = null; }
#endif

        public bool CanShowRewarded()
        {
#if CAS_INSTALLED
            return _manager != null && _manager.IsReadyAd(AdType.Rewarded);
#else
            return false;
#endif
        }

        public void ShowRewarded(Action onReward = null)
        {
#if CAS_INSTALLED
            if (!CanShowRewarded()) { Debug.LogWarning("[CAS] Rewarded hazır değil."); return; }
            _pendingReward = onReward;
            Analytics.Event("ad_shown", new() { { "network", "cas" }, { "placement", "rewarded" } });
            _manager.ShowAd(AdType.Rewarded);
#endif
        }

        public void ShowInterstitial()
        {
#if CAS_INSTALLED
            if (_manager == null || !_manager.IsReadyAd(AdType.Interstitial)) return;
            Analytics.Event("ad_shown", new() { { "network", "cas" }, { "placement", "interstitial" } });
            _manager.ShowAd(AdType.Interstitial);
#endif
        }
    }
}
