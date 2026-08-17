using System.Collections.Generic;
using System.Text.RegularExpressions;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // TMP rich text + emoji sprite atlas destekli chat. ChatUI'nin yerine kullanılır.
    // Emoji token'ları :grin: gibi kısa isimler → TMP sprite index. Renk için <color=...>.
    // Zararlı/aşırı büyük tag'leri filtreler (size 9999% gibi).
    public class RichChatUI : MonoBehaviourPun
    {
        public TMP_InputField inputField;
        public Button sendButton;
        public TMP_Text messagesText;
        public int maxLines = 10;
        public int maxLength = 120;
        public string emojiSpriteAssetName = "DreamCarEmojis";

        readonly Queue<string> _lines = new();
        static readonly Regex UnsafeSize = new(@"<size\s*=\s*[0-9]*[0-9]{4,}", RegexOptions.IgnoreCase);
        static readonly Regex EmojiToken = new(@":([a-z0-9_]{1,16}):", RegexOptions.IgnoreCase);

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
            if (msg.Length > maxLength) msg = msg.Substring(0, maxLength);
            msg = Sanitize(msg);
            photonView.RPC(nameof(RPC_Receive), RpcTarget.All, PhotonNetwork.NickName, msg);
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        [PunRPC]
        void RPC_Receive(string sender, string message)
        {
            string safe = Sanitize(message);
            safe = EmojiToken.Replace(safe, m => $"<sprite=\"{emojiSpriteAssetName}\" name=\"{m.Groups[1].Value}\">");
            _lines.Enqueue($"<b>{sender}</b>: {safe}");
            while (_lines.Count > maxLines) _lines.Dequeue();
            if (messagesText) messagesText.text = string.Join("\n", _lines);
        }

        static string Sanitize(string s) => string.IsNullOrEmpty(s) ? "" : UnsafeSize.Replace(s, "<size=100%");
    }
}
