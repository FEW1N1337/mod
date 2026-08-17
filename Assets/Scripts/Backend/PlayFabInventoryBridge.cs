using UnityEngine;
using DreamCar.Economy;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    // Araç satın alımını PlayFab CloudScript üstünden yapar (fiyat server'da validate edilir,
    // hile ile araç alınamaz). Local CarInventory.Buy override edilir.
    public class PlayFabInventoryBridge : MonoBehaviour
    {
        public bool useServerAuthoritativePurchase = true;

        void Start()
        {
            if (PlayFabAuth.Instance != null)
                PlayFabAuth.Instance.OnLoggedIn += Pull;
        }

        public void RequestBuy(CarDefinition def, System.Action<bool> onResult)
        {
            if (!useServerAuthoritativePurchase || def == null)
            {
                onResult?.Invoke(CarInventory.Instance && CarInventory.Instance.Buy(def));
                return;
            }
#if PLAYFAB_INSTALLED
            var req = new ExecuteCloudScriptRequest
            {
                FunctionName = "buyCar",
                FunctionParameter = new { carId = def.id },
                GeneratePlayStreamEvent = true
            };
            PlayFabClientAPI.ExecuteCloudScript(req, r =>
            {
                bool ok = r.FunctionResult is Newtonsoft.Json.Linq.JObject j &&
                          j["ok"] != null && (bool)j["ok"];
                if (ok && CarInventory.Instance != null)
                {
                    CarInventory.Instance.Buy(def);
                    Pull();
                }
                onResult?.Invoke(ok);
            }, err => { Debug.LogWarning("[PlayFab] buyCar failed: " + err.ErrorMessage); onResult?.Invoke(false); });
#else
            onResult?.Invoke(CarInventory.Instance && CarInventory.Instance.Buy(def));
#endif
        }

        void Pull()
        {
#if PLAYFAB_INSTALLED
            PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), r =>
            {
                if (CarInventory.Instance == null) return;
                foreach (var item in r.Inventory)
                {
                    var def = CarInventory.Instance.catalog?.Find(item.ItemId);
                    if (def && !CarInventory.Instance.Owns(def.id))
                        CarInventory.Instance.Buy(def);
                }
            }, err => Debug.LogWarning("[PlayFab] Inventory pull failed: " + err.ErrorMessage));
#endif
        }
    }
}
