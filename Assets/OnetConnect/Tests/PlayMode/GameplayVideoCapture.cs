using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onet.Core;
using Onet.Data;
using Onet.Runtime;
using Onet.Runtime.Boot;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    /// <summary>
    /// Opt-in gameplay recording for trailers / Shorts: set ONET_VIDEO=/some/dir and run PlayMode tests with graphics.
    /// Plays a scripted session at a fixed 30 fps (Time.captureFramerate), writing every frame as a 1080x1920 JPG plus
    /// the game's own audio (AudioRenderer) to audio.wav. Encode with ffmpeg afterwards.
    /// </summary>
    public class GameplayVideoCapture
    {
        const int Fps = 30;

        /// <summary>Renders the main camera into a portrait target each frame and stores the frame and its audio.</summary>
        class Recorder : MonoBehaviour
        {
            public string dir;
            public Camera cam;
            public RenderTexture target;
            int _frame;
            Texture2D _tex;
            readonly List<float> _audio = new List<float>();

            void Start()
            {
                _tex = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                AudioRenderer.Start();
            }

            void LateUpdate()
            {
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = target;
                _tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                _tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, $"f_{_frame++:00000}.jpg"), _tex.EncodeToJPG(92));

                int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
                int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                using (var buffer = new NativeArray<float>(samples * channels, Allocator.Temp))
                {
                    AudioRenderer.Render(buffer);
                    _audio.AddRange(buffer);
                }
            }

            public void Finish()
            {
                AudioRenderer.Stop();
                int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
                WriteWav(Path.Combine(dir, "audio.wav"), _audio, channels, AudioSettings.outputSampleRate);
                Debug.Log($"[video] {_frame} frames, {_audio.Count / channels} audio samples per channel");
                enabled = false;
            }

            static void WriteWav(string path, List<float> samples, int channels, int rate)
            {
                using (var w = new BinaryWriter(File.Create(path)))
                {
                    int bytes = samples.Count * 2;
                    w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
                    w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)channels);
                    w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                    w.Write("data".ToCharArray()); w.Write(bytes);
                    foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
                }
            }
        }

        GameController _game;
        BoardView _board;

        IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

        /// <summary>Jumps to <paramref name="level"/>, closing the win card the way its Next button would.</summary>
        void GoToLevel(int level)
        {
            var win = GameObject.Find("WinPanel");
            if (win != null) win.SetActive(false);
            _game.StartLevel(level);
        }

        IEnumerator UntilPlaying()
        {
            while (_game.State != GameState.Playing) yield return null;
        }

        /// <summary>Taps a legal pair with human-ish pacing, then waits for the match (and any gravity) to settle.</summary>
        IEnumerator PlayPair(float pause = 0.35f)
        {
            if (_game.State != GameState.Playing || !_game.TryFindMove(out Cell a, out Cell b)) yield break;
            _board.SimulateTap(a);
            yield return Wait(0.28f);
            _board.SimulateTap(b);
            yield return null;
            while (_game.State == GameState.Resolving) yield return null;
            yield return Wait(pause);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator RecordShort()
        {
            string dir = System.Environment.GetEnvironmentVariable("ONET_VIDEO");
            Assume.That(dir, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "f_*.jpg")) File.Delete(old);

            AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            Time.captureFramerate = Fps;
            yield return SceneManager.LoadSceneAsync(AppServices.GameScene);
            yield return null;
            _game = Object.FindFirstObjectByType<GameController>();
            _board = Object.FindFirstObjectByType<BoardView>();

            // Same portrait set-up as ScreenshotTests: camera into a 1080x1920 target, HUD as a camera canvas.
            var cam = Camera.main;
            var rt = new RenderTexture(1080, 1920, 24);
            cam.targetTexture = rt;
            var hud = GameObject.Find("HUD").GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = cam;
            hud.planeDistance = 5f;

            _game.StartLevel(1);
            var recorder = new GameObject("VideoRecorder").AddComponent<Recorder>();
            recorder.dir = dir; recorder.cam = cam; recorder.target = rt;

            // 1. Level 1 with the tutorial hand, played to a win and the star card.
            yield return UntilPlaying();
            yield return Wait(0.4f);
            for (int i = 0; i < 20 && _game.State == GameState.Playing; i++) yield return PlayPair(0.2f);
            yield return Wait(3.4f);

            // 2. Level 12: the Remove power-up unlocks, tiles fall after every match, and two power-ups get used.
            GoToLevel(12);
            yield return UntilPlaying();
            yield return Wait(1.9f);                         // unlock animation
            yield return PlayPair();
            yield return PlayPair();
            _game.UseBooster(BoosterType.Hint);
            yield return Wait(0.9f);
            yield return PlayPair();
            _game.UseBooster(BoosterType.AutoMatch);
            yield return null;
            while (_game.State == GameState.Resolving) yield return null;
            yield return Wait(0.5f);
            for (int i = 0; i < 4; i++) yield return PlayPair(0.15f);   // quick streak for a combo

            // 3. Level 31: the sunset chapter and a different gravity twist.
            GoToLevel(31);
            yield return UntilPlaying();
            yield return Wait(0.6f);
            for (int i = 0; i < 7; i++) yield return PlayPair(0.18f);
            yield return Wait(0.8f);

            recorder.Finish();
            Time.captureFramerate = 0;
            Object.Destroy(recorder.gameObject);
        }
    }
}
