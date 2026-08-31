using DreamCar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Oyuncunun kümülatif istatistikleri. PlayerStats verisini okur.
    public class StatsScreen : MonoBehaviour
    {
        public GameObject panel;
        public Button closeButton;

        [Header("Etiketler")]
        public TMP_Text distanceLabel;
        public TMP_Text driveTimeLabel;
        public TMP_Text topSpeedLabel;
        public TMP_Text racesLabel;
        public TMP_Text winsLabel;
        public TMP_Text winRateLabel;
        public TMP_Text bestDriftLabel;
        public TMP_Text moneyEarnedLabel;
        public TMP_Text carsOwnedLabel;
        public TMP_Text crashesLabel;

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            Refresh();
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged += Refresh;
        }

        void OnDisable()
        {
            if (PlayerStats.Instance != null) PlayerStats.Instance.OnChanged -= Refresh;
        }

        void Refresh()
        {
            var s = PlayerStats.Instance;
            if (s == null) return;

            if (distanceLabel) distanceLabel.text = FormatDistance(s.TotalDistanceMeters);
            if (driveTimeLabel) driveTimeLabel.text = FormatDuration(s.TotalDriveSeconds);
            if (topSpeedLabel) topSpeedLabel.text = Mathf.RoundToInt(s.TopSpeedKmh) + " km/h";
            if (racesLabel) racesLabel.text = s.RacesFinished.ToString("N0");
            if (winsLabel) winsLabel.text = s.RacesWon.ToString("N0");
            if (winRateLabel)
            {
                float rate = s.RacesFinished > 0 ? (float)s.RacesWon / s.RacesFinished * 100f : 0f;
                winRateLabel.text = rate.ToString("0.0") + "%";
            }
            if (bestDriftLabel) bestDriftLabel.text = s.BestDriftScore.ToString("N0");
            if (moneyEarnedLabel) moneyEarnedLabel.text = s.TotalMoneyEarned.ToString("N0") + " ₺";
            if (carsOwnedLabel) carsOwnedLabel.text = s.CarsOwned.ToString();
            if (crashesLabel) crashesLabel.text = s.CollisionCount.ToString("N0");
        }

        static string FormatDistance(float meters) => Util.GameMath.FormatDistance(meters);
        static string FormatDuration(float seconds) => Util.GameMath.FormatDuration(seconds);

        public void Open() { if (panel) panel.SetActive(true); Refresh(); }
        public void Close() { if (panel) panel.SetActive(false); }
    }
}
