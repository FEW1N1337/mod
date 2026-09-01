using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DreamCar.UI
{
    public class ToastNotification : MonoBehaviour
    {
        public static ToastNotification Instance { get; private set; }
        public RectTransform stackParent;
        public GameObject toastPrefab;
        public float lifeSeconds = 3f;
        public int maxOnScreen = 4;

        readonly Queue<GameObject> _live = new();

        void Awake() { Instance = this; }

        public static void Show(string message) { if (Instance) Instance.ShowInternal(message); }

        void ShowInternal(string msg)
        {
            if (!stackParent || !toastPrefab) return;
            var go = Instantiate(toastPrefab, stackParent);
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label) label.text = msg;

            _live.Enqueue(go);
            while (_live.Count > maxOnScreen) { var old = _live.Dequeue(); if (old) Destroy(old); }
            StartCoroutine(FadeAndKill(go));
        }

        IEnumerator FadeAndKill(GameObject go)
        {
            // PauseMenu Time.timeScale=0 yapıyor; WaitForSeconds o anda tamamen duruyor ve
            // duraklatma sırasında çıkan toast'lar ekranda kalıcı olarak asılı kalıyordu.
            yield return new WaitForSecondsRealtime(lifeSeconds);
            if (go) Destroy(go);
        }
    }
}
