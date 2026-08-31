#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamCar.EditorTools.Procedural
{
    // Weather bileşeninin üç alanını (rainFX / snowFX / rainLoop) kodla kurar.
    //
    // Sorun: harita üreticisi sahneye sadece `root.AddComponent<Weather>()`
    // yapıyordu; üç alan da boş kaldığı için "Yağmur" varyantı temiz havadan
    // hiçbir şekilde ayırt edilemiyordu — ne partikül, ne ses, ne görsel fark.
    //
    // Burada üretilenler:
    //   • RainFX  — ince uzun damlalar, kameranın üstündeki geniş kutudan düşer
    //   • SnowFX  — yavaş, salınımlı, yuvarlak taneler
    //   • RainLoop — ProceduralWeatherAudio ile Awake'te sentezlenen yağmur uğultusu
    //
    // Partikül dokuları ve materyalleri Assets/Generated altına varlık olarak
    // yazılır; sahneye gömülü materyal sahne yeniden yüklenince kaybolur.
    public static class ProceduralWeather
    {
        const string TextureFolder = "Assets/Generated/Textures";
        const string MaterialFolder = "Assets/Generated/Materials";

        // Yağmur damlası dokusunun oranı: dar ve uzun.
        const int DropWidth = 16;
        const int DropHeight = 64;
        const int FlakeSize = 32;

        // Yağmur sesinin taban seviyesi. AudioBus.RegisterSfx kaydolduğu andaki
        // volume'ü "taban" kabul ediyor — 0 bırakılırsa ses hiç duyulmaz.
        const float RainVolume = 0.35f;

        // parent altına FX'leri kurar ve weather alanlarına bağlar.
        public static void Attach(GameObject parent, DreamCar.Environment.Weather weather)
        {
            if (parent == null || weather == null) return;

            EnsureFolders();

            var rain = BuildRain(parent);
            var snow = BuildSnow(parent);
            var rainLoop = BuildRainLoop(parent);

            weather.rainFX = rain;
            weather.snowFX = snow;
            weather.rainLoop = rainLoop;

            EditorUtility.SetDirty(weather);
        }

        // ---------------------------------------------------------- Partiküller

        // Yağmur: hızlı düşen, neredeyse dikey, çok sayıda ince damla.
        static ParticleSystem BuildRain(GameObject parent)
        {
            var ps = NewParticleSystem(parent, "RainFX");

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = false;          // Weather.Update Play()/Stop() çağırıyor
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            main.startColor = new Color(0.74f, 0.80f, 0.88f, 0.55f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1400;

            var emission = ps.emission;
            emission.rateOverTime = 900f;

            // Kameranın üstünde geniş, ince bir kutu. Kutu yönü yerel +Z olduğu
            // için 90° döndürülüp aşağı bakması sağlanır (obje dönmez: takip
            // bileşeni sadece konum yazıyor).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.scale = new Vector3(60f, 60f, 1f);
            shape.randomDirectionAmount = 0.02f;   // dar koni: neredeyse dikey

            // Yere yaklaşırken sönümlensin — damlalar aniden yok olmasın.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(Color.white, holdUntil: 0.75f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            // Stretch: damla hız yönünde uzar, klasik yağmur görünümü.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2.4f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = ParticleMaterial("mat_fx_rain", "fx_raindrop");
            DisableShadows(renderer);

            AddFollow(ps.gameObject, height: 18f, lead: 16f);
            return ps;
        }

        // Kar: yavaş, salınımlı, iri ve yuvarlak taneler.
        static ParticleSystem BuildSnow(GameObject parent)
        {
            var ps = NewParticleSystem(parent, "SnowFX");

            var main = ps.main;
            main.duration = 6f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.gravityModifier = 0.04f;      // neredeyse havada asılı
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Ömür uzun olduğu için tavan yüksek: rate x ömür tavanı aşarsa
            // emisyon kesilir ve kar kesik kesik görünür.
            main.maxParticles = 1100;

            var emission = ps.emission;
            emission.rateOverTime = 120f;

            // Geniş koni — taneler tepeden yayılarak iner.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radius = 30f;
            shape.angle = 16f;

            // Rüzgârda salınım. Düşük kalite yeterli — mobilde ucuz kalsın.
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.9f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient(Color.white, holdUntil: 0.7f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ParticleMaterial("mat_fx_snow", "fx_snowflake");
            DisableShadows(renderer);

            AddFollow(ps.gameObject, height: 20f, lead: 8f);
            return ps;
        }

        static ParticleSystem NewParticleSystem(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static void AddFollow(GameObject go, float height, float lead)
        {
            // Kamera bulunamazsa takip bileşeni konumu yazmaz; başlangıç yüksekliği
            // burada duruyor ki o durumda da partiküller yerin altından çıkmasın.
            go.transform.localPosition = new Vector3(0f, height, 0f);

            var follow = go.AddComponent<DreamCar.Environment.WeatherFollowCamera>();
            follow.offset = new Vector3(0f, height, 0f);
            follow.forwardLead = lead;
        }

        static void DisableShadows(ParticleSystemRenderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Ömrün sonuna doğru alfayı sıfıra indiren gradient.
        static ParticleSystem.MinMaxGradient FadeGradient(Color tint, float holdUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(tint, 0f),
                    new GradientColorKey(tint, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(1f, holdUntil),
                    new GradientAlphaKey(0f, 1f),
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        // ---------------------------------------------------------- Ses

        static AudioSource BuildRainLoop(GameObject parent)
        {
            var go = new GameObject("RainLoop");
            go.transform.SetParent(parent.transform, false);

            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;         // Weather.Update Play()/Stop() ediyor
            source.spatialBlend = 0f;           // ortam sesi: her yerde aynı
            source.volume = RainVolume;         // AudioBus bunu taban seviye kabul eder
            source.priority = 160;              // motor sesinden geride kalsın

            // Klip bir varlık değil; oyun açılırken sentezlenir.
            var generator = go.AddComponent<DreamCar.Environment.ProceduralWeatherAudio>();
            generator.target = source;

            return source;
        }

        // ---------------------------------------------------------- Materyal

        // URP partikül shader'ı varsa o, yoksa built-in yedekleri
        // (ProceduralMapGenerator.VertexLitShader ile aynı yedekleme kalıbı).
        static Shader ParticleShader() =>
            Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");

        static Material ParticleMaterial(string name, string textureName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var shader = ParticleShader();

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                ConfigureParticleMaterial(mat, textureName);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                ConfigureParticleMaterial(mat, textureName);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static void ConfigureParticleMaterial(Material mat, string textureName)
        {
            var tex = LoadOrBuildTexture(textureName);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            // Saydam yüzey — URP ve built-in için ayrı anahtarlar, hangisi varsa o.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);   // 0 = alpha blend
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // ---------------------------------------------------------- Doku

        static Texture2D LoadOrBuildTexture(string name)
        {
            string path = $"{TextureFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var built = name == "fx_snowflake"
                ? BuildFlake(FlakeSize)
                : BuildDrop(DropWidth, DropHeight);

            SaveTexture(built, name);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Damla: dikeyde uzayan, kenarları yumuşak ince bir çizgi.
        // Renk beyaz, şekli tamamen alfa kanalında — materyal rengi damlayı boyar.
        static Texture2D BuildDrop(int width, int height)
        {
            var tex = NewTexture(width, height, "fx_raindrop");
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f) / width * 2f - 1f;
                float dy = (y + 0.5f) / height * 2f - 1f;

                // Yatayda gauss düşüş → yumuşak kenar
                float across = Mathf.Exp(-dx * dx * 7f);
                // Dikeyde uçlara doğru sönüm → çizginin başı/sonu keskin kesilmesin
                float along = Mathf.Clamp01(Mathf.Cos(dy * Mathf.PI * 0.5f));

                float a = across * Mathf.Pow(along, 0.55f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Kar tanesi: kenarı yumuşak dolu daire.
        static Texture2D BuildFlake(int size)
        {
            var tex = NewTexture(size, size, "fx_snowflake");
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01(1f - d);
                a *= a;   // merkeze doğru yoğunlaşan yumuşak düşüş
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D NewTexture(int width, int height, string name) =>
            new(width, height, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };

        static void SaveTexture(Texture2D tex, string name)
        {
            EnsureFolders();
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                // Partikül dokusu: kenarda tekrar etmesin, alfa saydamlık olarak yorumlansın.
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            Object.DestroyImmediate(tex);
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[] { "Assets/Generated", TextureFolder, MaterialFolder })
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }
    }
}
#endif
