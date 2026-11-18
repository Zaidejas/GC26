using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GC26.Findepair.FindepairUIKit;

namespace GC26.Findepair
{
    public enum FindepairScreen { Splash, Menu, Game, Settings }

    /// <summary>
    /// Builds every screen from code at startup (splash, menu, board + HUD, pause, win, settings, how to play)
    /// and switches between them. Layout follows the HTML prototype, scaled to a 1080 x 1920 reference.
    /// </summary>
    public class FindepairUI : MonoBehaviour
    {
        FindepairGame _game;
        FindepairArt _art;
        FindepairBoard _board;
        FindepairSave _save;
        FindepairAudio _audio;

        RectTransform _canvas;
        readonly Dictionary<FindepairScreen, GameObject> _screens = new Dictionary<FindepairScreen, GameObject>();
        public FindepairScreen Current { get; private set; }

        TextMeshProUGUI _splashLogo, _menuBest, _moves, _time, _result, _bestLine;
        GameObject _pause, _win, _rules, _confirm, _badge;
        Image[] _stars;
        RectTransform _confettiLayer;
        readonly List<(Image track, Image knob, RectTransform knobRt, Image frame, Image glow, System.Func<bool> get)> _toggles =
            new List<(Image, Image, RectTransform, Image, Image, System.Func<bool>)>();
        Coroutine _splashRoutine;

        public RectTransform Grid { get; private set; }
        public SparkleBurst Sparkles { get; private set; }
        public bool PauseVisible => _pause != null && _pause.activeSelf;
        public bool WinVisible => _win != null && _win.activeSelf;

        public void Build(FindepairGame game, FindepairArt art, FindepairBoard board, FindepairSave save, FindepairAudio audio)
        {
            _game = game; _art = art; _board = board; _save = save; _audio = audio;

            _canvas = Rect("Canvas", transform);
            var canvas = _canvas.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the GC26 shell menu
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

            BuildSplash();
            BuildMenu();
            BuildGame();
            BuildSettings();

            _board.PausedChanged += p => _pause.SetActive(p);
        }

        // ---------- Switching ----------

        public void Show(FindepairScreen screen)
        {
            Current = screen;
            foreach (var kv in _screens) kv.Value.SetActive(kv.Key == screen);
            if (screen == FindepairScreen.Menu) _menuBest.text = _save.BestText;
            if (screen == FindepairScreen.Settings) { RefreshToggles(); _rules.SetActive(false); _confirm.SetActive(false); }
            if (screen == FindepairScreen.Game) { _pause.SetActive(false); _win.SetActive(false); ClearConfetti(); }
            if (screen == FindepairScreen.Splash)
            {
                if (_splashRoutine != null) StopCoroutine(_splashRoutine);
                _splashRoutine = StartCoroutine(Splash());
            }
        }

        public void ShowWin(FindepairResult r, bool isRecord)
        {
            for (int i = 0; i < 3; i++) _stars[i].color = i < r.Stars ? Acid : Dim;
            for (int i = 0; i < 3; i++) _stars[i].transform.GetChild(0).gameObject.SetActive(i < r.Stars);
            _badge.SetActive(isRecord);
            _result.text = $"Moves: <b>{r.Moves}</b>\nTime: <b>{FindepairRules.FormatTime(r.TimeMs / 1000f)}</b>";
            _bestLine.text = _save.BestText;
            _win.SetActive(true);
            StartCoroutine(Confetti());
        }

        void Update()
        {
            if (Current != FindepairScreen.Game || _moves == null) return;
            _moves.text = _board.Moves.ToString();
            _time.text = FindepairRules.FormatTime(_board.Elapsed);
        }

        // ---------- Screens ----------

        RectTransform MakeScreen(FindepairScreen id, Sprite background, float darken)
        {
            var rt = Rect(id.ToString(), _canvas).Stretch();
            var bg = Img("Background", rt, null, Bg);
            bg.rectTransform.Stretch();
            if (background != null)
            {
                var pic = Img("Picture", rt, background, Color.white);
                pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var fit = pic.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // "cover"
                fit.aspectRatio = background.rect.width / background.rect.height;
                Img("Darken", rt, null, Alpha(Bg, darken)).rectTransform.Stretch();
            }
            _screens[id] = rt.gameObject;
            return rt;
        }

        static RectTransform SafeContent(RectTransform screen, string name = "Content")
        {
            var safe = Rect("SafeArea", screen);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return Rect(name, safe).Stretch(40, 0, 40, 0);
        }

        void BuildSplash()
        {
            var s = MakeScreen(FindepairScreen.Splash, null, 0);
            var content = SafeContent(s);
            _splashLogo = Text("Logo", content, "EMO PARTY", 150, Color.white, FontStyles.Bold | FontStyles.Italic).Neon(Pink, 0.75f, 0.5f);
            _splashLogo.rectTransform.Place(0.5f, 0.5f, 1000, 300, 6);
            var tap = s.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            s.GetComponentInChildren<Image>().raycastTarget = true;
            tap.targetGraphic = s.GetComponentInChildren<Image>();
            tap.onClick.AddListener(() => { if (Current == FindepairScreen.Splash) Show(FindepairScreen.Menu); });
        }

        IEnumerator Splash()
        {
            // Neon flicker like the prototype, then the menu after 1.5 s.
            float[] keys = { 0f, .1f, .18f, .1f, .2f, 1f, .22f, .1f, .4f, .1f, .45f, 1f, 1f, 1f };
            for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 1.4f), a = 1f;
                for (int i = 0; i + 2 < keys.Length; i += 2)
                    if (k >= keys[i] && k <= keys[i + 2])
                        a = Mathf.Lerp(keys[i + 1], keys[i + 3], (k - keys[i]) / Mathf.Max(0.0001f, keys[i + 2] - keys[i]));
                _splashLogo.alpha = a;
                yield return null;
            }
            _splashLogo.alpha = 1f;
            if (Current == FindepairScreen.Splash) Show(FindepairScreen.Menu);
        }

        void BuildMenu()
        {
            var s = MakeScreen(FindepairScreen.Menu, _art.menuWall, 0.55f);
            var glow = Img("PinkGlow", s, _art.flash, Alpha(Pink, 0.25f));
            glow.rectTransform.Place(0.5f, 0.62f, 1600, 1600);

            var decor = Rect("Decor", s).Stretch();
            Sticker(decor, _art.star, Cyan, 0.12f, 0.91f, 100, 18);
            Sticker(decor, _art.heart, Pink, 0.86f, 0.92f, 120, -14);
            Sticker(decor, _art.bolt, Acid, 0.13f, 0.25f, 100, 8);
            Graffiti(decor, "XOXO 2006", Violet, 0.07f, 0.66f, 40, 80);
            Graffiti(decor, "never grow up", Pink2, 0.76f, 0.09f, 42, 6);
            Flyer(decor, "Band wanted", "drummer, own fringe a must", false, 0.79f, 0.81f, -7);
            Flyer(decor, "Gig", "Friday, garage #3", true, 0.80f, 0.20f, 5);
            Flyer(decor, "Mixtape", "will swap CD for a tape", false, 0.20f, 0.10f, 9);

            var content = SafeContent(s);
            var col = Rect("Column", content).Stretch();
            Column(col, 40);
            var logo = Rect("Logo", col);
            logo.Size(900, 470);
            var title = Text("Title", logo, "EMO\nPARTY", 160, Color.white, FontStyles.Bold | FontStyles.Italic).Neon(Pink, 0.75f, 0.5f);
            title.lineSpacing = -28;
            title.rectTransform.Place(0.5f, 0.6f, 900, 380, 6);
            var sub = Text("Subtitle", logo, "find the pair", 46, Cyan).Neon(Alpha(Cyan, 0.8f), 0.7f, 0.2f);
            sub.characterSpacing = 30;
            sub.rectTransform.Place(0.5f, 0.06f, 900, 70, 6);

            Button("Play", col, "Play", Pink, _art, () => _game.StartGame());
            Button("Settings", col, "Settings", Cyan, _art, () => Show(FindepairScreen.Settings));
            Button("Exit", col, "Exit", Cyan, _art, () => _game.ExitToShell());
            _menuBest = Text("Record", col, "", 36, Muted);
            _menuBest.Size(900, 60);
        }

        void BuildGame()
        {
            var s = MakeScreen(FindepairScreen.Game, _art.boardCork, 0.66f);
            var edge = Img("CorkFrame", s, _art.frame, Hex("#3B2718"), true, 0.4f);
            edge.rectTransform.Stretch(-6, -6, -6, -6);

            var decor = Rect("Decor", s).Stretch();
            Note(decor, "Memory tournament", Pink2, 0.12f, 0.83f, 8);
            Note(decor, "Game of the week", Cyan, 0.90f, 0.50f, -6);
            Note(decor, "Don't forget the MP3 charger", Acid, 0.16f, 0.10f, -5);
            PolaroidDeco(decor, 0.84f, 0.13f, 10);
            PolaroidDeco(decor, 0.07f, 0.42f, -8);

            var content = SafeContent(s);

            // HUD: moves, time, pause
            var hud = Rect("HUD", content);
            hud.anchorMin = new Vector2(0, 1); hud.anchorMax = new Vector2(1, 1);
            hud.pivot = new Vector2(0.5f, 1);
            hud.sizeDelta = new Vector2(0, 150);
            hud.anchoredPosition = new Vector2(0, -40);
            var row = hud.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 26;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            _moves = Stat(hud, "Moves");
            _time = Stat(hud, "Time");
            var pause = Button("Pause", hud, "II", Pink2, _art, () => _board.SetPaused(true), 150, 150, 52);
            pause.GetComponent<LayoutElement>().flexibleWidth = 0;

            // Board and sparkles
            Grid = Rect("Board", content).Stretch(0, 40, 0, 220);
            var grid = Grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(32, 32);
            grid.padding = new RectOffset(24, 24, 24, 24);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            Grid.gameObject.AddComponent<BoardFitter>();
            Sparkles = Rect("Sparkles", content).Stretch().gameObject.AddComponent<SparkleBurst>();
            Sparkles.Init(_art.sparkle, _art.flash, () => _board.Delta);

            BuildPause(s);
            BuildWin(s);
        }

        void BuildPause(RectTransform screen)
        {
            var o = Overlay(screen, "Pause");
            _pause = o.overlay;
            Text("Title", o.column, "Paused", 88, Color.white, FontStyles.Bold | FontStyles.Italic).Neon(Pink).Size(900, 130);
            Button("Resume", o.column, "Resume", Pink, _art, () => _board.SetPaused(false));
            Button("Restart", o.column, "Restart", Cyan, _art, () => _game.StartGame());
            Button("Menu", o.column, "Menu", Cyan, _art, () => _game.ToMenu());
            Button("Exit", o.column, "Exit", Cyan, _art, () => _game.ExitToShell());
        }

        void BuildWin(RectTransform screen)
        {
            var o = Overlay(screen, "Win");
            _win = o.overlay;
            _confettiLayer = Rect("Confetti", o.overlay.transform).Stretch();
            _confettiLayer.SetAsLastSibling();

            Text("Title", o.column, "All pairs found!", 80, Color.white, FontStyles.Bold | FontStyles.Italic).Neon(Pink).Size(1000, 120);
            var stars = Rect("Stars", o.column);
            stars.Size(560, 150);
            var h = stars.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            _stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var star = Img("Star", stars, _art.star, Acid);
                star.rectTransform.sizeDelta = new Vector2(130, 130);
                var halo = Img("Glow", star.transform, _art.flash, Alpha(Acid, 0.45f));
                halo.rectTransform.Stretch(-60, -60, -60, -60);
                halo.transform.SetAsFirstSibling();
                _stars[i] = star;
            }

            var badgeRt = Rect("BadgeHolder", o.column);
            badgeRt.Size(400, 90);
            var badge = Img("Badge", badgeRt, _art.rounded, Acid, true, 0.6f);
            badge.rectTransform.Place(0.5f, 0.5f, 360, 84, 4);
            Text("Text", badge.transform, "New record!", 40, Bg, FontStyles.Bold).rectTransform.Stretch();
            _badge = badge.gameObject;

            _result = Text("Result", o.column, "", 48, Ink);
            _result.Size(900, 140);
            _bestLine = Text("Best", o.column, "", 36, Muted);
            _bestLine.Size(900, 50);
            Button("PlayAgain", o.column, "Play again", Pink, _art, () => _game.StartGame());
            Button("Menu", o.column, "Menu", Cyan, _art, () => _game.ToMenu());
            Button("Exit", o.column, "Exit", Cyan, _art, () => _game.ExitToShell());
        }

        void BuildSettings()
        {
            var s = MakeScreen(FindepairScreen.Settings, _art.menuWall, 0.62f);
            var content = SafeContent(s);

            var top = Rect("Top", content);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(0, 150);
            top.anchoredPosition = new Vector2(0, -50);
            var back = Button("Back", top, "<", Pink2, _art, () => Show(FindepairScreen.Menu), 130, 130, 60);
            ((RectTransform)back.transform).Place(0, 0.5f, 130, 130);
            ((RectTransform)back.transform).pivot = new Vector2(0, 0.5f);
            var title = Text("Title", top, "Settings", 76, Color.white, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.Left).Neon(Pink);
            title.rectTransform.Stretch(170, 0, 0, 0);

            var list = Rect("List", content);
            list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1);
            list.pivot = new Vector2(0.5f, 1);
            list.sizeDelta = new Vector2(0, 800);
            list.anchoredPosition = new Vector2(0, -250);
            var col = Column(list, 34, TextAnchor.UpperCenter);
            col.childForceExpandWidth = true;

            Toggle(list, "Sound", () => _save.Sound, v => _save.Sound = v);
            Toggle(list, "Music", () => _save.Music, v => { _save.Music = v; _audio.SetMusic(v); });
            Toggle(list, "Vibration", () => _save.Vibro, v => _save.Vibro = v);
            Row(list, "How to play", () => _rules.SetActive(true));

            var reset = Button("ResetRecord", content, "Reset record", Cyan, _art, () => _confirm.SetActive(true));
            ((RectTransform)reset.transform).Place(0.5f, 0, 560, 140);
            ((RectTransform)reset.transform).anchoredPosition = new Vector2(0, 140);

            BuildRules(s);
            BuildConfirm(s);
        }

        void BuildRules(RectTransform screen)
        {
            var o = Overlay(screen, "Rules");
            _rules = o.overlay;
            var card = Rect("Card", o.column);
            card.Size(960, 1180);
            NeonPanel(card, _art, Pink, 0.95f, 1.2f);
            var inner = Rect("Inner", card).Stretch(56, 56, 56, 56);
            var col = Column(inner, 30, TextAnchor.UpperCenter);
            col.childForceExpandWidth = true;
            Text("Title", inner, "How to play", 68, Color.white, FontStyles.Bold | FontStyles.Italic).Neon(Pink).Size(-1, 110);
            string[] rules =
            {
                "Tap a card to turn it over.",
                "Turn over a second one: if the pictures match, the pair bursts into sparkles and vanishes.",
                "No match? Remember where they are: the cards turn back over.",
                "Find all 6 pairs in as few moves as you can; one move is two turned cards.",
                "Stars: 3 for up to 9 moves, 2 for 10-13, 1 for more than 13. You can't lose - only beat your record.",
            };
            for (int i = 0; i < rules.Length; i++)
            {
                var item = Rect("Rule" + (i + 1), inner);
                item.Size(-1, i == 1 || i == 4 ? 150 : 110);
                var num = Img("Number", item, _art.circle, Pink);
                num.rectTransform.anchorMin = num.rectTransform.anchorMax = new Vector2(0, 1);
                num.rectTransform.pivot = new Vector2(0, 1);
                num.rectTransform.sizeDelta = new Vector2(62, 62);
                Text("N", num.transform, (i + 1).ToString(), 34, Bg, FontStyles.Bold).rectTransform.Stretch();
                var t = Text("Text", item, rules[i], 38, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                t.rectTransform.Stretch(90, 0, 16, 4);
            }
            var okRow = Rect("GotItRow", inner);
            okRow.Size(-1, 150);
            var ok = Button("GotIt", okRow, "Got it", Pink, _art, () => _rules.SetActive(false));
            ((RectTransform)ok.transform).Place(0.5f, 0.5f, 560, 140);
        }

        void BuildConfirm(RectTransform screen)
        {
            var o = Overlay(screen, "ConfirmReset");
            _confirm = o.overlay;
            Text("Question", o.column, "Reset your record?", 64, Color.white, FontStyles.Bold).Neon(Pink).Size(1000, 120);
            Button("Yes", o.column, "Reset", Pink, _art, () => { _save.ResetBest(); _confirm.SetActive(false); });
            Button("No", o.column, "Cancel", Cyan, _art, () => _confirm.SetActive(false));
        }

        // ---------- Pieces ----------

        (GameObject overlay, RectTransform column) Overlay(RectTransform screen, string name)
        {
            var rt = Rect(name, screen).Stretch();
            var dim = rt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.043f, 0.031f, 0.055f, 0.86f); // blocks taps on the board below
            var content = SafeContent(rt);
            var col = Rect("Column", content).Stretch();
            Column(col, 36);
            rt.gameObject.SetActive(false);
            return (rt.gameObject, col);
        }

        TextMeshProUGUI Stat(RectTransform parent, string label)
        {
            var rt = Rect(label, parent);
            var le = rt.Size(-1, 150);
            le.flexibleWidth = 1;
            NeonPanel(rt, _art, Violet, 0.82f, 0.9f, 0.45f);
            var l = Text("Label", rt, label.ToUpperInvariant(), 28, Muted, FontStyles.Bold);
            l.characterSpacing = 12;
            l.rectTransform.Stretch(0, 80, 0, 18);
            var v = Text("Value", rt, "0", 58, Color.white, FontStyles.Bold);
            v.rectTransform.Stretch(0, 14, 0, 58);
            return v;
        }

        void Toggle(RectTransform parent, string label, System.Func<bool> get, System.Action<bool> set)
        {
            var b = RowBase(parent, label, () => { });
            var rt = (RectTransform)b.transform;
            var track = Img("Track", rt, _art.rounded, Dim, true, 1.6f);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.pivot = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(136, 78);
            track.rectTransform.anchoredPosition = new Vector2(-46, 0);
            var knob = Img("Knob", track.transform, _art.circle, Muted);
            var krt = knob.rectTransform;
            krt.anchorMin = krt.anchorMax = new Vector2(0, 0.5f);
            krt.sizeDelta = new Vector2(62, 62);
            var frame = rt.Find("Frame").GetComponent<Image>();
            var glow = rt.Find("Glow").GetComponent<Image>();
            _toggles.Add((track, knob, krt, frame, glow, get));
            b.onClick.AddListener(() => { set(!get()); _save.Save(); RefreshToggles(); });
        }

        void RefreshToggles()
        {
            foreach (var t in _toggles)
            {
                bool on = t.get();
                t.track.color = on ? Pink : Dim;
                t.knob.color = on ? Color.white : Muted;
                t.knobRt.anchoredPosition = new Vector2(on ? 98 : 39, 0);
                t.frame.color = on ? Pink : Dim;
                t.glow.color = Alpha(Pink, on ? 0.45f : 0f);
            }
        }

        void Row(RectTransform parent, string label, System.Action onClick)
        {
            var b = RowBase(parent, label, onClick);
            var chev = Text("Chevron", b.transform, ">", 64, Pink2, FontStyles.Bold, TextAlignmentOptions.Right);
            chev.rectTransform.Stretch(0, 0, 50, 0);
        }

        Button RowBase(RectTransform parent, string label, System.Action onClick)
        {
            var rt = Rect(label, parent);
            rt.Size(-1, 150);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            NeonPanel(rt, _art, Dim, 0.85f, 0.8f, 0f);
            var t = Text("Label", rt, label, 46, Ink, FontStyles.Bold, TextAlignmentOptions.Left);
            t.rectTransform.Stretch(50, 0, 220, 0);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            b.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<FindepairPress>();
            return b;
        }

        void Sticker(RectTransform parent, Sprite sprite, Color color, float x, float y, float size, float rotation)
        {
            var halo = Img("Halo", parent, _art.flash, Alpha(color, 0.35f));
            halo.rectTransform.Place(x, y, size * 2.2f, size * 2.2f);
            Img("Sticker", parent, sprite, color).rectTransform.Place(x, y, size, size, rotation);
        }

        void Graffiti(RectTransform parent, string text, Color color, float x, float y, float size, float rotation)
        {
            var t = Text("Graffiti", parent, text, size, color, FontStyles.Bold | FontStyles.Italic).Neon(Alpha(color, 0.8f), 0.7f, 0.25f);
            t.alpha = 0.85f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.Place(x, y, 600, size * 1.6f, rotation);
        }

        void Flyer(RectTransform parent, string title, string body, bool dark, float x, float y, float rotation)
        {
            var paper = Img("Flyer", parent, null, dark ? Hex("#1D1424") : Paper);
            paper.rectTransform.Place(x, y, 330, 185, rotation);
            if (dark)
            {
                var border = Img("Border", paper.transform, _art.frame, Violet, true, 0.2f);
                border.rectTransform.Stretch();
            }
            var tape = Img("Tape", paper.transform, null, new Color(1, 1, 1, 0.45f));
            tape.rectTransform.Place(0.5f, 1f, 100, 36, 4);
            var t = Text("Title", paper.transform, title.ToUpperInvariant(), 30, dark ? Acid : Pink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            t.rectTransform.Stretch(22, 0, 16, 22);
            var b = Text("Body", paper.transform, body.ToUpperInvariant(), 25, dark ? Muted : PaperInk, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            b.rectTransform.Stretch(22, 12, 16, 64);
        }

        void Note(RectTransform parent, string text, Color color, float x, float y, float rotation)
        {
            var note = Img("Note", parent, null, Alpha(color, 0.55f));
            note.rectTransform.Place(x, y, 190, 190, rotation);
            var t = Text("Text", note.transform, text.ToUpperInvariant(), 20, Alpha(PaperInk, 0.9f), FontStyles.Bold, TextAlignmentOptions.TopLeft);
            t.rectTransform.Stretch(16, 16, 14, 16);
        }

        void PolaroidDeco(RectTransform parent, float x, float y, float rotation)
        {
            var frame = Img("Polaroid", parent, null, Alpha(Paper, 0.45f));
            frame.rectTransform.Place(x, y, 165, 195, rotation);
            var photo = Img("Photo", frame.transform, null, Alpha(Color.Lerp(Violet, Pink, 0.5f), 0.7f));
            photo.rectTransform.Stretch(14, 46, 14, 14);
        }

        // ---------- Confetti (win screen) ----------

        readonly List<Image> _confetti = new List<Image>();

        IEnumerator Confetti()
        {
            ClearConfetti();
            Color[] colors = { Pink, Pink2, Violet, Cyan, Acid };
            var items = new List<(Image img, float delay, float life, float x)>();
            for (int i = 0; i < 36; i++)
            {
                var img = Img("Confetti", _confettiLayer, i % 2 == 1 ? _art.heart : _art.star, colors[i % colors.Length]);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 1);
                img.rectTransform.sizeDelta = new Vector2(56, 56);
                float x = Random.value;
                items.Add((img, Random.value * 0.8f, 2.2f + Random.value * 2f, x));
                _confetti.Add(img);
            }
            float h = _confettiLayer.rect.height, w = _confettiLayer.rect.width;
            for (float t = 0; t < 5f && WinVisible; t += Time.unscaledDeltaTime)
            {
                foreach (var c in items)
                {
                    if (c.img == null) continue;
                    float k = Mathf.Clamp01((t - c.delay) / c.life);
                    c.img.rectTransform.anchoredPosition = new Vector2(c.x * w, 40 - k * (h * 1.1f + 40));
                    c.img.rectTransform.localRotation = Quaternion.Euler(0, 0, -540f * k);
                    c.img.enabled = t >= c.delay && k < 1f;
                }
                yield return null;
            }
            ClearConfetti();
        }

        void ClearConfetti()
        {
            foreach (var c in _confetti) if (c != null) Destroy(c.gameObject);
            _confetti.Clear();
        }
    }
}
