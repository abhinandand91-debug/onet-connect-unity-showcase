using System;

namespace Onet.Core
{
    /// <summary>Per-cell layout code painted in the level editor.</summary>
    public enum LayoutCell : byte
    {
        Hole = 0,     // not part of the board shape (walkable, never holds a tile)
        Slot = 1,     // receives a tile when dealt
        Blocked = 2,  // stone / wall: blocks paths, never holds a tile
    }

    /// <summary>
    /// Engine-agnostic description of one level. The Unity LevelData
    /// ScriptableObject converts itself into this for generation/validation,
    /// which keeps Core fully testable without the Editor.
    /// </summary>
    [Serializable]
    public sealed class LevelSpec
    {
        public int Width = 8;
        public int Height = 10;
        public LayoutCell[] Layout;       // row-major, Width*Height
        public int[] FixedTypes;          // optional row-major; 0 = random, >0 = pinned tile type
        public int TileTypes = 12;        // distinct icons used by this level
        public GravityMode Gravity = GravityMode.None;
        public int MaxTurns = 2;
        public int Seed = 1;
        public bool RequireSolvable = true;

        public LayoutCell At(int x, int y) => Layout[y * Width + x];
        public int FixedAt(int x, int y) => FixedTypes == null || FixedTypes.Length == 0 ? 0 : FixedTypes[y * Width + x];

        public static LevelSpec Rect(int w, int h, int types, GravityMode g = GravityMode.None, int seed = 1)
        {
            var s = new LevelSpec { Width = w, Height = h, TileTypes = types, Gravity = g, Seed = seed, Layout = new LayoutCell[w * h] };
            for (int i = 0; i < s.Layout.Length; i++) s.Layout[i] = LayoutCell.Slot;
            return s;
        }

        public int SlotCount()
        {
            int n = 0;
            foreach (var c in Layout) if (c == LayoutCell.Slot) n++;
            return n;
        }

        /// <summary>Returns null when valid, otherwise a human-readable problem.</summary>
        public string Validate()
        {
            if (Layout == null || Layout.Length != Width * Height) return "Layout size mismatch";
            if (FixedTypes != null && FixedTypes.Length != 0 && FixedTypes.Length != Layout.Length) return "FixedTypes size mismatch";
            int slots = SlotCount();
            if (slots == 0) return "No slots";
            if (slots % 2 != 0) return $"Odd slot count ({slots}) – every tile needs a partner";
            if (TileTypes < 1) return "TileTypes must be ≥ 1";
            if (MaxTurns < 0 || MaxTurns > 4) return "MaxTurns must be 0..4";
            return null;
        }
    }
}
