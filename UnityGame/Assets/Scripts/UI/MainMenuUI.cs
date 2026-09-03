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
            if (nicknameInput) nicknameInput.text = NicknameManager.Load();
            NicknameManager.Apply();

            if (playButton) playButton.onClick.AddListener(OnPlay);
            if (statusText) statusText.text = "Connecting...";
        }

        void Update()
        {
            bool online = PhotonConnector.Instance && PhotonConnector.Instance.IsConnected;

            // Eskiden "if (!statusText) return;" tek erken çıkışı playButton satırını da
            // kesiyordu: statusText bağlı olmadığında OYNA butonu hiç aktifleşmiyor,
            // oyuncu oyuna giremiyordu. İki alan artık ayrı ayrı korunuyor.
            if (statusText) statusText.text = online ? "Online" : "Connecting...";
            if (playButton) playButton.interactable = online;
        }

        void OnPlay()
        {
            if (nicknameInput) NicknameManager.Save(nicknameInput.text);
            if (lobbyPanel) lobbyPanel.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
