using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Boya.
    //
    // NEDEN BU MODÜL VAR: CarPaint bileşeni projede baştan beri duruyor,
    // MaterialPropertyBlock'la boyayı değiştiriyor, Photon üzerinden diğer
    // oyunculara bildiriyor ve PlayerPrefs'e kaydediyor — ama **Apply()'ı
    // hiçbir arayüzden çağıran yok.** Yani oyuncunun aracının rengini
    // değiştirmesinin hiçbir yolu yoktu ve hiç hata basmıyordu. Kayıt anahtarı
    // (car.color) da hiç yazılmıyordu, dolayısıyla LoadFromPrefs her zaman
    // varsayılan kırmızıyı döndürüyordu.
    //
    // Bu modül o çağıranı sağlıyor ve boyayı diğer parçalarla aynı yola
    // sokuyor: katalog, sahiplik, araç başına kayıt.
    public class PaintModule : ModModuleBase
    {
        public override string Slot => "paint";
        public override bool AffectsRemoteVisuals => true;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            // Gerçek araçta CarPaint üzerinden gidiyoruz: o bileşen aynı
            // zamanda Photon Custom Properties'e yazıyor ve uzak oyuncular
            // rengi oradan okuyor. Kendi başımıza MPB yazsaydık renk yalnızca
            // bizim ekranımızda değişirdi.
            var paint = vehicle.Root.GetComponent<CarPaint>();
            if (paint != null)
            {
                paint.Apply(item.color, item.metallic, item.smoothness);
                return;
            }

            // Önizleme prefabında CarPaint yok (bütün MonoBehaviour'lar
            // atılıyor). Orada doğrudan gövde renderer'ına yazıyoruz.
            var body = FindDeep(vehicle.Root.transform, "Body");
            var renderer = body ? body.GetComponent<Renderer>() : null;
            if (!renderer) return;

            SetBlockColor(renderer, _block, BaseColorId, item.color);
            SetBlockColor(renderer, _block, ColorId, item.color);
            SetBlockFloat(renderer, _block, MetallicId, item.metallic);
            SetBlockFloat(renderer, _block, SmoothnessId, item.smoothness);
        }

        // Boyanın "çıkarılması" yok: her araç bir renkte. Slot boşaltılırsa
        // fabrika rengine dönmesi gerekirdi ama fabrika rengi araç başına
        // farklı ve materyalde duruyor — MPB'yi temizlemek onu geri getiriyor.
        protected override void OnRemove(VehicleContext vehicle)
        {
            var body = FindDeep(vehicle.Root.transform, "Body");
            var renderer = body ? body.GetComponent<Renderer>() : null;
            if (renderer) renderer.SetPropertyBlock(null);
        }
    }
}
