using System.Collections.Generic;
using GC26.Lines.Core;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
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
        static readonly Color GainColor = Palette.Gain;

        Canvas canvas;
        RectTransform area;
        TextMeshProUGUI scoreLabel, scoreText, gainText, nextLabel;
        readonly Image[] next = new Image[GameConfig.BallsPerTurn];
        float unit = 10f;
        TweenHandle gainTween;
        AlertToast alert;
        PillButton undo, reset;
        ConfirmDialog dialog;
        GameOverDialog gameOver;

        /// <summary>The player pressed Reset.</summary>
        public event System.Action ResetClicked;
        public PillButton ResetButton => reset;
        /// <summary>The player pressed the house button (top-left): back to the main menu.</summary>
        public event System.Action BackClicked;
        public PillButton BackButton => back;
        PillButton back, settingsButton;
        SettingsDialog settings;
        /// <summary>The player pressed the gear (top-right): opens Settings.</summary>
        public event System.Action SettingsClicked;
        public PillButton SettingsButton => settingsButton;
        /// <summary>The Settings pop-up (opened from the game screen or the main menu).</summary>
        public SettingsDialog Settings => settings;
        GameObject popups; // own canvas on top, so dialogs also work while the HUD is hidden (main menu)
        /// <summary>The "Start a new game?" question (and later pop-ups).</summary>
        public ConfirmDialog Dialog => dialog;
        public GameOverDialog GameOver => gameOver;

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
            scoreLabel = NewText("Score label", "", Palette.SoftText, TextAlignmentOptions.BottomLeft, bold: true);
            scoreText = NewText("Score", "0", Palette.Text, TextAlignmentOptions.BottomLeft, bold: true);
            gainText = NewText("Gain", "", GainColor, TextAlignmentOptions.BottomLeft, bold: true);
            gainText.alpha = 0f;
            UiFonts.UseDisplay(scoreText);
            UiFonts.UseDisplay(gainText);
            nextLabel = NewText("Next label", "", Palette.SoftText, TextAlignmentOptions.BottomRight, bold: true);
            alert = AlertToast.Create((RectTransform)transform);
            undo = PillButton.Create((RectTransform)transform, "");
            undo.SetLeadingIcon(SpriteFactory.UndoIcon());
            undo.Interactable = false; // nothing to undo at the start
            undo.Clicked += () => UndoClicked?.Invoke();
            reset = PillButton.Create((RectTransform)transform, "");
            reset.SetLeadingIcon(SpriteFactory.ResetIcon());
            reset.Clicked += () => ResetClicked?.Invoke();
            back = PillButton.CreateIcon((RectTransform)transform, SpriteFactory.HomeIcon(), "Home");
            back.MakePlain(); // just the house, no ring or disc (teacher's feedback, prototype)
            back.Clicked += () => BackClicked?.Invoke();
            settingsButton = PillButton.CreateIcon((RectTransform)transform, SpriteFactory.GearIcon(), "Settings");
            settingsButton.MakePlain(); // just the white gear, opposite the house
            settingsButton.Clicked += () => SettingsClicked?.Invoke();

            popups = new GameObject("Popups");
            var popupCanvas = popups.AddComponent<Canvas>();
            popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            popupCanvas.worldCamera = camera;
            popupCanvas.planeDistance = 4f;
            popupCanvas.sortingOrder = 30;
            popups.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            popups.AddComponent<GraphicRaycaster>();
            gameOver = GameOverDialog.Create((RectTransform)popups.transform);
            dialog = ConfirmDialog.Create((RectTransform)popups.transform);
            settings = SettingsDialog.Create((RectTransform)popups.transform);
            for (int i = 0; i < next.Length; i++)
            {
                next[i] = NewRect("Next " + i, area).gameObject.AddComponent<Image>();
                next[i].preserveAspect = true;
                next[i].raycastTarget = false;
            }
            RefreshTexts();
        }

        /// <summary>Puts the HUD's texts and its pop-ups' texts in the current language (<see cref="Texts"/>).</summary>
        public void RefreshTexts()
        {
            scoreLabel.text = Texts.Get("score");
            nextLabel.text = Texts.Get("next");
            undo.SetText(Texts.Get("undo"));
            reset.SetText(Texts.Get("reset"));
            dialog.RefreshTexts();
            gameOver.RefreshTexts();
            settings.RefreshTexts();
            if (unit > 0f && area != null && area.sizeDelta.x > 0f) AlignScore();
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
            AlignScore();
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
            AlignScore(); // puts NEXT on the same line as SCORE

            alert.ApplyLayout(layout.Board.center, u); // messages appear over the middle of the board
            back.Place(layout.BackButton, u);
            settingsButton.Place(layout.SettingsButton, u);

            // Buttons under the board: Undo on the left half, Reset on the right, 4 units apart.
            var actions = layout.Actions;
            float half = (actions.width - 4f * u) / 2f;
            undo.Place(new Rect(actions.x, actions.y, half, actions.height), u);
            reset.Place(new Rect(actions.x + half + 4f * u, actions.y, half, actions.height), u);
            dialog.ApplyLayout(u);
            gameOver.ApplyLayout(u);
            settings.ApplyLayout(u);
        }

        /// <summary>Shows or hides the game's HUD (score, next, buttons). Dialogs are not affected.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        void OnDestroy()
        {
            if (popups == null) return;
            if (Application.isPlaying) Destroy(popups);
            else DestroyImmediate(popups);
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
            AlignScore();
            PlaceGain();
        }

        // The score number stands on the same line as the 3 Next mushrooms: the middle of its digits (their ink,
        // not the text box, which has empty room above and below the digits) is level with the middle of the
        // mushrooms' pictures, whatever the score and the screen.
        void AlignScore()
        {
            if (unit <= 0f) return;
            var rt = scoreText.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0f);
            scoreText.ForceMeshUpdate();
            if (!DigitInk(out float inkBottom, out float inkTop)) return;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, PieceSize * unit / 2f - (inkBottom + inkTop) / 2f);
            // The SCORE label stays right above the number, and NEXT right above the pieces; both share one line
            // (the higher of the two), so neither comes closer to what is under it.
            float labelsY = Mathf.Max(rt.anchoredPosition.y + ScoreSize * 1.1f * unit, (PieceSize + 1.4f) * unit);
            var label = scoreLabel.rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x, labelsY);
            var nextRect = nextLabel.rectTransform;
            nextRect.anchoredPosition = new Vector2(nextRect.anchoredPosition.x, labelsY);
        }

        // The top and bottom of the drawn digits, in the score text's own pixels (pivot at its bottom-left).
        bool DigitInk(out float bottom, out float top)
        {
            bottom = float.MaxValue; top = float.MinValue;
            var info = scoreText.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                bottom = Mathf.Min(bottom, c.bottomLeft.y);
                top = Mathf.Max(top, c.topRight.y);
            }
            return top > bottom;
        }

        /// <summary>The middle of the score's digits and of the Next mushrooms, in the HUD's pixels (tests).</summary>
        public float ScoreMiddleY => DigitInk(out float bottom, out float top) ? scoreText.rectTransform.anchoredPosition.y + (bottom + top) / 2f : 0f;
        public float NextMiddleY => next[0].rectTransform.anchoredPosition.y + next[0].rectTransform.sizeDelta.y / 2f;

        /// <summary>Where the SCORE and NEXT labels sit (their bottom edge, in the HUD's pixels; tests).</summary>
        public float ScoreLabelY => scoreLabel.rectTransform.anchoredPosition.y;
        public float NextLabelY => nextLabel.rectTransform.anchoredPosition.y;

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
            UiFonts.UseBody(tmp);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (name.EndsWith("label")) { UiFonts.UseDisplay(tmp); tmp.characterSpacing = 6f; } // letter-spaced Mystery Quest labels
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
