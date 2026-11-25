using System;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// A rounded button like the prototype's forest look, coloured by its job (GDD → Art → Colours): dark glass
    /// with a silver-lilac outline for ordinary buttons, glowing gold with dark text for the one main action,
    /// ember red for starting over (<see cref="SetDanger"/>). Disabled buttons fade to 40 %; a disabled main
    /// button turns into plain dark glass instead. Can also show an icon (the back arrow, the house and gear).
    /// Labels use Nunito ExtraBold. Built from code; sizes in units (1 % of the content width).
    /// </summary>
    public sealed class PillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const float BorderWidth = 0.4f, FontSize = 4.4f, GlowBlur = 5f;
        /// <summary>The label's size in units (4.4; the Debug panel's small buttons use less).</summary>
        public float TextUnits { get; set; } = FontSize; // the prototype's box-shadow: 0 0 5cqw

        RectTransform rect;
        Image hitArea, glow, border, fill;
        TextMeshProUGUI label;
        Button button;
        CanvasGroup group;
        Image icon;
        bool primary, danger;
        /// <summary>The navigation icons' sizes in units (prototype: the house 7, the gear 7.6; tap area 12).</summary>
        public const float HouseIconSize = 7f, GearIconSize = 7.6f;
        float iconSize = IconSize;
        const float IconSize = 5.4f, LeadingIconSize = 4.6f, LeadingIconGap = 1.6f;
        Image leadingIcon;

        public event Action Clicked;
        public string Text => label.text;
        /// <summary>The label itself (the menu places the stopwatch next to Continue's time).</summary>
        public TextMeshProUGUI Label => label;

        public bool Interactable
        {
            get => button.interactable;
            set
            {
                button.interactable = value;
                Paint();
            }
        }

        public static PillButton Create(RectTransform parent, string text, bool primary = false)
        {
            var go = new GameObject("Button " + text, typeof(RectTransform));
            var b = go.AddComponent<PillButton>();
            b.Build(parent, text, primary);
            return b;
        }

        /// <summary>A round button showing <paramref name="iconSprite"/> instead of text (e.g. the back arrow).</summary>
        public static PillButton CreateIcon(RectTransform parent, Sprite iconSprite, string name)
        {
            var b = Create(parent, "");
            b.gameObject.name = "Button " + name;
            b.icon = b.Child("Icon").gameObject.AddComponent<Image>();
            b.icon.sprite = iconSprite;
            b.icon.color = Palette.Text; // light cream on dark glass (the house and gear: blue-grey, see MakeNav)
            b.icon.preserveAspect = true;
            b.icon.raycastTarget = false;
            var r = b.icon.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return b;
        }

        /// <summary>
        /// Shows only the label or icon: no outline ring and no glass disc behind it (the game screen's house
        /// button). The tap area stays the same size.
        /// </summary>
        public void MakePlain()
        {
            border.enabled = false;
            fill.enabled = false;
            glow.enabled = false;
        }

        /// <summary>
        /// The house or gear: just the icon, <paramref name="iconUnits"/> big, in the quiet blue-grey of navigation
        /// (no ring, disc or glow), so it never looks like part of the game.
        /// </summary>
        public void MakeNav(float iconUnits)
        {
            MakePlain();
            iconSize = iconUnits;
            if (icon != null) icon.color = Palette.Nav;
            if (lastUnit > 0f && icon != null) icon.rectTransform.sizeDelta = Vector2.one * iconSize * lastUnit;
        }

        /// <summary>The icon's colour (tests).</summary>
        public Color IconColor => icon != null ? icon.color : Color.clear;
        /// <summary>The icon's size in pixels (tests).</summary>
        public float IconPixels => icon != null ? icon.rectTransform.sizeDelta.x : 0f;

        /// <summary>Makes this the gold main button or an ordinary one (the menu: New game is gold when there's nothing to continue).</summary>
        public void SetPrimary(bool on)
        {
            if (primary == on) return;
            primary = on;
            Paint();
        }

        public bool IsPrimary => primary;

        /// <summary>
        /// Ember red for starting over: <paramref name="filled"/> false is Reset (red outline and text, a step more
        /// serious than Undo or Pause); true is the question's Yes (a filled red button, since the game is lost).
        /// <paramref name="on"/> false makes it an ordinary (gold or glass) button again.
        /// </summary>
        public void SetDanger(bool filled, bool on = true)
        {
            danger = on;
            if (on) primary = filled;
            Paint();
            if (lastUnit > 0f) Place(new Rect(rect.anchoredPosition - rect.sizeDelta / 2f, rect.sizeDelta), lastUnit);
        }

        public bool IsDanger => danger;
        public Color LabelColor => label.color;
        public Color OutlineColor => border.color;

        // Colours by job: gold main button, ember (outline or filled), or ordinary silver-lilac glass.
        void Paint()
        {
            bool on = button == null || button.interactable;
            Color line, body, ink;
            if (primary && !on) { line = Palette.DisabledRim; body = Palette.DisabledFill; ink = Palette.SoftText; } // plain dark glass, not faded
            else if (primary && danger) { line = Palette.EmberText; body = Palette.Ember; ink = Palette.EmberDarkText; }
            else if (primary) { line = Palette.Gold; body = Palette.Gold; ink = Palette.GoldText; }
            else if (danger) { line = Palette.EmberLine; body = Palette.Glass; ink = Palette.EmberText; }
            else { line = Palette.ControlLine; body = Palette.Glass; ink = Palette.ControlText; }
            if (!IsPlain) { border.color = line; fill.color = body; }
            label.color = ink;
            if (leadingIcon != null) leadingIcon.color = ink;
            var g = danger ? Palette.EmberGlow : Palette.GoldGlow;
            glow.color = new Color(g.r, g.g, g.b, 1f); // its strength is in the picture
            glow.enabled = primary && on && !IsPlain; // only the main button glows (GDD → glow means "look here")
            group.alpha = primary || on ? 1f : 0.4f; // prototype: disabled ordinary buttons at 40 % opacity
        }

        /// <summary>True when the button shows no outline and no fill (see <see cref="MakePlain"/>).</summary>
        public bool IsPlain => !border.enabled && !fill.enabled;

        public bool HasIcon => icon != null;
        /// <summary>The icon's picture, or null for a text button.</summary>
        public Sprite IconSprite => icon != null ? icon.sprite : null;

        /// <summary>Changes an icon button's picture (L19: How to play's house becomes a back arrow from the game).</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon != null) icon.sprite = sprite;
        }
        public bool HasLeadingIcon => leadingIcon != null;

        /// <summary>Shows <paramref name="iconSprite"/> in front of the label, like the prototype's Undo and Reset.</summary>
        public void SetLeadingIcon(Sprite iconSprite)
        {
            if (leadingIcon == null)
            {
                leadingIcon = Child("Leading icon").gameObject.AddComponent<Image>();
                leadingIcon.raycastTarget = false;
                leadingIcon.preserveAspect = true;
                var r = leadingIcon.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            }
            leadingIcon.sprite = iconSprite;
            leadingIcon.color = label.color;
            if (lastUnit > 0f) PlaceLeadingIcon(lastUnit);
        }

        float lastUnit;

        // The icon and the word sit side by side, centred together in the button.
        void PlaceLeadingIcon(float unit)
        {
            float icon = LeadingIconSize * unit, gap = LeadingIconGap * unit;
            float textWidth = label.GetPreferredValues(label.text).x;
            float total = icon + gap + textWidth;
            leadingIcon.rectTransform.sizeDelta = Vector2.one * icon;
            leadingIcon.rectTransform.anchoredPosition = new Vector2(-total / 2f + icon / 2f, 0f);
            label.rectTransform.offsetMin = new Vector2(icon + gap, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
        }

        void Build(RectTransform parent, string text, bool primary)
        {
            this.primary = primary;
            rect = (RectTransform)transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f); // presses shrink it towards its middle
            group = gameObject.AddComponent<CanvasGroup>();

            // An invisible image catches taps; the visible parts are children: glow, outline, fill, label.
            hitArea = gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            glow = Child("Glow").gameObject.AddComponent<Image>();
            glow.raycastTarget = false;

            border = Child("Outline").gameObject.AddComponent<Image>();
            border.raycastTarget = false;

            fill = Child("Fill").gameObject.AddComponent<Image>();
            fill.raycastTarget = false;

            label = Child("Label").gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            UiFonts.UseBold(label);
            label.characterSpacing = 1f; // the prototype's 0.01em
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            button = gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None; // the 40 % fade shows the disabled state
            button.onClick.AddListener(() => Clicked?.Invoke());
            Paint();
        }

        /// <summary>Places the button at <paramref name="pixels"/> (origin bottom-left of the parent).</summary>
        public void Place(Rect pixels, float unit)
        {
            rect.anchoredPosition = pixels.center;
            rect.sizeDelta = pixels.size;
            UiShapes.MakePill(border, pixels.height);
            float reach = UiShapes.MakePillGlow(glow, pixels.height, GlowBlur * unit, (danger ? Palette.EmberGlow : Palette.GoldGlow).a);
            glow.rectTransform.offsetMin = new Vector2(-reach, -reach);
            glow.rectTransform.offsetMax = new Vector2(reach, reach);

            var inset = BorderWidth * unit;
            var fillRect = fill.rectTransform;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            UiShapes.MakePill(fill, pixels.height - 2f * inset);

            label.fontSize = TextUnits * unit;
            if (icon != null) icon.rectTransform.sizeDelta = Vector2.one * iconSize * unit;
            lastUnit = unit;
            if (leadingIcon != null) PlaceLeadingIcon(unit);
        }

        // Pressing shrinks the button a little; letting go springs it back (GDD → buttons feel smooth).
        public const float PressedScale = 0.95f, PressSeconds = 0.08f, ReleaseSeconds = 0.16f;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            Tween.Kill(rect);
            Tween.Scale(rect, Vector3.one * PressedScale, PressSeconds);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Tween.Kill(rect);
            Tween.Scale(rect, Vector3.one, ReleaseSeconds, Ease.OutBack);
        }

        /// <summary>
        /// How wide the button must be for its word at <paramref name="unit"/> pixels per unit, with
        /// <paramref name="padding"/> units each side (the prototype's buttons that fit their text, like Save).
        /// </summary>
        public float WidthForText(float unit, float padding)
        {
            label.fontSize = TextUnits * unit;
            return label.GetPreferredValues(label.text).x + 2f * padding * unit;
        }

        public void SetText(string text)
        {
            label.text = text;
            gameObject.name = "Button " + text;
        }

        /// <summary>Presses the button from code (tests); does nothing when it's disabled.</summary>
        public void Click()
        {
            if (Interactable) button.onClick.Invoke();
        }

        RectTransform Child(string name)
        {
            var child = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(rect, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = child.offsetMax = Vector2.zero;
            return child;
        }
    }

    /// <summary>Shared shapes for UI images.</summary>
    public static class UiShapes
    {
        /// <summary>
        /// Makes <paramref name="image"/> a soft glow that fades out over about <paramref name="fadePixels"/>
        /// at its edges; place it <paramref name="fadePixels"/> larger than the shape it lights up.
        /// </summary>
        public static void MakeGlow(Image image, float fadePixels)
        {
            image.sprite = SpriteFactory.SoftGlow(1f);
            image.type = Image.Type.Sliced;
            var canvas = image.GetComponentInParent<Canvas>();
            float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float edge = Mathf.Max(1f, fadePixels / SpriteFactory.SoftGlowFadeShare); // fade plus rounded corner
            image.pixelsPerUnitMultiplier = image.sprite.border.x * canvasPpu / (image.sprite.pixelsPerUnit * edge);
        }

        /// <summary>Makes <paramref name="image"/> a rounded rectangle with corners <paramref name="radiusPixels"/> round.</summary>
        public static void MakeRounded(Image image, float radiusPixels)
        {
            // Drawn at its real size and shown pixel for pixel, so the edges stay sharp (see SpriteFactory.ExactRoundRect).
            image.sprite = SpriteFactory.ExactRoundRect(Mathf.Max(0.5f, radiusPixels), CanvasPpu(image));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>
        /// Makes <paramref name="image"/> a rounded outline with corners <paramref name="radiusPixels"/> round;
        /// the line is 3/16 of the corner radius thick (the prototype's 0.5u outline on a 3u corner).
        /// </summary>
        public static void MakeRing(Image image, float radiusPixels)
        {
            float radius = Mathf.Max(0.5f, radiusPixels);
            image.sprite = SpriteFactory.ExactRoundRing(radius, radius * 3f / 16f, CanvasPpu(image));
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>
        /// Makes <paramref name="image"/> the glow around a pill <paramref name="heightPixels"/> tall, like CSS's
        /// <c>box-shadow: 0 0 blur</c> with alpha <paramref name="strength"/>. Returns how far the image must reach
        /// past the pill on each side.
        /// </summary>
        public static float MakePillGlow(Image image, float heightPixels, float blurPixels, float strength)
        {
            image.sprite = SpriteFactory.ExactGlow(Mathf.Max(1f, heightPixels) / 2f, blurPixels, strength, CanvasPpu(image), out float reach);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            return reach;
        }

        /// <summary>
        /// Makes <paramref name="image"/> a disc <paramref name="diameterPixels"/> across, drawn at that size so its
        /// edge stays sharp. Keeps the image's type, so a Filled (radial) image still fills.
        /// </summary>
        public static void MakeDisc(Image image, float diameterPixels)
        {
            image.sprite = SpriteFactory.ExactRoundRect(Mathf.Max(0.5f, diameterPixels / 2f), CanvasPpu(image));
            if (image.type == Image.Type.Sliced) image.type = Image.Type.Simple;
        }

        /// <summary>Makes <paramref name="image"/> a circle outline <paramref name="diameterPixels"/> across with a <paramref name="linePixels"/> line.</summary>
        public static void MakeCircleRing(Image image, float diameterPixels, float linePixels)
        {
            image.sprite = SpriteFactory.ExactRoundRing(Mathf.Max(0.5f, diameterPixels / 2f), linePixels, CanvasPpu(image));
            image.type = Image.Type.Simple;
        }

        /// <summary>Makes <paramref name="image"/> a pill: a rounded rectangle whose ends are half circles.</summary>
        public static void MakePill(Image image, float heightPixels) => MakeRounded(image, Mathf.Max(1f, heightPixels) / 2f);

        static float CanvasPpu(Image image)
        {
            var canvas = image.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.referencePixelsPerUnit : 100f;
        }
    }
}
