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

        // Yinelenen guard'ı YOK — ve olmamalı. ~Bootstrap üzerindeki başka
        // bileşenler DontDestroyOnLoad çağırdığı için ana menünün
        // ToastNotification'ı harita sahnesine kadar hayatta kalıyor; ama
        // stackParent ile toastPrefab ana menü Canvas'ının çocuğuydu ve o
        // Canvas sahneyle birlikte yok edildi. Yani hayatta kalan örnek
        // ÇALIŞMAYAN bir örnek. Kazanan, referansları CANLI olan olmalı.
        void Awake() { if (stackParent || !Instance) Instance = this; }

        public static void Show(string message)
        {
            var inst = Resolve();
            if (inst) inst.ShowInternal(message);
        }

        // Önbellekteki örnek iki şekilde işe yaramaz hale gelebiliyor:
        // sahnesiyle birlikte yok edilmiş olabilir (Unity sahte-null), ya da
        // kendisi yaşıyor ama gösterdiği UI önceki sahneyle gitmiş olabilir.
        // İkisinde de projedeki 40 Show() çağrısı sessizce düşüyordu — yarış
        // sayacı, ödül bildirimi, yakıt uyarısı, yeniden bağlanma durumu…
        static ToastNotification Resolve()
        {
            if (Instance && Instance.stackParent && Instance.toastPrefab) return Instance;

            // Yalnızca önbellek geçersizken çalışır, her Show()'da değil.
            var all = FindObjectsByType<ToastNotification>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
                if (t && t.stackParent && t.toastPrefab) { Instance = t; return t; }

            return null;
        }

        void ShowInternal(string msg)
        {
            if (!stackParent || !toastPrefab) return;
            var go = Instantiate(toastPrefab, stackParent);
            // toastPrefab sahnede kapalı duran bir şablon; klon da kapalı doğar ve
            // kapalı objede GetComponentInChildren (includeInactive olmadan) null döner.
            go.SetActive(true);
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
