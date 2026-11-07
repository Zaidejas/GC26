using System;
using System.Collections.Generic;
using Lines.Core;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// The start screen (GDD → Main menu; prototype's menu): 5 gently bobbing pieces, the "Lines" title,
    /// "Line up five of a kind", and the buttons Continue (with the saved game's score), New game,
    /// How to play and Leaderboard. Sizes follow the prototype, in units of 1 % of the content width.
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float PieceSize = 11f, PieceGap = 2f, DecoHeight = 13f, DecoMargin = 4f;
        const float LogoSize = 21f, TagSize = 4.2f, TagMargin = 2.5f;
        const float ButtonHeight = 13.5f, ButtonGap = 3f, BrandToButtons = 12f, SidePadding = 10f;
        const float FloatHeight = 0.14f, FloatHalfPeriod = 1.4f, FloatStagger = 0.17f;
        public const string Tagline = "Line up five of a kind";

        Canvas canvas;
        RectTransform root;
        readonly List<Image> deco = new List<Image>();
        readonly List<TweenHandle> floats = new List<TweenHandle>();
        TextMeshProUGUI logo, tagline;
        PillButton continueButton, newGame, howTo, leaderboard;
        BoardLayout layout;
        PieceSet pieces = PieceSet.Balls();

        public event Action ContinueClicked, NewGameClicked, HowToPlayClicked, LeaderboardClicked;

        public PillButton ContinueButton => continueButton;
        public PillButton NewGameButton => newGame;
        public PillButton HowToPlayButton => howTo;
        public PillButton LeaderboardButton => leaderboard;
        public bool IsShowing => gameObject.activeSelf;

        public static MenuView Create(Camera camera)
        {
            var go = new GameObject("Menu");
            var menu = go.AddComponent<MenuView>();
            menu.Build(camera);
            return menu;
        }

        void Build(Camera camera)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 20;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            for (int i = 0; i < 5; i++)
            {
                var img = NewRect("Piece " + i).gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                deco.Add(img);
            }
            logo = NewText("Title", "Lines", Palette.Text);
            tagline = NewText("Tagline", Tagline, Palette.SoftText);
            UiFonts.UseDisplay(logo);

            continueButton = PillButton.Create(root, "Continue", primary: true);
            newGame = PillButton.Create(root, "New game");
            howTo = PillButton.Create(root, "How to play");
            leaderboard = PillButton.Create(root, "Leaderboard");
            continueButton.Clicked += () => ContinueClicked?.Invoke();
            newGame.Clicked += () => NewGameClicked?.Invoke();
            howTo.Clicked += () => HowToPlayClicked?.Invoke();
            leaderboard.Clicked += () => LeaderboardClicked?.Invoke();
        }

        /// <summary>Places everything for the screen, centred in the safe area as one block.</summary>
        public void ApplyLayout(BoardLayout screen)
        {
            layout = screen;
            StopFloating(); // floating pieces snap back to their old spot when stopped, so stop them first
            float u = screen.Unit;
            var safe = screen.SafeArea;
            float contentLeft = safe.x + (safe.width - 100f * u) / 2f;
            float left = contentLeft + SidePadding * u, width = (100f - 2f * SidePadding) * u;

            float brandH = DecoHeight + DecoMargin + LogoSize * 0.95f + TagMargin + TagSize * 1.3f;
            float buttonsH = 4f * ButtonHeight + 3f * ButtonGap;
            float total = (brandH + BrandToButtons + buttonsH) * u;
            float top = safe.center.y + total / 2f;

            // Pieces, bottom-aligned in their row and centred.
            float rowWidth = 5f * PieceSize * u + 4f * PieceGap * u;
            float pieceY = top - DecoHeight * u;
            for (int i = 0; i < deco.Count; i++)
                Place(deco[i].rectTransform, safe.center.x - rowWidth / 2f + i * (PieceSize + PieceGap) * u, pieceY, PieceSize * u, PieceSize * u);

            float logoTop = top - (DecoHeight + DecoMargin) * u;
            logo.fontSize = LogoSize * u;
            Place(logo.rectTransform, left, logoTop - LogoSize * 0.95f * u, width, LogoSize * 0.95f * u);
            tagline.fontSize = TagSize * u;
            float tagTop = logoTop - (LogoSize * 0.95f + TagMargin) * u;
            Place(tagline.rectTransform, left, tagTop - TagSize * 1.3f * u, width, TagSize * 1.3f * u);

            float y = top - (brandH + BrandToButtons) * u;
            foreach (var b in new[] { continueButton, newGame, howTo, leaderboard })
            {
                y -= ButtonHeight * u;
                b.Place(new Rect(left, y, width, ButtonHeight * u), u);
                y -= ButtonGap * u;
            }
            if (IsShowing) StartFloating();
        }

        /// <summary>
        /// Shows the menu. <paramref name="savedScore"/> is the unfinished game's score, or null when
        /// there is none (then Continue is greyed out and shows no score).
        /// </summary>
        public void Show(int? savedScore, PieceSet pieceSet)
        {
            pieces = pieceSet ?? PieceSet.Balls();
            continueButton.Interactable = savedScore.HasValue;
            continueButton.SetText(savedScore.HasValue
                ? $"Continue <size=78%><alpha=#BF>· score {savedScore.Value}<alpha=#FF></size>"
                : "Continue");
            var colors = BallColors.All;
            for (int i = 0; i < deco.Count; i++) deco[i].sprite = pieces.SpriteFor(colors[i]);
            gameObject.SetActive(true);
            StartFloating();
        }

        public void Hide()
        {
            StopFloating();
            gameObject.SetActive(false);
        }

        /// <summary>Greys out the How to play and Leaderboard buttons until those screens exist.</summary>
        public void SetSecondaryScreensAvailable(bool howToPlay, bool leaderboardScreen)
        {
            howTo.Interactable = howToPlay;
            leaderboard.Interactable = leaderboardScreen;
        }

        // The pieces bob up and down one after another (prototype: 1.4 s each way, 0.17 s apart).
        void StartFloating()
        {
            StopFloating();
            for (int i = 0; i < deco.Count; i++)
            {
                var t = deco[i].rectTransform;
                float height = FloatHeight * PieceSize * layout.Unit;
                int index = i;
                floats.Add(Tween.Delay(index * FloatStagger, () =>
                {
                    if (this != null && gameObject.activeInHierarchy)
                        floats.Add(Tween.Bounce(t, height, FloatHalfPeriod));
                }));
            }
        }

        void StopFloating()
        {
            foreach (var f in floats) f?.Kill();
            floats.Clear();
        }

        RectTransform NewRect(string name)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI NewText(string name, string text, Color color)
        {
            var tmp = NewRect(name).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            UiFonts.UseBody(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
