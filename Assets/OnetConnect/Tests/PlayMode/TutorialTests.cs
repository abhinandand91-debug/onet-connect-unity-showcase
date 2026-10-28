using System.Collections;
using NUnit.Framework;
using Onet.Core;
using Onet.Data;
using Onet.Runtime;
using Onet.Runtime.Boot;
using Onet.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    public class TutorialTests
    {
        GameController _game; BoardView _board; TutorialController _tut;

        IEnumerator Boot()
        {
            AppServices.Shutdown();   // tutorial progress lives in the save, so drop any app context an earlier test left
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            _game = Object.FindFirstObjectByType<GameController>();
            _board = Object.FindFirstObjectByType<BoardView>();
            _tut = Object.FindFirstObjectByType<TutorialController>();
            Assert.NotNull(_tut, "Tutorial object missing from scene");
        }

        IEnumerator Playing(float timeout = 10f)
        {
            float t = 0;
            while (_game.State != GameState.Playing && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(GameState.Playing, _game.State);
            for (int i = 0; i < 5; i++) yield return null;
        }

        IEnumerator Settle() { yield return null; while (_game.State == GameState.Resolving) yield return null; for (int i = 0; i < 4; i++) yield return null; }

        [UnityTest]
        public IEnumerator Level1_HandGuidesBothTapsOfTwoMatches_ThenStops()
        {
            yield return Boot();
            yield return Playing();
            Assert.IsTrue(_tut.HandVisible, "hand should show on first level");
            Assert.IsTrue(_tut.CaptionVisible);

            for (int pair = 0; pair < 2; pair++)
            {
                Assert.IsTrue(_game.TryGetGuidance(MoveStyle.Straight, out Cell first));
                Assert.AreEqual(_board.CellToWorld(first).x, _tut.HandTarget.x, 0.01f, $"pair {pair}: hand should point at first tile");
                _board.SimulateTap(first);
                yield return Settle();
                Assert.IsTrue(_game.TryGetGuidance(MoveStyle.Straight, out Cell partner), "selected tile has a partner");
                Assert.AreEqual(_board.CellToWorld(partner).x, _tut.HandTarget.x, 0.01f, $"pair {pair}: hand should move to the partner");
                Assert.AreEqual(_board.CellToWorld(partner).y, _tut.HandTarget.y, 0.01f);
                _board.SimulateTap(partner);
                yield return Settle();
            }
            Assert.IsFalse(_tut.HandVisible, "hand hides after the guided pairs");
            Assert.IsTrue(_game.Save.HasSeenTutorial("L1"));

            _game.StartLevel(1);
            yield return Playing();
            Assert.IsFalse(_tut.HandVisible, "tutorial plays only once");
        }

        [UnityTest]
        public IEnumerator Level2_ShowsCornerCaption_AndLevel3PointsAtHintButton()
        {
            yield return Boot();
            _game.StartLevel(2);
            yield return Playing();
            StringAssert.Contains("bend", _tut.Caption);
            Assert.IsTrue(_tut.HandVisible);

            _game.StartLevel(3);
            yield return Playing();
            StringAssert.Contains("Find", _tut.Caption);
            Assert.IsTrue(_tut.HandVisible);
            _game.Save.AddBooster(BoosterType.Hint, 3);
            Assert.IsTrue(_game.UseBooster(BoosterType.Hint));
            yield return Settle();
            Assert.IsFalse(_tut.HandVisible);
            Assert.IsTrue(_game.Save.HasSeenTutorial("L3"));
        }

        [UnityTest]
        public IEnumerator GravityTwist_IsExplainedOnce()
        {
            yield return Boot();
            _game.StartLevel(11);
            yield return Playing();
            Assert.IsTrue(_tut.CaptionVisible);
            StringAssert.Contains("twist", _tut.Caption);
            Assert.IsFalse(_tut.HandVisible);

            _game.StartLevel(12);   // same gravity, already explained
            yield return Playing();
            Assert.IsFalse(_tut.CaptionVisible);
        }
    }
}
