using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using DreamCar.GameModes;
using DreamCar.Maps;

namespace DreamCar.Network
{
    public class RoomManager : MonoBehaviourPunCallbacks
    {
        [Tooltip("Prefab name that lives under Assets/Resources/ (Photon looks it up by name). Aktif araç varsa CarInventory.ActiveCar.resourcePrefabName ile override edilir.")]
        public string carPrefabName = "Car";

        public Transform[] spawnPoints;
        public bool addGameModeManager = true;
        public bool applyMapPreset = true;

        GameObject _localCar;

        // Yerel araç odaya girilince doğuyor; HUD bileşenleri (korna, sinyal,
        // emote butonları) onu Editor'de bağlayamıyor, çalışma anında buradan
        // alıyorlar.
        public static GameObject LocalCar { get; private set; }

        void Start()
        {
            if (addGameModeManager && !FindFirstObjectByType<GameModeManager>())
                gameObject.AddComponent<GameModeManager>();
            if (applyMapPreset)
            {
                var sel = FindFirstObjectByType<MapSelector>();
                if (sel) sel.ApplyForRoom();
            }
            if (PhotonNetwork.InRoom) SpawnLocalCar();
        }

        public override void OnJoinedRoom() => SpawnLocalCar();

        void SpawnLocalCar()
        {
            if (_localCar) return;
            int idx = PhotonNetwork.LocalPlayer.ActorNumber - 1;
            Transform spawn = (spawnPoints != null && spawnPoints.Length > 0)
                ? spawnPoints[idx % spawnPoints.Length]
                : null;

            Vector3 pos = spawn ? spawn.position : Vector3.up * 1f;
            Quaternion rot = spawn ? spawn.rotation : Quaternion.identity;

            string prefab = carPrefabName;
            var active = Economy.CarInventory.Instance ? Economy.CarInventory.Instance.ActiveCar : null;
            if (active && !string.IsNullOrEmpty(active.resourcePrefabName)) prefab = active.resourcePrefabName;

            _localCar = PhotonNetwork.Instantiate(prefab, pos, rot);
            LocalCar = _localCar;

            Monetization.Analytics.Event("car_spawn", new()
            {
                { "car", prefab },
                { "room", PhotonNetwork.CurrentRoom?.Name ?? "-" },
                { "players", PhotonNetwork.CurrentRoom?.PlayerCount ?? 0 },
            });

            var follow = Camera.main ? Camera.main.GetComponent<Car.CarCameraFollow>() : null;
            if (follow) follow.target = _localCar.transform;

            // CameraModeController'a yalnızca "follow" atanıyordu; "target" ve
            // çapa noktaları null kaldığı için LateUpdate ilk satırda dönüyor ve
            // kaput/tampon/kokpit/sinematik kameraların hiçbiri çalışmıyordu.
            // Araç prefabı bu çapaları üretiyor ama yalnızca OverheadAnchor
            // kullanılıyordu.
            var camModes = Camera.main ? Camera.main.GetComponent<CameraModes.CameraModeController>() : null;
            if (camModes) camModes.Bind(_localCar.transform);

            // Minimap kamerası yerel aracı takip eder. Araç ancak odaya girilince
            // doğduğu için bu bağlantı Editor'de kurulamıyor.
            var minimap = FindFirstObjectByType<UI.Minimap>();
            if (minimap) minimap.target = _localCar.transform;

            // Interest management mesafeyi bu araca göre ölçer.
            if (NetworkInterestManager.Instance)
                NetworkInterestManager.Instance.SetLocalCar(_localCar.transform);

            // Sürücüyü somut tip yerine IDriveInput üzerinden alıyoruz: prefab
            // WheelCollider'lı CarController da olabilir, RCCP'li RCCPCarAdapter da.
            var input = FindFirstObjectByType<InputSystemMobile.MobileTouchInput>();
            if (input) input.car = _localCar.GetComponent<Car.IDriveInput>();
        }

        public override void OnLeftRoom()
        {
            _localCar = null;
            LocalCar = null;
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[Room] {otherPlayer.NickName} left.");
        }
    }
}
