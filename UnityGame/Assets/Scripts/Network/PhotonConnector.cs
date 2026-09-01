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
        }
    }
}
