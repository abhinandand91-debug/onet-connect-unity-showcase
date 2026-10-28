// Simulates the Batch Level Generator for levels 1..60 outside Unity (piecewise-linear curves)
// and prints a CSV used for the design-doc difficulty chart.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Onet.Core;

static class ProgressionSim
{
    static float Curve(float x, params float[] kv)
    {
        for (int i = 0; i < kv.Length - 2; i += 2)
            if (x <= kv[i + 2]) { float t = (x - kv[i]) / (kv[i + 2] - kv[i]); return kv[i + 1] + (kv[i + 3] - kv[i + 1]) * Math.Max(0, t); }
        return kv[kv.Length - 1];
    }

    static void Main()
    {
        var unlocks = new (int lvl, GravityMode m)[] { (11, GravityMode.Down), (16, GravityMode.Left), (21, GravityMode.Up), (26, GravityMode.Right),
            (31, GravityMode.CollapseToCenterX), (36, GravityMode.CollapseToCenterY), (41, GravityMode.SplitFromCenterX), (46, GravityMode.SplitFromCenterY) };
        var shapes = (ShapePreset[])Enum.GetValues(typeof(ShapePreset));
        Console.WriteLine("level,w,h,shape,gravity,pairs,types,time,sec_per_pair,verified,attempts,initial_moves,avg_branch,difficulty,gen_ms");
        var solver = new Solver();
        for (int n = 1; n <= 60; n++)
        {
            var rng = new Random(n * 92821);
            bool breather = n % 5 == 0;
            int w = (int)Math.Round(Curve(n, 1, 4, 5, 6, 10, 7, 30, 8, 60, 8));
            int h = (int)Math.Round(Curve(n, 1, 4, 5, 6, 10, 9, 30, 11, 60, 12));
            var shape = ShapePreset.Rect;
            if (n >= 8 && !breather) { shape = shapes[rng.Next(shapes.Length)]; if (shape != ShapePreset.Rect) { w = Math.Min(9, w + 1); h = Math.Min(14, h + 1); } }
            var layout = ShapeLibrary.Create(shape, w, h, rng);
            if (n >= 25 && !breather && shape != ShapePreset.Pillars) ShapeLibrary.AddStones(layout, w, h, 0.06f, rng);

            GravityMode g = GravityMode.None;
            if (!breather)
            {
                var unlocked = new List<GravityMode> { GravityMode.None };
                (int lvl, GravityMode m)? newest = null;
                foreach (var u in unlocks) if (n >= u.lvl) { unlocked.Add(u.m); newest = u; }
                g = newest.HasValue && n - newest.Value.lvl < 3 ? newest.Value.m : unlocked[rng.Next(unlocked.Count)];
            }
            var spec = new LevelSpec { Width = w, Height = h, Layout = layout, Gravity = g, Seed = n * 1000 + 1 };
            int pairs = spec.SlotCount() / 2;
            spec.TileTypes = Math.Max(2, Math.Min(pairs / 2, (int)Math.Round(Curve(n, 1, 4, 10, 10, 20, 16, 40, 24, 60, 32))));
            float time = pairs * Curve(n, 1, 8, 10, 5, 30, 3.6f, 60, 2.8f);
            if (breather) time *= 1.25f;
            time = Math.Max(30f, (float)Math.Round(time / 5f) * 5f);

            var sw = Stopwatch.StartNew();
            var res = BoardGenerator.Generate(spec);
            long ms = sw.ElapsedMilliseconds;
            if (res.Board == null) { Console.WriteLine($"{n},{w},{h},{shape},{g},{pairs},{spec.TileTypes},{time},,False,{res.Attempts},,,,{ms}"); continue; }
            var s = solver.Solve(res.Board, g, 2, 100000);
            float diff = DifficultyModel.Score(pairs, spec.TileTypes, s.AverageBranching, g, time);
            Console.WriteLine($"{n},{w},{h},{shape},{g},{pairs},{spec.TileTypes},{time},{time / pairs:F2},{s.Solved},{res.Attempts},{s.InitialMoves},{s.AverageBranching:F1},{diff},{ms}");
        }
    }
}
