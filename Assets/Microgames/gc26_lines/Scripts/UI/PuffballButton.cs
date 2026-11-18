using System;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// L16 (GDD → Puffball bomb): the round button in the middle of the bottom row. A gold ring fills towards the
    /// next puffball (full while the pocket is full); the puffball picture is dim until one is ready, then it glows,
    /// its fuse sparkles and a gold number shows how many are ready. While aiming it pulses gold.
    /// Sizes follow the prototype, in units of 1 % of the content width.
    /// </summary>
    public sealed class PuffballButton : MonoBehaviour, IPointerClickHandler
    {
        public const float Size = 14f, RingWidth = 0.7f, BadgeSize = 5f, BadgeText = 3.2f;
        static readonly Color Gold = new Color(1f, 0.835f, 0.42f), RingRest = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color Core = new Color(28 / 255f, 20 / 255f, 58 / 255f, 0.92f);

        RectTransform root;
        Image glow, ringBack, ring, core, icon, spark, badge;
        TextMeshProUGUI count;
        float unit = 10f;
        TweenHandle pulse;

        /// <summary>The player tapped the button.</summary>
        public event Action Clicked;
        public int Count { get; private set; }
        public float Progress => ring.fillAmount;
        public bool Armed { get; private set; }
        public bool BadgeShowing => badge.gameObject.activeSelf;
        public RectTransform Rect => root;

        public static PuffballButton Create(RectTransform parent)
        {
            var go = new GameObject("Puffball", typeof(RectTransform));
            var view = go.AddComponent<PuffballButton>();
            view.Build(parent);
            return view;
        }

        void Build(RectTransform parent)
        {
            root = (RectTransform)transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            var hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear; // catches taps on the whole button
            glow = Child("Glow", SpriteFactory.SoftDisc(), Gold);
            ringBack = Child("Ring", SpriteFactory.Disc(), RingRest);
            ring = Child("Progress", SpriteFactory.Disc(), Gold);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            core = Child("Core", SpriteFactory.Disc(), Core);
            icon = Child("Icon", SpecialArt.Puffball(), Color.white);
            icon.preserveAspect = true;
            spark = Child("Spark", SpecialArt.PuffballSpark(), Color.white);
            spark.preserveAspect = true;
            badge = Child("Badge", SpriteFactory.Disc(), new Color(1f, 0.75f, 0.35f));
            count = new GameObject("Count", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            count.rectTransform.SetParent(badge.rectTransform, false);
            count.rectTransform.anchorMin = Vector2.zero;
            count.rectTransform.anchorMax = Vector2.one;
            count.rectTransform.offsetMin = count.rectTransform.offsetMax = Vector2.zero;
            UiFonts.UseBody(count);
            count.fontStyle = FontStyles.Bold;
            count.color = new Color(0.23f, 0.13f, 0.03f);
            count.alignment = TextAlignmentOptions.Center;
            count.raycastTarget = false;
            Set(0, 0f);
        }

        Image Child(string name, Sprite sprite, Color color)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Puts the button's middle at <paramref name="centre"/> (pixels) for a screen where 1 unit is <paramref name="unitPixels"/>.</summary>
        public void Place(Vector2 centre, float unitPixels)
        {
            unit = unitPixels;
            float d = Size * unit;
            root.anchoredPosition = centre;
            root.sizeDelta = new Vector2(d, d);
            SetSize(glow, d * 1.7f);
            SetSize(ringBack, d);
            SetSize(ring, d);
            SetSize(core, d - 2f * RingWidth * unit);
            // Drawn at their real size, so the ring's edges stay sharp on phones.
            UiShapes.MakeDisc(ringBack, d);
            UiShapes.MakeDisc(ring, d);
            UiShapes.MakeDisc(core, d - 2f * RingWidth * unit);
            SetSize(icon, d * 0.72f);
            SetSize(spark, d * 0.72f);
            float b = BadgeSize * unit;
            badge.rectTransform.sizeDelta = new Vector2(b, b);
            UiShapes.MakeDisc(badge, b);
            badge.rectTransform.anchoredPosition = new Vector2(d / 2f - b / 2f + 0.8f * unit, d / 2f - b / 2f + 0.8f * unit);
            count.fontSize = BadgeText * unit;
        }

        static void SetSize(Image image, float size) => image.rectTransform.sizeDelta = new Vector2(size, size);

        /// <summary>Shows <paramref name="ready"/> puffballs and the ring's <paramref name="progress"/> (0–1).</summary>
        public void Set(int ready, float progress)
        {
            Count = ready;
            ring.fillAmount = Mathf.Clamp01(progress);
            bool have = ready > 0;
            icon.color = have ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.4f); // dim until one is ready
            spark.enabled = have;
            badge.gameObject.SetActive(have); // the gold number goes with its badge
            count.text = ready.ToString();
            if (!Armed) glow.color = new Color(Gold.r, Gold.g, Gold.b, have ? 0.35f : 0f);
        }

        Canvas badgeCanvas;

        /// <summary>
        /// While a tutorial lights up this button, its count badge sits partly outside the lit circle: drawn on its
        /// own layer above the tutorial (25) but under the pop-ups (30), it isn't covered by the gold outline.
        /// </summary>
        public void SetBadgeOnTop(bool onTop)
        {
            if (onTop && badgeCanvas == null) badgeCanvas = badge.gameObject.AddComponent<Canvas>();
            if (badgeCanvas == null) return;
            badgeCanvas.overrideSorting = onTop;
            badgeCanvas.sortingOrder = onTop ? 26 : 0;
        }

        public bool BadgeOnTop => badgeCanvas != null && badgeCanvas.overrideSorting;

        /// <summary>Aiming: the button pulses gold (or stops pulsing).</summary>
        public void SetArmed(bool armed)
        {
            Armed = armed;
            pulse?.Kill();
            root.localScale = Vector3.one;
            if (!armed) { Set(Count, ring.fillAmount); return; }
            pulse = Tween.To(this, 1.6f, p =>
            {
                float wave = 0.5f - 0.5f * Mathf.Cos(p * Mathf.PI * 4f); // two pulses a loop (prototype: 0.8 s each way)
                glow.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.45f, 1f, wave));
                root.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, wave);
            });
            pulse.OnComplete(() => { if (Armed) SetArmed(true); });
        }

        /// <summary>A puffball earned: the button pops.</summary>
        public void Pop()
        {
            if (Armed) return;
            Tween.Kill(root);
            Tween.To(root, 0.6f, p => root.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, p), Ease.OutBack)
                .OnKilled = () => { if (root != null) root.localScale = Vector3.one; };
        }

        void Update()
        {
            // The fuse sparkles while a puffball is ready (prototype: 0.3 s each way).
            if (!spark.enabled) return;
            float wave = Mathf.PingPong(Time.time / 0.3f, 1f);
            spark.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, wave));
            spark.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.06f, wave);
        }

        public void OnPointerClick(PointerEventData eventData) => Click();

        /// <summary>Presses the button (also used by tests).</summary>
        public void Click() => Clicked?.Invoke();
    }
}
