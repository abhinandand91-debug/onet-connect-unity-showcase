using System.Collections;
using System.Collections.Generic;
using Onet.Core;
using Onet.Data;
using TMPro;
using UnityEngine;

namespace Onet.Runtime.Juice
{
    /// <summary>
    /// Listens to <see cref="GameController"/> events and plays presentation-only feedback:
    /// particle bursts, procedural SFX, floating score text and a camera nudge on big combos.
    /// Nothing here reads or mutates rules, so it can be removed without affecting gameplay.
    /// </summary>
    public class FxDirector : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] BoardView board;
        [SerializeField] SfxPlayer sfx;
        [SerializeField] Camera cam;

        [Header("Particle prefabs")]
        [SerializeField] GameObject matchFx;
        [SerializeField] GameObject[] comboFx;      // index = combo-2 (x2..x5)
        [SerializeField] GameObject shuffleFx;
        [SerializeField] GameObject hintFx;
        [SerializeField] GameObject winFx;
        [SerializeField] GameObject loseFx;

        [Header("Tuning")]
        [SerializeField] int particleSortingOrder = 100;
        [SerializeField] float fxLifetime = 2.5f;
        [SerializeField] float floatTextRise = 1.2f;
        [SerializeField] TMP_FontAsset floatFont;

        readonly Dictionary<GameObject, Stack<GameObject>> _pools = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Stack<TextMeshPro> _textPool = new Stack<TextMeshPro>();
        Vector3 _camHome;

        void Awake() { if (cam == null) cam = Camera.main; }

        void OnEnable()
        {
            if (cam != null) _camHome = cam.transform.position;
            game.TileSelected += OnSelected;
            game.TileRejected += OnRejected;
            game.MatchResolved += OnMatch;
            game.PairMatched += OnPair;
            game.Shuffled += OnShuffled;
            game.HintShown += OnHint;
            game.LevelWon += OnWon;
            game.LevelLost += OnLost;
        }

        void OnDisable()
        {
            game.TileSelected -= OnSelected;
            game.TileRejected -= OnRejected;
            game.MatchResolved -= OnMatch;
            game.PairMatched -= OnPair;
            game.Shuffled -= OnShuffled;
            game.HintShown -= OnHint;
            game.LevelWon -= OnWon;
            game.LevelLost -= OnLost;
        }

        // ------------------------------------------------------------------ handlers

        int _lastCombo;
        Vector3 _lastMid;

        // Taps are sound-only: particles are reserved for correct matches.
        void OnSelected(Cell c) => sfx.Play(Sfx.Select);
        void OnRejected(Cell c) => sfx.Play(Sfx.Reject);

        void OnMatch(Vector3 a, Vector3 b, int combo, int turns)
        {
            _lastCombo = combo;
            _lastMid = (a + b) * 0.5f;
            sfx.PlayMatch(combo);
            Spawn(matchFx, a, 0.8f);
            Spawn(matchFx, b, 0.8f);
            if (combo >= 2 && comboFx != null && comboFx.Length > 0)
                Spawn(comboFx[Mathf.Clamp(combo - 2, 0, comboFx.Length - 1)], _lastMid, 1f + 0.15f * combo);
            if (combo >= 4) StartCoroutine(CameraNudge(0.05f * combo, 0.18f));
        }

        void OnPair(int gained) => FloatText($"+{gained}", _lastMid, _lastCombo);

        void OnShuffled() { sfx.Play(Sfx.Shuffle); Spawn(shuffleFx, board.transform.position, 1.6f); }
        void OnHint(Cell a, Cell b) { sfx.Play(Sfx.Hint); Spawn(hintFx, board.CellToWorld(a), 0.6f); Spawn(hintFx, board.CellToWorld(b), 0.6f); }

        void OnWon(int stars, int score, int coins)
        {
            sfx.Play(Sfx.Win);
            var p = board.transform.position;
            Spawn(winFx, p, 2f);
            for (int i = 0; i < stars; i++) StartCoroutine(DelayedSpawn(winFx, p + new Vector3((i - 1) * 2.5f, 3f, 0), 1.2f, 0.25f * (i + 1)));
        }

        void OnLost() { sfx.Play(Sfx.Lose); Spawn(loseFx, board.transform.position, 1.5f); }

        // ------------------------------------------------------------------ spawning

        IEnumerator DelayedSpawn(GameObject prefab, Vector3 pos, float scale, float delay)
        {
            yield return new WaitForSeconds(delay);
            Spawn(prefab, pos, scale);
        }

        void Spawn(GameObject prefab, Vector3 world, float cellScale)
        {
            if (prefab == null) return;
            var go = Rent(prefab);
            go.transform.position = new Vector3(world.x, world.y, 0f);
            go.transform.localScale = Vector3.one * cellScale * Mathf.Max(0.5f, board.CellSize);
            foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.sortingOrder = particleSortingOrder;
            go.SetActive(true);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(true); ps.Play(true); }
            StartCoroutine(Return(prefab, go, fxLifetime));
        }

        GameObject Rent(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var stack)) _pools[prefab] = stack = new Stack<GameObject>();
            while (stack.Count > 0) { var g = stack.Pop(); if (g != null) return g; }
            var inst = Instantiate(prefab, transform);
            // Prefabs from the pack may carry self-destruct scripts; pooling owns lifetime instead.
            foreach (var d in inst.GetComponentsInChildren<MonoBehaviour>(true))
                if (d != null && d.GetType().Name.Contains("Destroy")) Destroy(d);
            return inst;
        }

        IEnumerator Return(GameObject prefab, GameObject go, float after)
        {
            yield return new WaitForSeconds(after);
            if (go == null) yield break;
            go.SetActive(false);
            _pools[prefab].Push(go);
        }

        // ------------------------------------------------------------------ text & camera

        void FloatText(string msg, Vector3 world, int combo)
        {
            var t = _textPool.Count > 0 ? _textPool.Pop() : CreateText();
            t.text = msg;
            t.color = Color.Lerp(Color.white, new Color(1f, 0.8f, 0.2f), Mathf.Clamp01((combo - 1) / 4f));
            t.fontSize = 6f + combo * 0.8f;
            t.gameObject.SetActive(true);
            StartCoroutine(FloatRoutine(t, new Vector3(world.x, world.y, 0)));
        }

        TextMeshPro CreateText()
        {
            var go = new GameObject("FloatText");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            if (floatFont != null) t.font = floatFont;
            t.sortingOrder = particleSortingOrder + 10;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(60, 40, 10, 255);
            return t;
        }

        IEnumerator FloatRoutine(TextMeshPro t, Vector3 start)
        {
            const float dur = 0.8f;
            for (float e = 0; e < dur; e += Time.deltaTime)
            {
                float k = e / dur;
                t.transform.position = start + Vector3.up * (floatTextRise * (1f - (1f - k) * (1f - k)));
                t.transform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.2f, k / 0.15f) : 1.2f - 0.2f * k);
                var c = t.color; c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f; t.color = c;
                yield return null;
            }
            t.gameObject.SetActive(false);
            _textPool.Push(t);
        }

        IEnumerator CameraNudge(float amount, float duration)
        {
            if (cam == null) yield break;
            for (float e = 0; e < duration; e += Time.deltaTime)
            {
                float fall = 1f - e / duration;
                cam.transform.position = _camHome + (Vector3)(Random.insideUnitCircle * amount * fall);
                yield return null;
            }
            cam.transform.position = _camHome;
        }
    }
}
