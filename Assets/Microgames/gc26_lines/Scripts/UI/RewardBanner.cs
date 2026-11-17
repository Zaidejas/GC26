using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// L16: "A puffball for you!" — a gold-framed card over the board with the puffball, a title and a line under it.
    /// It pops in, stays a moment, then rises a little and fades (prototype: 2.6 s).
    /// </summary>
    public sealed class RewardBanner : MonoBehaviour
    {
        public const float Seconds = 2.6f;
        const float IconSize = 11f, TitleSize = 5.4f, TextSize = 3.6f, PadX = 5f, PadLeft = 3f, PadY = 2.6f, Gap = 3f;
        static readonly Color Gold = new Color(1f, 0.835f, 0.42f);

        RectTransform root;
        Image border, background, icon;
        TextMeshProUGUI title, text;
        CanvasGroup group;
        Vector2 home;
        float unit = 10f;
        TweenHandle show;

        public bool IsShowing => group.alpha > 0.01f;
        public string Title => title.text;

        public static RewardBanner Create(RectTransform parent)
        {
            var go = new GameObject("Reward banner", typeof(RectTransform));
            var banner = go.AddComponent<RewardBanner>();
            banner.Build(parent);
            return banner;
        }

        void Build(RectTransform parent)
        {
            root = (RectTransform)transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            border = Child("Border", Gold);
            background = Child("Background", new Color(44 / 255f, 30 / 255f, 78 / 255f, 0.96f));
            icon = Child("Icon", Color.white);
            icon.sprite = SpecialArt.Puffball();
            icon.preserveAspect = true;
            title = Text("Title", Gold, TextAlignmentOptions.BottomLeft);
            UiFonts.UseDisplay(title);
            text = Text("Text", Palette.Text, TextAlignmentOptions.TopLeft);
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

        TextMeshProUGUI Text(string name, Color color, TextAlignmentOptions alignment)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            UiFonts.UseBody(tmp);
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Centres the banner on <paramref name="centre"/> (pixels; prototype: 30 % down the screen).</summary>
        public void ApplyLayout(Vector2 centre, float unitPixels)
        {
            home = centre;
            unit = unitPixels;
            root.anchoredPosition = centre;
            Layout();
        }

        void Layout()
        {
            float u = unit;
            title.fontSize = TitleSize * u;
            text.fontSize = TextSize * u;
            float textW = Mathf.Max(title.GetPreferredValues(title.text).x, text.GetPreferredValues(text.text).x);
            float titleH = TitleSize * 1.25f * u, textH = TextSize * 1.3f * u;
            float w = PadLeft * u + IconSize * u + Gap * u + textW + PadX * u, h = Mathf.Max(IconSize * u, titleH + textH) + 2f * PadY * u;
            root.sizeDelta = new Vector2(w, h);
            Place(border.rectTransform, -0.4f * u, -0.4f * u, w + 0.8f * u, h + 0.8f * u);
            Place(background.rectTransform, 0f, 0f, w, h);
            UiShapes.MakeRounded(border, 4.4f * u);
            UiShapes.MakeRounded(background, 4f * u);
            Place(icon.rectTransform, PadLeft * u, (h - IconSize * u) / 2f, IconSize * u, IconSize * u);
            float x = PadLeft * u + IconSize * u + Gap * u, middle = h / 2f;
            Place(title.rectTransform, x, middle + textH / 2f - titleH / 2f, textW + 1f, titleH);
            Place(text.rectTransform, x, middle - textH / 2f - titleH / 2f - 0.2f * u, textW + 1f, textH);
        }

        /// <summary>Shows the banner in the current language.</summary>
        public void Show()
        {
            title.text = Texts.Get("superGot");
            text.text = Texts.Get("superGotText");
            Layout();
            show?.Kill();
            float rise = 2f * unit;
            show = Tween.To(this, Seconds, p =>
            {
                group.alpha = p < 0.12f ? p / 0.12f : p < 0.8f ? 1f : 1f - (p - 0.8f) / 0.2f;
                float size = p < 0.12f ? Mathf.Lerp(0.6f, 1.06f, p / 0.12f) : p < 0.2f ? Mathf.Lerp(1.06f, 1f, (p - 0.12f) / 0.08f) : 1f;
                root.localScale = Vector3.one * size;
                root.anchoredPosition = home + Vector2.up * (p > 0.8f ? rise * (p - 0.8f) / 0.2f : 0f);
            }).OnComplete(() => { group.alpha = 0f; root.anchoredPosition = home; });
            show.OnKilled = () => { if (root != null) { root.anchoredPosition = home; root.localScale = Vector3.one; } };
        }

        /// <summary>Hides it at once (a new game, leaving the screen).</summary>
        public void Stop()
        {
            show?.Kill();
            group.alpha = 0f;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
