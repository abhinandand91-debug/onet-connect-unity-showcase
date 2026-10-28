using Onet.Runtime.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onet.Runtime.Boot
{
    /// <summary>
    /// Boot scene driver: the persistent <see cref="AppServices"/> already initialised itself in Awake, so this only
    /// presents the loading screen and hands over to the Game scene through the <see cref="SceneLoader"/>.
    /// The progress bar eases towards the real progress (so it never jumps), with scrolling candy stripes, a shine
    /// sweep, a spinning star riding the leading edge, a percentage and a little pop when it fills.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] TMP_Text status;
        [SerializeField] RectTransform[] mascots;
        [Tooltip("Smiling faces shown while each mascot is in the air; same order as mascots")]
        [SerializeField] Sprite[] mascotHappy;
        [SerializeField] float minSeconds = 1.8f;
        [Tooltip("Minimum loading screen time inside YouTube Playables, where time to first interaction is measured")]
        [SerializeField] float playablesMinSeconds = 0.4f;

        [Header("Progress bar")]
        [SerializeField] RectTransform bar;        // whole bar, pops when full
        [SerializeField] RectTransform track;      // masked inner track the fill grows in
        [SerializeField] RectTransform fill;       // width driven through anchorMax.x
        [SerializeField] RawImage stripes;         // child of fill, as wide as the track, uv scrolled
        [SerializeField] RectTransform shine;      // child of fill, sweeps left to right
        [SerializeField] RectTransform spark;      // star on the fill's leading edge
        [SerializeField] TMP_Text percent;

        const float StripeTile = 64f;              // canvas px per stripe texture repeat

        static readonly string[] Messages =
        {
            "Waking up the animals…", "Polishing the tiles…", "Shuffling the board…", "Ready to play!"
        };

        float _target, _shown, _popT = -1f;
        float[] _mascotY;                          // authored heights; the bounce is added on top
        Image[] _mascotFace;
        Sprite[] _mascotCalm;

        void Start()
        {
            if (stripes != null) stripes.texture = StripeTexture();
            if (mascots != null)
            {
                _mascotY = new float[mascots.Length];
                _mascotFace = new Image[mascots.Length];
                _mascotCalm = new Sprite[mascots.Length];
                for (int i = 0; i < mascots.Length; i++)
                {
                    if (mascots[i] == null) continue;
                    _mascotY[i] = mascots[i].anchoredPosition.y;
                    var icon = mascots[i].Find("Icon");
                    _mascotFace[i] = icon != null ? icon.GetComponent<Image>() : null;
                    if (_mascotFace[i] != null) _mascotCalm[i] = _mascotFace[i].sprite;
                }
            }
            ApplyBar(0f);
            if (!AppServices.Exists) { Debug.LogError("Boot scene has no AppServices object"); return; }
            SetProgress(0f);
            var app = AppServices.Instance;
            app.Loader.Load(AppServices.GameScene, Playables.InPlayables ? playablesMinSeconds : minSeconds, SetProgress,
                            holdUntil: () => app.IsReady);
        }

        void SetProgress(float p)
        {
            _target = Mathf.Max(_target, Mathf.Clamp01(p));
            if (status != null) status.text = Messages[Mathf.Min(Messages.Length - 1, (int)(_target * (Messages.Length - 0.01f)))];
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;

            // ease towards the real progress: quick when far behind, never stalling, and fast to finish once loaded
            float speed = _target >= 1f ? 3f : 0.5f + 2.5f * (_target - _shown);
            float before = _shown;
            _shown = Mathf.MoveTowards(_shown, _target, speed * dt);
            if (before < 1f && _shown >= 1f) _popT = 0f;
            ApplyBar(_shown);

            if (stripes != null && track != null)
            {
                var size = track.rect.size;
                stripes.uvRect = new Rect(-t * 0.9f, 0f, size.x / StripeTile, size.y / StripeTile);
            }
            if (shine != null && fill != null)
            {
                float k = Mathf.Repeat(t / 1.6f, 1f);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-60f, fill.rect.width + 60f, k), 0f);
            }
            if (spark != null)
            {
                spark.localRotation = Quaternion.Euler(0f, 0f, -t * 120f);
                spark.localScale = Vector3.one * ((1f + 0.15f * Mathf.Sin(t * 8f)) * Mathf.Clamp01(_shown * 20f));
            }
            if (bar != null && _popT >= 0f)
            {
                _popT += dt;
                float k = Mathf.Clamp01(_popT / 0.3f);
                bar.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f) _popT = -1f;
            }

            if (mascots == null || _mascotY == null) return;
            for (int i = 0; i < mascots.Length; i++)
            {
                if (mascots[i] == null) continue;
                float k = Mathf.Abs(Mathf.Sin(t * 3.2f - i * 0.7f));
                mascots[i].anchoredPosition = new Vector2(mascots[i].anchoredPosition.x, _mascotY[i] + 30f * k);
                mascots[i].localScale = new Vector3(1f + 0.06f * (1f - k), 1f - 0.08f * (1f - k), 1f);
                // smile while airborne, calm face on landing
                var face = _mascotFace[i];
                if (face != null && mascotHappy != null && i < mascotHappy.Length && mascotHappy[i] != null)
                {
                    var want = k > 0.45f ? mascotHappy[i] : _mascotCalm[i];
                    if (face.sprite != want) face.sprite = want;
                }
            }
        }

        void ApplyBar(float p)
        {
            if (fill != null)
            {
                // keep at least a full end cap visible so the fill stays pill-shaped at any value
                float minFraction = track != null && track.rect.width > 0f ? track.rect.height / track.rect.width : 0f;
                fill.anchorMax = new Vector2(Mathf.Lerp(minFraction, 1f, p), 1f);
                fill.gameObject.SetActive(p > 0.001f);
            }
            if (spark != null && track != null)
            {
                float w = track.rect.width;
                float edge = fill != null ? fill.anchorMax.x * w : p * w;
                spark.anchoredPosition = new Vector2(track.anchoredPosition.x - w * 0.5f + edge - track.rect.height * 0.35f, 0f);
            }
            if (percent != null) percent.text = Mathf.RoundToInt(p * 100f) + "%";
        }

        /// <summary>Soft diagonal white stripes on transparent, tiling in both directions.</summary>
        static Texture2D StripeTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float band = Mathf.Repeat((x + y) / (float)n * 2f, 1f);          // two stripes per tile, 45 degrees
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.5f - Mathf.Abs(band - 0.5f)) * 6f - 1f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 70));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
