using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using DreamCar.Monetization;

namespace DreamCar.Moderation
{
    // PlayFab sadece parayı koruyordu; fizik tarafında hiç doğrulama yoktu.
    // Bu detector uzak oyuncuların pozisyonunu izler, imkânsız hız/teleport
    // örüntülerini işaretler ve master client'a kick/rapor kararı bırakır.
    //
    // NOT: Bu client-side sezgisel bir katman — kesin çözüm değil. Gerçek koruma
    // Photon Server Plugin veya authoritative sunucu ister. Yine de basit hız
    // hilelerini ve NaN/teleport paketlerini yakalar.
    public class CheatDetector : MonoBehaviourPunCallbacks
    {
        public static CheatDetector Instance { get; private set; }

        [Header("Eşikler")]
        public float maxPlausibleSpeedKmh = 400f;   // en hızlı araç + nitro payı
        public float teleportDistanceMeters = 120f; // tek örnekte bu kadar sıçrama
        public float sampleIntervalSeconds = 0.5f;
        public int strikesBeforeAction = 5;

        [Header("Eylem")]
        public bool masterAutoKick = false;         // varsayılan kapalı — yanlış pozitif riski
        public bool showToastOnDetect = true;

        class Track
        {
            public Vector3 lastPosition;
            public float lastSampleTime;
            public int strikes;
            public bool initialized;
        }

        readonly Dictionary<int, Track> _tracks = new();
        float _nextSample;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update()
        {
            if (!PhotonNetwork.InRoom) return;
            if (Time.time < _nextSample) return;
            _nextSample = Time.time + sampleIntervalSeconds;

            foreach (var view in PhotonNetwork.PhotonViewCollection)
            {
                if (view == null || view.IsMine || view.Owner == null) continue;
                if (view.GetComponent<Car.CarNetworkSync>() == null) continue;
                Sample(view);
            }
        }

        void Sample(PhotonView view)
        {
            int actor = view.OwnerActorNr;
            if (!_tracks.TryGetValue(actor, out var track))
            {
                track = new Track();
                _tracks[actor] = track;
            }

            Vector3 pos = view.transform.position;

            // NaN/Infinity paketi — anında ihlal.
            if (!IsFinite(pos))
            {
                Flag(view, "invalid_position", instant: true);
                return;
            }

            if (!track.initialized)
            {
                track.initialized = true;
                track.lastPosition = pos;
                track.lastSampleTime = Time.time;
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.time - track.lastSampleTime);
            float distance = Vector3.Distance(pos, track.lastPosition);

            track.lastPosition = pos;
            track.lastSampleTime = Time.time;

            if (!Util.GameMath.IsPlausibleMovement(distance, dt, maxPlausibleSpeedKmh, teleportDistanceMeters))
            {
                Flag(view, distance > teleportDistanceMeters ? "teleport" : "speed", instant: false);
                return;
            }

            // Temiz örnekte strike'ı yavaşça geri al.
            if (track.strikes > 0) track.strikes--;
        }

        void Flag(PhotonView view, string kind, bool instant)
        {
            int actor = view.OwnerActorNr;
            var track = _tracks[actor];
            track.strikes += instant ? strikesBeforeAction : 1;

            if (track.strikes < strikesBeforeAction) return;
            track.strikes = 0;

            string nickname = view.Owner?.NickName ?? actor.ToString();
            Analytics.Event("cheat_suspected", new()
            {
                { "kind", kind },
                { "nickname", nickname },
            });
            Debug.LogWarning($"[CheatDetector] Şüpheli: {nickname} ({kind})");

            if (showToastOnDetect)
                UI.ToastNotification.Show($"Şüpheli hareket: {nickname}");

            if (masterAutoKick && PhotonNetwork.IsMasterClient && view.Owner != null)
                PhotonNetwork.CloseConnection(view.Owner);
        }

        static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        public override void OnPlayerLeftRoom(Player otherPlayer) => _tracks.Remove(otherPlayer.ActorNumber);
        public override void OnLeftRoom() => _tracks.Clear();
    }
}
