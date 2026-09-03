// Unity IAP scaffold. Aktive etmek için: Window → Package Manager → In App Purchasing paketini import et,
// sonra Project Settings → Services → In-App Purchasing → On. Otomatik olarak
// UNITY_PURCHASING define eklenir.
using UnityEngine;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

using DreamCar.Economy;

namespace DreamCar.Monetization
{
#if UNITY_PURCHASING
    public class IAPManager : MonoBehaviour, IDetailedStoreListener
    {
        public static IAPManager Instance { get; private set; }

        public string coinsSmallId = "com.few1n.dreamcar.coins.small";   // ör: 50k para
        public string coinsMediumId = "com.few1n.dreamcar.coins.medium"; // ör: 200k
        public string coinsLargeId = "com.few1n.dreamcar.coins.large";   // ör: 1M
        public string vipMonthlyId = "com.few1n.dreamcar.vip.monthly";   // subscription

        IStoreController _store;
        IExtensionProvider _ext;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitIAP();
        }

        void InitIAP()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(coinsSmallId, ProductType.Consumable);
            builder.AddProduct(coinsMediumId, ProductType.Consumable);
            builder.AddProduct(coinsLargeId, ProductType.Consumable);
            builder.AddProduct(vipMonthlyId, ProductType.Subscription);
            UnityPurchasing.Initialize(this, builder);
        }

        public void Buy(string productId) { if (_store != null) _store.InitiatePurchase(productId); }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _store = controller; _ext = extensions;
            Debug.Log("[IAP] Ready.");
        }

        public void OnInitializeFailed(InitializationFailureReason error) => Debug.LogError($"[IAP] Init failed: {error}");
        public void OnInitializeFailed(InitializationFailureReason error, string message) => Debug.LogError($"[IAP] Init failed: {error} {message}");

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            if (e.purchasedProduct.definition.id == coinsSmallId) PlayerMoney.Instance?.Add(50000);
            else if (e.purchasedProduct.definition.id == coinsMediumId) PlayerMoney.Instance?.Add(200000);
            else if (e.purchasedProduct.definition.id == coinsLargeId) PlayerMoney.Instance?.Add(1000000);
            else if (e.purchasedProduct.definition.id == vipMonthlyId) PlayerPrefs.SetInt("vip.active", 1);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason) => Debug.LogError($"[IAP] Failed: {failureReason}");
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription) => Debug.LogError($"[IAP] Failed: {failureDescription}");
    }
#else
    public class IAPManager : MonoBehaviour
    {
        public static IAPManager Instance { get; private set; }
        void Awake() { Instance = this; }
        public void Buy(string productId) => Debug.Log("[IAP] Package not installed. Add com.unity.purchasing.");
    }
#endif
}
