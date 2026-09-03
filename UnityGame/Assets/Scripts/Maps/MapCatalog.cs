using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Maps
{
    // Tüm harita/varyant tanımları tek yerde. Editor'de sürükle-bırak doldur.
    [CreateAssetMenu(menuName = "DreamCar/Map Catalog", fileName = "MapCatalog")]
    public class MapCatalog : ScriptableObject
    {
        public List<MapDefinition> maps = new();
        public MapDefinition Find(string id) => maps.Find(m => m && m.id == id);
    }
}
