using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamCar.Tutorial
{
    // İlk açılış rehberi. PlayerPrefs'e ilerleme yazılır — bir daha gösterilmez.
    public class TutorialManager : MonoBehaviour
    {
        [System.Serializable] public class Step { public string bodyText; public RectTransform highlight; }

        public List<Step> steps = new();
        public GameObject panel;
        public TMP_Text bodyLabel;
        public Button nextButton;
        public Image highlightMask;

        const string PrefKey = "tutorial.done.v1";
        int _index;

        void Start()
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) == 1) { if (panel) panel.SetActive(false); return; }
            if (nextButton) nextButton.onClick.AddListener(Next);
            Show();
        }

        void Show()
        {
            if (_index >= steps.Count) { Finish(); return; }
            if (panel) panel.SetActive(true);
            var s = steps[_index];
            if (bodyLabel) bodyLabel.text = s.bodyText;
            if (highlightMask && s.highlight)
            {
                var rt = highlightMask.rectTransform;
                rt.position = s.highlight.position;
                rt.sizeDelta = s.highlight.sizeDelta * 1.1f;
            }
        }

        void Next() { _index++; Show(); }
        void Finish() { PlayerPrefs.SetInt(PrefKey, 1); PlayerPrefs.Save(); if (panel) panel.SetActive(false); }
    }
}
