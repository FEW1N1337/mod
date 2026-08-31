using DreamCar.Backend;
using DreamCar.Economy;
using DreamCar.UI;
using UnityEngine;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Social
{
    // İlk açılışta 8 karakterli unique kod üretilir. Başka oyuncu bu kodu girerse
    // CloudScript "redeemReferral" iki tarafa da bonus ekler.
    public class ReferralSystem : MonoBehaviour
    {
        public static ReferralSystem Instance { get; private set; }
        public long referrerBonus = 5000;
        public long refereeBonus = 3000;

        const string MyCodeKey = "referral.myCode";
        const string RedeemedKey = "referral.redeemed";

        public string MyCode { get; private set; }
        public bool HasRedeemed { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void Load()
        {
            MyCode = PlayerPrefs.GetString(MyCodeKey, "");
            if (string.IsNullOrEmpty(MyCode))
            {
                MyCode = GenerateCode();
                PlayerPrefs.SetString(MyCodeKey, MyCode);
                PlayerPrefs.Save();
            }
            HasRedeemed = PlayerPrefs.GetInt(RedeemedKey, 0) == 1;
        }

        static string GenerateCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new System.Text.StringBuilder(8);
            for (int i = 0; i < 8; i++) sb.Append(chars[Random.Range(0, chars.Length)]);
            return sb.ToString();
        }

        public void Redeem(string code)
        {
            if (HasRedeemed) { ToastNotification.Show("Referral kodu zaten kullanıldı"); return; }
            if (string.IsNullOrWhiteSpace(code) || code.Trim().ToUpperInvariant() == MyCode)
            { ToastNotification.Show("Geçersiz kod"); return; }

#if PLAYFAB_INSTALLED
            var req = new ExecuteCloudScriptRequest
            {
                FunctionName = "redeemReferral",
                FunctionParameter = new { code = code.Trim().ToUpperInvariant() }
            };
            PlayFabClientAPI.ExecuteCloudScript(req, r =>
            {
                bool ok = r.FunctionResult is Newtonsoft.Json.Linq.JObject j && j["ok"] != null && (bool)j["ok"];
                if (ok)
                {
                    HasRedeemed = true;
                    PlayerPrefs.SetInt(RedeemedKey, 1);
                    PlayerPrefs.Save();
                    if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(refereeBonus);
                    Monetization.Analytics.Event("referral_redeemed", new() { { "bonus", refereeBonus } });
                    ToastNotification.Show($"Kod kabul edildi! +{refereeBonus:N0} ₺");
                }
                else ToastNotification.Show("Kod geçersiz veya süresi dolmuş");
            }, err => ToastNotification.Show("Referral hatası: " + err.ErrorMessage));
#else
            HasRedeemed = true;
            PlayerPrefs.SetInt(RedeemedKey, 1);
            PlayerPrefs.Save();
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(refereeBonus);
            ToastNotification.Show($"(Offline) Kod kabul edildi: +{refereeBonus:N0} ₺");
#endif
        }
    }
}
