using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GC26.ScienceSnake
{
    /// <summary>
    /// Science Snake — Unity port of docs/prototype_snake_2.html (version 2.1).
    /// One component builds everything at runtime: 2D board (sprites, orthographic camera), uGUI interface, sounds.
    /// Put it on an empty GameObject in an empty scene and press Play.
    /// </summary>
    public class ScienceSnakeGame : MonoBehaviour
    {
        public const string Id = "gc26_science_snake";
        public const string Version = "2.1";

        [Tooltip("Short thresholds for testing (100 cells, 10 s, 25 points). Also enabled by the -test command-line argument.")]
        public bool testMode;

        // ================= CONFIG =================
        const int N = 20;                                  // 20×20 grid, edges wrap around
        const float EvilMax = 9f, AppleLife = 10f, SimpleAppleLife = 6f, WarnSec = 2f, PopSec = .25f;
        const int MaxLives = 3;
        static readonly Vector2 GreedLife = new Vector2(9, 12), SpawnGap = new Vector2(3, 5);
        static readonly Vector2Int Up = new Vector2Int(0, -1), Down = new Vector2Int(0, 1), Left = new Vector2Int(-1, 0), Right = new Vector2Int(1, 0);
        static readonly Dictionary<string, int> Points = new Dictionary<string, int> { ["apple"] = 1, ["kitten"] = 5, ["farmer"] = 10 };

        struct Thresholds { public int tut, walk, see, walkDist, greed; public float starveSec, factSec, textSec; }
        bool test;
        Thresholds TH => test
            ? new Thresholds { tut = 5, walk = 10, see = 15, walkDist = 100, starveSec = 10, greed = 25, factSec = 8, textSec = 5 }
            : new Thresholds { tut = 5, walk = 10, see = 15, walkDist = 300, starveSec = 30, greed = 50, factSec = 15, textSec = 10 };

        // ================= SAVE & SETTINGS (PlayerPrefs, keys prefixed with the game id) =================
        [Serializable] class Checkpoint { public string phase; public int total, dist, fact, greedLine; }
        string lang = "ru", speedKey = "normal";
        bool padOn = true, soundOn = true, vibeOn = true, shakeOn = true;
        int best; readonly HashSet<string> endings = new HashSet<string>(); bool tutSeen; Checkpoint checkpoint;

        static string K(string k) => Id + "_" + k;
        void LoadAll()
        {
            lang = PlayerPrefs.GetString(K("lang"), "ru");
            speedKey = PlayerPrefs.GetString(K("speed"), "normal");
            padOn = PlayerPrefs.GetInt(K("pad"), 1) == 1;
            soundOn = PlayerPrefs.GetInt(K("sound"), 1) == 1;
            vibeOn = PlayerPrefs.GetInt(K("vibe"), 1) == 1;
            shakeOn = PlayerPrefs.GetInt(K("shake"), 1) == 1;
            best = PlayerPrefs.GetInt(K("best"), 0);
            tutSeen = PlayerPrefs.GetInt(K("tutSeen"), 0) == 1;
            endings.Clear();
            foreach (var e in PlayerPrefs.GetString(K("endings"), "").Split(',')) if (e.Length > 0) endings.Add(e);
            var cp = PlayerPrefs.GetString(K("checkpoint"), "");
            checkpoint = cp.Length > 0 ? JsonUtility.FromJson<Checkpoint>(cp) : null;
        }
        void SaveSettings()
        {
            PlayerPrefs.SetString(K("lang"), lang); PlayerPrefs.SetString(K("speed"), speedKey);
            PlayerPrefs.SetInt(K("pad"), padOn ? 1 : 0); PlayerPrefs.SetInt(K("sound"), soundOn ? 1 : 0);
            PlayerPrefs.SetInt(K("vibe"), vibeOn ? 1 : 0); PlayerPrefs.SetInt(K("shake"), shakeOn ? 1 : 0);
            PlayerPrefs.Save();
        }
        void SaveProgress()
        {
            PlayerPrefs.SetInt(K("best"), best); PlayerPrefs.SetInt(K("tutSeen"), tutSeen ? 1 : 0);
            PlayerPrefs.SetString(K("endings"), string.Join(",", endings));
            PlayerPrefs.SetString(K("checkpoint"), checkpoint != null ? JsonUtility.ToJson(checkpoint) : "");
            PlayerPrefs.Save();
        }

        // ================= TEXT =================
        Lang L => lang == "en" ? Texts.En : Texts.Ru;
        // the web texts use emoji (🐱 🧑‍🌾) that Unity's fonts cannot draw; these lines use words instead
        static readonly Dictionary<string, string> UnityRu = new Dictionary<string, string> { ["hudGreedSub"] = "котёнок +5 · фермер +10" };
        static readonly Dictionary<string, string> UnityEn = new Dictionary<string, string> { ["hudGreedSub"] = "kitten +5 · farmer +10" };
        string T(string key, params object[] a)
        {
            string s = (lang == "en" ? UnityEn : UnityRu).TryGetValue(key, out var o) ? o : L.S.TryGetValue(key, out var v) ? v : key;
            return a.Length > 0 ? string.Format(s, a) : s;
        }
        string OnOff(bool v) => lang == "en" ? (v ? "on" : "off") : (v ? "вкл" : "выкл");

        // ================= GAME STATE =================
        class Item { public Vector2Int p; public string type; public float born, life; }
        class Counters { public int total, dist, fact, greedLine; }
        class Game
        {
            public string mode, phase;
            public List<Vector2Int> snake = new List<Vector2Int>(), prev = new List<Vector2Int>(), queue = new List<Vector2Int>();
            public Vector2Int dir;
            public int grow, score, lives;
            public readonly List<Item> items = new List<Item>();
            public float t, acc, nextSpawn, countdown, evilSpeed, fieldStart = -1f, fieldDone = -1f, spreadAcc;
            public Counters c;
            public bool over, evil;
            public readonly HashSet<int> black = new HashSet<int>();
            public float flash, dark, hintUntil, hurt, respawnIn, noEat, factT;
        }
        Game G;
        bool modalOpen, inGame;

        // ================= FX (techniques adapted from SNKRX by a327ex, MIT) =================
        class Part { public Vector2 p; public float a, v0, dur, t, w0, w; public Color c; }
        class Circ { public Vector2 p; public float r0, dur, t; public Color c; }
        class Shk { public float amp, dur, freq, t; public float[] nx, ny; }
        class Floater { public Vector2 p; public string text; public Color c; public float t, dur = .9f; }
        readonly List<Part> parts = new List<Part>();
        readonly List<Circ> circs = new List<Circ>();
        readonly List<Shk> shakes = new List<Shk>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<float> bulges = new List<float>();
        float slow = 1f, slowFrom = 1f, slowT, slowDur, headX = 1f, headV, hitFlash;
        Vector2 shakeOffset;

        static readonly Dictionary<string, Color> ItemColor = new Dictionary<string, Color>
        { ["apple"] = UiKit.Hex("#d32f2f"), ["kitten"] = UiKit.Hex("#8e8e8e"), ["farmer"] = UiKit.Hex("#8d6e63") };

        struct Pal { public Color head, a, b, eye, outline, pattern; }
        static readonly Dictionary<string, Pal> Palettes = new Dictionary<string, Pal>
        {
            ["base"] = new Pal { head = UiKit.Hex("#2e7d32"), a = new Color32(46, 125, 50, 255), b = new Color32(129, 199, 132, 255), eye = UiKit.Hex("#ffc107"), outline = UiKit.Hex("#174d1b"), pattern = new Color(.08f, .3f, .1f, .55f) },
            ["fed"] = new Pal { head = UiKit.Hex("#558b2f"), a = new Color32(85, 139, 47, 255), b = new Color32(205, 220, 57, 255), eye = UiKit.Hex("#ffc107"), outline = UiKit.Hex("#2e5016"), pattern = new Color(.2f, .33f, .08f, .5f) },
            ["hungry"] = new Pal { head = UiKit.Hex("#6f9a73"), a = new Color32(111, 154, 115, 255), b = new Color32(190, 214, 191, 255), eye = UiKit.Hex("#d8cf96"), outline = UiKit.Hex("#46664a"), pattern = new Color(.27f, .4f, .29f, .45f) },
            ["evil"] = new Pal { head = UiKit.Hex("#5c0f25"), a = new Color32(92, 15, 37, 255), b = new Color32(178, 58, 72, 255), eye = UiKit.Hex("#ff1744"), outline = UiKit.Hex("#2a0510"), pattern = new Color(.12f, .01f, .05f, .6f) },
        };

        // ================= UNITY =================
        // ---------- debug log ----------
        // Every important game event is written to the log with the tag [ScienceSnake]:
        // Unity Console in the editor, Player.log on PC, and `adb logcat -s Unity` on an Android phone.
        // Start the game with -nolog to switch it off.
        bool logOn = true;
        string lastPhase;
        void Log(string msg)
        {
            if (!logOn) return;
            string where = G == null ? "menu" : $"{G.mode}/{G.phase} t={G.t:0.0}s len={G.snake.Count} lives={G.lives} total={G.c?.total ?? 0} score={G.score}";
            Debug.Log($"[ScienceSnake] {msg}  ({where})");
        }

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            test = testMode || Array.IndexOf(args, "-test") >= 0;
            logOn = Array.IndexOf(args, "-nolog") < 0;
            Application.targetFrameRate = 60;            // Android defaults to 30 fps; the snake must move smoothly
            int q = Array.IndexOf(args, "-quality");      // developer switch: -quality Mobile = the phone's graphics settings on PC
            if (q >= 0 && q + 1 < args.Length) { int lvl = Array.IndexOf(QualitySettings.names, args[q + 1]); if (lvl >= 0) QualitySettings.SetQualityLevel(lvl, true); }
#if !UNITY_EDITOR
            // The project's mobile settings render at 80 % resolution, which blurs the board; this game uses full resolution.
            // Only in the built game: in the editor this would change the shared settings file.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = 1f;
#endif
            LoadAll();
            BuildAudio();
            BuildWorld();
            Prewarm();
            BuildUI();
            ApplyLang();
            ShowMenu();
            if (!tutSeen && Array.IndexOf(args, "-sstest") < 0)          // first launch ever: the animated tutorial opens once over the menu
            {
                tutSeen = true; SaveProgress();
                Tutorial(0, null, false);
            }
            Log($"start: version {Application.version}, {Application.platform}, {SystemInfo.deviceModel}, screen {Screen.width}x{Screen.height} @{Screen.dpi:0}dpi, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, test thresholds {(test ? "ON" : "off")}");
            // developer shortcut for screenshots/tests: -sstest simple|walk|greed|tut|settings
            int i = Array.IndexOf(args, "-sstest");
            if (i >= 0 && i + 1 < args.Length) DevStart(args[i + 1]);
        }

        void DevStart(string what)
        {
            switch (what)
            {
                case "simple": StartSimple(); break;
                case "walk": NewGame("story", "walk", new Counters { total = 7, dist = 40 }); ShowGame(); Countdown(.6f); G.grow = 9; break;
                case "greed": NewGame("story", "greed", new Counters { total = 21 }); ShowGame(); Countdown(.6f); G.grow = 14; break;
                case "evil": NewGame("story", "greed", new Counters { total = TH.greed - 1 }); ShowGame(); G.grow = 12; StartEvil(); Countdown(.6f); break;
                case "tut": Tutorial(0, null, false); break;
                case "settings": SettingsModal(null); break;
            }
        }

        void Update()
        {
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime);
            LayoutColumn();
            HandleInput();
            FxUpdate(dt);
            if (inGame && G != null) { Tick(dt * slow); Render(); BindMaterials(); }
            UpdateToast(dt);
            DemoUpdate(dt);
        }

        void OnApplicationPause(bool paused) { Log(paused ? "app sent to background" : "app back in front"); if (paused) AutoPause(); }
        void OnApplicationFocus(bool focus) { if (!focus) AutoPause(); }
        void AutoPause() { if (inGame && G != null && !modalOpen && !G.over) PauseMenu(); }

        // ================= AUDIO =================
        AudioSource audioSrc;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static Procedural.Tone Tn(float f0, float dur, int wave, float vol, float f1 = 0, float delay = 0) =>
            new Procedural.Tone { f0 = f0, f1 = f1, dur = dur, wave = wave, vol = vol, delay = delay };
        void BuildAudio()
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            clips["eat"] = Procedural.Synth("eat", Tn(600, .09f, 1, .14f, 900));
            clips["die"] = Procedural.Synth("die", Tn(180, .35f, 2, .12f, 50));
            clips["ding"] = Procedural.Synth("ding", Tn(880, .25f, 0, .08f), Tn(1320, .3f, 0, .05f, 0, .05f));
            clips["growl"] = Procedural.Synth("growl", Tn(110, .6f, 2, .16f, 45), Tn(90, .6f, 3, .06f, 40));
            clips["warm"] = Procedural.Synth("warm", Tn(392, 1.2f, 0, .06f), Tn(494, 1.2f, 0, .06f, 0, .08f), Tn(587, 1.2f, 0, .06f, 0, .16f), Tn(784, 1.2f, 0, .06f, 0, .24f));
            clips["boom"] = Procedural.Synth("boom", Tn(70, 1f, 2, .2f, 30));
            clips["miss"] = Procedural.Synth("miss", Tn(300, .15f, 0, .06f, 200));
        }
        void Sfx(string name, float pitchMin = .95f, float pitchMax = 1.05f)
        {
            if (!soundOn || !clips.TryGetValue(name, out var c)) return;
            audioSrc.pitch = Random.Range(pitchMin, pitchMax);          // SNKRX: every sound at a slightly different pitch
            audioSrc.PlayOneShot(c);
        }
        void Buzz()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (vibeOn) Handheld.Vibrate();
#endif
        }

        // ================= WORLD (2D board) =================
        Camera bgCam, cam;
        Transform world;
        Texture2D fieldTex; bool fieldDirty = true;
        SpriteRenderer darkOv, flashOv, cdOv;
        readonly List<SpriteRenderer> segPool = new List<SpriteRenderer>();
        int segUsed;
        class HeadView
        {
            public Transform root; public SpriteRenderer head, tongue, forkA, forkB;
            public SpriteRenderer[] irises = new SpriteRenderer[2], pupils = new SpriteRenderer[2], smiles = new SpriteRenderer[2], brows = new SpriteRenderer[2], nostrils = new SpriteRenderer[2];
        }
        readonly HeadView[] heads = new HeadView[2];
        class ItemView { public SpriteRenderer icon, arc; }
        readonly List<ItemView> itemPool = new List<ItemView>();
        readonly List<SpriteRenderer> fxPool = new List<SpriteRenderer>();
        int fxUsed;
        float tongueT, slither;
        Vector2[] bodyBuf = new Vector2[64], swayBuf = new Vector2[256]; float[] bodyS = new float[64];

        static Vector3 W(float x, float y, float z = 0) => new Vector3(x + .5f, N - y - .5f, z);   // grid (y down) -> world (y up)

        SpriteRenderer SR(string name, Sprite s, int order, Color c, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : world, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s; r.sortingOrder = order; r.color = c;
            r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            if (spriteBase == null) spriteBase = r.sharedMaterial;
            allSr.Add(r);
            return r;
        }

        // Each sprite texture gets its own copy of the sprite material. With one shared material, the Unity editor
        // (DX12, Android target) drew every board sprite with the board's checker texture: the apple vanished
        // and the snake turned into flat shapes. Separate materials keep each sprite with its own texture.
        readonly List<SpriteRenderer> allSr = new List<SpriteRenderer>();
        readonly Dictionary<Texture, Material> texMats = new Dictionary<Texture, Material>();
        Material spriteBase;
        void BindMaterials()
        {
            foreach (var r in allSr)
            {
                if (r == null || !r.enabled || r.sprite == null || spriteBase == null) continue;
                var t = r.sprite.texture;
                if (!texMats.TryGetValue(t, out var m)) { m = new Material(spriteBase) { name = "Sprite " + t.width + "x" + t.height, mainTexture = t }; texMats[t] = m; }
                if (r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        void BuildWorld()
        {
            world = new GameObject("Board").transform;
            world.SetParent(transform, false);

            // the scene's own camera (so the Game view is never empty) becomes the background camera
            bgCam = GetComponentInChildren<Camera>();
            if (bgCam == null) { bgCam = new GameObject("Background Camera").AddComponent<Camera>(); bgCam.transform.SetParent(transform, false); }
            bgCam.clearFlags = CameraClearFlags.SolidColor; bgCam.backgroundColor = UiKit.Bg; bgCam.cullingMask = 0;
            bgCam.orthographic = true; bgCam.depth = 10;
            if (FindAnyObjectByType<AudioListener>() == null) bgCam.gameObject.AddComponent<AudioListener>();   // without it there is no sound

            cam = new GameObject("Board Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Panel;
            cam.depth = 11; cam.transform.position = new Vector3(N / 2f, N / 2f, -10);

            // soft shadow under the board, like the web version's card shadow
            var sh = SR("Board shadow", Procedural.SoftShadow, -2, new Color(.08f, .24f, .12f, .22f));
            sh.maskInteraction = SpriteMaskInteraction.None; sh.drawMode = SpriteDrawMode.Sliced;
            sh.size = new Vector2(N + 1.1f, N + 1.1f); sh.transform.localPosition = new Vector3(N / 2f, N / 2f - .22f);

            fieldTex = Procedural.FieldTexture(N);
            SR("Field", Sprite.Create(fieldTex, new Rect(0, 0, N, N), new Vector2(0, 0), 1), 0, Color.white).transform.localPosition = Vector3.zero;

            // rounded corners: everything on the board is drawn inside this mask
            var mask = new GameObject("Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(world, false);
            mask.sprite = Procedural.BoardMask(N);
            mask.transform.localPosition = new Vector3(N / 2f, N / 2f);

            // dashed frame: the edges are open (the snake comes out on the other side)
            var dash = new Color(.18f, .49f, .2f, .35f);
            for (float a = .1f; a < N - .1f; a += .75f)
            {
                float len = Mathf.Min(.4f, N - .1f - a);
                foreach (var (pos, size) in new[] {
                    (new Vector2(a + len / 2, N - .06f), new Vector2(len, .08f)), (new Vector2(a + len / 2, .06f), new Vector2(len, .08f)),
                    (new Vector2(.06f, a + len / 2), new Vector2(.08f, len)), (new Vector2(N - .06f, a + len / 2), new Vector2(.08f, len)) })
                {
                    var d = SR("Dash", Procedural.Square, 1, dash);
                    d.transform.localPosition = pos; d.transform.localScale = new Vector3(size.x, size.y, 1);
                }
            }

            darkOv = Overlay("Dark", 20); flashOv = Overlay("Flash", 21); cdOv = Overlay("Countdown", 1);   // under items and snake: only the field is dimmed, items keep their colours
            for (int i = 0; i < 2; i++) heads[i] = MakeHead(i);
        }

        /// <summary>
        /// Generates every sprite the board uses and creates the item renderers before the first frame,
        /// so items never pop in late, unshaded or with a missing texture when a game starts (mostly visible in the editor).
        /// </summary>
        void Prewarm()
        {
            var sprites = new List<Sprite> { Procedural.Apple, Procedural.Kitten, Procedural.Farmer, Procedural.Circle, Procedural.BodyDisc, Procedural.SnakeHead, Procedural.Logo };
            for (int k = 0; k <= 48; k++) sprites.Add(Procedural.Arc(k / 48f));
            for (int i = itemPool.Count; i < 10; i++)
            {
                var v = new ItemView { icon = SR("Item", Procedural.Apple, 3, Color.white), arc = SR("Timer", Procedural.Arc(1), 2, Color.white) };
                v.icon.enabled = v.arc.enabled = false; itemPool.Add(v);
            }
        }

        SpriteRenderer Overlay(string name, int order)
        {
            var o = SR(name, Procedural.Square, order, Color.clear);
            o.transform.localPosition = new Vector3(N / 2f, N / 2f); o.transform.localScale = new Vector3(N, N, 1);
            return o;
        }

        HeadView MakeHead(int i)
        {
            var h = new HeadView { root = new GameObject("Head " + i).transform };
            h.root.SetParent(world, false);
            h.head = SR("Skull", Procedural.SnakeHead, 6, Color.green, h.root);
            var red = UiKit.Hex("#e53935");
            h.tongue = SR("Tongue", Procedural.Square, 5, red, h.root);
            h.forkA = SR("Fork", Procedural.Square, 5, red, h.root);
            h.forkB = SR("Fork", Procedural.Square, 5, red, h.root);
            for (int s = 0; s < 2; s++)
            {
                h.irises[s] = SR("Iris", Procedural.Circle, 7, Color.yellow, h.root);
                h.pupils[s] = SR("Slit pupil", Procedural.Circle, 8, Color.black, h.root);
                h.smiles[s] = SR("Happy eye", Procedural.EyeSmile, 8, UiKit.Hex("#111111"), h.root);
                h.brows[s] = SR("Brow", Procedural.Square, 9, UiKit.Hex("#111111"), h.root);
                h.nostrils[s] = SR("Nostril", Procedural.Circle, 7, new Color(0, 0, 0, .55f), h.root);
            }
            return h;
        }

        readonly List<SpriteRenderer> patPool = new List<SpriteRenderer>();
        int patUsed;
        SpriteRenderer Pat()
        {
            if (patUsed >= patPool.Count) patPool.Add(SR("Scale", Procedural.Square, 5, Color.black));
            var r = patPool[patUsed++]; r.enabled = true; return r;
        }

        SpriteRenderer Seg()
        {
            if (segUsed >= segPool.Count) segPool.Add(SR("Segment", Procedural.BodyDisc, 4, Color.white));
            var r = segPool[segUsed++]; r.enabled = true; return r;
        }
        SpriteRenderer Fx(Sprite s, int order)
        {
            if (fxUsed >= fxPool.Count) fxPool.Add(SR("Fx", s, order, Color.white));
            var r = fxPool[fxUsed++]; r.enabled = true; r.sprite = s; r.sortingOrder = order; r.transform.localRotation = Quaternion.identity; return r;
        }

        // ================= UI =================
        // Colours and sizes follow the web version; the small grey and the hearts are darker/lighter than there for readability.
        static readonly Color Muted = UiKit.Hex("#4a6652"), Hearts = UiKit.Hex("#ffcdd2");
        Canvas canvas; RectTransform canvasRT, column, middle, padRT;
        GameObject menuGO, gameGO, overlayGO, padGO;
        RectTransform cardRT; Image cardBg, overlayImg;
        Text hud1, hud2, livesT, phaseT, factKick, factText, toastText, cdWord, cdNum, hintText, menuTag, menuStats, menuVer;
        Image toastBg, hintBg; CanvasGroup toastCG, factCG;
        class MenuBtn { public Button button; public Text title, sub; }
        MenuBtn mStory, mCont, mSimple;
        Button bTut, bSet, bAbout;
        readonly List<Text> floatPool = new List<Text>();
        readonly Dictionary<Vector2Int, PressButton> padKeys = new Dictionary<Vector2Int, PressButton>();
        float toastTimer;
        const float TopH = 54, FactsH = 92, PadH = 140;

        void BuildUI()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            var cgo = new GameObject("UI", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(480, 854); scaler.matchWidthOrHeight = 1f;
            cgo.AddComponent<GraphicRaycaster>();
            canvasRT = (RectTransform)cgo.transform;

            column = UiKit.Rect(canvasRT, "Column");
            column.anchorMin = new Vector2(.5f, 0); column.anchorMax = new Vector2(.5f, 1); column.sizeDelta = new Vector2(480, 0);

            BuildMenu();
            BuildGameScreen();
            BuildOverlay();
        }

        MenuBtn MenuButton(Transform parent, Sprite icon, bool primary, Action onClick, bool tint = true)
        {
            var img = UiKit.Image(parent, "Menu button", primary ? UiKit.G1 : Color.white, 16);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.pressedColor = new Color(.9f, .9f, .9f); colors.fadeDuration = .06f; b.colors = colors;
            b.onClick.AddListener(() => onClick());
            UiKit.Drop(img, primary ? UiKit.G2 : UiKit.Hex("#b9d6bb"), primary ? 3 : 1.5f);
            UiKit.Size(img, h: 60);
            var row = UiKit.Row(img.gameObject, 14);
            row.padding = new RectOffset(14, 12, 10, 10);
            var tile = UiKit.Image(img.transform, "Tile", primary ? new Color(1, 1, 1, .18f) : UiKit.G3, 10, false);
            UiKit.Size(tile, 38, 38);
            var ic = UiKit.Icon(tile.transform, icon, !tint ? Color.white : primary ? Color.white : UiKit.G2, 26);
            UiKit.Center(ic.rectTransform, new Vector2(tint ? 22 : 30, tint ? 22 : 30));
            var col = UiKit.Rect(img.transform, "Texts");
            UiKit.Column(col.gameObject, 1, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(col, flex: 1);
            var t = UiKit.Text(col, "Title", 17, primary ? Color.white : UiKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var s = UiKit.Text(col, "Sub", 13, primary ? UiKit.Hex("#d7ecd8") : Muted, TextAnchor.MiddleLeft);
            img.gameObject.AddComponent<PressButton>().Sink = 1.5f;
            return new MenuBtn { button = b, title = t, sub = s };
        }

        Button SmallButton(Transform parent, Action onClick)
        {
            var b = UiKit.Button(parent, "", Color.white, UiKit.Ink, () => onClick(), 15, 50, 16, UiKit.Hex("#b9d6bb"));
            return b;
        }

        void BuildMenu()
        {
            var bg = UiKit.Image(column, "Menu", Color.white);
            bg.sprite = Procedural.Gradient(UiKit.Hex("#f6fbf6"), UiKit.Hex("#d6ead7"));
            UiKit.Stretch(bg.rectTransform);
            menuGO = bg.gameObject;
            var box = UiKit.Rect(bg.transform, "Box");
            box.anchorMin = box.anchorMax = new Vector2(.5f, .5f); box.sizeDelta = new Vector2(330, 0);
            UiKit.Column(box.gameObject, 10, new RectOffset(0, 0, 0, 0));
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var logo = UiKit.Image(box, "Logo", Color.white, raycast: false);
            logo.sprite = Procedural.Logo; logo.preserveAspect = true;
            UiKit.Size(logo, h: 130);
            var title = UiKit.Text(box, "Title", 36, UiKit.G2, TextAnchor.MiddleCenter, FontStyle.Bold, serif: true); title.text = "Science Snake";
            menuTag = UiKit.Text(box, "Tagline", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Italic, serif: true);
            UiKit.Size(UiKit.Rect(box, "Gap"), h: 6);
            mStory = MenuButton(box, Procedural.Book, true, OnStory);
            mCont = MenuButton(box, Procedural.Play, false, ContinueStory);
            mSimple = MenuButton(box, Procedural.Apple, false, StartSimple, tint: false);
            var row = UiKit.Rect(box, "Row");
            var h = UiKit.Row(row.gameObject, 8, TextAnchor.MiddleCenter);
            h.childForceExpandWidth = true;
            UiKit.Size(row, h: 50);
            bTut = SmallButton(row, () => Tutorial(0, null, false));
            bSet = SmallButton(row, () => SettingsModal(null));
            bAbout = SmallButton(row, AboutModal);
            foreach (var b in new[] { bTut, bSet, bAbout }) UiKit.Size(b, flex: 1);
            UiKit.Size(UiKit.Rect(box, "Gap"), h: 4);
            menuStats = UiKit.Text(box, "Stats", 14, Muted);
            menuVer = UiKit.Text(box, "Version", 11, Muted);
        }

        void BuildGameScreen()
        {
            gameGO = UiKit.Rect(column, "Game").gameObject;
            UiKit.Stretch((RectTransform)gameGO.transform);

            // top bar: counters on the left, lives and phase in the middle, pause on the right
            var top = UiKit.Image(gameGO.transform, "Top bar", UiKit.G1); UiKit.Top(top.rectTransform, 0, TopH);
            hud1 = UiKit.Text(top.transform, "Hud1", 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, serif: true);
            hud1.rectTransform.anchorMin = new Vector2(0, 1); hud1.rectTransform.anchorMax = new Vector2(.42f, 1); hud1.rectTransform.offsetMin = new Vector2(14, -29); hud1.rectTransform.offsetMax = new Vector2(0, -5);
            hud2 = UiKit.Text(top.transform, "Hud2", 13, new Color(1, 1, 1, .9f), TextAnchor.MiddleLeft);
            hud2.rectTransform.anchorMin = new Vector2(0, 0); hud2.rectTransform.anchorMax = new Vector2(.45f, 0); hud2.rectTransform.offsetMin = new Vector2(14, 5); hud2.rectTransform.offsetMax = new Vector2(0, 23);
            hud2.horizontalOverflow = HorizontalWrapMode.Overflow;
            livesT = UiKit.Text(top.transform, "Lives", 17, Hearts);
            livesT.rectTransform.anchorMin = new Vector2(.4f, .5f); livesT.rectTransform.anchorMax = new Vector2(.7f, 1); livesT.rectTransform.offsetMin = new Vector2(0, -2); livesT.rectTransform.offsetMax = new Vector2(0, -4);
            var pill = UiKit.Image(top.transform, "Phase pill", new Color(1, 1, 1, .16f), 12, false);
            var prt = pill.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(.55f, .28f);
            var pl = UiKit.Row(pill.gameObject, 0, TextAnchor.MiddleCenter); pl.padding = new RectOffset(11, 11, 3, 3);
            var fit = pill.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            phaseT = UiKit.Text(pill.transform, "Phase", 13, Color.white); phaseT.horizontalOverflow = HorizontalWrapMode.Overflow;
            var pause = UiKit.Image(top.transform, "Pause", new Color(1, 1, 1, .16f), 12);
            var pb = pause.gameObject.AddComponent<Button>(); pb.onClick.AddListener(() => { if (!modalOpen) PauseMenu(); });
            var pzr = pause.rectTransform; pzr.anchorMin = pzr.anchorMax = pzr.pivot = new Vector2(1, .5f); pzr.sizeDelta = new Vector2(44, 44); pzr.anchoredPosition = new Vector2(-10, 0);
            UiKit.Center(UiKit.Icon(pause.transform, Procedural.PauseIcon, Color.white, 18).rectTransform, new Vector2(18, 18));

            // fact / text strip: never stops the game
            var facts = UiKit.Image(gameGO.transform, "Facts", UiKit.Panel); UiKit.Top(facts.rectTransform, TopH, FactsH);
            factCG = facts.gameObject.AddComponent<CanvasGroup>();
            factKick = UiKit.Text(facts.transform, "Kick", 12, Muted, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Top(factKick.rectTransform, 9, 16);
            factText = UiKit.Text(facts.transform, "Text", 15, UiKit.Ink, TextAnchor.UpperCenter, serif: true);
            factText.lineSpacing = 1.05f;
            UiKit.Stretch(factText.rectTransform, 18, 2, 18, 28);

            middle = UiKit.Rect(gameGO.transform, "Board area");
            toastBg = UiKit.Image(middle, "Toast", new Color(.09f, .23f, .13f, .93f), 12, false);
            var trt = toastBg.rectTransform; trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(.5f, 1); trt.anchoredPosition = new Vector2(0, -18);
            toastCG = toastBg.gameObject.AddComponent<CanvasGroup>(); toastCG.alpha = 0; toastCG.blocksRaycasts = false;
            toastText = UiKit.Text(toastBg.transform, "Text", 15, Color.white); UiKit.Stretch(toastText.rectTransform, 14, 8, 14, 8);
            cdWord = UiKit.Text(middle, "Ready", 26, UiKit.G2, TextAnchor.MiddleCenter, FontStyle.Bold, serif: true);
            cdNum = UiKit.Text(middle, "Count", 54, UiKit.G2, TextAnchor.MiddleCenter, FontStyle.Bold, serif: true);
            foreach (var t in new[] { cdWord, cdNum }) { t.horizontalOverflow = HorizontalWrapMode.Overflow; UiKit.Center(t.rectTransform, new Vector2(300, 80)); }
            hintBg = UiKit.Image(middle, "Hint", new Color(.09f, .23f, .13f, .82f), 10, false);
            var hrt = hintBg.rectTransform; hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(.5f, .5f);
            hintText = UiKit.Text(hintBg.transform, "Text", 13, Color.white); UiKit.Stretch(hintText.rectTransform); hintText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // on-screen arrows laid out like keyboard keys (inverted T)
            var pad = UiKit.Image(gameGO.transform, "Pad", UiKit.Panel);
            padGO = pad.gameObject; padRT = pad.rectTransform;
            padRT.anchorMin = new Vector2(0, 0); padRT.anchorMax = new Vector2(1, 0); padRT.pivot = new Vector2(.5f, 0); padRT.sizeDelta = new Vector2(0, PadH); padRT.anchoredPosition = Vector2.zero;
            PadKey(pad.transform, 0, Up, new Vector2(0, 31));
            PadKey(pad.transform, 90, Left, new Vector2(-70, -30));
            PadKey(pad.transform, 180, Down, new Vector2(0, -30));
            PadKey(pad.transform, -90, Right, new Vector2(70, -30));
        }

        void PadKey(Transform parent, float angle, Vector2Int d, Vector2 pos)
        {
            var img = UiKit.Image(parent, "Key " + d, Color.white, 10);
            UiKit.Center(img.rectTransform, new Vector2(64, 55), pos);
            img.gameObject.AddComponent<Outline>().effectColor = UiKit.Hex("#c3d3c4");
            UiKit.Drop(img, UiKit.Hex("#b3c4b4"), 4);
            var arrow = UiKit.Icon(img.transform, Procedural.Arrow, UiKit.Hex("#2b3a2f"), 24);
            UiKit.Center(arrow.rectTransform, new Vector2(24, 24)); arrow.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            var pbtn = img.gameObject.AddComponent<PressButton>();
            pbtn.OnPress = () => PushDir(d);
            padKeys[d] = pbtn;
        }

        void BuildOverlay()
        {
            overlayImg = UiKit.Image(canvasRT, "Overlay", new Color(.04f, .1f, .06f, .6f));
            UiKit.Stretch(overlayImg.rectTransform);
            overlayGO = overlayImg.gameObject;
            cardRT = UiKit.Rect(overlayImg.transform, "Card");
            cardRT.anchorMin = cardRT.anchorMax = cardRT.pivot = new Vector2(.5f, .5f); cardRT.sizeDelta = new Vector2(360, 0);
            UiKit.Column(cardRT.gameObject, 10, new RectOffset(22, 22, 24, 20));
            cardRT.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cardBg = UiKit.Image(cardRT, "BG", UiKit.Card, 20);
            UiKit.Stretch(cardBg.rectTransform);
            cardBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.SoftShadow(cardRT, 26, 8, .35f);   // inserted as the first child, behind the white card
            overlayGO.SetActive(false);
        }

        void LayoutColumn()
        {
            // keep everything inside the phone's safe area (notch, rounded corners, gesture bar)
            var sa = Screen.safeArea; float k = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1;
            float topInset = (Screen.height - sa.yMax) / k, bottomInset = sa.yMin / k;
            float cw = canvasRT.rect.width;
            column.sizeDelta = new Vector2(Mathf.Min(480, cw), 0);
            column.offsetMin = new Vector2(column.offsetMin.x, bottomInset); column.offsetMax = new Vector2(column.offsetMax.x, -topInset);
            cardRT.sizeDelta = new Vector2(Mathf.Min(360, cw - 28), cardRT.sizeDelta.y);
            float padH = padOn ? PadH : 0;
            padGO.SetActive(padOn);
            middle.anchorMin = Vector2.zero; middle.anchorMax = Vector2.one;
            middle.offsetMin = new Vector2(0, padH); middle.offsetMax = new Vector2(0, -(TopH + FactsH));
            toastBg.rectTransform.sizeDelta = new Vector2(Mathf.Min(400, middle.rect.width * .86f), toastBg.rectTransform.sizeDelta.y);

            // the board camera draws exactly into the free middle area of the column
            var c = new Vector3[4];
            middle.GetWorldCorners(c);
            var r = new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);   // the camera's clear colour is the panel colour
            if (r.width > 4 && r.height > 4)
            {
                cam.pixelRect = r;
                float half = N / 2f + .7f, aspect = r.width / r.height;
                cam.orthographicSize = Mathf.Max(half, half / aspect);
            }
            cam.enabled = inGame;
            cam.transform.position = new Vector3(N / 2f + shakeOffset.x, N / 2f + shakeOffset.y, -10);
        }

        /// <summary>One board cell in UI units (for text that sits on the board).</summary>
        float CellUi => cam.pixelHeight / (2f * cam.orthographicSize) / Mathf.Max(.01f, canvas.scaleFactor);

        // ---------- modal window ----------
        struct Btn { public string label; public int style; public Action on; public bool keep; }   // style: 0 normal, 1 primary, 2 warning
        static Btn B(string label, Action on, int style = 0, bool keep = false) => new Btn { label = label, on = on, style = style, keep = keep };

        void Modal(string kick = null, string title = null, string text = null, bool big = false, string src = null, bool dark = false, bool quiet = false, int demo = -1, Action<RectTransform> body = null, params Btn[] buttons)
        {
            modalOpen = true;
            this.demo = null;
            foreach (Transform ch in cardRT) if (ch.name != "BG" && ch.name != "Shadow") { ch.gameObject.SetActive(false); Destroy(ch.gameObject); }
            cardBg.color = dark ? UiKit.Hex("#1b1416") : UiKit.Card;
            overlayImg.color = dark ? new Color(0, 0, 0, .86f) : new Color(.04f, .1f, .06f, .6f);
            Color ink = dark ? UiKit.Hex("#f3e6e8") : UiKit.Ink, sub = dark ? new Color(1, 1, 1, .6f) : Muted;
            if (kick != null) { var k = UiKit.Text(cardRT, "Kick", 12, sub, TextAnchor.MiddleCenter, FontStyle.Bold); k.text = kick.ToUpperInvariant(); }
            if (title != null) { var t = UiKit.Text(cardRT, "Title", 23, ink, TextAnchor.MiddleCenter, FontStyle.Bold, serif: true); t.text = title; }
            if (demo >= 0) BuildDemo(demo);
            if (text != null) { var t = UiKit.Text(cardRT, "Text", big ? 24 : 18, ink, TextAnchor.MiddleCenter, FontStyle.Normal, serif: big); t.text = text; t.lineSpacing = 1.1f; }
            body?.Invoke(cardRT);
            if (src != null) { var t = UiKit.Text(cardRT, "Source", 13, sub); t.text = src; }
            if (buttons.Length > 0) UiKit.Size(UiKit.Rect(cardRT, "Gap"), h: 4);
            foreach (var b in buttons)
            {
                Color bg = b.style == 1 ? (dark ? UiKit.Wine : UiKit.G1) : b.style == 2 ? UiKit.Warn : (dark ? UiKit.Hex("#2c2225") : UiKit.Soft);
                Color fg = b.style == 1 ? Color.white : b.style == 2 ? UiKit.Hex("#a12626") : ink;
                Color? drop = b.style == 1 ? (dark ? UiKit.Hex("#4e1124") : UiKit.G2) : (Color?)null;
                var bb = b;
                UiKit.Button(cardRT, b.label, bg, fg, () => { if (!bb.keep) CloseModal(); bb.on?.Invoke(); }, 16, 50, 14, drop);
            }
            overlayGO.SetActive(true);
            overlayGO.transform.SetAsLastSibling();
            if (!quiet) Sfx("ding");
        }
        void CloseModal() { modalOpen = false; demo = null; overlayGO.SetActive(false); }

        // ---------- settings controls (rows with switches and segmented buttons, like the web version) ----------
        RectTransform SettingRow(RectTransform parent, string label, string sub)
        {
            var row = UiKit.Rect(parent, "Row " + label);
            var h = UiKit.Row(row.gameObject, 10); h.padding = new RectOffset(0, 0, 7, 7);
            UiKit.Size(row, h: sub != null ? 60 : 50);
            var col = UiKit.Rect(row, "Label");
            UiKit.Column(col.gameObject, 1, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(col, flex: 1);
            var t = UiKit.Text(col, "Text", 16, UiKit.Ink, TextAnchor.MiddleLeft); t.text = label; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (sub != null) { var s = UiKit.Text(col, "Sub", 12, Muted, TextAnchor.MiddleLeft); s.text = sub; }
            var line = UiKit.Image(row, "Line", UiKit.Hex("#edf3ed"), 0, false);
            var lrt = line.rectTransform; lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(.5f, 0); lrt.sizeDelta = new Vector2(0, 1);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return row;
        }

        void Segmented(RectTransform row, string[] values, string[] labels, string current, Action<string> pick)
        {
            var seg = UiKit.Image(row, "Segmented", UiKit.Soft, 12, false);
            var h = UiKit.Row(seg.gameObject, 2, TextAnchor.MiddleCenter); h.padding = new RectOffset(3, 3, 3, 3);
            for (int i = 0; i < values.Length; i++)
            {
                bool on = values[i] == current; string v = values[i];
                var opt = UiKit.Image(seg.transform, "Option", on ? UiKit.G1 : new Color(0, 0, 0, 0), 9);
                var b = opt.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => pick(v));
                var t = UiKit.Text(opt.transform, "Text", 13, on ? Color.white : UiKit.Ink, TextAnchor.MiddleCenter, on ? FontStyle.Bold : FontStyle.Normal);
                t.text = labels[i]; t.horizontalOverflow = HorizontalWrapMode.Overflow; UiKit.Stretch(t.rectTransform);
                UiKit.Size(opt, t.preferredWidth + 14, 40);
            }
        }

        void Toggle(RectTransform row, bool on, Action flip)
        {
            var track = UiKit.Image(row, "Switch", on ? UiKit.G1 : UiKit.Hex("#c9d6ca"), 15);
            UiKit.Size(track, 52, 30);
            track.gameObject.AddComponent<Button>().onClick.AddListener(() => flip());
            var knob = UiKit.Icon(track.transform, Procedural.Circle, Color.white, 24);
            var krt = knob.rectTransform; krt.anchorMin = krt.anchorMax = new Vector2(.5f, .5f); krt.anchoredPosition = new Vector2(on ? 11 : -11, 0);
        }

        // ---------- tutorial demos: a tiny animated board, like in the web version ----------
        const int DW = 10, DH = 6; const float DS = 30f, DemoSps = 3.2f;
        static readonly string[] DemoRoute = { "right", "right", "right", "right", "down", "down", "down", "left", "left", "left", "left", "left", "up", "up", "up", "right" };
        static readonly Dictionary<string, Vector2Int> DemoDir = new Dictionary<string, Vector2Int> { ["up"] = new Vector2Int(0, -1), ["down"] = new Vector2Int(0, 1), ["left"] = new Vector2Int(-1, 0), ["right"] = new Vector2Int(1, 0) };
        class Demo
        {
            public int kind, tick; public float acc;
            public string dir = "right";
            public List<Vector2Int> snake, prev; public Vector2Int? apple;
            public RectTransform board; public readonly List<Image> segs = new List<Image>();
            public Image appleImg; public float tongueT;
            public RectTransform headRT; public Image skull, tongue, forkA, forkB;
            public Image[] irises = new Image[2], pupils = new Image[2];
            public readonly List<Image> outl = new List<Image>(), body = new List<Image>(), pat = new List<Image>();
            public readonly Dictionary<string, Image> keys = new Dictionary<string, Image>();
            public readonly Dictionary<string, float> keyHit = new Dictionary<string, float>();
        }
        Demo demo;

        void BuildDemo(int kind)
        {
            var d = new Demo { kind = kind };
            d.snake = kind == 1 ? new List<Vector2Int> { new Vector2Int(5, 3), new Vector2Int(4, 3), new Vector2Int(3, 3), new Vector2Int(2, 3) }
                    : kind == 2 ? new List<Vector2Int> { new Vector2Int(3, 2), new Vector2Int(2, 2), new Vector2Int(1, 2), new Vector2Int(0, 2) }
                    : new List<Vector2Int> { new Vector2Int(4, 1), new Vector2Int(3, 1), new Vector2Int(2, 1), new Vector2Int(1, 1) };
            d.prev = new List<Vector2Int>(d.snake);
            if (kind == 2) d.apple = new Vector2Int(6, 2);

            var holder = UiKit.Rect(cardRT, "Demo");
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = DH * DS + 104;

            var frame = UiKit.Image(holder, "Board", UiKit.Hex("#d5e6d6"), 10, false);
            var frt = frame.rectTransform; frt.anchorMin = frt.anchorMax = new Vector2(.5f, 1); frt.pivot = new Vector2(.5f, 1);
            frt.sizeDelta = new Vector2(DW * DS + 4, DH * DS + 4); frt.anchoredPosition = new Vector2(0, -4);
            d.board = UiKit.Rect(frame.transform, "Cells");
            d.board.anchorMin = d.board.anchorMax = new Vector2(.5f, .5f); d.board.sizeDelta = new Vector2(DW * DS, DH * DS);
            d.board.gameObject.AddComponent<RectMask2D>();
            for (int y = 0; y < DH; y++)
            for (int x = 0; x < DW; x++)
            {
                var c = UiKit.Image(d.board, "Cell", (x + y) % 2 == 1 ? UiKit.Hex("#dcefdd") : UiKit.Hex("#e6f4e7"), raycast: false);
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = new Vector2(.5f, .5f);
                c.rectTransform.sizeDelta = new Vector2(DS, DS); c.rectTransform.anchoredPosition = DemoPos(x, y);
            }
            if (kind == 1)   // open edges: dashed-looking frame and arrows on both sides
            {
                var edge = new Color(.18f, .49f, .2f, .55f);
                foreach (var (min, max) in new[] { (new Vector2(0, 0), new Vector2(1, 0)), (new Vector2(0, 1), new Vector2(1, 1)), (new Vector2(0, 0), new Vector2(0, 1)), (new Vector2(1, 0), new Vector2(1, 1)) })
                {
                    var e = UiKit.Image(d.board, "Edge", edge, raycast: false); e.rectTransform.anchorMin = min; e.rectTransform.anchorMax = max; e.rectTransform.sizeDelta = new Vector2(3, 3);
                }
                foreach (var x in new[] { -DW * DS / 2 + 9, DW * DS / 2 - 9 })
                {
                    var a = UiKit.Text(d.board, "Arrow", 16, edge, TextAnchor.MiddleCenter, FontStyle.Bold); a.text = "→";
                    a.rectTransform.anchorMin = a.rectTransform.anchorMax = new Vector2(.5f, .5f); a.rectTransform.sizeDelta = new Vector2(20, 20); a.rectTransform.anchoredPosition = new Vector2(x, -.5f * DS);
                }
            }
            if (d.apple != null)
            {
                d.appleImg = UiKit.Image(d.board, "Apple", Color.white, raycast: false); d.appleImg.sprite = Procedural.Apple;
                d.appleImg.rectTransform.anchorMin = d.appleImg.rectTransform.anchorMax = new Vector2(.5f, .5f); d.appleImg.rectTransform.sizeDelta = new Vector2(DS * .9f, DS * .9f);
            }
            // the demo snake is drawn like the game's snake: outlined tapering body, then the head with snake eyes and tongue
            var look = Palettes["base"];
            for (int i = 0; i < 160; i++) { var o = DemoDisc(d.board, look.outline, DS); o.enabled = false; d.outl.Add(o); }
            for (int i = 0; i < 160; i++) { var b = DemoDisc(d.board, look.a, DS); b.enabled = false; d.body.Add(b); }
            for (int i = 0; i < 30; i++) { var q = UiKit.Image(d.board, "Scale", look.pattern, raycast: false); q.sprite = Procedural.Square; q.rectTransform.anchorMin = q.rectTransform.anchorMax = new Vector2(.5f, .5f); q.enabled = false; d.pat.Add(q); }
            d.headRT = UiKit.Rect(d.board, "Demo head");
            d.headRT.anchorMin = d.headRT.anchorMax = new Vector2(.5f, .5f); d.headRT.sizeDelta = Vector2.zero;
            var red = UiKit.Hex("#e53935");
            Image Part(string name, Sprite sp, Color c, Vector2 size, Vector2 pos, Vector2? pivot = null)
            {
                var im = UiKit.Image(d.headRT, name, c, raycast: false); im.sprite = sp;
                var rt = im.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = pivot ?? new Vector2(.5f, .5f);
                rt.sizeDelta = size; rt.anchoredPosition = pos; return im;
            }
            d.tongue = Part("Tongue", Procedural.Square, red, new Vector2(DS * .32f, DS * .06f), new Vector2(DS * .62f, 0), new Vector2(0, .5f));
            d.forkA = Part("Fork", Procedural.Square, red, new Vector2(DS * .13f, DS * .05f), new Vector2(DS * .92f, 0), new Vector2(0, .5f));
            d.forkB = Part("Fork", Procedural.Square, red, new Vector2(DS * .13f, DS * .05f), new Vector2(DS * .92f, 0), new Vector2(0, .5f));
            d.forkA.rectTransform.localEulerAngles = new Vector3(0, 0, 38); d.forkB.rectTransform.localEulerAngles = new Vector3(0, 0, -38);
            d.skull = Part("Skull", Procedural.SnakeHead, look.head, new Vector2(DS * 1.25f * .98f, DS * .9f), Vector2.zero, new Vector2(.42f, .5f));
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 1 : -1;
                d.irises[s] = Part("Iris", Procedural.Circle, look.eye, Vector2.one * DS * .24f, new Vector2(DS * .14f, DS * .27f * side));
                d.pupils[s] = Part("Slit pupil", Procedural.Circle, Color.black, new Vector2(DS * .17f, DS * .055f), new Vector2(DS * .155f, DS * .27f * side));
            }

            // keys laid out like a keyboard (inverted T); the pressed key lights up when the demo snake turns
            const float k = 38;
            foreach (var (name, angle, pos) in new[] { ("up", 0f, new Vector2(0, -DH * DS - 30)), ("left", 90f, new Vector2(-k - 6, -DH * DS - 30 - k * .86f - 6)), ("down", 180f, new Vector2(0, -DH * DS - 30 - k * .86f - 6)), ("right", -90f, new Vector2(k + 6, -DH * DS - 30 - k * .86f - 6)) })
            {
                var key = UiKit.Image(holder, "Key " + name, Color.white, 8, false);
                key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(.5f, 1); key.rectTransform.pivot = new Vector2(.5f, 1);
                key.rectTransform.sizeDelta = new Vector2(k, k * .86f); key.rectTransform.anchoredPosition = pos;
                key.gameObject.AddComponent<Outline>().effectColor = UiKit.Hex("#c3d3c4");
                UiKit.Drop(key, UiKit.Hex("#b3c4b4"), 3);
                var g = UiKit.Icon(key.transform, Procedural.Arrow, UiKit.Hex("#2b3a2f"), 15);
                UiKit.Center(g.rectTransform, new Vector2(15, 15)); g.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
                d.keys[name] = key; d.keyHit[name] = 0;
            }
            demo = d;
        }

        static Vector2 DemoPos(float x, float y) => new Vector2(x * DS + DS / 2 - DW * DS / 2, -(y * DS + DS / 2) + DH * DS / 2);
        static Image DemoDisc(Transform parent, Color c, float size)
        {
            var i = UiKit.Image(parent, "Disc", c, raycast: false); i.sprite = Procedural.Circle;
            i.rectTransform.anchorMin = i.rectTransform.anchorMax = new Vector2(.5f, .5f); i.rectTransform.sizeDelta = new Vector2(size, size);
            return i;
        }

        void DemoTick(Demo d)
        {
            d.tick++;
            string nd = d.dir;
            if (d.kind == 0) nd = DemoRoute[d.tick % DemoRoute.Length];
            if (d.kind == 2 && d.apple != null) { var h0 = d.snake[0]; nd = h0.y != d.apple.Value.y ? (d.apple.Value.y > h0.y ? "down" : "up") : "right"; }
            if (nd != d.dir) d.keyHit[nd] = .22f;
            d.dir = nd;
            var h = d.snake[0]; var v = DemoDir[d.dir];
            var nh = new Vector2Int((h.x + v.x + DW) % DW, (h.y + v.y + DH) % DH);
            d.prev = new List<Vector2Int>(d.snake);
            d.snake.Insert(0, nh);
            if (d.kind == 2 && d.apple != null && nh == d.apple.Value)
            {
                if (d.snake.Count > 8) d.snake = d.snake.GetRange(0, 3);
                d.apple = new Vector2Int((nh.x + 4 + Random.Range(0, 3)) % DW, Random.Range(0, DH));
            }
            else d.snake.RemoveAt(d.snake.Count - 1);
        }

        void DemoUpdate(float dt)
        {
            var d = demo; if (d == null || d.board == null) return;
            d.acc += dt * DemoSps;
            while (d.acc >= 1) { d.acc -= 1; DemoTick(d); }
            float k = d.acc;
            int n = d.snake.Count, used = 0;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var p = d.snake[i]; var q = i < d.prev.Count ? d.prev[i] : p;
                float dx = p.x - q.x, dy = p.y - q.y;
                if (dx > 1) dx -= DW; if (dx < -1) dx += DW; if (dy > 1) dy -= DH; if (dy < -1) dy += DH;
                pts[i] = new Vector2(q.x + dx * k, q.y + dy * k);
            }
            // sample the body densely along the (interpolated) cells so it reads as one smooth, tapering tube
            var look = Palettes["base"];
            var samples = new List<Vector2>(); var along = new List<float>();
            for (int i = 0; i < n; i++)
            {
                samples.Add(pts[i]); along.Add(i);
                if (i < n - 1 && Mathf.Abs(pts[i].x - pts[i + 1].x) <= 1.5f && Mathf.Abs(pts[i].y - pts[i + 1].y) <= 1.5f)
                    for (int t = 1; t < 7; t++) { samples.Add(Vector2.Lerp(pts[i], pts[i + 1], t / 7f)); along.Add(i + t / 7f); }
            }
            float len = Mathf.Max(1, n - 1 + k);
            float Width(float sp)
            {
                float w = .74f; if (sp < .6f) w = Mathf.Lerp(.62f, .74f, sp / .6f);
                float tail = Mathf.Min(2.2f, len * .55f), fromTail = (n - 1) - sp;
                if (fromTail < tail) w = Mathf.Min(w, Mathf.Lerp(.24f, .74f, Mathf.SmoothStep(0, 1, fromTail / tail)));
                return w;
            }
            int uo = 0, ub = 0;
            void Put(List<Image> pool, ref int used, Vector2 p, float size, Color c)
            {
                foreach (var ox in new[] { 0, DW, -DW })
                {
                    if (ox != 0 && !(p.x + ox > -1 && p.x + ox < DW)) continue;
                    if (used >= pool.Count) return;
                    var im = pool[used++]; im.enabled = true; im.color = c;
                    im.rectTransform.sizeDelta = new Vector2(size, size); im.rectTransform.anchoredPosition = DemoPos(p.x + ox, p.y);
                }
            }
            for (int i = samples.Count - 1; i >= 0; i--) Put(d.outl, ref uo, samples[i], (Width(along[i]) + .1f) * DS, look.outline);
            for (int i = samples.Count - 1; i >= 0; i--) Put(d.body, ref ub, samples[i], Width(along[i]) * DS, Color.Lerp(look.a, look.b, along[i] / Mathf.Max(1, n - 1)));
            for (int i = uo; i < d.outl.Count; i++) d.outl[i].enabled = false;
            for (int i = ub; i < d.body.Count; i++) d.body[i].enabled = false;
            // diamond pattern on the back, one per cell, like the game snake
            int up = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                float a = along[i]; if (a < .9f || Mathf.Abs(a - Mathf.Round(a)) > .01f || (n - 1) - a < .8f) continue;
                Put(d.pat, ref up, samples[i], Width(a) * DS * .3f, look.pattern);
            }
            for (int i = 0; i < up; i++) d.pat[i].rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            for (int i = up; i < d.pat.Count; i++) d.pat[i].enabled = false;
            // head on top, turned the way the snake moves; the tongue flicks out now and then
            var hv = DemoDir[d.dir];
            d.headRT.SetAsLastSibling();
            d.headRT.anchoredPosition = DemoPos(pts[0].x, pts[0].y);
            d.headRT.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(-hv.y, hv.x) * Mathf.Rad2Deg);
            d.tongueT += dt;
            bool tongueOut = d.tongueT % 1.6f < .3f;
            d.tongue.enabled = d.forkA.enabled = d.forkB.enabled = tongueOut;
            if (d.appleImg != null && d.apple != null) d.appleImg.rectTransform.anchoredPosition = DemoPos(d.apple.Value.x, d.apple.Value.y);
            foreach (var name in DemoDir.Keys)
            {
                d.keyHit[name] -= dt;
                bool on = d.keyHit[name] > 0;
                d.keys[name].color = on ? UiKit.Hex("#d3e8d4") : Color.white;
                d.keys[name].rectTransform.localScale = on ? new Vector3(.94f, .94f, 1) : Vector3.one;
            }
        }

        void Toast(string msg, float sec = 2.4f)
        {
            if (string.IsNullOrEmpty(msg)) return;
            toastText.text = msg;
            float h = toastText.preferredHeight;
            toastBg.rectTransform.sizeDelta = new Vector2(toastBg.rectTransform.sizeDelta.x, Mathf.Max(34, h + 18));
            toastTimer = sec;
        }
        void UpdateToast(float dt)
        {
            toastTimer -= dt;
            toastCG.alpha = Mathf.MoveTowards(toastCG.alpha, toastTimer > 0 ? 1 : 0, dt * 5);
        }

        // ================= SCREENS =================
        void ApplyLang()
        {
            menuTag.text = T("tagline");
            mStory.title.text = T("story"); mStory.sub.text = T("storySub");
            mCont.title.text = T("cont");
            mSimple.title.text = T("simple"); mSimple.sub.text = T("simpleSub");
            UiKit.SetLabel(bTut, T("tutorial")); UiKit.SetLabel(bSet, T("settings")); UiKit.SetLabel(bAbout, T("about"));
            RefreshMenu();
        }
        void RefreshMenu()
        {
            menuStats.text = T("stats", best, endings.Count);
            menuVer.text = T("version", Version);
            mCont.button.gameObject.SetActive(checkpoint != null);
            if (checkpoint != null) mCont.sub.text = L.Phase.TryGetValue(checkpoint.phase, out var p) ? p : "";
        }
        void ShowMenu()
        {
            inGame = false;
            menuGO.SetActive(true); gameGO.SetActive(false);
            RefreshMenu();
        }
        void ShowGame()
        {
            inGame = true;
            menuGO.SetActive(false); gameGO.SetActive(true);
        }

        // ================= GAME FLOW =================
        static Counters Fresh() => new Counters();

        void NewGame(string mode, string phase = null, Counters c = null)
        {
            G = new Game { mode = mode, phase = phase ?? (mode == "simple" ? "simple" : "tutorial"), c = c ?? Fresh(), lives = MaxLives };
            FxReset(); ResetSnake(); ShowFact();
            if (G.phase == "simple" || G.phase == "tutorial") { SpawnOne("apple", G.phase == "simple" ? SimpleAppleLife : float.PositiveInfinity); if (G.items.Count > 0) G.items[0].born -= PopSec; G.nextSpawn = G.t + 1.2f; }
            lastPhase = G.phase; Log("new game");
        }
        void ResetSnake()
        {
            int cx = N / 2, cy = N / 2;
            G.snake = new List<Vector2Int> { new Vector2Int(cx, cy), new Vector2Int(cx - 1, cy), new Vector2Int(cx - 2, cy) };
            G.prev = new List<Vector2Int>(G.snake);
            G.dir = Right; G.queue.Clear(); G.grow = 0; G.items.Clear(); G.acc = 0;
            G.nextSpawn = G.t + .3f; G.over = false; G.black.Clear(); fieldDirty = true; G.evil = false; G.fieldStart = -1; G.fieldDone = -1; G.spreadAcc = 0; G.dark = 0; G.respawnIn = 0; G.noEat = 0;
        }
        void RespawnSnake()
        {
            int cx = N / 2, cy = N / 2;
            G.snake = new List<Vector2Int> { new Vector2Int(cx, cy), new Vector2Int(cx - 1, cy), new Vector2Int(cx - 2, cy) };
            G.prev = new List<Vector2Int>(G.snake);
            G.dir = Right; G.queue.Clear(); G.grow = 0; G.acc = 0;
            G.items.RemoveAll(i => Mathf.Abs(i.p.y - cy) <= 1 && i.p.x >= cx - 3 && i.p.x <= cx + 4);   // nothing in the way at restart
        }
        void Countdown(float sec) => G.countdown = sec;

        float BaseSpeed() => speedKey == "slow" ? 2.75f : speedKey == "fast" ? 4.5f : 3.5f;
        float Speed()
        {
            if (G == null) return 1;
            if (G.evil) return G.evilSpeed;
            float b = BaseSpeed();
            if (G.phase == "simple") return b + Mathf.Min(1.5f, Mathf.Floor(G.score / 5f) * .15f);
            return b;
        }
        string SnakeLook()
        {
            if (G.evil) return "evil";
            if (G.phase == "tutorial" && G.c.total == 0 && G.noEat * 2 >= TH.starveSec) return "hungry";
            if (G.mode == "story" && G.c.total >= 10) return "fed";
            return "base";
        }

        void SaveCheckpoint()
        {
            if (G == null || G.mode != "story") return;
            if (G.phase == "walk" || G.phase == "greed")
            {
                checkpoint = new Checkpoint { phase = G.phase, total = G.c.total, dist = G.c.dist, fact = G.c.fact, greedLine = G.c.greedLine };
                SaveProgress();
            }
        }
        void ClearCheckpoint() { checkpoint = null; SaveProgress(); }

        void StartSimple() { CloseModal(); NewGame("simple"); ShowGame(); Countdown(1.8f); G.hintUntil = 5; }
        void OnStory()
        {
            if (checkpoint != null)
                Modal(title: T("restartQ"), text: T("storyRestartQ"), buttons: new[] { B(T("restart"), StartStory, 2), B(T("cancel"), null, 1) });
            else StartStory();
        }
        void StartStory()
        {
            ClearCheckpoint();
            NewGame("story"); ShowGame();
            Countdown(1.8f); G.hintUntil = 5;            // the tutorial is shown on the first launch and from the menu, not here
        }
        void ContinueStory()
        {
            var cp = checkpoint;
            if (cp == null || (cp.phase != "walk" && cp.phase != "greed")) { StartStory(); return; }
            NewGame("story", cp.phase, new Counters { total = cp.total, dist = cp.dist, fact = cp.fact, greedLine = cp.greedLine });
            ShowGame(); Countdown(1.8f);
        }
        void ToMenu() { Log("back to menu"); SaveCheckpoint(); G = null; FxReset(); CloseModal(); ShowMenu(); }

        // ================= INPUT =================
        bool swiping, swipeMoved; Vector2 swipeStart;
        PointerEventData ped; readonly List<RaycastResult> hits = new List<RaycastResult>();

        void PushDir(Vector2Int d)
        {
            if (G == null || modalOpen || G.over || G.evil) return;
            var last = G.queue.Count > 0 ? G.queue[G.queue.Count - 1] : G.dir;
            if (d == last || d == -last) return;           // no 180° turn
            if (G.queue.Count < 2) G.queue.Add(d);         // buffer up to 2 turns
        }

        bool OverButton(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            ped ??= new PointerEventData(EventSystem.current);
            ped.position = pos; hits.Clear();
            EventSystem.current.RaycastAll(ped, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<Selectable>() != null || h.gameObject.GetComponentInParent<PressButton>() != null) return true;
            return false;
        }

        void HandleInput()
        {
            if (!inGame) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                void Key(bool pressed, Vector2Int d) { if (!pressed) return; PushDir(d); if (padOn && padKeys.TryGetValue(d, out var k)) k.Flash(); }   // the on-screen key lights up too
                Key(kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame, Up);
                Key(kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame, Down);
                Key(kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame, Left);
                Key(kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame, Right);
                if ((kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) && !modalOpen) PauseMenu();
            }
            var ptr = Pointer.current;
            if (ptr == null) return;
            var pos = ptr.position.ReadValue();
            if (ptr.press.wasPressedThisFrame)
            {
                swiping = !modalOpen && !OverButton(pos);
                swipeStart = pos; swipeMoved = false;
            }
            else if (swiping && ptr.press.isPressed)
            {
                var d = pos - swipeStart;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) >= 22f * canvas.scaleFactor)
                {
                    PushDir(Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? Right : Left) : (d.y > 0 ? Up : Down));
                    swipeStart = pos; swipeMoved = true;          // chained turns in one gesture
                }
            }
            if (ptr.press.wasReleasedThisFrame && swiping)
            {
                swiping = false;
                if (!swipeMoved && ptr is Mouse && cam.pixelRect.Contains(pos)) ClickToward(pos);
            }
        }

        // mouse: click on the field -> turn towards the click
        void ClickToward(Vector2 screen)
        {
            if (G == null || modalOpen || G.over || G.evil) return;
            var wp = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            var h = G.snake[0];
            float dx = wp.x - (h.x + .5f), dy = (N - wp.y) - (h.y + .5f);
            if (Mathf.Abs(dx) < .5f && Mathf.Abs(dy) < .5f) return;
            var horiz = dx > 0 ? Right : Left; var vert = dy > 0 ? Down : Up;
            var main = Mathf.Abs(dx) >= Mathf.Abs(dy) ? horiz : vert; var side = main == horiz ? vert : horiz;
            var last = G.queue.Count > 0 ? G.queue[G.queue.Count - 1] : G.dir;
            PushDir(main == -last ? side : main);
        }

        // ================= MENUS =================
        void Resume() { if (G != null && !G.over) Countdown(1.2f); }
        void PauseMenu()
        {
            if (G == null || G.over) return;
            SaveCheckpoint();
            bool story = G.mode == "story";
            Modal(title: T("pause"), buttons: new[] {
                B(T("resume"), Resume, 1),
                B(story ? T("restartCp") : T("restart"), () => { if (story) RestartPhase(); else ConfirmSimpleRestart(); }),
                B(T("settings"), () => SettingsModal(PauseMenu)),
                B(T("toMenu"), ToMenu) });
        }
        // the record is saved only at game over, so restarting throws the run away: warn first
        void ConfirmSimpleRestart()
        {
            if (G == null || G.score == 0) { StartSimple(); return; }
            bool isBest = G.score > best;
            Modal(title: T("restartQ"), text: isBest ? T("restartLoseBest", G.score) : T("restartLose", G.score),
                  buttons: new[] { B(T("restart"), StartSimple, 2), B(T("cancel"), PauseMenu, 1) });
        }

        void Tutorial(int i, Action onDone, bool forStory)
        {
            var steps = L.Tut; int n = steps.Length; bool last = i == n - 1;
            var buttons = new List<Btn> { B(last ? (forStory ? T("play") : T("done")) : T("next"), () => { if (last) onDone?.Invoke(); else Tutorial(i + 1, onDone, forStory); }, 1) };
            if (!last) buttons.Add(B(T("skip"), () => onDone?.Invoke()));
            Modal(kick: T("stepOf", i + 1, n), title: steps[i].title, text: steps[i].text, quiet: i > 0, demo: i, buttons: buttons.ToArray());
        }

        void SettingsModal(Action after)
        {
            void Redraw() { SaveSettings(); SettingsModal(after); }
            Modal(title: T("settings"), quiet: true, body: card =>
            {
                Segmented(SettingRow(card, T("speed"), null), new[] { "slow", "normal", "fast" }, new[] { T("slow"), T("normal"), T("fast") }, speedKey, v => { speedKey = v; Redraw(); });
                Toggle(SettingRow(card, T("pad"), T("padSub")), padOn, () => { padOn = !padOn; Redraw(); });
                Toggle(SettingRow(card, T("sound"), null), soundOn, () => { soundOn = !soundOn; Redraw(); });
                Toggle(SettingRow(card, T("vibe"), null), vibeOn, () => { vibeOn = !vibeOn; Redraw(); });
                Toggle(SettingRow(card, T("shake"), T("shakeSub")), shakeOn, () => { shakeOn = !shakeOn; Redraw(); });
                Segmented(SettingRow(card, T("lang"), null), new[] { "ru", "en" }, new[] { "RU", "EN" }, lang, v => { lang = v; ApplyLang(); if (G != null) ShowFact(); Redraw(); });
            }, buttons: new[] {
                B(T("reset"), () => ConfirmReset(after), 2),
                B(T("done"), () => after?.Invoke(), 1) });
        }
        void ConfirmReset(Action after)
        {
            Modal(title: T("reset"), text: T("resetQ"), buttons: new[] {
                B(T("yes"), () => { best = 0; endings.Clear(); checkpoint = null; tutSeen = false; SaveProgress(); RefreshMenu(); Toast(T("resetDone")); after?.Invoke(); }, 2),
                B(T("cancel"), () => SettingsModal(after), 1) });
        }
        void AboutModal() => Modal(title: T("aboutTitle"), text: T("aboutBody"), src: T("aboutSrc") + "\n" + T("version", Version), buttons: new[] { B(T("close"), null, 1) });

        // ================= SPAWNING =================
        static int WrapDist(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Min(dx, N - dx) + Mathf.Min(dy, N - dy);
        }
        Vector2Int? FreeCell(int minDist)
        {
            var occ = new HashSet<Vector2Int>(G.snake);
            foreach (var i in G.items) occ.Add(i.p);
            var h = G.snake[0]; var cands = new List<Vector2Int>();
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2Int(x, y);
                if (occ.Contains(p) || G.black.Contains(x + y * N) || WrapDist(p, h) < minDist) continue;
                cands.Add(p);
            }
            return cands.Count > 0 ? cands[Random.Range(0, cands.Count)] : (Vector2Int?)null;
        }
        int MaxItems() => G.phase switch { "simple" => 2, "tutorial" => 1, "walk" => 2, "greed" => 3, _ => 0 };
        void SpawnOne(string type, float life)
        {
            var p = FreeCell(G.phase == "walk" ? 4 : 3) ?? FreeCell(1);
            if (p == null) return;
            G.items.Add(new Item { p = p.Value, type = type, born = G.t, life = life });
        }
        void SpawnLogic()
        {
            if (G.items.Count >= MaxItems() || G.t < G.nextSpawn) return;
            switch (G.phase)
            {
                case "tutorial": SpawnOne("apple", float.PositiveInfinity); break;   // no missed apples before "Мне хватит"
                case "simple": SpawnOne("apple", SimpleAppleLife); break;        // apples appear and disappear after 6 seconds
                case "walk": SpawnOne("apple", AppleLife); break;
                case "greed":
                    float r = Random.value;
                    SpawnOne(r < .5f ? "apple" : r < .75f ? "kitten" : "farmer", Random.Range(GreedLife.x, GreedLife.y)); break;
            }
            G.nextSpawn = G.t + (G.phase == "tutorial" ? 1.2f : G.phase == "greed" ? Random.Range(2.5f, 3.5f) : Random.Range(SpawnGap.x, SpawnGap.y));
        }
        void ExpireItems()
        {
            for (int i = G.items.Count - 1; i >= 0; i--)
            {
                var it = G.items[i];
                if (G.t >= it.born + it.life) { G.items.RemoveAt(i); OnExpire(it); if (modalOpen || G.over) return; }
            }
        }

        // ================= STORY =================
        /* tutorial : eat 5 apples -> "Мне хватит." -> walk;  0 eaten for 30 s -> Ending 4
           walk     : 10th apple -> "просто поползаем"; 15th -> "Посмотрим…" -> greed
                      crawl 300 cells (eating does not reset): eaten == 5 -> Ending 1, 6..14 -> Ending 2
           greed    : kittens +5, farmers +10, apples +1; 50 points -> evil snake -> Ending 3 */
        void StoryLine(string key, Action after) => Modal(text: T(key), big: true, buttons: new[] { B(T("ok"), () => { after?.Invoke(); Resume(); }, 1) });

        void ShowFact()
        {
            if (G == null) return;
            if (G.phase == "tutorial") { factKick.text = ""; factText.text = T("factsWait"); return; }
            if (G.mode == "simple")
            {
                var s = L.Simple; int i = G.c.fact % s.Length;
                factKick.text = T("factN", T("topicC"), i + 1, s.Length).ToUpperInvariant(); factText.text = s[i];
            }
            else
            {
                var h = L.Hunger; int i = G.c.fact % h.Length;
                factKick.text = T("factKick", i + 1, h.Length).ToUpperInvariant(); factText.text = h[i];
            }
            factCG.alpha = 0;
        }
        void FactTick(float dt)
        {
            if (G.phase == "tutorial") return;
            G.factT += dt;
            if (G.factT >= (G.mode == "simple" ? TH.textSec : TH.factSec)) { G.factT = 0; G.c.fact++; ShowFact(); }
        }
        void StoryStep()
        {
            if (G.mode != "story" || G.phase != "walk") return;
            G.c.dist++;
            if (G.c.dist >= TH.walkDist) { if (G.c.total <= TH.tut) EndingThanks(); else EndingChairs(); }
        }
        void StoryTick(float dt)
        {
            FactTick(dt);
            if (G.phase == "tutorial" && G.c.total == 0)
            {
                G.noEat += dt;
                if (G.noEat >= TH.starveSec) EndingCare();
            }
        }
        void OnExpire(Item it)
        {
            var soft = G.phase == "walk" ? UiKit.Hex("#9cbfa2") : UiKit.Hex("#d9a3a3");
            var c = Center(it.p);
            Burst(c, soft, 3, 4, 1.5f, 3); HitCircle(c, soft, .4f, .25f);
        }
        static Vector2 Center(Vector2Int p) => new Vector2(p.x + .5f, p.y + .5f);

        void EatItem(Item it)
        {
            G.grow++; Sfx("eat", .9f, 1.3f);
            Log($"ate {it.type} at {it.p.x},{it.p.y}");
            var col = ItemColor.TryGetValue(it.type, out var cc) ? cc : ItemColor["apple"];
            var c = Center(it.p);
            Burst(c, col); HitCircle(c, col);
            headX += .3f;                       // head squash-and-stretch (spring pull)
            bulges.Add(0);                      // swallowed lump travels down the body
            if (G.evil) Shake(.15f, .2f);
            var k = G.c;
            switch (G.phase)
            {
                case "simple": G.score++; break;
                case "tutorial":
                    k.total++;
                    if (k.total >= TH.tut) { G.items.Clear(); StoryLine("enough", () => { G.phase = "walk"; G.nextSpawn = G.t + .5f; G.factT = 0; SaveCheckpoint(); ShowFact(); }); }
                    break;
                case "walk":
                    k.total++;
                    if (k.total >= TH.see) { G.items.Clear(); StoryLine("letsSee", () => { G.phase = "greed"; G.nextSpawn = G.t + .8f; SaveCheckpoint(); Toast(T("greedHint"), 3.5f); }); }
                    else if (k.total == TH.walk) StoryLine("walkLine", null);
                    break;
                case "greed":
                    int pts = Points.TryGetValue(it.type, out var p) ? p : 1, before = k.total;
                    k.total += pts;
                    floaters.Add(new Floater { p = new Vector2(c.x, c.y - .3f), text = "+" + pts, c = col });
                    if (k.total >= TH.greed) { StartEvil(); break; }
                    if (it.type != "apple") Toast(L.Touch.TryGetValue(it.type, out var tt) ? tt : "", 2.2f);
                    else if (k.total / 5 > before / 5) Toast(L.Greed[k.greedLine++ % L.Greed.Length], 2.6f);
                    break;
                case "evil":
                    if (G.evil) G.evilSpeed = Mathf.Min(EvilMax, G.evilSpeed + .35f);
                    break;
            }
        }
        void RestartPhase()
        {
            string ph = G.evil ? "greed" : G.phase;      // story checkpoint = start of the current phase, counters persist
            if (ph == "tutorial") G.c = Fresh();
            G.phase = ph; G.lives = MaxLives; ResetSnake(); Countdown(1.8f); ShowFact();
        }
        void CrashFx()
        {
            var h = G.snake[0]; var col = Palettes[SnakeLook()].head; var c = Center(h);
            Burst(c, col, 6, 9, 4, 9); HitCircle(c, col, .9f, .3f);
            hitFlash = .15f; headX += .35f; Shake(.3f, .5f); SlowMo(.25f, 1f);
        }
        void Die()
        {
            G.lives = Mathf.Max(0, G.lives - 1); G.hurt = .6f;
            Log("crashed into itself");
            CrashFx();
            G.flash = .35f; Sfx("die"); Buzz();
            if (G.lives > 0) { Toast(T("lifeLost", G.lives), 1.8f); G.respawnIn = .55f; return; }   // let the crash be seen, then respawn
            G.over = true;
            StartCoroutine(After(.45f, () =>
            {
                if (G == null) return;
                if (G.mode == "simple")
                {
                    bool rec = G.score > best; if (rec) { best = G.score; SaveProgress(); }
                    Modal(title: T("over"), text: T("score", G.score) + "\n" + (rec ? T("newBest") : T("best", best)),
                          buttons: new[] { B(T("again"), StartSimple, 1), B(T("toMenu"), ToMenu) });
                }
                else
                {
                    SaveCheckpoint();
                    Modal(title: T("over"), buttons: new[] { B(T("restartCp"), RestartPhase, 1), B(T("toMenu"), ToMenu) });
                }
            }));
        }
        IEnumerator After(float sec, Action a) { yield return new WaitForSecondsRealtime(sec); a(); }

        // ---------- evil snake ----------
        void StartEvil()
        {
            G.phase = "evil"; G.evil = true; G.queue.Clear(); G.flash = .6f;
            G.evilSpeed = Mathf.Min(EvilMax, BaseSpeed() * 1.8f); G.fieldStart = -1;
            Sfx("growl"); Buzz(); Toast(T("evilToast"), 2.6f);
            Burst(Center(G.snake[0]), UiKit.Hex("#b23a48"), 10, 14, 4, 10);
            hitFlash = .2f; headX += .5f; Shake(.45f, 1f); SlowMo(.4f, .8f);
            G.items.Clear();
            foreach (var ty in new[] { "apple", "kitten", "farmer", "apple", "apple", "farmer", "kitten" }) SpawnOne(ty, float.PositiveInfinity);
        }
        Vector2Int Autopilot()
        {
            var h = G.snake[0];
            Vector2Int? target = null; int bestD = int.MaxValue;
            if (G.items.Count > 0) { foreach (var i in G.items) { int d = WrapDist(h, i.p); if (d < bestD) { bestD = d; target = i.p; } } }
            else
            {
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (G.black.Contains(x + y * N)) continue;
                    int d = WrapDist(h, new Vector2Int(x, y)); if (d > 0 && d < bestD) { bestD = d; target = new Vector2Int(x, y); }
                }
            }
            if (target == null) return G.dir;
            int dx = target.Value.x - h.x, dy = target.Value.y - h.y;
            if (dx > N / 2) dx -= N; if (dx < -N / 2) dx += N;
            if (dy > N / 2) dy -= N; if (dy < -N / 2) dy += N;
            var opts = new List<(Vector2Int d, int w)>();
            if (dx != 0) opts.Add((dx > 0 ? Right : Left, Mathf.Abs(dx)));
            if (dy != 0) opts.Add((dy > 0 ? Down : Up, Mathf.Abs(dy)));
            opts.Sort((a, b) => b.w.CompareTo(a.w));
            foreach (var o in opts) if (o.d != -G.dir) return o.d;
            return G.dir.x != 0 ? Down : Right;              // only option is a U-turn: sidestep first
        }

        // ---------- endings ----------
        void EndingThanks()          // Ending 1 (good): stopped at exactly 5 apples
        {
            Log("ending 1: thanks");
            G.over = true; endings.Add("thanks"); ClearCheckpoint(); Sfx("warm"); G.items.Clear();
            Modal(kick: T("e1Kick"), title: T("e1Title"), text: T("e1Text"), big: true, buttons: new[] { B(T("mainMenu"), ToMenu, 1), B(T("playAgain"), StartStory) });
        }
        void EndingChairs()          // Ending 2: ate 6–14, then stopped
        {
            Log("ending 2: two chairs");
            G.over = true; endings.Add("chairs"); ClearCheckpoint(); Sfx("warm"); G.items.Clear();
            Modal(kick: T("e2Kick"), title: T("e2Title"), text: T("e2Text"), big: true, buttons: new[] { B(T("mainMenu"), ToMenu, 1), B(T("playAgain"), StartStory) });
        }
        void EndingCare()            // Ending 4: ate nothing at all
        {
            Log("ending 4: care");
            G.over = true; endings.Add("care"); ClearCheckpoint(); Sfx("miss");
            Modal(kick: T("e4Kick"), title: T("e4Title"), text: T("e4Text"), big: true, buttons: new[] { B(T("playAgain"), StartStory, 1), B(T("mainMenu"), ToMenu) });
        }
        void EndingGreed()           // Ending 3 (bad): 50 points
        {
            Log("ending 3: ignorance");
            Shake(.5f, 1f);
            G.over = true; endings.Add("greed"); ClearCheckpoint(); Sfx("boom");
            Modal(kick: T("e3Kick"), title: T("e3Title"), text: T("e3Text"), dark: true, buttons: new[] { B(T("newRoute"), NewRoute, 1), B(T("mainMenu"), ToMenu) });
        }
        void NewRoute()
        {
            NewGame("story", "walk", new Counters { total = TH.tut });   // start right after "Мне хватит" with exactly 5 eaten
            ShowGame(); SaveCheckpoint(); Countdown(1.8f);
            Toast(T("routeHint"), 3.5f);
        }

        // ================= SIMULATION =================
        void Step()
        {
            if (G.evil) G.dir = Autopilot();
            else if (G.queue.Count > 0) { G.dir = G.queue[0]; G.queue.RemoveAt(0); }
            var h = G.snake[0];
            var nh = new Vector2Int((h.x + G.dir.x + N) % N, (h.y + G.dir.y + N) % N);   // wrap-around
            G.prev = new List<Vector2Int>(G.snake);
            bool growing = G.grow > 0;
            int bodyLen = growing ? G.snake.Count : G.snake.Count - 1;
            if (!G.evil) for (int i = 0; i < bodyLen; i++) if (G.snake[i] == nh) { Die(); return; }
            G.snake.Insert(0, nh);
            if (growing) G.grow--; else G.snake.RemoveAt(G.snake.Count - 1);
            int idx = G.items.FindIndex(i => i.p == nh);
            if (idx >= 0) { var it = G.items[idx]; G.items.RemoveAt(idx); EatItem(it); }
            StoryStep();
            if (G.evil && G != null)
            {
                if (G.items.Count == 0 && G.fieldStart < 0) { G.fieldStart = G.t; G.evilSpeed = 12f; Sfx("growl"); Log("evil snake starts eating the field"); }   // nothing left to eat: it starts on the field itself
                if (G.fieldStart >= 0)
                {
                    int r = G.t - G.fieldStart < 1.5f ? 1 : 2;          // the bite grows: 3×3, then 5×5
                    for (int oy = -r; oy <= r; oy++) for (int ox = -r; ox <= r; ox++)
                    {
                        if (Mathf.Abs(ox) == r && Mathf.Abs(oy) == r && r > 1) continue;   // rounded bite
                        G.black.Add((nh.x + ox + N) % N + ((nh.y + oy + N) % N) * N);
                    }
                    fieldDirty = true;
                }
            }
        }

        void Tick(float dt)
        {
            if (G.phase != lastPhase) { Log($"phase {lastPhase} -> {G.phase}"); lastPhase = G.phase; }
            if (G.flash > 0) G.flash -= dt;
            if (G.hurt > 0) G.hurt -= dt;
            if (modalOpen || G.over) return;
            if (G.respawnIn > 0) { G.respawnIn -= dt; if (G.respawnIn <= 0) { RespawnSnake(); Countdown(1.8f); } return; }
            if (G.countdown > 0) { G.countdown -= dt; return; }
            G.t += dt;
            if (G.hintUntil > 0) G.hintUntil -= dt;
            ExpireItems(); if (modalOpen || G.over) return;
            SpawnLogic();
            G.acc += dt;
            float st = 1f / Speed();
            while (G != null && G.acc >= st && !modalOpen && !G.over && !(G.respawnIn > 0)) { G.acc -= st; Step(); st = 1f / Speed(); }
            if (G == null) return;
            for (int i = bulges.Count - 1; i >= 0; i--) { bulges[i] += Speed() * dt; if (bulges[i] > G.snake.Count) bulges.RemoveAt(i); }
            if (!modalOpen && !G.over) { if (G.mode == "story") StoryTick(dt); else FactTick(dt); }
            if (G.evil && G.fieldStart >= 0 && !G.over)
            {
                // The snake eats the field; after a while the black also spreads from what it has eaten, like ink,
                // so the whole grid always ends up black. Then the screen fades out and the ending appears.
                float e = G.t - G.fieldStart;
                if (G.fieldDone < 0)
                {
                    if (e >= 3f)
                    {
                        G.spreadAcc += dt;
                        if (G.spreadAcc >= .1f)
                        {
                            G.spreadAcc = 0;
                            float chance = Mathf.Min(.9f, .15f + (e - 3f) * .12f);
                            var add = new List<int>();
                            for (int y = 0; y < N; y++)
                            for (int x = 0; x < N; x++)
                            {
                                int id = x + y * N; if (G.black.Contains(id)) continue;
                                bool near = G.black.Contains((x + 1) % N + y * N) || G.black.Contains((x + N - 1) % N + y * N)
                                         || G.black.Contains(x + ((y + 1) % N) * N) || G.black.Contains(x + ((y + N - 1) % N) * N);
                                if (near && UnityEngine.Random.value < chance) add.Add(id);
                            }
                            foreach (var id in add) G.black.Add(id);
                            if (add.Count > 0) fieldDirty = true;
                        }
                    }
                    if (e >= 12f) for (int id = 0; id < N * N; id++) G.black.Add(id);   // safety net
                    if (G.black.Count >= N * N) { Log("the whole field is black"); G.fieldDone = G.t; fieldDirty = true; Shake(.3f, .6f); }
                }
                else
                {
                    float d = G.t - G.fieldDone;
                    G.dark = Mathf.Clamp01((d - .5f) / 1.2f);          // the snake circles the black field, then everything goes dark
                    if (d >= 2f) EndingGreed();
                }
            }
        }

        // ================= FX =================
        void FxReset()
        {
            parts.Clear(); circs.Clear(); shakes.Clear(); floaters.Clear(); bulges.Clear();
            slow = 1; slowDur = 0; headX = 1; headV = 0; hitFlash = 0; shakeOffset = Vector2.zero;
        }
        // burst of small rectangles flying out and shrinking (SNKRX HitParticle)
        void Burst(Vector2 c, Color col, int nMin = 4, int nMax = 6, float sMin = 3, float sMax = 7)
        {
            int n = Random.Range(nMin, nMax + 1);
            for (int i = 0; i < n; i++) parts.Add(new Part { p = c, a = Random.value * Mathf.PI * 2, v0 = Random.Range(sMin, sMax), dur = Random.Range(.2f, .6f), w0 = Random.Range(.2f, .36f), c = col });
        }
        // circle that flashes white, then takes the colour and shrinks (SNKRX HitCircle)
        void HitCircle(Vector2 c, Color col, float r = .6f, float dur = .22f) => circs.Add(new Circ { p = c, r0 = r, dur = dur, c = col });
        // interpolated noise shake that decays linearly (SNKRX Shake); amp in cells
        void Shake(float amp, float dur, float freq = 60)
        {
            if (!shakeOn) return;
            int n = Mathf.CeilToInt(dur * freq) + 2;
            var s = new Shk { amp = amp, dur = dur, freq = freq, nx = new float[n], ny = new float[n] };
            for (int i = 0; i < n; i++) { s.nx[i] = Random.value * 2 - 1; s.ny[i] = Random.value * 2 - 1; }
            shakes.Add(s);
        }
        // time scale drops and eases back to 1 (SNKRX slow()); the game never touches Time.timeScale
        void SlowMo(float amount, float dur) { slow = slowFrom = amount; slowT = 0; slowDur = dur; }
        static float Ease(float k) => k < .5f ? 4 * k * k * k : 1 - Mathf.Pow(-2 * k + 2, 3) / 2;

        void FxUpdate(float dt)
        {
            if (slowDur > 0) { slowT += dt; float k = Mathf.Min(1, slowT / slowDur); slow = slowFrom + (1 - slowFrom) * Ease(k); if (k >= 1) slowDur = 0; }
            float a = -200f * (headX - 1f) - 10f * headV; headV += a * dt; headX += headV * dt;   // damped spring (k=200, d=10)
            if (hitFlash > 0) hitFlash -= dt;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i]; p.t += dt; if (p.t >= p.dur) { parts.RemoveAt(i); continue; }
                float e = Ease(p.t / p.dur), v = p.v0 * (1 - e);
                p.p += new Vector2(Mathf.Cos(p.a), Mathf.Sin(p.a)) * v * dt; p.w = p.w0 + (.08f - p.w0) * e;
            }
            for (int i = circs.Count - 1; i >= 0; i--) { circs[i].t += dt; if (circs[i].t >= circs[i].dur) circs.RemoveAt(i); }
            for (int i = floaters.Count - 1; i >= 0; i--) { floaters[i].t += dt; if (floaters[i].t >= floaters[i].dur) floaters.RemoveAt(i); }
            Vector2 off = Vector2.zero;
            for (int i = shakes.Count - 1; i >= 0; i--)
            {
                var s = shakes[i]; s.t += dt; if (s.t >= s.dur) { shakes.RemoveAt(i); continue; }
                float q = s.t * s.freq; int i0 = Mathf.FloorToInt(q); float f = q - i0, decay = (s.dur - s.t) / s.dur;
                off.x += s.amp * (s.nx[i0] + f * (s.nx[i0 + 1] - s.nx[i0])) * decay;
                off.y += s.amp * (s.ny[i0] + f * (s.ny[i0 + 1] - s.ny[i0])) * decay;
            }
            shakeOffset = shakeOn ? off : Vector2.zero;
        }

        // ================= RENDER =================
        void Render()
        {
            if (fieldDirty)
            {
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color32 c = G.black.Contains(x + y * N) ? new Color32(5, 5, 5, 255) : ((x + y) % 2 == 1 ? new Color32(220, 239, 221, 255) : new Color32(230, 244, 231, 255));
                    px[(N - 1 - y) * N + x] = c;
                }
                fieldTex.SetPixels32(px); fieldTex.Apply(); fieldDirty = false;
            }

            // items: icon + timer arc that empties clockwise (like the web version)
            for (int i = 0; i < Mathf.Max(itemPool.Count, G.items.Count); i++)
            {
                if (i >= itemPool.Count) itemPool.Add(new ItemView { icon = SR("Item", Procedural.Apple, 3, Color.white), arc = SR("Timer", Procedural.Arc(1), 2, Color.white) });
                var view = itemPool[i];
                bool on = i < G.items.Count;
                view.icon.enabled = on; view.arc.enabled = false;
                if (!on) continue;
                var it = G.items[i];
                view.icon.sprite = it.type == "kitten" ? Procedural.Kitten : it.type == "farmer" ? Procedural.Farmer : Procedural.Apple;
                float rem = it.born + it.life - G.t, age = G.t - it.born, pop = Mathf.Min(1, age / PopSec);
                float sc = pop < 1 ? .3f + .7f * (1 - Mathf.Pow(1 - pop, 3)) + Mathf.Sin(pop * Mathf.PI) * .12f : 1;
                float alpha = !float.IsInfinity(rem) && rem < WarnSec ? .55f + .45f * Mathf.Cos((WarnSec - rem) * Mathf.PI * 2 * (rem < .7f ? 4 : 2)) : 1;
                view.icon.transform.localPosition = W(it.p.x, it.p.y);
                view.icon.transform.localScale = Vector3.one * (it.type == "apple" ? 1.15f : 1.08f) * sc;
                view.icon.color = new Color(1, 1, 1, alpha);
                if (!float.IsInfinity(it.life))
                {
                    view.arc.enabled = true;
                    view.arc.sprite = Procedural.Arc(Mathf.Max(0, rem / it.life));
                    view.arc.transform.localPosition = W(it.p.x, it.p.y);
                    view.arc.transform.localScale = Vector3.one * 1.1f * sc;
                    var rc = G.phase == "greed" && it.type != "apple" ? new Color(.09f, .23f, .13f, .55f) : new Color(.83f, .18f, .18f, .6f);
                    rc.a *= alpha; view.arc.color = rc;
                }
            }

            // snake: one smooth tapering tube with an outline and a diamond pattern on the back; it sways while crawling
            segUsed = 0; patUsed = 0;
            var look = Palettes[SnakeLook()];
            float tt = Mathf.Clamp01(G.acc * Speed());
            int len = G.snake.Count;
            var pts = new Vector2[len];
            for (int i = 0; i < len; i++)
            {
                var p = G.snake[i]; var q = i < G.prev.Count ? G.prev[i] : p;
                float dx = p.x - q.x, dy = p.y - q.y;
                if (dx > 1) dx -= N; if (dx < -1) dx += N; if (dy > 1) dy -= N; if (dy < -1) dy += N;
                pts[i] = new Vector2(q.x + dx * tt, q.y + dy * tt);
            }
            // unwrap so neighbours are always next to each other (the ghost copies handle the wrapped edges)
            for (int i = 1; i < len; i++)
            {
                var d = pts[i] - pts[i - 1];
                if (d.x > N / 2f) pts[i].x -= N; if (d.x < -N / 2f) pts[i].x += N;
                if (d.y > N / 2f) pts[i].y -= N; if (d.y < -N / 2f) pts[i].y += N;
            }
            bool moving = !modalOpen && !G.over && G.countdown <= 0 && !(G.respawnIn > 0);
            if (moving) slither += Time.unscaledDeltaTime * slow;
            bool white = hitFlash > 0;
            const int Steps = 5;                       // samples per cell
            int n = (len - 1) * Steps + 1;
            if (bodyBuf.Length < n) { bodyBuf = new Vector2[n * 2]; bodyS = new float[n * 2]; swayBuf = new Vector2[n * 2]; }
            for (int i = 0; i < len - 1; i++)
                for (int k = 0; k < Steps; k++) { bodyBuf[i * Steps + k] = Vector2.Lerp(pts[i], pts[i + 1], k / (float)Steps); bodyS[i * Steps + k] = i + k / (float)Steps; }
            bodyBuf[n - 1] = pts[len - 1]; bodyS[n - 1] = len - 1;
            // side-to-side sway (S-shape), none at the head so steering stays exact
            for (int j = 0; j < n; j++)
            {
                var t0 = bodyBuf[Mathf.Max(0, j - 1)]; var t1 = bodyBuf[Mathf.Min(n - 1, j + 1)];
                var tan = (t1 - t0); if (tan.sqrMagnitude < 1e-6f) continue;
                tan.Normalize();
                float sv = bodyS[j], env = Mathf.Clamp01(sv / 1.6f) * (moving || G.over ? 1 : .6f);
                swayBuf[j] = new Vector2(-tan.y, tan.x) * (.075f * env * Mathf.Sin(sv * 2.2f - slither * 7f));
            }
            for (int pass = 0; pass < 2; pass++)            // pass 0: dark outline, pass 1: body colour
            {
                for (int j = n - 1; j >= 0; j--)
                {
                    float sv = bodyS[j], w = BodyWidth(sv, len);
                    Color col = pass == 0 ? (white ? new Color(.85f, .85f, .85f) : look.outline)
                                          : (white ? Color.white : Color.Lerp(look.a, look.b, sv / Mathf.Max(1, len - 1)));
                    Disc(bodyBuf[j] + swayBuf[j], pass == 0 ? w + .1f : w, col, pass == 0 ? 3 : 4);
                }
            }
            // diamond scales along the back, every half cell
            if (!white)
                for (int j = Steps; j < n - Steps * 2; j += Steps / 2 + (j / Steps % 2))
                {
                    float sv = bodyS[j], w = BodyWidth(sv, len);
                    var a = bodyBuf[Mathf.Max(0, j - 1)] + swayBuf[Mathf.Max(0, j - 1)]; var b = bodyBuf[Mathf.Min(n - 1, j + 1)] + swayBuf[Mathf.Min(n - 1, j + 1)];
                    float ang = Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg;
                    foreach (var o in Ghosts(bodyBuf[j] + swayBuf[j]))
                    {
                        var r = Pat(); r.color = look.pattern;
                        r.transform.localPosition = W(o.x, o.y);
                        r.transform.localRotation = Quaternion.Euler(0, 0, ang + 45);
                        r.transform.localScale = new Vector3(w * .36f, w * .36f, 1);
                    }
                }
            for (int i = segUsed; i < segPool.Count; i++) segPool[i].enabled = false;
            for (int i = patUsed; i < patPool.Count; i++) patPool[i].enabled = false;
            DrawHead(pts[0], look, white);

            // fx
            fxUsed = 0;
            foreach (var c in circs)
            {
                float e = Ease(c.t / c.dur);
                var r = Fx(Procedural.Circle, 10); r.color = c.t < c.dur / 2 ? Color.white : c.c;
                r.transform.localPosition = W(c.p.x - .5f, c.p.y - .5f); r.transform.localScale = Vector3.one * Mathf.Max(0, c.r0 * (1 - e)) * 2;
            }
            foreach (var p in parts)
            {
                var r = Fx(Procedural.Square, 11); r.color = p.c;
                r.transform.localPosition = W(p.p.x - .5f, p.p.y - .5f);
                r.transform.localRotation = Quaternion.Euler(0, 0, -p.a * Mathf.Rad2Deg);
                r.transform.localScale = new Vector3(p.w, Mathf.Max(.06f, p.w / 2), 1);
            }
            for (int i = fxUsed; i < fxPool.Count; i++) fxPool[i].enabled = false;

            // overlays
            darkOv.color = new Color(0, 0, 0, G.dark);
            flashOv.color = G.flash > 0 ? (G.evil ? new Color(.7f, 0, .12f, G.flash * .6f) : new Color(0, 0, 0, G.flash * .6f)) : Color.clear;
            bool cd = G.countdown > 0 && !modalOpen;
            cdOv.color = cd ? new Color(.92f, .96f, .92f, .55f) : Color.clear;
            float cell = CellUi;
            cdWord.enabled = cdNum.enabled = cd;
            if (cd)
            {
                cdWord.text = T("ready"); cdWord.fontSize = Mathf.RoundToInt(cell * 1.1f); cdWord.rectTransform.anchoredPosition = new Vector2(0, cell * 1.6f);
                cdNum.text = Mathf.CeilToInt(G.countdown / .6f).ToString(); cdNum.fontSize = Mathf.RoundToInt(cell * 2.2f); cdNum.rectTransform.anchoredPosition = new Vector2(0, -cell * .6f);
            }
            bool hint = G.hintUntil > 0 && G.countdown <= 0 && !modalOpen;
            hintBg.gameObject.SetActive(hint);
            if (hint)
            {
                hintText.text = T("hint"); hintText.fontSize = Mathf.Max(12, Mathf.RoundToInt(cell * .62f));
                hintBg.rectTransform.sizeDelta = new Vector2(hintText.preferredWidth + cell, cell * 1.3f);
                hintBg.rectTransform.anchoredPosition = new Vector2(0, -(N / 2f - 1.45f) * cell);
                var hc = hintBg.color; hc.a = .82f * Mathf.Min(1, G.hintUntil); hintBg.color = hc;
            }
            factCG.alpha = Mathf.MoveTowards(factCG.alpha, 1, Time.unscaledDeltaTime * 4);

            // floating "+5"
            int used = 0;
            foreach (var f in floaters)
            {
                if (used >= floatPool.Count)
                {
                    var t = UiKit.Text(middle, "Float", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, serif: true);
                    t.rectTransform.sizeDelta = new Vector2(90, 40); t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var o = t.gameObject.AddComponent<Outline>(); o.effectColor = Color.white; o.effectDistance = new Vector2(1.5f, -1.5f);
                    floatPool.Add(t);
                }
                var ft = floatPool[used++]; ft.enabled = true;
                float k = f.t / f.dur, pop = 1 + .4f * Mathf.Max(0, 1 - k * 5);
                var sp = cam.WorldToScreenPoint(W(f.p.x - .5f, f.p.y - .5f - k * 1.2f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(middle, sp, null, out var lp);
                ft.rectTransform.anchorMin = ft.rectTransform.anchorMax = new Vector2(.5f, .5f);
                ft.rectTransform.anchoredPosition = lp;
                ft.fontSize = Mathf.RoundToInt(cell * .8f * pop);
                var fc = f.c; fc.a = 1 - Ease(k); ft.color = fc; ft.text = f.text;
            }
            for (int i = used; i < floatPool.Count; i++) floatPool[i].enabled = false;

            UpdateHud();
        }

        // body width at position s along the snake (0 = head): slim neck behind the head, full body, tail tapering to a point
        float BodyWidth(float s, int len)
        {
            const float full = .74f;
            float w = full;
            if (s < .6f) w = Mathf.Lerp(.62f, full, s / .6f);
            float tailLen = Mathf.Min(3f, (len - 1) * .55f), fromTail = (len - 1) - s;
            if (fromTail < tailLen) w = Mathf.Min(w, Mathf.Lerp(len < 6 ? .26f : .16f, full, Mathf.SmoothStep(0, 1, fromTail / tailLen)));
            foreach (var bl in bulges) w *= 1 + .38f * Mathf.Max(0, 1 - Mathf.Abs(s - bl) / 1.3f);   // swallowed lump
            return w;
        }

        // a body disc, plus copies across the wrapped edges so the snake slides through them
        void Disc(Vector2 p, float d, Color c, int order)
        {
            foreach (var o in Ghosts(p))
            {
                var r = Seg(); r.color = c; r.sortingOrder = order;
                r.transform.localPosition = W(o.x, o.y); r.transform.localScale = Vector3.one * d;
            }
        }
        static readonly List<Vector2> ghostBuf = new List<Vector2>();
        static List<Vector2> Ghosts(Vector2 p)
        {
            ghostBuf.Clear(); ghostBuf.Add(p);
            float gx = p.x < .5f ? N : p.x > N - 1.5f ? -N : 0, gy = p.y < .5f ? N : p.y > N - 1.5f ? -N : 0;
            if (gx != 0) ghostBuf.Add(new Vector2(p.x + gx, p.y));
            if (gy != 0) ghostBuf.Add(new Vector2(p.x, p.y + gy));
            if (gx != 0 && gy != 0) ghostBuf.Add(new Vector2(p.x + gx, p.y + gy));
            return ghostBuf;
        }

        static void Line(SpriteRenderer r, Vector2 a, Vector2 b, float w)
        {
            var d = b - a;
            r.transform.localPosition = (a + b) / 2;
            r.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude + w, w, 1);
        }

        // snake head seen from above: viper shape, amber eyes with slit pupils on the sides, nostrils, forked tongue;
        // the eyes show the mood (fed: happy closed eyes, hungry: pale, evil: red eyes and angry brows)
        void DrawHead(Vector2 p, Pal look, bool white)
        {
            var spots = new List<Vector2>(Ghosts(p));
            var d = G.dir;
            float ang = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
            float along = headX, across = 1f / Mathf.Sqrt(Mathf.Max(.3f, headX));   // spring: stretch along, squash across
            string mood = SnakeLook();
            tongueT += Time.unscaledDeltaTime;
            bool tongue = (tongueT % 1.6f) < .22f && !G.over;
            for (int h = 0; h < heads.Length; h++)
            {
                var v = heads[h];
                bool on = h < spots.Count;
                v.root.gameObject.SetActive(on);
                if (!on) continue;
                v.root.localPosition = W(spots[h].x, spots[h].y);
                v.root.localRotation = Quaternion.Euler(0, 0, ang);
                v.root.localScale = new Vector3(along, across, 1);
                v.head.color = white ? Color.white : look.head;
                v.head.transform.localPosition = Vector3.zero; v.head.transform.localScale = new Vector3(.98f, .9f, 1);
                v.tongue.enabled = v.forkA.enabled = v.forkB.enabled = tongue;
                if (tongue)
                {
                    Vector2 a = new Vector2(.62f, 0), e = new Vector2(.92f, 0);
                    Line(v.tongue, a, e, .055f); Line(v.forkA, e, e + new Vector2(.1f, .08f), .05f); Line(v.forkB, e, e + new Vector2(.1f, -.08f), .05f);
                }
                bool happy = mood == "fed";
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1 : 1;
                    var eye = new Vector3(.14f, .27f * side);
                    v.nostrils[s].transform.localPosition = new Vector3(.53f, .09f * side); v.nostrils[s].transform.localScale = Vector3.one * .055f;
                    v.irises[s].enabled = !happy; v.pupils[s].enabled = !happy; v.smiles[s].enabled = happy;
                    v.irises[s].color = white ? Color.white : look.eye;
                    v.irises[s].transform.localPosition = eye; v.irises[s].transform.localScale = Vector3.one * .22f;
                    // vertical slit pupil (along the head), thinner when hungry
                    v.pupils[s].transform.localPosition = eye + new Vector3(.015f, 0);
                    v.pupils[s].transform.localScale = new Vector3(.17f, mood == "hungry" ? .03f : .05f, 1);
                    if (happy)
                    {
                        v.smiles[s].transform.localPosition = eye; v.smiles[s].transform.rotation = Quaternion.identity;
                        v.smiles[s].transform.localScale = new Vector3(.2f, .2f, 1);
                    }
                    v.brows[s].enabled = mood == "evil";
                    if (mood == "evil") Line(v.brows[s], new Vector2(-.02f, side * .44f), new Vector2(.26f, side * .3f), .06f);
                }
            }
        }

        void UpdateHud()
        {
            var c = G.c; var th = TH; string a = "", b = "";
            switch (G.phase)
            {
                case "simple": a = T("hudScore", G.score); b = T("hudBest", Mathf.Max(best, G.score)); break;
                case "tutorial":
                    a = T("hudTut", c.total, th.tut);
                    int left = Mathf.CeilToInt(th.starveSec - G.noEat);
                    b = c.total == 0 && left <= 10 ? "<color=#ffcdd2>" + T("starveWarn", Mathf.Max(0, left)) + "</color>" : T("tutGoal"); break;
                case "walk": a = T("hudEaten", c.total); b = T("hudWalk", c.dist, th.walkDist); break;
                case "greed": a = T("hudGreed", c.total, th.greed); b = T("hudGreedSub"); break;
                case "evil": a = T("hudEvil"); b = "…"; break;
            }
            hud1.text = a; hud2.text = b;
            phaseT.text = L.Phase.TryGetValue(G.phase, out var ph) ? ph : "";
            livesT.text = G.evil ? "" : new string('♥', G.lives) + new string('♡', Mathf.Max(0, MaxLives - G.lives));
            livesT.rectTransform.localScale = Vector3.one * (G.hurt > 0 ? 1 + .15f * Mathf.Sin(G.hurt * 30) : 1);
        }
    }
}
