using System;
using System.Collections.Generic;
using UnityEngine;
using DreamCar.Backend;
using DreamCar.UI;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Social
{
    public class PlayFabFriends : MonoBehaviour
    {
        public static PlayFabFriends Instance { get; private set; }

        public class FriendInfo
        {
            public string playFabId;
            public string nickname;
            public bool online;
            public string currentRoom;
        }

        public event Action OnFriendsUpdated;
        public readonly List<FriendInfo> Friends = new();

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
            if (PlayFabAuth.Instance != null) PlayFabAuth.Instance.OnLoggedIn += Refresh;
        }

        public void AddByNickname(string nickname)
        {
#if PLAYFAB_INSTALLED
            var req = new AddFriendRequest { FriendTitleDisplayName = nickname };
            PlayFabClientAPI.AddFriend(req, r => { if (r.Created) { ToastNotification.Show("Arkadaş eklendi"); Refresh(); } },
                err => ToastNotification.Show("Ekleme hatası: " + err.ErrorMessage));
#endif
        }

        public void AddByPlayFabId(string playFabId)
        {
#if PLAYFAB_INSTALLED
            var req = new AddFriendRequest { FriendPlayFabId = playFabId };
            PlayFabClientAPI.AddFriend(req, r => { if (r.Created) { ToastNotification.Show("Arkadaş eklendi"); Refresh(); } },
                err => ToastNotification.Show("Ekleme hatası: " + err.ErrorMessage));
#endif
        }

        public void Remove(string playFabId)
        {
#if PLAYFAB_INSTALLED
            var req = new RemoveFriendRequest { FriendPlayFabId = playFabId };
            PlayFabClientAPI.RemoveFriend(req, _ => Refresh(), err => Debug.LogWarning(err.ErrorMessage));
#endif
        }

        public void Refresh()
        {
#if PLAYFAB_INSTALLED
            var req = new GetFriendsListRequest { IncludeSteamFriends = false, IncludeFacebookFriends = false };
            PlayFabClientAPI.GetFriendsList(req, r =>
            {
                Friends.Clear();
                foreach (var f in r.Friends)
                {
                    Friends.Add(new FriendInfo
                    {
                        playFabId = f.FriendPlayFabId,
                        nickname = f.TitleDisplayName ?? f.FriendPlayFabId
                    });
                }
                OnFriendsUpdated?.Invoke();
            }, err => Debug.LogWarning("[Friends] " + err.ErrorMessage));
#endif
        }
    }
}
