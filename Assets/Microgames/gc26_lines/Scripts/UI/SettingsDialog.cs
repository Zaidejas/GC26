using System;
using System.Collections.Generic;
using GC26.Lines.Core;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// The Settings pop-up (GDD → Screens → Settings; prototype's #settings card): Music, Sounds and Vibration
    /// switches, the Language (English / Русский) and How to play, with ✕ in the corner. A tap outside the card
    /// also closes it. Every change is reported at once through <see cref="Changed"/>. Sizes in units (1 % of
    /// the content width), as in the prototype.
    /// </summary>
    public sealed class SettingsDialog : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float CardWidth = 78f, CardRadius = 5f, PadTop = 7f, PadSide = 6f, PadBottom = 6f, GlowSize = 8f;
        const float TitleSize = 7f, TitleGap = 4f, RowHeight = 13f, IconColumn = 7f, IconSize = 6f, ColumnGap = 3f;
        const float NameSize = 5.4f, LineWidth = 0.3f, ChoiceHeight = 9f, ChoiceBottom = 3f, RowsToButton = 5f;
        const float ButtonHeight = 12f, CloseSize = 10f, CloseInset = 3f;

        RectTransform root, card;
        Image cardImage, cardGlow;
        TextMeshProUGUI title;
        PillButton close, howTo;
        PopupMotion motion;
        float unit = 10f;
        GameSettings settings = new GameSettings();

        readonly List<Row> rows = new List<Row>();
        Row languageRow;
        LanguageChoice languages;

        /// <summary>A switch or the language changed; carries the new settings (a copy to keep).</summary>
        public event Action<GameSettings> Changed;
        /// <summary>How to play was pressed (the pop-up has already closed).</summary>
        public event Action HowToPlayClicked;

        public bool IsOpen => motion != null && motion.IsOpen;
        public string Title => title.text;
        public ToggleSwitch MusicSwitch => rows[0].Switch;
        public ToggleSwitch SoundsSwitch => rows[1].Switch;
        public ToggleSwitch VibrationSwitch => rows[2].Switch;
        public PillButton HowToPlayButton => howTo;
        public PillButton CloseButton => close;
        public RectTransform Card => card;
        public Language ChosenLanguage => languages.Chosen;

        sealed class Row
        {
            public string Key;
            public Image Icon, Line;
            public TextMeshProUGUI Name;
            public ToggleSwitch Switch; // null for the language row
            public Func<GameSettings, bool> Read;
        }

        public static SettingsDialog Create(RectTransform canvas)
        {
            var go = new GameObject("Settings", typeof(RectTransform));
            var dialog = go.AddComponent<SettingsDialog>();
            dialog.Build(canvas);
            go.SetActive(false);
            return dialog;
        }

        void Build(RectTransform canvas)
        {
            root = (RectTransform)transform;
            root.SetParent(canvas, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var dim = gameObject.AddComponent<Image>();
            dim.color = Palette.Dim;
            dim.raycastTarget = true; // swallows taps on whatever is behind
            var outside = gameObject.AddComponent<Button>(); // a tap outside the card closes it
            outside.targetGraphic = dim;
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(Close);

            cardGlow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            cardGlow.rectTransform.anchorMin = cardGlow.rectTransform.anchorMax = cardGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cardGlow.color = Palette.CardGlow;
            cardGlow.raycastTarget = false;
            card = NewRect("Card", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Palette.Card;
            cardImage.raycastTarget = true; // taps on the card don't count as "outside"

            title = NewText("Title", NameSize, TextAlignmentOptions.Center);
            AddRow("music", SpriteFactory.MusicIcon(), s => s.music, (s, on) => s.music = on);
            AddRow("sounds", SpriteFactory.SoundIcon(), s => s.sounds, (s, on) => s.sounds = on);
            AddRow("vibration", SpriteFactory.VibrationIcon(), s => s.vibration, (s, on) => s.vibration = on);
            languageRow = AddRow("language", SpriteFactory.GlobeIcon(), null, null);
            languages = new LanguageChoice(card);
            languages.ChosenChanged += language =>
            {
                settings.language = Texts.Code(language);
                Changed?.Invoke(settings.Copy());
            };

            motion = new PopupMotion(gameObject, card);
            howTo = PillButton.Create(card, "");
            howTo.Clicked += () => { Close(); HowToPlayClicked?.Invoke(); };
            close = PillButton.CreateIcon(card, SpriteFactory.CloseIcon(), "Close");
            close.MakePlain();
            close.Clicked += Close;
            RefreshTexts();
        }

        Row AddRow(string key, Sprite icon, Func<GameSettings, bool> read, Action<GameSettings, bool> write)
        {
            var row = new Row { Key = key };
            row.Line = NewRect("Line " + key, card).gameObject.AddComponent<Image>();
            row.Line.color = Palette.Line;
            row.Line.raycastTarget = false;
            row.Icon = NewRect("Icon " + key, card).gameObject.AddComponent<Image>();
            row.Icon.sprite = icon;
            row.Icon.color = Palette.SoftText;
            row.Icon.preserveAspect = true;
            row.Icon.raycastTarget = false;
            row.Name = NewText("Name " + key, NameSize, TextAlignmentOptions.Left);
            if (read != null)
            {
                row.Switch = ToggleSwitch.Create(card, key);
                row.Switch.Toggled += on =>
                {
                    write(settings, on);
                    Changed?.Invoke(settings.Copy());
                };
                row.Read = read;
            }
            rows.Add(row);
            return row;
        }

        /// <summary>Lays the card out for a screen where 1 unit is <paramref name="unitPixels"/> pixels.</summary>
        public void ApplyLayout(float unitPixels)
        {
            unit = unitPixels;
            LayoutCard();
        }

        void LayoutCard()
        {
            float u = unit;
            float width = CardWidth * u, inner = width - 2f * PadSide * u;
            // Fixed line heights, so the card keeps its size (and doesn't jump) when the language changes.
            float titleH = TitleSize * 1.25f * u;
            float switchRows = 3f * RowHeight * u;
            float languageH = (RowHeight + ChoiceHeight + ChoiceBottom) * u;
            float height = PadTop * u + titleH + TitleGap * u + switchRows + languageH + RowsToButton * u + ButtonHeight * u + PadBottom * u;
            card.sizeDelta = new Vector2(width, height);
            cardGlow.rectTransform.sizeDelta = card.sizeDelta + Vector2.one * 2f * GlowSize * u;
            UiShapes.MakeGlow(cardGlow, GlowSize * u);
            UiShapes.MakeRounded(cardImage, CardRadius * u);

            title.fontSize = TitleSize * u;
            float y = height - PadTop * u - titleH;
            Place(title.rectTransform, PadSide * u, y, inner, titleH);
            close.Place(new Rect(width - (CloseInset + CloseSize) * u, height - (CloseInset + CloseSize) * u, CloseSize * u, CloseSize * u), u);
            y -= TitleGap * u;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool language = row == languageRow;
                float rowTop = y;
                float lineY = rowTop - RowHeight * u;           // middle of the label line
                float labelMid = rowTop - RowHeight * u / 2f;
                Place(row.Icon.rectTransform, PadSide * u + (IconColumn - IconSize) / 2f * u, labelMid - IconSize / 2f * u, IconSize * u, IconSize * u);
                row.Name.fontSize = NameSize * u;
                float nameX = PadSide * u + (IconColumn + ColumnGap) * u;
                Place(row.Name.rectTransform, nameX, labelMid - NameSize * 0.625f * u, inner - (IconColumn + ColumnGap) * u, NameSize * 1.25f * u);
                if (row.Switch != null)
                    row.Switch.Place(new Rect(PadSide * u + inner - ToggleSwitch.Width * u, labelMid - ToggleSwitch.Height / 2f * u,
                        ToggleSwitch.Width * u, ToggleSwitch.Height * u), u);
                if (language)
                {
                    languages.Layout(new Rect(PadSide * u, lineY - ChoiceHeight * u, inner, ChoiceHeight * u), u);
                    y = lineY - (ChoiceHeight + ChoiceBottom) * u;
                }
                else y = lineY;
                // The thin line under every row but the last.
                row.Line.enabled = i < rows.Count - 1;
                Place(row.Line.rectTransform, PadSide * u, y - LineWidth * u / 2f, inner, LineWidth * u);
            }
            y -= RowsToButton * u;
            howTo.Place(new Rect(PadSide * u, y - ButtonHeight * u, inner, ButtonHeight * u), u);
        }

        /// <summary>Opens the pop-up showing <paramref name="current"/> (kept as a copy).</summary>
        public void Show(GameSettings current)
        {
            settings = (current ?? new GameSettings()).Copy();
            foreach (var row in rows)
                row.Switch?.Set(row.Read(settings), animate: false);
            languages.Set(Texts.Current);
            RefreshTexts();
            transform.SetAsLastSibling();
            LayoutCard();
            motion.Open();
        }

        public void Close() => motion.Close();

        /// <summary>Chooses <paramref name="language"/> as if the player tapped it (tests).</summary>
        public void ChooseLanguage(Language language) => languages.Choose(language);

        /// <summary>Puts every text in the current language.</summary>
        public void RefreshTexts()
        {
            title.text = Texts.Get("settings");
            foreach (var row in rows) row.Name.text = Texts.Get(row.Key);
            howTo.SetText(Texts.Get("howToPlay"));
            languages.Set(Texts.Current);
        }

        RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI NewText(string name, float size, TextAlignmentOptions align)
        {
            var tmp = NewRect(name, card).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = Palette.Text;
            UiFonts.UseDisplay(tmp);
            tmp.alignment = align;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>English | Русский: a pill with two halves; the chosen one is gold (prototype's .langs).</summary>
        sealed class LanguageChoice
        {
            const float Border = 0.4f, Inset = 0.6f, TextSize = 3.6f;
            readonly Image outline, inside;
            readonly List<(Language language, Image fill, TextMeshProUGUI label, Button button)> options =
                new List<(Language, Image, TextMeshProUGUI, Button)>();

            public Language Chosen { get; private set; } = Language.English;
            public event Action<Language> ChosenChanged;

            public LanguageChoice(RectTransform card)
            {
                outline = NewImage("Languages", card);
                outline.color = Palette.Line;
                inside = NewImage("Languages inside", card); // the card colour inside: leaves a thin outline
                inside.color = Palette.Card;
                AddOption(card, Language.English, "English");
                AddOption(card, Language.Russian, "Русский");
            }

            void AddOption(RectTransform card, Language language, string name)
            {
                var fill = NewImage("Language " + name, card);
                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(fill.rectTransform, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.text = name; // each language in its own words
                label.fontStyle = FontStyles.Bold;
                UiFonts.UseBody(label);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                var button = fill.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Choose(language));
                fill.raycastTarget = true;
                options.Add((language, fill, label, button));
            }

            public void Choose(Language language)
            {
                if (language == Chosen) return;
                Set(language);
                ChosenChanged?.Invoke(language);
            }

            public void Set(Language language)
            {
                Chosen = language;
                foreach (var (lang, fill, label, _) in options)
                {
                    bool on = lang == language;
                    fill.color = on ? Palette.Gold : Color.clear;
                    label.color = on ? Palette.GoldText : Palette.SoftText;
                }
            }

            public void Layout(Rect pixels, float u)
            {
                Place(outline.rectTransform, pixels.x, pixels.y, pixels.width, pixels.height);
                UiShapes.MakePill(outline, pixels.height);
                float b = Border * u;
                Place(inside.rectTransform, pixels.x + b, pixels.y + b, pixels.width - 2f * b, pixels.height - 2f * b);
                UiShapes.MakePill(inside, pixels.height - 2f * b);
                float inset = (Border + Inset) * u, gap = Inset * u;
                float half = (pixels.width - 2f * inset - gap) / 2f, h = pixels.height - 2f * inset;
                for (int i = 0; i < options.Count; i++)
                {
                    var (_, fill, label, _) = options[i];
                    Place(fill.rectTransform, pixels.x + inset + i * (half + gap), pixels.y + inset, half, h);
                    UiShapes.MakePill(fill, h);
                    label.fontSize = TextSize * u;
                }
            }

            static Image NewImage(string name, RectTransform parent)
            {
                var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                var image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                return image;
            }
        }
    }
}
