using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.Network
{
    public class RoomManager : MonoBehaviourPunCallbacks
    {
        [Tooltip("Prefab name that lives under Assets/Resources/ (Photon looks it up by name).")]
        public string carPrefabName = "Car";

        public Transform[] spawnPoints;

        GameObject _localCar;

        void Start()
        {
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

            _localCar = PhotonNetwork.Instantiate(carPrefabName, pos, rot);

            var follow = Camera.main ? Camera.main.GetComponent<Car.CarCameraFollow>() : null;
            if (follow) follow.target = _localCar.transform;

            var input = FindFirstObjectByType<InputSystemMobile.MobileTouchInput>();
            if (input) input.car = _localCar.GetComponent<Car.CarController>();
        }

        public override void OnLeftRoom()
        {
            _localCar = null;
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[Room] {otherPlayer.NickName} left.");
        }
    }
}
