using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Alt neon. Prefabda kapalı duran "NeonStrip" çocuğunu açıyor ve rengini
    // ürüne göre ayarlıyor — emissive materyal + gerçek Light birlikte.
    //
    // Yalnızca emissive olsaydı yerde ışık havuzu olmazdı; yalnızca Light
    // olsaydı şeridin kendisi karanlıkta görünmezdi. İkisi de gerekiyor.
    //
    // ÖNİZLEMEDE IŞIK YOK: SavePreviewPrefab menü sahnesini yıkmamak için
    // bütün Light'ları gameObject'iyle beraber siliyor. Garajda neon emissive
    // olarak görünüyor, yerde havuz olmuyor. Bilinen ve kabul edilmiş sınır.
    public class NeonModule : ModModuleBase
    {
        public override string Slot => "neon";
        public override bool AffectsRemoteVisuals => true;

        const string ChildName = "NeonStrip";
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            var child = FindDeep(vehicle.Root.transform, ChildName);
            if (child == null) return;

            child.gameObject.SetActive(true);

            // HDR emissive: bloom'un eşiği 0.95 (PostFX_High), 1'in altındaki
            // parlaklık parlamıyor. Çarpan olmadan neon sönük bir renk lekesi
            // olurdu.
            var hdr = item.color * 3.5f;
            foreach (var r in child.GetComponentsInChildren<Renderer>(true))
            {
                SetBlockColor(r, _block, EmissionId, hdr);
                SetBlockColor(r, _block, BaseColorId, item.color);
            }

            foreach (var light in child.GetComponentsInChildren<Light>(true))
            {
                light.color = item.color;
                light.enabled = true;
            }
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            var child = FindDeep(vehicle.Root.transform, ChildName);
            if (child) child.gameObject.SetActive(false);
        }
    }
}
