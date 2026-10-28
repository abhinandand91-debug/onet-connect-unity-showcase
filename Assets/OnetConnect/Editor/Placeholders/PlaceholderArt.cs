using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Repaints the tile icons, tile frames, power-ups, backdrops, app icon and tutorial hand with simple art drawn in code.
    /// Each PNG keeps its file name, pixel size and import settings, so the theme builder and scenes use them exactly
    /// as before. Every icon gets its own colour, shape and two letters, so all 36 stay easy to tell apart.
    /// Menu: Onet > Setup > Repaint Placeholder Art.
    /// </summary>
    public static class PlaceholderArt
    {
        private const string Root = "Assets/OnetConnect/Art";
        private static readonly string[] Folders = { Root + "/Cute", Root + "/AppIcon" };
        private const string HandPath = Root + "/UI/hand.png";

        private static readonly Color Ink = new Color(0.25f, 0.18f, 0.3f);

        [MenuItem("Onet/Setup/Repaint Placeholder Art")]
        public static void RepaintAll()
        {
            foreach (string folder in Folders)
                foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                    Repaint(path.Replace('\\', '/'));
            Repaint(HandPath);
            AssetDatabase.Refresh();
            Debug.Log("[Onet] Placeholder art repainted.");
        }

        private static void Repaint(string path)
        {
            Vector2Int size = ReadPngSize(path);
            var canvas = new PixelCanvas(size.x, size.y);
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            Paint(folder, Path.GetFileNameWithoutExtension(path), canvas);

            Texture2D texture = canvas.ToTexture();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>Chooses what to draw from the folder and file name.</summary>
        private static void Paint(string folder, string name, PixelCanvas c)
        {
            switch (folder)
            {
                case "Icons": Icon(name, c, happy: false); break;
                case "Happy": Icon(name, c, happy: true); break;
                case "Tiles": Tile(name, c); break;
                case "Powerups": Powerup(name, c); break;
                case "Backdrops": Backdrop(name, c); break;
                case "AppIcon": AppIcon(name, c); break;
                default: if (name == "hand") Hand(c); break;
            }
        }

        #region icons
        /// <summary>
        /// "07_strawberry" becomes a shape picked by the number, in one of twelve colours, with "ST" written on it.
        /// The happy version adds a smile and a sparkle.
        /// </summary>
        private static void Icon(string name, PixelCanvas c, bool happy)
        {
            int index = int.TryParse(name.Substring(0, 2), out int n) ? n - 1 : name.GetHashCode() & 0xFF;
            string word = name.Length > 3 ? name.Substring(3) : name;
            string letters = word.Substring(0, Mathf.Min(2, word.Length)).ToUpperInvariant();

            Color color = Color.HSVToRGB((index % 12) / 12f, 0.55f, 0.95f);
            Color outline = Color.Lerp(color, Ink, 0.6f);
            float cx = c.Width * 0.5f, cy = c.Height * 0.5f, r = c.Width * 0.4f;

            switch ((index / 12) % 3)
            {
                case 0: c.Circle(cx, cy, r, r, color, outline, 8f, shine: true); break;
                case 1: c.RoundedBox(cx, cy, r * 1.8f, r * 1.8f, r * 0.45f, Color.Lerp(color, Ink, 0.15f), Color.Lerp(color, Color.white, 0.3f), outline, 8f); break;
                default: c.Star(cx, cy, r * 1.1f, r * 0.62f, 6, outline); c.Star(cx, cy, r * 0.98f, r * 0.52f, 6, color); break;
            }

            c.Text(letters, cx, cy + (happy ? r * 0.15f : 0f), r * 0.6f, Color.white);
            if (happy)
            {
                c.Circle(cx, cy - r * 0.45f, r * 0.28f, r * 0.12f, outline);
                c.Star(cx + r * 0.75f, cy + r * 0.75f, r * 0.3f, r * 0.08f, 4, Color.white);
            }
        }
        #endregion icons

        #region tiles, power-ups and backdrops
        private static void Tile(string name, PixelCanvas c)
        {
            float cx = c.Width * 0.5f, cy = c.Height * 0.5f, s = c.Width * 0.92f;
            Color face = new Color(1f, 0.97f, 0.9f), edge = new Color(0.85f, 0.75f, 0.65f);
            switch (name)
            {
                case "tile_selected": edge = new Color(1f, 0.75f, 0.1f); face = new Color(1f, 0.95f, 0.75f); break;
                case "tile_hint": edge = new Color(0.3f, 0.8f, 0.4f); face = new Color(0.9f, 1f, 0.9f); break;
                case "tile_matching": edge = Color.white; face = new Color(1f, 1f, 0.95f); break;
                case "tile_disabled": edge = new Color(0.6f, 0.6f, 0.65f); face = new Color(0.82f, 0.82f, 0.85f); break;
            }
            c.RoundedBox(cx, cy - 6f, s, s, s * 0.2f, Color.Lerp(edge, Ink, 0.3f), Color.Lerp(edge, Ink, 0.3f)); // shadow lip
            c.RoundedBox(cx, cy + 2f, s, s - 8f, s * 0.2f, Color.Lerp(face, edge, 0.35f), face, edge, 10f);
        }

        private static void Powerup(string name, PixelCanvas c)
        {
            float cx = c.Width * 0.5f, cy = c.Height * 0.5f, r = c.Width * 0.42f;
            (Color color, string label) look;
            switch (name)
            {
                case "hint": look = (new Color(1f, 0.8f, 0.2f), "?"); break;
                case "shuffle": look = (new Color(0.4f, 0.6f, 1f), "<>"); break;
                case "remove": look = (new Color(0.95f, 0.35f, 0.4f), "X"); break;
                case "find": look = (new Color(0.45f, 0.8f, 0.5f), "F"); break;
                case "time": look = (new Color(0.65f, 0.45f, 0.95f), "+T"); break;
                default: look = (Color.gray, name.Substring(0, 1).ToUpperInvariant()); break;
            }
            c.Circle(cx, cy, r, r, look.color, Color.Lerp(look.color, Ink, 0.6f), 9f, shine: true);
            c.Text(look.label, cx, cy, r * 0.75f, Color.white);
        }

        private static void Backdrop(string name, PixelCanvas c)
        {
            Color top, bottom;
            switch (name)
            {
                case "bg_meadow": top = new Color(0.65f, 0.9f, 0.75f); bottom = new Color(0.35f, 0.7f, 0.5f); break;
                case "bg_sunset": top = new Color(1f, 0.75f, 0.55f); bottom = new Color(0.85f, 0.45f, 0.55f); break;
                default: top = new Color(0.7f, 0.65f, 0.95f); bottom = new Color(0.4f, 0.35f, 0.7f); break;
            }
            c.Gradient(bottom, top);
            var random = new System.Random(name.Length * 97);
            for (int i = 0; i < 14; i++)
            {
                float x = (float)random.NextDouble() * c.Width, y = (float)random.NextDouble() * c.Height;
                c.Glow(x, y, c.Width * (0.06f + 0.1f * (float)random.NextDouble()), new Color(1f, 1f, 1f, 0.18f));
            }
        }

        /// <summary>The launcher icon: a tile with the first icon on it, plus the adaptive background and foreground layers.</summary>
        private static void AppIcon(string name, PixelCanvas c)
        {
            float cx = c.Width * 0.5f, cy = c.Height * 0.5f;
            bool background = name.Contains("background"), foreground = name.Contains("foreground");
            if (!foreground) c.Gradient(new Color(0.55f, 0.4f, 0.85f), new Color(0.75f, 0.65f, 1f));
            if (background) return;

            // the adaptive foreground must stay inside the middle two thirds, which the launcher never crops
            float scale = foreground ? 0.55f : 0.75f;
            var tile = new PixelCanvas(256, 256);
            Tile("tile_default", tile);
            Icon("01_onet", tile, happy: true);
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    float u = (x - cx) / (c.Width * scale) + 0.5f, v = (y - cy) / (c.Height * scale) + 0.5f;
                    if (u < 0f || v < 0f || u >= 1f || v >= 1f) continue;
                    Color source = tile.Pixels[(int)(v * 256) * 256 + (int)(u * 256)];
                    c.Blend(x, y, source, 1f);
                }
        }

        /// <summary>A simple pointing hand: palm, an upright finger with its tip at the top centre, and a thumb.</summary>
        private static void Hand(PixelCanvas c)
        {
            float w = c.Width, h = c.Height, cx = w * 0.5f;
            Color skin = new Color(1f, 0.85f, 0.7f), line = new Color(0.45f, 0.3f, 0.25f);
            c.RoundedBox(cx, h * 0.62f, w * 0.2f, h * 0.7f, w * 0.1f, skin, skin, line, 5f);          // finger
            c.RoundedBox(cx + w * 0.08f, h * 0.3f, w * 0.6f, h * 0.4f, w * 0.16f, skin, skin, line, 5f); // palm
            c.RoundedBox(cx - w * 0.25f, h * 0.36f, w * 0.18f, h * 0.22f, w * 0.09f, skin, skin, line, 5f); // thumb
        }
        #endregion tiles, power-ups and backdrops

        private static Vector2Int ReadPngSize(string path)
        {
            byte[] header = new byte[24];
            using (var stream = File.OpenRead(path)) stream.Read(header, 0, header.Length);
            int Read(int o) => (header[o] << 24) | (header[o + 1] << 16) | (header[o + 2] << 8) | header[o + 3];
            return new Vector2Int(Read(16), Read(20)); // width and height from the IHDR chunk
        }
    }
}
