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

        void Start()
        {
            if (repairButton) repairButton.onClick.AddListener(Repair);
            if (damage != null) damage.OnDamaged += _ => Refresh();
            Refresh();
        }

        void Refresh()
        {
            if (!damage) return;
            float ratio = damage.health / damage.maxHealth;
            if (healthFill) healthFill.fillAmount = ratio;

            long price = ComputePrice();
            if (priceLabel) priceLabel.text = price > 0 ? $"{price:N0} ₺" : "-";
            if (repairButton) repairButton.interactable = price > 0 && PlayerMoney.Instance && PlayerMoney.Instance.Money >= price;
        }

        long ComputePrice()
        {
            if (!damage) return 0;
            float missing = 1f - (damage.health / damage.maxHealth);
            return (long)Mathf.Ceil(missing * 100f * baseUnitPrice);
        }

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
