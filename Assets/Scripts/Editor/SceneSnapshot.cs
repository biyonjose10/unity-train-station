using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Renders one still from each scene's own shot camera, so the framing, lighting and geometry
    /// can be checked without opening the editor and pressing Play.
    ///
    /// What these stills do show: composition, silhouette, materials, lighting, and the full
    /// post-processing grade — a camera's render callbacks fire in edit mode too, so the ACES
    /// tonemapping, bloom and vignette are all present.
    ///
    /// What they do not show: motion. No script has run and no particle has spawned, so the train
    /// always sits at its start position — in 01_Arrival that is correctly just a dot in the fog.
    /// </summary>
    public static class SceneSnapshot
    {
        public const string OutputDir = "Snapshots";
        public const int Width = 1280;
        public const int Height = 720;

        [MenuItem("Tools/Train Station/Snapshot Scenes", priority = 60)]
        public static void Snapshot()
        {
            Directory.CreateDirectory(OutputDir);

            foreach (string name in SceneBuilders.All)
            {
                // Bootstrap has no geometry; a still of it is a black rectangle.
                if (name == SceneBuilders.Bootstrap) continue;

                string path = SceneBuilders.SceneDir + "/" + name + ".unity";
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[TrainStation] No scene at " + path);
                    continue;
                }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Capture(name);
            }

            Debug.Log("[TrainStation] Snapshots written to " + OutputDir + "/");
        }

        public static void BatchSnapshot()
        {
            int exit = 0;
            try
            {
                Snapshot();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TrainStation] Snapshot failed: " + e);
                exit = 1;
            }
            EditorApplication.Exit(exit);
        }

        static void Capture(string name)
        {
            var cam = FindShotCamera();
            if (cam == null)
            {
                Debug.LogWarning("[TrainStation] No camera in " + name);
                return;
            }

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };

            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();

            // Put the camera and the global render target back, or the next scene renders into
            // a texture that no longer exists.
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            File.WriteAllBytes(Path.Combine(OutputDir, name + ".png"), shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log("[TrainStation] Captured " + name);
        }

        /// <summary>
        /// Camera.main only finds enabled cameras tagged MainCamera, and in edit mode that tag
        /// lookup is not always populated, so fall back to whatever camera the scene has.
        /// </summary>
        static Camera FindShotCamera()
        {
            if (Camera.main != null) return Camera.main;

            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return cams.Length > 0 ? cams[0] : null;
        }
    }
}
