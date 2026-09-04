using DreamCar.GameModes;
using DreamCar.Network;
using DreamCar.Race;
using DreamCar.Vehicle;
using TMPro;
using UnityEngine;

namespace DreamCar.UI
{
    // Vites, drift skoru/combo ve drift seansı sayacı.
    //
    // Üçü de hesaplanıyordu ve HİÇBİRİNİN EKRAN TÜKETİCİSİ YOKTU:
    //   GearBox.GearLabel        — projede tek geçiş, tanımın kendisi
    //   DriftScore.OnScoreChanged/OnCombo — sıfır abone
    //   DriftMode.RemainingSeconds — sıfır çağrı
    // Yani drift modu üç dakika boyunca sessizce puan biriktirip sonunda
    // sessizce ödeme yapıyordu; oyuncu ne skorunu ne kalan süreyi görüyordu.
    //
    // GearBox ve DriftScore araç prefabında ve araç odaya girilince doğuyor,
    // o yüzden Editor'de bağlanamıyorlar.
    public class DriveHud : MonoBehaviour
    {
        public TMP_Text gearLabel;
        public TMP_Text driftLabel;
        public TMP_Text driftTimerLabel;

        GameObject _car;
        GearBox _gears;
        DriftScore _drift;
        DriftMode _driftMode;

        // Combo bildirimi anlık; skor satırında kısa süre görünsün diye
        // zaman damgası tutuyoruz.
        int _lastCombo;
        float _comboUntil;

        void Update()
        {
            Rebind();

            if (gearLabel) gearLabel.text = _gears ? _gears.GearLabel : "-";

            if (driftLabel)
            {
                if (!_drift) driftLabel.text = "";
                else if (Time.time < _comboUntil)
                    driftLabel.text = $"{_drift.Bank:N0}   x{_lastCombo}";
                else if (_drift.Current > 0)
                    driftLabel.text = $"{_drift.Bank:N0}   +{_drift.Current:N0}";
                else
                    driftLabel.text = _drift.Bank > 0 ? $"{_drift.Bank:N0}" : "";
            }

            if (driftTimerLabel)
            {
                if (!_driftMode) _driftMode = FindAnyObjectByType<DriftMode>();
                if (_driftMode)
                {
                    float t = _driftMode.RemainingSeconds;
                    driftTimerLabel.text = $"{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}";
                }
                else driftTimerLabel.text = "";
            }
        }

        void Rebind()
        {
            var car = RoomManager.LocalCar;
            if (car == _car) return;

            // Aboneliği eski araçtan çöz, yoksa yok edilmiş bir bileşene
            // yapılan çağrı birikir.
            if (_drift) _drift.OnCombo -= OnCombo;

            _car = car;
            _gears = car ? car.GetComponent<GearBox>() : null;
            _drift = car ? car.GetComponent<DriftScore>() : null;

            if (_drift) _drift.OnCombo += OnCombo;
        }

        void OnDestroy() { if (_drift) _drift.OnCombo -= OnCombo; }

        void OnCombo(int combo)
        {
            _lastCombo = combo;
            _comboUntil = Time.time + 1.5f;
        }
    }
}
