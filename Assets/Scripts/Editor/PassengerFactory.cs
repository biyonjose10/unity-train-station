using UnityEngine;

namespace TrainStation.Build
{
    /// <summary>
    /// Builds one articulated passenger.
    ///
    /// The rig is deliberately minimal — hip, torso, head, two arms, two jointed legs — but the
    /// joints have to pivot in the right places: each limb's pivot sits at the joint with the
    /// box hanging below it. Get that wrong and the leg rotates about its middle, which reads as
    /// a swimming motion rather than a stride.
    ///
    /// Built facing +Z. PassengerWalker turns the root to face travel.
    /// </summary>
    public static class PassengerFactory
    {
        // Proportions of a roughly 1.75 m figure, in metres.
        const float HipY = 0.92f;
        const float ThighLen = 0.45f;
        const float ShinLen = 0.45f;
        const float TorsoLen = 0.55f;
        const float ArmLen = 0.58f;

        static readonly Color[] Coats =
        {
            new Color(0.16f, 0.19f, 0.28f),   // navy
            new Color(0.28f, 0.13f, 0.12f),   // maroon
            new Color(0.20f, 0.20f, 0.19f),   // charcoal
            new Color(0.33f, 0.26f, 0.16f),   // tweed
            new Color(0.13f, 0.24f, 0.21f),   // dark green
            new Color(0.42f, 0.35f, 0.28f),   // fawn
            new Color(0.30f, 0.16f, 0.24f)    // plum
        };

        static readonly Color[] Skins =
        {
            new Color(0.68f, 0.52f, 0.40f),
            new Color(0.53f, 0.37f, 0.26f),
            new Color(0.78f, 0.62f, 0.49f),
            new Color(0.38f, 0.26f, 0.19f),
            new Color(0.60f, 0.45f, 0.33f)
        };

        /// <summary>
        /// One passenger, parented to <paramref name="parent"/> and standing at
        /// <paramref name="position"/>. The walker component is attached but has no route yet.
        /// </summary>
        public static PassengerWalker Build(Transform parent, string name, Vector3 position,
                                            System.Random rng, bool withLuggage)
        {
            var root = Prim.Empty(parent, name);
            root.transform.position = position;

            // A crowd of identical heights looks like a row of bollards.
            float scale = 0.90f + (float)rng.NextDouble() * 0.20f;
            root.transform.localScale = Vector3.one * scale;

            int coatIndex = rng.Next(Coats.Length);
            Color coatColour = Coats[coatIndex];
            Color trouserColour = coatColour * 0.55f;
            trouserColour.a = 1f;

            var coat = Prim.Mat("Coat" + coatIndex, coatColour, 0f, 0.16f);
            var trouser = Prim.Mat("Trouser" + coatIndex, trouserColour, 0f, 0.14f);
            var skin = Prim.Mat("Skin" + rng.Next(Skins.Length), Skins[rng.Next(Skins.Length)], 0f, 0.22f);
            var shoe = Prim.Mat("Shoe", new Color(0.08f, 0.07f, 0.07f), 0f, 0.3f);

            var walker = root.AddComponent<PassengerWalker>();

            // ---- hip: everything hangs off this, and the walker bobs and leans it
            var hip = Prim.Empty(root.transform, "Hip", new Vector3(0f, HipY, 0f)).transform;
            walker.hip = hip;

            // ---- torso and head
            Prim.Box(hip, "Torso", new Vector3(0f, TorsoLen * 0.5f, 0f),
                     new Vector3(0.40f, TorsoLen, 0.23f), coat);
            Prim.Box(hip, "Hips", new Vector3(0f, 0.04f, 0f),
                     new Vector3(0.34f, 0.20f, 0.22f), trouser);

            var neck = Prim.Empty(hip, "Head", new Vector3(0f, TorsoLen + 0.10f, 0f)).transform;
            walker.head = neck;
            Prim.Box(neck, "Skull", new Vector3(0f, 0.11f, 0f), new Vector3(0.21f, 0.24f, 0.21f), skin);

            // Roughly a third of a 1950s platform crowd would be in a hat.
            if (rng.NextDouble() < 0.35)
            {
                Prim.Cyl(neck, "HatBrim", new Vector3(0f, 0.23f, 0f), 0.19f, 0.02f, Prim.AxisY, coat);
                Prim.Cyl(neck, "HatCrown", new Vector3(0f, 0.30f, 0f), 0.12f, 0.13f, Prim.AxisY, coat);
            }

            // ---- arms: pivot at the shoulder, limb hanging below
            // A shade darker than the coat: identical colouring made the arms disappear into
            // the torso and the figures read as legs with a box on top.
            var sleeve = Prim.Mat("Sleeve" + coatIndex, coatColour * 0.74f, 0f, 0.16f);

            walker.leftArm = Limb(hip, "LeftArm", new Vector3(0.28f, TorsoLen - 0.06f, 0f),
                                  ArmLen, 0.12f, sleeve);
            walker.rightArm = Limb(hip, "RightArm", new Vector3(-0.28f, TorsoLen - 0.06f, 0f),
                                   ArmLen, 0.12f, sleeve);

            // ---- legs: thigh pivots at the hip, shin pivots at the knee
            walker.leftThigh = Limb(hip, "LeftThigh", new Vector3(0.11f, 0f, 0f),
                                    ThighLen, 0.15f, trouser);
            walker.leftShin = Limb(walker.leftThigh, "LeftShin", new Vector3(0f, -ThighLen, 0f),
                                   ShinLen, 0.13f, trouser);
            Foot(walker.leftShin, shoe);

            walker.rightThigh = Limb(hip, "RightThigh", new Vector3(-0.11f, 0f, 0f),
                                     ThighLen, 0.15f, trouser);
            walker.rightShin = Limb(walker.rightThigh, "RightShin", new Vector3(0f, -ThighLen, 0f),
                                    ShinLen, 0.13f, trouser);
            Foot(walker.rightShin, shoe);

            if (withLuggage)
            {
                var caseColour = rng.NextDouble() < 0.5
                    ? new Color(0.26f, 0.16f, 0.10f)
                    : new Color(0.17f, 0.15f, 0.14f);

                var suitcase = Prim.Box(walker.rightArm, "Suitcase",
                                        new Vector3(-0.14f, -ArmLen - 0.14f, 0f),
                                        new Vector3(0.16f, 0.30f, 0.42f),
                                        Prim.Mat("Suitcase" + rng.Next(2), caseColour, 0f, 0.2f));

                // A carried case swings with the arm, so it lives under the arm pivot.
                suitcase.transform.localRotation = Quaternion.identity;
            }

            return walker;
        }

        /// <summary>
        /// A limb segment. Returns the pivot to rotate; the visible box hangs below it so that
        /// rotating the pivot swings the limb from its joint.
        /// </summary>
        static Transform Limb(Transform parent, string name, Vector3 pivotPos,
                              float length, float thickness, Material mat)
        {
            var pivot = Prim.Empty(parent, name, pivotPos).transform;
            Prim.Box(pivot, name + "_Mesh", new Vector3(0f, -length * 0.5f, 0f),
                     new Vector3(thickness, length, thickness * 1.15f), mat);
            return pivot;
        }

        static void Foot(Transform shin, Material shoe)
        {
            Prim.Box(shin, "Foot", new Vector3(0f, -ShinLen - 0.02f, 0.06f),
                     new Vector3(0.14f, 0.07f, 0.26f), shoe);
        }
    }
}
