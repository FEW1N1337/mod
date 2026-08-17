using Photon.Pun;
using TMPro;
using UnityEngine;

namespace DreamCar.Customization
{
    // İki parçalı plaka: sol tarafta 2 haneli il kodu, sağ tarafta harf-rakam kombo.
    // TR standardı örnek: "34" | "FEW 1337". Photon custom prop ile diğer oyunculara sync.
    [RequireComponent(typeof(PhotonView))]
    public class SplitLicensePlate : MonoBehaviourPun
    {
        public TMP_Text leftText;
        public TMP_Text rightText;
        public bool splitParts = true;
        public string defaultLeft = "34";
        public string defaultRight = "FEW 1337";

        void Start()
        {
            string left = defaultLeft, right = defaultRight;
            if (photonView.IsMine)
            {
                left = PlayerPrefs.GetString("plate.left", defaultLeft);
                right = PlayerPrefs.GetString("plate.right", defaultRight);
            }
            else if (photonView.Owner != null)
            {
                var p = photonView.Owner.CustomProperties;
                if (p.TryGetValue("plate.left", out object l)) left = l as string;
                if (p.TryGetValue("plate.right", out object r)) right = r as string;
            }
            Apply(left, right);
        }

        public void Apply(string left, string right)
        {
            if (string.IsNullOrEmpty(left)) left = defaultLeft;
            if (string.IsNullOrEmpty(right)) right = defaultRight;
            if (left.Length > 4) left = left.Substring(0, 4);
            if (right.Length > 10) right = right.Substring(0, 10);

            if (splitParts)
            {
                if (leftText) { leftText.text = left; leftText.gameObject.SetActive(true); }
                if (rightText) rightText.text = right;
            }
            else
            {
                if (leftText) leftText.gameObject.SetActive(false);
                if (rightText) rightText.text = left + " " + right;
            }

            if (photonView.IsMine)
            {
                PlayerPrefs.SetString("plate.left", left);
                PlayerPrefs.SetString("plate.right", right);
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { "plate.left", left },
                    { "plate.right", right }
                };
                photonView.Owner?.SetCustomProperties(props);
            }
        }
    }
}
