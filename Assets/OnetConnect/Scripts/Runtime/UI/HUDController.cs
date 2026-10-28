using System;
using System.Collections;
using Onet.Data;
using Onet.Runtime.Boot;
using Onet.Runtime.Juice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onet.Runtime.UI
{
    /// <summary>
    /// Top bar (level, timer, star progress) + bottom booster bar,
    /// matching the reference layout. Listens to GameController events only.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] GameController game;

        [Header("Top bar")]
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text chapterLabel;
        [SerializeField] TMP_Text coinLabel;
        [SerializeField] TMP_Text pairsLabel;
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] Image starBarFill;               // legacy Image.Filled bar (optional)
        [SerializeField] RectTransform starFillRect;      // masked rounded bar: fill width is driven through anchorMax
        [SerializeField] RectTransform starBarRect;
        [SerializeField] Image star2Marker, star3Marker;   // placed at the 2★/3★ thresholds
        [SerializeField] Sprite starOn, starOff;
        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] TMP_Text comboLabel;
        [SerializeField] Button pauseButton;

        [Header("Boosters")]
        [SerializeField] BoosterButton[] boosterButtons;

        [Header("Booster unlock")]
        [SerializeField] Sprite padlockOpen;
        [SerializeField] SfxPlayer sfx;

        [Header("Toast")]
        [SerializeField] CanvasGroup toastGroup;
        [SerializeField] TMP_Text toastLabel;

        [Serializable]
        public class BoosterButton
        {
            public BoosterType type;
            public Button button;
            public TMP_Text countLabel;
            public GameObject lockIcon;
            public GameObject plusBadge; // shown when count == 0 (tap to buy)
            public TMP_Text lockLabel;   // "Lv 6" chip shown while locked
            [NonSerialized] public Coroutine unlocking;
        }

        Color _timerColor = Color.white;
        int _lastCoins = -1;
        int _lastCombo;
        Coroutine _comboPop;

        static IEnumerator Pop(RectTransform rt)
        {
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t / 0.25f * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        void OnEnable()
        {
            if (timerLabel != null) _timerColor = timerLabel.color;
            game.LevelStarted += OnLevelStarted;
            game.PairMatched += OnPairMatched;
            game.BoostersChanged += RefreshBoosters;
            game.Toast += ShowToast;
            game.Timer.Tick += OnTick;
            game.Score.ScoreChanged += OnScore;
            foreach (var b in boosterButtons)
            {
                var type = b.type;
                b.button.onClick.AddListener(() => OnBoosterTapped(type));
            }
            if (pauseButton != null) pauseButton.onClick.AddListener(game.Pause);
            game.PairMatched += OnPairsChanged;
        }

        void OnDisable()
        {
            game.LevelStarted -= OnLevelStarted;
            game.PairMatched -= OnPairMatched;
            game.BoostersChanged -= RefreshBoosters;
            game.Toast -= ShowToast;
            game.Timer.Tick -= OnTick;
            game.Score.ScoreChanged -= OnScore;
            foreach (var b in boosterButtons) b.button.onClick.RemoveAllListeners();
            if (pauseButton != null) pauseButton.onClick.RemoveListener(game.Pause);
            game.PairMatched -= OnPairsChanged;
            foreach (var b in boosterButtons) b.unlocking = null;   // coroutines stop with the component
        }

        void OnLevelStarted(GameController g)
        {
            levelLabel.text = $"Level {g.LevelNumber}";
            if (chapterLabel != null)
            {
                var chapter = AppServices.Exists ? AppServices.Instance.Database.ChapterFor(g.LevelNumber) : null;
                chapterLabel.text = chapter != null ? chapter.title.ToUpperInvariant() : "";
            }
            RefreshPairs();
            timerLabel.text = LevelTimer.Format(g.Level.timeLimit);
            SetFill(1f);
            PlaceMarker(star2Marker, g.Level.twoStarFraction);
            PlaceMarker(star3Marker, g.Level.threeStarFraction);
            OnScore(0, 0);
            RefreshBoosters();
        }

        void SetFill(float f)
        {
            if (starFillRect != null) starFillRect.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            else if (starBarFill != null) starBarFill.fillAmount = f;
        }

        void OnPairsChanged(int _) => RefreshPairs();

        void RefreshPairs()
        {
            if (pairsLabel == null) return;
            pairsLabel.text = $"{game.PairsMatched}<size=58%><color=#5f8f8a>/{game.PairsTotal}</color></size>";
        }

        void Update()
        {
            if (coinLabel != null && game.Save != null && game.Save.Data.coins != _lastCoins)
            {
                _lastCoins = game.Save.Data.coins;
                coinLabel.text = _lastCoins.ToString("N0");
            }
        }

        void PlaceMarker(Image marker, float fraction)
        {
            if (marker == null || starBarRect == null) return;
            var rt = marker.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(fraction, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        void OnTick(float left, float limit)
        {
            timerLabel.text = LevelTimer.Format(left);
            float fill = Onet.Core.ScoreSystem.StarBarFill(left, limit);
            SetFill(fill);
            if (star3Marker) star3Marker.sprite = fill >= game.Level.threeStarFraction ? starOn : starOff;
            if (star2Marker) star2Marker.sprite = fill >= game.Level.twoStarFraction ? starOn : starOff;
            bool urgent = left < 10f && left > 0f;
            timerLabel.color = left < 10f ? new Color(0.95f, 0.30f, 0.28f) : _timerColor;
            timerLabel.rectTransform.localScale = Vector3.one * (urgent ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f);
        }

        void OnScore(int score, int combo)
        {
            if (scoreLabel) scoreLabel.text = score.ToString("N0");
            if (comboLabel)
            {
                comboLabel.gameObject.SetActive(combo > 1);
                comboLabel.text = $"Combo x{combo}";
                if (combo > 1 && combo != _lastCombo) { if (_comboPop != null) StopCoroutine(_comboPop); _comboPop = StartCoroutine(Pop(comboLabel.rectTransform)); }
                _lastCombo = combo;
            }
        }

        void OnPairMatched(int gained) { /* hook for floating "+20" text / SFX / haptics */ }

        void OnBoosterTapped(BoosterType t)
        {
            if (game.Save.GetBooster(t) <= 0)
            {
                // Simple in-place purchase; swap for a shop popup in production
                if (!game.BuyBooster(t)) ShowToast("Not enough coins");
                return;
            }
            if (!game.UseBooster(t)) ShowToast("Can't use that right now");
        }

        void RefreshBoosters()
        {
            foreach (var b in boosterButtons)
            {
                int count = game.Save.GetBooster(b.type);
                bool locked = game.LevelNumber < game.Config.Get(b.type).unlockLevel;
                b.countLabel.text = count.ToString();
                var badge = b.countLabel.transform.parent;
                (badge != null ? badge.gameObject : b.countLabel.gameObject).SetActive(!locked && count > 0);
                if (b.plusBadge) b.plusBadge.SetActive(!locked && count == 0);
                int unlockLevel = game.Config.Get(b.type).unlockLevel;
                if (b.lockLabel) b.lockLabel.text = "Lv " + unlockLevel;
                b.button.interactable = !locked;

                // The level that unlocks a booster plays the unlock once; players already past it just see it open.
                string unlockKey = "unlock_" + b.type;
                bool firstTime = !locked && !game.Save.HasSeenTutorial(unlockKey);
                if (!locked) game.Save.MarkTutorialSeen(unlockKey);
                if (firstTime && game.LevelNumber == unlockLevel && b.lockIcon != null) b.unlocking = StartCoroutine(PlayUnlock(b));
                else if (b.unlocking == null && b.lockIcon) b.lockIcon.SetActive(locked);
            }
        }

        /// <summary>The padlock jiggles, springs open, then floats off with its "Lv N" chip and the booster pops.</summary>
        IEnumerator PlayUnlock(BoosterButton b)
        {
            var lockRt = (RectTransform)b.lockIcon.transform;
            var padlockTf = lockRt.Find("Padlock");
            var padlock = padlockTf != null ? padlockTf.GetComponent<Image>() : null;
            var group = b.lockIcon.GetComponent<CanvasGroup>();
            if (group == null) group = b.lockIcon.AddComponent<CanvasGroup>();
            Sprite closed = padlock != null ? padlock.sprite : null;
            Vector2 home = lockRt.anchoredPosition;
            b.lockIcon.SetActive(true);
            group.alpha = 1f;

            yield return new WaitForSecondsRealtime(0.6f);           // let the board intro draw the eye first

            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)  // jiggle, faster and smaller towards the end
            {
                float k = t / 0.4f;
                lockRt.localRotation = Quaternion.Euler(0, 0, 12f * Mathf.Sin(t * 45f) * (1f - k));
                yield return null;
            }
            lockRt.localRotation = Quaternion.identity;

            if (padlock != null && padlockOpen != null) padlock.sprite = padlockOpen;
            if (sfx != null) sfx.Play(Sfx.Click, 0.8f);
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                lockRt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / 0.18f * Mathf.PI * 0.5f));
                yield return null;
            }

            if (sfx != null) sfx.Play(Sfx.Hint, 1.1f);
            StartCoroutine(Pop((RectTransform)b.button.transform));
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                lockRt.anchoredPosition = home + Vector2.up * (60f * k);
                lockRt.localScale = Vector3.one * Mathf.Lerp(1.25f, 0.9f, k);
                group.alpha = 1f - k * k;
                yield return null;
            }

            b.lockIcon.SetActive(false);
            lockRt.anchoredPosition = home;
            lockRt.localScale = Vector3.one;
            group.alpha = 1f;
            if (padlock != null) padlock.sprite = closed;
            b.unlocking = null;
        }

        Coroutine _toast;
        void ShowToast(string msg)
        {
            if (toastGroup == null) return;
            if (_toast != null) StopCoroutine(_toast);
            _toast = StartCoroutine(ToastRoutine(msg));
        }

        IEnumerator ToastRoutine(string msg)
        {
            toastLabel.text = msg;
            for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.2f; yield return null; }
            toastGroup.alpha = 1;
            yield return new WaitForSecondsRealtime(1.4f);
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1 - t / 0.3f; yield return null; }
            toastGroup.alpha = 0;
        }
    }
}
