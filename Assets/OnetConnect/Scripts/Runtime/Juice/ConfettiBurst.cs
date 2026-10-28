using UnityEngine;
using UnityEngine.UI;

namespace Onet.Runtime.Juice
{
    /// <summary>
    /// UI confetti for the win card: two cannons low on the sides fire paper pieces that arc up over the stars,
    /// flutter and fall.
    /// Lives on a full-screen RectTransform above the card (so it draws over the popup's dim overlay, which world-space
    /// particles cannot), creates its pieces once and reuses them. Runs on unscaled time like the rest of the win card.
    /// </summary>
    public class ConfettiBurst : MonoBehaviour
    {
        [SerializeField] int count = 110;
        [SerializeField] float lifetime = 2.8f;
        [SerializeField] Color[] colors =
        {
            new Color(0.96f, 0.45f, 0.42f), new Color(1f, 0.78f, 0.26f), new Color(0.35f, 0.75f, 0.4f),
            new Color(0.35f, 0.66f, 0.95f), new Color(0.62f, 0.46f, 0.88f), new Color(1f, 0.62f, 0.78f),
        };

        struct Piece
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float spin, angle, flip, age, delay;
        }

        Piece[] _pieces;
        bool _running;
        float _time;

        void Awake()
        {
            _pieces = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = Random.value < 0.3f ? new Vector2(20, 20) : new Vector2(16, 30);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _pieces[i] = new Piece { rt = rt, img = img };
                go.SetActive(false);
            }
        }

        public void Play()
        {
            var area = ((RectTransform)transform).rect;
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref var p = ref _pieces[i];
                bool left = i % 2 == 0;
                p.pos = new Vector2(area.width * (left ? -0.46f : 0.46f), area.height * -0.26f);
                float speed = Random.Range(1900f, 2900f);
                float angle = Random.Range(62f, 84f) * Mathf.Deg2Rad;
                p.vel = new Vector2(Mathf.Cos(angle) * speed * (left ? 1f : -1f), Mathf.Sin(angle) * speed);
                p.spin = Random.Range(-540f, 540f);
                p.angle = Random.Range(0f, 360f);
                p.flip = Random.Range(6f, 14f);
                p.age = 0f;
                p.delay = Random.Range(0f, 0.25f);
                p.img.color = colors[Random.Range(0, colors.Length)];
                p.rt.gameObject.SetActive(false);
            }
            _time = 0f;
            _running = true;
        }

        void OnDisable()
        {
            _running = false;
            if (_pieces != null) foreach (var p in _pieces) if (p.rt != null) p.rt.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_running) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _time += dt;
            bool alive = false;
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref var p = ref _pieces[i];
                if (_time < p.delay) { alive = true; continue; }
                p.age += dt;
                if (p.age > lifetime) { if (p.rt.gameObject.activeSelf) p.rt.gameObject.SetActive(false); continue; }
                alive = true;
                if (!p.rt.gameObject.activeSelf) p.rt.gameObject.SetActive(true);

                p.vel.y -= 2600f * dt;                                  // gravity
                p.vel *= 1f - 1.6f * dt;                                // air drag
                if (p.vel.y < -420f) p.vel.y = -420f;                   // paper falls slowly
                p.pos += p.vel * dt + new Vector2(Mathf.Sin(p.age * p.flip) * 40f * dt, 0f);
                p.angle += p.spin * dt;

                p.rt.anchoredPosition = p.pos;
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.angle);
                p.rt.localScale = new Vector3(Mathf.Cos(p.age * p.flip), 1f, 1f);   // tumbling paper
                var c = p.img.color;
                c.a = Mathf.Clamp01((lifetime - p.age) / 0.6f);
                p.img.color = c;
            }
            _running = alive;
        }
    }
}
