using System.Collections.Generic;
using DreamCar.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // PlayFabLeaderboards backend'i vardı ama görsel karşılığı yoktu.
    // Sekmeli liderlik tablosu: En İyi Tur / Drift Skoru.
    public class LeaderboardScreen : MonoBehaviour
    {
        public GameObject panel;
        public Button closeButton;
        public Button raceTabButton;
        public Button driftTabButton;
        public Transform listParent;
        public GameObject rowPrefab;
        public TMP_Text titleLabel;
        public GameObject loadingIndicator;
        public int rowCount = 25;

        PlayFabLeaderboards _backend;
        string _currentStat;

        void Start()
        {
            _backend = FindFirstObjectByType<PlayFabLeaderboards>();
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (raceTabButton) raceTabButton.onClick.AddListener(() => Show(PlayFabLeaderboards.RaceBestLapStat));
            if (driftTabButton) driftTabButton.onClick.AddListener(() => Show(PlayFabLeaderboards.DriftScoreStat));
        }

        public void Open()
        {
            if (panel) panel.SetActive(true);
            Show(PlayFabLeaderboards.RaceBestLapStat);
        }

        public void Close() { if (panel) panel.SetActive(false); }

        void Show(string stat)
        {
            _currentStat = stat;
            bool isRace = stat == PlayFabLeaderboards.RaceBestLapStat;
            if (titleLabel) titleLabel.text = isRace ? "En İyi Tur" : "Drift Skoru";

            ClearRows();
            if (loadingIndicator) loadingIndicator.SetActive(true);

            if (_backend == null)
            {
                if (loadingIndicator) loadingIndicator.SetActive(false);
                AddRow(0, "PlayFab bağlı değil", "-");
                return;
            }

            _backend.FetchTop(stat, rowCount, rows =>
            {
                if (loadingIndicator) loadingIndicator.SetActive(false);
                if (_currentStat != stat) return; // kullanıcı sekme değiştirdi

                if (rows == null || rows.Count == 0) { AddRow(0, "Henüz kayıt yok", "-"); return; }

                for (int i = 0; i < rows.Count; i++)
                    AddRow(i + 1, rows[i].name, FormatValue(stat, rows[i].value));
            });
        }

        static string FormatValue(string stat, int value)
        {
            // Backend negatif ms yazıyor (küçük süre = büyük değer); FormatLapTime
            // mutlak değeri kullanır.
            return stat == PlayFabLeaderboards.RaceBestLapStat
                ? Util.GameMath.FormatLapTime(value)
                : value.ToString("N0");
        }

        void AddRow(int rank, string name, string value)
        {
            if (!listParent || !rowPrefab) return;
            var go = Instantiate(rowPrefab, listParent);
            // rowPrefab sahnede kapalı duran bir şablon; klon da kapalı doğuyordu.
            go.SetActive(true);
            var texts = go.GetComponentsInChildren<TMP_Text>();
            if (texts.Length > 0) texts[0].text = rank > 0 ? rank.ToString() : "";
            if (texts.Length > 1) texts[1].text = name;
            if (texts.Length > 2) texts[2].text = value;
        }

        void ClearRows()
        {
            if (!listParent) return;
            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);
        }
    }
}
