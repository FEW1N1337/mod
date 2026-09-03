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
        public const string PwdKey = "pWd";
        public const string ModeKey = "mode";
        public const string MapKey = "map";
        static readonly string[] Lobby = { PwdKey, ModeKey, MapKey };

        public static void Register()
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.EnableLobbyStatistics = true;
        }

        public static void CreateWithPassword(string roomName, string password, byte maxPlayers = 16,
                                              int mode = 0, string mapId = null, bool visible = true)
        {
            var props = new Hashtable();
            if (!string.IsNullOrEmpty(password)) props[PwdKey] = password;
            props[ModeKey] = mode;
            if (!string.IsNullOrEmpty(mapId)) props[MapKey] = mapId;

            var opts = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsVisible = visible,
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
