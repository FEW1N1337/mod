using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using DreamCar.Race;
using DreamCar.Economy;
using DreamCar.UI;

namespace DreamCar.GameModes
{
    // 3 dakikalık drift oturumu. Süre bitince en yüksek skor kazanır.
    public class DriftMode : GameModeBase
    {
        public float sessionSeconds = 180f;
        public long rewardPerThousandPoints = 5;

        float _endTime;
        bool _running;

        public override GameModeType Type => GameModeType.Drift;

        public override void OnModeStart()
        {
            _endTime = Time.time + sessionSeconds;
            _running = true;
            StartCoroutine(Timer());
        }

        IEnumerator Timer()
        {
            while (_running && Time.time < _endTime) yield return null;
            Finish();
        }

        void Finish()
        {
            _running = false;

            var mine = FindObjectsByType<DriftScore>(FindObjectsSortMode.None)
                .FirstOrDefault(d => d.GetComponent<PhotonView>() && d.GetComponent<PhotonView>().IsMine);
            if (mine != null)
            {
                long reward = (mine.Bank / 1000L) * rewardPerThousandPoints;
                if (reward > 0 && PlayerMoney.Instance) PlayerMoney.Instance.Add(reward);
                ToastNotification.Show($"Drift bitti: {mine.Bank:N0} puan → +{reward:N0}");
            }
        }

        public float RemainingSeconds => Mathf.Max(0f, _endTime - Time.time);
    }
}
