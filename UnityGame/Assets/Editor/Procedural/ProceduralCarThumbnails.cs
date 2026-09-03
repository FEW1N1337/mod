#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using DreamCar.Economy;

namespace DreamCar.EditorTools.Procedural
{
    // GARAJIN ORTASI BOŞ BİR DİKDÖRTGENDİ.
    //
    // CarDefinition.thumbnail alanı vardı, GarageCarousel onu okuyordu
    // (GarageCarousel.cs:60 → thumbnail.sprite = def.thumbnail), ama projede
    // o alana HİÇBİR YERDEN atama yoktu. sprite = null → garajın ortasındaki
    // 760×340'lık alan bomboş. Oklar yanında, ad ve fiyat altında, araç yok.
    //
    // Oturum boyunca kovaladığım hata ailesinin bir örneği daha: alan var,
    // okuyan var, yazan yok — ve hiçbir hata vermiyor.
    //
    // Canlı 3B önizleme (GarageCarousel.previewMount) bilerek kapalı: araç
    // prefabı PhotonView ve Rigidbody taşıyor, menü sahnesinde odaya bağlı
    // olmadan doğurmak hata üretiyor. Editörde render alıp sprite kaydetmek
    // o sorunu hiç doğurmuyor — edit modunda Awake koşmuyor.
    //
    // Menü: DreamCar → Procedural → Araç küçük resimlerini üret
    public static class ProceduralCarThumbnails
    {
        const string Folder = "Assets/Generated/CarThumbs";
        const int Size = 512;

        [MenuItem("DreamCar/Procedural/Araç küçük resimlerini üret")]
        public static void GenerateInteractive()
        {
            int n = GenerateAll();
            EditorUtility.DisplayDialog("DreamCar",
                n > 0 ? $"{n} araç için küçük resim üretildi.\n\n{Folder}"
                      : "Küçük resim üretilemedi — katalog boş ya da prefablar " +
                        "Resources altında çözülemedi. Önce BUILD EVERYTHING.",
                "Tamam");
        }

        public static int GenerateAll()
        {
            var catalog = LoadCatalog();
            if (catalog == null || catalog.cars == null || catalog.cars.Count == 0)
            {
                Debug.LogWarning("[Thumb] CarCatalog bulunamadı ya da boş.");
                return 0;
            }

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            int made = 0;
            foreach (var def in catalog.cars)
            {
                if (def == null || string.IsNullOrEmpty(def.resourcePrefabName)) continue;

                var prefab = Resources.Load<GameObject>(def.resourcePrefabName);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Thumb] '{def.id}': Resources/{def.resourcePrefabName} yok, atlandı.");
                    continue;
                }

                var sprite = Render(prefab, def.id);
                if (sprite == null) continue;

                def.thumbnail = sprite;
                EditorUtility.SetDirty(def);
                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Thumb] {made} araç küçük resmi üretildi → {Folder}");
            return made;
        }

        // --- Tek aracın render'ı ---

        static Sprite Render(GameObject prefab, string id)
        {
            // Sahneden UZAKTA örnekliyoruz: üretim sırasında açık olan sahnede
            // arazi, şehir ve ışıklar duruyor; aracı oraya koymak kadraja
            // onları da sokardı.
            var instance = Object.Instantiate(prefab, new Vector3(0f, -5000f, 0f), Quaternion.identity);
            instance.hideFlags = HideFlags.HideAndDontSave;

            var camGo = new GameObject("~ThumbCam") { hideFlags = HideFlags.HideAndDontSave };
            var lightGo = new GameObject("~ThumbLight") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                var bounds = ComputeBounds(instance);
                if (bounds.size.sqrMagnitude < 0.0001f)
                {
                    Debug.LogWarning($"[Thumb] '{id}': renderer bulunamadı, atlandı.");
                    return null;
                }

                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Alfa 0 — panelin üstünde arka planı olmayan bir araç duruyor.
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = false;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 200f;
                // Sahnedeki post-processing bu render'a karışmasın.
                var camData = camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                           ?? camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                camData.renderPostProcessing = false;
                camData.renderShadows = false;

                // 3/4 önden bakış — aracın hem yanı hem ön yüzü görünsün.
                // Mesafe SINIRLARDAN hesaplanıyor: sabit bir sayı yazmak farklı
                // boyuttaki araçlarda kimini kırpar, kimini nokta gibi bırakırdı.
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
                var dir = Quaternion.Euler(18f, 145f, 0f) * Vector3.forward;
                camGo.transform.position = bounds.center - dir * distance;
                camGo.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.shadows = LightShadows.None;
                lightGo.transform.rotation = Quaternion.Euler(40f, 160f, 0f);

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4
                };
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;

                return Save(tex, "car_" + Sanitize(id));
            }
            finally
            {
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(instance);
            }
        }

        // Aracın dünya sınırları. RCCPCarConverter.ComputeBounds ile aynı
        // yaklaşım; orada yerel uzaya çevriliyordu (çapa konumu için), burada
        // kamerayı dünyada konumlandırdığımız için dünya uzayında kalıyor.
        static Bounds ComputeBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ProceduralUISprites.Save ile aynı yol: PNG yaz, Sprite olarak içe aktar.
        static Sprite Save(Texture2D tex, string name)
        {
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string Sanitize(string id)
        {
            var chars = id.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        static CarCatalog LoadCatalog()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CarCatalog"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CarCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }
    }
}
#endif
