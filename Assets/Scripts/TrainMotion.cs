using UnityEngine;

namespace TrainStation
{
    public enum TrainPhase
    {
        /// <summary>Rolls in under braking and comes to a stand on the platform mark.</summary>
        Arrive,
        /// <summary>Stationary, safety valve feathering.</summary>
        Hold,
        /// <summary>Waits out the dwell, then accelerates away.</summary>
        Depart
    }

    /// <summary>
    /// Moves a train rig along one axis and keeps its wheels and steam honest about it.
    /// Speed is the single source of truth: wheel rotation, steam output and the audio
    /// layer all derive from it, so they can never drift out of sync with each other.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainMotion : MonoBehaviour
    {
        [Header("Phase")]
        public TrainPhase phase = TrainPhase.Depart;

        [Header("Arrive")]
        [Tooltip("Speed in m/s as the train first enters shot.")]
        public float entrySpeed = 16f;
        [Tooltip("Seconds spent braking from entrySpeed down to a stand.")]
        public float arriveTime = 12f;

        [Header("Depart")]
        [Tooltip("Seconds held at the platform before pulling away.")]
        public float dwellTime = 4f;
        [Tooltip("Seconds from starting to move to reaching top speed.")]
        public float accelTime = 16f;
        public float topSpeed = 22f;
        [Tooltip("Shape of the pull-away. Flat at the start reads as a heavy train.")]
        public AnimationCurve accelCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.15f),
            new Keyframe(0.45f, 0.4f),
            new Keyframe(1f, 1f, 0.4f, 0f));

        [Header("Common")]
        public Vector3 direction = Vector3.right;
        [Tooltip("Dead time before this train does anything at all.")]
        public float startDelay = 0f;

        [Header("Wheels")]
        [Tooltip("Every wheel on the train. Each one knows its own radius, so drivers and " +
                 "carriage wheels roll correctly off the same distance.")]
        public TrainWheel[] wheels;
        [Tooltip("Radius of the driving wheels, in metres. Only sets the exhaust beat rate.")]
        public float drivingWheelRadius = 0.95f;

        [Header("Steam")]
        public ParticleSystem stack;
        public float idleEmission = 4f;
        public float workingEmission = 95f;

        /// <summary>Current ground speed in m/s. Read by TrainAudio and the wheel spin.</summary>
        public float Speed { get; private set; }

        /// <summary>Metres travelled since the scene started.</summary>
        public float Distance { get; private set; }

        /// <summary>Seconds since the scene started, including startDelay.</summary>
        public float Clock { get; private set; }

        /// <summary>
        /// Ground covered during an arrival, being the integral of entrySpeed*(1-u)^2 over
        /// arriveTime. Builders use this to place the train back down the track so that it
        /// coasts to a stand exactly on the platform mark rather than somewhere near it.
        /// </summary>
        public float ArrivalDistance
        {
            get { return entrySpeed * arriveTime / 3f; }
        }

        /// <summary>True once the train is moving off, so audio can time the whistle.</summary>
        public bool HasStarted
        {
            get { return phase == TrainPhase.Depart && Clock >= startDelay + dwellTime; }
        }

        /// <summary>Fraction of the braking run completed, for cueing brake squeal.</summary>
        public float ArrivalProgress
        {
            get
            {
                if (phase != TrainPhase.Arrive) return 1f;
                return Mathf.Clamp01((Clock - startDelay) / Mathf.Max(0.01f, arriveTime));
            }
        }

        Vector3 _origin;

        void Awake()
        {
            _origin = transform.position;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
            direction = direction.normalized;
        }

        void Update()
        {
            Clock += Time.deltaTime;

            Speed = SpeedAt(Clock);
            Distance += Speed * Time.deltaTime;
            transform.position = _origin + direction * Distance;

            SpinWheels(Speed * Time.deltaTime);
            DriveSteam();
        }

        /// <summary>Ground speed in m/s at <paramref name="t"/> seconds into the scene.</summary>
        public float SpeedAt(float t)
        {
            float local = t - startDelay;
            if (local <= 0f) return 0f;

            switch (phase)
            {
                case TrainPhase.Arrive:
                {
                    // v = entrySpeed * (1-u)^2 brakes hard early then eases onto the mark,
                    // which is what a driver actually does and lands the stop precisely.
                    float u = Mathf.Clamp01(local / Mathf.Max(0.01f, arriveTime));
                    float k = 1f - u;
                    return entrySpeed * k * k;
                }

                case TrainPhase.Depart:
                {
                    if (local < dwellTime) return 0f;
                    float u = Mathf.Clamp01((local - dwellTime) / Mathf.Max(0.01f, accelTime));
                    return accelCurve.Evaluate(u) * topSpeed;
                }

                default:
                    return 0f;
            }
        }

        void SpinWheels(float metresTravelled)
        {
            if (wheels == null || Mathf.Approximately(metresTravelled, 0f)) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null) wheels[i].Roll(metresTravelled);
            }
        }

        void DriveSteam()
        {
            if (stack == null) return;

            // A steam engine works hardest getting a train moving, not at line speed, so
            // effort peaks early in the pull-away and eases off once it is rolling.
            float effort;
            if (phase == TrainPhase.Depart)
            {
                effort = Mathf.Clamp01(Speed / Mathf.Max(0.01f, topSpeed * 0.5f));
            }
            else
            {
                // Coasting in, or standing: just enough to show it is still in steam.
                effort = 0f;
            }

            var emission = stack.emission;
            emission.rateOverTime = Mathf.Lerp(idleEmission, workingEmission, effort);

            var main = stack.main;
            main.startSpeed = Mathf.Lerp(1.8f, 6.5f, effort);
        }
    }
}
