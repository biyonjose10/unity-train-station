using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Walks one blocky passenger along a path and puts them on the train.
    ///
    /// The walk is driven by distance covered rather than by a clock, so the feet stay planted
    /// at whatever speed the figure happens to be moving and there is no skating. Everything is
    /// a sine wave off a single stride phase: legs, arms, hip bob and body lean all come from it,
    /// which is why a handful of boxes reads as a person walking.
    /// </summary>
    public class PassengerWalker : MonoBehaviour
    {
        public enum State { Waiting, Walking, Boarding, Gone }

        [Header("Rig")]
        public Transform hip;
        public Transform leftThigh, rightThigh;
        public Transform leftShin, rightShin;
        public Transform leftArm, rightArm;
        public Transform head;

        [Header("Gait")]
        public float walkSpeed = 1.25f;
        [Tooltip("Metres per complete stride cycle. Wrong value shows up as sliding feet.")]
        public float strideLength = 1.45f;
        public float legSwing = 34f;
        public float kneeBend = 46f;
        public float armSwing = 26f;
        public float hipBob = 0.035f;
        public float bodyLean = 4f;

        [Header("Route")]
        [Tooltip("World-space points. The last one is the carriage door.")]
        public Vector3[] path;
        [Tooltip("Seconds to stand on the platform before setting off.")]
        public float waitBefore = 2f;
        public float turnSpeed = 8f;
        public float arriveDistance = 0.12f;

        [Header("Boarding")]
        [Tooltip("Rise from platform level up to the carriage floor.")]
        public float stepUpHeight = 0.22f;
        [Tooltip("How far they carry on into the carriage before vanishing inside.")]
        public float stepInDistance = 1.1f;
        public float boardSeconds = 1.0f;

        /// <summary>Fires as the passenger disappears into the carriage, for a door slam cue.</summary>
        public System.Action onBoarded;

        State _state = State.Waiting;
        int _leg;
        float _clock;
        float _phase;
        float _boardClock;
        Vector3 _boardFrom;
        Vector3 _boardHeading;
        Vector3 _boardScale;
        float _fidgetSeed;
        Vector3 _restHip;

        public State CurrentState { get { return _state; } }

        void Awake()
        {
            _fidgetSeed = Random.value * 100f;
            if (hip != null) _restHip = hip.localPosition;
        }

        void Update()
        {
            _clock += Time.deltaTime;

            switch (_state)
            {
                case State.Waiting:  TickWaiting();  break;
                case State.Walking:  TickWalking();  break;
                case State.Boarding: TickBoarding(); break;
            }
        }

        void TickWaiting()
        {
            Idle();

            if (_clock >= waitBefore && path != null && path.Length > 0)
            {
                _state = State.Walking;
                _leg = 0;
            }
        }

        /// <summary>Standing still is never truly still; a little sway stops them reading as props.</summary>
        void Idle()
        {
            // Settle the limbs first. Pose also writes the hip, so calling it after the sway
            // below would immediately overwrite it and the fidget would never be visible.
            Pose(0f, 0f);

            float t = Time.time * 0.6f + _fidgetSeed;

            if (hip != null)
            {
                float sway = Mathf.Sin(t) * 0.012f;
                hip.localPosition = _restHip + new Vector3(sway, Mathf.Abs(Mathf.Sin(t * 0.5f)) * 0.008f, 0f);
            }

            if (head != null)
            {
                // Occasional glance down the line for the train.
                float look = Mathf.Sin(t * 0.37f) * 18f + Mathf.Sin(t * 0.11f) * 10f;
                head.localRotation = Quaternion.Euler(0f, look, 0f);
            }
        }

        void TickWalking()
        {
            if (path == null || _leg >= path.Length)
            {
                BeginBoarding();
                return;
            }

            Vector3 target = path[_leg];
            Vector3 flat = new Vector3(target.x - transform.position.x, 0f, target.z - transform.position.z);

            if (flat.magnitude <= arriveDistance)
            {
                _leg++;
                if (_leg >= path.Length) BeginBoarding();
                return;
            }

            Vector3 dir = flat.normalized;

            // Turn towards travel, then move: turning first stops the figure crabbing sideways.
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * turnSpeed);

            float step = walkSpeed * Time.deltaTime;
            transform.position += dir * step;

            // One full stride cycle per strideLength covered.
            _phase += (step / Mathf.Max(0.05f, strideLength)) * Mathf.PI * 2f;

            Pose(_phase, 1f);
        }

        void BeginBoarding()
        {
            if (_state == State.Boarding || _state == State.Gone) return;

            _state = State.Boarding;
            _boardClock = 0f;
            _boardFrom = transform.position;
            _boardHeading = transform.forward;
            _boardScale = transform.localScale;
        }

        void TickBoarding()
        {
            _boardClock += Time.deltaTime;
            float u = Mathf.Clamp01(_boardClock / Mathf.Max(0.05f, boardSeconds));
            float eased = Mathf.SmoothStep(0f, 1f, u);

            // Step up onto the carriage floor and keep walking in, rather than rising on the spot.
            transform.position = _boardFrom
                               + Vector3.up * (stepUpHeight * eased)
                               + _boardHeading * (stepInDistance * eased);

            // Shrinking as they go sells passing through the doorway into a dark interior
            // without needing an actual interior to walk into.
            transform.localScale = _boardScale * Mathf.Lerp(1f, 0.7f, eased);

            // Keep the legs moving through the step rather than freezing mid-stride.
            _phase += Time.deltaTime * 6f;
            Pose(_phase, 1f - u);

            if (u >= 1f)
            {
                _state = State.Gone;
                if (onBoarded != null) onBoarded();
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Applies the whole gait from one phase value. <paramref name="weight"/> blends the
        /// swing out so a stopping figure settles rather than snapping to attention.
        /// </summary>
        void Pose(float phase, float weight)
        {
            float s = Mathf.Sin(phase) * weight;
            float sOpp = Mathf.Sin(phase + Mathf.PI) * weight;

            SetPitch(leftThigh, s * legSwing);
            SetPitch(rightThigh, sOpp * legSwing);

            // The knee bends on the swing-through, when the leg is travelling forwards, and
            // stays near straight while the foot is carrying weight.
            SetPitch(leftShin, -Mathf.Max(0f, Mathf.Cos(phase)) * kneeBend * weight);
            SetPitch(rightShin, -Mathf.Max(0f, Mathf.Cos(phase + Mathf.PI)) * kneeBend * weight);

            // Arms counter the legs.
            SetPitch(leftArm, sOpp * armSwing);
            SetPitch(rightArm, s * armSwing);

            if (hip != null)
            {
                // Two bobs per stride: one per footfall.
                float bob = Mathf.Abs(Mathf.Sin(phase)) * hipBob * weight;
                hip.localPosition = _restHip + new Vector3(0f, bob, 0f);
                hip.localRotation = Quaternion.Euler(bodyLean * weight, 0f, 0f);
            }

            if (head != null && weight > 0.5f)
            {
                head.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f) * 4f, 0f);
            }
        }

        static void SetPitch(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
