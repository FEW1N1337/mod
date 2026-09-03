using DreamCar.Maps;
using DreamCar.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    public class LobbyUI : MonoBehaviour
    {
        public TMP_InputField createRoomInput;
        public Button createButton;
        public Button quickJoinButton;
        public Transform roomListParent;
        public GameObject roomEntryPrefab;

        [Tooltip("Oda satırında harita adını çözmek için. LobbyManager'daki ile aynı varlık.")]
        public MapCatalog mapCatalog;

        [Tooltip("Oda adına göre canlı filtre. Boşsa bütün odalar listelenir.")]
        public TMP_InputField searchInput;

        void OnEnable()
        {
            if (LobbyManager.Instance)
                LobbyManager.Instance.OnRoomListChanged += Refresh;

            // Butonlar bağlanmamışsa OnEnable NullReferenceException atıp Refresh()'e hiç
            // gelmiyor, oda listesi de boş kalıyordu — dosyanın geri kalanındaki gibi koru.
            if (createButton)
                createButton.onClick.AddListener(() => LobbyManager.Instance?.CreateRoom(createRoomInput ? createRoomInput.text : null));
            if (quickJoinButton)
                quickJoinButton.onClick.AddListener(() => LobbyManager.Instance?.JoinRandom());
            if (searchInput)
                searchInput.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        void OnDisable()
        {
            if (LobbyManager.Instance)
                LobbyManager.Instance.OnRoomListChanged -= Refresh;
            if (createButton) createButton.onClick.RemoveAllListeners();
            if (quickJoinButton) quickJoinButton.onClick.RemoveAllListeners();
            if (searchInput) searchInput.onValueChanged.RemoveAllListeners();
        }

        void Refresh()
        {
            if (!roomListParent || !roomEntryPrefab || LobbyManager.Instance == null) return;

            for (int i = roomListParent.childCount - 1; i >= 0; i--)
                Destroy(roomListParent.GetChild(i).gameObject);

            string filter = searchInput ? searchInput.text : null;
            bool filtering = !string.IsNullOrWhiteSpace(filter);

            foreach (var kv in LobbyManager.Instance.Rooms)
            {
                var info = kv.Value;
                if (filtering &&
                    info.Name.IndexOf(filter.Trim(), System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var go = Instantiate(roomEntryPrefab, roomListParent);
                // roomEntryPrefab sahnede kapalı duran bir şablonsa klon da kapalı doğar.
                go.SetActive(true);

                // Sütunlar ADLA bulunuyor, sıraya göre değil: şablona ileride bir
                // çocuk eklenirse dizin tabanlı erişim sessizce yanlış alanı yazardı.
                SetText(go, "Name", info.Name);
                SetText(go, "Count", $"{info.PlayerCount}/{info.MaxPlayers}");
                SetText(go, "Map", MapLabel(info));

                // Kilit: şifre bilgisi lobiye yayımlanıyor (RoomPassword.Lobby),
                // ama bugüne kadar hiç gösterilmiyordu — oyuncu şifreli bir odaya
                // tıklayıp neden giremediğini anlamıyordu.
                var lockGo = Find(go, "Lock");
                if (lockGo) lockGo.SetActive(RoomPassword.IsPasswordProtected(info));

                var btn = go.GetComponent<Button>();
                string roomName = info.Name;
                if (btn) btn.onClick.AddListener(() => LobbyManager.Instance.JoinRoom(roomName));
            }
        }

        // Harita kimliğini okunur ada çevirir. MapDefinition.displayName zaten
        // zaman dilimini içeriyor ("Otoyol (Gece)"), ayrı bir alan gerekmiyor.
        string MapLabel(Photon.Realtime.RoomInfo info)
        {
            if (!info.CustomProperties.TryGetValue(RoomPassword.MapKey, out object idObj)) return "";
            string id = idObj as string;
            if (string.IsNullOrEmpty(id)) return "";

            if (mapCatalog)
            {
                var def = mapCatalog.Find(id);
                if (def && !string.IsNullOrEmpty(def.displayName)) return def.displayName;
            }
            // Katalog bağlı değilse ham kimlik, hiç yoktan iyidir.
            return id;
        }

        static GameObject Find(GameObject root, string childName)
        {
            var t = root.transform.Find(childName);
            return t ? t.gameObject : null;
        }

        static void SetText(GameObject root, string childName, string value)
        {
            var child = Find(root, childName);
            if (!child) return;
            var text = child.GetComponent<TMP_Text>();
            if (text) text.text = value;
        }
    }
}
