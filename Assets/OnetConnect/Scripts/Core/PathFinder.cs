using System.Collections.Generic;

namespace Onet.Core
{
    /// <summary>
    /// Onet connection rule: two identical tiles connect if a path of at most
    /// (maxTurns + 1) straight segments joins them through empty cells.
    ///   maxTurns = 0 → straight line
    ///   maxTurns = 1 → L-shape
    ///   maxTurns = 2 → Z / U / C shapes (the classic rule: "max 3 lines")
    /// The outer lane ring (x = -1 / W, y = -1 / H) is always walkable.
    ///
    /// Algorithm: layered ray casting ("BFS by number of segments").
    ///   Layer 0: cast rays from A in 4 directions; every empty cell hit is
    ///            reachable with 1 segment.
    ///   Layer k: from every cell first reached in layer k-1, cast rays again;
    ///            newly reached cells need k+1 segments.
    /// Rays pass through already-visited cells (so straight continuation is
    /// never lost) but only unvisited cells are recorded. This gives the
    /// minimum-turn path in O(maxTurns · cells · (W+H)) with zero allocations
    /// after warm-up — fast enough to test every pair on a 12x20 board per frame.
    /// </summary>
    public sealed class PathFinder
    {
        int _w, _h;             // padded size
        int[] _parent;          // parent index (ray origin) per padded cell, -1 = unvisited
        int[] _stamp;           // visit stamp to avoid clearing arrays each query
        int _currentStamp;
        readonly List<int> _frontier = new List<int>(256);
        readonly List<int> _next = new List<int>(256);

        void Ensure(BoardModel board)
        {
            int w = board.Width + 2, h = board.Height + 2;
            if (_parent == null || w != _w || h != _h)
            {
                _w = w; _h = h;
                _parent = new int[w * h];
                _stamp = new int[w * h];
                _currentStamp = 0;
            }
            _currentStamp++;
            if (_currentStamp == int.MaxValue) { System.Array.Clear(_stamp, 0, _stamp.Length); _currentStamp = 1; }
        }

        int Idx(int x, int y) => (y + 1) * _w + (x + 1);
        Cell FromIdx(int i) => new Cell(i % _w - 1, i / _w - 1);

        /// <summary>Quick check without building the path.</summary>
        public bool CanConnect(BoardModel board, Cell a, Cell b, int maxTurns = 2)
            => FindPath(board, a, b, null, maxTurns);

        /// <summary>
        /// Finds a connecting path. If <paramref name="corners"/> is non-null it is
        /// filled with [A, corner1, (corner2), B] — ready for a LineRenderer.
        /// Tiles must be the same non-empty type and distinct cells.
        /// </summary>
        public bool FindPath(BoardModel board, Cell a, Cell b, List<Cell> corners, int maxTurns = 2)
        {
            corners?.Clear();
            if (a == b) return false;
            int type = board.Get(a);
            if (type <= 0 || type != board.Get(b)) return false;

            Ensure(board);
            int start = Idx(a.X, a.Y), goal = Idx(b.X, b.Y);
            Visit(start, -1);
            _frontier.Clear();
            _frontier.Add(start);

            for (int layer = 0; layer <= maxTurns && _frontier.Count > 0; layer++)
            {
                _next.Clear();
                foreach (int origin in _frontier)
                {
                    Cell o = FromIdx(origin);
                    for (int d = 0; d < 4; d++)
                    {
                        Cell dir = Cell.Directions[d];
                        int x = o.X + dir.X, y = o.Y + dir.Y;
                        while (board.InPaddedBounds(x, y))
                        {
                            int i = Idx(x, y);
                            if (i == goal)
                            {
                                if (!Visited(i)) Visit(i, origin);
                                if (corners != null) Build(goal, corners);
                                return true;
                            }
                            if (!board.IsWalkable(x, y)) break;
                            if (!Visited(i)) { Visit(i, origin); _next.Add(i); }
                            x += dir.X; y += dir.Y;
                        }
                    }
                }
                _frontier.Clear();
                _frontier.AddRange(_next);
            }
            return false;
        }

        bool Visited(int i) => _stamp[i] == _currentStamp;
        void Visit(int i, int parent) { _stamp[i] = _currentStamp; _parent[i] = parent; }

        void Build(int goal, List<Cell> corners)
        {
            for (int i = goal; i != -1; i = _parent[i]) corners.Add(FromIdx(i));
            corners.Reverse();
        }
    }
}
