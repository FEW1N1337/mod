using System;
using Photon.Pun;
using UnityEngine;
using DreamCar.UI;

namespace DreamCar.Core
{
    // Davet linki yoktu — referral kodu vardı ama paylaşılamıyordu.
    // dreamcar://room/<odaAdı>?pwd=<şifre>  ve  dreamcar://ref/<kod>
    // formatlarını karşılar; hem soğuk başlatmayı hem çalışırken gelen linki yakalar.
    public class DeepLinkManager : MonoBehaviour
    {
        public static DeepLinkManager Instance { get; private set; }

        public string scheme = "dreamcar";
        public string webFallbackHost = "dreamcar.example";

        public event Action<string, string> OnRoomInvite;   // roomName, password
        public event Action<string> OnReferral;             // code

        public string PendingRoom { get; private set; }
        public string PendingRoomPassword { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.deepLinkActivated += OnDeepLinkActivated;

            // Uygulama link ile soğuk başlatıldıysa.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLinkActivated(Application.absoluteURL);
        }

        void OnDestroy() => Application.deepLinkActivated -= OnDeepLinkActivated;

        void OnDeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Debug.Log("[DeepLink] " + url);

            try { Parse(url); }
            catch (Exception e) { Debug.LogWarning("[DeepLink] Parse hatası: " + e.Message); }
        }

        void Parse(string url)
        {
            var uri = new Uri(url);

            // dreamcar://room/ABC  → Host="room", Segments=["/","ABC"]
            // https://host/room/ABC → Host=host,  Segments=["/","room/","ABC"]
            string kind;
            string value;

            if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
            {
                kind = uri.Host;
                value = uri.AbsolutePath.Trim('/');
            }
            else
            {
                var parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length < 2) return;
                kind = parts[0];
                value = parts[1];
            }

            if (string.IsNullOrEmpty(value)) return;

            switch (kind.ToLowerInvariant())
            {
                case "room":
                    PendingRoom = Uri.UnescapeDataString(value);
                    PendingRoomPassword = QueryValue(uri.Query, "pwd");
                    ToastNotification.Show($"Davet: {PendingRoom}");
                    OnRoomInvite?.Invoke(PendingRoom, PendingRoomPassword);
                    TryJoinPending();
                    break;

                case "ref":
                    OnReferral?.Invoke(Uri.UnescapeDataString(value));
                    break;
            }
        }

        // Photon hazırsa hemen katıl; değilse bağlanınca çağırılabilir.
        public bool TryJoinPending()
        {
            if (string.IsNullOrEmpty(PendingRoom)) return false;
            if (!PhotonNetwork.IsConnectedAndReady) return false;

            PhotonNetwork.JoinRoom(PendingRoom);
            PendingRoom = null;
            PendingRoomPassword = null;
            return true;
        }

        // --- Paylaşım linki üretimi ---
        public string BuildRoomLink(string roomName, string password = null)
        {
            var encoded = Uri.EscapeDataString(roomName ?? "");
            var link = $"https://{webFallbackHost}/room/{encoded}";
            if (!string.IsNullOrEmpty(password)) link += "?pwd=" + Uri.EscapeDataString(password);
            return link;
        }

        public string BuildReferralLink(string code) =>
            $"https://{webFallbackHost}/ref/{Uri.EscapeDataString(code ?? "")}";

        // Panoya kopyala — iOS'ta sistem paylaşım sayfası native plugin ister,
        // pano herkes için çalışan en basit yol.
        public void CopyToClipboard(string text, string toast = "Link kopyalandı")
        {
            GUIUtility.systemCopyBuffer = text ?? "";
            ToastNotification.Show(toast);
        }

        public void ShareCurrentRoom()
        {
            if (!PhotonNetwork.InRoom) { ToastNotification.Show("Odada değilsin"); return; }
            var pwd = Network.RoomPassword.GetPassword(PhotonNetwork.CurrentRoom);
            CopyToClipboard(BuildRoomLink(PhotonNetwork.CurrentRoom.Name, pwd), "Oda linki kopyalandı");
        }

        public void ShareReferral()
        {
            var code = PlayerPrefs.GetString("referral.myCode", "");
            if (string.IsNullOrEmpty(code)) { ToastNotification.Show("Kod yok"); return; }
            CopyToClipboard(BuildReferralLink(code), "Davet linki kopyalandı");
        }

        static string QueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;
            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                var idx = pair.IndexOf('=');
                if (idx <= 0) continue;
                if (pair.Substring(0, idx) == key) return Uri.UnescapeDataString(pair.Substring(idx + 1));
            }
            return null;
        }
    }
}
