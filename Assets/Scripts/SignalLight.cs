using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// A two-aspect colour-light signal. Sits at red while our train is held, then clears to
    /// green once the other train is out of the section, which is the whole point of scene 2:
    /// it gives the audience a reason for the wait rather than just a pause.
    ///
    /// Lens brightness is driven through a MaterialPropertyBlock so the two signals in a scene
    /// do not end up sharing one mutated material and changing aspect together.
    /// </summary>
    public class SignalLight : MonoBehaviour
    {
        [Header("Lenses")]
        public Renderer redLens;
        public Renderer greenLens;
        public Light redGlow;
        public Light greenGlow;

        [Header("Colours")]
        public Color redOn = new Color(1f, 0.10f, 0.06f);
        public Color greenOn = new Color(0.20f, 1f, 0.35f);
        [Tooltip("Brightness of an unlit lens. Not quite black, so the glass still catches light.")]
        public float offLevel = 0.03f;

        [Header("Timing")]
        [Tooltip("Seconds into the scene at which the signal clears. Negative = never changes.")]
        public float clearsAt = 9f;
        public bool startsAtDanger = true;
        [Tooltip("Seconds the lamps take to swap over.")]
        public float changeSeconds = 0.35f;

        [Header("Glow")]
        public float glowIntensity = 2.2f;
        public float emissionBoost = 3.5f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock _block;
        float _clock;
        float _green;        // 0 = red showing, 1 = green showing
        bool _cleared;

        /// <summary>True once the signal is showing a proceed aspect.</summary>
        public bool IsClear { get { return _green > 0.5f; } }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _green = startsAtDanger ? 0f : 1f;
            _cleared = !startsAtDanger;
            Apply();
        }

        void Update()
        {
            _clock += Time.deltaTime;

            if (!_cleared && clearsAt >= 0f && _clock >= clearsAt)
            {
                _cleared = true;
            }

            float target = _cleared ? 1f : 0f;
            float rate = changeSeconds <= 0f ? 1f : Time.deltaTime / changeSeconds;

            float next = Mathf.MoveTowards(_green, target, rate);
            if (!Mathf.Approximately(next, _green))
            {
                _green = next;
                Apply();
            }
        }

        /// <summary>Clear the signal now, regardless of the timer.</summary>
        public void Clear()
        {
            _cleared = true;
        }

        void Apply()
        {
            SetLens(redLens, redOn, 1f - _green);
            SetLens(greenLens, greenOn, _green);

            if (redGlow != null)
            {
                redGlow.color = redOn;
                redGlow.intensity = glowIntensity * (1f - _green);
                redGlow.enabled = redGlow.intensity > 0.01f;
            }

            if (greenGlow != null)
            {
                greenGlow.color = greenOn;
                greenGlow.intensity = glowIntensity * _green;
                greenGlow.enabled = greenGlow.intensity > 0.01f;
            }
        }

        void SetLens(Renderer r, Color on, float level)
        {
            if (r == null) return;

            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, on * Mathf.Lerp(offLevel, emissionBoost, level));
            r.SetPropertyBlock(_block);
        }
    }
}
