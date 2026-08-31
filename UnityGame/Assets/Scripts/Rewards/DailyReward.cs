using System;
using System.Globalization;
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
            // Kayıt UTC olarak ("o" formatı) yazılıyor, ama düz TryParse dizeyi YEREL
            // saate çevirip Kind=Local döndürüyordu. UTC'nin gerisindeki saat dilimlerinde
            // (örn. UTC-7) tarih bir gün geriye kayıyor ve aynı gün ödül ikinci kez
            // alınabiliyordu. Bu yüzden açıkça UTC'ye sabitliyoruz.
            DateTime.TryParse(lastRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime last);
            // "==" yerine ">=": cihaz saati geri alınırsa kayıt gelecekte kalıyor ve
            // eşitlik kontrolü tutmadığı için ödül tekrar tekrar alınabiliyordu.
            if (last.Date >= DateTime.UtcNow.Date) return;

            _streakForToday = LoginStreak.Instance ? LoginStreak.Instance.RegisterLoginToday() : 1;
            float mult = LoginStreak.Instance ? LoginStreak.Instance.MultiplierFor(_streakForToday) : 1f;
            _amountForToday = (long)(baseAmount * mult);

            if (amountLabel) amountLabel.text = $"+{_amountForToday:N0} ₺";
            if (streakLabel) streakLabel.text = $"{_streakForToday}. gün";
            if (popup) popup.SetActive(true);
        }

        void Claim()
        {
            // Tek kullanımlık: butona çift tıklanırsa veya Claim TryShow çalışmadan
            // (popup dışından) tetiklenirse ödül iki kez verilmesin.
            if (_amountForToday <= 0) { if (popup) popup.SetActive(false); return; }
            long amount = _amountForToday;
            _amountForToday = 0;

            PlayerPrefs.SetString(LastClaimKey, DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(amount);
            Monetization.Analytics.Event("daily_reward_claimed", new()
            {
                { "streak", _streakForToday },
                { "amount", amount },
            });
            if (popup) popup.SetActive(false);
            ToastNotification.Show($"Günlük ödül: +{amount:N0} ₺");
        }
    }
}
