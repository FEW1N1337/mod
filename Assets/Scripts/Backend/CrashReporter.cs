using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DreamCar.Backend
{
    // Cihazda çökme/exception olduğunda hiçbir kayıt tutulmuyordu. Bu bileşen
    // yakalanmamış exception'ları toplar, son N log satırıyla birlikte saklar ve
    // (varsa) Firebase Crashlytics'e iletir. Yoksa PlayerPrefs'e yazıp bir sonraki
    // açılışta destek e-postasına eklenebilir hale getirir.
    public class CrashReporter : MonoBehaviour
    {
        public static CrashReporter Instance { get; private set; }

        public int breadcrumbCount = 40;
        public bool logHandledExceptions = true;

        const string PendingKey = "crash.pending.v1";

        readonly Queue<string> _breadcrumbs = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.logMessageReceived += OnLog;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;

#if FIREBASE_CRASHLYTICS
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
#endif
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            Breadcrumb($"[{type}] {condition}");

            if (type != LogType.Exception && type != LogType.Error) return;
            if (type == LogType.Error && !logHandledExceptions) return;

            Report(condition, stackTrace, fatal: type == LogType.Exception);
        }

        void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) Report(ex.Message, ex.StackTrace, fatal: true);
        }

        public void Breadcrumb(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message.Length > 200) message = message.Substring(0, 200);
            _breadcrumbs.Enqueue($"{Time.realtimeSinceStartup:0.0}s {message}");
            while (_breadcrumbs.Count > breadcrumbCount) _breadcrumbs.Dequeue();

#if FIREBASE_CRASHLYTICS
            Firebase.Crashlytics.Crashlytics.Log(message);
#endif
        }

        void Report(string message, string stackTrace, bool fatal)
        {
            string payload = BuildReport(message, stackTrace, fatal);

#if FIREBASE_CRASHLYTICS
            Firebase.Crashlytics.Crashlytics.LogException(new Exception(message + "\n" + stackTrace));
#else
            // Bir sonraki açılışta gönderilmek üzere sakla.
            PlayerPrefs.SetString(PendingKey, payload);
            PlayerPrefs.Save();
#endif
            if (fatal) Debug.LogWarning("[CrashReporter] Fatal kaydedildi.");
        }

        string BuildReport(string message, string stackTrace, bool fatal)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"fatal={fatal}");
            sb.AppendLine($"time={DateTime.UtcNow:o}");
            sb.AppendLine($"version={Application.version}");
            sb.AppendLine($"platform={Application.platform}");
            sb.AppendLine($"device={SystemInfo.deviceModel}");
            sb.AppendLine($"os={SystemInfo.operatingSystem}");
            sb.AppendLine($"memory={SystemInfo.systemMemorySize}MB");
            sb.AppendLine($"playFabId={(PlayFabAuth.Instance ? PlayFabAuth.Instance.PlayFabId ?? "-" : "-")}");
            sb.AppendLine($"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            sb.AppendLine("--- message ---");
            sb.AppendLine(message);
            sb.AppendLine("--- stack ---");
            sb.AppendLine(stackTrace);
            sb.AppendLine("--- breadcrumbs ---");
            foreach (var b in _breadcrumbs) sb.AppendLine(b);
            return sb.ToString();
        }

        // Destek ekranı bunu okuyup mailto gövdesine ekleyebilir.
        public static string ConsumePendingReport()
        {
            var report = PlayerPrefs.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(report))
            {
                PlayerPrefs.DeleteKey(PendingKey);
                PlayerPrefs.Save();
            }
            return report;
        }

        public static bool HasPendingReport() => !string.IsNullOrEmpty(PlayerPrefs.GetString(PendingKey, ""));
    }
}
