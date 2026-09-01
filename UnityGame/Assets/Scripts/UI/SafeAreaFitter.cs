using UnityEngine;

namespace DreamCar.UI
{
    // Çentik, Dynamic Island ve ev göstergesi ekranın kenarlarını yiyor. Kontroller
    // köşelere sabitlendiği için (gaz, fren, el freni, çıkış, duraklat) bu bölgeler
    // tam olarak onların durduğu yer — güvenli alan uygulanmazsa yatay modda kısa
    // kenarda butonlar çentiğin altında kalır, altta ev göstergesi üstlerine biner.
    //
    // Bu bileşen bir RectTransform'u Screen.safeArea'ya oturtur. HUD panelleri onun
    // altında olduğu için hepsi birlikte içeri çekilir.
    //
    // Sahne kurulumu bunu Canvas'ın altındaki her tam ekran panele ekler.
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(-100)]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;

        void Awake() => _rt = GetComponent<RectTransform>();

        void OnEnable() => Apply();

        // Cihaz döndürüldüğünde güvenli alan değişir; her karede karşılaştırmak ucuz
        // (iki Rect), yeniden hesaplama yalnızca gerçekten değiştiğinde yapılır.
        void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastScreen.x && Screen.height == _lastScreen.y) return;
            Apply();
        }

        void Apply()
        {
            if (!_rt) _rt = GetComponent<RectTransform>();
            if (!_rt) return;

            var safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            // Sıfır boyutlu ekran (bazı editör/başlangıç kareleri) sıfıra bölmeye yol açar.
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
