// Unity Ads scaffold. Package Manager'dan "Advertisement Legacy" veya
// "Advertisement with Mediation" import et. Project Settings → Services → Ads → On,
// iOS Game ID gir.
using System;
using UnityEngine;

#if UNITY_ADS
using UnityEngine.Advertisements;
#endif

using DreamCar.Economy;

namespace DreamCar.Monetization
{
#if UNITY_ADS
    public class AdsManager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
    {
        public static AdsManager Instance { get; private set; }
        public string iosGameId = "0000000";
        public string rewardedPlacement = "Rewarded_iOS";
        public long rewardAmount = 5000;
        public bool testMode = true;

        Action _pendingReward;
        bool _loaded;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Advertisement.Initialize(iosGameId, testMode, this);
        }

        public void OnInitializationComplete() => Advertisement.Load(rewardedPlacement, this);
        public void OnInitializationFailed(UnityAdsInitializationError error, string message) => Debug.LogError($"[Ads] Init failed: {error} {message}");

        public void ShowRewarded(Action onReward = null)
        {
            // CAS mediation kuruluysa onu tercih et (daha yüksek eCPM waterfall).
            if (CASAdsManager.Instance != null && CASAdsManager.Instance.CanShowRewarded())
            {
                CASAdsManager.Instance.ShowRewarded(onReward);
                return;
            }

            _pendingReward = onReward;
            Analytics.Event("ad_shown", new() { { "network", "unity" }, { "placement", rewardedPlacement } });
            if (_loaded) Advertisement.Show(rewardedPlacement, this);
            else Advertisement.Load(rewardedPlacement, this);
        }

        public void OnUnityAdsAdLoaded(string placementId) => _loaded = true;
        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message) => _loaded = false;
        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message) { _loaded = false; }
        public void OnUnityAdsShowStart(string placementId) { }
        public void OnUnityAdsShowClick(string placementId) { }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
        {
            if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
            {
                PlayerMoney.Instance?.Add(rewardAmount);
                Analytics.Event("ad_completed", new() { { "network", "unity" }, { "reward", rewardAmount } });
                _pendingReward?.Invoke();
            }
            _pendingReward = null;
            _loaded = false;
            Advertisement.Load(rewardedPlacement, this);
        }
    }
#else
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }
        public long rewardAmount = 5000;
        void Awake() { Instance = this; }
        public void ShowRewarded(Action onReward = null)
        {
            if (CASAdsManager.Instance != null && CASAdsManager.Instance.CanShowRewarded())
            {
                CASAdsManager.Instance.ShowRewarded(onReward);
                return;
            }
            Debug.Log("[Ads] Reklam SDK'sı yok — ödül simüle ediliyor.");
            PlayerMoney.Instance?.Add(rewardAmount);
            Analytics.Event("ad_completed", new() { { "network", "none" }, { "reward", rewardAmount } });
            onReward?.Invoke();
        }
    }
#endif
}
