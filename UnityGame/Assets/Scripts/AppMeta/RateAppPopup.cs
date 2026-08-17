using DreamCar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.AppMeta
{
    // N yarış sonrasında "beğendin mi?" popup. Evet → App Store. Hayır → geri bildirim.
    // "Bir daha sorma" flag kaydedilir.
    public class RateAppPopup : MonoBehaviour
    {
        public static RateAppPopup Instance { get; private set; }

        public int triggerAfterRaces = 5;
        public string iosAppId = "0000000000";
        public GameObject popup;
        public TMP_Text prompt;
        public Button yesButton;
        public Button noButton;
        public Button neverButton;
        public GameObject feedbackPanel;
        public TMP_InputField feedbackField;
        public Button feedbackSendButton;

        const string RaceCountKey = "rate.raceCount";
        const string DoneKey = "rate.done.v1";
        const string NeverKey = "rate.never.v1";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (popup) popup.SetActive(false);
            if (feedbackPanel) feedbackPanel.SetActive(false);
            if (yesButton) yesButton.onClick.AddListener(OpenStore);
            if (noButton) noButton.onClick.AddListener(ShowFeedback);
            if (neverButton) neverButton.onClick.AddListener(NeverAgain);
            if (feedbackSendButton) feedbackSendButton.onClick.AddListener(SendFeedback);
        }

        public void OnRaceFinished()
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) return;
            if (PlayerPrefs.GetInt(NeverKey, 0) == 1) return;

            int count = PlayerPrefs.GetInt(RaceCountKey, 0) + 1;
            PlayerPrefs.SetInt(RaceCountKey, count);
            PlayerPrefs.Save();

            if (count >= triggerAfterRaces && popup) popup.SetActive(true);
        }

        void OpenStore()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
#if UNITY_IOS
            Application.OpenURL($"itms-apps://itunes.apple.com/app/id{iosAppId}?action=write-review");
#elif UNITY_ANDROID
            Application.OpenURL($"market://details?id={Application.identifier}");
#endif
            if (popup) popup.SetActive(false);
        }

        void ShowFeedback()
        {
            if (popup) popup.SetActive(false);
            if (feedbackPanel) feedbackPanel.SetActive(true);
        }

        void NeverAgain()
        {
            PlayerPrefs.SetInt(NeverKey, 1);
            PlayerPrefs.Save();
            if (popup) popup.SetActive(false);
        }

        void SendFeedback()
        {
            string text = feedbackField ? feedbackField.text : "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                PlayerPrefs.SetString("rate.feedback", text);
                PlayerPrefs.Save();
                ToastNotification.Show("Geri bildirim gönderildi. Teşekkürler.");
            }
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            if (feedbackPanel) feedbackPanel.SetActive(false);
        }
    }
}
