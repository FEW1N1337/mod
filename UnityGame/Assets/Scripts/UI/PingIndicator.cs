using Photon.Pun;
using TMPro;
using UnityEngine;

namespace DreamCar.UI
{
    public class PingIndicator : MonoBehaviour
    {
        public TMP_Text label;
        public float updateInterval = 0.5f;
        float _next;

        void Update()
        {
            if (Time.time < _next || !label) return;
            _next = Time.time + updateInterval;
            int ping = PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() : -1;
            label.text = ping >= 0 ? $"{ping} ms" : "--";
            label.color = ping < 80 ? Color.green : ping < 180 ? Color.yellow : Color.red;
        }
    }
}
