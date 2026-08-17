using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Monetization
{
    // Firebase Analytics veya Unity Analytics'e log basmak için tek nokta.
    // Package eklenene kadar log'a yazar.
    public static class Analytics
    {
        public static void Event(string name, Dictionary<string, object> parameters = null)
        {
#if UNITY_ANALYTICS
            var custom = new Dictionary<string, object>(parameters ?? new Dictionary<string, object>());
            UnityEngine.Analytics.AnalyticsEvent.Custom(name, custom);
#elif FIREBASE_ANALYTICS
            var list = new List<Firebase.Analytics.Parameter>();
            if (parameters != null)
                foreach (var kv in parameters)
                    list.Add(new Firebase.Analytics.Parameter(kv.Key, kv.Value?.ToString() ?? ""));
            Firebase.Analytics.FirebaseAnalytics.LogEvent(name, list.ToArray());
#else
            string p = parameters != null ? string.Join(",", parameters) : "";
            Debug.Log($"[Analytics] {name} {{{p}}}");
#endif
        }

        public static void LevelStart(string level) => Event("level_start", new() { { "level", level } });
        public static void RaceFinished(float seconds) => Event("race_finished", new() { { "duration", seconds } });
        public static void CarPurchased(string carId, long price) => Event("car_purchased", new() { { "car", carId }, { "price", price } });
        public static void IAP(string productId, string currency, float price) => Event("iap", new() { { "product", productId }, { "currency", currency }, { "price", price } });
    }
}
