using System;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// L17 (GDD → Tutorials on the board): coach marks. The screen dims except one spot with a gold outline, an
    /// animated hand taps on it, and a caption explains, with Got it and/or Skip. Every tap lands here: the game
    /// decides what a tap on the lit spot does (<see cref="Tapped"/>, <see cref="Dragged"/>). Its own canvas sits
    /// above the game screen and below the pop-ups. Positions are screen pixels, origin bottom-left.
    /// </summary>
    public sealed class CoachMarks : MonoBehaviour, IPointerClickHandler, IDragHandler, IPointerDownHandler
    {
        const float HandSize = 15f, CaptionWidth = 80f, TitleSize = 5f, SubSize = 3.4f, PadX = 4.4f, PadY = 2.6f;
        static readonly Color Dim = new Color(5 / 255f, 6 / 255f, 18 / 255f, 0.74f), Gold = new Color(1f, 0.835f, 0.42f);

        Canvas canvas;
        RectTransform root, hand, caption;
        readonly Image[] dims = new Image[4];
        Image ring, ringGlow, captionBorder, captionBack, handImage;
        TextMeshProUGUI title, sub;
        PillButton gotIt, skip;
        float unit = 10f;
        Vector2 screen;
        Vector2 handPoint;
        bool handFlipped;

        /// <summary>A tap on the screen while showing (not on its buttons): the screen position.</summary>
        public event Action<Vector2> Tapped;
        /// <summary>A finger sliding on the screen while showing: the screen position (L16: moves the burst).</summary>
        public event Action<Vector2> Dragged;
        public event Action GotItClicked, SkipClicked;

        public bool IsShowing => gameObject.activeSelf;
        public string Title => title.text;
        public string Subtitle => sub.text;
        public bool GotItShowing => gotIt.gameObject.activeSelf;
        public bool SkipShowing => skip.gameObject.activeSelf;
        public PillButton GotItButton => gotIt;
        public PillButton SkipButton => skip;
        /// <summary>The lit spot, in screen pixels (tests).</summary>
        public Rect Hole { get; private set; }
        public bool HandFlipped => handFlipped;
        public Vector2 HandPoint => handPoint;

        public static CoachMarks Create(Camera camera)
        {
            var go = new GameObject("Coach marks");
            var view = go.AddComponent<CoachMarks>();
            view.Build(camera);
            go.SetActive(false);
            return view;
        }

        void Build(Camera camera)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 4.5f;
            canvas.sortingOrder = 25; // over the game screen (20), under the pop-ups (30)
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            var catcher = NewRect("Catcher", root).gameObject.AddComponent<Image>();
            catcher.color = Color.clear; // every tap lands here, the lit spot included
            catcher.rectTransform.anchorMin = Vector2.zero;
            catcher.rectTransform.anchorMax = Vector2.one;
            catcher.rectTransform.offsetMin = catcher.rectTransform.offsetMax = Vector2.zero;
            for (int i = 0; i < dims.Length; i++)
            {
                dims[i] = NewRect("Dim " + i, root).gameObject.AddComponent<Image>();
                dims[i].color = Dim;
                dims[i].raycastTarget = false;
            }
            ringGlow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            ringGlow.sprite = SpriteFactory.SoftGlow(1f);
            ringGlow.color = new Color(1f, 0.82f, 0.43f, 0.55f);
            ringGlow.raycastTarget = false;
            ring = NewRect("Outline", root).gameObject.AddComponent<Image>();
            ring.color = Gold;
            ring.raycastTarget = false;

            hand = NewRect("Hand", root);
            hand.pivot = new Vector2(26f / 64f, 1f - 6f / 64f); // the fingertip (the picture's 64-unit grid)
            handImage = hand.gameObject.AddComponent<Image>();
            handImage.sprite = SpecialArt.Hand();
            handImage.preserveAspect = true;
            handImage.raycastTarget = false;
            var shadow = hand.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);

            caption = NewRect("Caption", root);
            caption.pivot = new Vector2(0.5f, 0f);
            captionBorder = NewRect("Border", caption).gameObject.AddComponent<Image>();
            captionBorder.color = new Color(1f, 0.835f, 0.42f, 0.6f);
            captionBorder.raycastTarget = false;
            captionBack = NewRect("Background", caption).gameObject.AddComponent<Image>();
            captionBack.color = new Color(20 / 255f, 14 / 255f, 42 / 255f, 0.96f);
            captionBack.raycastTarget = false;
            title = NewText("Title", Gold);
            UiFonts.UseDisplay(title);
            sub = NewText("Text", Palette.SoftText);
            gotIt = PillButton.Create(caption, Texts.Get("gotIt"), primary: true);
            skip = PillButton.Create(caption, Texts.Get("skip"));
            gotIt.Clicked += () => GotItClicked?.Invoke();
            skip.Clicked += () => SkipClicked?.Invoke();
        }

        RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI NewText(string name, Color color)
        {
            var tmp = NewRect(name, caption).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            UiFonts.UseBody(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>The screen size and the size of 1 unit (1 % of the content width), in pixels.</summary>
        public void ApplyLayout(Vector2 screenSize, float unitPixels)
        {
            screen = screenSize;
            unit = unitPixels;
        }

        /// <summary>
        /// Lights up <paramref name="hole"/> (round for a button) with the hand tapping <paramref name="point"/>, and a
        /// caption: <paramref name="titleText"/> and <paramref name="subText"/> (may be empty), Got it and/or Skip
        /// (<paramref name="skipText"/>: its label, e.g. "End game"). <paramref name="captionAbove"/>: over the lit spot.
        /// </summary>
        public void Show(Rect hole, bool round, Vector2 point, string titleText, string subText, bool showGotIt, bool showSkip,
            string skipText = null, bool captionAbove = false)
        {
            gameObject.SetActive(true);
            float u = unit;
            float pad = (round ? 0.6f : 0.3f) * u;
            hole = new Rect(hole.x - pad, hole.y - pad, hole.width + 2f * pad, hole.height + 2f * pad);
            Hole = hole;
            // Four dim strips around the lit spot.
            Place(dims[0].rectTransform, 0f, hole.yMax, screen.x, Mathf.Max(0f, screen.y - hole.yMax));
            Place(dims[1].rectTransform, 0f, 0f, screen.x, Mathf.Max(0f, hole.y));
            Place(dims[2].rectTransform, 0f, hole.y, Mathf.Max(0f, hole.x), hole.height);
            Place(dims[3].rectTransform, hole.xMax, hole.y, Mathf.Max(0f, screen.x - hole.xMax), hole.height);
            Place(ring.rectTransform, hole.x, hole.y, hole.width, hole.height);
            float radius = round ? hole.width / 2f : 2.4f * u;
            UiShapes.MakeRing(ring, radius);
            ringGlow.enabled = false; // a glow would cover the lit spot: just the gold outline, as in the prototype

            // The hand: pointing up from below; low on the screen it points down from above, so it isn't cut off.
            handPoint = point;
            handFlipped = point.y < screen.y * 0.2f;
            float h = HandSize * u;
            hand.sizeDelta = new Vector2(h, h);
            hand.localRotation = Quaternion.Euler(0f, 0f, handFlipped ? 180f : 0f);
            hand.anchoredPosition = point;

            // The caption: title, line, buttons.
            title.text = titleText;
            sub.text = subText ?? "";
            gotIt.SetText(Texts.Get("gotIt"));
            skip.SetText(string.IsNullOrEmpty(skipText) ? Texts.Get("skip") : skipText);
            gotIt.gameObject.SetActive(showGotIt);
            skip.gameObject.SetActive(showSkip);
            title.fontSize = TitleSize * u;
            sub.fontSize = SubSize * u;
            float inner = CaptionWidth * u - 2f * PadX * u;
            float width = Mathf.Min(inner, Mathf.Max(title.GetPreferredValues(title.text).x, sub.text.Length > 0 ? sub.GetPreferredValues(sub.text).x : 0f, showGotIt ? 50f * u : 0f) + 1f);
            float titleH = title.GetPreferredValues(title.text, width, 0f).y;
            float subH = sub.text.Length > 0 ? sub.GetPreferredValues(sub.text, width, 0f).y + 0.8f * u : 0f;
            float gotH = showGotIt ? 10f * u + 3f * u : 0f;
            skip.Place(new Rect(0, 0, skip.WidthForText(u, 6f), 8.5f * u), u);
            float skipH = showSkip ? 8.5f * u + 2.6f * u : 0f;
            float boxW = width + 2f * PadX * u, boxH = PadY * u + titleH + subH + gotH + skipH + PadY * u;
            caption.sizeDelta = new Vector2(boxW, boxH);
            Place(captionBorder.rectTransform, -0.35f * u, -0.35f * u, boxW + 0.7f * u, boxH + 0.7f * u);
            Place(captionBack.rectTransform, 0f, 0f, boxW, boxH);
            UiShapes.MakeRounded(captionBorder, 3.3f * u);
            UiShapes.MakeRounded(captionBack, 3f * u);
            float y = boxH - PadY * u - titleH;
            Place(title.rectTransform, PadX * u, y, width, titleH);
            if (subH > 0f) { y -= subH; Place(sub.rectTransform, PadX * u, y, width, subH - 0.8f * u); }
            if (showGotIt) { y -= gotH; gotIt.Place(new Rect(PadX * u, y, width, 10f * u), u); }
            if (showSkip)
            {
                float w = skip.WidthForText(u, 6f);
                y -= skipH;
                skip.Place(new Rect((boxW - w) / 2f, y + 0.4f * u, w, 8.5f * u), u);
            }

            float x = Mathf.Clamp(screen.x / 2f, boxW / 2f + 2f * u, screen.x - boxW / 2f - 2f * u);
            float bottom;
            if (captionAbove) bottom = hole.yMax + 3f * u;                        // over the lit spot (the board)
            else if (point.y > screen.y * 0.45f) bottom = hole.y - h - 3f * u - boxH; // upper part: under the hand
            else bottom = (handFlipped ? point.y + h : hole.yMax) + 4f * u;      // lower part: above the hand
            caption.anchoredPosition = new Vector2(x, Mathf.Clamp(bottom, 2f * u, screen.y - boxH - 2f * u));
            caption.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            // The hand taps: from 3 units back towards the spot, a little press, and back (prototype: 1.1 s).
            float p = Time.time % 1.1f / 1.1f;
            float k = p < 0.45f ? p / 0.45f : p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
            float back = (1f - Smooth(k)) * 3f * unit;
            hand.anchoredPosition = handPoint + new Vector2(0f, handFlipped ? back : -back);
            hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, Smooth(k));
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(eventData.position);
        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.position);

        /// <summary>Taps the screen at <paramref name="screenPosition"/> (tests).</summary>
        public void TapAt(Vector2 screenPosition) => Tapped?.Invoke(screenPosition);

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
