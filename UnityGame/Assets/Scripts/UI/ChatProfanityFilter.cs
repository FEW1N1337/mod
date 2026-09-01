using System.Text.RegularExpressions;
using UnityEngine;

namespace DreamCar.UI
{
    // Küfür/hakaret filtresi. Dahili küçük ilk sürüm liste; genişletmek için
    // Resources/ProfanityList.txt (satır satır kelime) yükler.
    public class ChatProfanityFilter : MonoBehaviour
    {
        public static ChatProfanityFilter Instance { get; private set; }

        static readonly string[] DefaultList =
        {
            "aptal", "salak", "gerizekali", "moron",
            "idiot", "stupid", "dumb"
        };

        Regex _pattern;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        void Build()
        {
            var extra = Resources.Load<TextAsset>("ProfanityList");
            var list = new System.Collections.Generic.List<string>(DefaultList);
            if (extra != null)
            {
                foreach (var line in extra.text.Split('\n'))
                {
                    var w = line.Trim().ToLowerInvariant();
                    if (w.Length >= 3) list.Add(w);
                }
            }

            var escaped = new string[list.Count];
            for (int i = 0; i < list.Count; i++) escaped[i] = Regex.Escape(list[i]);
            _pattern = new Regex(@"\b(" + string.Join("|", escaped) + @")\b", RegexOptions.IgnoreCase);
        }

        public string Sanitize(string message)
        {
            if (string.IsNullOrEmpty(message) || _pattern == null) return message;
            return _pattern.Replace(message, m => new string('*', m.Length));
        }
    }
}
