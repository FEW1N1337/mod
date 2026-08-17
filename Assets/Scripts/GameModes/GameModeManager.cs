using Photon.Pun;
using UnityEngine;
using DreamCar.Race;

namespace DreamCar.GameModes
{
    // Sahne yüklendiğinde room custom property "mode"'a bakıp uygun mod bileşenini
    // AddComponent eder ve OnModeStart çağırır.
    public class GameModeManager : MonoBehaviour
    {
        public const string ModePropKey = "mode";
        public GameModeBase Active { get; private set; }

        void Start() => SpawnForRoom();

        public void SpawnForRoom()
        {
            int modeInt = 0;
            if (PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ModePropKey, out object v) && v is int i)
                modeInt = i;

            var type = (GameModeType)modeInt;
            switch (type)
            {
                case GameModeType.Race:
                    if (!GetComponent<RaceManager>()) gameObject.AddComponent<RaceManager>();
                    Active = gameObject.AddComponent<RaceMode>();
                    break;
                case GameModeType.Drift:
                    Active = gameObject.AddComponent<DriftMode>();
                    break;
                default:
                    Active = gameObject.AddComponent<FreeRoamMode>();
                    break;
            }
            Active.OnModeStart();
        }
    }
}
