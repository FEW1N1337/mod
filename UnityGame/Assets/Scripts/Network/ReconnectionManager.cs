using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using DreamCar.UI;

namespace DreamCar.Network
{
    // Mobilde en kritik eksik: telefon uykuya girince / ağ 4G↔WiFi geçince Photon düşer.
    // Bu manager düşüşü yakalar, üstel geri çekilme ile yeniden bağlanır ve mümkünse
    // aynı odaya ReconnectAndRejoin ile geri girer.
    public class ReconnectionManager : MonoBehaviourPunCallbacks
    {
        public static ReconnectionManager Instance { get; private set; }

        [Header("Retry")]
        public int maxAttempts = 6;
        public float firstDelaySeconds = 2f;
        public float maxDelaySeconds = 30f;

        [Header("UI (opsiyonel)")]
        public GameObject reconnectingOverlay;
        public TMPro.TMP_Text statusLabel;

        int _attempt;
        string _lastRoomName;
        bool _retrying;
        bool _userInitiatedLeave;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => SetOverlay(false);

        // Oyuncu kendi isteğiyle çıkıyorsa reconnect denenmemeli.
        public void MarkUserInitiatedLeave() => _userInitiatedLeave = true;

        public override void OnJoinedRoom()
        {
            _lastRoomName = PhotonNetwork.CurrentRoom?.Name;
            _attempt = 0;
            _userInitiatedLeave = false;
            SetOverlay(false);
        }

        public override void OnLeftRoom()
        {
            if (_userInitiatedLeave) _lastRoomName = null;
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_userInitiatedLeave || IsUnrecoverable(cause))
            {
                Debug.Log($"[Reconnect] Yeniden denenmiyor (cause={cause}, userLeave={_userInitiatedLeave})");
                SetOverlay(false);
                return;
            }
            if (!_retrying) StartCoroutine(RetryLoop(cause));
        }

        static bool IsUnrecoverable(DisconnectCause cause) =>
            cause == DisconnectCause.DisconnectByClientLogic ||
            cause == DisconnectCause.DisconnectByServerLogic ||
            cause == DisconnectCause.InvalidAuthentication ||
            cause == DisconnectCause.CustomAuthenticationFailed ||
            cause == DisconnectCause.InvalidRegion ||
            cause == DisconnectCause.MaxCcuReached;

        IEnumerator RetryLoop(DisconnectCause cause)
        {
            _retrying = true;
            SetOverlay(true, "Bağlantı koptu, yeniden bağlanıyor…");

            float delay = firstDelaySeconds;
            for (_attempt = 1; _attempt <= maxAttempts; _attempt++)
            {
                SetOverlay(true, $"Yeniden bağlanıyor… ({_attempt}/{maxAttempts})");
                yield return new WaitForSecondsRealtime(delay);

                if (PhotonNetwork.IsConnectedAndReady) break;

                bool started = !string.IsNullOrEmpty(_lastRoomName)
                    ? PhotonNetwork.ReconnectAndRejoin()
                    : PhotonNetwork.Reconnect();

                if (!started) started = PhotonNetwork.ConnectUsingSettings();

                // Bağlantı sonucunu bekle (callback'ler durumu güncelleyecek).
                float waited = 0f;
                while (waited < 10f && !PhotonNetwork.IsConnectedAndReady)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (PhotonNetwork.IsConnectedAndReady)
                {
                    ToastNotification.Show("Yeniden bağlanıldı");
                    _attempt = 0;
                    _retrying = false;
                    SetOverlay(false);
                    yield break;
                }

                delay = Mathf.Min(delay * 2f, maxDelaySeconds);
            }

            _retrying = false;
            SetOverlay(true, "Bağlanılamadı. Ana menüye dön.");
            Debug.LogWarning($"[Reconnect] {maxAttempts} deneme başarısız (ilk cause={cause})");
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            // Oda artık yoksa sadece lobiye bağlan.
            _lastRoomName = null;
        }

        void SetOverlay(bool on, string text = null)
        {
            if (reconnectingOverlay) reconnectingOverlay.SetActive(on);
            if (statusLabel && text != null) statusLabel.text = text;
        }
    }
}
