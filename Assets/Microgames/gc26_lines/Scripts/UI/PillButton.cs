using System;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// A rounded button like the prototype's forest look: dark glass with a warm outline and light text,
    /// or glowing gold with dark text for the main action. Disabled buttons fade to 40 %; a disabled main
    /// button turns into plain dark glass instead (as the prototype's Continue). Can also show an icon (the
    /// back arrow). Labels use the Mystery Quest font. Built from code; sizes in units (1 % of the content width).
    /// </summary>
    public sealed class PillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const float BorderWidth = 0.4f, FontSize = 5f, GlowSize = 3f;

        RectTransform rect;
        Image hitArea, glow, border, fill;
        TextMeshProUGUI label;
        Button button;
        CanvasGroup group;
        Image icon;
        bool primary;
        const float IconSize = 5.4f, LeadingIconSize = 4.6f, LeadingIconGap = 1.6f;
        Image leadingIcon;

        public event Action Clicked;
        public string Text => label.text;

        public bool Interactable
        {
            get => button.interactable;
            set
            {
                button.interactable = value;
                if (primary)
                {
                    // Prototype: a disabled main button is plain dark glass with soft text, not faded.
                    border.color = value ? Palette.Gold : Palette.DisabledRim;
                    fill.color = value ? Palette.Gold : Palette.DisabledFill;
                    label.color = value ? Palette.GoldText : Palette.SoftText;
                    glow.enabled = value;
                    group.alpha = 1f;
                }
                else
                {
                    group.alpha = value ? 1f : 0.4f; // prototype: disabled buttons at 40 % opacity
                }
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
            b.icon.color = Palette.Text; // light cream on dark glass
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

        /// <summary>True when the button shows no outline and no fill (see <see cref="MakePlain"/>).</summary>
        public bool IsPlain => !border.enabled && !fill.enabled;

        public bool HasIcon => icon != null;
        /// <summary>The icon's picture, or null for a text button.</summary>
        public Sprite IconSprite => icon != null ? icon.sprite : null;
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
            glow.color = Palette.GoldGlow;
            glow.enabled = primary; // only the gold main buttons glow; the glass ones stay calm
            glow.raycastTarget = false;

            border = Child("Outline").gameObject.AddComponent<Image>();
            border.color = primary ? Palette.Gold : Palette.Line;
            border.raycastTarget = false;

            fill = Child("Fill").gameObject.AddComponent<Image>();
            fill.color = primary ? Palette.Gold : Palette.Glass;
            fill.raycastTarget = false;

            label = Child("Label").gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = primary ? Palette.GoldText : Palette.Text;
            UiFonts.UseDisplay(label);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            button = gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None; // the 40 % fade shows the disabled state
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        /// <summary>Places the button at <paramref name="pixels"/> (origin bottom-left of the parent).</summary>
        public void Place(Rect pixels, float unit)
        {
            rect.anchoredPosition = pixels.center;
            rect.sizeDelta = pixels.size;
            UiShapes.MakePill(border, pixels.height);
            float glowSize = GlowSize * unit;
            glow.rectTransform.offsetMin = new Vector2(-glowSize, -glowSize);
            glow.rectTransform.offsetMax = new Vector2(glowSize, glowSize);
            UiShapes.MakeGlow(glow, glowSize);

            var inset = BorderWidth * unit;
            var fillRect = fill.rectTransform;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            UiShapes.MakePill(fill, pixels.height - 2f * inset);

            label.fontSize = FontSize * unit;
            if (icon != null) icon.rectTransform.sizeDelta = Vector2.one * IconSize * unit;
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
            label.fontSize = FontSize * unit;
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
            image.sprite = SpriteFactory.RoundRect(0.5f);
            image.type = Image.Type.Sliced;
            var canvas = image.GetComponentInParent<Canvas>();
            float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            image.pixelsPerUnitMultiplier = image.sprite.border.x * canvasPpu / (image.sprite.pixelsPerUnit * Mathf.Max(0.5f, radiusPixels));
        }

        /// <summary>
        /// Makes <paramref name="image"/> a rounded outline with corners <paramref name="radiusPixels"/> round;
        /// the line is 3/16 of the corner radius thick (the prototype's 0.5u outline on a 3u corner).
        /// </summary>
        public static void MakeRing(Image image, float radiusPixels)
        {
            image.sprite = SpriteFactory.RoundRectRing();
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            var canvas = image.GetComponentInParent<Canvas>();
            float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            image.pixelsPerUnitMultiplier = image.sprite.border.x * canvasPpu / (image.sprite.pixelsPerUnit * Mathf.Max(0.5f, radiusPixels));
        }

        /// <summary>Makes <paramref name="image"/> a pill: a rounded rectangle whose ends are half circles.</summary>
        public static void MakePill(Image image, float heightPixels)
        {
            image.sprite = SpriteFactory.RoundRect(0.5f);
            image.type = Image.Type.Sliced;
            // A sliced UI image draws its corners border / (sprite ppu / canvas ppu × multiplier) pixels wide.
            var canvas = image.GetComponentInParent<Canvas>();
            float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float border = image.sprite.border.x;
            image.pixelsPerUnitMultiplier = border * canvasPpu / (image.sprite.pixelsPerUnit * Mathf.Max(1f, heightPixels) / 2f);
        }
    }
}
