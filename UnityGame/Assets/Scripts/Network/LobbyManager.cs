using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.Network
{
    public class LobbyManager : MonoBehaviourPunCallbacks
    {
        public static LobbyManager Instance { get; private set; }

        public byte defaultMaxPlayers = 16;
        public string gameSceneName = "Game";

        public readonly Dictionary<string, RoomInfo> Rooms = new();
        public System.Action OnRoomListChanged;
        public System.Action<short, string> OnJoinFailed;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void CreateRoom(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) roomName = $"Room-{Random.Range(1000, 9999)}";
            var opts = new RoomOptions
            {
                MaxPlayers = defaultMaxPlayers,
                IsVisible = true,
                IsOpen = true,
                PublishUserId = true
            };
            PhotonNetwork.CreateRoom(roomName, opts, TypedLobby.Default);
        }

        public void JoinRoom(string roomName)
        {
            PhotonNetwork.JoinRoom(roomName);
        }

        public void JoinRandom()
        {
            PhotonNetwork.JoinRandomRoom();
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (var r in roomList)
            {
                if (r.RemovedFromList || !r.IsVisible || !r.IsOpen) Rooms.Remove(r.Name);
                else Rooms[r.Name] = r;
            }
            OnRoomListChanged?.Invoke();
        }

        public override void OnJoinedRoom()
        {
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel(gameSceneName);
        }

        public override void OnCreateRoomFailed(short returnCode, string message) => OnJoinFailed?.Invoke(returnCode, message);
        public override void OnJoinRoomFailed(short returnCode, string message) => OnJoinFailed?.Invoke(returnCode, message);
        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            CreateRoom(null);
        }
    }
}
