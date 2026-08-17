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
        }
    }
}
