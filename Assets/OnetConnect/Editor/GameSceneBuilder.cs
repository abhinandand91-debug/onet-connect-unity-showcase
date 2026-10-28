using System.Collections.Generic;
using System.IO;
using Onet.Data;
using Onet.Runtime;
using Onet.Runtime.Juice;
using Onet.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Onet.EditorTools
{
    /// <summary>
    /// Onet ▸ Setup ▸ Build Full Game
    /// Runs the demo-project builder, then adds the HUD canvas (top bar, star bar, booster bar,
    /// win/lose/pause popups, toast), the particle FX director and procedural SFX to Game.unity.
    /// Idempotent: re-running rebuilds the scene from scratch.
    /// </summary>
    public static class GameSceneBuilder
    {
        const string Root = "Assets/OnetConnect";
        const string Art = Root + "/Art/Placeholder";
        const string ScenePath = Root + "/Scenes/Game.unity";

        static readonly Color Ink = new Color(0.16f, 0.2f, 0.18f);
        static readonly Color Teal = new Color(0.10f, 0.32f, 0.33f);
        static readonly Color Glass = new Color(1f, 0.99f, 0.93f, 0.78f);
        static readonly Color Coral = new Color(0.96f, 0.45f, 0.42f);
        static readonly Color Cream = new Color(0.98f, 0.96f, 0.88f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);

        static Sprite _round, _circle, _starOn, _starOff, _card, _tray, _btnCream, _btnCoral, _coin, _heart, _pill, _padlock, _bar;
        static TMP_FontAsset _font, _fontBody;

        /// <summary>
        /// Menu entry. Pass 1 creates the assets; pass 2 runs after the import settles because
        /// references to assets created in the same editor frame can serialise as null.
        /// From the command line call <see cref="CreateAssets"/> and <see cref="Wire"/> as two -executeMethod runs.
        /// </summary>
        [MenuItem("Onet/Setup/Build Full Game (HUD + FX)")]
        public static void BuildAll()
        {
            CreateAssets();
            // CreateAssets resets the chapter themes, so themes (and fonts) must be rebuilt before the scene is wired
            EditorApplication.delayCall += () =>
            {
                ThemeBuilder.Build();
                EditorApplication.delayCall += () => EditorApplication.delayCall += Wire;
            };
        }

        static void WireBoosterUnlock()
        {
            var hud = Object.FindFirstObjectByType<HUDController>();
            if (hud == null) { Debug.LogError("[Onet] no HUDController in the open scene"); return; }
            var hso = new SerializedObject(hud);
            hso.FindProperty("padlockOpen").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/padlock_open.png");
            hso.FindProperty("sfx").objectReferenceValue = Object.FindFirstObjectByType<SfxPlayer>();
            hso.ApplyModifiedPropertiesWithoutUndo();
            if (hso.FindProperty("padlockOpen").objectReferenceValue == null) Debug.LogError("[Onet] padlock_open.png did not load as a sprite");
        }

        /// <summary>
        /// Gives the board as much of the screen as possible, so big grids keep readable tiles: the header and status
        /// row pack against the top, tutorial captions and toasts appear
        /// briefly over the status row instead of keeping their own lane, and the combo label floats over the board edge.
        /// Runs after the HUD, popups and tutorial are built (it moves objects they create), and on existing scenes via
        /// the menu. Positions are in reference pixels (1080x1920).
        /// </summary>
        static void CompactHud()
        {
            var hud = Object.FindFirstObjectByType<HUDController>();
            if (hud == null) { Debug.LogError("[Onet] no HUDController in the open scene"); return; }
            var safe = hud.transform.Find("SafeArea");

            RectTransform Find(Transform root, string path)
            {
                var t = root.Find(path) as RectTransform;
                if (t == null) Debug.LogError("[Onet] CompactHud: missing " + path);
                return t;
            }
            void Place(string path, Vector2 pos, Vector2? size = null)
            {
                var rt = Find(safe, path);
                if (rt == null) return;
                rt.anchoredPosition = pos;
                if (size.HasValue) rt.sizeDelta = size.Value;
            }
            void PinTop(RectTransform rt, Vector2 pos, Vector2 size)
            {
                if (rt == null) return;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            // header: eyebrow, title and coins in the top 160 px (the title and coin pill are placed by their top edge)
            Place("Chapter", new Vector2(0, -36));
            Place("Level", new Vector2(0, -62), new Vector2(460, 96));
            Place("CoinPill", new Vector2(-50, -54));

            // status row straight under it; the cards and pause button are placed by their top edge: -176 .. -348
            Place("PairsCard", new Vector2(60, -176));
            Place("TimeCard", new Vector2(370, -176), new Vector2(510, 172));

            // pause: a small cream button centred on the row, so it no longer outshouts the board (120 x 120)
            Place("Pause", new Vector2(-60, -202), new Vector2(120, 120));
            var pause = Find(safe, "Pause");
            if (pause != null)
            {
                var cream = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_btn_cream.png");
                if (cream != null) pause.GetComponent<Image>().sprite = cream;
                var glyph = pause.Find("Glyph") as RectTransform;
                if (glyph != null)
                {
                    var t = glyph.GetComponent<TMP_Text>();
                    t.color = Teal; t.fontSize = 52;
                    glyph.sizeDelta = new Vector2(110, 80);
                }
            }

            // the combo label gets its own band under the row (-351 .. -421), so it never covers the cards or tiles
            Place("Combo", new Vector2(0, -386));

            // board space: from under the combo band to just above the power-ups
            Place("BoardTopLimit", new Vector2(0, -428));
            Place("BoardBottomLimit", new Vector2(0, 300));

            // captions and toasts are short-lived: show them over the status row, which they cover exactly
            PinTop(Find(safe, "Caption"), new Vector2(0, -174), new Vector2(940, 176));
            PinTop(Find(safe, "Toast"), new Vector2(0, -206), new Vector2(760, 110));

            // no tile-set picker: the leading tile set rotates with the level (TileTheme.ApplyForLevel). Remove the
            // tabs from scenes that still have them, and give the pause card back its original size.
            var card = Find(hud.transform, "Popups/PausePanel/Card");
            foreach (var t in new[] { safe.Find("Tabs"), card?.Find("Tabs"), card?.Find("TilesCaption") })
                if (t != null) Object.DestroyImmediate(t.gameObject);
            if (card != null)
            {
                card.sizeDelta = new Vector2(800, 640);
                Find(card, "Title").anchoredPosition = new Vector2(0, -90);
                Find(card, "Resume").anchoredPosition = new Vector2(0, 0);
                Find(card, "Retry").anchoredPosition = new Vector2(0, -170);
            }
        }

        /// <summary>Power-up captions follow the config (e.g. "+10s" from addTimeSeconds).</summary>
        static void SyncBoosterLabels()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Data/GameConfig.asset");
            var label = GameObject.Find("Name_" + BoosterType.AddTime);
            if (cfg != null && label != null) label.GetComponent<TMP_Text>().text = $"+{cfg.addTimeSeconds:0}s";
        }

        /// <summary>A full-screen confetti layer on top of the win card, played by <see cref="ResultPopup"/>.</summary>
        static void AddWinConfetti()
        {
            var popup = Object.FindFirstObjectByType<ResultPopup>(FindObjectsInactive.Include);
            var win = popup != null ? popup.transform.Find("WinPanel") : null;
            if (win == null) { Debug.LogError("[Onet] no WinPanel for confetti"); return; }
            var old = win.Find("Confetti");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var layer = Stretch("Confetti", win);
            layer.SetAsLastSibling();
            var burst = layer.gameObject.AddComponent<ConfettiBurst>();
            var so = new SerializedObject(popup);
            so.FindProperty("confetti").objectReferenceValue = burst;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Applies the HUD layout, power-up labels and win confetti to the existing Game scene.</summary>
        [MenuItem("Onet/Setup/Apply HUD Polish")]
        public static void PolishGameScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Display) ?? TMP_Settings.defaultFontAsset;
            _fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Body) ?? _font;
            CompactHud();
            SyncBoosterLabels();
            AddWinConfetti();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Onet] HUD polish applied");
        }

        /// <summary>Deletes the header back button from the existing Game scene (see BuildHud for why there is none).</summary>
        [MenuItem("Onet/Setup/Remove Back Button")]
        public static void RemoveBackButton()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hud = Object.FindFirstObjectByType<HUDController>();
            Transform back = null;
            if (hud != null)
                foreach (var t in hud.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Back" && t.GetComponent<Button>() != null) { back = t; break; }
            bool found = back != null;   // check before destroying: a destroyed object compares equal to null
            if (found) Object.DestroyImmediate(back.gameObject);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(found ? "[Onet] back button removed" : "[Onet] no back button found");
        }

        /// <summary>Wires the open padlock and SFX for the booster unlock into the existing Game scene.</summary>
        [MenuItem("Onet/Setup/Wire Booster Unlock")]
        public static void WireBoosterUnlockInScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireBoosterUnlock();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Onet] booster unlock wired");
        }

        /// <summary>Adds <see cref="AdaptiveCanvasScaler"/> to the screen-size-scaled canvases of the existing scenes.</summary>
        [MenuItem("Onet/Setup/Add Adaptive Canvas Scalers")]
        public static void AddAdaptiveScalers()
        {
            foreach (var path in new[] { Root + "/Scenes/Boot.unity", Root + "/Scenes/Game.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int added = 0;
                foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.GetComponent<AdaptiveCanvasScaler>()) continue;
                    scaler.gameObject.AddComponent<AdaptiveCanvasScaler>();
                    added++;
                }
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Onet] {path}: {added} adaptive scaler(s) added");
            }
        }

        public static void CreateAssets()
        {
            DemoProjectBuilder.Build();
            _round = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/board_panel.png");
            MakeSprite("circle", 128, (x, y) => Circle(x, y, 128));
            MakeSprite("star_off", 256, (x, y) => Gray(StarPixel(x, y, 256)));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void Wire()
        {
            _round = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/board_panel.png");
            _starOn = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Themes/Common/path_star.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/path_star.png");
            _circle = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/circle.png");
            _starOff = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/star_off.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/star_off.png");
            _card = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_card.png");
            _tray = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_tray.png");
            _btnCream = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_btn_cream.png");
            _btnCoral = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_btn_coral.png");
            _pill = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_pill.png");
            _padlock = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/padlock.png");
            _bar = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_bar.png") ?? _pill;
            _coin = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/coin.png");
            _heart = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/heart.png");
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Display) ?? TMP_Settings.defaultFontAsset;
            _fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Body) ?? _font;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var name in new[] { "HUD", "Juice", "EventSystem", "Tutorial" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            var game = Object.FindFirstObjectByType<GameController>();
            var boardView = Object.FindFirstObjectByType<BoardView>();
            var cam = Camera.main;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            // re-resolve every asset reference from disk
            var gso = new SerializedObject(game);
            gso.FindProperty("database").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelDatabase>(Root + "/Data/LevelDatabase.asset");
            gso.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Data/GameConfig.asset");
            gso.ApplyModifiedPropertiesWithoutUndo();
            var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Tile.prefab").GetComponent<TileView>();

            var bso = new SerializedObject(boardView);
            bso.FindProperty("tilePrefab").objectReferenceValue = tilePrefab;
            bso.FindProperty("fitArea").vector2Value = new Vector2(14f, 12f);   // upper bounds; real fit comes from the HUD limits
            var bd = GameObject.Find("Backdrop");
            if (bd != null) Object.DestroyImmediate(bd);
            bd = new GameObject("Backdrop");
            var bdr = bd.AddComponent<SpriteRenderer>();
            bdr.sortingOrder = -100;
            bso.FindProperty("backdrop").objectReferenceValue = bdr;
            bso.ApplyModifiedPropertiesWithoutUndo();
            boardView.transform.position = new Vector3(0, -0.2f, 0);
            cam.backgroundColor = new Color(0.62f, 0.85f, 0.76f);
            if (gso.FindProperty("database").objectReferenceValue == null || gso.FindProperty("config").objectReferenceValue == null || tilePrefab == null)
                Debug.LogError("Onet wiring failed: a core asset did not load");

            BuildEventSystem();
            BuildHud(game);
            var lso = new SerializedObject(boardView);
            lso.FindProperty("topLimit").objectReferenceValue = _topLimit;
            lso.FindProperty("bottomLimit").objectReferenceValue = _bottomLimit;
            lso.ApplyModifiedPropertiesWithoutUndo();
            BuildJuice(game, boardView, cam);
            BuildTutorial(game, boardView, cam);

            WireBoosterUnlock();
            CompactHud();
            SyncBoosterLabels();
            AddWinConfetti();

            var popup = Object.FindFirstObjectByType<ResultPopup>();
            var pso = new SerializedObject(popup);
            pso.FindProperty("sfx").objectReferenceValue = Object.FindFirstObjectByType<SfxPlayer>();
            pso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildBoot();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),      // index 0: the app always starts in Boot
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Onet full game built: " + ScenePath);
        }

        // ------------------------------------------------------------------ boot scene

        static readonly Color[] MascotTints =
        {
            new Color(0.98f, 0.66f, 0.60f), new Color(0.78f, 0.68f, 0.94f), new Color(1f, 0.86f, 0.48f), new Color(0.56f, 0.82f, 0.96f)
        };

        const string BootScenePath = Root + "/Scenes/Boot.unity";

        /// <summary>Boot: persistent AppServices + a loading screen that hands over to Game through the SceneLoader.</summary>
        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.85f, 0.76f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<Onet.Runtime.Boot.AppServices>();
            var aso = new SerializedObject(app);
            aso.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Data/GameConfig.asset");
            aso.FindProperty("database").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelDatabase>(Root + "/Data/LevelDatabase.asset");
            aso.FindProperty("persistent").boolValue = true;
            aso.ApplyModifiedPropertiesWithoutUndo();

            var canvasGo = new GameObject("BootCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<AdaptiveCanvasScaler>();

            var bg = Img("Background", canvasGo.transform, AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Cute/Backdrops/bg_meadow.png"), Color.white);
            bg.type = Image.Type.Simple;
            Stretch(bg.rectTransform, 0);
            var safe = Stretch("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var eyebrow = Label("Eyebrow", safe, "CLAY COLLECTION", 40, TextAlignmentOptions.Center, Muted, new Vector2(0.5f, 0.5f), new Vector2(0, 600), new Vector2(800, 60));
            eyebrow.characterSpacing = 14f;
            Label("Title1", safe, "Onet", 230, TextAlignmentOptions.Center, Teal, new Vector2(0.5f, 0.5f), new Vector2(0, 450), new Vector2(900, 240));
            Label("Title2", safe, "Connect", 190, TextAlignmentOptions.Center, new Color(0.96f, 0.50f, 0.46f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(900, 200));

            // mascots bounce while loading
            string[] names = BootMascots;
            var mascots = new RectTransform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Art/Cute/Icons/{names[i]}.png");
                var tile = Box("Mascot_" + i, safe, AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Cute/Tiles/tile_default.png"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), new Vector2((i - 1.5f) * 230, -170), new Vector2(200, 200), MascotTints[i]);
                Box("Icon", tile, spr, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170), Color.white);
                mascots[i] = tile;
            }

            var status = Label("Status", safe, "Loading…", 42, TextAlignmentOptions.Center, Teal, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(800, 60));
            Label("Version", safe, "v" + Application.version, 30, TextAlignmentOptions.Center, Muted, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(400, 40));

            var loader = canvasGo.AddComponent<Onet.Runtime.Boot.BootLoader>();
            var lso = new SerializedObject(loader);
            BuildLoadingBar(safe, lso);
            WireMascotFaces(lso, names);
            lso.FindProperty("status").objectReferenceValue = status;
            var mp = lso.FindProperty("mascots");
            mp.arraySize = mascots.Length;
            for (int i = 0; i < mascots.Length; i++) mp.GetArrayElementAtIndex(i).objectReferenceValue = mascots[i];
            lso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootScenePath);
            // leave the Game scene open as the working scene
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// The Boot screen's progress bar: a cream pill frame around a masked track; the gold fill (also masked, so it
        /// keeps round ends) carries scrolling stripes and a shine, a star rides its leading edge and a percentage sits
        /// on top. <see cref="Onet.Runtime.Boot.BootLoader"/> animates it.
        /// </summary>
        static void BuildLoadingBar(Transform safe, SerializedObject lso)
        {
            var center = new Vector2(0.5f, 0.5f);
            var bar = Box("LoadingBar", safe, _pill, new Vector2(0.5f, 0), center, new Vector2(0, 340), new Vector2(780, 84), Color.white);
            bar.GetComponent<Image>().pixelsPerUnitMultiplier = 120f / 84f;   // pill border is 60 px: make it exactly half the height

            var track = Box("Track", bar, _bar, center, center, Vector2.zero, new Vector2(752, 60), new Color(0.16f, 0.42f, 0.40f, 0.22f));
            track.GetComponent<Image>().pixelsPerUnitMultiplier = 30f / 60f;
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var fill = Anchored("Fill", track, new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.1f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = _bar; fillImg.type = Image.Type.Sliced; fillImg.pixelsPerUnitMultiplier = 30f / 60f;
            fillImg.color = new Color(1f, 0.76f, 0.24f); fillImg.raycastTarget = false;
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var stripesGo = new GameObject("Stripes", typeof(RectTransform), typeof(RawImage));
            var stripes = (RectTransform)stripesGo.transform;
            stripes.SetParent(fill, false);
            stripes.anchorMin = new Vector2(0, 0); stripes.anchorMax = new Vector2(0, 1); stripes.pivot = new Vector2(0, 0.5f);
            stripes.anchoredPosition = Vector2.zero; stripes.sizeDelta = new Vector2(752, 0);
            stripesGo.GetComponent<RawImage>().raycastTarget = false;

            var shine = Box("Shine", fill, _circle, new Vector2(0, 0.5f), center, Vector2.zero, new Vector2(44, 130), new Color(1f, 1f, 1f, 0.45f));
            shine.localRotation = Quaternion.Euler(0, 0, -22f);

            var star = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Themes/Common/path_star.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/path_star.png");
            var spark = Box("Spark", bar, star, center, center, Vector2.zero, new Vector2(80, 80), Color.white);
            var pct = Label("Percent", bar, "0%", 40, TextAlignmentOptions.Center, Teal, center, Vector2.zero, new Vector2(200, 60));

            foreach (var g in bar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            lso.FindProperty("bar").objectReferenceValue = bar;
            lso.FindProperty("track").objectReferenceValue = track;
            lso.FindProperty("fill").objectReferenceValue = fill;
            lso.FindProperty("stripes").objectReferenceValue = stripesGo.GetComponent<RawImage>();
            lso.FindProperty("shine").objectReferenceValue = shine;
            lso.FindProperty("spark").objectReferenceValue = spark;
            lso.FindProperty("percent").objectReferenceValue = pct;
        }

        static readonly string[] BootMascots = { "01_panda", "02_fox", "06_bunny", "04_chick" };

        /// <summary>The smiling "happy" icon each Boot mascot shows while it is in the air.</summary>
        static void WireMascotFaces(SerializedObject lso, string[] names)
        {
            var happy = lso.FindProperty("mascotHappy");
            happy.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                happy.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Art/Cute/Happy/{names[i]}.png");
        }

        /// <summary>Replaces the old thin progress bar in the existing Boot scene with <see cref="BuildLoadingBar"/>
        /// and gives the mascots their smiling jump faces.</summary>
        [MenuItem("Onet/Setup/Upgrade Boot Screen")]
        public static void UpgradeBootLoadingBar()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Display) ?? TMP_Settings.defaultFontAsset;
            _fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontBuilder.Body) ?? _font;
            _pill = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_pill.png");
            _bar = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/ui9_bar.png") ?? _pill;
            _circle = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/circle.png");

            var loader = Object.FindFirstObjectByType<Onet.Runtime.Boot.BootLoader>();
            var safe = loader != null ? loader.transform.Find("SafeArea") : null;
            if (safe == null) { Debug.LogError("[Onet] Boot scene has no BootLoader / SafeArea"); return; }
            foreach (var old in new[] { "ProgressTrack", "LoadingBar" })
            {
                var t = safe.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var lso = new SerializedObject(loader);
            BuildLoadingBar(safe, lso);
            WireMascotFaces(lso, BootMascots);
            lso.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Onet] boot loading bar upgraded");
        }

        // ------------------------------------------------------------------ tutorial

        static void BuildTutorial(GameController game, BoardView board, Camera cam)
        {
            var handSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/UI/hand.png");
            var go = new GameObject("Tutorial");
            var tut = go.AddComponent<Onet.Runtime.Tutorial.TutorialController>();

            var handGo = new GameObject("Hand");
            handGo.transform.SetParent(go.transform, false);
            var hr = handGo.AddComponent<SpriteRenderer>();
            hr.sprite = handSprite; hr.sortingOrder = 200;

            var canvas = GameObject.Find("HUD").transform;
            var safe = canvas.Find("SafeArea");
            var cap = Panel("Caption", safe, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 336), new Vector2(940, 100), new Color(0.10f, 0.32f, 0.33f, 0.92f));
            var group = cap.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var label = Label("Text", cap, "", 46, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 96));
            label.enableWordWrapping = true;
            label.fontSize = 40;

            var hint = GameObject.Find("Booster_Hint");
            var so = new SerializedObject(tut);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("hand").objectReferenceValue = hr;
            so.FindProperty("captionGroup").objectReferenceValue = group;
            so.FindProperty("captionLabel").objectReferenceValue = label;
            so.FindProperty("hintButton").objectReferenceValue = hint != null ? hint.GetComponent<RectTransform>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ juice

        static void BuildJuice(GameController game, BoardView board, Camera cam)
        {
            var go = new GameObject("Juice");
            var sfx = go.AddComponent<SfxPlayer>();
            var fx = go.AddComponent<FxDirector>();
            var so = new SerializedObject(fx);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("sfx").objectReferenceValue = sfx;
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("floatFont").objectReferenceValue = _font;
            // every effect is a small particle burst built in code (see ParticleFxFactory)
            Color gold = new Color(1f, 0.82f, 0.2f);
            Color[] party = { new Color(1f, 0.4f, 0.5f), gold, new Color(0.4f, 0.85f, 0.5f), new Color(0.4f, 0.65f, 1f), new Color(0.75f, 0.5f, 1f) };
            so.FindProperty("matchFx").objectReferenceValue = MakeFx("Match", 14, 4f, 0.35f, 0.5f, 0f, gold, Color.white);
            var combo = so.FindProperty("comboFx");
            combo.arraySize = 4;
            combo.GetArrayElementAtIndex(0).objectReferenceValue = MakeFx("Combo2", 16, 5f, 0.35f, 0.6f, 0f, new Color(0.4f, 0.65f, 1f));
            combo.GetArrayElementAtIndex(1).objectReferenceValue = MakeFx("Combo3", 20, 5.5f, 0.4f, 0.6f, 0f, new Color(0.4f, 0.85f, 0.5f));
            combo.GetArrayElementAtIndex(2).objectReferenceValue = MakeFx("Combo4", 24, 6f, 0.45f, 0.7f, 0f, new Color(1f, 0.5f, 0.75f));
            combo.GetArrayElementAtIndex(3).objectReferenceValue = MakeFx("Combo5", 32, 7f, 0.5f, 0.8f, 0f, party);
            so.FindProperty("shuffleFx").objectReferenceValue = MakeFx("Shuffle", 30, 5f, 0.4f, 0.7f, 0f, party);
            so.FindProperty("hintFx").objectReferenceValue = MakeFx("Hint", 1, 0f, 2f, 0.25f, 0f, Color.white);
            so.FindProperty("winFx").objectReferenceValue = MakeFx("Win", 60, 9f, 0.4f, 1.2f, 1f, party);
            so.FindProperty("loseFx").objectReferenceValue = MakeFx("Lose", 20, 2f, 0.6f, 1f, -0.2f, new Color(0.55f, 0.35f, 0.8f), new Color(0.45f, 0.7f, 0.4f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A one-shot burst: how many particles, how fast, how big, how long, gravity and the colours.</summary>
        static GameObject MakeFx(string name, int count, float speed, float size, float life, float gravity, params Color[] colors)
        {
            return ParticleFxFactory.Create(name, new ParticleFxFactory.Recipe
            {
                Count = count, Speed = speed, Size = size, Life = life, Gravity = gravity, Colors = colors,
            });
        }

        // ------------------------------------------------------------------ HUD

        static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        static readonly Color Muted = new Color(0.30f, 0.46f, 0.46f, 0.85f);
        static RectTransform _topLimit, _bottomLimit;

        static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Box(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var img = Img(name, parent, sprite, color);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Button RaisedButton(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color tint)
        {
            var rt = Box(name, parent, sprite, anchor, pivot, pos, size, tint);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<ButtonFeedback>();
            b.targetGraphic = img;
            return b;
        }

        static void BuildHud(GameController game)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<AdaptiveCanvasScaler>();

            var safe = Stretch("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var hud = canvasGo.AddComponent<HUDController>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("game").objectReferenceValue = game;
            var TL = new Vector2(0, 1); var TR = new Vector2(1, 1); var TC = new Vector2(0.5f, 1);

            // ---- header: chapter eyebrow + level title, coin pill. No back button: there is no screen to go back to, and
            // YouTube Playables forbids in-game exit buttons and icons that look like its own back / close controls.
            var eyebrow = Label("Chapter", safe, "CLAY MEADOW", 30, TextAlignmentOptions.Center, Muted, TC, new Vector2(0, -50), new Vector2(460, 44));
            eyebrow.characterSpacing = 10f;
            var level = Label("Level", safe, "Level 1", 88, TextAlignmentOptions.Center, Teal, TC, new Vector2(0, -92), new Vector2(460, 110));
            level.rectTransform.pivot = new Vector2(0.5f, 1f);
            level.enableAutoSizing = true; level.fontSizeMin = 48; level.fontSizeMax = 88;   // long titles shrink instead of colliding with the coin pill
            var pill = Box("CoinPill", safe, _card, TR, TR, new Vector2(-50, -58), new Vector2(260, 100), Color.white);
            Box("Coin", pill, _coin, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 3), new Vector2(76, 76), Color.white);
            var coins = Label("Coins", pill, "0", 50, TextAlignmentOptions.Left, Teal, new Vector2(0, 0.5f), new Vector2(98, 0), new Vector2(150, 80));
            coins.rectTransform.pivot = new Vector2(0, 0.5f);

            // ---- status row: pairs | time | pause (60 px margins, 30 px gaps: 280 + 470 + 150 + 60 + 120 = 1080, nothing overlaps)
            var pairs = Box("PairsCard", safe, _card, TL, TL, new Vector2(60, -362), new Vector2(280, 172), Color.white);
            Box("Heart", pairs, _heart, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(80, 80), Color.white);
            Label("PairsCaption", pairs, "PAIRS", 30, TextAlignmentOptions.Left, Muted, new Vector2(0, 0.5f), new Vector2(116, 38), new Vector2(150, 40)).rectTransform.pivot = new Vector2(0, 0.5f);
            var pairsValue = Label("PairsValue", pairs, "0/12", 72, TextAlignmentOptions.Left, Teal, new Vector2(0, 0.5f), new Vector2(116, -22), new Vector2(160, 90));
            pairsValue.rectTransform.pivot = new Vector2(0, 0.5f);
            pairsValue.richText = true;

            var timeCard = Box("TimeCard", safe, _card, TL, TL, new Vector2(370, -362), new Vector2(470, 172), Color.white);
            Label("TimeCaption", timeCard, "TIME", 30, TextAlignmentOptions.Left, Muted, new Vector2(0, 1), new Vector2(32, -32), new Vector2(160, 40)).rectTransform.pivot = new Vector2(0, 0.5f);
            var timer = Label("Timer", timeCard, "1:30", 76, TextAlignmentOptions.Left, Teal, new Vector2(0, 1), new Vector2(30, -80), new Vector2(260, 90));
            timer.rectTransform.pivot = new Vector2(0, 0.5f);
            var score = Label("Score", timeCard, "0", 44, TextAlignmentOptions.Right, Teal, new Vector2(1, 1), new Vector2(-32, -78), new Vector2(170, 56));
            score.rectTransform.pivot = new Vector2(1, 0.5f);
            Label("ScoreCaption", timeCard, "SCORE", 26, TextAlignmentOptions.Right, Muted, new Vector2(1, 1), new Vector2(-32, -32), new Vector2(170, 34)).rectTransform.pivot = new Vector2(1, 0.5f);

            // progress bar: a masked rounded track so the gold fill keeps pill-shaped ends at any value;
            // the 2-star / 3-star markers are siblings of the track so the mask doesn't clip them
            var holder = Anchored("StarBar", timeCard, new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-60, 26));
            holder.anchorMin = new Vector2(0, 0); holder.anchorMax = new Vector2(1, 0);
            holder.sizeDelta = new Vector2(-60, 26);
            var track = Img("Track", holder, _bar, new Color(0.30f, 0.46f, 0.46f, 0.20f));
            Stretch(track.rectTransform, 0);
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var fillGo = Anchored("Fill", track.transform, new Vector2(0, 0), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            fillGo.anchorMin = new Vector2(0, 0); fillGo.anchorMax = new Vector2(1, 1); fillGo.offsetMin = fillGo.offsetMax = Vector2.zero;
            var fillImg = fillGo.gameObject.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.78f, 0.26f); fillImg.raycastTarget = false;
            var s2 = Img("Star2", holder, _starOff, Color.white); s2.rectTransform.sizeDelta = new Vector2(46, 46);
            var s3 = Img("Star3", holder, _starOff, Color.white); s3.rectTransform.sizeDelta = new Vector2(46, 46);

            var pause = RaisedButton("Pause", safe, _btnCoral, TR, TR, new Vector2(-60, -358), new Vector2(150, 176), Color.white);
            Label("Glyph", pause.transform, "II", 76, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(140, 100));

            var combo = Label("Combo", safe, "Combo x2", 54, TextAlignmentOptions.Center, Coral, TC, new Vector2(0, -566), new Vector2(520, 70));

            hso.FindProperty("levelLabel").objectReferenceValue = level;
            hso.FindProperty("chapterLabel").objectReferenceValue = eyebrow;
            hso.FindProperty("coinLabel").objectReferenceValue = coins;
            hso.FindProperty("pairsLabel").objectReferenceValue = pairsValue;
            hso.FindProperty("timerLabel").objectReferenceValue = timer;
            hso.FindProperty("starFillRect").objectReferenceValue = fillGo;
            hso.FindProperty("starBarRect").objectReferenceValue = holder;
            hso.FindProperty("star2Marker").objectReferenceValue = s2;
            hso.FindProperty("star3Marker").objectReferenceValue = s3;
            hso.FindProperty("starOn").objectReferenceValue = _starOn;
            hso.FindProperty("starOff").objectReferenceValue = _starOff;
            hso.FindProperty("scoreLabel").objectReferenceValue = score;
            hso.FindProperty("comboLabel").objectReferenceValue = combo;
            hso.FindProperty("pauseButton").objectReferenceValue = pause;

            // ---- board limits (invisible): the board is fitted between these at runtime
            _topLimit = Anchored("BoardTopLimit", safe, TC, TC, new Vector2(0, -600), new Vector2(10, 0));
            _bottomLimit = Anchored("BoardBottomLimit", safe, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 440), new Vector2(10, 0));   // leaves a 100 px lane above the power-ups for tutorial captions and toasts

            // ---- power-ups sit directly on the hills
            var defs = new[]
            {
                (BoosterType.Hint, "Find", new Color(0.35f, 0.75f, 0.95f)),
                (BoosterType.Shuffle, "Shuffle", new Color(0.55f, 0.85f, 0.4f)),
                (BoosterType.AddTime, "+10s", new Color(0.98f, 0.62f, 0.3f)),
                (BoosterType.AutoMatch, "Remove", new Color(0.9f, 0.4f, 0.55f)),
            };
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Data/GameConfig.asset");
            var boosterArr = hso.FindProperty("boosterButtons");
            boosterArr.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                float ax = (i + 0.5f) / defs.Length;
                Sprite iconSprite = null;
                if (cfg != null) foreach (var bd in cfg.boosters) if (bd.type == defs[i].Item1) iconSprite = bd.icon;
                var bottomAnchor = new Vector2(ax, 0);
                RectTransform btnRt;
                Button btn;
                if (iconSprite != null)
                {
                    btn = RaisedButton("Booster_" + defs[i].Item1, safe, iconSprite, bottomAnchor, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(170, 170), Color.white);
                    var cb = btn.colors; cb.disabledColor = new Color(0.62f, 0.68f, 0.70f, 0.92f); btn.colors = cb;   // locked: greyed but still readable
                    btnRt = (RectTransform)btn.transform;
                }
                else
                {
                    btnRt = RoundButton("Booster_" + defs[i].Item1, safe, bottomAnchor, new Vector2(0, 190), 160, defs[i].Item2, Color.white, defs[i].Item3);
                    btn = btnRt.GetComponent<Button>();
                }
                Label("Name_" + defs[i].Item1, safe, defs[i].Item2, 38, TextAlignmentOptions.Center, Teal, bottomAnchor, new Vector2(0, 74), new Vector2(240, 50));
                var count = Badge("Count", btnRt, new Vector2(1, 1), new Vector2(-4, -4), "0", new Color(0.35f, 0.50f, 0.52f));
                var plus = Badge("Plus", btnRt, new Vector2(1, 1), new Vector2(-4, -4), "+", new Color(0.25f, 0.7f, 0.35f));
                // locked state: the icon stays visible (dimmed by the button's disabled tint) with a padlock and "Lv N" chip on top
                var lockRoot = Anchored("Lock", btnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
                Box("Padlock", lockRoot, _padlock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(84, 84), Color.white);
                var chip = Box("Chip", lockRoot, _pill, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(112, 44), new Color(0.12f, 0.30f, 0.33f, 0.92f));
                var lockText = Label("Text", chip, "Lv 6", 30, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 44));
                var lockIcon = lockRoot.gameObject;
                var el = boosterArr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("type").enumValueIndex = (int)defs[i].Item1;
                el.FindPropertyRelative("button").objectReferenceValue = btn;
                el.FindPropertyRelative("countLabel").objectReferenceValue = count.GetComponentInChildren<TMP_Text>();
                el.FindPropertyRelative("plusBadge").objectReferenceValue = plus;
                el.FindPropertyRelative("lockIcon").objectReferenceValue = lockIcon;
                el.FindPropertyRelative("lockLabel").objectReferenceValue = lockText;
            }

            // ---- toast
            var toast = Box("Toast", safe, _card, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(760, 90), new Color(0.1f, 0.3f, 0.32f, 0.92f));
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
            var toastLabel = Label("Text", toast, "", 40, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740, 80));
            hso.FindProperty("toastGroup").objectReferenceValue = toastGroup;
            hso.FindProperty("toastLabel").objectReferenceValue = toastLabel;
            hso.ApplyModifiedPropertiesWithoutUndo();

            BuildPopups(game, canvasGo.transform);
        }

        static void BuildPopups(GameController game, Transform canvas)
        {
            var root = Stretch("Popups", canvas);
            var popup = root.gameObject.AddComponent<ResultPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("starOn").objectReferenceValue = _starOn;
            so.FindProperty("starOff").objectReferenceValue = _starOff;

            // ---- win
            var win = Dim("WinPanel", root);
            var card = Card(win, new Vector2(860, 1120));
            Label("Title", card, "Level Clear!", 84, TextAlignmentOptions.Center, Ink, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(800, 110));
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Img("Star" + i, card, _starOff, Color.white);
                var rt = stars[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
                rt.anchoredPosition = new Vector2((i - 1) * 230, -300 + (i == 1 ? 40 : 0));
                rt.sizeDelta = new Vector2(220, 220);
            }
            var winScore = Label("Score", card, "0", 72, TextAlignmentOptions.Center, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(700, 90));
            var winCoins = Label("Coins", card, "+0", 60, TextAlignmentOptions.Center, new Color(0.8f, 0.55f, 0.05f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(700, 80));
            var next = TextButton("Next", card, new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(560, 130), "Next Level", new Color(0.35f, 0.75f, 0.4f));
            var winRetry = TextButton("Retry", card, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(560, 100), "Replay", new Color(0.55f, 0.6f, 0.65f));
            so.FindProperty("winPanel").objectReferenceValue = win.gameObject;
            var starsProp = so.FindProperty("stars");
            starsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) starsProp.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.FindProperty("winScore").objectReferenceValue = winScore;
            so.FindProperty("winCoins").objectReferenceValue = winCoins;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("winRetryButton").objectReferenceValue = winRetry;

            // ---- lose
            var lose = Dim("LosePanel", root);
            card = Card(lose, new Vector2(860, 880));
            Label("Title", card, "Time's Up!", 84, TextAlignmentOptions.Center, Ink, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(800, 110));
            Label("Sub", card, "Keep going with 30 more seconds?", 44, TextAlignmentOptions.Center, Ink, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(760, 80));
            var contCoins = TextButton("ContinueCoins", card, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(620, 130), "Continue", new Color(0.98f, 0.7f, 0.2f));
            var price = Label("Price", contCoins.transform, "150", 48, TextAlignmentOptions.Right, Ink, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(160, 80));
            var contAd = TextButton("ContinueAd", card, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(620, 130), "Watch Ad", new Color(0.4f, 0.65f, 0.95f));
            var loseRetry = TextButton("Retry", card, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(560, 110), "Retry", new Color(0.55f, 0.6f, 0.65f));
            so.FindProperty("losePanel").objectReferenceValue = lose.gameObject;
            so.FindProperty("continueCoinsButton").objectReferenceValue = contCoins;
            so.FindProperty("continueAdButton").objectReferenceValue = contAd;
            so.FindProperty("loseRetryButton").objectReferenceValue = loseRetry;
            so.FindProperty("continuePriceLabel").objectReferenceValue = price;

            // ---- pause
            var pausePanel = Dim("PausePanel", root);
            card = Card(pausePanel, new Vector2(800, 640));
            Label("Title", card, "Paused", 84, TextAlignmentOptions.Center, Ink, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(700, 110));
            var resume = TextButton("Resume", card, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(560, 130), "Resume", new Color(0.35f, 0.75f, 0.4f));
            var pauseRetry = TextButton("Retry", card, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(560, 110), "Restart", new Color(0.55f, 0.6f, 0.65f));
            so.FindProperty("pausePanel").objectReferenceValue = pausePanel.gameObject;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("pauseRetryButton").objectReferenceValue = pauseRetry;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ UI helpers

        static RectTransform Stretch(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt, 0);
            return rt;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        static RectTransform Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var img = Img(name, parent, _card != null ? _card : _round, color);
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color, Vector2 anchor, Vector2 pos, Vector2 dims)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.font = size >= 50 ? _font : _fontBody;
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = dims;
            return t;
        }

        static RectTransform RoundButton(string name, Transform parent, Vector2 anchor, Vector2 pos, float diameter, string text, Color textColor, Color fill)
        {
            var img = Img(name, parent, _circle, fill);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(diameter, diameter);
            var b = img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<ButtonFeedback>();
            b.targetGraphic = img;
            Label("Label", rt, text, 52, TextAlignmentOptions.Center, textColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));
            return rt;
        }

        static Button TextButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, Color fill)
        {
            var rt = Panel(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size, fill);
            rt.GetComponent<Image>().raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<ButtonFeedback>();
            b.targetGraphic = rt.GetComponent<Image>();
            Label("Label", rt, text, 56, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return b;
        }

        static GameObject Badge(string name, RectTransform parent, Vector2 anchor, Vector2 pos, string text, Color fill)
        {
            var img = Img(name, parent, _circle, fill);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(64, 64);
            Label("Text", rt, text, 38, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            return img.gameObject;
        }

        static RectTransform Dim(string name, Transform parent)
        {
            var img = Img(name, parent, null, new Color(0, 0, 0, 0.6f));
            img.raycastTarget = true;
            Stretch(img.rectTransform, 0);
            return img.rectTransform;
        }

        static RectTransform Card(Transform parent, Vector2 size) =>
            Panel("Card", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, Cream);

        // ------------------------------------------------------------------ generated sprites

        delegate Color32 PixelFn(int x, int y);

        static Sprite MakeSprite(string name, int size, PixelFn fn)
        {
            string path = $"{Art}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = fn(x, y);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = size;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color32 Circle(int x, int y, int s)
        {
            float cx = x - s / 2f + 0.5f, cy = y - s / 2f + 0.5f, r = s / 2f - 1f;
            float d = Mathf.Sqrt(cx * cx + cy * cy);
            float a = Mathf.Clamp01(r - d + 0.5f);
            return new Color32(255, 255, 255, (byte)(a * 255));
        }

        static Color32 StarPixel(int x, int y, int s)
        {
            float cx = x - s / 2f + 0.5f, cy = y - s / 2f + 0.5f;
            float ang = Mathf.Atan2(cy, cx), dist = Mathf.Sqrt(cx * cx + cy * cy);
            float k = Mathf.Cos(5f * (ang - Mathf.PI / 2f)) * 0.5f + 0.5f;
            float edge = s * (0.22f + 0.24f * k);
            return dist <= edge ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        }

        static Color32 Gray(Color32 c) => c.a == 0 ? c : new Color32(120, 125, 130, 255);
    }
}
