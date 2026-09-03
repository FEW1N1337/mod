using System;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Consent
{
    // KVKK / GDPR / Apple ATT ilk açılış onayı. Onay verilmeden analytics/ads yüklenmez.
    public class KVKKConsent : MonoBehaviour
    {
        const string PrefKey = "consent.given.v1";

        public GameObject dialog;
        public Button acceptButton;
        public Button rejectButton;
        public event Action<bool> OnDecision;

        public static bool HasConsent => PlayerPrefs.GetInt(PrefKey, 0) == 1;

        void Start()
        {
            if (HasConsent) { if (dialog) dialog.SetActive(false); return; }
            if (dialog) dialog.SetActive(true);

            if (acceptButton) acceptButton.onClick.AddListener(() => Decide(true));
            if (rejectButton) rejectButton.onClick.AddListener(() => Decide(false));
        }

        void Decide(bool accept)
        {
            PlayerPrefs.SetInt(PrefKey, accept ? 1 : 0);
            PlayerPrefs.Save();
            if (dialog) dialog.SetActive(false);
            OnDecision?.Invoke(accept);
#if UNITY_IOS
            RequestATT();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void _RequestTracking();
        void RequestATT()
        {
            try { _RequestTracking(); }
            catch (EntryPointNotFoundException) { Debug.LogWarning("[ATT] Native binding not present."); }
        }
#else
        void RequestATT() { }
#endif
    }
}
