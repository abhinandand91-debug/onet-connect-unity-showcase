using System.IO;
using Onet.Data;
using Onet.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Setup ▸ Create Demo Project
    /// One click → playable game with placeholder art:
    ///   • 32 generated placeholder icons + tile/frame/star sprites
    ///   • GameConfig, ProgressionCurve, TileTheme, LevelDatabase (3 chapters)
    ///   • 60 generated, baked &amp; validated levels (via the batch pipeline)
    ///   • Tile prefab, PathView, Game scene wired up (HUD is optional)
    /// Replace the placeholder theme with real art later – nothing else changes.
    /// </summary>
    public static class DemoProjectBuilder
    {
        const string Root = "Assets/OnetConnect";
        const string Art = Root + "/Art/Placeholder";
        const string Data = Root + "/Data";

        [MenuItem("Onet/Setup/Create Demo Project")]
        public static void Build()
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Root + "/Levels");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");

            // ---- sprites
            var icons = new Sprite[32];
            for (int i = 0; i < icons.Length; i++) icons[i] = MakeSprite($"icon_{i + 1:00}", 128, (x, y) => IconPixel(i, x, y, 128));
            var tileBg = MakeSprite("tile_bg", 128, (x, y) => RoundedRect(x, y, 128, 16, new Color32(250, 246, 232, 255), new Color32(214, 206, 186, 255)));
            var frame  = MakeSprite("tile_frame", 128, (x, y) => RoundedRect(x, y, 128, 16, new Color32(0, 0, 0, 0), new Color32(255, 214, 64, 255), 8));
            var star   = MakeSprite("path_star", 64, (x, y) => StarPixel(x, y, 64));
            var panel  = MakeSprite("board_panel", 64, (x, y) => RoundedRect(x, y, 64, 12, new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)), 12);

            // ---- data assets
            var theme = CreateOrLoad<TileTheme>(Data + "/Theme_Placeholder.asset");
            theme.icons = icons; theme.tileBackground = tileBg; theme.selectedFrame = frame; theme.pathStarSprite = star;
            EditorUtility.SetDirty(theme);

            var config = CreateOrLoad<GameConfig>(Data + "/GameConfig.asset");
            var curve = CreateOrLoad<ProgressionCurve>(Data + "/ProgressionCurve.asset");
            var db = CreateOrLoad<LevelDatabase>(Data + "/LevelDatabase.asset");
            db.chapters.Clear();
            db.chapters.Add(new Chapter { title = "Fruit Garden", theme = theme, firstLevel = 1, lastLevel = 20 });
            db.chapters.Add(new Chapter { title = "Sweet Shop", theme = theme, firstLevel = 21, lastLevel = 40 });
            db.chapters.Add(new Chapter { title = "Flower Market", theme = theme, firstLevel = 41, lastLevel = 60 });
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            // ---- levels (reuse batch pipeline)
            LevelBatchWindow.GenerateHeadless(curve, db, Root + "/Levels", 1, 60);
            db.endlessPool.Clear();
            for (int i = 40; i < db.levels.Count; i++) db.endlessPool.Add(db.levels[i]);
            EditorUtility.SetDirty(db);

            // ---- prefabs
            var tilePrefab = BuildTilePrefab();
            var starPrefab = BuildStarPrefab(star);

            // ---- scene
            BuildScene(db, config, tilePrefab, starPrefab, panel);
            AssetDatabase.SaveAssets();
            Debug.Log("Onet demo project created. Open Scenes/Game and press Play (or Onet ▸ Level Editor ▸ ▶ Play Level).");
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(LevelDatabase db, GameConfig config, TileView tilePrefab, SpriteRenderer starPrefab, Sprite panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.82f, 0.74f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var boardGo = new GameObject("Board");
            var panelSr = new GameObject("Panel").AddComponent<SpriteRenderer>();
            panelSr.transform.SetParent(boardGo.transform, false);
            panelSr.sprite = panel;
            panelSr.sortingOrder = -10;
            var boardView = boardGo.AddComponent<BoardView>();
            SetRef(boardView, "tilePrefab", tilePrefab);
            SetRef(boardView, "boardPanel", panelSr);
            SetRef(boardView, "cam", cam);

            var pathGo = new GameObject("PathView");
            var lr = pathGo.AddComponent<LineRenderer>();
            lr.sharedMaterial = LineMaterial();
            var pathView = pathGo.AddComponent<PathView>();
            SetRef(pathView, "starPrefab", starPrefab);

            var gameGo = new GameObject("Game");
            var timer = gameGo.AddComponent<LevelTimer>();
            var game = gameGo.AddComponent<GameController>();
            SetRef(game, "database", db);
            SetRef(game, "config", config);
            SetRef(game, "boardView", boardView);
            SetRef(game, "pathView", pathView);
            SetRef(game, "timer", timer);

            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Game.unity");
        }

        static Material LineMaterial()
        {
            string path = Data + "/PathLine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static TileView BuildTilePrefab()
        {
            var go = new GameObject("Tile");
            var shadow = Child(go, "Shadow", 0);
            shadow.color = new Color(0, 0, 0, 0.25f);
            shadow.transform.localPosition = new Vector3(0.03f, -0.05f, 0);
            var bg = Child(go, "Bg", 1);
            var icon = Child(go, "Icon", 2);
            var frame = Child(go, "Frame", 3);
            frame.enabled = false;
            var tv = go.AddComponent<TileView>();
            SetRef(tv, "shadow", shadow);
            SetRef(tv, "background", bg);
            SetRef(tv, "icon", icon);
            SetRef(tv, "frame", frame);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Tile.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<TileView>();
        }

        static SpriteRenderer Child(GameObject parent, string n, int order)
        {
            var c = new GameObject(n).AddComponent<SpriteRenderer>();
            c.transform.SetParent(parent.transform, false);
            c.sortingOrder = order;
            return c;
        }

        static SpriteRenderer BuildStarPrefab(Sprite star)
        {
            var go = new GameObject("PathStar");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = star;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/PathStar.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<SpriteRenderer>();
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"{target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ------------------------------------------------------------------ placeholder art

        delegate Color32 PixelFn(int x, int y);

        static Sprite MakeSprite(string name, int size, PixelFn fn, int border = 0)
        {
            string path = $"{Art}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) px[y * size + x] = fn(x, y);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = size;
            imp.alphaIsTransparency = true;
            if (border > 0) imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>32 distinct icons: 8 hues × 4 shapes (circle, square, diamond, ring).</summary>
        static Color32 IconPixel(int index, int x, int y, int s)
        {
            float cx = x - s / 2f + 0.5f, cy = y - s / 2f + 0.5f, r = s * 0.36f;
            int shape = index / 8;
            Color c = Color.HSVToRGB((index % 8) / 8f, 0.7f, 0.95f);
            bool inside;
            switch (shape)
            {
                case 0: inside = cx * cx + cy * cy <= r * r; break;
                case 1: inside = Mathf.Abs(cx) <= r * 0.85f && Mathf.Abs(cy) <= r * 0.85f; break;
                case 2: inside = Mathf.Abs(cx) + Mathf.Abs(cy) <= r * 1.2f; break;
                default: float d = Mathf.Sqrt(cx * cx + cy * cy); inside = d <= r && d >= r * 0.55f; break;
            }
            if (!inside) return new Color32(0, 0, 0, 0);
            float shade = 1f - (cy + r) / (4f * r); // subtle top-light gradient
            return (Color32)new Color(c.r * (0.8f + 0.3f * (1 - shade)), c.g * (0.8f + 0.3f * (1 - shade)), c.b * (0.8f + 0.3f * (1 - shade)), 1f);
        }

        static Color32 RoundedRect(int x, int y, int s, int radius, Color32 fill, Color32 edge, int edgeWidth = 4)
        {
            float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (s - 1 - radius)));
            float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (s - 1 - radius)));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > radius) return new Color32(0, 0, 0, 0);
            bool border = x < edgeWidth || y < edgeWidth || x >= s - edgeWidth || y >= s - edgeWidth || d > radius - edgeWidth;
            return border ? edge : fill;
        }

        static Color32 StarPixel(int x, int y, int s)
        {
            float cx = x - s / 2f + 0.5f, cy = y - s / 2f + 0.5f;
            float ang = Mathf.Atan2(cy, cx), dist = Mathf.Sqrt(cx * cx + cy * cy);
            float k = Mathf.Cos(5f * (ang - Mathf.PI / 2f)) * 0.5f + 0.5f;
            float edge = s * (0.22f + 0.24f * k);
            return dist <= edge ? new Color32(255, 214, 64, 255) : new Color32(0, 0, 0, 0);
        }
    }
}
