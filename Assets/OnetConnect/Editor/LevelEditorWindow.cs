using System;
using System.Collections.Generic;
using Onet.Core;
using Onet.Data;
using Onet.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Level Editor
    ///  • Paint the board shape (Slot / Hole / Stone / Pin tile) with click-drag, optional X/Y mirroring
    ///  • Shape presets, resize, clear
    ///  • Generate &amp; Bake a verified deal (seeded), preview it with theme icons
    ///  • Validate: solver proof + difficulty metrics, step through the solution
    ///  • Play: jump straight into the game scene on this level
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        enum Brush { Slot, Hole, Stone, Pin }

        LevelData _level;
        Brush _brush = Brush.Slot;
        int _pinType = 1;
        bool _mirrorX, _mirrorY;
        bool _showDeal = true;
        ShapePreset _preset = ShapePreset.Rect;
        Vector2 _scroll;
        int _newW, _newH;

        // validation state
        SolveResult _lastSolve;
        int _solutionStep = -1;
        string _status = "";
        MessageType _statusType = MessageType.None;

        const float CellPx = 34f;
        const string GameScenePath = "Assets/OnetConnect/Scenes/Game.unity";

        [MenuItem("Onet/Level Editor %#l")]
        public static void Open() => GetWindow<LevelEditorWindow>("Onet Level Editor");

        public static void Open(LevelData level)
        {
            var w = GetWindow<LevelEditorWindow>("Onet Level Editor");
            w.SetLevel(level);
        }

        void SetLevel(LevelData level)
        {
            _level = level;
            _lastSolve = null;
            _solutionStep = -1;
            _status = "";
            if (_level != null) { _level.EnsureArrays(); _newW = _level.width; _newH = _level.height; }
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is LevelData ld && ld != _level) { SetLevel(ld); Repaint(); }
        }

        void OnGUI()
        {
            DrawHeader();
            if (_level == null) { EditorGUILayout.HelpBox("Select or create a LevelData asset.", MessageType.Info); return; }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawRules();
            DrawShapeTools();
            DrawBrushes();
            DrawActions();
            EditorGUILayout.EndVertical();
            GUILayout.Space(12);
            EditorGUILayout.BeginVertical();
            DrawStats();
            DrawGrid();
            DrawSolutionStepper();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ panels

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var picked = (LevelData)EditorGUILayout.ObjectField(_level, typeof(LevelData), false, GUILayout.Width(260));
            if (picked != _level) SetLevel(picked);
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) CreateNew();
            if (_level != null && GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70))) Duplicate();
            GUILayout.FlexibleSpace();
            if (_level != null && GUILayout.Button("▶ Play Level", EditorStyles.toolbarButton, GUILayout.Width(90))) PlayLevel();
            EditorGUILayout.EndHorizontal();
        }

        void DrawRules()
        {
            var so = new SerializedObject(_level);
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("levelNumber"));
            EditorGUILayout.PropertyField(so.FindProperty("theme"));
            EditorGUILayout.PropertyField(so.FindProperty("tileTypes"));
            EditorGUILayout.PropertyField(so.FindProperty("gravity"));
            EditorGUILayout.PropertyField(so.FindProperty("maxTurns"));
            EditorGUILayout.PropertyField(so.FindProperty("timeLimit"));
            EditorGUILayout.PropertyField(so.FindProperty("twoStarFraction"), new GUIContent("2★ at time left"));
            EditorGUILayout.PropertyField(so.FindProperty("threeStarFraction"), new GUIContent("3★ at time left"));
            EditorGUILayout.PropertyField(so.FindProperty("coinReward"));
            EditorGUILayout.PropertyField(so.FindProperty("freshDealOnRetry"));
            EditorGUILayout.PropertyField(so.FindProperty("seed"));
            if (so.ApplyModifiedProperties()) InvalidateBake("Rules changed");
            EditorGUILayout.Space();
        }

        void DrawShapeTools()
        {
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _newW = EditorGUILayout.IntField("W", _newW);
            _newH = EditorGUILayout.IntField("H", _newH);
            if (GUILayout.Button("Resize", GUILayout.Width(60)))
            {
                Undo.RecordObject(_level, "Resize level");
                _level.Resize(Mathf.Clamp(_newW, 2, 20), Mathf.Clamp(_newH, 2, 24));
                MarkDirty("Resized");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _preset = (ShapePreset)EditorGUILayout.EnumPopup(_preset);
            if (GUILayout.Button("Apply preset", GUILayout.Width(100)))
            {
                Undo.RecordObject(_level, "Apply preset");
                _level.layout = ShapeLibrary.Create(_preset, _level.width, _level.height, new System.Random(_level.seed));
                _level.fixedTypes = new int[_level.layout.Length];
                MarkDirty("Preset applied");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add stones")) { Undo.RecordObject(_level, "Stones"); ShapeLibrary.AddStones(_level.layout, _level.width, _level.height, 0.06f, new System.Random(_level.seed)); MarkDirty("Stones added"); }
            if (GUILayout.Button("Fix parity")) { Undo.RecordObject(_level, "Parity"); ShapeLibrary.FixParity(_level.layout, _level.width, _level.height); MarkDirty("Parity fixed"); }
            if (GUILayout.Button("Clear pins")) { Undo.RecordObject(_level, "Pins"); _level.fixedTypes = new int[_level.layout.Length]; MarkDirty("Pins cleared"); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        void DrawBrushes()
        {
            EditorGUILayout.LabelField("Brush (LMB paint, RMB = Hole)", EditorStyles.boldLabel);
            _brush = (Brush)GUILayout.Toolbar((int)_brush, new[] { "Slot", "Hole", "Stone", "Pin" });
            if (_brush == Brush.Pin) _pinType = EditorGUILayout.IntSlider("Pin tile type", _pinType, 1, Mathf.Max(1, _level.tileTypes));
            EditorGUILayout.BeginHorizontal();
            _mirrorX = GUILayout.Toggle(_mirrorX, "Mirror X", "Button");
            _mirrorY = GUILayout.Toggle(_mirrorY, "Mirror Y", "Button");
            _showDeal = GUILayout.Toggle(_showDeal, "Show deal", "Button");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        void DrawActions()
        {
            EditorGUILayout.LabelField("Deal & Validate", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate & Bake (current seed)", GUILayout.Height(26))) GenerateAndBake(false);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New random seed")) GenerateAndBake(true);
            if (GUILayout.Button("Clear bake")) { Undo.RecordObject(_level, "Clear bake"); _level.bakedTiles = Array.Empty<int>(); MarkDirty("Bake cleared"); }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Validate (solver + metrics)")) Validate();
            if (GUILayout.Button("Suggest time limit")) SuggestTime();

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
        }

        void DrawStats()
        {
            int slots = _level.SlotCount;
            int pairs = slots / 2;
            var m = _level.metrics;
            string parity = slots % 2 == 0 ? "" : "  ⚠ ODD";
            EditorGUILayout.LabelField(
                $"{_level.width}×{_level.height}   slots {slots}{parity}   pairs {pairs}   " +
                $"≈{(pairs > 0 ? (float)pairs / Mathf.Max(1, _level.tileTypes) : 0):F1} pairs/type   " +
                $"{(pairs > 0 ? _level.timeLimit / pairs : 0):F1}s/pair", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                m.verified
                    ? $"✔ Verified  diff {m.difficultyScore} ({DifficultyModel.Label(m.difficultyScore)})  initial moves {m.initialMoves}  avg branching {m.averageBranching:F1}  forced steps {m.forcedSteps}"
                    : "✖ Not validated", EditorStyles.miniLabel);
            if (_level.theme != null && _level.tileTypes > _level.theme.IconCount)
                EditorGUILayout.HelpBox($"Theme has only {_level.theme.IconCount} icons for {_level.tileTypes} types.", MessageType.Warning);
        }

        // ------------------------------------------------------------ grid

        void DrawGrid()
        {
            int w = _level.width, h = _level.height;
            Rect area = GUILayoutUtility.GetRect((w + 2) * CellPx, (h + 2) * CellPx, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.18f, 0.2f, 0.18f));
            Vector2 origin = area.position + new Vector2(CellPx, CellPx);

            Move? stepMove = null;
            if (_lastSolve != null && _lastSolve.Solved && _solutionStep >= 0 && _solutionStep < _lastSolve.Solution.Count)
                stepMove = _lastSolve.Solution[_solutionStep];

            BoardModel stepBoard = stepMove.HasValue ? ReplayTo(_solutionStep) : null;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Rect r = new Rect(origin.x + x * CellPx + 1, origin.y + y * CellPx + 1, CellPx - 2, CellPx - 2);
                    var lc = _level.layout[i];
                    Color bg = lc == LayoutCell.Slot ? new Color(0.93f, 0.92f, 0.85f)
                             : lc == LayoutCell.Blocked ? new Color(0.35f, 0.33f, 0.3f)
                             : new Color(0, 0, 0, 0.15f);
                    int shownType = 0;
                    if (stepBoard != null) shownType = Mathf.Max(0, stepBoard.Get(x, y));
                    else if (_showDeal && _level.HasBakedDeal) shownType = _level.bakedTiles[i];
                    if (stepBoard != null && lc == LayoutCell.Slot && shownType == 0) bg = new Color(0.6f, 0.6f, 0.55f, 0.4f);

                    bool isStepCell = stepMove.HasValue && (stepMove.Value.A == new Cell(x, y) || stepMove.Value.B == new Cell(x, y));
                    if (isStepCell) bg = new Color(1f, 0.85f, 0.3f);
                    EditorGUI.DrawRect(r, bg);

                    int pin = _level.fixedTypes != null && i < _level.fixedTypes.Length ? _level.fixedTypes[i] : 0;
                    if (pin > 0) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3), new Color(0.9f, 0.3f, 0.3f));

                    int iconType = shownType > 0 ? shownType : pin;
                    if (iconType > 0 && lc == LayoutCell.Slot) DrawIcon(r, iconType);
                    if (lc == LayoutCell.Blocked) GUI.Label(r, "■", CenteredLabel(Color.gray));
                }

            HandlePaint(area, origin);
        }

        void DrawIcon(Rect r, int type)
        {
            var sprite = _level.theme != null ? _level.theme.GetIcon(type) : null;
            if (sprite != null && sprite.texture != null)
            {
                var tex = sprite.texture;
                var tr = sprite.textureRect;
                var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                GUI.DrawTextureWithTexCoords(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), tex, uv);
            }
            else
            {
                // Colour-coded number fallback when no theme is assigned
                Color c = Color.HSVToRGB((type * 0.137f) % 1f, 0.55f, 0.85f);
                EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), c);
                GUI.Label(r, type.ToString(), CenteredLabel(Color.black));
            }
        }

        static GUIStyle CenteredLabel(Color c)
        {
            var s = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = c;
            return s;
        }

        void HandlePaint(Rect area, Vector2 origin)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition)) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            int x = Mathf.FloorToInt((e.mousePosition.x - origin.x) / CellPx);
            int y = Mathf.FloorToInt((e.mousePosition.y - origin.y) / CellPx);
            if (x < 0 || y < 0 || x >= _level.width || y >= _level.height) return;

            Undo.RecordObject(_level, "Paint level");
            bool erase = e.button == 1;
            PaintAt(x, y, erase);
            if (_mirrorX) PaintAt(_level.width - 1 - x, y, erase);
            if (_mirrorY) PaintAt(x, _level.height - 1 - y, erase);
            if (_mirrorX && _mirrorY) PaintAt(_level.width - 1 - x, _level.height - 1 - y, erase);
            MarkDirty(null);
            e.Use();
        }

        void PaintAt(int x, int y, bool erase)
        {
            int i = y * _level.width + x;
            if (erase) { _level.layout[i] = LayoutCell.Hole; _level.fixedTypes[i] = 0; return; }
            switch (_brush)
            {
                case Brush.Slot:  _level.layout[i] = LayoutCell.Slot; break;
                case Brush.Hole:  _level.layout[i] = LayoutCell.Hole; _level.fixedTypes[i] = 0; break;
                case Brush.Stone: _level.layout[i] = LayoutCell.Blocked; _level.fixedTypes[i] = 0; break;
                case Brush.Pin:   _level.layout[i] = LayoutCell.Slot; _level.fixedTypes[i] = _pinType; break;
            }
        }

        // ------------------------------------------------------------ solution stepper

        void DrawSolutionStepper()
        {
            if (_lastSolve == null || !_lastSolve.Solved) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Solution preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(30))) _solutionStep = Mathf.Max(-1, _solutionStep - 1);
            _solutionStep = EditorGUILayout.IntSlider(_solutionStep, -1, _lastSolve.Solution.Count - 1);
            if (GUILayout.Button("▶", GUILayout.Width(30))) _solutionStep = Mathf.Min(_lastSolve.Solution.Count - 1, _solutionStep + 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(_solutionStep < 0 ? "Initial board" : $"Step {_solutionStep + 1}: {_lastSolve.Solution[_solutionStep]} (highlighted pair is removed next)", EditorStyles.miniLabel);
        }

        BoardModel _replayCache;
        int _replayCacheStep = -2;

        BoardModel ReplayTo(int step)
        {
            if (_replayCache != null && _replayCacheStep == step) return _replayCache;
            var board = _level.CreateBoard(false, out _);
            if (board == null) return null;
            for (int s = 0; s < step; s++)
            {
                var m = _lastSolve.Solution[s];
                board.RemovePair(m.A, m.B);
                Gravity.Apply(board, _level.gravity);
            }
            _replayCache = board;
            _replayCacheStep = step;
            return board;
        }

        // ------------------------------------------------------------ actions

        void GenerateAndBake(bool newSeed)
        {
            Undo.RecordObject(_level, "Bake deal");
            if (newSeed) _level.seed = UnityEngine.Random.Range(1, 999999);
            var spec = _level.ToSpec();
            string err = spec.Validate();
            if (err != null) { SetStatus(err, MessageType.Error); return; }

            var res = BoardGenerator.Generate(spec);
            if (res.Board == null) { SetStatus(res.Error, MessageType.Error); return; }

            _level.seed = res.SeedUsed;
            _level.bakedTiles = new int[_level.width * _level.height];
            for (int y = 0; y < _level.height; y++)
                for (int x = 0; x < _level.width; x++)
                    _level.bakedTiles[y * _level.width + x] = Mathf.Max(0, res.Board.Get(x, y));
            EditorUtility.SetDirty(_level);
            SetStatus($"Baked seed {res.SeedUsed} after {res.Attempts} attempt(s). Verified: {res.Verified}", res.Verified ? MessageType.Info : MessageType.Warning);
            Validate();
        }

        void Validate()
        {
            var board = _level.CreateBoard(false, out string err);
            if (board == null) { SetStatus(err, MessageType.Error); return; }
            var t0 = DateTime.Now;
            _lastSolve = new Solver().Solve(board, _level.gravity, _level.maxTurns, 200000);
            _replayCache = null;
            double ms = (DateTime.Now - t0).TotalMilliseconds;
            _solutionStep = -1;

            Undo.RecordObject(_level, "Validate");
            int pairs = board.TileCount / 2;
            _level.metrics = new LevelMetrics
            {
                verified = _lastSolve.Solved,
                initialMoves = _lastSolve.InitialMoves,
                averageBranching = _lastSolve.AverageBranching,
                forcedSteps = _lastSolve.ForcedSteps,
                solverNodes = _lastSolve.NodesExpanded,
                difficultyScore = _lastSolve.Solved ? DifficultyModel.Score(pairs, _level.tileTypes, _lastSolve.AverageBranching, _level.gravity, _level.timeLimit) : 10f,
                validatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            EditorUtility.SetDirty(_level);

            if (_lastSolve.Solved) SetStatus($"Solvable ✔  ({_lastSolve.NodesExpanded} nodes, {ms:F0} ms)", MessageType.Info);
            else if (_lastSolve.BudgetExceeded) SetStatus("Solver budget exceeded – probably solvable but unproven. Try another seed.", MessageType.Warning);
            else SetStatus("NOT solvable without shuffles. Re-deal or change shape.", MessageType.Error);
        }

        void SuggestTime()
        {
            Undo.RecordObject(_level, "Suggest time");
            int pairs = _level.SlotCount / 2;
            // 4 s/pair baseline, +1 s per gravity twist, +0.5 s when types ≥ pairs/2 (hard to spot)
            float perPair = 4f + (_level.gravity != GravityMode.None ? 1f : 0f) + (_level.tileTypes >= pairs / 2 ? 0.5f : 0f);
            _level.timeLimit = Mathf.Max(30f, Mathf.Round(pairs * perPair / 5f) * 5f);
            EditorUtility.SetDirty(_level);
            SetStatus($"Time limit set to {_level.timeLimit}s ({perPair:F1}s/pair)", MessageType.Info);
        }

        void PlayLevel()
        {
            AssetDatabase.SaveAssets();
            PlayerPrefs.SetInt(GameController.EditorPlayLevelKey, _level.levelNumber);
            PlayerPrefs.Save();
            if (!EditorApplication.isPlaying)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (System.IO.File.Exists(GameScenePath)) EditorSceneManager.OpenScene(GameScenePath);
                EditorApplication.isPlaying = true;
            }
        }

        void CreateNew()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Level", "Level_001", "asset", "Create level");
            if (string.IsNullOrEmpty(path)) return;
            var ld = CreateInstance<LevelData>();
            ld.Resize(8, 10);
            AssetDatabase.CreateAsset(ld, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = ld;
            SetLevel(ld);
        }

        void Duplicate()
        {
            string src = AssetDatabase.GetAssetPath(_level);
            string dst = AssetDatabase.GenerateUniqueAssetPath(src);
            AssetDatabase.CopyAsset(src, dst);
            var copy = AssetDatabase.LoadAssetAtPath<LevelData>(dst);
            copy.levelNumber = _level.levelNumber + 1;
            EditorUtility.SetDirty(copy);
            SetLevel(copy);
            Selection.activeObject = copy;
        }

        void InvalidateBake(string why)
        {
            _lastSolve = null;
            _solutionStep = -1;
            _level.metrics.verified = false;
            SetStatus(why + " – re-validate before shipping.", MessageType.Warning);
        }

        void MarkDirty(string msg)
        {
            // Shape edits invalidate the baked deal
            _level.bakedTiles = Array.Empty<int>();
            _level.metrics.verified = false;
            _lastSolve = null;
            _solutionStep = -1;
            EditorUtility.SetDirty(_level);
            if (msg != null) SetStatus(msg, MessageType.None);
            Repaint();
        }

        void SetStatus(string s, MessageType t) { _status = s; _statusType = t; Repaint(); }
    }
}
