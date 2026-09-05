using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Entry points for regenerating the film.
    ///
    /// The .unity files are build output, not hand-authored source, so this is the only correct
    /// way to change the scenes: edit the builders, run this, and all five are rewritten together.
    /// </summary>
    public static class BuildAll
    {
        [MenuItem("Tools/Train Station/Build All Scenes %#t", priority = 0)]
        public static void Run()
        {
            try
            {
                ApplyProjectSettings();

                // Bootstrap last, so the editor is left sitting on the scene you press Play from.
                SceneBuilders.BuildArrival();
                SceneBuilders.BuildWaiting();
                SceneBuilders.BuildBoarding();
                SceneBuilders.BuildDeparture();
                SceneBuilders.BuildBootstrap();

                RegisterScenes();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[TrainStation] Built " + SceneBuilders.All.Length +
                          " scenes into " + SceneBuilders.SceneDir +
                          ". Open 00_Bootstrap and press Play.");
            }
            catch (Exception e)
            {
                Debug.LogError("[TrainStation] Build failed: " + e);
                throw;
            }
        }

        [MenuItem("Tools/Train Station/Open Bootstrap", priority = 20)]
        public static void OpenBootstrap()
        {
            EditorSceneManager.OpenScene(SceneBuilders.SceneDir + "/" + SceneBuilders.Bootstrap + ".unity");
        }

        /// <summary>
        /// Called from the command line with -executeMethod. Builds everything, then quits with
        /// a non-zero code if anything logged an error, so a failed build cannot look like a pass.
        /// </summary>
        public static void BatchBuild()
        {
            int exitCode = 0;

            try
            {
                Run();
            }
            catch (Exception)
            {
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Puts all five scenes in Build Settings. Without this, SceneFlowManager's
        /// LoadSceneAsync would fail at runtime with nothing but a console warning to show for it.
        /// </summary>
        static void RegisterScenes()
        {
            var scenes = new EditorBuildSettingsScene[SceneBuilders.All.Length];

            for (int i = 0; i < SceneBuilders.All.Length; i++)
            {
                string path = SceneBuilders.SceneDir + "/" + SceneBuilders.All[i] + ".unity";
                scenes[i] = new EditorBuildSettingsScene(path, true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        static void ApplyProjectSettings()
        {
            // Linear colour space. Gamma would wash the dusk lighting out completely and make the
            // emissive windows look like flat stickers.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[TrainStation] Switched colour space to Linear.");
            }

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = true;

            // The platform has a row of lamps plus lit windows and a firebox. The default of four
            // per-pixel lights leaves most of them falling back to vertex lighting.
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadowDistance = 160f;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
        }
    }
}
