using UnityEngine;
using DreamCar.Car;

namespace DreamCar.UI
{
    // Analog kilometre saati iğnesi. UI ise RectTransform.eulerAngles.z döner,
    // 3D mesh ise transform.localEulerAngles.
    public class SpeedometerNeedle : MonoBehaviour
    {
        // Somut tip yerine arayüz: gösterge RCCP'li araçta da çalışsın.
        // Unity arayüz alanını serileştiremez; referans dışarıdan, çalışma anında
        // atanıyor (araç PhotonNetwork.Instantiate ile doğduktan sonra).
        [System.NonSerialized] public IDriveInput car;
        public RectTransform needle;
        public float minAngle = 220f;
        public float maxAngle = -40f;
        public float smoothing = 8f;

        float _current;

        void Update()
        {
            // Yerel aracı kendisi bulur — InGameHUD ile aynı kalıp. Dışarıdan
            // bağlanmayı beklemek, bileşen bir sahneye eklendiğinde sessizce
            // çalışmamasına yol açardı (alan serileştirilemiyor).
            // FindObjectsByType arayüz tipiyle kullanılamaz (T : Object şartı),
            // bu yüzden PhotonView üzerinden gidiyoruz.
            if (!IsAlive(car))
            {
                foreach (var pv in FindObjectsByType<Photon.Pun.PhotonView>(FindObjectsSortMode.None))
                {
                    if (!pv.IsMine) continue;
                    var drive = pv.GetComponent<IDriveInput>();
                    if (drive != null) { car = drive; break; }
                }
            }

            if (!IsAlive(car) || !needle) return;

            float target = Util.GameMath.SpeedometerAngle(car.SpeedKmh, car.TopSpeedKmh, minAngle, maxAngle);
            _current = Mathf.LerpAngle(_current, target, Time.deltaTime * smoothing);
            needle.localEulerAngles = new Vector3(0f, 0f, _current);
        }

        // Arayüz referansında Unity'nin `!obj` kısayolu çalışmaz (yok edilmiş bileşen
        // C# tarafında null görünmez); Unity nesnesiyse kendi null operatörünü kullan.
        static bool IsAlive(IDriveInput drive)
        {
            if (drive == null) return false;
            if (drive is UnityEngine.Object obj) return obj != null;
            return true;
        }
    }
}
