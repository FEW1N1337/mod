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

        // 5x7 bit eşlemli yazı tipi. Buradaki eski kod bir yazı tipi DEĞİLDİ:
        //   ink = ((x + y + text[c]) % 5) == 0 || y == 0 || y == charH - 1
        // yani harf yerine çapraz taramalı dikdörtgenler çiziyordu ("prod'da
        // TMP → RenderTexture kullan" notuyla). Plaka bu haliyle bağlansaydı
        // araçlara okunamayan çizgili kutular yapıştırırdı.
        //
        // Her glif 7 satır x 5 sütun; '#' mürekkep. Harici bir font varlığına
        // ihtiyaç duymuyor (projede zaten yok) ve plaka metni gerçekten okunuyor.
        const string Charset = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // Her glif 7 satır x 5 sütun, satırlar üstten alta; '#' mürekkep.
        static readonly string[] Glyphs =
        {
            "     " + "     " + "     " + "     " + "     " + "     " + "     ", // boşluk
            ".###." + "#...#" + "#..##" + "#.#.#" + "##..#" + "#...#" + ".###.", // 0
            "..#.." + ".##.." + "..#.." + "..#.." + "..#.." + "..#.." + ".###.", // 1
            ".###." + "#...#" + "....#" + "...#." + "..#.." + ".#..." + "#####", // 2
            "#####" + "...#." + "..#.." + "...#." + "....#" + "#...#" + ".###.", // 3
            "...#." + "..##." + ".#.#." + "#..#." + "#####" + "...#." + "...#.", // 4
            "#####" + "#...." + "####." + "....#" + "....#" + "#...#" + ".###.", // 5
            "..##." + ".#..." + "#...." + "####." + "#...#" + "#...#" + ".###.", // 6
            "#####" + "....#" + "...#." + "..#.." + ".#..." + ".#..." + ".#...", // 7
            ".###." + "#...#" + "#...#" + ".###." + "#...#" + "#...#" + ".###.", // 8
            ".###." + "#...#" + "#...#" + ".####" + "....#" + "...#." + ".##..", // 9
            ".###." + "#...#" + "#...#" + "#####" + "#...#" + "#...#" + "#...#", // A
            "####." + "#...#" + "#...#" + "####." + "#...#" + "#...#" + "####.", // B
            ".###." + "#...#" + "#...." + "#...." + "#...." + "#...#" + ".###.", // C
            "###.." + "#..#." + "#...#" + "#...#" + "#...#" + "#..#." + "###..", // D
            "#####" + "#...." + "#...." + "####." + "#...." + "#...." + "#####", // E
            "#####" + "#...." + "#...." + "####." + "#...." + "#...." + "#....", // F
            ".###." + "#...#" + "#...." + "#.###" + "#...#" + "#...#" + ".####", // G
            "#...#" + "#...#" + "#...#" + "#####" + "#...#" + "#...#" + "#...#", // H
            ".###." + "..#.." + "..#.." + "..#.." + "..#.." + "..#.." + ".###.", // I
            "..###" + "...#." + "...#." + "...#." + "...#." + "#..#." + ".##..", // J
            "#...#" + "#..#." + "#.#.." + "##..." + "#.#.." + "#..#." + "#...#", // K
            "#...." + "#...." + "#...." + "#...." + "#...." + "#...." + "#####", // L
            "#...#" + "##.##" + "#.#.#" + "#.#.#" + "#...#" + "#...#" + "#...#", // M
            "#...#" + "##..#" + "##..#" + "#.#.#" + "#..##" + "#..##" + "#...#", // N
            ".###." + "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".###.", // O
            "####." + "#...#" + "#...#" + "####." + "#...." + "#...." + "#....", // P
            ".###." + "#...#" + "#...#" + "#...#" + "#.#.#" + "#..#." + ".##.#", // Q
            "####." + "#...#" + "#...#" + "####." + "#.#.." + "#..#." + "#...#", // R
            ".####" + "#...." + "#...." + ".###." + "....#" + "....#" + "####.", // S
            "#####" + "..#.." + "..#.." + "..#.." + "..#.." + "..#.." + "..#..", // T
            "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".###.", // U
            "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".#.#." + "..#..", // V
            "#...#" + "#...#" + "#...#" + "#.#.#" + "#.#.#" + "##.##" + "#...#", // W
            "#...#" + "#...#" + ".#.#." + "..#.." + ".#.#." + "#...#" + "#...#", // X
            "#...#" + "#...#" + ".#.#." + "..#.." + "..#.." + "..#.." + "..#..", // Y
            "#####" + "....#" + "...#." + "..#.." + ".#..." + "#...." + "#####", // Z
        };

        void StampPixelText(string text)
        {
            const int gw = 5, gh = 7;              // glif ızgarası
            int scaleY = Mathf.Max(1, (textureHeight - 24) / gh);
            int scaleX = Mathf.Max(1, scaleY - 2); // hafif dar: plaka yazısı böyle
            int cellW = gw * scaleX + scaleX;      // gliflerin arasına bir sütun boşluk

            text = text.ToUpperInvariant();
            int totalW = text.Length * cellW;
            int startX = (textureWidth - totalW) / 2;
            int startY = (textureHeight - gh * scaleY) / 2;

            for (int c = 0; c < text.Length; c++)
            {
                int gi = Charset.IndexOf(text[c]);
                if (gi < 0) continue;              // desteklenmeyen karakter: boş bırak
                string glyph = Glyphs[gi];

                for (int gy = 0; gy < gh; gy++)
                for (int gx = 0; gx < gw; gx++)
                {
                    if (glyph[gy * gw + gx] == ' ') continue;

                    // Doku kaynağı ALTTAN yukarı; glif satırları ÜSTTEN aşağı.
                    int baseX = startX + c * cellW + gx * scaleX;
                    int baseY = startY + (gh - 1 - gy) * scaleY;

                    for (int sy = 0; sy < scaleY; sy++)
                    for (int sx = 0; sx < scaleX; sx++)
                    {
                        int px = baseX + sx, py = baseY + sy;
                        if (px < 0 || px >= textureWidth || py < 0 || py >= textureHeight) continue;
                        _tex.SetPixel(px, py, plateFg);
                    }
                }
            }
        }

    }
}
