using DreamCar.Car;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    public class InGameHUD : MonoBehaviour
    {
        public TMP_Text speedText;
        public TMP_Text playerCountText;
        public TMP_Text roomNameText;
        public Button leaveButton;

        // Somut tip yerine arayüz: HUD hem bizim sürücümüzü hem RCCP'li aracı okur.
        IDriveInput _car;

        void Start()
        {
            if (leaveButton) leaveButton.onClick.AddListener(Leave);
        }

        void Update()
        {
            if (!IsAlive(_car))
            {
                // FindObjectsByType arayüz tipiyle kullanılamaz (T : Object şartı var).
                // Bu yüzden tarama PhotonView üzerinden yürüyor: bizim olan görünümü
                // bulup üstündeki sürücüyü alıyoruz — eskisiyle aynı araç, somut tipe
                // bağlanmadan (GetComponent arayüzle çalışır).
                foreach (var pv in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
                {
                    if (!pv.IsMine) continue;
                    var drive = pv.GetComponent<IDriveInput>();
                    if (drive != null) { _car = drive; break; }
                }
            }

            bool hasCar = IsAlive(_car);
            if (speedText) speedText.text = hasCar ? $"{Mathf.RoundToInt(_car.SpeedKmh)} km/h" : "-- km/h";
            if (playerCountText && PhotonNetwork.CurrentRoom != null)
                playerCountText.text = $"{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
            if (roomNameText && PhotonNetwork.CurrentRoom != null)
                roomNameText.text = PhotonNetwork.CurrentRoom.Name;
        }

        // Arayüz referansında Unity'nin `!obj` kısayolu çalışmaz (yok edilmiş bileşen
        // C# tarafında null görünmez); Unity nesnesiyse kendi null operatörünü kullan.
        // Böylece araç yok edilince eskisi gibi yeniden tarama başlar.
        static bool IsAlive(IDriveInput drive)
        {
            if (drive == null) return false;
            if (drive is UnityEngine.Object obj) return obj != null;
            return true;
        }

        void Leave()
        {
            // Bilerek çıkış — ReconnectionManager yeniden bağlanmayı denememeli.
            if (Network.ReconnectionManager.Instance) Network.ReconnectionManager.Instance.MarkUserInitiatedLeave();
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.LoadLevel("MainMenu");
        }
    }
}
