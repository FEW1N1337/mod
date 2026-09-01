using Photon.Pun;
using UnityEngine;

namespace DreamCar.Car
{
    // CarController'a RequireComponent KOYMUYORUZ: RCCP'li araçta sürücü
    // RCCPCarAdapter, Unity eksik bileşeni otomatik eklerdi ve iki sürücü
    // aynı Rigidbody'yi sürerdi. Sürücüyü IDriveInput üzerinden buluyoruz.
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody))]
    public class CarNetworkSync : MonoBehaviourPun, IPunObservable
    {
        [Tooltip("How aggressively to interpolate remote cars toward the last received state.")]
        public float interpSpeed = 12f;

        Rigidbody _rb;
        IDriveInput _car;
        CarController _controller;
        Vector3 _netPos;
        Quaternion _netRot = Quaternion.identity;
        Vector3 _netVel;

        // Uzak araçlarda Rigidbody kinematik ve sürücü bileşeni kapalı. Bunun
        // sonucu, ağ pozisyonu doğru olsa bile aracın ÖLÜ görünmesiydi:
        // SpeedKmh linearVelocity'den okunuyor ve kinematik gövdede 0 —
        // motor sesi rölantide sabit kalıyor, WheelCollider simüle edilmediği
        // için tekerlek mesh'leri hiç dönmüyor ve direksiyon kırılmıyordu.
        // Diğer oyuncular donmuş tekerlekli sessiz kutular gibi kayıyordu.
        //
        // Çözüm: hızı, gazı ve direksiyonu da senkronla ve görsel/işitsel
        // sistemler uzak araçta bunları okusun. Kare başına üç float.
        public bool IsRemote { get; private set; }
        public float RemoteSpeedKmh { get; private set; }
        public float RemoteThrottle { get; private set; }
        public float RemoteSteer { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _car = GetComponent<IDriveInput>();
            _controller = GetComponent<CarController>();
            _netPos = transform.position;
            _netRot = transform.rotation;

            if (!photonView.IsMine)
            {
                // Kinematik Rigidbody yalnızca ContinuousSpeculative destekler; sürücü
                // bileşeni Awake'te ContinuousDynamic'e çekmiş olabileceğinden (Awake sırası
                // garanti değil) isKinematic'ten ÖNCE güvenli moda alıyoruz, yoksa Unity her
                // uzak araç için hata basıyor.
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _rb.isKinematic = true;

                // IDriveInput arayüzünde 'enabled' yok; kapatmak için somut bileşene
                // iniyoruz. Sürücü bulunamamışsa (prefab eksik kurulmuşsa) sessiz geçiyoruz.
                var carBehaviour = _car as MonoBehaviour;
                if (carBehaviour) carBehaviour.enabled = false;

                IsRemote = true;
            }
        }

        void Update()
        {
            // Tekerlekleri ağdan gelen hızla döndür. Yerel araçta bunu
            // CarController.SyncMesh yapıyor ama o FixedUpdate'te ve bileşen
            // uzak araçta kapalı.
            if (IsRemote && _controller)
                _controller.ApplyRemoteVisuals(RemoteSpeedKmh, RemoteSteer, Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (photonView.IsMine) return;

            transform.position = Vector3.Lerp(transform.position, _netPos + _netVel * 0.05f, Time.fixedDeltaTime * interpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _netRot, Time.fixedDeltaTime * interpSpeed);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
                stream.SendNext(_rb.linearVelocity);
                stream.SendNext(_car != null ? _car.SpeedKmh : 0f);
                stream.SendNext(_car != null ? _car.ThrottleInput : 0f);
                stream.SendNext(_car != null ? _car.SteerInput : 0f);
            }
            else
            {
                _netPos = (Vector3)stream.ReceiveNext();
                _netRot = (Quaternion)stream.ReceiveNext();
                _netVel = (Vector3)stream.ReceiveNext();
                RemoteSpeedKmh = (float)stream.ReceiveNext();
                RemoteThrottle = (float)stream.ReceiveNext();
                RemoteSteer = (float)stream.ReceiveNext();
            }
        }
    }
}
