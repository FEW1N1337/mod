#if UNITY_EDITOR
using UnityEngine;

namespace DreamCar.EditorTools.Procedural.Maps
{
    // Bir harita tipinin tüm tarifi. Yeni harita eklemek = buraya yeni preset yazmak.
    public class MapArchetype
    {
        public enum RoadLayout { Circuit, Highway, Winding, CityGrid }
        public enum PropKind { Tree, Pine, Rock, Cactus, Building, Container, Crane, House, Barn, Barrier, Lamp }

        public string id = "map.custom";
        public string displayName = "Custom";

        // --- Yol ---
        public RoadLayout layout = RoadLayout.Circuit;
        public float roadExtent = 420f;          // pist yarıçapı / otoyol uzunluğu
        public int roadCorners = 12;
        public float roadIrregularity = 0.22f;
        public float roadHeightAmplitude = 0f;
        public RoadMeshBuilder.Settings road = new();

        // --- Arazi ---
        public TerrainMeshBuilder.Settings terrain = new();

        // --- Ortam ---
        public Color skyTint = new(0.55f, 0.72f, 0.95f);
        public Color fogColor = new(0.62f, 0.74f, 0.88f);
        public float fogDensity = 0.0016f;
        public float sunPitch = 48f;
        public float sunYaw = -35f;
        public Color sunColor = new(1f, 0.97f, 0.90f);
        public float sunIntensity = 1.15f;
        public Color ambient = new(0.42f, 0.46f, 0.52f);

        // --- Proplar ---
        public PropRule[] props = System.Array.Empty<PropRule>();

        public class PropRule
        {
            public PropKind kind;
            public int count = 200;
            public float minRoadDistance = 24f;   // yola bu kadar yakına koyma
            public float maxRoadDistance = 400f;  // bundan uzağa da koyma (boş alan olmasın)
            public float minScale = 0.85f;
            public float maxScale = 1.35f;
            public float maxSlope = 0.55f;        // dik yamaçlara koyma (0-1)
            public float minHeight = -9999f;
            public float maxHeight = 9999f;
            public Color tint = Color.white;
        }

        // ================================================================
        //  PRESETLER — Dream Road'daki harita çeşitliliğine karşılık gelir
        // ================================================================
        public static MapArchetype[] All() => new[]
        {
            Track(), Highway(), Desert(), Forest(), Snow(), Port(), Offroad(), Village(),
        };

        // Kapalı yarış pisti — düz arazi, bariyerli, hızlı
        public static MapArchetype Track() => new()
        {
            id = "map.track", displayName = "Pist",
            layout = RoadLayout.Circuit,
            roadExtent = 380f, roadCorners = 14, roadIrregularity = 0.26f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 16f, shoulderWidth = 4f, guardrails = true,
                maxBankAngle = 11f, centerLine = false,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 700f, heightAmplitude = 8f, noiseScale = 0.0018f,
                flatRadius = 40f, blendRadius = 120f, seed = 7001,
                lowColor = new Color(0.30f, 0.46f, 0.22f),
                midColor = new Color(0.34f, 0.48f, 0.24f),
                highColor = new Color(0.38f, 0.50f, 0.26f),
            },
            props = new[]
            {
                new PropRule { kind = PropKind.Barrier, count = 60, minRoadDistance = 16f, maxRoadDistance = 26f },
                new PropRule { kind = PropKind.Tree, count = 140, minRoadDistance = 45f, maxRoadDistance = 600f },
                new PropRule { kind = PropKind.Lamp, count = 40, minRoadDistance = 14f, maxRoadDistance = 22f },
            },
        };

        // Uzun otoyol — hafif virajlar, yüksek hız
        public static MapArchetype Highway() => new()
        {
            id = "map.highway", displayName = "Otoyol",
            layout = RoadLayout.Highway,
            roadExtent = 1800f, roadHeightAmplitude = 18f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 18f, shoulderWidth = 3.5f, guardrails = true, maxBankAngle = 5f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 1000f, resolution = 160, heightAmplitude = 32f,
                noiseScale = 0.0016f, flatRadius = 30f, blendRadius = 110f, seed = 7002,
                lowColor = new Color(0.32f, 0.40f, 0.20f),
                midColor = new Color(0.40f, 0.42f, 0.24f),
                highColor = new Color(0.50f, 0.48f, 0.34f),
            },
            props = new[]
            {
                new PropRule { kind = PropKind.Lamp, count = 90, minRoadDistance = 13f, maxRoadDistance = 20f },
                new PropRule { kind = PropKind.Tree, count = 260, minRoadDistance = 38f, maxRoadDistance = 700f },
                new PropRule { kind = PropKind.Building, count = 30, minRoadDistance = 90f, maxRoadDistance = 500f },
            },
        };

        // Çöl — sıcak tonlar, kanyon sırtları, kaktüs ve kaya
        public static MapArchetype Desert() => new()
        {
            id = "map.desert", displayName = "Çöl",
            layout = RoadLayout.Winding,
            roadExtent = 500f, roadHeightAmplitude = 26f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 13f, shoulderWidth = 5f, guardrails = false, maxBankAngle = 7f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 850f, heightAmplitude = 70f, noiseScale = 0.0026f,
                ridgeSharpness = 0.55f, octaves = 5,
                flatRadius = 22f, blendRadius = 80f, seed = 7003,
                lowColor = new Color(0.76f, 0.62f, 0.38f),
                midColor = new Color(0.68f, 0.50f, 0.30f),
                highColor = new Color(0.55f, 0.38f, 0.26f),
            },
            skyTint = new Color(0.92f, 0.80f, 0.58f),
            fogColor = new Color(0.88f, 0.76f, 0.56f),
            fogDensity = 0.0011f,
            sunPitch = 58f, sunColor = new Color(1f, 0.92f, 0.74f), sunIntensity = 1.4f,
            ambient = new Color(0.58f, 0.50f, 0.38f),
            props = new[]
            {
                new PropRule { kind = PropKind.Cactus, count = 220, minRoadDistance = 20f, maxRoadDistance = 600f, maxSlope = 0.35f },
                new PropRule { kind = PropKind.Rock, count = 320, minRoadDistance = 18f, maxRoadDistance = 800f,
                               minScale = 0.6f, maxScale = 3.2f },
            },
        };

        // Orman — sık ağaç, dolambaçlı dar yol
        public static MapArchetype Forest() => new()
        {
            id = "map.forest", displayName = "Orman",
            layout = RoadLayout.Winding,
            roadExtent = 430f, roadHeightAmplitude = 34f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 11f, shoulderWidth = 2f, guardrails = true, maxBankAngle = 9f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 750f, heightAmplitude = 58f, noiseScale = 0.0030f, octaves = 5,
                flatRadius = 18f, blendRadius = 70f, seed = 7004,
                lowColor = new Color(0.18f, 0.34f, 0.16f),
                midColor = new Color(0.22f, 0.38f, 0.18f),
                highColor = new Color(0.34f, 0.40f, 0.26f),
            },
            skyTint = new Color(0.48f, 0.62f, 0.72f),
            fogColor = new Color(0.52f, 0.62f, 0.58f),
            fogDensity = 0.0034f,
            sunPitch = 38f, sunIntensity = 0.95f,
            ambient = new Color(0.30f, 0.36f, 0.30f),
            props = new[]
            {
                new PropRule { kind = PropKind.Pine, count = 900, minRoadDistance = 13f, maxRoadDistance = 700f,
                               minScale = 0.8f, maxScale = 1.8f },
                new PropRule { kind = PropKind.Tree, count = 300, minRoadDistance = 14f, maxRoadDistance = 700f },
                new PropRule { kind = PropKind.Rock, count = 140, minRoadDistance = 16f, maxRoadDistance = 600f },
            },
        };

        // Kar — beyaz arazi, çam ormanı, kaygan his
        public static MapArchetype Snow() => new()
        {
            id = "map.snow", displayName = "Kar",
            layout = RoadLayout.Winding,
            roadExtent = 460f, roadHeightAmplitude = 40f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 12f, shoulderWidth = 3f, guardrails = true, maxBankAngle = 6f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 800f, heightAmplitude = 85f, noiseScale = 0.0024f,
                ridgeSharpness = 0.4f, octaves = 5,
                flatRadius = 20f, blendRadius = 85f, seed = 7005,
                lowColor = new Color(0.82f, 0.86f, 0.92f),
                midColor = new Color(0.88f, 0.91f, 0.95f),
                highColor = new Color(0.96f, 0.97f, 1.00f),
            },
            skyTint = new Color(0.72f, 0.80f, 0.90f),
            fogColor = new Color(0.80f, 0.85f, 0.92f),
            fogDensity = 0.0042f,
            sunPitch = 26f, sunColor = new Color(0.90f, 0.94f, 1f), sunIntensity = 0.85f,
            ambient = new Color(0.52f, 0.58f, 0.66f),
            props = new[]
            {
                new PropRule { kind = PropKind.Pine, count = 700, minRoadDistance = 14f, maxRoadDistance = 700f,
                               minScale = 0.9f, maxScale = 2.0f, tint = new Color(0.72f, 0.82f, 0.78f) },
                new PropRule { kind = PropKind.Rock, count = 180, minRoadDistance = 18f, maxRoadDistance = 700f,
                               tint = new Color(0.85f, 0.88f, 0.92f) },
            },
        };

        // Liman — düz zemin, konteyner yığınları, vinçler
        public static MapArchetype Port() => new()
        {
            id = "map.port", displayName = "Liman",
            layout = RoadLayout.Circuit,
            roadExtent = 400f, roadCorners = 10, roadIrregularity = 0.34f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 15f, shoulderWidth = 3f, guardrails = false, maxBankAngle = 4f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 700f, heightAmplitude = 4f, noiseScale = 0.0020f,
                flatRadius = 45f, blendRadius = 130f, seed = 7006,
                lowColor = new Color(0.34f, 0.35f, 0.37f),
                midColor = new Color(0.40f, 0.41f, 0.43f),
                highColor = new Color(0.46f, 0.47f, 0.49f),
            },
            skyTint = new Color(0.60f, 0.70f, 0.80f),
            fogColor = new Color(0.66f, 0.72f, 0.78f),
            fogDensity = 0.0022f,
            ambient = new Color(0.44f, 0.47f, 0.50f),
            props = new[]
            {
                new PropRule { kind = PropKind.Container, count = 420, minRoadDistance = 20f, maxRoadDistance = 600f,
                               minScale = 0.95f, maxScale = 1.05f },
                new PropRule { kind = PropKind.Crane, count = 14, minRoadDistance = 40f, maxRoadDistance = 500f },
                new PropRule { kind = PropKind.Lamp, count = 60, minRoadDistance = 13f, maxRoadDistance = 22f },
            },
        };

        // Arazi — engebeli toprak yol, taşlık
        public static MapArchetype Offroad() => new()
        {
            id = "map.offroad", displayName = "Arazi",
            layout = RoadLayout.Winding,
            roadExtent = 480f, roadHeightAmplitude = 48f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 10f, shoulderWidth = 4f, guardrails = false,
                maxBankAngle = 12f, centerLine = false,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 820f, heightAmplitude = 95f, noiseScale = 0.0032f,
                ridgeSharpness = 0.45f, octaves = 6,
                flatRadius = 14f, blendRadius = 55f, seed = 7007,
                lowColor = new Color(0.42f, 0.38f, 0.26f),
                midColor = new Color(0.48f, 0.42f, 0.28f),
                highColor = new Color(0.52f, 0.48f, 0.40f),
            },
            skyTint = new Color(0.66f, 0.72f, 0.80f),
            fogDensity = 0.0018f,
            props = new[]
            {
                new PropRule { kind = PropKind.Rock, count = 520, minRoadDistance = 12f, maxRoadDistance = 750f,
                               minScale = 0.5f, maxScale = 3.5f },
                new PropRule { kind = PropKind.Tree, count = 240, minRoadDistance = 16f, maxRoadDistance = 700f,
                               maxSlope = 0.45f },
            },
        };

        // Köy — dar yollar, evler, ahırlar, tarlalar
        public static MapArchetype Village() => new()
        {
            id = "map.village", displayName = "Köy",
            layout = RoadLayout.Circuit,
            roadExtent = 340f, roadCorners = 16, roadIrregularity = 0.30f,
            roadHeightAmplitude = 12f,
            road = new RoadMeshBuilder.Settings
            {
                roadWidth = 9f, shoulderWidth = 2f, guardrails = false, maxBankAngle = 5f,
            },
            terrain = new TerrainMeshBuilder.Settings
            {
                extent = 650f, heightAmplitude = 26f, noiseScale = 0.0021f,
                flatRadius = 20f, blendRadius = 70f, seed = 7008,
                lowColor = new Color(0.40f, 0.50f, 0.22f),
                midColor = new Color(0.46f, 0.52f, 0.26f),
                highColor = new Color(0.52f, 0.50f, 0.32f),
            },
            skyTint = new Color(0.62f, 0.76f, 0.92f),
            props = new[]
            {
                new PropRule { kind = PropKind.House, count = 90, minRoadDistance = 15f, maxRoadDistance = 260f,
                               maxSlope = 0.30f },
                new PropRule { kind = PropKind.Barn, count = 26, minRoadDistance = 22f, maxRoadDistance = 320f,
                               maxSlope = 0.25f },
                new PropRule { kind = PropKind.Tree, count = 380, minRoadDistance = 14f, maxRoadDistance = 600f },
            },
        };
    }
}
#endif
