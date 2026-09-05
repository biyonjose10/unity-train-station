using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Holds a shot from the platform.
    ///
    /// The damped look-at is the point: a camera welded to its subject looks like a video game,
    /// while one that turns a beat late lets the train pull ahead of frame and reads like someone
    /// standing there watching it go. Drift and a little Perlin handheld noise finish the job of
    /// stopping it feeling locked off.
    /// </summary>
    public class CameraDolly : MonoBehaviour
    {
        [Header("Framing")]
        public Transform lookTarget;
        [Tooltip("Higher = lazier turn, so the subject slides further off centre.")]
        public float lookDamping = 1.9f;
        public Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        [Tooltip("Stop tracking once the subject is further away than this. 0 = always track.")]
        public float releaseDistance = 0f;

        [Header("Drift")]
        public Vector3 driftPerSecond = new Vector3(0.22f, 0.015f, -0.03f);
        [Tooltip("Seconds to hold still before the drift starts.")]
        public float driftDelay = 3.5f;

        [Header("Handheld")]
        public float shakeAmplitude = 0.018f;
        public float shakeFrequency = 0.55f;

        [Header("Lens")]
        [Tooltip("Slow push in on the subject. 0 = fixed focal length.")]
        public float fovDriftPerSecond = 0f;
        public float minFov = 22f;

        float _clock;
        Vector3 _basePosition;
        Quaternion _look;
        float _seed;
        Camera _cam;

        void Awake()
        {
            _basePosition = transform.position;
            _look = transform.rotation;
            _seed = Random.value * 100f;
            _cam = GetComponent<Camera>();

            // Aim properly on frame one, otherwise the damping spends the first second of the
            // shot swinging round from whatever rotation the builder left behind.
            if (lookTarget != null)
            {
                Vector3 aim = lookTarget.position + targetOffset - transform.position;
                if (aim.sqrMagnitude > 0.0001f) _look = Quaternion.LookRotation(aim, Vector3.up);
                transform.rotation = _look;
            }
        }

        void LateUpdate()
        {
            _clock += Time.deltaTime;

            if (_clock > driftDelay)
            {
                _basePosition += driftPerSecond * Time.deltaTime;
            }

            Vector3 shake = new Vector3(
                Mathf.PerlinNoise(_seed, _clock * shakeFrequency) - 0.5f,
                Mathf.PerlinNoise(_seed + 17f, _clock * shakeFrequency) - 0.5f,
                Mathf.PerlinNoise(_seed + 43f, _clock * shakeFrequency) - 0.5f) * (shakeAmplitude * 2f);

            transform.position = _basePosition + shake;

            if (lookTarget != null)
            {
                Vector3 aim = lookTarget.position + targetOffset - transform.position;

                // Past the release distance the operator gives up and lets it go, rather than
                // creeping round to follow a dot in the fog.
                bool tracking = releaseDistance <= 0f || aim.magnitude < releaseDistance;

                if (tracking && aim.sqrMagnitude > 0.0001f)
                {
                    Quaternion want = Quaternion.LookRotation(aim, Vector3.up);
                    _look = Quaternion.Slerp(_look, want, Time.deltaTime / Mathf.Max(0.01f, lookDamping));
                }
            }

            transform.rotation = _look;

            if (_cam != null && !Mathf.Approximately(fovDriftPerSecond, 0f))
            {
                _cam.fieldOfView = Mathf.Max(minFov, _cam.fieldOfView + fovDriftPerSecond * Time.deltaTime);
            }
        }
    }
}
