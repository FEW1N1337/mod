using Photon.Pun;
using UnityEngine;

namespace DreamCar.Customization
{
    // Dream Road'daki PlateVariant.Change eşdeğeri. Plaka meshine dinamik texture çizer
    // (TR formatı: "34 ABC 123"). Photon custom prop ile diğer oyunculara yayılır.
    [RequireComponent(typeof(PhotonView))]
    public class LicensePlate : MonoBehaviourPun
    {
        public Renderer[] plateRenderers;
        public Font font;
        public int textureWidth = 512;
        public int textureHeight = 128;
        public Color plateBg = Color.white;
        public Color plateFg = Color.black;
        public string defaultText = "34 FEW 1337";
        public string texturePropertyName = "_BaseMap";

        Texture2D _tex;

        void Start()
        {
            _tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            if (photonView.IsMine)
                Apply(PlayerPrefs.GetString("plate.text", defaultText));
            else if (photonView.Owner != null &&
                     photonView.Owner.CustomProperties.TryGetValue("plate.text", out object v))
                Apply(v as string);
            else Apply(defaultText);
        }

        public void Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) text = defaultText;
            if (text.Length > 12) text = text.Substring(0, 12);

            Redraw(text);
            foreach (var r in plateRenderers)
                if (r) r.material.SetTexture(texturePropertyName, _tex);

            if (photonView.IsMine)
            {
                PlayerPrefs.SetString("plate.text", text);
                var props = new ExitGames.Client.Photon.Hashtable { { "plate.text", text } };
                photonView.Owner?.SetCustomProperties(props);
            }
        }

        void Redraw(string text)
        {
            Color[] px = new Color[textureWidth * textureHeight];
            for (int i = 0; i < px.Length; i++) px[i] = plateBg;
            _tex.SetPixels(px);
            _tex.Apply(false);

            // GPU'da GUI ile yazmak için RenderTexture kullanılabilir; basit yol:
            // sahnede bir kez GUIText/TMP kullanıp Blit et. MVP için placeholder text stamping:
            StampPixelText(text);
            _tex.Apply(false);
        }

        void StampPixelText(string text)
        {
            // Placeholder 5x7 piksel yazı (minimalist). Prod'da TMP → RenderTexture kullan.
            int charW = 32, charH = 56;
            int startX = (textureWidth - text.Length * charW) / 2;
            int startY = (textureHeight - charH) / 2;

            for (int c = 0; c < text.Length; c++)
            {
                for (int x = 0; x < charW - 4; x++)
                    for (int y = 0; y < charH; y++)
                    {
                        int px = startX + c * charW + x;
                        int py = startY + y;
                        if (px < 0 || px >= textureWidth || py < 0 || py >= textureHeight) continue;
                        bool ink = ((x + y + text[c]) % 5) == 0 || (y == 0 || y == charH - 1);
                        if (ink) _tex.SetPixel(px, py, plateFg);
                    }
            }
        }
    }
}
