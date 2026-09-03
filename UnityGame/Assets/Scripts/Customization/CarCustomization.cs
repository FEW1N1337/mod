using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization
{
    // Araç modifikasyonlarının araç üzerindeki tek bileşeni.
    //
    // Modüllerin kendisi bileşen değil (bkz. CustomizationRuntime); bu sınıf
    // yalnızca üç işi yapıyor: doğru yapılandırmayı seçmek, kaydetmek ve
    // diğer oyunculara bildirmek.
    public class CarCustomization : MonoBehaviourPun
    {
        // Photon Custom Property anahtarı. CarPaint'in "car.color" deseniyle
        // aynı: sahip yazıyor, diğerleri okuyor.
        public const string NetworkKey = "car.mods";

        // Katalog Resources'tan yükleniyor, sahneden BAĞLANMIYOR.
        // Sebebi: araç prefabı PhotonNetwork.Instantiate ile çalışma anında
        // doğuyor ve kimse alanlarını doldurmuyor. Sahneye bağlı bir referans
        // bu prefabda daima null kalırdı — bu projenin baskın hata ailesi.
        const string CatalogResourceName = "ModCatalog";

        static ModCatalog _catalog;

        CustomizationRuntime _runtime;
        string _carId;

        public CustomizationRuntime Runtime => _runtime;

        void Awake()
        {
            _runtime = new CustomizationRuntime(gameObject, Catalog());

            // CarPaint'i devre dışı bırakmıyoruz, yalnızca KENDİ yüklemesini
            // yapmasını engelliyoruz. O bileşen global bir anahtardan
            // (car.color) renk yüklüyor, biz araç başına kayıttan; ikisi de
            // Start'ta koşsaydı kazanan bileşen sırasına kalırdı.
            // Awake, her Start'tan önce koştuğu için bayrak zamanında set oluyor.
            var paint = GetComponent<CarPaint>();
            if (paint) paint.externallyManaged = true;
        }

        void Start()
        {
            if (photonView == null || photonView.IsMine)
            {
                _carId = CarInventory.Instance ? CarInventory.Instance.ActiveCarId : null;
                _runtime.ApplySaved(_carId);
                PushToNetwork();
            }
            else
            {
                // Uzak araç: sahibin yazdığı dizeyi okuyup YALNIZCA görünen
                // modülleri uyguluyoruz.
                _runtime.ApplyAll(ReadFromOwner(), visualOnly: true);
            }
        }

        // --------------------------------------------------------------- Menü API

        public ItemId Equipped(string slot) => _runtime.Equipped(slot);

        // Parçayı tak (veya id boşsa çıkar). Kayıt ve ağ bildirimi burada.
        public void Equip(string slot, ItemId id)
        {
            if (photonView != null && !photonView.IsMine) return;

            _runtime.Equip(slot, id);
            ModSave.SetEquipped(_carId, slot, id);
            PushToNetwork();
        }

        // Satın alma. Zaten sahipse para düşmüyor — çift ödeme, mağaza
        // sistemlerinin klasik hatası.
        public static bool Buy(ModItem item)
        {
            if (item == null) return false;
            var id = new ItemId(item.id);
            if (ModSave.Owns(id)) return false;
            if (PlayerMoney.Instance == null) return false;
            if (!PlayerMoney.Instance.TrySpend(item.price)) return false;

            ModSave.AddOwned(id);
            return true;
        }

        public static bool Owns(ModItem item) =>
            item != null && ModSave.Owns(new ItemId(item.id));

        // -------------------------------------------------------------- Ağ

        void PushToNetwork()
        {
            if (photonView == null || !photonView.IsMine) return;
            if (photonView.Owner == null) return;

            var packed = ModSave.Serialize(_carId, _runtime.Slots);
            photonView.Owner.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { NetworkKey, packed },
            });
        }

        Dictionary<string, ItemId> ReadFromOwner()
        {
            if (photonView == null || photonView.Owner == null) return null;
            var props = photonView.Owner.CustomProperties;
            if (props == null) return null;
            if (!props.TryGetValue(NetworkKey, out object raw)) return null;

            // "is not string" yerine as/null: daha yeni dil özelliklerine
            // gerek yok ve derleyici sürümünden bağımsız çalışıyor.
            var packed = raw as string;
            if (string.IsNullOrEmpty(packed)) return null;
            return ModSave.Deserialize(packed);
        }

        // -------------------------------------------------------------- Katalog

        public static ModCatalog Catalog()
        {
            // Yalnızca BAŞARI önbelleğe alınıyor. Başarısızlık da önbelleğe
            // alınsaydı, katalog üretilmeden önce doğan bir araç bütün oturum
            // boyunca kataloğu bulamaz hâle gelirdi.
            if (_catalog != null) return _catalog;

            _catalog = Resources.Load<ModCatalog>(CatalogResourceName);
            if (_catalog == null)
                Debug.LogWarning(
                    $"[Mod] Resources/{CatalogResourceName} bulunamadı — modifikasyon " +
                    "mağazası boş görünür. DreamCar → BUILD EVERYTHING çalıştırıldı mı?");
            return _catalog;
        }
    }
}
