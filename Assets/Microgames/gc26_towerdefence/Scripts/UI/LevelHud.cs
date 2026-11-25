using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GC26.Towerdefence
{
    /// <summary>
    /// First version of the HUD, laid out like the prototype:
    /// gold and lives top left, wave X/10 top center, pause top right,
    /// a "start wave" play button with the build timer bottom right,
    /// and a result screen when the level ends: Victory with 1-3 stars or Defeat, plus Restart / Main menu.
    /// Pause opens a small menu: Resume, Restart level, Main menu, and at the bottom two phone icons
    /// that turn the screen upright (9:16) or sideways (16:9). Next to pause, a 1x/2x speed toggle.
    /// Keys: Esc pauses / resumes, Space starts the next wave. A hint line at the bottom says what to do next.
    /// Built from code with plain uGUI: works with touches (Input System) and stays inside the safe area.
    /// Final look and icons come later (W5-W6).
    /// </summary>
    public class LevelHud : MonoBehaviour
    {
        [SerializeField] TowerdefenceGame game;
        [SerializeField] LevelFlow level;
        [SerializeField] EconomyManager economy;

        static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        static readonly Color Gold = new(1f, 0.84f, 0.31f);
        static readonly Color Heart = new(1f, 0.42f, 0.42f);
        static readonly Color Green = new(0.22f, 0.55f, 0.24f);
        static readonly Color Grey = new(0.35f, 0.35f, 0.38f);

        CanvasScaler _scaler;
        Canvas _canvas;
        RectTransform _safeArea;
        Rect _appliedSafeArea;
        Vector2Int _appliedScreen;

        TMP_Text _gold;
        TMP_Text _lives;
        TMP_Text _wave;
        RectTransform _wavePanel;
        GameObject _resultScreen;
        TMP_Text _resultTitle;
        TMP_Text _resultText;
        GameObject _starRow;
        readonly Image[] _stars = new Image[3];
        Button _startButton;
        TMP_Text _startTimer;
        GameObject _pauseMenu;
        TMP_Text _bonus;
        float _bonusUntil;

        readonly System.Collections.Generic.List<Object> _owned = new();
        bool _paused;
        float _speed = 1f;
        TMP_Text _speedLabel;
        TMP_Text _hint;
        Button _portraitButton;
        Button _landscapeButton;
        ScreenOrientation _startOrientation;
        bool _orientationChanged;

        public bool IsPaused => _paused;

        void Start()
        {
            if (level == null || economy == null)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] LevelHud: assign Level and Economy", this);
                enabled = false;
                return;
            }
            _startOrientation = Screen.orientation;
            EnsureEventSystem();
            BuildUi();
            level.BonusPaid += ShowBonus;
        }

        void ShowBonus(string reason, int gold)
        {
            if (_bonus == null) return;
            _bonus.text = $"+{gold} gold  <size=75%>{reason}</size>";
            _bonus.alpha = 1f;
            _bonusUntil = Time.unscaledTime + 2f;
        }

        void OnDisable()
        {
            // Rule 6: never leave the shell paused, sped up or turned.
            Time.timeScale = 1f;
            _paused = false;
            if (_orientationChanged)
            {
                Screen.orientation = _startOrientation;
                _orientationChanged = false;
            }
        }

        void OnDestroy()
        {
            if (level != null) level.BonusPaid -= ShowBonus;
            foreach (var o in _owned)
                if (o != null) Destroy(o);
        }

        void Update()
        {
            if (_gold == null) return;

            // Hidden while the main menu is up (no level running).
            bool inLevel = level.State != LevelState.Waiting;
            if (_canvas.enabled != inLevel) _canvas.enabled = inLevel;
            if (!inLevel) return;

            ApplyScreen();
            HandleKeys();

            _gold.text = $"Gold  <b>{economy.Gold}</b>";
            _lives.text = $"Lives  <b>{level.Lives}</b>";
            _wave.text = $"Wave  <b>{level.CurrentWave}/{level.TotalWaves}</b>";

            bool build = level.State == LevelState.Build;
            _startButton.gameObject.SetActive(!level.IsOver);
            _startButton.interactable = level.CanStartWave && !_paused;
            // The timer shows what an early start pays right now.
            int early = level.EarlyCallBonus;
            _startTimer.text = build && early > 0 ? $"+{early} gold" : "";

            if (_bonusUntil > 0f)
            {
                float left = _bonusUntil - Time.unscaledTime;
                if (left <= 0f) { _bonusUntil = 0f; _bonus.text = ""; }
                else _bonus.alpha = Mathf.Clamp01(left / 0.5f); // fade out over the last half second
            }

            UpdateResultScreen();

            _speedLabel.text = _speed > 1f ? "2x" : "1x";
            if (_pauseMenu.activeSelf) UpdateOrientationButtons();
            _hint.text = level.State switch
            {
                LevelState.Build when level.CurrentWave == 0 => "Tap a grey square to build a tower, then press the green play button",
                LevelState.Build => "Tap a tower to upgrade or sell it. Start early for bonus gold",
                LevelState.Wave => "Tap a tower to change its target",
                _ => "",
            };
        }

        void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && !level.IsOver) SetPaused(!_paused);
            if (keyboard.spaceKey.wasPressedThisFrame && !_paused) level.StartNextWave();
        }

        void ToggleSpeed()
        {
            _speed = _speed > 1f ? 1f : 2f;
            if (!_paused) Time.timeScale = _speed;
        }

        // ---------- actions ----------

        /// <summary>
        /// Turns the screen. Works on phones and in the Device Simulator; a desktop window cannot turn.
        /// The original orientation comes back when the game closes (OnDisable).
        /// </summary>
        void SetOrientation(bool portrait)
        {
            Screen.orientation = portrait ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            _orientationChanged = true;
        }

        void UpdateOrientationButtons()
        {
            bool portrait = Screen.height > Screen.width;
            Highlight(_portraitButton, portrait);
            Highlight(_landscapeButton, !portrait);
        }

        static void Highlight(Button button, bool on)
        {
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = on ? Green : Grey;
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.15f);
            button.colors = colors;
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : _speed;
            _pauseMenu.SetActive(paused);
        }

        void Restart()
        {
            SetPaused(false);
            level.BeginLevel(); // WaveSpawner and BuildManager clear the field
        }

        /// <summary>Back to the game's own main menu (level select); Exit there returns to GC26.</summary>
        void ExitToMainMenu()
        {
            SetPaused(false);
            level.CloseLevel();
        }

        /// <summary>The shell scene has its own EventSystem; create one only when the game runs alone.</summary>
        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem (towerdefence)");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- layout ----------
        // Sizes are in reference pixels: the short side of the screen is always 1080.

        void BuildUi()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvas = canvas;
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _safeArea = NewRect("SafeArea", canvasGo.transform);
            Stretch(_safeArea);

            const float m = 24f; // margin from the safe-area edge

            // Top left: gold and lives.
            var stats = Panel("Stats", _safeArea, new Vector2(0f, 1f), new Vector2(m, -m), new Vector2(560f, 110f));
            _gold = Label("Gold", stats, 54f, TextAlignmentOptions.Left, Gold);
            Place(_gold.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(28f, 0f), Vector2.zero);
            _lives = Label("Lives", stats, 54f, TextAlignmentOptions.Left, Heart);
            Place(_lives.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-20f, 0f));

            // Top center (upright: under the stats): wave counter.
            _wavePanel = Panel("Wave", _safeArea, new Vector2(0.5f, 1f), new Vector2(0f, -m), new Vector2(360f, 110f));
            _wave = Label("Text", _wavePanel, 54f, TextAlignmentOptions.Center, Color.white);
            Stretch(_wave.rectTransform);

            // Top right: pause (two bars).
            var pause = NewButton("Pause", _safeArea, new Vector2(1f, 1f), new Vector2(-m, -m), new Vector2(110f, 110f), Grey);
            pause.onClick.AddListener(() => SetPaused(true));
            Bar(pause.transform, -16f);
            Bar(pause.transform, 16f);

            // Left of pause: game speed 1x / 2x.
            var speed = NewButton("Speed", _safeArea, new Vector2(1f, 1f), new Vector2(-m - 110f - 16f, -m), new Vector2(130f, 110f), Grey);
            speed.onClick.AddListener(ToggleSpeed);
            _speedLabel = Label("Label", speed.transform, 52f, TextAlignmentOptions.Center, Color.white);
            _speedLabel.fontStyle = FontStyles.Bold;
            Stretch(_speedLabel.rectTransform);

            // Bottom: what to do next (left of the start button).
            _hint = Label("Hint", _safeArea, 38f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            _hint.textWrappingMode = TextWrappingModes.Normal;
            Place(_hint.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(m, m), new Vector2(-m - 200f - 24f, m + 110f));

            // Bottom right: start wave (play triangle) with the build timer above it.
            _startButton = NewButton("StartWave", _safeArea, new Vector2(1f, 0f), new Vector2(-m, m), new Vector2(200f, 200f), Green);
            _startButton.onClick.AddListener(level.StartNextWave);
            var icon = NewRect("Play", _startButton.transform);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(10f, 0f); // a triangle looks centered a bit to the right
            icon.sizeDelta = new Vector2(110f, 110f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = PlaySprite();
            iconImage.raycastTarget = false;

            _startTimer = Label("Timer", _startButton.transform, 46f, TextAlignmentOptions.Center, Gold);
            var timerRect = _startTimer.rectTransform;
            timerRect.anchorMin = new Vector2(0f, 1f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(0.5f, 0f);
            timerRect.anchoredPosition = new Vector2(0f, 8f);
            timerRect.sizeDelta = new Vector2(0f, 60f);

            // Bottom center: "+19 gold Early" / "+35 gold Wave 3 cleared", shown for 2 seconds.
            _bonus = Label("Bonus", _safeArea, 60f, TextAlignmentOptions.Center, Gold);
            _bonus.fontStyle = FontStyles.Bold;
            Place(_bonus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 260f), new Vector2(0f, 340f));

            BuildResultScreen(canvasGo.transform);
            BuildPauseMenu(canvasGo.transform);
        }

        void BuildPauseMenu(Transform canvas)
        {
            // Full-screen dark layer that also blocks taps on the game behind it.
            var dim = NewRect("PauseMenu", canvas);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            _pauseMenu = dim.gameObject;

            var box = NewRect("Box", dim);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(620f, 760f);
            box.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.12f, 0.95f);

            var title = Label("Title", box, 72f, TextAlignmentOptions.Center, Color.white);
            title.text = "Paused";
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -120f), new Vector2(0f, -20f));

            MenuButton(box, "Resume", -150f, Green, () => SetPaused(false));
            MenuButton(box, "Restart level", -280f, Grey, Restart);
            MenuButton(box, "Main menu", -410f, Grey, ExitToMainMenu);

            // Bottom row: screen orientation, shown as an upright and a sideways phone.
            var label = Label("ScreenLabel", box, 40f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            label.text = "Screen";
            Place(label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -585f), new Vector2(0f, -535f));
            _portraitButton = PhoneButton(box, new Vector2(-90f, -600f), portrait: true);
            _landscapeButton = PhoneButton(box, new Vector2(90f, -600f), portrait: false);

            _pauseMenu.SetActive(false);
        }

        void BuildResultScreen(Transform canvas)
        {
            var dim = NewRect("ResultScreen", canvas);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            _resultScreen = dim.gameObject;

            var box = NewRect("Box", dim);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(760f, 760f);
            box.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.12f, 0.95f);

            _resultTitle = Label("Title", box, 96f, TextAlignmentOptions.Center, Color.white);
            _resultTitle.fontStyle = FontStyles.Bold;
            Place(_resultTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -150f), new Vector2(0f, -30f));

            // Three stars; earned ones are gold, missing ones dark.
            var row = NewRect("Stars", box);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -160f);
            row.sizeDelta = new Vector2(620f, 180f);
            _starRow = row.gameObject;
            var star = StarSprite();
            for (int i = 0; i < 3; i++)
            {
                var s = NewRect($"Star{i + 1}", row);
                s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
                s.anchoredPosition = new Vector2((i - 1) * 200f, i == 1 ? 14f : 0f); // middle star a bit higher
                s.sizeDelta = new Vector2(170f, 170f);
                _stars[i] = s.gameObject.AddComponent<Image>();
                _stars[i].sprite = star;
                _stars[i].raycastTarget = false;
            }

            _resultText = Label("Text", box, 50f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.9f));
            Place(_resultText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -430f), new Vector2(-30f, -350f));

            MenuButton(box, "Restart level", -470f, Green, Restart);
            MenuButton(box, "Main menu", -610f, Grey, ExitToMainMenu);

            _resultScreen.SetActive(false);
        }

        void UpdateResultScreen()
        {
            bool over = level.IsOver;
            if (_resultScreen.activeSelf != over) _resultScreen.SetActive(over);
            if (!over) return;

            bool won = level.State == LevelState.Won;
            _resultTitle.text = won ? "Victory!" : "Defeat";
            _resultTitle.color = won ? new Color(0.6f, 0.9f, 0.48f) : Heart;
            _starRow.SetActive(won);
            for (int i = 0; i < _stars.Length; i++)
                _stars[i].color = i < level.Stars ? Gold : new Color(0.25f, 0.25f, 0.25f);
            _resultText.text = won
                ? $"Lives left: {level.Lives}/{level.StartLives}"
                : $"You reached wave {level.CurrentWave} of {level.TotalWaves}";
        }

        /// <summary>A square button with a phone drawing: body, screen and a small camera dot.</summary>
        Button PhoneButton(Transform parent, Vector2 topCenter, bool portrait)
        {
            var button = NewButton(portrait ? "Portrait" : "Landscape", parent, new Vector2(0.5f, 1f), topCenter, new Vector2(150f, 140f), Grey);
            button.onClick.AddListener(() => SetOrientation(portrait));

            Vector2 body = portrait ? new Vector2(58f, 100f) : new Vector2(100f, 58f);
            var phone = NewRect("Phone", button.transform);
            phone.anchorMin = phone.anchorMax = new Vector2(0.5f, 0.5f);
            phone.sizeDelta = body;
            var bodyImage = phone.gameObject.AddComponent<Image>();
            bodyImage.color = Color.white;
            bodyImage.raycastTarget = false;

            var screen = NewRect("Screen", phone);
            screen.anchorMin = Vector2.zero;
            screen.anchorMax = Vector2.one;
            // Thin frame on the long sides, a wider one on the short sides (top/bottom of the phone).
            screen.offsetMin = portrait ? new Vector2(6f, 14f) : new Vector2(14f, 6f);
            screen.offsetMax = portrait ? new Vector2(-6f, -14f) : new Vector2(-14f, -6f);
            var screenImage = screen.gameObject.AddComponent<Image>();
            screenImage.color = new Color(0.15f, 0.2f, 0.28f);
            screenImage.raycastTarget = false;

            var dot = NewRect("Camera", phone);
            dot.anchorMin = dot.anchorMax = portrait ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f);
            dot.anchoredPosition = portrait ? new Vector2(0f, -7f) : new Vector2(7f, 0f);
            dot.sizeDelta = new Vector2(6f, 6f);
            var dotImage = dot.gameObject.AddComponent<Image>();
            dotImage.color = new Color(0.15f, 0.2f, 0.28f);
            dotImage.raycastTarget = false;
            return button;
        }

        void MenuButton(Transform parent, string text, float top, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = NewButton(text, parent, new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(520f, 110f), color);
            button.onClick.AddListener(onClick);
            var label = Label("Label", button.transform, 52f, TextAlignmentOptions.Center, Color.white);
            label.text = text;
            Stretch(label.rectTransform);
        }

        void ApplyScreen()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if ((safe == _appliedSafeArea && screen == _appliedScreen) || screen.x <= 0 || screen.y <= 0) return;
            _appliedSafeArea = safe;
            _appliedScreen = screen;

            // Scale by the short side, so the HUD is the same size held upright or sideways.
            bool portrait = screen.y > screen.x;
            _scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = portrait ? 0f : 1f;

            // Upright there is no room for three boxes in one row: the wave counter goes under gold and lives.
            const float m = 24f;
            var corner = portrait ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            _wavePanel.anchorMin = _wavePanel.anchorMax = _wavePanel.pivot = corner;
            _wavePanel.anchoredPosition = portrait ? new Vector2(m, -m - 110f - 16f) : new Vector2(0f, -m);

            _safeArea.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            _safeArea.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        }

        /// <summary>A white "play" triangle pointing right, drawn once at runtime (no art file needed yet).</summary>
        Sprite PlaySprite() => PolygonSprite(new[] { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0.5f) });

        /// <summary>A white five-pointed star.</summary>
        Sprite StarSprite()
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.5f : 0.21f;
                points[i] = new Vector2(0.5f + Mathf.Cos(angle) * r, 0.47f + Mathf.Sin(angle) * r);
            }
            return PolygonSprite(points);
        }

        /// <summary>
        /// Fills a polygon given in 0..1 coordinates (y up) into a 128 px sprite,
        /// with 4x4 samples per pixel for smooth edges. Works for concave shapes like a star.
        /// </summary>
        Sprite PolygonSprite(Vector2[] points)
        {
            const int size = 128, samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / samples) / size, (y + (sy + 0.5f) / samples) / size);
                    if (Inside(p, points)) inside++;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Even-odd rule: count how many polygon edges a ray to the right crosses.</summary>
        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ---------- small builders ----------

        static void Bar(Transform parent, float x)
        {
            var bar = NewRect("Bar", parent);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(x, 0f);
            bar.sizeDelta = new Vector2(18f, 56f);
            var image = bar.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        static RectTransform Panel(string name, Transform parent, Vector2 corner, Vector2 offset, Vector2 size)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        static Button NewButton(string name, Transform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.gameObject.AddComponent<Image>().color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.3f);
            colors.selectedColor = color; // otherwise the last pressed button stays white
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.75f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        static TMP_Text Label(string name, Transform parent, float size, TextAlignmentOptions align, Color color)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect) => Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
