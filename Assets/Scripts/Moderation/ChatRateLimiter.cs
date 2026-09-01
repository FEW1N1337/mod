using System.Collections.Generic;
using UnityEngine;

namespace DreamCar.Moderation
{
    // Küfür filtresi vardı ama spam floodunu engelleyen yoktu. Token-bucket +
    // tekrar tespiti + kademeli susturma.
    public class ChatRateLimiter : MonoBehaviour
    {
        public static ChatRateLimiter Instance { get; private set; }

        [Header("Token bucket")]
        public int burstCapacity = 4;          // arka arkaya kaç mesaj
        public float refillPerSecond = 0.5f;   // sonrasında saniyede kaç token

        [Header("Tekrar tespiti")]
        public int repeatThreshold = 3;        // aynı mesaj kaç kez üst üste
        public float repeatWindowSeconds = 20f;

        [Header("Ceza")]
        public float firstMuteSeconds = 10f;
        public float muteMultiplier = 2f;
        public float maxMuteSeconds = 300f;

        float _tokens;
        float _mutedUntil;
        float _currentPenalty;
        string _lastMessage;
        int _repeatCount;
        float _lastMessageAt;

        public bool IsMuted => Time.unscaledTime < _mutedUntil;
        public float MutedSecondsLeft => Mathf.Max(0f, _mutedUntil - Time.unscaledTime);

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _tokens = burstCapacity;
            _currentPenalty = firstMuteSeconds;
        }

        void Update()
        {
            if (_tokens < burstCapacity)
                _tokens = Mathf.Min(burstCapacity, _tokens + refillPerSecond * Time.unscaledDeltaTime);

            // Uzun süre sessiz kalınca ceza kademesi sıfırlanır.
            if (!IsMuted && Time.unscaledTime - _lastMessageAt > 120f)
                _currentPenalty = firstMuteSeconds;
        }

        // Mesaj gönderilmeden önce çağrılır. false dönerse gönderme.
        public bool TrySend(string message, out string reason)
        {
            reason = null;

            if (IsMuted)
            {
                reason = $"Sohbet {Mathf.CeilToInt(MutedSecondsLeft)} sn susturuldu";
                return false;
            }

            if (_tokens < 1f)
            {
                ApplyMute();
                reason = $"Çok hızlı yazıyorsun — {Mathf.CeilToInt(MutedSecondsLeft)} sn bekle";
                return false;
            }

            string normalized = Normalize(message);
            bool withinWindow = Time.unscaledTime - _lastMessageAt < repeatWindowSeconds;

            if (withinWindow && normalized == _lastMessage)
            {
                _repeatCount++;
                if (_repeatCount >= repeatThreshold)
                {
                    ApplyMute();
                    reason = "Aynı mesajı tekrarlama";
                    return false;
                }
            }
            else _repeatCount = 0;

            _tokens -= 1f;
            _lastMessage = normalized;
            _lastMessageAt = Time.unscaledTime;
            return true;
        }

        void ApplyMute()
        {
            _mutedUntil = Time.unscaledTime + _currentPenalty;
            _currentPenalty = Mathf.Min(maxMuteSeconds, _currentPenalty * muteMultiplier);
            _tokens = 0f;
            _repeatCount = 0;
        }

        static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Trim().ToLowerInvariant();
    }
}
