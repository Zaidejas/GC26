using System;
using System.Collections.Generic;
using System.Globalization;
using Lines.Core;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// The Leaderboard screen (GDD → Screens; prototype's "scores"): a back arrow, the result line after
    /// a game ("You're #3, Anna!"), the top 10 (rank, name, date, score; the game just saved has a blue
    /// border), and Play again right after a game. The list scrolls when it
    /// doesn't fit. With no games yet it shows a short explanation instead.
    /// </summary>
    public sealed class LeaderboardView : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float PadSide = 5f, PadBottom = 7f, Gap = 5f;
        const float MessageSize = 5.2f, EmptySize = 4.4f, PlayAgainHeight = 13f;
        const float RowHeight = 15f, RowGap = 2f, RowRadius = 3f, RowBorder = 0.5f, RowPadX = 4f;
        const float RankWidth = 8f, ColumnGap = 3f, RankSize = 5f, NameSize = 4.4f, DateSize = 3.6f, PointsSize = 6f;
        public const string EmptyText = "No finished games yet. Your score is added here when a game ends.";
        static readonly Color Focus = Palette.Focus;

        sealed class Row
        {
            public RectTransform Rect;
            public Image Border, Fill, Ring;
            public TextMeshProUGUI Rank, Name, Date, Points;
        }

        Canvas canvas;
        RectTransform root, viewport, content;
        ScrollRect scroll;
        readonly List<Row> rows = new List<Row>();
        TextMeshProUGUI message, empty;
        PillButton back, playAgain;
        BoardLayout layout;
        Leaderboard board = new Leaderboard();
        long highlightTime;

        /// <summary>The back arrow.</summary>
        public event Action Closed;
        public event Action PlayAgainClicked;

        public bool IsShowing => gameObject.activeSelf;
        public string Message => message.gameObject.activeSelf ? message.text : "";
        public bool ShowsEmptyText => empty.gameObject.activeSelf;
        public int RowCount => board.Count;
        public PillButton BackButton => back;
        public PillButton PlayAgainButton => playAgain;
        public bool PlayAgainShowing => playAgain.gameObject.activeSelf;
        /// <summary>The highlighted row (the game just saved), or -1.</summary>
        public int HighlightedRow { get; private set; } = -1;
        public float ScrollPosition => scroll.verticalNormalizedPosition;

        public static LeaderboardView Create(Camera camera)
        {
            var go = new GameObject("Leaderboard");
            var view = go.AddComponent<LeaderboardView>();
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

            message = NewText("Message", root, Palette.Text, true, TextAlignmentOptions.Center);
            UiFonts.UseDisplay(message);
            empty = NewText("Empty", root, Palette.SoftText, false, TextAlignmentOptions.Center);
            empty.text = EmptyText;
            empty.textWrappingMode = TextWrappingModes.Normal;
            empty.lineSpacing = 12f;

            // The list: a masked viewport that can be dragged, with the rows inside.
            viewport = NewRect("List", root);
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f); // invisible, but catches drags
            viewport.gameObject.AddComponent<RectMask2D>();
            content = NewRect("Rows", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 20f;

            for (int i = 0; i < Leaderboard.MaxEntries; i++)
            {
                var row = new Row { Rect = NewRect("Row " + (i + 1), content) };
                row.Rect.anchorMin = row.Rect.anchorMax = row.Rect.pivot = new Vector2(0f, 1f);
                row.Border = row.Rect.gameObject.AddComponent<Image>();
                row.Border.raycastTarget = false;
                row.Fill = NewRect("Fill", row.Rect).gameObject.AddComponent<Image>();
                row.Fill.raycastTarget = false;
                row.Fill.enabled = false; // the row itself is the see-through panel
                row.Ring = NewRect("Ring", row.Rect).gameObject.AddComponent<Image>();
                row.Ring.color = Focus;
                row.Ring.raycastTarget = false;
                row.Rank = NewText("Rank", row.Rect, Palette.SoftText, true, TextAlignmentOptions.Left);
                row.Name = NewText("Name", row.Rect, Palette.Text, true, TextAlignmentOptions.BottomLeft);
                row.Name.overflowMode = TextOverflowModes.Ellipsis;
                row.Date = NewText("Date", row.Rect, Palette.SoftText, true, TextAlignmentOptions.TopLeft);
                row.Points = NewText("Points", row.Rect, Palette.Text, true, TextAlignmentOptions.Right);
                UiFonts.UseDisplay(row.Rank);
                UiFonts.UseDisplay(row.Points);
                rows.Add(row);
            }

            playAgain = PillButton.Create(root, "Play again", primary: true);
            playAgain.Clicked += () => PlayAgainClicked?.Invoke();
        }

        public void ApplyLayout(BoardLayout screen)
        {
            layout = screen;
            if (IsShowing) Draw();
        }

        /// <summary>
        /// Shows <paramref name="scores"/>. The entry saved at <paramref name="latestTime"/> is highlighted
        /// (0: none); <paramref name="result"/> is the line above the list (null: none); Play again shows
        /// only right after a finished game.
        /// </summary>
        public void Show(Leaderboard scores, long latestTime = 0, string result = null, bool showPlayAgain = false)
        {
            board = scores ?? new Leaderboard();
            highlightTime = latestTime;
            message.text = result ?? "";
            message.gameObject.SetActive(!string.IsNullOrEmpty(result));
            playAgain.gameObject.SetActive(showPlayAgain);
            gameObject.SetActive(true);
            Draw();
        }

        public void Hide() => gameObject.SetActive(false);

        void Draw()
        {
            float u = layout.Unit;
            var safe = layout.SafeArea;
            float contentLeft = safe.x + (safe.width - 100f * u) / 2f;
            float left = contentLeft + PadSide * u, width = (100f - 2f * PadSide) * u;

            back.Place(layout.BackButton, u);

            // From the bottom up: Play again; from the top down: the result line.
            float bottom = safe.y + PadBottom * u;
            if (PlayAgainShowing)
            {
                playAgain.Place(new Rect(left, bottom, width, PlayAgainHeight * u), u);
                bottom += (PlayAgainHeight + Gap) * u;
            }

            float top = layout.BackButton.y - Gap * u;
            if (message.gameObject.activeSelf)
            {
                message.fontSize = MessageSize * u;
                float messageH = MessageSize * 1.3f * u;
                Place(message.rectTransform, left, top - messageH, width, messageH);
                top -= messageH + Gap * u;
            }

            bool any = board.Count > 0;
            empty.gameObject.SetActive(!any);
            viewport.gameObject.SetActive(any);
            if (!any)
            {
                empty.fontSize = EmptySize * u;
                Place(empty.rectTransform, left + 6f * u, bottom, width - 12f * u, Mathf.Max(0f, top - bottom));
                HighlightedRow = -1;
                return;
            }

            Place(viewport, left, bottom, width, Mathf.Max(0f, top - bottom));
            float rowH = RowHeight * u;
            content.sizeDelta = new Vector2(0f, board.Count * rowH + (board.Count - 1) * RowGap * u);
            content.anchoredPosition = Vector2.zero;
            HighlightedRow = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool used = i < board.Count;
                row.Rect.gameObject.SetActive(used);
                if (!used) continue;
                var entry = board.entries[i];
                bool latest = highlightTime != 0 && entry.time == highlightTime;
                if (latest) HighlightedRow = i;
                DrawRow(row, i, entry, latest, width, u);
            }
            ScrollToHighlight(top - bottom);
        }

        void DrawRow(Row row, int index, ScoreEntry entry, bool latest, float width, float u)
        {
            float h = RowHeight * u;
            row.Rect.anchoredPosition = new Vector2(0f, -index * (RowHeight + RowGap) * u);
            row.Rect.sizeDelta = new Vector2(width, h);
            row.Border.color = Palette.SeeThroughPanel;
            UiShapes.MakeRounded(row.Border, RowRadius * u);
            row.Ring.enabled = latest; // the game just saved: a gold outline on top
            Place(row.Ring.rectTransform, 0f, 0f, width, h);
            UiShapes.MakeRing(row.Ring, RowRadius * u);

            float x = (RowBorder + RowPadX) * u;
            row.Rank.text = (index + 1).ToString();
            row.Rank.fontSize = RankSize * u;
            Place(row.Rank.rectTransform, x, 0f, RankWidth * u, h);

            row.Points.text = entry.score.ToString();
            row.Points.fontSize = PointsSize * u;
            float pointsW = row.Points.GetPreferredValues(row.Points.text).x;
            float right = width - x;
            Place(row.Points.rectTransform, right - pointsW, 0f, pointsW, h);

            float whoX = x + (RankWidth + ColumnGap) * u;
            float whoW = Mathf.Max(0f, right - pointsW - ColumnGap * u - whoX);
            row.Name.text = entry.name;
            row.Name.fontSize = NameSize * u;
            row.Date.text = DateText(entry.time);
            row.Date.fontSize = DateSize * u;
            // Boxes as tall as the font's own lines: a name that doesn't fit its box would vanish (it can
            // only be shortened with "…" sideways).
            float nameH = row.Name.GetPreferredValues("Ag").y, dateH = row.Date.GetPreferredValues("Ag").y;
            float textTop = h / 2f + (nameH + dateH) / 2f;
            Place(row.Name.rectTransform, whoX, textTop - nameH, whoW, nameH);
            Place(row.Date.rectTransform, whoX, textTop - nameH - dateH, whoW, dateH);
        }

        // Brings the highlighted row into the middle of the list when the list is taller than the screen.
        void ScrollToHighlight(float viewportHeight)
        {
            float overflow = content.sizeDelta.y - viewportHeight;
            if (overflow <= 0f || HighlightedRow < 0)
            {
                scroll.verticalNormalizedPosition = 1f;
                return;
            }
            float u = layout.Unit;
            float rowTop = HighlightedRow * (RowHeight + RowGap) * u;
            float offset = Mathf.Clamp(rowTop - (viewportHeight - RowHeight * u) / 2f, 0f, overflow);
            scroll.verticalNormalizedPosition = 1f - offset / overflow;
        }

        /// <summary>The date under a name, like the prototype's: "6 Nov".</summary>
        public static string DateText(long time) =>
            DateTimeOffset.FromUnixTimeMilliseconds(time).ToLocalTime().ToString("d MMM", CultureInfo.InvariantCulture);

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        static TextMeshProUGUI NewText(string name, RectTransform parent, Color color, bool bold, TextAlignmentOptions align)
        {
            var tmp = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            UiFonts.UseBody(tmp);
            tmp.alignment = align;
            if (align == TextAlignmentOptions.Left || align == TextAlignmentOptions.Right || align == TextAlignmentOptions.Center)
                tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
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
