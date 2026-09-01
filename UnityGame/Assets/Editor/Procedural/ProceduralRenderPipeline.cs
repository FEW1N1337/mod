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
            asset.shadowDistance = 120f;
            asset.shadowCascadeCount = 2;
            asset.supportsCameraDepthTexture = true;   // SSAO ve motion blur buna bağlı
            asset.supportsCameraOpaqueTexture = false; // pahalı, kullanmıyoruz

            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;

            Debug.Log("[Pipeline] URP kuruldu ve atandı: " + AssetPath);
            return asset;
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
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            Debug.Log("[Pipeline] Player Settings uygulandı (Linear, IL2CPP, ARM64, yatay).");
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
