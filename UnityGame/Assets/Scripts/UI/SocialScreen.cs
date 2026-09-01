using DreamCar.Core;
using DreamCar.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Referans kodu + "beraber oynadıkların" listesinin ekran tarafı.
    //
    // Bu iki sistem de veri üretiyor ve hiçbir giriş noktası yoktu:
    //   ReferralSystem.Redeem(code)        — proje genelinde sıfır çağrı
    //   DeepLinkManager.ShareReferral()    — sıfır çağrı
    //   DeepLinkManager.OnReferral         — invoke ediliyor, sıfır abone
    //   PlayedWithList.listParent/entryPrefab — hiç atanmıyor
    // Yani oyuncunun bir referans kodu vardı ama onu ne girebiliyor ne
    // paylaşabiliyordu; gelen "dreamcar://ref/<kod>" linkleri de düşüyordu.
    //
    // Liste içeriğini PlayedWithList kendi Refresh()'inde dolduruyor; burası
    // yalnızca paneli açıp onu tetikliyor.
    public class SocialScreen : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text myCodeLabel;
        public Button shareButton;
        public TMP_InputField redeemInput;
        public Button redeemButton;
        public Button closeButton;

        void Start()
        {
            if (panel) panel.SetActive(false);
            if (shareButton) shareButton.onClick.AddListener(Share);
            if (redeemButton) redeemButton.onClick.AddListener(Redeem);
            if (closeButton) closeButton.onClick.AddListener(Close);

            // Derin linkle gelen kod artık boşa düşmüyor: alana yazılıyor ki
            // oyuncu tek dokunuşla kullanabilsin.
            if (DeepLinkManager.Instance != null)
                DeepLinkManager.Instance.OnReferral += OnIncomingReferral;
        }

        void OnDestroy()
        {
            if (DeepLinkManager.Instance != null)
                DeepLinkManager.Instance.OnReferral -= OnIncomingReferral;
        }

        void OnIncomingReferral(string code)
        {
            if (redeemInput) redeemInput.text = code;
            Open();
            ToastNotification.Show("Davet kodu geldi — Kullan'a bas");
        }

        public void Open()
        {
            if (panel) panel.SetActive(true);
            RefreshCode();
            // PlayedWithList kendi OnEnable'ında yenileniyor ama o bileşen
            // ~Bootstrap'te ve hep açık; panel açılınca elle tetikliyoruz.
            if (PlayedWithList.Instance) PlayedWithList.Instance.Refresh();
        }

        public void Close() { if (panel) panel.SetActive(false); }

        void RefreshCode()
        {
            if (!myCodeLabel) return;
            var rs = ReferralSystem.Instance;
            myCodeLabel.text = rs ? rs.MyCode : "-";
        }

        void Share()
        {
            if (DeepLinkManager.Instance != null) DeepLinkManager.Instance.ShareReferral();
            else ToastNotification.Show("Paylaşım hazır değil");
        }

        void Redeem()
        {
            var rs = ReferralSystem.Instance;
            if (!rs) { ToastNotification.Show("Referans sistemi hazır değil"); return; }
            rs.Redeem(redeemInput ? redeemInput.text : "");
        }
    }
}
