using System;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// L09 (GDD → Timed mode): the Classic | Timed choice — two round badges, a mushroom and a stopwatch, each with
    /// its name; the chosen one glows gold. On the menu the names sit under the badges (above Continue); on the
    /// Leaderboard they sit beside smaller badges and work as the tabs.
    /// </summary>
    public sealed class ModeBadges : MonoBehaviour
    {
        sealed class Badge
        {
            public RectTransform Root;
            public Button Button;
            public Image Glow, Disc, Ring, Icon;
            public TextMeshProUGUI Label;
        }

        static readonly Color Gold = new Color(1f, 0.835f, 0.42f);
        static readonly Color DiscIdle = new Color(1f, 1f, 1f, 0.05f), RingIdle = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color DiscChosen = new Color(1f, 0.835f, 0.42f, 0.12f), GlowChosen = new Color(1f, 0.78f, 0.35f, 0.45f);

        readonly Badge[] badges = new Badge[2]; // 0: Classic, 1: Timed
        bool sideLabels;

        /// <summary>A badge was tapped: true for Timed.</summary>
        public event Action<bool> Chosen;
        /// <summary>True when Timed is the chosen one.</summary>
        public bool TimedChosen { get; private set; }
        public Button ClassicButton => badges[0].Button;
        public Button TimedButton => badges[1].Button;

        /// <summary>
        /// Two badges inside <paramref name="parent"/>. <paramref name="sideLabels"/>: the names beside the badges
        /// (the Leaderboard's tabs) instead of under them (the menu).
        /// </summary>
        public static ModeBadges Create(RectTransform parent, bool sideLabels)
        {
            var go = new GameObject("Mode badges", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var view = go.AddComponent<ModeBadges>();
            view.sideLabels = sideLabels;
            view.badges[0] = view.NewBadge("Classic", SpriteFactory.MushroomIcon(), false);
            view.badges[1] = view.NewBadge("Timed", SpriteFactory.StopwatchIcon(), true);
            view.RefreshTexts();
            view.SetChosen(false);
            return view;
        }

        Badge NewBadge(string name, Sprite icon, bool timed)
        {
            var b = new Badge { Root = NewRect(name, (RectTransform)transform) };
            var hit = b.Root.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // catches taps on the badge and its name
            b.Glow = NewRect("Glow", b.Root).gameObject.AddComponent<Image>();
            b.Glow.sprite = SpriteFactory.SoftDisc();
            b.Disc = NewRect("Disc", b.Root).gameObject.AddComponent<Image>();
            b.Disc.sprite = SpriteFactory.Disc();
            b.Ring = NewRect("Ring", b.Root).gameObject.AddComponent<Image>();
            b.Ring.sprite = SpriteFactory.CircleRing(0.067f);
            b.Icon = NewRect("Icon", b.Root).gameObject.AddComponent<Image>();
            b.Icon.sprite = icon;
            b.Icon.preserveAspect = true;
            b.Label = NewRect("Label", b.Root).gameObject.AddComponent<TextMeshProUGUI>();
            UiFonts.UseDisplay(b.Label);
            b.Label.textWrappingMode = TextWrappingModes.NoWrap;
            b.Label.alignment = sideLabels ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Top;
            foreach (var g in new Graphic[] { b.Glow, b.Disc, b.Ring, b.Icon, b.Label }) g.raycastTarget = false;
            b.Button = b.Root.gameObject.AddComponent<Button>();
            b.Button.targetGraphic = hit;
            b.Button.transition = Selectable.Transition.None;
            b.Button.onClick.AddListener(() => { if (TimedChosen != timed) Chosen?.Invoke(timed); });
            return b;
        }

        /// <summary>Puts the names in the current language.</summary>
        public void RefreshTexts()
        {
            badges[0].Label.text = Texts.Get("classic");
            badges[1].Label.text = Texts.Get("timed");
        }

        /// <summary>Marks the chosen mode (gold disc, ring, icon and name, with a soft glow).</summary>
        public void SetChosen(bool timed)
        {
            TimedChosen = timed;
            for (int i = 0; i < 2; i++)
            {
                var b = badges[i];
                bool on = (i == 1) == timed;
                b.Glow.enabled = on;
                b.Glow.color = GlowChosen;
                b.Disc.color = on ? DiscChosen : DiscIdle;
                b.Ring.color = on ? Gold : RingIdle;
                b.Icon.color = on ? Gold : Palette.SoftText;
                b.Label.color = on ? Gold : Palette.SoftText;
            }
        }

        /// <summary>
        /// Centres the two badges at <paramref name="centre"/> (pixels, parent's bottom-left origin): discs
        /// <paramref name="disc"/> units across, names <paramref name="text"/> units tall, <paramref name="gap"/> apart.
        /// </summary>
        public void Place(Vector2 centre, float unit, float disc, float text, float gap)
        {
            float d = disc * unit, labelH = text * 1.3f * unit;
            for (int i = 0; i < 2; i++) badges[i].Label.fontSize = text * unit;
            float[] widths = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float labelW = badges[i].Label.GetPreferredValues(badges[i].Label.text).x;
                widths[i] = sideLabels ? d + 1.6f * unit + labelW : Mathf.Max(d, labelW);
            }
            float total = widths[0] + widths[1] + gap * unit;
            float x = centre.x - total / 2f;
            float height = sideLabels ? d : d + 1.2f * unit + labelH;
            for (int i = 0; i < 2; i++)
            {
                var b = badges[i];
                Place(b.Root, x, centre.y - height / 2f, widths[i], height);
                float discX = sideLabels ? 0f : (widths[i] - d) / 2f, discY = height - d;
                Place((RectTransform)b.Disc.transform, discX, discY, d, d);
                Place((RectTransform)b.Ring.transform, discX, discY, d, d);
                b.Ring.sprite = SpriteFactory.CircleRing(0.4f / (disc / 2f)); // a 0.4-unit line, as in the prototype
                Place((RectTransform)b.Glow.transform, discX - d * 0.4f, discY - d * 0.4f, d * 1.8f, d * 1.8f);
                Place((RectTransform)b.Icon.transform, discX + d * 0.23f, discY + d * 0.23f, d * 0.54f, d * 0.54f);
                if (sideLabels) Place(b.Label.rectTransform, d + 1.6f * unit, 0f, widths[i] - d - 1.6f * unit, d);
                else Place(b.Label.rectTransform, 0f, 0f, widths[i], labelH);
                x += widths[i] + gap * unit;
            }
        }

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
