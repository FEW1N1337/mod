using UnityEngine;

namespace DreamCar.Traffic
{
    // Waypoint takip eden basit trafik AI'ı. Rigidbody + WheelCollider gerekli değil —
    // performans için transform hareketi kullanıyor. Önündeki oyuncu arabasını görürse
    // yavaşlar/durur (basit raycast).
    public class TrafficCar : MonoBehaviour
    {
        public Transform[] waypoints;
        public float speedKmh = 40f;
        public float turnLerp = 3f;
        public float lookAheadDistance = 8f;
        public LayerMask obstacleLayers = ~0;
        public float waypointReachDistance = 3f;

        int _idx;
        float _currentSpeed;

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform target = waypoints[_idx];
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < waypointReachDistance * waypointReachDistance)
            {
                _idx = (_idx + 1) % waypoints.Length;
                return;
            }

            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * turnLerp);

            float targetSpeed = speedKmh / 3.6f;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, lookAheadDistance, obstacleLayers))
            {
                float t = Mathf.Clamp01((hit.distance - 1.5f) / (lookAheadDistance - 1.5f));
                targetSpeed *= t;
            }
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * 2f);

            transform.position += transform.forward * _currentSpeed * Time.deltaTime;
        }

        void OnDrawGizmosSelected()
        {
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!waypoints[i]) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.4f);
                var next = waypoints[(i + 1) % waypoints.Length];
                if (next) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
