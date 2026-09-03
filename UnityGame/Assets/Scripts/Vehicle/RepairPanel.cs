using DreamCar.Economy;
using DreamCar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Vehicle
{
    // HUD tamir paneli. Hasar bar + "Tamir Et" butonu. Fiyat = hasar % × birim fiyat.
    public class RepairPanel : MonoBehaviour
    {
        public CarDamage damage;
        public Image healthFill;
        public TMP_Text priceLabel;
        public Button repairButton;
        public float baseUnitPrice = 10f;

        GameObject _car;

        void Start()
        {
            if (repairButton) repairButton.onClick.AddListener(Repair);
            Refresh();
        }

        // "damage" Editor'de bağlanamıyor: CarDamage araç prefabında ve araç
        // ancak odaya girilince doğuyor. Eskiden Start'ta null olduğu için
        // OnDamaged aboneliği hiç kurulmuyordu ve Refresh ilk satırda
        // dönüyordu — tamir paneli hiçbir sahnede zaten yoktu, olsaydı da boş
        // kalırdı. Hasarın oyunda tamir edilecek bir yolu yoktu.
        void Update()
        {
            var car = Network.RoomManager.LocalCar;
            if (car == _car) return;

            if (damage) damage.OnDamaged -= OnDamaged;
            _car = car;
            damage = car ? car.GetComponent<CarDamage>() : null;
            if (damage) damage.OnDamaged += OnDamaged;

            Refresh();
        }

        void OnDestroy() { if (damage) damage.OnDamaged -= OnDamaged; }

        // Lambda yerine adlandırılmış metot: "-=" ile abonelikten çıkmak için
        // aynı delege örneğine ihtiyaç var, lambda her seferinde yenisini üretir.
        void OnDamaged(float _) => Refresh();

        void Refresh()
        {
            if (!damage) return;
            float ratio = damage.health / damage.maxHealth;
            if (healthFill) healthFill.fillAmount = ratio;

            long price = ComputePrice();
            if (priceLabel) priceLabel.text = price > 0 ? $"{price:N0} ₺" : "-";
            if (repairButton) repairButton.interactable = price > 0 && PlayerMoney.Instance && PlayerMoney.Instance.Money >= price;
        }

        long ComputePrice() =>
            damage ? Util.GameMath.RepairPrice(damage.health, damage.maxHealth, baseUnitPrice) : 0;

        void Repair()
        {
            long price = ComputePrice();
            if (price <= 0) return;
            if (PlayerMoney.Instance == null || !PlayerMoney.Instance.TrySpend(price))
            {
                ToastNotification.Show("Yetersiz para");
                return;
            }
            damage.Repair();
            ToastNotification.Show($"Tamir edildi (-{price:N0} ₺)");
            Refresh();
        }
    }
}
