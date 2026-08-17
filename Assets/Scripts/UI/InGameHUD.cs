using DreamCar.Car;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    public class InGameHUD : MonoBehaviour
    {
        public TMP_Text speedText;
        public TMP_Text playerCountText;
        public TMP_Text roomNameText;
        public Button leaveButton;

        CarController _car;

        void Start()
        {
            if (leaveButton) leaveButton.onClick.AddListener(Leave);
        }

        void Update()
        {
            if (!_car)
            {
                foreach (var c in FindObjectsByType<CarController>(FindObjectsSortMode.None))
                {
                    var pv = c.GetComponent<PhotonView>();
                    if (pv && pv.IsMine) { _car = c; break; }
                }
            }

            if (speedText) speedText.text = _car ? $"{Mathf.RoundToInt(_car.SpeedKmh)} km/h" : "-- km/h";
            if (playerCountText && PhotonNetwork.CurrentRoom != null)
                playerCountText.text = $"{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
            if (roomNameText && PhotonNetwork.CurrentRoom != null)
                roomNameText.text = PhotonNetwork.CurrentRoom.Name;
        }

        void Leave()
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.LoadLevel("MainMenu");
        }
    }
}
