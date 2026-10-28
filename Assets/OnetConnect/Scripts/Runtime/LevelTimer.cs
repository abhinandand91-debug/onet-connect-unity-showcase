using System;
using UnityEngine;

namespace Onet.Runtime
{
    /// <summary>Countdown timer; pauses with the game and supports +time boosters.</summary>
    public class LevelTimer : MonoBehaviour
    {
        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }
        public bool Running { get; private set; }
        public float Elapsed => TimeLimit - TimeLeft;

        public event Action<float, float> Tick;   // timeLeft, timeLimit
        public event Action Expired;

        public void Begin(float limit)
        {
            TimeLimit = limit;
            TimeLeft = limit;
            Running = true;
            Tick?.Invoke(TimeLeft, TimeLimit);
        }

        public void Pause() => Running = false;
        public void Resume() { if (TimeLeft > 0) Running = true; }
        public void Stop() => Running = false;

        /// <summary>Adds time; the bar may exceed the original limit visually capped at 1.</summary>
        public void AddTime(float seconds)
        {
            TimeLeft += seconds;
            if (TimeLeft > TimeLimit) TimeLimit = TimeLeft;
            Tick?.Invoke(TimeLeft, TimeLimit);
        }

        void Update()
        {
            if (!Running) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                Running = false;
                Tick?.Invoke(TimeLeft, TimeLimit);
                Expired?.Invoke();
                return;
            }
            Tick?.Invoke(TimeLeft, TimeLimit);
        }

        public static string Format(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
