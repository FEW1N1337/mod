// Yerel bildirim planlayıcı. Sunucu gerektirmez — günlük ödül hatırlatması gibi
// zamanlı bildirimleri cihazda kurar.
// Aktive etmek için: Package Manager → "Mobile Notifications" import
// (com.unity.mobile.notifications). Define otomatik gelir: UNITY_NOTIFICATIONS
using System;
using UnityEngine;

#if UNITY_IOS && UNITY_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

namespace DreamCar.Notifications
{
    public class LocalNotificationScheduler : MonoBehaviour
    {
        public static LocalNotificationScheduler Instance { get; private set; }

        [Header("Günlük ödül hatırlatması")]
        public bool enableDailyReminder = true;
        [Range(0, 23)] public int reminderHour = 20;
        public string reminderTitle = "Günlük ödülün hazır!";
        public string reminderBody = "Bugünkü ödülünü almadın — seri bozulmasın.";

        const string DailyClaimKey = "daily.lastClaimUtc";
        const string ReminderIdKey = "notif.dailyId";

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            RequestAuthorization();
            if (enableDailyReminder) RescheduleDailyReminder();
        }

        void OnApplicationPause(bool paused)
        {
            // Uygulamadan çıkarken güncel duruma göre yeniden planla.
            if (paused && enableDailyReminder) RescheduleDailyReminder();
        }

        public void RequestAuthorization()
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            StartCoroutine(RequestIosAuthorization());
#endif
        }

#if UNITY_IOS && UNITY_NOTIFICATIONS
        System.Collections.IEnumerator RequestIosAuthorization()
        {
            var options = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using var req = new AuthorizationRequest(options, true);
            while (!req.IsFinished) yield return null;
            if (!req.Granted) Debug.Log("[Notif] Bildirim izni verilmedi.");
        }
#endif

        // Bugün ödül alındıysa yarın, alınmadıysa bugün belirtilen saate kurar.
        public void RescheduleDailyReminder()
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            CancelDailyReminder();

            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, reminderHour, 0, 0);

            bool claimedToday = false;
            var raw = PlayerPrefs.GetString(DailyClaimKey, "");
            if (DateTime.TryParse(raw, out var lastClaim))
                claimedToday = lastClaim.ToLocalTime().Date == now.Date;

            if (claimedToday || target <= now) target = target.AddDays(1);

            var trigger = new iOSNotificationCalendarTrigger
            {
                Year = target.Year,
                Month = target.Month,
                Day = target.Day,
                Hour = target.Hour,
                Minute = target.Minute,
                Repeats = false
            };

            var id = "dreamcar_daily_" + target.ToString("yyyyMMdd");
            var notification = new iOSNotification
            {
                Identifier = id,
                Title = reminderTitle,
                Body = reminderBody,
                ShowInForeground = false,
                Trigger = trigger
            };

            iOSNotificationCenter.ScheduleNotification(notification);
            PlayerPrefs.SetString(ReminderIdKey, id);
            PlayerPrefs.Save();
#endif
        }

        public void CancelDailyReminder()
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            var id = PlayerPrefs.GetString(ReminderIdKey, "");
            if (!string.IsNullOrEmpty(id)) iOSNotificationCenter.RemoveScheduledNotification(id);
#endif
        }

        // Özel bildirim (örn. "yarışın 5 dk sonra başlıyor").
        public void ScheduleIn(TimeSpan delay, string title, string body, string id = null)
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay,
                Repeats = false
            };
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id ?? Guid.NewGuid().ToString("N"),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = trigger
            });
#endif
        }
    }
}
