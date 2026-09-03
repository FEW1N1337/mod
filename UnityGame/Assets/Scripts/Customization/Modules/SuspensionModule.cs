using System.Collections.Generic;
using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Süspansiyon. İstatistik etkisinin (Grip) yanında gerçek bir GÖRSEL ve
    // fiziksel etki daha veriyor: aracı alçaltıyor.
    //
    // Alçaltma WheelCollider.suspensionSpring.targetPosition ile yapılıyor,
    // suspensionDistance ile DEĞİL. Sebebi: suspensionDistance yayın toplam
    // hareket aralığı; küçültmek aracı alçaltmıyor, süspansiyonu sertleştirip
    // her tümsekte dibe vurdurtuyor. targetPosition ise yayın dinlenme noktası
    // — düşürmek aracı gerçekten yere yaklaştırıyor ve hareket aralığını
    // korunuyor.
    //
    // ModItem.alpha alanı burada "alçaltma miktarı" olarak kullanılıyor
    // (0 = fabrika, 1 = tam alçak). Alan adı görsel modüller için konmuştu ama
    // 0..1 aralığında serbest bir katsayı ve katalogda anlamı yazılı.
    public class SuspensionModule : ModModuleBase
    {
        public override string Slot => "suspension";

        // Uzak araçta da uygulanmalı: aracın yerden yüksekliği GÖRÜNÜR bir fark.
        // Uzak araçta WheelCollider simüle edilmiyor ama yay hedefi yazmak
        // zararsız; simülasyon açılırsa doğru değerle başlıyor.
        public override bool AffectsRemoteVisuals => true;

        readonly Dictionary<WheelCollider, float> _originalTargets = new();

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            foreach (var wc in vehicle.Root.GetComponentsInChildren<WheelCollider>(true))
            {
                if (!wc) continue;
                var spring = wc.suspensionSpring;

                if (!_originalTargets.ContainsKey(wc)) _originalTargets[wc] = spring.targetPosition;

                // 0.5 fabrika değeri (ProceduralCarGenerator böyle kuruyor).
                // 0.15'in altına inmiyoruz: yay dinlenme noktası sıfıra
                // yaklaştıkça araç sürekli dibe vurur hâle geliyor.
                spring.targetPosition = Mathf.Lerp(_originalTargets[wc], 0.15f, Mathf.Clamp01(item.alpha));
                wc.suspensionSpring = spring;
            }
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            foreach (var pair in _originalTargets)
            {
                if (!pair.Key) continue;
                var spring = pair.Key.suspensionSpring;
                spring.targetPosition = pair.Value;
                pair.Key.suspensionSpring = spring;
            }
            _originalTargets.Clear();
        }
    }
}
