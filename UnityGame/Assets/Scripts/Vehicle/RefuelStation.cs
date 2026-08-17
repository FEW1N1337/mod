using UnityEngine;
using DreamCar.UI;

namespace DreamCar.Vehicle
{
    // Benzin istasyonu trigger volume. Araç girince tam depo doldurur.
    [RequireComponent(typeof(Collider))]
    public class RefuelStation : MonoBehaviour
    {
        public float refuelDelay = 2f;
        float _lastRefuel;

        void Awake() { var c = GetComponent<Collider>(); c.isTrigger = true; }

        void OnTriggerStay(Collider other)
        {
            if (Time.time - _lastRefuel < refuelDelay) return;
            var fuel = other.GetComponentInParent<FuelSystem>();
            if (!fuel) return;
            if (fuel.TryFillTank())
            {
                _lastRefuel = Time.time;
                ToastNotification.Show("Depo dolduruldu");
            }
            else if (fuel.Percent < 0.99f)
            {
                _lastRefuel = Time.time;
                ToastNotification.Show("Yetersiz para");
            }
        }
    }
}
