using UnityEngine;

namespace DreamCar.Effects
{
    // Nesneyi her karede aktif kameraya döndürür.
    //
    // Emote baloncuğu SpriteRenderer ile çiziliyor ve SpriteRenderer kendi
    // +Z'sine bakar. EmoteSystem popup'ı Quaternion.identity ile doğuruyor,
    // yani hiçbir şey onu kameraya çevirmezdi: oyuncu araca yandan bakınca
    // emote bir çizgiye dönüşür, arkadan bakınca ters görünürdü.
    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        // Yalnızca yatay eksende dön: yukarıdan bakan bir kamerada baloncuğun
        // yan yatmasını istemiyoruz.
        public bool lockUpright = true;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;

            Vector3 forward = transform.position - cam.transform.position;
            if (lockUpright) forward.y = 0f;
            // sqrMagnitude: kamera tam üstteyken (lockUpright ile) sıfır vektör
            // çıkabiliyor ve LookRotation o durumda uyarı basıyor.
            if (forward.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
