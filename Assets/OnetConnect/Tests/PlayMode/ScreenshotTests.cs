using System.Collections;
using System.IO;
using NUnit.Framework;
using Onet.Core;
using Onet.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    /// <summary>Opt-in visual capture: set ONET_SHOTS=/some/dir and run PlayMode tests with graphics enabled.</summary>
    public class ScreenshotTests
    {
        static RenderTexture _rt;

        /// <summary>
        /// Batch mode has no real Game view, so render everything through the main camera into a 1080x1920 target and
        /// switch the HUD to a camera canvas. Layout code then sees the same size a phone would.
        /// </summary>
        static void PrepareCamera()
        {
            var cam = Camera.main;
            _rt = new RenderTexture(1080, 1920, 24);
            cam.targetTexture = _rt;
            var hud = GameObject.Find("HUD").GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = cam;
            hud.planeDistance = 5f;
            Canvas.ForceUpdateCanvases();
        }

        static IEnumerator Shot(string dir, string name)
        {
            yield return null;
            var cam = Camera.main;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[shots] saved " + name);
        }

        /// <summary>Pause card, time-out card, and a landscape (desktop window) frame of a big level.</summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureCardsAndLandscape()
        {
            string dir = System.Environment.GetEnvironmentVariable("ONET_SHOTS");
            Assume.That(dir, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(dir);
            Onet.Runtime.Boot.AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            var game = Object.FindFirstObjectByType<GameController>();
            PrepareCamera();

            game.StartLevel(5);
            while (game.State != GameState.Playing) yield return null;
            game.Pause();
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "pause");

            game.Resume();
            game.Timer.AddTime(-game.Timer.TimeLeft + 0.05f);
            while (game.State != GameState.Lost) yield return null;
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "lose");

            var popups = GameObject.Find("LosePanel");
            if (popups != null) popups.SetActive(false);
            _rt = new RenderTexture(1920, 1080, 24);
            Camera.main.targetTexture = _rt;
            Canvas.ForceUpdateCanvases();
            game.StartLevel(31);
            while (game.State != GameState.Playing) yield return null;
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "landscape_lv31");
        }

        /// <summary>The Boot loading screen at a few points while its bar fills.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator CaptureBootScreen()
        {
            string dir = System.Environment.GetEnvironmentVariable("ONET_SHOTS");
            Assume.That(dir, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(dir);
            Onet.Runtime.Boot.AppServices.Shutdown();
            yield return SceneManager.LoadSceneAsync(Onet.Runtime.Boot.AppServices.BootScene);
            var cam = Camera.main;
            _rt = new RenderTexture(1080, 1920, 24);
            cam.targetTexture = _rt;
            var canvas = GameObject.Find("BootCanvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSecondsRealtime(0.45f);
                yield return Shot(dir, "boot" + i);
            }
            Onet.Runtime.Boot.AppServices.Shutdown();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureKeyScreens()
        {
            string dir = System.Environment.GetEnvironmentVariable("ONET_SHOTS");
            Assume.That(dir, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(dir);
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            var game = Object.FindFirstObjectByType<GameController>();
            var board = Object.FindFirstObjectByType<BoardView>();
            PrepareCamera();
            Debug.Log($"[shots] screen {Screen.width}x{Screen.height}");

            foreach (int lv in new[] { 1, 10, 11, 21, 31, 41, 51 })
            {
                game.StartLevel(lv);
                while (game.State != GameState.Playing) yield return null;
                if (lv >= 11) { game.TryFindMove(out Cell a, out Cell b); board.SimulateTap(a); }
                yield return new WaitForSeconds(0.7f);
                yield return Shot(dir, $"lv{lv:00}");
            }
            game.StartLevel(1);
            while (game.State != GameState.Playing) yield return null;
            for (int guard = 0; guard < 100 && game.State != GameState.Won; guard++)
            {
                if (game.State == GameState.Playing && game.TryFindMove(out Cell a, out Cell b)) { board.SimulateTap(a); board.SimulateTap(b); }
                yield return null;
                while (game.State == GameState.Resolving) yield return null;
            }
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot(dir, "win_confetti");
            yield return new WaitForSeconds(2.1f);
            yield return Shot(dir, "win");
        }
    }
}
