using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DreamCar.Customization;
using DreamCar.Economy;

namespace DreamCar.UI
{
    // Modifikasyon ekranı. Solda slot sekmeleri, sağda o slottaki parçalar.
    //
    // Seçim GARAJDAKİ CANLI ÖNİZLEMEYE anında uygulanıyor — mağazada seçip
    // "acaba nasıl duruyor" diye oyuna girmek zorunda kalmak, modifikasyon
    // ekranını işe yaramaz hâle getirirdi.
    //
    // Menüde gerçek araç yok (o prefab PhotonView ve Rigidbody taşıyor).
    // Bu yüzden takma iki adımdan oluşuyor: kayda yazılıyor (ModSave) ve
    // önizleme örneğine uygulanıyor (GarageCarousel.PreviewMods). Oyuna
    // girildiğinde CarCustomization aynı kaydı okuyup gerçek araca uyguluyor.
    public class ModShopUI : MonoBehaviour
    {
        public GameObject panel;
        public GarageCarousel garage;

        public Transform slotTabParent;
        public GameObject slotTabPrefab;
        public Transform itemListParent;
        public GameObject itemRowPrefab;

        public TMP_Text moneyLabel;
        public TMP_Text slotTitleLabel;
        public Button closeButton;

        // Slot anahtarları katalogdan geliyor; gösterilecek adları burada.
        // Katalogda olup burada olmayan bir slot anahtarın kendisiyle
        // gösteriliyor — sessizce kaybolmasından iyidir.
        static readonly Dictionary<string, string> SlotNames = new()
        {
            { "paint",      "Boya" },
            { "tint",       "Cam Filmi" },
            { "rim",         "Jant" },
            { "spoiler",    "Spoiler" },
            { "neon",       "Neon" },
            { "engine",     "Motor" },
            { "turbo",      "Turbo" },
            { "exhaust",    "Egzoz" },
            { "tire",       "Lastik" },
            { "brake",      "Fren" },
            { "suspension", "Süspansiyon" },
        };

        string _slot;

        // Nav ikonu açıkken tekrar tıklanabiliyor. Koruma olmasa olay
        // aboneliği ikinci kez eklenir ve Refresh her para değişiminde iki kez
        // koşardı — liste iki kez silinip kurulur, tıklama yanlış satıra gider.
        bool _open;

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (panel) panel.SetActive(false);
        }

        public void Open()
        {
            if (_open) return;
            _open = true;

            if (panel) panel.SetActive(true);

            if (PlayerMoney.Instance != null)
            {
                PlayerMoney.Instance.OnMoneyChanged += OnMoneyChanged;
                OnMoneyChanged(PlayerMoney.Instance.Money);
            }

            // Garajda araç değişince liste de değişmeli: takılı parçalar araç
            // başına saklanıyor, aynı listede farklı işaretlenmeleri gerekiyor.
            if (garage) garage.OnCarChanged += Refresh;

            BuildTabs();
            Refresh();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            if (PlayerMoney.Instance != null) PlayerMoney.Instance.OnMoneyChanged -= OnMoneyChanged;
            if (garage) garage.OnCarChanged -= Refresh;
            if (panel) panel.SetActive(false);
        }

        void OnDisable() => Close();

        void BuildTabs()
        {
            if (!slotTabParent || !slotTabPrefab) return;
            var catalog = CarCustomization.Catalog();
            if (catalog == null) return;

            for (int i = slotTabParent.childCount - 1; i >= 0; i--)
                Destroy(slotTabParent.GetChild(i).gameObject);

            foreach (var slot in catalog.Slots())
            {
                var go = Instantiate(slotTabPrefab, slotTabParent);
                // Şablon sahnede KAPALI duruyor; klon da kapalı doğar ve kapalı
                // objede GetComponentsInChildren (includeInactive olmadan) boş
                // döner. ShopUI'de aynı hata satırları hem görünmez hem metinsiz
                // bırakıyordu.
                go.SetActive(true);

                var label = go.GetComponentInChildren<TMP_Text>();
                if (label) label.text = SlotLabel(slot);

                var button = go.GetComponentInChildren<Button>();
                if (button)
                {
                    var captured = slot;
                    button.onClick.AddListener(() => SelectSlot(captured));
                }

                if (string.IsNullOrEmpty(_slot)) _slot = slot;
            }
        }

        void SelectSlot(string slot)
        {
            _slot = slot;
            Refresh();
        }

        void Refresh()
        {
            if (!itemListParent || !itemRowPrefab) return;

            var catalog = CarCustomization.Catalog();
            if (catalog == null) return;

            if (slotTitleLabel) slotTitleLabel.text = SlotLabel(_slot);

            for (int i = itemListParent.childCount - 1; i >= 0; i--)
                Destroy(itemListParent.GetChild(i).gameObject);

            string carId = garage ? garage.CurrentCarId : null;
            var equipped = ModSave.Equipped(carId, _slot);

            foreach (var item in catalog.InSlot(_slot))
            {
                if (item == null) continue;

                var go = Instantiate(itemRowPrefab, itemListParent);
                go.SetActive(true);

                var texts = go.GetComponentsInChildren<TMP_Text>();
                bool owned = CarCustomization.Owns(item) || item.price <= 0;
                bool isEquipped = equipped.Value == item.id;

                if (texts.Length > 0) texts[0].text = item.displayName;
                if (texts.Length > 1) texts[1].text = item.effectSummary;
                if (texts.Length > 2)
                    texts[2].text = owned ? (isEquipped ? "Takılı" : "Sahip")
                                          : item.price.ToString("N0") + " ₺";

                // Renk kullanan slotlarda satır ikonunu ürünün rengine
                // boyuyoruz: on farklı boyayı adından ayırt etmek zor,
                // renkten anında belli oluyor.
                var iconTr = go.transform.Find("Icon");
                var icon = iconTr ? iconTr.GetComponent<Image>() : null;
                if (icon && UsesColor(item.slot)) icon.color = item.color;

                var button = go.GetComponentInChildren<Button>();
                if (!button) continue;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label) label.text = !owned ? "Satın Al" : (isEquipped ? "Çıkar" : "Tak");

                var captured = item;
                bool capturedOwned = owned;
                bool capturedEquipped = isEquipped;
                button.onClick.AddListener(() => OnRowClicked(captured, capturedOwned, capturedEquipped));
            }
        }

        void OnRowClicked(ModItem item, bool owned, bool isEquipped)
        {
            if (!owned)
            {
                // Satın alma başarısızsa (para yetmedi) hiçbir şey takılmıyor.
                // Eskiden ShopUI'de olduğu gibi sessiz kalmıyoruz: liste
                // yenileniyor, bakiye güncel görünüyor.
                if (!CarCustomization.Buy(item)) { Refresh(); return; }
                Equip(item.slot, new ItemId(item.id));
                return;
            }

            Equip(item.slot, isEquipped ? ItemId.None : new ItemId(item.id));
        }

        void Equip(string slot, ItemId id)
        {
            string carId = garage ? garage.CurrentCarId : null;

            // İki hedef: kalıcı kayıt ve önizleme. Yalnızca kayda yazsaydık
            // garajdaki araç değişmezdi; yalnızca önizlemeye uygulasaydık
            // oyuna girince modifikasyon kaybolurdu.
            ModSave.SetEquipped(carId, slot, id);
            garage?.PreviewMods?.Equip(slot, id);

            Refresh();
        }

        void OnMoneyChanged(long money)
        {
            if (moneyLabel) moneyLabel.text = money.ToString("N0") + " ₺";
        }

        static string SlotLabel(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return "-";
            return SlotNames.TryGetValue(slot, out var name) ? name : slot;
        }

        static bool UsesColor(string slot) =>
            slot == "paint" || slot == "rim" || slot == "neon";
    }
}
