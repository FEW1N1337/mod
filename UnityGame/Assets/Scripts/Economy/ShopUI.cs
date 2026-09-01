using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Economy
{
    public class ShopUI : MonoBehaviour
    {
        public CarCatalog catalog;
        public Transform listParent;
        public GameObject entryPrefab;
        public TMP_Text moneyLabel;

        void OnEnable() { Refresh(); if (PlayerMoney.Instance != null) PlayerMoney.Instance.OnMoneyChanged += UpdateMoney; }
        void OnDisable() { if (PlayerMoney.Instance != null) PlayerMoney.Instance.OnMoneyChanged -= UpdateMoney; }

        void Refresh()
        {
            if (!listParent || !entryPrefab || !catalog) return;

            for (int i = listParent.childCount - 1; i >= 0; i--) Destroy(listParent.GetChild(i).gameObject);
            UpdateMoney(PlayerMoney.Instance ? PlayerMoney.Instance.Money : 0);

            foreach (var def in catalog.cars)
            {
                if (!def) continue;
                var go = Instantiate(entryPrefab, listParent);
                // entryPrefab sahnede kapalı duran bir şablon; klon da kapalı doğar ve
                // kapalı objede GetComponentsInChildren (includeInactive olmadan) boş
                // döner — satırlar hem görünmez hem metinsiz kalırdı.
                go.SetActive(true);
                var texts = go.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = def.displayName;
                if (texts.Length > 1) texts[1].text = def.price.ToString("N0") + " ₺";

                var img = go.GetComponentInChildren<Image>();
                if (img && def.thumbnail) img.sprite = def.thumbnail;

                var btn = go.GetComponentInChildren<Button>();
                if (btn)
                {
                    bool owned = CarInventory.Instance && CarInventory.Instance.Owns(def.id);
                    btn.interactable = !owned;
                    // Butonda etiket yoksa korumasız erişim NullReferenceException atıp
                    // döngüyü kesiyordu: o araçtan sonrakiler hiç listelenmezdi.
                    var btnLabel = btn.GetComponentInChildren<TMP_Text>();
                    if (btnLabel) btnLabel.text = owned ? "Sahip" : "Satın Al";
                    var d = def;
                    btn.onClick.AddListener(() =>
                    {
                        if (CarInventory.Instance && CarInventory.Instance.Buy(d)) Refresh();
                    });
                }
            }
        }

        void UpdateMoney(long m) { if (moneyLabel) moneyLabel.text = m.ToString("N0") + " ₺"; }
    }
}
