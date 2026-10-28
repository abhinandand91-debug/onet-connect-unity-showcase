using System.IO;
using System.Linq;
using Onet.Data;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Setup ▸ Build Themes
    /// Turns each Art/Themes/&lt;Name&gt; folder (numbered icon PNGs + bg.png) into a TileTheme asset
    /// and a SpriteAtlas, packs the shared tile chrome into a Common atlas, and assigns the themes to chapters.
    /// Run after the PNGs have been imported (a fresh editor process, or after Refresh) so sprites resolve.
    /// </summary>
    public static class ThemeBuilder
    {
        const string Root = "Assets/OnetConnect";
        const string Themes = Root + "/Art/Themes";
        const string Data = Root + "/Data";
        const string Atlases = Root + "/Atlases";

        struct Def { public string folder, id, display; public Color panel, line; public Color sel; }

        static readonly Def[] Defs =
        {
            new Def { folder = "Fruits",  id = "fruits",  display = "Fruits",  panel = new Color(0.20f, 0.36f, 0.22f, 0.80f), line = new Color(1f, 0.82f, 0.30f), sel = new Color(1f, 0.93f, 0.55f) },
            new Def { folder = "Sweets",  id = "sweets",  display = "Sweets",  panel = new Color(0.55f, 0.28f, 0.38f, 0.80f), line = new Color(1f, 0.95f, 0.55f), sel = new Color(1f, 0.93f, 0.62f) },
            new Def { folder = "Flowers", id = "flowers", display = "Flowers", panel = new Color(0.30f, 0.32f, 0.55f, 0.80f), line = new Color(1f, 0.86f, 0.45f), sel = new Color(1f, 0.93f, 0.58f) },
        };

        [MenuItem("Onet/Setup/Build Themes")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Atlases);

            var tileBg = Load<Sprite>(Themes + "/Common/tile_bg.png");
            var frame = Load<Sprite>(Themes + "/Common/tile_frame.png");
            var star = Load<Sprite>(Themes + "/Common/path_star.png");
            if (tileBg == null || frame == null || star == null) { Debug.LogError("Theme chrome sprites did not import"); return; }

            var db = Load<LevelDatabase>(Data + "/LevelDatabase.asset");
            var built = new TileTheme[Defs.Length];
            for (int d = 0; d < Defs.Length; d++)
            {
                var def = Defs[d];
                string folder = $"{Themes}/{def.folder}";
                var icons = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.EndsWith("/bg.png"))
                    .OrderBy(p => p, System.StringComparer.Ordinal)
                    .Select(Load<Sprite>).Where(s => s != null).ToArray();

                string assetPath = $"{Data}/Theme_{def.folder}.asset";
                var theme = Load<TileTheme>(assetPath);
                if (theme == null) { theme = ScriptableObject.CreateInstance<TileTheme>(); AssetDatabase.CreateAsset(theme, assetPath); }
                theme.themeId = def.id; theme.displayName = def.display;
                theme.icons = icons;
                theme.tileBackground = tileBg; theme.selectedFrame = frame; theme.pathStarSprite = star;
                theme.background = Load<Sprite>(folder + "/bg.png");
                theme.boardPanelColor = def.panel; theme.lineColor = def.line; theme.selectedTint = def.sel;
                EditorUtility.SetDirty(theme);
                built[d] = theme;

                MakeAtlas($"{Atlases}/Atlas_{def.folder}.spriteatlasv2", icons);
                Debug.Log($"Theme {def.display}: {icons.Length} icons");
            }
            MakeAtlas($"{Atlases}/Atlas_Common.spriteatlasv2", new[] { tileBg, frame, star });

            FontBuilder.Build();
            var cute = BuildCute();
            if (db != null)
            {
                var use = cute != null ? cute : built;
                for (int c = 0; c < db.chapters.Count && c < use.Length; c++)
                {
                    db.chapters[c].theme = use[c];
                    if (cute != null) db.chapters[c].title = use[c].displayName;
                }
                EditorUtility.SetDirty(db);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Onet themes built.");
        }

        // ------------------------------------------------------------------ Cute Onet pack (clay animals, treats, magic, sea)

        const string Cute = Root + "/Art/Cute";

        static Sprite[] LoadSprites(string folder) => AssetDatabase.FindAssets("t:Sprite", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal).Select(Load<Sprite>).Where(s => s != null).ToArray();

        /// <summary>
        /// Rebuilds only the three Cute chapter themes (icons, happy faces, collections, atlases) after icons are added
        /// to Art/Cute. Unlike <see cref="Build"/> it leaves fonts and the legacy themes alone; the theme assets are
        /// updated in place, so the chapters keep their references.
        /// </summary>
        [MenuItem("Onet/Setup/Rebuild Cute Themes")]
        public static void RebuildCuteThemes()
        {
            var themes = BuildCute();
            AssetDatabase.SaveAssets();
            Debug.Log(themes != null ? $"[Onet] cute themes rebuilt: {themes[0].IconCount} icons" : "[Onet] cute pack missing");
        }

        /// <summary>Three chapter skins (Meadow / Sunset / Dream) sharing the cute icons; each differs in backdrop and board colour.</summary>
        static readonly Color[] TileTints =
        {
            new Color(1.00f, 0.78f, 0.72f), new Color(0.84f, 0.76f, 1.00f), new Color(1.00f, 0.90f, 0.55f), new Color(0.70f, 0.88f, 1.00f),
            new Color(0.70f, 0.95f, 0.82f), new Color(1.00f, 0.82f, 0.60f), new Color(0.82f, 0.95f, 0.65f), new Color(1.00f, 0.78f, 0.88f),
        };

        static IconCategory[] BuildCategories(Sprite[] icons)
        {
            var groups = new (string name, Color dot, string[] members)[]
            {
                ("Animals", new Color(0.96f, 0.50f, 0.46f), new[] { "panda", "fox", "frog", "chick", "piglet", "bunny", "bear", "cat", "lion", "puppy", "koala", "penguin" }),
                ("Treats",  new Color(0.98f, 0.76f, 0.30f), new[] { "strawberry", "watermelon", "orange", "donut", "cupcake", "icecream" }),
                ("Magic",   new Color(0.62f, 0.46f, 0.88f), new[] { "star", "rainbow", "mushroom", "flower", "planet", "crystal" }),
                ("More",    new Color(0.30f, 0.66f, 0.90f), new[] { "whale", "octopus", "turtle", "crab", "seahorse", "elephant",
                                                                    "raccoon", "tiger", "sheep", "owl", "deer", "giraffe" }),
            };
            var result = new IconCategory[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                var idx = new System.Collections.Generic.List<int>();
                for (int i = 0; i < icons.Length; i++)
                {
                    string n = icons[i].name; int us = n.IndexOf('_');
                    if (us >= 0) n = n.Substring(us + 1);
                    if (System.Array.IndexOf(groups[g].members, n) >= 0) idx.Add(i);
                }
                result[g] = new IconCategory { name = groups[g].name, dot = groups[g].dot, indices = idx.ToArray() };
            }
            return result;
        }

        static TileTheme[] BuildCute()
        {
            var icons = LoadSprites(Cute + "/Icons");
            var happy = LoadSprites(Cute + "/Happy");
            var tiles = new { def = Load<Sprite>(Cute + "/Tiles/tile_default.png"), sel = Load<Sprite>(Cute + "/Tiles/tile_selected.png"), hint = Load<Sprite>(Cute + "/Tiles/tile_hint.png") };
            var star = Load<Sprite>(Themes + "/Common/path_star.png");
            if (icons.Length == 0 || tiles.def == null) { Debug.LogWarning("Cute pack not imported; keeping legacy themes"); return null; }

            var skins = new[]
            {
                (name: "Meadow", bg: "bg_meadow.png", panel: new Color(0.30f, 0.52f, 0.46f, 0.78f), line: new Color(1f, 0.78f, 0.30f)),
                (name: "Sunset", bg: "bg_sunset.png", panel: new Color(0.72f, 0.48f, 0.38f, 0.78f), line: new Color(1f, 0.95f, 0.50f)),
                (name: "Dream",  bg: "bg_dream.png",  panel: new Color(0.42f, 0.40f, 0.66f, 0.78f), line: new Color(1f, 0.86f, 0.45f)),
            };
            var result = new TileTheme[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                string path = $"{Data}/Theme_Cute{skins[i].name}.asset";
                var t = Load<TileTheme>(path);
                if (t == null) { t = ScriptableObject.CreateInstance<TileTheme>(); AssetDatabase.CreateAsset(t, path); }
                t.themeId = "cute_" + skins[i].name.ToLowerInvariant(); t.displayName = "Cute " + skins[i].name;
                t.icons = icons; t.happyIcons = happy;
                t.categories = BuildCategories(icons);
                t.tileTints = TileTints;
                t.tileBackground = tiles.def; t.tileSelected = tiles.sel; t.tileHint = tiles.hint; t.selectedFrame = null; t.pathStarSprite = star;
                t.background = Load<Sprite>(Cute + "/Backdrops/" + skins[i].bg);
                t.boardPanelColor = skins[i].panel; t.lineColor = skins[i].line;
                EditorUtility.SetDirty(t);
                result[i] = t;
            }

            MakeAtlas($"{Atlases}/Atlas_CuteIcons.spriteatlasv2", icons);
            MakeAtlas($"{Atlases}/Atlas_CuteHappy.spriteatlasv2", happy);
            MakeAtlas($"{Atlases}/Atlas_CuteChrome.spriteatlasv2", LoadSprites(Cute + "/Tiles").Concat(LoadSprites(Cute + "/Powerups")).ToArray());

            var config = Load<GameConfig>(Data + "/GameConfig.asset");
            if (config != null)
            {
                for (int b = 0; b < config.boosters.Count; b++)
                {
                    var def = config.boosters[b];
                    string file = def.type == BoosterType.Hint ? "hint" : def.type == BoosterType.Shuffle ? "shuffle" : def.type == BoosterType.AutoMatch ? "remove" : "time";
                    def.icon = Load<Sprite>($"{Cute}/Powerups/{file}.png");
                    config.boosters[b] = def;
                }
                EditorUtility.SetDirty(config);
            }
            Debug.Log($"Cute themes: {icons.Length} icons, {happy.Length} happy variants");
            return result;
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        static void MakeAtlas(string path, Sprite[] sprites)
        {
            var asset = new SpriteAtlasAsset();
            asset.SetIncludeInBuild(true);
            asset.Add(sprites.Cast<Object>().ToArray());
            SpriteAtlasAsset.Save(asset, path);
            AssetDatabase.ImportAsset(path);
            var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            imp.packingSettings = new SpriteAtlasPackingSettings { padding = 4, enableRotation = false, enableTightPacking = false, blockOffset = 1 };
            imp.textureSettings = new SpriteAtlasTextureSettings { generateMipMaps = false, filterMode = FilterMode.Bilinear, sRGB = true, readable = false };
            var plat = imp.GetPlatformSettings("DefaultTexturePlatform");
            plat.maxTextureSize = 2048;
            plat.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformSettings(plat);
            imp.SaveAndReimport();
        }
    }
}
