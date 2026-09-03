using UnityEngine;

namespace DreamCar.Environment
{
    // Anlık gündüz/gece snapshot preset. MapSelector harita varyantı yüklerken
    // DayNightCycle'a bu preset'i uygular (sabit değer, otomatik döngü kesilir).
    [CreateAssetMenu(menuName = "DreamCar/Time of Day Preset", fileName = "TOD_")]
    public class TimeOfDayPreset : ScriptableObject
    {
        [Range(0f, 1f)] public float timeOfDay = 0.5f;
        public bool freeze = true;
        public float sunIntensity = 1f;
        public Color sunColor = Color.white;
        public Color ambientColor = new Color(0.5f, 0.55f, 0.6f);

        public void ApplyTo(DayNightCycle cycle)
        {
            if (!cycle) return;
            cycle.startTimeOfDay = timeOfDay;
            if (freeze) cycle.dayLengthSeconds = 0f;
            if (cycle.sun)
            {
                cycle.sun.color = sunColor;
                cycle.sun.intensity = sunIntensity;
            }
            RenderSettings.ambientLight = ambientColor;
        }
    }
}
