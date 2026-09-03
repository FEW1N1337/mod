using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DreamCar.AppMeta
{
    // KVKK ekranından açılan gizlilik politikası popup'ı. İki mod:
    // (a) URL varsa Application.OpenURL,
    // (b) yoksa dahili TextAsset içeriği panel'de gösterilir.
    public class PrivacyPolicyScreen : MonoBehaviour
    {
        public string webUrl = "";
        public TextAsset fallbackText;
        public GameObject panel;
        public TMP_Text bodyLabel;
        public Button closeButton;
        public Button openLinkButton;

        void Start()
        {
            if (panel) panel.SetActive(false);
            if (closeButton) closeButton.onClick.AddListener(() => panel?.SetActive(false));
            if (openLinkButton) openLinkButton.onClick.AddListener(OpenLink);
        }

        public void Show()
        {
            if (!string.IsNullOrEmpty(webUrl))
            {
                OpenLink();
                return;
            }
            if (panel) panel.SetActive(true);
            if (bodyLabel) bodyLabel.text = fallbackText ? fallbackText.text : "Gizlilik politikası metni eklenmemiş.";
        }

        void OpenLink()
        {
            if (string.IsNullOrEmpty(webUrl)) return;
            Application.OpenURL(webUrl);
        }
    }
}
