#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DreamCar.EditorTools.Procedural
{
    // Projede hiç render pipeline varlığı yoktu ve onu üreten kod da yoktu.
    // Unity, ayar dosyası bulamayınca varsayılanlara düşer — varsayılan da
    // Built-in pipeline'dır. Ama bütün malzemelerimiz URP/Lit kullanıyor, arazi
    // ve proplar kendi URP shader'ımızı kullanıyor: Built-in altında hepsi
    // MACENTA render edilir. Yani bu dosya olmadan oyun ilk açılışta tamamen
    // bozuk görünür ve hiçbir sanat kararının anlamı kalmaz.
    //
    // Menü: DreamCar → Procedural → Setup Render Pipeline & Player Settings
    public static class ProceduralRenderPipeline
    {
        const string Folder = "Assets/Generated/Rendering";
        const string AssetPath = Folder + "/DreamCarURP.asset";
        const string RendererPath = Folder + "/DreamCarRenderer.asset";

        [MenuItem("DreamCar/Procedural/Setup Render Pipeline & Player Settings")]
        public static void SetupInteractive() => Setup(confirm: true);

        public static void Setup(bool confirm)
        {
            var asset = BuildPipelineAsset();
            ApplyPlayerSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!confirm) return;
            EditorUtility.DisplayDialog("DreamCar",
                asset != null
                    ? "Render pipeline kuruldu ve atandı.\n\n" +
                      "• URP varlığı: " + AssetPath + "\n" +
                      "• Renk uzayı: Linear\n" +
                      "• IL2CPP + ARM64\n\n" +
                      "Bunlar olmadan bütün yüzeyler macenta görünürdü."
                    : "Render pipeline kurulamadı — Console'daki uyarılara bak.",
                "Tamam");
        }

        // ---------------------------------------------------------- URP varlığı

        static UniversalRenderPipelineAsset BuildPipelineAsset()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            TryAddAmbientOcclusion(rendererData);
            EditorUtility.SetDirty(rendererData);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            if (!LinkRenderer(asset, rendererData))
            {
                Debug.LogError(
                    "[Pipeline] URP varlığına renderer bağlanamadı. Bu alan Unity sürümleri " +
                    "arasında ad değiştirebiliyor. Project Settings → Graphics'ten elle " +
                    "atamak gerekebilir: " + AssetPath);
                return null;
            }

            // Mobil için makul varsayılanlar. HDR açık olmalı — bloom ve tonemapping
            // HDR olmadan doğru çalışmaz, parlak yüzeyler beyaza yapışır.
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;
            asset.supportsCameraDepthTexture = true;   // SSAO ve motion blur buna bağlı
            asset.supportsCameraOpaqueTexture = false; // pahalı, kullanmıyoruz

            // RENK DERECELENDİRME MODU — burası bir ayar değil, bir HATA düzeltmesi.
            //
            // ProceduralPostProcessing profillere TonemappingMode.ACES kuruyor.
            // ACES, HDR bir renk hattı varsayar. Ama bu alan varsayılanda
            // LowDynamicRange kalıyordu: sinyal derecelendirmeden ÖNCE kırpılıyor,
            // ACES'in eğrisi kırpılmış veriye uygulanıyor ve sonuç soluk,
            // kontrastsız çıkıyordu. Yani supportsHDR = true demenin faydası tam
            // burada kayboluyordu — parlak gökyüzü, far hüzmesi ve metalik araç
            // boyası HDR'ın vermesi gereken derinliği hiç alamıyordu.
            //
            // Hiçbir hata vermiyordu; sadece görüntü olması gerekenden kötüydü.
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;

            // HDR modunda LUT 32 yetersiz — gökyüzü gradyanlarında ve gece
            // aydınlatmasında bantlaşma (banding) görünür. 64 mobilde de makul.
            asset.colorGradingLutSize = 64;

            // Gölgeler: araç oyununda kamera sürekli ileri bakıyor, uzaktaki
            // gölgeler ekranın büyük kısmını kaplıyor. 2 kademe yakını iyi
            // gösterip uzağı bulanıklaştırıyordu; 4 kademe aynı mesafeyi çok
            // daha iyi dağıtıyor.
            asset.shadowDistance = 150f;
            asset.shadowCascadeCount = 4;
            asset.shadowDepthBias = 0.8f;
            asset.shadowNormalBias = 0.8f;
            SetSoftShadows(asset);

            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;

            Debug.Log("[Pipeline] URP kuruldu ve atandı: " + AssetPath);
            return asset;
        }

        // Yumuşak gölgeler: sert gölge kenarı, araç oyununda en çok "ucuz" hissi
        // veren şeylerden biri — özellikle aracın kendi gölgesi asfaltta
        // merdiven basamağı gibi görünüyor.
        //
        // Alanı doğrudan yazmıyoruz: "supportsSoftShadows" özelliğinin setter'ı
        // bazı URP sürümlerinde yok, o zaman derleme hatası olurdu.
        // SerializedObject ile yazmak, alan adı değişmişse sessizce geçilebilir
        // bir duruma dönüştürüyor — LinkRenderer ile aynı gerekçe.
        static void SetSoftShadows(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("m_SoftShadowsSupported");
            if (prop == null) return;

            prop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // m_RendererDataList özel bir alan; public API'si yok. SerializedObject ile
        // yazıyoruz. Alan adı yanlışsa FindProperty null döner — derleme hatası değil,
        // çalışma anında yakalanabilir bir durum, o yüzden bu yol daha güvenli.
        static bool LinkRenderer(UniversalRenderPipelineAsset asset, UniversalRendererData data)
        {
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null) return false;

            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = data;

            var defaultIndex = so.FindProperty("m_DefaultRendererIndex");
            if (defaultIndex != null) defaultIndex.intValue = 0;

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // SSAO temas gölgeleri verir — nesneler zemine "oturur", derinlik hissi artar.
        // Tip adı ve namespace'i URP sürümleri arasında oynayabildiği için doğrudan
        // tipe bağlanmıyoruz: yanlış tahmin derleme hatası olurdu.
        static void TryAddAmbientOcclusion(UniversalRendererData data)
        {
            if (data.rendererFeatures != null)
                foreach (var existing in data.rendererFeatures)
                    if (existing != null && existing.GetType().Name == "ScreenSpaceAmbientOcclusion") return;

            var type = FindUrpType("ScreenSpaceAmbientOcclusion");
            if (type == null)
            {
                Debug.LogWarning("[Pipeline] SSAO tipi bulunamadı, atlanıyor. " +
                                 "Renderer varlığına elle eklenebilir.");
                return;
            }

            var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
            if (feature == null) return;

            feature.name = "SSAO";
            data.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, data);
        }

        static System.Type FindUrpType(string simpleName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name.IndexOf("Universal", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                    if (t != null && t.Name == simpleName) return t;
            }
            return null;
        }

        // ---------------------------------------------------------- Player Settings

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "DreamCar";
            PlayerSettings.productName = "DreamCar";

            // Linear renk uzayı: PBR malzemeler ancak burada doğru görünür. Gamma'da
            // aynı metalik boya cansız ve düz çıkar — "grafik kötü" hissinin büyük
            // bir kısmı buradan gelir.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Araba oyunu — yatay.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            const string bundleId = "com.dreamcar.game";

            TryConfigure(NamedBuildTarget.iOS, bundleId);
            TryConfigure(NamedBuildTarget.Android, bundleId);

            // Android: 64-bit zorunlu (Play Store şartı), IL2CPP olmadan seçilemez.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // API 24 Unity 6'da [Obsolete] ve Play Store'un kabul ettiği minimumun
            // altında; desteklenen en düşük seviye 26 (Android 8.0).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // HEDEF API'yi SABİT BİR SAYIYA ÇAKMIYORUZ.
            //
            // Play Store hedef API eşiğini her yıl ağustosta yükseltiyor; sabit
            // yazılan her sayı bir sonraki ağustosta yayını engelliyor ve bunu
            // ancak yükleme reddedilince fark ediyorsun. "Auto", kurulu Android
            // SDK'nın en yükseğini kullanıyor — yani SDK'yı güncellemek hedefi
            // de güncelliyor.
            //
            // CI tarafında da workflow'daki sabit "AndroidApiLevel34" kaldırıldı;
            // orası da artık bu ayarı takip ediyor.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            EnsureBothInputBackends();
            ApplyQualitySettings();

            Debug.Log("[Pipeline] Player Settings uygulandı (Linear, IL2CPP, ARM64, yatay).");
        }

        // URP varlığının kapsamadığı, QualitySettings tarafında duran ayarlar.
        static void ApplyQualitySettings()
        {
            // ANİZOTROPİK FİLTRELEME — bu oyunda en çok göze çarpan tek ayar.
            //
            // Yol kaplaması sürekli sığ açıyla görülüyor: kamera aracın
            // arkasında, asfalt ufka doğru uzanıyor. İki doğrusal filtreleme o
            // açıda dokuyu birkaç metre ötede lapaya çeviriyor ve şerit
            // çizgileri kayboluyor. Anizotropik filtreleme tam olarak bu durumu
            // düzeltiyor ve mobil GPU'larda maliyeti düşük.
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // Uzak nesneler erken basitleşmesin — açık dünyada silüetler
            // gözle görülür şekilde "atlıyor".
            QualitySettings.lodBias = 1.5f;

            // Araç boyası metalik; yansıma probu güncellenmezse gün/gece
            // döngüsünde boya rengi ortamla uyumsuz kalıyor.
            QualitySettings.realtimeReflectionProbes = true;

            // BİLEREK dokunulmayanlar: globalTextureMipmapLimit, softParticles ve
            // skinWeights. Üçü de ya Unity 6'da yeniden adlandırıldı ya URP
            // tarafına taşındı, ve üçü de zaten istediğimiz varsayılanda —
            // derleme riskini karşılığı olmayan bir değişiklik için almıyoruz.
        }

        // Girdi arka ucu "Both" olmalı — bu bir tercih değil, ZORUNLULUK.
        //
        // Projede iki farklı girdi API'si yan yana çalışıyor:
        //   • Bizim kodumuz ESKİ Input Manager'ı kullanıyor: MobileTouchInput
        //     (Input.GetTouch, Input.touchCount, Input.GetAxisRaw), PauseMenu ve
        //     CameraModeController (Input.GetKeyDown), CruiseControl, HighBeam.
        //   • RCCP YENİ Input System'i kullanıyor (InputAction, InputActionMap).
        //
        // Yalnızca "Input System Package (New)" seçilirse bizim her
        // Input.GetTouch çağrımız çalışma anında InvalidOperationException
        // atar — yani dokunmatik sürüş tamamen ölür. Yalnızca "Input Manager
        // (Old)" seçilirse RCCP girdi alamaz.
        //
        // Ayar PlayerSettings API'sinde açık değil; ProjectSettings.asset
        // içindeki "activeInputHandler" alanına SerializedObject ile yazılıyor
        // (0 = eski, 1 = yeni, 2 = ikisi). Değişiklik Editor yeniden
        // başlatılınca etkinleşir.
        static void EnsureBothInputBackends()
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;

                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop == null) return;               // alan adı sürümle değişmişse sessizce geç
                if (prop.intValue == 2) return;         // zaten "Both"

                prop.intValue = 2;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.LogWarning("[Pipeline] Active Input Handling → Both yapıldı " +
                                 "(RCCP yeni, bizim kodumuz eski girdi API'sini kullanıyor). " +
                                 "Etkinleşmesi için Unity'yi yeniden başlat.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Pipeline] Girdi arka ucu ayarlanamadı: " + e.Message +
                                 "\nElle: Edit → Project Settings → Player → Other Settings → " +
                                 "Active Input Handling → Both");
            }
        }

        static void TryConfigure(NamedBuildTarget target, string bundleId)
        {
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, bundleId);
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Pipeline] {target.TargetName} ayarlanamadı: {e.Message}");
            }
        }
    }
}
#endif
