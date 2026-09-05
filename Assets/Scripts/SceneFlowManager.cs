using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrainStation
{
    /// <summary>
    /// Plays the four scenes back to back as one film.
    ///
    /// Lives in 00_Bootstrap and survives every load, so it is the only object alive for the
    /// whole running time. The fade to black is not decoration: loading a scene takes an
    /// unpredictable moment, and cutting on black turns that hitch into an edit instead of a
    /// stutter.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneFlowManager : MonoBehaviour
    {
        [System.Serializable]
        public class Shot
        {
            public string sceneName;
            [Tooltip("Seconds this scene is on screen, fades included.")]
            public float duration = 25f;
        }

        [Header("Running order")]
        public Shot[] shots =
        {
            new Shot { sceneName = "01_Arrival",   duration = 25f },
            new Shot { sceneName = "02_Waiting",   duration = 18f },
            new Shot { sceneName = "03_Boarding",  duration = 28f },
            new Shot { sceneName = "04_Departure", duration = 24f }
        };

        [Header("Edit")]
        public float fadeSeconds = 0.7f;
        [Tooltip("Hold on black at the very start before the first scene appears.")]
        public float openOnBlack = 0.5f;
        [Tooltip("Fade to black and stay there once the last scene ends.")]
        public bool fadeOutAtEnd = true;
        public bool loopForever = false;

        [Header("State (read-only)")]
        [SerializeField] int _index = -1;

        float _alpha = 1f;          // 1 = fully black
        Texture2D _black;
        bool _running;

        /// <summary>Index of the scene currently on screen, or -1 before the first.</summary>
        public int CurrentIndex { get { return _index; } }

        /// <summary>
        /// How black the screen currently is, 0 to 1. The fade is drawn in OnGUI, which an
        /// offscreen Camera.Render never sees, so the frame capture composites it in itself.
        /// </summary>
        public float FadeAlpha { get { return _alpha; } }

        /// <summary>True once the running order has played out.</summary>
        public bool Finished { get { return !_running && _index >= 0; } }

        void Awake()
        {
            // One flow manager only. A second Bootstrap load would otherwise fight the first.
            var existing = FindObjectsByType<SceneFlowManager>(FindObjectsSortMode.None);
            if (existing.Length > 1 && existing[0] != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            _black = new Texture2D(1, 1);
            _black.SetPixel(0, 0, Color.white);   // tinted black at draw time
            _black.Apply();
        }

        void Start()
        {
            if (!_running) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            _running = true;
            _alpha = 1f;

            if (openOnBlack > 0f) yield return new WaitForSeconds(openOnBlack);

            do
            {
                for (int i = 0; i < shots.Length; i++)
                {
                    var shot = shots[i];
                    if (shot == null || string.IsNullOrEmpty(shot.sceneName)) continue;

                    _index = i;

                    // Load while the screen is already black.
                    var load = SceneManager.LoadSceneAsync(shot.sceneName, LoadSceneMode.Single);
                    if (load == null)
                    {
                        Debug.LogError("SceneFlowManager: scene '" + shot.sceneName +
                                       "' could not be loaded. Is it in Build Settings?");
                        continue;
                    }

                    while (!load.isDone) yield return null;

                    // Give the new scene one frame to run its Awake/Start before revealing it,
                    // otherwise the first visible frame can show objects at their default pose.
                    yield return null;

                    yield return Fade(1f, 0f, fadeSeconds);

                    float hold = Mathf.Max(0f, shot.duration - fadeSeconds * 2f);
                    yield return new WaitForSeconds(hold);

                    bool last = (i == shots.Length - 1);
                    if (!last || fadeOutAtEnd || loopForever)
                    {
                        yield return Fade(0f, 1f, fadeSeconds);
                    }
                }
            }
            while (loopForever);

            _running = false;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (seconds <= 0f)
            {
                _alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                _alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }

            _alpha = to;
        }

        void OnGUI()
        {
            if (_alpha <= 0.001f || _black == null) return;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _alpha);
            GUI.depth = -1000;   // in front of everything
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
            GUI.color = prev;
        }

        void OnDestroy()
        {
            if (_black != null) Destroy(_black);
        }
    }
}
