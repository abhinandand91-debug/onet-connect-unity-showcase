using System;
using System.Collections;
using System.Collections.Generic;
using Onet.Core;
using Onet.Runtime.Boot;
using Onet.Runtime.Platform;
using Onet.Data;
using UnityEngine;

namespace Onet.Runtime
{
    public enum GameState { Loading, Intro, Playing, Resolving, Paused, Won, Lost }

    /// <summary>Which kind of pair the tutorial wants to show first.</summary>
    public enum MoveStyle { Any, Straight, Turning }

    /// <summary>
    /// Orchestrates one level: owns the BoardModel and rules, drives the views.
    ///
    ///   Input → OnCellClicked → selection rules → PathFinder
    ///        → match: PathView + BoardView.AnimateMatch → model.RemovePair
    ///                 → Gravity.Apply → BoardView.AnimateGravity
    ///                 → win check → deadlock check (auto-shuffle)
    ///
    /// All rule code lives in Onet.Core (unit tested); this class only sequences it.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] LevelDatabase database;
        [SerializeField] GameConfig config;

        [Header("Scene refs")]
        [SerializeField] BoardView boardView;
        [SerializeField] PathView pathView;
        [SerializeField] LevelTimer timer;

        [Header("Debug")]
        [Tooltip("> 0 forces this level (also set by the Level Editor 'Play' button)")]
        [SerializeField] int debugLevelOverride = 0;

        public const string EditorPlayLevelKey = "onet_editor_play_level";

        // ---- public state / events for UI ----
        public GameState State { get; private set; } = GameState.Loading;
        public LevelData Level { get; private set; }
        public int LevelNumber { get; private set; }
        public ScoreSystem Score { get; } = new ScoreSystem();
        public SaveService Save { get; private set; }
        public LevelTimer Timer => timer;
        public GameConfig Config => config;
        public int PairsLeft => _model != null ? _model.TileCount / 2 : 0;

        public event Action<GameController> LevelStarted;
        public event Action<int> PairMatched;                   // score gained
        public event Action<int, int, int> LevelWon;            // stars, score, coins
        public event Action LevelLost;
        public event Action<string> Toast;                      // "No moves – shuffling!"
        public event Action BoostersChanged;
        public event Action<Cell> TileSelected;
        public event Action<Cell> TileRejected;                 // same icon, no legal path
        public event Action<Vector3, Vector3, int, int> MatchResolved; // worldA, worldB, combo, turns
        public event Action Shuffled;
        public event Action<Cell, Cell> HintShown;
        public event Action<BoosterType> BoosterUsed;

        BoardModel _model;
        readonly MoveFinder _moves = new MoveFinder();
        readonly List<Cell> _pathCells = new List<Cell>(4);
        readonly List<Vector3> _pathWorld = new List<Vector3>(4);
        readonly List<TileMove> _gravityMoves = new List<TileMove>();
        System.Random _rng;
        Cell? _selected;
        Move? _activeHint;
        bool _usedContinue;
        int _retryCount;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            // Services come from the app context (Boot scene); a directly-played scene gets a scene-scoped fallback.
            if (!AppServices.Exists) AppServices.CreateFallback(config, database);
            var app = AppServices.Instance;
            if (app.Config != null) config = app.Config;
            if (app.Database != null) database = app.Database;
            Save = app.Save;
            Score.BasePairScore = config.basePairScore;
            Score.ComboWindow = config.comboWindow;
            Score.MaxCombo = config.maxCombo;
            boardView.CellClicked += OnCellClicked;
            timer.Expired += OnTimeUp;
        }

        void OnEnable() => Playables.Paused += Pause;
        void OnDisable() => Playables.Paused -= Pause;

        void Start()
        {
            var app = AppServices.Instance;
            int level = app.RequestedLevel > 0 ? app.RequestedLevel : Save.Data.currentLevel;
            app.RequestedLevel = 0;
#if UNITY_EDITOR
            int editorLevel = PlayerPrefs.GetInt(EditorPlayLevelKey, 0);
            if (editorLevel > 0) { level = editorLevel; PlayerPrefs.DeleteKey(EditorPlayLevelKey); }
#endif
            if (debugLevelOverride > 0) level = debugLevelOverride;
            StartLevel(level);
        }

        public void StartLevel(int levelNumber, bool isRetry = false)
        {
            StopAllCoroutines();
            State = GameState.Loading;
            LevelNumber = levelNumber;
            Level = database.Get(levelNumber, out bool endless);
            _retryCount = isRetry ? _retryCount + 1 : 0;
            bool fresh = endless || (isRetry && Level.freshDealOnRetry);

            _model = Level.CreateBoard(fresh, out string error);
            if (_model == null) { Debug.LogError($"Level {levelNumber}: {error}"); return; }

            _moves.MaxTurns = Level.maxTurns;
            _rng = new System.Random(Level.seed ^ (_retryCount * 7919) ^ Environment.TickCount);
            _selected = null;
            _activeHint = null;
            _usedContinue = false;
            Score.Reset();
            PairsTotal = _model.TileCount / 2;
            Score.TwoStarTimeFraction = Level.twoStarFraction;
            Score.ThreeStarTimeFraction = Level.threeStarFraction;

            var levelTheme = database.ThemeFor(Level);
            if (levelTheme != null) levelTheme.ApplyForLevel(levelNumber);
            boardView.Build(_model, levelTheme);
            pathView.Hide();
            StartCoroutine(IntroThenPlay());
        }

        IEnumerator IntroThenPlay()
        {
            State = GameState.Intro;
            LevelStarted?.Invoke(this);
            yield return boardView.PlayIntro();
            timer.Begin(Level.timeLimit);
            State = GameState.Playing;
            Playables.GameReady();
            EnsureMoveAvailable();
        }

        // ------------------------------------------------------------------ input & rules

        void OnCellClicked(Cell c)
        {
            if (State != GameState.Playing) return;
            ClearHint();

            if (!_selected.HasValue) { Select(c); return; }

            Cell first = _selected.Value;
            if (first == c) { Deselect(); return; }

            if (_model.Get(first) == _model.Get(c) && _moves.PathFinder.FindPath(_model, first, c, _pathCells, Level.maxTurns))
            {
                Deselect();
                StartCoroutine(ResolveMatch(first, c, _pathCells.Count - 2));
                return;
            }

            // wrong pair (different icons, or the same icon with no legal path): both tiles shake their heads and the
            // selection clears, so the next tap starts a fresh pair
            Deselect();
            boardView.Nope(first);
            boardView.Nope(c);
            TileRejected?.Invoke(c);
        }

        void Select(Cell c) { _selected = c; boardView.SetSelected(c, true); TileSelected?.Invoke(c); }

        void Deselect()
        {
            if (_selected.HasValue) boardView.SetSelected(_selected.Value, false);
            _selected = null;
        }

        IEnumerator ResolveMatch(Cell a, Cell b, int turns)
        {
            State = GameState.Resolving;

            _pathWorld.Clear();
            foreach (var pc in _pathCells) _pathWorld.Add(boardView.CellToWorld(pc));
            var theme = database.ThemeFor(Level);
            pathView.Show(_pathWorld, boardView.CellSize, theme != null ? theme.lineColor : Color.yellow,
                          theme != null ? theme.pathStarSprite : null, config.pathDisplayTime);

            _model.RemovePair(a, b);
            int gained = Score.RegisterMatch(Time.time, turns);
            PairMatched?.Invoke(gained);
            MatchResolved?.Invoke(boardView.CellToWorld(a), boardView.CellToWorld(b), Score.Combo, turns);

            yield return boardView.AnimateMatch(a, b, config.matchPopTime);

            Gravity.Apply(_model, Level.gravity, _gravityMoves);
            if (_gravityMoves.Count > 0) yield return boardView.AnimateGravity(_gravityMoves, config.gravitySlideTime);

            if (_model.IsCleared) { Win(); yield break; }
            if (State != GameState.Resolving) yield break; // timed out / paused meanwhile

            State = GameState.Playing;
            EnsureMoveAvailable();
        }

        /// <summary>Deadlock recovery: free automatic shuffle (classic Onet behaviour).</summary>
        void EnsureMoveAvailable()
        {
            if (_moves.HasAnyMove(_model)) return;
            if (!config.autoShuffleOnDeadlock) return;
            Toast?.Invoke("No moves left – shuffling!");
            StartCoroutine(DoShuffle());
        }

        IEnumerator DoShuffle()
        {
            State = GameState.Resolving;
            Deselect();
            Shuffler.Shuffle(_model, _rng, _moves);
            Shuffled?.Invoke();
            yield return boardView.AnimateShuffle(0.5f);
            if (State == GameState.Resolving) State = GameState.Playing;
        }

        // ------------------------------------------------------------------ boosters

        /// <summary>Any currently legal pair (tutorial pointer, automated play-throughs). Does not consume a hint.</summary>
        public bool TryFindMove(out Cell a, out Cell b)
        {
            a = b = default;
            if (_model == null) return false;
            var m = _moves.FindAny(_model);
            if (!m.HasValue) return false;
            a = m.Value.A; b = m.Value.B;
            return true;
        }

        public Cell? SelectedCell => _selected;

        readonly List<Move> _guideMoves = new List<Move>();
        readonly List<Cell> _guidePath = new List<Cell>(4);

        /// <summary>
        /// Where a tutorial hand should point right now: the partner of the selected tile, or the first tile of a
        /// pair of the requested style. False when there is nothing sensible to point at.
        /// </summary>
        public bool TryGetGuidance(MoveStyle style, out Cell target)
        {
            target = default;
            if (_model == null) return false;
            _moves.FindAll(_model, _guideMoves);
            if (_selected.HasValue)
            {
                foreach (var m in _guideMoves)
                {
                    if (m.A == _selected.Value) { target = m.B; return true; }
                    if (m.B == _selected.Value) { target = m.A; return true; }
                }
                return false;
            }
            int best = -1;
            foreach (var m in _guideMoves)
            {
                _moves.PathFinder.FindPath(_model, m.A, m.B, _guidePath, Level.maxTurns);
                int turns = Mathf.Max(0, _guidePath.Count - 2);
                int score = style == MoveStyle.Straight ? (turns == 0 ? 2 : 1)
                          : style == MoveStyle.Turning ? (turns >= 1 ? 1 + turns : 0) : 1;
                if (score > best) { best = score; target = m.A; }
            }
            return best >= 0;
        }

        public bool CanUseBooster(BoosterType t) =>
            State == GameState.Playing && LevelNumber >= config.Get(t).unlockLevel && Save.GetBooster(t) > 0;

        public bool UseBooster(BoosterType t)
        {
            if (!CanUseBooster(t)) return false;
            bool used = false;
            switch (t)
            {
                case BoosterType.Hint:
                    ClearHint();
                    _activeHint = _moves.FindAny(_model);
                    if (_activeHint.HasValue) { boardView.SetHint(_activeHint, true); HintShown?.Invoke(_activeHint.Value.A, _activeHint.Value.B); used = true; StartCoroutine(HintTimeout()); }
                    break;
                case BoosterType.Shuffle:
                    ClearHint();
                    StartCoroutine(DoShuffle());
                    used = true;
                    break;
                case BoosterType.AutoMatch:
                    var m = _moves.FindAny(_model);
                    if (m.HasValue)
                    {
                        ClearHint(); Deselect();
                        _moves.PathFinder.FindPath(_model, m.Value.A, m.Value.B, _pathCells, Level.maxTurns);
                        StartCoroutine(ResolveMatch(m.Value.A, m.Value.B, _pathCells.Count - 2));
                        used = true;
                    }
                    break;
                case BoosterType.AddTime:
                    timer.AddTime(config.addTimeSeconds);
                    used = true;
                    break;
            }
            if (used) { Save.TryConsumeBooster(t); BoostersChanged?.Invoke(); BoosterUsed?.Invoke(t); }
            return used;
        }

        public bool BuyBooster(BoosterType t)
        {
            if (!Save.TrySpendCoins(config.Get(t).coinPrice)) return false;
            Save.AddBooster(t, 1);
            BoostersChanged?.Invoke();
            return true;
        }

        IEnumerator HintTimeout()
        {
            var hint = _activeHint;
            yield return new WaitForSeconds(config.hintHighlightTime);
            if (_activeHint.Equals(hint)) ClearHint();
        }

        void ClearHint()
        {
            if (_activeHint.HasValue) boardView.SetHint(_activeHint, false);
            _activeHint = null;
        }

        // ------------------------------------------------------------------ end states

        void Win()
        {
            State = GameState.Won;
            timer.Stop();
            int stars = Score.StarsFor(timer.TimeLeft, timer.TimeLimit);
            Score.AddTimeBonus(timer.TimeLeft);
            int coins = Level.coinReward * stars / 3 + (Save.GetStars(LevelNumber) == 0 ? Level.coinReward : 0);
            Save.RecordWin(LevelNumber, stars, Score.Score, coins);
            LevelWon?.Invoke(stars, Score.Score, coins);
        }

        void OnTimeUp()
        {
            if (State != GameState.Playing && State != GameState.Resolving) return;
            State = GameState.Lost;
            Deselect();
            ClearHint();
            LevelLost?.Invoke();
        }

        /// <summary>The time-out continue is still on offer for this attempt.</summary>
        public bool CanContinue => State == GameState.Lost && !_usedContinue;

        /// <summary>Continue after time-out (paid with coins or a rewarded ad). Once per attempt.</summary>
        public bool TryContinue(bool paidWithAd)
        {
            if (State != GameState.Lost || _usedContinue) return false;
            if (!paidWithAd && !Save.TrySpendCoins(config.continueCoinPrice)) return false;
            _usedContinue = true;
            timer.Begin(config.continueSeconds);
            State = GameState.Playing;
            return true;
        }

        public void Pause()
        {
            if (State != GameState.Playing && State != GameState.Resolving) return;
            State = GameState.Paused;
            timer.Pause();
            boardView.InputEnabled = false;
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            timer.Resume();
            boardView.InputEnabled = true;
        }

        public int PairsTotal { get; private set; }
        public int PairsMatched => PairsTotal - PairsLeft;

        public void Retry() => StartLevel(LevelNumber, isRetry: true);
        public void NextLevel() => StartLevel(LevelNumber + 1);

        void OnApplicationPause(bool paused) { if (paused) Pause(); }
    }
}
