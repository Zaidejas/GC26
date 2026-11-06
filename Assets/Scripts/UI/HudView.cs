using System.Collections.Generic;
using Lines.Core;
using Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lines.UI
{
    /// <summary>
    /// The strip above the board: SCORE on the left (with a "+N" that floats up after a clear) and
    /// NEXT on the right (the 3 pieces that arrive after the next move). Built from code with uGUI and
    /// TextMeshPro; sizes follow the prototype, in units of 1% of the content width.
    /// The canvas is drawn by the game camera, so it also appears in layout screenshots.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float LabelSize = 3.2f, ScoreSize = 10f, GainSize = 5f, PieceSize = 8.5f, PieceGap = 1.6f, Inset = 1f;
        static readonly Color GainColor = Palette.Hex(0x2F8F4E);

        Canvas canvas;
        RectTransform area;
        TextMeshProUGUI scoreLabel, scoreText, gainText, nextLabel;
        readonly Image[] next = new Image[GameConfig.BallsPerTurn];
        float unit = 10f;
        TweenHandle gainTween;
        AlertToast alert;
        PillButton undo;

        /// <summary>The player pressed Undo.</summary>
        public event System.Action UndoClicked;
        public PillButton UndoButton => undo;

        public string ScoreText => scoreText.text;
        public IReadOnlyList<Image> NextPieces => next;
        public int Score { get; private set; }

        public static HudView Create(Camera camera)
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<HudView>();
            hud.Build(camera);
            return hud;
        }

        void Build(Camera camera)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 20; // in front of the board
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // we size everything in pixels ourselves
            gameObject.AddComponent<GraphicRaycaster>();

            area = NewRect("Area", (RectTransform)transform);
            scoreLabel = NewText("Score label", "SCORE", Palette.SoftText, TextAlignmentOptions.BottomLeft, bold: true);
            scoreText = NewText("Score", "0", Palette.Text, TextAlignmentOptions.BottomLeft, bold: true);
            gainText = NewText("Gain", "", GainColor, TextAlignmentOptions.BottomLeft, bold: true);
            gainText.alpha = 0f;
            nextLabel = NewText("Next label", "NEXT", Palette.SoftText, TextAlignmentOptions.BottomRight, bold: true);
            alert = AlertToast.Create((RectTransform)transform);
            undo = PillButton.Create((RectTransform)transform, "Undo");
            undo.Interactable = false; // nothing to undo at the start
            undo.Clicked += () => UndoClicked?.Invoke();
            for (int i = 0; i < next.Length; i++)
            {
                next[i] = NewRect("Next " + i, area).gameObject.AddComponent<Image>();
                next[i].preserveAspect = true;
                next[i].raycastTarget = false;
            }
        }

        /// <summary>Places the HUD in <see cref="BoardLayout.Hud"/> (screen pixels, origin bottom-left).</summary>
        public void ApplyLayout(BoardLayout layout)
        {
            unit = layout.Unit;
            var rect = layout.Hud;
            Place(area, rect.x, rect.y, rect.width, rect.height);

            float u = unit;
            Place(scoreText.rectTransform, Inset * u, 0, 50 * u, ScoreSize * 1.1f * u);
            scoreText.fontSize = ScoreSize * u;
            Place(scoreLabel.rectTransform, Inset * u, ScoreSize * 1.1f * u, 40 * u, LabelSize * 1.4f * u);
            scoreLabel.fontSize = LabelSize * u;
            gainText.fontSize = GainSize * u;
            PlaceGain();

            float right = rect.width - Inset * u;
            for (int i = 0; i < next.Length; i++)
            {
                float x = right - (next.Length - i) * PieceSize * u - (next.Length - 1 - i) * PieceGap * u;
                Place(next[i].rectTransform, x, 0, PieceSize * u, PieceSize * u);
            }
            Place(nextLabel.rectTransform, right - 40 * u, (PieceSize + 1.4f) * u, 40 * u, LabelSize * 1.4f * u);
            nextLabel.fontSize = LabelSize * u;

            alert.ApplyLayout(layout.Board.center, u); // messages appear over the middle of the board

            // Buttons under the board: Undo on the left half (Reset joins on the right), 4 units apart.
            var actions = layout.Actions;
            float half = (actions.width - 4f * u) / 2f;
            undo.Place(new Rect(actions.x, actions.y, half, actions.height), u);
        }

        /// <summary>Enables or greys out the Undo button.</summary>
        public void SetUndoEnabled(bool enabled) => undo.Interactable = enabled;

        /// <summary>Shows a short message over the board ("No path").</summary>
        public void ShowAlert(string text) => alert.Show(text);

        public AlertToast Alert => alert;

        /// <summary>Shows <paramref name="score"/> without the "+N" effect.</summary>
        public void SetScore(int score)
        {
            Score = score;
            scoreText.text = score.ToString();
            PlaceGain();
        }

        /// <summary>Adds <paramref name="points"/> and floats "+points" up next to the score (prototype: 0.9 s).</summary>
        public void AddScore(int points)
        {
            SetScore(Score + points);
            gainText.text = "+" + points;
            gainTween?.Kill();
            var start = gainText.rectTransform.anchoredPosition;
            float rise = 6f * unit;
            gainTween = Tween.To(gainText, 0.9f, p =>
            {
                gainText.alpha = p < 0.2f ? p / 0.2f : 1f - (p - 0.2f) / 0.8f;
                gainText.rectTransform.anchoredPosition = start + new Vector2(0, -2f * unit + rise * p);
            }).OnComplete(() => gainText.rectTransform.anchoredPosition = start);
        }

        /// <summary>Shows the colors of the next pieces, drawn with <paramref name="pieces"/>.</summary>
        public void SetPreview(IReadOnlyList<BallColor> colors, PieceSet pieces)
        {
            for (int i = 0; i < next.Length; i++)
            {
                bool show = i < colors.Count;
                next[i].enabled = show;
                if (show) next[i].sprite = pieces.SpriteFor(colors[i]);
            }
        }

        void PlaceGain()
        {
            if (gainText == null) return;
            float x = Inset * unit + scoreText.GetPreferredValues(scoreText.text).x + 2f * unit;
            Place(gainText.rectTransform, x, 1.5f * unit, 20 * unit, GainSize * 1.3f * unit);
        }

        // ---- building blocks ----

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // measured from the bottom-left corner
            return rt;
        }

        TextMeshProUGUI NewText(string name, string text, Color color, TextAlignmentOptions alignment, bool bold)
        {
            var tmp = NewRect(name, area).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (name.EndsWith("label")) tmp.characterSpacing = 12f; // the prototype's letter-spaced labels
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
