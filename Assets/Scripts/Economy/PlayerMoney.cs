using System;
using Photon.Pun;
using UnityEngine;

namespace DreamCar.Economy
{
    // Dream Road'daki PlayerManager.Money eşdeğeri. PlayerPrefs'e persist eder,
    // Photon custom property'e de yayımlar (başka oyunculara görünür — leaderboard/mağaza için).
    // Cloud sync için PlayFab/GameSparks buraya bağlanabilir.
    public class PlayerMoney : MonoBehaviour
    {
        public static PlayerMoney Instance { get; private set; }
        public event Action<long> OnMoneyChanged;

        const string PrefKey = "player.money";
        const long StartingMoney = 5000;

        long _money;
        public long Money => _money;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _money = (long)PlayerPrefs.GetInt(PrefKey, (int)StartingMoney);
            Publish();
        }

        public void Add(long amount)
        {
            if (amount <= 0) return;
            _money += amount;
            Save();
            OnMoneyChanged?.Invoke(_money);
            if (Core.PlayerStats.Instance) Core.PlayerStats.Instance.AddMoneyEarned(amount);
        }

        public bool TrySpend(long amount)
        {
            if (amount <= 0) return true;
            if (_money < amount) return false;
            _money -= amount;
            Save();
            OnMoneyChanged?.Invoke(_money);
            return true;
        }

        void Save()
        {
            // Mathf.Clamp(long, ...) float overload'una düşüyordu: 16.7M üstündeki bakiyeler
            // float hassasiyetinde yuvarlanıyor, int.MaxValue civarında ise (int) dönüşümü
            // taşıp parayı negatife çeviriyordu. Kırpmayı long aritmetiğiyle yapıyoruz.
            long clamped = _money < 0 ? 0 : (_money > int.MaxValue ? int.MaxValue : _money);
            PlayerPrefs.SetInt(PrefKey, (int)clamped);
            PlayerPrefs.Save();
            Publish();
        }

        void Publish()
        {
            if (PhotonNetwork.LocalPlayer != null && PhotonNetwork.IsConnected)
            {
                var props = new ExitGames.Client.Photon.Hashtable { { "money", (int)_money } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
        }
    }
}
