using System;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// A rounded button like the prototype's: white with a thin border and dark bold text
    /// (or dark with white text for the main action). Disabled buttons fade to 40 %; a disabled main
    /// button turns light grey instead (as the prototype's Continue). Can also show an icon (the back arrow).
    /// Built from code; sizes are given in units (1 % of the content width).
    /// </summary>
    public sealed class PillButton : MonoBehaviour
    {
        const float BorderWidth = 0.4f, FontSize = 4.6f;

        RectTransform rect;
        Image border, fill;
        TextMeshProUGUI label;
        Button button;
        CanvasGroup group;
        Image icon;
        bool primary;
        const float IconSize = 5.4f;

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
                    // Prototype: a disabled main button is light grey with soft text, not faded.
                    border.color = value ? Palette.Text : Palette.Line;
                    fill.color = value ? Palette.Text : Palette.Hex(0xEBE5DA);
                    label.color = value ? Color.white : Palette.SoftText;
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
            b.icon.color = Palette.Text;
            b.icon.preserveAspect = true;
            b.icon.raycastTarget = false;
            var r = b.icon.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return b;
        }

        public bool HasIcon => icon != null;

        void Build(RectTransform parent, string text, bool primary)
        {
            this.primary = primary;
            rect = (RectTransform)transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();

            border = gameObject.AddComponent<Image>();
            border.color = primary ? Palette.Text : Palette.Line;

            fill = Child("Fill").gameObject.AddComponent<Image>();
            fill.color = primary ? Palette.Text : Color.white;
            fill.raycastTarget = false;

            label = Child("Label").gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = primary ? Color.white : Palette.Text;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            button = gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            button.transition = Selectable.Transition.None; // the 40 % fade shows the disabled state
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        /// <summary>Places the button at <paramref name="pixels"/> (origin bottom-left of the parent).</summary>
        public void Place(Rect pixels, float unit)
        {
            rect.anchoredPosition = pixels.position;
            rect.sizeDelta = pixels.size;
            UiShapes.MakePill(border, pixels.height);

            var inset = BorderWidth * unit;
            var fillRect = fill.rectTransform;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            UiShapes.MakePill(fill, pixels.height - 2f * inset);

            label.fontSize = FontSize * unit;
            if (icon != null) icon.rectTransform.sizeDelta = Vector2.one * IconSize * unit;
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
