using System.Collections;
using NUnit.Framework;
using Onet.Core;
using Onet.Data;
using Onet.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    /// <summary>Loads the real Game scene and plays it through the public controller / view API.</summary>
    public class GameSmokeTests
    {
        static IEnumerator Boot(System.Action<GameController, BoardView> done)
        {
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null; // let Start() run its own StartLevel before we take over
            var game = Object.FindFirstObjectByType<GameController>();
            var board = Object.FindFirstObjectByType<BoardView>();
            Assert.NotNull(game); Assert.NotNull(board);
            done(game, board);
        }

        static IEnumerator WaitFor(GameController g, GameState s, float timeout = 10f)
        {
            float t = 0;
            while (g.State != s && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(s, g.State, $"Timed out waiting for {s}");
        }

        [UnityTest]
        public IEnumerator Level1_PlaysToWin_ByTapping()
        {
            GameController game = null; BoardView board = null;
            yield return Boot((g, b) => { game = g; board = b; });
            yield return WaitFor(game, GameState.Playing);

            int matches = 0;
            game.PairMatched += _ => matches++;
            int wins = 0, stars = 0;
            game.LevelWon += (s, _, __) => { wins++; stars = s; };

            for (int guard = 0; guard < 100 && game.State != GameState.Won; guard++)
            {
                if (game.State == GameState.Playing && game.TryFindMove(out Cell a, out Cell b))
                {
                    board.SimulateTap(a);
                    board.SimulateTap(b);
                }
                yield return null;
                while (game.State == GameState.Resolving) yield return null;
            }
            Assert.AreEqual(GameState.Won, game.State);
            Assert.AreEqual(1, wins);
            Assert.AreEqual(8, matches, "Lv1 is 4x4 = 8 pairs");
            Assert.AreEqual(0, game.PairsLeft);
            Assert.GreaterOrEqual(stars, 1);
            Assert.GreaterOrEqual(game.Save.GetStars(1), 1);
        }

        [UnityTest]
        public IEnumerator WrongPair_ShakesBothTiles_AndClearsSelection()
        {
            GameController game = null; BoardView board = null;
            yield return Boot((g, b) => { game = g; board = b; });
            yield return WaitFor(game, GameState.Playing);

            TileView first = null, second = null;
            foreach (var t in Object.FindObjectsByType<TileView>(FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (first == null) first = t;
                else if (t.TileType != first.TileType) { second = t; break; }
            }
            Assert.NotNull(second, "level 1 has more than one tile type");

            int rejected = 0;
            game.TileRejected += _ => rejected++;
            board.SimulateTap(first.Cell);
            board.SimulateTap(second.Cell);
            Assert.AreEqual(1, rejected, "a wrong pair is rejected once");
            Assert.IsNull(game.SelectedCell, "selection clears after a wrong pair");
            Assert.AreEqual(8, game.PairsLeft, "nothing was removed");

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(Quaternion.identity, first.transform.localRotation, "tile settles after the wobble");
            Assert.AreEqual(Quaternion.identity, second.transform.localRotation);
        }

        [UnityTest]
        public IEnumerator Level1_TimeOut_ShowsLose_ThenContinue()
        {
            GameController game = null;
            yield return Boot((g, b) => game = g);
            yield return WaitFor(game, GameState.Playing);
            game.Timer.AddTime(-game.Timer.TimeLeft + 0.05f);
            yield return WaitFor(game, GameState.Lost, 5f);
            Assert.IsTrue(game.CanContinue);
            Assert.IsTrue(game.TryContinue(true));
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.IsFalse(game.TryContinue(true), "only one continue per attempt");

            // Second time-out in the same attempt: no continue (so the lose card offers neither coins nor an ad).
            game.Timer.AddTime(-game.Timer.TimeLeft + 0.05f);
            yield return WaitFor(game, GameState.Lost, 5f);
            Assert.IsFalse(game.CanContinue);
        }

        [UnityTest]
        public IEnumerator EveryBooster_Works_AndShuffleKeepsTileCount()
        {
            GameController game = null;
            yield return Boot((g, b) => game = g);
            game.StartLevel(12);
            yield return WaitFor(game, GameState.Playing);
            foreach (BoosterType t in System.Enum.GetValues(typeof(BoosterType))) game.Save.AddBooster(t, 5);

            int before = game.PairsLeft;
            Assert.IsTrue(game.UseBooster(BoosterType.Hint));
            Assert.IsTrue(game.UseBooster(BoosterType.AddTime));
            Assert.IsTrue(game.UseBooster(BoosterType.Shuffle));
            yield return WaitFor(game, GameState.Playing, 5f);
            Assert.AreEqual(before, game.PairsLeft);
            Assert.IsTrue(game.UseBooster(BoosterType.AutoMatch));
            yield return WaitFor(game, GameState.Playing, 5f);
            Assert.AreEqual(before - 1, game.PairsLeft);
        }

        [UnityTest]
        public IEnumerator SampledLevels1To60_AllStart()
        {
            GameController game = null;
            yield return Boot((g, b) => game = g);
            for (int lv = 1; lv <= 60; lv += 7)
            {
                game.StartLevel(lv);
                yield return WaitFor(game, GameState.Playing);
                Assert.Greater(game.PairsLeft, 0, $"level {lv} empty");
            }
        }

        [UnityTest]
        public IEnumerator GravityLevels_PlayCleanly_ForTwentyMatches()
        {
            GameController game = null; BoardView board = null;
            yield return Boot((g, b) => { game = g; board = b; });
            foreach (int lv in new[] { 11, 16, 21, 31, 36, 41, 46 })   // each gravity mode's intro level
            {
                game.StartLevel(lv);
                yield return WaitFor(game, GameState.Playing);
                for (int i = 0; i < 20 && game.State != GameState.Won; i++)
                {
                    int before = game.PairsLeft;
                    Assert.IsTrue(game.TryFindMove(out Cell a, out Cell b), $"level {lv}: no move with {before} pairs");
                    board.SimulateTap(a); board.SimulateTap(b);
                    yield return null;
                    while (game.State == GameState.Resolving) yield return null;
                    Assert.AreEqual(before - 1, game.PairsLeft, $"level {lv} match {i}");
                }
            }
        }
    }
}
