using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Economy
{
    // Satın alınabilir tüm arabaların merkezi kataloğu. ScriptableObject olduğu için
    // Editor'de Assets → Create → DreamCar → Car Catalog ile oluşturup içine
    // CarDefinition'lar sürüklersin.
    [CreateAssetMenu(menuName = "DreamCar/Car Catalog", fileName = "CarCatalog")]
    public class CarCatalog : ScriptableObject
    {
        public List<CarDefinition> cars = new();

        public CarDefinition Find(string id) => cars.Find(c => c && c.id == id);
    }

    [CreateAssetMenu(menuName = "DreamCar/Car Definition", fileName = "Car_")]
    public class CarDefinition : ScriptableObject
    {
        public string id = "car.default";
        public string displayName = "Sport Coupe";
        public long price = 15000;
        public Sprite thumbnail;
        [Tooltip("Prefab name under Assets/Resources/ (PhotonNetwork.Instantiate uses this string).")]
        public string resourcePrefabName = "Car";

        // Menü garajındaki 3B önizleme için AYRI prefab: yalnızca görünen
        // hiyerarşi, Rigidbody/PhotonView/collider yok. Asıl prefabı menüde
        // doğurmak odaya bağlı olmadan hata üretiyor.
        // Dize kuralıyla ("Preview_" + ad) çözmek sessizce bozulabilecek bir
        // varsayım olurdu; alan açık duruyor.
        public string previewPrefabName = "";

        [Header("Stats (display only)")]
        [Range(0, 10)] public int speedStat = 7;
        [Range(0, 10)] public int accelerationStat = 7;
        [Range(0, 10)] public int handlingStat = 6;

        [Header("Actual physics overrides")]
        public float maxMotorTorque = 1500f;
        public float topSpeedKmh = 180f;
    }
}
