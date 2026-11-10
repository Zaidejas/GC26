using System;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// A question over a dimmed screen, like the prototype's pop-up: white rounded card, title, a short
    /// line of text and two buttons (Cancel and a dark main button). While it's open, nothing behind it
    /// can be tapped. Sizes in units (1 % of the content width).
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float CardWidth = 78f, CardRadius = 5f, PadTop = 7f, PadSide = 6f, PadBottom = 6f;
        const float TitleSize = 7f, TextSize = 4.4f, ButtonHeight = 12f, ButtonGap = 3f;
        const float GlowSize = 8f; // the prototype's soft violet glow around the card

        RectTransform root, card;
        Image cardImage, cardGlow;
        TextMeshProUGUI title, message;
        PillButton cancel, confirm;
        Action onConfirm;

        PopupMotion motion;

        public bool IsOpen => motion != null && motion.IsOpen;
        public string Title => title.text;
        public string Message => message.text;
        public PillButton CancelButton => cancel;
        public PillButton ConfirmButton => confirm;

        public static ConfirmDialog Create(RectTransform canvas)
        {
            var go = new GameObject("Dialog", typeof(RectTransform));
            var dialog = go.AddComponent<ConfirmDialog>();
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
            root.SetAsLastSibling(); // on top of everything else on the canvas

            var dim = gameObject.AddComponent<Image>();
            dim.color = Palette.Dim;
            dim.raycastTarget = true; // swallows taps on whatever is behind

            cardGlow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            cardGlow.rectTransform.anchorMin = cardGlow.rectTransform.anchorMax = cardGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cardGlow.color = Palette.CardGlow;
            cardGlow.raycastTarget = false;
            card = NewRect("Card", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Palette.Card;

            title = NewText("Title", Palette.Text, true);
            message = NewText("Message", Palette.SoftText, false);
            motion = new PopupMotion(gameObject, card);
            cancel = PillButton.Create(card, "Cancel");
            confirm = PillButton.Create(card, "Yes", primary: true);
            cancel.Clicked += Close;
            confirm.Clicked += () =>
            {
                var action = onConfirm;
                Close();
                action?.Invoke();
            };
        }

        float unit = 10f;

        /// <summary>Lays the card out for a screen where 1 unit is <paramref name="unitPixels"/> pixels.</summary>
        public void ApplyLayout(float unitPixels)
        {
            unit = unitPixels;
            LayoutCard();
        }

        /// <summary>Sizes the card to its text: the title and message may wrap onto more lines.</summary>
        void LayoutCard()
        {
            float u = unit;
            float width = CardWidth * u;
            float inner = width - 2f * PadSide * u;
            title.fontSize = TitleSize * u;
            message.fontSize = TextSize * u;
            float titleH = title.GetPreferredValues(title.text, inner, 0f).y;
            float messageH = string.IsNullOrEmpty(message.text) ? 0f : message.GetPreferredValues(message.text, inner, 0f).y;
            float buttonsH = ButtonHeight * u;
            float height = PadTop * u + titleH + 2f * u + messageH + 6f * u + buttonsH + PadBottom * u;
            card.sizeDelta = new Vector2(width, height);
            cardGlow.rectTransform.sizeDelta = card.sizeDelta + Vector2.one * 2f * GlowSize * u;
            UiShapes.MakeGlow(cardGlow, GlowSize * u);
            cardImage.sprite = SpriteFactory.RoundRect(0.5f);
            cardImage.type = Image.Type.Sliced;
            var canvas = GetComponentInParent<Canvas>();
            float canvasPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            cardImage.pixelsPerUnitMultiplier = cardImage.sprite.border.x * canvasPpu / (cardImage.sprite.pixelsPerUnit * CardRadius * u);

            // Top to bottom inside the card (positions from the card's bottom-left corner).
            float y = height - PadTop * u - titleH;
            Place(title.rectTransform, PadSide * u, y, inner, titleH);
            y -= 2f * u + messageH;
            Place(message.rectTransform, PadSide * u, y, inner, messageH);

            float half = (inner - ButtonGap * u) / 2f;
            cancel.Place(new Rect(PadSide * u, PadBottom * u, half, buttonsH), u);
            confirm.Place(new Rect(PadSide * u + half + ButtonGap * u, PadBottom * u, half, buttonsH), u);
        }

        /// <summary>Opens the dialog; <paramref name="onYes"/> runs if the player presses the main button.</summary>
        public void Show(string titleText, string messageText, string confirmText, Action onYes)
        {
            title.text = titleText;
            message.text = messageText;
            confirm.SetText(confirmText);
            onConfirm = onYes;
            LayoutCard();
            root.SetAsLastSibling();
            motion.Open();
        }

        public void Close()
        {
            onConfirm = null;
            motion.Close();
        }

        RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        TextMeshProUGUI NewText(string name, Color color, bool bold)
        {
            var rt = NewRect(name, card);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            if (bold) UiFonts.UseDisplay(tmp); else UiFonts.UseBody(tmp); // the title is Mystery Quest
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
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
