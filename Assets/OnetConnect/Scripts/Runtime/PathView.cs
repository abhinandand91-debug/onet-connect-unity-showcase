using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Onet.Runtime
{
    /// <summary>
    /// Draws the glowing connection line with stars placed along it
    /// (see reference screenshots). Uses a LineRenderer + pooled star sprites.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer starPrefab;
        [SerializeField] float starSpacing = 0.9f;   // in cell units
        [SerializeField] float lineWidth = 0.12f;    // in cell units
        [SerializeField] int sortingOrder = 50;

        LineRenderer _line;
        readonly List<SpriteRenderer> _stars = new List<SpriteRenderer>();
        Coroutine _routine;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.sortingOrder = sortingOrder;
            _line.numCornerVertices = 4;
        }

        public void Show(IList<Vector3> points, float cellSize, Color color, Sprite starSprite, float duration)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(points, cellSize, color, starSprite, duration));
        }

        IEnumerator ShowRoutine(IList<Vector3> points, float cellSize, Color color, Sprite starSprite, float duration)
        {
            _line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) _line.SetPosition(i, points[i]);
            _line.startWidth = _line.endWidth = lineWidth * cellSize;
            _line.startColor = _line.endColor = color;

            // Stars at corners and evenly spaced along each segment
            int used = 0;
            float spacing = starSpacing * cellSize;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float len = Vector3.Distance(a, b);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing));
                for (int k = (i == 0 ? 1 : 0); k < n; k++) PlaceStar(ref used, Vector3.Lerp(a, b, (float)k / n), cellSize, starSprite);
            }
            for (int i = used; i < _stars.Count; i++) _stars[i].gameObject.SetActive(false);

            // Fade out
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float alpha = t < duration * 0.6f ? 1f : 1f - (t - duration * 0.6f) / (duration * 0.4f);
                var c = color; c.a = alpha;
                _line.startColor = _line.endColor = c;
                for (int i = 0; i < used; i++) _stars[i].color = new Color(1, 1, 1, alpha);
                yield return null;
            }
            Hide();
        }

        void PlaceStar(ref int used, Vector3 pos, float cellSize, Sprite sprite)
        {
            if (starPrefab == null) return;
            if (used >= _stars.Count) _stars.Add(Instantiate(starPrefab, transform));
            var s = _stars[used++];
            if (sprite != null) s.sprite = sprite;
            s.sortingOrder = sortingOrder + 1;
            s.transform.position = pos;
            s.transform.localScale = Vector3.one * cellSize * 0.45f;
            s.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _line.positionCount = 0;
            foreach (var s in _stars) s.gameObject.SetActive(false);
        }
    }
}
