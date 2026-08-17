using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace DreamCar.Emote
{
    // Basit emote sistemi: 8 ön tanımlı emote, RPC ile herkesin ekranında ikon belirir.
    public class EmoteSystem : MonoBehaviourPun
    {
        [System.Serializable] public class EmoteEntry { public string id; public Sprite icon; public AudioClip sfx; }

        public List<EmoteEntry> emotes = new();
        public Transform overheadAnchor;
        public GameObject emotePopupPrefab;
        public AudioSource audioSource;

        public void Play(string id)
        {
            if (photonView.IsMine) photonView.RPC(nameof(RPC_Play), RpcTarget.All, id);
        }

        [PunRPC]
        void RPC_Play(string id)
        {
            var entry = emotes.Find(e => e.id == id);
            if (entry == null) return;

            if (audioSource && entry.sfx) audioSource.PlayOneShot(entry.sfx);
            if (overheadAnchor && emotePopupPrefab)
            {
                var go = Instantiate(emotePopupPrefab, overheadAnchor.position + Vector3.up * 2f, Quaternion.identity, overheadAnchor);
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr) sr.sprite = entry.icon;
                Destroy(go, 2.5f);
            }
        }
    }
}
