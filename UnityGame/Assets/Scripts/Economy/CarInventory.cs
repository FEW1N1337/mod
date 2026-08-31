using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Economy
{
    // Oyuncunun sahip olduğu arabalar + aktif seçili araba.
    public class CarInventory : MonoBehaviour
    {
        public static CarInventory Instance { get; private set; }
        public CarCatalog catalog;

        const string OwnedKey = "cars.owned";
        const string ActiveKey = "cars.active";
        const string StartCarId = "car.default";

        public event Action OnChanged;

        readonly HashSet<string> _owned = new();
        string _activeId = StartCarId;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // Awake sırasında PlayerStats singleton'ı henüz hazır olmayabilir; ilk senkron burada.
        void Start() => SyncCarsOwnedStat();

        public bool Owns(string carId) => _owned.Contains(carId);
        public string ActiveCarId => _activeId;
        public CarDefinition ActiveCar => catalog ? catalog.Find(_activeId) : null;

        public bool Buy(CarDefinition def)
        {
            if (!def || Owns(def.id)) return false;
            if (PlayerMoney.Instance == null) return false;
            if (!PlayerMoney.Instance.TrySpend(def.price)) return false;

            _owned.Add(def.id);
            Save();
            // "carsBought" başarımını besleyen tek nokta burasıydı ama OnCarPurchased()
            // hiçbir yerden çağrılmıyordu; araç satın alma başarımı hiç açılmıyordu.
            var ach = Backend.PlayFabAchievements.Instance;
            if (ach) ach.OnCarPurchased();
            OnChanged?.Invoke();
            return true;
        }

        public void SetActive(string carId)
        {
            if (!_owned.Contains(carId)) return;
            _activeId = carId;
            Save();
            OnChanged?.Invoke();
        }

        // Cloud save için: sahip olunan araçların listesi.
        public List<string> OwnedCarIds() => new(_owned);

        // Buluttan gelen listeyi yerel ile birleştirir (union) — offline satın alınan
        // araçlar kaybolmasın. Aktif araç buluttakine ayarlanır, o da sahip değilse
        // mevcut seçim korunur.
        public void MergeOwnedFromCloud(List<string> cloudOwned, string cloudActive)
        {
            bool changed = false;

            if (cloudOwned != null)
            {
                foreach (var id in cloudOwned)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (_owned.Add(id.Trim())) changed = true;
                }
            }

            if (!string.IsNullOrEmpty(cloudActive) && _owned.Contains(cloudActive) && _activeId != cloudActive)
            {
                _activeId = cloudActive;
                changed = true;
            }

            if (!changed) return;
            Save();
            OnChanged?.Invoke();
        }

        void Load()
        {
            var raw = PlayerPrefs.GetString(OwnedKey, StartCarId);
            _owned.Clear();
            foreach (var s in raw.Split(',')) if (!string.IsNullOrWhiteSpace(s)) _owned.Add(s.Trim());
            _owned.Add(StartCarId);
            _activeId = PlayerPrefs.GetString(ActiveKey, StartCarId);
            if (!_owned.Contains(_activeId)) _activeId = StartCarId;
        }

        void Save()
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(",", _owned));
            PlayerPrefs.SetString(ActiveKey, _activeId);
            PlayerPrefs.Save();
            SyncCarsOwnedStat();
        }

        // PlayerStats.SetCarsOwned hiçbir yerden çağrılmıyordu: istatistik ekranındaki
        // "Araç" satırı bu yüzden hep 0 kalıyordu. Sahiplik listesi her değiştiğinde
        // (ve açılışta) sayacı güncelliyoruz.
        void SyncCarsOwnedStat()
        {
            if (Core.PlayerStats.Instance) Core.PlayerStats.Instance.SetCarsOwned(_owned.Count);
        }
    }
}
