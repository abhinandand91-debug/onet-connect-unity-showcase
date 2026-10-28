using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Setup ▸ Build Fonts – turns the Baloo 2 (display) and Nunito (body) TTFs into dynamic TMP SDF assets,
    /// pre-populated with the characters the HUD uses so the atlas ships ready.
    /// </summary>
    public static class FontBuilder
    {
        const string Dir = "Assets/OnetConnect/Fonts";
        public const string Display = Dir + "/Baloo2-ExtraBold SDF.asset";
        public const string Body = Dir + "/Nunito-Bold SDF.asset";

        const string Chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~–—’…";

        [MenuItem("Onet/Setup/Build Fonts")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Make(Dir + "/Baloo2-ExtraBold.ttf", Display);
            Make(Dir + "/Nunito-Bold.ttf", Body);
            AssetDatabase.SaveAssets();
        }

        static void Make(string ttf, string output)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(output) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogError("Font not imported: " + ttf); return; }

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = System.IO.Path.GetFileNameWithoutExtension(output);
            AssetDatabase.CreateAsset(fa, output);
            if (fa.material != null) { fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            foreach (var tex in fa.atlasTextures) { tex.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            fa.TryAddCharacters(Chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"{fa.name}: no glyph for \"{missing}\"");
            EditorUtility.SetDirty(fa);
            if (fa.material != null) EditorUtility.SetDirty(fa.material);
            Debug.Log("Font asset built: " + output);
        }
    }
}
