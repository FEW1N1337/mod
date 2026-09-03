using System;
using DreamCar.Customization;
using DreamCar.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Ana menü / garage araç carousel — sol/sağ ok butonlarıyla sahip olunan araçlar
    // arasında geçiş. Aktif araç CarInventory.SetActive ile set edilir; 3D preview
    // turntable dönüşü aracı tanıtır.
    public class GarageCarousel : MonoBehaviour
    {
        public Button prevButton;
        public Button nextButton;
        public Button selectButton;
        public TMP_Text nameLabel;
        public TMP_Text priceOrOwnedLabel;
        public Image thumbnail;
        public Transform previewMount;
        public float turntableDegPerSecond = 20f;

        int _index;
        GameObject _preview;

        // Önizleme örneğine takılı modifikasyon modülleri. ModShopUI buradan
        // canlı değişiklik uyguluyor — mağazada seçilen parça anında garajdaki
        // araçta görünüyor.
        //
        // Önizleme prefabında hiç MonoBehaviour yok (SavePreviewPrefab hepsini
        // atıyor), o yüzden modüller bileşen değil düz sınıf ve sahibi bu
        // bileşen.
        public CustomizationRuntime PreviewMods { get; private set; }

        // Şu an görünen aracın kimliği. Modifikasyon kaydı araç başına.
        public string CurrentCarId { get; private set; }

        // Araç değişince mağaza listesinin de yenilenmesi gerekiyor: takılı
        // parçalar araç başına saklanıyor.
        public event Action OnCarChanged;

        void Start()
        {
            if (prevButton) prevButton.onClick.AddListener(() => Cycle(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Cycle(+1));
            if (selectButton) selectButton.onClick.AddListener(Select);

            if (CarInventory.Instance != null && CarInventory.Instance.catalog != null)
                _index = CarInventory.Instance.catalog.cars.FindIndex(c => c && c.id == CarInventory.Instance.ActiveCarId);
            if (_index < 0) _index = 0;
            Refresh();
        }

        void Update()
        {
            if (_preview) _preview.transform.Rotate(Vector3.up, turntableDegPerSecond * Time.deltaTime);
        }

        public void Cycle(int dir)
        {
            var cat = CarInventory.Instance?.catalog;
            if (cat == null || cat.cars.Count == 0) return;
            _index = (_index + dir + cat.cars.Count) % cat.cars.Count;
            Refresh();
        }

        void Refresh()
        {
            var cat = CarInventory.Instance?.catalog;
            if (cat == null || _index >= cat.cars.Count) return;
            var def = cat.cars[_index];
            if (!def) return;

            CurrentCarId = def.id;

            if (nameLabel) nameLabel.text = def.displayName;
            bool owned = CarInventory.Instance.Owns(def.id);
            if (priceOrOwnedLabel) priceOrOwnedLabel.text = owned ? "Sahip" : def.price.ToString("N0") + " ₺";
            if (thumbnail) thumbnail.sprite = def.thumbnail;
            if (selectButton)
            {
                selectButton.interactable = owned;
                var t = selectButton.GetComponentInChildren<TMP_Text>();
                if (t) t.text = owned ? (CarInventory.Instance.ActiveCarId == def.id ? "Seçili" : "Seç") : "Kilitli";
            }

            if (_preview) Destroy(_preview);
            // resourcePrefabName DEĞİL: o prefab PhotonView ve Rigidbody
            // taşıyor, menüde odaya bağlı olmadan doğurmak hata üretiyor.
            // previewPrefabName yalnızca görünen hiyerarşiyi içeriyor.
            PreviewMods = null;
            if (previewMount && !string.IsNullOrEmpty(def.previewPrefabName))
            {
                var prefab = Resources.Load<GameObject>(def.previewPrefabName);
                if (prefab)
                {
                    _preview = Instantiate(prefab, previewMount.position, previewMount.rotation, previewMount);

                    // Kayıtlı modifikasyonlar HEMEN uygulanıyor. Yalnızca
                    // mağazadan seçilince uygulansaydı, oyuncu menüye her
                    // dönüşünde aracını sade görürdü — satın aldığı her şey
                    // yokmuş gibi.
                    PreviewMods = new CustomizationRuntime(_preview, CarCustomization.Catalog());
                    PreviewMods.ApplySaved(def.id);
                }
            }

            OnCarChanged?.Invoke();
        }

        void Select()
        {
            var cat = CarInventory.Instance?.catalog;
            if (cat == null || _index >= cat.cars.Count) return;
            var def = cat.cars[_index];
            if (def && CarInventory.Instance.Owns(def.id)) CarInventory.Instance.SetActive(def.id);
            Refresh();
        }
    }
}
