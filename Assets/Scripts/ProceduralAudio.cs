using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Every sound in this film is synthesised here at runtime. Nothing is loaded from disk,
    /// which is what keeps the project free of downloaded assets.
    ///
    /// The building blocks are deliberately plain: white noise, sine partials, a state-variable
    /// filter and amplitude envelopes. A steam whistle really is a few detuned tones plus breath,
    /// and a chuff really is a filtered noise burst, so this gets closer than it has any right to.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        // ------------------------------------------------------------------ public clips

        /// <summary>One exhaust beat. Pitched and repeated by TrainAudio to match wheel speed.</summary>
        public static AudioClip Chuff(float seconds = 0.5f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(1207);

            float lp = 0f, bp = 0f;

            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;

                // Near-instant attack, long exponential tail: the shape of a cylinder emptying.
                float env = Mathf.Min(1f, u / 0.015f) * Mathf.Exp(-6.5f * u);

                // The band sweeps down as the blast loses pressure.
                float f = Mathf.Lerp(0.16f, 0.035f, u);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                Svf(noise, f, 0.85f, ref lp, ref bp);

                data[i] = bp * env;
            }

            Normalise(data, 0.85f);
            return Make("Chuff", data);
        }

        /// <summary>A three-tone steam whistle with vibrato and breath noise behind it.</summary>
        public static AudioClip Whistle(float seconds = 2.3f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(77);

            // A real chime whistle is several bells sounding at once, slightly out of tune.
            float[] partials = { 523.3f, 659.3f, 784.0f, 1046.5f };
            float[] weights = { 1.0f, 0.75f, 0.55f, 0.28f };

            float lp = 0f, bp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float u = (float)i / n;

                // Slow swell on, hold, then a tail that falls away.
                float attack = Mathf.Min(1f, u / 0.10f);
                float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 1f, u));
                float env = attack * release;

                // A whistle is never perfectly steady; the wobble is most of the character.
                float vib = 1f + 0.007f * Mathf.Sin(2f * Mathf.PI * 5.4f * t)
                               + 0.003f * Mathf.Sin(2f * Mathf.PI * 11.3f * t);

                float tone = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    // Detune each partial slightly so they beat against one another.
                    float detune = 1f + (p - 1.5f) * 0.0016f;
                    tone += weights[p] * Mathf.Sin(2f * Mathf.PI * partials[p] * detune * vib * t);
                }
                tone /= 2.6f;

                // Breath: band-passed noise sitting under the tones.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                Svf(noise, 0.22f, 0.6f, ref lp, ref bp);

                data[i] = (tone * 0.82f + bp * 0.30f) * env;
            }

            Normalise(data, 0.8f);
            return Make("Whistle", data);
        }

        /// <summary>Cast-iron brake blocks on a steel tyre: a high, wandering resonance.</summary>
        public static AudioClip BrakeSqueal(float seconds = 3.2f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(4242);

            float lp = 0f, bp = 0f;
            float lp2 = 0f, bp2 = 0f;
            // The grind needs its own filter state; sharing it with the resonant pair above
            // would trample the ringing that makes this a squeal rather than noise.
            float lp3 = 0f, bp3 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float u = (float)i / n;

                // Swells in, then dies as the train loses the speed that was driving it.
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)) * (1f - u * 0.35f);

                // The squeal is unstable: it drifts and occasionally jumps.
                float drift = 0.30f + 0.05f * Mathf.Sin(2f * Mathf.PI * 0.7f * t)
                                    + 0.03f * Mathf.Sin(2f * Mathf.PI * 2.3f * t);

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Two very resonant passes stacked: one filter alone sounds like noise, two ring.
                Svf(noise, drift, 0.03f, ref lp, ref bp);
                Svf(bp, drift * 1.005f, 0.03f, ref lp2, ref bp2);

                // A little grind underneath so it is not purely a whistle.
                Svf(noise, 0.02f, 0.9f, ref lp3, ref bp3);
                float grind = lp3 * 0.4f;

                data[i] = (bp2 * 0.9f + grind) * env;
            }

            Normalise(data, 0.7f);
            return Make("BrakeSqueal", data);
        }

        /// <summary>Safety valve feathering. Loops seamlessly.</summary>
        public static AudioClip Hiss(float seconds = 4f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(909);

            float lp = 0f, bp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                Svf(noise, 0.30f, 0.55f, ref lp, ref bp);

                // Gentle breathing so it does not sit dead flat under the scene.
                float wobble = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 0.31f * t);

                data[i] = bp * wobble;
            }

            LoopFade(ref data, 0.25f);
            Normalise(data, 0.5f);
            return Make("Hiss", data);
        }

        /// <summary>Station room tone: low rumble plus a wind bed. Loops seamlessly.</summary>
        public static AudioClip Ambience(float seconds = 8f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(31337);

            float lp = 0f, bp = 0f;
            float lpWind = 0f, bpWind = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Deep rumble, felt more than heard.
                Svf(noise, 0.008f, 0.9f, ref lp, ref bp);

                // Wind across an open platform.
                float noise2 = (float)(rng.NextDouble() * 2.0 - 1.0);
                Svf(noise2, 0.06f + 0.02f * Mathf.Sin(2f * Mathf.PI * 0.13f * t), 0.8f, ref lpWind, ref bpWind);

                data[i] = lp * 1.6f + lpWind * 0.5f;
            }

            LoopFade(ref data, 0.5f);
            Normalise(data, 0.34f);
            return Make("Ambience", data);
        }

        /// <summary>Two-note platform announcement chime.</summary>
        public static AudioClip Chime(float seconds = 3f)
        {
            int n = Samples(seconds);
            var data = new float[n];

            // A falling fourth, the shape almost every station uses.
            AddBell(data, 987.8f, 0.00f, 1.5f);
            AddBell(data, 739.99f, 0.42f, 1.9f);

            Normalise(data, 0.55f);
            return Make("Chime", data);
        }

        /// <summary>A slam door closing: a thump with a latch on top.</summary>
        public static AudioClip DoorSlam(float seconds = 0.6f)
        {
            int n = Samples(seconds);
            var data = new float[n];
            var rng = new System.Random(5150);

            float lp = 0f, bp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float u = (float)i / n;

                float env = Mathf.Min(1f, u / 0.004f) * Mathf.Exp(-14f * u);

                // Body of the door: a low thump that drops in pitch as it seats.
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 62f, u) * t);

                // Latch: a short bright rattle.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                Svf(noise, 0.42f, 0.5f, ref lp, ref bp);

                data[i] = (thump * 0.8f + bp * 0.5f * Mathf.Exp(-30f * u)) * env;
            }

            Normalise(data, 0.75f);
            return Make("DoorSlam", data);
        }

        // ------------------------------------------------------------------------ helpers

        static int Samples(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
        }

        /// <summary>
        /// One step of a state-variable filter. Cheap, stable at these settings, and gives a
        /// band-pass and a low-pass from the same two state variables.
        /// <paramref name="f"/> is roughly cutoff/nyquist; lower <paramref name="q"/> = more resonant.
        /// </summary>
        static void Svf(float input, float f, float q, ref float lp, ref float bp)
        {
            f = Mathf.Clamp(f, 0.0005f, 0.45f);
            q = Mathf.Clamp(q, 0.02f, 2f);

            float hp = input - lp - q * bp;
            bp += f * hp;
            lp += f * bp;
        }

        static void AddBell(float[] data, float freq, float startSeconds, float decay)
        {
            int start = Samples(startSeconds);

            for (int i = start; i < data.Length; i++)
            {
                float t = (float)(i - start) / SampleRate;
                float env = Mathf.Exp(-decay * t);

                // Fundamental plus a quiet, slightly sharp overtone: that is what makes it a bell
                // rather than a beep.
                float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.32f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-decay * 2.2f * t);

                data[i] += s * env * 0.5f;
            }
        }

        /// <summary>
        /// Crossfades the tail of a clip over its head so a looping source does not click at
        /// the seam. Costs the last <paramref name="fadeSeconds"/> of material.
        /// Takes the array by ref because it trims it; without that the resize would be lost
        /// and the seam would still click.
        /// </summary>
        static void LoopFade(ref float[] data, float fadeSeconds)
        {
            int fade = Mathf.Min(Samples(fadeSeconds), data.Length / 2);
            if (fade <= 1) return;

            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                int tail = data.Length - fade + i;
                data[i] = Mathf.Lerp(data[tail], data[i], k);
            }

            // The faded tail has been folded into the head, so drop it.
            System.Array.Resize(ref data, data.Length - fade);
        }

        static void Normalise(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > max) max = a;
            }

            if (max < 1e-6f) return;

            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
