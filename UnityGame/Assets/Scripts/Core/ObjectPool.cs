using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Core
{
    // TrafficSpawner ve particle'lar sürekli Instantiate/Destroy yapıyordu → mobilde
    // GC spike ve takılma. Bu generic pool onu ortadan kaldırır.
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [System.Serializable]
        public class PoolConfig
        {
            public GameObject prefab;
            public int prewarmCount = 8;
            public int maxSize = 64;
        }

        public PoolConfig[] prewarm;

        readonly Dictionary<GameObject, Stack<GameObject>> _available = new();
        readonly Dictionary<GameObject, int> _maxSizes = new();
        readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (prewarm == null) return;
            foreach (var cfg in prewarm)
            {
                if (cfg?.prefab == null) continue;
                _maxSizes[cfg.prefab] = Mathf.Max(1, cfg.maxSize);
                Prewarm(cfg.prefab, cfg.prewarmCount);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var stack = GetStack(prefab);
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                _instanceToPrefab[go] = prefab;
                stack.Push(go);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            var stack = GetStack(prefab);
            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop(); // yok edilmiş referansları atla

            if (go == null)
            {
                go = Instantiate(prefab);
                _instanceToPrefab[go] = prefab;
            }

            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            foreach (var p in go.GetComponentsInChildren<IPooled>(true)) p.OnSpawned();
            return go;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!_instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                // Pool dışından gelmiş — normal yok et.
                Destroy(instance);
                return;
            }

            foreach (var p in instance.GetComponentsInChildren<IPooled>(true)) p.OnDespawned();

            var stack = GetStack(prefab);
            int max = _maxSizes.TryGetValue(prefab, out var m) ? m : 64;
            if (stack.Count >= max)
            {
                _instanceToPrefab.Remove(instance);
                Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            stack.Push(instance);
        }

        public void DespawnAfter(GameObject instance, float seconds) =>
            StartCoroutine(DespawnDelayed(instance, seconds));

        System.Collections.IEnumerator DespawnDelayed(GameObject instance, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Despawn(instance);
        }

        Stack<GameObject> GetStack(GameObject prefab)
        {
            if (!_available.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                _available[prefab] = stack;
            }
            return stack;
        }
    }

    // Havuzdan çıkarken/girerken state sıfırlamak isteyen bileşenler bunu uygular.
    public interface IPooled
    {
        void OnSpawned();
        void OnDespawned();
    }
}
