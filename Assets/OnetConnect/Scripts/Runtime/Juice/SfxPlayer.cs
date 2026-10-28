using System.Collections.Generic;
using UnityEngine;

namespace Onet.Runtime.Juice
{
    public enum Sfx { Select, Match, Reject, Win, Lose, Shuffle, Hint, Click, Combo }

    /// <summary>
    /// Procedural sound effects so the game has audio before real assets exist.
    /// Every clip is synthesised once at startup (short sine/triangle blips and arpeggios);
    /// swap in recorded clips via <see cref="Override"/> without touching any caller.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        const int Rate = 44100;

        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        AudioSource _src;

        public bool Muted { get; set; }

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _clips[Sfx.Select]  = Tone("select", 0.07f, 660f, 880f);
            _clips[Sfx.Match]   = Arp("match", 0.07f, 523.25f, 659.25f, 783.99f);
            _clips[Sfx.Combo]   = Arp("combo", 0.06f, 783.99f, 987.77f, 1174.66f, 1567.98f);
            _clips[Sfx.Reject]  = Tone("reject", 0.16f, 220f, 150f);
            _clips[Sfx.Win]     = Arp("win", 0.12f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f);
            _clips[Sfx.Lose]    = Arp("lose", 0.18f, 392f, 349.23f, 311.13f, 261.63f);
            _clips[Sfx.Shuffle] = Sweep("shuffle", 0.4f, 300f, 900f);
            _clips[Sfx.Hint]    = Arp("hint", 0.08f, 880f, 1108.73f);
            _clips[Sfx.Click]   = Tone("click", 0.04f, 500f, 500f);
        }

        public void Override(Sfx sfx, AudioClip clip) => _clips[sfx] = clip;

        public void Play(Sfx sfx, float pitch = 1f)
        {
            if (Muted || !_clips.TryGetValue(sfx, out var clip)) return;
            _src.pitch = pitch;
            _src.PlayOneShot(clip, volume);
        }

        /// <summary>Combo 1..5 maps to a rising pitch so streaks sound rewarding.</summary>
        public void PlayMatch(int combo) => Play(combo > 1 ? Sfx.Combo : Sfx.Match, 1f + 0.06f * Mathf.Clamp(combo - 1, 0, 4));

        // ------------------------------------------------------------------ synthesis

        static AudioClip Tone(string name, float seconds, float f0, float f1) => Build(name, seconds, (t, k) =>
            Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(f0, f1, k) * t) * Env(k));

        static AudioClip Sweep(string name, float seconds, float f0, float f1) => Build(name, seconds, (t, k) =>
        {
            float f = Mathf.Lerp(f0, f1, k * k);
            return (Mathf.Sin(2 * Mathf.PI * f * t) * 0.6f + (Mathf.PerlinNoise(t * 400f, 0f) - 0.5f) * 0.5f) * Env(k) * 0.8f;
        });

        static AudioClip Arp(string name, float step, params float[] notes)
        {
            float total = step * notes.Length + 0.12f;
            return Build(name, total, (t, k) =>
            {
                int i = Mathf.Min(notes.Length - 1, (int)(t / step));
                float local = (t - i * step) / (step + 0.12f);
                float f = notes[i];
                float s = Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4 * Mathf.PI * f * t);
                return s * 0.6f * Mathf.Exp(-3f * Mathf.Clamp01(local));
            });
        }

        static float Env(float k) => Mathf.Min(1f, k * 20f) * (1f - k) * (1f - k);

        delegate float Gen(float t, float k);

        static AudioClip Build(string name, float seconds, Gen gen)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(gen(i / (float)Rate, i / (float)n), -1f, 1f) * 0.5f;
            var clip = AudioClip.Create("sfx_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
