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

        // Ödül miktarları kodda sabitti; RemoteConfig okunmuyordu.
        long RefereeBonus => Backend.RemoteConfig.GetLong("referral.refereeBonus", refereeBonus);

        const string MyCodeKey = "referral.myCode";
        const string RedeemedKey = "referral.redeemed";

        // PlayerPrefs'ten HER OKUMADA taze değer. Eskiden Awake'te bir kez kopyalanıp
        // alanda tutuluyordu; PlayFabCloudSave.ApplyProfile bu anahtarları sonradan
        // yazınca bellekteki kopya bayatlıyor ve referral bonusu ikinci kez
        // ödenebiliyordu (çevrimdışı dal koşulsuz ödüyor).
        public string MyCode
        {
            get => PlayerPrefs.GetString(MyCodeKey, "");
            private set { PlayerPrefs.SetString(MyCodeKey, value); PlayerPrefs.Save(); }
        }

        public bool HasRedeemed
        {
            get => PlayerPrefs.GetInt(RedeemedKey, 0) == 1;
            private set { PlayerPrefs.SetInt(RedeemedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void Load()
        {
            // Kod yalnızca hiç yoksa üretilir; property zaten PlayerPrefs'e yazıyor.
            if (string.IsNullOrEmpty(MyCode)) MyCode = GenerateCode();
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
                    HasRedeemed = true;   // property PlayerPrefs'e de yazıyor
                    PlayerPrefs.Save();
                    if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(RefereeBonus);
                    Monetization.Analytics.Event("referral_redeemed", new() { { "bonus", RefereeBonus } });
                    ToastNotification.Show($"Kod kabul edildi! +{RefereeBonus:N0} ₺");
                }
                else ToastNotification.Show("Kod geçersiz veya süresi dolmuş");
            }, err => ToastNotification.Show("Referral hatası: " + err.ErrorMessage));
#else
            HasRedeemed = true;   // property PlayerPrefs'e de yazıyor
            if (PlayerMoney.Instance != null) PlayerMoney.Instance.Add(RefereeBonus);
            ToastNotification.Show($"(Offline) Kod kabul edildi: +{RefereeBonus:N0} ₺");
#endif
        }
    }
}
