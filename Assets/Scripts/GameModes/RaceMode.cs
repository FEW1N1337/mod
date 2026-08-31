using System.Collections;
using Photon.Pun;
using UnityEngine;
using DreamCar.Race;
using DreamCar.Economy;
using DreamCar.UI;

namespace DreamCar.GameModes
{
    // Mevcut RaceManager'ı sarar: başlama sayacı, yarış boyunca skor takibi,
    // bitişte ödül dağıtımı.
    [RequireComponent(typeof(RaceManager))]
    public class RaceMode : GameModeBase
    {
        public int countdownSeconds = 3;
        public long finishReward = 1000;

        RaceManager _race;

        public override GameModeType Type => GameModeType.Race;

        void Awake() => _race = GetComponent<RaceManager>();

        public override void OnModeStart()
        {
            _race.winReward = finishReward;
            StartCoroutine(RunCountdown());
        }

        IEnumerator RunCountdown()
        {
            for (int i = countdownSeconds; i > 0; i--)
            {
                ToastNotification.Show(i.ToString());
                yield return new WaitForSeconds(1f);
            }
            ToastNotification.Show("GO!");
            Monetization.Analytics.Event("race_start", new()
            {
                { "laps", _race.totalLaps },
                { "players", PhotonNetwork.CurrentRoom?.PlayerCount ?? 1 },
            });
            _race.StartRace(PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }
}
