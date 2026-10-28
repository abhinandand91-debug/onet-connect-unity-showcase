using System;

namespace Onet.Core
{
    public enum ShapePreset { Rect, Diamond, Frame, Hourglass, Cross, Ring, Checker, Pillars, RandomSymmetric }

    /// <summary>
    /// Procedural board shapes used by the Level Editor presets and the batch
    /// generator. All results are left/right mirror-symmetric (looks designed)
    /// and parity-fixed so the slot count is always even.
    /// </summary>
    public static class ShapeLibrary
    {
        public static LayoutCell[] Create(ShapePreset preset, int w, int h, Random rng = null)
        {
            rng = rng ?? new Random(1);
            var L = new LayoutCell[w * h];
            float cx = (w - 1) / 2f, cy = (h - 1) / 2f;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Math.Abs(x - cx) / Math.Max(1f, cx), dy = Math.Abs(y - cy) / Math.Max(1f, cy);
                    bool slot;
                    switch (preset)
                    {
                        case ShapePreset.Diamond:   slot = dx + dy <= 1.05f; break;
                        case ShapePreset.Frame:     slot = x < 2 || y < 2 || x >= w - 2 || y >= h - 2; break;
                        case ShapePreset.Hourglass: slot = dx <= dy + 0.2f; break;
                        case ShapePreset.Cross:     slot = dx <= 0.4f || dy <= 0.4f; break;
                        case ShapePreset.Ring:      slot = !(dx < 0.4f && dy < 0.4f); break;
                        case ShapePreset.Checker:   slot = !((x % 3 == 1) && (y % 3 == 1)); break;
                        case ShapePreset.Pillars:   slot = true; break;
                        default:                    slot = true; break;
                    }
                    L[y * w + x] = slot ? LayoutCell.Slot : LayoutCell.Hole;
                }

            if (preset == ShapePreset.Pillars)
                for (int y = 1; y < h - 1; y += 3)
                    for (int x = 1; x < w - 1; x += 3)
                        L[y * w + x] = LayoutCell.Blocked;

            if (preset == ShapePreset.RandomSymmetric)
            {
                // Carve symmetric holes (≈15%) keeping the outer ring solid
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x <= (w - 1) / 2; x++)
                        if (rng.NextDouble() < 0.15)
                        {
                            L[y * w + x] = LayoutCell.Hole;
                            L[y * w + (w - 1 - x)] = LayoutCell.Hole;
                        }
            }

            FixParity(L, w, h);
            return L;
        }

        /// <summary>Scatter blocked stones symmetrically over existing slots.</summary>
        public static void AddStones(LayoutCell[] L, int w, int h, float density, Random rng)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x <= (w - 1) / 2; x++)
                {
                    int i = y * w + x, j = y * w + (w - 1 - x);
                    if (L[i] == LayoutCell.Slot && L[j] == LayoutCell.Slot && rng.NextDouble() < density)
                    { L[i] = LayoutCell.Blocked; L[j] = LayoutCell.Blocked; }
                }
            FixParity(L, w, h);
        }

        /// <summary>If the slot count is odd, turn the slot closest to the centre into a hole.</summary>
        public static void FixParity(LayoutCell[] L, int w, int h)
        {
            int count = 0;
            foreach (var c in L) if (c == LayoutCell.Slot) count++;
            if (count % 2 == 0) return;
            float cx = (w - 1) / 2f, cy = (h - 1) / 2f;
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < L.Length; i++)
            {
                if (L[i] != LayoutCell.Slot) continue;
                float d = Math.Abs(i % w - cx) + Math.Abs(i / w - cy);
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) L[best] = LayoutCell.Hole;
        }
    }
}
