using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;

#if PHOTON_VOICE_DEFINED
using Photon.Voice.Unity;
#endif

namespace DreamCar.Voice
{
    // Oyuncu bazlı yerel susturma. PlayerListPanel satırlarındaki mute butonu bunu çağırır.
    // Sadece yerel duyuşu keser; karşı taraf konuşmaya devam eder.
    public class PlayerVoiceMute : MonoBehaviour
    {
        public static PlayerVoiceMute Instance { get; private set; }

        const string PrefKey = "voice.muted";

        readonly HashSet<string> _muted = new();

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public bool IsMuted(Player p) => p != null && !string.IsNullOrEmpty(p.UserId) && _muted.Contains(p.UserId);

        public void ToggleMute(Player p)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId)) return;
            if (!_muted.Add(p.UserId)) _muted.Remove(p.UserId);
            Save();
            Apply();
        }

        public void SetMuted(Player p, bool muted)
        {
            if (p == null || string.IsNullOrEmpty(p.UserId)) return;
            if (muted) _muted.Add(p.UserId); else _muted.Remove(p.UserId);
            Save();
            Apply();
        }

        // Sahnedeki tüm Speaker'ları gezip ilgili oyuncunun sesini kısar.
        public void Apply()
        {
#if PHOTON_VOICE_DEFINED
            foreach (var speaker in FindObjectsByType<Speaker>(FindObjectsSortMode.None))
            {
                if (speaker == null || speaker.Actor == null) continue;
                var src = speaker.GetComponent<AudioSource>();
                if (src == null) continue;
                src.mute = _muted.Contains(speaker.Actor.UserId);
            }
#endif
        }

        void Load()
        {
            _muted.Clear();
            foreach (var s in PlayerPrefs.GetString(PrefKey, "").Split(','))
                if (!string.IsNullOrWhiteSpace(s)) _muted.Add(s.Trim());
        }

        void Save()
        {
            PlayerPrefs.SetString(PrefKey, string.Join(",", _muted));
            PlayerPrefs.Save();
        }
    }
}
