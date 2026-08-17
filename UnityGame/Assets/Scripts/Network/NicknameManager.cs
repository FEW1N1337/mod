using Photon.Pun;
using UnityEngine;

namespace DreamCar.Network
{
    public static class NicknameManager
    {
        const string Key = "player.nickname";
        const int MaxLen = 16;

        public static string Load()
        {
            string n = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrWhiteSpace(n)) n = $"Player{Random.Range(1000, 9999)}";
            return n;
        }

        public static void Save(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return;
            if (nickname.Length > MaxLen) nickname = nickname.Substring(0, MaxLen);
            PlayerPrefs.SetString(Key, nickname);
            PlayerPrefs.Save();
            PhotonNetwork.NickName = nickname;
        }

        public static void Apply()
        {
            PhotonNetwork.NickName = Load();
        }
    }
}
