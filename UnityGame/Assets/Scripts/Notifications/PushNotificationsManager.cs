// Firebase Cloud Messaging entegrasyonu. Aktive etmek için:
//   1) Firebase Unity SDK (Messaging) import
//   2) iOS: GoogleService-Info.plist projeye eklenmeli, APNs Auth Key Firebase konsoluna yüklenmeli
//   3) Player Settings → Scripting Define Symbols → FIREBASE_MESSAGING
using System;
using UnityEngine;

#if FIREBASE_MESSAGING
using Firebase.Messaging;
#endif

namespace DreamCar.Notifications
{
    public class PushNotificationsManager : MonoBehaviour
    {
        public static PushNotificationsManager Instance { get; private set; }

        public bool requestPermissionOnStart = true;
        public event Action<string> OnDeepLink;

        public string FcmToken { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (requestPermissionOnStart) Initialize();
        }

        public void Initialize()
        {
#if FIREBASE_MESSAGING
            FirebaseMessaging.TokenReceived += OnTokenReceived;
            FirebaseMessaging.MessageReceived += OnMessageReceived;
            FirebaseMessaging.RequestPermissionAsync().ContinueWith(t =>
            {
                if (t.IsFaulted) Debug.LogWarning("[Push] İzin alınamadı: " + t.Exception);
            });
#else
            Debug.Log("[Push] Firebase Messaging yüklü değil.");
#endif
        }

#if FIREBASE_MESSAGING
        void OnDestroy()
        {
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
        }

        void OnTokenReceived(object sender, TokenReceivedEventArgs e)
        {
            FcmToken = e.Token;
            Debug.Log("[Push] Token alındı.");
            PublishTokenToBackend(e.Token);
        }

        void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var n = e.Message.Notification;
            if (n != null) Debug.Log($"[Push] Bildirim: {n.Title} — {n.Body}");

            // data payload'ında "link" varsa deep link akışını tetikle.
            if (e.Message.Data != null && e.Message.Data.TryGetValue("link", out var link))
                OnDeepLink?.Invoke(link);
        }
#endif

        // Token'ı PlayFab profiline yaz → sunucu tarafı hedefli bildirim gönderebilsin.
        void PublishTokenToBackend(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            PlayerPrefs.SetString("push.token", token);
            PlayerPrefs.Save();

            if (Backend.PlayFabCloudSave.Instance != null)
                Backend.PlayFabCloudSave.Instance.MarkDirty();
        }
    }
}
