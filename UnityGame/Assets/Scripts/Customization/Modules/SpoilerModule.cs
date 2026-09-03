using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Spoiler. Geometri ÇALIŞMA ANINDA ÜRETİLMİYOR: ProceduralCarGenerator her
    // varyantı araç prefabına KAPALI çocuk olarak koyuyor, modül yalnızca
    // SetActive yapıyor.
    //
    // Neden böyle: çalışma anında mesh üretmek her araç doğuşunda tahsisat
    // demek ve ağdan gelen araçlarda da tekrarlanırdı. Kapalı çocuk yaklaşımı
    // ayrıca garaj önizlemesinde de çalışıyor — SavePreviewPrefab bileşenleri
    // siliyor ama GameObject'leri değil.
    //
    // Spoiler görsel olmanın yanında gerçek bir etki de veriyor: ModItem'ın
    // Downforce istatistiği. O yüzden hem AffectsRemoteVisuals hem istatistik.
    public class SpoilerModule : ModModuleBase
    {
        public override string Slot => "spoiler";
        public override bool AffectsRemoteVisuals => true;

        string _activeChild;

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            if (string.IsNullOrEmpty(item.childName)) return;

            var child = FindDeep(vehicle.Root.transform, item.childName);
            if (child == null)
            {
                Debug.LogWarning($"[Mod] Spoiler '{item.childName}' araçta yok — " +
                                 "geometri prefab'a eklenmemiş olabilir.");
                return;
            }

            child.gameObject.SetActive(true);
            _activeChild = item.childName;
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            if (string.IsNullOrEmpty(_activeChild)) return;
            var child = FindDeep(vehicle.Root.transform, _activeChild);
            if (child) child.gameObject.SetActive(false);
            _activeChild = null;
        }
    }
}
