using System.Collections;
using NUnit.Framework;
using Onet.Runtime;
using Onet.Runtime.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onet.Tests
{
    public class BoosterUnlockTests
    {
        GameController _game;

        IEnumerator Boot()
        {
            AppServices.Shutdown();   // unlock progress lives in the save, so start from a clean app context
            PlayerPrefs.DeleteAll();
            yield return SceneManager.LoadSceneAsync(AppServices.GameScene);
            yield return null;
            _game = Object.FindFirstObjectByType<GameController>();
        }

        static GameObject Lock(string booster) => GameObject.Find("Booster_" + booster).transform.Find("Lock").gameObject;

        [UnityTest]
        public IEnumerator UnlockLevel_PlaysOpenAnimationOnce_ThenHidesLock()
        {
            yield return Boot();
            _game.StartLevel(2);
            yield return null;
            Assert.IsTrue(Lock("Hint").activeSelf, "Find is locked before level 3");

            _game.StartLevel(3);
            yield return null;
            Assert.IsTrue(Lock("Hint").activeSelf, "lock stays up while it animates open");
            Assert.IsTrue(_game.Save.HasSeenTutorial("unlock_Hint"));

            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsFalse(Lock("Hint").activeSelf, "lock is gone once the animation ends");
            Assert.AreEqual(Vector3.one, Lock("Hint").transform.localScale, "lock transform is restored");

            _game.StartLevel(3);
            yield return null;
            Assert.IsFalse(Lock("Hint").activeSelf, "replaying the unlock level does not replay the animation");
        }

        [UnityTest]
        public IEnumerator PlayerAlreadyPastUnlockLevel_SeesBoosterOpenWithoutAnimation()
        {
            yield return Boot();
            _game.StartLevel(7);
            yield return null;
            Assert.IsFalse(Lock("Shuffle").activeSelf, "Shuffle (Lv 6) is simply open on level 7");
            Assert.IsTrue(_game.Save.HasSeenTutorial("unlock_Shuffle"));
            Assert.IsTrue(Lock("AddTime").activeSelf, "+10s (Lv 8) is still locked");
        }
    }
}
