using System;
using System.Collections.Generic;

namespace Onet.Core
{
    public sealed class SolveResult
    {
        public bool Solved;
        public int NodesExpanded;
        public bool BudgetExceeded;
        public List<Move> Solution = new List<Move>();

        // Difficulty metrics sampled along the found solution
        public int InitialMoves;
        public float AverageBranching;
        public int MinBranching = int.MaxValue;
        public int ForcedSteps; // steps where only 1 pair (one move) was available
    }

    /// <summary>
    /// Depth-first solver with memoisation and a node budget.
    /// Move ordering heuristic: prefer pairs whose type has the fewest
    /// remaining tiles (clears "tight" types first), then shorter paths.
    /// Used in-editor to validate levels and in the generator to reject
    /// unsolvable deals (a shipped level should be clearable without shuffles).
    /// </summary>
    public sealed class Solver
    {
        readonly MoveFinder _finder = new MoveFinder();
        readonly HashSet<ulong> _dead = new HashSet<ulong>();
        readonly List<TileMove> _gravityScratch = new List<TileMove>();
        int _budget, _nodes;
        GravityMode _gravity;
        List<int> _branchSamples;

        public SolveResult Solve(BoardModel start, GravityMode gravity, int maxTurns = 2, int nodeBudget = 50000)
        {
            _finder.MaxTurns = maxTurns;
            _gravity = gravity;
            _budget = nodeBudget;
            _nodes = 0;
            _dead.Clear();
            _branchSamples = new List<int>();

            var result = new SolveResult();
            var board = start.Clone();
            var path = new List<Move>();
            result.InitialMoves = _finder.FindAll(board).Count;
            result.Solved = Dfs(board, path);
            result.NodesExpanded = _nodes;
            result.BudgetExceeded = !result.Solved && _nodes >= _budget;
            if (result.Solved)
            {
                result.Solution = path;
                int sum = 0;
                foreach (int b in _branchSamples) { sum += b; if (b < result.MinBranching) result.MinBranching = b; if (b == 1) result.ForcedSteps++; }
                result.AverageBranching = _branchSamples.Count > 0 ? (float)sum / _branchSamples.Count : 0f;
            }
            return result;
        }

        bool Dfs(BoardModel board, List<Move> path)
        {
            if (board.IsCleared) return true;
            if (++_nodes > _budget) return false;
            ulong h = board.StateHash();
            if (_dead.Contains(h)) return false;

            var moves = _finder.FindAll(board, new List<Move>());
            if (moves.Count == 0) { _dead.Add(h); return false; }
            Order(board, moves);

            foreach (var m in moves)
            {
                var next = board.Clone();
                next.RemovePair(m.A, m.B);
                Gravity.Apply(next, _gravity, _gravityScratch);
                path.Add(m);
                _branchSamples.Add(moves.Count);
                if (Dfs(next, path)) return true;
                path.RemoveAt(path.Count - 1);
                _branchSamples.RemoveAt(_branchSamples.Count - 1);
                if (_nodes > _budget) return false;
            }
            _dead.Add(h);
            return false;
        }

        readonly Dictionary<int, int> _typeCounts = new Dictionary<int, int>();

        void Order(BoardModel board, List<Move> moves)
        {
            _typeCounts.Clear();
            foreach (var c in board.AllTiles())
            {
                int t = board.Get(c);
                _typeCounts[t] = _typeCounts.TryGetValue(t, out int n) ? n + 1 : 1;
            }
            moves.Sort((x, y) =>
            {
                int cx = _typeCounts[board.Get(x.A)], cy = _typeCounts[board.Get(y.A)];
                if (cx != cy) return cx.CompareTo(cy);
                int dx = Math.Abs(x.A.X - x.B.X) + Math.Abs(x.A.Y - x.B.Y);
                int dy = Math.Abs(y.A.X - y.B.X) + Math.Abs(y.A.Y - y.B.Y);
                return dx.CompareTo(dy);
            });
        }
    }
}
