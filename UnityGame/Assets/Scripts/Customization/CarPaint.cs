using Photon.Pun;
using UnityEngine;

namespace DreamCar.Customization
{
    // Araba boyası: MaterialPropertyBlock ile _BaseColor (URP) veya _Color (built-in) değişir.
    // Photon Custom Properties'e yazıldığı için diğer oyuncular da görür.
    public class CarPaint : MonoBehaviourPun
    {
        public Renderer[] paintRenderers;
        public string colorProperty = "_BaseColor";
        public string metallicProperty = "_Metallic";
        public string smoothnessProperty = "_Smoothness";

        // CarCustomization tarafından Awake'te set ediliyor.
        //
        // NEDEN GEREKLİ: bu bileşen Start'ta global PlayerPrefs anahtarından
        // (car.color) renk yüklüyor, modifikasyon sistemi ise ARAÇ BAŞINA
        // kayıttan. İkisi de Start'ta koşsaydı hangisinin kazandığı Unity'nin
        // bileşen sırasına kalırdı — aynı build'de bile araçtan araca değişen
        // bir renk. Yönetici varsa bu bileşen kendi yüklemesini yapmıyor,
        // yalnızca Apply çağrılarını uyguluyor (Photon senkronu dahil).
        [HideInInspector] public bool externallyManaged;

        MaterialPropertyBlock _mpb;
        int _colorId, _metallicId, _smoothnessId;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(colorProperty);
            _metallicId = Shader.PropertyToID(metallicProperty);
            _smoothnessId = Shader.PropertyToID(smoothnessProperty);
        }

        void Start()
        {
            if (externallyManaged) return;
            if (photonView.IsMine) LoadFromPrefs();
            else LoadFromOwnerProperties();
        }

        public void Apply(Color color, float metallic = 0.8f, float smoothness = 0.85f)
        {
            foreach (var r in paintRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_colorId, color);
                _mpb.SetFloat(_metallicId, metallic);
                _mpb.SetFloat(_smoothnessId, smoothness);
                r.SetPropertyBlock(_mpb);
            }

            if (photonView && photonView.IsMine)
            {
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { "car.color", ColorUtility.ToHtmlStringRGB(color) },
                    { "car.metallic", metallic },
                    { "car.smoothness", smoothness },
                };
                photonView.Owner?.SetCustomProperties(props);
                PlayerPrefs.SetString("car.color", ColorUtility.ToHtmlStringRGB(color));
                PlayerPrefs.SetFloat("car.metallic", metallic);
                PlayerPrefs.SetFloat("car.smoothness", smoothness);
            }
        }

        void LoadFromPrefs()
        {
            if (ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString("car.color", "E63946"), out Color c))
                Apply(c, PlayerPrefs.GetFloat("car.metallic", 0.8f), PlayerPrefs.GetFloat("car.smoothness", 0.85f));
        }

        void LoadFromOwnerProperties()
        {
            if (photonView.Owner == null) return;
            var p = photonView.Owner.CustomProperties;
            if (p.TryGetValue("car.color", out object hex) && hex is string s &&
                ColorUtility.TryParseHtmlString("#" + s, out Color c))
            {
                float m = p.TryGetValue("car.metallic", out object mo) ? (float)mo : 0.8f;
                float sm = p.TryGetValue("car.smoothness", out object so) ? (float)so : 0.85f;
                Apply(c, m, sm);
            }
        }
    }
}
