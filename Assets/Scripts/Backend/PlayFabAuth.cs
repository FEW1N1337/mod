// PlayFab Unity SDK — Asset Store'dan "PlayFab SDK" ücretsiz import.
// Aktive etmek için: Player Settings → Scripting Define Symbols → PLAYFAB_INSTALLED
using System;
using UnityEngine;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Backend
{
    public class PlayFabAuth : MonoBehaviour
    {
        public static PlayFabAuth Instance { get; private set; }
        public string titleId = "";
        public event Action OnLoggedIn;
        public bool IsLoggedIn { get; private set; }
        public string PlayFabId { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => Login();

        public void Login()
        {
#if PLAYFAB_INSTALLED
            if (!string.IsNullOrEmpty(titleId)) PlayFabSettings.staticSettings.TitleId = titleId;
            string customId = PlayerPrefs.GetString("playfab.customId", "");
            if (string.IsNullOrEmpty(customId))
            {
                customId = SystemInfo.deviceUniqueIdentifier + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString("playfab.customId", customId);
            }
            var req = new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = true };
            PlayFabClientAPI.LoginWithCustomID(req, r =>
            {
                IsLoggedIn = true;
                PlayFabId = r.PlayFabId;
                Debug.Log("[PlayFab] Login OK: " + PlayFabId);
                Monetization.Analytics.Event("login", new()
                {
                    { "newly_created", r.NewlyCreated },
                    { "platform", Application.platform.ToString() },
                });
                OnLoggedIn?.Invoke();
            }, err => Debug.LogError("[PlayFab] Login failed: " + err.GenerateErrorReport()));
#else
            Debug.Log("[PlayFab] SDK not installed.");
#endif
        }
    }
}
