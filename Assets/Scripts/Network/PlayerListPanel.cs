using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Network
{
    // Odadaki oyuncuları listeler, ping gösterir. Master client kick butonu görür.
    public class PlayerListPanel : MonoBehaviourPunCallbacks
    {
        public Transform listParent;
        public GameObject entryPrefab;
        public float refreshInterval = 1f;

        float _next;

        void OnEnable() => Refresh();
        void Update() { if (Time.time >= _next) { _next = Time.time + refreshInterval; Refresh(); } }

        public override void OnPlayerEnteredRoom(Player p) => Refresh();
        public override void OnPlayerLeftRoom(Player p) => Refresh();
        public override void OnMasterClientSwitched(Player p) => Refresh();

        void Refresh()
        {
            if (!listParent || !entryPrefab || !PhotonNetwork.InRoom) return;

            for (int i = listParent.childCount - 1; i >= 0; i--) Destroy(listParent.GetChild(i).gameObject);

            foreach (var kv in PhotonNetwork.CurrentRoom.Players)
            {
                var p = kv.Value;
                var go = Instantiate(entryPrefab, listParent);
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label)
                {
                    string ping = p.IsLocal ? $"{PhotonNetwork.GetPing()}ms" : "-";
                    string master = p.IsMasterClient ? " ★" : "";
                    label.text = $"{p.NickName}{master}  ({ping})";
                }

                var btn = go.GetComponentInChildren<Button>();
                if (btn)
                {
                    bool canKick = PhotonNetwork.IsMasterClient && !p.IsLocal;
                    btn.gameObject.SetActive(canKick);
                    var target = p;
                    btn.onClick.AddListener(() => PhotonNetwork.CloseConnection(target));
                }
            }
        }
    }
}
