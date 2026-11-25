using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.ScienceSnake
{
    /// <summary>Small helpers to build uGUI from code (the game has no prefabs). Styles follow the web version.</summary>
    public static class UiKit
    {
        public static readonly Color Bg = Hex("#13241a"), Panel = Hex("#eaf4ea"), Ink = Hex("#173a22"), Muted = Hex("#5d7a64"),
            G1 = Hex("#2e7d32"), G2 = Hex("#1b5e20"), G3 = Hex("#c8e6c9"), Card = Color.white, Wine = Hex("#7b1e3a"), Warn = Hex("#fdecec"),
            Soft = Hex("#eef5ee"), Line = Hex("#cfe3d0");

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        // Fonts: the web version uses Georgia for headings and the system sans for UI. Use the same system fonts when they exist.
        static Font sans, serif;
        public static Font Sans => sans != null ? sans : (sans = OsFont(new[] { "Segoe UI", "Roboto", "Helvetica Neue", "Arial" }));
        public static Font Serif => serif != null ? serif : (serif = OsFont(new[] { "Georgia", "Noto Serif", "Droid Serif", "Times New Roman", "serif" }));
        static Font OsFont(string[] names)
        {
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                foreach (var n in names)
                    if (Array.IndexOf(installed, n) >= 0) return Font.CreateDynamicFontFromOSFont(n, 32);
            }
            catch { }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchored to the top edge, full width, fixed height.</summary>
        public static RectTransform Top(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.offsetMin = new Vector2(0, -y - h); rt.offsetMax = new Vector2(0, -y);
            return rt;
        }

        public static RectTransform Center(RectTransform rt, Vector2 size, Vector2 pos = default)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        static Sprite[] rounded = new Sprite[64];
        /// <summary>9-sliced rounded rectangle with the given corner radius (UI units).</summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 60);
            if (rounded[radius] != null) return rounded[radius];
            int r2 = radius * 2, size = r2 * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, r2, size - r2), cy = Mathf.Clamp(y + .5f, r2, size - r2);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)) - r2;
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - d / 2f));
            }
            tex.SetPixels(px); tex.Apply();
            // drawn at 2× and shown at pixelsPerUnit 200, so corners stay smooth on dense screens
            return rounded[radius] = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f), 200, 0, SpriteMeshType.FullRect, new Vector4(r2 + 1, r2 + 1, r2 + 1, r2 + 1));
        }

        public static Image Image(Transform parent, string name, Color c, int radius = 0, bool raycast = true)
        {
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = raycast;
            if (radius > 0) { img.sprite = Rounded(radius); img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1; }
            return img;
        }

        public static Image Icon(Transform parent, Sprite s, Color c, float size)
        {
            var img = Rect(parent, "Icon").gameObject.AddComponent<Image>();
            img.sprite = s; img.color = c; img.preserveAspect = true; img.raycastTarget = false;
            ((RectTransform)img.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        public static Text Text(Transform parent, string name, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, bool serif = false)
        {
            var t = Rect(parent, name).gameObject.AddComponent<Text>();
            t.font = serif ? Serif : Sans; t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            return t;
        }

        /// <summary>Hard drop shadow under an element (the "3D" button look of the web version).</summary>
        public static void Drop(Graphic g, Color c, float y)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = c; s.effectDistance = new Vector2(0, -y); s.useGraphicAlpha = true;
        }

        /// <summary>Soft blurred shadow behind a rect (added as the first child, ignored by layout).</summary>
        public static Image SoftShadow(RectTransform target, float blur, float y, float alpha)
        {
            var img = Image(target, "Shadow", new Color(.06f, .2f, .1f, alpha), raycast: false);
            img.sprite = Procedural.SoftShadow; img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 44f / blur;
            var rt = img.rectTransform; Stretch(rt, -blur, -blur - y, -blur, -blur + y);
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            img.transform.SetAsFirstSibling();
            return img;
        }

        public static Button Button(Transform parent, string label, Color bg, Color fg, Action onClick, int size = 16, float minHeight = 48, int radius = 14, Color? drop = null)
        {
            var img = Image(parent, "Button " + label, bg, radius);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.pressedColor = new Color(.88f, .88f, .88f); colors.highlightedColor = new Color(.97f, .97f, .97f); colors.fadeDuration = .06f; b.colors = colors;
            b.onClick.AddListener(() => onClick?.Invoke());
            if (drop.HasValue) Drop(img, drop.Value, 3);
            var t = Text(img.transform, "Label", size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 10, 4, 10, 4);
            t.text = label;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = minHeight; le.preferredHeight = minHeight;
            img.gameObject.AddComponent<PressButton>().Sink = 2;
            return b;
        }

        public static void SetLabel(Button b, string label) => b.transform.Find("Label").GetComponent<Text>().text = label;

        public static VerticalLayoutGroup Column(GameObject go, float spacing, RectOffset pad)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = pad;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Row(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flex = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flex >= 0) le.flexibleWidth = flex;
            return le;
        }
    }

    /// <summary>Fires on press (not release), so the on-screen arrows react instantly; also sinks the element while it is held, like a real key.</summary>
    public class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action OnPress;
        public float Sink = 3;
        RectTransform content; Shadow shadow; float shadowY; bool down;

        void Awake()
        {
            shadow = GetComponent<Shadow>();
            if (shadow != null) shadowY = shadow.effectDistance.y;
        }
        // only the children (label, glyph) move, so layout groups never fight with the press animation
        void Visual(bool pressed)
        {
            down = pressed;
            foreach (RectTransform ch in transform) if (ch.name != "Shadow") ch.anchoredPosition += new Vector2(0, pressed ? -Sink : Sink);
            if (shadow != null) shadow.effectDistance = new Vector2(0, pressed ? shadowY * .3f : shadowY);
        }
        public void OnPointerDown(PointerEventData e) { if (!down) Visual(true); OnPress?.Invoke(); }
        public void OnPointerUp(PointerEventData e) { if (down) Visual(false); }
        public void OnPointerExit(PointerEventData e) { if (down) Visual(false); }
        void OnDisable() { if (down) Visual(false); }

        /// <summary>Briefly press the key from code (keyboard input), so the on-screen arrow shows it.</summary>
        public void Flash() { if (!down && isActiveAndEnabled) StartCoroutine(FlashCo()); }
        System.Collections.IEnumerator FlashCo() { Visual(true); yield return new WaitForSecondsRealtime(.12f); if (down) Visual(false); }
    }
}
