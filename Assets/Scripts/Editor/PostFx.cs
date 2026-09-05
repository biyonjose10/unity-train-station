using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace TrainStation.Build
{
    /// <summary>
    /// Sets up the post-processing stack.
    ///
    /// This matters more here than it usually would. The scene is lit by a very low sun and is
    /// full of small bright sources — lamps, lit carriage windows, a firebox — and without a
    /// tonemapper those simply clip to flat white, which is exactly what was happening to the
    /// carriage roofs. ACES rolls the highlights off instead, and bloom lets the lamps bleed the
    /// way a real lamp does at dusk.
    ///
    /// Everything degrades gracefully: if the package resources cannot be found, the scene still
    /// builds and still reads, just without the polish.
    /// </summary>
    public static class PostFx
    {
        const string ProfilePath = "Assets/Materials/DuskGrade.asset";

        /// <summary>Adds a global volume and wires the camera up to see it.</summary>
        public static void Apply(Camera cam, Transform parent)
        {
            var resources = FindResources();
            if (resources == null)
            {
                Debug.LogWarning("[TrainStation] PostProcessResources not found; " +
                                 "skipping post-processing. The scene will still render.");
                return;
            }

            var profile = BuildProfile();

            var volumeGo = Prim.Empty(parent, "PostProcessing");
            // PostProcessLayer filters volumes by layer mask, so the volume has to sit on a layer
            // the camera is actually looking for. Default keeps that simple.
            volumeGo.layer = 0;

            var volume = volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            var layer = cam.gameObject.AddComponent<PostProcessLayer>();
            // Init assigns the shader/texture resources the stack needs. Skipping it is the
            // classic way to end up with a magenta screen at runtime.
            layer.Init(resources);
            layer.volumeTrigger = cam.transform;
            layer.volumeLayer = 1 << 0;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            layer.fog.enabled = true;
        }

        static PostProcessProfile BuildProfile()
        {
            // Reuse the asset but always rewrite the values into it. Returning an existing profile
            // untouched meant edits to the grade below silently did nothing on a rebuild, and
            // deleting and recreating it would break the scenes already built in this run.
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                // The asset has to exist before any effect can be parented into it, so it is
                // created empty here rather than at the end.
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // An older build could have left null entries in the list; they would throw inside
            // the stack at runtime.
            profile.settings.RemoveAll(s => s == null);

            var grade = GetOrAdd<ColorGrading>(profile);
            grade.enabled.Override(true);
            grade.tonemapper.Override(Tonemapper.ACES);
            // Pulled down slightly: the low sun is deliberately strong, and ACES plus a touch of
            // negative exposure keeps the boiler tops from going to paper white.
            grade.postExposure.Override(-0.30f);
            grade.contrast.Override(8f);
            grade.saturation.Override(6f);
            grade.temperature.Override(12f);   // push it warmer still

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.enabled.Override(true);
            bloom.intensity.Override(1.25f);
            bloom.threshold.Override(1.22f);
            bloom.softKnee.Override(0.55f);
            bloom.diffusion.Override(7.5f);
            bloom.color.Override(new Color(1f, 0.92f, 0.82f));

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.enabled.Override(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            return profile;
        }

        /// <summary>
        /// Fetch an effect block from the profile, adding it only if it is not there.
        ///
        /// The AddObjectToAsset call is the important part. PostProcessProfile.AddSettings only
        /// creates the effect and puts it in the profile's list — it does not parent it into the
        /// asset, so without this the saved profile is three null entries and the whole stack is
        /// silently absent at runtime.
        /// </summary>
        static T GetOrAdd<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
        {
            T found;
            if (profile.TryGetSettings(out found) && found != null) return found;

            var created = profile.AddSettings<T>();

            if (AssetDatabase.Contains(profile))
            {
                // Hidden, or every effect shows up as a stray child of the asset in the Project
                // window. The stack still finds them through the profile's own list.
                created.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(created, profile);
            }

            return created;
        }

        static PostProcessResources FindResources()
        {
            var guids = AssetDatabase.FindAssets("t:PostProcessResources");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(path);
                if (res != null) return res;
            }
            return null;
        }
    }
}
