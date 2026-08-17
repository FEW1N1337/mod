using System.Collections.Generic;
using DreamCar.GameModes;
using DreamCar.Maps;
using DreamCar.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Genişletilmiş oda oluşturucu: isim + şifre + mod + harita + max oyuncu + görünür/gizli.
    public class RoomCreatorUI : MonoBehaviour
    {
        public TMP_InputField nameInput;
        public TMP_InputField passwordInput;
        public TMP_Dropdown modeDropdown;
        public TMP_Dropdown mapDropdown;
        public Slider maxPlayersSlider;
        public TMP_Text maxPlayersLabel;
        public Toggle visibleToggle;
        public Button createButton;
        public MapCatalog mapCatalog;

        readonly List<MapDefinition> _mapOrder = new();

        void Start()
        {
            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(new List<string> { "Free Roam", "Race", "Drift" });

            mapDropdown.ClearOptions();
            _mapOrder.Clear();
            if (mapCatalog != null)
            {
                var opts = new List<string>();
                foreach (var m in mapCatalog.maps)
                {
                    if (!m) continue;
                    _mapOrder.Add(m);
                    opts.Add(m.displayName);
                }
                mapDropdown.AddOptions(opts);
            }

            maxPlayersSlider.minValue = 2; maxPlayersSlider.maxValue = 16;
            maxPlayersSlider.wholeNumbers = true;
            maxPlayersSlider.onValueChanged.AddListener(v => maxPlayersLabel.text = $"{(int)v} oyuncu");
            maxPlayersSlider.value = 10;

            createButton.onClick.AddListener(OnCreate);
        }

        void OnCreate()
        {
            string roomName = string.IsNullOrWhiteSpace(nameInput.text)
                ? $"Room-{Random.Range(1000, 9999)}"
                : nameInput.text.Trim();

            int modeIndex = modeDropdown.value;
            string mapId = (_mapOrder.Count > 0 && mapDropdown.value < _mapOrder.Count)
                ? _mapOrder[mapDropdown.value].id
                : null;

            RoomPassword.CreateWithPassword(
                roomName,
                passwordInput ? passwordInput.text : null,
                (byte)maxPlayersSlider.value,
                modeIndex,
                mapId,
                visibleToggle ? visibleToggle.isOn : true);
        }
    }
}
