#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DreamCar.Race;
using DreamCar.Traffic;
using DreamCar.Vehicle;

namespace DreamCar.EditorTools.Procedural
{
    // Izgara planlı bir şehir üretir: yollar, kaldırımlar, farklı yükseklikte binalar,
    // sokak lambaları, trafik waypoint zincirleri, yarış checkpoint'leri, spawn noktaları
    // ve benzin istasyonu. Hepsi tek mesh'lerde birleştirilir (draw call az olsun).
    //
    // Menü: DreamCar → Procedural → Generate City
    public static class ProceduralCityGenerator
    {
        const string MeshFolder = "Assets/Generated/Meshes";

        // --- Ayarlar ---
        const int BlocksX = 6;              // ızgara blok sayısı
        const int BlocksZ = 6;
        const float BlockSize = 60f;        // blok kenarı (bina alanı)
        const float RoadWidth = 14f;        // yol genişliği
        const float SidewalkWidth = 2.4f;
        const float SidewalkHeight = 0.16f;
        const int Seed = 20240815;

        static float Pitch => BlockSize + RoadWidth;
        static float CityWidth => BlocksX * Pitch;
        static float CityDepth => BlocksZ * Pitch;

        [MenuItem("DreamCar/Procedural/Generate City")]
        public static void GenerateCity()
        {
            EnsureFolders();
            ProceduralTextures.GenerateAll();

            var root = GameObject.Find("~City");
            if (root != null) Object.DestroyImmediate(root);
            root = new GameObject("~City");

            var rng = new System.Random(Seed);

            BuildGround(root);
            BuildRoads(root);
            BuildSidewalks(root);
            var buildingBounds = BuildBuildings(root, rng);
            BuildStreetLights(root);
            var waypointLoop = BuildTrafficWaypoints(root);
            BuildCheckpoints(root, waypointLoop);
            BuildSpawnPoints(root);
            BuildGasStation(root);
            WireTrafficSpawner(root, waypointLoop);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Procedural] Şehir üretildi: {BlocksX}x{BlocksZ} blok, {buildingBounds} bina.");
            EditorUtility.DisplayDialog("DreamCar",
                $"Şehir hazır!\n\n{BlocksX}x{BlocksZ} blok, {buildingBounds} bina,\n" +
                $"{waypointLoop.Count} trafik waypoint'i, 8 checkpoint.\n\n" +
                "Sahneyi kaydetmeyi unutma (Ctrl+S).", "Tamam");
        }

        // ---------------------------------------------------------- Zemin
        static void BuildGround(GameObject root)
        {
            var mb = new MeshBuilder();
            float halfW = CityWidth * 0.75f;
            float halfD = CityDepth * 0.75f;

            mb.AddFlatQuad(
                new Vector3(-halfW, -0.05f, -halfD),
                new Vector3( halfW, -0.05f, -halfD),
                new Vector3( halfW, -0.05f,  halfD),
                new Vector3(-halfW, -0.05f,  halfD));

            var go = CreateMeshObject(root, "Ground", mb.ToMesh("city_ground"),
                ProceduralTextures.CreateTexturedMaterial("mat_grass", "grass", 0f, 0.1f, new Vector2(60f, 60f)));
            go.AddComponent<MeshCollider>();
        }

        // ---------------------------------------------------------- Yollar
        static void BuildRoads(GameObject root)
        {
            var mb = new MeshBuilder();
            float half = RoadWidth * 0.5f;
            float extentX = CityWidth * 0.5f;
            float extentZ = CityDepth * 0.5f;

            // X yönünde uzanan yollar (Z kesitlerinde)
            for (int i = 0; i <= BlocksZ; i++)
            {
                float z = -extentZ + i * Pitch;
                mb.AddFlatQuad(
                    new Vector3(-extentX, 0f, z - half),
                    new Vector3( extentX, 0f, z - half),
                    new Vector3( extentX, 0f, z + half),
                    new Vector3(-extentX, 0f, z + half));
            }

            // Z yönünde uzanan yollar (X kesitlerinde)
            for (int i = 0; i <= BlocksX; i++)
            {
                float x = -extentX + i * Pitch;
                mb.AddFlatQuad(
                    new Vector3(x - half, 0.001f, -extentZ),
                    new Vector3(x + half, 0.001f, -extentZ),
                    new Vector3(x + half, 0.001f,  extentZ),
                    new Vector3(x - half, 0.001f,  extentZ));
            }

            var go = CreateMeshObject(root, "Roads", mb.ToMesh("city_roads"),
                ProceduralTextures.CreateTexturedMaterial("mat_asphalt", "asphalt", 0f, 0.25f, new Vector2(8f, 40f)));
            go.AddComponent<MeshCollider>();
        }

        // ---------------------------------------------------------- Kaldırımlar
        static void BuildSidewalks(GameObject root)
        {
            var mb = new MeshBuilder();
            float extentX = CityWidth * 0.5f;
            float extentZ = CityDepth * 0.5f;

            for (int bx = 0; bx < BlocksX; bx++)
            for (int bz = 0; bz < BlocksZ; bz++)
            {
                float cx = -extentX + RoadWidth * 0.5f + bx * Pitch + BlockSize * 0.5f;
                float cz = -extentZ + RoadWidth * 0.5f + bz * Pitch + BlockSize * 0.5f;

                float outer = BlockSize * 0.5f + SidewalkWidth;
                float inner = BlockSize * 0.5f;

                // Blok çevresinde dikdörtgen halka — dört şerit
                AddSlab(mb, new Vector3(cx, SidewalkHeight, cz + inner + SidewalkWidth * 0.5f),
                        new Vector2(outer * 2f, SidewalkWidth));
                AddSlab(mb, new Vector3(cx, SidewalkHeight, cz - inner - SidewalkWidth * 0.5f),
                        new Vector2(outer * 2f, SidewalkWidth));
                AddSlab(mb, new Vector3(cx - inner - SidewalkWidth * 0.5f, SidewalkHeight, cz),
                        new Vector2(SidewalkWidth, inner * 2f));
                AddSlab(mb, new Vector3(cx + inner + SidewalkWidth * 0.5f, SidewalkHeight, cz),
                        new Vector2(SidewalkWidth, inner * 2f));
            }

            var go = CreateMeshObject(root, "Sidewalks", mb.ToMesh("city_sidewalks"),
                ProceduralTextures.CreateTexturedMaterial("mat_sidewalk", "sidewalk", 0f, 0.2f, new Vector2(6f, 6f)));
            go.AddComponent<MeshCollider>();
        }

        static void AddSlab(MeshBuilder mb, Vector3 center, Vector2 size)
        {
            // Üst yüzey + dış kenar duvarı (araç takılmasın diye alçak)
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            mb.AddFlatQuad(
                new Vector3(center.x - hx, center.y, center.z - hz),
                new Vector3(center.x + hx, center.y, center.z - hz),
                new Vector3(center.x + hx, center.y, center.z + hz),
                new Vector3(center.x - hx, center.y, center.z + hz));
        }

        // ---------------------------------------------------------- Binalar
        static int BuildBuildings(GameObject root, System.Random rng)
        {
            var mb = new MeshBuilder();
            float extentX = CityWidth * 0.5f;
            float extentZ = CityDepth * 0.5f;
            int count = 0;

            for (int bx = 0; bx < BlocksX; bx++)
            for (int bz = 0; bz < BlocksZ; bz++)
            {
                float blockCx = -extentX + RoadWidth * 0.5f + bx * Pitch + BlockSize * 0.5f;
                float blockCz = -extentZ + RoadWidth * 0.5f + bz * Pitch + BlockSize * 0.5f;

                // Merkez bloğu meydan olarak boş bırak — spawn ve buluşma alanı.
                bool isCentralPlaza = bx == BlocksX / 2 && bz == BlocksZ / 2;
                if (isCentralPlaza) continue;

                // Her blokta 2x2 bina parseli
                for (int px = 0; px < 2; px++)
                for (int pz = 0; pz < 2; pz++)
                {
                    if (rng.NextDouble() < 0.12) continue; // seyrek boşluk

                    float parcel = BlockSize * 0.5f;
                    float cx = blockCx + (px - 0.5f) * parcel;
                    float cz = blockCz + (pz - 0.5f) * parcel;

                    float w = parcel * (0.62f + (float)rng.NextDouble() * 0.24f);
                    float d = parcel * (0.62f + (float)rng.NextDouble() * 0.24f);

                    // Merkeze yakın binalar daha yüksek — şehir silueti oluşsun.
                    float distFromCenter = new Vector2(blockCx, blockCz).magnitude / (CityWidth * 0.5f);
                    float heightBias = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(distFromCenter));
                    float h = Mathf.Lerp(10f, 62f, (float)rng.NextDouble() * heightBias) + 6f;

                    mb.AddBox(new Vector3(cx, h * 0.5f, cz), new Vector3(w, h, d));

                    // Çatı katı — silueti kırmak için küçük ek blok
                    if (rng.NextDouble() > 0.55)
                    {
                        float ch = 3f + (float)rng.NextDouble() * 5f;
                        mb.AddBox(new Vector3(cx, h + ch * 0.5f, cz), new Vector3(w * 0.45f, ch, d * 0.45f));
                    }
                    count++;
                }
            }

            var go = CreateMeshObject(root, "Buildings", mb.ToMesh("city_buildings"),
                ProceduralTextures.CreateTexturedMaterial("mat_facade", "facade_day", 0.05f, 0.35f, new Vector2(2f, 6f)));
            go.AddComponent<MeshCollider>();
            return count;
        }

        // ---------------------------------------------------------- Sokak lambaları
        static void BuildStreetLights(GameObject root)
        {
            var parent = new GameObject("StreetLights");
            parent.transform.SetParent(root.transform, false);

            var poleMat = ProceduralTextures.CreateRimMaterial();
            var mb = new MeshBuilder();
            float extentX = CityWidth * 0.5f;
            float extentZ = CityDepth * 0.5f;

            int lightCount = 0;
            for (int i = 0; i <= BlocksZ; i++)
            for (int bx = 0; bx < BlocksX; bx++)
            {
                float z = -extentZ + i * Pitch;
                float x = -extentX + RoadWidth * 0.5f + bx * Pitch + BlockSize * 0.5f;

                // Direk gövdesi tek mesh'te
                mb.AddBox(new Vector3(x, 4f, z + RoadWidth * 0.5f + 0.4f), new Vector3(0.22f, 8f, 0.22f));
                mb.AddBox(new Vector3(x, 7.9f, z + RoadWidth * 0.5f - 0.9f), new Vector3(0.18f, 0.18f, 2.8f));

                // Işık — sadece her 2 direkte bir (performans)
                if (lightCount % 2 == 0)
                {
                    var lampGo = new GameObject($"Lamp_{bx}_{i}");
                    lampGo.transform.SetParent(parent.transform, false);
                    lampGo.transform.position = new Vector3(x, 7.6f, z + RoadWidth * 0.5f - 2.2f);
                    var light = lampGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 22f;
                    light.intensity = 2.2f;
                    light.color = new Color(1f, 0.92f, 0.72f);
                    light.shadows = LightShadows.None;
                }
                lightCount++;
            }

            CreateMeshObject(parent, "Poles", mb.ToMesh("city_poles"), poleMat);
        }

        // ---------------------------------------------------------- Trafik waypoint'leri
        // Şehir çevresini dolaşan kapalı bir halka üretir — trafik ve yarış bunu kullanır.
        static List<Transform> BuildTrafficWaypoints(GameObject root)
        {
            var parent = new GameObject("TrafficWaypoints");
            parent.transform.SetParent(root.transform, false);

            var points = new List<Transform>();
            float extentX = CityWidth * 0.5f;
            float extentZ = CityDepth * 0.5f;

            // İç halka: ikinci sıradaki yolları takip eder.
            float ringX = extentX - Pitch;
            float ringZ = extentZ - Pitch;
            float laneOffset = RoadWidth * 0.25f; // sağ şerit

            var corners = new[]
            {
                new Vector2(-ringX,  ringZ),
                new Vector2( ringX,  ringZ),
                new Vector2( ringX, -ringZ),
                new Vector2(-ringX, -ringZ),
            };

            const int perEdge = 6;
            int index = 0;
            for (int c = 0; c < corners.Length; c++)
            {
                Vector2 from = corners[c];
                Vector2 to = corners[(c + 1) % corners.Length];

                for (int s = 0; s < perEdge; s++)
                {
                    float t = (float)s / perEdge;
                    Vector2 p = Vector2.Lerp(from, to, t);

                    // Şerit ofseti — yolun sağ tarafında kal
                    Vector2 dir = (to - from).normalized;
                    Vector2 right = new(dir.y, -dir.x);
                    p += right * laneOffset;

                    var go = new GameObject($"WP_{index:D2}");
                    go.transform.SetParent(parent.transform, false);
                    go.transform.position = new Vector3(p.x, 0.2f, p.y);
                    go.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
                    points.Add(go.transform);
                    index++;
                }
            }

            return points;
        }

        // ---------------------------------------------------------- Checkpoint'ler
        static void BuildCheckpoints(GameObject root, List<Transform> loop)
        {
            var parent = new GameObject("Checkpoints");
            parent.transform.SetParent(root.transform, false);

            const int checkpointCount = 8;
            int stride = Mathf.Max(1, loop.Count / checkpointCount);

            for (int i = 0; i < checkpointCount; i++)
            {
                var anchor = loop[(i * stride) % loop.Count];

                var go = new GameObject($"Checkpoint_{i}");
                go.transform.SetParent(parent.transform, false);
                go.transform.SetPositionAndRotation(
                    anchor.position + Vector3.up * 2f,
                    anchor.rotation);

                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(RoadWidth, 6f, 2f);

                var cp = go.AddComponent<Checkpoint>();
                cp.index = i;
                cp.isFinishLine = i == 0;
            }
        }

        // ---------------------------------------------------------- Spawn noktaları
        static void BuildSpawnPoints(GameObject root)
        {
            var parent = new GameObject("SpawnPoints");
            parent.transform.SetParent(root.transform, false);

            // Merkez meydanda ızgara dizilimi
            const int rows = 4, cols = 4;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var go = new GameObject($"SpawnPoint_{r * cols + c:D2}");
                go.transform.SetParent(parent.transform, false);
                go.transform.position = new Vector3(
                    (c - (cols - 1) * 0.5f) * 4.5f,
                    0.6f,
                    (r - (rows - 1) * 0.5f) * 8f);
                go.transform.rotation = Quaternion.identity;
            }
        }

        // ---------------------------------------------------------- Benzin istasyonu
        static void BuildGasStation(GameObject root)
        {
            var station = new GameObject("GasStation");
            station.transform.SetParent(root.transform, false);
            station.transform.position = new Vector3(Pitch * 1.5f, 0f, 0f);

            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(18f, 0.16f, 12f));   // platform
            mb.AddBox(new Vector3(0f, 5.2f, 0f), new Vector3(18f, 0.5f, 12f));      // saçak
            mb.AddBox(new Vector3(-7.5f, 2.6f, -4.5f), new Vector3(0.5f, 5.2f, 0.5f));
            mb.AddBox(new Vector3( 7.5f, 2.6f, -4.5f), new Vector3(0.5f, 5.2f, 0.5f));
            mb.AddBox(new Vector3(-7.5f, 2.6f,  4.5f), new Vector3(0.5f, 5.2f, 0.5f));
            mb.AddBox(new Vector3( 7.5f, 2.6f,  4.5f), new Vector3(0.5f, 5.2f, 0.5f));
            mb.AddBox(new Vector3(-2.5f, 1.0f, 0f), new Vector3(1.2f, 2.0f, 2.4f)); // pompa
            mb.AddBox(new Vector3( 2.5f, 1.0f, 0f), new Vector3(1.2f, 2.0f, 2.4f));

            var go = CreateMeshObject(station, "Structure", mb.ToMesh("gas_station"),
                ProceduralTextures.CreateTexturedMaterial("mat_station", "sidewalk", 0.1f, 0.4f, new Vector2(3f, 3f)));
            go.AddComponent<MeshCollider>();

            // Dolum tetikleyicisi
            var trigger = new GameObject("RefuelTrigger");
            trigger.transform.SetParent(station.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(14f, 3f, 9f);
            trigger.AddComponent<RefuelStation>();

            // Aydınlatma
            var lightGo = new GameObject("StationLight");
            lightGo.transform.SetParent(station.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 4.8f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 24f;
            light.intensity = 3f;
            light.color = new Color(0.9f, 0.96f, 1f);
        }

        // ---------------------------------------------------------- Trafik spawner
        static void WireTrafficSpawner(GameObject root, List<Transform> loop)
        {
            var go = new GameObject("TrafficSpawner");
            go.transform.SetParent(root.transform, false);
            var spawner = go.AddComponent<TrafficSpawner>();

            // Halkayı iki şeride böl — karşılıklı akış hissi
            int half = loop.Count / 2;
            var laneA = loop.GetRange(0, half).ToArray();
            var laneB = loop.GetRange(half, loop.Count - half).ToArray();

            spawner.lanes = new[]
            {
                new TrafficSpawner.Lane { waypoints = laneA, spawnIntervalSeconds = 7f },
                new TrafficSpawner.Lane { waypoints = laneB, spawnIntervalSeconds = 9f },
            };
            spawner.maxAlive = 14;
            spawner.despawnDistance = 160f;

            // Trafik araçları: üretilmiş prefab'lardan sedan ve hatchback
            var prefabs = new List<GameObject>();
            foreach (var id in new[] { "Car_sedan", "Car_hatchback", "Car_suv" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/{id}.prefab");
                if (prefab != null) prefabs.Add(prefab);
            }
            spawner.trafficCarPrefabs = prefabs.ToArray();

            if (prefabs.Count == 0)
                Debug.LogWarning("[Procedural] Trafik prefab'ları bulunamadı — önce 'Generate All Cars' çalıştır.");
        }

        // ---------------------------------------------------------- Yardımcılar
        // ProceduralGarage da bunu kullanıyor: mesh'i varlık olarak kaydedip
        // nesneyi kurma mantığı (var olan mesh'i CopySerialized ile tazeleme
        // dahil) iki yerde ayrı yazılırsa yeniden üretimde ayrışır.
        internal static GameObject CreateMeshObject(GameObject parent, string name, Mesh mesh, Material material)
        {
            SaveMesh(mesh, mesh.name);

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void SaveMesh(Mesh mesh, string name)
        {
            EnsureFolders();
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
            foreach (var folder in new[] { "Assets/Generated", MeshFolder })
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
#endif
