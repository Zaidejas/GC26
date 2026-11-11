using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048
{
    /// <summary>Small helpers that build the uGUI hierarchy from code (no prefabs to keep in sync).</summary>
    public static class CheeseUi
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors by fractions of the parent, y measured from the top.</summary>
        public static RectTransform Place(this RectTransform rt, float x0, float top0, float x1, float top1)
        {
            rt.anchorMin = new Vector2(x0, 1f - top1);
            rt.anchorMax = new Vector2(x1, 1f - top0);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt) => rt.Place(0, 0, 1, 1);

        public static Image Box(Transform parent, string name, Sprite sprite, Color color, bool shadow = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            if (shadow) AddShadow(img.gameObject, 9f);
            return img;
        }

        public static void AddShadow(GameObject go, float distance)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = CheeseArt.Ink;
            s.effectDistance = new Vector2(0, -distance);
        }

        public static Image Picture(Transform parent, string name, Sprite sprite)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string text, bool primary, CheeseArt art, Action onClick, float fontSize = 46f)
        {
            var img = Box(parent, text, art.Box, primary ? (Color)CheeseArt.Cheese : CheeseArt.Panel, true);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var label = Label(img.transform, text, fontSize, CheeseArt.Ink, TextAlignmentOptions.Center, true);
            label.rectTransform.Fill();
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        public static void SetText(this Button button, string text) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;

        public static void SetPrimary(this Button button, bool primary) =>
            button.GetComponent<Image>().color = primary ? (Color)CheeseArt.Cheese : CheeseArt.Panel;

        public static LayoutElement Size(this Component c, float preferredHeight, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup Column(GameObject go, float spacing, int padding = 0)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup RowGroup(GameObject go, float spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }
    }
}
