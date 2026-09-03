using System.Collections.Generic;
using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Jant rengi. Dört tekerleğin "*_Rim" renderer'ına yazıyor.
    //
    // SINIR: jantın ŞEKLİ değişmiyor, yalnızca rengi ve yüzeyi. Farklı jant
    // modeli ayrı mesh varyantları demek; prosedürel üretici bunu yapabilir ama
    // bu fazın dışında.
    public class RimColorModule : ModModuleBase
    {
        public override string Slot => "rim";
        public override bool AffectsRemoteVisuals => true;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Color _original;
        bool _hasOriginal;

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            var rims = FindRims(vehicle);
            if (rims.Count == 0) return;

            if (!_hasOriginal)
            {
                var m = rims[0].sharedMaterial;
                _original = m && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.gray;
                _hasOriginal = true;
            }

            foreach (var r in rims)
            {
                SetBlockColor(r, _block, BaseColorId, item.color);
                SetBlockColor(r, _block, ColorId, item.color);
                SetBlockFloat(r, _block, MetallicId, item.metallic);
                SetBlockFloat(r, _block, SmoothnessId, item.smoothness);
            }
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            if (!_hasOriginal) return;
            foreach (var r in FindRims(vehicle))
            {
                SetBlockColor(r, _block, BaseColorId, _original);
                SetBlockColor(r, _block, ColorId, _original);
            }
        }

        // Ada göre değil SONEKE göre arıyoruz: FL_Rim, FR_Rim, RL_Rim, RR_Rim.
        static List<Renderer> FindRims(VehicleContext vehicle)
        {
            var result = new List<Renderer>();
            foreach (var r in vehicle.Root.GetComponentsInChildren<Renderer>(true))
                if (r && r.name.EndsWith("_Rim")) result.Add(r);
            return result;
        }
    }
}
