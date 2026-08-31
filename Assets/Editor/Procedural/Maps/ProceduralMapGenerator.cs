#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DreamCar.Race;
using DreamCar.Traffic;
using DreamCar.Vehicle;
using DreamCar.Environment;
using DreamCar.Network;
using DreamCar.Game;
using Kind = DreamCar.EditorTools.Procedural.Maps.MapArchetype.PropKind;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Bir MapArchetype'tan tam oynanabilir sahne üretir:
    // yol + arazi + proplar + checkpoint + trafik + spawn + ışıklandırma.
    //
    // Menü: DreamCar → Maps → …
    public static class ProceduralMapGenerator
    {
        const string MeshFolder = "Assets/Generated/Maps";
        const string SceneFolder = "Assets/Scenes/Maps";

        [MenuItem("DreamCar/Maps/Generate ALL Maps")]
        public static void GenerateAll()
        {
            var archetypes = MapArchetype.All();
            if (!EditorUtility.DisplayDialog("DreamCar — Haritalar",
                    $"{archetypes.Length} harita üretilecek:\n\n" +
                    string.Join("\n", System.Array.ConvertAll(archetypes, a => "• " + a.displayName)) +
                    "\n\nHer biri ayrı sahne olarak kaydedilecek.\nBirkaç dakika sürebilir.",
                    "Üret", "İptal"))
                return;

            try
            {
                for (int i = 0; i < archetypes.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Haritalar üretiliyor",
                        archetypes[i].displayName, (float)i / archetypes.Length);
                    Generate(archetypes[i], saveScene: true);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AddMapsToBuildSettings();
            BuildMapCatalog();

            EditorUtility.DisplayDialog("DreamCar",
                $"{archetypes.Length} harita hazır.\n\n" +
                $"Sahneler: {SceneFolder}\n" +
                "Build Settings güncellendi.\n" +
                "MapCatalog oluşturuldu.", "Tamam");
        }

        [MenuItem("DreamCar/Maps/Generate Single Map (aktif sahneye)")]
        public static void GenerateSingleIntoActiveScene()
        {
            // İlk arketipi aktif sahneye kurar — deneme/ayar için.
            Generate(MapArchetype.Track(), saveScene: false);
        }

        // ================================================================
        public static void Generate(MapArchetype arch, bool saveScene)
        {
            EnsureFolders();
            PropMeshLibrary.ClearCache();

            if (saveScene)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var existing = GameObject.Find("~Map");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("~Map");
            var rng = new System.Random(arch.terrain.seed);

            // --- 1. Yol hattı ---
            var spline = BuildSpline(arch);
            var roadResult = RoadMeshBuilder.Build(spline, arch.road, sampleSpacing: 5f);
            if (roadResult.samples.Count < 4)
            {
                Debug.LogError($"[Map] {arch.displayName}: yol örneklenemedi.");
                return;
            }

            // --- 2. Arazi (yola uyar) ---
            var terrainMesh = TerrainMeshBuilder.Build(arch.terrain, roadResult.samples, out var heights);
            var terrainGo = CreateMeshObject(root, "Terrain", terrainMesh, VertexColorMaterial("mat_terrain"), arch.id);
            terrainGo.AddComponent<MeshCollider>();

            // --- 3. Yol yüzeyleri ---
            BuildRoadObjects(root, roadResult, arch);

            // --- 4. Proplar ---
            int propCount = ScatterProps(root, arch, roadResult.samples, heights, rng);

            // --- 5. Oynanış öğeleri ---
            BuildCheckpoints(root, roadResult.samples, arch);
            BuildSpawnPoints(root, roadResult.samples);
            var waypoints = BuildTrafficWaypoints(root, roadResult.samples, arch);
            WireTrafficSpawner(root, waypoints);
            BuildRefuelStation(root, roadResult.samples, heights, arch);

            // --- 6. Ortam ---
            SetupEnvironment(root, arch);

            // --- 7. Yönetim bileşenleri ---
            SetupSceneManagers(root, arch);

            Debug.Log($"[Map] {arch.displayName}: {roadResult.samples.Count} yol örneği, {propCount} prop.");

            if (saveScene)
            {
                string path = $"{SceneFolder}/{SceneNameFor(arch)}.unity";
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
                AssetDatabase.SaveAssets();
            }
        }

        public static string SceneNameFor(MapArchetype arch) =>
            "Map_" + arch.id.Replace("map.", "");

        // ---------------------------------------------------------- Yol
        static RoadSpline BuildSpline(MapArchetype arch) => arch.layout switch
        {
            MapArchetype.RoadLayout.Highway => RoadSpline.Highway(
                new Vector3(0f, 0f, -arch.roadExtent * 0.5f), arch.roadExtent, 60,
                curviness: 180f, seed: arch.terrain.seed, arch.roadHeightAmplitude),

            MapArchetype.RoadLayout.Winding => RoadSpline.Winding(
                Vector3.zero, arch.roadExtent, 20, arch.terrain.seed, arch.roadHeightAmplitude),

            _ => RoadSpline.Circuit(
                Vector3.zero, arch.roadExtent, arch.roadCorners,
                arch.roadIrregularity, arch.terrain.seed, arch.roadHeightAmplitude),
        };

        static void BuildRoadObjects(GameObject root, RoadMeshBuilder.Result r, MapArchetype arch)
        {
            var roadGo = CreateMeshObject(root, "Road", r.road,
                TexturedMaterial("mat_road_asphalt", "asphalt", 0f, 0.28f, new Vector2(1f, 1f)), arch.id);
            roadGo.AddComponent<MeshCollider>();

            if (r.shoulders != null)
            {
                var shoulderGo = CreateMeshObject(root, "Shoulders", r.shoulders,
                    TexturedMaterial("mat_road_shoulder", "sidewalk", 0f, 0.2f, new Vector2(2f, 2f)), arch.id);
                shoulderGo.AddComponent<MeshCollider>();
            }

            if (r.guardrails != null)
            {
                var railGo = CreateMeshObject(root, "Guardrails", r.guardrails,
                    SolidMaterial("mat_guardrail", new Color(0.72f, 0.74f, 0.76f), metallic: 0.7f, smoothness: 0.55f),
                    arch.id);
                railGo.AddComponent<MeshCollider>();
            }

            if (r.centerLine != null)
                CreateMeshObject(root, "CenterLine", r.centerLine,
                    SolidMaterial("mat_road_line", new Color(0.92f, 0.90f, 0.82f), 0f, 0.1f), arch.id);
        }

        // ---------------------------------------------------------- Proplar
        static int ScatterProps(GameObject root, MapArchetype arch,
                                List<RoadSpline.Sample> samples, float[,] heights, System.Random rng)
        {
            if (arch.props == null || arch.props.Length == 0) return 0;

            var propRoot = new GameObject("Props");
            propRoot.transform.SetParent(root.transform, false);

            var propMaterial = VertexColorMaterial("mat_props");
            int placed = 0;

            foreach (var rule in arch.props)
            {
                var mesh = PropMeshLibrary.Get(rule.kind);
                var kindRoot = new GameObject(rule.kind.ToString());
                kindRoot.transform.SetParent(propRoot.transform, false);

                // Bariyer ve lamba yol kenarına dizilir, diğerleri rastgele saçılır.
                if (rule.kind is Kind.Barrier or Kind.Lamp)
                {
                    placed += PlaceAlongRoad(kindRoot, mesh, propMaterial, rule, samples, arch);
                    continue;
                }

                int attempts = rule.count * 6;
                int spawned = 0;

                for (int i = 0; i < attempts && spawned < rule.count; i++)
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * arch.terrain.extent * 0.95f;
                    float z = ((float)rng.NextDouble() * 2f - 1f) * arch.terrain.extent * 0.95f;

                    float roadDistance = DistanceToRoad(samples, x, z);
                    if (roadDistance < rule.minRoadDistance || roadDistance > rule.maxRoadDistance) continue;

                    float y = TerrainMeshBuilder.SampleHeight(heights, arch.terrain, x, z);
                    if (y < rule.minHeight || y > rule.maxHeight) continue;

                    // Eğim kontrolü — dik yamaca ağaç dikme
                    if (Slope(heights, arch.terrain, x, z) > rule.maxSlope) continue;

                    var go = new GameObject($"{rule.kind}_{spawned}");
                    go.transform.SetParent(kindRoot.transform, false);
                    go.transform.position = new Vector3(x, y, z);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                    float scale = Mathf.Lerp(rule.minScale, rule.maxScale, (float)rng.NextDouble());
                    go.transform.localScale = Vector3.one * scale;

                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = propMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    // Büyük yapılar çarpışsın, bitkiler çarpışmasın (performans)
                    if (rule.kind is Kind.Building or Kind.Container or Kind.Crane
                                  or Kind.House or Kind.Barn or Kind.Rock)
                        go.AddComponent<BoxCollider>();

                    spawned++;
                    placed++;
                }
            }

            return placed;
        }

        static int PlaceAlongRoad(GameObject parent, Mesh mesh, Material material,
                                  MapArchetype.PropRule rule, List<RoadSpline.Sample> samples,
                                  MapArchetype arch)
        {
            int stride = Mathf.Max(1, samples.Count / Mathf.Max(1, rule.count));
            float lateral = (rule.minRoadDistance + rule.maxRoadDistance) * 0.5f;
            int placed = 0;

            for (int i = 0; i < samples.Count; i += stride)
            {
                var sample = samples[i];
                // Lambalar tek tarafta, bariyerler iki tarafta
                int sides = rule.kind == Kind.Barrier ? 2 : 1;

                for (int s = 0; s < sides; s++)
                {
                    float side = (sides == 1) ? 1f : (s == 0 ? -1f : 1f);
                    var go = new GameObject($"{rule.kind}_{placed}");
                    go.transform.SetParent(parent.transform, false);
                    go.transform.position = sample.position + sample.right * lateral * side;
                    go.transform.rotation = Quaternion.LookRotation(sample.forward, Vector3.up);

                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = material;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    if (rule.kind == Kind.Barrier) go.AddComponent<BoxCollider>();
                    placed++;
                }
            }
            return placed;
        }

        static float DistanceToRoad(List<RoadSpline.Sample> samples, float x, float z)
        {
            float best = float.MaxValue;
            // Her örneği taramak pahalı; 4'er atlayarak yaklaşık değer yeterli
            for (int i = 0; i < samples.Count; i += 4)
            {
                float dx = samples[i].position.x - x;
                float dz = samples[i].position.z - z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) best = d2;
            }
            return Mathf.Sqrt(best);
        }

        static float Slope(float[,] heights, TerrainMeshBuilder.Settings s, float x, float z)
        {
            const float delta = 4f;
            float hc = TerrainMeshBuilder.SampleHeight(heights, s, x, z);
            float hx = TerrainMeshBuilder.SampleHeight(heights, s, x + delta, z);
            float hz = TerrainMeshBuilder.SampleHeight(heights, s, x, z + delta);
            float gradient = Mathf.Max(Mathf.Abs(hx - hc), Mathf.Abs(hz - hc)) / delta;
            return Mathf.Clamp01(gradient);
        }

        // ---------------------------------------------------------- Oynanış
        static void BuildCheckpoints(GameObject root, List<RoadSpline.Sample> samples, MapArchetype arch)
        {
            var parent = new GameObject("Checkpoints");
            parent.transform.SetParent(root.transform, false);

            const int count = 10;
            int stride = Mathf.Max(1, samples.Count / count);

            for (int i = 0; i < count; i++)
            {
                var sample = samples[(i * stride) % samples.Count];
                var go = new GameObject($"Checkpoint_{i}");
                go.transform.SetParent(parent.transform, false);
                go.transform.SetPositionAndRotation(
                    sample.position + Vector3.up * 3f,
                    Quaternion.LookRotation(sample.forward, Vector3.up));

                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(arch.road.roadWidth + 6f, 8f, 3f);

                var cp = go.AddComponent<Checkpoint>();
                cp.index = i;
                cp.isFinishLine = i == 0;
            }
        }

        static void BuildSpawnPoints(GameObject root, List<RoadSpline.Sample> samples)
        {
            var parent = new GameObject("SpawnPoints");
            parent.transform.SetParent(root.transform, false);

            // Başlangıç çizgisinin gerisinde 4x4 ızgara
            var start = samples[0];
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                var go = new GameObject($"SpawnPoint_{row * 4 + col:D2}");
                go.transform.SetParent(parent.transform, false);
                go.transform.position = start.position
                    - start.forward * (8f + row * 7f)
                    + start.right * ((col - 1.5f) * 3.6f)
                    + Vector3.up * 1.2f;
                go.transform.rotation = Quaternion.LookRotation(start.forward, Vector3.up);
            }
        }

        static List<Transform> BuildTrafficWaypoints(GameObject root, List<RoadSpline.Sample> samples,
                                                     MapArchetype arch)
        {
            var parent = new GameObject("TrafficWaypoints");
            parent.transform.SetParent(root.transform, false);

            var list = new List<Transform>();
            int stride = Mathf.Max(1, samples.Count / 40);
            float laneOffset = arch.road.roadWidth * 0.25f;

            for (int i = 0; i < samples.Count; i += stride)
            {
                var sample = samples[i];
                var go = new GameObject($"WP_{list.Count:D2}");
                go.transform.SetParent(parent.transform, false);
                go.transform.position = sample.position + sample.right * laneOffset + Vector3.up * 0.3f;
                go.transform.rotation = Quaternion.LookRotation(sample.forward, Vector3.up);
                list.Add(go.transform);
            }
            return list;
        }

        static void WireTrafficSpawner(GameObject root, List<Transform> waypoints)
        {
            if (waypoints.Count < 4) return;

            var go = new GameObject("TrafficSpawner");
            go.transform.SetParent(root.transform, false);
            var spawner = go.AddComponent<TrafficSpawner>();

            int half = waypoints.Count / 2;
            spawner.lanes = new[]
            {
                new TrafficSpawner.Lane { waypoints = waypoints.GetRange(0, half).ToArray(), spawnIntervalSeconds = 8f },
                new TrafficSpawner.Lane { waypoints = waypoints.GetRange(half, waypoints.Count - half).ToArray(), spawnIntervalSeconds = 10f },
            };
            spawner.maxAlive = 12;
            spawner.despawnDistance = 180f;

            var prefabs = new List<GameObject>();
            foreach (var id in new[] { "Car_sedan", "Car_hatchback", "Car_suv" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/{id}.prefab");
                if (prefab != null) prefabs.Add(prefab);
            }
            spawner.trafficCarPrefabs = prefabs.ToArray();
        }

        static void BuildRefuelStation(GameObject root, List<RoadSpline.Sample> samples,
                                       float[,] heights, MapArchetype arch)
        {
            // Pistin dörtte birinde, yol kenarında
            var sample = samples[samples.Count / 4];
            var station = new GameObject("GasStation");
            station.transform.SetParent(root.transform, false);
            station.transform.position = sample.position + sample.right * (arch.road.roadWidth * 0.5f + 12f);
            station.transform.rotation = Quaternion.LookRotation(sample.forward, Vector3.up);

            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0f, 0.1f, 0f), new Vector3(18f, 0.2f, 12f));
            mb.AddBox(new Vector3(0f, 5.2f, 0f), new Vector3(18f, 0.5f, 12f));
            foreach (var (x, z) in new[] { (-7.5f, -4.5f), (7.5f, -4.5f), (-7.5f, 4.5f), (7.5f, 4.5f) })
                mb.AddBox(new Vector3(x, 2.6f, z), new Vector3(0.5f, 5.2f, 0.5f));
            mb.AddBox(new Vector3(-2.5f, 1f, 0f), new Vector3(1.2f, 2f, 2.4f));
            mb.AddBox(new Vector3(2.5f, 1f, 0f), new Vector3(1.2f, 2f, 2.4f));

            var go = CreateMeshObject(station, "Structure", mb.ToMesh($"gas_station_{arch.id}"),
                SolidMaterial("mat_station", new Color(0.78f, 0.79f, 0.80f), 0.1f, 0.4f), arch.id);
            go.AddComponent<MeshCollider>();

            var trigger = new GameObject("RefuelTrigger");
            trigger.transform.SetParent(station.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(14f, 3f, 9f);
            trigger.AddComponent<RefuelStation>();

            var lightGo = new GameObject("StationLight");
            lightGo.transform.SetParent(station.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 4.8f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 26f;
            light.intensity = 2.6f;
        }

        // ---------------------------------------------------------- Ortam
        static void SetupEnvironment(GameObject root, MapArchetype arch)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = arch.sunColor;
            sun.intensity = arch.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(arch.sunPitch, arch.sunYaw, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = arch.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = arch.fogColor;
            RenderSettings.fogDensity = arch.fogDensity;

            var dayNight = root.AddComponent<DayNightCycle>();
            dayNight.sun = sun;
            dayNight.dayLengthSeconds = 0f;   // harita kendi sabit ışığını korur

            root.AddComponent<Weather>();
        }

        static void SetupSceneManagers(GameObject root, MapArchetype arch)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 1400f;
            cam.backgroundColor = arch.skyTint;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Car.CarCameraFollow>();

            var boot = new GameObject("~Bootstrap");
            boot.AddComponent<GameBootstrap>();
            boot.AddComponent<PhotonConnector>();
            boot.AddComponent<ReconnectionManager>();

            var roomManager = boot.AddComponent<RoomManager>();
            var spawnParent = root.transform.Find("SpawnPoints");
            if (spawnParent != null)
            {
                var spawns = new List<Transform>();
                foreach (Transform t in spawnParent) spawns.Add(t);
                roomManager.spawnPoints = spawns.ToArray();
            }

            boot.AddComponent<NetworkInterestManager>();
            boot.AddComponent<DreamCar.Maps.MapSelector>();
        }

        // ---------------------------------------------------------- Katalog
        [MenuItem("DreamCar/Maps/Build Map Catalog")]
        public static void BuildMapCatalog()
        {
            const string folder = "Assets/Generated/Catalog";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            var catalog = ScriptableObject.CreateInstance<DreamCar.Maps.MapCatalog>();

            // Her harita için gündüz + gece + yağmurlu varyant
            var variants = new (string suffix, string label, Weather.Type weather, float time)[]
            {
                ("",       "",          Weather.Type.Clear, 0.50f),
                (".night", " (Gece)",   Weather.Type.Clear, 0.88f),
                (".rain",  " (Yağmur)", Weather.Type.Rain,  0.42f),
            };

            foreach (var arch in MapArchetype.All())
            foreach (var v in variants)
            {
                var def = ScriptableObject.CreateInstance<DreamCar.Maps.MapDefinition>();
                def.id = arch.id + v.suffix;
                def.displayName = arch.displayName + v.label;
                def.sceneName = SceneNameFor(arch);
                def.weather = v.weather;
                def.timeOfDay = v.time;

                AssetDatabase.CreateAsset(def, $"{folder}/{def.id.Replace('.', '_')}.asset");
                catalog.maps.Add(def);
            }

            AssetDatabase.CreateAsset(catalog, $"{folder}/MapCatalog.asset");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Map] Katalog: {catalog.maps.Count} giriş → {folder}/MapCatalog.asset");
        }

        [MenuItem("DreamCar/Maps/Add Maps To Build Settings")]
        public static void AddMapsToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (var existing in EditorBuildSettings.scenes)
                if (!existing.path.StartsWith(SceneFolder))
                    scenes.Add(existing);

            foreach (var arch in MapArchetype.All())
            {
                string path = $"{SceneFolder}/{SceneNameFor(arch)}.unity";
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Map] Build Settings: {scenes.Count} sahne");
        }

        // ---------------------------------------------------------- Yardımcılar
        static GameObject CreateMeshObject(GameObject parent, string name, Mesh mesh,
                                           Material material, string mapId)
        {
            SaveMesh(mesh, $"{mapId.Replace('.', '_')}_{name}");

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Material VertexColorMaterial(string name)
        {
            // Arazi ve proplar vertex color kullanır — tek materyal, çok renk.
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            return GetOrCreateMaterial(name, shader, mat =>
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.12f);
            });
        }

        static Material SolidMaterial(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return GetOrCreateMaterial(name, shader, mat =>
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            });
        }

        static Material TexturedMaterial(string name, string textureName,
                                         float metallic, float smoothness, Vector2 tiling)
            => ProceduralTextures.CreateTexturedMaterial(name, textureName, metallic, smoothness, tiling);

        static Material GetOrCreateMaterial(string name, Shader shader, System.Action<Material> configure)
        {
            const string folder = "Assets/Generated/Materials";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string path = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                configure(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                configure(mat);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

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
            foreach (var f in new[] { "Assets/Generated", MeshFolder, "Assets/Scenes", SceneFolder })
                if (!Directory.Exists(f)) Directory.CreateDirectory(f);
            AssetDatabase.Refresh();
        }
    }
}
#endif
