using System.Collections;
using Onet.Runtime.Juice;
using Onet.Runtime.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Onet.Runtime.UI
{
    /// <summary>Win / lose / pause popups.</summary>
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] SfxPlayer sfx;

        [Header("Win")]
        [SerializeField] GameObject winPanel;
        [SerializeField] Image[] stars;          // 3 images
        [SerializeField] Sprite starOn, starOff;
        [SerializeField] TMP_Text winScore, winCoins;
        [SerializeField] Button nextButton, winRetryButton;
        [SerializeField] ConfettiBurst confetti;

        [Header("Lose")]
        [SerializeField] GameObject losePanel;
        [SerializeField] Button continueCoinsButton, continueAdButton, loseRetryButton;
        [SerializeField] TMP_Text continuePriceLabel;

        const string ContinueRewardId = "continue-after-timeout";
        const int MaxRewardedFailures = 2;      // ads are served to a subset of players; stop offering after repeated misses
        const float AdBreakTimeout = 10f;       // seconds of running game to wait for an interstitial that never answers

        TMP_Text _loseSubtitle;
        string _continuePrompt;

        static int _rewardedFailures;
        static float _lastAdBreak;              // realtime of the last ad break; 0 = session start (YouTube already ran a pre-roll)
        bool _waitingForAd;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { _rewardedFailures = 0; _lastAdBreak = 0f; }

        [Header("Pause")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton, pauseRetryButton;

        void Awake()
        {
            // Leaving a result card is a natural break between levels: the only place ad breaks are offered.
            nextButton.onClick.AddListener(() => AfterAdBreak(game.NextLevel));
            winRetryButton.onClick.AddListener(() => AfterAdBreak(game.Retry));
            loseRetryButton.onClick.AddListener(() => AfterAdBreak(game.Retry));
            continueCoinsButton.onClick.AddListener(() => { if (game.TryContinue(false)) HideAll(); });
            continueAdButton.onClick.AddListener(OnContinueAd);
            resumeButton.onClick.AddListener(() => { HideAll(); game.Resume(); });
            pauseRetryButton.onClick.AddListener(() => { HideAll(); game.Retry(); });
            var subtitle = losePanel.transform.GetChild(0).Find("Sub");
            _loseSubtitle = subtitle != null ? subtitle.GetComponent<TMP_Text>() : null;
            if (_loseSubtitle != null) _continuePrompt = _loseSubtitle.text;
            HideAll();
        }

        void OnEnable()
        {
            game.LevelWon += ShowWin;
            game.LevelLost += ShowLose;
            Playables.EscapePressed += OnEscape;
        }

        void OnDisable()
        {
            game.LevelWon -= ShowWin;
            game.LevelLost -= ShowLose;
            Playables.EscapePressed -= OnEscape;
        }

        void Update()
        {
            // Pause panel follows game state (pause can come from HUD button or app focus loss)
            bool paused = game.State == GameState.Paused;
            if (pausePanel.activeSelf != paused) pausePanel.SetActive(paused);
#if ENABLE_INPUT_SYSTEM
            // Editor, desktop and Android back. In the web build Esc arrives through Playables.EscapePressed instead.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) OnEscape();
#endif
        }

        /// <summary>Esc closes whichever card is up the way its main button would, or pauses a running level.</summary>
        void OnEscape()
        {
            if (_waitingForAd) return;
            if (pausePanel.activeSelf) { HideAll(); game.Resume(); return; }
            if (winPanel.activeSelf) { if (nextButton.gameObject.activeSelf) nextButton.onClick.Invoke(); return; }
            if (losePanel.activeSelf) { loseRetryButton.onClick.Invoke(); return; }
            if (game.State == GameState.Playing) game.Pause();
        }

        Coroutine _anim;

        void ShowWin(int starCount, int score, int coins)
        {
            HideAll();
            winPanel.SetActive(true);
            for (int i = 0; i < stars.Length; i++) { stars[i].sprite = starOff; stars[i].transform.localScale = Vector3.one; }
            winScore.text = "0"; winCoins.text = "+0";
            if (confetti != null) confetti.Play();
            Restart(WinRoutine(starCount, score, coins));
        }

        void ShowLose()
        {
            HideAll();
            losePanel.SetActive(true);
            continuePriceLabel.text = game.Config.continueCoinPrice.ToString();
            // One continue per attempt: once it is used, never offer an ad that could not pay out.
            bool canContinue = game.CanContinue;
            continueCoinsButton.gameObject.SetActive(canContinue);
            continueAdButton.gameObject.SetActive(canContinue && game.Config.rewardedAdContinue && Playables.InPlayables
                                                  && _rewardedFailures < MaxRewardedFailures);
            if (_loseSubtitle != null) _loseSubtitle.text = canContinue ? _continuePrompt : "Out of time! Give it another go?";
            continueAdButton.interactable = true;
            Restart(PopIn(losePanel.transform.GetChild(0)));
        }

        void Restart(IEnumerator routine)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(routine);
        }

        IEnumerator PopIn(Transform card)
        {
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                card.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0.6f, 1.08f, k / 0.7f) : Mathf.Lerp(1.08f, 1f, (k - 0.7f) / 0.3f));
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        IEnumerator WinRoutine(int starCount, int score, int coins)
        {
            var card = winPanel.transform.GetChild(0);
            var buttons = new[] { nextButton, winRetryButton };
            foreach (var b in buttons) b.gameObject.SetActive(false);
            yield return PopIn(card);

            for (int i = 0; i < stars.Length; i++)
            {
                yield return new WaitForSecondsRealtime(0.22f);
                bool lit = i < starCount;
                if (lit) { stars[i].sprite = starOn; if (sfx != null) sfx.Play(Sfx.Match, 1f + 0.18f * i); }
                else if (sfx != null) sfx.Play(Sfx.Select, 0.7f);
                for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.3f;
                    stars[i].transform.localScale = Vector3.one * (lit ? 1f + 0.45f * Mathf.Sin(k * Mathf.PI) : 1f - 0.12f * Mathf.Sin(k * Mathf.PI));
                    stars[i].transform.localRotation = Quaternion.Euler(0, 0, lit ? 12f * Mathf.Sin(k * Mathf.PI * 2f) : 0f);
                    yield return null;
                }
                stars[i].transform.localScale = Vector3.one; stars[i].transform.localRotation = Quaternion.identity;
            }

            for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.8f, 3f);
                winScore.text = Mathf.RoundToInt(score * k).ToString("N0");
                winCoins.text = "+" + Mathf.RoundToInt(coins * k);
                yield return null;
            }
            winScore.text = score.ToString("N0"); winCoins.text = "+" + coins;

            foreach (var b in buttons)
            {
                b.gameObject.SetActive(true);
                var rt = (RectTransform)b.transform;
                StartCoroutine(PopIn(rt));
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        // Strictly opt-in: only this tap requests an ad, and Retry / coin Continue stay available whatever happens.
        void OnContinueAd()
        {
            continueAdButton.interactable = false;
            Playables.RequestRewardedAd(ContinueRewardId, (shown, granted) =>
            {
                if (this == null || !losePanel.activeSelf) return;
                if (!shown)
                {
                    // No ad for this player right now: drop the offer here; coin Continue and Retry still work.
                    _rewardedFailures++;
                    continueAdButton.gameObject.SetActive(false);
                    return;
                }
                _rewardedFailures = 0;
                if (granted && game.TryContinue(true)) { HideAll(); return; }
                continueAdButton.interactable = true;   // closed early: no reward, but they may try again
            });
        }

        bool AdBreakDue()
        {
            var cfg = game.Config;
            return Playables.InPlayables && cfg.interstitialAds && game.LevelNumber >= cfg.interstitialFromLevel
                   && Time.realtimeSinceStartup - _lastAdBreak >= cfg.interstitialMinGap;
        }

        /// <summary>Closes the card, lets YouTube show an interstitial if one is due, then runs <paramref name="next"/>.</summary>
        void AfterAdBreak(System.Action next)
        {
            HideAll();
            if (_waitingForAd) return;
            if (!AdBreakDue()) { next(); return; }
            _lastAdBreak = Time.realtimeSinceStartup;
            _waitingForAd = true;
            // YouTube pauses and resumes the game around the ad itself (onPause / onResume); we only wait for it to end.
            Playables.RequestInterstitialAd(_ => FinishAdBreak(next));
            StartCoroutine(AdBreakFallback(next));
        }

        // If the SDK never answers, carry on rather than leave the player on an empty screen. Each frame counts at most
        // 0.1 s, so time spent with the main loop halted for the ad itself does not use up the allowance.
        IEnumerator AdBreakFallback(System.Action next)
        {
            for (float t = 0f; t < AdBreakTimeout && _waitingForAd; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f)) yield return null;
            FinishAdBreak(next);
        }

        void FinishAdBreak(System.Action next)
        {
            if (!_waitingForAd || this == null) return;
            _waitingForAd = false;
            next();
        }

        void HideAll()
        {
            winPanel.SetActive(false);
            losePanel.SetActive(false);
        }
    }
}
