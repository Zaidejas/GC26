using System;
using System.Collections.Generic;
using GC26.Lines.Core;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// How to play (GDD → Screens; prototype's tutorial): a back arrow, then 3 cards, each with a small
    /// board picture, a title and a short text; dots show which card is open; Back / Next, and "Got it"
    /// on the last card. Every card reserves the height of the tallest title and text, so the picture
    /// and the title stay in the same place when switching cards. A swipe left or right also changes the card:
    /// the card follows the finger, slides out and the next one slides in (Back / Next slide the same way);
    /// a short swipe springs back; the first and last card only stretch a little; a swipe never closes.
    /// </summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        /// <summary>A card: its mini board ("0"–"6" piece, "a"–"g" preview dot, "*" path step, "o" target, "." empty).</summary>
        /// Its title and text come from <see cref="Texts"/> in the current language.
        public readonly struct Card
        {
            public readonly string Mini, TitleKey, TextKey;
            public Card(string mini, string titleKey, string textKey) { Mini = mini; TitleKey = titleKey; TextKey = textKey; }
            public string Title => Texts.Get(TitleKey);
            public string Text => Texts.Get(TextKey);
        }

        public static readonly Card[] Cards =
        {
            new Card("0****o", "howto1Title", "howto1Text"),
            new Card(".22222", "howto2Title", "howto2Text"),
            new Card("a.4.bc", "howto3Title", "howto3Text"),
        };

        // Prototype sizes (cqw).
        const float PadSide = 5f, PadBottom = 7f, Gap = 5f, TutGap = 4.5f;
        const float MiniWidth = 78f, MiniPad = 1.2f, MiniGap = 0.8f, MiniRadius = 2.6f, CellRadius = 1.1f;
        const float TitleSize = 7f, TitleTopMargin = 2f, TextSize = 4.5f, TextWidth = 76f;
        const float DotSize = 2.2f, DotGap = 2.2f, ButtonHeight = 12f, ButtonGap = 3f, TargetRing = 0.6f;
        static readonly Color Focus = Palette.Focus;
        static readonly Color MiniCell = Palette.MiniCell;
        const int MiniCells = 6;

        // Prototype's slide: out in 0.15 s, the next card in from the other side in 0.22 s, 45 % of the width.
        public const float SlideOutSeconds = 0.15f, SlideInSeconds = 0.22f, SlideShare = 0.45f, SpringSeconds = 0.2f;
        // A swipe changes the card if it moved 18 % of the width, or was quick (1.2 widths per second).
        public const float SwipeShare = 0.18f, FlickWidthsPerSecond = 1.2f, EdgeStretch = 0.25f;

        Canvas canvas;
        RectTransform root, mini;
        RectTransform card;        // the mini board, title and text: the part that slides
        CanvasGroup cardFade;
        SwipeArea swipe;
        bool sliding;
        Image miniBackground;
        readonly List<Image> cellImages = new List<Image>();
        readonly List<Image> cellInner = new List<Image>();
        readonly List<Image> cellContent = new List<Image>();
        readonly List<Image> pageDots = new List<Image>();
        TextMeshProUGUI title, text;
        PillButton back, previous, next;
        BoardLayout layout;
        PieceSet pieces = PieceSet.Balls();

        /// <summary>The player left (back arrow, or "Got it" on the last card).</summary>
        public event Action Closed;

        public int Step { get; private set; }
        public bool IsShowing => gameObject.activeSelf;
        public string Title => title.text;
        public string Text => text.text;
        public PillButton BackButton => back;
        public PillButton PreviousButton => previous;
        public PillButton NextButton => next;
        public RectTransform MiniBoard => mini;
        /// <summary>How far the card is shifted sideways now (pixels), and how visible it is (tests).</summary>
        public float CardOffset => card.anchoredPosition.x;
        public float CardAlpha => cardFade.alpha;
        /// <summary>A card change is sliding (buttons and swipes wait for it).</summary>
        public bool Sliding => sliding;
        public RectTransform TitleRect => title.rectTransform;

        public static HowToPlayView Create(Camera camera)
        {
            var go = new GameObject("How to play");
            var view = go.AddComponent<HowToPlayView>();
            view.Build(camera);
            go.SetActive(false);
            return view;
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

            back = PillButton.CreateIcon(root, SpriteFactory.HomeIcon(), "Home");
            back.MakePlain(); // the same plain house as on the game screen (teacher's feedback)
            back.Clicked += () => Closed?.Invoke();

            card = NewRect("Card", root);
            cardFade = card.gameObject.AddComponent<CanvasGroup>();
            cardFade.blocksRaycasts = false;
            mini = NewRect("Mini board", card);
            miniBackground = mini.gameObject.AddComponent<Image>();
            miniBackground.color = Palette.Board;
            for (int i = 0; i < MiniCells; i++)
            {
                var cell = NewRect("Cell " + i, mini).gameObject.AddComponent<Image>();
                cell.color = MiniCell;
                var inner = NewRect("Inner", cell.rectTransform).gameObject.AddComponent<Image>();
                inner.color = MiniCell;
                var content = NewRect("Content", cell.rectTransform).gameObject.AddComponent<Image>();
                content.preserveAspect = true;
                cellImages.Add(cell);
                cellInner.Add(inner);
                cellContent.Add(content);
            }

            title = NewText("Title", Palette.Text, true);
            text = NewText("Text", Palette.Text, false);
            title.rectTransform.SetParent(card, false);
            text.rectTransform.SetParent(card, false);
            for (int i = 0; i < Cards.Length; i++)
            {
                var dot = NewRect("Page " + i, root).gameObject.AddComponent<Image>();
                dot.sprite = SpriteFactory.Disc();
                pageDots.Add(dot);
            }

            previous = PillButton.Create(root, Texts.Get("back"));
            next = PillButton.Create(root, Texts.Get("nextButton"), primary: true);
            previous.Clicked += () => Slide(-1);
            next.Clicked += () =>
            {
                if (Step < Cards.Length - 1) Slide(1);
                else if (!sliding) Closed?.Invoke();
            };
            swipe = SwipeArea.Create(root);
            swipe.Moved += Drag;
            swipe.Released += Release;
            foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            foreach (var b in new[] { back, previous, next }) b.GetComponent<Image>().raycastTarget = true;
            swipe.Target.raycastTarget = true;
        }

        public void ApplyLayout(BoardLayout screen)
        {
            layout = screen;
            if (IsShowing) Draw();
        }

        /// <summary>Opens on the first card, drawing pieces with <paramref name="pieceSet"/>.</summary>
        public void Show(PieceSet pieceSet)
        {
            pieces = pieceSet ?? PieceSet.Balls();
            Step = 0;
            ResetCard();
            gameObject.SetActive(true);
            Draw();
        }

        public void Hide()
        {
            ResetCard();
            gameObject.SetActive(false);
        }

        /// <summary>Puts the buttons and the open card in the current language.</summary>
        public void RefreshTexts()
        {
            previous.SetText(Texts.Get("back"));
            next.SetText(Texts.Get(Step == Cards.Length - 1 ? "gotIt" : "nextButton"));
            if (IsShowing && layout.Unit > 0f) Draw();
        }

        /// <summary>Opens card <paramref name="step"/> at once (no slide).</summary>
        public void GoTo(int step)
        {
            ResetCard();
            Step = Mathf.Clamp(step, 0, Cards.Length - 1);
            Draw();
        }

        // ---- Sliding and swiping ----

        float Width => layout.ScreenSize.x > 0f ? layout.ScreenSize.x : 1000f;

        void SetCard(float offset, float alpha)
        {
            card.anchoredPosition = new Vector2(offset, 0f);
            cardFade.alpha = alpha;
        }

        void ResetCard()
        {
            Tween.Kill(card);
            sliding = false;
            SetCard(0f, 1f);
        }

        /// <summary>
        /// Changes to the next (<paramref name="direction"/> 1) or previous (-1) card: the card slides out to the
        /// other side and the new one slides in. Nothing happens past the first or last card, or during a slide.
        /// </summary>
        public void Slide(int direction)
        {
            int target = Step + direction;
            if (sliding || target < 0 || target >= Cards.Length) return;
            sliding = true;
            Tween.Kill(card);
            float fromX = CardOffset, fromAlpha = CardAlpha, outX = -direction * SlideShare * Width;
            Tween.To(card, SlideOutSeconds, p => SetCard(Mathf.Lerp(fromX, outX, p), Mathf.Lerp(fromAlpha, 0f, p)), Ease.InQuad)
                .OnComplete(() =>
                {
                    Step = target;
                    Draw();
                    float inX = direction * SlideShare * Width;
                    SetCard(inX, 0f);
                    Tween.To(card, SlideInSeconds, p => SetCard(Mathf.Lerp(inX, 0f, p), p), Ease.OutQuad)
                        .OnComplete(() => { sliding = false; SetCard(0f, 1f); });
                });
        }

        // The card follows the finger; at the first and last card it only stretches a little.
        void Drag(float dx)
        {
            if (sliding) return;
            Tween.Kill(card);
            bool edge = (dx > 0f && Step == 0) || (dx < 0f && Step == Cards.Length - 1);
            SetCard(edge ? dx * EdgeStretch : dx, edge ? 1f : 1f - Mathf.Min(0.6f, Mathf.Abs(dx) / Width));
        }

        void Release(float dx, float seconds)
        {
            if (sliding) return;
            int direction = dx < 0f ? 1 : -1;
            int target = Step + direction;
            bool far = Mathf.Abs(dx) > SwipeShare * Width;
            bool quick = Mathf.Abs(dx) / Mathf.Max(0.01f, seconds) > FlickWidthsPerSecond * Width;
            if ((far || quick) && target >= 0 && target < Cards.Length) { Slide(direction); return; }
            // Too short, or past the first or last card: spring back.
            float fromX = CardOffset, fromAlpha = CardAlpha;
            Tween.To(card, SpringSeconds, p => SetCard(Mathf.Lerp(fromX, 0f, p), Mathf.Lerp(fromAlpha, 1f, p)), Ease.OutQuad);
        }

        /// <summary>A swipe as if by a finger: it moves <paramref name="dx"/> pixels in <paramref name="seconds"/> (tests).</summary>
        public void Swipe(float dx, float seconds)
        {
            Drag(dx);
            Release(dx, seconds);
        }

        void Draw()
        {
            float u = layout.Unit;
            var safe = layout.SafeArea;
            float contentLeft = safe.x + (safe.width - 100f * u) / 2f;
            float centerX = contentLeft + 50f * u;
            var card = Cards[Step];

            back.Place(layout.BackButton, u);

            // Bottom: Back / Next buttons, then the page dots above them.
            float innerWidth = (100f - 2f * PadSide) * u;
            float left = contentLeft + PadSide * u;
            float buttonsY = safe.y + PadBottom * u;
            float half = (innerWidth - ButtonGap * u) / 2f;
            previous.Place(new Rect(left, buttonsY, half, ButtonHeight * u), u);
            next.Place(new Rect(left + half + ButtonGap * u, buttonsY, half, ButtonHeight * u), u);
            previous.Interactable = Step > 0;
            next.SetText(Texts.Get(Step == Cards.Length - 1 ? "gotIt" : "nextButton"));

            float dotsY = buttonsY + (ButtonHeight + Gap) * u;
            float dotsWidth = Cards.Length * DotSize * u + (Cards.Length - 1) * DotGap * u;
            for (int i = 0; i < pageDots.Count; i++)
            {
                Place(pageDots[i].rectTransform, centerX - dotsWidth / 2f + i * (DotSize + DotGap) * u, dotsY, DotSize * u, DotSize * u);
                pageDots[i].color = i == Step ? Palette.Focus : Palette.Line;
            }

            // The card: mini board, title and text, centred between the back arrow and the dots.
            title.fontSize = TitleSize * u;
            text.fontSize = TextSize * u;
            float textWidth = Mathf.Min(TextWidth * u, innerWidth);
            float titleH = 0f, textH = 0f;
            foreach (var c in Cards) // the tallest card decides, so nothing jumps between cards
            {
                titleH = Mathf.Max(titleH, title.GetPreferredValues(c.Title, innerWidth, 0f).y);
                textH = Mathf.Max(textH, text.GetPreferredValues(c.Text, textWidth, 0f).y);
            }
            float cell = (MiniWidth - 2f * MiniPad - (MiniCells - 1) * MiniGap) / MiniCells;
            float miniH = (cell + 2f * MiniPad) * u;
            float blockH = miniH + TutGap * u + TitleTopMargin * u + titleH + TutGap * u + textH;
            float areaTop = layout.BackButton.y - Gap * u;
            float areaBottom = dotsY + DotSize * u + Gap * u;
            float blockTop = (areaTop + areaBottom) / 2f + blockH / 2f;

            float miniWidth = MiniWidth * u;
            Place(mini, centerX - miniWidth / 2f, blockTop - miniH, miniWidth, miniH);
            UiShapes.MakeRounded(miniBackground, MiniRadius * u);
            DrawMini(card.Mini, cell * u, u);

            float titleTop = blockTop - miniH - TutGap * u - TitleTopMargin * u;
            title.text = card.Title;
            Place(title.rectTransform, left, titleTop - titleH, innerWidth, titleH);
            text.text = card.Text;
            float textTop = titleTop - titleH - TutGap * u;
            Place(text.rectTransform, centerX - textWidth / 2f, textTop - textH, textWidth, textH);
        }

        void DrawMini(string spec, float cellPx, float u)
        {
            for (int i = 0; i < MiniCells; i++)
            {
                var cell = cellImages[i];
                Place(cell.rectTransform, MiniPad * u + i * (cellPx + MiniGap * u), MiniPad * u, cellPx, cellPx);
                UiShapes.MakeRounded(cell, CellRadius * u);

                char ch = i < spec.Length ? spec[i] : '.';
                bool target = ch == 'o';
                cell.color = target ? Focus : MiniCell;   // the target cell gets a gold ring
                var inner = cellInner[i];
                float ring = target ? TargetRing * u : 0f;
                Place(inner.rectTransform, ring, ring, cellPx - 2f * ring, cellPx - 2f * ring);
                UiShapes.MakeRounded(inner, Mathf.Max(0.5f, CellRadius * u - ring));

                var content = cellContent[i];
                content.enabled = true;
                content.color = Color.white;
                float size;
                if (ch >= '0' && ch <= '6') { content.sprite = pieces.SpriteFor(BallColors.All[ch - '0']); size = pieces.SizeInCell; }
                else if (ch >= 'a' && ch <= 'g') { content.sprite = pieces.SpriteFor(BallColors.All[ch - 'a']); content.color = PieceSet.GhostColor; size = pieces.DotSizeInCell; }
                else if (ch == '*') { content.sprite = SpriteFactory.Disc(); content.color = new Color(Palette.SoftText.r, Palette.SoftText.g, Palette.SoftText.b, 0.5f); size = 0.22f; }
                else { content.enabled = false; size = 0f; }
                float s = cellPx * size;
                Place(content.rectTransform, (cellPx - s) / 2f, (cellPx - s) / 2f, s, s);
            }
        }

        /// <summary>What a mini-board cell shows: "piece", "dot", "step", "target" or "empty" (for tests).</summary>
        public string CellKind(int index)
        {
            char ch = Cards[Step].Mini[index];
            if (ch >= '0' && ch <= '6') return "piece";
            if (ch >= 'a' && ch <= 'g') return "dot";
            if (ch == '*') return "step";
            if (ch == 'o') return "target";
            return "empty";
        }

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI NewText(string name, Color color, bool bold)
        {
            var tmp = NewRect(name, root).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            if (bold) UiFonts.UseDisplay(tmp); else UiFonts.UseBody(tmp); // the title is Mystery Quest
            tmp.alignment = bold ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Top;
            tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.lineSpacing = bold ? 0f : 10f;
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
