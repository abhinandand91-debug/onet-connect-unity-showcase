using UnityEditor;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Everything under Art/Themes imports as a single clamped sprite (256 px = 1 unit, no mipmaps),
    /// so theme art behaves the same no matter which tool dropped the PNGs in.
    /// </summary>
    class ThemeTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/OnetConnect/Art/Themes/") && !assetPath.StartsWith("Assets/OnetConnect/Art/Cute/") && !assetPath.StartsWith("Assets/OnetConnect/Art/UI/")) return;
            var imp = (TextureImporter)assetImporter;
            bool backdrop = assetPath.EndsWith("/bg.png") || System.IO.Path.GetFileName(assetPath).StartsWith("bg_");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = backdrop ? 64f : 256f;
            if (assetPath.EndsWith("/hand.png"))
            {
                // the hand art keeps its fingertip at the horizontal centre, near the top
                var ts = new TextureImporterSettings(); imp.ReadTextureSettings(ts);
                ts.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(ts);
                imp.spritePivot = new Vector2(0.5f, 0.984f);
            }
            if (System.IO.Path.GetFileName(assetPath).StartsWith("ui9_"))
            {
                imp.spritePixelsPerUnit = 100f;      // 128 px source -> 1.28 canvas units; borders stay in canvas px
                string fn = System.IO.Path.GetFileName(assetPath);
                float b = fn.StartsWith("ui9_bar") ? 15f : fn.StartsWith("ui9_pill") ? 60f : 44f;   // bar/pill borders equal their end-cap radius
                imp.spriteBorder = new Vector4(b, b, b, b);
                imp.maxTextureSize = 128;
            }
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = !backdrop;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = backdrop ? 1024 : 256;
            imp.textureCompression = backdrop ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        }
    }
}
