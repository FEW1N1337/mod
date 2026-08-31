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
            if (string.IsNullOrWhiteSpace(n))
            {
                // Üretilen varsayılan ad hemen kalıcı yazılmalı. Aksi halde her Load()
                // çağrısı farklı bir "PlayerXXXX" döndürüyordu: GameBootstrap.Apply(),
                // MainMenuUI'daki isim kutusu ve tekrar Apply() üç ayrı ad üretiyor,
                // ekranda görünen ad ile PhotonNetwork.NickName birbirini tutmuyordu.
                n = $"Player{Random.Range(1000, 9999)}";
                PlayerPrefs.SetString(Key, n);
                PlayerPrefs.Save();
            }
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
