using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace DreamCar.Race
{
    public class LeaderboardUI : MonoBehaviour
    {
        public RaceManager race;
        public TMP_Text label;
        public float refreshInterval = 0.5f;
        float _next;

        void Update()
        {
            if (Time.time < _next || !race || !label || !PhotonNetwork.InRoom) return;
            _next = Time.time + refreshInterval;

            List<(string name, int lap, float best)> rows = new();
            foreach (var kv in PhotonNetwork.CurrentRoom.Players)
            {
                var s = race.StatusFor(kv.Key);
                rows.Add((kv.Value.NickName, s.lap, s.best));
            }
            rows = rows.OrderByDescending(r => r.lap)
                       .ThenBy(r => r.best <= 0 ? float.MaxValue : r.best)
                       .ToList();

            var sb = new System.Text.StringBuilder();
            int rank = 1;
            foreach (var r in rows)
            {
                string bt = r.best > 0 ? $"{r.best:F2}s" : "-";
                sb.AppendLine($"{rank++}. {r.name}  Tur {r.lap}  BestLap {bt}");
            }
            label.text = sb.ToString();
        }
    }
}
