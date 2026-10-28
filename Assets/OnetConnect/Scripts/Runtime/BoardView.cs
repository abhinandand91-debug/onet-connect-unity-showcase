using System;
using System.Collections;
using System.Collections.Generic;
using Onet.Core;
using Onet.Data;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Onet.Runtime
{
    /// <summary>
    /// Presentation of the BoardModel: spawns/pools TileViews, converts
    /// grid↔world, reads pointer input and runs board-level animations
    /// (intro slide-in, match pop, gravity slides, shuffle).
    /// Contains NO rules – it raises CellClicked and the GameController decides.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [SerializeField] TileView tilePrefab;
        [SerializeField] SpriteRenderer boardPanel;
        [SerializeField] Camera cam;
        [SerializeField] SpriteRenderer backdrop;   // optional full-screen theme background

        [Header("Layout (optional)")]
        [Tooltip("HUD rect whose bottom edge limits the board from above")]
        [SerializeField] RectTransform topLimit;
        [Tooltip("HUD rect whose top edge limits the board from below")]
        [SerializeField] RectTransform bottomLimit;
        [Tooltip("Cells of width/height reserved for the outer path lane (1 = half a cell each side)")]
        [SerializeField] float laneAllowance = 1f;

        [Header("Fit")]
        [Tooltip("World-space rect the board (incl. outer lane) must fit into, centred on this transform")]
        [SerializeField] Vector2 fitArea = new Vector2(9f, 14f);
        [SerializeField] float maxCellSize = 1.7f;
        [SerializeField] float tileGap = 0.05f;

        [Header("Intro")]
        [SerializeField] float introRowDelay = 0.04f;
        [SerializeField] float introSlideTime = 0.35f;

        public event Action<Cell> CellClicked;

        /// <summary>World size of one cell. The board scales as a whole when the screen changes, so this follows it.</summary>
        public float CellSize => _cell * transform.localScale.x;
        public bool InputEnabled { get; set; } = true;

        BoardModel _model;
        Vector2 _fit;
        float _cell;               // cell size in board-local units, fixed for the life of a Build
        Vector2Int _fittedScreen;  // screen size the current scale was fitted to
        TileTheme _theme;
        TileView[,] _views;
        readonly Stack<TileView> _pool = new Stack<TileView>();

        // ---------- Build ----------

        public void Build(BoardModel model, TileTheme theme)
        {
            Clear();
            _model = model;
            _theme = theme;
            _views = new TileView[model.Width, model.Height];

            // +2 for the outer lane so paths drawn outside the board stay on screen
            transform.localScale = Vector3.one;
            ApplyLayout();
            _cell = FittedCellSize();
            _fittedScreen = default;   // refit next LateUpdate, once the HUD canvases have their final scale

            ApplyBackdrop(theme);

            if (boardPanel != null)
            {
                boardPanel.drawMode = SpriteDrawMode.Sliced;
                boardPanel.size = new Vector2((model.Width + 0.4f) * _cell, (model.Height + 0.4f) * _cell);
                boardPanel.color = theme != null ? theme.boardPanelColor : boardPanel.color;
            }

            for (int y = 0; y < model.Height; y++)
                for (int x = 0; x < model.Width; x++)
                {
                    int v = model.Get(x, y);
                    if (v > 0) Spawn(new Cell(x, y), v);
                }
        }

        /// <summary>
        /// Fits the board between the HUD bars (world space, any aspect ratio): width is the screen width,
        /// height is the gap between the top and bottom limit rects, and the board centres in that gap.
        /// </summary>
        void ApplyLayout()
        {
            var camera = cam != null ? cam : Camera.main;
            _fit = fitArea;
            if (camera == null || !camera.orthographic) return;
            Canvas.ForceUpdateCanvases();
            float halfH = camera.orthographicSize, halfW = halfH * camera.aspect;
            float top = camera.transform.position.y + halfH, bottom = camera.transform.position.y - halfH;
            var corners = new Vector3[4];
            float z = -camera.transform.position.z;
            // Overlay canvases report screen pixels directly; camera canvases report world points that must be projected.
            var anyLimit = topLimit != null ? topLimit : bottomLimit;
            var canvas = anyLimit != null ? anyLimit.GetComponentInParent<Canvas>() : null;
            Camera uiCam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (topLimit != null)
            {
                topLimit.GetWorldCorners(corners);                       // [0] = bottom-left
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]);
                top = camera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z)).y;
            }
            if (bottomLimit != null)
            {
                bottomLimit.GetWorldCorners(corners);                    // [1] = top-left
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[1]);
                bottom = camera.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z)).y;
            }
            _fit = new Vector2(Mathf.Min(fitArea.x, halfW * 2f * 0.97f), Mathf.Max(2f, top - bottom - 0.3f));
            if (topLimit != null || bottomLimit != null)
                transform.position = new Vector3(camera.transform.position.x, (top + bottom) * 0.5f, 0f);
        }

        float FittedCellSize() =>
            Mathf.Min(maxCellSize, _fit.x / (_model.Width + laneAllowance), _fit.y / (_model.Height + laneAllowance));

        /// <summary>
        /// Window resized or rotated: refit by scaling the whole board, so tile positions, running animations and
        /// cell lookups (all in local space) stay valid mid-move.
        /// </summary>
        void LateUpdate()
        {
            if (_model == null || (_fittedScreen.x == Screen.width && _fittedScreen.y == Screen.height)) return;
            _fittedScreen = new Vector2Int(Screen.width, Screen.height);
            ApplyLayout();
            transform.localScale = Vector3.one * (FittedCellSize() / _cell);
            ApplyBackdrop(_theme);
        }

        void ApplyBackdrop(TileTheme theme)
        {
            if (backdrop == null) return;
            var camera = cam != null ? cam : Camera.main;
            bool has = theme != null && theme.background != null && camera != null;
            backdrop.gameObject.SetActive(has);
            if (!has) return;
            backdrop.sprite = theme.background;
            float h = camera.orthographicSize * 2f, w = h * camera.aspect;
            var size = theme.background.bounds.size;
            float k = Mathf.Max(w / size.x, h / size.y) * 1.02f;
            backdrop.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
            backdrop.transform.localScale = new Vector3(k, k, 1f);
        }

        TileView Spawn(Cell c, int type)
        {
            var v = _pool.Count > 0 ? _pool.Pop() : Instantiate(tilePrefab, transform);
            v.Cell = c;
            v.transform.localPosition = CellToLocal(c);
            v.Setup(type, _theme, _cell - tileGap);
            _views[c.X, c.Y] = v;
            return v;
        }

        public void Clear()
        {
            if (_views == null) return;
            foreach (var v in _views)
                if (v != null) { v.gameObject.SetActive(false); _pool.Push(v); }
            _views = null;
        }

        // ---------- Coordinates ----------

        /// <summary>Local position of a cell centre. Works for the outer lane (-1 / W / H).</summary>
        public Vector3 CellToLocal(Cell c)
        {
            float ox = -(_model.Width - 1) * 0.5f * _cell;
            float oy = (_model.Height - 1) * 0.5f * _cell;
            return new Vector3(ox + c.X * _cell, oy - c.Y * _cell, 0f);
        }

        public Vector3 CellToWorld(Cell c) => transform.TransformPoint(CellToLocal(c));

        public bool TryWorldToCell(Vector3 world, out Cell cell)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float ox = -(_model.Width - 1) * 0.5f * _cell;
            float oy = (_model.Height - 1) * 0.5f * _cell;
            int x = Mathf.RoundToInt((local.x - ox) / _cell);
            int y = Mathf.RoundToInt((oy - local.y) / _cell);
            cell = new Cell(x, y);
            return _model.InBounds(cell);
        }

        // ---------- Input ----------

        void Update()
        {
            if (!InputEnabled || _model == null) return;
            if (!PointerDown(out Vector2 screen)) return;
            var camera = cam != null ? cam : Camera.main;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            if (TryWorldToCell(world, out Cell c) && _model.IsTile(c)) CellClicked?.Invoke(c);
        }

        /// <summary>Raises the same event a real tap would (tutorial scripts, automated tests).</summary>
        public void SimulateTap(Cell c) => CellClicked?.Invoke(c);

        static bool PointerDown(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            var p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame) { pos = p.position.ReadValue(); return true; }
            pos = default; return false;
#else
            if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
            pos = default; return false;
#endif
        }

        // ---------- Tile state ----------

        public TileView ViewAt(Cell c) => _model.InBounds(c) ? _views[c.X, c.Y] : null;

        public void SetSelected(Cell c, bool on)
        {
            var v = ViewAt(c);
            if (v != null) v.SetSelected(on, _theme != null ? _theme.selectedTint : Color.yellow);
        }

        public void SetHint(Move? m, bool on)
        {
            if (!m.HasValue) return;
            var tint = _theme != null ? _theme.hintTint : Color.green;
            ViewAt(m.Value.A)?.SetHint(on, tint);
            ViewAt(m.Value.B)?.SetHint(on, tint);
        }

        /// <summary>Wrong-pair reaction on one tile (see <see cref="TileView.Nope"/>).</summary>
        public void Nope(Cell c) => ViewAt(c)?.Nope();

        // ---------- Animations ----------

        public IEnumerator AnimateMatch(Cell a, Cell b, float duration)
        {
            var va = ViewAt(a); var vb = ViewAt(b);
            _views[a.X, a.Y] = null; _views[b.X, b.Y] = null;
            if (va != null) StartCoroutine(va.PlayMatch(duration));
            if (vb != null) yield return vb.PlayMatch(duration);
            else yield return new WaitForSeconds(duration);
            if (va != null) _pool.Push(va);
            if (vb != null) _pool.Push(vb);
        }

        public IEnumerator AnimateGravity(List<TileMove> moves, float duration)
        {
            if (moves.Count == 0) yield break;
            // Detach first, then re-attach, so chains (A→B while B→C) don't overwrite.
            var moving = new List<(TileView v, Cell to)>(moves.Count);
            foreach (var m in moves)
            {
                var v = _views[m.From.X, m.From.Y];
                _views[m.From.X, m.From.Y] = null;
                if (v != null) moving.Add((v, m.To));
            }
            foreach (var item in moving)
            {
                TileView v = item.v; Cell to = item.to;
                _views[to.X, to.Y] = v;
                v.Cell = to;
                StartCoroutine(v.MoveTo(CellToLocal(to), duration));
            }
            yield return new WaitForSeconds(duration);
        }

        /// <summary>After a shuffle, re-skin every tile in place with a quick flip.</summary>
        public IEnumerator AnimateShuffle(float duration)
        {
            // Gather tiles to the centre, then fly back to (new) types
            var views = new List<TileView>();
            foreach (var v in _views) if (v != null) views.Add(v);
            foreach (var v in views) StartCoroutine(v.MoveTo(Vector3.zero, duration * 0.5f));
            yield return new WaitForSeconds(duration * 0.5f);
            Build(_model, _theme);
            foreach (var v in _views)
            {
                if (v == null) continue;
                Vector3 target = v.transform.localPosition;
                v.transform.localPosition = Vector3.zero;
                StartCoroutine(v.MoveTo(target, duration * 0.5f));
            }
            yield return new WaitForSeconds(duration * 0.5f);
        }

        /// <summary>Rows slide in from alternating sides (reference screenshot 6).</summary>
        public IEnumerator PlayIntro()
        {
            InputEnabled = false;
            float offscreen = _fit.x + 2f;
            for (int y = 0; y < _model.Height; y++)
            {
                float dir = (y % 2 == 0) ? -1f : 1f;
                for (int x = 0; x < _model.Width; x++)
                {
                    var v = _views[x, y];
                    if (v == null) continue;
                    Vector3 target = CellToLocal(new Cell(x, y));
                    v.transform.localPosition = target + new Vector3(dir * offscreen, -dir * 0.8f, 0);
                    StartCoroutine(v.MoveTo(target, introSlideTime));
                }
                yield return new WaitForSeconds(introRowDelay);
            }
            yield return new WaitForSeconds(introSlideTime);
            InputEnabled = true;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(fitArea.x, fitArea.y, 0));
        }
#endif
    }
}
