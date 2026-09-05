using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Runs the film in play mode and grabs frames along the way.
    ///
    /// Everything else in this project verifies the film statically — that it compiles, that the
    /// geometry is right, that the shots are framed. None of that proves it actually *runs*: that
    /// the scenes hand over to one another, that the train brakes onto its mark, that the
    /// passengers reach a door. This does, and it does it headlessly.
    ///
    /// Domain reload has to be off for this to work at all, since entering play mode would
    /// otherwise wipe the static state tracking where we are in the run.
    /// </summary>
    public static class Playtest
    {
        public const string OutputDir = "Playtest";

        /// <summary>
        /// When to grab a frame, in seconds from the start of play. Chosen to land inside each
        /// scene rather than on a fade: roughly the arrival, the wait, the boarding and the
        /// departure, twice each.
        /// </summary>
        static readonly float[] CaptureAt =
        {
            4f, 14f, 22f,          // 01 arrival    (0.6 - 25.6)
            30f, 38f,              // 02 waiting    (25.6 - 43.6)
            50f, 62f,              // 03 boarding   (43.6 - 71.6)
            78f, 88f               // 04 departure  (71.6 - 95.6)
        };

        const float GiveUpAfter = 105f;

        static int _next;
        static bool _started;
        static bool _batch;
        static readonly List<string> _errors = new List<string>();

        [MenuItem("Tools/Train Station/Playtest", priority = 61)]
        public static void Run()
        {
            Begin(false);
        }

        /// <summary>Command-line entry point. Quits the editor with the result as its exit code.</summary>
        public static void BatchPlaytest()
        {
            Begin(true);
        }

        static void Begin(bool batch)
        {
            _batch = batch;
            Directory.CreateDirectory(OutputDir);

            _next = 0;
            _started = false;
            _errors.Clear();

            // Static state below must survive entering play mode.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            string bootstrap = SceneBuilders.SceneDir + "/" + SceneBuilders.Bootstrap + ".unity";
            if (!File.Exists(bootstrap))
            {
                Debug.LogError("[Playtest] Missing " + bootstrap +
                               ". Run Build All Scenes first.");
                if (_batch) EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(bootstrap);

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            // A null reference inside a scene transition is exactly the sort of thing a static
            // render would never catch, so treat any runtime error as a failure.
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add(type + ": " + message);
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;

            if (!_started)
            {
                _started = true;
                Debug.Log("[Playtest] Play mode entered.");
            }

            // Game time, not editor wall-clock. The two diverge whenever the editor hitches, and
            // a run that stalled once had its captures firing 70 minutes of wall time apart while
            // the film itself had barely advanced.
            float t = Time.time;

            if (_next < CaptureAt.Length && t >= CaptureAt[_next])
            {
                Capture(string.Format("t{0:000}s", Mathf.RoundToInt(CaptureAt[_next])), t);
                _next++;
            }

            if (t >= GiveUpAfter || _next >= CaptureAt.Length && t >= CaptureAt[CaptureAt.Length - 1] + 6f)
            {
                Finish();
            }
        }

        static void Capture(string label, float t)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                cam = cams.Length > 0 ? cams[0] : null;
            }

            if (cam == null)
            {
                Debug.LogWarning("[Playtest] No camera at " + label);
                return;
            }

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            shot.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            File.WriteAllBytes(Path.Combine(OutputDir, label + ".png"), shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);

            // Report where the film thinks it is, so a stalled scene flow is obvious in the log.
            var flow = Object.FindFirstObjectByType<SceneFlowManager>();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log(string.Format("[Playtest] {0} at {1:0.0}s - scene '{2}', shot index {3}",
                                    label, t, scene, flow != null ? flow.CurrentIndex : -1));
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;

            if (_errors.Count > 0)
            {
                Debug.Log("[Playtest] FAILED with " + _errors.Count + " runtime error(s):");
                for (int i = 0; i < Mathf.Min(_errors.Count, 12); i++)
                {
                    Debug.Log("[Playtest]   " + _errors[i]);
                }
            }
            else
            {
                Debug.Log("[Playtest] PASSED - ran to the end with no runtime errors.");
            }

            EditorApplication.ExitPlaymode();

            // Only tear the editor down when this was launched from the command line. Doing it
            // from the menu item would close Unity on whoever clicked it.
            if (_batch) EditorApplication.Exit(_errors.Count > 0 ? 1 : 0);
        }
    }
}
