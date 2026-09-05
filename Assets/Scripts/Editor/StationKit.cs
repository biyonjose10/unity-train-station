using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Builds the station itself: ground, track, platform, canopy, buildings, signals and the
    /// scenery that fades off into the fog.
    ///
    /// Every scene calls into here, so the four scenes cannot drift apart visually — there is
    /// one station, described once, instantiated four times.
    /// </summary>
    public static class StationKit
    {
        // ------------------------------------------------------------------- world layout
        // Track runs along X. The train departs towards +X. Z is across the tracks.

        /// <summary>Platform road: the track our train uses.</summary>
        public const float TrackAZ = 0f;
        /// <summary>Through road: where the other train sits in scene 2.</summary>
        public const float TrackBZ = -7.6f;

        public const float RailTopY = 0.52f;
        public const float RailGauge = 1.52f;

        public const float PlatformTopY = 1.5f;
        public const float PlatformEdgeZ = 2.1f;
        public const float PlatformBackZ = 13f;
        public const float PlatformXMin = -58f;
        public const float PlatformXMax = 40f;

        public const float TrackLength = 900f;

        /// <summary>Where the front of the locomotive comes to rest.</summary>
        public const float StopMarkX = 26f;

        // ------------------------------------------------------------------------ palette

        public static Material Ballast { get { return Prim.Mat("Ballast", new Color(0.185f, 0.175f, 0.165f), 0f, 0.10f); } }
        public static Material Sleeper { get { return Prim.Mat("Sleeper", new Color(0.125f, 0.095f, 0.075f), 0f, 0.08f); } }
        public static Material Rail { get { return Prim.Mat("Rail", new Color(0.36f, 0.35f, 0.34f), 0.85f, 0.55f); } }
        public static Material Concrete { get { return Prim.Mat("Concrete", new Color(0.32f, 0.305f, 0.29f), 0f, 0.16f); } }
        public static Material ConcreteDark { get { return Prim.Mat("ConcreteDark", new Color(0.22f, 0.215f, 0.205f), 0f, 0.14f); } }
        public static Material SafetyLine { get { return Prim.Mat("SafetyLine", new Color(0.86f, 0.70f, 0.14f), 0f, 0.25f); } }
        public static Material Steel { get { return Prim.Mat("Steel", new Color(0.26f, 0.27f, 0.29f), 0.70f, 0.42f); } }
        public static Material RoofPanel { get { return Prim.Mat("RoofPanel", new Color(0.185f, 0.195f, 0.215f), 0.25f, 0.28f); } }
        public static Material Timber { get { return Prim.Mat("Timber", new Color(0.32f, 0.20f, 0.115f), 0f, 0.18f); } }
        public static Material Brick { get { return Prim.Mat("Brick", new Color(0.355f, 0.225f, 0.185f), 0f, 0.11f); } }
        public static Material Earth { get { return Prim.Mat("Earth", new Color(0.135f, 0.125f, 0.105f), 0f, 0.06f); } }
        public static Material Silhouette { get { return Prim.Mat("Silhouette", new Color(0.05f, 0.05f, 0.065f), 0f, 0.05f); } }
        public static Material Foliage { get { return Prim.Mat("Foliage", new Color(0.055f, 0.07f, 0.05f), 0f, 0.07f); } }

        public static Material LitWindow
        {
            get { return Prim.Emissive("LitWindow", new Color(0.92f, 0.84f, 0.66f), new Color(1f, 0.70f, 0.34f) * 1.35f); }
        }

        public static Material LampGlass
        {
            get { return Prim.Emissive("LampGlass", new Color(1f, 0.92f, 0.76f), new Color(1f, 0.76f, 0.42f) * 2.3f); }
        }

        public static Material SignalLens
        {
            get { return Prim.Emissive("SignalLens", new Color(0.2f, 0.2f, 0.2f), Color.black, 0.85f); }
        }

        // -------------------------------------------------------------------- environment

        /// <summary>Dusk lighting, fog and sky. The single biggest lever on how this looks.</summary>
        public static Light BuildEnvironment()
        {
            // Reuse the asset if it is already there. Recreating it on every scene build would
            // hand out a fresh GUID and leave the scenes built earlier in the run with no sky.
            const string skyPath = Prim.MaterialDir + "/Sky_Dusk.mat";
            var sky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            bool newSky = sky == null;
            if (newSky) sky = new Material(Shader.Find("Skybox/Procedural"));

            sky.name = "Sky_Dusk";
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 3f);
            sky.SetFloat("_AtmosphereThickness", 1.72f);
            sky.SetColor("_SkyTint", new Color(0.40f, 0.44f, 0.60f));
            sky.SetColor("_GroundColor", new Color(0.145f, 0.135f, 0.155f));
            sky.SetFloat("_Exposure", 0.92f);

            if (newSky) UnityEditor.AssetDatabase.CreateAsset(sky, skyPath);
            else UnityEditor.EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // Lifted a little. The waiting scene looks away from the sun, so it is lit almost
            // entirely by ambient, and the earlier values crushed its foreground to black while
            // the two sunlit shots looked fine.
            RenderSettings.ambientSkyColor = new Color(0.37f, 0.42f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.31f, 0.28f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.125f, 0.13f);

            // Fog does most of the work here: it hides where the world stops, separates the
            // train from the background as it recedes, and catches the low sun.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.315f, 0.30f, 0.355f);
            RenderSettings.fogDensity = 0.0072f;

            var go = new GameObject("Sun (dusk)");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.71f, 0.46f);
            // Pulled back from 1.25: with an ACES tonemapper now doing the highlight roll-off,
            // the old value clipped the carriage roofs to flat white.
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.shadowBias = 0.02f;

            // Low and raking straight down the platform, so everything throws a long shadow.
            go.transform.rotation = Quaternion.Euler(7.5f, -118f, 0f);
            RenderSettings.sun = sun;

            return sun;
        }

        public static void BuildGround(Transform root)
        {
            Prim.Box(root, "Ground", new Vector3(0f, -0.15f, -6f), new Vector3(TrackLength + 200f, 0.3f, 420f), Earth);
        }

        // -------------------------------------------------------------------------- track

        /// <summary>
        /// One running line. Sleepers are only laid across the stretch the fog lets you see —
        /// laying them the full length would be several thousand invisible objects.
        /// </summary>
        public static void LayTrack(Transform parent, string name, float z, float sleeperFrom, float sleeperTo)
        {
            var root = Prim.Empty(parent, name).transform;

            Prim.Box(root, "Ballast", new Vector3(0f, 0.11f, z), new Vector3(TrackLength, 0.32f, 5.0f), Ballast);

            var sleepers = Prim.Empty(root, "Sleepers").transform;
            for (float x = sleeperFrom; x <= sleeperTo; x += 1.55f)
            {
                Prim.Box(sleepers, "Sleeper", new Vector3(x, 0.34f, z), new Vector3(0.27f, 0.17f, 3.0f), Sleeper);
            }

            var rails = Prim.Empty(root, "Rails").transform;
            for (int side = -1; side <= 1; side += 2)
            {
                Prim.Box(rails, "Rail", new Vector3(0f, RailTopY - 0.07f, z + side * RailGauge * 0.5f),
                         new Vector3(TrackLength, 0.14f, 0.13f), Rail);
            }
        }

        // ----------------------------------------------------------------------- platform

        public static void BuildPlatform(Transform parent)
        {
            var root = Prim.Empty(parent, "Platform").transform;

            float len = PlatformXMax - PlatformXMin;
            float cx = (PlatformXMax + PlatformXMin) * 0.5f;
            float wid = PlatformBackZ - PlatformEdgeZ;
            float cz = (PlatformBackZ + PlatformEdgeZ) * 0.5f;

            Prim.Box(root, "Slab", new Vector3(cx, PlatformTopY * 0.5f, cz),
                     new Vector3(len, PlatformTopY, wid), Concrete);

            Prim.Box(root, "EdgeStone", new Vector3(cx, PlatformTopY - 0.03f, PlatformEdgeZ + 0.3f),
                     new Vector3(len, 0.06f, 0.6f), ConcreteDark);

            Prim.Box(root, "SafetyLine", new Vector3(cx, PlatformTopY + 0.01f, PlatformEdgeZ + 0.95f),
                     new Vector3(len, 0.02f, 0.22f), SafetyLine);

            BuildCanopy(root, cx, len);
            BuildFurniture(root);
        }

        static void BuildCanopy(Transform root, float cx, float len)
        {
            var canopy = Prim.Empty(root, "Canopy").transform;

            const float roofY = 6.1f;
            const float frontZ = 4.2f;
            const float backZ = 11.6f;

            float from = PlatformXMin + 6f;
            float to = PlatformXMax - 6f;

            for (float x = from; x <= to; x += 9f)
            {
                foreach (float z in new[] { frontZ, backZ })
                {
                    Prim.Cyl(canopy, "Pillar", new Vector3(x, PlatformTopY + (roofY - PlatformTopY) * 0.5f, z),
                             0.13f, roofY - PlatformTopY, Prim.AxisY, Steel);

                    // Bracket where the pillar meets the roof; cheap, but it stops the canopy
                    // looking like it is balanced on sticks.
                    Prim.Box(canopy, "Bracket", new Vector3(x, roofY - 0.45f, z),
                             new Vector3(0.1f, 0.7f, 0.7f), new Vector3(45f, 0f, 0f), Steel);
                }
            }

            Prim.Box(canopy, "Roof", new Vector3(cx, roofY + 0.12f, (frontZ + backZ) * 0.5f),
                     new Vector3(len - 10f, 0.22f, backZ - frontZ + 2.6f), RoofPanel);

            // Valance along the platform edge: the scalloped board every station canopy has.
            Prim.Box(canopy, "ValanceFront", new Vector3(cx, roofY - 0.28f, frontZ - 1.25f),
                     new Vector3(len - 10f, 0.55f, 0.08f), Timber);
            Prim.Box(canopy, "ValanceBack", new Vector3(cx, roofY - 0.28f, backZ + 1.25f),
                     new Vector3(len - 10f, 0.55f, 0.08f), Timber);

            BuildLamps(canopy, from, to, roofY);
        }

        static void BuildLamps(Transform canopy, float from, float to, float roofY)
        {
            var lamps = Prim.Empty(canopy, "Lamps").transform;

            for (float x = from + 4.5f; x <= to; x += 13.5f)
            {
                var lamp = Prim.Empty(lamps, "Lamp", new Vector3(x, roofY - 0.35f, 7.9f)).transform;

                Prim.Cyl(lamp, "Stem", new Vector3(0f, -0.2f, 0f), 0.04f, 0.4f, Prim.AxisY, Steel);
                Prim.Cyl(lamp, "Shade", new Vector3(0f, -0.48f, 0f), 0.34f, 0.16f, Prim.AxisY, RoofPanel);
                Prim.Sphere(lamp, "Bulb", new Vector3(0f, -0.58f, 0f), 0.22f, LampGlass);

                var lightGo = Prim.Empty(lamp, "Light", new Vector3(0f, -0.62f, 0f));
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.76f, 0.46f);
                light.intensity = 2.6f;
                light.range = 15f;

                // Only the sun casts shadows. A dozen shadow-casting point lights would cost a
                // great deal and add almost nothing at this scale.
                light.shadows = LightShadows.None;
            }
        }

        static void BuildFurniture(Transform root)
        {
            var props = Prim.Empty(root, "Furniture").transform;

            for (float x = PlatformXMin + 14f; x < PlatformXMax - 8f; x += 22f)
            {
                Bench(props, new Vector3(x, PlatformTopY, 10.6f));
            }

            // Running-in boards: the station name, twice, facing the train.
            for (float x = -30f; x <= 20f; x += 50f)
            {
                NameBoard(props, new Vector3(x, PlatformTopY + 2.35f, 12.2f), "ASHFORD  HILL");
            }

            // Luggage waiting to be loaded, near where the passengers will queue.
            Trolley(props, new Vector3(6.5f, PlatformTopY, 9.4f));
        }

        static void Bench(Transform parent, Vector3 basePos)
        {
            var b = Prim.Empty(parent, "Bench", basePos).transform;

            for (int side = -1; side <= 1; side += 2)
            {
                Prim.Box(b, "Leg", new Vector3(side * 0.85f, 0.22f, 0f), new Vector3(0.1f, 0.44f, 0.5f), Steel);
            }

            Prim.Box(b, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(2.0f, 0.07f, 0.52f), Timber);
            Prim.Box(b, "Back", new Vector3(0f, 0.72f, 0.24f), new Vector3(2.0f, 0.45f, 0.07f), Timber);
        }

        static void NameBoard(Transform parent, Vector3 pos, string text)
        {
            var board = Prim.Empty(parent, "NameBoard", pos).transform;

            Prim.Box(board, "Panel", Vector3.zero, new Vector3(6.2f, 1.0f, 0.09f), Concrete);
            Prim.Box(board, "Frame", new Vector3(0f, 0f, -0.06f), new Vector3(6.4f, 1.2f, 0.05f), Steel);

            for (int side = -1; side <= 1; side += 2)
            {
                Prim.Cyl(board, "Post", new Vector3(side * 2.4f, -1.35f, 0f), 0.07f, 2.6f, Prim.AxisY, Steel);
            }

            // A TextMesh reads correctly when viewed from its local -Z side, so identity rotation
            // is what faces it at a camera standing between the board and the track. Rotating it
            // 180 to "turn it round" is what produced mirrored lettering.
            Prim.Text3D(board, "Text", new Vector3(0f, 0f, -0.09f), Vector3.zero,
                        text, 0.42f, new Color(0.10f, 0.10f, 0.13f));
        }

        static void Trolley(Transform parent, Vector3 basePos)
        {
            var t = Prim.Empty(parent, "Trolley", basePos).transform;

            Prim.Box(t, "Deck", new Vector3(0f, 0.42f, 0f), new Vector3(1.7f, 0.09f, 0.9f), Timber);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Prim.Cyl(t, "Wheel", new Vector3(sx * 0.66f, 0.19f, sz * 0.36f),
                             0.19f, 0.08f, Prim.AxisZ, Steel);
                }
            }

            Prim.Box(t, "Case", new Vector3(-0.3f, 0.66f, 0f), new Vector3(0.75f, 0.42f, 0.5f), Timber);
            Prim.Box(t, "Case2", new Vector3(0.35f, 0.60f, 0.05f), new Vector3(0.6f, 0.3f, 0.44f), Brick);
        }

        // ---------------------------------------------------------------------- buildings

        public static void BuildStationBuilding(Transform parent)
        {
            var root = Prim.Empty(parent, "StationBuilding").transform;

            const float z = 19f;
            Prim.Box(root, "Body", new Vector3(-9f, 4f, z), new Vector3(30f, 8f, 12f), Brick);
            Prim.Box(root, "Roof", new Vector3(-9f, 8.3f, z), new Vector3(31.5f, 0.6f, 13.5f), RoofPanel);
            Prim.Box(root, "Chimney", new Vector3(-19f, 10f, z + 2f), new Vector3(1.4f, 3.6f, 1.4f), Brick);

            // Lit windows facing the platform. These plus the carriage windows are what make
            // dusk read as dusk rather than as an underexposed afternoon.
            var windows = Prim.Empty(root, "Windows").transform;
            for (float x = -22f; x <= 4f; x += 4.4f)
            {
                Prim.Box(windows, "Window", new Vector3(x, 3.1f, z - 6.05f), new Vector3(1.7f, 2.3f, 0.12f), LitWindow);
                Prim.Box(windows, "WindowUpper", new Vector3(x, 6.3f, z - 6.05f), new Vector3(1.5f, 1.5f, 0.12f), LitWindow);
            }

            // Doorway onto the platform, with light spilling out of it.
            Prim.Box(root, "Doorway", new Vector3(-9f, 2.2f, z - 6.1f), new Vector3(2.6f, 4.4f, 0.15f), LitWindow);

            var spill = Prim.Empty(root, "DoorSpill", new Vector3(-9f, 2.6f, z - 7.4f));
            var light = spill.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.40f);
            light.intensity = 3.4f;
            light.range = 18f;
            light.shadows = LightShadows.None;

            // Station clock.
            Prim.Cyl(root, "ClockFace", new Vector3(-9f, 6.6f, z - 6.15f), 0.85f, 0.14f, Prim.AxisZ, LitWindow);
            Prim.Box(root, "ClockHandH", new Vector3(-9f, 6.75f, z - 6.25f), new Vector3(0.09f, 0.42f, 0.03f),
                     new Vector3(0f, 0f, 28f), Steel);
            Prim.Box(root, "ClockHandM", new Vector3(-9f, 6.72f, z - 6.25f), new Vector3(0.07f, 0.62f, 0.03f),
                     new Vector3(0f, 0f, -66f), Steel);
        }

        // ------------------------------------------------------------------------ signals

        /// <summary>A colour-light signal beside the given track, returning its controller.</summary>
        public static SignalLight BuildSignal(Transform parent, Vector3 basePos, string name = "Signal")
        {
            var root = Prim.Empty(parent, name, basePos).transform;

            Prim.Cyl(root, "Post", new Vector3(0f, 2.6f, 0f), 0.13f, 5.2f, Prim.AxisY, Steel);
            Prim.Box(root, "Head", new Vector3(0f, 5.5f, 0f), new Vector3(0.7f, 1.5f, 0.5f), Steel);
            Prim.Box(root, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.7f), ConcreteDark);

            // Separate material instances so the two lenses light independently.
            var redMat = Prim.Emissive("SignalLensRed", new Color(0.24f, 0.05f, 0.04f), Color.black, 0.85f);
            var greenMat = Prim.Emissive("SignalLensGreen", new Color(0.05f, 0.22f, 0.09f), Color.black, 0.85f);

            var red = Prim.Cyl(root, "RedLens", new Vector3(0f, 5.92f, -0.28f), 0.21f, 0.1f, Prim.AxisZ, redMat);
            var green = Prim.Cyl(root, "GreenLens", new Vector3(0f, 5.14f, -0.28f), 0.21f, 0.1f, Prim.AxisZ, greenMat);

            var redGlowGo = Prim.Empty(root, "RedGlow", new Vector3(0f, 5.92f, -0.7f));
            var redGlow = redGlowGo.AddComponent<Light>();
            redGlow.type = LightType.Point;
            redGlow.range = 9f;
            redGlow.shadows = LightShadows.None;

            var greenGlowGo = Prim.Empty(root, "GreenGlow", new Vector3(0f, 5.14f, -0.7f));
            var greenGlow = greenGlowGo.AddComponent<Light>();
            greenGlow.type = LightType.Point;
            greenGlow.range = 9f;
            greenGlow.shadows = LightShadows.None;

            var sig = root.gameObject.AddComponent<SignalLight>();
            sig.redLens = red.GetComponent<Renderer>();
            sig.greenLens = green.GetComponent<Renderer>();
            sig.redGlow = redGlow;
            sig.greenGlow = greenGlow;

            return sig;
        }

        // ------------------------------------------------------------------------ scenery

        /// <summary>
        /// Depth cues. None of this is looked at directly; it exists so the fog has something
        /// to eat, which is what sells distance.
        /// </summary>
        public static void BuildScenery(Transform parent)
        {
            var root = Prim.Empty(parent, "Scenery").transform;

            var poles = Prim.Empty(root, "TelegraphPoles").transform;
            for (float x = -180f; x <= 320f; x += 32f)
            {
                var p = Prim.Empty(poles, "Pole", new Vector3(x, 0f, -13.5f)).transform;
                Prim.Cyl(p, "Trunk", new Vector3(0f, 4.2f, 0f), 0.16f, 8.4f, Prim.AxisY, Timber);
                Prim.Box(p, "ArmUpper", new Vector3(0f, 7.9f, 0f), new Vector3(0.09f, 0.09f, 2.0f), Timber);
                Prim.Box(p, "ArmLower", new Vector3(0f, 7.25f, 0f), new Vector3(0.09f, 0.09f, 1.5f), Timber);
            }

            var trees = Prim.Empty(root, "Trees").transform;
            var rng = new System.Random(20260906);
            for (int i = 0; i < 46; i++)
            {
                float x = -220f + (float)rng.NextDouble() * 560f;
                float z = -34f - (float)rng.NextDouble() * 46f;
                float h = 5.5f + (float)rng.NextDouble() * 7f;

                var t = Prim.Empty(trees, "Tree", new Vector3(x, 0f, z)).transform;
                Prim.Cyl(t, "Trunk", new Vector3(0f, h * 0.3f, 0f), 0.28f, h * 0.6f, Prim.AxisY, Silhouette);
                Prim.Capsule(t, "Canopy", new Vector3(0f, h * 0.78f, 0f), h * 0.62f, h * 0.85f, Prim.AxisY, Foliage);
            }

            // Town roofline behind the station, deep in the fog.
            var town = Prim.Empty(root, "Town").transform;
            for (int i = 0; i < 34; i++)
            {
                float x = -200f + (float)rng.NextDouble() * 520f;
                float z = 42f + (float)rng.NextDouble() * 60f;
                float w = 8f + (float)rng.NextDouble() * 18f;
                float h = 6f + (float)rng.NextDouble() * 16f;

                Prim.Box(town, "Block", new Vector3(x, h * 0.5f, z), new Vector3(w, h, w * 0.8f), Silhouette);
            }
        }

        /// <summary>
        /// The whole station in one call, minus trains and cameras. Every scene starts here.
        /// </summary>
        public static Transform BuildWorld(bool includeSignal, out SignalLight signal)
        {
            var world = new GameObject("World").transform;

            BuildEnvironment();
            BuildGround(world);

            // Sleepers only where the camera can see them through the fog.
            LayTrack(world, "TrackA_Platform", TrackAZ, -190f, 330f);
            LayTrack(world, "TrackB_Through", TrackBZ, -190f, 330f);

            BuildPlatform(world);
            BuildStationBuilding(world);
            BuildScenery(world);

            // Ahead of our train and inside the shot. At x = 64 it sat behind the camera in
            // the waiting scene, which rather defeated the point of having a signal at all.
            signal = includeSignal
                ? BuildSignal(world, new Vector3(46f, 0f, -3.4f), "Signal_Ahead")
                : null;

            return world;
        }
    }
}
