using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Findepair
{
    /// <summary>Palette and small helpers for building the UI from code (sizes are in 1080 x 1920 reference pixels).</summary>
    public static class FindepairUIKit
    {
        public static readonly Color Pink = Hex("#FF2FB3");
        public static readonly Color Pink2 = Hex("#FF7AD9");
        public static readonly Color Violet = Hex("#9B5CFF");
        public static readonly Color Cyan = Hex("#2DE2E6");
        public static readonly Color Acid = Hex("#39FF88");
        public static readonly Color Bg = Hex("#15101C");
        public static readonly Color Ink = Hex("#FCEBFF");
        public static readonly Color Muted = Hex("#C9B3D3");
        public static readonly Color Dim = Hex("#3A2D40");
        public static readonly Color Paper = Hex("#EFE6D8");
        public static readonly Color PaperInk = Hex("#2A1D2B");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        public static Color Alpha(Color c, float a) { c.a = a; return c; }

        // ---------- Layout ----------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors the rect at a point given in 0..1 of the parent, with a fixed size.</summary>
        public static RectTransform Place(this RectTransform rt, float ax, float ay, float w, float h, float rotation = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            return rt;
        }

        public static LayoutElement Size(this Component c, float w, float h)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w;
            if (h >= 0) le.preferredHeight = h;
            return le;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            return v;
        }

        // ---------- Graphics ----------

        public static Image Img(string name, Transform parent, Sprite sprite, Color color, bool sliced = false, float cornerScale = 1f)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced && sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, cornerScale);
            }
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            // Set the font explicitly: on an inactive object TMP hasn't loaded its default yet.
            var font = TMP_Settings.defaultFontAsset;
            if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Neon look for text: a soft colored underlay around the letters.</summary>
        public static TextMeshProUGUI Neon(this TextMeshProUGUI t, Color glow, float softness = 0.6f, float dilate = 0.45f)
        {
            var m = t.fontMaterial; // per-text instance
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0);
            t.fontMaterial = m;
            return t;
        }

        /// <summary>A neon frame: soft glow + rounded dark fill + bright border, all inside <paramref name="rt"/>.</summary>
        public static Image NeonPanel(RectTransform rt, FindepairArt art, Color accent, float fillAlpha = 0.85f,
            float corner = 1f, float glowAlpha = 0.55f, float border = 1f)
        {
            var glow = Img("Glow", rt, art.glow, Alpha(accent, glowAlpha), true, corner);
            glow.rectTransform.Stretch(-40, -40, -40, -40);
            var fill = Img("Fill", rt, art.rounded, Alpha(Bg, fillAlpha), true, corner);
            fill.rectTransform.Stretch();
            var frame = Img("Frame", rt, art.frame, accent, true, corner * border);
            frame.rectTransform.Stretch();
            return frame;
        }

        // ---------- Buttons ----------

        public static Button Button(string name, Transform parent, string label, Color accent, FindepairArt art,
            Action onClick, float width = 560, float height = 140, float fontSize = 46)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(width, height);
            rt.Size(width, height);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            NeonPanel(rt, art, accent, 0.85f, 1.1f);
            var text = Text("Label", rt, label, fontSize, Ink, FontStyles.Bold).Neon(Alpha(accent, 0.9f), 0.7f, 0.3f);
            text.rectTransform.Stretch(20, 0, 20, 0);
            text.characterSpacing = 4;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<FindepairPress>();
            return b;
        }

        /// <summary>Runs <paramref name="step"/> with t from 0 to 1 over <paramref name="seconds"/> of game time.</summary>
        public static IEnumerator Animate(float seconds, Func<float> delta, Action<float> step)
        {
            for (float t = 0; t < seconds; t += delta())
            {
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
