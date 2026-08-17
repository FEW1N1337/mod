using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.Network
{
    // Dream Road'daki "pWd" custom property mantığı. Master oda oluşturur, password'ü
    // custom prop olarak yayımlar. Katılmak isteyen aynı password'ü girmeli — yoksa reddedilir.
    public static class RoomPassword
    {
        const string PwdKey = "pWd";
        static readonly string[] Lobby = { PwdKey };

        public static void Register()
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.EnableLobbyStatistics = true;
        }

        public static void CreateWithPassword(string roomName, string password, byte maxPlayers = 16)
        {
            var props = new Hashtable();
            if (!string.IsNullOrEmpty(password)) props[PwdKey] = password;

            var opts = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsVisible = true,
                IsOpen = true,
                CustomRoomProperties = props,
                CustomRoomPropertiesForLobby = Lobby
            };
            PhotonNetwork.CreateRoom(roomName, opts, TypedLobby.Default);
        }

        public static bool IsPasswordProtected(RoomInfo room) =>
            room.CustomProperties.ContainsKey(PwdKey) && !string.IsNullOrEmpty(room.CustomProperties[PwdKey] as string);

        public static string GetPassword(RoomInfo room) =>
            room.CustomProperties.TryGetValue(PwdKey, out object v) ? v as string : null;

        public static bool TryJoin(RoomInfo room, string enteredPassword)
        {
            if (!IsPasswordProtected(room)) { PhotonNetwork.JoinRoom(room.Name); return true; }
            if (GetPassword(room) == enteredPassword) { PhotonNetwork.JoinRoom(room.Name); return true; }
            Debug.LogWarning("[Room] Wrong password.");
            return false;
        }
    }
}
