namespace DreamCar.Car
{
    // "Bu aracı kim sürüyor ve söylediklerine güvenilir mi?"
    //
    // Bugün projede bu soru her bileşende ayrı ayrı ve elle yanıtlanıyor:
    // GetComponent<PhotonView>().IsMine. Bu yaklaşımın iki sorunu var:
    //
    //   1. Araç ağ dışında da doğabiliyor (menü önizlemesi, tek oyunculu test,
    //      offline fallback odası). PhotonView yoksa "IsMine" sorusunun yanıtı
    //      null referans oluyor, her bileşen bunu kendi başına ele almak zorunda.
    //   2. Faz 10'da ağ katmanı değişecek. O gün PhotonView'a bakan her satır
    //      tek tek elden geçirilmek zorunda kalır.
    //
    // Bu arayüz o soruyu tek yere topluyor. Ağ teknolojisi değişince değişen tek
    // dosya arayüzü uygulayan bileşen olur.
    public interface IVehicleAuthority
    {
        // Girdi bu istemciden geliyor mu? Ağ yoksa true (tek oyunculu = sahibiz).
        bool IsLocallyDriven { get; }

        // Fizik bu istemcide simüle ediliyor mu? Uzak araçlarda Rigidbody kinematik
        // ve WheelCollider'lar simüle edilmiyor — o araçta ABS/TC koşturmak anlamsız.
        bool SimulatesPhysics { get; }

        // Bu aracın ürettiği sayılara (hız, mesafe, kazanılan para) sunucu tarafında
        // güvenilebilir mi?
        //
        // BUGÜN HER ZAMAN false. Photon PUN 2 istemci otoriter: konumu, hızı ve
        // parayı istemci bildiriyor, kimse doğrulamıyor. Bu bir eksiklik değil,
        // PUN 2'nin tasarımı. Faz 10'daki sunucu otoriter göçünden sonra true
        // dönecek. Ekonomiye yazan her sistem bu bayrağa bakmalı ki göç günü
        // "hangi sayıya güveniyorduk" sorusu grep'lenebilir olsun.
        bool IsServerVerified { get; }

        // Sahibin ağ kimliği. Ağ yoksa 0.
        int OwnerActorNumber { get; }
    }
}
