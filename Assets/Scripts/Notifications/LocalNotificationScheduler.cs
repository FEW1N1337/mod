// Yerel bildirim planlayıcı — sunucu gerektirmez. Günlük ödül hatırlatması gibi
// zamanlı bildirimleri cihazda kurar. iOS ve Android ayrı ayrı desteklenir.
//
// Kurulum: Package Manager → "Mobile Notifications" (com.unity.mobile.notifications)
// Define: UNITY_NOTIFICATIONS (paket otomatik ekler)
using System;
using UnityEngine;

#if UNITY_IOS && UNITY_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

#if UNITY_ANDROID && UNITY_NOTIFICATIONS
using Unity.Notifications.Android;
using UnityEngine.Android;
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

        [Header("Android kanalı")]
        public string androidChannelId = "dreamcar_reminders";
        public string androidChannelName = "Hatırlatmalar";
        public string androidChannelDescription = "Günlük ödül ve etkinlik hatırlatmaları";

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
#if UNITY_ANDROID && UNITY_NOTIFICATIONS
            EnsureAndroidChannel();
#endif
            if (enableDailyReminder) RescheduleDailyReminder();
        }

        void OnApplicationPause(bool paused)
        {
            // Uygulamadan çıkarken güncel duruma göre yeniden planla.
            if (paused && enableDailyReminder) RescheduleDailyReminder();
        }

        // ---------------------------------------------------------- İzin
        public void RequestAuthorization()
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            StartCoroutine(RequestIosAuthorization());
#elif UNITY_ANDROID && UNITY_NOTIFICATIONS
            RequestAndroidAuthorization();
#endif
        }

#if UNITY_IOS && UNITY_NOTIFICATIONS
        System.Collections.IEnumerator RequestIosAuthorization()
        {
            var options = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using var req = new AuthorizationRequest(options, true);
            while (!req.IsFinished) yield return null;
            if (!req.Granted) Debug.Log("[Notif] iOS bildirim izni verilmedi.");
        }
#endif

#if UNITY_ANDROID && UNITY_NOTIFICATIONS
        // Android 13 (API 33) ve üzeri POST_NOTIFICATIONS izni ister.
        void RequestAndroidAuthorization()
        {
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!Permission.HasUserAuthorizedPermission(permission))
                Permission.RequestUserPermission(permission);
        }

        void EnsureAndroidChannel()
        {
            var channel = new AndroidNotificationChannel
            {
                Id = androidChannelId,
                Name = androidChannelName,
                Description = androidChannelDescription,
                Importance = Importance.Default,
                CanShowBadge = true,
                EnableVibration = true,
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
        }
#endif

        // ---------------------------------------------------------- Günlük hatırlatma
        // Bugün ödül alındıysa yarına, alınmadıysa bugünkü saate kurar.
        public void RescheduleDailyReminder()
        {
            DateTime target = ComputeNextReminderTime();

#if UNITY_IOS && UNITY_NOTIFICATIONS
            ScheduleIosDaily(target);
#elif UNITY_ANDROID && UNITY_NOTIFICATIONS
            ScheduleAndroidDaily(target);
#endif
        }

        DateTime ComputeNextReminderTime()
        {
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, reminderHour, 0, 0);

            bool claimedToday = false;
            var raw = PlayerPrefs.GetString(DailyClaimKey, "");
            if (DateTime.TryParse(raw, out var lastClaim))
                claimedToday = lastClaim.ToLocalTime().Date == now.Date;

            if (claimedToday || target <= now) target = target.AddDays(1);
            return target;
        }

#if UNITY_IOS && UNITY_NOTIFICATIONS
        void ScheduleIosDaily(DateTime target)
        {
            CancelDailyReminder();

            var trigger = new iOSNotificationCalendarTrigger
            {
                Year = target.Year, Month = target.Month, Day = target.Day,
                Hour = target.Hour, Minute = target.Minute,
                Repeats = false
            };

            var id = "dreamcar_daily_" + target.ToString("yyyyMMdd");
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id,
                Title = reminderTitle,
                Body = reminderBody,
                ShowInForeground = false,
                Trigger = trigger
            });

            PlayerPrefs.SetString(ReminderIdKey, id);
            PlayerPrefs.Save();
        }
#endif

#if UNITY_ANDROID && UNITY_NOTIFICATIONS
        void ScheduleAndroidDaily(DateTime target)
        {
            CancelDailyReminder();

            var notification = new AndroidNotification
            {
                Title = reminderTitle,
                Text = reminderBody,
                FireTime = target,
                SmallIcon = "icon_0",   // Player Settings → Android → Notification Icons
                LargeIcon = "icon_1",
                ShouldAutoCancel = true,
            };

            int id = AndroidNotificationCenter.SendNotification(notification, androidChannelId);
            PlayerPrefs.SetString(ReminderIdKey, id.ToString());
            PlayerPrefs.Save();
        }
#endif

        public void CancelDailyReminder()
        {
            var stored = PlayerPrefs.GetString(ReminderIdKey, "");
            if (string.IsNullOrEmpty(stored)) return;

#if UNITY_IOS && UNITY_NOTIFICATIONS
            iOSNotificationCenter.RemoveScheduledNotification(stored);
#elif UNITY_ANDROID && UNITY_NOTIFICATIONS
            if (int.TryParse(stored, out int id))
                AndroidNotificationCenter.CancelScheduledNotification(id);
#endif
        }

        // ---------------------------------------------------------- Özel bildirim
        // Örn. "yarışın 5 dk sonra başlıyor".
        public void ScheduleIn(TimeSpan delay, string title, string body, string id = null)
        {
#if UNITY_IOS && UNITY_NOTIFICATIONS
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id ?? Guid.NewGuid().ToString("N"),
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = delay, Repeats = false }
            });
#elif UNITY_ANDROID && UNITY_NOTIFICATIONS
            AndroidNotificationCenter.SendNotification(new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = DateTime.Now.Add(delay),
                SmallIcon = "icon_0",
                ShouldAutoCancel = true,
            }, androidChannelId);
#endif
        }

        // Uygulama bildirime tıklanarak açıldıysa hangi bildirim olduğunu döner.
        public string GetLaunchNotificationId()
        {
#if UNITY_ANDROID && UNITY_NOTIFICATIONS
            var intent = AndroidNotificationCenter.GetLastNotificationIntent();
            return intent?.Id.ToString();
#elif UNITY_IOS && UNITY_NOTIFICATIONS
            var notification = iOSNotificationCenter.GetLastRespondedNotification();
            return notification?.Identifier;
#else
            return null;
#endif
        }
    }
}
