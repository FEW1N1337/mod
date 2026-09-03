using UnityEngine;
using DreamCar.Car;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Bütün modifikasyon modüllerinin ortak iskeleti.
    //
    // NEDEN MONOBEHAVIOUR DEĞİL: garaj önizlemesi Preview_<id>.prefab kullanıyor
    // ve o prefab BÜTÜN MonoBehaviour'ları atıyor (ProceduralCarGenerator.
    // SavePreviewPrefab). Modüller bileşen olsaydı menüde hiç çalışmazlardı —
    // oyuncu cam filmi satın alır, garajda hiçbir şey değişmezdi. Düz sınıf
    // olunca aynı kod hem gerçek araçta hem önizleme prefabında koşuyor;
    // araca erişim VehicleContext üzerinden.
    public abstract class ModModuleBase : ICustomizationModule
    {
        public abstract string Slot { get; }
        public ItemId Current { get; private set; }

        // Yönetici veriyor. Modül id → ModItem çözümlemesini kendisi yapıyor
        // çünkü ICustomizationModule.Apply yalnızca kimlik alıyor.
        public ModCatalog Catalog { get; set; }

        // Uzak (başka oyuncuya ait) araçta uygulanmalı mı? Görsel modüller evet.
        // İstatistik modülleri hayır: uzak araçta fizik simüle edilmiyor
        // (IVehicleAuthority.SimulatesPhysics), tork değiştirmenin karşılığı yok.
        public virtual bool AffectsRemoteVisuals => false;

        public void Apply(VehicleContext vehicle, ItemId id)
        {
            if (vehicle == null || vehicle.Root == null) return;
            if (Current == id) return;

            // Çağıran tarafın iki kez Remove çağırması beklenmiyor; slotu
            // boşaltmak modülün kendi sorumluluğu (arayüz sözleşmesi böyle).
            Remove(vehicle);

            if (id.IsNone) return;

            var item = Catalog != null ? Catalog.Find(id.Value) : null;
            if (item == null)
            {
                Debug.LogWarning($"[Mod] '{id}' katalogda yok — '{Slot}' slotu boş kaldı.");
                return;
            }

            if (item.slot != Slot)
            {
                Debug.LogWarning($"[Mod] '{item.id}' ürününün slotu '{item.slot}', " +
                                 $"ama '{Slot}' modülüne verildi — uygulanmadı.");
                return;
            }

            Current = id;
            ApplyStats(vehicle, item);
            OnApply(vehicle, item);
        }

        public void Remove(VehicleContext vehicle)
        {
            if (Current.IsNone) return;
            if (vehicle != null && vehicle.Root != null)
            {
                OnRemove(vehicle);
                // İstatistik temizliği görsel geri almadan SONRA ve her zaman:
                // parça çıkarıldığında istatistiğin tabloda kalması modifikasyon
                // sistemlerinin en sık hatası. VehicleStatSheet kaynak adına göre
                // sildiği için yalnızca bu slotun katkısı kalkıyor.
                vehicle.StatSheet?.Clear(Slot);
            }
            Current = ItemId.None;
        }

        protected virtual void OnApply(VehicleContext vehicle, ModItem item) { }
        protected virtual void OnRemove(VehicleContext vehicle) { }

        void ApplyStats(VehicleContext vehicle, ModItem item)
        {
            if (vehicle.StatSheet == null) return;   // önizleme aracı: tablo yok

            // Etkisiz değiştirici yazmıyoruz: tabloyu kalabalıklaştırır ve
            // Evaluate'in yeniden hesaplamasını boşuna tetikler.
            if (!IsNeutral(item.statAAdd, item.statAMul))
                vehicle.StatSheet.Set(Slot, item.statA, item.statAAdd, item.statAMul);

            if (item.useStatB && !IsNeutral(item.statBAdd, item.statBMul))
                vehicle.StatSheet.Set(Slot, item.statB, item.statBAdd, item.statBMul);
        }

        static bool IsNeutral(float add, float mul) =>
            Mathf.Approximately(add, 0f) && Mathf.Approximately(mul, 1f);

        // ------------------------------------------------------------ Yardımcılar

        // Ada göre çocuk arar (derin). Araç hiyerarşisi düz değil: jantlar
        // FL_Mesh'in altında, spoiler kökün altında.
        protected static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // MaterialPropertyBlock'u OKUYUP değiştirip geri yazıyoruz, sıfırdan
        // kurmuyoruz. Aynı renderer'a başka bir sistem de yazıyor olabilir —
        // jantta WheelGlow her karede emissive basıyor. Blok komple
        // değiştirilseydi ikisi birbirini silerdi.
        protected static void SetBlockColor(Renderer renderer, MaterialPropertyBlock block,
                                            int propertyId, Color value)
        {
            if (!renderer) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(propertyId, value);
            renderer.SetPropertyBlock(block);
        }

        protected static void SetBlockFloat(Renderer renderer, MaterialPropertyBlock block,
                                            int propertyId, float value)
        {
            if (!renderer) return;
            renderer.GetPropertyBlock(block);
            block.SetFloat(propertyId, value);
            renderer.SetPropertyBlock(block);
        }
    }
}
