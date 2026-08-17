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
        }

        readonly Dictionary<int, RaceState> _states = new();

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

            if (cp.index != s.nextCheckpoint) return;

            s.nextCheckpoint++;

            if (cp.isFinishLine)
            {
                s.lap++;
                float lap = Time.time - s.lapStartTime;
                if (s.bestLapTime <= 0f || lap < s.bestLapTime) s.bestLapTime = lap;
                s.lapStartTime = Time.time;
                s.nextCheckpoint = 0;

                if (s.lap >= totalLaps) FinishRace(actor, s);
            }
        }

        void FinishRace(int actor, RaceState s)
        {
            float total = Time.time - s.startTime;
            Debug.Log($"[Race] Player {actor} finished in {total:F2}s (best lap {s.bestLapTime:F2}s)");
            if (PhotonNetwork.LocalPlayer.ActorNumber == actor && PlayerMoney.Instance)
                PlayerMoney.Instance.Add(winReward);
            _states.Remove(actor);
        }

        public (int lap, int totalLaps, float lapTime, float best) StatusFor(int actor)
        {
            if (_states.TryGetValue(actor, out var s))
                return (s.lap, totalLaps, Time.time - s.lapStartTime, s.bestLapTime);
            return (0, totalLaps, 0f, 0f);
        }
    }
}
