using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// The top level of a built scene: one field per numbered group in the Hierarchy.
    ///
    /// This type exists so the scene builders can say "put the train in <c>w.trains</c>" instead
    /// of parenting everything to one anonymous root. That is the whole reason the Hierarchy is
    /// readable — the structure is stated once, here, rather than being an accident of the order
    /// things happened to get created in.
    /// </summary>
    public class StationWorld
    {
        /// <summary>00 GUIDE — Scene-view gizmos. Editor only; nothing here reaches the film.</summary>
        public SceneGuide guide;

        /// <summary>01 CAMERA — the shot camera, its dolly and its post-processing.</summary>
        public Transform camera;

        /// <summary>02 LIGHTING — the sun. Fog, sky and ambient are render settings, not objects.</summary>
        public Transform lighting;

        /// <summary>03 TRACK — both running lines, and the signal that guards them.</summary>
        public Transform track;

        /// <summary>04 STATION — ground, platform, canopy, furniture, station building.</summary>
        public Transform station;

        /// <summary>05 SCENERY — telegraph poles, trees and the town roofline. Depth cues only.</summary>
        public Transform scenery;

        /// <summary>06 TRAINS — one child per train. Scene 2 is the only one with two.</summary>
        public Transform trains;

        /// <summary>07 PASSENGERS — the boarding crowd. Only scene 3 creates this.</summary>
        public Transform passengers;

        /// <summary>08 DIRECTION — the things that run the scene rather than appear in it.</summary>
        public Transform direction;

        /// <summary>The signal ahead, or null in a scene built without one.</summary>
        public SignalLight signal;
    }
}
