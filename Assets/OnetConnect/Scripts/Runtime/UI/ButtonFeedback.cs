using Onet.Runtime.Juice;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Onet.Runtime.UI
{
    /// <summary>Squash-on-press and click sound for any uGUI button.</summary>
    public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        static SfxPlayer _sfx;
        [SerializeField] float pressedScale = 0.92f;
        Vector3 _rest = Vector3.one;

        void Awake() => _rest = transform.localScale;
        void OnDisable() => transform.localScale = _rest;

        public void OnPointerDown(PointerEventData e) => transform.localScale = _rest * pressedScale;
        public void OnPointerUp(PointerEventData e) => transform.localScale = _rest;

        public void OnPointerClick(PointerEventData e)
        {
            if (_sfx == null) _sfx = FindFirstObjectByType<SfxPlayer>();
            if (_sfx != null) _sfx.Play(Sfx.Click);
        }
    }
}
