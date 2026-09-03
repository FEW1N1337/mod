using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // Sahne geçişleri sert kesme yapıyordu. Bu ekran PhotonNetwork.LevelLoadingProgress
    // veya SceneManager async progress'ini gösterir + rastgele ipucu döndürür.
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        public GameObject panel;
        public Image progressFill;
        public TMP_Text progressLabel;
        public TMP_Text tipLabel;
        public CanvasGroup canvasGroup;
        public float fadeSeconds = 0.35f;
        public float tipRotateSeconds = 4f;

        [TextArea]
        public string[] tips =
        {
            "El freniyle savrulup drift skorunu artırabilirsin.",
            "Nitro bittiğinde kendini yavaşça doldurur — sabırlı ol.",
            "Yakıtın azalınca benzin istasyonuna uğra.",
            "V tuşu ile kamera modunu değiştir: takip, kaput, kokpit, sinematik.",
            "Hasar arttıkça araç zorlanır. Tamir panelinden onarabilirsin.",
            "Şifreli oda kurup sadece arkadaşlarınla sürebilirsin.",
            "Günlük ödülünü almayı unutma — ardışık günlerde çarpan artar.",
            "Yarışta checkpoint'leri sırayla geçmen gerekir.",
        };

        Coroutine _tipRoutine;

        void Awake()
        {
            // Yalnızca yinelenen bileşeni yok et: ~Bootstrap üzerinde sahneye özel
            // bileşenler de duruyor (RoomManager, Weather, MapSelector…) ve
            // GameObject yok edilirse onlar da giderdi.
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Panel AYRI bir Canvas'ın altında ve o Canvas da hayatta kalmalı:
            // aksi halde bu bileşen sahne geçişinden sağ çıkar ama gösterdiği
            // UI yok edilir ve yükleme ekranı tam da gerektiği anda (sahne
            // geçişinde) çalışmayan bir referansa dönerdi.
            if (panel)
            {
                var root = panel.transform.root.gameObject;
                if (root != gameObject) DontDestroyOnLoad(root);
            }

            HideImmediate();
        }

        public void HideImmediate()
        {
            if (panel) panel.SetActive(false);
            if (canvasGroup) canvasGroup.alpha = 0f;
        }

        // Photon senkron sahne yüklemesi için (master LoadLevel çağırır, herkes yükler).
        public void ShowForPhotonLoad()
        {
            StopAllCoroutines();
            StartCoroutine(PhotonLoadRoutine());
        }

        // Normal (tek oyunculu / menü) sahne geçişi için.
        public void LoadScene(string sceneName)
        {
            StopAllCoroutines();
            StartCoroutine(LocalLoadRoutine(sceneName));
        }

        IEnumerator PhotonLoadRoutine()
        {
            yield return Show();

            while (PhotonNetwork.LevelLoadingProgress < 1f)
            {
                SetProgress(PhotonNetwork.LevelLoadingProgress);
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Hide();
        }

        IEnumerator LocalLoadRoutine(string sceneName)
        {
            yield return Show();

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                SetProgress(op.progress / 0.9f);
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return Hide();
        }

        IEnumerator Show()
        {
            if (panel) panel.SetActive(true);
            SetProgress(0f);
            NextTip();
            _tipRoutine = StartCoroutine(RotateTips());

            if (canvasGroup)
            {
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(t / fadeSeconds);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }
        }

        IEnumerator Hide()
        {
            if (_tipRoutine != null) { StopCoroutine(_tipRoutine); _tipRoutine = null; }

            if (canvasGroup)
            {
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeSeconds);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
            if (panel) panel.SetActive(false);
        }

        IEnumerator RotateTips()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(tipRotateSeconds);
                NextTip();
            }
        }

        void NextTip()
        {
            if (tipLabel == null || tips == null || tips.Length == 0) return;
            tipLabel.text = tips[Random.Range(0, tips.Length)];
        }

        void SetProgress(float p)
        {
            p = Mathf.Clamp01(p);
            if (progressFill) progressFill.fillAmount = p;
            if (progressLabel) progressLabel.text = Mathf.RoundToInt(p * 100f) + "%";
        }
    }
}
