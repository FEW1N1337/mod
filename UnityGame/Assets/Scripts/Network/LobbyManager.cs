using System.Collections.Generic;
using DreamCar.UI;
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
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
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

        [Tooltip("Harita kataloğu. Oda 'map' özelliğini taşıyorsa o haritanın sahnesi " +
                 "yüklenir; katalog yoksa ya da eşleşme bulunamazsa gameSceneName'e düşülür.")]
        public Maps.MapCatalog mapCatalog;

        public override void OnJoinedRoom()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // Eskiden HER ZAMAN sabit "Game" sahnesi yükleniyordu. Sonucu: üretilen
            // sekiz harita sahnesinin hiçbiri hiç açılmıyordu ve oda kurarken yapılan
            // harita seçimi tamamen anlamsızdı — MapSelector.LoadMap'in de projede
            // hiçbir çağıranı yok. Oda özelliğindeki haritanın sahnesini yüklüyoruz.
            PhotonNetwork.LoadLevel(ResolveSceneName());
        }

        string ResolveSceneName()
        {
            if (!mapCatalog) return gameSceneName;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                    Maps.MapSelector.MapPropKey, out object idObj)) return gameSceneName;

            var def = mapCatalog.Find(idObj as string);
            if (!def || string.IsNullOrEmpty(def.sceneName)) return gameSceneName;

            // Sahne Build Settings'te yoksa LoadLevel sessizce başarısız olur ve
            // oyuncular boş bir odada asılı kalırdı; o durumda bilinen sahneye dön.
            if (!Application.CanStreamedLevelBeLoaded(def.sceneName))
            {
                Debug.LogWarning($"[Lobby] '{def.sceneName}' Build Settings'te yok, " +
                                 $"'{gameSceneName}' yükleniyor.");
                return gameSceneName;
            }
            return def.sceneName;
        }

        // OnJoinFailed event'ine projede hiçbir yerde abone olunmuyor (LobbyUI sadece
        // OnRoomListChanged'e bağlanıyor). Yani oda kurulamadığında / girilemediğinde
        // oyuncuya hiçbir şey görünmüyor, konsola log bile düşmüyordu: buton basılıyor
        // ve hiçbir şey olmuyor. Event'i koruyup hatayı burada da görünür kılıyoruz.
        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[Lobby] Oda oluşturulamadı ({returnCode}): {message}");
            ToastNotification.Show($"Oda oluşturulamadı: {message}");
            OnJoinFailed?.Invoke(returnCode, message);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[Lobby] Odaya girilemedi ({returnCode}): {message}");
            ToastNotification.Show($"Odaya girilemedi: {message}");
            OnJoinFailed?.Invoke(returnCode, message);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            CreateRoom(null);
        }

        // Photon oda listesini yalnızca lobideyken ve artımlı (sadece değişenler)
        // gönderir. Lobiden çıkınca / bağlantı kopunca elimizdeki sözlük bayatlıyordu;
        // temizlenmediği için kapanmış odalar listede kalıyor, tıklanınca da yukarıdaki
        // hata yolundan sessizce dönülüyordu.
        public override void OnLeftLobby() => ClearRooms();

        public override void OnDisconnected(DisconnectCause cause) => ClearRooms();

        void ClearRooms()
        {
            if (Rooms.Count == 0) return;
            Rooms.Clear();
            OnRoomListChanged?.Invoke();
        }
    }
}
