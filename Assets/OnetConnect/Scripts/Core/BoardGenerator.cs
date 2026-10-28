using System;
using System.Collections.Generic;

namespace Onet.Core
{
    public sealed class GenerationResult
    {
        public BoardModel Board;
        public int SeedUsed;
        public int Attempts;
        public bool Verified;       // proven clearable without shuffles
        public string Error;
    }

    /// <summary>
    /// Deals tiles into a level shape.
    ///
    /// Strategy – Deal &amp; verify:
    ///   1. Build the shape (slots / stones / pinned tiles).
    ///   2. Build a balanced pair pool (pairs spread evenly over TileTypes).
    ///   3. Random deal with a seeded RNG.
    ///   4. The DFS Solver proves the deal is clearable WITHOUT shuffles
    ///      (respecting the level's gravity); otherwise retry with next seed.
    ///   Random Onet deals are solvable the vast majority of the time, so this
    ///   usually succeeds on attempt 1 (measured: &lt;20 ms for 8x12 boards).
    ///
    /// Same spec + same seed → same board, so levels are deterministic and
    /// reproducible for QA without storing the full board.
    /// </summary>
    public static class BoardGenerator
    {
        public static GenerationResult Generate(LevelSpec spec, int maxAttempts = 40, int solverBudget = 30000)
        {
            var res = new GenerationResult();
            string err = spec.Validate();
            if (err != null) { res.Error = err; return res; }

            var finder = new MoveFinder { MaxTurns = spec.MaxTurns };
            var solver = new Solver();

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = spec.Seed + attempt * 7919;
                var rng = new Random(seed);
                BoardModel board = CreateShape(spec);
                List<int> pool = BuildTilePool(spec, board, rng, out string poolErr);
                if (poolErr != null) { res.Error = poolErr; return res; }

                RandomDeal(board, pool, rng);
                bool ok = finder.HasAnyMove(board);
                res.Verified = false;
                if (ok && spec.RequireSolvable)
                {
                    var s = solver.Solve(board, spec.Gravity, spec.MaxTurns, solverBudget);
                    ok = s.Solved;
                    res.Verified = s.Solved;
                }

                res.Attempts = attempt + 1;
                if (ok || !spec.RequireSolvable)
                {
                    res.Board = board;
                    res.SeedUsed = seed;
                    return res;
                }
            }
            res.Error = $"No solvable deal in {maxAttempts} attempts – reduce size, add types, or change gravity.";
            return res;
        }

        static BoardModel CreateShape(LevelSpec spec)
        {
            var b = new BoardModel(spec.Width, spec.Height);
            for (int y = 0; y < spec.Height; y++)
                for (int x = 0; x < spec.Width; x++)
                {
                    var c = spec.At(x, y);
                    if (c == LayoutCell.Blocked) b.Set(x, y, BoardModel.Blocked);
                    b.SetSlot(x, y, c == LayoutCell.Slot);
                    int f = spec.FixedAt(x, y);
                    if (c == LayoutCell.Slot && f > 0) b.Set(x, y, f);
                }
            return b;
        }

        /// <summary>Builds the list of tile types for the empty slots (pairs), balancing pinned tiles.</summary>
        static List<int> BuildTilePool(LevelSpec spec, BoardModel board, Random rng, out string error)
        {
            error = null;
            var pool = new List<int>();
            var fixedCount = new Dictionary<int, int>();
            int freeSlots = 0;
            foreach (var c in board.AllSlots())
            {
                int v = board.Get(c);
                if (v > 0) fixedCount[v] = fixedCount.TryGetValue(v, out int n) ? n + 1 : 1;
                else freeSlots++;
            }
            // Partner for odd pinned counts
            foreach (var kv in fixedCount) if (kv.Value % 2 == 1) pool.Add(kv.Key);
            if (pool.Count > freeSlots || (freeSlots - pool.Count) % 2 != 0)
            {
                error = "Pinned tiles cannot be paired with remaining free slots";
                return null;
            }

            int pairs = (freeSlots - pool.Count) / 2;
            // Distribute pairs across types as evenly as possible, random type order
            var types = new List<int>();
            for (int t = 1; t <= spec.TileTypes; t++) types.Add(t);
            ShuffleList(types, rng);
            for (int p = 0; p < pairs; p++)
            {
                int t = types[p % types.Count];
                pool.Add(t); pool.Add(t);
            }
            return pool;
        }

        static void RandomDeal(BoardModel board, List<int> pool, Random rng)
        {
            var free = new List<Cell>();
            foreach (var c in board.AllSlots()) if (board.Get(c) == BoardModel.Empty) free.Add(c);
            ShuffleList(pool, rng);
            for (int i = 0; i < free.Count && i < pool.Count; i++) board.Set(free[i], pool[i]);
        }

        static void ShuffleList<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
