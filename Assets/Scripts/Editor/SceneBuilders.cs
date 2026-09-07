using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Builds the five scenes. Each one is generated from scratch, so the .unity files are
    /// disposable output: change the code, rebuild, and every scene picks the change up.
    /// </summary>
    public static class SceneBuilders
    {
        public const string SceneDir = "Assets/Scenes";

        public const string Bootstrap = "00_Bootstrap";
        public const string Arrival = "01_Arrival";
        public const string Waiting = "02_Waiting";
        public const string Boarding = "03_Boarding";
        public const string Departure = "04_Departure";

        public static readonly string[] All =
        {
            Bootstrap, Arrival, Waiting, Boarding, Departure
        };

        // ------------------------------------------------------------------------ liveries

        static TrainSpec OurTrain()
        {
            return new TrainSpec
            {
                name = "TrainA",
                carriages = 3,
                locoBody = new Color(0.085f, 0.115f, 0.098f),      // lined green
                carriageBody = new Color(0.315f, 0.105f, 0.085f),  // crimson lake
                lining = new Color(0.74f, 0.62f, 0.28f),
                litWindows = true,
                headlight = true
            };
        }

        static TrainSpec OtherTrain()
        {
            return new TrainSpec
            {
                name = "TrainB",
                carriages = 2,
                locoBody = new Color(0.10f, 0.10f, 0.115f),        // plain black
                carriageBody = new Color(0.16f, 0.20f, 0.30f),     // blue stock
                lining = new Color(0.60f, 0.60f, 0.63f),
                litWindows = true,
                headlight = true
            };
        }

        // ----------------------------------------------------------------------- bootstrap

        /// <summary>
        /// No geometry: just the flow manager, which survives every load and drives the film.
        /// </summary>
        public static void BuildBootstrap()
        {
            NewScene();

            var go = new GameObject("SceneFlow");
            var flow = go.AddComponent<SceneFlowManager>();
            flow.shots = new[]
            {
                new SceneFlowManager.Shot { sceneName = Arrival,   duration = 25f },
                new SceneFlowManager.Shot { sceneName = Waiting,   duration = 18f },
                new SceneFlowManager.Shot { sceneName = Boarding,  duration = 28f },
                new SceneFlowManager.Shot { sceneName = Departure, duration = 24f }
            };
            flow.fadeSeconds = 0.7f;
            flow.openOnBlack = 0.6f;
            flow.fadeOutAtEnd = true;

            // A camera so the bootstrap scene is not a black "no cameras rendering" error in the
            // half second before the first real scene loads.
            var camGo = new GameObject("Bootstrap Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            Save(Bootstrap);
        }

        // ------------------------------------------------------------------- 01 · arrival

        public static void BuildArrival()
        {
            NewScene();

            var w = StationKit.BuildWorld(true);
            w.signal.startsAtDanger = false;
            w.signal.clearsAt = -1f;

            var rig = TrainFactory.Build(w.trains, OurTrain());

            var motion = Motion(rig, TrainPhase.Arrive);
            motion.entrySpeed = 16f;
            motion.arriveTime = 13.5f;
            motion.startDelay = 1.5f;

            // Place the train so that braking from entrySpeed lands the nose exactly on the mark
            // rather than somewhere near it.
            PlaceTrain(rig, StationKit.StopMarkX - motion.ArrivalDistance, StationKit.TrackAZ);

            Audio(rig, motion, brakes: true, whistle: false, ambience: true);

            // Head-on: she comes out of the fog towards us and stops short.
            var cam = MakeCamera(w, new Vector3(37f, 2.8f, 3.2f), rig.nose, 40f);
            cam.GetComponent<CameraDolly>().lookDamping = 1.2f;
            cam.GetComponent<CameraDolly>().driftPerSecond = new Vector3(-0.14f, 0.01f, 0.05f);
            cam.GetComponent<CameraDolly>().driftDelay = 2f;
            cam.GetComponent<CameraDolly>().targetOffset = new Vector3(0f, 0.6f, 0f);

            w.guide.headline =
                "01 ARRIVAL  —  25 s\n" +
                "She enters at 16 m/s and brakes to a stand on the mark.";

            w.guide.markers = new[]
            {
                Mark("Nose starts here (off camera, in the fog)",
                     new Vector3(StationKit.StopMarkX - motion.ArrivalDistance, 0f, StationKit.TrackAZ),
                     GuideStart, 6f),
                Mark("STOP MARK — the nose comes to rest here, x = " + StationKit.StopMarkX,
                     new Vector3(StationKit.StopMarkX, 0f, StationKit.TrackAZ), GuideStop, 7f),
                CameraMark(cam, rig.nose.position)
            };

            Save(Arrival);
        }

        // ------------------------------------------------------------------- 02 · waiting

        public static void BuildWaiting()
        {
            NewScene();

            var w = StationKit.BuildWorld(true);
            w.signal.startsAtDanger = true;
            w.signal.clearsAt = 10.5f;

            // Ours, standing at the platform with the brakes on.
            var ours = TrainFactory.Build(w.trains, OurTrain());
            PlaceTrain(ours, StationKit.StopMarkX, StationKit.TrackAZ);
            var held = Motion(ours, TrainPhase.Hold);
            Audio(ours, held, brakes: false, whistle: false, ambience: true);

            // Theirs, on the through road, clearing the section.
            var theirs = TrainFactory.Build(w.trains, OtherTrain());
            PlaceTrain(theirs, 18f, StationKit.TrackBZ);

            var leaving = Motion(theirs, TrainPhase.Depart);
            leaving.dwellTime = 1.5f;
            leaving.accelTime = 13f;
            leaving.topSpeed = 19f;
            leaving.startDelay = 0f;
            Audio(theirs, leaving, brakes: false, whistle: true, ambience: false);

            // Wide, from beyond the platform end, so both trains are in shot and the other one
            // runs past us on its way out.
            var cam = MakeCamera(w, new Vector3(58f, 3.6f, 3.2f), ours.nose, 42f);
            var dolly = cam.GetComponent<CameraDolly>();
            dolly.lookDamping = 2.4f;
            dolly.driftPerSecond = new Vector3(-0.05f, 0.02f, 0.06f);
            dolly.driftDelay = 1f;
            dolly.targetOffset = new Vector3(0f, 0.8f, 0f);

            w.guide.headline =
                "02 WAITING  —  18 s\n" +
                "Ours stands at the platform. The signal is red until the other train is clear.";

            w.guide.markers = new[]
            {
                Mark("Our train, held here with the brakes on",
                     new Vector3(StationKit.StopMarkX, 0f, StationKit.TrackAZ), GuideStop, 7f),
                Mark("Signal — red, clears at t = " + w.signal.clearsAt + " s",
                     w.signal.transform.position, GuideNote, 7.5f),
                Mark("The other train starts here and runs out towards +X",
                     new Vector3(18f, 0f, StationKit.TrackBZ), GuideStart, 6f),
                CameraMark(cam, ours.nose.position)
            };

            Save(Waiting);
        }

        // ------------------------------------------------------------------ 03 · boarding

        public static void BuildBoarding()
        {
            NewScene();

            var w = StationKit.BuildWorld(true);
            w.signal.startsAtDanger = false;
            w.signal.clearsAt = -1f;

            var rig = TrainFactory.Build(w.trains, OurTrain());
            PlaceTrain(rig, StationKit.StopMarkX, StationKit.TrackAZ);

            var motion = Motion(rig, TrainPhase.Hold);
            var audio = Audio(rig, motion, brakes: false, whistle: false, ambience: true);

            var passengers = BuildCrowd(w, rig);

            // Everything that runs the scene rather than appearing in it goes here, so a viewer
            // can see at a glance that this scene is directed and the other three are not.
            w.direction = Prim.Empty(null, "08 DIRECTION").transform;

            var directorGo = Prim.Empty(w.direction, "BoardingDirector");
            var director = directorGo.AddComponent<BoardingDirector>();
            director.trainAudio = audio;
            director.passengers = passengers.ToArray();
            director.doors = rig.doorPanels.ToArray();
            director.chimeAt = 1.2f;
            director.doorsOpenAt = 0.4f;
            director.doorsCloseFrom = 20.5f;
            director.guardWhistleAt = 25.5f;

            // Down the platform at head height, so the doors and the queue are both in shot.
            var focus = Prim.Empty(w.direction, "BoardingFocus", new Vector3(-15f, 2.1f, 3.2f));

            var cam = MakeCamera(w, new Vector3(6f, 2.95f, 10.2f), focus.transform, 46f);
            var dolly = cam.GetComponent<CameraDolly>();
            dolly.lookDamping = 2.8f;
            dolly.driftPerSecond = new Vector3(-0.14f, 0.005f, -0.02f);
            dolly.driftDelay = 1.5f;
            dolly.targetOffset = Vector3.zero;

            w.guide.headline =
                "03 BOARDING  —  28 s\n" +
                passengers.Count + " passengers, each routed to their nearest door.\n" +
                "Chime, doors open, they board, doors slam down the train, guard's whistle.";

            var guide = new List<SceneGuide.Marker>
            {
                Mark("Walking lane, z = " + LaneZ + " — everyone joins this before turning for a door",
                     new Vector3(-34f, StationKit.PlatformTopY, LaneZ), GuideNote, 2.5f),
                Mark("They step aboard from z = " + DoorZ,
                     new Vector3(-34f, StationKit.PlatformTopY, DoorZ), GuideStart, 1.6f)
            };

            // A post at every door, so you can see the queue targets without pressing Play.
            for (int i = 0; i < rig.doors.Count; i++)
            {
                var at = new Vector3(rig.doors[i].position.x, StationKit.PlatformTopY, DoorZ);
                guide.Add(Mark("door " + (i + 1), at, GuideStop, 2.2f));
            }

            guide.Add(CameraMark(cam, focus.transform.position));
            w.guide.markers = guide.ToArray();

            Save(Boarding);
        }

        /// <summary>
        /// Fills the platform and gives everybody a route to a door.
        ///
        /// Each passenger walks out to a lane running parallel to the platform edge, along it to
        /// their door, and then in. Routing everyone through that middle lane is what stops them
        /// cutting diagonally through one another.
        /// </summary>
        static List<PassengerWalker> BuildCrowd(StationWorld w, TrainRig rig)
        {
            w.passengers = Prim.Empty(null, "07 PASSENGERS").transform;
            var crowd = w.passengers;
            var list = new List<PassengerWalker>();

            if (rig.doors.Count == 0) return list;

            var rng = new System.Random(19541107);
            const int count = 18;

            for (int i = 0; i < count; i++)
            {
                // Spawn spread back across the platform, away from the edge.
                float sx = -34f + (float)rng.NextDouble() * 44f;
                float sz = 6.4f + (float)rng.NextDouble() * 5.2f;

                var walker = PassengerFactory.Build(crowd, "Passenger" + (i + 1),
                                                    new Vector3(sx, StationKit.PlatformTopY, sz),
                                                    rng, rng.NextDouble() < 0.45);

                // Nearest door, so nobody walks the length of the train past three empty ones.
                Transform door = rig.doors[0];
                float best = float.MaxValue;
                for (int d = 0; d < rig.doors.Count; d++)
                {
                    float dist = Mathf.Abs(rig.doors[d].position.x - sx);
                    if (dist < best) { best = dist; door = rig.doors[d]; }
                }

                float doorX = door.position.x;

                walker.path = new[]
                {
                    new Vector3(sx, StationKit.PlatformTopY, LaneZ),
                    new Vector3(doorX, StationKit.PlatformTopY, LaneZ),
                    new Vector3(doorX, StationKit.PlatformTopY, DoorZ)
                };

                // Spread across most of the scene. At 1.5 + 11 everybody was aboard by the
                // halfway mark and the shot played out over an empty platform.
                walker.waitBefore = 1.0f + (float)rng.NextDouble() * 16.5f;
                walker.walkSpeed = 1.05f + (float)rng.NextDouble() * 0.45f;
                walker.stepUpHeight = 0.22f;
                walker.stepInDistance = 1.15f;

                list.Add(walker);
            }

            return list;
        }

        // ----------------------------------------------------------------- 04 · departure

        public static void BuildDeparture()
        {
            NewScene();

            var w = StationKit.BuildWorld(true);
            w.signal.startsAtDanger = false;
            w.signal.clearsAt = -1f;

            var rig = TrainFactory.Build(w.trains, OurTrain());
            PlaceTrain(rig, StationKit.StopMarkX, StationKit.TrackAZ);

            var motion = Motion(rig, TrainPhase.Depart);
            motion.dwellTime = 4f;
            motion.accelTime = 17f;
            motion.topSpeed = 23f;
            motion.startDelay = 0f;

            Audio(rig, motion, brakes: false, whistle: true, ambience: true);

            // From the far end of the platform, watching her go. The lazy damping lets the train
            // pull ahead of frame instead of staying pinned to the middle.
            var cam = MakeCamera(w, new Vector3(-57f, 3.1f, 3.4f), rig.tail, 38f);
            var dolly = cam.GetComponent<CameraDolly>();
            dolly.lookDamping = 2.6f;
            dolly.driftPerSecond = new Vector3(0.25f, 0.02f, -0.04f);
            dolly.driftDelay = 3f;
            dolly.targetOffset = new Vector3(0f, 1.6f, 0f);
            dolly.releaseDistance = 320f;

            w.guide.headline =
                "04 DEPARTURE  —  24 s\n" +
                "Whistle, then away towards +X: 4 s standing, then " + motion.accelTime +
                " s building to " + motion.topSpeed + " m/s.";

            w.guide.markers = new[]
            {
                Mark("Standing here at the mark when the scene opens",
                     new Vector3(StationKit.StopMarkX, 0f, StationKit.TrackAZ), GuideStop, 7f),
                Mark("Departs this way  ——>",
                     new Vector3(StationKit.StopMarkX + 90f, 0f, StationKit.TrackAZ), GuideStart, 6f),
                CameraMark(cam, rig.tail.position)
            };

            Save(Departure);
        }

        // -------------------------------------------------------------------------- shared

        static void NewScene()
        {
            Prim.ResetCache();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void Save(string name)
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SceneDir + "/" + name + ".unity");
        }

        /// <summary>Positions a rig by its nose, on the given track.</summary>
        static void PlaceTrain(TrainRig rig, float noseX, float trackZ)
        {
            rig.root.position = new Vector3(noseX, 0f, trackZ);
        }

        static TrainMotion Motion(TrainRig rig, TrainPhase phase)
        {
            var m = rig.root.gameObject.AddComponent<TrainMotion>();
            m.phase = phase;
            m.direction = Vector3.right;
            m.wheels = rig.wheels.ToArray();
            m.drivingWheelRadius = TrainFactory.DrivingWheelRadius;
            m.stack = rig.stack;
            return m;
        }

        static TrainAudio Audio(TrainRig rig, TrainMotion motion, bool brakes, bool whistle, bool ambience)
        {
            var source = rig.root.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            var a = rig.root.gameObject.AddComponent<TrainAudio>();
            a.motion = motion;
            a.brakesOnArrival = brakes;
            a.whistleOnDeparture = whistle;
            a.stationAmbience = ambience;
            a.safetyValveWhenStanding = true;
            return a;
        }

        // ------------------------------------------------------------------- scene guide

        /// <summary>Walking lane, clear of the platform edge. Drawn by the guide as well as walked.</summary>
        const float LaneZ = 4.6f;
        /// <summary>Where a passenger stands before stepping aboard.</summary>
        const float DoorZ = 2.55f;

        static readonly Color GuideStop = new Color(1f, 0.35f, 0.30f);
        static readonly Color GuideStart = new Color(0.55f, 0.90f, 0.55f);
        static readonly Color GuideCamera = new Color(1f, 0.55f, 0.95f);
        static readonly Color GuideNote = new Color(0.75f, 0.85f, 1f);

        static SceneGuide.Marker Mark(string label, Vector3 at, Color color, float height = 4f)
        {
            return new SceneGuide.Marker { label = label, position = at, color = color, height = height };
        }

        /// <summary>A marker with a line drawn to whatever the camera is aimed at.</summary>
        static SceneGuide.Marker CameraMark(Camera cam, Vector3 lookingAt)
        {
            return new SceneGuide.Marker
            {
                label = "Camera — " + Mathf.RoundToInt(cam.fieldOfView) + "°, aimed along this line",
                position = cam.transform.position,
                color = GuideCamera,
                height = 0.6f,
                hasLine = true,
                lineTo = lookingAt
            };
        }

        static Camera MakeCamera(StationWorld w, Vector3 position, Transform lookAt, float fov)
        {
            var go = Prim.Empty(w.camera, "Main Camera");
            go.tag = "MainCamera";
            go.transform.position = position;

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;

            go.AddComponent<AudioListener>();

            var dolly = go.AddComponent<CameraDolly>();
            dolly.lookTarget = lookAt;
            dolly.shakeAmplitude = 0.016f;
            dolly.shakeFrequency = 0.5f;

            if (lookAt != null) go.transform.LookAt(lookAt.position, Vector3.up);

            PostFx.Apply(cam, null);

            return cam;
        }
    }
}
