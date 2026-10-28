using System.Collections;
using Onet.Core;
using Onet.Data;
using TMPro;
using UnityEngine;

namespace Onet.Runtime.Tutorial
{
    /// <summary>
    /// First-run guidance: a pointing hand on Lv1 (straight match), Lv2 (a pair that needs corners) and Lv3 (the Hint
    /// booster), plus a one-card caption the first time each gravity twist appears. Progress is remembered per step in
    /// the save (<see cref="SaveService.HasSeenTutorial"/>), so each lesson plays once. Purely presentational: it only reads GameController state and events.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] BoardView board;
        [SerializeField] Camera cam;
        [SerializeField] SpriteRenderer hand;
        [SerializeField] CanvasGroup captionGroup;
        [SerializeField] TMP_Text captionLabel;
        [SerializeField] RectTransform hintButton;

        enum Mode { None, Pairs, HintBooster }

        public bool HandVisible => hand != null && hand.gameObject.activeSelf;
        public Vector3 HandTarget { get; private set; }
        public string Caption => captionLabel != null ? captionLabel.text : "";
        public bool CaptionVisible => captionGroup != null && captionGroup.alpha > 0.01f;

        Mode _mode;
        MoveStyle _style;
        string _stepKey;
        int _pairsToGuide, _matches;
        bool _dirty, _done;
        float _t;
        Coroutine _loop, _captionRoutine;

        public void ResetProgress() => game.Save.ResetTutorial();

        bool Seen(string key) => game.Save.HasSeenTutorial(key);
        void MarkSeen(string key) => game.Save.MarkTutorialSeen(key);

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            hand.gameObject.SetActive(false);
            captionGroup.alpha = 0f;
        }

        void OnEnable()
        {
            game.LevelStarted += OnLevelStarted;
            game.TileSelected += OnTileSelected;
            game.MatchResolved += OnMatch;
            game.BoosterUsed += OnBooster;
        }

        void OnDisable()
        {
            game.LevelStarted -= OnLevelStarted;
            game.TileSelected -= OnTileSelected;
            game.MatchResolved -= OnMatch;
            game.BoosterUsed -= OnBooster;
        }

        // ------------------------------------------------------------------ script selection

        void OnLevelStarted(GameController g)
        {
            if (_loop != null) StopCoroutine(_loop);
            if (_captionRoutine != null) StopCoroutine(_captionRoutine);
            hand.gameObject.SetActive(false);
            captionGroup.alpha = 0f;
            _mode = Mode.None; _matches = 0; _done = false; _dirty = true; _stepKey = null;

            int lv = g.LevelNumber;
            if (lv == 1 && !Seen("L1")) Begin("L1", Mode.Pairs, MoveStyle.Straight, 2, "Tap two matching tiles!");
            else if (lv == 2 && !Seen("L2")) Begin("L2", Mode.Pairs, MoveStyle.Turning, 1, "Lines can bend up to 2 times!");
            else if (lv == 3 && !Seen("L3")) Begin("L3", Mode.HintBooster, MoveStyle.Any, 0, "Stuck? Tap Find to spot a match!");
            else if (g.Level != null && g.Level.gravity != GravityMode.None && !Seen("g_" + g.Level.gravity))
            {
                MarkSeen("g_" + g.Level.gravity);
                _captionRoutine = StartCoroutine(Caption4(TwistText(g.Level.gravity), 4.5f));
            }
        }

        void Begin(string key, Mode mode, MoveStyle style, int pairs, string caption)
        {
            _stepKey = key; _mode = mode; _style = style; _pairsToGuide = pairs;
            _captionRoutine = StartCoroutine(Caption4(caption, 60f));
            _loop = StartCoroutine(GuideLoop());
        }

        static string TwistText(GravityMode m)
        {
            switch (m)
            {
                case GravityMode.Down: return "New twist: tiles fall down after each match!";
                case GravityMode.Up: return "New twist: tiles float up after each match!";
                case GravityMode.Left: return "New twist: tiles slide left after each match!";
                case GravityMode.Right: return "New twist: tiles slide right after each match!";
                case GravityMode.CollapseToCenterX:
                case GravityMode.CollapseToCenterY: return "New twist: tiles squeeze toward the center!";
                default: return "New twist: tiles split apart from the center!";
            }
        }

        // ------------------------------------------------------------------ events

        void OnTileSelected(Cell c) => _dirty = true;

        void OnMatch(Vector3 a, Vector3 b, int combo, int turns)
        {
            if (_mode != Mode.Pairs || _done) return;
            _matches++; _dirty = true;
            if (_matches >= _pairsToGuide) Finish("Nice! Now beat the clock!");
        }

        void OnBooster(BoosterType t)
        {
            if (_mode == Mode.HintBooster && t == BoosterType.Hint) Finish("The glowing pair can be matched!");
        }

        void Finish(string message)
        {
            if (_done) return;
            _done = true;
            MarkSeen(_stepKey);
            hand.gameObject.SetActive(false);
            if (_captionRoutine != null) StopCoroutine(_captionRoutine);
            _captionRoutine = StartCoroutine(Caption4(message, 2.2f));
        }

        // ------------------------------------------------------------------ hand

        IEnumerator GuideLoop()
        {
            float hintShownFor = 0f;
            while (!_done)
            {
                if (game.State != GameState.Playing) { hand.gameObject.SetActive(false); _dirty = true; yield return null; continue; }

                if (_mode == Mode.Pairs)
                {
                    if (_dirty && game.TryGetGuidance(_style, out Cell target))
                    {
                        HandTarget = board.CellToWorld(target);
                        hand.gameObject.SetActive(true);
                        _dirty = false;
                    }
                    else if (_dirty) { hand.gameObject.SetActive(false); _dirty = false; }
                }
                else if (_mode == Mode.HintBooster && hintButton != null)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, hintButton.position);
                    HandTarget = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                    HandTarget = new Vector3(HandTarget.x, HandTarget.y, 0f);
                    hand.gameObject.SetActive(true);
                    hintShownFor += Time.unscaledDeltaTime;
                    if (hintShownFor > 14f) Finish("You can use Find any time!");
                }
                yield return null;
            }
        }

        void Update()
        {
            if (!hand.gameObject.activeSelf) return;
            _t += Time.unscaledDeltaTime;
            float tap = Mathf.Max(0f, Mathf.Sin(_t * 5f));          // quick press once per cycle
            float size = Mathf.Min(board.CellSize * 1.25f, 1.9f) * (1f - 0.12f * tap);   // capped so a huge level-1 tile doesn't give a giant hand
            hand.transform.localScale = Vector3.one * size;
            hand.transform.position = HandTarget + new Vector3(0.04f, -0.06f - 0.05f * tap, 0f) * board.CellSize;
        }

        // ------------------------------------------------------------------ caption

        IEnumerator Caption4(string text, float hold)
        {
            captionLabel.text = text;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { captionGroup.alpha = t / 0.25f; yield return null; }
            captionGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { captionGroup.alpha = 1f - t / 0.4f; yield return null; }
            captionGroup.alpha = 0f;
        }
    }
}
