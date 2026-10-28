using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Onet.Core;
using Onet.Data;
using UnityEditor;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Batch Levels
    ///  • Generate N levels from a ProgressionCurve (size, types, time, gravity, stones, shapes)
    ///  • Bake + validate every level, add to LevelDatabase
    ///  • Audit all levels: solvability, difficulty curve graph, CSV export for balancing
    /// </summary>
    public class LevelBatchWindow : EditorWindow
    {
        ProgressionCurve _curve;
        LevelDatabase _db;
        string _folder = "Assets/OnetConnect/Levels";
        int _from = 1, _to = 60;
        bool _overwrite;
        Vector2 _scroll;

        struct Row { public LevelData level; public bool ok; public float diff; public string note; }
        readonly List<Row> _report = new List<Row>();

        [MenuItem("Onet/Batch Levels")]
        static void Open() => GetWindow<LevelBatchWindow>("Onet Batch Levels");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);
            _curve = (ProgressionCurve)EditorGUILayout.ObjectField("Progression Curve", _curve, typeof(ProgressionCurve), false);
            _db = (LevelDatabase)EditorGUILayout.ObjectField("Level Database", _db, typeof(LevelDatabase), false);
            _folder = EditorGUILayout.TextField("Output folder", _folder);
            EditorGUILayout.BeginHorizontal();
            _from = EditorGUILayout.IntField("From", _from);
            _to = EditorGUILayout.IntField("To", _to);
            EditorGUILayout.EndHorizontal();
            _overwrite = EditorGUILayout.Toggle("Overwrite existing", _overwrite);

            using (new EditorGUI.DisabledScope(_curve == null || _db == null))
                if (GUILayout.Button("Generate + Bake + Validate", GUILayout.Height(28))) Generate();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audit", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_db == null))
            {
                if (GUILayout.Button("Validate all in database")) ValidateAll();
                if (GUILayout.Button("Export CSV")) ExportCsv();
                if (GUILayout.Button("Sort DB by level number")) { _db.levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber)); EditorUtility.SetDirty(_db); }
            }
            EditorGUILayout.EndHorizontal();

            DrawDifficultyGraph();
            DrawReport();
        }

        // ------------------------------------------------------------ generation

        void Generate()
        {
            _report.Clear();
            _report.AddRange(GenerateCore(_curve, _db, _folder, _from, _to, _overwrite));
        }

        /// <summary>Used by the Demo Project builder / CI scripts.</summary>
        public static void GenerateHeadless(ProgressionCurve curve, LevelDatabase db, string folder, int from, int to)
            => GenerateCore(curve, db, folder, from, to, true);

        static List<Row> GenerateCore(ProgressionCurve curve, LevelDatabase db, string folder, int from, int to, bool overwrite)
        {
            var report = new List<Row>();
            Directory.CreateDirectory(folder);
            try
            {
                for (int n = from; n <= to; n++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Generating levels", $"Level {n}", (float)(n - from) / (to - from + 1))) break;
                    string path = $"{folder}/Level_{n:000}.asset";
                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    if (level != null && !overwrite) { report.Add(new Row { level = level, ok = level.metrics.verified, diff = level.metrics.difficultyScore, note = "kept" }); continue; }
                    if (level == null) { level = CreateInstance<LevelData>(); AssetDatabase.CreateAsset(level, path); }

                    Configure(curve, db, level, n);
                    report.Add(BakeAndValidate(level));
                    if (!db.levels.Contains(level)) db.levels.Add(level);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            db.levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return report;
        }

        static void Configure(ProgressionCurve curve, LevelDatabase db, LevelData level, int n)
        {
            var rng = new System.Random(n * 92821);
            int w = curve.WidthAt(n), h = curve.HeightAt(n);

            ShapePreset shape = ShapePreset.Rect;
            if (n >= curve.shapesFromLevel && !curve.IsBreather(n))
            {
                var shapes = (ShapePreset[])Enum.GetValues(typeof(ShapePreset));
                shape = shapes[rng.Next(shapes.Length)];
                // non-rect shapes lose cells – grow the canvas a little to compensate
                if (shape != ShapePreset.Rect) { w = Mathf.Min(curve.maxWidth, w + 1); h = Mathf.Min(curve.maxHeight, h + 1); }
            }

            level.levelNumber = n;
            level.width = w; level.height = h;
            level.layout = ShapeLibrary.Create(shape, w, h, rng);
            if (n >= curve.stonesFromLevel && !curve.IsBreather(n) && shape != ShapePreset.Pillars)
                ShapeLibrary.AddStones(level.layout, w, h, curve.stoneDensity, rng);
            level.fixedTypes = new int[w * h];
            level.gravity = curve.GravityAt(n, rng);
            int pairs = level.SlotCount / 2;
            // Design rule: every icon appears at least 4 times (≥2 pairs) – avoids "needle in haystack" boards
            level.tileTypes = Mathf.Clamp(curve.TypesAt(n), 2, Mathf.Max(2, pairs / 2));
            level.timeLimit = curve.TimeFor(n, pairs);
            level.seed = n * 1000 + 1;
            level.coinReward = 10 + n / 5 * 2;
            var chapter = db.ChapterFor(n);
            if (chapter != null && chapter.theme != null && level.tileTypes > chapter.theme.IconCount && chapter.theme.IconCount > 0)
                level.tileTypes = chapter.theme.IconCount;
            EditorUtility.SetDirty(level);
        }

        static Row BakeAndValidate(LevelData level)
        {
            var res = BoardGenerator.Generate(level.ToSpec());
            if (res.Board == null) return new Row { level = level, ok = false, diff = 10, note = res.Error };
            level.seed = res.SeedUsed;
            level.bakedTiles = new int[level.width * level.height];
            for (int y = 0; y < level.height; y++)
                for (int x = 0; x < level.width; x++)
                    level.bakedTiles[y * level.width + x] = Mathf.Max(0, res.Board.Get(x, y));

            var solve = new Solver().Solve(res.Board, level.gravity, level.maxTurns, 100000);
            int pairs = res.Board.TileCount / 2;
            level.metrics = new LevelMetrics
            {
                verified = solve.Solved,
                initialMoves = solve.InitialMoves,
                averageBranching = solve.AverageBranching,
                forcedSteps = solve.ForcedSteps,
                solverNodes = solve.NodesExpanded,
                difficultyScore = solve.Solved ? DifficultyModel.Score(pairs, level.tileTypes, solve.AverageBranching, level.gravity, level.timeLimit) : 10,
                validatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            EditorUtility.SetDirty(level);
            return new Row { level = level, ok = solve.Solved, diff = level.metrics.difficultyScore, note = $"{level.width}x{level.height} {level.gravity}" };
        }

        void ValidateAll()
        {
            _report.Clear();
            try
            {
                for (int i = 0; i < _db.levels.Count; i++)
                {
                    var l = _db.levels[i];
                    if (l == null) continue;
                    EditorUtility.DisplayProgressBar("Validating", l.name, (float)i / _db.levels.Count);
                    if (!l.HasBakedDeal) { _report.Add(BakeAndValidate(l)); continue; }
                    var board = l.CreateBoard(false, out _);
                    var s = new Solver().Solve(board, l.gravity, l.maxTurns, 100000);
                    l.metrics.verified = s.Solved;
                    l.metrics.averageBranching = s.AverageBranching;
                    l.metrics.initialMoves = s.InitialMoves;
                    l.metrics.difficultyScore = s.Solved ? DifficultyModel.Score(board.TileCount / 2, l.tileTypes, s.AverageBranching, l.gravity, l.timeLimit) : 10;
                    EditorUtility.SetDirty(l);
                    _report.Add(new Row { level = l, ok = s.Solved, diff = l.metrics.difficultyScore, note = s.BudgetExceeded ? "budget exceeded" : "" });
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
        }

        void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export level report", "", "onet_levels.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder("level,width,height,slots,pairs,types,gravity,time,sec_per_pair,verified,initial_moves,avg_branching,forced,difficulty\n");
            foreach (var l in _db.levels)
            {
                if (l == null) continue;
                int pairs = l.SlotCount / 2;
                var m = l.metrics;
                sb.AppendLine($"{l.levelNumber},{l.width},{l.height},{l.SlotCount},{pairs},{l.tileTypes},{l.gravity},{l.timeLimit},{(pairs > 0 ? l.timeLimit / pairs : 0):F2},{m.verified},{m.initialMoves},{m.averageBranching:F2},{m.forcedSteps},{m.difficultyScore:F1}");
            }
            File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);
        }

        // ------------------------------------------------------------ report UI

        void DrawDifficultyGraph()
        {
            if (_db == null || _db.levels.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Difficulty curve (bars = difficulty 0–10, red = unverified, blue = breather)", EditorStyles.miniBoldLabel);
            Rect r = GUILayoutUtility.GetRect(position.width - 20, 120);
            EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
            int n = _db.levels.Count;
            float bw = r.width / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                var l = _db.levels[i];
                if (l == null) continue;
                float d = Mathf.Clamp(l.metrics.difficultyScore, 0, 10) / 10f;
                Color c = !l.metrics.verified ? new Color(0.9f, 0.3f, 0.3f)
                        : (_curve != null && _curve.IsBreather(l.levelNumber)) ? new Color(0.4f, 0.6f, 1f)
                        : Color.Lerp(new Color(0.4f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.2f), d);
                EditorGUI.DrawRect(new Rect(r.x + i * bw, r.yMax - d * r.height, Mathf.Max(1, bw - 1), d * r.height), c);
            }
        }

        void DrawReport()
        {
            if (_report.Count == 0) return;
            int fails = 0; foreach (var row in _report) if (!row.ok) fails++;
            EditorGUILayout.HelpBox($"{_report.Count} levels processed, {fails} unverified.", fails > 0 ? MessageType.Warning : MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            foreach (var row in _report)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(row.level, typeof(LevelData), false, GUILayout.Width(160));
                GUILayout.Label(row.ok ? "✔" : "✖", GUILayout.Width(20));
                GUILayout.Label($"{row.diff:F1} {DifficultyModel.Label(row.diff)}", GUILayout.Width(110));
                GUILayout.Label(row.note);
                if (GUILayout.Button("Edit", GUILayout.Width(40))) LevelEditorWindow.Open(row.level);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
