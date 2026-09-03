using DreamCar.CameraModes;
using DreamCar.Effects;
using DreamCar.Emote;
using DreamCar.Network;
using DreamCar.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Korna, sinyal, dörtlü, emote ve kamera modu: hepsinin kodu ve RPC
    // altyapısı yazılmıştı ama HİÇBİRİNİ ÇAĞIRAN YOKTU.
    //
    //   HornController.Press()   — Emote/ klasörü dışında sıfır çağrı
    //   TurnSignals.Left/Right/Hazard() — sıfır çağrı
    //   EmoteSystem.Play(id)     — sıfır çağrı
    //   CameraModeController.Cycle() — yalnızca KeyCode.V, mobilde ulaşılamaz
    //
    // Yani korna butonu yoktu, sinyal yoktu, emote yoktu ve oyunda tek kamera
    // açısı vardı. Bunlar araç prefabının üzerinde duruyor; araç ise odaya
    // girilince doğuyor, o yüzden Editor'de bağlanamıyorlar. Bu bileşen HUD
    // tarafında durup çağrıları çalışma anında yerel araca iletir.
    public class CarActionButtons : MonoBehaviour
    {
        public Button cameraButton;
        public Button hornButton;
        public Button signalLeftButton;
        public Button signalRightButton;
        public Button hazardButton;
        public Button emoteButton;

        // Kurtarma: takla atan, haritadan düşen veya yakıtı biten araç için
        // tek çıkış yolu. CarRescue kendiliğinden de devreye giriyor ama
        // oyuncunun beş saniye beklemek zorunda kalmaması gerekiyor.
        public Button rescueButton;

        // EmoteSystem.emotes listesi boşken Play() sessizce dönüyor; üretici
        // en az bir giriş koyuyor ve bu kimlik onunla eşleşiyor.
        public string emoteId = "wave";

        GameObject _car;
        HornController _horn;
        TurnSignals _signals;
        EmoteSystem _emotes;
        CarRescue _rescue;

        void Start()
        {
            // Kalıcı listener yerine çalışma anında bağlama: hedef metotlar
            // sahnede var olmayan bir nesnenin üzerinde, Editor'den
            // serileştirilemezler. Bu bileşenin KENDİSİ sahnede duruyor ve
            // butonlara burada bağlanıyor.
            Hook(cameraButton, CycleCamera);
            Hook(hornButton, Horn);
            Hook(signalLeftButton, () => { if (Signals()) _signals.Left(); });
            Hook(signalRightButton, () => { if (Signals()) _signals.Right(); });
            Hook(hazardButton, () => { if (Signals()) _signals.Hazard(); });
            Hook(emoteButton, () => { if (Emotes()) _emotes.Play(emoteId); });
            Hook(rescueButton, () => { if (Ready(ref _rescue)) _rescue.Rescue(); });
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b) b.onClick.AddListener(a);
        }

        void CycleCamera()
        {
            var cam = Camera.main ? Camera.main.GetComponent<CameraModeController>() : null;
            if (cam) cam.Cycle();
        }

        void Horn() { if (Ready(ref _horn)) _horn.Press(); }
        bool Signals() => Ready(ref _signals);
        bool Emotes() => Ready(ref _emotes);

        // Araç sahne içinde yeniden doğabiliyor (odadan çıkıp girme, araç
        // değiştirme). Önbelleği araç kimliği değişince tazeliyoruz; her
        // tıklamada GetComponent yapmak da olurdu ama bu daha ucuz ve
        // yok edilmiş bir bileşene tutunma riskini kapatıyor.
        bool Ready<T>(ref T cached) where T : Component
        {
            var car = RoomManager.LocalCar;
            if (!car) { cached = null; return false; }
            if (car != _car) { _car = car; _horn = null; _signals = null; _emotes = null; _rescue = null; }
            if (!cached) cached = car.GetComponent<T>();
            return cached;
        }
    }
}
