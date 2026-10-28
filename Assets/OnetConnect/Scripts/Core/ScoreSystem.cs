using System;

namespace Onet.Core
{
    /// <summary>
    /// Score + combo + star rating. Pure logic, driven by the GameController.
    ///   Pair score   = BasePairScore × comboMultiplier (+ turn bonus for straight matches)
    ///   Combo        = consecutive matches each within ComboWindow seconds (max ×5)
    ///   Stars        = 1 for clearing; 2/3 based on fraction of time remaining
    /// </summary>
    public sealed class ScoreSystem
    {
        public int BasePairScore = 10;
        public float ComboWindow = 3f;
        public int MaxCombo = 5;
        public float TwoStarTimeFraction = 0.25f;
        public float ThreeStarTimeFraction = 0.5f;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        float _lastMatchTime = -999f;

        public event Action<int, int> ScoreChanged; // score, combo

        public void Reset() { Score = 0; Combo = 0; BestCombo = 0; _lastMatchTime = -999f; }

        /// <param name="turns">Corner count of the connecting path (0..2).</param>
        public int RegisterMatch(float time, int turns)
        {
            Combo = time - _lastMatchTime <= ComboWindow ? Math.Min(Combo + 1, MaxCombo) : 1;
            _lastMatchTime = time;
            BestCombo = Math.Max(BestCombo, Combo);
            int gained = BasePairScore * Combo + (turns == 0 ? 5 : 0);
            Score += gained;
            ScoreChanged?.Invoke(Score, Combo);
            return gained;
        }

        public int AddTimeBonus(float secondsLeft)
        {
            int bonus = (int)(secondsLeft * 5f);
            Score += bonus;
            ScoreChanged?.Invoke(Score, Combo);
            return bonus;
        }

        /// <summary>0..1 fill of the HUD star bar (1 = full time remaining).</summary>
        public static float StarBarFill(float timeLeft, float timeLimit) => timeLimit <= 0 ? 1f : Math.Max(0f, Math.Min(1f, timeLeft / timeLimit));

        public int StarsFor(float timeLeft, float timeLimit)
        {
            float f = StarBarFill(timeLeft, timeLimit);
            if (f >= ThreeStarTimeFraction) return 3;
            if (f >= TwoStarTimeFraction) return 2;
            return 1;
        }
    }
}
