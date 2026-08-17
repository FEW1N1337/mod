using Photon.Pun;
using UnityEngine;
using DreamCar.UI;

namespace DreamCar.Vehicle
{
    // Benzin istasyonu trigger volume. Owner araç girince RefuelStationPanel açar.
    // Çıkınca panel kapanır.
    [RequireComponent(typeof(Collider))]
    public class RefuelStation : MonoBehaviour
    {
        void Awake() { var c = GetComponent<Collider>(); c.isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            var fuel = other.GetComponentInParent<FuelSystem>();
            if (!fuel) return;
            if (!IsLocalOwnedCar(fuel.gameObject)) return;
            if (RefuelStationPanel.Instance != null) RefuelStationPanel.Instance.Open(fuel);
        }

        void OnTriggerExit(Collider other)
        {
            var fuel = other.GetComponentInParent<FuelSystem>();
            if (!fuel) return;
            if (!IsLocalOwnedCar(fuel.gameObject)) return;
            if (RefuelStationPanel.Instance != null) RefuelStationPanel.Instance.Close();
        }

        static bool IsLocalOwnedCar(GameObject go)
        {
            var pv = go.GetComponentInParent<PhotonView>();
            return pv == null || pv.IsMine;
        }
    }
}
