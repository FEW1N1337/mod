using UnityEngine;

namespace DreamCar.Race
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        public int index;
        public bool isFinishLine;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var manager = FindAnyObjectByType<RaceManager>();
            if (manager) manager.OnCheckpointHit(other, this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = isFinishLine ? Color.red : Color.yellow;
            var b = GetComponent<Collider>() ? GetComponent<Collider>().bounds : new Bounds(transform.position, Vector3.one * 4);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
