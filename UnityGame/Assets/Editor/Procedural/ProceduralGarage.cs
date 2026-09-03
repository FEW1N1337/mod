#if UNITY_EDITOR
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // ANA MENÜ SAHNESİ TAMAMEN BOŞTU.
    //
    // CreateMainMenu sahneye yalnızca bir kamera ve UI koyuyordu — hiç 3B
    // içerik yok. Garaj diye bir mekân yok, aydınlatma yok, zemin yok. Araç
    // önizlemesi de düz bir panele basılan küçük resimdi.
    //
    // Referans oyundaki garaj görüntüsünün oluşmamasının sebebi model
    // kalitesi değil, SAHNENİN BOŞ OLMASIYDI. Şehir üreten, sekiz harita
    // üreten, araç mesh'i üreten bir altyapı vardı; garaj için hiç
    // kullanılmamıştı.
    //
    // Bu dosya o odayı kuruyor: zemin, üç duvar, tavan, tavan ışıkları ve
    // ortada aracın döneceği bir pivot.
    public static class ProceduralGarage
    {
        // Ölçüler araca göre: araçlar ~4.2 m uzunluğunda, ~1.8 m eninde.
        const float HalfWidth = 9f;    // x: -9 .. +9
        const float BackZ = 6f;        // arka duvar
        const float FrontZ = -8f;      // ön açık — kamera oradan bakıyor
        const float Height = 4.6f;

        public static Transform Build(GameObject parent)
        {
            var root = new GameObject("Garage");
            root.transform.SetParent(parent ? parent.transform : null, false);

            var floorMat = ProceduralTextures.CreateTexturedMaterial(
                "mat_garage_floor", "garage_floor", 0.05f, 0.55f, new Vector2(4f, 4f));
            var wallMat = ProceduralTextures.CreateTexturedMaterial(
                "mat_garage_wall", "garage_wall", 0f, 0.22f, new Vector2(6f, 2f));
            var ceilMat = ProceduralTextures.CreateTexturedMaterial(
                "mat_garage_ceiling", "sidewalk", 0f, 0.15f, new Vector2(5f, 4f));

            BuildFloor(root, floorMat);
            BuildWalls(root, wallMat);
            BuildCeiling(root, ceilMat);
            BuildLights(root);

            // Araç boyası metalik; yansıtacak bir şey yoksa parlak değil, koyu
            // ve mat görünür. Oda kapalı olduğu için prob burada özellikle
            // işe yarıyor — duvarları ve ışıkları yansıtıyor.
            AddReflectionProbe(root, HalfWidth, Height);

            // Aracın oturacağı pivot. GarageCarousel önizlemeyi buraya
            // Instantiate ediyor ve turntableDegPerSecond ile döndürüyor.
            var turntable = new GameObject("~Turntable");
            turntable.transform.SetParent(root.transform, false);
            turntable.transform.localPosition = Vector3.zero;

            return turntable.transform;
        }

        // Kamerayı aracı 3/4 önden görecek şekilde konumlandırır.
        public static void FrameCamera(Camera cam)
        {
            if (!cam) return;
            cam.transform.position = new Vector3(-5.6f, 1.75f, -7.2f);
            cam.transform.LookAt(new Vector3(0f, 0.75f, 0f));
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            // Oda kapalı: gökyüzü hiç görünmüyor, düz renk hem daha ucuz hem
            // duvar kenarlarında sızıntı bırakmıyor.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f);
        }

        // ------------------------------------------------------------ parçalar

        static void BuildFloor(GameObject root, Material mat)
        {
            var mb = new MeshBuilder();
            mb.AddFlatQuad(
                new Vector3(-HalfWidth, 0f, FrontZ),
                new Vector3( HalfWidth, 0f, FrontZ),
                new Vector3( HalfWidth, 0f, BackZ),
                new Vector3(-HalfWidth, 0f, BackZ));
            ProceduralCityGenerator.CreateMeshObject(root, "Floor", mb.ToMesh("garage_floor"), mat);
        }

        static void BuildWalls(GameObject root, Material mat)
        {
            var mb = new MeshBuilder();

            // Arka duvar — içeriden görünsün diye sarım yönü içe bakıyor.
            mb.AddFlatQuad(
                new Vector3( HalfWidth, 0f,      BackZ),
                new Vector3(-HalfWidth, 0f,      BackZ),
                new Vector3(-HalfWidth, Height,  BackZ),
                new Vector3( HalfWidth, Height,  BackZ));

            // Sol duvar
            mb.AddFlatQuad(
                new Vector3(-HalfWidth, 0f,      FrontZ),
                new Vector3(-HalfWidth, 0f,      BackZ),
                new Vector3(-HalfWidth, Height,  BackZ),
                new Vector3(-HalfWidth, Height,  FrontZ));

            // Sağ duvar
            mb.AddFlatQuad(
                new Vector3( HalfWidth, 0f,      BackZ),
                new Vector3( HalfWidth, 0f,      FrontZ),
                new Vector3( HalfWidth, Height,  FrontZ),
                new Vector3( HalfWidth, Height,  BackZ));

            // ÖN DUVAR YOK: kamera oradan bakıyor. Koysaydık kamera duvarın
            // içinde kalırdı.
            ProceduralCityGenerator.CreateMeshObject(root, "Walls", mb.ToMesh("garage_walls"), mat);
        }

        static void BuildCeiling(GameObject root, Material mat)
        {
            var mb = new MeshBuilder();
            mb.AddFlatQuad(
                new Vector3(-HalfWidth, Height, BackZ),
                new Vector3( HalfWidth, Height, BackZ),
                new Vector3( HalfWidth, Height, FrontZ),
                new Vector3(-HalfWidth, Height, FrontZ));
            ProceduralCityGenerator.CreateMeshObject(root, "Ceiling", mb.ToMesh("garage_ceiling"), mat);
        }

        // Tavan ışıkları: hem GÖRÜNEN emissive şerit hem GERÇEK Light.
        //
        // Yalnızca emissive koyarsak araç aydınlanmaz (emissive yüzey URP'de
        // gerçek zamanlı ışık yaymıyor); yalnızca Light koyarsak ışık kaynağı
        // görünmez ve tavan boş kalır. İkisi birlikte gerekiyor.
        static void BuildLights(GameObject root)
        {
            // CreateEmissiveMaterial tek renk alıyor; parlaklığı rengin
            // kendisine katıyoruz (HDR emission).
            var glow = ProceduralTextures.CreateEmissiveMaterial(
                "mat_garage_light", new Color(1f, 0.97f, 0.9f) * 3.2f);

            float[] xs = { -3.4f, 3.4f };
            foreach (float x in xs)
            {
                var mb = new MeshBuilder();
                mb.AddBox(new Vector3(x, Height - 0.12f, (BackZ + FrontZ) * 0.5f),
                          new Vector3(0.55f, 0.12f, (BackZ - FrontZ) * 0.72f));
                ProceduralCityGenerator.CreateMeshObject(
                    root, $"LightStrip_{(x < 0 ? "L" : "R")}",
                    mb.ToMesh($"garage_light_{(x < 0 ? "l" : "r")}"), glow);

                var go = new GameObject($"Lamp_{(x < 0 ? "L" : "R")}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(x, Height - 0.3f, 0f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 18f;
                light.intensity = 2.4f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.Soft;
            }

            // Ön taraftan yumuşak dolgu: yalnızca tepeden ışıkla aracın ön yüzü
            // ve kaputu karanlıkta kalıyor, kamera tam oradan bakıyor.
            var fill = new GameObject("FillLight");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(-4f, 2.6f, FrontZ + 1.5f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 16f;
            fillLight.intensity = 1.1f;
            fillLight.color = new Color(0.85f, 0.9f, 1f);
            fillLight.shadows = LightShadows.None;
        }

        static void AddReflectionProbe(GameObject parent, float extent, float height)
        {
            var go = new GameObject("~ReflectionProbe");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            // ViaScripting + RenderProbe() editör zamanında güvenilir değil
            // (sahne henüz kaydedilmemiş, prob boş kalabiliyor). Oyun
            // sahnesindeki probla aynı ayar: her kare, yüz başına bölünmüş.
            // Menü sahnesi küçük, maliyeti düşük.
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(extent * 2f, height, BackZ - FrontZ);
            probe.resolution = 128;
            probe.hdr = true;
            probe.shadowDistance = 0f;
            probe.cullingMask = ~0;
            probe.boxProjection = true;   // kapalı oda — kutu izdüşümü burada DOĞRU
            probe.importance = 1;
        }
    }
}
#endif
