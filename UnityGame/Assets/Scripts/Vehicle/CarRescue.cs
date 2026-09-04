using Photon.Pun;
using UnityEngine;
using DreamCar.UI;

namespace DreamCar.Vehicle
{
    // KURTARMA — projede hiç yoktu ve yokluğu oyunu kilitliyordu.
    //
    // "Respawn", "Reset", "Flip" diye aranınca proje genelinde tek bir sonuç
    // çıkmıyordu. Yani bir araba oyununda şunların hiçbirinden dönüş yoktu:
    //   • Araç takla attı → tekerlekler havada, gaz hiçbir şey yapmıyor.
    //   • Araç haritadan düştü → sonsuza kadar düşüyor.
    //   • Yakıt istasyondan uzakta bitti → FuelSystem motoru kesiyor, araç
    //     duruyor ve bir daha asla hareket edemiyor.
    // Üçünde de tek çıkış odadan çıkmaktı. Her araba oyununda bir "Kurtar"
    // düğmesi olmasının sebebi bu.
    //
    // Kurtarma iki kademeli: mümkünse aracı OLDUĞU YERDE doğrultur (uzun bir
    // yolculuğu geri almak cezalandırıcı olurdu), altında zemin yoksa —
    // haritadan düşmüşse — doğma noktasına geri koyar.
    public class CarRescue : MonoBehaviour
    {
        [Tooltip("Yakıt bitmişse kurtarmada bedava verilen acil yakıt. İstasyona ulaşmaya yeter.")]
        public float emergencyFuelLiters = 6f;

        [Tooltip("Ters duran ve duran araç bu kadar saniye sonra kendiliğinden kurtarılır.")]
        public float autoRescueSeconds = 5f;

        [Tooltip("Bu Y değerinin altına düşen araç anında kurtarılır.")]
        public float fallY = -60f;

        [Tooltip("Arka arkaya kurtarma arasındaki en kısa süre — ışınlanarak ilerlemeyi engeller.")]
        public float cooldownSeconds = 3f;

        Rigidbody _rb;
        PhotonView _pv;
        FuelSystem _fuel;
        float _uprightTimer;
        float _lastRescueTime = -999f;

        public bool IsReady => Time.time - _lastRescueTime >= cooldownSeconds;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _pv = GetComponent<PhotonView>();
            _fuel = GetComponent<FuelSystem>();
        }

        void Start()
        {
            // Uzak oyuncuların araçlarını kurtarmak bizim işimiz değil.
            if (_pv && !_pv.IsMine) enabled = false;
        }

        void Update()
        {
            if (transform.position.y < fallY) { Rescue(); return; }

            bool onRoof = transform.up.y < 0.2f;
            bool stopped = _rb == null || _rb.linearVelocity.sqrMagnitude < 1f;

            if (onRoof && stopped)
            {
                _uprightTimer += Time.deltaTime;
                if (_uprightTimer >= autoRescueSeconds) Rescue();
            }
            else _uprightTimer = 0f;
        }

        public void Rescue()
        {
            if (!IsReady) return;
            _lastRescueTime = Time.time;
            _uprightTimer = 0f;

            Vector3 position;
            Quaternion rotation;
            bool teleported;

            if (TryGroundBelow(out Vector3 ground))
            {
                // Yerinde doğrult: yön (yaw) korunur, yatma/takla sıfırlanır.
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

                position = ground + Vector3.up * 1.2f;
                rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                teleported = false;
            }
            else if (TryNearestSpawn(out Transform spawn))
            {
                position = spawn.position + Vector3.up * 0.5f;
                rotation = spawn.rotation;
                teleported = true;
            }
            else
            {
                // Ne zemin ne doğma noktası: en azından havaya al, düşüşü durdur.
                position = transform.position + Vector3.up * 3f;
                rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                teleported = true;
            }

            // Rigidbody varken transform'a yazmak yetmez; ikisini birden
            // ayarlayıp hızları sıfırlıyoruz, yoksa araç eski hızıyla fırlar.
            if (_rb)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position = position;
                _rb.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, rotation);

            // Yakıtsız kurtarma anlamsız olurdu: araç doğrulur ve yerinde
            // yine kalırdı. İstasyona ulaşacak kadar bedava yakıt veriyoruz.
            bool refuelled = false;
            if (_fuel && _fuel.IsEmpty && emergencyFuelLiters > 0f)
            {
                _fuel.current = Mathf.Min(_fuel.capacity, emergencyFuelLiters);
                refuelled = true;
            }

            ToastNotification.Show(
                refuelled ? $"Araç kurtarıldı · +{emergencyFuelLiters:0} L acil yakıt"
                : teleported ? "Araç doğma noktasına alındı"
                : "Araç doğrultuldu");
        }

        // Aracın altında zemin var mı? Kendi çarpıştırıcılarımızı atlıyoruz —
        // basit bir Raycast ilk olarak aracın kendi gövdesine çarpardı.
        bool TryGroundBelow(out Vector3 point)
        {
            point = default;
            Vector3 origin = transform.position + Vector3.up * 4f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            float best = float.MaxValue;
            bool found = false;
            Transform self = transform.root;

            foreach (var hit in hits)
            {
                if (hit.transform == null || hit.transform.root == self) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                point = hit.point;
                found = true;
            }
            return found;
        }

        // En yakın doğma noktası — mesafe ARACA göre ölçülür. Kameraya göre
        // ölçmek sinematik/tepeden kamerada yanlış noktayı seçerdi, üstelik
        // Camera.main sahne yüklenirken null olabiliyor.
        bool TryNearestSpawn(out Transform spawn)
        {
            spawn = null;
            var room = FindAnyObjectByType<Network.RoomManager>();
            if (room == null || room.spawnPoints == null) return false;

            float best = float.MaxValue;
            Vector3 from = transform.position;
            foreach (var t in room.spawnPoints)
            {
                if (t == null) continue;
                float d = (t.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                spawn = t;
            }
            return spawn != null;
        }
    }
}
