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
            if (Instance && Instance != this) { Destroy(gameObject); return; }
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
