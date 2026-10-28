using System;
using System.Collections.Generic;

namespace Onet.Core
{
    /// <summary>
    /// Pure data model of an Onet board.
    /// Internally stored with a 1-cell padding ring so that paths can route
    /// around the outside of the board (classic Onet rule).
    ///
    /// Cell values:
    ///   Empty   (0)  – walkable, no tile
    ///   Blocked (-1) – wall / stone obstacle: never walkable, never holds a tile
    ///   1..N         – tile type id
    ///
    /// Slots: cells that are part of the level shape. Gravity only moves tiles
    /// between slots. Non-slot cells are permanent holes (walkable, empty).
    /// </summary>
    public sealed class BoardModel
    {
        public const int Empty = 0;
        public const int Blocked = -1;

        public int Width { get; }
        public int Height { get; }

        readonly int[] _cells;   // (W+2)*(H+2)
        readonly bool[] _slots;  // (W+2)*(H+2)
        int _tileCount;

        public int TileCount => _tileCount;
        public bool IsCleared => _tileCount == 0;

        /// <summary>Raised whenever a tile value changes (index-safe, playable coords).</summary>
        public event Action<Cell, int> CellChanged;

        public BoardModel(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Board size must be positive");
            Width = width;
            Height = height;
            _cells = new int[(width + 2) * (height + 2)];
            _slots = new bool[(width + 2) * (height + 2)];
        }

        int Index(int x, int y) => (y + 1) * (Width + 2) + (x + 1);

        /// <summary>True for playable cells plus the outer lane ring.</summary>
        public bool InPaddedBounds(int x, int y) => x >= -1 && y >= -1 && x <= Width && y <= Height;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Cell c) => InBounds(c.X, c.Y);

        public int Get(int x, int y) => InPaddedBounds(x, y) ? _cells[Index(x, y)] : Blocked;
        public int Get(Cell c) => Get(c.X, c.Y);

        public bool IsSlot(int x, int y) => InBounds(x, y) && _slots[Index(x, y)];
        public bool IsSlot(Cell c) => IsSlot(c.X, c.Y);

        public bool IsTile(Cell c) => Get(c) > 0;
        public bool IsWalkable(int x, int y) => InPaddedBounds(x, y) && _cells[Index(x, y)] == Empty;

        public void SetSlot(int x, int y, bool isSlot)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException();
            _slots[Index(x, y)] = isSlot;
        }

        public void Set(Cell c, int value) => Set(c.X, c.Y, value);

        public void Set(int x, int y, int value)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException($"({x},{y}) outside playable area");
            int i = Index(x, y);
            int old = _cells[i];
            if (old == value) return;
            if (old > 0) _tileCount--;
            if (value > 0) _tileCount++;
            _cells[i] = value;
            CellChanged?.Invoke(new Cell(x, y), value);
        }

        /// <summary>Removes a matched pair. Returns false if they aren't the same tile type.</summary>
        public bool RemovePair(Cell a, Cell b)
        {
            int ta = Get(a);
            if (ta <= 0 || ta != Get(b) || a == b) return false;
            Set(a, Empty);
            Set(b, Empty);
            return true;
        }

        public IEnumerable<Cell> AllTiles()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_cells[Index(x, y)] > 0) yield return new Cell(x, y);
        }

        public IEnumerable<Cell> AllSlots()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_slots[Index(x, y)]) yield return new Cell(x, y);
        }

        public BoardModel Clone()
        {
            var b = new BoardModel(Width, Height);
            Array.Copy(_cells, b._cells, _cells.Length);
            Array.Copy(_slots, b._slots, _slots.Length);
            b._tileCount = _tileCount;
            return b;
        }

        /// <summary>Fast state hash for solver memoisation.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < _cells.Length; i++)
            {
                h ^= (ulong)(_cells[i] + 2);
                h *= 1099511628211UL;
            }
            return h;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int v = Get(x, y);
                    sb.Append(v == Blocked ? " ##" : v == Empty ? (IsSlot(x, y) ? "  ." : "   ") : v.ToString().PadLeft(3));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
