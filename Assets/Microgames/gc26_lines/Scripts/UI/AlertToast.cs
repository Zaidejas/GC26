using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// A short message in a dark rounded label over the middle of the board ("No path").
    /// As in the prototype: fades and grows in (0.15 s), stays, fades out after 1.1 s.
    /// </summary>
    public sealed class AlertToast : MonoBehaviour
    {
        public const float FadeSeconds = 0.15f;
        public const float VisibleSeconds = 1.1f;
        const float FontSize = 4.2f, PaddingX = 5f, PaddingY = 2.2f; // prototype, in units (1 % of the width)
        static readonly Color Background = Palette.Toast;

        RectTransform rect;
        CanvasGroup group;
        Image background;
        TextMeshProUGUI label, sub;
        const float SubSize = 3.4f, SubSeconds = 2f; // L15: a smaller second line ("Make a line through it…"), shown longer
        float unit = 10f;
        TweenHandle showTween;
        TweenHandle hideTimer;

        public string Text => label.text;
        public float Alpha => group.alpha;
        public bool IsShowing { get; private set; }

        public static AlertToast Create(RectTransform parent)
        {
            var go = new GameObject("Alert", typeof(RectTransform));
            var toast = go.AddComponent<AlertToast>();
            toast.Build(parent);
            return toast;
        }

        void Build(RectTransform parent)
        {
            rect = (RectTransform)transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            background = gameObject.AddComponent<Image>();
            background.color = Background;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.color = Palette.Text;
            label.fontStyle = FontStyles.Bold;
            UiFonts.UseBody(label);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            var subGo = new GameObject("Second line", typeof(RectTransform));
            var subRect = (RectTransform)subGo.transform;
            subRect.SetParent(rect, false);
            subRect.anchorMin = subRect.anchorMax = subRect.pivot = new Vector2(0.5f, 0f);
            sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.color = Palette.Text;
            sub.alpha = 0.85f;
            UiFonts.UseBody(sub);
            sub.alignment = TextAlignmentOptions.Center;
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.raycastTarget = false;
            sub.gameObject.SetActive(false);
        }

        /// <summary>The second line, or "" (tests).</summary>
        public string SecondLine => sub.gameObject.activeSelf ? sub.text : "";

        /// <summary>Centres the label on <paramref name="centre"/> (pixels, origin bottom-left) and sizes it for the screen.</summary>
        public void ApplyLayout(Vector2 centre, float unitPixels)
        {
            unit = unitPixels;
            rect.anchoredPosition = centre;
            label.fontSize = FontSize * unit;
            sub.fontSize = SubSize * unit;
            Resize();
        }

        /// <summary>Shows <paramref name="text"/>; showing again while visible restarts the timer.</summary>
        public void Show(string text, string secondLine = null)
        {
            label.text = text;
            bool two = !string.IsNullOrEmpty(secondLine);
            sub.gameObject.SetActive(two);
            if (two) sub.text = secondLine;
            Resize();
            IsShowing = true;
            showTween?.Kill();
            hideTimer?.Kill();
            float startAlpha = group.alpha;
            showTween = Tween.To(this, FadeSeconds, p =>
            {
                group.alpha = Mathf.Lerp(startAlpha, 1f, p);
                rect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, p);
            });
            hideTimer = Tween.Delay(two ? SubSeconds : VisibleSeconds, Hide);
        }

        void Hide()
        {
            if (this == null) return; // the screen was torn down before the timer ran out
            showTween?.Kill();
            showTween = Tween.To(this, FadeSeconds, p =>
            {
                group.alpha = 1f - p;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, p);
            }).OnComplete(() => IsShowing = false);
        }

        void Resize()
        {
            var size = label.GetPreferredValues(label.text);
            float lineH = label.fontSize * 1.25f;
            bool two = sub.gameObject.activeSelf;
            float subH = two ? sub.fontSize * 1.3f : 0f;
            float width = Mathf.Max(size.x, two ? sub.GetPreferredValues(sub.text).x : 0f);
            float height = lineH + subH + 2f * PaddingY * unit;
            rect.sizeDelta = new Vector2(width + 2f * PaddingX * unit, height);
            // One line: a pill. Two lines: a rounded card (corners as round as one line's pill).
            UiShapes.MakePill(background, two ? lineH + 2f * PaddingY * unit : height);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, two ? 1f : 0f);
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(0f, two ? -(PaddingY * unit + lineH) : 0f);
            labelRect.offsetMax = Vector2.zero;
            if (two)
            {
                labelRect.offsetMax = new Vector2(0f, -PaddingY * unit * 0.6f);
                sub.rectTransform.anchoredPosition = new Vector2(0f, PaddingY * unit * 0.9f);
                sub.rectTransform.sizeDelta = new Vector2(width, subH);
            }
        }
    }
}
