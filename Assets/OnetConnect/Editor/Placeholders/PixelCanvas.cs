using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// A tiny drawing surface for making placeholder art in code: fills, rounded boxes, circles, glows and text.
    /// Every shape is anti-aliased and blended over what is already there. Pixel (0, 0) is the bottom-left corner.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color[] Pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[width * height]; // starts fully transparent
        }

        /// <summary>Copies the canvas into a new texture (the caller destroys it).</summary>
        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels(Pixels);
            texture.Apply();
            return texture;
        }

        #region shapes
        /// <summary>A vertical gradient over the whole canvas.</summary>
        public void Gradient(Color bottom, Color top)
        {
            for (int y = 0; y < Height; y++)
            {
                Color row = Color.Lerp(bottom, top, y / (float)(Height - 1));
                for (int x = 0; x < Width; x++) Pixels[y * Width + x] = row;
            }
        }

        /// <summary>A rounded rectangle centred at (cx, cy), with a vertical gradient fill and an optional outline.</summary>
        public void RoundedBox(float cx, float cy, float width, float height, float radius, Color bottom, Color top,
                               Color outline = default, float outlineWidth = 0f)
        {
            radius = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);
            Vector2 half = new Vector2(width * 0.5f, height * 0.5f);
            ForEachPixelIn(cx - half.x, cy - half.y, cx + half.x, cy + half.y, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f - cx, y + 0.5f - cy);
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - Vector2.one * radius);
                float distance = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                float coverage = Mathf.Clamp01(0.5f - distance);
                if (coverage <= 0f) return;

                float t = Mathf.InverseLerp(cy - half.y, cy + half.y, y);
                Color color = Color.Lerp(bottom, top, t);
                if (outlineWidth > 0f && distance > -outlineWidth) color = outline;
                Blend(x, y, color, coverage);
            });
        }

        /// <summary>A filled circle (or ellipse when rx and ry differ) with a soft top-left highlight if asked.</summary>
        public void Circle(float cx, float cy, float rx, float ry, Color color, Color outline = default, float outlineWidth = 0f, bool shine = false)
        {
            ForEachPixelIn(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) =>
            {
                float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = (1f - d) * Mathf.Min(rx, ry); // distance to the edge in pixels
                float coverage = Mathf.Clamp01(edge + 0.5f);
                if (coverage <= 0f) return;

                Color c = color;
                if (shine)
                {
                    // darker towards the bottom, lighter spot near the top-left
                    c = Color.Lerp(c, c * 0.7f, Mathf.Clamp01(-ny * 0.6f));
                    float spot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.35f, 0.4f)) / 0.45f);
                    c = Color.Lerp(c, Color.white, spot * 0.75f);
                    c.a = color.a;
                }
                if (outlineWidth > 0f && edge < outlineWidth) c = outline;
                Blend(x, y, c, coverage);
            });
        }

        /// <summary>A soft radial glow that fades to transparent at the radius.</summary>
        public void Glow(float cx, float cy, float radius, Color color)
        {
            ForEachPixelIn(cx - radius, cy - radius, cx + radius, cy + radius, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) / radius;
                float a = Mathf.Clamp01(1f - d);
                Blend(x, y, color, a * a);
            });
        }

        /// <summary>A straight line with round ends.</summary>
        public void Line(Vector2 from, Vector2 to, float thickness, Color color)
        {
            float r = thickness * 0.5f;
            ForEachPixelIn(Mathf.Min(from.x, to.x) - r, Mathf.Min(from.y, to.y) - r, Mathf.Max(from.x, to.x) + r, Mathf.Max(from.y, to.y) + r, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 ab = to - from;
                float t = Mathf.Clamp01(Vector2.Dot(p - from, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                float d = Vector2.Distance(p, from + ab * t);
                Blend(x, y, color, Mathf.Clamp01(r - d + 0.5f));
            });
        }

        /// <summary>A star with the given number of points.</summary>
        public void Star(float cx, float cy, float outer, float inner, int points, Color color)
        {
            ForEachPixelIn(cx - outer, cy - outer, cx + outer, cy + outer, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f - cx, y + 0.5f - cy);
                float angle = Mathf.Atan2(p.x, p.y);
                float sector = Mathf.PI / points;
                float local = Mathf.Abs(Mathf.Repeat(angle, sector * 2f) - sector) / sector; // 0 at a tip, 1 between tips
                float limit = Mathf.Lerp(outer, inner, local);
                Blend(x, y, color, Mathf.Clamp01(limit - p.magnitude + 0.5f));
            });
        }

        /// <summary>Writes text with the built-in pixel font, centred on (cx, cy) and scaled to the given letter height.</summary>
        public void Text(string text, float cx, float cy, float letterHeight, Color color)
        {
            PixelFont.Draw(this, text, cx, cy, letterHeight, color);
        }
        #endregion shapes

        #region helpers
        /// <summary>Blends a colour onto one pixel with the given coverage (0..1).</summary>
        public void Blend(int x, int y, Color color, float coverage)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            float a = color.a * coverage;
            if (a <= 0f) return;

            int i = y * Width + x;
            Color dst = Pixels[i];
            float outA = a + dst.a * (1f - a);
            Color rgb = outA > 0f ? (color * a + dst * dst.a * (1f - a)) / outA : Color.clear;
            rgb.a = outA;
            Pixels[i] = rgb;
        }

        private void ForEachPixelIn(float xMin, float yMin, float xMax, float yMax, System.Action<int, int> action)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(xMin) - 1), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(xMax) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(yMin) - 1), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(yMax) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    action(x, y);
        }
        #endregion helpers
    }
}
