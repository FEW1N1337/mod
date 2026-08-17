using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    public class ChatUI : MonoBehaviourPun
    {
        public TMP_InputField inputField;
        public Button sendButton;
        public TMP_Text messagesText;
        public int maxLines = 8;

        readonly Queue<string> _lines = new();

        void Start()
        {
            if (sendButton) sendButton.onClick.AddListener(Send);
            if (inputField) inputField.onSubmit.AddListener(_ => Send());
        }

        void Send()
        {
            if (!inputField || string.IsNullOrWhiteSpace(inputField.text)) return;
            if (!PhotonNetwork.InRoom) return;

            string msg = inputField.text.Trim();
            if (msg.Length > 120) msg = msg.Substring(0, 120);
            photonView.RPC(nameof(ReceiveChat), RpcTarget.All, PhotonNetwork.NickName, msg);
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        [PunRPC]
        void ReceiveChat(string sender, string message)
        {
            _lines.Enqueue($"<b>{sender}</b>: {message}");
            while (_lines.Count > maxLines) _lines.Dequeue();
            if (messagesText) messagesText.text = string.Join("\n", _lines);
        }
    }
}
