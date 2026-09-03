using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // PhotonConnector.preferredRegion kod tarafında vardı ama seçim ekranı yoktu —
    // herkes default bölgeye düşüyordu. Bu ekran ping'leri gösterir ve seçimi kalıcı yapar.
    public class RegionSelector : MonoBehaviourPunCallbacks
    {
        [System.Serializable]
        public class RegionOption { public string code; public string displayName; }

        public GameObject panel;
        public TMP_Dropdown dropdown;
        public TMP_Text currentRegionLabel;
        public Button applyButton;
        public Button closeButton;
        public bool autoBestRegionByDefault = true;

        // Photon Cloud bölgeleri — Türkiye için "eu" genelde en düşük ping.
        public List<RegionOption> regions = new()
        {
            new() { code = "",    displayName = "Otomatik (en iyi ping)" },
            new() { code = "eu",  displayName = "Avrupa" },
            new() { code = "us",  displayName = "ABD (Doğu)" },
            new() { code = "usw", displayName = "ABD (Batı)" },
            new() { code = "asia",displayName = "Asya" },
            new() { code = "jp",  displayName = "Japonya" },
            new() { code = "au",  displayName = "Avustralya" },
            new() { code = "sa",  displayName = "Güney Amerika" },
            new() { code = "in",  displayName = "Hindistan" },
            new() { code = "ru",  displayName = "Rusya" },
        };

        const string PrefKey = "photon.region";

        void Start()
        {
            BuildDropdown();
            if (applyButton) applyButton.onClick.AddListener(Apply);
            if (closeButton) closeButton.onClick.AddListener(Close);
            UpdateCurrentLabel();
        }

        void BuildDropdown()
        {
            if (!dropdown) return;
            dropdown.ClearOptions();

            var labels = new List<string>();
            foreach (var r in regions) labels.Add(r.displayName);
            dropdown.AddOptions(labels);

            var saved = PlayerPrefs.GetString(PrefKey, "");
            int index = regions.FindIndex(r => r.code == saved);
            dropdown.value = index >= 0 ? index : 0;
            dropdown.RefreshShownValue();
        }

        public void Apply()
        {
            if (!dropdown) return;
            var chosen = regions[Mathf.Clamp(dropdown.value, 0, regions.Count - 1)];

            PlayerPrefs.SetString(PrefKey, chosen.code);
            PlayerPrefs.Save();

            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion =
                string.IsNullOrEmpty(chosen.code) ? null : chosen.code;

            // Bölge değişikliği için yeniden bağlanmak şart.
            if (PhotonNetwork.IsConnected)
            {
                ToastNotification.Show("Bölge değişti, yeniden bağlanılıyor…");
                if (Network.ReconnectionManager.Instance)
                    Network.ReconnectionManager.Instance.MarkUserInitiatedLeave();
                PhotonNetwork.Disconnect();
                Invoke(nameof(Reconnect), 1f);
            }
            else Reconnect();

            UpdateCurrentLabel();
            Close();
        }

        void Reconnect()
        {
            if (Network.PhotonConnector.Instance) Network.PhotonConnector.Instance.Connect();
            else PhotonNetwork.ConnectUsingSettings();
        }

        void UpdateCurrentLabel()
        {
            if (!currentRegionLabel) return;
            string active = PhotonNetwork.IsConnected ? PhotonNetwork.CloudRegion : PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(active)) active = "otomatik";
            currentRegionLabel.text = "Bölge: " + active;
        }

        public override void OnConnectedToMaster() => UpdateCurrentLabel();

        public void Open() { if (panel) panel.SetActive(true); UpdateCurrentLabel(); }
        public void Close() { if (panel) panel.SetActive(false); }

        // PhotonConnector başlarken kaydedilmiş bölgeyi uygulasın diye.
        public static string SavedRegion => PlayerPrefs.GetString(PrefKey, "");
    }
}
