#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DreamCar.EditorTools.Procedural
{
    // Kalite kademesi başına bir Volume Profile üretir. GraphicsTuner çalışma
    // anında cihaza uygun olanı yükler.
    //
    // Mobil maliyet sırası (ucuzdan pahalıya):
    //   Color Adjustments ≈ bedava · Vignette ucuz · Bloom orta ·
    //   Tonemapping orta · Chromatic Aberration orta · Motion Blur pahalı
    //
    // Menü: DreamCar → Procedural → Generate Post-Processing Profiles
    public static class ProceduralPostProcessing
    {
        const string Folder = "Assets/Generated/PostProcessing";

        [MenuItem("DreamCar/Procedural/Generate Post-Processing Profiles")]
        public static void GenerateAllInteractive() => GenerateAll(confirm: true);

        // confirm=false → BUILD EVERYTHING zincirinden çağrılırken diyalog açmaz.
        public static void GenerateAll(bool confirm)
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            BuildLow();
            BuildMid();
            BuildHigh();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PostFX] Üç profil üretildi: " + Folder);
            if (confirm)
                EditorUtility.DisplayDialog("DreamCar",
                    "Post-processing profilleri hazır.\n\n" +
                    "• PostFX_Low — sadece renk düzeltme (neredeyse bedava)\n" +
                    "• PostFX_Mid — + bloom, vignette\n" +
                    "• PostFX_High — + ACES tonemapping, motion blur\n\n" +
                    "GraphicsTuner cihaza göre otomatik seçecek.\n" +
                    "Kameranın Post Processing kutusu işaretli olmalı.", "Tamam");
        }

        // --- Düşük: görüntüyü canlandır ama hiçbir ek geçiş ekleme ---
        static void BuildLow()
        {
            var profile = CreateProfile("PostFX_Low");

            var color = Add<ColorAdjustments>(profile);
            Set(color.postExposure, 0.08f);
            Set(color.contrast, 8f);
            Set(color.saturation, 12f);

            EditorUtility.SetDirty(profile);
        }

        // --- Orta: bloom + vignette, bloom kalitesi düşük tutulur ---
        static void BuildMid()
        {
            var profile = CreateProfile("PostFX_Mid");

            var color = Add<ColorAdjustments>(profile);
            Set(color.postExposure, 0.10f);
            Set(color.contrast, 12f);
            Set(color.saturation, 14f);

            var bloom = Add<Bloom>(profile);
            Set(bloom.intensity, 0.55f);
            Set(bloom.threshold, 1.05f);
            Set(bloom.scatter, 0.62f);
            // highQualityFiltering kapalı: mobilde bloom maliyetinin çoğu buradan gelir
            Set(bloom.highQualityFiltering, false);
            Set(bloom.downscale, BloomDownscaleMode.Half);

            var vignette = Add<Vignette>(profile);
            Set(vignette.intensity, 0.22f);
            Set(vignette.smoothness, 0.42f);

            EditorUtility.SetDirty(profile);
        }

        // --- Yüksek: ACES + motion blur + hafif kromatik sapma ---
        static void BuildHigh()
        {
            var profile = CreateProfile("PostFX_High");

            var tonemap = Add<Tonemapping>(profile);
            Set(tonemap.mode, TonemappingMode.ACES);

            var color = Add<ColorAdjustments>(profile);
            Set(color.postExposure, 0.15f);
            Set(color.contrast, 14f);
            Set(color.saturation, 10f);
            // Hafif sıcak ton — asfalt ve gökyüzü daha az steril görünür
            Set(color.colorFilter, new Color(1.02f, 1.0f, 0.97f));

            var bloom = Add<Bloom>(profile);
            Set(bloom.intensity, 0.85f);
            Set(bloom.threshold, 0.95f);
            Set(bloom.scatter, 0.68f);
            Set(bloom.highQualityFiltering, true);
            Set(bloom.downscale, BloomDownscaleMode.Half);

            var vignette = Add<Vignette>(profile);
            Set(vignette.intensity, 0.26f);
            Set(vignette.smoothness, 0.40f);

            // Hız hissi — yalnızca yüksek kademede, düşük şiddette
            var motionBlur = Add<MotionBlur>(profile);
            Set(motionBlur.mode, MotionBlurMode.CameraOnly);
            Set(motionBlur.quality, MotionBlurQuality.Low);
            Set(motionBlur.intensity, 0.22f);

            var chromatic = Add<ChromaticAberration>(profile);
            Set(chromatic.intensity, 0.08f);

            EditorUtility.SetDirty(profile);
        }

        // ---------------------------------------------------------- Yardımcılar

        static VolumeProfile CreateProfile(string name)
        {
            string path = $"{Folder}/{name}.asset";

            // Var olanı temizleyip yeniden kur — üst üste bileşen birikmesin
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        // VolumeProfile.Add<T> bileşeni oluşturur ama alt-varlık olarak kaydetmez;
        // kaydedilmezse profil yeniden yüklendiğinde bileşen kaybolur.
        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(overrides: true);
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // Volume parametrelerinde hem değeri hem "override edildi" bayrağını
        // ayarlamak gerekir; yalnızca değeri yazmak etkisiz kalır.
        static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }
    }
}
#endif
