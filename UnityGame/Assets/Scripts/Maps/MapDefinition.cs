using UnityEngine;
using DreamCar.Environment;

namespace DreamCar.Maps
{
    // Tek harita = base sahne + varyant preset (weather + time-of-day).
    // 1 gerçek sahne × N varyant = N farklı görünüm, tek asset yükü.
    [CreateAssetMenu(menuName = "DreamCar/Map Definition", fileName = "Map_")]
    public class MapDefinition : ScriptableObject
    {
        public string id = "map.city";
        public string displayName = "City";
        [Tooltip("Build Settings'e eklenmiş sahne adı — PhotonNetwork.LoadLevel bunu yükler.")]
        public string sceneName = "Game";
        public Sprite thumbnail;

        public Weather.Type weather = Weather.Type.Clear;
        [Range(0f, 1f)] public float timeOfDay = 0.5f;
    }
}
