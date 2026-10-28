using UnityEngine;

namespace Onet.Runtime.UI
{
    /// <summary>Keeps a full-stretch RectTransform inside the device safe area (notches, home bar).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply();
        void Update() { if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height) Apply(); }

        void Apply()
        {
            var rt = (RectTransform)transform;
            _applied = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (_screen.x == 0 || _screen.y == 0) return;
            rt.anchorMin = new Vector2(_applied.xMin / _screen.x, _applied.yMin / _screen.y);
            rt.anchorMax = new Vector2(_applied.xMax / _screen.x, _applied.yMax / _screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
