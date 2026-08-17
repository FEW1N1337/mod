using Photon.Pun;
using UnityEngine;

namespace DreamCar.Emote
{
    [RequireComponent(typeof(PhotonView))]
    public class HornController : MonoBehaviourPun
    {
        public AudioSource horn;

        public void Press()
        {
            if (photonView.IsMine) photonView.RPC(nameof(RPC_Horn), RpcTarget.All);
        }

        [PunRPC]
        void RPC_Horn()
        {
            if (horn && !horn.isPlaying) horn.Play();
        }
    }
}
