using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Captures the whole film to a single MP4.
    ///
    /// Unity Recorder captures the Game View for the length of a play session, and a scene load
    /// does not interrupt that. So although the film is four separate scene files, recording it
    /// still produces one continuous take with no seams beyond the fades we put there ourselves.
    ///
    /// The settings are pushed into the Recorder Window rather than driven through
    /// <see cref="RecorderController"/> directly, because RecorderController.PrepareRecording can
    /// only be called from play mode and its instance would not survive the domain reload that
    /// entering play mode triggers. The window already solves both problems.
    /// </summary>
    public static class RecordSequence
    {
        public const string OutputDir = "Recordings";
        public const string OutputName = "TrainStation";

        /// <summary>Total running time of the film, plus a little tail so the last fade lands.</summary>
        public const float FilmSeconds = 96f;

        public const int Width = 1920;
        public const int Height = 1080;
        public const float Fps = 60f;

        [MenuItem("Tools/Train Station/Record Film (MP4)", priority = 40)]
        public static void Record()
        {
            var settings = BuildSettings();

            // Record from the bootstrap scene, or we would capture whatever happened to be open.
            string bootstrap = SceneBuilders.SceneDir + "/" + SceneBuilders.Bootstrap + ".unity";
            if (File.Exists(bootstrap))
            {
                EditorSceneManager.OpenScene(bootstrap);
            }
            else
            {
                Debug.LogError("[TrainStation] " + bootstrap +
                               " is missing. Run Tools > Train Station > Build All Scenes first.");
                return;
            }

            var window = EditorWindow.GetWindow<RecorderWindow>();
            window.SetRecorderControllerSettings(settings);
            window.Show();

            Debug.Log("[TrainStation] Recorder configured: " + Width + "x" + Height + " @ " + Fps +
                      " fps, " + FilmSeconds + "s, audio on. Output -> " +
                      OutputDir + "/" + OutputName + ".mp4\n" +
                      "Press START RECORDING in the Recorder window. It will enter play mode, " +
                      "capture the whole film and stop on its own.");
        }

        /// <summary>
        /// Saves the same configuration as a reusable asset, so the Recorder window can be
        /// pointed at it again later without going through this menu item.
        /// </summary>
        [MenuItem("Tools/Train Station/Save Recorder Preset", priority = 41)]
        public static void SavePreset()
        {
            const string dir = "Assets/Recorder";
            Directory.CreateDirectory(dir);

            var settings = BuildSettings();
            AssetDatabase.CreateAsset(settings, dir + "/TrainStationRecorder.asset");

            // The movie recorder is a separate object and has to be stored inside the settings
            // asset, or it is silently dropped when the asset is reloaded.
            foreach (var r in settings.RecorderSettings)
            {
                AssetDatabase.AddObjectToAsset(r, settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TrainStation] Recorder preset saved to " + dir + "/TrainStationRecorder.asset");
        }

        static RecorderControllerSettings BuildSettings()
        {
            Directory.CreateDirectory(OutputDir);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "TrainStationFilm";
            movie.Enabled = true;

            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };

            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = Width,
                OutputHeight = Height
            };

            // The whole point of synthesising the audio is that it ends up in the video.
            movie.AudioInputSettings.PreserveAudio = true;

            // Relative to the project root, without an extension: Recorder adds ".mp4".
            movie.OutputFile = OutputDir + "/" + OutputName;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);

            // A fixed window means the recording stops itself at the end of the film rather than
            // running until somebody remembers to press stop.
            settings.SetRecordModeToTimeInterval(0f, FilmSeconds);

            settings.FrameRate = Fps;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;

            // Constant frame rate: let the film run slower than real time if it has to, rather
            // than dropping frames and producing a stuttery capture on a laptop GPU.
            settings.CapFrameRate = true;

            return settings;
        }
    }
}
