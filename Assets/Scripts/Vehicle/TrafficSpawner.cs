using System.Collections.Generic;
using UnityEngine;
using DreamCar.Traffic;

namespace DreamCar.Vehicle
{
    // Waypoint chain'ler üzerinden belirli aralıklarla TrafficCar spawn eder. Oyuncu
    // uzaklaşınca despawn (pool). Merkezi trafik yoğunluğu / max araç ayarları burada.
    public class TrafficSpawner : MonoBehaviour
    {
        [System.Serializable] public class Lane { public Transform[] waypoints; public float spawnIntervalSeconds = 8f; }

        public GameObject[] trafficCarPrefabs;
        public Lane[] lanes;
        public int maxAlive = 20;
        public float despawnDistance = 120f;
        public Transform tracker;

        readonly List<GameObject> _alive = new();
        readonly List<float> _laneTimers = new();

        void Start()
        {
            _laneTimers.Clear();
            foreach (var _ in lanes) _laneTimers.Add(Random.Range(0f, 3f));
        }

        void Update()
        {
            if (trafficCarPrefabs == null || trafficCarPrefabs.Length == 0 || lanes == null) return;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var go = _alive[i];
                if (!go) { _alive.RemoveAt(i); continue; }
                if (tracker && Vector3.Distance(go.transform.position, tracker.position) > despawnDistance)
                {
                    if (Core.ObjectPool.Instance) Core.ObjectPool.Instance.Despawn(go);
                    else Destroy(go);
                    _alive.RemoveAt(i);
                }
            }

            if (_alive.Count >= maxAlive) return;

            for (int i = 0; i < lanes.Length; i++)
            {
                _laneTimers[i] -= Time.deltaTime;
                if (_laneTimers[i] > 0f) continue;
                _laneTimers[i] = lanes[i].spawnIntervalSeconds;
                SpawnOn(lanes[i]);
                if (_alive.Count >= maxAlive) break;
            }
        }

        void SpawnOn(Lane lane)
        {
            if (lane.waypoints == null || lane.waypoints.Length == 0) return;
            var prefab = trafficCarPrefabs[Random.Range(0, trafficCarPrefabs.Length)];
            if (!prefab) return;

            var spawn = lane.waypoints[0];
            var go = Core.ObjectPool.Instance
                ? Core.ObjectPool.Instance.Spawn(prefab, spawn.position, spawn.rotation)
                : Instantiate(prefab, spawn.position, spawn.rotation);
            if (!go) return;
            var ai = go.GetComponent<TrafficCar>() ?? go.AddComponent<TrafficCar>();
            ai.waypoints = lane.waypoints;
            _alive.Add(go);
        }
    }
}
