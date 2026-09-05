using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// One slam door, hinged at its leading edge and swinging out over the platform.
    ///
    /// Without this a passenger walks straight into a painted panel, which rather undermines the
    /// one scene whose whole subject is people getting on a train. Doors stand open through the
    /// boarding and are slammed shut one by one before departure — which is both what actually
    /// happened on stock like this, and a good way to end the scene.
    /// </summary>
    public class CarriageDoor : MonoBehaviour
    {
        [Tooltip("Degrees the door stands open. Positive swings out over the platform.")]
        public float openAngle = 82f;
        [Tooltip("Degrees per second. Opening is unhurried; slamming is not.")]
        public float openSpeed = 90f;
        public float closeSpeed = 420f;

        [SerializeField] bool _open;

        float _angle;

        /// <summary>True while the door is standing open.</summary>
        public bool IsOpen { get { return _open; } }

        /// <summary>True once the door has finished travelling to wherever it was last sent.</summary>
        public bool Settled
        {
            get { return Mathf.Approximately(_angle, _open ? openAngle : 0f); }
        }

        public void Open() { _open = true; }
        public void Close() { _open = false; }

        void Update()
        {
            float target = _open ? openAngle : 0f;
            if (Mathf.Approximately(_angle, target)) return;

            float speed = _open ? openSpeed : closeSpeed;
            _angle = Mathf.MoveTowards(_angle, target, speed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
