using DreamCar.Economy;
using DreamCar.Vehicle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // İstasyon trigger'ına girince açılan panel. FuelSystem + PlayerMoney ile etkileşir.
    public class RefuelStationPanel : MonoBehaviour
    {
        public static RefuelStationPanel Instance { get; private set; }

        public GameObject panel;
        public Image fuelFill;
        public TMP_Text fuelPercentLabel;
        public TMP_Text priceLabel;
        public Button payButton;
        public Button cancelButton;

        FuelSystem _target;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (panel) panel.SetActive(false);
            if (payButton) payButton.onClick.AddListener(Pay);
            if (cancelButton) cancelButton.onClick.AddListener(Close);
        }

        public void Open(FuelSystem fuel)
        {
            _target = fuel;
            if (!_target || !panel) return;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            _target = null;
            if (panel) panel.SetActive(false);
        }

        void Update()
        {
            if (!_target || !panel || !panel.activeSelf) return;
            Refresh();
        }

        void Refresh()
        {
            float p = _target.Percent;
            if (fuelFill) fuelFill.fillAmount = p;
            if (fuelPercentLabel) fuelPercentLabel.text = Mathf.RoundToInt(p * 100f) + "%";

            float missing = _target.capacity - _target.current;
            long price = (long)Mathf.Ceil(missing * _target.pricePerLiter);
            if (priceLabel) priceLabel.text = price > 0 ? $"{price:N0} ₺" : "Depo dolu";
            if (payButton) payButton.interactable = price > 0 &&
                PlayerMoney.Instance && PlayerMoney.Instance.Money >= price;
        }

        void Pay()
        {
            if (!_target) return;
            if (_target.TryFillTank())
            {
                ToastNotification.Show("Depo dolduruldu");
                Close();
            }
            else
            {
                ToastNotification.Show("Yetersiz para");
            }
        }
    }
}
