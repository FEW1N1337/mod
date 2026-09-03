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
#if PLAYFAB_INSTALLED
        bool _pulled;
#endif
        System.Action<long> _moneyHandler;

        void Start()
        {
            if (PlayFabAuth.Instance != null)
            {
                PlayFabAuth.Instance.OnLoggedIn += Pull;
                // Login bu obje sahneye gelmeden bittiyse event bir daha gelmez;
                // Pull hiç çalışmaz ve sunucudaki bakiye hiç okunmazdı.
                if (PlayFabAuth.Instance.IsLoggedIn) Pull();
            }
            if (PlayerMoney.Instance != null)
            {
                _moneyHandler = _ => _pending = true;
                PlayerMoney.Instance.OnMoneyChanged += _moneyHandler;
            }
        }

        void OnDestroy()
        {
            // Abonelik hiç bırakılmıyordu: sahne değişiminde yok olan bu bileşen
            // PlayerMoney'nin (DontDestroyOnLoad) event listesinde kalıyordu.
            if (PlayFabAuth.Instance != null) PlayFabAuth.Instance.OnLoggedIn -= Pull;
            if (PlayerMoney.Instance != null && _moneyHandler != null)
                PlayerMoney.Instance.OnMoneyChanged -= _moneyHandler;
        }

        void Update()
        {
            if (!_pending) return;
            _saveTimer += Time.deltaTime;
            if (_saveTimer <= 2f) return;
            // Login/Pull tamamlanmadan yazarsak sunucudaki bakiye yeni cihazın sıfır
            // bakiyesiyle ezilir, istek de "not logged in" hatasıyla düşerdi.
            // _pending korunur, koşul sağlanınca aynı değişiklik tekrar denenir.
            if (!CanPush()) return;
            Push(); _saveTimer = 0f; _pending = false;
        }

        bool CanPush()
        {
#if PLAYFAB_INSTALLED
            return _pulled && (PlayFabAuth.Instance == null || PlayFabAuth.Instance.IsLoggedIn);
#else
            return false; // SDK yok — yazacak yer de yok
#endif
        }

        void Pull()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(), r =>
            {
                _pulled = true;
                if (r.Data == null || !r.Data.ContainsKey(Key)) return;
                if (long.TryParse(r.Data[Key].Value, out long m) && PlayerMoney.Instance != null)
                {
                    long diff = m - PlayerMoney.Instance.Money;
                    if (diff > 0) PlayerMoney.Instance.Add(diff);
                    else if (diff < 0) PlayerMoney.Instance.TrySpend(-diff);
                }
            }, err =>
            {
                // Okuma başarısız olsa da yazmayı tamamen kilitlemiyoruz; aksi halde
                // ağ bir kez hata verdiğinde para hiç senkronlanmazdı.
                _pulled = true;
                Debug.LogWarning("[PlayFab] GetUserData failed: " + err.ErrorMessage);
            });
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
