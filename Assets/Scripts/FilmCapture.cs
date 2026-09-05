using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// Captures the film to a JPEG sequence plus a WAV, headlessly, for ffmpeg to mux into an MP4.
    ///
    /// This exists because Unity Recorder needs its editor window driven by hand, and the point of
    /// this project is that the whole film can be produced from a command line. The two pieces
    /// that make it work:
    ///
    /// - Time.captureFramerate pins Time.deltaTime to exactly 1/fps and lets the game run as fast
    ///   as it can render. Without it the capture would be tied to wall-clock speed and the film
    ///   would play back at whatever framerate the machine happened to manage.
    /// - AudioRenderer taps the audio mixer's output, which is the only way to get the synthesised
    ///   whistle and chuffs out of a headless run.
    ///
    /// Camera.Render is used rather than a screen grab, so the OnGUI fade never appears in the
    /// captured frame; the fade is composited back in from SceneFlowManager.FadeAlpha.
    /// </summary>
    public class FilmCapture : MonoBehaviour
    {
        public int width = 1920;
        public int height = 1080;
        public int fps = 30;
        public float seconds = 96f;
        public int jpegQuality = 92;
        public string frameDir = "Capture/frames";
        public string wavPath = "Capture/audio.wav";

        /// <summary>Set once the capture has written its last frame and closed the WAV.</summary>
        public bool Done { get; private set; }
        public int FramesWritten { get; private set; }

        int _totalFrames;
        RenderTexture _rt;
        Texture2D _readback;
        SceneFlowManager _flow;

        readonly List<float> _audio = new List<float>();
        bool _audioRunning;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            Directory.CreateDirectory(frameDir);
            Directory.CreateDirectory(Path.GetDirectoryName(wavPath));

            _totalFrames = Mathf.CeilToInt(seconds * fps);

            // Fixed timestep for the whole run: this is what makes the output play at real speed
            // regardless of how long the machine actually takes to render each frame.
            Time.captureFramerate = fps;

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            _readback = new Texture2D(width, height, TextureFormat.RGB24, false);

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

            if (FramesWritten >= _totalFrames) Finish();
        }

        void CaptureAudioForThisFrame()
        {
            if (!_audioRunning) return;

            // One capture frame's worth of mixer output. This has to be drained every frame or
            // the audio drifts out of step with the picture.
            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples <= 0) return;

            var buffer = new NativeArray<float>(samples * 2, Allocator.Temp);
            AudioRenderer.Render(buffer);

            for (int i = 0; i < buffer.Length; i++) _audio.Add(buffer[i]);

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
            }
            else
            {
                // Between scenes there may be no camera at all; a black frame is correct here,
                // because that is exactly when the film is fading through black anyway.
                Fill(Color.black);
            }

            // The fade lives in OnGUI, which an offscreen render never sees, so apply it here.
            float alpha = _flow != null ? _flow.FadeAlpha : 0f;
            if (alpha > 0.001f) Darken(alpha);

            _readback.Apply();

            string path = Path.Combine(frameDir, "f" + FramesWritten.ToString("D5") + ".jpg");
            File.WriteAllBytes(path, _readback.EncodeToJPG(jpegQuality));
        }

        void Fill(Color c)
        {
            var pixels = _readback.GetPixels32();
            var c32 = (Color32)c;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c32;
            _readback.SetPixels32(pixels);
        }

        void Darken(float alpha)
        {
            float k = Mathf.Clamp01(1f - alpha);
            var pixels = _readback.GetPixels32();

            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                p.r = (byte)(p.r * k);
                p.g = (byte)(p.g * k);
                p.b = (byte)(p.b * k);
                pixels[i] = p;
            }

            _readback.SetPixels32(pixels);
        }

        void Finish()
        {
            if (Done) return;

            if (_audioRunning)
            {
                AudioRenderer.Stop();
                _audioRunning = false;
            }

            WriteWav(wavPath, _audio, AudioSettings.outputSampleRate, 2);

            Debug.Log("[FilmCapture] Wrote " + FramesWritten + " frames and " +
                      (_audio.Count / 2) + " audio samples to " + wavPath);

            Done = true;
        }

        void OnDestroy()
        {
            if (_audioRunning) AudioRenderer.Stop();

            Time.captureFramerate = 0;

            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_readback != null) Destroy(_readback);
        }

        /// <summary>Plain 16-bit PCM WAV. Nothing clever, but ffmpeg reads it without complaint.</summary>
        static void WriteWav(string path, List<float> samples, int sampleRate, int channels)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Count * 2;

                w.Write(new char[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new char[] { 'W', 'A', 'V', 'E' });

                w.Write(new char[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);                                  // PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * channels * 2);                 // byte rate
                w.Write((short)(channels * 2));                     // block align
                w.Write((short)16);                                 // bits per sample

                w.Write(new char[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);

                for (int i = 0; i < samples.Count; i++)
                {
                    w.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                }
            }
        }
    }
}
