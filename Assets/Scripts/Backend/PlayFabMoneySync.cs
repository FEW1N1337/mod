using System.Collections.Generic;
using UnityEngine;
using DreamCar.Economy;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    // PlayerMoney değişimlerini PlayFab UserData'ya yansıtır. Login sonrası
    // server değerini local'e geri yükler.
    public class PlayFabMoneySync : MonoBehaviour
    {
        const string Key = "money";
        float _saveTimer;
        bool _pending;

        void Start()
        {
            if (PlayFabAuth.Instance != null)
                PlayFabAuth.Instance.OnLoggedIn += Pull;
            if (PlayerMoney.Instance != null)
                PlayerMoney.Instance.OnMoneyChanged += _ => _pending = true;
        }

        void Update()
        {
            if (!_pending) return;
            _saveTimer += Time.deltaTime;
            if (_saveTimer > 2f) { Push(); _saveTimer = 0f; _pending = false; }
        }

        void Pull()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(), r =>
            {
                if (r.Data == null || !r.Data.ContainsKey(Key)) return;
                if (long.TryParse(r.Data[Key].Value, out long m) && PlayerMoney.Instance != null)
                {
                    long diff = m - PlayerMoney.Instance.Money;
                    if (diff > 0) PlayerMoney.Instance.Add(diff);
                    else if (diff < 0) PlayerMoney.Instance.TrySpend(-diff);
                }
            }, err => Debug.LogWarning("[PlayFab] GetUserData failed: " + err.ErrorMessage));
#endif
        }

        void Push()
        {
#if PLAYFAB_INSTALLED
            if (PlayerMoney.Instance == null) return;
            var req = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { Key, PlayerMoney.Instance.Money.ToString() } }
            };
            PlayFabClientAPI.UpdateUserData(req, null,
                err => Debug.LogWarning("[PlayFab] UpdateUserData failed: " + err.ErrorMessage));
#endif
        }
    }
}
