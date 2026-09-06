using System.IO;
using Unity.Collections;
using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Captures the film to a JPEG sequence plus a WAV, headlessly, for ffmpeg to mux into an MP4.
    ///
    /// This exists because Unity Recorder needs its editor window driven by hand, and the point of
    /// this project is that the whole film can be produced from a command line. Two things make it
    /// work:
    ///
    /// - Time.captureFramerate pins Time.deltaTime to exactly 1/fps and lets the game run as fast
    ///   as it can render. Without it the capture would be tied to wall-clock speed and the film
    ///   would play back at whatever framerate the machine happened to manage.
    /// - AudioRenderer taps the audio mixer output, which is the only way to get the synthesised
    ///   whistle and chuffs out of a headless run.
    ///
    /// It is written to hold almost nothing in memory. A first version accumulated the whole
    /// soundtrack in a List&lt;float&gt; and allocated two full-frame Color32 arrays per frame, and
    /// the machine ran out of RAM and killed the editor halfway through. Audio now streams
    /// straight to disk and the frame is darkened in place through the texture's raw bytes.
    ///
    /// Camera.Render is used rather than a screen grab, so the OnGUI fade never reaches the
    /// captured frame; it is composited back in from SceneFlowManager.FadeAlpha.
    /// </summary>
    public class FilmCapture : MonoBehaviour
    {
        public int width = 1280;
        public int height = 720;
        public int fps = 30;
        public float seconds = 96f;
        public int jpegQuality = 90;
        public string frameDir = "Capture/frames";
        public string wavPath = "Capture/audio.wav";

        /// <summary>Set once the capture has written its last frame and closed the WAV.</summary>
        public bool Done { get; private set; }
        public int FramesWritten { get; private set; }

        int _totalFrames;
        RenderTexture _rt;
        Texture2D _readback;
        SceneFlowManager _flow;

        FileStream _wavStream;
        BinaryWriter _wav;
        int _samplesWritten;
        bool _audioRunning;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            Directory.CreateDirectory(frameDir);
            string wavDir = Path.GetDirectoryName(wavPath);
            if (!string.IsNullOrEmpty(wavDir)) Directory.CreateDirectory(wavDir);

            _totalFrames = Mathf.CeilToInt(seconds * fps);

            // Fixed timestep for the whole run: this is what makes the output play at real speed
            // regardless of how long the machine actually takes to render each frame.
            Time.captureFramerate = fps;

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            _readback = new Texture2D(width, height, TextureFormat.RGB24, false);

            OpenWav();

            AudioRenderer.Start();
            _audioRunning = true;

            Debug.Log("[FilmCapture] Capturing " + _totalFrames + " frames at " + width + "x" + height +
                      ", " + fps + " fps -> " + frameDir);
        }

        void LateUpdate()
        {
            if (Done) return;

            CaptureAudioForThisFrame();
            CaptureFrame();

            FramesWritten++;

            if (FramesWritten % (fps * 10) == 0)
            {
                Debug.Log("[FilmCapture] " + FramesWritten + " / " + _totalFrames + " frames");
            }

            if (FramesWritten >= _totalFrames) Finish();
        }

        void CaptureAudioForThisFrame()
        {
            if (!_audioRunning) return;

            // One capture frame's worth of mixer output. This must be drained every frame or the
            // audio drifts out of step with the picture.
            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples <= 0) return;

            var buffer = new NativeArray<float>(samples * 2, Allocator.Temp);
            AudioRenderer.Render(buffer);

            for (int i = 0; i < buffer.Length; i++)
            {
                _wav.Write((short)(Mathf.Clamp(buffer[i], -1f, 1f) * short.MaxValue));
            }

            _samplesWritten += buffer.Length;
            buffer.Dispose();
        }

        void CaptureFrame()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                cam = cams.Length > 0 ? cams[0] : null;
            }

            if (_flow == null) _flow = FindFirstObjectByType<SceneFlowManager>();

            bool rendered = false;

            if (cam != null)
            {
                var prevTarget = cam.targetTexture;
                var prevActive = RenderTexture.active;

                cam.targetTexture = _rt;
                cam.Render();

                RenderTexture.active = _rt;
                _readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);

                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;

                rendered = true;
            }

            // The fade lives in OnGUI, which an offscreen render never sees, so apply it here.
            // Between scenes there may briefly be no camera at all, which is fine: that is exactly
            // when the film is sitting on black anyway.
            float alpha = _flow != null ? _flow.FadeAlpha : 0f;
            if (!rendered) alpha = 1f;

            if (alpha > 0.001f) Darken(alpha);

            _readback.Apply();

            string path = Path.Combine(frameDir, "f" + FramesWritten.ToString("D5") + ".jpg");
            File.WriteAllBytes(path, _readback.EncodeToJPG(jpegQuality));
        }

        /// <summary>
        /// Scales every colour byte in place. RGB24 has no alpha channel and no padding, so every
        /// byte in the buffer is a colour component and a uniform scale is exactly a fade to black
        /// — no per-pixel struct copies, and nothing allocated.
        /// </summary>
        void Darken(float alpha)
        {
            var raw = _readback.GetRawTextureData<byte>();
            float k = Mathf.Clamp01(1f - alpha);

            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = (byte)(raw[i] * k);
            }
        }

        void Finish()
        {
            if (Done) return;

            if (_audioRunning)
            {
                AudioRenderer.Stop();
                _audioRunning = false;
            }

            CloseWav();

            Debug.Log("[FilmCapture] Wrote " + FramesWritten + " frames and " +
                      (_samplesWritten / 2) + " audio frames to " + wavPath);

            Done = true;
        }

        void OnDestroy()
        {
            if (_audioRunning) AudioRenderer.Stop();
            CloseWav();

            Time.captureFramerate = 0;

            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_readback != null) Destroy(_readback);
        }

        // ---------------------------------------------------------------------------- wav

        void OpenWav()
        {
            _wavStream = new FileStream(wavPath, FileMode.Create, FileAccess.Write);
            _wav = new BinaryWriter(_wavStream);

            // Sizes are patched in CloseWav once the length is known.
            int rate = AudioSettings.outputSampleRate;
            const int channels = 2;

            _wav.Write(new char[] { 'R', 'I', 'F', 'F' });
            _wav.Write(0);
            _wav.Write(new char[] { 'W', 'A', 'V', 'E' });

            _wav.Write(new char[] { 'f', 'm', 't', ' ' });
            _wav.Write(16);
            _wav.Write((short)1);                       // PCM
            _wav.Write((short)channels);
            _wav.Write(rate);
            _wav.Write(rate * channels * 2);            // byte rate
            _wav.Write((short)(channels * 2));          // block align
            _wav.Write((short)16);                      // bits per sample

            _wav.Write(new char[] { 'd', 'a', 't', 'a' });
            _wav.Write(0);
        }

        void CloseWav()
        {
            if (_wav == null) return;

            _wav.Flush();

            int dataBytes = _samplesWritten * 2;

            _wavStream.Seek(4, SeekOrigin.Begin);
            _wav.Write(36 + dataBytes);

            _wavStream.Seek(40, SeekOrigin.Begin);
            _wav.Write(dataBytes);

            _wav.Flush();
            _wav.Close();

            _wav = null;
            _wavStream = null;
        }
    }
}
