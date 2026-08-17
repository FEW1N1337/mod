using Photon.Realtime;
using DreamCar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if PLAYFAB_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace DreamCar.Moderation
{
    public class ReportPlayer : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Dropdown reasonDropdown;
        public TMP_InputField detailField;
        public Button submitButton;
        public Button cancelButton;

        Player _target;

        void Awake()
        {
            if (panel) panel.SetActive(false);
            if (reasonDropdown)
            {
                reasonDropdown.ClearOptions();
                reasonDropdown.AddOptions(new System.Collections.Generic.List<string>
                { "Spam", "Küfür / Hakaret", "Hile", "Uygunsuz İsim / Plaka", "Diğer" });
            }
            if (submitButton) submitButton.onClick.AddListener(Submit);
            if (cancelButton) cancelButton.onClick.AddListener(Close);
        }

        public void Open(Player target)
        {
            _target = target;
            if (detailField) detailField.text = "";
            if (panel) panel.SetActive(true);
        }

        void Close()
        {
            if (panel) panel.SetActive(false);
            _target = null;
        }

        void Submit()
        {
            if (_target == null) { Close(); return; }

            string reason = reasonDropdown ? reasonDropdown.options[reasonDropdown.value].text : "?";
            string detail = detailField ? detailField.text : "";

#if PLAYFAB_INSTALLED
            var req = new ExecuteCloudScriptRequest
            {
                FunctionName = "submitReport",
                FunctionParameter = new
                {
                    targetPlayFabId = _target.UserId,
                    targetNickname = _target.NickName,
                    reason,
                    detail
                }
            };
            PlayFabClientAPI.ExecuteCloudScript(req,
                r => ToastNotification.Show("Rapor gönderildi. Teşekkürler."),
                err => ToastNotification.Show("Rapor gönderilemedi."));
#else
            ToastNotification.Show("Rapor kaydedildi (PlayFab offline)");
#endif
            Close();
        }
    }
}
