using System;

namespace Onet.Core
{
    /// <summary>
    /// Heuristic 0–10 difficulty score used by the editor to sort / audit levels.
    /// Calibrate the weights against real funnel data (attempts-per-win) once live.
    ///
    ///   Size      0–3  : more pairs = longer search
    ///   Scarcity  0–2  : low average legal moves along the solution = few options
    ///   Variety   0–2  : types/pairs → 1 means each icon appears only twice
    ///   Twist     0–1.2: gravity modes reshuffle the player's mental map
    ///   Time      0–2  : seconds per pair below ~6 s starts to bite
    /// </summary>
    public static class DifficultyModel
    {
        public static float Score(int pairs, int tileTypes, float avgBranching, GravityMode gravity, float timeLimit)
        {
            if (pairs <= 0) return 0f;
            float size = Math.Min(3f, pairs / 60f * 3f);
            float scarcity = 2f * (1f - avgBranching / (avgBranching + 4f));
            float variety = 2f * Math.Min(1f, (float)tileTypes / pairs);
            float twist = gravity == GravityMode.None ? 0f
                        : gravity <= GravityMode.Right ? 0.8f : 1.2f;
            float secPerPair = timeLimit / pairs;
            float time = 2f * Clamp01((6f - secPerPair) / 4f);
            return (float)Math.Round(Math.Min(10f, size + scarcity + variety + twist + time), 1);
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public static string Label(float score) =>
            score < 3 ? "Easy" : score < 5 ? "Medium" : score < 7 ? "Hard" : "Super Hard";
    }
}
