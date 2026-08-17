// Photon Voice 2 entegrasyonu (opsiyonel). Sadece PHOTON_VOICE_DEFINED tanımlıysa derlenir.
// Kullanmak için: Asset Store'dan "Photon Voice 2" import et → Player Settings → Scripting
// Define Symbols'e "PHOTON_VOICE_DEFINED" ekle. Cihazda mikrofon izni istenir.
using UnityEngine;

#if PHOTON_VOICE_DEFINED
using Photon.Voice.Unity;
#endif

namespace DreamCar.Voice
{
    public class VoiceChatController : MonoBehaviour
    {
        public bool startMuted = true;
        bool _muted;

#if PHOTON_VOICE_DEFINED
        Recorder _recorder;

        void Start()
        {
            _recorder = FindFirstObjectByType<Recorder>();
            if (_recorder == null) { Debug.LogWarning("[Voice] Recorder yok — Photon Voice sahnede eksik."); return; }
            _muted = startMuted;
            _recorder.TransmitEnabled = !_muted;
        }

        public void SetPushToTalk(bool held)
        {
            if (!_recorder) return;
            _recorder.TransmitEnabled = held && !_muted;
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            if (_recorder) _recorder.TransmitEnabled = !_muted;
        }
#else
        void Start() => Debug.Log("[Voice] Photon Voice yüklü değil. Package import + define ekle.");
        public void SetPushToTalk(bool _) { }
        public void ToggleMute() { }
#endif
    }
}
