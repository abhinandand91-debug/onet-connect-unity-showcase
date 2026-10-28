using System.Collections;
using NUnit.Framework;
using Onet.Runtime;
using Onet.Runtime.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    public class BootFlowTests
    {
        [TearDown] public void TearDown() => AppServices.Shutdown();

        [UnityTest, Timeout(90000)]
        public IEnumerator Boot_CreatesAppServices_ThenLoadsGameThroughLoader()
        {
            AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync(AppServices.BootScene);
            yield return null;
            Assert.IsTrue(AppServices.Exists, "Boot scene must create the AppServices");
            var app = AppServices.Instance;
            Assert.NotNull(app.Config); Assert.NotNull(app.Database); Assert.NotNull(app.Save); Assert.NotNull(app.Loader);

            float t = 0;
            while (SceneManager.GetActiveScene().name != AppServices.GameScene && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(AppServices.GameScene, SceneManager.GetActiveScene().name, "loader did not reach Game");

            yield return null;
            var game = Object.FindFirstObjectByType<GameController>();
            Assert.NotNull(game);
            Assert.AreSame(app.Save, game.Save, "Game must use the app-wide save, not its own");
            Assert.AreSame(app, AppServices.Instance, "AppServices must survive the scene switch");

            t = 0;
            while (game.State != GameState.Playing && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.IsFalse(app.Loader.IsBusy == true && t > 14f);
        }

        [UnityTest]
        public IEnumerator RequestedLevel_IsHonouredByGameScene()
        {
            AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync(AppServices.BootScene);
            yield return null;
            AppServices.Instance.RequestedLevel = 5;
            float t = 0;
            while (SceneManager.GetActiveScene().name != AppServices.GameScene && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null; yield return null;
            var game = Object.FindFirstObjectByType<GameController>();
            Assert.AreEqual(5, game.LevelNumber);
            Assert.AreEqual(0, AppServices.Instance.RequestedLevel, "request is consumed");
        }

        [UnityTest]
        public IEnumerator GameScene_PlayedDirectly_UsesSceneScopedFallback()
        {
            AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync(AppServices.GameScene);
            yield return null;
            Assert.IsTrue(AppServices.Exists);
            var game = Object.FindFirstObjectByType<GameController>();
            Assert.AreSame(AppServices.Instance.Save, game.Save);
            // fallback must not outlive its scene
            yield return SceneManager.LoadSceneAsync(AppServices.BootScene);
            yield return null;
            Assert.IsTrue(AppServices.Exists);   // Boot made its own, persistent one
        }

        [UnityTest]
        public IEnumerator TileSet_RotatesWithLevel_AfterTutorial()
        {
            AppServices.Shutdown();
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync(AppServices.GameScene);
            yield return null;
            var game = Object.FindFirstObjectByType<GameController>();
            while (game.State != GameState.Playing) yield return null;
            var theme = AppServices.Instance.Database.ThemeFor(game.Level);
            string Lead() { string n = theme.GetIcon(1).name; return n.Substring(n.IndexOf('_') + 1); }

            Assert.AreEqual(36, theme.IconCount, "30 original tiles plus raccoon, tiger, sheep, owl, deer, giraffe");
            Assert.AreEqual(12, theme.categories[3].indices.Length, "the More set holds the six new animals too");
            Assert.AreEqual(0, theme.CollectionForLevel(1));
            CollectionAssert.Contains(new[] { "panda", "fox", "frog", "chick", "piglet", "bunny", "penguin", "bear", "cat", "lion", "puppy", "koala" }, Lead(), "tutorial levels lead with animals");

            game.StartLevel(4);
            while (game.State != GameState.Playing) yield return null;
            Assert.AreEqual(1, theme.CollectionForLevel(4));
            CollectionAssert.Contains(new[] { "strawberry", "watermelon", "orange", "donut", "cupcake", "icecream" }, Lead(), "level 4 leads with treats");

            Assert.AreNotEqual(theme.CollectionForLevel(5), theme.CollectionForLevel(4), "the lead set changes every level");
            Assert.AreEqual(theme.CollectionForLevel(4), theme.CollectionForLevel(4 + theme.CollectionCount), "and cycles");
            theme.ApplyForLevel(1);
        }
    }
}
