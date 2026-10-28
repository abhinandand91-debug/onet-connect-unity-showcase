using System;

namespace Onet.Core
{
    /// <summary>
    /// Integer grid coordinate. X = column (left→right), Y = row (top→bottom).
    /// Pure C# so the whole Core assembly can be unit-tested outside Unity.
    /// Playable cells are 0..W-1 / 0..H-1; the ring at -1 and W / H is the
    /// always-empty "outside lane" that connection paths may travel through.
    /// </summary>
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public int X;
        public int Y;

        public Cell(int x, int y) { X = x; Y = y; }

        public static readonly Cell Up    = new Cell(0, -1);
        public static readonly Cell Down  = new Cell(0, 1);
        public static readonly Cell Left  = new Cell(-1, 0);
        public static readonly Cell Right = new Cell(1, 0);
        public static readonly Cell[] Directions = { Up, Right, Down, Left };

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Cell a, Cell b) => !(a == b);

        public bool Equals(Cell other) => this == other;
        public override bool Equals(object obj) => obj is Cell c && c == this;
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => $"({X},{Y})";
    }
}
