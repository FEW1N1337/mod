using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using DreamCar.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Son 20 oyuncu isim+UserId cache. Odadan çıktıktan sonra ana menüde liste görülür,
    // her satırın "Arkadaş Ekle" butonu var.
    public class PlayedWithList : MonoBehaviourPunCallbacks
    {
        public static PlayedWithList Instance { get; private set; }
        public int maxKeep = 20;

        [System.Serializable]
        public class Entry { public string userId; public string nickname; public string lastRoom; public long lastSeen; }

        public readonly List<Entry> Entries = new();

        [Header("UI (opsiyonel)")]
        public Transform listParent;
        public GameObject entryPrefab;

        const string Key = "playedwith.v1";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) => Record(newPlayer);
        public override void OnJoinedRoom()
        {
            foreach (var kv in PhotonNetwork.CurrentRoom.Players)
                if (!kv.Value.IsLocal) Record(kv.Value);
        }

        void Record(Player p)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId)) return;
            Entries.RemoveAll(e => e.userId == p.UserId);
            Entries.Insert(0, new Entry
            {
                userId = p.UserId,
                nickname = p.NickName,
                lastRoom = PhotonNetwork.CurrentRoom?.Name ?? "?",
                lastSeen = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            while (Entries.Count > maxKeep) Entries.RemoveAt(Entries.Count - 1);
            Save();
        }

        void OnEnable() => Refresh();

        public void Refresh()
        {
            if (!listParent || !entryPrefab) return;
            for (int i = listParent.childCount - 1; i >= 0; i--) Destroy(listParent.GetChild(i).gameObject);

            foreach (var e in Entries)
            {
                var go = Instantiate(entryPrefab, listParent);
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label) label.text = $"{e.nickname}  <size=70%>({e.lastRoom})</size>";

                var btn = go.GetComponentInChildren<Button>();
                if (btn)
                {
                    string id = e.userId;
                    btn.onClick.AddListener(() => PlayFabFriends.Instance?.AddByPlayFabId(id));
                }
            }
        }

        void Save()
        {
            var payload = JsonUtility.ToJson(new Wrapper { entries = Entries });
            PlayerPrefs.SetString(Key, payload);
            PlayerPrefs.Save();
        }

        void Load()
        {
            var raw = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(raw)) return;
            try { var w = JsonUtility.FromJson<Wrapper>(raw); if (w?.entries != null) Entries.AddRange(w.entries); }
            catch { /* ignore corrupt cache */ }
        }

        [System.Serializable] class Wrapper { public List<Entry> entries; }
    }
}
