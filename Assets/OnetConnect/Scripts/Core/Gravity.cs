using System.Collections.Generic;

namespace Onet.Core
{
    /// <summary>
    /// Post-match board shift rules (the "Pikachu/Onet" level twists).
    /// Tiles only slide between slot cells; Blocked cells split a line into
    /// independent segments; non-slot holes are skipped.
    /// </summary>
    public enum GravityMode
    {
        None = 0,
        Down,
        Up,
        Left,
        Right,
        CollapseToCenterX,  // left half slides right, right half slides left
        CollapseToCenterY,  // top half slides down, bottom half slides up
        SplitFromCenterX,   // left half slides left, right half slides right
        SplitFromCenterY,   // top half slides up, bottom half slides down
    }

    public struct TileMove
    {
        public Cell From;
        public Cell To;
        public TileMove(Cell f, Cell t) { From = f; To = t; }
        public override string ToString() => $"{From}->{To}";
    }

    public static class Gravity
    {
        /// <summary>Applies gravity and returns the list of tile moves (for animation).</summary>
        public static List<TileMove> Apply(BoardModel board, GravityMode mode, List<TileMove> moves = null)
        {
            if (moves == null) moves = new List<TileMove>();
            moves.Clear();
            if (mode == GravityMode.None) return moves;

            int w = board.Width, h = board.Height;
            var line = new List<Cell>(System.Math.Max(w, h));

            switch (mode)
            {
                case GravityMode.Down:
                    for (int x = 0; x < w; x++) { line.Clear(); for (int y = h - 1; y >= 0; y--) line.Add(new Cell(x, y)); Compact(board, line, moves); }
                    break;
                case GravityMode.Up:
                    for (int x = 0; x < w; x++) { line.Clear(); for (int y = 0; y < h; y++) line.Add(new Cell(x, y)); Compact(board, line, moves); }
                    break;
                case GravityMode.Left:
                    for (int y = 0; y < h; y++) { line.Clear(); for (int x = 0; x < w; x++) line.Add(new Cell(x, y)); Compact(board, line, moves); }
                    break;
                case GravityMode.Right:
                    for (int y = 0; y < h; y++) { line.Clear(); for (int x = w - 1; x >= 0; x--) line.Add(new Cell(x, y)); Compact(board, line, moves); }
                    break;

                case GravityMode.CollapseToCenterX:
                case GravityMode.SplitFromCenterX:
                {
                    bool toCenter = mode == GravityMode.CollapseToCenterX;
                    int half = w / 2;
                    for (int y = 0; y < h; y++)
                    {
                        // left half
                        line.Clear();
                        if (toCenter) for (int x = half - 1; x >= 0; x--) line.Add(new Cell(x, y));
                        else for (int x = 0; x < half; x++) line.Add(new Cell(x, y));
                        Compact(board, line, moves);
                        // right half
                        line.Clear();
                        if (toCenter) for (int x = half; x < w; x++) line.Add(new Cell(x, y));
                        else for (int x = w - 1; x >= half; x--) line.Add(new Cell(x, y));
                        Compact(board, line, moves);
                    }
                    break;
                }
                case GravityMode.CollapseToCenterY:
                case GravityMode.SplitFromCenterY:
                {
                    bool toCenter = mode == GravityMode.CollapseToCenterY;
                    int half = h / 2;
                    for (int x = 0; x < w; x++)
                    {
                        line.Clear();
                        if (toCenter) for (int y = half - 1; y >= 0; y--) line.Add(new Cell(x, y));
                        else for (int y = 0; y < half; y++) line.Add(new Cell(x, y));
                        Compact(board, line, moves);
                        line.Clear();
                        if (toCenter) for (int y = half; y < h; y++) line.Add(new Cell(x, y));
                        else for (int y = h - 1; y >= half; y--) line.Add(new Cell(x, y));
                        Compact(board, line, moves);
                    }
                    break;
                }
            }
            return moves;
        }

        /// <summary>
        /// Compacts tiles toward line[0]. Blocked cells break the line into
        /// segments; non-slot cells are ignored entirely.
        /// </summary>
        static void Compact(BoardModel board, List<Cell> line, List<TileMove> moves)
        {
            int write = 0; // index into line of next free slot within current segment
            for (int read = 0; read < line.Count; read++)
            {
                Cell c = line[read];
                int v = board.Get(c);
                if (v == BoardModel.Blocked) { write = read + 1; continue; }
                if (!board.IsSlot(c)) continue;
                if (v > 0)
                {
                    // find next slot position at/after write
                    while (write < read && !board.IsSlot(line[write])) write++;
                    if (write < read)
                    {
                        Cell dst = line[write];
                        board.Set(dst, v);
                        board.Set(c, BoardModel.Empty);
                        moves.Add(new TileMove(c, dst));
                    }
                    write = (write < read ? write : read) + 1;
                }
            }
        }
    }
}
