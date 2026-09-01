using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.Race
{
    public class RaceManager : MonoBehaviourPun
    {
        public Checkpoint[] checkpoints;
        public int totalLaps = 3;
        public long winReward = 1000;

        class RaceState
        {
            public int nextCheckpoint;
            public int lap;
            public float startTime;
            public float bestLapTime;
            public float lapStartTime;
            public bool started;   // start/finish çizgisinden ilk (turu saymayan) geçiş yapıldı mı
            public bool finished;  // yarışı bitirdi — bir daha tur/ödül işlemesin
        }

        readonly Dictionary<int, RaceState> _states = new();
        int _checkpointCount = -1;

        // Tur döngüsü için toplam checkpoint sayısı gerekiyor. "checkpoints" alanı Editor'de
        // doldurulamıyor (haritalar prosedürel üretiliyor, RaceManager de runtime'da
        // AddComponent ediliyor) — boşsa sahneden toplanır, yoksa alan hiç kullanılmıyordu.
        int CheckpointCount
        {
            get
            {
                if (_checkpointCount > 0) return _checkpointCount;
                if (checkpoints == null || checkpoints.Length == 0)
                    checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
                int max = -1;
                foreach (var c in checkpoints)
                    if (c && c.index > max) max = c.index;
                _checkpointCount = max + 1;
                return _checkpointCount;
            }
        }

        public void StartRace(int actorNumber)
        {
            _states[actorNumber] = new RaceState { startTime = Time.time, lapStartTime = Time.time };
        }

        public void OnCheckpointHit(Collider carCollider, Checkpoint cp)
        {
            var pv = carCollider.GetComponentInParent<PhotonView>();
            if (!pv || !pv.IsMine) return;

            int actor = pv.OwnerActorNr;
            if (!_states.TryGetValue(actor, out var s))
                _states[actor] = s = new RaceState { startTime = Time.time, lapStartTime = Time.time };

            if (s.finished) return;
            if (cp.index != s.nextCheckpoint) return;

            // Sıradaki checkpoint döngüsel ilerlemeli. Eskiden bitiş çizgisinde sabit 0'a
            // sıfırlanıyordu; harita üreticisi bitiş çizgisini 0. checkpoint yaptığı için
            // beklenen index bitiş çizgisinde takılı kalıyor, aradaki checkpoint'ler hiç
            // kabul edilmiyor ve oyuncu pisti hiç dolaşmadan çizgiden ileri-geri geçerek
            // tur kasabiliyordu.
            int count = CheckpointCount;
            s.nextCheckpoint = count > 0 ? (cp.index + 1) % count : cp.index + 1;

            if (!cp.isFinishLine) return;

            // Start ve finish aynı çizgi (isFinishLine == index 0): araçlar çizginin
            // gerisinde doğduğu için ilk geçiş bir turu tamamlamaz, yarışı başlatır.
            // Eskiden bu geçiş de tur sayılıp yarış bir tur erken bitiyordu.
            if (!s.started && cp.index == 0)
            {
                s.started = true;
                s.startTime = Time.time;
                s.lapStartTime = Time.time;
                return;
            }

            s.lap++;
            float lapTime = Time.time - s.lapStartTime;
            if (s.bestLapTime <= 0f || lapTime < s.bestLapTime) s.bestLapTime = lapTime;
            s.lapStartTime = Time.time;

            if (s.lap >= totalLaps) FinishRace(actor, s);
        }

        void FinishRace(int actor, RaceState s)
        {
            float total = Time.time - s.startTime;
            Debug.Log($"[Race] Player {actor} finished in {total:F2}s (best lap {s.bestLapTime:F2}s)");
            bool isLocal = PhotonNetwork.LocalPlayer.ActorNumber == actor;
            if (isLocal && PlayerMoney.Instance)
                PlayerMoney.Instance.Add(winReward);

            if (isLocal)
            {
                var ach = Backend.PlayFabAchievements.Instance;
                if (ach) ach.OnRaceFinished(won: true);
                var rate = AppMeta.RateAppPopup.Instance;
                if (rate) rate.OnRaceFinished();
                if (Core.PlayerStats.Instance) Core.PlayerStats.Instance.ReportRaceFinished(won: true);
                // Bitişin tek geri bildirimi Debug.Log'du — oyuncu ekranda hiçbir şey görmüyordu.
                UI.ToastNotification.Show($"Yarış bitti! Süre {total:F2}s · En iyi tur {s.bestLapTime:F2}s · +{winReward:N0}");
            }
            // Durumu silmek yerine işaretle: silinince oyuncu çizgiden tekrar geçtiğinde
            // sıfırdan yeni bir yarış başlatıp ödülü defalarca alabiliyordu.
            s.finished = true;
        }

        public (int lap, int totalLaps, float lapTime, float best) StatusFor(int actor)
        {
            if (_states.TryGetValue(actor, out var s))
                return (s.lap, totalLaps, Time.time - s.lapStartTime, s.bestLapTime);
            return (0, totalLaps, 0f, 0f);
        }
    }
}
