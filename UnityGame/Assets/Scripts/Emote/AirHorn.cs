using System.Collections;
using Photon.Pun;
using UnityEngine;

namespace DreamCar.Emote
{
    // Ritmik korna: bir nota dizisini sırayla çalar. Musical horn tuşu basılınca RPC ile
    // etraftaki oyunculara yayılır. Nota AudioClip'leri Editor'de dizilir (do-re-mi vs).
    [RequireComponent(typeof(PhotonView))]
    public class AirHorn : MonoBehaviourPun
    {
        public AudioSource source;
        public AudioClip[] notes;
        public float noteSpacingSeconds = 0.18f;
        [Range(0f, 1f)] public float volume = 1f;

        public void Play(int patternIndex = 0)
        {
            if (photonView.IsMine) photonView.RPC(nameof(RPC_Play), RpcTarget.All, patternIndex);
        }

        [PunRPC]
        void RPC_Play(int patternIndex) => StartCoroutine(PlaySequence(patternIndex));

        IEnumerator PlaySequence(int patternIndex)
        {
            if (notes == null || notes.Length == 0 || !source) yield break;

            int[] pattern = GetPattern(patternIndex, notes.Length);
            foreach (var idx in pattern)
            {
                if (idx < 0 || idx >= notes.Length || !notes[idx]) continue;
                source.PlayOneShot(notes[idx], volume);
                yield return new WaitForSeconds(noteSpacingSeconds);
            }
        }

        static int[] GetPattern(int index, int max)
        {
            switch (index)
            {
                case 1: return new[] { 0, 2, 4, 5 };
                case 2: return new[] { 5, 4, 2, 0 };
                case 3: return new[] { 0, 0, 2, 2, 4 };
                default:
                {
                    var arr = new int[max];
                    for (int i = 0; i < max; i++) arr[i] = i;
                    return arr;
                }
            }
        }
    }
}
