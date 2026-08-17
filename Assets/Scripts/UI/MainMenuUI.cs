using DreamCar.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        public TMP_InputField nicknameInput;
        public Button playButton;
        public TMP_Text statusText;
        public GameObject lobbyPanel;

        void Start()
        {
            nicknameInput.text = NicknameManager.Load();
            NicknameManager.Apply();

            playButton.onClick.AddListener(OnPlay);
            if (statusText) statusText.text = "Connecting...";
        }

        void Update()
        {
            if (!statusText) return;
            statusText.text = PhotonConnector.Instance && PhotonConnector.Instance.IsConnected
                ? "Online"
                : "Connecting...";
            playButton.interactable = PhotonConnector.Instance && PhotonConnector.Instance.IsConnected;
        }

        void OnPlay()
        {
            NicknameManager.Save(nicknameInput.text);
            if (lobbyPanel) lobbyPanel.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
