using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.Network
{
    public class PhotonConnector : MonoBehaviourPunCallbacks
    {
        public static PhotonConnector Instance { get; private set; }

        [Tooltip("Bump this when releasing a breaking version — players on different versions won't matchmake together.")]
        public string gameVersion = "0.1";

        public bool autoConnectOnStart = true;
        public string preferredRegion = "";

        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            if (autoConnectOnStart) Connect();
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected) return;
            if (!HasAppId()) return;
            PhotonNetwork.GameVersion = gameVersion;

            // Öncelik: oyuncunun RegionSelector'dan seçtiği bölge, yoksa inspector değeri.
            var saved = UI.RegionSelector.SavedRegion;
            var region = !string.IsNullOrEmpty(saved) ? saved : preferredRegion;
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion =
                string.IsNullOrEmpty(region) ? null : region;
            PhotonNetwork.ConnectUsingSettings();
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log($"[Photon] Connected to master. Region={PhotonNetwork.CloudRegion}");
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[Photon] Disconnected: {cause}");

            // Kopma yalnızca Console'a yazılıyordu. Oyuncunun gördüğü tek şey
            // hiç dolmayan bir oda listesiydi: buton basılıyor, hiçbir şey
            // olmuyor, sebep görünmüyor. ReconnectionManager yeniden bağlanmayı
            // deniyor, ama denediğini de söylemek gerekiyor.
            if (cause == DisconnectCause.InvalidAuthentication ||
                cause == DisconnectCause.CustomAuthenticationFailed)
                UI.ToastNotification.Show("Photon App Id gecersiz — PhotonServerSettings'i kontrol et");
            else if (cause != DisconnectCause.DisconnectByClientLogic)
                UI.ToastNotification.Show($"Baglanti koptu: {cause}");
        }

        // App Id boşken ConnectUsingSettings sessizce false döner: menü açılır,
        // oda listesi sonsuza kadar boş kalır ve HİÇBİR YERDE sebep yazmaz.
        // Kurulumun en sık atlanan adımı bu, o yüzden tek ekranda anlaşılır
        // olmalı. Kendi sunucusunu kuranlarda App Id gerekmiyor — Server alanı
        // doluysa uyarmıyoruz.
        static bool HasAppId()
        {
            var settings = PhotonNetwork.PhotonServerSettings;
            if (settings == null || settings.AppSettings == null) return true;

            bool hasAppId = !string.IsNullOrWhiteSpace(settings.AppSettings.AppIdRealtime);
            bool hasOwnServer = !string.IsNullOrWhiteSpace(settings.AppSettings.Server);
            if (hasAppId || hasOwnServer) return true;

            Debug.LogError(
                "[Photon] App Id GİRİLMEMİŞ — çevrimiçi hiçbir şey çalışmayacak.\n" +
                "Yapılacak:\n" +
                "  1) dashboard.photonengine.com → Create a New App\n" +
                "  2) Photon SDK = 'Pun', sürüm = 'Pun 2'  (VARSAYILAN 'Fusion' İŞE YARAMAZ)\n" +
                "  3) Uygulamanın App ID'sini kopyala\n" +
                "  4) Unity: Window → Photon Unity Networking → Highlight Server Settings\n" +
                "     → App Id PUN alanına yapıştır");
            UI.ToastNotification.Show("Photon App Id girilmemiş — Console'a bak");
            return false;
        }
    }
}
