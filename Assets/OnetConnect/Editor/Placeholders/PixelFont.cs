using System.Collections.Generic;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>A 5x7 pixel font (A-Z, 0-9 and a few symbols) for writing labels on generated art.</summary>
    public static class PixelFont
    {
        private const int GlyphW = 5, GlyphH = 7;

        // each glyph is 7 rows of 5 characters, '#' = filled
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { 'A', ".###.#...##...#######...##...##...#" }, { 'B', "####.#...##...#####.#...##...#####." },
            { 'C', ".###.#...##....#....#....#...#.###." }, { 'D', "####.#...##...##...##...##...#####." },
            { 'E', "######....#....####.#....#....#####" }, { 'F', "######....#....####.#....#....#...." },
            { 'G', ".###.#...##....#.####...##...#.###." }, { 'H', "#...##...##...#######...##...##...#" },
            { 'I', ".###...#....#....#....#....#...###." }, { 'J', "..###...#....#....#....#.#..#..##.." },
            { 'K', "#...##..#.#.#..##...#.#..#..#.#...#" }, { 'L', "#....#....#....#....#....#....#####" },
            { 'M', "#...###.###.#.##...##...##...##...#" }, { 'N', "#...###..##.#.##..###...##...##...#" },
            { 'O', ".###.#...##...##...##...##...#.###." }, { 'P', "####.#...##...#####.#....#....#...." },
            { 'Q', ".###.#...##...##...##.#.##..#..##.#" }, { 'R', "####.#...##...#####.#.#..#..#.#...#" },
            { 'S', ".#####....#.....###.....#....#####." }, { 'T', "#####..#....#....#....#....#....#.." },
            { 'U', "#...##...##...##...##...##...#.###." }, { 'V', "#...##...##...##...##...#.#.#...#.." },
            { 'W', "#...##...##...##.#.##.#.###.###...#" }, { 'X', "#...##...#.#.#...#...#.#.#...##...#" },
            { 'Y', "#...##...#.#.#...#....#....#....#.." }, { 'Z', "#####....#...#...#...#...#....#####" },
            { '0', ".###.#...##..###.#.###..##...#.###." }, { '1', "..#...##....#....#....#....#...###." },
            { '2', ".###.#...#....#...#...#...#...#####" }, { '3', "####.....#....#.###.....#....#####." },
            { '4', "...#...##..#.#.#..#.#####...#....#." }, { '5', "######....####.....#....#....#####." },
            { '6', ".###.#....#....####.#...##...#.###." }, { '7', "#####....#...#...#...#....#....#..." },
            { '8', ".###.#...##...#.###.#...##...#.###." }, { '9', ".###.#...##...#.####....#....#.###." },
            { '+', ".......#....#..#####..#....#......." }, { '-', "...............#####..............." },
            { '?', ".###.#...#....#...#...#.........#.." }, { '!', "..#....#....#....#....#.........#.." },
            { '>', ".#.....#.....#.....#...#...#...#..." }, { '<', "...#...#...#...#.....#.....#.....#." },
            { 'x', "......#...#.#.#...#...#.#.#...#...." }, { '*', "..#..#.#.#.###.#####.###.#.#.#..#.." },
        };

        /// <summary>Draws text centred on (cx, cy). Each letter is <paramref name="letterHeight"/> pixels tall.</summary>
        public static void Draw(PixelCanvas canvas, string text, float cx, float cy, float letterHeight, Color color)
        {
            float cell = letterHeight / GlyphH;                 // size of one font pixel
            float advance = (GlyphW + 1) * cell;                 // letter width plus one pixel gap
            float left = cx - (text.Length * advance - cell) * 0.5f;
            float bottom = cy - letterHeight * 0.5f;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!Glyphs.TryGetValue(c, out string rows) && !Glyphs.TryGetValue(char.ToUpperInvariant(c), out rows)) continue;

                for (int row = 0; row < GlyphH; row++)
                    for (int col = 0; col < GlyphW; col++)
                    {
                        if (rows[row * GlyphW + col] != '#') continue;
                        // glyph rows go top to bottom, the canvas goes bottom to top
                        float px = left + i * advance + col * cell;
                        float py = bottom + (GlyphH - 1 - row) * cell;
                        FillCell(canvas, px, py, cell, color);
                    }
            }
        }

        private static void FillCell(PixelCanvas canvas, float x, float y, float size, Color color)
        {
            for (int py = Mathf.FloorToInt(y); py < Mathf.CeilToInt(y + size); py++)
                for (int px = Mathf.FloorToInt(x); px < Mathf.CeilToInt(x + size); px++)
                    canvas.Blend(px, py, color, 1f);
        }
    }
}
