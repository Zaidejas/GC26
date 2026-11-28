using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GC26._2048Nastya.EditorTools
{
    /// <summary>
    /// GC26 → Colours (nastya) → Rebuild assets and scene.
    /// Creates the rounded sprite and the theme (only if missing), rebuilds the Cell and Tile prefabs
    /// and the entry scene with every reference wired. Re-running it overwrites manual edits
    /// of the prefabs and the scene, so tweak colors in the theme asset instead.
    /// Batch mode: -executeMethod GC26._2048Nastya.EditorTools.ColoursBuilder2048.Build
    /// </summary>
    public static class ColoursBuilder2048
    {
        const string Root = "Assets/Microgames/gc26_2048_nastya";
        const string SpritePath = Root + "/Art/gc26_2048_nastya_RoundedRect.png";
        const string GlowSpritePath = Root + "/Art/gc26_2048_nastya_Glow.png";
        const string ThemePath = Root + "/Art/gc26_2048_nastya_Theme.asset";
        const string CellPrefabPath = Root + "/Prefabs/gc26_2048_nastya_Cell.prefab";
        const string TilePrefabPath = Root + "/Prefabs/gc26_2048_nastya_Tile.prefab";
        const string ScenePath = Root + "/Scenes/gc26_2048_nastya_Main.unity";
        const string ActionsPath = Root + "/Input/gc26_2048_nastya_Controls.inputactions";
        const string AudioDir = Root + "/Audio/";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        const int SpriteSize = 64;
        const int SpriteRadius = 20;

        static TMP_FontAsset _font;
        static Sprite _sprite;
        static Sprite _glowSprite;
        static Theme2048 _theme;

        [MenuItem("GC26/Colours (nastya)/Rebuild assets and scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>True when the saved entry scene was built for the current <see cref="Game2048Nastya.SceneVersion"/>.</summary>
        public static bool IsSceneUpToDate()
        {
            if (!File.Exists(ScenePath)) return false;
            // The project uses text serialization (RULES, setup step 1), so the stamp is a plain YAML line.
            // Multiline regex, so both LF and CRLF checkouts match.
            return System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(ScenePath),
                @"^\s*sceneVersion: " + Game2048Nastya.SceneVersion + @"\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        public const string EntryScenePath = ScenePath;

        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _sprite = EnsureSprite();
            _glowSprite = EnsureGlowSprite();
            _theme = EnsureTheme();
            BuildCellPrefab();
            BuildTilePrefab();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[gc26_2048_nastya] assets and scene rebuilt");
        }

        // ---------- assets ----------

        static Sprite EnsureSprite()
        {
            if (!File.Exists(SpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[SpriteSize * SpriteSize];
                for (int y = 0; y < SpriteSize; y++)
                {
                    for (int x = 0; x < SpriteSize; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float cx = Mathf.Clamp(px, SpriteRadius, SpriteSize - SpriteRadius);
                        float cy = Mathf.Clamp(py, SpriteRadius, SpriteSize - SpriteRadius);
                        float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(SpriteRadius - d + 0.5f) * 255f);
                        pixels[y * SpriteSize + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(pixels);
                File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(SpriteRadius + 2, SpriteRadius + 2, SpriteRadius + 2, SpriteRadius + 2);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        const int GlowSize = 96;
        const int GlowBorder = 40;

        /// <summary>A soft rounded halo for high tiles (T51): opaque inside, fading out over ~24 px.</summary>
        static Sprite EnsureGlowSprite()
        {
            if (!File.Exists(GlowSpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GlowSpritePath));
                var tex = new Texture2D(GlowSize, GlowSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[GlowSize * GlowSize];
                const float inner = GlowBorder - 4f; // the inner rounded rect starts here
                const float radius = 10f;
                const float falloff = 13f;
                for (int y = 0; y < GlowSize; y++)
                {
                    for (int x = 0; x < GlowSize; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float cx = Mathf.Clamp(px, inner + radius, GlowSize - inner - radius);
                        float cy = Mathf.Clamp(py, inner + radius, GlowSize - inner - radius);
                        float d = Mathf.Max(0f, Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) - radius);
                        float a = Mathf.Exp(-(d / falloff) * (d / falloff));
                        pixels[y * GlowSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                tex.SetPixels32(pixels);
                File.WriteAllBytes(GlowSpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(GlowSpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(GlowSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(GlowBorder, GlowBorder, GlowBorder, GlowBorder);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
        }

        static Theme2048 EnsureTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<Theme2048>(ThemePath);
            if (theme != null) return theme;
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            theme = ScriptableObject.CreateInstance<Theme2048>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }

        static void BuildCellPrefab()
        {
            var go = Node("gc26_2048_nastya_Cell", null);
            var image = Img(go, _theme.emptyCell);
            image.raycastTarget = false;
            SavePrefab(go, CellPrefabPath);
        }

        static void BuildTilePrefab()
        {
            var go = Node("gc26_2048_nastya_Tile", null);

            // Children draw in order, so the glow goes first, behind the tile body.
            var glowGo = Node("Glow", go.transform);
            var glowRect = Stretch(glowGo, Vector2.zero, Vector2.one);
            glowRect.offsetMin = new Vector2(-26f, -26f); // about the gap between cells
            glowRect.offsetMax = new Vector2(26f, 26f);
            var glow = glowGo.AddComponent<Image>();
            glow.sprite = _glowSprite;
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            glow.enabled = false;

            var body = Node("Body", go.transform);
            Stretch(body, Vector2.zero, Vector2.one);
            var image = Img(body, _theme.StyleFor(2).tile);
            image.raycastTarget = false;
            var outline = body.AddComponent<Outline>();
            outline.useGraphicAlpha = true;
            outline.enabled = false;

            var label = Text(Node("Label", go.transform), "2", 110f, _theme.StyleFor(2).text, TextAlignmentOptions.Center, true);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);

            var view = go.AddComponent<TileView2048>();
            Wire(view, "background", image);
            Wire(view, "label", label);
            Wire(view, "glow", glow);
            Wire(view, "outline", outline);
            SavePrefab(go, TilePrefabPath);
        }

        // ---------- scene ----------

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load the prefabs after NewScene: references taken before it do not survive the scene switch.
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath).GetComponent<Image>();
            var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath).GetComponent<TileView2048>();

            // Own camera for standalone testing only (TDD 9): no AudioListener, no MainCamera tag.
            var camGo = new GameObject("gc26_2048_nastya_Camera");
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _theme.background;
            cam.orthographic = true;

            var root = new GameObject("gc26_2048_nastya_Root");
            var game = root.AddComponent<Game2048Nastya>();
            var input = root.AddComponent<Input2048>();
            Wire(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));

            // Canvas: Screen Space - Camera, so it unloads with the scene and never covers the shell (TDD 6.1).
            var canvasGo = Node("gc26_2048_nastya_Canvas", root.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = Node("Background", canvasGo.transform);
            Stretch(background, Vector2.zero, Vector2.one);
            background.AddComponent<Image>().color = _theme.background;

            // HUD, board and hint live inside the safe area (T58); the background and overlay blockers stay full-screen.
            var safe = Node("SafeArea", canvasGo.transform);
            Stretch(safe, Vector2.zero, Vector2.one);
            safe.AddComponent<SafeArea2048>();

            var hud = BuildHud(safe.transform);
            var boardView = BuildBoard(safe.transform, cellPrefab, tilePrefab);
            var hint = BuildHint(safe.transform);
            Wire(hud, "hintText", hint);
            var overlays = BuildOverlays(canvasGo.transform);
            var confirmPanel = BuildConfirm(canvasGo.transform, out var yes, out var no);
            var splash = BuildSplash(canvasGo.transform);
            Wire(hud, "confirmPanel", confirmPanel);
            Wire(hud, "confirmYesButton", yes);
            Wire(hud, "confirmNoButton", no);

            // Own EventSystem, switched off when the shell already has one (TDD 7.3).
            var es = new GameObject("gc26_2048_nastya_EventSystem");
            es.transform.SetParent(root.transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            es.AddComponent<EventSystemGuard2048>();

            Wire(game, "theme", _theme);
            Wire(game, "boardView", boardView);
            Wire(game, "hud", hud);
            Wire(game, "overlays", overlays);
            Wire(game, "splash", splash);
            Wire(game, "input", input);
            Wire(game, "sfx", BuildSfx(root, canvasGo.transform));
            Wire(game, "ownCamera", cam);
            Wire(game, "canvas", canvas);
            var stamp = new SerializedObject(game);
            stamp.FindProperty("sceneVersion").intValue = Game2048Nastya.SceneVersion;
            stamp.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static Hud2048 BuildHud(Transform canvas)
        {
            var go = Node("Hud", canvas);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-80f, 300f);
            rect.anchoredPosition = new Vector2(0f, -50f);
            var hud = go.AddComponent<Hud2048>();

            // "Colours" with "2048" right-aligned under it (T5B), in the top-left corner.
            var title = BuildTitle(go.transform, 104f);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = Vector2.zero;

            var score = ScoreBox(go.transform, "ScoreBox", "SCORE", new Vector2(0.46f, 0.45f), new Vector2(0.72f, 1f));
            var best = ScoreBox(go.transform, "BestBox", "BEST", new Vector2(0.74f, 0.45f), new Vector2(1f, 1f));
            var undo = MakeButton(go.transform, "UndoButton", "Undo", new Vector2(0.46f, 0f), new Vector2(0.58f, 0.36f));
            var newGame = MakeButton(go.transform, "NewGameButton", "New game", new Vector2(0.60f, 0f), new Vector2(0.80f, 0.36f));
            var pause = MakeButton(go.transform, "PauseButton", "Pause", new Vector2(0.82f, 0f), new Vector2(1f, 0.36f));
            Wire(hud, "undoButton", undo);

            Wire(hud, "scoreText", score);
            Wire(hud, "bestText", best);
            Wire(hud, "newGameButton", newGame);
            Wire(hud, "pauseButton", pause);
            return hud;
        }

        static TMP_Text ScoreBox(Transform parent, string name, string caption, Vector2 min, Vector2 max)
        {
            var box = Node(name, parent);
            Stretch(box, min, max);
            Img(box, _theme.board).raycastTarget = false;
            var label = Text(Node("Caption", box.transform), caption, 34f, _theme.hudMutedText, TextAlignmentOptions.Center, true);
            Stretch(label.gameObject, new Vector2(0f, 0.58f), new Vector2(1f, 0.95f));
            var value = Text(Node("Value", box.transform), "0", 64f, _theme.hudText, TextAlignmentOptions.Center, true);
            Stretch(value.gameObject, new Vector2(0f, 0.05f), new Vector2(1f, 0.62f));
            value.enableAutoSizing = true;
            value.fontSizeMin = 28f;
            value.fontSizeMax = 64f;
            return value;
        }

        static BoardView2048 BuildBoard(Transform canvas, Image cellPrefab, TileView2048 tilePrefab)
        {
            var area = Node("BoardArea", canvas);
            var areaRect = Stretch(area, Vector2.zero, Vector2.one);
            areaRect.offsetMin = new Vector2(40f, 120f);
            areaRect.offsetMax = new Vector2(-40f, -400f);

            var board = Node("Board", area.transform);
            Stretch(board, Vector2.zero, Vector2.one);
            var fitter = board.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            var image = Img(board, _theme.board);
            image.raycastTarget = false;

            var cells = Node("Cells", board.transform);
            Stretch(cells, Vector2.zero, Vector2.one);
            var tiles = Node("Tiles", board.transform);
            Stretch(tiles, Vector2.zero, Vector2.one);

            var view = board.AddComponent<BoardView2048>();
            Wire(view, "boardImage", image);
            Wire(view, "cellsRoot", cells.transform);
            Wire(view, "tilesRoot", tiles.transform);
            Wire(view, "cellPrefab", cellPrefab);
            Wire(view, "tilePrefab", tilePrefab);
            Wire(view, "bumpTarget", areaRect); // the fitter owns the board's own rect
            return view;
        }

        /// <summary>T52: two 2D AudioSources (merges change pitch) and the clips, imported as TDD 10 says.</summary>
        static Sfx2048 BuildSfx(GameObject root, Transform canvas)
        {
            var go = new GameObject("gc26_2048_nastya_Sfx");
            go.transform.SetParent(root.transform, false);
            var sfx = go.AddComponent<Sfx2048>();
            Wire(sfx, "source", Source(go));
            Wire(sfx, "mergeSource", Source(go));
            foreach (var (field, file) in new[]
                     {
                         ("slide", "Slide"), ("merge", "Merge"), ("invalid", "Invalid"),
                         ("win", "Win"), ("lose", "Lose"), ("click", "Click"),
                     })
                Wire(sfx, field, Clip(file));
            Wire(sfx, "buttonsRoot", canvas);
            return sfx;
        }

        static AudioSource Source(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        static AudioClip Clip(string name)
        {
            string path = AudioDir + "gc26_2048_nastya_" + name + ".wav";
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError($"[gc26_2048_nastya] missing {path}: run python Docs~/generate_sfx.py");
            return clip;
        }

        /// <summary>
        /// The game title: "Colours", and "2048" on the next line, right-aligned to the end of "Colours",
        /// same font and size. A vertical layout sized to its widest line keeps them aligned for any font.
        /// </summary>
        static GameObject BuildTitle(Transform parent, float size, bool gradient = false)
        {
            var go = Node("Title", parent);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;   // both lines get the width of the widest one
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.spacing = -size * 0.25f;          // the two lines sit close together
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var color = gradient ? Color.white : _theme.hudText; // vertex colors multiply with it
            var name = Text(Node("Name", go.transform), "Colours", size, color, TextAlignmentOptions.Left, true);
            var number = Text(Node("Number", go.transform), "2048", size, color, TextAlignmentOptions.Right, true);
            if (gradient)
            {
                Wire(name.gameObject.AddComponent<TextGradient2048>(), "theme", _theme);
                Wire(number.gameObject.AddComponent<TextGradient2048>(), "theme", _theme);
            }
            return go;
        }

        /// <summary>T5A: full-screen splash over everything; the whole screen is one button that skips it.</summary>
        static Splash2048 BuildSplash(Transform canvas)
        {
            var panel = Node("Splash", canvas);
            Stretch(panel, Vector2.zero, Vector2.one);
            var image = panel.AddComponent<Image>();
            image.color = _theme.background;
            var skip = panel.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.targetGraphic = image;
            var group = panel.AddComponent<CanvasGroup>();

            var title = BuildTitle(panel.transform, 180f, gradient: true);
            var rect = (RectTransform)title.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);

            var splash = panel.AddComponent<Splash2048>();
            Wire(splash, "group", group);
            Wire(splash, "skipArea", skip);
            panel.SetActive(false);
            return splash;
        }

        static TMP_Text BuildHint(Transform canvas)
        {
            // Under the board, in the band the board area leaves free at the bottom (T47).
            var hint = Text(Node("Hint", canvas), "Swipe or use the arrow keys to combine tiles",
                40f, _theme.hudMutedText, TextAlignmentOptions.Center, false);
            var rect = (RectTransform)hint.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-80f, 100f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 24f;
            hint.fontSizeMax = 40f;
            return hint;
        }

        // Every button in every window has the same size (menu, dialogs, confirm).
        const float DialogWidth = 820f;
        const float DialogButtonWidth = 600f;
        const float DialogButtonHeight = 120f;
        const float DialogButtonGap = 24f;
        const float DialogPadding = 48f;
        const float DialogTitleHeight = 130f;

        static Overlays2048 BuildOverlays(Transform canvas)
        {
            var root = Node("Overlays", canvas);
            Stretch(root, Vector2.zero, Vector2.one);
            var overlays = root.AddComponent<Overlays2048>();

            // Start menu (GDD 4.0): opaque, gradient title, four buttons, version at the bottom.
            var menu = Node("MenuPanel", root.transform);
            Stretch(menu, Vector2.zero, Vector2.one);
            menu.AddComponent<Image>().color = _theme.background;
            Wire(overlays, "menuPanel", menu.AddComponent<CanvasGroup>());
            var menuTitle = (RectTransform)BuildTitle(menu.transform, 160f, gradient: true).transform;
            menuTitle.anchorMin = menuTitle.anchorMax = new Vector2(0.5f, 0.72f);
            menuTitle.pivot = new Vector2(0.5f, 0.5f);
            var menuButtons = Node("Buttons", menu.transform);
            var menuButtonsRect = (RectTransform)menuButtons.transform;
            menuButtonsRect.anchorMin = menuButtonsRect.anchorMax = new Vector2(0.5f, 0.38f);
            menuButtonsRect.sizeDelta = new Vector2(DialogButtonWidth, ButtonsHeight(4));
            Wire(overlays, "menuNewGameButton", DialogButton(menuButtons.transform, 0, 4, "NewGameButton", "New Game"));
            Wire(overlays, "continueButton", DialogButton(menuButtons.transform, 1, 4, "ContinueButton", "Continue"));
            Wire(overlays, "tutorialButton", DialogButton(menuButtons.transform, 2, 4, "TutorialButton", "Tutorial"));
            Wire(overlays, "settingsButton", DialogButton(menuButtons.transform, 3, 4, "SettingsButton", "Settings"));
            var version = Text(Node("Version", menu.transform), "v" + Game2048Nastya.Version, 32f, _theme.hudMutedText,
                TextAlignmentOptions.Bottom, false);
            Stretch(version.gameObject, new Vector2(0f, 0.02f), new Vector2(1f, 0.08f));
            Wire(overlays, "versionText", version);
            menu.SetActive(false);

            // Tutorial
            var tutorial = OverlayPanel(root.transform, "TutorialPanel", "How to Play", 1, 560f, out var tutorialDialog,
                "Swipe or use the arrow keys (WASD) to move all tiles.\n\n" +
                "Two tiles with the same number merge into one: 2 + 2 = 4, 4 + 4 = 8...\n\n" +
                "Each merge adds the new number to your score.\n" +
                "Make a 2048 tile to win, then keep going for more.\n\n" +
                "The game ends when the board is full and no tiles can merge.\n\n" +
                "Undo (U) takes back one move. Esc pauses the game.");
            Wire(overlays, "tutorialPanel", tutorial);
            Wire(overlays, "tutorialBackButton", DialogButton(tutorialDialog, 0, 1, "BackButton", "Back"));

            // Settings
            var settings = OverlayPanel(root.transform, "SettingsPanel", "Settings", 2, 0f, out var settingsDialog);
            Wire(overlays, "settingsPanel", settings);
            var settingsSound = DialogButton(settingsDialog, 0, 2, "SoundButton", "Sound Effects: On");
            Wire(overlays, "settingsSoundButton", settingsSound);
            Wire(overlays, "settingsSoundLabel", settingsSound.GetComponentInChildren<TMP_Text>());
            Wire(overlays, "settingsBackButton", DialogButton(settingsDialog, 1, 2, "BackButton", "Back"));

            // Win (T40)
            var win = OverlayPanel(root.transform, "WinPanel", "You made 2048!", 2, 90f, out var winDialog,
                "Keep going for a bigger tile?");
            Wire(overlays, "winPanel", win);
            Wire(overlays, "keepGoingButton", DialogButton(winDialog, 0, 2, "KeepGoingButton", "Keep Going"));
            Wire(overlays, "winNewGameButton", DialogButton(winDialog, 1, 2, "NewGameButton", "New Game"));

            // Game over (T41)
            var over = OverlayPanel(root.transform, "GameOverPanel", "No more moves", 2, 160f, out var overDialog,
                "Score  0\nBest  0");
            Wire(overlays, "gameOverPanel", over);
            Wire(overlays, "gameOverScoreText", overDialog.Find("Message").GetComponent<TMP_Text>());
            Wire(overlays, "tryAgainButton", DialogButton(overDialog, 0, 2, "TryAgainButton", "Try Again"));
            Wire(overlays, "gameOverMenuButton", DialogButton(overDialog, 1, 2, "MenuButton", "To Menu"));

            // Pause (T42)
            var pause = OverlayPanel(root.transform, "PausePanel", "Paused", 4, 0f, out var pauseDialog);
            Wire(overlays, "pausePanel", pause);
            Wire(overlays, "resumeButton", DialogButton(pauseDialog, 0, 4, "ResumeButton", "Resume"));
            Wire(overlays, "pauseNewGameButton", DialogButton(pauseDialog, 1, 4, "NewGameButton", "New Game"));
            var sound = DialogButton(pauseDialog, 2, 4, "SoundButton", "Sound Effects: On");
            Wire(overlays, "soundButton", sound);
            Wire(overlays, "soundLabel", sound.GetComponentInChildren<TMP_Text>());
            Wire(overlays, "pauseMenuButton", DialogButton(pauseDialog, 3, 4, "MenuButton", "To Menu"));

            return overlays;
        }

        static float ButtonsHeight(int count) => count * DialogButtonHeight + (count - 1) * DialogButtonGap;

        /// <summary>
        /// A full-screen dark blocker with a centered dialog: title, an optional message, then <paramref name="buttons"/>
        /// rows for <see cref="DialogButton"/>. The dialog height follows its content, so buttons never change size.
        /// </summary>
        static CanvasGroup OverlayPanel(Transform parent, string name, string title, int buttons, float messageHeight,
            out Transform dialog, string message = null)
        {
            var panel = Node(name, parent);
            Stretch(panel, Vector2.zero, Vector2.one);
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f); // blocks clicks behind it
            var group = panel.AddComponent<CanvasGroup>();

            float buttonsTop = DialogPadding + ButtonsHeight(buttons);
            float height = buttonsTop + (messageHeight > 0f ? messageHeight + DialogPadding : DialogPadding) + DialogTitleHeight;
            var box = Node("Dialog", panel.transform);
            ((RectTransform)box.transform).sizeDelta = new Vector2(DialogWidth, height);
            Img(box, _theme.board);

            var titleText = Text(Node("Title", box.transform), title, 72f, _theme.hudText, TextAlignmentOptions.Center, true);
            var titleRect = Stretch(titleText.gameObject, new Vector2(0f, 1f), new Vector2(1f, 1f));
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-80f, DialogTitleHeight);
            titleRect.anchoredPosition = new Vector2(0f, -16f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 40f;
            titleText.fontSizeMax = 72f;

            if (message != null)
            {
                var text = Text(Node("Message", box.transform), message, 40f, _theme.hudMutedText, TextAlignmentOptions.Center, false);
                var rect = Stretch(text.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0f));
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(-96f, messageHeight);
                rect.anchoredPosition = new Vector2(0f, buttonsTop + DialogPadding * 0.5f);
                text.textWrappingMode = TextWrappingModes.Normal;
                text.enableAutoSizing = true;
                text.fontSizeMin = 26f;
                text.fontSizeMax = 40f;
            }

            panel.SetActive(false);
            dialog = box.transform;
            return group;
        }

        /// <summary>Button number <paramref name="index"/> (0 = top) of <paramref name="count"/>, all the same size, at the bottom.</summary>
        static Button DialogButton(Transform parent, int index, int count, string name, string caption)
        {
            var button = MakeButton(parent, name, caption, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(DialogButtonWidth, DialogButtonHeight);
            bool inDialog = parent.name == "Dialog";
            rect.anchoredPosition = new Vector2(0f,
                (inDialog ? DialogPadding : 0f) + (count - 1 - index) * (DialogButtonHeight + DialogButtonGap));
            return button;
        }

        static GameObject BuildConfirm(Transform canvas, out Button yes, out Button no)
        {
            var group = OverlayPanel(canvas, "ConfirmPanel", "Start a new game?", 2, 70f, out var dialog,
                "The current game will be lost.");
            yes = DialogButton(dialog, 0, 2, "YesButton", "New Game");
            no = DialogButton(dialog, 1, 2, "NoButton", "Cancel");
            return group.gameObject;
        }

        // ---------- helpers ----------

        static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 }; // built-in UI layer
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static RectTransform Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Image Img(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = _sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI Text(GameObject go, string text, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string caption, Vector2 min, Vector2 max)
        {
            var go = Node(name, parent);
            Stretch(go, min, max);
            var image = Img(go, _theme.button);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Text(Node("Label", go.transform), caption, 40f, _theme.hudText, TextAlignmentOptions.Center, true);
            Stretch(label.gameObject, Vector2.zero, Vector2.one);
            label.margin = new Vector4(8f, 0f, 8f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 40f;
            return button;
        }

        static void SavePrefab(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[gc26_2048_nastya] {target.GetType().Name} has no field '{field}'");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
