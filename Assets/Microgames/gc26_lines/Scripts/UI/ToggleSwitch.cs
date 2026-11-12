using System;
using GC26.Lines.View;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// An on/off switch like the prototype's Settings switches: a pill with a round knob. On is gold with a
    /// cream knob on the right; off is see-through glass with a grey knob on the left. The knob slides in 0.2 s.
    /// Sizes in units (1 % of the content width).
    /// </summary>
    public sealed class ToggleSwitch : MonoBehaviour
    {
        public const float Width = 15f, Height = 8.4f, Knob = 6f, Border = 0.4f, KnobInset = 0.9f, SlideSeconds = 0.2f;
        // Off: the card's dark glass with a hint of white (solid, so the light outline doesn't show through).
        static readonly Color OffFill = Color.Lerp(Palette.Card, Color.white, 0.08f), KnobOn = Palette.Hex(0xFFF8E6);

        RectTransform rect;
        Image outline, fill, knob;
        float unit = 10f;

        public bool IsOn { get; private set; }

        /// <summary>The player flipped the switch; gives the new state.</summary>
        public event Action<bool> Toggled;

        public static ToggleSwitch Create(RectTransform parent, string name)
        {
            var go = new GameObject("Switch " + name, typeof(RectTransform));
            var s = go.AddComponent<ToggleSwitch>();
            s.Build(parent);
            return s;
        }

        void Build(RectTransform parent)
        {
            rect = (RectTransform)transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            outline = gameObject.AddComponent<Image>(); // also the tap area
            fill = NewImage("Fill");
            knob = NewImage("Knob");
            knob.sprite = SpriteFactory.Disc();
            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = outline;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Click);
            Set(false, animate: false);
        }

        /// <summary>Flips the switch as if tapped (tests too).</summary>
        public void Click()
        {
            Set(!IsOn, animate: true);
            Toggled?.Invoke(IsOn);
        }

        /// <summary>Shows <paramref name="on"/> without telling anyone (opening the pop-up).</summary>
        public void Set(bool on, bool animate)
        {
            IsOn = on;
            outline.color = on ? Palette.Gold : Palette.Line;
            fill.color = on ? Palette.Gold : OffFill;
            knob.color = on ? KnobOn : Palette.SoftText;
            Tween.Kill(knob.rectTransform);
            var target = KnobPosition(on);
            if (animate && Application.isPlaying)
            {
                var start = knob.rectTransform.anchoredPosition;
                Tween.To(knob.rectTransform, SlideSeconds, p => knob.rectTransform.anchoredPosition = Vector2.Lerp(start, target, p), Ease.OutQuad);
            }
            else knob.rectTransform.anchoredPosition = target;
        }

        /// <summary>Places the switch at <paramref name="pixels"/> (origin bottom-left of the parent).</summary>
        public void Place(Rect pixels, float u)
        {
            unit = u;
            rect.anchoredPosition = pixels.position;
            rect.sizeDelta = pixels.size;
            UiShapes.MakePill(outline, pixels.height);
            float b = Border * u;
            var f = fill.rectTransform;
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one;
            f.offsetMin = new Vector2(b, b); f.offsetMax = new Vector2(-b, -b);
            UiShapes.MakePill(fill, pixels.height - 2f * b);
            knob.rectTransform.sizeDelta = Vector2.one * Knob * u;
            knob.rectTransform.anchoredPosition = KnobPosition(IsOn);
        }

        // The knob's centre: KnobInset from the left edge when off, from the right edge when on.
        Vector2 KnobPosition(bool on)
        {
            float r = Knob / 2f * unit, inset = KnobInset * unit;
            float x = on ? rect.sizeDelta.x - inset - r : inset + r;
            return new Vector2(x, rect.sizeDelta.y / 2f);
        }

        /// <summary>The knob's centre from the switch's left edge, in pixels (tests).</summary>
        public float KnobX => knob.rectTransform.anchoredPosition.x;

        Image NewImage(string name)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(rect, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
