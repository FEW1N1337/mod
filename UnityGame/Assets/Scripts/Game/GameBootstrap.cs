using DreamCar.Network;
using UnityEngine;

namespace DreamCar.Game
{
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        public GameObject photonConnectorPrefab;

        void Awake()
        {
            NicknameManager.Apply();

            if (PhotonConnector.Instance == null)
            {
                if (photonConnectorPrefab)
                {
                    Instantiate(photonConnectorPrefab);
                }
                else
                {
                    var go = new GameObject("~PhotonConnector");
                    go.AddComponent<PhotonConnector>();
                }
            }

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
