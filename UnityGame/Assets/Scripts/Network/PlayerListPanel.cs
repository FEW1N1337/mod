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
        // Rapor akışı ve ses susturma buradan tetikleniyor. ReportPlayer'ın
        // Open(Player)'ının ve PlayerVoiceMute.ToggleMute'un proje genelinde
        // hiçbir çağıranı yoktu; bu panel de hiçbir sahnede yoktu, yani
        // oyuncuyu atmanın, raporlamanın ya da susturmanın hiçbir yolu
        // bulunmuyordu. Mağaza incelemesi için rapor akışı gerekiyor.
        public Moderation.ReportPlayer report;
        public float refreshInterval = 1f;

        float _next;

        // "void OnEnable()" taban sınıfın "public virtual void OnEnable()"
        // metodunu gizliyordu; o metot PhotonNetwork.AddCallbackTarget(this)
        // çağırıyor. Gizlendiği için oyuncu giriş/çıkış callback'leri hiç
        // tetiklenmiyordu: liste yalnızca panel açıldığı anda dolup sonra
        // donuyordu.
        public override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }
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
                // entryPrefab sahnede kapalı duran bir şablon; klon da kapalı
                // doğar ve satırlar hem görünmez hem metinsiz kalırdı.
                go.SetActive(true);
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label)
                {
                    string ping = p.IsLocal ? $"{PhotonNetwork.GetPing()}ms" : "-";
                    string master = p.IsMasterClient ? " ★" : "";
                    label.text = $"{p.NickName}{master}  ({ping})";
                }

                var target = p;

                // Butonlar ADIYLA bulunuyor: satırda üç tane var ve
                // GetComponentInChildren<Button>() hep ilkini döndürürdü.
                var kick = FindButton(go, "KickButton");
                if (kick)
                {
                    bool canKick = PhotonNetwork.IsMasterClient && !p.IsLocal;
                    kick.gameObject.SetActive(canKick);
                    kick.onClick.AddListener(() => PhotonNetwork.CloseConnection(target));
                }

                var reportBtn = FindButton(go, "ReportButton");
                if (reportBtn)
                {
                    reportBtn.gameObject.SetActive(!p.IsLocal);
                    reportBtn.onClick.AddListener(() => { if (report) report.Open(target); });
                }

                var muteBtn = FindButton(go, "MuteButton");
                if (muteBtn)
                {
                    muteBtn.gameObject.SetActive(!p.IsLocal);
                    var muteLabel = muteBtn.GetComponentInChildren<TMP_Text>();
                    RefreshMuteLabel(muteLabel, target);
                    muteBtn.onClick.AddListener(() =>
                    {
                        var vm = Voice.PlayerVoiceMute.Instance;
                        if (!vm) return;
                        vm.ToggleMute(target);
                        RefreshMuteLabel(muteLabel, target);
                    });
                }
            }
        }

        static void RefreshMuteLabel(TMP_Text label, Photon.Realtime.Player p)
        {
            if (!label) return;
            var vm = Voice.PlayerVoiceMute.Instance;
            label.text = vm && vm.IsMuted(p) ? "Aç" : "Sustur";
        }

        static Button FindButton(GameObject row, string name)
        {
            var t = row.transform.Find(name);
            return t ? t.GetComponent<Button>() : null;
        }
    }
}
