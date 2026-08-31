using System;
using DreamCar.Economy;
using DreamCar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Rewards
{
    // İlk açılışta bugün ödül alındı mı? Alınmadıysa popup + streak bonus ile ödeme.
    public class DailyReward : MonoBehaviour
    {
        public GameObject popup;
        public TMP_Text amountLabel;
        public TMP_Text streakLabel;
        public Button claimButton;
        public long baseAmount = 500;

        const string LastClaimKey = "daily.lastClaimUtc";

        int _streakForToday;
        long _amountForToday;

        void Start()
        {
            if (popup) popup.SetActive(false);
            if (claimButton) claimButton.onClick.AddListener(Claim);
            TryShow();
        }

        void TryShow()
        {
            string lastRaw = PlayerPrefs.GetString(LastClaimKey, "");
            DateTime.TryParse(lastRaw, out DateTime last);
            if (last.Date == DateTime.UtcNow.Date) return;

            _streakForToday = LoginStreak.Instance ? LoginStreak.Instance.RegisterLoginToday() : 1;
            float mult = LoginStreak.Instance ? LoginStreak.Instance.MultiplierFor(_streakForToday) : 1f;
            _amountForToday = (long)(baseAmount * mult);

            if (amountLabel) amountLabel.text = $"+{_amountForToday:N0} ₺";
            if (streakLabel) streakLabel.text = $"{_streakForToday}. gün";
            if (popup) popup.SetActive(true);
        }

        void Claim()
        {
            PlayerPrefs.SetString(LastClaimKey, DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(_amountForToday);
            Monetization.Analytics.Event("daily_reward_claimed", new()
            {
                { "streak", _streakForToday },
                { "amount", _amountForToday },
            });
            if (popup) popup.SetActive(false);
            ToastNotification.Show($"Günlük ödül: +{_amountForToday:N0} ₺");
        }
    }
}
