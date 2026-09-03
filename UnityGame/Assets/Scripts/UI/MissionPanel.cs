using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DreamCar.Progression;

namespace DreamCar.UI
{
    // Günlük görevler ekranı. MissionSystem'i dinleyip satırları kuruyor;
    // tamamlanan görevin "Al" butonu ödülü veriyor.
    public class MissionPanel : MonoBehaviour
    {
        public GameObject panel;
        public Transform listParent;
        public GameObject rowPrefab;
        public Button closeButton;

        bool _open;

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (panel) panel.SetActive(false);
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            if (panel) panel.SetActive(true);
            if (MissionSystem.Instance != null) MissionSystem.Instance.OnChanged += Refresh;
            Refresh();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (MissionSystem.Instance != null) MissionSystem.Instance.OnChanged -= Refresh;
            if (panel) panel.SetActive(false);
        }

        void OnDisable() => Close();

        void Refresh()
        {
            if (!listParent || !rowPrefab) return;
            var sys = MissionSystem.Instance;
            if (sys == null) return;

            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);

            foreach (var m in sys.Missions)
            {
                if (m == null) continue;

                var go = Instantiate(rowPrefab, listParent);
                go.SetActive(true);

                var texts = go.GetComponentsInChildren<TMP_Text>();
                double prog = sys.Progress(m);
                bool complete = sys.IsComplete(m);

                // texts[0] = açıklama, texts[1] = ilerleme, texts[2] = ödül/durum
                if (texts.Length > 0) texts[0].text = sys.Describe(m);
                if (texts.Length > 1)
                    texts[1].text = $"{Mathf.Min((float)prog, (float)m.target):N0} / {m.target:N0}";
                if (texts.Length > 2)
                    texts[2].text = m.claimed ? "Alındı" : $"+{m.rewardMoney:N0} ₺";

                var fill = go.transform.Find("Fill")?.GetComponent<Image>();
                if (fill) fill.fillAmount = m.target > 0 ? Mathf.Clamp01((float)(prog / m.target)) : 0f;

                var btn = go.GetComponentInChildren<Button>();
                if (!btn) continue;

                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label) label.text = m.claimed ? "Alındı" : (complete ? "Al" : "…");
                btn.interactable = complete;

                var captured = m;
                btn.onClick.AddListener(() => { if (sys.Claim(captured)) Refresh(); });
            }
        }
    }
}
