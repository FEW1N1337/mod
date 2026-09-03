using UnityEngine;
using DreamCar.Core;

namespace DreamCar.UI
{
    // Seviye atlayınca toast gösterir. ~Bootstrap üzerinde durur (sahneden
    // bağımsız), çünkü seviye atlama oyun içinde de olur (yarış/drift kazancı).
    //
    // Neden ayrı bileşen: DriverProfile Core'da, ToastNotification UI'da.
    // Core'u UI'a bağımlı kılmamak için olay Core'da fırlatılıp burada
    // (UI tarafında) dinleniyor.
    public class LevelUpAnnouncer : MonoBehaviour
    {
        void OnEnable()
        {
            if (DriverProfile.Instance != null) DriverProfile.Instance.OnLevelUp += Announce;
        }

        void OnDisable()
        {
            if (DriverProfile.Instance != null) DriverProfile.Instance.OnLevelUp -= Announce;
        }

        void Start()
        {
            if (DriverProfile.Instance != null)
            {
                DriverProfile.Instance.OnLevelUp -= Announce;
                DriverProfile.Instance.OnLevelUp += Announce;
            }
        }

        void Announce(int level)
        {
            ToastNotification.Show($"Seviye atladın! Sürücü Seviyesi {level}");
        }
    }
}
