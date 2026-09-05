using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Runs the boarding scene: the announcement chime that opens it, a door slam each time
    /// somebody disappears inside, and the guard's whistle that closes it.
    ///
    /// This exists because <see cref="PassengerWalker.onBoarded"/> is a delegate, which cannot be
    /// wired up from a build script and saved into a scene. Something has to connect the two at
    /// runtime, and that something is this.
    /// </summary>
    public class BoardingDirector : MonoBehaviour
    {
        [Header("Wiring")]
        public TrainAudio trainAudio;
        public PassengerWalker[] passengers;
        public CarriageDoor[] doors;

        [Header("Cues (seconds into the scene)")]
        public float chimeAt = 1.2f;
        [Tooltip("Guard's whistle near the end. Negative to skip it.")]
        public float guardWhistleAt = 24f;

        [Header("Doors")]
        [Tooltip("Doors stand open from here, ready for the passengers.")]
        public float doorsOpenAt = 0.4f;
        [Tooltip("Slamming starts here and works down the train.")]
        public float doorsCloseFrom = 21f;
        [Tooltip("Gap between one door slamming and the next.")]
        public float slamInterval = 0.55f;

        [Header("Doors")]
        [Tooltip("Minimum gap between slams, so a rush of boarders does not machine-gun them.")]
        public float minSlamGap = 0.4f;
        [Range(0f, 1f)] public float slamChance = 0.75f;

        float _clock;
        float _lastSlam = -99f;
        bool _chimed;
        bool _whistled;
        bool _doorsOpened;
        int _nextToSlam;

        void Start()
        {
            if (passengers == null) return;

            for (int i = 0; i < passengers.Length; i++)
            {
                var p = passengers[i];
                if (p != null) p.onBoarded = OnSomebodyBoarded;
            }
        }

        void Update()
        {
            _clock += Time.deltaTime;

            if (!_chimed && _clock >= chimeAt)
            {
                _chimed = true;
                if (trainAudio != null) trainAudio.PlayChime();
            }

            DriveDoors();

            if (!_whistled && guardWhistleAt >= 0f && _clock >= guardWhistleAt)
            {
                _whistled = true;
                if (trainAudio != null) trainAudio.PlayWhistle(0.7f);
            }
        }

        void DriveDoors()
        {
            if (doors == null || doors.Length == 0) return;

            if (!_doorsOpened && _clock >= doorsOpenAt)
            {
                _doorsOpened = true;
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null) doors[i].Open();
                }
            }

            // Slamming them one at a time down the train, each with its own bang, is the sound of
            // a train about to leave — and it gives the scene an ending.
            if (_clock < doorsCloseFrom || _nextToSlam >= doors.Length) return;

            float due = doorsCloseFrom + _nextToSlam * slamInterval;
            if (_clock < due) return;

            var door = doors[_nextToSlam];
            _nextToSlam++;

            if (door == null) return;

            door.Close();
            if (trainAudio != null) trainAudio.PlayDoorSlam(0.6f);
            _lastSlam = _clock;
        }

        void OnSomebodyBoarded()
        {
            if (trainAudio == null) return;

            // Not every passenger shuts a door behind them, and two slams in the same instant
            // read as a glitch rather than a busy platform.
            if (_clock - _lastSlam < minSlamGap) return;
            if (Random.value > slamChance) return;

            _lastSlam = _clock;
            trainAudio.PlayDoorSlam();
        }
    }
}
