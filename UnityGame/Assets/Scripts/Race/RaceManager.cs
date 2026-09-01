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
            // Photon'un sunucu senkronlu saati. Time.time her istemcide farklı
            // bir sıfır noktasından sayıyor, o yüzden tur başlangıcı ağ
            // üzerinden taşınamıyor.
            public double netLapStart;
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

        // Oyuncu başına custom property anahtarları — sıralama tablosu bunları okuyor.
        const string LapKey = "rlap";
        const string BestKey = "rbest";
        const string LapStartKey = "rls";

        public void StartRace(int actorNumber)
        {
            _states[actorNumber] = new RaceState
            {
                startTime = Time.time,
                lapStartTime = Time.time,
                netLapStart = PhotonNetwork.Time,
            };
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                Publish(_states[actorNumber]);
        }

        // OnCheckpointHit "if (!pv.IsMine) return" ile başlıyor — ve başlamak
        // ZORUNDA, yoksa her istemci başkasının turunu da sayardı. Sonucu şuydu:
        // her istemci YALNIZCA kendi RaceState'ini kuruyor, StatusFor uzak
        // oyuncular için sıfır dönüyordu. Yani sıralama tablosu yapısal olarak
        // imkânsızdı — rakipler sonsuza kadar "Tur 0" görünürdü.
        //
        // Çözüm: kendi ilerlemeni oyuncu özelliklerine yaz, ötekilerinkini
        // oradan oku. Photon bunları odadaki herkese dağıtıyor.
        void Publish(RaceState s)
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { LapKey, s.lap },
                { BestKey, s.bestLapTime },
                { LapStartKey, s.netLapStart },
            });
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
                s.netLapStart = PhotonNetwork.Time;
                Publish(s);
                return;
            }

            s.lap++;
            float lapTime = Time.time - s.lapStartTime;
            if (s.bestLapTime <= 0f || lapTime < s.bestLapTime) s.bestLapTime = lapTime;
            s.lapStartTime = Time.time;
            s.netLapStart = PhotonNetwork.Time;
            Publish(s);

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
                // Eskiden bitiren HERKES won:true yazıyordu — sonuncu gelen bile
                // "kazandı" sayılıyor, zafer başarımı ve istatistiği anlamsızlaşıyordu.
                // Çizgiyi ilk geçen odaya kendini yazar; sonrakiler dolu bulur.
                bool won = ClaimFirstPlace();

                var ach = Backend.PlayFabAchievements.Instance;
                if (ach) ach.OnRaceFinished(won);
                var rate = AppMeta.RateAppPopup.Instance;
                if (rate) rate.OnRaceFinished();
                if (Core.PlayerStats.Instance) Core.PlayerStats.Instance.ReportRaceFinished(won);

                // Bitişin tek geri bildirimi Debug.Log'du — oyuncu ekranda hiçbir şey görmüyordu.
                string head = won ? "Yarışı kazandın!" : "Yarış bitti";
                UI.ToastNotification.Show(
                    $"{head} Süre {total:F2}s · En iyi tur {s.bestLapTime:F2}s · +{winReward:N0}");
            }
            // Durumu silmek yerine işaretle: silinince oyuncu çizgiden tekrar geçtiğinde
            // sıfırdan yeni bir yarış başlatıp ödülü defalarca alabiliyordu.
            s.finished = true;
        }

        const string WinnerKey = "rw";

        // Birincilik odada tek bir özellikte tutulur. Boşsa bu oyuncu ilk bitirendir
        // ve kendini yazar; doluysa biri önce gelmiştir.
        //
        // Sınır: iki oyuncu aynı milisaniyede bitirirse ikisi de boş görüp ikisi de
        // yazabilir. Photon'un check-and-swap'i bunu tam çözer ama tek oyunculu ve
        // offline modda davranışı doğrulayamadığım için basit yolu seçtim; pencere
        // milisaniye mertebesinde ve sonucu yalnızca bir başarım bayrağı.
        bool ClaimFirstPlace()
        {
            if (!PhotonNetwork.InRoom) return true;   // tek oyunculu: bitiren kazanır

            var room = PhotonNetwork.CurrentRoom;
            if (room.CustomProperties.TryGetValue(WinnerKey, out object existing) && existing != null)
                return (int)existing == PhotonNetwork.LocalPlayer.ActorNumber;

            room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { WinnerKey, PhotonNetwork.LocalPlayer.ActorNumber },
            });
            return true;
        }

        public (int lap, int totalLaps, float lapTime, float best) StatusFor(int actor)
        {
            if (_states.TryGetValue(actor, out var s))
                return (s.lap, totalLaps, Time.time - s.lapStartTime, s.bestLapTime);

            // Uzak oyuncu: ilerlemesini kendi custom property'lerinden oku.
            if (PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var player))
            {
                var props = player.CustomProperties;
                int lap = props.TryGetValue(LapKey, out var l) ? (int)l : 0;
                float best = props.TryGetValue(BestKey, out var b) ? (float)b : 0f;
                float lapTime = 0f;
                if (props.TryGetValue(LapStartKey, out var ls))
                    lapTime = Mathf.Max(0f, (float)(PhotonNetwork.Time - (double)ls));
                return (lap, totalLaps, lapTime, best);
            }

            return (0, totalLaps, 0f, 0f);
        }
    }
}
