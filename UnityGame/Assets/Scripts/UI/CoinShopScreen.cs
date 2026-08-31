using DreamCar.Economy;
using DreamCar.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // IAPManager ve AdsManager backend olarak vardı ama para satın alma ekranı yoktu.
    // Coin paketleri (IAP) + "reklam izle, para kazan" tek ekranda.
    public class CoinShopScreen : MonoBehaviour
    {
        [System.Serializable]
        public class CoinPack
        {
            public string productId;
            public string displayName = "50.000 ₺";
            public string priceLabel = "₺29,99";
            public Sprite icon;
            public Button buyButton;
        }

        public GameObject panel;
        public Button closeButton;
        public TMP_Text balanceLabel;

        [Header("IAP paketleri")]
        public CoinPack[] packs;

        [Header("Rewarded reklam")]
        public Button watchAdButton;
        public TMP_Text adRewardLabel;
        public GameObject adCooldownOverlay;
        public float adCooldownSeconds = 60f;

        float _adAvailableAt;

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);

            foreach (var pack in packs)
            {
                if (pack?.buyButton == null) continue;
                var id = pack.productId;
                pack.buyButton.onClick.AddListener(() =>
                {
                    if (IAPManager.Instance != null) IAPManager.Instance.Buy(id);
                    else ToastNotification.Show("Mağaza hazır değil");
                });
            }

            if (watchAdButton) watchAdButton.onClick.AddListener(WatchAd);
            if (adRewardLabel && AdsManager.Instance != null)
                adRewardLabel.text = $"+{AdsManager.Instance.rewardAmount:N0} ₺";
        }

        void OnEnable()
        {
            RefreshBalance();
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.OnMoneyChanged += OnMoney;
        }

        void OnDisable()
        {
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.OnMoneyChanged -= OnMoney;
        }

        void Update()
        {
            bool onCooldown = Time.unscaledTime < _adAvailableAt;
            if (adCooldownOverlay) adCooldownOverlay.SetActive(onCooldown);
            if (watchAdButton) watchAdButton.interactable = !onCooldown;
        }

        void WatchAd()
        {
            if (Time.unscaledTime < _adAvailableAt) return;
            if (AdsManager.Instance == null) { ToastNotification.Show("Reklam hazır değil"); return; }

            _adAvailableAt = Time.unscaledTime + adCooldownSeconds;
            AdsManager.Instance.ShowRewarded(() => ToastNotification.Show("Ödül alındı"));
        }

        void OnMoney(long _) => RefreshBalance();

        void RefreshBalance()
        {
            if (balanceLabel && PlayerMoney.Instance != null)
                balanceLabel.text = PlayerMoney.Instance.Money.ToString("N0") + " ₺";
        }

        public void Open() { if (panel) panel.SetActive(true); RefreshBalance(); }
        public void Close() { if (panel) panel.SetActive(false); }
    }
}
