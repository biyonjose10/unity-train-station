using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TrainStation
{
    /// <summary>
    /// Draws the invisible half of the scene into the Unity Scene view: the two running lines,
    /// the platform edge, where the locomotive is supposed to stop, where the passengers walk,
    /// and what the camera is pointed at.
    ///
    /// None of this renders in the film — it is gizmos, which only exist in the editor. The point
    /// is that a scene built entirely by script otherwise gives you nothing to look at while it is
    /// standing still: you can see a train and a platform, but not the plan they are following.
    /// With the guide on, you can open a scene and talk through what will happen without ever
    /// pressing Play.
    ///
    /// Untick <see cref="show"/> to turn the whole thing off.
    /// </summary>
    public class SceneGuide : MonoBehaviour
    {
        [System.Serializable]
        public class Marker
        {
            public string label;
            public Vector3 position;
            public Color color = Color.white;

            /// <summary>Height of the vertical post drawn at the marker.</summary>
            public float height = 4f;

            /// <summary>If set, a line is drawn from the marker to here — camera to subject, say.</summary>
            public bool hasLine;
            public Vector3 lineTo;
        }

        [Tooltip("Turn every gizmo in this scene off.")]
        public bool show = true;

        [Tooltip("Shown at the origin, so you can tell which scene you have open.")]
        [TextArea(2, 4)]
        public string headline = "";

        [Header("Track")]
        public float trackAZ;
        public float trackBZ = -7.6f;
        public float platformEdgeZ = 2.1f;
        public float platformFromX = -58f;
        public float platformToX = 40f;

        [Header("Markers")]
        public Marker[] markers = new Marker[0];

#if UNITY_EDITOR
        static readonly Color TrackA = new Color(0.35f, 0.85f, 1f);
        static readonly Color TrackB = new Color(0.55f, 0.55f, 0.65f);
        static readonly Color Edge = new Color(1f, 0.82f, 0.25f);

        void OnDrawGizmos()
        {
            if (!show) return;

            // Drawn from OnDrawGizmos rather than OnDrawGizmosSelected so it is there the moment
            // the scene opens. Having to hunt for and click an object first would defeat it.
            DrawLine(TrackA, trackAZ, "Track A — platform road (our train)");
            DrawLine(TrackB, trackBZ, "Track B — through road (the other train)");

            Gizmos.color = Edge;
            Gizmos.DrawLine(new Vector3(platformFromX, 1.5f, platformEdgeZ),
                            new Vector3(platformToX, 1.5f, platformEdgeZ));

            foreach (var m in markers)
            {
                if (m == null) continue;

                Gizmos.color = m.color;

                var foot = m.position;
                var head = m.position + Vector3.up * m.height;
                Gizmos.DrawLine(foot, head);
                Gizmos.DrawSphere(head, 0.35f);

                if (m.hasLine) Gizmos.DrawLine(head, m.lineTo);

                if (!string.IsNullOrEmpty(m.label)) Label(head + Vector3.up * 0.6f, m.label, m.color);
            }

            if (!string.IsNullOrEmpty(headline))
                Label(new Vector3(platformFromX, 14f, 6f), headline, Color.white);
        }

        void DrawLine(Color c, float z, string label)
        {
            Gizmos.color = c;
            Gizmos.DrawLine(new Vector3(-200f, 0.55f, z), new Vector3(340f, 0.55f, z));
            Label(new Vector3(-70f, 1.4f, z), label, c);
        }

        /// <summary>
        /// Handles.Label lives in UnityEditor, which is why this whole block is behind
        /// UNITY_EDITOR — without the guard the project would not compile into a build.
        /// </summary>
        static void Label(Vector3 at, string text, Color c)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = c;
            style.fontSize = 12;
            Handles.Label(at, text, style);
        }
#endif
    }
}
