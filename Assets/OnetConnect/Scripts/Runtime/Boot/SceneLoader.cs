using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Onet.Runtime.Boot
{
    /// <summary>
    /// Async scene transitions with a full-screen fade that survives the switch.
    /// Reports 0..1 progress (which already accounts for a minimum display time) so a loading screen can show it.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] Color fadeColor = new Color(0.78f, 0.93f, 0.85f, 1f);
        [SerializeField] float fadeSeconds = 0.3f;

        CanvasGroup _group;
        AsyncOperation _pending;
        bool _busy;

        public bool IsBusy => _busy;

        void Awake() => BuildOverlay();

        // A load held back with allowSceneActivation=false blocks every later scene load in Unity, so never leave one dangling.
        void OnDestroy() { if (_pending != null) _pending.allowSceneActivation = true; }

        void BuildOverlay()
        {
            var go = new GameObject("SceneFade", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            go.GetComponent<Image>().color = fadeColor;
            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        /// <summary>
        /// Loads <paramref name="scene"/>; returns false if a load is already running. The scene is not activated until
        /// <paramref name="holdUntil"/> (when given) returns true, e.g. while a cloud save is still arriving.
        /// </summary>
        public bool Load(string scene, float minSeconds = 0f, Action<float> progress = null, Action loaded = null, Func<bool> holdUntil = null)
        {
            if (_busy) return false;
            StartCoroutine(Routine(scene, minSeconds, progress, loaded, holdUntil));
            return true;
        }

        IEnumerator Routine(string scene, float minSeconds, Action<float> progress, Action loaded, Func<bool> holdUntil)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            var op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            _pending = op;

            float t = 0f;
            while (op.progress < 0.9f || t < minSeconds || (holdUntil != null && !holdUntil()))
            {
                t += Time.unscaledDeltaTime;
                float real = Mathf.Clamp01(op.progress / 0.9f);
                float timed = minSeconds > 0f ? Mathf.Clamp01(t / minSeconds) : 1f;
                progress?.Invoke(Mathf.Min(real, timed));
                yield return null;
            }
            progress?.Invoke(1f);

            yield return Fade(0f, 1f);                  // cover the old scene
            op.allowSceneActivation = true;
            yield return op;
            _pending = null;
            loaded?.Invoke();
            yield return null;
            yield return Fade(1f, 0f);                  // reveal the new one
            _group.blocksRaycasts = false;
            _busy = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
