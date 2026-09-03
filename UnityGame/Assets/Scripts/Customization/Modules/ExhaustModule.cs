using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Customization.Modules
{
    // Egzoz. Tork etkisinin yanında motor SESİNİ de değiştiriyor — sportif
    // egzozun tek görünür (duyulur) karşılığı bu.
    //
    // EngineAudio.maxPitch'e yazıyoruz, doğrudan AudioSource.pitch'e değil:
    // EngineAudio her karede pitch'i hıza göre yeniden hesaplıyor ve
    // AudioSource'a yazılan değer hemen ezilirdi. Aynı tuzağa FuelSystem daha
    // önce düşmüştü (bkz. IDriveInput.EngineCutoff yorumu).
    public class ExhaustModule : ModModuleBase
    {
        public override string Slot => "exhaust";

        // Ses uzak araçlarda da duyuluyor (motor sesi 3B konumsal).
        public override bool AffectsRemoteVisuals => true;

        // TAM NİTELENDİRİLMİŞ: 'using UnityEngine' yüzünden kısa 'Audio.'
        // öneki UnityEngine.Audio ile karışabilecek kadar yakın duruyor.
        DreamCar.Audio.EngineAudio _audio;
        float _originalMaxPitch;
        bool _hasOriginal;

        protected override void OnApply(VehicleContext vehicle, ModItem item)
        {
            _audio = vehicle.Root.GetComponent<DreamCar.Audio.EngineAudio>();
            if (!_audio) return;

            if (!_hasOriginal)
            {
                _originalMaxPitch = _audio.maxPitch;
                _hasOriginal = true;
            }

            // ModItem.smoothness burada "ses sertliği" olarak kullanılıyor:
            // 0 = fabrika, 1 = belirgin sportif. Katalog bunu yazıyor.
            _audio.maxPitch = Mathf.Lerp(_originalMaxPitch, _originalMaxPitch * 1.35f,
                                         Mathf.Clamp01(item.smoothness));
        }

        protected override void OnRemove(VehicleContext vehicle)
        {
            if (!_hasOriginal || !_audio) return;
            _audio.maxPitch = _originalMaxPitch;
        }
    }
}
