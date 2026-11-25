using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// L16: the bubble above the puffball button: a gold title, a soft line under it and, when the next puffball is
    /// still coming, a gold progress bar ("Puffball in 40 points"); while aiming, "Where to burst?" and how.
    /// </summary>
    public sealed class HintBubble : MonoBehaviour
    {
        const float TitleSize = 4.4f, TextSize = 3.4f, PadX = 4f, PadY = 2.4f, BarHeight = 1.2f, MaxWidth = 78f, Gap = 3f;
        static readonly Color Gold = new Color(1f, 0.835f, 0.42f);

        RectTransform root;
        Image background, border, barBack, barFill;
        TextMeshProUGUI title, text;
        CanvasGroup group;
        float unit = 10f, progress = -1f;
        Vector2 anchor, boardTop;
        bool aboveBoard;
        const float AboveBoardGap = 2f;
        /// <summary>The bubble sits above the board (while aiming), not above the puffball button (tests).</summary>
        public bool AboveBoard => aboveBoard;
        /// <summary>The bubble's bottom edge, in the HUD's pixels (tests).</summary>
        public float BottomY => root.anchoredPosition.y;
        TweenHandle fade, timer;

        public bool IsShowing { get; private set; }
        public string Title => title.text;
        public string Text => text.text;
        public bool BarShowing => barBack.enabled;

        public static HintBubble Create(RectTransform parent)
        {
            var go = new GameObject("Hint bubble", typeof(RectTransform));
            var bubble = go.AddComponent<HintBubble>();
            bubble.Build(parent);
            return bubble;
        }

        void Build(RectTransform parent)
        {
            root = (RectTransform)transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            border = Child("Border", Palette.GuideLine); // hints are guidance: moonlight blue (the progress bar stays gold: a reward)
            background = Child("Background", new Color(20 / 255f, 14 / 255f, 42 / 255f, 0.95f));
            title = NewText("Title", Palette.Guide);
            UiFonts.UseBold(title); // read mid-game: Nunito ExtraBold (GDD → Fonts)
            text = NewText("Text", Palette.SoftText);
            barBack = Child("Bar", new Color(1f, 1f, 1f, 0.12f));
            barFill = Child("Fill", Gold);
        }

        Image Child(string name, Color color)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI NewText(string name, Color color)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            UiFonts.UseBody(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>The bubble's tail points down at <paramref name="buttonTop"/> (the button's top middle, pixels).</summary>
        public void ApplyLayout(Vector2 buttonTop, float unitPixels) => ApplyLayout(buttonTop, unitPixels, buttonTop);

        /// <summary><paramref name="boardTopMiddle"/>: where an "above the board" bubble stands (the board's top middle).</summary>
        public void ApplyLayout(Vector2 buttonTop, float unitPixels, Vector2 boardTopMiddle)
        {
            anchor = buttonTop;
            boardTop = boardTopMiddle;
            unit = unitPixels;
            if (IsShowing) Layout();
        }

        /// <summary>
        /// Shows <paramref name="titleText"/> and <paramref name="lineText"/>; <paramref name="bar"/>: a progress bar
        /// (0–1) or below 0 for none; <paramref name="seconds"/>: hides by itself after that long (0: stays).
        /// <paramref name="overBoard"/>: the bubble rises above the board, over SCORE / NEXT (while aiming: over the
        /// board it hid the bottom rows the player aims at).
        /// </summary>
        public void Show(string titleText, string lineText, float bar = -1f, float seconds = 0f, bool overBoard = false)
        {
            aboveBoard = overBoard;
            title.text = titleText;
            text.text = lineText;
            progress = bar;
            IsShowing = true;
            Layout();
            fade?.Kill();
            timer?.Kill();
            float start = group.alpha;
            fade = Tween.To(this, 0.18f, p =>
            {
                group.alpha = Mathf.Lerp(start, 1f, p);
                root.anchoredPosition = Home() - new Vector2(0f, unit * (1f - p));
            });
            if (seconds > 0f) timer = Tween.Delay(seconds, Hide);
        }

        // Where the bubble's bottom middle stands: above the puffball button, or above the board while aiming.
        Vector2 Home() => aboveBoard ? boardTop + new Vector2(0f, AboveBoardGap * unit) : anchor + new Vector2(0f, Gap * unit);

        public void Hide()
        {
            if (this == null || !IsShowing) return;
            IsShowing = false;
            fade?.Kill();
            timer?.Kill();
            float start = group.alpha;
            fade = Tween.To(this, 0.18f, p => group.alpha = Mathf.Lerp(start, 0f, p));
        }

        void Layout()
        {
            float u = unit;
            title.fontSize = TitleSize * u;
            text.fontSize = TextSize * u;
            float inner = MaxWidth * u - 2f * PadX * u;
            float width = Mathf.Min(inner, Mathf.Max(title.GetPreferredValues(title.text).x, text.GetPreferredValues(text.text).x) + 1f);
            float titleH = title.GetPreferredValues(title.text, width, 0f).y;
            float textH = text.GetPreferredValues(text.text, width, 0f).y;
            bool bar = progress >= 0f;
            float barH = bar ? BarHeight * u + 1.8f * u : 0f;
            float boxW = width + 2f * PadX * u, boxH = PadY * u + titleH + 0.6f * u + textH + barH + PadY * u;
            root.sizeDelta = new Vector2(boxW, boxH);
            root.anchoredPosition = Home();
            Place(border.rectTransform, -0.35f * u, -0.35f * u, boxW + 0.7f * u, boxH + 0.7f * u);
            Place(background.rectTransform, 0f, 0f, boxW, boxH);
            UiShapes.MakeRounded(border, 3.3f * u);
            UiShapes.MakeRounded(background, 3f * u);
            float y = boxH - PadY * u - titleH;
            Place(title.rectTransform, PadX * u, y, width, titleH);
            y -= 0.6f * u + textH;
            Place(text.rectTransform, PadX * u, y, width, textH);
            barBack.enabled = barFill.enabled = bar;
            if (!bar) return;
            y -= 1.8f * u + BarHeight * u - 0.6f * u;
            Place(barBack.rectTransform, PadX * u, y, width, BarHeight * u);
            Place(barFill.rectTransform, PadX * u, y, width * Mathf.Clamp01(progress), BarHeight * u);
            UiShapes.MakePill(barBack, BarHeight * u);
            UiShapes.MakePill(barFill, BarHeight * u);
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
