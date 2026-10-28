using System.Collections.Generic;

namespace Onet.Core
{
    public struct Move
    {
        public Cell A;
        public Cell B;
        public Move(Cell a, Cell b) { A = a; B = b; }
        public override string ToString() => $"{A}<->{B}";
    }

    /// <summary>
    /// Enumerates legal matches. Used by Hint booster, deadlock detection
    /// (auto-shuffle), the solver and the editor difficulty analyzer.
    /// </summary>
    public sealed class MoveFinder
    {
        readonly PathFinder _path = new PathFinder();
        readonly Dictionary<int, List<Cell>> _byType = new Dictionary<int, List<Cell>>();
        readonly Stack<List<Cell>> _pool = new Stack<List<Cell>>();

        public int MaxTurns { get; set; } = 2;
        public PathFinder PathFinder => _path;

        void Group(BoardModel board)
        {
            foreach (var l in _byType.Values) { l.Clear(); _pool.Push(l); }
            _byType.Clear();
            foreach (var c in board.AllTiles())
            {
                int t = board.Get(c);
                if (!_byType.TryGetValue(t, out var list))
                {
                    list = _pool.Count > 0 ? _pool.Pop() : new List<Cell>(8);
                    _byType[t] = list;
                }
                list.Add(c);
            }
        }

        /// <summary>Returns the first legal move found, or null if the board is deadlocked.</summary>
        public Move? FindAny(BoardModel board)
        {
            Group(board);
            foreach (var list in _byType.Values)
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        if (_path.CanConnect(board, list[i], list[j], MaxTurns))
                            return new Move(list[i], list[j]);
            return null;
        }

        /// <summary>All legal moves (used by solver and difficulty metrics).</summary>
        public List<Move> FindAll(BoardModel board, List<Move> result = null)
        {
            if (result == null) result = new List<Move>();
            result.Clear();
            Group(board);
            foreach (var list in _byType.Values)
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        if (_path.CanConnect(board, list[i], list[j], MaxTurns))
                            result.Add(new Move(list[i], list[j]));
            return result;
        }

        public bool HasAnyMove(BoardModel board) => FindAny(board).HasValue;
    }
}
