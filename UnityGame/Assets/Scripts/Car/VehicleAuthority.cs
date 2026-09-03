using Photon.Pun;
using UnityEngine;

namespace DreamCar.Car
{
    // IVehicleAuthority'nin tek uygulaması. Photon'a bakan tek araç bileşeni olması
    // hedefleniyor: Faz 10'daki sunucu otoriter göçünde ağ tipine bağlı araç kodu
    // yalnızca burada olacak.
    //
    // Bugün projede "bu araç benim mi" sorusu on ayrı bileşende ayrı ayrı
    // yanıtlanıyor (StatsTracker, CarRescue, FuelSystem, CruiseControl, ...).
    // Bu bileşen o soruyu tek noktaya alıyor; mevcut bileşenler kendi
    // kontrollerini koruyor, yenileri buradan soruyor.
    public class VehicleAuthority : MonoBehaviour, IVehicleAuthority
    {
        PhotonView _pv;
        Rigidbody _rb;

        void Awake()
        {
            _pv = GetComponent<PhotonView>();
            _rb = GetComponent<Rigidbody>();
        }

        // PhotonView yoksa araç ağ dışında doğmuş demektir (menü önizlemesi, editör
        // testi, offline oda). O durumda sahibi biziz.
        public bool IsLocallyDriven => _pv == null || _pv.IsMine;

        // Uzak araçlarda Rigidbody kinematik yapılıyor ve WheelCollider'lar simüle
        // edilmiyor; orada sürüş yardımcısı koşturmak boşa iş.
        public bool SimulatesPhysics => IsLocallyDriven && (_rb == null || !_rb.isKinematic);

        // Photon PUN 2 istemci otoriter. Hiçbir sayı sunucuda doğrulanmıyor, bu
        // yüzden sabit false. Sunucu otoriter katman geldiğinde tek satır değişecek;
        // o gün "hangi sayıya güveniyorduk" sorusunun yanıtı bu bayrağın
        // okunduğu yerler olacak.
        public bool IsServerVerified => false;

        public int OwnerActorNumber => _pv != null && _pv.Owner != null ? _pv.Owner.ActorNumber : 0;
    }
}
