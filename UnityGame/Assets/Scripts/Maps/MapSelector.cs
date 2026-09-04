using Photon.Pun;
using UnityEngine;
using DreamCar.Environment;

namespace DreamCar.Maps
{
    // Sahne yüklendikten sonra room custom property "map"'a bakıp o haritanın
    // Weather + TimeOfDay preset'ini sahnedeki bileşenlere uygular.
    public class MapSelector : MonoBehaviour
    {
        public const string MapPropKey = "map";
        public MapCatalog catalog;

        void Start() => ApplyForRoom();

        public void ApplyForRoom()
        {
            if (!catalog || !PhotonNetwork.InRoom) return;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapPropKey, out object idObj)) return;
            var def = catalog.Find(idObj as string);
            if (!def) return;

            var weather = FindAnyObjectByType<Weather>();
            if (weather) weather.SetType(def.weather);

            var day = FindAnyObjectByType<DayNightCycle>();
            if (day) { day.startTimeOfDay = def.timeOfDay; day.enabled = false; day.enabled = true; }
        }

        public static void LoadMap(MapDefinition def)
        {
            if (!def || !PhotonNetwork.IsMasterClient) return;
            var props = new ExitGames.Client.Photon.Hashtable { { MapPropKey, def.id } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            PhotonNetwork.LoadLevel(def.sceneName);
        }
    }
}
