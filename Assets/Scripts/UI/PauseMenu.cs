using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // ESC/mobil buton ile duraklat. Time.timeScale=0. Multiplayer'da sadece yerel HUD
    // durmuş görünür — sunucu ve diğer client'lar akmaya devam eder (Photon senkron kaymaz).
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;
        public Button resumeButton;
        public Button settingsButton;
        public Button leaveRoomButton;
        public Button mainMenuButton;
        public GameObject settingsPanel;
        public KeyCode toggleKey = KeyCode.Escape;

        bool _paused;

        void Start()
        {
            if (panel) panel.SetActive(false);
            if (resumeButton) resumeButton.onClick.AddListener(Resume);
            if (settingsButton) settingsButton.onClick.AddListener(() => { if (settingsPanel) settingsPanel.SetActive(true); });
            if (leaveRoomButton) leaveRoomButton.onClick.AddListener(LeaveRoom);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(GoMainMenu);
        }

        void Update() { if (Input.GetKeyDown(toggleKey)) Toggle(); }

        public void Toggle() { if (_paused) Resume(); else Pause(); }

        public void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            if (panel) panel.SetActive(true);
        }

        public void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;
            if (panel) panel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
        }

        void LeaveRoom()
        {
            Time.timeScale = 1f;
            MarkIntentionalLeave();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        }

        void GoMainMenu()
        {
            Time.timeScale = 1f;
            MarkIntentionalLeave();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("MainMenu");
        }

        static void MarkIntentionalLeave()
        {
            if (Network.ReconnectionManager.Instance)
                Network.ReconnectionManager.Instance.MarkUserInitiatedLeave();
        }
    }
}
