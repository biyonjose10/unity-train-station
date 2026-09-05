using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Primitive and material helpers shared by every scene builder.
    /// Everything in this film is made from Unity's built-in primitives, so this is the
    /// layer that makes that bearable to write.
    /// </summary>
    public static class Prim
    {
        public const string MaterialDir = "Assets/Materials";

        /// <summary>Cylinder rotations, named by the world axis the cylinder ends up lying along.</summary>
        public static readonly Vector3 AxisX = new Vector3(0f, 0f, 90f);
        public static readonly Vector3 AxisY = Vector3.zero;
        public static readonly Vector3 AxisZ = new Vector3(90f, 0f, 0f);

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void ResetCache()
        {
            Cache.Clear();
        }

        // ------------------------------------------------------------------ materials

        public static Material Mat(string name, Color albedo, float metallic = 0f, float smoothness = 0.25f)
        {
            return Build(name, albedo, metallic, smoothness, null);
        }

        public static Material Emissive(string name, Color albedo, Color emission, float smoothness = 0.6f)
        {
            return Build(name, albedo, 0f, smoothness, emission);
        }

        static Material Build(string name, Color albedo, float metallic, float smoothness, Color? emission)
        {
            Material hit;
            if (Cache.TryGetValue(name, out hit) && hit != null) return hit;

            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";

            // Update the existing asset in place rather than replacing it. AssetDatabase.CreateAsset
            // deletes whatever is already at the path first, and since all five scenes are built in
            // one run off the same shared palette, recreating a material would leave every scene
            // built before this point referencing a destroyed object.
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;

            if (isNew) m = new Material(Shader.Find("Standard"));

            m.name = name;
            m.color = albedo;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);

            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                // Clear it explicitly: a reused asset may have been emissive on a previous run.
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                m.SetColor("_EmissionColor", Color.black);
            }

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);

            Cache[name] = m;
            return m;
        }

        // ----------------------------------------------------------------- primitives

        public static GameObject Spawn(PrimitiveType type, Transform parent, string name,
                                       Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Nothing in this film uses physics, and a few thousand colliders is pure waste.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            return Spawn(PrimitiveType.Cube, parent, name, pos, size, Quaternion.identity, mat);
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, Material mat)
        {
            return Spawn(PrimitiveType.Cube, parent, name, pos, size, Quaternion.Euler(euler), mat);
        }

        /// <summary>
        /// Unity's Cylinder mesh is 2 units tall and 1 unit across, so a cylinder of radius r
        /// and length L wants a scale of (2r, L/2, 2r). This wrapper hides that, because
        /// getting it wrong silently gives you a boiler of the wrong length.
        /// </summary>
        public static GameObject Cyl(Transform parent, string name, Vector3 pos,
                                     float radius, float length, Vector3 euler, Material mat)
        {
            return Spawn(PrimitiveType.Cylinder, parent, name, pos,
                         new Vector3(radius * 2f, length * 0.5f, radius * 2f),
                         Quaternion.Euler(euler), mat);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, float diameter, Material mat)
        {
            return Spawn(PrimitiveType.Sphere, parent, name, pos, Vector3.one * diameter,
                         Quaternion.identity, mat);
        }

        /// <summary>Capsule mesh is 2 tall and 1 across, same convention as the cylinder.</summary>
        public static GameObject Capsule(Transform parent, string name, Vector3 pos,
                                         float diameter, float height, Vector3 euler, Material mat)
        {
            return Spawn(PrimitiveType.Capsule, parent, name, pos,
                         new Vector3(diameter, height * 0.5f, diameter),
                         Quaternion.Euler(euler), mat);
        }

        public static GameObject Empty(Transform parent, string name)
        {
            return Empty(parent, name, Vector3.zero);
        }

        public static GameObject Empty(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go;
        }

        // --------------------------------------------------------------------- wheels

        /// <summary>
        /// A wheel whose axle runs across the track (world Z), so spinning it about its own
        /// local Y rolls it along X. Returns the pivot that TrainMotion rotates.
        /// </summary>
        public static Transform Wheel(Transform parent, string name, Vector3 pos,
                                      float radius, float width, Material tyre, Material hub)
        {
            var pivot = Empty(parent, name, pos).transform;
            pivot.localRotation = Quaternion.Euler(AxisZ);

            // The wheel carries its own radius so drivers and carriage wheels roll correctly
            // off the same distance travelled.
            pivot.gameObject.AddComponent<TrainStation.TrainWheel>().radius = radius;

            Spawn(PrimitiveType.Cylinder, pivot, "Tyre", Vector3.zero,
                  new Vector3(radius * 2f, width * 0.5f, radius * 2f), Quaternion.identity, tyre);

            Spawn(PrimitiveType.Cylinder, pivot, "Hub", Vector3.zero,
                  new Vector3(radius * 0.68f, width * 0.62f, radius * 0.68f), Quaternion.identity, hub);

            // A counterweight bar on each face. Without it a smooth cylinder gives the eye
            // nothing to track, and a rolling wheel reads as a sliding one.
            for (int side = -1; side <= 1; side += 2)
            {
                Spawn(PrimitiveType.Cube, pivot, "Counterweight",
                      new Vector3(0f, side * width * 0.52f, 0f),
                      new Vector3(radius * 1.5f, 0.05f, radius * 0.3f), Quaternion.identity, hub);
            }

            return pivot;
        }

        // ----------------------------------------------------------------------- signs

        /// <summary>
        /// Unity 6 dropped the old built-in Arial in favour of LegacyRuntime. Try both, and
        /// let callers carry on with a blank sign rather than throwing if neither exists.
        /// </summary>
        public static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        public static GameObject Text3D(Transform parent, string name, Vector3 pos, Vector3 euler,
                                        string content, float size, Color color)
        {
            var font = BuiltinFont();
            if (font == null) return null;

            var go = Empty(parent, name, pos);
            go.transform.localRotation = Quaternion.Euler(euler);

            var tm = go.AddComponent<TextMesh>();
            tm.text = content;
            tm.font = font;
            tm.fontSize = 72;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return go;
        }
    }
}
