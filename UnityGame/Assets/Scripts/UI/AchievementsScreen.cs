using DreamCar.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.UI
{
    // AchievementCatalog + PlayFabAchievements vardı ama oyuncunun göreceği liste yoktu.
    // Kilitli/açık ayrımı, ilerleme ve ödül gösterimi.
    public class AchievementsScreen : MonoBehaviour
    {
        public GameObject panel;
        public Button closeButton;
        public AchievementCatalog catalog;
        public Transform listParent;
        public GameObject rowPrefab;
        public TMP_Text summaryLabel;

        [Header("Renkler")]
        public Color unlockedColor = new Color(1f, 0.85f, 0.3f);
        public Color lockedColor = new Color(0.5f, 0.5f, 0.5f);

        void Start()
        {
            if (closeButton) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            if (panel) panel.SetActive(true);
            Refresh();
        }

        public void Close() { if (panel) panel.SetActive(false); }

        void Refresh()
        {
            if (!listParent || !rowPrefab || !catalog) return;

            for (int i = listParent.childCount - 1; i >= 0; i--)
                Destroy(listParent.GetChild(i).gameObject);

            int unlocked = 0;
            foreach (var def in catalog.achievements)
            {
                if (!def) continue;
                bool isUnlocked = IsUnlocked(def.id);
                if (isUnlocked) unlocked++;

                var go = Instantiate(rowPrefab, listParent);
                // rowPrefab sahnede kapalı duran bir şablon; klon da kapalı doğuyor ve
                // satırlar hiç görünmüyordu. Ayrıca kapalı obje GetComponentsInChildren'da
                // includeInactive olmadan bulunmaz.
                go.SetActive(true);
                var texts = go.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = def.displayName;
                if (texts.Length > 1) texts[1].text = def.description;
                if (texts.Length > 2) texts[2].text = isUnlocked ? "✓" : $"+{def.moneyReward:N0} ₺";

                // GetComponentInChildren<Image>() satırın KÖK arka planını
                // buluyordu: kilitli başarımlarda lockedColor bütün satırı
                // griye boyuyor ve def.icon satır arka planının yerine
                // geçiyordu. Şablondaki adı belli "Icon" çocuğunu arıyoruz.
                var iconTr = go.transform.Find("Icon");
                var icon = iconTr ? iconTr.GetComponent<Image>() : null;
                if (icon)
                {
                    if (def.icon) icon.sprite = def.icon;
                    icon.color = isUnlocked ? unlockedColor : lockedColor;
                }
                var canvasGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                canvasGroup.alpha = isUnlocked ? 1f : 0.55f;
            }

            if (summaryLabel)
                summaryLabel.text = $"{unlocked} / {catalog.achievements.Count}";
        }

        static bool IsUnlocked(string id)
        {
            var raw = PlayerPrefs.GetString("ach.unlocked", "");
            if (string.IsNullOrEmpty(raw)) return false;
            foreach (var s in raw.Split(','))
                if (s.Trim() == id) return true;
            return false;
        }
    }
}
