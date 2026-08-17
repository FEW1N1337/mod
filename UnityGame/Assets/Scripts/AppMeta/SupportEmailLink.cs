using UnityEngine;
using UnityEngine.UI;
using DreamCar.Backend;

namespace DreamCar.AppMeta
{
    // Ayarlar → Destek butonu. mailto: link. Konu satırı otomatik doldurulur
    // (sürüm + cihaz + PlayFabId) → destek biletlerini eşleştirmek kolaylaşır.
    public class SupportEmailLink : MonoBehaviour
    {
        public Button supportButton;
        public string toAddress = "support@dreamcar.example";

        void Start()
        {
            if (supportButton) supportButton.onClick.AddListener(Open);
        }

        public void Open()
        {
            string version = Application.version;
            string device = SystemInfo.deviceModel;
            string os = SystemInfo.operatingSystem;
            string playFabId = PlayFabAuth.Instance ? PlayFabAuth.Instance.PlayFabId ?? "-" : "-";

            string subject = System.Uri.EscapeDataString($"[DreamCar {version}] Destek Talebi");
            string body = System.Uri.EscapeDataString(
                $"Lütfen sorunuzu buraya yazın.\n\n---\nSürüm: {version}\nCihaz: {device}\nOS: {os}\nID: {playFabId}\n");

            Application.OpenURL($"mailto:{toAddress}?subject={subject}&body={body}");
        }
    }
}
