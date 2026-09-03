using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Cam filmi. Araç prefabındaki "Glass" renderer'ının rengini ve saydamlığını
    // değiştiriyor — Dream Road'un imza modifikasyonlarından biri ve projede hiç
    // yoktu (WindowTint diye bir tip aramada yalnızca arayüz yorumunda geçiyordu).
    //
    // Cam materyali zaten saydam (ProceduralTextures.CreateGlassMaterial), yani
    // alfayı düşürmek/yükseltmek shader tarafında çalışıyor; ek geçiş yok.
    public class WindowTintModule : ModModuleBase
    {
        public override string Slot => "tint";
        public override bool AffectsRemoteVisuals => true;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Color _original;
        bool _hasOriginal;

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            var glass = FindGlass(vehicle);
            if (!glass) return;

            // İlk uygulamada fabrika rengini saklıyoruz. Çıkarma bunu geri
            // yazıyor; sabit bir "varsayılan cam rengi" yazmak araç başına
            // farklı olan cam tonunu bozardı.
            if (!_hasOriginal)
            {
                _original = glass.sharedMaterial ? GetColor(glass.sharedMaterial) : Color.white;
                _hasOriginal = true;
            }

            var tint = item.color;
            tint.a = item.alpha;
            SetBlockColor(glass, _block, BaseColorId, tint);
            SetBlockColor(glass, _block, ColorId, tint);
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            if (!_hasOriginal) return;
            var glass = FindGlass(vehicle);
            if (!glass) return;
            SetBlockColor(glass, _block, BaseColorId, _original);
            SetBlockColor(glass, _block, ColorId, _original);
        }

        // RCCP ile dönüştürülmüş araçta "Glass" adında bir nesne olmayabilir;
        // bulunamazsa modül sessizce hiçbir şey yapmıyor (CarCustomization
        // eksik hedefleri bir kez Console'a yazıyor).
        static Renderer FindGlass(VehicleContext vehicle)
        {
            var t = FindDeep(vehicle.Root.transform, "Glass");
            return t ? t.GetComponent<Renderer>() : null;
        }

        static Color GetColor(Material m) =>
            m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) :
            m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
    }
}
