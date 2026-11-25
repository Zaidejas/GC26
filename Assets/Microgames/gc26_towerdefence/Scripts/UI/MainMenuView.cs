using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The game's own main menu, shown whenever no level is being played (LevelState.Waiting):
    /// title with Play, How to play and Exit (back to the GC26 menu); Play opens the level select,
    /// one button per level with its best stars, and Back.
    /// How to play explains the rules, towers, monsters and buttons (text from the prototype).
    /// Built from code with plain uGUI like the HUD; final look comes with the art (W5-W6).
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] TowerdefenceGame game;
        [SerializeField] LevelFlow level;
        [SerializeField] PlaceholderArt art;
        [Tooltip("Levels offered in the menu, in this order.")]
        [SerializeField] LevelData[] levels;

        static readonly Color Gold = new(1f, 0.84f, 0.31f);
        static readonly Color Green = new(0.22f, 0.55f, 0.24f);
        static readonly Color Grey = new(0.35f, 0.35f, 0.38f);
        static readonly Color StarOff = new(0.25f, 0.25f, 0.25f);

        const string HowToText =
            "<b>Goal:</b> stop the monsters before they reach your castle. You have 20 lives. " +
            "Every monster that gets in takes lives.\n\n" +
            "<b>1. Build:</b> tap a grey square and choose a tower.\n" +
            "<b>2. Start:</b> press the green play button. Starting early gives bonus gold.\n" +
            "<b>3. Earn:</b> you get gold for every monster you kill and every wave you clear.\n" +
            "<b>4. Improve:</b> tap a tower to upgrade it, sell it (70% back) or change who it shoots first.\n" +
            "<b>5. Win:</b> survive all the waves. 3 stars with 18+ lives left, 2 with 10+, 1 for any win.\n\n" +
            "<size=115%><b>Towers</b></size>\n" +
            "<b>Archer</b>: cheap, hits flying monsters\n" +
            "<b>Cannon</b>: hits a group, cannot hit flying monsters\n" +
            "<b>Frost</b>: slows monsters down\n" +
            "<b>Tesla</b>: lightning jumps to 3 monsters\n" +
            "<b>Sniper</b>: very long range, ignores armor\n\n" +
            "<size=115%><b>Monsters</b></size>\n" +
            "<b>Grunt</b>: basic.  <b>Runner</b>: fast.  <b>Brute</b>: armored, slow.\n" +
            "<b>Bat</b>: flies straight over the map.  <b>Shaman</b>: heals monsters near it.\n" +
            "<b>Warlord</b>: the boss, with a shield.\n\n" +
            "<size=115%><b>Buttons</b></size>\n" +
            "Play button or <b>Space</b>: start the next wave.  <b>1x / 2x</b>: game speed.\n" +
            "Pause or <b>Esc</b>: pause menu, restart, main menu, turn the screen.";

        CanvasScaler _scaler;
        GameObject _root;
        GameObject _mainPage;
        GameObject _levelsPage;
        GameObject _howToPage;
        readonly List<(LevelData data, Image[] stars)> _levelStars = new();
        Vector2Int _appliedScreen;

        void Start()
        {
            if (level == null || art == null)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] MainMenuView: assign Level and Art", this);
                enabled = false;
                return;
            }
            EnsureEventSystem();
            Build();
            ShowMain();
        }

        void Update()
        {
            if (_root == null) return;

            // Visible whenever no level is running, once the game has begun.
            bool show = level.State == LevelState.Waiting && (game == null || game.IsStarted);
            if (_root.activeSelf != show)
            {
                _root.SetActive(show);
                if (show) ShowMain();
            }
            if (show) ApplyScreen();
        }

        void ShowMain() => ShowPage(_mainPage);
        void ShowHowTo() => ShowPage(_howToPage);

        void ShowLevels()
        {
            ShowPage(_levelsPage);
            RefreshStars();
        }

        void ShowPage(GameObject page)
        {
            _mainPage.SetActive(page == _mainPage);
            _levelsPage.SetActive(page == _levelsPage);
            _howToPage.SetActive(page == _howToPage);
        }

        void RefreshStars()
        {
            foreach (var (data, stars) in _levelStars)
            {
                int best = LevelFlow.BestStars(data);
                for (int s = 0; s < stars.Length; s++)
                    stars[s].color = s < best ? Gold : StarOff;
            }
        }

        /// <summary>Back to the GC26 menu; the score is the total of the best stars of all levels.</summary>
        void Exit()
        {
            int total = 0;
            if (levels != null)
                foreach (var l in levels) total += LevelFlow.BestStars(l);
            if (game != null) game.ExitToMenu(total);
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

        void ApplyScreen()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == _appliedScreen || screen.x <= 0 || screen.y <= 0) return;
            _appliedScreen = screen;
            // Same rule as the HUD: the short side of the screen is 1080 reference pixels.
            bool portrait = screen.y > screen.x;
            _scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = portrait ? 0f : 1f;
        }

        // ---------- layout (reference pixels, short side = 1080) ----------

        void Build()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; // above the HUD
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dark layer over the field (the road stays faintly visible) that also blocks taps.
            var dim = NewRect("Background", canvasGo.transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.1f, 0.06f, 0.88f);
            _root = dim.gameObject;

            BuildMainPage(dim);
            BuildLevelsPage(dim);
            BuildHowToPage(dim);
        }

        void BuildMainPage(Transform parent)
        {
            var page = Column("Main", parent, 760f);
            _mainPage = page.gameObject;

            float y = 0f;
            var title = Label("Title", page, 120f, Gold);
            title.text = "Last Bastion";
            title.fontStyle = FontStyles.Bold;
            Row(title.rectTransform, ref y, 150f);
            var subtitle = Label("Subtitle", page, 44f, new Color(1f, 1f, 1f, 0.7f));
            subtitle.text = "Tower Defense";
            Row(subtitle.rectTransform, ref y, 110f);

            var play = NewButton("Play", page, Green, ShowLevels);
            Row((RectTransform)play.transform, ref y, 140f);
            var playLabel = CenterLabel(play.transform, "Play");
            playLabel.fontSize = 64f;
            playLabel.fontStyle = FontStyles.Bold;
            y += 24f;
            var howTo = NewButton("HowTo", page, Grey, ShowHowTo);
            Row((RectTransform)howTo.transform, ref y, 110f);
            CenterLabel(howTo.transform, "How to play");
            y += 24f;
            var exit = NewButton("Exit", page, Grey, Exit);
            Row((RectTransform)exit.transform, ref y, 110f);
            CenterLabel(exit.transform, "Exit");

            page.sizeDelta = new Vector2(page.sizeDelta.x, y);
        }

        /// <summary>Level select: one button per level with its best stars, then Back.</summary>
        void BuildLevelsPage(Transform parent)
        {
            var page = Column("Levels", parent, 760f);
            _levelsPage = page.gameObject;

            float y = 0f;
            var title = Label("Title", page, 84f, Gold);
            title.text = "Choose a level";
            title.fontStyle = FontStyles.Bold;
            Row(title.rectTransform, ref y, 140f);

            // Name on the left, best stars on the right.
            _levelStars.Clear();
            if (levels != null)
            {
                foreach (var data in levels)
                {
                    if (data == null) continue;
                    var chosen = data;
                    var button = NewButton(data.displayName, page, Green, () => level.StartLevel(chosen));
                    Row((RectTransform)button.transform, ref y, 130f);
                    var nameLabel = Label("Name", button.transform, 56f, Color.white);
                    nameLabel.text = data.displayName;
                    nameLabel.alignment = TextAlignmentOptions.Left;
                    Place(nameLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 0f), new Vector2(-260f, 0f));

                    var stars = new Image[3];
                    for (int s = 0; s < 3; s++)
                    {
                        var star = NewRect($"Star{s + 1}", button.transform);
                        star.anchorMin = star.anchorMax = new Vector2(1f, 0.5f);
                        star.anchoredPosition = new Vector2(-200f + s * 75f, 0f);
                        star.sizeDelta = new Vector2(70f, 70f);
                        stars[s] = star.gameObject.AddComponent<Image>();
                        stars[s].sprite = art.Star;
                        stars[s].raycastTarget = false;
                    }
                    _levelStars.Add((chosen, stars));
                    y += 20f;
                }
            }

            y += 20f;
            var back = NewButton("Back", page, Grey, ShowMain);
            Row((RectTransform)back.transform, ref y, 110f);
            CenterLabel(back.transform, "Back");

            page.sizeDelta = new Vector2(page.sizeDelta.x, y);
        }

        void BuildHowToPage(Transform parent)
        {
            var page = NewRect("HowTo", parent);
            page.anchorMin = new Vector2(0.5f, 0f);
            page.anchorMax = new Vector2(0.5f, 1f);
            page.sizeDelta = new Vector2(980f, -80f); // full height minus 40 px top and bottom
            page.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.12f, 0.97f);
            _howToPage = page.gameObject;

            var title = Label("Title", page, 72f, Gold);
            title.text = "How to play";
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -120f), new Vector2(0f, -20f));

            // Scrollable text: drag with a finger or the mouse wheel.
            var viewport = NewRect("Viewport", page);
            Place(viewport, Vector2.zero, Vector2.one, new Vector2(40f, 160f), new Vector2(-40f, -130f));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // catches drags

            var text = Label("Text", viewport, 40f, Color.white);
            text.text = HowToText;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            var content = text.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var back = NewButton("Back", page, Green, ShowMain);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 30f);
            rect.sizeDelta = new Vector2(520f, 110f);
            CenterLabel(back.transform, "Back");
        }

        // ---------- small builders ----------

        /// <summary>A centered column; children are stacked from the top with Row.</summary>
        static RectTransform Column(string name, Transform parent, float width)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            return rect;
        }

        /// <summary>Places a child as the next row of a column, full width, `height` tall.</summary>
        static void Row(RectTransform child, ref float y, float height)
        {
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(1f, 1f);
            child.pivot = new Vector2(0.5f, 1f);
            child.anchoredPosition = new Vector2(0f, -y);
            child.sizeDelta = new Vector2(0f, height);
            y += height;
        }

        static Button NewButton(string name, Transform parent, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect(name, parent);
            rect.gameObject.AddComponent<Image>().color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.3f);
            colors.selectedColor = color; // otherwise the last pressed button stays white
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return button;
        }

        static TMP_Text CenterLabel(Transform button, string text)
        {
            var label = Label("Label", button, 52f, Color.white);
            label.text = text;
            Stretch(label.rectTransform);
            return label;
        }

        static TMP_Text Label(string name, Transform parent, float size, Color color)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
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
