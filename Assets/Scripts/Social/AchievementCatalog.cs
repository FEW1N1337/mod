using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Social
{
    // Tüm başarım tanımlarının merkezi kataloğu.
    [CreateAssetMenu(menuName = "DreamCar/Achievement Catalog", fileName = "AchievementCatalog")]
    public class AchievementCatalog : ScriptableObject
    {
        public List<AchievementDefinition> achievements = new();
        public AchievementDefinition Find(string id) => achievements.Find(a => a && a.id == id);
    }

    [CreateAssetMenu(menuName = "DreamCar/Achievement Definition", fileName = "Ach_")]
    public class AchievementDefinition : ScriptableObject
    {
        public string id = "ach.first_win";
        public string displayName = "İlk Zafer";
        [TextArea] public string description = "İlk yarışı kazan.";
        public Sprite icon;
        public long moneyReward = 1000;

        [Tooltip("Sunucu tarafında takip edilen statistic adı.")]
        public string statistic = "raceWins";
        public int threshold = 1;
    }
}
