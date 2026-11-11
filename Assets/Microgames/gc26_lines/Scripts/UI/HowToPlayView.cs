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
    /// and the title stay in the same place when switching cards.
    /// </summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        /// <summary>A card: its mini board ("0"–"6" piece, "a"–"g" preview dot, "*" path step, "o" target, "." empty).</summary>
        public readonly struct Card
        {
            public readonly string Mini, Title, Text;
            public Card(string mini, string title, string text) { Mini = mini; Title = title; Text = text; }
        }

        public static readonly Card[] Cards =
        {
            new Card("0****o", "Move a piece",
                "Tap a piece, then an empty cell. It moves only if there is a free path up, down, left or right."),
            new Card(".22222", "Make lines of five",
                "Five or more of the same kind in a row, column or diagonal disappear and score points. Longer lines score more."),
            new Card("a.4.bc", "Watch what's coming",
                "If your move makes no line, 3 new pieces arrive where the small dots show. The game ends when the board is full. Undo takes back one move."),
        };

        // Prototype sizes (cqw).
        const float PadSide = 5f, PadBottom = 7f, Gap = 5f, TutGap = 4.5f;
        const float MiniWidth = 78f, MiniPad = 1.2f, MiniGap = 0.8f, MiniRadius = 2.6f, CellRadius = 1.1f;
        const float TitleSize = 7f, TitleTopMargin = 2f, TextSize = 4.5f, TextWidth = 76f;
        const float DotSize = 2.2f, DotGap = 2.2f, ButtonHeight = 12f, ButtonGap = 3f, TargetRing = 0.6f;
        static readonly Color Focus = Palette.Focus;
        static readonly Color MiniCell = Palette.MiniCell;
        const int MiniCells = 6;

        Canvas canvas;
        RectTransform root, mini;
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

            back = PillButton.CreateIcon(root, SpriteFactory.BackArrow(), "Back");
            back.Clicked += () => Closed?.Invoke();

            mini = NewRect("Mini board", root);
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
            for (int i = 0; i < Cards.Length; i++)
            {
                var dot = NewRect("Page " + i, root).gameObject.AddComponent<Image>();
                dot.sprite = SpriteFactory.Disc();
                pageDots.Add(dot);
            }

            previous = PillButton.Create(root, "Back");
            next = PillButton.Create(root, "Next", primary: true);
            previous.Clicked += () => GoTo(Step - 1);
            next.Clicked += () =>
            {
                if (Step < Cards.Length - 1) GoTo(Step + 1);
                else Closed?.Invoke();
            };
            foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            foreach (var b in new[] { back, previous, next }) b.GetComponent<Image>().raycastTarget = true;
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
            gameObject.SetActive(true);
            Draw();
        }

        public void Hide() => gameObject.SetActive(false);

        public void GoTo(int step)
        {
            Step = Mathf.Clamp(step, 0, Cards.Length - 1);
            Draw();
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
            next.SetText(Step == Cards.Length - 1 ? "Got it" : "Next");

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
