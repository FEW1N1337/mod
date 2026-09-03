using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.Moderation
{
    // Master client'in ban listesi. PlayerPrefs'te tutulur. Ban'lı UserId odaya girmeye
    // çalışırsa master onu otomatik kick eder.
    public class BanList : MonoBehaviourPunCallbacks
    {
        public static BanList Instance { get; private set; }
        const string Key = "banned.userIds";

        readonly HashSet<string> _banned = new();

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public bool IsBanned(string userId) => !string.IsNullOrEmpty(userId) && _banned.Contains(userId);

        public void Ban(Player p)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId)) return;
            _banned.Add(p.UserId);
            Save();
            Monetization.Analytics.Event("player_banned", new() { { "nickname", p.NickName ?? "-" } });
            if (PhotonNetwork.IsMasterClient) PhotonNetwork.CloseConnection(p);
        }

        public void Unban(string userId)
        {
            if (_banned.Remove(userId)) Save();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (IsBanned(newPlayer.UserId)) PhotonNetwork.CloseConnection(newPlayer);
        }

        // Yalnızca "sonradan giren" kontrol ediliyordu. Odaya master olarak sonradan
        // girildiğinde ya da master devredildiğinde İÇERİDE zaten duran banlı bir
        // oyuncu hiç atılmıyordu. Bu iki anda listeyi bir kez tarıyoruz.
        public override void OnJoinedRoom() => SweepBanned();
        public override void OnMasterClientSwitched(Player newMaster) => SweepBanned();

        void SweepBanned()
        {
            if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;

            // Sözlüğün kopyası üzerinde dönüyoruz: CloseConnection oyuncu listesini
            // değiştirebilir ve dönerken değişen koleksiyon istisna atardı.
            var players = new List<Player>(PhotonNetwork.CurrentRoom.Players.Values);
            foreach (var p in players)
                if (p != null && IsBanned(p.UserId)) PhotonNetwork.CloseConnection(p);
        }

        void Load()
        {
            _banned.Clear();
            var raw = PlayerPrefs.GetString(Key, "");
            foreach (var s in raw.Split(',')) if (!string.IsNullOrWhiteSpace(s)) _banned.Add(s.Trim());
        }

        void Save()
        {
            PlayerPrefs.SetString(Key, string.Join(",", _banned));
            PlayerPrefs.Save();
        }
    }
}
