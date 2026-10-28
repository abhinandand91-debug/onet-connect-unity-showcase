// Standalone test runner for Onet.Core (compile with: mcs -out:t.exe Core/*.cs Tests/CoreTests.cs)
// Inside Unity, the same checks can be ported to NUnit EditMode tests.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Onet.Core;

static class CoreTests
{
    static int _fail, _pass;
    static void Check(bool cond, string name)
    {
        if (cond) _pass++; else { _fail++; Console.WriteLine("FAIL: " + name); }
    }

    static BoardModel FromRows(params string[] rows)
    {
        // '.' empty slot, '#' blocked, ' ' hole, letters = tile types
        var b = new BoardModel(rows[0].Length, rows.Length);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                char ch = rows[y][x];
                if (ch == '#') { b.Set(x, y, BoardModel.Blocked); continue; }
                if (ch == ' ') continue;
                b.SetSlot(x, y, true);
                if (ch != '.') b.Set(x, y, ch - 'A' + 1);
            }
        return b;
    }

    static void Main()
    {
        var pf = new PathFinder();
        var path = new List<Cell>();

        // Straight line
        var b = FromRows("A..A", "BBBB");
        Check(pf.FindPath(b, new Cell(0, 0), new Cell(3, 0), path, 0), "straight 0-turn");
        Check(path.Count == 2, "straight path has 2 points");

        // Adjacent
        b = FromRows("AA", "BB");
        Check(pf.CanConnect(b, new Cell(0, 0), new Cell(1, 0), 0), "adjacent");

        // Blocked straight but L works
        b = FromRows(
            "A#C",
            "...",
            "C#A");
        Check(!pf.CanConnect(b, new Cell(0, 0), new Cell(2, 2), 0), "no straight diag");
        Check(pf.FindPath(b, new Cell(0, 0), new Cell(2, 2), path, 1) == false, "L blocked by C tiles");
        Check(pf.FindPath(b, new Cell(0, 0), new Cell(2, 2), path, 2), "Z path through middle");
        Check(path.Count == 4, "Z path 4 points, got " + path.Count);

        // Needs outer lane (U shape around the top)
        b = FromRows(
            "ABA",
            "BBB");
        Check(pf.FindPath(b, new Cell(0, 0), new Cell(2, 0), path, 2), "U via outer lane");
        Check(path.Count == 4 && path[1].Y == -1 && path[2].Y == -1, "U path uses row -1");

        // 3 turns required → illegal with classic rule
        b = FromRows(
            "#####",
            "#A.B#",
            "###.#",
            "#A..#",
            "#####");
        // A(1,1) to A(1,3): right to (2,1)? blocked by B at (3,1). Only via (2,1) down? (2,2) is # → unreachable
        Check(!pf.CanConnect(b, new Cell(1, 1), new Cell(1, 3), 2), "enclosed unreachable");

        // Different types never connect
        b = FromRows("AB");
        Check(!pf.CanConnect(b, new Cell(0, 0), new Cell(1, 0)), "different types");

        // Exactly-2-turn vs 3-turn discrimination
        b = FromRows(
            "A..B",
            "BBB.",
            "....",
            ".BBB",
            "B..A");
        // A(0,0) → right to (3,0), down to (3,2), left to (0,2)… A is at (3,4): route (0,0)->(3,0)->(3,2)?  (3,3) is B.
        // Outer lane: (0,0) up to (0,-1), right to (4,-1), down to (4,4), left to (3,4) = 3 turns → illegal
        // Alternative: (0,0)->(-1,0)->(-1,5)->(3,5)->(3,4) = 3 turns → illegal
        Check(!pf.CanConnect(b, new Cell(0, 0), new Cell(3, 4), 2), "requires 3 turns → illegal");
        Check(pf.CanConnect(b, new Cell(0, 0), new Cell(3, 4), 3), "3 turns legal when maxTurns=3");

        // Gravity down
        b = FromRows(
            "A.",
            ".B",
            "..");
        var mv = Gravity.Apply(b, GravityMode.Down);
        Check(b.Get(0, 2) == 1 && b.Get(1, 2) == 2 && b.Get(0, 0) == 0, "gravity down");
        Check(mv.Count == 2, "gravity move count");

        // Gravity respects blocked segments
        b = FromRows(
            "A",
            ".",
            "#",
            "B",
            ".");
        Gravity.Apply(b, GravityMode.Down);
        Check(b.Get(0, 1) == 1 && b.Get(0, 4) == 2 && b.Get(0, 2) == BoardModel.Blocked, "gravity blocked segments");

        // Gravity skips holes
        b = FromRows(
            "A",
            " ",
            ".");
        Gravity.Apply(b, GravityMode.Down);
        Check(b.Get(0, 2) == 1 && b.Get(0, 1) == 0, "gravity skips hole");

        // Collapse to center X
        b = FromRows("A..B");
        Gravity.Apply(b, GravityMode.CollapseToCenterX);
        Check(b.Get(1, 0) == 1 && b.Get(2, 0) == 2, "collapse center x");
        b = FromRows(".AB.");
        Gravity.Apply(b, GravityMode.SplitFromCenterX);
        Check(b.Get(0, 0) == 1 && b.Get(3, 0) == 2, "split center x");

        // Generator: reverse construction on full rect, then solve
        var solver = new Solver();
        var sw = Stopwatch.StartNew();
        int verified = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var spec = LevelSpec.Rect(8, 12, 16, GravityMode.None, seed);
            var g = BoardGenerator.Generate(spec);
            Check(g.Board != null, "gen rect seed " + seed + " " + g.Error);
            if (g.Board == null) continue;
            Check(g.Board.TileCount == 96, "tile count 96");
            var s = solver.Solve(g.Board, GravityMode.None, 2, 200000);
            if (s.Solved) verified++;
            // Replay the solution on a clone to be sure
            var replay = g.Board.Clone();
            bool ok = s.Solved;
            foreach (var m in s.Solution) ok &= pf.CanConnect(replay, m.A, m.B) && replay.RemovePair(m.A, m.B);
            Check(!s.Solved || (ok && replay.IsCleared), "replay solution seed " + seed);
        }
        Console.WriteLine($"Deal+verify 8x12: {verified}/20 solver-confirmed in {sw.ElapsedMilliseconds} ms");
        Check(verified == 20, "all generated boards solvable");

        // Determinism
        var d1 = BoardGenerator.Generate(LevelSpec.Rect(6, 8, 10, GravityMode.None, 42)).Board.ToString();
        var d2 = BoardGenerator.Generate(LevelSpec.Rect(6, 8, 10, GravityMode.None, 42)).Board.ToString();
        Check(d1 == d2, "deterministic by seed");

        // Gravity levels via deal & verify
        foreach (GravityMode gm in Enum.GetValues(typeof(GravityMode)))
        {
            if (gm == GravityMode.None) continue;
            sw.Restart();
            var spec = LevelSpec.Rect(8, 10, 14, gm, 7);
            var g = BoardGenerator.Generate(spec);
            Check(g.Board != null && g.Verified, "gravity gen " + gm + " " + g.Error);
            Console.WriteLine($"  {gm,-18} attempts={g.Attempts} verified={g.Verified} {sw.ElapsedMilliseconds} ms");
        }

        // Shaped layout with blocked cells and pinned tiles
        var shaped = new LevelSpec { Width = 7, Height = 7, TileTypes = 8, Seed = 3, Layout = new LayoutCell[49], FixedTypes = new int[49] };
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++)
        {
            int dx = Math.Abs(x - 3), dy = Math.Abs(y - 3);
            shaped.Layout[y * 7 + x] = dx + dy <= 3 ? LayoutCell.Slot : LayoutCell.Hole; // diamond (25 cells)
        }
        shaped.Layout[3 * 7 + 3] = LayoutCell.Blocked; // centre stone → 24 slots
        shaped.FixedTypes[0 * 7 + 3] = 5; // pinned tile at top
        var sg = BoardGenerator.Generate(shaped);
        Check(sg.Board != null && sg.Verified, "diamond w/ pinned " + sg.Error);
        if (sg.Board != null) { Console.WriteLine("Diamond board:"); Console.Write(sg.Board.ToString()); }

        // Shuffle keeps multiset and yields a move
        var sb = BoardGenerator.Generate(LevelSpec.Rect(6, 6, 18, GravityMode.None, 5)).Board;
        var before = Histogram(sb);
        var finder = new MoveFinder();
        Check(Shuffler.Shuffle(sb, new Random(1), finder), "shuffle succeeds");
        Check(Same(before, Histogram(sb)), "shuffle preserves tiles");
        Check(finder.HasAnyMove(sb), "shuffle yields move");

        // Score
        var score = new ScoreSystem();
        score.RegisterMatch(0f, 1); score.RegisterMatch(1f, 2); score.RegisterMatch(10f, 0);
        Check(score.Score == 10 + 20 + 15 && score.BestCombo == 2, "combo scoring " + score.Score);
        Check(score.StarsFor(60, 100) == 3 && score.StarsFor(30, 100) == 2 && score.StarsFor(5, 100) == 1, "stars");

        // Perf: hint scan on a 12x20 board
        var big = BoardGenerator.Generate(LevelSpec.Rect(12, 20, 36, GravityMode.None, 9)).Board;
        sw.Restart();
        int movesTotal = 0;
        for (int i = 0; i < 100; i++) movesTotal += finder.FindAll(big).Count;
        Console.WriteLine($"FindAll on 12x20 (240 tiles): {sw.Elapsed.TotalMilliseconds / 100:F2} ms/scan, {movesTotal / 100} moves");

        // Shapes: every preset has even slots and generates a verified board
        foreach (ShapePreset sp in Enum.GetValues(typeof(ShapePreset)))
        {
            var lay = ShapeLibrary.Create(sp, 9, 11, new Random(4));
            var spec = new LevelSpec { Width = 9, Height = 11, Layout = lay, TileTypes = 14, Seed = 11 };
            Check(spec.SlotCount() % 2 == 0, "parity " + sp);
            var g = BoardGenerator.Generate(spec);
            Check(g.Board != null && g.Verified, "shape gen " + sp + " " + g.Error);
            var sres = solver.Solve(g.Board, GravityMode.None);
            Console.WriteLine($"  shape {sp,-16} slots={spec.SlotCount(),3} initialMoves={sres.InitialMoves,2} avgBranch={sres.AverageBranching:F1} diff={DifficultyModel.Score(spec.SlotCount()/2, 14, sres.AverageBranching, GravityMode.None, 120)}");
        }

        // Randomised cross-check of PathFinder against brute-force state BFS
        var rnd = new Random(123);
        int mismatches = 0, checks = 0;
        for (int trial = 0; trial < 300; trial++)
        {
            int w = rnd.Next(2, 9), h = rnd.Next(2, 9);
            var rb = new BoardModel(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            { int r = rnd.Next(10); rb.SetSlot(x, y, true); rb.Set(x, y, r < 4 ? 0 : r == 4 ? BoardModel.Blocked : 1 + rnd.Next(3)); }
            var tiles = new List<Cell>(rb.AllTiles());
            for (int i = 0; i < tiles.Count; i++) for (int j = i + 1; j < tiles.Count; j++)
            {
                if (rb.Get(tiles[i]) != rb.Get(tiles[j])) continue;
                for (int turns = 0; turns <= 2; turns++)
                {
                    checks++;
                    if (pf.CanConnect(rb, tiles[i], tiles[j], turns) != Brute(rb, tiles[i], tiles[j], turns)) mismatches++;
                }
            }
        }
        Console.WriteLine($"PathFinder vs brute force: {checks} checks, {mismatches} mismatches");
        Check(mismatches == 0, "pathfinder matches brute force");

        Console.WriteLine($"\n{_pass} passed, {_fail} failed");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }

    // BFS over (cell, direction, turnsUsed)
    static bool Brute(BoardModel b, Cell a, Cell t, int maxTurns)
    {
        var seen = new HashSet<(int, int, int, int)>();
        var q = new Queue<(Cell c, int d, int k)>();
        for (int d = 0; d < 4; d++) q.Enqueue((a, d, 0));
        while (q.Count > 0)
        {
            var (c, d, k) = q.Dequeue();
            var n = c + Cell.Directions[d];
            if (n == t) return true;
            if (!b.IsWalkable(n.X, n.Y)) continue;
            for (int nd = 0; nd < 4; nd++)
            {
                int nk = k + (nd == d ? 0 : 1);
                if (nk > maxTurns || (nd + 2) % 4 == d) continue;
                if (seen.Add((n.X, n.Y, nd, nk))) q.Enqueue((n, nd, nk));
            }
        }
        return false;
    }

    static Dictionary<int, int> Histogram(BoardModel b)
    {
        var h = new Dictionary<int, int>();
        foreach (var c in b.AllTiles()) { int t = b.Get(c); h[t] = h.TryGetValue(t, out int n) ? n + 1 : 1; }
        return h;
    }
    static bool Same(Dictionary<int, int> a, Dictionary<int, int> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a) if (!b.TryGetValue(kv.Key, out int v) || v != kv.Value) return false;
        return true;
    }
}
