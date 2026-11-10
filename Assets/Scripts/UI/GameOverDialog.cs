using System;
using Lines.Core;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// The game-over pop-up (GDD → Screens; prototype's "over" card): "Game over", the final score and a
    /// name field with Save, nothing else. The field starts with the last typed name or PlayerN; tapping
    /// it selects the whole name, so typing replaces it. Save (or Done on the keyboard) hands the name on.
    /// </summary>
    public sealed class GameOverDialog : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float CardWidth = 78f, CardRadius = 5f, PadTop = 7f, PadSide = 6f, PadBottom = 6f;
        const float TitleSize = 7f, NoteSize = 4.4f, FinalSize = 13f, LabelSize = 3.4f;
        const float FieldHeight = 12f, FieldRadius = 3f, FieldBorder = 0.4f, FieldPad = 3.4f, FieldFont = 4.6f;
        const float RowGap = 2f, LabelGap = 1.6f, SaveWidth = 22f; // "Save" plus 5u padding each side
        public const string Title = "Game over";
        public const string Label = "Your name for the leaderboard";
        const float GlowSize = 8f; // the prototype's soft violet glow around the card
        static readonly Color FieldFill = Palette.Hex(0x1A1433);

        RectTransform root, card, field;
        Image cardImage, cardGlow, fieldBorder, fieldFill;
        TextMeshProUGUI title, note, final, label, placeholder;
        TMP_InputField input;
        PillButton save;
        float unit = 10f;

        /// <summary>Save was pressed; gives what is in the name field (may be empty).</summary>
        public event Action<string> Saved;

        PopupMotion motion;

        public bool IsOpen => motion != null && motion.IsOpen;
        public int Score { get; private set; }
        public string FinalText => final.text;
        public PillButton SaveButton => save;
        public TMP_InputField NameField => input;

        public static GameOverDialog Create(RectTransform canvas)
        {
            var go = new GameObject("Game over", typeof(RectTransform));
            var dialog = go.AddComponent<GameOverDialog>();
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
            dim.raycastTarget = true; // nothing behind can be tapped

            cardGlow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            cardGlow.rectTransform.anchorMin = cardGlow.rectTransform.anchorMax = cardGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cardGlow.color = Palette.CardGlow;
            cardGlow.raycastTarget = false;
            card = NewRect("Card", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Palette.Card;
            cardImage.raycastTarget = false;

            title = NewText("Title", Title, Palette.Text, true, TextAlignmentOptions.Center);
            note = NewText("Note", "Final score", Palette.SoftText, false, TextAlignmentOptions.Center);
            final = NewText("Final", "0", Palette.Text, true, TextAlignmentOptions.Center);
            label = NewText("Label", Label, Palette.SoftText, true, TextAlignmentOptions.Left);
            label.characterSpacing = 3f;
            UiFonts.UseDisplay(title);
            UiFonts.UseDisplay(final);

            // The name field: a rounded white box with a thin border, built inactive so the input field
            // has its text parts before it starts.
            field = NewRect("Name field", card);
            field.anchorMin = field.anchorMax = field.pivot = Vector2.zero;
            field.gameObject.SetActive(false);
            fieldBorder = field.gameObject.AddComponent<Image>();
            fieldBorder.color = Palette.Line;
            fieldFill = NewRect("Fill", field).gameObject.AddComponent<Image>();
            fieldFill.color = FieldFill;
            fieldFill.raycastTarget = false;
            Stretch(fieldFill.rectTransform);
            var area = NewRect("Text area", field);
            Stretch(area);
            area.gameObject.AddComponent<RectMask2D>();
            var text = NewText("Text", "", Palette.Text, true, TextAlignmentOptions.Left, area);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform);
            placeholder = NewText("Placeholder", "Player", Palette.SoftText, true, TextAlignmentOptions.Left, area);
            Stretch(placeholder.rectTransform);
            input = field.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = fieldBorder;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = Leaderboard.MaxNameLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.onFocusSelectAll = true;
            input.caretColor = Palette.Text;
            input.customCaretColor = true;
            input.selectionColor = Palette.Hex(0xFFD56B, 0.35f);
            input.onSubmit.AddListener(_ => Submit());
            field.gameObject.SetActive(true);

            motion = new PopupMotion(gameObject, card);
            save = PillButton.Create(card, "Save", primary: true);
            save.Clicked += Submit;
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
            title.fontSize = TitleSize * u;
            note.fontSize = NoteSize * u;
            final.fontSize = FinalSize * u;
            label.fontSize = LabelSize * u;
            input.textComponent.fontSize = FieldFont * u;
            placeholder.fontSize = FieldFont * u;
            input.pointSize = FieldFont * u;

            float titleH = TitleSize * 1.25f * u, noteH = NoteSize * 1.3f * u, finalH = FinalSize * 1.1f * u;
            float labelH = LabelSize * 1.3f * u, fieldH = FieldHeight * u;
            // Title, 2u, "Final score", the number, 6u, (form pulled up 2u) label, 1.6u, the field row.
            float height = PadTop * u + titleH + 2f * u + noteH + finalH + (6f - 2f) * u + labelH + LabelGap * u + fieldH + PadBottom * u;
            card.sizeDelta = new Vector2(width, height);
            cardGlow.rectTransform.sizeDelta = card.sizeDelta + Vector2.one * 2f * GlowSize * u;
            UiShapes.MakeGlow(cardGlow, GlowSize * u);
            UiShapes.MakeRounded(cardImage, CardRadius * u);

            float y = height - PadTop * u - titleH;
            Place(title.rectTransform, PadSide * u, y, inner, titleH);
            y -= 2f * u + noteH;
            Place(note.rectTransform, PadSide * u, y, inner, noteH);
            y -= finalH;
            Place(final.rectTransform, PadSide * u, y, inner, finalH);
            y -= (6f - 2f) * u + labelH;
            Place(label.rectTransform, PadSide * u, y, inner, labelH);

            // The field takes what Save leaves (Save is as wide as its word plus padding).
            float saveW = SaveWidth * u;
            float fieldW = inner - RowGap * u - saveW;
            Place(field, PadSide * u, PadBottom * u, fieldW, fieldH);
            UiShapes.MakeRounded(fieldBorder, FieldRadius * u);
            float b = FieldBorder * u;
            fieldFill.rectTransform.offsetMin = new Vector2(b, b);
            fieldFill.rectTransform.offsetMax = new Vector2(-b, -b);
            UiShapes.MakeRounded(fieldFill, FieldRadius * u - b);
            var area = input.textViewport;
            area.offsetMin = new Vector2(FieldPad * u, b);
            area.offsetMax = new Vector2(-FieldPad * u, -b);
            save.Place(new Rect(PadSide * u + fieldW + RowGap * u, PadBottom * u, saveW, fieldH), u);
        }

        /// <summary>Opens the pop-up with the final score and the name field filled with <paramref name="defaultName"/>.</summary>
        public void Show(int score, string defaultName)
        {
            Score = score;
            final.text = score.ToString();
            input.text = defaultName ?? "";
            placeholder.text = defaultName ?? "Player";
            transform.SetAsLastSibling();
            LayoutCard();
            motion.Open();
        }

        public void Close()
        {
            input.DeactivateInputField();
            motion.Close();
        }

        /// <summary>Types <paramref name="name"/> into the field (tests).</summary>
        public void SetName(string name) => input.text = name;

        void Submit()
        {
            if (!IsOpen) return;
            Saved?.Invoke(input.text);
        }

        RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI NewText(string name, string text, Color color, bool bold, TextAlignmentOptions align, RectTransform parent = null)
        {
            var tmp = NewRect(name, parent ?? card).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            UiFonts.UseBody(tmp);
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
    }
}
