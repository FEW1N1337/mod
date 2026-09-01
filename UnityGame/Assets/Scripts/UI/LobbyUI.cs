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
            Refresh();
        }

        void OnDisable()
        {
            if (LobbyManager.Instance)
                LobbyManager.Instance.OnRoomListChanged -= Refresh;
            if (createButton) createButton.onClick.RemoveAllListeners();
            if (quickJoinButton) quickJoinButton.onClick.RemoveAllListeners();
        }

        void Refresh()
        {
            if (!roomListParent || !roomEntryPrefab || LobbyManager.Instance == null) return;

            for (int i = roomListParent.childCount - 1; i >= 0; i--)
                Destroy(roomListParent.GetChild(i).gameObject);

            foreach (var kv in LobbyManager.Instance.Rooms)
            {
                var go = Instantiate(roomEntryPrefab, roomListParent);
                // roomEntryPrefab sahnede kapalı duran bir şablonsa klon da kapalı doğar.
                go.SetActive(true);
                var label = go.GetComponentInChildren<TMP_Text>();
                if (label) label.text = $"{kv.Value.Name}  ({kv.Value.PlayerCount}/{kv.Value.MaxPlayers})";
                var btn = go.GetComponent<Button>();
                string name = kv.Value.Name;
                if (btn) btn.onClick.AddListener(() => LobbyManager.Instance.JoinRoom(name));
            }
        }
    }
}
