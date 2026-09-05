using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Turns the numbers coming out of <see cref="TrainMotion"/> into sound.
    ///
    /// The important trick is that chuffs are scheduled by <em>distance</em>, not by a timer:
    /// a steam engine fires four exhaust beats per revolution of its driving wheels, so if you
    /// emit one beat every quarter-circumference the rhythm accelerates with the train for free
    /// and can never drift out of step with the wheels the audience is watching.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class TrainAudio : MonoBehaviour
    {
        [Header("Source")]
        public TrainMotion motion;

        [Header("Chuffs")]
        [Tooltip("Exhaust beats per revolution of the driving wheels. A two-cylinder loco gives four.")]
        public float chuffsPerRevolution = 4f;
        [Tooltip("Overlapping voices, so a fast chuff does not cut off the tail of the last one.")]
        public int chuffVoices = 6;
        public float chuffVolume = 0.75f;

        [Header("What this train does")]
        public bool whistleOnDeparture = true;
        public bool brakesOnArrival = true;
        public bool safetyValveWhenStanding = true;
        public bool stationAmbience = true;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Tooltip("Metres at which train sounds fade to nothing.")]
        public float audibleRange = 260f;

        AudioSource[] _chuffPool;
        int _nextVoice;

        AudioSource _oneShots;   // whistle, brakes, doors: 3D, travels with the train
        AudioSource _valve;      // looping hiss
        AudioSource _ambience;   // looping room tone, 2D

        AudioClip _chuff, _whistle, _brake, _hiss, _room, _chime, _slam;

        float _distanceSinceChuff;
        bool _whistleDone;
        bool _brakeDone;

        void Awake()
        {
            if (motion == null) motion = GetComponent<TrainMotion>();

            // Synthesised once here, then shared by every voice below.
            _chuff = ProceduralAudio.Chuff();
            _whistle = ProceduralAudio.Whistle();
            _brake = ProceduralAudio.BrakeSqueal();
            _hiss = ProceduralAudio.Hiss();
            _room = ProceduralAudio.Ambience();
            _chime = ProceduralAudio.Chime();
            _slam = ProceduralAudio.DoorSlam();

            _oneShots = GetComponent<AudioSource>();
            Configure3D(_oneShots);

            _chuffPool = new AudioSource[Mathf.Max(1, chuffVoices)];
            for (int i = 0; i < _chuffPool.Length; i++)
            {
                var go = new GameObject("ChuffVoice" + i);
                go.transform.SetParent(transform, false);
                _chuffPool[i] = go.AddComponent<AudioSource>();
                Configure3D(_chuffPool[i]);
                _chuffPool[i].clip = _chuff;
            }

            if (safetyValveWhenStanding)
            {
                var go = new GameObject("SafetyValve");
                go.transform.SetParent(transform, false);
                _valve = go.AddComponent<AudioSource>();
                Configure3D(_valve);
                _valve.clip = _hiss;
                _valve.loop = true;
                _valve.volume = 0f;
                _valve.Play();
            }

            if (stationAmbience)
            {
                var go = new GameObject("StationAmbience");
                go.transform.SetParent(transform, false);
                _ambience = go.AddComponent<AudioSource>();
                _ambience.clip = _room;
                _ambience.loop = true;
                _ambience.spatialBlend = 0f;      // room tone should not swing around the head
                _ambience.volume = 0.35f * masterVolume;
                _ambience.Play();
            }
        }

        void Configure3D(AudioSource s)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 8f;
            s.maxDistance = audibleRange;
            s.dopplerLevel = 0.6f;   // enough to hear the pitch drop as it goes past, not a cartoon
        }

        void Update()
        {
            if (motion == null) return;

            float speed = motion.Speed;

            ScheduleChuffs(speed);
            DriveSafetyValve(speed);
            CueOneShots();
        }

        void ScheduleChuffs(float speed)
        {
            if (speed <= 0.05f || _chuffPool == null) return;

            float circumference = 2f * Mathf.PI * Mathf.Max(0.05f, motion.drivingWheelRadius);
            float metresPerBeat = circumference / Mathf.Max(1f, chuffsPerRevolution);

            _distanceSinceChuff += speed * Time.deltaTime;

            // A while loop rather than an if: at line speed more than one beat can fall inside
            // a single frame, and dropping those would thin the rhythm out as the train speeds up.
            int guard = 0;
            while (_distanceSinceChuff >= metresPerBeat && guard++ < 8)
            {
                _distanceSinceChuff -= metresPerBeat;
                FireChuff(speed);
            }
        }

        void FireChuff(float speed)
        {
            var voice = _chuffPool[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _chuffPool.Length;

            float fast = Mathf.Clamp01(speed / Mathf.Max(1f, motion.topSpeed));

            // Beats get shorter and slightly brighter as she picks up, and the blast is heaviest
            // at the start when the engine is actually fighting the load.
            voice.pitch = Mathf.Lerp(0.82f, 1.25f, fast);
            voice.volume = chuffVolume * masterVolume * Mathf.Lerp(1f, 0.55f, fast);
            voice.Play();
        }

        void DriveSafetyValve(float speed)
        {
            if (_valve == null) return;

            // Loudest standing still, gone once she is working.
            float standing = 1f - Mathf.Clamp01(speed / 4f);
            float target = 0.30f * masterVolume * standing;

            _valve.volume = Mathf.MoveTowards(_valve.volume, target, Time.deltaTime * 0.6f);
        }

        void CueOneShots()
        {
            if (whistleOnDeparture && !_whistleDone && motion.phase == TrainPhase.Depart)
            {
                // Just before the regulator opens, the way a driver actually does it.
                if (motion.Clock >= motion.startDelay + motion.dwellTime - 1.6f)
                {
                    _oneShots.PlayOneShot(_whistle, 0.9f * masterVolume);
                    _whistleDone = true;
                }
            }

            if (brakesOnArrival && !_brakeDone && motion.phase == TrainPhase.Arrive)
            {
                // Brakes bite in the back half of the approach, not the moment she appears.
                if (motion.ArrivalProgress >= 0.45f)
                {
                    _oneShots.PlayOneShot(_brake, 0.8f * masterVolume);
                    _brakeDone = true;
                }
            }
        }

        // ------------------------------------------------------- cues driven from elsewhere

        /// <summary>Platform announcement, used to open the boarding scene.</summary>
        public void PlayChime(float volume = 0.8f)
        {
            if (_oneShots != null) _oneShots.PlayOneShot(_chime, volume * masterVolume);
        }

        /// <summary>A slam door closing. Called by the boarding sequence.</summary>
        public void PlayDoorSlam(float volume = 0.7f)
        {
            if (_oneShots != null) _oneShots.PlayOneShot(_slam, volume * masterVolume);
        }

        /// <summary>The guard's whistle, or any other cue that wants the loco to answer.</summary>
        public void PlayWhistle(float volume = 0.85f)
        {
            if (_oneShots != null) _oneShots.PlayOneShot(_whistle, volume * masterVolume);
        }
    }
}
