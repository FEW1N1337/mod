using UnityEngine;

namespace DreamCar.Environment
{
    // Hava partiküllerini kameranın üstünde tutar.
    //
    // Neden gerekli: yağmur/kar emisyon kutusu haritanın tamamını kaplayacak
    // kadar büyük olamaz (yüz binlerce partikül eder). Bunun yerine kameranın
    // üstünde birkaç on metrelik bir kutu tutulur ve kutu kamerayla gezer.
    // Partiküller World uzayında simüle edildiği için kutu kayarken havadaki
    // damlalar yerinde kalır — takip fark edilmez.
    //
    // LateUpdate: kamera takibi (CarCameraFollow) da LateUpdate'te çalışıyor,
    // execution order ile ondan sonraya alındık; yoksa bir kare gecikirdik.
    [DefaultExecutionOrder(100)]
    public class WeatherFollowCamera : MonoBehaviour
    {
        [Tooltip("Takip edilecek hedef. Boşsa Camera.main kullanılır.")]
        public Transform target;

        [Tooltip("Hedefe göre konum kayması — partiküller tepeden düşsün diye yukarıda durur.")]
        public Vector3 offset = new(0f, 16f, 0f);

        [Tooltip("Bakış yönüne doğru kaydırma. Hızlı giderken önümüz boş kalmasın.")]
        public float forwardLead = 14f;

        Transform _cached;

        void LateUpdate()
        {
            var t = Resolve();
            if (!t) return;

            Vector3 forward = t.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            transform.position = t.position + offset + forward * forwardLead;
        }

        // Camera.main sahne değişiminde geçersizleşebilir; bulunamazsa her karede
        // yeniden aranır ama bulununca önbelleğe alınır.
        Transform Resolve()
        {
            if (target) return target;
            if (_cached) return _cached;

            var cam = Camera.main;
            _cached = cam ? cam.transform : null;
            return _cached;
        }
    }
}
