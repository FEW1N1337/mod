using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DreamCar.Core;

namespace DreamCar.UI
{
    // Ana menüde sürücü seviyesi rozeti + XP çubuğu. DriverProfile'ı dinliyor.
    public class DriverLevelBadge : MonoBehaviour
    {
        public TMP_Text levelLabel;
        public TMP_Text xpLabel;
        public Image xpFill;

        void OnEnable()
        {
            if (DriverProfile.Instance != null) DriverProfile.Instance.OnChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (DriverProfile.Instance != null) DriverProfile.Instance.OnChanged -= Refresh;
        }

        // DriverProfile ~Bootstrap'te; bu rozet menü Canvas'ında. Awake sırası
        // belirsiz olduğu için Start'ta yeniden bağlanıyoruz.
        void Start()
        {
            if (DriverProfile.Instance != null)
            {
                DriverProfile.Instance.OnChanged -= Refresh;
                DriverProfile.Instance.OnChanged += Refresh;
            }
            Refresh();
        }

        void Refresh()
        {
            var p = DriverProfile.Instance;
            if (p == null) return;
            if (levelLabel) levelLabel.text = "Sv " + p.Level;
            if (xpLabel) xpLabel.text = $"{p.XpIntoLevel:N0} / {p.XpForNextLevel:N0} XP";
            if (xpFill) xpFill.fillAmount = Mathf.Clamp01(p.LevelProgress01);
        }
    }
}
