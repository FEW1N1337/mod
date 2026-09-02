#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using DreamCar.Audio;
using DreamCar.Customization;
using DreamCar.Effects;
using DreamCar.RCCPBridge;
using DreamCar.Vehicle;

namespace DreamCar.EditorTools.RCCPTools
{
    // RCCP'nin hazır araç prefabını alıp üzerine oyunun katmanını ekler:
    // ağ senkronu, ses, drift, yakıt, plaka, emote, kamera çapaları.
    //
    // NEDEN BÖYLE:
    // Köprü katmanı (Assets/Scripts/RCCPBridge/) yazılıydı ama hiçbir prefaba
    // eklenmiyordu — yani satın alınan RCCP oyunda hiç kullanılmıyordu.
    // Giriş noktası burası.
    //
    // RCCP aracını SIFIRDAN üretmiyoruz: RCCP'nin araç kurulumu motor, debriyaj,
    // şanzıman, diferansiyel ve aks bileşenlerinden oluşan ayarlı bir ağaç.
    // Onu kodla kurmak RCCP'nin iç API'sine bağımlılık demek. Bunun yerine
    // RCCP'nin kendi prefabını taban alıyoruz — RCCP kullanıcılarının fiilen
    // yaptığı da bu. Bu dosya RCCP'nin iç API'sine HİÇ dokunmuyor; yalnızca
    // "bu prefabda RCCP denetleyicisi var mı?" diye soruyor (reflection ile).
    //
    // Menü: DreamCar → RCCP → Seçili RCCP aracını DreamCar aracına çevir
    public static class RCCPCarConverter
    {
        const string ResourceFolder = "Assets/Resources";
        const string CatalogFolder = "Assets/Generated/Catalog";
        const string RccpTypeName = "RCCP_CarController";
        const string Define = "RCCP_INSTALLED";

        [MenuItem("DreamCar/RCCP/Seçili RCCP aracını DreamCar aracına çevir", priority = 200)]
        public static void ConvertSelected()
        {
            var src = Selection.activeGameObject;
            if (src == null || !PrefabUtility.IsPartOfPrefabAsset(src))
            {
                EditorUtility.DisplayDialog("DreamCar — RCCP",
                    "Önce Project penceresinden bir RCCP araç PREFAB'ı seç.\n\n" +
                    "RCCP'nin hazır araçları genelde şurada:\n" +
                    "Assets/Realistic Car Controller Pro/Resources/Vehicles/\n\n" +
                    "(Sahnedeki bir nesne değil, Project'teki prefab dosyası.)",
                    "Tamam");
                return;
            }

            var rccpType = RCCPReflection.FindType(RccpTypeName);
            if (rccpType == null)
            {
                EditorUtility.DisplayDialog("DreamCar — RCCP",
                    $"'{RccpTypeName}' tipi projede bulunamadı.\n\n" +
                    "RCCP import edilmemiş ya da sürümünde denetleyici sınıfının adı " +
                    "farklı olabilir. Import ettiysen Console'a bakıp bana adı gönder; " +
                    "köprü aday ad listesiyle çalışıyor, yenisini eklerim.",
                    "Tamam");
                return;
            }

            if (src.GetComponentInChildren(rccpType, true) == null)
            {
                EditorUtility.DisplayDialog("DreamCar — RCCP",
                    $"Seçili prefabda '{rccpType.Name}' bileşeni yok:\n\n{src.name}\n\n" +
                    "Bu bir RCCP aracı değil. RCCP'nin Vehicles klasöründen bir araç seç.",
                    "Tamam");
                return;
            }

            // RCCP'nin varlığı bu noktada KANITLANDI — define'ı kullanıcıya elle
            // bıraktırmanın anlamı yok. Köprü bileşenleri bu define olmadan
            // kendilerini sessizce devre dışı bırakıyor.
            EnsureDefine();

            string id = "rccp." + Sanitize(src.name);
            string prefabName = "Car_rccp_" + Sanitize(src.name);
            string prefabPath = $"{ResourceFolder}/{prefabName}.prefab";

            if (File.Exists(prefabPath) &&
                !EditorUtility.DisplayDialog("DreamCar — RCCP",
                    $"{prefabPath}\n\nzaten var. Üzerine yazılsın mı?", "Evet, yaz", "İptal"))
                return;

            if (!Directory.Exists(ResourceFolder)) Directory.CreateDirectory(ResourceFolder);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                // Prefab bağını koparıyoruz: üzerine bileşen ekleyip AYRI bir prefab
                // olarak kaydedeceğiz. Bağlı kalsaydı eklediklerimiz "override"
                // olurdu ve RCCP prefabı güncellenince beklenmedik şekilde ezilirdi.
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                                                   InteractionMode.AutomatedAction);
                root.name = prefabName;

                var report = BuildLayer(root);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                RegisterInCatalog(id, src.name, prefabName, root);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[RCCP] {src.name} → {prefabPath}\n{report}");
                EditorUtility.DisplayDialog("DreamCar — RCCP",
                    $"Hazır: {saved.name}\n\n{report}\n\n" +
                    "Araç, ana menüdeki \"Araçlar\" mağazasında görünecek.\n" +
                    "Satın alıp seçtikten sonra odaya girince o araçla doğarsın.",
                    "Tamam");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- katman

        static string BuildLayer(GameObject root)
        {
            var log = new System.Text.StringBuilder();

            var rb = root.GetComponent<Rigidbody>();
            var wheels = root.GetComponentsInChildren<WheelCollider>(true);
            var bounds = ComputeBounds(root);

            // --- Ağ ---
            // PhotonView EN ÖNCE: aşağıdaki MonoBehaviourPun türevlerinde
            // [RequireComponent(typeof(PhotonView))] var; PhotonView yokken
            // eklenirlerse Unity kendi görünümünü üretir ve ikinci bir
            // PhotonView oluşurdu.
            var pv = Get<Photon.Pun.PhotonView>(root);
            var sync = Get<DreamCar.Car.CarNetworkSync>(root);
            pv.ObservedComponents = new List<Component> { sync };
            pv.Synchronization = Photon.Pun.ViewSynchronization.UnreliableOnChange;
            log.AppendLine("• Ağ: PhotonView + CarNetworkSync");

            // --- Sürücü köprüsü ---
            // ASIL OLAN BU. RCCP denetleyicisini IDriveInput altında sarıyor;
            // MobileTouchInput, HUD, vites ve yakıt sistemi RCCP'den habersiz
            // çalışmaya devam ediyor.
            Get<RCCPCarAdapter>(root);
            Get<RCCPNitroBridge>(root);
            Get<RCCPDamageBridge>(root);
            Get<RCCPDetachableBridge>(root);
            Get<RCCPWheelGlowBridge>(root);
            log.AppendLine("• Köprü: RCCPCarAdapter + nitro/hasar/parça/parıltı");

            // --- Ses ---
            var idle = AddSource(root, loop: true, volume: 0.55f, maxDistance: 45f);
            var rev = AddSource(root, loop: true, volume: 0f, maxDistance: 60f);
            var engine = Get<EngineAudio>(root);
            engine.idleLoop = idle;
            engine.revLoop = rev;

            var synth = Get<ProceduralEngineAudio>(root);
            synth.idleSource = idle;
            synth.revSource = rev;

            var screechSource = AddSource(root, loop: true, volume: 0.6f, maxDistance: 40f);
            var screech = Get<TireScreechAudio>(root);
            screech.wheels = wheels;
            screech.loop = screechSource;

            var hornSource = AddSource(root, loop: false, volume: 0.8f, maxDistance: 60f);
            Get<DreamCar.Emote.HornController>(root).horn = hornSource;

            // Taban volume sıfır bırakılamaz: CarDamage bu kaynağı Awake'te
            // AudioBus.RegisterSfx ile kaydedip o andaki volume'u "taban seviye"
            // sayıyor — 0 olursa sonsuza dek sessiz kalır.
            var crashSource = AddSource(root, loop: false, volume: 0.8f, maxDistance: 70f);

            synth.screechSource = screechSource;
            synth.hornSource = hornSource;
            synth.crashSource = crashSource;
            log.AppendLine("• Ses: motor (prosedürel), lastik, korna, çarpma");

            // --- Oynanış ---
            var damage = Get<CarDamage>(root);
            damage.crashSfx = crashSource;
            damage.smoke = Procedural.ProceduralCarGenerator.MakeDamageSmoke(
                root, new Vector3(0f, bounds.center.y + bounds.extents.y * 0.5f,
                                  bounds.max.z - bounds.size.z * 0.15f));

            Get<DreamCar.Race.DriftScore>(root);
            Get<CruiseControl>(root);
            Get<GearBox>(root);
            Get<FuelSystem>(root);
            Get<DreamCar.Core.StatsTracker>(root);
            log.AppendLine("• Oynanış: drift, vites, yakıt, hasar, istatistik");

            // --- Drift dumanı + fren izi (tekerlek başına) ---
            int fx = 0;
            foreach (var wc in wheels)
            {
                if (!wc) continue;
                AttachWheelFx(wc);
                fx++;
            }
            log.AppendLine($"• Drift dumanı ve fren izi: {fx} tekerlek");

            // --- Görsel ---
            var body = FindPaintTarget(root);
            if (body)
            {
                Get<CarPaint>(root).paintRenderers = new[] { body };
                Get<CarPaintHDR>(root).paintRenderers = new[] { body };
                log.AppendLine($"• Boya hedefi: {body.name}");
            }
            else log.AppendLine("• Boya hedefi bulunamadı (gövde renderer'ı seçilemedi)");

            var plateMat = Procedural.ProceduralTextures.CreatePlateMaterial();
            Get<LicensePlate>(root).plateRenderers = new[]
            {
                Procedural.ProceduralCarGenerator.MakePlateQuad(root, "Plate_Front", plateMat,
                    new Vector3(0f, bounds.min.y + bounds.size.y * 0.22f, bounds.max.z + 0.02f), 0f),
                Procedural.ProceduralCarGenerator.MakePlateQuad(root, "Plate_Rear", plateMat,
                    new Vector3(0f, bounds.min.y + bounds.size.y * 0.24f, bounds.min.z - 0.02f), 180f),
            };
            log.AppendLine("• Plaka: ön + arka");

            // --- Kamera çapaları ---
            // Sabit sayı yazılamaz: her RCCP aracının boyutu farklı, çapa yanlış
            // yere düşerdi. Prefabın gerçek sınırlarından hesaplanıyor.
            AddAnchor(root, "HoodCam", new Vector3(0f, bounds.max.y - 0.10f, bounds.center.z + bounds.extents.z * 0.30f));
            AddAnchor(root, "BumperCam", new Vector3(0f, bounds.center.y, bounds.max.z - 0.10f));
            AddAnchor(root, "InteriorCam", new Vector3(-0.34f, bounds.max.y - 0.28f, bounds.center.z));
            var overhead = AddAnchor(root, "OverheadAnchor", new Vector3(0f, bounds.max.y + 0.55f, bounds.center.z));
            Get<InteriorCamera>(root);
            log.AppendLine("• Kamera çapaları: kaput, tampon, kokpit, üst");

            // --- Emote ---
            var emotes = Get<DreamCar.Emote.EmoteSystem>(root);
            emotes.overheadAnchor = overhead;
            emotes.audioSource = hornSource;
            emotes.emotePopupPrefab = Procedural.ProceduralCarGenerator.EmotePopupPrefab();
            emotes.emotes = new List<DreamCar.Emote.EmoteSystem.EmoteEntry>
            {
                new() { id = "wave", icon = Procedural.ProceduralCarGenerator.UiSprite("icon_emote") },
                new() { id = "gg",   icon = Procedural.ProceduralCarGenerator.UiSprite("icon_trophy") },
                new() { id = "race", icon = Procedural.ProceduralCarGenerator.UiSprite("icon_flag") },
            };
            Get<DreamCar.Emote.AirHorn>(root).source = hornSource;
            log.AppendLine("• Emote + korna");

            if (rb == null)
                log.AppendLine("! UYARI: Rigidbody yok — RCCP aracı olarak beklenmedik.");
            if (wheels.Length == 0)
                log.AppendLine("! UYARI: WheelCollider bulunamadı — lastik sesi ve drift dumanı çalışmaz.");

            // Bilerek EKLENMEYENLER — bkz. sınıf başlığı:
            //   CarController / WheelCollider'larımız → fizik RCCP'nin
            //   CarNitro                              → RCCPNitroBridge devralıyor
            //   WheelGlow                             → RCCP kendi parıltısını getiriyor
            //   Far / sinyal bileşenleri              → RCCP'nin kendi ışık sistemi var,
            //                                           ikisi aynı Light'ları sürerse titrer
            return log.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------- yardımcılar

        // Bileşen zaten varsa onu döndür — RCCP prefabında bizimkilerden biri
        // bulunuyorsa ikinci kopya eklemek çift davranış demek.
        static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c ? c : go.AddComponent<T>();
        }

        static AudioSource AddSource(GameObject go, bool loop, float volume, float maxDistance)
        {
            var s = go.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.volume = volume;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.maxDistance = maxDistance;
            return s;
        }

        static Transform AddAnchor(GameObject root, string name, Vector3 localPosition)
        {
            var existing = root.transform.Find(name);
            if (existing) return existing;

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        // Duman ve iz, WheelCollider'ın GameObject'ine bağlanır — tekerlek MESH'i
        // dönüyor, collider dönmüyor. İz dönen mesh'e bağlansaydı spiral çizerdi.
        static void AttachWheelFx(WheelCollider wc)
        {
            if (wc.transform.Find("_DriftSmoke")) return;   // ikinci kez çalıştırılmışsa

            var smokeGo = new GameObject("_DriftSmoke");
            smokeGo.transform.SetParent(wc.transform, false);
            var ps = smokeGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.76f, 0.74f, 0.55f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 120;
            // playOnAwake AÇIK: DriftSmoke yalnızca emission.rateOverTime yazıyor,
            // hiç Play() çağırmıyor. Duran bir sistemde oran yazmak hiçbir şey yapmaz.
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var smokeMat = Procedural.ProceduralWeather.ParticleMaterial("mat_fx_smoke", "fx_smoke");
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = smokeMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            var trailGo = new GameObject("_SkidTrail");
            trailGo.transform.SetParent(wc.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, -wc.radius + 0.02f, 0f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 4.5f;
            trail.startWidth = wc.radius * 0.55f;
            trail.endWidth = wc.radius * 0.45f;
            trail.emitting = false;
            trail.sharedMaterial = smokeMat;
            trail.startColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);
            trail.endColor = new Color(0.05f, 0.05f, 0.05f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ds = wc.gameObject.AddComponent<DriftSmoke>();
            ds.wheel = wc;
            ds.smoke = ps;
            ds.skidTrail = trail;
        }

        // Boya hedefi: en çok üçgeni olan renderer. Gövde neredeyse her zaman
        // aracın en büyük mesh'i; cam, jant ve far ondan çok daha küçük.
        static Renderer FindPaintTarget(GameObject root)
        {
            Renderer best = null;
            int bestVerts = -1;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var r = mf.GetComponent<Renderer>();
                if (!r) continue;
                if (mf.sharedMesh.vertexCount > bestVerts)
                {
                    bestVerts = mf.sharedMesh.vertexCount;
                    best = r;
                }
            }
            return best;
        }

        static Bounds ComputeBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(Vector3.zero, new Vector3(1.8f, 1.4f, 4.2f));

            // Dünya sınırlarını aracın YEREL uzayına çeviriyoruz: çapa konumları
            // localPosition olarak yazılıyor.
            var b = new Bounds(root.transform.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                b.Encapsulate(root.transform.InverseTransformPoint(r.bounds.min));
                b.Encapsulate(root.transform.InverseTransformPoint(r.bounds.max));
            }
            return b;
        }

        static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            return sb.ToString().Trim('_');
        }

        // ---------------------------------------------------------------- define

        static void EnsureDefine()
        {
            foreach (var group in new[]
                     {
                         NamedBuildTarget.Standalone,
                         NamedBuildTarget.Android,
                         NamedBuildTarget.iOS,
                     })
            {
                try
                {
                    PlayerSettings.GetScriptingDefineSymbols(group, out string[] defines);
                    var list = new List<string>(defines);
                    if (list.Contains(Define)) continue;
                    list.Add(Define);
                    PlayerSettings.SetScriptingDefineSymbols(group, list.ToArray());
                    Debug.Log($"[RCCP] {Define} define'ı eklendi: {group.TargetName}");
                }
                catch (System.Exception e)
                {
                    // Bir hedef platform kurulu değilse burası patlar; diğerleri
                    // etkilenmemeli. Elle yol: Player Settings → Scripting Define Symbols.
                    Debug.LogWarning($"[RCCP] {group.TargetName} için define eklenemedi: {e.Message}");
                }
            }
        }

        // ---------------------------------------------------------------- katalog

        static void RegisterInCatalog(string id, string displayName, string prefabName, GameObject root)
        {
            if (!Directory.Exists(CatalogFolder)) Directory.CreateDirectory(CatalogFolder);

            string defPath = $"{CatalogFolder}/{id.Replace('.', '_')}.asset";
            var def = AssetDatabase.LoadAssetAtPath<DreamCar.Economy.CarDefinition>(defPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<DreamCar.Economy.CarDefinition>();

            def.id = id;
            def.displayName = displayName;
            def.resourcePrefabName = prefabName;
            def.price = 120000;                 // RCCP araçları üst segment
            def.topSpeedKmh = 240f;
            def.maxMotorTorque = 2000f;
            def.speedStat = 9;
            def.accelerationStat = 9;

            // Ağırlık gerçek Rigidbody'den: yol tutuşu istatistiği uydurma olmasın.
            var rb = root.GetComponent<Rigidbody>();
            float mass = rb ? rb.mass : 1400f;
            def.handlingStat = Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(1900f, 1050f, mass) * 10f), 1, 10);

            if (isNew) AssetDatabase.CreateAsset(def, defPath);
            else EditorUtility.SetDirty(def);

            var catalog = Procedural.ProceduralCarGenerator.LoadCatalog();
            if (catalog == null)
            {
                Debug.LogWarning("[RCCP] Araç kataloğu yok. Önce DreamCar → BUILD EVERYTHING " +
                                 "çalıştır; araç sonra katalogda görünür.");
                return;
            }

            if (!catalog.cars.Contains(def))
            {
                catalog.cars.Add(def);
                EditorUtility.SetDirty(catalog);
            }
        }
    }
}
#endif
