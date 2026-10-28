using System;
using Onet.Core;
using UnityEngine;

namespace Onet.Data
{
    /// <summary>Metrics written by the editor validator (read-only at runtime).</summary>
    [Serializable]
    public struct LevelMetrics
    {
        public bool verified;
        public int initialMoves;
        public float averageBranching;
        public int forcedSteps;
        public int solverNodes;
        public float difficultyScore;   // 0 (trivial) .. 10 (brutal)
        public string validatedAt;
    }

    /// <summary>
    /// Authoring asset for a single level. Painted with the Level Editor window.
    /// Holds the shape, rules and (optionally) a baked deal so runtime loading
    /// is instant and every player sees the same board.
    /// </summary>
    [CreateAssetMenu(menuName = "Onet/Level", fileName = "Level_000")]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        public int levelNumber = 1;
        public TileTheme theme;

        [Header("Shape")]
        [Min(2)] public int width = 8;
        [Min(2)] public int height = 10;
        public LayoutCell[] layout = Array.Empty<LayoutCell>();
        [Tooltip("Row-major. 0 = random, >0 = pinned tile type")]
        public int[] fixedTypes = Array.Empty<int>();

        [Header("Rules")]
        [Min(1)] public int tileTypes = 12;
        public GravityMode gravity = GravityMode.None;
        [Range(0, 3)] public int maxTurns = 2;
        public bool requireSolvable = true;

        [Header("Timing & stars")]
        [Min(10)] public float timeLimit = 120f;
        [Range(0, 1)] public float twoStarFraction = 0.25f;
        [Range(0, 1)] public float threeStarFraction = 0.5f;

        [Header("Deal")]
        public int seed = 1;
        [Tooltip("Row-major tile types baked by the editor. Empty = generate at runtime.")]
        public int[] bakedTiles = Array.Empty<int>();
        [Tooltip("Retries deal a fresh random board instead of the baked one.")]
        public bool freshDealOnRetry = false;

        [Header("Rewards")]
        public int coinReward = 20;

        [Header("Validation (auto)")]
        public LevelMetrics metrics;

        public bool HasBakedDeal => bakedTiles != null && bakedTiles.Length == width * height;
        public int SlotCount { get { int n = 0; foreach (var c in layout) if (c == LayoutCell.Slot) n++; return n; } }

        public LevelSpec ToSpec(int? seedOverride = null) => new LevelSpec
        {
            Width = width,
            Height = height,
            Layout = (LayoutCell[])layout.Clone(),
            FixedTypes = fixedTypes != null && fixedTypes.Length == layout.Length ? (int[])fixedTypes.Clone() : null,
            TileTypes = tileTypes,
            Gravity = gravity,
            MaxTurns = maxTurns,
            Seed = seedOverride ?? seed,
            RequireSolvable = requireSolvable,
        };

        /// <summary>Creates the runtime board: baked deal if present, else generated.</summary>
        public BoardModel CreateBoard(bool forceFresh, out string error)
        {
            error = null;
            if (!forceFresh && HasBakedDeal)
            {
                var b = new BoardModel(width, height);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (layout[i] == LayoutCell.Blocked) b.Set(x, y, BoardModel.Blocked);
                        b.SetSlot(x, y, layout[i] == LayoutCell.Slot);
                        if (layout[i] == LayoutCell.Slot) b.Set(x, y, bakedTiles[i]);
                    }
                return b;
            }
            var spec = ToSpec(forceFresh ? UnityEngine.Random.Range(1, int.MaxValue / 2) : seed);
            var res = BoardGenerator.Generate(spec);
            error = res.Error;
            return res.Board;
        }

        public void EnsureArrays()
        {
            int n = width * height;
            if (layout == null || layout.Length != n) Resize(width, height);
            if (fixedTypes == null || fixedTypes.Length != n) fixedTypes = new int[n];
        }

        /// <summary>Resizes the grid keeping the top-left content.</summary>
        public void Resize(int newW, int newH)
        {
            var newLayout = new LayoutCell[newW * newH];
            var newFixed = new int[newW * newH];
            for (int y = 0; y < newH; y++)
                for (int x = 0; x < newW; x++)
                {
                    int ni = y * newW + x;
                    bool had = layout != null && x < width && y < height && y * width + x < layout.Length;
                    newLayout[ni] = had ? layout[y * width + x] : LayoutCell.Slot;
                    newFixed[ni] = had && fixedTypes != null && y * width + x < fixedTypes.Length ? fixedTypes[y * width + x] : 0;
                }
            width = newW; height = newH;
            layout = newLayout; fixedTypes = newFixed;
            bakedTiles = Array.Empty<int>();
        }

        void OnValidate()
        {
            if (threeStarFraction < twoStarFraction) threeStarFraction = twoStarFraction;
            if (theme != null && theme.IconCount > 0 && tileTypes > theme.IconCount)
                Debug.LogWarning($"{name}: tileTypes ({tileTypes}) exceeds theme icons ({theme.IconCount})", this);
        }
    }
}
