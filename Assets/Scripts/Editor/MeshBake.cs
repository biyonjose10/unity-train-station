using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrainStation.Build
{
    /// <summary>
    /// Collapses a group of static decoration into one object per material.
    ///
    /// The station is drawn with a lot of small repeated pieces — 336 sleepers per running line,
    /// 46 trees, a town roofline — and while that is the right way to *describe* the scene, it is
    /// a terrible way to have to *read* it. Baking them leaves the Hierarchy showing
    /// "Sleepers (baked)" instead of 336 rows called "Sleeper", which is the difference between a
    /// scene you can walk somebody through and a scene you can only scroll.
    ///
    /// Only ever call this on scenery. Anything that moves, lights, or is referenced by a script
    /// must stay as separate objects, and <see cref="Flatten"/> refuses to touch a group
    /// containing one rather than quietly deleting it.
    /// </summary>
    public static class MeshBake
    {
        public const string MeshDir = "Assets/Meshes";

        /// <summary>
        /// Merges every mesh under <paramref name="group"/> into one child per material and
        /// deletes the originals. Returns how many GameObjects that removed.
        ///
        /// <paramref name="assetName"/> names the saved mesh assets, so it has to be unique
        /// across the project — two groups sharing a name would overwrite each other.
        /// </summary>
        public static int Flatten(Transform group, string assetName)
        {
            if (group == null) return 0;

            var filters = group.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return 0;

            if (!SafeToBake(group)) return 0;

            // Group by material: one draw call and one Hierarchy row per material, rather than
            // one mesh with submeshes, which would be harder to explain and no cheaper.
            var byMaterial = new Dictionary<Material, List<MeshFilter>>();
            var order = new List<Material>();

            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;

                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || r.sharedMaterial == null) continue;

                List<MeshFilter> bucket;
                if (!byMaterial.TryGetValue(r.sharedMaterial, out bucket))
                {
                    bucket = new List<MeshFilter>();
                    byMaterial[r.sharedMaterial] = bucket;
                    order.Add(r.sharedMaterial);
                }

                bucket.Add(mf);
            }

            if (byMaterial.Count == 0) return 0;

            int before = group.GetComponentsInChildren<Transform>(true).Length;
            var baked = new List<GameObject>();

            foreach (var mat in order)
            {
                var bucket = byMaterial[mat];

                var combine = new CombineInstance[bucket.Count];
                for (int i = 0; i < bucket.Count; i++)
                {
                    combine[i].mesh = bucket[i].sharedMesh;

                    // Into the group's local space, so the baked child can sit at identity and
                    // the group as a whole can still be moved or hidden.
                    combine[i].transform = group.worldToLocalMatrix *
                                           bucket[i].transform.localToWorldMatrix;
                }

                var mesh = MeshAsset(assetName + "_" + mat.name);

                // 672 sleepers is 16k vertices, which fits in the default 16-bit index buffer,
                // but only just. 32-bit costs nothing here and takes the ceiling away entirely.
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(combine, true, true);
                mesh.RecalculateBounds();

                EditorUtility.SetDirty(mesh);

                var go = new GameObject(group.name + " (baked, " + mat.name + ")");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;

                baked.Add(go);
            }

            // Delete the originals only once every bake has succeeded, so a throw halfway through
            // leaves the scene intact rather than half-erased.
            var doomed = new List<GameObject>();
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                var child = group.GetChild(i).gameObject;
                if (!baked.Contains(child)) doomed.Add(child);
            }

            foreach (var go in doomed) Object.DestroyImmediate(go);

            int after = group.GetComponentsInChildren<Transform>(true).Length;
            return before - after;
        }

        /// <summary>
        /// True only if every descendant is plain geometry. A Light, a script, an AudioSource or
        /// anything else means the group is load-bearing and baking it would destroy behaviour —
        /// the signal lenses and the platform lamps both look like scenery and are not.
        /// </summary>
        static bool SafeToBake(Transform group)
        {
            foreach (var c in group.GetComponentsInChildren<Component>(true))
            {
                if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;

                Debug.LogWarning("[TrainStation] Not baking '" + group.name + "': it contains a " +
                                 c.GetType().Name + " on '" + c.gameObject.name +
                                 "'. Baking is for static scenery only.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads the mesh asset at this name and empties it, or creates one if it is not there.
        ///
        /// Reusing the asset matters for the same reason it matters for materials: all five
        /// scenes are built in a single run off this one set of meshes, and CreateAsset deletes
        /// whatever is already at the path, which would leave every scene built before this point
        /// pointing at a destroyed mesh.
        /// </summary>
        static Mesh MeshAsset(string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = MeshDir + "/" + name + ".asset";

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                mesh.Clear();
                mesh.name = name;
            }

            return mesh;
        }
    }
}
