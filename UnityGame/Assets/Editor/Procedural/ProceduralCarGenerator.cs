#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DreamCar.Car;
using DreamCar.Effects;
using DreamCar.Audio;
using DreamCar.Vehicle;
using DreamCar.Customization;
using DreamCar.Network;

namespace DreamCar.EditorTools.Procedural
{
    // Araba gövdesini matematiksel olarak üretir: uzunluk boyunca kesitler tanımlanır,
    // aralarına yüzey gerilir (loft). Kaput eğimi, kabin yükselmesi, bagaj düşüşü
    // kesit yüksekliklerinden gelir — gerçek bir araba silueti çıkar.
    //
    // Menü: DreamCar → Procedural → Generate All Cars
    public static class ProceduralCarGenerator
    {
        const string MeshFolder = "Assets/Generated/Meshes";
        const string MaterialFolder = "Assets/Generated/Materials";
        const string PrefabFolder = "Assets/Resources";

        // Gövde uzunluğu boyunca bir kesit.
        struct Section
        {
            public float z;          // uzunluk ekseni konumu
            public float halfWidth;
            public float halfHeight;
            public float centerY;
            public Section(float z, float halfWidth, float halfHeight, float centerY)
            { this.z = z; this.halfWidth = halfWidth; this.halfHeight = halfHeight; this.centerY = centerY; }
        }

        class CarPreset
        {
            public string id;
            public string displayName;
            public Section[] sections;
            public float wheelRadius = 0.34f;
            public float wheelWidth = 0.22f;
            public float frontAxleZ = 1.35f;
            public float rearAxleZ = -1.35f;
            public float trackHalfWidth = 0.78f;
            public Color paint = new Color(0.85f, 0.15f, 0.18f);
            public float mass = 1250f;
            public float motorTorque = 1500f;
            public float topSpeed = 180f;
        }

        // --- Gövde profilleri ---
        // Kesitler: (z, yarıGenişlik, yarıYükseklik, merkezY)
        // centerY + halfHeight = üst çizgi → kabin buradan yükselir.
        static readonly CarPreset[] Presets =
        {
            new CarPreset
            {
                id = "car.sedan", displayName = "Sedan",
                paint = new Color(0.16f, 0.32f, 0.68f),
                mass = 1350f, motorTorque = 1450f, topSpeed = 175f,
                sections = new[]
                {
                    new Section(-2.20f, 0.76f, 0.20f, 0.52f),
                    new Section(-1.95f, 0.86f, 0.26f, 0.54f),
                    new Section(-1.40f, 0.90f, 0.30f, 0.56f),
                    new Section(-0.95f, 0.91f, 0.42f, 0.62f),
                    new Section(-0.35f, 0.92f, 0.52f, 0.68f),
                    new Section( 0.25f, 0.92f, 0.54f, 0.70f),
                    new Section( 0.80f, 0.90f, 0.44f, 0.64f),
                    new Section( 1.35f, 0.88f, 0.30f, 0.55f),
                    new Section( 1.90f, 0.84f, 0.24f, 0.51f),
                    new Section( 2.25f, 0.74f, 0.19f, 0.49f),
                }
            },
            new CarPreset
            {
                id = "car.hatchback", displayName = "Hatchback",
                paint = new Color(0.92f, 0.72f, 0.12f),
                mass = 1100f, motorTorque = 1250f, topSpeed = 165f,
                frontAxleZ = 1.15f, rearAxleZ = -1.15f,
                sections = new[]
                {
                    new Section(-1.80f, 0.78f, 0.34f, 0.60f),
                    new Section(-1.55f, 0.86f, 0.46f, 0.66f),
                    new Section(-1.10f, 0.89f, 0.52f, 0.70f),
                    new Section(-0.50f, 0.90f, 0.54f, 0.72f),
                    new Section( 0.10f, 0.90f, 0.53f, 0.71f),
                    new Section( 0.65f, 0.88f, 0.42f, 0.63f),
                    new Section( 1.20f, 0.85f, 0.29f, 0.54f),
                    new Section( 1.70f, 0.80f, 0.23f, 0.50f),
                    new Section( 1.98f, 0.72f, 0.19f, 0.48f),
                }
            },
            new CarPreset
            {
                id = "car.sport", displayName = "Sport Coupe",
                paint = new Color(0.88f, 0.16f, 0.14f),
                mass = 1150f, motorTorque = 1950f, topSpeed = 235f,
                wheelRadius = 0.36f, wheelWidth = 0.27f,
                frontAxleZ = 1.30f, rearAxleZ = -1.28f, trackHalfWidth = 0.84f,
                sections = new[]
                {
                    new Section(-2.05f, 0.82f, 0.17f, 0.44f),
                    new Section(-1.80f, 0.92f, 0.22f, 0.46f),
                    new Section(-1.30f, 0.95f, 0.26f, 0.48f),
                    new Section(-0.80f, 0.94f, 0.38f, 0.55f),
                    new Section(-0.25f, 0.93f, 0.44f, 0.59f),
                    new Section( 0.30f, 0.92f, 0.42f, 0.57f),
                    new Section( 0.85f, 0.91f, 0.32f, 0.50f),
                    new Section( 1.45f, 0.89f, 0.22f, 0.43f),
                    new Section( 1.95f, 0.84f, 0.17f, 0.40f),
                    new Section( 2.20f, 0.74f, 0.14f, 0.39f),
                }
            },
            new CarPreset
            {
                id = "car.suv", displayName = "SUV",
                paint = new Color(0.20f, 0.22f, 0.24f),
                mass = 1850f, motorTorque = 1750f, topSpeed = 165f,
                wheelRadius = 0.40f, wheelWidth = 0.26f,
                frontAxleZ = 1.42f, rearAxleZ = -1.42f, trackHalfWidth = 0.86f,
                sections = new[]
                {
                    new Section(-2.25f, 0.84f, 0.38f, 0.72f),
                    new Section(-2.00f, 0.92f, 0.50f, 0.80f),
                    new Section(-1.40f, 0.96f, 0.56f, 0.84f),
                    new Section(-0.70f, 0.97f, 0.58f, 0.86f),
                    new Section( 0.00f, 0.97f, 0.58f, 0.86f),
                    new Section( 0.70f, 0.96f, 0.55f, 0.83f),
                    new Section( 1.30f, 0.94f, 0.40f, 0.72f),
                    new Section( 1.90f, 0.90f, 0.31f, 0.65f),
                    new Section( 2.30f, 0.80f, 0.26f, 0.62f),
                }
            },
            new CarPreset
            {
                id = "car.pickup", displayName = "Pickup",
                paint = new Color(0.30f, 0.55f, 0.35f),
                mass = 1700f, motorTorque = 1650f, topSpeed = 160f,
                wheelRadius = 0.39f, wheelWidth = 0.25f,
                frontAxleZ = 1.45f, rearAxleZ = -1.50f, trackHalfWidth = 0.85f,
                sections = new[]
                {
                    new Section(-2.55f, 0.88f, 0.28f, 0.66f),
                    new Section(-2.20f, 0.94f, 0.32f, 0.68f),
                    new Section(-1.20f, 0.94f, 0.32f, 0.68f),
                    new Section(-0.85f, 0.94f, 0.56f, 0.84f),
                    new Section(-0.30f, 0.95f, 0.58f, 0.86f),
                    new Section( 0.35f, 0.94f, 0.54f, 0.83f),
                    new Section( 0.95f, 0.93f, 0.38f, 0.71f),
                    new Section( 1.75f, 0.90f, 0.30f, 0.65f),
                    new Section( 2.20f, 0.80f, 0.25f, 0.62f),
                }
            },
        };

        [MenuItem("DreamCar/Procedural/Generate All Cars")]
        public static void GenerateAll()
        {
            EnsureFolders();
            ProceduralTextures.GenerateAll();
            // Emote ikonları UI sprite klasöründen geliyor. BUILD EVERYTHING
            // zinciri onları zaten araçlardan önce üretiyor, ama bu menü tek
            // başına da çağrılabiliyor ve o durumda ikonlar null kalırdı.
            ProceduralUISprites.GenerateAll();

            foreach (var preset in Presets) Generate(preset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("DreamCar",
                $"{Presets.Length} araç üretildi.\n\n" +
                "Assets/Resources/ altında prefab'ları,\n" +
                "Assets/Generated/ altında mesh ve materyalleri bulacaksın.\n\n" +
                "Şimdi: DreamCar → Procedural → Build Car Catalog", "Tamam");
        }

        static void Generate(CarPreset preset)
        {
            string shortName = preset.id.Replace("car.", "");

            Mesh bodyMesh = BuildBodyMesh(preset, out Mesh glassMesh);
            Mesh wheelMesh = BuildWheelMesh(preset);

            SaveMesh(bodyMesh, $"{shortName}_body");
            SaveMesh(glassMesh, $"{shortName}_glass");
            SaveMesh(wheelMesh, $"{shortName}_wheel");

            var paintMat = ProceduralTextures.CreatePaintMaterial($"{shortName}_paint", preset.paint);
            var glassMat = ProceduralTextures.CreateGlassMaterial($"{shortName}_glass");
            var tireMat = ProceduralTextures.CreateTireMaterial();
            var rimMat = ProceduralTextures.CreateRimMaterial();
            var lightMat = ProceduralTextures.CreateEmissiveMaterial("headlight", new Color(1.6f, 1.55f, 1.3f));
            var tailMat = ProceduralTextures.CreateEmissiveMaterial("taillight", new Color(1.4f, 0.08f, 0.05f));

            var root = new GameObject(preset.displayName);
            BuildHierarchy(root, preset, bodyMesh, glassMesh, wheelMesh,
                           paintMat, glassMat, tireMat, rimMat, lightMat, tailMat);

            string prefabPath = $"{PrefabFolder}/{ToPrefabName(preset.id)}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);

            // Menü garajı için AYRI bir önizleme prefabı.
            //
            // GarageCarousel.previewMount bugüne kadar bilerek boş bırakılmıştı:
            // asıl araç prefabı PhotonView ve Rigidbody taşıyor, menü sahnesinde
            // odaya bağlı olmadan doğurmak hata üretiyor. O gerekçe hâlâ geçerli
            // — çözüm prefabı ayırmak, önizlemeden vazgeçmek değil.
            SavePreviewPrefab(root, preset.id);

            Object.DestroyImmediate(root);

            if (!ok) Debug.LogError($"[Procedural] Prefab kaydedilemedi: {prefabPath}");
            else Debug.Log($"[Procedural] {preset.displayName} → {prefabPath}");
        }

        // Yalnızca GÖRÜNEN hiyerarşi: MeshFilter + MeshRenderer. Rigidbody,
        // WheelCollider, PhotonView, collider ve bütün MonoBehaviour'lar
        // atılıyor. Menüde doğurulması tamamen zararsız.
        internal static void SavePreviewPrefab(GameObject source, string carId)
        {
            var copy = Object.Instantiate(source);
            copy.name = PreviewPrefabName(carId);

            // Alttan üste doğru: bir bileşeni silmek başka bir bileşenin
            // RequireComponent bağımlılığını kırabiliyor, ters sırada gitmek
            // "can't remove because X depends on it" hatalarını doğuruyor.
            foreach (var c in copy.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (c is Transform) continue;
                if (c is MeshFilter) continue;
                if (c is MeshRenderer) continue;
                Object.DestroyImmediate(c, allowDestroyingAssets: false);
            }

            // Işık ve partikül gibi görsel olmayan çocuklar da kalksın: menüde
            // araç prefabındaki far ışıkları sahneyi yıkar.
            foreach (var light in copy.GetComponentsInChildren<Light>(true))
                if (light) Object.DestroyImmediate(light.gameObject);

            string path = $"{PrefabFolder}/{copy.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(copy, path, out bool previewOk);
            Object.DestroyImmediate(copy);

            if (!previewOk) Debug.LogWarning($"[Procedural] Önizleme prefabı kaydedilemedi: {path}");
        }

        internal static string PreviewPrefabName(string carId) => "Preview_" + ToPrefabName(carId);

        public static string ToPrefabName(string carId) =>
            "Car_" + carId.Replace("car.", "");

        // --- Gövde mesh'i ---
        static Mesh BuildBodyMesh(CarPreset preset, out Mesh glassMesh)
        {
            const int ringSegments = 20;
            const float exponent = 3.4f; // yuvarlatılmış dikdörtgen

            var body = new MeshBuilder();
            var rings = new List<Vector3[]>();

            foreach (var s in preset.sections)
                rings.Add(MeshBuilder.SuperellipseRing(s.z, s.halfWidth, s.halfHeight, s.centerY, ringSegments, exponent));

            for (int i = 0; i < rings.Count - 1; i++)
            {
                float vA = (float)i / (rings.Count - 1);
                float vB = (float)(i + 1) / (rings.Count - 1);
                body.LoftRings(rings[i], rings[i + 1], vA, vB);
            }
            body.CapRing(rings[0], Vector3.back, true);
            body.CapRing(rings[^1], Vector3.forward, false);

            // Farlar ve stoplar — gövdenin ön/arka ucuna küçük kutular.
            var frontSection = preset.sections[^1];
            var rearSection = preset.sections[0];
            float fz = frontSection.z + 0.02f;
            float rz = rearSection.z - 0.02f;

            body.AddBox(new Vector3(-frontSection.halfWidth * 0.62f, frontSection.centerY + 0.02f, fz), new Vector3(0.34f, 0.13f, 0.06f));
            body.AddBox(new Vector3( frontSection.halfWidth * 0.62f, frontSection.centerY + 0.02f, fz), new Vector3(0.34f, 0.13f, 0.06f));
            body.AddBox(new Vector3(-rearSection.halfWidth * 0.64f, rearSection.centerY + 0.03f, rz), new Vector3(0.30f, 0.11f, 0.06f));
            body.AddBox(new Vector3( rearSection.halfWidth * 0.64f, rearSection.centerY + 0.03f, rz), new Vector3(0.30f, 0.11f, 0.06f));

            // Camlar: kabin bölgesindeki kesitleri biraz içeri alıp ayrı mesh yaparız.
            glassMesh = BuildGlassMesh(preset, ringSegments, exponent);

            return body.ToMesh(preset.id + "_body");
        }

        // Kabin kesitlerinin üst yarısını hafif içerlek kopyalayarak cam yüzeyi üretir.
        static Mesh BuildGlassMesh(CarPreset preset, int ringSegments, float exponent)
        {
            var glass = new MeshBuilder();

            // Kabin = en yüksek centerY+halfHeight değerine sahip kesitlerin çevresi.
            float maxTop = 0f;
            foreach (var s in preset.sections) maxTop = Mathf.Max(maxTop, s.centerY + s.halfHeight);
            float threshold = maxTop - 0.16f;

            var cabinRings = new List<Vector3[]>();
            foreach (var s in preset.sections)
            {
                if (s.centerY + s.halfHeight < threshold) continue;
                cabinRings.Add(MeshBuilder.SuperellipseRing(
                    s.z, s.halfWidth * 0.965f, s.halfHeight * 0.985f, s.centerY + 0.005f,
                    ringSegments, exponent));
            }

            if (cabinRings.Count < 2) return glass.ToMesh(preset.id + "_glass");

            for (int i = 0; i < cabinRings.Count - 1; i++)
            {
                float vA = (float)i / (cabinRings.Count - 1);
                float vB = (float)(i + 1) / (cabinRings.Count - 1);
                glass.LoftRings(cabinRings[i], cabinRings[i + 1], vA, vB);
            }
            return glass.ToMesh(preset.id + "_glass");
        }

        // --- Tekerlek mesh'i (lastik + jant yüzü) ---
        static Mesh BuildWheelMesh(CarPreset preset)
        {
            var wheel = new MeshBuilder();
            float r = preset.wheelRadius;
            float hw = preset.wheelWidth * 0.5f;
            const int seg = 24;

            // Lastik: dış silindir (kapaksız — jant kapatacak)
            wheel.AddCylinderX(Vector3.zero, r, hw, seg, caps: false);

            // Yanaklar: dıştan janta inen halkalar
            float rimR = r * 0.62f;
            var outerL = new Vector3[seg];
            var innerL = new Vector3[seg];
            var outerR = new Vector3[seg];
            var innerR = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float t = 2f * Mathf.PI * i / seg;
                float cy = Mathf.Cos(t), sz = Mathf.Sin(t);
                outerL[i] = new Vector3(-hw, cy * r, sz * r);
                innerL[i] = new Vector3(-hw * 0.72f, cy * rimR, sz * rimR);
                outerR[i] = new Vector3(hw, cy * r, sz * r);
                innerR[i] = new Vector3(hw * 0.72f, cy * rimR, sz * rimR);
            }
            wheel.LoftRings(outerL, innerL, 0f, 1f);
            wheel.LoftRings(innerR, outerR, 0f, 1f);

            // Jant yüzeyleri
            wheel.AddDiscX(new Vector3(-hw * 0.72f, 0f, 0f), rimR, seg, faceLeft: true);
            wheel.AddDiscX(new Vector3( hw * 0.72f, 0f, 0f), rimR, seg, faceLeft: false);

            // Jant kollari (5 kol) — göbeğe doğru ince kutular
            for (int i = 0; i < 5; i++)
            {
                float ang = 2f * Mathf.PI * i / 5f;
                float mid = rimR * 0.55f;
                var center = new Vector3(-hw * 0.76f, Mathf.Cos(ang) * mid, Mathf.Sin(ang) * mid);
                wheel.AddBox(center, new Vector3(0.03f, rimR * 0.72f, 0.06f));
            }

            return wheel.ToMesh(preset.id + "_wheel");
        }

        // --- Prefab hiyerarşisi + oyun bileşenleri ---
        static void BuildHierarchy(GameObject root, CarPreset preset,
                                   Mesh bodyMesh, Mesh glassMesh, Mesh wheelMesh,
                                   Material paint, Material glass, Material tire, Material rim,
                                   Material headlight, Material taillight)
        {
            // Gövde
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var bodyRenderer = bodyGo.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = paint;

            // Cam
            if (glassMesh.vertexCount > 0)
            {
                var glassGo = new GameObject("Glass");
                glassGo.transform.SetParent(root.transform, false);
                glassGo.AddComponent<MeshFilter>().sharedMesh = glassMesh;
                glassGo.AddComponent<MeshRenderer>().sharedMaterial = glass;
            }

            // Fizik gövdesi
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = preset.mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.6f;

            float bodyLength = preset.sections[^1].z - preset.sections[0].z;
            float bodyCenterZ = (preset.sections[^1].z + preset.sections[0].z) * 0.5f;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(preset.trackHalfWidth * 2f * 0.95f, 0.9f, bodyLength * 0.92f);
            collider.center = new Vector3(0f, 0.65f, bodyCenterZ);

            // Tekerlekler
            var wheelSpecs = new (string name, Vector3 pos, bool front)[]
            {
                ("FL", new Vector3(-preset.trackHalfWidth, preset.wheelRadius, preset.frontAxleZ), true),
                ("FR", new Vector3( preset.trackHalfWidth, preset.wheelRadius, preset.frontAxleZ), true),
                ("RL", new Vector3(-preset.trackHalfWidth, preset.wheelRadius, preset.rearAxleZ), false),
                ("RR", new Vector3( preset.trackHalfWidth, preset.wheelRadius, preset.rearAxleZ), false),
            };

            var frontColliders = new List<WheelCollider>();
            var rearColliders = new List<WheelCollider>();
            var frontMeshes = new List<Transform>();
            var rearMeshes = new List<Transform>();
            var allColliders = new List<WheelCollider>();

            // Drift dumanı ve fren izi ortak materyal kullanır — dört tekerlek için
            // ayrı materyal üretmenin anlamı yok. Partikül plumbing'i (shader yedekleme
            // zinciri, saydamlık anahtarları, yumuşak daire dokusu) hava durumundan
            // paylaşılıyor.
            var smokeMaterial = ProceduralWeather.ParticleMaterial("mat_fx_smoke", "fx_smoke");

            foreach (var spec in wheelSpecs)
            {
                var colGo = new GameObject(spec.name + "_Collider");
                colGo.transform.SetParent(root.transform, false);
                colGo.transform.localPosition = spec.pos;
                var wc = colGo.AddComponent<WheelCollider>();
                wc.radius = preset.wheelRadius;
                wc.mass = 22f;
                wc.wheelDampingRate = 0.3f;
                wc.suspensionDistance = 0.22f;
                var spring = wc.suspensionSpring;
                spring.spring = preset.mass * 30f;
                spring.damper = preset.mass * 3.6f;
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;

                var fwdFriction = wc.forwardFriction;
                fwdFriction.stiffness = 2.0f;
                wc.forwardFriction = fwdFriction;
                var sideFriction = wc.sidewaysFriction;
                sideFriction.stiffness = spec.front ? 2.1f : 1.9f;
                wc.sidewaysFriction = sideFriction;

                var meshGo = new GameObject(spec.name + "_Mesh");
                meshGo.transform.SetParent(root.transform, false);
                meshGo.transform.localPosition = spec.pos;
                meshGo.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
                var wr = meshGo.AddComponent<MeshRenderer>();
                wr.sharedMaterials = new[] { tire };

                // Jant görseli ayrı child — WheelGlow buna emissive basacak
                var rimGo = new GameObject(spec.name + "_Rim");
                rimGo.transform.SetParent(meshGo.transform, false);
                var rimRenderer = rimGo.AddComponent<MeshRenderer>();
                rimGo.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
                rimRenderer.sharedMaterial = rim;
                rimGo.transform.localScale = Vector3.one * 0.99f;

                var glow = rimGo.AddComponent<WheelGlow>();
                glow.wheel = wc;
                // Rampayı BURADA atıyoruz. WheelGlow.Awake'teki
                // "glowColor.colorKeys.Length == 0" yedeği hiç tetiklenmiyor:
                // Unity serileştirilmiş bir Gradient alanını iki beyaz anahtarla
                // doldurup veriyor. Yani siyah→kırmızı→turuncu ısı rampası hiç
                // kurulmuyordu ve fren/drift parıltısı beyazımsı görünüyordu.
                var heat = new Gradient();
                heat.SetKeys(
                    new[] {
                        new GradientColorKey(Color.black, 0f),
                        new GradientColorKey(new Color(0.6f, 0.05f, 0f), 0.5f),
                        new GradientColorKey(new Color(1.5f, 0.3f, 0f), 0.85f),
                        new GradientColorKey(new Color(2.5f, 1.2f, 0.2f), 1f),
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                glow.glowColor = heat;

                // Drift dumanı + fren izi. DriftSmoke yazılmıştı ama hiçbir prefab'a
                // eklenmiyordu; drift modu olan bir oyunda kayma tamamen görsel
                // geri bildirimsizdi.
                //
                // Duman ve iz colGo'nun altında: WheelCollider'ın GameObject'i
                // tekerlekle birlikte DÖNMEZ (dönüş wc.steerAngle ile olur), mesh
                // ise döner. İz dönen mesh'e bağlansaydı spiral çizerdi.
                var smokeGo = new GameObject(spec.name + "_Smoke");
                smokeGo.transform.SetParent(colGo.transform, false);
                var smokePs = smokeGo.AddComponent<ParticleSystem>();
                smokePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var smokeMain = smokePs.main;
                smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
                smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
                smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                smokeMain.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.78f, 0.76f, 0.74f, 0.55f));
                smokeMain.gravityModifier = -0.05f;      // duman hafifçe yükselir
                smokeMain.maxParticles = 120;
                // playOnAwake AÇIK kalmalı: DriftSmoke yalnızca emission.rateOverTime
                // yazıyor, hiç Play() çağırmıyor. Duran bir sistemde oran yazmak
                // hiçbir şey yapmaz — sistem sürekli çalışır, kaymadığında oran 0
                // olduğu için partikül üretilmez, maliyeti de yok denecek kadar azdır.
                smokeMain.playOnAwake = true;
                // Dünya uzayı şart: araç ilerlerken duman lastiğe yapışmasın, geride kalsın.
                smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;

                // Emisyon oranını DriftSmoke her karede kaymaya göre yazıyor.
                var smokeEmission = smokePs.emission;
                smokeEmission.rateOverTime = 0f;

                var smokeShape = smokePs.shape;
                smokeShape.shapeType = ParticleSystemShapeType.Sphere;
                smokeShape.radius = 0.18f;

                var smokeSizeOverLife = smokePs.sizeOverLifetime;
                smokeSizeOverLife.enabled = true;
                smokeSizeOverLife.size = new ParticleSystem.MinMaxCurve(
                    1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

                var smokeRenderer = smokePs.GetComponent<ParticleSystemRenderer>();
                smokeRenderer.sharedMaterial = smokeMaterial;
                smokeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smokeRenderer.receiveShadows = false;
                smokeRenderer.sortingFudge = 8f;

                // Fren izi — yere değen noktada, tekerlek yarıçapı kadar aşağıda.
                var trailGo = new GameObject(spec.name + "_SkidTrail");
                trailGo.transform.SetParent(colGo.transform, false);
                trailGo.transform.localPosition = new Vector3(0f, -preset.wheelRadius + 0.02f, 0f);
                var trail = trailGo.AddComponent<TrailRenderer>();
                trail.time = 4.5f;
                trail.startWidth = preset.wheelRadius * 0.42f;
                trail.endWidth = preset.wheelRadius * 0.30f;
                trail.minVertexDistance = 0.12f;
                trail.autodestruct = false;
                trail.emitting = false;                  // DriftSmoke kaymaya göre açar
                trail.sharedMaterial = smokeMaterial;
                trail.startColor = new Color(0.05f, 0.05f, 0.05f, 0.55f);
                trail.endColor = new Color(0.05f, 0.05f, 0.05f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                var driftSmoke = colGo.AddComponent<DriftSmoke>();
                driftSmoke.wheel = wc;
                driftSmoke.smoke = smokePs;
                driftSmoke.skidTrail = trail;

                if (spec.front) { frontColliders.Add(wc); frontMeshes.Add(meshGo.transform); }
                else { rearColliders.Add(wc); rearMeshes.Add(meshGo.transform); }
                allColliders.Add(wc);
            }

            // Sürüş
            var controller = root.AddComponent<CarController>();
            controller.maxMotorTorque = preset.motorTorque;
            controller.topSpeedKmh = preset.topSpeed;
            controller.centerOfMassOffset = new Vector3(0f, -0.45f, bodyCenterZ * 0.15f);
            controller.axles = new[]
            {
                new CarController.AxleInfo
                {
                    leftWheel = frontColliders[0], rightWheel = frontColliders[1],
                    leftMesh = frontMeshes[0], rightMesh = frontMeshes[1],
                    steering = true, motor = false
                },
                new CarController.AxleInfo
                {
                    leftWheel = rearColliders[0], rightWheel = rearColliders[1],
                    leftMesh = rearMeshes[0], rightMesh = rearMeshes[1],
                    steering = false, motor = true
                },
            };

            // Işıklar
            var frontSection = preset.sections[^1];
            var rearSection = preset.sections[0];
            var headlights = new List<Light>();
            var taillights = new List<Light>();
            var headlightRenderers = new List<Renderer>();

            foreach (int side in new[] { -1, 1 })
            {
                var hl = new GameObject($"Headlight_{(side < 0 ? "L" : "R")}");
                hl.transform.SetParent(root.transform, false);
                hl.transform.localPosition = new Vector3(frontSection.halfWidth * 0.62f * side, frontSection.centerY + 0.02f, frontSection.z);
                var light = hl.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 45f; light.spotAngle = 55f; light.intensity = 3.2f;
                light.color = new Color(1f, 0.97f, 0.88f);
                light.enabled = false;
                headlights.Add(light);

                var tl = new GameObject($"Taillight_{(side < 0 ? "L" : "R")}");
                tl.transform.SetParent(root.transform, false);
                tl.transform.localPosition = new Vector3(rearSection.halfWidth * 0.64f * side, rearSection.centerY + 0.03f, rearSection.z);
                var tlight = tl.AddComponent<Light>();
                tlight.type = LightType.Point;
                tlight.range = 4f; tlight.intensity = 1.4f;
                tlight.color = new Color(1f, 0.12f, 0.08f);
                tlight.enabled = false;
                taillights.Add(tlight);
            }

            var headlightController = root.AddComponent<HeadlightController>();
            headlightController.headlights = headlights.ToArray();
            headlightController.tailLights = taillights.ToArray();
            headlightController.headlightGlow = bodyRenderer;

            var highBeam = root.AddComponent<HighBeamController>();
            highBeam.headlights = headlights.ToArray();

            // Sinyaller — gövde renderer'ını emissive hedefi olarak kullanır
            var signals = root.AddComponent<TurnSignals>();
            signals.leftLights = new[] { headlights[0], taillights[0] };
            signals.rightLights = new[] { headlights[1], taillights[1] };
            // leftEmissive / rightEmissive hiç atanmıyordu: sinyal yalnızca
            // Light bileşenlerini yakıp söndürüyor, gövdede görünür bir parlama
            // olmuyordu. Gündüz ışığında Light etkisi neredeyse görünmez.
            signals.leftEmissive = new[] { bodyRenderer };
            signals.rightEmissive = new[] { bodyRenderer };

            // Kamera bağlantı noktaları
            AddAnchor(root, "HoodCam", new Vector3(0f, frontSection.centerY + 0.42f, 0.55f));
            AddAnchor(root, "BumperCam", new Vector3(0f, frontSection.centerY, frontSection.z - 0.1f));
            AddAnchor(root, "InteriorCam", new Vector3(-0.34f, 1.02f, 0.05f));
            var overhead = AddAnchor(root, "OverheadAnchor", new Vector3(0f, 1.6f, 0f));

            // Ağ — ses/emote bileşenlerinden ÖNCE kurulmalı. HornController, AirHorn ve
            // benzeri MonoBehaviourPun türevlerinde [RequireComponent(typeof(PhotonView))]
            // var; PhotonView henüz yokken eklenirlerse Unity kendi PhotonView'ını üretir ve
            // aşağıdaki AddComponent ikinci bir PhotonView yaratırdı.
            var pv = root.AddComponent<Photon.Pun.PhotonView>();
            var sync = root.AddComponent<CarNetworkSync>();
            pv.ObservedComponents = new List<Component> { sync };
            pv.Synchronization = Photon.Pun.ViewSynchronization.UnreliableOnChange;

            // Ses
            var idleSource = root.AddComponent<AudioSource>();
            idleSource.loop = true; idleSource.spatialBlend = 1f; idleSource.volume = 0.55f;
            idleSource.rolloffMode = AudioRolloffMode.Linear; idleSource.maxDistance = 45f;

            var revSource = root.AddComponent<AudioSource>();
            revSource.loop = true; revSource.spatialBlend = 1f; revSource.volume = 0f;
            revSource.rolloffMode = AudioRolloffMode.Linear; revSource.maxDistance = 60f;

            var engine = root.AddComponent<EngineAudio>();
            engine.idleLoop = idleSource;
            engine.revLoop = revSource;

            // Prosedürel motor sesi — klip dosyası gerektirmez
            var synth = root.AddComponent<ProceduralEngineAudio>();
            synth.idleSource = idleSource;
            synth.revSource = revSource;

            var screechSource = root.AddComponent<AudioSource>();
            screechSource.loop = true; screechSource.spatialBlend = 1f; screechSource.playOnAwake = false;
            var screech = root.AddComponent<TireScreechAudio>();
            screech.wheels = allColliders.ToArray();
            screech.loop = screechSource;

            var hornSource = root.AddComponent<AudioSource>();
            hornSource.spatialBlend = 1f; hornSource.playOnAwake = false;
            var horn = root.AddComponent<Emote.HornController>();
            horn.horn = hornSource;

            // Nitro döngüsü ve çarpma sesi. Taban volume sıfır bırakılamaz: CarNitro
            // ve CarDamage bu kaynakları Awake'te AudioBus.RegisterSfx ile kaydeder ve
            // o andaki volume'u "taban seviye" kabul eder — 0 olursa sonsuza dek sessiz kalır.
            var nitroSource = root.AddComponent<AudioSource>();
            nitroSource.loop = true; nitroSource.spatialBlend = 1f; nitroSource.playOnAwake = false;
            nitroSource.volume = 0.5f;
            nitroSource.rolloffMode = AudioRolloffMode.Linear; nitroSource.maxDistance = 50f;

            var crashSource = root.AddComponent<AudioSource>();
            crashSource.spatialBlend = 1f; crashSource.playOnAwake = false;
            crashSource.volume = 0.8f;
            crashSource.rolloffMode = AudioRolloffMode.Linear; crashSource.maxDistance = 70f;

            // Sentezleyiciye tüm kaynakları bağla — bağlanmayan kaynak için klip
            // üretilmez, yani o ses tamamen sessiz kalır.
            synth.screechSource = screechSource;
            synth.hornSource = hornSource;
            synth.nitroSource = nitroSource;
            synth.crashSource = crashSource;

            // Emote ve ritmik korna — ikisi de MonoBehaviourPun, araçta PhotonView zaten var.
            // İçerik (ikon/nota klipleri) Editor'de doldurulur; boş listeyle RPC'ler sessizce geçer.
            var emotes = root.AddComponent<Emote.EmoteSystem>();
            emotes.overheadAnchor = overhead;
            emotes.audioSource = hornSource;
            // emotes listesi BOŞ bırakılıyordu: RPC_Play "emotes.Find(...)"
            // ile null bulup sessizce dönüyordu, yani emote sistemi zorlansa
            // bile hiçbir şey yapmazdı. emotePopupPrefab de yoktu, dolayısıyla
            // gösterilecek bir baloncuk da yoktu.
            emotes.emotePopupPrefab = EmotePopupPrefab();
            emotes.emotes = new System.Collections.Generic.List<Emote.EmoteSystem.EmoteEntry>
            {
                new() { id = "wave", icon = UiSprite("icon_emote") },
                new() { id = "gg",   icon = UiSprite("icon_trophy") },
                new() { id = "race", icon = UiSprite("icon_flag") },
            };

            var airHorn = root.AddComponent<Emote.AirHorn>();
            airHorn.source = hornSource;

            // Oyun bileşenleri
            var nitro = root.AddComponent<CarNitro>();
            nitro.nitroLoop = nitroSource;
            // exhaustFlames boş bir diziydi: StartBoost'taki foreach hiçbir şey
            // gezmiyor, NOS hız ve ses veriyor ama GÖRSELİ hiç yoktu.
            nitro.exhaustFlames = new[]
            {
                MakeExhaustFlame(root, "ExhaustFlame_L",
                    new Vector3(-rearSection.halfWidth * 0.64f, rearSection.centerY + 0.03f, rearSection.z - 0.06f)),
                MakeExhaustFlame(root, "ExhaustFlame_R",
                    new Vector3( rearSection.halfWidth * 0.64f, rearSection.centerY + 0.03f, rearSection.z - 0.06f)),
            };

            var damage = root.AddComponent<CarDamage>();
            damage.crashSfx = crashSource;
            // smoke hiç atanmıyordu: CarDamage'deki "if (smoke)" bloğu hiç
            // koşmuyor, hasar birikiyor ama araçtan duman çıkmıyordu.
            damage.smoke = MakeDamageSmoke(root, new Vector3(0f, frontSection.centerY + 0.30f, frontSection.z - 0.25f));

            // DriftScore hiçbir prefaba eklenmiyordu: DriftMode.Finish() hiçbir şey
            // bulamıyor, drift modu üç dakika sonunda sessizce hiçbir şey yapmıyordu.
            // Drift başarımı ve "en iyi drift" istatistiği de bu bileşene bağlı.
            root.AddComponent<Race.DriftScore>();

            root.AddComponent<CruiseControl>();
            root.AddComponent<GearBox>();
            root.AddComponent<FuelSystem>();
            // Takla atan, haritadan düşen veya yakıtı biten araç için tek
            // çıkış yolu — bu bileşen olmadan oyuncu odadan çıkmak zorunda.
            root.AddComponent<CarRescue>();
            root.AddComponent<Core.StatsTracker>();

            // Faz 1 sözleşmeleri. Üçü de veri taşıyıcı: telemetri okur,
            // otorite "bu araç benim mi" sorusunu yanıtlar, tablo istatistik
            // değiştiricilerini tutar. Nitro ve yakıt tüketimi tabloyu zaten
            // kullanıyor, o yüzden bu bileşenler olmadan araç eksik doğar.
            root.AddComponent<DreamCar.Car.VehicleStatSheet>();
            root.AddComponent<DreamCar.Car.VehicleTelemetry>();
            root.AddComponent<DreamCar.Car.VehicleAuthority>();

            var carPaint = root.AddComponent<CarPaint>();
            carPaint.paintRenderers = new[] { bodyRenderer };

            var hdr = root.AddComponent<CarPaintHDR>();
            hdr.paintRenderers = new[] { bodyRenderer };

            // Sürücü alanı burada ATANMAZ: Unity arayüz tipindeki alanları
            // serileştirmiyor, değer prefab'a kaydedilmezdi. InteriorCamera aracın
            // üzerinde durduğu için Awake'te kendisi buluyor.
            root.AddComponent<InteriorCamera>();

            // Plaka hiçbir prefaba eklenmiyordu ve plaka geometrisi de
            // üretilmiyordu — Dream Road'un imza özelliklerinden biri oyunda
            // hiç yoktu. (LicensePlate'in yazı tipi de gerçek harf çizmiyordu;
            // o ayrıca düzeltildi.)
            var plateMat = ProceduralTextures.CreatePlateMaterial();
            var plate = root.AddComponent<LicensePlate>();
            plate.plateRenderers = new[]
            {
                MakePlateQuad(root, "Plate_Front", plateMat,
                    new Vector3(0f, frontSection.centerY - 0.16f, frontSection.z + 0.03f), 0f),
                MakePlateQuad(root, "Plate_Rear", plateMat,
                    new Vector3(0f, rearSection.centerY - 0.14f, rearSection.z - 0.03f), 180f),
            };
        }

        // Plaka yüzeyi. Doku 512x128 (4:1), quad da o oranda.
        internal static Renderer MakePlateQuad(GameObject root, string name, Material mat,
                                      Vector3 localPosition, float yawDegrees)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            // Quad'ın normali +Z; ön plaka ileri, arka plaka geriye baksın.
            go.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            go.transform.localScale = new Vector3(0.52f, 0.13f, 1f);

            // CreatePrimitive collider de ekliyor: aracın çarpışma kutusuna
            // yapışık ikinci bir yüzey hem gereksiz hem fizik gürültüsü.
            var col = go.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        // NOS alev püskürtücüsü. CarNitro Play()/Stop() çağırdığı için
        // playOnAwake KAPALI (drift dumanının tersine — o sürekli çalışıp
        // oranı 0'da tutuyor).
        static ParticleSystem MakeExhaustFlame(GameObject root, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            // Alev geriye doğru püskürsün: aracın -Z'sine bak.
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.55f, 0.12f, 0.95f), new Color(0.45f, 0.75f, 1f, 0.95f));
            main.gravityModifier = 0f;
            main.maxParticles = 60;
            main.playOnAwake = false;
            // Yerel uzay: alev egzoza yapışık kalsın.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 90f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.05f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ProceduralWeather.ParticleMaterial("mat_fx_smoke", "fx_smoke");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // Hasar dumanı — kaputtan yükselir. CarDamage emission.rateOverTime'ı
        // sağlığa göre yazıp Play() çağırıyor, o yüzden playOnAwake kapalı.
        internal static ParticleSystem MakeDamageSmoke(GameObject root, Vector3 localPosition)
        {
            var go = new GameObject("DamageSmoke");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.18f, 0.19f, 0.75f), new Color(0.42f, 0.42f, 0.44f, 0.55f));
            main.gravityModifier = -0.12f;
            main.maxParticles = 80;
            main.playOnAwake = false;
            // Dünya uzayı: araç ilerlerken duman geride kalsın.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.16f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ProceduralWeather.ParticleMaterial("mat_fx_smoke", "fx_smoke");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // internal: RCCPCarConverter da aynı ikonları kullanıyor.
        internal static Sprite UiSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Generated/UI/{name}.png");

        // Araç üstünde beliren emote baloncuğu. Tek bir paylaşılan prefab —
        // her araç için yeniden üretmenin anlamı yok.
        internal static GameObject EmotePopupPrefab()
        {
            const string folder = "Assets/Generated/Prefabs";
            const string path = folder + "/EmotePopup.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var go = new GameObject("EmotePopup");
            var sr = go.AddComponent<SpriteRenderer>();
            // İkon RPC_Play tarafından atanıyor; burada yalnızca varsayılan.
            sr.sprite = UiSprite("icon_emote");
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            go.transform.localScale = Vector3.one * 1.4f;
            go.AddComponent<Effects.Billboard>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Transform AddAnchor(GameObject root, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        // --- Katalog ---
        [MenuItem("DreamCar/Procedural/Build Car Catalog")]
        public static void BuildCatalog()
        {
            EnsureFolders();
            const string catalogFolder = "Assets/Generated/Catalog";
            if (!Directory.Exists(catalogFolder)) Directory.CreateDirectory(catalogFolder);

            var catalog = ScriptableObject.CreateInstance<Economy.CarCatalog>();
            long[] prices = { 0, 25000, 85000, 60000, 48000 };

            for (int i = 0; i < Presets.Length; i++)
            {
                var preset = Presets[i];
                var def = ScriptableObject.CreateInstance<Economy.CarDefinition>();
                def.id = preset.id;
                def.displayName = preset.displayName;
                def.price = i < prices.Length ? prices[i] : 50000;
                def.resourcePrefabName = ToPrefabName(preset.id);
                def.previewPrefabName = PreviewPrefabName(def.id);
                def.topSpeedKmh = preset.topSpeed;
                def.maxMotorTorque = preset.motorTorque;
                def.speedStat = Mathf.RoundToInt(Mathf.InverseLerp(150f, 240f, preset.topSpeed) * 10f);
                def.accelerationStat = Mathf.RoundToInt(Mathf.InverseLerp(1100f, 2000f, preset.motorTorque) * 10f);
                def.handlingStat = Mathf.RoundToInt(Mathf.InverseLerp(1900f, 1050f, preset.mass) * 10f);

                AssetDatabase.CreateAsset(def, $"{catalogFolder}/{preset.id.Replace('.', '_')}.asset");
                catalog.cars.Add(def);
            }

            // DIŞARIDAN eklenen araçları koru. Bu metot kataloğu SIFIRDAN kuruyor:
            // Presets dizisini gezip CarCatalog.asset'i baştan yazıyor. Dönüştürülmüş
            // bir RCCP aracı eklendikten sonra çalıştırılan her BUILD EVERYTHING onu
            // listeden silerdi — varlık diskte kalır ama mağazada görünmez olurdu.
            // Klasördeki id'si Presets'te olmayan her tanım geri ekleniyor.
            var presetIds = new HashSet<string>();
            foreach (var p in Presets) presetIds.Add(p.id);

            foreach (var guid in AssetDatabase.FindAssets("t:CarDefinition", new[] { catalogFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<Economy.CarDefinition>(path);
                if (def == null || presetIds.Contains(def.id)) continue;
                if (!catalog.cars.Contains(def))
                {
                    catalog.cars.Add(def);
                    Debug.Log($"[Procedural] Katalogda korundu (dışarıdan eklenmiş): {def.displayName}");
                }
            }

            AssetDatabase.CreateAsset(catalog, $"{catalogFolder}/CarCatalog.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Procedural] Katalog: {catalogFolder}/CarCatalog.asset ({catalog.cars.Count} araç)");
        }

        public static Economy.CarCatalog LoadCatalog() =>
            AssetDatabase.LoadAssetAtPath<Economy.CarCatalog>(
                "Assets/Generated/Catalog/CarCatalog.asset");

        static void SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(mesh, path);
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Assets/Generated", MeshFolder, MaterialFolder, PrefabFolder })
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
#endif
