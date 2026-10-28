using System;
using System.Collections.Generic;

namespace Onet.Core
{
    /// <summary>
    /// Re-deals the remaining tiles into the cells they currently occupy.
    /// Guarantees at least one legal move afterwards (when possible).
    /// Used by the Shuffle booster and by automatic deadlock recovery.
    /// </summary>
    public static class Shuffler
    {
        public static bool Shuffle(BoardModel board, Random rng, MoveFinder finder, int maxAttempts = 50)
        {
            var cells = new List<Cell>(board.AllTiles());
            if (cells.Count < 2) return false;
            var values = new int[cells.Count];
            for (int i = 0; i < cells.Count; i++) values[i] = board.Get(cells[i]);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                for (int i = values.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int tmp = values[i]; values[i] = values[j]; values[j] = tmp;
                }
                for (int i = 0; i < cells.Count; i++) board.Set(cells[i], values[i]);
                if (finder.HasAnyMove(board)) return true;
            }

            // Fallback: force a guaranteed move by placing a pair on two cells
            // that are mutually reachable (e.g. both touching the outer lane).
            return ForcePair(board, cells, finder);
        }

        static bool ForcePair(BoardModel board, List<Cell> cells, MoveFinder finder)
        {
            // Pick any pair of tile cells which would connect if they held the same type.
            for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                int vi = board.Get(cells[i]), vj = board.Get(cells[j]);
                // find a cell k (≠ i,j) holding vi to swap with cell j
                for (int k = 0; k < cells.Count; k++)
                {
                    if (k == i || k == j || board.Get(cells[k]) != vi) continue;
                    board.Set(cells[j], vi);
                    board.Set(cells[k], vj);
                    if (finder.PathFinder.CanConnect(board, cells[i], cells[j], finder.MaxTurns)) return true;
                    board.Set(cells[j], vj);
                    board.Set(cells[k], vi);
                    break;
                }
            }
            return false;
        }
    }
}
