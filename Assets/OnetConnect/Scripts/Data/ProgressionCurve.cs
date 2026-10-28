using System;
using System.Collections.Generic;
using Onet.Core;
using UnityEngine;

namespace Onet.Data
{
    /// <summary>
    /// Designer-tunable difficulty ramp used by the Batch Level Generator.
    /// X axis of every curve = level number (1..N).
    /// </summary>
    [CreateAssetMenu(menuName = "Onet/Progression Curve", fileName = "ProgressionCurve")]
    public class ProgressionCurve : ScriptableObject
    {
        [Header("Board size (cells)")]
        // Portrait phone defaults (≈ reference screenshots: Lv.10 ≈ 8×10)
        public AnimationCurve width  = new AnimationCurve(new Keyframe(1, 4), new Keyframe(5, 6), new Keyframe(10, 7), new Keyframe(30, 8), new Keyframe(60, 8));
        public AnimationCurve height = new AnimationCurve(new Keyframe(1, 4), new Keyframe(5, 6), new Keyframe(10, 9), new Keyframe(30, 11), new Keyframe(60, 12));
        public int maxWidth = 9, maxHeight = 14;

        [Header("Variety")]
        [Tooltip("Distinct tile types. More types = fewer duplicates = harder to spot")]
        public AnimationCurve tileTypes = new AnimationCurve(new Keyframe(1, 4), new Keyframe(10, 10), new Keyframe(20, 16), new Keyframe(40, 24), new Keyframe(60, 32));

        [Header("Time")]
        [Tooltip("Seconds granted per pair on the board")]
        public AnimationCurve secondsPerPair = new AnimationCurve(new Keyframe(1, 8f), new Keyframe(10, 5f), new Keyframe(30, 3.6f), new Keyframe(60, 2.8f));
        public float minTime = 30f;

        [Header("Twists")]
        public List<GravityUnlock> gravityUnlocks = new List<GravityUnlock>
        {
            new GravityUnlock { fromLevel = 11, mode = GravityMode.Down },
            new GravityUnlock { fromLevel = 16, mode = GravityMode.Left },
            new GravityUnlock { fromLevel = 21, mode = GravityMode.Up },
            new GravityUnlock { fromLevel = 26, mode = GravityMode.Right },
            new GravityUnlock { fromLevel = 31, mode = GravityMode.CollapseToCenterX },
            new GravityUnlock { fromLevel = 36, mode = GravityMode.CollapseToCenterY },
            new GravityUnlock { fromLevel = 41, mode = GravityMode.SplitFromCenterX },
            new GravityUnlock { fromLevel = 46, mode = GravityMode.SplitFromCenterY },
        };
        [Tooltip("Every Nth level is a breather: gravity off, +25% time")]
        public int breatherEvery = 5;

        [Tooltip("Probability a level gets blocked stones once unlocked")]
        public int stonesFromLevel = 25;
        [Range(0, 0.2f)] public float stoneDensity = 0.06f;

        [Tooltip("Shape presets unlocked over time (Rect always available)")]
        public int shapesFromLevel = 8;

        [Serializable]
        public struct GravityUnlock
        {
            public int fromLevel;
            public GravityMode mode;
        }

        public int WidthAt(int level)  => Mathf.Clamp(Mathf.RoundToInt(width.Evaluate(level)), 2, maxWidth);
        public int HeightAt(int level) => Mathf.Clamp(Mathf.RoundToInt(height.Evaluate(level)), 2, maxHeight);
        public int TypesAt(int level)  => Mathf.Max(2, Mathf.RoundToInt(tileTypes.Evaluate(level)));

        public bool IsBreather(int level) => breatherEvery > 0 && level % breatherEvery == 0;

        /// <summary>Newest unlocked twist is introduced first; later levels mix older ones.</summary>
        public GravityMode GravityAt(int level, System.Random rng)
        {
            if (IsBreather(level)) return GravityMode.None;
            var unlocked = new List<GravityMode> { GravityMode.None };
            GravityUnlock? newest = null;
            foreach (var g in gravityUnlocks)
                if (level >= g.fromLevel) { unlocked.Add(g.mode); newest = g; }
            // Introduction window: the 3 levels right after an unlock showcase it
            if (newest.HasValue && level - newest.Value.fromLevel < 3) return newest.Value.mode;
            return unlocked[rng.Next(unlocked.Count)];
        }

        public float TimeFor(int level, int pairs)
        {
            float t = pairs * secondsPerPair.Evaluate(level);
            if (IsBreather(level)) t *= 1.25f;
            return Mathf.Max(minTime, Mathf.Round(t / 5f) * 5f);
        }
    }
}
