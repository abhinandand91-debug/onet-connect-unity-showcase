using UnityEngine;
using UnityEngine.UI;

namespace Onet.Runtime.UI
{
    /// <summary>
    /// Keeps the portrait-designed UI usable at any aspect ratio (YouTube Playables runs in landscape desktop windows
    /// and on tall phones). The canvas always keeps at least the full reference width and height:
    /// screens wider than the reference match height, so the HUD takes the same share of the height as on a phone and
    /// the board keeps its space; narrower (taller) screens match width, so left- and right-anchored HUD cards never
    /// squeeze into each other. Both give the same scale at exactly the reference aspect, so resizing does not jump.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [DefaultExecutionOrder(-200)]   // before CanvasScaler applies the scale this frame
    public class AdaptiveCanvasScaler : MonoBehaviour
    {
        CanvasScaler _scaler;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var reference = _scaler.referenceResolution;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            _scaler.matchWidthOrHeight = aspect > reference.x / reference.y ? 1f : 0f;
        }
    }
}
