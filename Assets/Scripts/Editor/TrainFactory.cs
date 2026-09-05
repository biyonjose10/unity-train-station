using System.Collections.Generic;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>What a builder gets back after making a train, so it can wire up scripts.</summary>
    public class TrainRig
    {
        public Transform root;
        public readonly List<TrainWheel> wheels = new List<TrainWheel>();
        public ParticleSystem stack;
        public Transform nose;
        public Transform tail;
        /// <summary>Platform-side doorways, in the order they run down the train.</summary>
        public readonly List<Transform> doors = new List<Transform>();
        /// <summary>The hinged door panels for those doorways, in the same order.</summary>
        public readonly List<CarriageDoor> doorPanels = new List<CarriageDoor>();
    }

    /// <summary>How one train should look. Two trains share the shape and differ in livery.</summary>
    public class TrainSpec
    {
        public string name = "Train";
        public int carriages = 3;
        public Color locoBody = new Color(0.105f, 0.135f, 0.115f);
        public Color carriageBody = new Color(0.30f, 0.10f, 0.085f);
        public Color lining = new Color(0.72f, 0.60f, 0.26f);
        public bool litWindows = true;
        public bool headlight = true;
    }

    /// <summary>
    /// Builds a steam locomotive, tender and carriages out of primitives.
    ///
    /// The train is built nose-first at local X = 0 and runs backwards down -X, so a builder can
    /// place the whole rig by its nose and know exactly where the front buffer beam is.
    /// </summary>
    public static class TrainFactory
    {
        public const float DrivingWheelRadius = 0.95f;
        public const float CarriageWheelRadius = 0.55f;
        const float RailTop = StationKit.RailTopY;

        public static TrainRig Build(Transform parent, TrainSpec spec)
        {
            var rig = new TrainRig();
            rig.root = Prim.Empty(parent, spec.name).transform;

            // Low metallic and gloss on purpose: at 0.42 smoothness the flat cab side acted as a
            // mirror for the low sun and blew the whole cab out to white.
            var loco = Prim.Mat(spec.name + "_Loco", spec.locoBody, 0.08f, 0.20f);
            var coach = Prim.Mat(spec.name + "_Coach", spec.carriageBody, 0.1f, 0.34f);
            var lining = Prim.Mat(spec.name + "_Lining", spec.lining, 0.6f, 0.55f);
            var black = Prim.Mat("LocoBlack", new Color(0.055f, 0.055f, 0.06f), 0.2f, 0.30f);
            var iron = Prim.Mat("Ironwork", new Color(0.16f, 0.16f, 0.17f), 0.55f, 0.35f);
            var brass = Prim.Mat("Brass", new Color(0.62f, 0.47f, 0.18f), 0.8f, 0.68f);
            var roof = Prim.Mat("CoachRoof", new Color(0.20f, 0.20f, 0.21f), 0.15f, 0.22f);

            var glass = spec.litWindows
                ? Prim.Emissive(spec.name + "_Window", new Color(0.92f, 0.85f, 0.68f),
                                new Color(1f, 0.72f, 0.36f) * 1.25f)
                : Prim.Mat(spec.name + "_WindowDark", new Color(0.06f, 0.07f, 0.09f), 0.1f, 0.85f);

            float cursor = 0f;
            cursor = BuildLocomotive(rig, spec, cursor, loco, black, iron, brass, glass);
            cursor -= 0.55f;
            cursor = BuildTender(rig, spec, cursor, loco, black, iron);

            for (int i = 0; i < spec.carriages; i++)
            {
                cursor -= 0.85f;
                bool last = (i == spec.carriages - 1);
                cursor = BuildCarriage(rig, spec, cursor, i, last, coach, lining, roof, iron, glass);
            }

            rig.tail = Prim.Empty(rig.root, "Tail", new Vector3(cursor, 3f, 0f)).transform;
            return rig;
        }

        // -------------------------------------------------------------------- locomotive

        static float BuildLocomotive(TrainRig rig, TrainSpec spec, float front,
                                     Material body, Material black, Material iron,
                                     Material brass, Material glass)
        {
            var loco = Prim.Empty(rig.root, "Locomotive").transform;

            const float bodyY = 3.15f;
            const float footplateY = 2.05f;

            rig.nose = Prim.Empty(loco, "Nose", new Vector3(front, 2.4f, 0f)).transform;

            // Buffer beam and buffers.
            Prim.Box(loco, "BufferBeam", new Vector3(front - 0.14f, 1.95f, 0f),
                     new Vector3(0.28f, 1.0f, 3.0f), Prim.Mat("BufferBeam", new Color(0.45f, 0.06f, 0.05f), 0.1f, 0.3f));
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Cyl(loco, "Buffer", new Vector3(front - 0.42f, 1.95f, s * 1.0f),
                         0.22f, 0.55f, Prim.AxisX, iron);
            }

            // Smokebox, boiler, firebox: one barrel with a fatter front.
            float smokeboxLen = 2.4f;
            float smokeboxCentre = front - 0.55f - smokeboxLen * 0.5f;
            Prim.Cyl(loco, "Smokebox", new Vector3(smokeboxCentre, bodyY, 0f),
                     1.16f, smokeboxLen, Prim.AxisX, black);
            Prim.Cyl(loco, "SmokeboxDoor", new Vector3(front - 0.5f, bodyY, 0f),
                     1.10f, 0.16f, Prim.AxisX, black);
            Prim.Sphere(loco, "DoorDart", new Vector3(front - 0.58f, bodyY, 0f), 0.26f, brass);

            float boilerFront = front - 0.55f - smokeboxLen;
            float boilerLen = 6.0f;
            Prim.Cyl(loco, "Boiler", new Vector3(boilerFront - boilerLen * 0.5f, bodyY, 0f),
                     1.06f, boilerLen, Prim.AxisX, body);

            // Boiler bands.
            for (int i = 1; i <= 3; i++)
            {
                Prim.Cyl(loco, "Band", new Vector3(boilerFront - boilerLen * i / 4f, bodyY, 0f),
                         1.08f, 0.1f, Prim.AxisX, brass);
            }

            // Chimney on the smokebox, and the domes along the barrel.
            var chimneyBase = new Vector3(smokeboxCentre + 0.35f, bodyY + 1.05f, 0f);
            Prim.Cyl(loco, "Chimney", chimneyBase + new Vector3(0f, 0.62f, 0f), 0.32f, 1.25f, Prim.AxisY, black);
            Prim.Cyl(loco, "ChimneyCap", chimneyBase + new Vector3(0f, 1.26f, 0f), 0.40f, 0.22f, Prim.AxisY, black);

            Prim.Cyl(loco, "SteamDome", new Vector3(boilerFront - 1.9f, bodyY + 0.95f, 0f),
                     0.48f, 0.72f, Prim.AxisY, body);
            Prim.Sphere(loco, "SteamDomeTop", new Vector3(boilerFront - 1.9f, bodyY + 1.28f, 0f), 0.94f, body);
            Prim.Cyl(loco, "SafetyValve", new Vector3(boilerFront - 3.8f, bodyY + 1.0f, 0f),
                     0.26f, 0.5f, Prim.AxisY, brass);

            // Footplate and splashers.
            float rearOfBoiler = boilerFront - boilerLen;
            Prim.Box(loco, "Footplate", new Vector3((front + rearOfBoiler) * 0.5f, footplateY, 0f),
                     new Vector3(front - rearOfBoiler, 0.14f, 2.9f), iron);
            Prim.Box(loco, "HandrailL", new Vector3((front + rearOfBoiler) * 0.5f, footplateY + 0.5f, 1.42f),
                     new Vector3(front - rearOfBoiler, 0.05f, 0.05f), brass);
            Prim.Box(loco, "HandrailR", new Vector3((front + rearOfBoiler) * 0.5f, footplateY + 0.5f, -1.42f),
                     new Vector3(front - rearOfBoiler, 0.05f, 0.05f), brass);

            // Cab.
            float cabLen = 3.3f;
            float cabCentre = rearOfBoiler - cabLen * 0.5f;
            Prim.Box(loco, "Cab", new Vector3(cabCentre, footplateY + 1.6f, 0f),
                     new Vector3(cabLen, 3.2f, 3.0f), body);
            Prim.Box(loco, "CabRoof", new Vector3(cabCentre, footplateY + 3.28f, 0f),
                     new Vector3(cabLen + 0.35f, 0.16f, 3.3f), black);

            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Box(loco, "CabWindow", new Vector3(cabCentre + 1.0f, footplateY + 2.35f, s * 1.53f),
                         new Vector3(0.9f, 0.85f, 0.08f), glass);
            }

            // Firebox glow, hung below the footplate between the frames so it spills onto the
            // track and the motion.
            //
            // It used to sit inside the cab at range 9 with shadows off, and an unshadowed point
            // light does not care about walls: it lit the cab sides from within at point-blank
            // range and blew the whole cab out to white. Keep it small, keep it outside.
            var fire = Prim.Empty(loco, "FireboxGlow", new Vector3(cabCentre + 0.7f, footplateY - 0.55f, 0f));
            var fireLight = fire.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.42f, 0.11f);
            fireLight.intensity = 1.7f;
            fireLight.range = 3.4f;
            fireLight.shadows = LightShadows.None;

            // Cylinders at the front, outside the frames.
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Cyl(loco, "Cylinder", new Vector3(front - 2.1f, 1.35f, s * 1.35f),
                         0.44f, 1.5f, Prim.AxisX, black);
            }

            // Wheels: a leading pony truck and three pairs of drivers.
            AddAxle(rig, loco, "Pony", front - 2.0f, 0.5f, iron, brass);
            AddAxle(rig, loco, "Driver1", front - 4.6f, DrivingWheelRadius, black, brass);
            AddAxle(rig, loco, "Driver2", front - 7.0f, DrivingWheelRadius, black, brass);
            AddAxle(rig, loco, "Driver3", front - 9.4f, DrivingWheelRadius, black, brass);

            if (spec.headlight)
            {
                Prim.Cyl(loco, "LampBody", new Vector3(front - 0.32f, bodyY + 1.05f, 0f),
                         0.22f, 0.32f, Prim.AxisX, black);

                var lampGo = Prim.Empty(loco, "Headlight", new Vector3(front + 0.1f, bodyY + 1.05f, 0f));
                lampGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Spot;
                lamp.color = new Color(1f, 0.90f, 0.72f);
                lamp.intensity = 5.5f;
                lamp.range = 120f;
                lamp.spotAngle = 34f;
                lamp.shadows = LightShadows.None;

                Prim.Sphere(loco, "LampLens", new Vector3(front - 0.16f, bodyY + 1.05f, 0f), 0.2f,
                            Prim.Emissive("HeadlampLens", Color.white, new Color(1f, 0.92f, 0.75f) * 4f));
            }

            rig.stack = BuildSteam(loco, chimneyBase + new Vector3(0f, 1.35f, 0f));

            return rearOfBoiler - cabLen;
        }

        // ------------------------------------------------------------------------ tender

        static float BuildTender(TrainRig rig, TrainSpec spec, float front,
                                 Material body, Material black, Material iron)
        {
            var t = Prim.Empty(rig.root, "Tender").transform;

            const float len = 6.4f;
            float centre = front - len * 0.5f;

            Prim.Box(t, "Frame", new Vector3(centre, 1.75f, 0f), new Vector3(len, 0.5f, 2.9f), iron);
            Prim.Box(t, "Body", new Vector3(centre, 2.85f, 0f), new Vector3(len, 1.8f, 3.0f), body);

            // Coal, heaped and uneven.
            var rng = new System.Random(88);
            for (int i = 0; i < 16; i++)
            {
                float x = centre - len * 0.32f + (float)rng.NextDouble() * len * 0.64f;
                float z = -0.95f + (float)rng.NextDouble() * 1.9f;
                float s = 0.28f + (float)rng.NextDouble() * 0.42f;
                Prim.Sphere(t, "Coal", new Vector3(x, 3.72f + (float)rng.NextDouble() * 0.2f, z), s, black);
            }

            AddAxle(rig, t, "TenderAxle1", front - 1.6f, CarriageWheelRadius, black, iron);
            AddAxle(rig, t, "TenderAxle2", front - 3.2f, CarriageWheelRadius, black, iron);
            AddAxle(rig, t, "TenderAxle3", front - 4.8f, CarriageWheelRadius, black, iron);

            return front - len;
        }

        // --------------------------------------------------------------------- carriages

        static float BuildCarriage(TrainRig rig, TrainSpec spec, float front, int index, bool isLast,
                                   Material body, Material lining, Material roofMat,
                                   Material iron, Material glass)
        {
            var c = Prim.Empty(rig.root, "Carriage" + (index + 1)).transform;

            // Doors are the body colour knocked back a shade, not the lining colour. Painting a
            // whole door in lining gold made them read as decorative panels rather than doors.
            var doorMat = Prim.Mat(spec.name + "_Door", spec.carriageBody * 0.78f, 0.1f, 0.30f);
            var recessMat = Prim.Mat("DoorRecess", new Color(0.045f, 0.04f, 0.035f), 0f, 0.1f);

            const float len = 17f;
            const float floorY = 1.55f;
            const float bodyH = 2.9f;
            float centre = front - len * 0.5f;
            float bodyY = floorY + bodyH * 0.5f;

            Prim.Box(c, "Underframe", new Vector3(centre, floorY - 0.18f, 0f),
                     new Vector3(len, 0.3f, 2.7f), iron);
            Prim.Box(c, "Body", new Vector3(centre, bodyY, 0f), new Vector3(len, bodyH, 2.95f), body);

            // Arc roof, faked with a slightly narrower slab on top.
            Prim.Box(c, "Roof", new Vector3(centre, floorY + bodyH + 0.16f, 0f),
                     new Vector3(len, 0.32f, 2.75f), roofMat);
            Prim.Box(c, "RoofEdge", new Vector3(centre, floorY + bodyH + 0.02f, 0f),
                     new Vector3(len, 0.1f, 3.0f), roofMat);

            // Waist lining, the stripe that stops a coach reading as a plain box.
            Prim.Box(c, "LiningUpper", new Vector3(centre, floorY + bodyH - 0.55f, 1.49f),
                     new Vector3(len, 0.07f, 0.04f), lining);
            Prim.Box(c, "LiningLower", new Vector3(centre, floorY + 0.5f, 1.49f),
                     new Vector3(len, 0.07f, 0.04f), lining);
            Prim.Box(c, "LiningUpperFar", new Vector3(centre, floorY + bodyH - 0.55f, -1.49f),
                     new Vector3(len, 0.07f, 0.04f), lining);
            Prim.Box(c, "LiningLowerFar", new Vector3(centre, floorY + 0.5f, -1.49f),
                     new Vector3(len, 0.07f, 0.04f), lining);

            // Compartments: alternating door, window, window down the side.
            float compartment = len / 6f;
            for (int i = 0; i < 6; i++)
            {
                float x = front - compartment * (i + 0.5f);

                bool isDoor = (i % 2 == 0);

                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * 1.50f;

                    if (isDoor)
                    {
                        // The doorway itself: a dark recess, so an open door reveals a hole in the
                        // side of the coach rather than the coach's own paintwork.
                        Prim.Box(c, "DoorRecess", new Vector3(x, floorY + 1.35f, z - s * 0.06f),
                                 new Vector3(1.02f, 2.46f, 0.06f), recessMat);

                        // Hinged at the leading edge so it swings out over the platform.
                        var hinge = Prim.Empty(c, "DoorHinge",
                                               new Vector3(x + 0.525f, floorY + 1.35f, z)).transform;

                        Prim.Box(hinge, "Door", new Vector3(-0.525f, 0f, 0f),
                                 new Vector3(1.05f, 2.5f, 0.07f), doorMat);
                        Prim.Box(hinge, "DoorEdge", new Vector3(-0.525f, -1.23f, s * 0.02f),
                                 new Vector3(1.05f, 0.05f, 0.05f), lining);
                        Prim.Box(hinge, "Droplight", new Vector3(-0.525f, 0.70f, s * 0.03f),
                                 new Vector3(0.82f, 0.85f, 0.06f), glass);
                        Prim.Box(hinge, "DoorHandle", new Vector3(-0.13f, 0f, s * 0.06f),
                                 new Vector3(0.06f, 0.22f, 0.06f), lining);

                        var panel = hinge.gameObject.AddComponent<CarriageDoor>();
                        // The far side hinges the other way, purely so both sides look right if
                        // anything ever opens them.
                        panel.openAngle = s > 0 ? 82f : -82f;

                        // Only the platform side is boardable.
                        if (s > 0)
                        {
                            rig.doors.Add(Prim.Empty(c, "Doorway", new Vector3(x, floorY, z + 0.1f)).transform);
                            rig.doorPanels.Add(panel);
                        }
                    }
                    else
                    {
                        Prim.Box(c, "Window", new Vector3(x, floorY + 1.95f, z),
                                 new Vector3(1.25f, 1.05f, 0.06f), glass);
                    }
                }
            }

            // Interior light, so the windows glow from something rather than from an emissive map
            // alone. Deliberately weak and short-range: it is unshadowed, so anything it reaches
            // through the coach sides gets lit from behind — which was turning the open doors into
            // pale rectangles once they swung out into it. The emissive glass does the real work.
            var glow = Prim.Empty(c, "InteriorLight", new Vector3(centre, floorY + 1.5f, 0f));
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.76f, 0.45f);
            light.intensity = spec.litWindows ? 0.85f : 0f;
            light.range = 6.5f;
            light.shadows = LightShadows.None;

            // Bogies at the usual quarter points.
            AddAxle(rig, c, "BogieA1", front - 2.6f, CarriageWheelRadius, iron, iron);
            AddAxle(rig, c, "BogieA2", front - 4.2f, CarriageWheelRadius, iron, iron);
            AddAxle(rig, c, "BogieB1", front - len + 4.2f, CarriageWheelRadius, iron, iron);
            AddAxle(rig, c, "BogieB2", front - len + 2.6f, CarriageWheelRadius, iron, iron);

            if (!isLast)
            {
                // Corridor connection to the next vehicle. The last carriage must not get one,
                // or the back of the train is a black slab hanging in mid air.
                Prim.Box(c, "Gangway", new Vector3(front - len - 0.42f, floorY + 1.3f, 0f),
                         new Vector3(0.85f, 2.1f, 1.6f), roofMat);
            }
            else
            {
                BuildTailEnd(rig, c, front - len, floorY, bodyH, body, lining, iron, glass);
            }

            return front - len;
        }

        /// <summary>
        /// The back of the train, which in the departure scene is the thing the camera watches for
        /// twenty seconds — so it gets an end window, a buffer beam and a red tail lamp rather
        /// than the blank end of a box.
        /// </summary>
        static void BuildTailEnd(TrainRig rig, Transform c, float rear, float floorY, float bodyH,
                                 Material body, Material lining, Material iron, Material glass)
        {
            var tail = Prim.Empty(c, "TailEnd").transform;

            Prim.Box(tail, "EndPanel", new Vector3(rear - 0.09f, floorY + bodyH * 0.5f, 0f),
                     new Vector3(0.18f, bodyH, 2.95f), body);

            Prim.Box(tail, "EndWindow", new Vector3(rear - 0.20f, floorY + 1.95f, 0f),
                     new Vector3(0.08f, 1.0f, 1.5f), glass);

            Prim.Box(tail, "BufferBeam", new Vector3(rear - 0.28f, floorY - 0.15f, 0f),
                     new Vector3(0.24f, 0.85f, 2.9f),
                     Prim.Mat("BufferBeam", new Color(0.45f, 0.06f, 0.05f), 0.1f, 0.3f));

            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Cyl(tail, "Buffer", new Vector3(rear - 0.55f, floorY - 0.15f, s * 0.95f),
                         0.21f, 0.52f, Prim.AxisX, iron);
            }

            // Tail lamp: the one red light every train shows to the section behind it.
            Prim.Sphere(tail, "TailLampLens", new Vector3(rear - 0.34f, floorY + 0.55f, 0.75f), 0.26f,
                        Prim.Emissive("TailLamp", new Color(0.5f, 0.05f, 0.04f),
                                      new Color(1f, 0.09f, 0.05f) * 3.4f));

            var lampGo = Prim.Empty(tail, "TailLampGlow", new Vector3(rear - 0.7f, floorY + 0.55f, 0.75f));
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.14f, 0.08f);
            lamp.intensity = 2.2f;
            lamp.range = 7f;
            lamp.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------------ helpers

        /// <summary>One axle: a wheel each side, both registered with the rig.</summary>
        static void AddAxle(TrainRig rig, Transform parent, string name, float x, float radius,
                            Material tyre, Material hub)
        {
            float y = RailTop + radius;

            for (int s = -1; s <= 1; s += 2)
            {
                var w = Prim.Wheel(parent, name + (s < 0 ? "_L" : "_R"),
                                   new Vector3(x, y, s * (StationKit.RailGauge * 0.5f + 0.06f)),
                                   radius, 0.16f, tyre, hub);

                rig.wheels.Add(w.GetComponent<TrainWheel>());
            }

            // Axle shaft between them, so the gap under the train is not see-through.
            Prim.Cyl(parent, name + "_Axle", new Vector3(x, y, 0f), 0.09f,
                     StationKit.RailGauge + 0.1f, Prim.AxisZ, hub);
        }

        /// <summary>Chimney exhaust. Emission rate and speed are driven at runtime by TrainMotion.</summary>
        static ParticleSystem BuildSteam(Transform parent, Vector3 localPos)
        {
            var go = Prim.Empty(parent, "ChimneySteam", localPos);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 4.6f;
            main.startSpeed = 2.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.97f, 0.97f, 0.99f, 0.9f),
                new Color(0.76f, 0.76f, 0.82f, 0.75f));
            main.gravityModifier = -0.05f;          // steam rises
            main.maxParticles = 700;
            // World space, or the plume would ride along with the train instead of being left behind.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.24f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // point the cone up

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.78f, 0.78f, 0.84f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var particleMat = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (particleMat != null) renderer.sharedMaterial = particleMat;
            renderer.sortingFudge = 12f;

            return ps;
        }
    }
}
