using DreamCar.Economy;
using TMPro;
using UnityEngine;

namespace DreamCar.UI
{
    // Bakiyeyi gösteren küçük bir etiket sürücüsü.
    //
    // PlayerMoney.OnMoneyChanged'in ana menüde HİÇ abonesi yoktu: para
    // yalnızca mağaza panelleri açıkken (ShopUI/ModShopUI.moneyLabel) ve oyun
    // içi HUD'da görünüyordu. Dream Road'da bakiye her ekranda üstte duruyor;
    // MakeMoneyPill'in ürettiği hapı bu bileşen besliyor.
    public class MoneyDisplay : MonoBehaviour
    {
        public TMP_Text label;

        // long.MinValue = "henüz hiç yazılmadı". 0 kullanılamaz: parası
        // gerçekten 0 olan oyuncuda etiket boş kalırdı.
        long _shown = long.MinValue;
        bool _subscribed;

        void OnEnable() { TryBind(); }

        void OnDisable()
        {
            if (_subscribed && PlayerMoney.Instance)
                PlayerMoney.Instance.OnMoneyChanged -= OnMoneyChanged;
            _subscribed = false;
        }

        // PlayerMoney ~Bootstrap'te; Awake sırası garanti değil ve sahne
        // geçişinde tekil yeniden bağlanabiliyor. Abone olamadığımız sürece
        // her karede deniyoruz — bağlandıktan sonra Update hiçbir şey yapmıyor.
        void Update()
        {
            if (!_subscribed) TryBind();
        }

        void TryBind()
        {
            var money = PlayerMoney.Instance;
            if (!money) { Show(0L); return; }

            if (!_subscribed)
            {
                money.OnMoneyChanged += OnMoneyChanged;
                _subscribed = true;
            }
            Show(money.Money);
        }

        void OnMoneyChanged(long money) => Show(money);

        // Değer değişmedikçe string üretmiyor: bu bileşen Update'te dönüyor.
        void Show(long money)
        {
            if (!label || money == _shown) return;
            _shown = money;
            label.text = $"{money:N0} ₺";
        }
    }
}
