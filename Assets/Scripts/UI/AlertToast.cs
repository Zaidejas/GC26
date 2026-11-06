using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
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
        static readonly Color Background = new Color(40 / 255f, 34 / 255f, 28 / 255f, 0.9f);

        RectTransform rect;
        CanvasGroup group;
        Image background;
        TextMeshProUGUI label;
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
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        /// <summary>Centres the label on <paramref name="centre"/> (pixels, origin bottom-left) and sizes it for the screen.</summary>
        public void ApplyLayout(Vector2 centre, float unitPixels)
        {
            unit = unitPixels;
            rect.anchoredPosition = centre;
            label.fontSize = FontSize * unit;
            // A pill: corners as round as half the height.
            float height = label.fontSize * 1.25f + 2f * PaddingY * unit;
            UiShapes.MakePill(background, height);
            Resize();
        }

        /// <summary>Shows <paramref name="text"/>; showing again while visible restarts the timer.</summary>
        public void Show(string text)
        {
            label.text = text;
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
            hideTimer = Tween.Delay(VisibleSeconds, Hide);
        }

        void Hide()
        {
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
            rect.sizeDelta = new Vector2(size.x + 2f * PaddingX * unit, label.fontSize * 1.25f + 2f * PaddingY * unit);
        }
    }
}
