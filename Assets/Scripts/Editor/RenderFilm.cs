using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Drives <see cref="FilmCapture"/> from the command line: enters play mode, runs the film at
    /// a fixed capture framerate, and writes a JPEG sequence plus a WAV for ffmpeg to mux.
    ///
    /// This is the headless alternative to the Recorder window, so the whole film — scenes,
    /// frames, audio and all — can be produced without anybody clicking anything.
    ///
    /// Two ordering traps, both of which silently produced a full-length 720p render when a
    /// short 640x360 probe was asked for:
    ///
    /// - AddComponent runs Awake immediately on an active GameObject, so configuring the capture
    ///   after adding it is too late. The object is created inactive and switched on afterwards.
    /// - Entering play mode can trigger a domain reload, which resets statics and drops the update
    ///   subscription. Settings live in SessionState, which survives reloads, and
    ///   [InitializeOnLoadMethod] puts the hook back if one lands mid-capture.
    /// </summary>
    public static class RenderFilm
    {
        public const string CaptureDir = "Capture";
        public const string FrameDir = "Capture/frames";
        public const string WavPath = "Capture/audio.wav";

        const string KeyPending = "TrainStation.Render.Pending";
        const string KeyBatch = "TrainStation.Render.Batch";
        const string KeySeconds = "TrainStation.Render.Seconds";
        const string KeyFps = "TrainStation.Render.Fps";
        const string KeyWidth = "TrainStation.Render.Width";
        const string KeyHeight = "TrainStation.Render.Height";

        static FilmCapture _capture;

        [MenuItem("Tools/Train Station/Render Film (frames + wav)", priority = 42)]
        public static void Run()
        {
            Begin(96f, 30, 1280, 720, false);
        }

        /// <summary>Full film, for the command line.</summary>
        public static void BatchRender()
        {
            Begin(96f, 30, 1280, 720, true);
        }

        /// <summary>
        /// A few seconds at low resolution, to check the pipeline end to end before committing to
        /// the full run.
        /// </summary>
        public static void BatchProbe()
        {
            Begin(8f, 30, 640, 360, true);
        }

        [InitializeOnLoadMethod]
        static void Reattach()
        {
            // If a domain reload landed in the middle of a capture, the update subscription went
            // with it. Put it back.
            if (!SessionState.GetBool(KeyPending, false)) return;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Begin(float seconds, int fps, int width, int height, bool batch)
        {
            _capture = null;

            // Old frames left behind would be picked up by ffmpeg's glob and spliced into the
            // new film.
            if (Directory.Exists(FrameDir)) Directory.Delete(FrameDir, true);
            Directory.CreateDirectory(FrameDir);

            string bootstrap = SceneBuilders.SceneDir + "/" + SceneBuilders.Bootstrap + ".unity";
            if (!File.Exists(bootstrap))
            {
                Debug.LogError("[RenderFilm] Missing " + bootstrap + ". Run Build All Scenes first.");
                if (batch) EditorApplication.Exit(1);
                return;
            }

            SessionState.SetBool(KeyPending, true);
            SessionState.SetBool(KeyBatch, batch);
            SessionState.SetFloat(KeySeconds, seconds);
            SessionState.SetInt(KeyFps, fps);
            SessionState.SetInt(KeyWidth, width);
            SessionState.SetInt(KeyHeight, height);

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            EditorSceneManager.OpenScene(bootstrap);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            Debug.Log("[RenderFilm] Requested " + seconds + "s at " + width + "x" + height +
                      ", " + fps + " fps.");

            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;

            if (_capture == null)
            {
                // Look for one that survived a reload before making another.
                _capture = Object.FindFirstObjectByType<FilmCapture>();
            }

            if (_capture == null)
            {
                // Created inactive so the fields below are in place before Awake runs. Adding a
                // component to a live GameObject fires Awake on the spot, and the capture would
                // read its defaults instead of what was asked for.
                var go = new GameObject("FilmCapture");
                go.SetActive(false);

                _capture = go.AddComponent<FilmCapture>();
                _capture.width = SessionState.GetInt(KeyWidth, 1280);
                _capture.height = SessionState.GetInt(KeyHeight, 720);
                _capture.fps = SessionState.GetInt(KeyFps, 30);
                _capture.seconds = SessionState.GetFloat(KeySeconds, 96f);
                _capture.frameDir = FrameDir;
                _capture.wavPath = WavPath;

                go.SetActive(true);
                return;
            }

            if (!_capture.Done) return;

            bool batch = SessionState.GetBool(KeyBatch, false);

            EditorApplication.update -= Tick;
            SessionState.SetBool(KeyPending, false);

            Debug.Log("[RenderFilm] Done - " + _capture.FramesWritten + " frames in " + FrameDir);

            EditorApplication.ExitPlaymode();
            if (batch) EditorApplication.Exit(0);
        }
    }
}
