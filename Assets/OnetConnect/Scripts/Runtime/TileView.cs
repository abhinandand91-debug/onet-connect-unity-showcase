using System.Collections;
using Onet.Core;
using Onet.Data;
using UnityEngine;

namespace Onet.Runtime
{
    /// <summary>
    /// Visual for one tile: background, icon, selection frame.
    /// Pure presentation – never touches the BoardModel. Pooled by BoardView.
    /// Prefab layout:
    ///   TileView (this)
    ///     ├─ Shadow   (SpriteRenderer, order 0)
    ///     ├─ Bg       (SpriteRenderer, order 1)
    ///     ├─ Icon     (SpriteRenderer, order 2)
    ///     └─ Frame    (SpriteRenderer, order 3, disabled by default)
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer shadow;
        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] SpriteRenderer frame;
        [SerializeField] float iconScale = 0.90f;
        [SerializeField] float iconRest = 0.05f;   // tile art has a thicker bottom edge, so the face sits slightly high
        [SerializeField] float selectedLift = 0.08f;
        [SerializeField] AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        public Cell Cell { get; set; }
        public int TileType { get; private set; }

        TileTheme _theme;
        Color _tint = Color.white;
        Sprite _iconSprite, _happySprite, _bgSprite;
        bool _selected, _hinting;
        Vector3 _baseScale = Vector3.one;
        Color _bgColor = Color.white;
        Coroutine _anim;

        public void Setup(int type, TileTheme theme, float cellSize)
        {
            TileType = type;
            _theme = theme;
            _iconSprite = theme != null ? theme.GetIcon(type) : null;
            _happySprite = theme != null ? theme.GetHappyIcon(type) : null;
            _bgSprite = theme != null ? theme.tileBackground : null;
            _tint = theme != null ? theme.GetTint(type) : Color.white;
            _selected = _hinting = false;
            icon.sprite = _iconSprite;
            if (_bgSprite != null) background.sprite = _bgSprite;
            if (shadow != null) shadow.sprite = background.sprite;
            if (theme != null && theme.selectedFrame != null) frame.sprite = theme.selectedFrame;
            _baseScale = Vector3.one * cellSize;
            transform.localScale = _baseScale;
            icon.transform.localScale = Vector3.one * iconScale;
            icon.transform.localPosition = new Vector3(0f, iconRest, 0f);
            frame.enabled = false;
            _bgColor = _tint;
            background.color = _bgColor;
            icon.color = Color.white;
            gameObject.SetActive(true);
        }

        bool UsesTileSprites => _theme != null && _theme.tileSelected != null;

        void ApplyLook()
        {
            bool on = _selected || _hinting;
            icon.sprite = on && _happySprite != null ? _happySprite : _iconSprite;
            if (UsesTileSprites)
            {
                background.sprite = _selected ? _theme.tileSelected : _hinting && _theme.tileHint != null ? _theme.tileHint : _bgSprite;
                background.color = _bgColor;
                frame.enabled = false;
            }
        }

        public void SetSelected(bool on, Color tint)
        {
            _selected = on;
            ApplyLook();
            if (!UsesTileSprites)
            {
                frame.enabled = on;
                frame.color = tint;
                background.color = on ? tint : _bgColor;
            }
            var p = icon.transform.localPosition;
            icon.transform.localPosition = new Vector3(p.x, iconRest + (on ? selectedLift : 0f), p.z);
            if (UsesTileSprites && !_hinting) transform.localScale = _baseScale * (on ? 1.07f : 1f);
        }

        public void SetHint(bool on, Color tint)
        {
            if (_anim != null) StopCoroutine(_anim);
            _hinting = on;
            ApplyLook();
            if (on) _anim = StartCoroutine(Pulse(tint));
            else { background.color = _bgColor; transform.localScale = _baseScale; }
        }

        IEnumerator Pulse(Color tint)
        {
            float t = 0;
            while (true)
            {
                t += Time.deltaTime * 4f;
                float k = (Mathf.Sin(t) + 1f) * 0.5f;
                if (!UsesTileSprites) background.color = Color.Lerp(_bgColor, tint, k);
                transform.localScale = _baseScale * (1f + 0.06f * k);
                yield return null;
            }
        }

        /// <summary>
        /// "Nope" for a wrong pair: a quick squash, a head-shake wobble and a pink blush on the face. Uses scale,
        /// rotation and colour only, so it can overlap a gravity slide (which moves the position).
        /// </summary>
        public void Nope()
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(NopeRoutine());
        }

        IEnumerator NopeRoutine()
        {
            const float duration = 0.5f, squashTime = 0.12f;
            var blush = new Color(1f, 0.62f, 0.66f);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float squash = t < squashTime ? Mathf.Sin(t / squashTime * Mathf.PI) : 0f;
                transform.localScale = new Vector3(_baseScale.x * (1f + 0.10f * squash), _baseScale.y * (1f - 0.12f * squash), _baseScale.z);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 38f) * 13f * (1f - k));
                icon.color = Color.Lerp(Color.white, blush, Mathf.Sin(k * Mathf.PI) * 0.75f);
                yield return null;
            }
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
            icon.color = Color.white;
            _anim = null;
        }

        /// <summary>Pop-and-vanish; calls onDone so BoardView can return it to the pool.</summary>
        public IEnumerator PlayMatch(float duration)
        {
            SetSelected(true, Color.white);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                transform.localScale = _baseScale * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) * popCurve.Evaluate(k);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        public IEnumerator MoveTo(Vector3 target, float duration)
        {
            Vector3 start = transform.localPosition;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / duration, 3f); // ease-out cubic
                transform.localPosition = Vector3.LerpUnclamped(start, target, k);
                yield return null;
            }
            transform.localPosition = target;
        }
    }
}
