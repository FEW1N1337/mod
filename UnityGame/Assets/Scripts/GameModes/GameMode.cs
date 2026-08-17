using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DreamCar.GameModes
{
    public enum GameModeType { Free = 0, Race = 1, Drift = 2 }

    public abstract class GameModeBase : MonoBehaviourPunCallbacks
    {
        public abstract GameModeType Type { get; }
        public virtual void OnModeStart() { }
        public virtual void OnModeEnd() { }
        public virtual void OnPlayerJoinedRoomCustom(Player p) { }
        public virtual void OnScore(int actorNumber, int amount) { }
    }
}
