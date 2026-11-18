using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GC26.Towerdefence
{
    /// <summary>
    /// First version of the HUD, laid out like the prototype:
    /// gold and lives top left, wave X/10 top center, pause top right,
    /// a "start wave" play button with the build timer bottom right,
    /// and a big result line in the middle when the level is won or lost.
    /// Pause opens a small menu: Resume, Restart level, Main menu.
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
        RectTransform _safeArea;
        Rect _appliedSafeArea;
        Vector2Int _appliedScreen;

        TMP_Text _gold;
        TMP_Text _lives;
        TMP_Text _wave;
        RectTransform _wavePanel;
        TMP_Text _result;
        Button _startButton;
        TMP_Text _startTimer;
        GameObject _pauseMenu;

        Sprite _playSprite;
        Texture2D _playTexture;
        bool _paused;

        public bool IsPaused => _paused;

        void Start()
        {
            if (level == null || economy == null)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] LevelHud: assign Level and Economy", this);
                enabled = false;
                return;
            }
            EnsureEventSystem();
            BuildUi();
        }

        void OnDisable()
        {
            // Rule 6: never leave the shell paused.
            if (_paused) Time.timeScale = 1f;
            _paused = false;
        }

        void OnDestroy()
        {
            if (_playSprite != null) Destroy(_playSprite);
            if (_playTexture != null) Destroy(_playTexture);
        }

        void Update()
        {
            if (_gold == null) return;
            ApplyScreen();

            _gold.text = $"Gold  <b>{economy.Gold}</b>";
            _lives.text = $"Lives  <b>{level.Lives}</b>";
            _wave.text = $"Wave  <b>{level.CurrentWave}/{level.TotalWaves}</b>";

            bool build = level.State == LevelState.Build;
            _startButton.gameObject.SetActive(!level.IsOver);
            _startButton.interactable = level.CanStartWave && !_paused;
            _startTimer.text = build ? $"{Mathf.CeilToInt(level.Countdown)} s" : "";

            _result.text = level.State switch
            {
                LevelState.Won => "VICTORY",
                LevelState.Lost => "DEFEAT",
                _ => "",
            };
            _result.color = level.State == LevelState.Won ? new Color(0.6f, 0.9f, 0.48f) : Heart;
        }

        // ---------- actions ----------

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _pauseMenu.SetActive(paused);
        }

        void Restart()
        {
            SetPaused(false);
            // Remove enemies still on the field, then start over.
            foreach (var unit in FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None))
                Destroy(unit.gameObject);
            level.BeginLevel();
        }

        void ExitToMainMenu()
        {
            SetPaused(false);
            if (game != null) game.ExitToMenu(0);
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

            // Middle: result.
            _result = Label("Result", _safeArea, 140f, TextAlignmentOptions.Center, Color.white);
            _result.fontStyle = FontStyles.Bold;
            Place(_result.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);

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
            box.sizeDelta = new Vector2(620f, 560f);
            box.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.12f, 0.95f);

            var title = Label("Title", box, 72f, TextAlignmentOptions.Center, Color.white);
            title.text = "Paused";
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -120f), new Vector2(0f, -20f));

            MenuButton(box, "Resume", -150f, Green, () => SetPaused(false));
            MenuButton(box, "Restart level", -280f, Grey, Restart);
            MenuButton(box, "Main menu", -410f, Grey, ExitToMainMenu);

            _pauseMenu.SetActive(false);
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
        Sprite PlaySprite()
        {
            if (_playSprite != null) return _playSprite;

            const int size = 128;
            _playTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            // Triangle corners: left top, left bottom, right middle.
            Vector2 a = new(0f, size), b = new(0f, 0f), c = new(size, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                // Signed distance to each edge (positive inside); 1-pixel soft edge.
                float d = Mathf.Min(EdgeDistance(p, a, b), Mathf.Min(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
                byte alpha = (byte)(Mathf.Clamp01(d + 0.5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            _playTexture.SetPixels32(pixels);
            _playTexture.Apply();
            _playSprite = Sprite.Create(_playTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _playSprite;
        }

        static float EdgeDistance(Vector2 p, Vector2 from, Vector2 to)
        {
            Vector2 edge = to - from;
            // Corners are listed counter-clockwise, so "inside" is to the left of each edge.
            return (edge.x * (p.y - from.y) - edge.y * (p.x - from.x)) / edge.magnitude;
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
